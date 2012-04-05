﻿// ****************************************************************************
//
// Copyright (C) 2005-2012 Doom9 & al
// 
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
// 
// ****************************************************************************

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

using MeGUI.core.util;

namespace MeGUI
{
    class MkvInfo
    {
        private bool _bHasChapters;
        private String _strResult, _strFile;
        private List<MkvInfoTrack> _oTracks = new List<MkvInfoTrack>();
        private LogItem _oLog;

        public MkvInfo(String strFile, ref LogItem oLog)
        {
            this._oLog = oLog;
            this._strFile = strFile;
            getInfo();
        }

        public bool HasChapters
        {
            get { return _bHasChapters; }
        }

        public List<MkvInfoTrack> Track
        {
            get { return _oTracks; }
        }

        private void getInfo()
        {
            _strResult = null;
            using (Process mkvinfo = new Process())
            {
                mkvinfo.StartInfo.FileName = MainForm.Instance.Settings.MkvmergePath;
                mkvinfo.StartInfo.Arguments = string.Format("--ui-language en --identify-verbose \"{0}\"", _strFile);
                mkvinfo.StartInfo.CreateNoWindow = true;
                mkvinfo.StartInfo.UseShellExecute = false;
                mkvinfo.StartInfo.RedirectStandardOutput = true;
                mkvinfo.StartInfo.RedirectStandardError = true;
                mkvinfo.StartInfo.ErrorDialog = false;
                mkvinfo.EnableRaisingEvents = true;
                mkvinfo.ErrorDataReceived += new DataReceivedEventHandler(backgroundWorker_ErrorDataReceived);
                mkvinfo.OutputDataReceived += new DataReceivedEventHandler(backgroundWorker_OutputDataReceived);
                try
                {
                    mkvinfo.Start();
                    mkvinfo.BeginErrorReadLine();
                    mkvinfo.BeginOutputReadLine();
                    while (!mkvinfo.HasExited) // wait until the process has terminated without locking the GUI
                    {
                        System.Windows.Forms.Application.DoEvents();
                        System.Threading.Thread.Sleep(100);
                    }
                    mkvinfo.WaitForExit();

                    if (mkvinfo.ExitCode != 0)
                        _oLog.LogValue("MkvInfo", _strResult, ImageType.Error);
                    else
                        _oLog.LogValue("MkvInfo", _strResult);
                    parseResult();
                }
                catch (Exception ex)
                {
                    _oLog.LogValue("MkvInfo - Unhandled Error", ex, ImageType.Error);
                }
                finally
                {
                    mkvinfo.ErrorDataReceived -= new DataReceivedEventHandler(backgroundWorker_ErrorDataReceived);
                    mkvinfo.OutputDataReceived -= new DataReceivedEventHandler(backgroundWorker_OutputDataReceived);
                }
            } 
        }

        public bool extractChapters(String strChapterFile)
        {
            _strResult = null;
            bool bResult = false;
            using (Process mkvinfo = new Process())
            {
                mkvinfo.StartInfo.FileName = MainForm.Instance.Settings.MkvExtractPath;
                mkvinfo.StartInfo.Arguments = string.Format("chapters \"{0}\" --ui-language en --simple", _strFile);
                mkvinfo.StartInfo.CreateNoWindow = true;
                mkvinfo.StartInfo.UseShellExecute = false;
                mkvinfo.StartInfo.RedirectStandardOutput = true;
                mkvinfo.StartInfo.RedirectStandardError = true;
                mkvinfo.StartInfo.ErrorDialog = false;
                mkvinfo.EnableRaisingEvents = true;
                mkvinfo.ErrorDataReceived += new DataReceivedEventHandler(backgroundWorker_ErrorDataReceived);
                mkvinfo.OutputDataReceived += new DataReceivedEventHandler(backgroundWorker_OutputDataReceived);
                try
                {
                    mkvinfo.Start();
                    mkvinfo.BeginErrorReadLine();
                    mkvinfo.BeginOutputReadLine();
                    while (!mkvinfo.HasExited) // wait until the process has terminated without locking the GUI
                    {
                        System.Windows.Forms.Application.DoEvents();
                        System.Threading.Thread.Sleep(100);
                    }
                    mkvinfo.WaitForExit();

                    if (mkvinfo.ExitCode != 0)
                        _oLog.LogValue("MkvExtract", _strResult, ImageType.Error);
                    else
                    {
                        _oLog.LogValue("MkvExtract", _strResult);
                        try
                        {
                            StreamWriter sr = new StreamWriter(strChapterFile, false);
                            sr.Write(_strResult);
                            sr.Close();
                            bResult = true;
                        }
                        catch (Exception e)
                        {
                            _oLog.LogValue("MkvExtract - Unhandled Error", e, ImageType.Error);
                        }
                    }
                    parseResult();
                }
                catch (Exception ex)
                {
                    _oLog.LogValue("MkvExtract - Unhandled Error", ex, ImageType.Error);
                }
                finally
                {
                    mkvinfo.ErrorDataReceived -= new DataReceivedEventHandler(backgroundWorker_ErrorDataReceived);
                    mkvinfo.OutputDataReceived -= new DataReceivedEventHandler(backgroundWorker_OutputDataReceived);
                }
                return bResult;
            }
        }

        private void parseResult()
        {
            MkvInfoTrack oTempTrack;
            foreach (String Line in Regex.Split(_strResult, "\r\n"))
            {
                if (Line.StartsWith("Track ID"))
                {
                    oTempTrack = new MkvInfoTrack(_strFile);
                    int ID = -1;
                    Int32.TryParse(Line.Substring(9, Line.IndexOf(':') - 9), out ID);
                    oTempTrack.TrackID = ID;

                    string strLine = Line.Substring(0, Line.Length - 1);

                    switch (strLine.Substring(strLine.IndexOf(':') + 2, 5)) 
                    {
                        case "video": oTempTrack.Type = TrackType.Video; break;
                        case "audio": oTempTrack.Type = TrackType.Audio; break;
                        case "subti": oTempTrack.Type = TrackType.Subtitle; break;
                    }

                    foreach (string strData in strLine.Split(' '))
                    {
                        string[] value = strData.Split(':');
                        if (value.Length < 2)
                            continue;
                        if (value[0].StartsWith("["))
                            value[0] = value[0].Substring(1);
                        value[1] = value[1].Replace("\\s", " ").Replace("\\2", "\"").Replace("\\c", ":").Replace("\\h", "#").Replace("\\\\", "\\");
                        switch (value[0].ToLower())
                        {
                            case "default_track": if (value[1].Equals("0")) oTempTrack.DefaultTrack = false; break;
                            case "forced_track": if (value[1].Equals("1")) oTempTrack.ForcedTrack = true; break;
                            case "codec_id": oTempTrack.CodecID = value[1]; break;
                            case "language": oTempTrack.Language = value[1]; break;
                            case "track_name": oTempTrack.Name = value[1]; break;
                            case "audio_channels": oTempTrack.AudioChannels = value[1] + " Channels"; break;
                        }
                    }

                    if (oTempTrack.TrackID != -1)
                        _oTracks.Add(oTempTrack);
                }

                if (Line.StartsWith("Chapters:"))
                    _bHasChapters = true;
            }
        }

        void backgroundWorker_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!String.IsNullOrEmpty(e.Data))
                _strResult += e.Data.Trim() + "\r\n";
        }

        void backgroundWorker_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!String.IsNullOrEmpty(e.Data))
                _strResult += e.Data.Trim() + "\r\n";
        }
    }
}
