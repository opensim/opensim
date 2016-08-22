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
using System.Xml;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;
using OpenMetaverse;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Client;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes.Animation;
using OpenSim.Region.Framework.Scenes.Types;
using OpenSim.Region.PhysicsModules.SharedBase;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Services.Interfaces;
using TeleportFlags = OpenSim.Framework.Constants.TeleportFlags;

namespace OpenSim.Region.Framework.Scenes
{
    public class MovementAnimationOverrides
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private object MAOLock = new object();
        private Dictionary<string, UUID> m_overrides = new Dictionary<string, UUID>();
        public void SetOverride(string state, UUID animID)
        {
            if (animID == UUID.Zero)
            {
                if (state == "ALL")
                    m_overrides.Clear();
                else
                    m_overrides.Remove(state);
                return;
            }

            m_log.DebugFormat("Setting override for {0} to {1}", state, animID);

            lock (MAOLock)
                m_overrides[state] = animID;
        }

        public UUID GetOverriddenAnimation(string state)
        {
            lock (MAOLock)
            {
                if (m_overrides.ContainsKey(state))
                    return m_overrides[state];
            }

            return UUID.Zero;
        }

        public Dictionary<string, UUID> CloneAOPairs()
        {
            lock (MAOLock)
            {
                return new Dictionary<string, UUID>(m_overrides);
            }
        }

        public void CopyAOPairsFrom(Dictionary<string, UUID> src)
        {
            lock (MAOLock)
            {
                m_overrides.Clear();
                m_overrides = new Dictionary<string, UUID>(src);
            }
        }
    }
}
