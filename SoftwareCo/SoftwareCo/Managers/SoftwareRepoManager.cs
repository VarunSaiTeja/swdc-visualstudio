﻿
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace SoftwareCo
{
    class SoftwareRepoManager
    {

        public class RepoCommitChanges
        {
            public int insertions = 0;
            public int deletions = 0;
            public RepoCommitChanges(int insertions, int deletions)
            {
                this.insertions = insertions;
                this.deletions = deletions;
            }
        }

        public class RepoCommit
        {
            public string commitId = "";
            public string message = "";
            public long timestamp = 0L;
            public string date = "";
            public IDictionary<string, RepoCommitChanges> changes = new Dictionary<string, RepoCommitChanges>();

            public RepoCommit(string commitId, string message, long timestamp)
            {
                this.commitId = commitId;
                this.message = message;
                this.timestamp = timestamp;
            }

            public JsonObject GetAsJsonObj()
            {
                JsonObject jsonObj = new JsonObject();
                jsonObj.Add("commitId", this.commitId);
                jsonObj.Add("message", this.message);
                jsonObj.Add("timestamp", this.timestamp);

                JsonObject changesJsonObj = new JsonObject();
                foreach (string key in changes.Keys)
                {
                    RepoCommitChanges commitChanges = changes[key];
                    JsonObject changesObj = new JsonObject();
                    changesObj.Add("deletions", commitChanges.deletions);
                    changesObj.Add("insertions", commitChanges.insertions);
                    changesJsonObj.Add(key, changesObj);
                }
                jsonObj.Add("changes", changesJsonObj);
                return jsonObj;
            }
        }

        public class RepoData
        {
            public string identifier = "";
            public string tag = "";
            public string branch = "";
            public List<RepoMember> members = new List<RepoMember>();
            public RepoData(string identifier, string tag, string branch, List<RepoMember> members)
            {
                this.identifier = identifier;
                this.tag = tag;
                this.branch = branch;
                this.members = members;
            }
        }

        public class RepoCommitData
        {
            public string identifier = "";
            public string tag = "";
            public string branch = "";
            public List<RepoCommit> commits = new List<RepoCommit>();
            public RepoCommitData(string identifier, string tag, string branch, List<RepoCommit> commits)
            {
                this.identifier = identifier;
                this.tag = tag;
                this.branch = branch;
                this.commits = commits;
            }

            public string GetAsJson()
            {
                JsonObject jsonObj = new JsonObject();
                jsonObj.Add("identifier", this.identifier);
                jsonObj.Add("tag", this.tag);
                jsonObj.Add("branch", this.branch);
                JsonArray jsonArr = new JsonArray();
                foreach (RepoCommit commit in commits)
                {
                    jsonArr.Add(commit.GetAsJsonObj());
                }
                jsonObj.Add("commits", jsonArr);
                return jsonObj.ToString();
            }
        }

        public List<RepoMember> GetRepoUsers(string projectDir)
        {
            if (!SoftwareCoUtil.IsGitProject(projectDir))
            {
                return new List<RepoMember>();
            }

            RepoResourceInfo info = GitUtilManager.GetResourceInfo(projectDir, true);
            if (info != null && info.members != null)
            {
                return info.members;
            }
            return new List<RepoMember>();
        }

        /**
         * Get the latest repo commit
         **/
        public async Task<RepoCommit> GetLatestCommitAsync(string projectDir)
        {
            try
            {
                if (!SoftwareCoUtil.IsGitProject(projectDir))
                {
                    return null;
                }
                RepoResourceInfo info = GitUtilManager.GetResourceInfo(projectDir, false);

                if (info != null && info.identifier != null)
                {
                    string identifier = info.identifier;
                    if (identifier != null && !identifier.Equals(""))
                    {
                        string tag = info.tag;
                        string branch = info.branch;

                        string qryString = "?identifier=" + identifier;
                        qryString += "&tag=" + tag;
                        qryString += "&branch=" + branch;

                        HttpResponseMessage response = await SoftwareHttpManager.SendRequestAsync(
                                HttpMethod.Get, "/commits/latest?" + qryString);

                        if (SoftwareHttpManager.IsOk(response))
                        {

                            // get the json data
                            string responseBody = await response.Content.ReadAsStringAsync();
                            IDictionary<string, object> jsonObj = (IDictionary<string, object>)SimpleJson.DeserializeObject(responseBody, new Dictionary<string, object>());

                            jsonObj.TryGetValue("commitId", out object commitIdObj);
                            string commitId = (commitIdObj == null) ? "" : Convert.ToString(commitIdObj);

                            jsonObj.TryGetValue("message", out object messageObj);
                            string message = (messageObj == null) ? "" : Convert.ToString(messageObj);

                            jsonObj.TryGetValue("message", out object timestampObj);
                            long timestamp = (timestampObj == null) ? 0L : Convert.ToInt64(timestampObj);

                            RepoCommit repoCommit = new RepoCommit(commitId, message, timestamp);
                            return repoCommit;
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Logger.Error("GetLatestCommitAsync ,error: " + ex.Message, ex);

            }
            return null;
        }

        public async void GetHistoricalCommitsAsync(string projectDir)
        {
            try
            {
                if (!SoftwareCoUtil.IsGitProject(projectDir))
                {
                    return;
                }
                RepoResourceInfo info = GitUtilManager.GetResourceInfo(projectDir, false);

                if (info != null && info.identifier != null)
                {
                    string identifier = info.identifier;
                    if (identifier != null && !identifier.Equals(""))
                    {
                        string tag = info.tag;
                        string branch = info.branch;
                        string email = info.email;

                        RepoCommit latestCommit = null;
                        latestCommit = await this.GetLatestCommitAsync(projectDir);

                        string sinceOption = "";
                        if (latestCommit != null)
                        {
                            sinceOption = " --since=" + latestCommit.timestamp;
                        }
                        else
                        {
                            sinceOption = " --max-count=100";
                        }

                        string cmd = "git log --stat --pretty=COMMIT:%H,%ct,%cI,%s --author=" + email + "" + sinceOption;

                        List<string> lines = SoftwareCoUtil.RunCommand(cmd, projectDir);

                        RepoCommit currentRepoCommit = null;
                        List<RepoCommit> repoCommits = new List<RepoCommit>();
                        if (lines != null && lines.Count > 0)
                        {
                            for (int i = 0; i < lines.Count; i++)
                            {
                                string line = lines[i];
                                if (line.Length > 0)
                                {
                                    bool hasPipe = line.IndexOf("|") != -1 ? true : false;
                                    bool isBin = line.ToLower().IndexOf("bin") != -1 ? true : false;
                                    if (line.IndexOf("COMMIT:") == 0)
                                    {
                                        line = line.Substring("COMMIT:".Length);
                                        if (currentRepoCommit != null)
                                        {
                                            repoCommits.Add(currentRepoCommit);
                                        }

                                        string[] commitInfos = line.Split(',');
                                        if (commitInfos != null && commitInfos.Length > 0)
                                        {
                                            string commitId = commitInfos[0].Trim();
                                            // go to the next line if we've already processed this commitId
                                            if (latestCommit != null && commitId.Equals(latestCommit.commitId))
                                            {
                                                currentRepoCommit = null;
                                                continue;
                                            }

                                            // get the other attributes now
                                            long timestamp = Convert.ToInt64(commitInfos[1].Trim());
                                            string date = commitInfos[2].Trim();
                                            string message = commitInfos[3].Trim();
                                            currentRepoCommit = new RepoCommit(commitId, message, timestamp);
                                            currentRepoCommit.date = date;

                                            RepoCommitChanges changesObj = new RepoCommitChanges(0, 0);
                                            currentRepoCommit.changes.Add("__sftwTotal__", changesObj);
                                        }
                                    }
                                    else if (currentRepoCommit != null && hasPipe && !isBin)
                                    {
                                        // get the file and changes
                                        // i.e. somefile.cs                             | 20 +++++++++---------
                                        line = string.Join(" ", line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                                        string[] lineInfos = line.Split('|');
                                        if (lineInfos != null && lineInfos.Length > 1)
                                        {
                                            string file = lineInfos[0].Trim();
                                            string[] metricInfos = lineInfos[1].Trim().Split(' ');
                                            if (metricInfos != null && metricInfos.Length > 1)
                                            {
                                                string addAndDeletes = metricInfos[1].Trim();
                                                int len = addAndDeletes.Length;
                                                int lastPlusIdx = addAndDeletes.LastIndexOf('+');
                                                int insertions = 0;
                                                int deletions = 0;
                                                if (lastPlusIdx != -1)
                                                {
                                                    insertions = lastPlusIdx + 1;
                                                    deletions = len - insertions;
                                                }
                                                else if (len > 0)
                                                {
                                                    // all deletions
                                                    deletions = len;
                                                }

                                                if (!currentRepoCommit.changes.ContainsKey(file))
                                                {
                                                    RepoCommitChanges changesObj = new RepoCommitChanges(insertions, deletions);
                                                    currentRepoCommit.changes.Add(file, changesObj);
                                                }
                                                else
                                                {
                                                    RepoCommitChanges fileCommitChanges;
                                                    currentRepoCommit.changes.TryGetValue(file, out fileCommitChanges);
                                                    if (fileCommitChanges != null)
                                                    {
                                                        fileCommitChanges.deletions += deletions;
                                                        fileCommitChanges.insertions += insertions;
                                                    }
                                                }


                                                RepoCommitChanges totalRepoCommit;
                                                currentRepoCommit.changes.TryGetValue("__sftwTotal__", out totalRepoCommit);
                                                if (totalRepoCommit != null)
                                                {
                                                    totalRepoCommit.deletions += deletions;
                                                    totalRepoCommit.insertions += insertions;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if (currentRepoCommit != null)
                        {
                            repoCommits.Add(currentRepoCommit);
                        }

                        if (repoCommits != null && repoCommits.Count > 0)
                        {
                            // batch 10 at a time
                            int batch_size = 10;
                            List<RepoCommit> batch = new List<RepoCommit>();
                            for (int i = 0; i < repoCommits.Count; i++)
                            {
                                batch.Add(repoCommits[i]);
                                if (i > 0 && i % batch_size == 0)
                                {
                                    // send this batch.
                                    RepoCommitData commitData = new RepoCommitData(identifier, tag, branch, batch);

                                    string jsonContent = commitData.GetAsJson();// SimpleJson.SerializeObject(commitData);
                                                                                // send the members
                                    HttpResponseMessage response = await SoftwareHttpManager.SendRequestAsync(
                                        HttpMethod.Post, "/commits", jsonContent);

                                    if (SoftwareHttpManager.IsOk(response))
                                    {
                                        Logger.Info(response.ToString());
                                    }
                                    else
                                    {
                                        Logger.Error(response.ToString());
                                    }
                                }
                            }

                            if (batch.Count > 0)
                            {
                                RepoCommitData commitData = new RepoCommitData(identifier, tag, branch, batch);

                                string jsonContent = commitData.GetAsJson();// SimpleJson.SerializeObject(commitData);
                                                                            // send the members
                                HttpResponseMessage response = await SoftwareHttpManager.SendRequestAsync(
                                    HttpMethod.Post, "/commits", jsonContent);

                                if (SoftwareHttpManager.IsOk(response))
                                {
                                    Logger.Info(response.ToString());
                                }
                                else if (response != null)
                                {
                                    Logger.Error("Unable to complete commit request, status: " + response.StatusCode);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("GetHistoricalCommitsAsync ,error: " + ex.Message, ex);

            }

        }
    }
}
