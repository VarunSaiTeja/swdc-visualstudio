﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Threading;
using EnvDTE;
using EnvDTE80;

namespace SoftwareCo
{
    public sealed class WallclockManager
    {
        private static readonly Lazy<WallclockManager> lazy = new Lazy<WallclockManager>(() => new WallclockManager());

        private System.Threading.Timer timer;
        private static int SECONDS_TO_INCREMENT = 30;
        private static int THIRTY_SECONDS_IN_MILLIS = 1000 * SECONDS_TO_INCREMENT;
        private static int ONE_MINUTE = THIRTY_SECONDS_IN_MILLIS * 2;

        private long _wctime = 0;
        private string _currentDay = "";

        private DTE2 ObjDte;
        private SoftwareCoPackage package;
        private SessionSummaryManager sessionSummaryMgr;

        public static WallclockManager Instance { get { return lazy.Value; } }

        public CancellationToken DisposalToken { get; private set; }

        private WallclockManager()
        {
            timer = new System.Threading.Timer(
                      WallclcockTimerHandlerAsync,
                      null,
                      1000,
                      THIRTY_SECONDS_IN_MILLIS);
            sessionSummaryMgr = SessionSummaryManager.Instance;
        }

        private void WallclcockTimerHandlerAsync(object stateinfo)
        {
            if (IsVisualStudioAppInForeground())
            {
                this._wctime = SoftwareCoUtil.getItemAsLong("wctime");
                this._wctime += SECONDS_TO_INCREMENT;
                SoftwareCoUtil.setNumericItem("wctime", this._wctime);

                // update the file info file
                this.UpdateTimeData();
            }
            DispatchUpdateAsync();
        }

        public bool IsVisualStudioAppInForeground()
        {
            bool isRunning = false;
            System.Diagnostics.Process[] processes =
                System.Diagnostics.Process.GetProcesses();
            foreach (System.Diagnostics.Process p in processes)
            {
                if (!string.IsNullOrEmpty(p.MainWindowTitle))
                {
                    string title = p.MainWindowTitle.ToLower();
                    
                    if (title.Contains("software") && title.Contains("microsoft")
                        && title.Contains("visual") && title.Contains("studio") && title.Contains("running"))
                    {
                        /**
                         * [CodeTime Info 11:53:55 AM] Code Time: File open incremented
                            [CodeTime Info 11:54:17 AM] process: MusicTime - Microsoft Visual Studio  - Experimental Instance, 00:00:27.2812500
                            [CodeTime Info 11:54:17 AM] process: Software (Running) - Microsoft Visual Studio , 01:09:12.0468750
                            [CodeTime Info 11:54:47 AM] process: MusicTime - Microsoft Visual Studio  - Experimental Instance, 00:00:28.3593750
                            [CodeTime Info 11:54:47 AM] process: Software (Running) - Microsoft Visual Studio , 01:09:13.3281250
                        **/
                        // Logger.Info("app: " + p.MainWindowTitle);
                        isRunning = true;
                        break;
                    }
                }
            }
            return isRunning;
        }

        private async Task UpdateTimeData()
        {
            TimeData td = TimeDataManager.Instance.GetTimeDataSummary();
            td.editor_seconds = this._wctime;
            TimeDataManager.Instance.UpdateTimeSummaryData(td.editor_seconds, td.session_seconds, td.file_seconds);
        }

        public void InjectAsyncPackage(SoftwareCoPackage package, DTE2 ObjDte)
        {
            this.package = package;
            this.ObjDte = ObjDte;
        }

        public long GetWcTimeInMinutes()
        {
            this._wctime = SoftwareCoUtil.getItemAsLong("wctime");
            return this._wctime / 60;
        }

        public void ClearWcTime()
        {
            this._wctime = 0L;
            SoftwareCoUtil.setNumericItem("wctime", this._wctime);
            DispatchUpdateAsync();
        }

        private async Task DispatchUpdateAsync()
        {
            SessionSummaryManager.Instance.UpdateStatusBarWithSummaryData();
            package.RebuildCodeMetricsAsync();
            package.RebuildGitMetricsAsync();
        }

        public void UpdateBasedOnSessionSeconds(long session_seconds)
        {

            // check to see if the session seconds has gained before the editor seconds
            // if so, then update the editor seconds
            if (this._wctime < session_seconds)
            {
                this._wctime = session_seconds + 1;
                SoftwareCoUtil.setNumericItem("wctime", this._wctime);

                // update the code metrics part of the tree since this value has changed
                DispatchUpdateAsync();
            }
        }

        private async Task GetNewDayChecker()
        {
            NowTime nowTime = SoftwareCoUtil.GetNowTime();
            if (!nowTime.local_day.Equals(_currentDay))
            {
                // send the offline data
                SoftwareCoPackage.SendOfflineData(null);

                // send the offline TimeData payloads
                // this will clear the time data summary as well
                TimeDataManager.Instance.SendTimeDataAsync();

                // day does't match. clear the wall clock time,
                // the session summary, time data summary,
                // and the file change info summary data
                ClearWcTime();
                SessionSummaryManager.Instance.ÇlearSessionSummaryData();

                // set the current day
                _currentDay = nowTime.local_day;

                // update the current day
                SoftwareCoUtil.setItem("currentDay", _currentDay);
                // update the last payload timestamp
                long latestPayloadTimestampEndUtc = 0;
                SoftwareCoUtil.setNumericItem("latestPayloadTimestampEndUtc", latestPayloadTimestampEndUtc);

                // update the session summary global and averages for the new day
                Task.Delay(ONE_MINUTE).ContinueWith((task) => { UpdateSessionSummaryFromServerAsync(); });
            }
        }

        public async Task UpdateSessionSummaryFromServerAsync()
        {
            object jwt = SoftwareCoUtil.getItem("jwt");
            if (jwt != null)
            {
                string api = "/sessions/summary";
                HttpResponseMessage response = await SoftwareHttpManager.SendRequestAsync(HttpMethod.Get, api, jwt.ToString());
                if (SoftwareHttpManager.IsOk(response))
                {
                    SessionSummary summary = SessionSummaryManager.Instance.GetSessionSummayData();
                    string responseBody = await response.Content.ReadAsStringAsync();
                    IDictionary<string, object> jsonObj = (IDictionary<string, object>)SimpleJson.DeserializeObject(responseBody);
                    if (jsonObj != null)
                    {
                        try
                        {
                            SessionSummary incomingSummary = summary.GetSessionSummaryFromDictionary(jsonObj);
                            summary.CloneSessionSummary(incomingSummary);
                            SessionSummaryManager.Instance.SaveSessionSummaryToDisk(summary);

                            // update the wallclock time if the session seconds is greater. this can happen when using multiple editor types
                            WallclockManager.Instance.UpdateBasedOnSessionSeconds(summary.currentDayMinutes * 60);
                        } catch (Exception e)
                        {
                            Logger.Error("failed to read json: " + e.Message);
                        }
                    }
                }
            }
            sessionSummaryMgr.UpdateStatusBarWithSummaryData();
        }
    }
}

