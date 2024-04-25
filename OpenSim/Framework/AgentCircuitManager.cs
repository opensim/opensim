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
using System.Runtime.InteropServices;
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
        private readonly Dictionary<uint, AgentCircuitData> m_agentCircuits = new();

        /// <summary>
        /// Agent circuits indexed by agent UUID.
        /// </summary>
        private readonly Dictionary<UUID, AgentCircuitData> m_agentCircuitsByUUID = new();

        private readonly object m_lock = new();

        public virtual AuthenticateResponse AuthenticateSession(UUID sessionID, UUID agentID, uint circuitcode)
        {
            lock (m_lock)
            {
                if (m_agentCircuits.TryGetValue(circuitcode, out AgentCircuitData validcircuit) && validcircuit is not null)
                {
                    if (sessionID.Equals(validcircuit.SessionID) && agentID.Equals(validcircuit.AgentID))
                    {
                        return new AuthenticateResponse()
                        {
                            Authorised = true,
                            LoginInfo = new Login
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
                            }
                        };
                    }
                }
            }
            return new AuthenticateResponse();
        }

        /// <summary>
        /// Add information about a new circuit so that later on we can authenticate a new client session.
        /// </summary>
        /// <param name="circuitCode"></param>
        /// <param name="agentData"></param>
        public virtual void AddNewCircuit(AgentCircuitData agentData)
        {
            agentData.child = true;
            lock (m_lock)
            {
                ref AgentCircuitData acd = ref CollectionsMarshal.GetValueRefOrAddDefault(m_agentCircuits, agentData.circuitcode, out bool existed);
                if (existed && acd is not null && acd.AgentID.NotEqual(agentData.AgentID))
                    m_agentCircuitsByUUID.Remove(acd.AgentID);
                acd = agentData;
                m_agentCircuitsByUUID[agentData.AgentID] = agentData;
            }
        }

        public virtual void AddNewCircuit(uint circuitCode, AgentCircuitData agentData)
        {
            agentData.circuitcode = circuitCode;
            lock (m_lock)
            {
                ref AgentCircuitData acd = ref CollectionsMarshal.GetValueRefOrAddDefault(m_agentCircuits, agentData.circuitcode, out bool existed);
                if (existed && acd is not null && acd.AgentID.NotEqual(agentData.AgentID))
                    m_agentCircuitsByUUID.Remove(acd.AgentID);
                acd = agentData;
                m_agentCircuitsByUUID[agentData.AgentID] = agentData;
            }
        }

        public virtual void RemoveCircuit(uint circuitCode)
        {
            lock (m_lock)
            {
                if (m_agentCircuits.Remove(circuitCode, out AgentCircuitData ac))
                    m_agentCircuitsByUUID.Remove(ac.AgentID);
            }
        }

        public virtual void RemoveCircuit(UUID agentID)
        {
            lock (m_lock)
            {
                if (m_agentCircuitsByUUID.Remove(agentID, out AgentCircuitData ac))
                    m_agentCircuits.Remove(ac.circuitcode);
            }
        }

        public virtual void RemoveCircuit(AgentCircuitData ac)
        {
            lock (m_lock)
            {
                if (m_agentCircuitsByUUID.Remove(ac.AgentID, out AgentCircuitData byuuid))
                {
                    if (byuuid.circuitcode != ac.circuitcode)
                        m_agentCircuits.Remove(byuuid.circuitcode);
                }
                m_agentCircuits.Remove(ac.circuitcode);
            }
        }

        public AgentCircuitData GetAgentCircuitData(uint circuitCode)
        {
            lock (m_lock)
            {
                return m_agentCircuits.TryGetValue(circuitCode, out AgentCircuitData agentCircuit) ? agentCircuit : null;
            }
        }

        public AgentCircuitData GetAgentCircuitData(UUID agentID)
        {
            lock (m_lock)
            {
                return m_agentCircuitsByUUID.TryGetValue(agentID, out AgentCircuitData agentCircuit) ? agentCircuit : null;
            }
        }

        /// <summary>
        /// Get all current agent circuits indexed by agent UUID.
        /// </summary>
        /// <returns></returns>
        public Dictionary<UUID, AgentCircuitData> GetAgentCircuits()
        {
            lock (m_lock)
                return new Dictionary<UUID, AgentCircuitData>(m_agentCircuitsByUUID);
        }

        public void UpdateAgentData(AgentCircuitData agentData)
        {
            lock (m_lock)
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
        }

        /// <summary>
        /// Sometimes the circuitcode may not be known before setting up the connection
        /// </summary>
        /// <param name="circuitcode"></param>
        /// <param name="newcircuitcode"></param>
        public bool TryChangeCircuitCode(uint circuitcode, uint newcircuitcode)
        {
            lock (m_lock)
            {
                if (m_agentCircuits.ContainsKey(newcircuitcode))
                    return false;
                if (m_agentCircuits.Remove(circuitcode, out AgentCircuitData agentData))
                {
                    agentData.circuitcode = newcircuitcode;
                    m_agentCircuits[newcircuitcode] = agentData;
                    m_agentCircuitsByUUID[agentData.AgentID] = agentData;
                    return true;
                }
                return false;
            }
        }

        public void UpdateAgentChildStatus(uint circuitcode, bool childstatus)
        {
            lock (m_lock)
            {
                if (m_agentCircuits.TryGetValue(circuitcode, out AgentCircuitData ac))
                    ac.child = childstatus;
            }
        }

        public bool GetAgentChildStatus(uint circuitcode)
        {
            lock (m_lock)
            {
                return m_agentCircuits.TryGetValue(circuitcode, out AgentCircuitData ac) && ac.child;
            }
        }
    }
}