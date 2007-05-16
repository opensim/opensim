using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Types;

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

            this.AddNewCircuit(agent.circuitcode, agent);
        }
    }
}
