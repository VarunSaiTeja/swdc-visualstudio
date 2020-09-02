﻿using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace SoftwareCo
{

    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [Guid(PackageGuidString)]
    [ProvideAutoLoad(UIContextGuids.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(CodeMetricsToolPane),
        Window = ToolWindowGuids.SolutionExplorer,
        MultiInstances = false)]
    public sealed class SoftwareCoPackage : AsyncPackage
    {
        #region fields

        public const string PackageGuidString = "0ae38c4e-1ac5-4457-bdca-bb2dfc342a1c";

        private Events2 events;
        private DocumentEvents _docEvents;
        private TextEditorEvents _textEditorEvents;
        private TextDocumentKeyPressEvents _textDocKeyEvents;
        private WindowVisibilityEvents _windowVisibilityEvents;

        private Timer offlineDataTimer;
        private Timer processPayloadTimer;

        // Used by Constants for version info
        public static DTE ObjDte;
        private DocEventManager docEventMgr;

        private static int ONE_MINUTE = 1000 * 60;
        public static bool INITIALIZED = false;

        private int solutionTryThreshold = 10;
        private int solutionTryCount = 0;

        public SoftwareCoPackage() { }

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            try
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync();
                base.Initialize();

                // obtain the DTE service to track doc changes
                ObjDte = await GetServiceAsync(typeof(DTE)) as DTE;
                events = (Events2)ObjDte.Events;

                // init the package manager that will use the AsyncPackage to run main thread requests
                PackageManager.initialize(this, ObjDte);

                // initialize the rest of the plugin in 10 seconds (allow the user to select a solution to open)
                new Scheduler().Execute(() => CheckSolutionActivation(), 10000);
            }
            catch (Exception ex)
            {
                Logger.Error("Error Initializing Code Time", ex);
            }
        }

        public async void CheckSolutionActivation()
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (!INITIALIZED)
            {
                // don't initialize the rest of the plugin until a project is loaded
                string solutionDir = await PackageManager.GetSolutionDirectory();
                if (string.IsNullOrEmpty(solutionDir) || solutionTryCount > solutionTryThreshold)
                {
                    solutionTryCount++;
                    // no solution, try again later
                    new Scheduler().Execute(() => CheckSolutionActivation(), 5000);
                }
                else
                {
                    // solution is activated or it's empty, initialize
                    new Scheduler().Execute(() => InitializeListeners(), 1000);
                }
            }
        }

        private async void InitializeListeners()
        {

            // Intialize the document event handlers
            _textEditorEvents = events.TextEditorEvents;
            _textDocKeyEvents = events.TextDocumentKeyPressEvents;
            _docEvents = events.DocumentEvents;
            _windowVisibilityEvents = events.WindowVisibilityEvents;

            // init the doc event mgr and inject ObjDte
            docEventMgr = DocEventManager.Instance;

            // setup event handlers
            _textDocKeyEvents.BeforeKeyPress += new _dispTextDocumentKeyPressEvents_BeforeKeyPressEventHandler(BeforeKeyPress);
            _docEvents.DocumentClosing += docEventMgr.DocEventsOnDocumentClosedAsync;
            _windowVisibilityEvents.WindowShowing += docEventMgr.WindowVisibilityEventAsync;
            _textEditorEvents.LineChanged += docEventMgr.LineChangedAsync;

            // solution is activated, initialize
            new Scheduler().Execute(() => InitializePlugin(), 1000);
        }

        private async void InitializePlugin()
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (!INITIALIZED)
            {
                await this.InitializeUserInfoAsync();

                // update the latestPayloadTimestampEndUtc
                NowTime nowTime = SoftwareCoUtil.GetNowTime();
                FileManager.setNumericItem("latestPayloadTimestampEndUtc", nowTime.now);

                await PackageManager.InitializeStatusBar();

                // init the wallclock
                WallclockManager wallclockMgr = WallclockManager.Instance;

                // initialize the menu commands
                SoftwareLaunchCommand.InitializeAsync(this);
                SoftwareDashboardLaunchCommand.InitializeAsync(this);
                SoftwareLoginCommand.InitializeAsync(this);
                SoftwareToggleStatusInfoCommand.InitializeAsync(this);
                SoftwareOpenCodeMetricsTreeCommand.InitializeAsync(this);

                // check if the "name" is set. if not, get the user
                string name = FileManager.getItemAsString("name");
                if (string.IsNullOrEmpty(name))
                {
                    // enable the login command
                    SoftwareLoginCommand.UpdateEnabledState(true);
                }
                else
                {
                    // enable web dashboard command
                    SoftwareLaunchCommand.UpdateEnabledState(true);
                }

                // create a 5 minute timer to send offline data
                offlineDataTimer = new Timer(
                      SendOfflineData,
                      null,
                      ONE_MINUTE / 2,
                      ONE_MINUTE * 5);

                new Scheduler().Execute(() => SendOfflinePluginBatchData(), 15000);

                INITIALIZED = true;

                string PluginVersion = EnvUtil.GetVersion();
                Logger.Info(string.Format("Initialized Code Time v{0}", PluginVersion));

                // show the readme if it's the initial install
                InitializeReadme();

                // initialize the tracker manager
                InitializeTracker();
            }
        }

        private async void InitializeTracker()
        {
            // initialize the tracker event manager
            TrackerEventManager.init();
        }

        private async void InitializeReadme()
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync();
            // check if we've shown the readme or not
            bool initializedVisualStudioPlugin = FileManager.getItemAsBool("visualstudio_CtInit");
            if (!initializedVisualStudioPlugin)
            {
                DashboardManager.Instance.LaunchReadmeFileAsync();
                FileManager.setBoolItem("visualstudio_CtInit", true);

                // launch the tree view
                PackageManager.OpenCodeMetricsPaneAsync();
            }
        }

        void BeforeKeyPress(string Keypress, EnvDTE.TextSelection Selection, bool InStatementCompletion, ref bool CancelKeypress)
        {
            docEventMgr.BeforeKeyPressAsync(Keypress, Selection, InStatementCompletion, CancelKeypress);
        }

        public void Dispose()
        {
            TrackerEventManager.TrackEditorActionEvent("editor", "deactivate");

            WallclockManager.Instance.Dispose();

            TrackerManager.Dispose();

            if (offlineDataTimer != null)
            {
                _textDocKeyEvents.BeforeKeyPress -= new _dispTextDocumentKeyPressEvents_BeforeKeyPressEventHandler(BeforeKeyPress);
                _docEvents.DocumentClosing -= docEventMgr.DocEventsOnDocumentClosedAsync;
                _textEditorEvents.LineChanged -= docEventMgr.LineChangedAsync;
                _windowVisibilityEvents.WindowShowing -= docEventMgr.WindowVisibilityEventAsync;

                offlineDataTimer.Dispose();
                offlineDataTimer = null;
            }
            if (processPayloadTimer != null)
            {
                processPayloadTimer.Dispose();
                processPayloadTimer = null;
            }

            INITIALIZED = false;
        }
        #endregion

        #region Methods

        public void ProcessKeystrokePayload(Object stateInfo)
        {
            DocEventManager.Instance.PostData();
        }

        public static async void SendOfflineData(object stateinfo)
        {
            SendOfflinePluginBatchData();
        }

        public static async void SendOfflinePluginBatchData()
        {

            Logger.Info(DateTime.Now.ToString());
            bool online = await SoftwareUserManager.IsOnlineAsync();

            if (!online)
            {
                return;
            }

            int batch_limit = 25;
            bool succeeded = false;
            List<string> offlinePluginData = FileManager.GetOfflinePayloadList();
            List<string> batchList = new List<string>();
            if (offlinePluginData != null && offlinePluginData.Count > 0)
            {
                for (int i = 0; i < offlinePluginData.Count; i++)
                {
                    string line = offlinePluginData[i];
                    if (i >= batch_limit)
                    {
                        // send this batch off
                        succeeded = await SendBatchData(batchList);
                        if (!succeeded)
                        {
                            if (offlinePluginData.Count > 1000)
                            {
                                // delete anyway, there's an issue and the data is gathering
                                File.Delete(FileManager.getSoftwareDataStoreFile());
                            }
                            return;
                        }
                    }
                    batchList.Add(line);
                }

                if (batchList.Count > 0)
                {
                    succeeded = await SendBatchData(batchList);
                }

                // delete the file
                if (succeeded)
                {
                    File.Delete(FileManager.getSoftwareDataStoreFile());
                }
                else if (offlinePluginData.Count > 1000)
                {
                    File.Delete(FileManager.getSoftwareDataStoreFile());
                }
            }
        }

        private static async Task<bool> SendBatchData(List<string> batchList)
        {
            // send this batch off
            string jsonData = "[" + string.Join(",", batchList) + "]";
            HttpResponseMessage response = await SoftwareHttpManager.SendRequestAsync(HttpMethod.Post, "/data/batch", jsonData);
            if (!SoftwareHttpManager.IsOk(response) && response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            {
                // there was an error, don't delete the offline data
                return false;
            }
            batchList.Clear();
            return true;
        }

        private async Task InitializeUserInfoAsync()
        {
            try
            {
                bool softwareSessionFileExists = FileManager.softwareSessionFileExists();
                string jwt = FileManager.getItemAsString("jwt");
                if (string.IsNullOrEmpty(jwt))
                {
                    string result = await SoftwareUserManager.CreateAnonymousUserAsync();
                }

                long sessionTresholdSeconds = FileManager.getItemAsLong("sessionThresholdInSec");
                if (sessionTresholdSeconds == 0)
                {
                    // update the session threshold in seconds config
                    FileManager.setNumericItem("sessionThresholdInSec", Constants.DEFAULT_SESSION_THRESHOLD_SECONDS);
                }

            }
            catch (Exception ex)
            {
                Logger.Error("Error Initializing UserInfo", ex);

            }

        }

        #endregion
    }
}
