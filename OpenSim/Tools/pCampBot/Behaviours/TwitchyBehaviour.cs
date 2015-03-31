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

using OpenMetaverse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using log4net;
using pCampBot.Interfaces;

namespace pCampBot
{
    /// <summary>
    /// This behavior is for the systematic study of some performance improvements made
    /// for OSCC'13.
    /// Do nothing, but send AgentUpdate packets all the time that have only slightly 
    /// different state. The delta of difference will be filtered by OpenSim early on
    /// in the packet processing pipeline. These filters did not exist before OSCC'13.
    /// </summary>
    public class TwitchyBehaviour : AbstractBehaviour
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public TwitchyBehaviour() 
        { 
            AbbreviatedName = "tw";
            Name = "Twitchy"; 
        }

        private const float TWITCH = 0.0001f;
        private int direction = 1;

        public override void Action()
        {
            Bot.Client.Self.Movement.BodyRotation = new Quaternion(Bot.Client.Self.Movement.BodyRotation.X + direction * TWITCH,
                Bot.Client.Self.Movement.BodyRotation.Y,
                Bot.Client.Self.Movement.BodyRotation.Z,
                Bot.Client.Self.Movement.BodyRotation.W);

            //m_log.DebugFormat("[TWITCH]: BodyRot {0}", Bot.Client.Self.Movement.BodyRotation);
            direction = -direction;

            Bot.Client.Self.Movement.SendUpdate();

        }

    }
}