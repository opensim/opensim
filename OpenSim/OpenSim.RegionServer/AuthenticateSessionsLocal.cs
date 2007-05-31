using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework.Types;
using OpenSim.Framework;

namespace OpenSim
{
    public class AuthenticateSessionsLocal : AuthenticateSessionsBase
    {
        public AuthenticateSessionsLocal()
        {

        }

        public void AddNewSession(Login loginData)
        {
            AgentCircuitData agent = new AgentCircuitData();
            agent.AgentID = loginData.Agent;
            agent.firstname = loginData.First;
            agent.lastname = loginData.Last;
            agent.SessionID = loginData.Session;
            agent.SecureSessionID = loginData.SecureSession;
            agent.circuitcode = loginData.CircuitCode;
            agent.BaseFolder = loginData.BaseFolder;
            agent.InventoryFolder = loginData.InventoryFolder;
            agent.startpos = new LLVector3(128,128,70);
            this.AddNewCircuit(agent.circuitcode, agent);
        }
    }
}
