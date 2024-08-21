using Nini.Config;
using log4net;
using System;
using System.Reflection;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using System.Collections.Generic;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.ServiceAuth;
using OpenSim.Framework.Servers.HttpServer;
using OpenMetaverse;
using System.Linq;

namespace OpenSim.Server.Handlers.Experience
{
    public class ExperienceServerPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IExperienceService m_service;

        public ExperienceServerPostHandler(IExperienceService service, IServiceAuth auth) :
                base("POST", "/experience", auth)
        {
            m_service = service;
        }

        protected override byte[] ProcessRequest(string path, Stream requestData,
                IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            string body;
            using(StreamReader sr = new StreamReader(requestData))
                body = sr.ReadToEnd();
            body = body.Trim();

            //m_log.InfoFormat("[EXPERIENCE POST HANDLER]: {0}", body);

            string method = string.Empty;

            try
            {
                Dictionary<string, object> request = ServerUtils.ParseQueryString(body);

                if (!request.ContainsKey("METHOD"))
                    return FailureResult();

                method = request["METHOD"].ToString();

                switch (method)
                {
                    case "getpermissions":
                        return GetPermissions(request);
                    case "updatepermission":
                        return UpdatePermission(request);
                    case "getexperienceinfos":
                        return GetExperienceInfos(request);
                    case "getagentexperiences":
                        return GetAgentExperiences(request);
                    case "updateexperienceinfo":
                        return UpdateExperienceInfo(request);
                    case "findexperiences":
                        return FindExperiences(request);
                    case "getgroupexperiences":
                        return GetGroupExperiences(request);
                    case "getexperiencesforgroups":
                        return GetExperiencesForGroups(request);
                    case "accesskvdatabase":
                        return AccessKeyValueDatabase(request);
                }
                m_log.DebugFormat("[EXPERIENCE HANDLER]: unknown method request: {0}", method);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[EXPERIENCE HANDLER]: Exception in method {0}: {1}", method, e);
            }

            return FailureResult();
        }

