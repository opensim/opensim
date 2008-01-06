using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using libsecondlife;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;

namespace OpenSim.Grid.UserServer
{

    public class MessageServersConnector
    {
        private LogBase m_log;
        public Dictionary<string, MessageServerInfo> MessageServers;
        
        public MessageServersConnector(LogBase log)
        {
            m_log=log;
            MessageServers = new Dictionary<string, MessageServerInfo>();
        }
        
        public void RegisterMessageServer(string URI, MessageServerInfo serverData)
        {
            MessageServers.Add(URI, serverData);
        }
        
        public void DeRegisterMessageServer(string URI)
        {
            MessageServers.Remove(URI);
        }
        
        public void AddResponsibleRegion(string URI, ulong regionhandle)
        {
            if (!MessageServers.ContainsKey(URI))
            {
                m_log.Warn("MSGSERVER", "Got addResponsibleRegion Request for a MessageServer that isn't registered");
            }
            else
            {
                MessageServerInfo msginfo = MessageServers["URI"];
                msginfo.responsibleForRegions.Add(regionhandle);
                MessageServers["URI"] = msginfo;
            }   
        }
        public void RemoveResponsibleRegion(string URI, ulong regionhandle)
        {
            if (!MessageServers.ContainsKey(URI))
            {
                m_log.Warn("MSGSERVER", "Got RemoveResponsibleRegion Request for a MessageServer that isn't registered");
            }
            else
            {
                MessageServerInfo msginfo = MessageServers["URI"];
                if (msginfo.responsibleForRegions.Contains(regionhandle))
                {
                    msginfo.responsibleForRegions.Remove(regionhandle);
                    MessageServers["URI"] = msginfo;
                }
            }   

        }
        public XmlRpcResponse XmlRPCRegisterMessageServer(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();

            if (requestData.Contains("uri"))
            {
                string URI = (string)requestData["uri"];
                string sendkey=(string)requestData["sendkey"];
                string recvkey=(string)requestData["recvkey"];
                MessageServerInfo m = new MessageServerInfo();
                m.URI = URI;
                m.sendkey = sendkey;
                m.recvkey = recvkey;
                RegisterMessageServer(URI, m);
                responseData["responsestring"] = "TRUE";
                response.Value = responseData;
            }
            return response;
        }
        public XmlRpcResponse XmlRPCDeRegisterMessageServer(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();

            if (requestData.Contains("uri"))
            {
                string URI = (string)requestData["URI"];

                DeRegisterMessageServer(URI);
                responseData["responsestring"] = "TRUE";
                response.Value = responseData;
            }
            return response;
        }
        public XmlRpcResponse XmlRPCUserMovedtoRegion(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();

            if (requestData.Contains("fromuri"))
            {
                string sURI = (string)requestData["fromuri"];
                string sagentID = (string)requestData["agentid"];
                string ssessionID = (string)requestData["sessionid"];
                string scurrentRegionID = (string)requestData["regionid"];
                string sregionhandle = (string)requestData["regionhandle"];
                string scurrentpos = (string)requestData["currentpos"];
                //LLVector3.TryParse((string)reader["currentPos"], out retval.currentPos);
                // TODO: Okay now raise event so the user server can pass this data to the Usermanager
                
                responseData["responsestring"] = "TRUE";
                response.Value = responseData;
            }
            return response;
        }

        public void TellMessageServersAboutUser(LLUUID agentID, LLUUID sessionID, LLUUID RegionID, ulong regionhandle, LLVector3 Position)
        {
            // Loop over registered Message Servers ( AND THERE WILL BE MORE THEN ONE :D )
            foreach (MessageServerInfo serv in MessageServers.Values)
            {
                NotifyMessageServerAboutUser(serv, agentID, sessionID, RegionID, regionhandle, Position);
            }
        }

        private void NotifyMessageServerAboutUser(MessageServerInfo serv, LLUUID agentID, LLUUID sessionID, LLUUID RegionID, ulong regionhandle, LLVector3 Position)
        {
            Hashtable reqparams = new Hashtable();
            reqparams["sendkey"] = serv.sendkey;
            reqparams["agentid"] = agentID.ToString();
            reqparams["sessionid"] = sessionID.ToString();
            reqparams["regionid"] = RegionID.ToString();
            reqparams["regionhandle"] = regionhandle.ToString();
            reqparams["position"] = Position.ToString();

            ArrayList SendParams = new ArrayList();
            SendParams.Add(reqparams);

            XmlRpcRequest GridReq = new XmlRpcRequest("login_to_simulator", SendParams);
            XmlRpcResponse GridResp = GridReq.Send(serv.URI, 6000);
            m_log.Verbose("LOGIN","Notified : " + serv.URI + " about user login");

        }


    }
}
