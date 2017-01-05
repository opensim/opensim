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

namespace OpenSim.Region.Framework.Scenes
{
    /// <summary>
    /// The possible states that a scene presence can be in.  This is currently orthagonal to whether a scene presence
    /// is root or child.
    /// </summary>
    /// <remarks>
    /// This is a state machine.
    ///
    /// [Entry]               => Running
    /// Running               => PreRemove, Removing
    /// PreRemove             => Running, Removing
    /// Removing              => Removed
    ///
    /// All other methods should only see the scene presence in running state - this is the normal operational state
    /// Removed state occurs when the presence has been removed.  This is the end state with no exit.
    /// </remarks>
    public enum ScenePresenceState
    {
        Running,                // Normal operation state.  The scene presence is available.
        PreRemove,              // The presence is due to be removed but can still be returning to running.
        Removing,               // The presence is in the process of being removed from the scene via Scene.RemoveClient.
        Removed,                // The presence has been removed from the scene and is effectively dead.
                                // There is no exit from this state.
    }

    internal class ScenePresenceStateMachine
    {
        private ScenePresence m_sp;
        private ScenePresenceState m_state;

        internal ScenePresenceStateMachine(ScenePresence sp)
        {
            m_sp = sp;
            m_state = ScenePresenceState.Running;
        }

        internal ScenePresenceState GetState()
        {
            return m_state;
        }

        /// <summary>
        /// Updates the state of an agent that is already in transit.
        /// </summary>
        /// <param name='id'></param>
        /// <param name='newState'></param>
        /// <returns></returns>
        /// <exception cref='Exception'>Illegal transitions will throw an Exception</exception>
        internal void SetState(ScenePresenceState newState)
        {
            bool transitionOkay = false;

            lock (this)
            {
                if (newState == m_state)
                    return;
                else if (newState == ScenePresenceState.Running && m_state == ScenePresenceState.PreRemove)
                    transitionOkay = true;
                else if (newState == ScenePresenceState.PreRemove && m_state == ScenePresenceState.Running)
                    transitionOkay = true;
                else if (newState == ScenePresenceState.Removing)
                {
                    if (m_state == ScenePresenceState.Running || m_state == ScenePresenceState.PreRemove)
                        transitionOkay = true;
                }
                else if (newState == ScenePresenceState.Removed && m_state == ScenePresenceState.Removing)
                    transitionOkay = true;
            }

            if (!transitionOkay)
            {
                throw new Exception(
                    string.Format(
                        "Scene presence {0} is not allowed to move from state {1} to new state {2} in {3}",
                        m_sp.Name, m_state, newState, m_sp.Scene.Name));
            }
            else
            {
                m_state = newState;
            }
        }
    }
}