        private byte[] GetExperiencesForGroups(Dictionary<string, object> request)
        {
            List<UUID> groups = new List<UUID>();
            int i = 0;
            while (true)
            {
                string key = string.Format("id_{0}", i);
                if (request.ContainsKey(key) == false)
                    break;

                UUID group_id;

                if (!UUID.TryParse(request[key].ToString(), out group_id))
                    break;

                groups.Add(group_id);
                i++;
            }

            Dictionary<string, object> result = new Dictionary<string, object>();

            UUID[] experiences = m_service.GetExperiencesForGroups(groups.ToArray());

            i = 0;
            foreach (var id in experiences)
                result.Add("id_" + i++, id.ToString());

            string xmlString = ServerUtils.BuildXmlResponse(result);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        private byte[] GetGroupExperiences(Dictionary<string, object> request)
        {
            UUID group_id;

            if (!UUID.TryParse(request["GROUP"].ToString(), out group_id))
                return FailureResult();

            UUID[] experiences = m_service.GetGroupExperiences(group_id);
            Dictionary<string, object> result = new Dictionary<string, object>();

            int i = 0;
            foreach (var id in experiences)
                result.Add("id_" + i++, id.ToString());

            string xmlString = ServerUtils.BuildXmlResponse(result);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        private byte[] FindExperiences(Dictionary<string, object> request)
        {
            if (!request.ContainsKey("SEARCH"))
                return FailureResult();

            string search = request["SEARCH"].ToString();

            ExperienceInfo[] infos = m_service.FindExperiencesByName(search);

            Dictionary<string, object> result = new Dictionary<string, object>();
            if ((infos == null) || ((infos != null) && (infos.Length == 0)))
            {
                result["result"] = "null";
            }
            else
            {
                int n = 0;
                foreach (ExperienceInfo ex in infos)
                {
                    if (ex == null)
                        continue;
                    Dictionary<string, object> rinfoDict = ex.ToDictionary();
                    result["experience_" + n] = rinfoDict;
                    n++;
                }
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] GetPermissions(Dictionary<string, object> request)
        {
			UUID agent_id;
            if( !UUID.TryParse(request["agent_id"].ToString(), out agent_id))
                return FailureResult();

            Dictionary<UUID, bool> reply_data = m_service.FetchExperiencePermissions(agent_id);

            XmlDocument doc = new XmlDocument();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration, "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse", "");

            doc.AppendChild(rootElement);

            int i = 0;

            foreach(var pair in reply_data)
            {
                XmlElement key = doc.CreateElement("", string.Format("uuid_{0}", i), "");
                key.AppendChild(doc.CreateTextNode(pair.Key.ToString()));
                rootElement.AppendChild(key);

                XmlElement perm = doc.CreateElement("", string.Format("perm_{0}", i), "");
                perm.AppendChild(doc.CreateTextNode(pair.Value.ToString()));
                rootElement.AppendChild(perm);

                i++;
            }

            return Util.DocToBytes(doc);
        }

        byte[] GetAgentExperiences(Dictionary<string, object> request)
        {
            UUID agent_id;

            if (!UUID.TryParse(request["AGENT"].ToString(), out agent_id))
                return FailureResult();

            UUID[] experiences = m_service.GetAgentExperiences(agent_id);
            Dictionary<string, object> result = new Dictionary<string, object>();

            int i = 0;
            foreach (var id in experiences) 
                result.Add("id_" + i++, id.ToString());

            string xmlString = ServerUtils.BuildXmlResponse(result);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] UpdatePermission(Dictionary<string, object> request)
        {
            UUID agent_id;
            UUID experience;

            if (!UUID.TryParse(request["agent_id"].ToString(), out agent_id))
                return FailureResult();

            if (!UUID.TryParse(request["experience"].ToString(), out experience))
                return FailureResult();

            string perm = request["permission"].ToString();

            ExperiencePermission permissions = ExperiencePermission.None;
            if (perm == "allow") permissions = ExperiencePermission.Allowed;
            else if (perm == "block") permissions = ExperiencePermission.Blocked;

            return m_service.UpdateExperiencePermissions(agent_id, experience, permissions) ? SuccessResult() : FailureResult();
        }

        byte[] GetExperienceInfos(Dictionary<string, object> request)
        {
            List<UUID> experiences = new List<UUID>();
            int i = 0;
            while(true)
            {
                string key = string.Format("id_{0}", i);
                if (request.ContainsKey(key) == false)
                    break;

                UUID experience_id;

                if (!UUID.TryParse(request[key].ToString(), out experience_id))
                    break;

                experiences.Add(experience_id);
                i++;
            }

            ExperienceInfo[] infos = m_service.GetExperienceInfos(experiences.ToArray());

            Dictionary<string, object> result = new Dictionary<string, object>();
            if ((infos == null) || ((infos != null) && (infos.Length == 0)))
            {
                result["result"] = "null";
            }
            else
            {
                int n = 0;
                foreach (ExperienceInfo ex in infos)
                {
                    if (ex == null)
                        continue;
                    Dictionary<string, object> rinfoDict = ex.ToDictionary();
                    result["experience_" + n] = rinfoDict;
                    n++;
                }
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] UpdateExperienceInfo(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (request.ContainsKey("public_id"))
            {
                ExperienceInfo info = new ExperienceInfo(request);

                var updated = m_service.UpdateExpereienceInfo(info);
                if(updated != null)
                {
                    result = updated.ToDictionary();
                }
                else result["result"] = "failed";
            }
            else result["result"] = "failed";

            string xmlString = ServerUtils.BuildXmlResponse(result);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        private byte[] AccessKeyValueDatabase(Dictionary<string, object> request)
        {
            UUID experience_id;

            if (!UUID.TryParse(request["EXPERIENCE"].ToString(), out experience_id))
                return FailureResult();

            if (request.ContainsKey("ACTION") == false)
                return FailureResult();

            string action = request["ACTION"].ToString();

            Dictionary<string, object> result = new Dictionary<string, object>();

            if (action == "GET")
            {
                if (request.ContainsKey("KEY") == false)
                    return FailureResult();

                string key = request["KEY"].ToString();

                string get = m_service.GetKeyValue(experience_id, key);

                if(get != null)
                {
                    result.Add("result", "success");
                    result.Add("value", get);
                }
                else
                {
                    result.Add("result", "missing");
                }
            }
            else if (action == "CREATE")
            {
                if (request.ContainsKey("KEY") == false || request.ContainsKey("VALUE") == false)
                    return FailureResult();

                string key = request["KEY"].ToString();
                string val = request["VALUE"].ToString();

                string get = m_service.GetKeyValue(experience_id, key);

                if (get == null)
                {
                    result.Add("result", m_service.CreateKeyValue(experience_id, key, val));
                }
                else
                {
                    result.Add("result", "exists");
                }
            }
            else if (action == "UPDATE")
            {
                if (request.ContainsKey("KEY") == false || request.ContainsKey("VALUE") == false || request.ContainsKey("CHECK") == false)
                    return FailureResult();

                string key = request["KEY"].ToString();
                string val = request["VALUE"].ToString();
                bool check = request["CHECK"].ToString() == "TRUE";

                string original = string.Empty;
                if (check)
                {
                    if (request.ContainsKey("ORIGINAL") == false)
                        return FailureResult();
                    else
                        original = request["ORIGINAL"].ToString();
                }

                result.Add("result", m_service.UpdateKeyValue(experience_id, key, val, check, original));
            }
            else if (action == "DELETE")
            {
                if (request.ContainsKey("KEY") == false)
                    return FailureResult();

                string key = request["KEY"].ToString();

                result.Add("result", m_service.DeleteKey(experience_id, key));
            }
            else if (action == "COUNT")
            {
                int count = m_service.GetKeyCount(experience_id);
                result.Add("result", "success");
                result.Add("count", count);
            }
            else if (action == "GETKEYS")
            {
                if (request.ContainsKey("START") == false || request.ContainsKey("COUNT") == false)
                    return FailureResult();

                int start = int.Parse(request["START"].ToString());
                int count = int.Parse(request["COUNT"].ToString());

                string[] keys = m_service.GetKeys(experience_id, start, count);

                int i = 0;
                foreach (var str in keys)
                    result.Add("key_" + i++, str);
            }
            else if(action == "SIZE")
            {
                int size = m_service.GetSize(experience_id);
                result.Add("result", "success");
                result.Add("count", size);
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        private byte[] SuccessResult()
        {
            XmlDocument doc = new XmlDocument();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration, "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse", "");

            doc.AppendChild(rootElement);

            XmlElement result = doc.CreateElement("", "result", "");
            result.AppendChild(doc.CreateTextNode("Success"));

            rootElement.AppendChild(result);

            return Util.DocToBytes(doc);
        }

        private byte[] FailureResult()
        {
            XmlDocument doc = new XmlDocument();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration, "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse", "");

            doc.AppendChild(rootElement);

            XmlElement result = doc.CreateElement("", "result", "");
            result.AppendChild(doc.CreateTextNode("Failure"));

            rootElement.AppendChild(result);

            return Util.DocToBytes(doc);
        }
    }
}
