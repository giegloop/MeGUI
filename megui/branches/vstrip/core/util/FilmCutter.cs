using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Globalization;
using System.Xml.Serialization;
using System.Diagnostics;

namespace MeGUI.core.util
{
    internal class OverlappingSectionException : Exception
    {
        public OverlappingSectionException() : base("Sections overlap") { }
    }

    public class CutSection : IComparable<CutSection>
    {
        public int startFrame;
        public int endFrame;

        #region IComparable<CutSection> Members

        public int CompareTo(CutSection other)
        {
            if (other == this) return 0;
            if (other.startFrame == this.startFrame)
                throw new OverlappingSectionException();

            if (other.startFrame < this.startFrame)
            {
                if (other.endFrame < this.startFrame)
                    return 1;
                throw new OverlappingSectionException();
            }

            return 0 - other.CompareTo(this);

        }

        #endregion
    }
    public enum TransitionStyle
    {
        [EnumTitle("Fade (recommended)")]
        FADE,
        [EnumTitle("No transition")]
        NO_TRANSITION,
        [EnumTitle("Dissolve")]
        DISSOLVE
    }

    [XmlInclude(typeof(CutSection))]
    public class Cuts
    {
        public void AdaptToFramerate(double newFramerate)
        {
            double ratio = newFramerate / Framerate;
            foreach (CutSection c in cuts)
            {
                c.startFrame = (int) ((double)c.startFrame * ratio);
                c.endFrame = (int) ((double)c.endFrame * ratio);
            }
            Framerate = newFramerate;
        }

        public int MinLength
        {
            get
            {
                if (cuts.Count == 0) throw new Exception("Must have at least one cut");
                return cuts[cuts.Count - 1].endFrame;
            }
        }

        public Cuts() { }

        private List<CutSection> cuts = new List<CutSection>();
        public double Framerate = -1;
        public TransitionStyle Style = TransitionStyle.FADE;

        public List<CutSection> AllCuts
        {
            get { return cuts; }
            set { cuts = value; }
        }

        public Cuts(TransitionStyle style)
        {
            this.Style = style;
        }

        public bool addSection(CutSection cut)
        {
            List<CutSection> old = new List<CutSection>(cuts);
            cuts.Add(cut);
            try
            {
                try
                {
                    cuts.Sort();
                }
                catch (InvalidOperationException e)
                { throw e.InnerException; }
            }
            catch (OverlappingSectionException) { cuts = old; return false; }

            return true;
        }

        public void Clear()
        {
            cuts.Clear();
        }

        public ulong TotalFrames
        {
            get
            {
                ulong ans = 0;
                foreach (CutSection c in AllCuts)
                    ans += (ulong)(c.endFrame - c.startFrame);
                return ans;
            }
        }

        public Cuts clone()
        {
            Cuts copy = new Cuts(Style);
            copy.cuts = new List<CutSection>(cuts);
            copy.Framerate = this.Framerate;
            return copy;
        }

        public void remove(CutSection cutSection)
        {
            cuts.Remove(cutSection);
        }
    }

    public class FilmCutter
    {
        public static void WriteCutsToFile(string filename, Cuts cuts)
        {
            Debug.Assert(cuts.AllCuts.Count > 0);
            using (Stream s = File.Open(filename, FileMode.Create))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(Cuts));
                serializer.Serialize(s, cuts);
            }
        }

        public static Cuts ReadCutsFromFile(string filename)
        {
            using (Stream s = File.OpenRead(filename))
            {
                XmlSerializer deserializer = new XmlSerializer(typeof(Cuts));
                return (Cuts)deserializer.Deserialize(s);
            }
        }

        public static string GetCutsScript(Cuts cuts, bool isAudio)
        {
            using (StringWriter s = new StringWriter())
            {
                s.WriteLine();
                s.WriteLine("__film = last");

                if (isAudio) // We need to generate a fake video track to add
                {
                    s.WriteLine("__just_audio = __film");
                    s.WriteLine("__blank = BlankClip(length={0}, fps={1})", cuts.MinLength, cuts.Framerate);
                    s.WriteLine("__film = AudioDub(__blank, __film)");
                }

                int counter = 0;
                foreach (CutSection c in cuts.AllCuts)
                {
                    s.WriteLine("__t{0} = __film.trim({1}, {2})", counter, c.startFrame, c.endFrame);
                    counter++;
                }

                if (cuts.Style == TransitionStyle.NO_TRANSITION)
                {
                    for (int i = 0; i < counter; i++)
                    {
                        s.Write("__t{0} ", i);
                        if (i < counter - 1) s.Write("++ ");
                    }
                    s.WriteLine();
                }
                else if (cuts.Style == TransitionStyle.FADE)
                {
                    for (int i = 0; i < counter; i++)
                    {
                        bool first = (i == 0);
                        bool last = (i == (counter - 1));
                        s.Write(addFades("__t" + i, first, last, isAudio, cuts.Framerate));
                        if (!last) s.Write(" ++ ");
                    }
                    s.WriteLine();
                }
                else if (cuts.Style == TransitionStyle.DISSOLVE && counter != 0)
                {
                    string scriptlet = "__t" + (counter - 1);
                    for (int i = counter - 2; i >= 0; i--)
                    {
                        scriptlet = string.Format("__t{0}.Dissolve({1}, 60)", i, scriptlet);
                    }
                    s.WriteLine(scriptlet);
                }

                if (isAudio) // We now need to remove the dummy audio track
                {
                    // It will try to take the video from __just_audio, but there isn't any, so it just takes 
                    // the audio stream from last
                    s.WriteLine("AudioDubEx(__just_audio, last)");
                }
                return s.ToString();
            }
        }

        public static void WriteCutsToScript(string script, Cuts cuts, bool isAudio)
        {
            using (TextWriter s = new StreamWriter(File.Open(script, FileMode.Append)))
            {
                s.WriteLine(GetCutsScript(cuts, isAudio));
            }
        }


        private static string addFades(string p, bool first, bool last, bool isAudio, double framerate)
        {
            string number = ((int)framerate).ToString();
            if (first && last) return p;
            if (isAudio)
            {
                if (!first && !last) return string.Format("FadeIO(FadeIO0({0}, {1}), {1})", p, number);
                if (first) return string.Format("FadeOut(FadeOut0({0}, {1}), {1})", p, number);
                if (last) return string.Format("FadeIn(FadeIn0({0}, {1}), {1})", p, number);
            }
            else
            {
                if (!first && !last) return "FadeIO(" + p + ", " + number + ")";
                if (first) return "FadeOut(" + p + ", " + number + ")";
                if (last) return "FadeIn(" + p + ", " + number + ")";
            }
            Debug.Assert(false);
            return null;
        }
    }
}
