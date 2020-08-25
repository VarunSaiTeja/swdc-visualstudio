﻿
using System;
using EnvDTE;
using System.IO;
using System.Linq;

namespace SoftwareCo
{
    public sealed class DocEventManager
    {
        private static readonly Lazy<DocEventManager> lazy = new Lazy<DocEventManager>(() => new DocEventManager());

        // private SoftwareData _softwareData;
        private PluginData _pluginData;

        private Document doc = null;
        public static DocEventManager Instance { get { return lazy.Value; } }


        public bool hasData()
        {
            if (_pluginData != null && _pluginData.source != null && _pluginData.source.Count > 0 && _pluginData.keystrokes > 0)
            {
                return true;
            }
            return false;
        }

        private bool IsTrueEventFile(string fileName)
        {
            return (fileName == null || fileName.IndexOf("CodeTime.txt") != -1) ? false : true;
        }

        private async void InitPluginDataIfNotExists()
        {
            if (_pluginData == null)
            {
                string _solutionDirectory = await PackageManager.GetSolutionDirectory();
                if (_solutionDirectory != null && !_solutionDirectory.Equals(""))
                {
                    FileInfo fi = new FileInfo(_solutionDirectory);
                    _pluginData = new PluginData(fi.Name, _solutionDirectory);
                } else
                {
                    // set it to unnamed
                    _pluginData = new PluginData("Unnamed", "Untitled");
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
            if (!IsTrueEventFile(fileName))
            {
                return;
            }
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
            if (!IsTrueEventFile(fileName))
            {
                return;
            }
            InitPluginDataIfNotExists();
            _pluginData.InitFileInfoIfNotExists(fileName);
        }

        public static int CountLinesLINQ(string fileName)
        {
            FileInfo fi = new FileInfo(fileName);
            if (fi != null && fi.Exists)
            {
                return File.ReadLines(fi.FullName).Count();
            }
            return 0;
        }

        private void UpdateLineCount(PluginDataFileInfo pdfileInfo)
        {
            pdfileInfo.lines = CountLinesLINQ(pdfileInfo.file);
        }

        public async void SelectionEventAsync()
        {
            doc = await PackageManager.GetActiveDocument();
        }

        public async void LineChangedAsync(TextPoint start, TextPoint end, int hint)
        {
            if (doc == null)
            {
                doc = (start.DTE != null && start.DTE.ActiveDocument != null) ? start.DTE.ActiveDocument : await PackageManager.GetActiveDocument();
            }

            if (doc == null)
            {
                return;
            }

            string fileName = doc.FullName;
            if (!IsTrueEventFile(fileName))
            {
                return;
            }

            InitPluginDataIfNotExists();
            _pluginData.InitFileInfoIfNotExists(fileName);

            PluginDataFileInfo pdfileInfo = _pluginData.GetFileInfo(fileName);

            if (pdfileInfo == null)
            {
                return;
            }

            int line_count = CountLinesLINQ(pdfileInfo.file);

            if (start.DisplayColumn == end.DisplayColumn && line_count == pdfileInfo.lines)
            {
                return;
            }

            UpdateFileInfoMetrics(pdfileInfo, start, end, null, line_count);
            
        }

        public async void AfterKeyPressedAsync(
            string Keypress, TextSelection Selection, bool InStatementCompletion)
        {

            if (doc == null)
            {
                doc = (Selection.DTE != null && Selection.DTE.ActiveDocument != null) ? Selection.DTE.ActiveDocument : await PackageManager.GetActiveDocument();
            }

            if (doc == null)
            {
                return;
            }


            String fileName = doc.FullName;
            if (!IsTrueEventFile(fileName))
            {
                return;
            }

            InitPluginDataIfNotExists();
            _pluginData.InitFileInfoIfNotExists(fileName);

            PluginDataFileInfo pdfileInfo = _pluginData.GetFileInfo(fileName);
            if (pdfileInfo == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(pdfileInfo.syntax))
            {
                string syntax = doc.Language;

                if (!string.IsNullOrEmpty(syntax))
                {
                    pdfileInfo.syntax = syntax;
                }
            }

            int line_count = CountLinesLINQ(pdfileInfo.file);

            UpdateFileInfoMetrics(pdfileInfo, null, null, Keypress, line_count);
        }

        public async void DocEventsOnDocumentOpenedAsync(Document document)
        {
            if (document == null || document.FullName == null)
            {
                return;
            }
            String fileName = document.FullName;
            if (!IsTrueEventFile(fileName))
            {
                return;
            }
            InitPluginDataIfNotExists();
            _pluginData.InitFileInfoIfNotExists(fileName);

            PluginDataFileInfo pdfileInfo = _pluginData.GetFileInfo(fileName);
            UpdateLineCount(pdfileInfo);

            TrackerEventManager.TrackEditorFileActionEvent("file", "open", fileName);
        }

    
        public async void DocEventsOnDocumentClosedAsync(Document document)
        {
            if (document == null || document.FullName == null)
            {
                return;
            }
            String fileName = document.FullName;
            if (!IsTrueEventFile(fileName))
            {
                return;
            }
            InitPluginDataIfNotExists();
            _pluginData.InitFileInfoIfNotExists(fileName);

            TrackerEventManager.TrackEditorFileActionEvent("file", "open", fileName);
        }

        
        public async void PostData()
        {
            NowTime nowTime = SoftwareCoUtil.GetNowTime();

            if (_pluginData != null && _pluginData.source.Count > 0 && _pluginData.keystrokes > 0)
            {

                // create the aggregates, end the file times, gather the cumulatives
                string softwareDataContent = await _pluginData.CompletePayloadAndReturnJsonString();

                Logger.Info("Code Time: storing plugin data: " + softwareDataContent);

                TrackerEventManager.TrackCodeTimeEventAsync(_pluginData);

                FileManager.AppendPluginData(softwareDataContent);

                // update the latestPayloadTimestampEndUtc
                FileManager.setNumericItem("latestPayloadTimestampEndUtc", nowTime.now);

                // update the status bar and tree
                WallclockManager.Instance.DispatchUpdateAsync();
                _pluginData = null;
            }
        }

        private void UpdateFileInfoMetrics(PluginDataFileInfo fileInfo, TextPoint start, TextPoint end, string Keypress, int line_count)
        {

            bool hasAutoIndent = false;
            bool newLineAutoIndent = false;

            int numKeystrokes = 0;
            int numDeleteKeystrokes = 0;

            int linesRemoved = 0;
            int linesAdded = 0;

            if (fileInfo.lines > line_count)
            {
                linesRemoved = fileInfo.lines - line_count;
            }
            else if (fileInfo.lines < line_count)
            {
                linesAdded = line_count - fileInfo.lines;
            }

            if (start != null && end != null)
            {
                numKeystrokes = end.DisplayColumn - start.DisplayColumn;
            }

            if (Keypress != null)
            {
                if (Keypress == "\b")
                {
                    // it's a single delete
                    numDeleteKeystrokes = 1;
                } else if (Keypress == "\r")
                {
                    // it's a single carriage return
                    linesAdded = 1;
                } else
                {
                    // it's a single character
                    numKeystrokes = 1;
                }
            }

            // update the deletion keystrokes if there are lines removed
            numDeleteKeystrokes = numDeleteKeystrokes >= linesRemoved ? numDeleteKeystrokes - linesRemoved : numDeleteKeystrokes;

            // event updates
            if (newLineAutoIndent)
            {
                // it's a new line with auto-indent
                fileInfo.auto_indents += 1;
                fileInfo.linesAdded += 1;
            }
            else if (hasAutoIndent)
            {
                // it's an auto indent action
                fileInfo.auto_indents += 1;
            }
            else if (linesAdded == 1)
            {
                // it's a single new line action (single_adds)
                fileInfo.single_adds += 1;
                fileInfo.linesAdded += 1;
            }
            else if (linesAdded > 1)
            {
                // it's a multi line paste action (multi_adds)
                fileInfo.linesAdded += linesAdded;
                fileInfo.paste += 1;
                fileInfo.multi_adds += 1;
                fileInfo.is_net_change = true;
                fileInfo.characters_added += Math.Abs(numKeystrokes - linesAdded);
            }
            else if (numDeleteKeystrokes > 0 && numKeystrokes > 0)
            {
                // it's a replacement
                fileInfo.replacements += 1;
                fileInfo.characters_added += numKeystrokes;
                fileInfo.characters_deleted += numDeleteKeystrokes;
            }
            else if (numKeystrokes > 1)
            {
                // pasted characters (multi_adds)
                fileInfo.paste += 1;
                fileInfo.multi_adds += 1;
                fileInfo.is_net_change = true;
                fileInfo.characters_added += numKeystrokes;
            }
            else if (numKeystrokes == 1)
            {
                // it's a single keystroke action (single_adds)
                fileInfo.add += 1;
                fileInfo.single_adds += 1;
                fileInfo.characters_added += 1;
            }
            else if (linesRemoved == 1)
            {
                // it's a single line deletion
                fileInfo.linesRemoved += 1;
                fileInfo.single_deletes += 1;
                fileInfo.characters_deleted += numDeleteKeystrokes;
            }
            else if (linesRemoved > 1)
            {
                // it's a multi line deletion and may contain characters
                fileInfo.characters_deleted += numDeleteKeystrokes;
                fileInfo.multi_deletes += 1;
                fileInfo.is_net_change = true;
                fileInfo.linesRemoved += linesRemoved;
            }
            else if (numDeleteKeystrokes == 1)
            {
                // it's a single character deletion action
                fileInfo.delete += 1;
                fileInfo.single_deletes += 1;
                fileInfo.characters_deleted += 1;
            }
            else if (numDeleteKeystrokes > 1)
            {
                // it's a multi character deletion action
                fileInfo.multi_deletes += 1;
                fileInfo.is_net_change = true;
                fileInfo.characters_deleted += numDeleteKeystrokes;
            }

            fileInfo.lines = line_count;
            fileInfo.keystrokes += 1;
            _pluginData.keystrokes += 1;
        }
    }
}
