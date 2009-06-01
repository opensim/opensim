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

#region Header

// BeamMetaEntity.cs created with MonoDevelop
// User: bongiojp at 3:03 PM 8/6/2008
//
// To change standard headers go to Edit->Preferences->Coding->Standard Headers
//

#endregion Header

using System;
using System.Collections.Generic;
using System.Drawing;

using OpenMetaverse;

using Nini.Config;

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Physics.Manager;

using log4net;

namespace OpenSim.Region.OptionalModules.ContentManagement
{
    public class BeamMetaEntity : PointMetaEntity
    {
        #region Constructors

        public BeamMetaEntity(Scene scene, Vector3 groupPos, float transparency, SceneObjectPart To, Vector3 color)
            : base(scene, groupPos, transparency)
        {
            SetBeamToUUID(To, color);
        }

        public BeamMetaEntity(Scene scene, UUID uuid, Vector3 groupPos, float transparency, SceneObjectPart To, Vector3 color)
            : base(scene, uuid, groupPos, transparency)
        {
            SetBeamToUUID(To, color);
        }

        #endregion Constructors

        #region Public Methods

        public void SetBeamToUUID(SceneObjectPart To, Vector3 color)
        {
            SceneObjectPart From = m_Entity.RootPart;
            //Scale size of particles to distance objects are apart (for better visibility)
            Vector3 FromPos = From.GetWorldPosition();
            Vector3 ToPos = From.GetWorldPosition();
            // UUID toUUID = To.UUID;
            float distance = (float) (Math.Sqrt(Math.Pow(FromPos.X-ToPos.X, 2) +
                                                Math.Pow(FromPos.X-ToPos.Y, 2) +
                                                Math.Pow(FromPos.X-ToPos.Z, 2)
                                                )
                                      );
            //float rate = (float)  (distance/4f);
            float rate = 0.5f;
            float scale = (float) (distance/128f);
            float speed = (float) (2.0f - distance/128f);

            SetBeamToUUID(From, To, color, rate, scale, speed);
        }

        public void SetBeamToUUID(SceneObjectPart From, SceneObjectPart To,  Vector3 color, float rate, float scale, float speed)
        {
            Primitive.ParticleSystem prules = new Primitive.ParticleSystem();
            //prules.PartDataFlags = Primitive.ParticleSystem.ParticleDataFlags.Emissive |
            //      Primitive.ParticleSystem.ParticleDataFlags.FollowSrc;   //PSYS_PART_FLAGS
            prules.PartDataFlags = Primitive.ParticleSystem.ParticleDataFlags.Beam |
                Primitive.ParticleSystem.ParticleDataFlags.TargetPos;
            prules.PartStartColor.R = color.X;                                               //PSYS_PART_START_COLOR
            prules.PartStartColor.G = color.Y;
            prules.PartStartColor.B = color.Z;
            prules.PartStartColor.A = 1.0f;                                               //PSYS_PART_START_ALPHA, transparency
            prules.PartEndColor.R = color.X;                                                 //PSYS_PART_END_COLOR
            prules.PartEndColor.G = color.Y;
            prules.PartEndColor.B = color.Z;
            prules.PartEndColor.A = 1.0f;                                                 //PSYS_PART_END_ALPHA, transparency
            prules.PartStartScaleX = scale;                                                //PSYS_PART_START_SCALE
            prules.PartStartScaleY = scale;
            prules.PartEndScaleX = scale;                                                  //PSYS_PART_END_SCALE
            prules.PartEndScaleY = scale;
            prules.PartMaxAge = 1.0f;                                                     //PSYS_PART_MAX_AGE
            prules.PartAcceleration.X = 0.0f;                                             //PSYS_SRC_ACCEL
            prules.PartAcceleration.Y = 0.0f;
            prules.PartAcceleration.Z = 0.0f;
            //prules.Pattern = Primitive.ParticleSystem.SourcePattern.Explode;                 //PSYS_SRC_PATTERN
            //prules.Texture = UUID.Zero;//= UUID                                                     //PSYS_SRC_TEXTURE, default used if blank
            prules.BurstRate = rate;                                                      //PSYS_SRC_BURST_RATE
            prules.BurstPartCount = 1;                                                   //PSYS_SRC_BURST_PART_COUNT
            prules.BurstRadius = 0.5f;                                                    //PSYS_SRC_BURST_RADIUS
            prules.BurstSpeedMin = speed;                                                  //PSYS_SRC_BURST_SPEED_MIN
            prules.BurstSpeedMax = speed;                                                  //PSYS_SRC_BURST_SPEED_MAX
            prules.MaxAge = 0.0f;                                                         //PSYS_SRC_MAX_AGE
            prules.Target = To.UUID;                                                 //PSYS_SRC_TARGET_KEY
            prules.AngularVelocity.X = 0.0f;                                              //PSYS_SRC_OMEGA
            prules.AngularVelocity.Y = 0.0f;
            prules.AngularVelocity.Z = 0.0f;
            prules.InnerAngle = 0.0f;                                                     //PSYS_SRC_ANGLE_BEGIN
            prules.OuterAngle = 0.0f;                                                     //PSYS_SRC_ANGLE_END

            prules.CRC = 1;  //activates the particle system??
            From.AddNewParticleSystem(prules);
        }

        #endregion Public Methods
    }
}
