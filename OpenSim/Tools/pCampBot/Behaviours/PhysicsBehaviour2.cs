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
using System.Threading;
using log4net;
using pCampBot.Interfaces;

namespace pCampBot
{
    /// <summary>
    /// This behavior is for the systematic study of some performance improvements made
    /// for OSCC'13.
    /// Walk around, sending AgentUpdate packets all the time.
    /// </summary>
    public class PhysicsBehaviour2 : AbstractBehaviour
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public PhysicsBehaviour2()
        {
            AbbreviatedName = "ph2";
            Name = "Physics2";
        }

        private const int TIME_WALKING = 5 * 10; // 5 seconds
        private int counter = 0;

        public override void Action()
        {

            if (counter >= TIME_WALKING)
            {
                counter = 0;

                Vector3 target = new Vector3(Bot.Random.Next(1, 254), Bot.Random.Next(1, 254), Bot.Client.Self.SimPosition.Z);
                MyTurnToward(target);

                Bot.Client.Self.Movement.AtPos = true;

            }
            else
                counter++;
            // In any case, send an update
            Bot.Client.Self.Movement.SendUpdate();
        }

        private void MyTurnToward(Vector3 target)
        {
            Quaternion between = Vector3.RotationBetween(Vector3.UnitX, Vector3.Normalize(target - Bot.Client.Self.SimPosition));
            Quaternion rot = between ;

            Bot.Client.Self.Movement.BodyRotation = rot;
            Bot.Client.Self.Movement.HeadRotation = rot;
            Bot.Client.Self.Movement.Camera.LookAt(Bot.Client.Self.SimPosition, target);
        }
    }
}