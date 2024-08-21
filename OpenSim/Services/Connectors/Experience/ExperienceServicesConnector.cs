using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;

using OpenSim.Framework.ServiceAuth;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Server.Base;
using OpenMetaverse;
using System.Security.AccessControl;
using OpenSim.Data;
using System.Linq;

namespace OpenSim.Services.Connectors
{
    public class ExperienceServicesConnector : BaseServiceConnector, IExperienceService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURI = String.Empty;

        public ExperienceServicesConnector()
        {
        }

        public ExperienceServicesConnector(string serverURI)
        {
            m_ServerURI = serverURI.TrimEnd('/') + "/experience";
        }

        public ExperienceServicesConnector(IConfigSource source)
        {
            Initialise(source);
        }

        public virtual void Initialise(IConfigSource source)
        {
            IConfig gridConfig = source.Configs["ExperienceService"];
            if (gridConfig == null)
            {
                m_log.Error("[EXPERIENCE CONNECTOR]: ExperienceService missing from configuration");
                throw new Exception("Experience connector init error");
            }

            string serviceURI = gridConfig.GetString("ExperienceServerURI",
                    String.Empty);

            if (serviceURI == String.Empty)
            {
                m_log.Error("[EXPERIENCE CONNECTOR]: No Server URI named in section GridUserService");
                throw new Exception("Experience connector init error");
            }
            m_ServerURI = serviceURI + "/experience";
            base.Initialise(source, "ExperienceService");
        }

        #region IExperienceService
        
        public Dictionary<UUID, bool> FetchExperiencePermissions(UUID agent_id)
        {
            //m_log.InfoFormat("[ExperienceServiceConnector]: FetchExperiencePermissions for {0}", agent_id);

            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["METHOD"] = "getpermissions";
            sendData["agent_id"] = agent_id.ToString();

            string request_str = ServerUtils.BuildQueryString(sendData);

            Dictionary<UUID, bool> experiences = new Dictionary<UUID, bool>();

            string reply = SynchronousRestFormsRequester.MakeRequest("POST", m_ServerURI, request_str, m_Auth);
            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                int iter = 0;
                while(true)
                {
                    string key = string.Format("uuid_{0}", iter);
                    string perm = string.Format("perm_{0}", iter);

                    if (replyData.ContainsKey(key) && replyData.ContainsKey(perm))
                    {
                        UUID experience_id;
                        if (UUID.TryParse(replyData[key].ToString(), out experience_id))
                        {
                            bool allow = bool.Parse(replyData[perm].ToString());

                            experiences.Add(experience_id, allow);

                            //m_log.InfoFormat("[EXPERIENCE SERVICE CONNECTOR]: {0} = {1}", experience_id, allow);
                        }
                    }
                    else break;

                    iter++;
                }
            }

