﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.IO;
using System.Net.Http;
using System.Reflection;
using Microsoft.VisualStudio;
using System.Windows.Forms;

namespace SoftwareCo
{
    public sealed class DocEventManager
    {
        private static readonly Lazy<DocEventManager> lazy = new Lazy<DocEventManager>(() => new DocEventManager());

        // private SoftwareData _softwareData;
        private PluginData _pluginData;
        // Used by Constants for version info
        public static DTE2 ObjDte { set; get; }

        private static int THIRTY_SECONDS = 1000 * 30;
        private static int ONE_MINUTE = THIRTY_SECONDS * 2;
        private static int ZERO_SECOND = 1;

        private SessionSummaryManager sessionSummaryMgr;

        public static DocEventManager Instance { get { return lazy.Value; } }

        private DocEventManager()
        {
            sessionSummaryMgr = SessionSummaryManager.Instance;
        }

        private void InitPluginDataIfNotExists()
        {
            if (_pluginData == null)
            {
                string solutionDirectory = SoftwareCoPackage.GetSolutionDirectory();
                if (solutionDirectory != null && !solutionDirectory.Equals(""))
                {
                    FileInfo fi = new FileInfo(solutionDirectory);
                    _pluginData = new PluginData(fi.Name, solutionDirectory);
                } else
                {
                    // set it to unnamed
                    _pluginData = new PluginData("Untitled", "Unnamed");
                }
            }
        }

        public void DocEventsOnDocumentSaved(Document document)
        {
            if (document == null || document.FullName == null)
            {
                return;
            }
            String fileName = document.FullName;
            InitPluginDataIfNotExists();
            _pluginData.InitFileInfoIfNotExists(fileName);


            // wrapper for a file path
            FileInfo fi = new FileInfo(fileName);
            _pluginData.GetFileInfo(fileName).length = fi.Length;
        }

        public async void DocEventsOnDocumentOpeningAsync(String docPath, Boolean readOnly)
        {
            // wrapper for a file path
            FileInfo fi = new FileInfo(docPath);
            String fileName = fi.FullName;
            InitPluginDataIfNotExists();
            _pluginData.InitFileInfoIfNotExists(fileName);
        }

        public async void AfterKeyPressedAsync(
            string Keypress, TextSelection Selection, bool InStatementCompletion)
        {
            String fileName = ObjDte.ActiveWindow.Document.FullName;
            InitPluginDataIfNotExists();
            _pluginData.InitFileInfoIfNotExists(fileName);

            PluginDataFileInfo pdfileInfo = _pluginData.GetFileInfo(fileName);
            if (ObjDte.ActiveWindow.Document.Language != null)
            {
                pdfileInfo.syntax = ObjDte.ActiveWindow.Document.Language;
            }
            if (!String.IsNullOrEmpty(Keypress))
            {
                FileInfo fi = new FileInfo(fileName);

                bool isNewLine = false;
                if (Keypress == "\b")
                {
                    // register a delete event
                    pdfileInfo.delete += 1;
                    Logger.Info("Code Time: Delete character incremented");
                }
                else if (Keypress == "\r")
                {
                    isNewLine = true;
                }
                else
                {
                    pdfileInfo.add += 1;
                    Logger.Info("Code Time: KPM incremented");
                }

                if (isNewLine)
                {
                    pdfileInfo.linesAdded += 1;
                }
                pdfileInfo.keystrokes += 1;
            }
        }

        public async void DocEventsOnDocumentOpenedAsync(Document document)
        {
            if (document == null || document.FullName == null)
            {
                return;
            }
            String fileName = document.FullName;
            InitPluginDataIfNotExists();
            _pluginData.InitFileInfoIfNotExists(fileName);

            try
            {
                _pluginData.GetFileInfo(fileName).open += 1;
                Logger.Info("Code Time: File open incremented");
            }
            catch (Exception ex)
            {
                Logger.Error("DocEventsOnDocumentOpened", ex);
            }
        }

        public async void DocEventsOnDocumentClosedAsync(Document document)
        {
            if (document == null || document.FullName == null)
            {
                return;
            }
            String fileName = document.FullName;
            InitPluginDataIfNotExists();
            _pluginData.InitFileInfoIfNotExists(fileName);

            try
            {
                _pluginData.GetFileInfo(fileName).close += 1;
                Logger.Info("Code Time: File close incremented");
            }
            catch (Exception ex)
            {
                Logger.Error("DocEventsOnDocumentClosed", ex);
            }
        }

        
        public void PostData()
        {
            double offset = 0;
            long end = 0;
            long local_end = 0;

            NowTime nowTime = SoftwareCoUtil.GetNowTime();
            DateTime now = DateTime.UtcNow;

            if (_pluginData != null && _pluginData.source.Count > 0)
            {
                // update the latestPayloadTimestampEndUtc
                SoftwareCoUtil.setNumericItem("latestPayloadTimestampEndUtc", nowTime.now);

                offset = TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now).TotalMinutes;
                _pluginData.offset = Math.Abs((int)offset);
                if (TimeZone.CurrentTimeZone.DaylightName != null
                    && TimeZone.CurrentTimeZone.DaylightName != TimeZone.CurrentTimeZone.StandardName)
                {
                    _pluginData.timezone = TimeZone.CurrentTimeZone.DaylightName;
                }
                else
                {
                    _pluginData.timezone = TimeZone.CurrentTimeZone.StandardName;
                }

                // make sure all of the end times are set
                foreach (PluginDataFileInfo pdFileInfo in _pluginData.source)
                {
                    pdFileInfo.EndFileInfoTime();
                }

                _pluginData.EndPluginDataTime();

                UpdateAggregates();

                string softwareDataContent = _pluginData.GetPluginDataAsJsonString();
                Logger.Info("Code Time: storing: " + softwareDataContent);

                string datastoreFile = SoftwareCoUtil.getSoftwareDataStoreFile();
                // append to the file
                File.AppendAllText(datastoreFile, softwareDataContent + Environment.NewLine);

                _pluginData = null;
            }

        }

        private void UpdateAggregates()
        {
            if (_pluginData == null)
            {
                return;
            }
            List<FileInfoSummary> fileInfoList = _pluginData.GetSourceFileInfoList();
            KeystrokeAggregates aggregates = new KeystrokeAggregates();
            aggregates.directory = _pluginData.project.directory;

            foreach (FileInfoSummary fileInfo in fileInfoList)
            {
                aggregates.Aggregate(fileInfo);
            }

            sessionSummaryMgr.IncrementSessionSummaryData(aggregates);
        }
    }
}
