using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;

using OpenSim.Services.Interfaces;

using OpenMetaverse;
using log4net;
using Nwc.XmlRpc;
using Nini.Config;

namespace OpenSim.Services.Connectors.Hypergrid
{
    public class HomeUsersSecurityServiceConnector : IHomeUsersSecurityService
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(
            MethodBase.GetCurrentMethod().DeclaringType);

        string m_ServerURL;
        public HomeUsersSecurityServiceConnector(string url)
        {
            m_ServerURL = url;
        }

        public HomeUsersSecurityServiceConnector(IConfigSource config)
        {
        }

        public void SetEndPoint(UUID sessionID, IPEndPoint ep)
        {
            Hashtable hash = new Hashtable();
            hash["sessionID"] = sessionID.ToString();
            hash["ep_addr"] = ep.Address.ToString();
            hash["ep_port"] = ep.Port.ToString();

            Call("ep_set",  hash);
        }

        public void RemoveEndPoint(UUID sessionID)
        {
            Hashtable hash = new Hashtable();
            hash["sessionID"] = sessionID.ToString();

            Call("ep_remove", hash);
        }

        public IPEndPoint GetEndPoint(UUID sessionID)
        {
            Hashtable hash = new Hashtable();
            hash["sessionID"] = sessionID.ToString();

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("ep_get", paramList);
            //m_log.Debug("[HGrid]: Linking to " + uri);
            XmlRpcResponse response = null;
            try
            {
                response = request.Send(m_ServerURL, 10000);
            }
            catch (Exception e)
            {
                m_log.Debug("[HGrid]: Exception " + e.Message);
                return null;
            }

            if (response.IsFault)
            {
                m_log.ErrorFormat("[HGrid]: remote call returned an error: {0}", response.FaultString);
                return null;
            }

            hash = (Hashtable)response.Value;
            //foreach (Object o in hash)
            //    m_log.Debug(">> " + ((DictionaryEntry)o).Key + ":" + ((DictionaryEntry)o).Value);
            try
            {
                bool success = false;
                Boolean.TryParse((string)hash["result"], out success);
                if (success)
                {
                    IPEndPoint ep = null;
                    int port = 0;
                    if (hash["ep_port"] != null)
                        Int32.TryParse((string)hash["ep_port"], out port);
                    if (hash["ep_addr"] != null)
                        ep = new IPEndPoint(IPAddress.Parse((string)hash["ep_addr"]), port);

                    return ep;
                }

            }
            catch (Exception e)
            {
                m_log.Error("[HGrid]: Got exception while parsing GetEndPoint response " + e.StackTrace);
                return null;
            }

            return null;
        }

        private void Call(string method, Hashtable hash)
        {
            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest(method, paramList);
            XmlRpcResponse response = null;
            try
            {
                response = request.Send(m_ServerURL, 10000);
            }
            catch (Exception e)
            {
                m_log.Debug("[HGrid]: Exception " + e.Message);
                return ;
            }

            if (response.IsFault)
            {
                m_log.ErrorFormat("[HGrid]: remote call returned an error: {0}", response.FaultString);
                return ;
            }

        }

    }
}
