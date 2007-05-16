using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using libsecondlife;
using OpenSim.Framework.Types;
using Nwc.XmlRpc;

namespace OpenSim
{
    public class AuthenticateSessionsRemote : AuthenticateSessionsBase
    {
        public AuthenticateSessionsRemote()
        {

        }

        public XmlRpcResponse ExpectUser(XmlRpcRequest request)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            AgentCircuitData agentData = new AgentCircuitData();
            agentData.SessionID = new LLUUID((string)requestData["session_id"]);
            agentData.SecureSessionID = new LLUUID((string)requestData["secure_session_id"]);
            agentData.firstname = (string)requestData["firstname"];
            agentData.lastname = (string)requestData["lastname"];
            agentData.AgentID = new LLUUID((string)requestData["agent_id"]);
            agentData.circuitcode = Convert.ToUInt32(requestData["circuit_code"]);
            if (requestData.ContainsKey("child_agent") && requestData["child_agent"].Equals("1"))
            {
                agentData.child = true;
            }
            else
            {
                agentData.startpos = new LLVector3(Convert.ToUInt32(requestData["startpos_x"]), Convert.ToUInt32(requestData["startpos_y"]), Convert.ToUInt32(requestData["startpos_z"]));
                agentData.child = false;
            }

            this.AddNewCircuit(agentData.circuitcode, agentData);

            return new XmlRpcResponse();
        }
    }
}
