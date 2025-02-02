﻿using System;
using System.Collections.Generic;

namespace SoftwareCo
{
    public class TimeData
    {
        public long timestamp { get; set; }
        public long timestamp_local { get; set; }
        public long editor_seconds { get; set; }
        public long session_seconds { get; set; }
        public long file_seconds { get; set; }
        public PluginDataProject project { get; set; }
        public string day { get; set; }

        public TimeData GeTimeSummaryFromDictionary(IDictionary<string, object> dict)
        {
            TimeData summary = new TimeData();

            summary.timestamp = SoftwareCoUtil.GetLongVal(dict, "timestamp");
            summary.timestamp_local = SoftwareCoUtil.GetLongVal(dict, "timestamp_local");
            summary.editor_seconds = SoftwareCoUtil.GetLongVal(dict, "editor_seconds");
            summary.session_seconds = SoftwareCoUtil.GetLongVal(dict, "session_seconds");
            summary.file_seconds = SoftwareCoUtil.GetLongVal(dict, "file_seconds");
            summary.day = SoftwareCoUtil.GetStringVal(dict, "day");

            summary.project = PluginDataProject.GetPluginDataFromDictionary(dict);

            return summary;
        }

        public JsonObject GetAsJson()
        {
            JsonObject jsonObj = new JsonObject();
            jsonObj.Add("timestamp", this.timestamp);
            jsonObj.Add("timestamp_local", this.timestamp_local);
            jsonObj.Add("editor_seconds", this.editor_seconds);
            jsonObj.Add("session_seconds", this.session_seconds);
            jsonObj.Add("file_seconds", this.file_seconds);
            jsonObj.Add("day", this.day);
            jsonObj.Add("project", this.project);
            return jsonObj;
        }

        public void CloneFromDictionary(IDictionary<string, object> dict)
        {
            this.timestamp = SoftwareCoUtil.GetLongVal(dict, "timestamp");
            this.timestamp_local = SoftwareCoUtil.GetLongVal(dict, "timestamp_local");
            this.editor_seconds = SoftwareCoUtil.GetLongVal(dict, "editor_seconds");
            this.session_seconds = SoftwareCoUtil.GetLongVal(dict, "session_seconds");
            this.file_seconds = SoftwareCoUtil.GetLongVal(dict, "file_seconds");
            this.day = SoftwareCoUtil.GetStringVal(dict, "day");

            dict.TryGetValue("project", out object projJson);
            try
            {
                JsonObject projJsonObj = (projJson == null) ? null : (JsonObject)projJson;
                if (projJson != null)
                {
                    this.project = PluginDataProject.GetPluginDataFromDictionary(projJsonObj);
                }
            }
            catch (Exception e)
            {
                //
            }
        }

        public void Clone(TimeData td)
        {
            this.timestamp = td.timestamp;
            this.timestamp_local = td.timestamp_local;
            this.editor_seconds = td.editor_seconds;
            this.session_seconds = td.session_seconds;
            this.file_seconds = td.file_seconds;
            this.day = td.day;
            this.project = td.project;
        }
    }
}
