using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using MeGUI.core.plugins.interfaces;
using MeGUI.core.util;
using System.IO;
using MeGUI.core.details;

namespace MeGUI.packages.tools.besplitter
{
    public partial class Besplitter : Form
    {
        MainForm info;
        OutputFileType[] filters;

        public Besplitter(MainForm info)
        {
            this.info = info;
            InitializeComponent();

            filters = new OutputFileType[] {
                    AudioType.AC3,
                    AudioType.MP2,
                    AudioType.MP3,
                    AudioType.RAWAAC,
                    AudioType.WAV,
                    AudioType.PCM};
            
            input.Filter = VideoUtil.GenerateCombinedFilter(filters);
            output.Filter = input.Filter;
        }

        private void goButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(input.Filename) || string.IsNullOrEmpty(output.Filename) || string.IsNullOrEmpty(cuts.Filename))
            {
                MessageBox.Show("Can't create job: input not configured.", "Can't create job", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!output.Filename.ToLower().EndsWith(Path.GetExtension(input.Filename).ToLower()))
            {
                MessageBox.Show("Can't create job: input and output have different types.", "Can't create job", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Cuts c = null;
            try
            {
                c = FilmCutter.ReadCutsFromFile(cuts.Filename);
            }
            catch (Exception)
            {
                MessageBox.Show("Error reading cutlist. Is it the correct format?", "Error reading cutlist", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string prefix = getAcceptableFilename(output.Filename, c.AllCuts.Count*2);
            string[] tempfiles = generateNumberedFilenames(prefix, Path.GetExtension(output.Filename), c.AllCuts.Count * 2);
            
            AudioSplitJob split = new AudioSplitJob(input.Filename, prefix, c);
            for (int i = 1; i < tempfiles.Length; i += 2)
                split.FilesToDelete.Add(tempfiles[i]);

            int length = tempfiles.Length / 2;
            if (tempfiles.Length % 2 != 0)
                length++;
            string[] evens = new string[length];
            for (int i = 0; i < evens.Length; i++)
                evens[i] = tempfiles[2 * i];

            AudioJoinJob join = new AudioJoinJob(evens, output.Filename);
            join.FilesToDelete.AddRange(evens);
            // generate the join commandline later

            join.ClipLength = TimeSpan.FromSeconds((double)c.TotalFrames / c.Framerate);

            info.Jobs.addJobsWithDependencies(new SequentialChain(split, join));
            this.Dispose();
        }

        private static string[] generateNumberedFilenames(string prefix, string ext, int num)
        {
            string[] ans = new string[num];
            for (int i = 1; i <= num; i++)
            {
                ans[i-1] = prefix + i.ToString("00") + ext;
            }
            return ans;
        }

        private static string getAcceptableFilename(string p, int p_2)
        {
            string ext = Path.GetExtension(p);
            string name = Path.Combine(Path.GetDirectoryName(p), Path.GetFileNameWithoutExtension(p));
            int suffix = 0;
            while (true)
            {
                string test = name + "_" + suffix + "_";
                bool failed = false;
                foreach (string s in generateNumberedFilenames(test, ext, p_2))
                    if (File.Exists(s))
                    {
                        failed = true;
                        break;
                    }

                if (!failed)
                    return test;
                suffix++;
            }
        }

        private void input_FileSelected(FileBar sender, FileBarEventArgs args)
        {
            foreach (OutputFileType type in filters)
            {
                if (sender.Filename.ToLower().EndsWith(type.Extension))
                {
                    output.Filter = type.OutputFilterString;
                    break;
                }
            }
        }


    }

    public class BesplitterTool : ITool
    {

        #region ITool Members

        public string Name
        {
            get { return "Audio Cutter"; }
        }

        public void Run(MainForm info)
        {
            (new Besplitter(info)).Show();
        }

        public Shortcut[] Shortcuts
        {
            get { return new Shortcut[] { Shortcut.CtrlK }; }
        }

        #endregion

        #region IIDable Members

        public string ID
        {
            get { return Name; }
        }

        #endregion
    }
}