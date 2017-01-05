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

using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading;
using OpenMetaverse;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Client;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.PhysicsModules.SharedBase;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.CoreModules.Framework.EntityTransfer
{
    /// <summary>
    /// The possible states that an agent can be in when its being transferred between regions.
    /// </summary>
    /// <remarks>
    /// This is a state machine.
    ///
    /// [Entry]               => Preparing
    /// Preparing             => { Transferring || Cancelling || CleaningUp || Aborting || [Exit] }
    /// Transferring          => { ReceivedAtDestination || Cancelling || CleaningUp || Aborting }
    /// Cancelling            => CleaningUp || Aborting
    /// ReceivedAtDestination => CleaningUp || Aborting
    /// CleaningUp            => [Exit]
    /// Aborting              => [Exit]
    ///
    /// In other words, agents normally travel throwing Preparing => Transferring => ReceivedAtDestination => CleaningUp
    /// However, any state can transition to CleaningUp if the teleport has failed.
    /// </remarks>
    enum AgentTransferState
    {
        Preparing,              // The agent is being prepared for transfer
        Transferring,           // The agent is in the process of being transferred to a destination
        ReceivedAtDestination,  // The destination has notified us that the agent has been successfully received
        CleaningUp,             // The agent is being changed to child/removed after a transfer
        Cancelling,             // The user has cancelled the teleport but we have yet to act upon this.
        Aborting                // The transfer is aborting.  Unlike Cancelling, no compensating actions should be performed
    }

    /// <summary>
    /// Records the state of entities when they are in transfer within or between regions (cross or teleport).
    /// </summary>
    public class EntityTransferStateMachine
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly string LogHeader = "[ENTITY TRANSFER STATE MACHINE]";

        /// <summary>
        /// If true then on a teleport, the source region waits for a callback from the destination region.  If
        /// a callback fails to arrive within a set time then the user is pulled back into the source region.
        /// </summary>
        public bool EnableWaitForAgentArrivedAtDestination { get; set; }

        private EntityTransferModule m_mod;

        private Dictionary<UUID, AgentTransferState> m_agentsInTransit = new Dictionary<UUID, AgentTransferState>();

        public EntityTransferStateMachine(EntityTransferModule module)
        {
            m_mod = module;
        }

        /// <summary>
        /// Set that an agent is in transit.
        /// </summary>
        /// <param name='id'>The ID of the agent being teleported</param>
        /// <returns>true if the agent was not already in transit, false if it was</returns>
        internal bool SetInTransit(UUID id)
        {
//            m_log.DebugFormat("{0} SetInTransit. agent={1}, newState=Preparing", LogHeader, id);
            lock (m_agentsInTransit)
            {
                if (!m_agentsInTransit.ContainsKey(id))
                {
                    m_agentsInTransit[id] = AgentTransferState.Preparing;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Updates the state of an agent that is already in transit.
        /// </summary>
        /// <param name='id'></param>
        /// <param name='newState'></param>
        /// <returns></returns>
        /// <exception cref='Exception'>Illegal transitions will throw an Exception</exception>
        internal bool UpdateInTransit(UUID id, AgentTransferState newState)
        {
 //           m_log.DebugFormat("{0} UpdateInTransit. agent={1}, newState={2}", LogHeader, id, newState);

            bool transitionOkay = false;

            // We don't want to throw an exception on cancel since this can come it at any time.
            bool failIfNotOkay = true;

            // Should be a failure message if failure is not okay.
            string failureMessage = null;

            AgentTransferState? oldState = null;

            lock (m_agentsInTransit)
            {
                // Illegal to try and update an agent that's not actually in transit.
                if (!m_agentsInTransit.ContainsKey(id))
                {
                    if (newState != AgentTransferState.Cancelling && newState != AgentTransferState.Aborting)
                        failureMessage = string.Format(
                                "Agent with ID {0} is not registered as in transit in {1}",
                                id, m_mod.Scene.RegionInfo.RegionName);
                    else
                        failIfNotOkay = false;
                }
                else
                {
                    oldState = m_agentsInTransit[id];

                    if (newState == AgentTransferState.Aborting)
                    {
                        transitionOkay = true;
                    }
                    else if (newState == AgentTransferState.CleaningUp && oldState != AgentTransferState.CleaningUp)
                    {
                        transitionOkay = true;
                    }
                    else if (newState == AgentTransferState.Transferring && oldState == AgentTransferState.Preparing)
                    {
                        transitionOkay = true;
                    }
                    else if (newState == AgentTransferState.ReceivedAtDestination && oldState == AgentTransferState.Transferring)
                    {
                        transitionOkay = true;
                    }
                    else
                    {
                        if (newState == AgentTransferState.Cancelling
                            && (oldState == AgentTransferState.Preparing || oldState == AgentTransferState.Transferring))
                        {
                            transitionOkay = true;
                        }
                        else
                        {
                            failIfNotOkay = false;
                        }
                    }

                    if (!transitionOkay)
                        failureMessage
                            = string.Format(
                                "Agent with ID {0} is not allowed to move from old transit state {1} to new state {2} in {3}",
                                id, oldState, newState, m_mod.Scene.RegionInfo.RegionName);
                }

                if (transitionOkay)
                {
                    m_agentsInTransit[id] = newState;

//                    m_log.DebugFormat(
//                        "[ENTITY TRANSFER STATE MACHINE]: Changed agent with id {0} from state {1} to {2} in {3}",
//                        id, oldState, newState, m_mod.Scene.Name);
                }
                else if (failIfNotOkay)
                {
                    m_log.DebugFormat("{0} UpdateInTransit. Throwing transition failure = {1}", LogHeader, failureMessage);
                    throw new Exception(failureMessage);
                }
//                else
//                {
//                    if (oldState != null)
//                        m_log.DebugFormat(
//                            "[ENTITY TRANSFER STATE MACHINE]: Ignored change of agent with id {0} from state {1} to {2} in {3}",
//                            id, oldState, newState, m_mod.Scene.Name);
//                    else
//                        m_log.DebugFormat(
//                            "[ENTITY TRANSFER STATE MACHINE]: Ignored change of agent with id {0} to state {1} in {2} since agent not in transit",
//                            id, newState, m_mod.Scene.Name);
//                }
            }

            return transitionOkay;
        }

        /// <summary>
        /// Gets the current agent transfer state.
        /// </summary>
        /// <returns>Null if the agent is not in transit</returns>
        /// <param name='id'>
        /// Identifier.
        /// </param>
        internal AgentTransferState? GetAgentTransferState(UUID id)
        {
            lock (m_agentsInTransit)
            {
                if (!m_agentsInTransit.ContainsKey(id))
                    return null;
                else
                    return m_agentsInTransit[id];
            }
        }

        /// <summary>
        /// Removes an agent from the transit state machine.
        /// </summary>
        /// <param name='id'></param>
        /// <returns>true if the agent was flagged as being teleported when this method was called, false otherwise</returns>
        internal bool ResetFromTransit(UUID id)
        {
            lock (m_agentsInTransit)
            {
                if (m_agentsInTransit.ContainsKey(id))
                {
                    AgentTransferState state = m_agentsInTransit[id];

//                    if (state == AgentTransferState.Transferring || state == AgentTransferState.ReceivedAtDestination)
//                    {
                        // FIXME: For now, we allow exit from any state since a thrown exception in teleport is now guranteed
                        // to be handled properly - ResetFromTransit() could be invoked at any step along the process
//                        m_log.WarnFormat(
//                            "[ENTITY TRANSFER STATE MACHINE]: Agent with ID {0} should not exit directly from state {1}, should go to {2} state first in {3}",
//                            id, state, AgentTransferState.CleaningUp, m_mod.Scene.RegionInfo.RegionName);

//                        throw new Exception(
//                            "Agent with ID {0} cannot exit directly from state {1}, it must go to {2} state first",
//                            state, AgentTransferState.CleaningUp);
//                    }

                    m_agentsInTransit.Remove(id);

//                    m_log.DebugFormat(
//                        "[ENTITY TRANSFER STATE MACHINE]: Agent {0} cleared from transit in {1}",
//                        id, m_mod.Scene.RegionInfo.RegionName);

                    return true;
                }
            }

//            m_log.WarnFormat(
//                "[ENTITY TRANSFER STATE MACHINE]: Agent {0} requested to clear from transit in {1} but was already cleared",
//                id, m_mod.Scene.RegionInfo.RegionName);

            return false;
        }

        internal bool WaitForAgentArrivedAtDestination(UUID id)
        {
            if (!m_mod.WaitForAgentArrivedAtDestination)
                return true;

            lock (m_agentsInTransit)
            {
                AgentTransferState? currentState = GetAgentTransferState(id);

                if (currentState == null)
                    throw new Exception(
                        string.Format(
                            "Asked to wait for destination callback for agent with ID {0} in {1} but agent is not in transit",
                            id, m_mod.Scene.RegionInfo.RegionName));

                if (currentState != AgentTransferState.Transferring && currentState != AgentTransferState.ReceivedAtDestination)
                    throw new Exception(
                        string.Format(
                            "Asked to wait for destination callback for agent with ID {0} in {1} but agent is in state {2}",
                            id, m_mod.Scene.RegionInfo.RegionName, currentState));
            }

            int count = 400;

            // There should be no race condition here since no other code should be removing the agent transfer or
            // changing the state to another other than Transferring => ReceivedAtDestination.

            while (count-- > 0)
            {
                lock (m_agentsInTransit)
                {
                    if (m_agentsInTransit[id] == AgentTransferState.ReceivedAtDestination)
                        break;
                }

//                m_log.Debug("  >>> Waiting... " + count);
                Thread.Sleep(100);
            }

            return count > 0;
        }

        internal void SetAgentArrivedAtDestination(UUID id)
        {
            lock (m_agentsInTransit)
            {
                if (!m_agentsInTransit.ContainsKey(id))
                {
                    m_log.WarnFormat(
                        "[ENTITY TRANSFER STATE MACHINE]: Region {0} received notification of arrival in destination of agent {1} but no teleport request is active",
                        m_mod.Scene.RegionInfo.RegionName, id);

                    return;
                }

                AgentTransferState currentState = m_agentsInTransit[id];

                if (currentState == AgentTransferState.ReceivedAtDestination)
                {
                    // An anomoly but don't make this an outright failure - destination region could be overzealous in sending notification.
                    m_log.WarnFormat(
                        "[ENTITY TRANSFER STATE MACHINE]: Region {0} received notification of arrival in destination of agent {1} but notification has already previously been received",
                        m_mod.Scene.RegionInfo.RegionName, id);
                }
                else if (currentState != AgentTransferState.Transferring)
                {
                    m_log.ErrorFormat(
                        "[ENTITY TRANSFER STATE MACHINE]: Region {0} received notification of arrival in destination of agent {1} but agent is in state {2}",
                        m_mod.Scene.RegionInfo.RegionName, id, currentState);

                    return;
                }

                m_agentsInTransit[id] = AgentTransferState.ReceivedAtDestination;
            }
        }
    }
}
