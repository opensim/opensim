using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;

namespace OpenSim
{
    public class AuthenticateSessionsBase
    {
        private Dictionary<uint, AgentCircuitData> AgentCircuits = new Dictionary<uint, AgentCircuitData>();

        public AuthenticateSessionsBase()
        {

        }

        public virtual AuthenticateResponse AuthenticateSession(LLUUID sessionID, LLUUID agentID, uint circuitcode)
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
                user.Authorised = true;
                user.LoginInfo = new Login();
                user.LoginInfo.Agent = agentID;
                user.LoginInfo.Session = sessionID;
                user.LoginInfo.SecureSession = validcircuit.SecureSessionID;
                user.LoginInfo.First = validcircuit.firstname;
                user.LoginInfo.Last = validcircuit.lastname;
                user.LoginInfo.InventoryFolder = validcircuit.InventoryFolder;
                user.LoginInfo.BaseFolder = validcircuit.BaseFolder;
            }
            else
            {
                // Invalid
                user.Authorised = false;
            }

            return (user);
        }

        public virtual void AddNewCircuit(uint circuitCode, AgentCircuitData agentData)
        {
            if (this.AgentCircuits.ContainsKey(circuitCode))
            {
                this.AgentCircuits[circuitCode] = agentData;
            }
            else
            {
                this.AgentCircuits.Add(circuitCode, agentData);
            }
        }

        public LLVector3 GetPosition(uint circuitCode)
        {
            LLVector3 vec = new LLVector3();
            if (this.AgentCircuits.ContainsKey(circuitCode))
            {
                vec = this.AgentCircuits[circuitCode].startpos;
            }
            return vec;
        }
    }
}
