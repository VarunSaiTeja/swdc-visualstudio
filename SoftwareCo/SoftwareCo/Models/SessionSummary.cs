﻿using Commons.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoftwareCo
{

     public class SessionSummary
     {
        public long currentDayMinutes { get; set; }
        public long currentDayKeystrokes { get; set; }
        public long currentDayKpm { get; set; }
        public long currentDayLinesAdded { get; set; }
        public long currentDayLinesRemoved { get; set; }
        public float currentSessionGoalPercent { get; set; }

        public long averageDailyMinutes { get; set; }
        public long averageDailyKeystrokes { get; set; }
        public long averageDailyKpm { get; set; }
        public long averageDailyLinesAdded { get; set; }
        public long averageDailyLinesRemoved { get; set; }

        public long globalAverageSeconds { get; set; }
        public long globalAverageDailyMinutes { get; set; }
        public long globalAverageDailyKeystrokes { get; set; }
        public long globalAverageLinesAdded { get; set; }
        public long globalAverageLinesRemoved { get; set; }

        public bool inflow { get; set; }
        public float timePercent { get; set; }
        public float volumePercent { get; set; }
        public float velocityPercent { get; set; }

        public int liveshareMinutes { get; set; }
        public long latestPayloadTimestamp { get; set; }
        public long latestPayloadTimestampEndUtc { get; set; }
        public bool lastUpdatedToday { get; set; }

        public int dailyMinutesGoal { get; set; }


        public string GetSessionSummaryAsJson()
        {
            JsonObject jsonObj = new JsonObject();
            jsonObj.Add("currentDayMinutes", this.currentDayMinutes);
            jsonObj.Add("currentDayKeystrokes", this.currentDayKeystrokes);
            jsonObj.Add("currentDayKpm", this.currentDayKpm);
            jsonObj.Add("currentDayLinesAdded", this.currentDayLinesAdded);
            jsonObj.Add("currentDayLinesRemoved", this.currentDayLinesRemoved);
            jsonObj.Add("currentSessionGoalPercent", this.currentSessionGoalPercent);

            jsonObj.Add("averageDailyMinutes", this.averageDailyMinutes);
            jsonObj.Add("averageDailyKeystrokes", this.averageDailyKeystrokes);
            jsonObj.Add("averageDailyKpm", this.averageDailyKpm);
            jsonObj.Add("averageDailyLinesAdded", this.averageDailyLinesAdded);
            jsonObj.Add("averageDailyLinesRemoved", this.averageDailyLinesRemoved);

            jsonObj.Add("globalAverageSeconds", this.globalAverageSeconds);
            jsonObj.Add("globalAverageDailyMinutes", this.globalAverageDailyMinutes);
            jsonObj.Add("globalAverageDailyKeystrokes", this.globalAverageDailyKeystrokes);
            jsonObj.Add("globalAverageLinesAdded", this.globalAverageLinesAdded);
            jsonObj.Add("globalAverageLinesRemoved", this.globalAverageLinesRemoved);

            jsonObj.Add("inflow", this.inflow);
            jsonObj.Add("timePercent", this.timePercent);
            jsonObj.Add("volumePercent", this.volumePercent);
            jsonObj.Add("velocityPercent", this.velocityPercent);

            jsonObj.Add("liveshareMinutes", this.liveshareMinutes);
            jsonObj.Add("latestPayloadTimestamp", this.latestPayloadTimestamp);
            jsonObj.Add("latestPayloadTimestampEndUtc", this.latestPayloadTimestampEndUtc);
            jsonObj.Add("lastUpdatedToday", this.lastUpdatedToday);

            jsonObj.Add("dailyMinutesGoal", this.dailyMinutesGoal);
            return jsonObj.ToString();
        }

        public void CloneSessionSummary(SessionSummary summary)
        {
            if (this.currentDayMinutes < summary.currentDayMinutes)
            {
                // add the current attributes
                this.currentDayMinutes = summary.currentDayMinutes;
                this.currentDayKeystrokes = summary.currentDayKeystrokes;
                this.currentDayKpm = summary.currentDayKpm;
                this.currentDayLinesAdded = summary.currentDayLinesAdded;
                this.currentDayLinesRemoved = summary.currentDayLinesRemoved;
            }

            this.currentSessionGoalPercent = summary.currentSessionGoalPercent;
            this.averageDailyMinutes = summary.averageDailyMinutes;
            this.averageDailyKeystrokes = summary.averageDailyKeystrokes;
            this.averageDailyKpm = summary.averageDailyKpm;
            this.averageDailyLinesAdded = summary.averageDailyLinesAdded;
            this.averageDailyLinesRemoved = summary.averageDailyLinesRemoved;

            this.globalAverageSeconds = summary.globalAverageSeconds;
            this.globalAverageDailyMinutes = summary.globalAverageDailyMinutes;
            this.globalAverageDailyKeystrokes = summary.globalAverageDailyKeystrokes;
            this.globalAverageLinesAdded = summary.globalAverageLinesAdded;
            this.globalAverageLinesRemoved = summary.globalAverageLinesRemoved;

            this.inflow = summary.inflow;
            this.timePercent = summary.timePercent;
            this.volumePercent = summary.volumePercent;
            this.velocityPercent = summary.velocityPercent;

            this.liveshareMinutes = summary.liveshareMinutes;
            this.latestPayloadTimestamp = summary.latestPayloadTimestamp;
            this.latestPayloadTimestampEndUtc = summary.latestPayloadTimestampEndUtc;
            this.lastUpdatedToday = summary.lastUpdatedToday;

            this.dailyMinutesGoal = summary.dailyMinutesGoal;
        }
      
    }
}