            return experiences;
        }

        public bool UpdateExperiencePermissions(UUID agent_id, UUID experience, ExperiencePermission perm)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["METHOD"] = "updatepermission";
            sendData["agent_id"] = agent_id.ToString();
            sendData["experience"] = experience.ToString();
            sendData["permission"] = perm == ExperiencePermission.None ? "forget" : perm == ExperiencePermission.Allowed ? "allow" : "block";

            string request_str = ServerUtils.BuildQueryString(sendData);

            return doSimplePost(request_str, "updatepermission");
        }

        public ExperienceInfo[] GetExperienceInfos(UUID[] experiences)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["METHOD"] = "getexperienceinfos";
            int i = 0;
            foreach(UUID id in experiences)
            {
                sendData[string.Format("id_{0}", i)] = id.ToString();
                i++;
            }

            string request_str = ServerUtils.BuildQueryString(sendData);

            List<ExperienceInfo> infos = new List<ExperienceInfo>();

            string reply = SynchronousRestFormsRequester.MakeRequest("POST", m_ServerURI, request_str, m_Auth);

            //m_log.InfoFormat("[EXPERIENCE SERVICE CONNECTOR]: Reply: {0}", reply);

            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                Dictionary<string, object>.ValueCollection experienceList = replyData.Values;

                foreach (object ex in experienceList)
                {
                    if (ex is Dictionary<string, object>)
                    {
                        Dictionary<string, object> experience = (Dictionary<string, object>)ex;
                        infos.Add(new ExperienceInfo(experience));
                    }
                }
            }

            return infos.ToArray();
        }

        #endregion IExperienceService

        private bool doSimplePost(string reqString, string meth)
        {
            try
            {
                string reply = SynchronousRestFormsRequester.MakeRequest("POST", m_ServerURI, reqString, m_Auth);
                if (reply != string.Empty)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                    if (replyData.ContainsKey("result"))
                    {
                        if (replyData["result"].ToString().ToLower() == "success")
                            return true;
                        else
                            return false;
                    }
                    else
                        m_log.DebugFormat("[EXPERIENCE CONNECTOR]: {0} reply data does not contain result field", meth);
                }
                else
                    m_log.DebugFormat("[EXPERIENCE CONNECTOR]: {0} received empty reply", meth);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[EXPERIENCE CONNECTOR]: Exception when contacting server at {0}: {1}", m_ServerURI, e.Message);
            }

            return false;
        }

        public UUID[] GetAgentExperiences(UUID agent_id)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["METHOD"] = "getagentexperiences";
            sendData["AGENT"] = agent_id.ToString();

            string request_str = ServerUtils.BuildQueryString(sendData);

            List<ExperienceInfo> infos = new List<ExperienceInfo>();

            string reply = SynchronousRestFormsRequester.MakeRequest("POST", m_ServerURI, request_str, m_Auth);

            //m_log.InfoFormat("[EXPERIENCE SERVICE CONNECTOR]: Reply: {0}", reply);

            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
                if(replyData != null)
                {
                    Dictionary<string, object>.ValueCollection experienceList = replyData.Values;

                    return experienceList.Select(x => UUID.Parse(x.ToString())).ToArray();
                }
            }

            return new UUID[0];
        }

        public ExperienceInfo UpdateExpereienceInfo(ExperienceInfo info)
        {
            // let's just pray they never add a parameter named "method"
            Dictionary<string, object> sendData = info.ToDictionary();
            sendData["METHOD"] = "updateexperienceinfo";

            string request_str = ServerUtils.BuildQueryString(sendData);

            string reply = SynchronousRestFormsRequester.MakeRequest("POST", m_ServerURI, request_str, m_Auth);

            //m_log.InfoFormat("[EXPERIENCE SERVICE CONNECTOR]: UpdateExpereienceInfo Reply: {0}", reply);

            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                ExperienceInfo responseInfo = new ExperienceInfo(replyData);
                return responseInfo;
            }

            return null;
        }

        public ExperienceInfo[] FindExperiencesByName(string search)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["METHOD"] = "findexperiences";
            sendData["SEARCH"] = search;

            string request_str = ServerUtils.BuildQueryString(sendData);

            List<ExperienceInfo> infos = new List<ExperienceInfo>();

            string reply = SynchronousRestFormsRequester.MakeRequest("POST", m_ServerURI, request_str, m_Auth);
            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                Dictionary<string, object>.ValueCollection experienceList = replyData.Values;

                foreach (object ex in experienceList)
                {
                    if (ex is Dictionary<string, object>)
                    {
                        Dictionary<string, object> experience = (Dictionary<string, object>)ex;
                        infos.Add(new ExperienceInfo(experience));
                    }
                }
            }

            return infos.ToArray();
        }

        public UUID[] GetGroupExperiences(UUID group_id)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["METHOD"] = "getgroupexperiences";
            sendData["GROUP"] = group_id.ToString();

            string request_str = ServerUtils.BuildQueryString(sendData);

            List<ExperienceInfo> infos = new List<ExperienceInfo>();

            string reply = SynchronousRestFormsRequester.MakeRequest("POST", m_ServerURI, request_str, m_Auth);
            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
                if (replyData != null)
                {
                    Dictionary<string, object>.ValueCollection experienceList = replyData.Values;

                    return experienceList.Select(x => UUID.Parse(x.ToString())).ToArray();
                }
            }

            return new UUID[0];
        }

        public UUID[] GetExperiencesForGroups(UUID[] groups)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["METHOD"] = "getexperiencesforgroups";

            int i = 0;
            foreach(var id in groups)
            {
                sendData["id_" + i] = id.ToString();
                i++;
            }

            string request_str = ServerUtils.BuildQueryString(sendData);

            string reply = SynchronousRestFormsRequester.MakeRequest("POST", m_ServerURI, request_str, m_Auth);
            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
                if (replyData != null)
                {
                    Dictionary<string, object>.ValueCollection experienceList = replyData.Values;

                    return experienceList.Select(x => UUID.Parse(x.ToString())).ToArray();
                }
            }

            return new UUID[0];
        }

        public string GetKeyValue(UUID experience, string key)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["METHOD"] = "accesskvdatabase";
            sendData["ACTION"] = "GET";
            sendData["EXPERIENCE"] = experience.ToString();
            sendData["KEY"] = key;

            string request_str = ServerUtils.BuildQueryString(sendData);

            string reply = SynchronousRestFormsRequester.MakeRequest("POST", m_ServerURI, request_str, m_Auth);
            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
                if (replyData != null)
                {
                    if(replyData.ContainsKey("result"))
                    {
                        if(replyData["result"].ToString() == "success")
                        {
                            if (replyData.ContainsKey("value"))
                            {
                                return replyData["value"].ToString();
                            }
                        }
                    }
                }
            }

            return null;
        }

        public string CreateKeyValue(UUID experience, string key, string value)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["METHOD"] = "accesskvdatabase";
            sendData["ACTION"] = "CREATE";
            sendData["EXPERIENCE"] = experience.ToString();
            sendData["KEY"] = key;
            sendData["VALUE"] = value;

            string request_str = ServerUtils.BuildQueryString(sendData);

            string reply = SynchronousRestFormsRequester.MakeRequest("POST", m_ServerURI, request_str, m_Auth);
            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
                if (replyData != null)
                {
                    if (replyData.ContainsKey("result"))
                    {
                        return replyData["result"].ToString();
                    }
                }
            }

            return "error";
        }

        public string UpdateKeyValue(UUID experience, string key, string val, bool check, string original)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["METHOD"] = "accesskvdatabase";
            sendData["ACTION"] = "UPDATE";
            sendData["EXPERIENCE"] = experience.ToString();
            sendData["KEY"] = key;
            sendData["VALUE"] = val;
            sendData["CHECK"] = check ? "TRUE" : "FALSE";
            sendData["ORIGINAL"] = check ? original : string.Empty;

            string request_str = ServerUtils.BuildQueryString(sendData);

            string reply = SynchronousRestFormsRequester.MakeRequest("POST", m_ServerURI, request_str, m_Auth);
            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
                if (replyData != null)
                {
                    if (replyData.ContainsKey("result"))
                    {
                        return replyData["result"].ToString();
                    }
                }
            }

            return "error";
        }

        public string DeleteKey(UUID experience, string key)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["METHOD"] = "accesskvdatabase";
            sendData["ACTION"] = "DELETE";
            sendData["EXPERIENCE"] = experience.ToString();
            sendData["KEY"] = key;

            string request_str = ServerUtils.BuildQueryString(sendData);

            string reply = SynchronousRestFormsRequester.MakeRequest("POST", m_ServerURI, request_str, m_Auth);
            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
                if (replyData != null)
                {
                    if (replyData.ContainsKey("result"))
                    {
                        return replyData["result"].ToString();
                    }
                }
            }

            return "error";
        }

        public int GetKeyCount(UUID experience)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["METHOD"] = "accesskvdatabase";
            sendData["ACTION"] = "COUNT";
            sendData["EXPERIENCE"] = experience.ToString();

            string request_str = ServerUtils.BuildQueryString(sendData);

            string reply = SynchronousRestFormsRequester.MakeRequest("POST", m_ServerURI, request_str, m_Auth);
            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
                if (replyData != null)
                {
                    if (replyData.ContainsKey("result"))
                    {
                        if(replyData["result"].ToString() == "success")
                        {
                            if (replyData.ContainsKey("count"))
                            {
                                return int.Parse(replyData["count"].ToString());
                            }
                        }
                    }
                }
            }

            return 0;
        }

        public string[] GetKeys(UUID experience, int start, int count)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["METHOD"] = "accesskvdatabase";
            sendData["ACTION"] = "GETKEYS";
            sendData["EXPERIENCE"] = experience.ToString();
            sendData["START"] = start.ToString();
            sendData["COUNT"] = count.ToString();

            string request_str = ServerUtils.BuildQueryString(sendData);

            string reply = SynchronousRestFormsRequester.MakeRequest("POST", m_ServerURI, request_str, m_Auth);

            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
                if (replyData != null)
                {
                    Dictionary<string, object>.ValueCollection keyList = replyData.Values;

                    return keyList.Select(x => x.ToString()).ToArray();
                }
            }

            return new string[0];
        }

        public int GetSize(UUID experience)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["METHOD"] = "accesskvdatabase";
            sendData["ACTION"] = "SIZE";
            sendData["EXPERIENCE"] = experience.ToString();

            string request_str = ServerUtils.BuildQueryString(sendData);

            string reply = SynchronousRestFormsRequester.MakeRequest("POST", m_ServerURI, request_str, m_Auth);
            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
                if (replyData != null)
                {
                    if (replyData.ContainsKey("result"))
                    {
                        if (replyData["result"].ToString() == "success")
                        {
                            if (replyData.ContainsKey("count"))
                            {
                                return int.Parse(replyData["count"].ToString());
                            }
                        }
                    }
                }
            }

            return 0;
        }
    }
}
