/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System.Collections.Generic;
using System.Collections.Concurrent;
using OpenMetaverse;

namespace OpenSim.Framework
{
    /// <summary>
    /// Manage client circuits
    /// </summary>
    public class AgentCircuitManager
    {
        /// <summary>
        /// Agent circuits indexed by circuit code.
        /// </summary>
        /// <remarks>
        /// We lock this for operations both on this dictionary and on m_agentCircuitsByUUID
        /// </remarks>
        private readonly ConcurrentDictionary<uint, AgentCircuitData> m_agentCircuits = new();

        /// <summary>
        /// Agent circuits indexed by agent UUID.
        /// </summary>
        private readonly ConcurrentDictionary<UUID, AgentCircuitData> m_agentCircuitsByUUID = new();

        public virtual AuthenticateResponse AuthenticateSession(UUID sessionID, UUID agentID, uint circuitcode)
        {
            AuthenticateResponse user = new();
            if (!m_agentCircuits.TryGetValue(circuitcode, out AgentCircuitData validcircuit) || validcircuit is null)
            {
                //don't have this circuit code in our list
                user.Authorised = false;
                return user;
            }

            if (sessionID.Equals(validcircuit.SessionID) && agentID.Equals(validcircuit.AgentID))
            {
                user.Authorised = true;
                user.LoginInfo = new Login
                {
                    Agent = agentID,
                    Session = sessionID,
                    SecureSession = validcircuit.SecureSessionID,
                    First = validcircuit.firstname,
                    Last = validcircuit.lastname,
                    InventoryFolder = validcircuit.InventoryFolder,
                    BaseFolder = validcircuit.BaseFolder,
                    StartPos = validcircuit.startpos,
                    StartFar = validcircuit.startfar
                };
            }
            else
            {
                // Invalid
                user.Authorised = false;
            }
            return user;
        }

        /// <summary>
        /// Add information about a new circuit so that later on we can authenticate a new client session.
        /// </summary>
        /// <param name="circuitCode"></param>
        /// <param name="agentData"></param>
        public virtual void AddNewCircuit(AgentCircuitData agentData)
        {
            agentData.child = true;
            RemoveCircuit(agentData.AgentID); // no duplications
            m_agentCircuits[agentData.circuitcode] = agentData;
            m_agentCircuitsByUUID[agentData.AgentID] = agentData;
        }

        public virtual void AddNewCircuit(uint circuitCode, AgentCircuitData agentData)
        {
            agentData.circuitcode = circuitCode;
            RemoveCircuit(agentData.AgentID); // no duplications
            m_agentCircuits[circuitCode] = agentData;
            m_agentCircuitsByUUID[agentData.AgentID] = agentData;
        }

        public virtual void RemoveCircuit(uint circuitCode)
        {
            if (m_agentCircuits.TryRemove(circuitCode, out AgentCircuitData ac))
            {
                m_agentCircuitsByUUID.TryRemove(ac.AgentID, out AgentCircuitData _);
            }
        }

        public virtual void RemoveCircuit(UUID agentID)
        {
            if (m_agentCircuitsByUUID.TryRemove(agentID, out AgentCircuitData ac))
            {
                m_agentCircuits.TryRemove(ac.circuitcode, out AgentCircuitData _);
            }
        }

        public virtual void RemoveCircuit(AgentCircuitData ac)
        {
            m_agentCircuitsByUUID.TryRemove(ac.AgentID, out AgentCircuitData byuuid);
            m_agentCircuits.TryRemove(ac.circuitcode, out AgentCircuitData _);
            if (byuuid is not null && byuuid.circuitcode != ac.circuitcode) //??
                m_agentCircuits.TryRemove(byuuid.circuitcode, out AgentCircuitData _);
        }

        public AgentCircuitData GetAgentCircuitData(uint circuitCode)
        {
            if(m_agentCircuits.TryGetValue(circuitCode, out AgentCircuitData agentCircuit))
                return agentCircuit;
            return null;
        }

        public AgentCircuitData GetAgentCircuitData(UUID agentID)
        {
            if(m_agentCircuitsByUUID.TryGetValue(agentID, out AgentCircuitData agentCircuit))
                return agentCircuit;
            return null;
        }

        /// <summary>
        /// Get all current agent circuits indexed by agent UUID.
        /// </summary>
        /// <returns></returns>
        public Dictionary<UUID, AgentCircuitData> GetAgentCircuits()
        {
            lock (m_agentCircuits)
                return new Dictionary<UUID, AgentCircuitData>(m_agentCircuitsByUUID);
        }

        public void UpdateAgentData(AgentCircuitData agentData)
        {
            if (m_agentCircuits.TryGetValue(agentData.circuitcode, out AgentCircuitData ac))
            {
                ac.firstname = agentData.firstname;
                ac.lastname = agentData.lastname;
                ac.startpos = agentData.startpos;
                ac.startfar = agentData.startfar;

                // Updated for when we don't know them before calling Scene.NewUserConnection
                ac.SecureSessionID = agentData.SecureSessionID;
                ac.SessionID = agentData.SessionID;
            }
        }

        /// <summary>
        /// Sometimes the circuitcode may not be known before setting up the connection
        /// </summary>
        /// <param name="circuitcode"></param>
        /// <param name="newcircuitcode"></param>
        public bool TryChangeCircuitCode(uint circuitcode, uint newcircuitcode)
        {
            if(m_agentCircuits.ContainsKey(newcircuitcode))
                return false;
            if (m_agentCircuits.TryRemove(circuitcode, out AgentCircuitData agentData))
            {
                agentData.circuitcode = newcircuitcode;
                m_agentCircuits[newcircuitcode] = agentData;
                m_agentCircuitsByUUID[agentData.AgentID] = agentData;
                return true;
            }
            return false;
        }

        public void UpdateAgentChildStatus(uint circuitcode, bool childstatus)
        {
            if (m_agentCircuits.TryGetValue(circuitcode, out AgentCircuitData ac))
                ac.child = childstatus;
        }

        public bool GetAgentChildStatus(uint circuitcode)
        {
            if (m_agentCircuits.TryGetValue(circuitcode, out AgentCircuitData ac))
                return ac.child;
            return false;
        }
    }
}