/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Physics.Manager;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;

namespace OpenSim.Region.Scenes
{
    partial class ScenePresence
    { 
        /// <summary>
        /// 
        /// </summary>
        public override void update()
        {
            if (this.childAvatar == false)
            {
                if (this.newForce)
                {
                    this.SendTerseUpdateToALLClients();
                    _updateCount = 0;
                }
                else if (movementflag != 0)
                {
                    _updateCount++;
                    if (_updateCount > 3)
                    {
                        this.SendTerseUpdateToALLClients();
                        _updateCount = 0;
                    }
                }

                this.CheckForBorderCrossing();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteAvatar"></param>
        public void SendUpdateToOtherClient(ScenePresence remoteAvatar)
        {
          
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendInitialPosition()
        {
            this.ControllingClient.SendAvatarData(m_regionInfo.RegionHandle, this.firstname, this.lastname, this.uuid, this.LocalId, this.Pos);
            if (this.newAvatar)
            {
                this.m_world.InformClientOfNeighbours(this.ControllingClient);
                this.newAvatar = false;
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="OurClient"></param>
        public void SendOurAppearance(IClientAPI OurClient)
        {
            this.ControllingClient.SendWearables(this.Wearables);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="avatarInfo"></param>
        public void SendAppearanceToOtherAgent(ScenePresence avatarInfo)
        {
            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="visualParam"></param>
        public void SetAppearance(byte[] texture, AgentSetAppearancePacket.VisualParamBlock[] visualParam)
        {
           
        }

        /// <summary>
        /// 
        /// </summary>
        public void StopMovement()
        {
           
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="animID"></param>
        /// <param name="seq"></param>
        public void SendAnimPack(LLUUID animID, int seq)
        {
            
          
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendAnimPack()
        {
           
        }

        /// <summary>
        /// 
        /// </summary>
        protected void CheckForBorderCrossing()
        {
            LLVector3 pos2 = this.Pos;
            LLVector3 vel = this.Velocity;

            float timeStep = 0.2f;
            pos2.X = pos2.X + (vel.X * timeStep);
            pos2.Y = pos2.Y + (vel.Y * timeStep);
            pos2.Z = pos2.Z + (vel.Z * timeStep);

            if ((pos2.X < 0) || (pos2.X > 256))
            {
                this.CrossToNewRegion();
            }

            if ((pos2.Y < 0) || (pos2.Y > 256))
            {
                this.CrossToNewRegion();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        protected void CrossToNewRegion()
        {
            LLVector3 pos = this.Pos;
            LLVector3 newpos = new LLVector3(pos.X, pos.Y, pos.Z);
            uint neighbourx = this.m_regionInfo.RegionLocX;
            uint neighboury = this.m_regionInfo.RegionLocY;

            if (pos.X < 2)
            {
                neighbourx -= 1;
                newpos.X = 254;
            }
            if (pos.X > 253)
            {
                neighbourx += 1;
                newpos.X = 1;
            }
            if (pos.Y < 2)
            {
                neighboury -= 1;
                newpos.Y = 254;
            }
            if (pos.Y > 253)
            {
                neighboury += 1;
                newpos.Y = 1;
            }

            LLVector3 vel = this.velocity;
            ulong neighbourHandle = Helpers.UIntsToLong((uint)(neighbourx * 256), (uint)(neighboury* 256));
            RegionInfo neighbourRegion = this.m_world.RequestNeighbouringRegionInfo(neighbourHandle);
            if (neighbourRegion != null)
            {
                this.m_world.InformNeighbourOfCrossing(neighbourHandle, this.ControllingClient.AgentId, newpos);
                this.DownGradeAvatar();
                this.ControllingClient.CrossRegion(neighbourHandle, newpos, vel, System.Net.IPAddress.Parse(neighbourRegion.IPListenAddr), (ushort)neighbourRegion.IPListenPort);
               
            }
        }

    }
}
