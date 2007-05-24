/*
* Copyright (c) OpenSim project, http://sim.opensecondlife.org/
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY <copyright holder> ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;
using libsecondlife;
using Nwc.XmlRpc;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;

namespace OpenSim.GridInterfaces.Remote
{
    public class RemoteGridServer : RemoteGridBase
    {
        private string GridServerUrl;
        private string GridSendKey;
        private string GridRecvKey;
        private Dictionary<uint, AgentCircuitData> AgentCircuits = new Dictionary<uint, AgentCircuitData>();
        private ArrayList simneighbours = new ArrayList();
        private Hashtable griddatahash;

        public override Dictionary<uint, AgentCircuitData> agentcircuits
        {
            get { return AgentCircuits; }
            set { AgentCircuits = value; }
        }

        public override ArrayList neighbours
        {
            get { return simneighbours; }
            set { simneighbours = value; }
        }

        public override Hashtable GridData
        {
            get { return griddatahash; }
            set { griddatahash = value; }
        }


        public RemoteGridServer()
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Remote Grid Server class created");
        }

        public override bool RequestConnection(LLUUID SimUUID, string sim_ip, uint sim_port)
        {
            Hashtable GridParams = new Hashtable();
            GridParams["authkey"] = GridSendKey;
            GridParams["UUID"] = SimUUID.ToString();
            GridParams["sim_ip"] = sim_ip;
            GridParams["sim_port"] = sim_port.ToString();
            ArrayList SendParams = new ArrayList();
            SendParams.Add(GridParams);

            XmlRpcRequest GridReq = new XmlRpcRequest("simulator_login", SendParams);
            XmlRpcResponse GridResp = GridReq.Send(this.GridServerUrl, 3000);
            Hashtable GridRespData = (Hashtable)GridResp.Value;
            this.griddatahash = GridRespData;

            if (GridRespData.ContainsKey("error"))
            {
                string errorstring = (string)GridRespData["error"];
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, "Error connecting to grid:");
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, errorstring);
                return false;
            }
            this.neighbours = (ArrayList)GridRespData["neighbours"];
            Console.WriteLine(simneighbours.Count);
            return true;
        }

        public override AuthenticateResponse AuthenticateSession(LLUUID sessionID, LLUUID agentID, uint circuitcode)
        {
            AgentCircuitData validcircuit = null;
            if (this.AgentCircuits.ContainsKey(circuitcode))
            {
                validcircuit = this.AgentCircuits[circuitcode];
            }
            AuthenticateResponse user = new AuthenticateResponse();
            if (validcircuit == null)
            {
                //don't have this circuit code in our list
                user.Authorised = false;
                return (user);
            }

            if ((sessionID == validcircuit.SessionID) && (agentID == validcircuit.AgentID))
            {
                // YAY! Valid login
                user.Authorised = true;
                user.LoginInfo = new Login();
                user.LoginInfo.Agent = agentID;
                user.LoginInfo.Session = sessionID;
                user.LoginInfo.SecureSession = validcircuit.SecureSessionID;
                user.LoginInfo.First = validcircuit.firstname;
                user.LoginInfo.Last = validcircuit.lastname;
            }
            else
            {
                // Invalid
                user.Authorised = false;
            }

            return (user);
        }

        public override bool LogoutSession(LLUUID sessionID, LLUUID agentID, uint circuitCode)
        {
            WebRequest DeleteSession = WebRequest.Create(GridServerUrl + "/usersessions/" + sessionID.ToString());
            DeleteSession.Method = "DELETE";
            DeleteSession.ContentType = "text/plaintext";
            DeleteSession.ContentLength = 0;

            StreamWriter stOut = new StreamWriter(DeleteSession.GetRequestStream(), System.Text.Encoding.ASCII);
            stOut.Write("");
            stOut.Close();

            StreamReader stIn = new StreamReader(DeleteSession.GetResponse().GetResponseStream());
            string GridResponse = stIn.ReadToEnd();
            stIn.Close();
            return (true);
        }

        public override UUIDBlock RequestUUIDBlock()
        {
            UUIDBlock uuidBlock = new UUIDBlock();
            return (uuidBlock);
        }

        public override NeighbourInfo[] RequestNeighbours()
        {
            return null;
        }

        public override IList RequestMapBlocks(int minX, int minY, int maxX, int maxY)
        {
            Hashtable param = new Hashtable();
            param["xmin"] = minX;
            param["ymin"] = minY;
            param["xmax"] = maxX;
            param["ymax"] = maxY;
            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("map_block", parameters);
            XmlRpcResponse resp = req.Send(GridServerUrl, 3000);
            Hashtable respData = (Hashtable)resp.Value;
            return (IList)respData["sim-profiles"];
        }

        public override void SetServerInfo(string ServerUrl, string SendKey, string RecvKey)
        {
            this.GridServerUrl = ServerUrl;
            this.GridSendKey = SendKey;
            this.GridRecvKey = RecvKey;
        }

        public override string GetName()
        {
            return "Remote";
        }

        public override void Close()
        {

        }
    }

    public class RemoteGridPlugin : IGridPlugin
    {
        public RemoteGridPlugin()
        {

        }

        public IGridServer GetGridServer()
        {
            return (new RemoteGridServer());
        }
    }

}
