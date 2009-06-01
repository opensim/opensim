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

// AuraMetaEntity.cs created with MonoDevelop
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
    public class AuraMetaEntity : PointMetaEntity
    {
        #region Constructors

        //transparency of root part, NOT particle system. Should probably add support for changing particle system transparency.
        public AuraMetaEntity(Scene scene, Vector3 groupPos, float transparency, Vector3 color, Vector3 scale)
            : base(scene, groupPos, transparency)
        {
            SetAura(color, scale);
        }

        public AuraMetaEntity(Scene scene, UUID uuid, Vector3 groupPos, float transparency, Vector3 color, Vector3 scale)
            : base(scene, uuid, groupPos, transparency)
        {
            SetAura(color, scale);
        }

        #endregion Constructors

        #region Private Methods

        private float Average(Vector3 values)
        {
            return (values.X + values.Y + values.Z)/3f;
        }

        #endregion Private Methods

        #region Public Methods

        public void SetAura(Vector3 color, Vector3 scale)
        {
            SetAura(color, Average(scale) * 2.0f);
        }

        public void SetAura(Vector3 color, float radius)
        {
            SceneObjectPart From = m_Entity.RootPart;

            //m_log.Debug("[META ENTITY] BEFORE: radius = " + radius);
            float burstRadius = 0.1f;
            Primitive.ParticleSystem.SourcePattern patternFlags = Primitive.ParticleSystem.SourcePattern.None;
            float age = 1.5f;
            float burstRate = 0.4f;
            if (radius >= 8.0f)
            {
                //float sizeOfObject = radius / 2.0f;
                burstRadius = (radius - 8.0f)/3f;
                burstRate = 1.5f;
                radius = 7.99f;
                patternFlags = Primitive.ParticleSystem.SourcePattern.Explode;
                age = 4.0f;
            }
            SetAura(From, color, radius, burstRadius, age, burstRate, patternFlags);
        }

        public void SetAura(SceneObjectPart From, Vector3 color, float radius, float burstRadius, float age, float burstRate, Primitive.ParticleSystem.SourcePattern patternFlags)
        {
            Primitive.ParticleSystem prules = new Primitive.ParticleSystem();
            //prules.PartDataFlags = Primitive.ParticleSystem.ParticleDataFlags.Emissive |
            //      Primitive.ParticleSystem.ParticleDataFlags.FollowSrc;   //PSYS_PART_FLAGS
            //prules.PartDataFlags = Primitive.ParticleSystem.ParticleDataFlags.Beam |
            //    Primitive.ParticleSystem.ParticleDataFlags.TargetPos;
            prules.PartStartColor.R = color.X;                                               //PSYS_PART_START_COLOR
            prules.PartStartColor.G = color.Y;
            prules.PartStartColor.B = color.Z;
            prules.PartStartColor.A = 0.5f;                                               //PSYS_PART_START_ALPHA, transparency
            prules.PartEndColor.R = color.X;                                                 //PSYS_PART_END_COLOR
            prules.PartEndColor.G = color.Y;
            prules.PartEndColor.B = color.Z;
            prules.PartEndColor.A = 0.5f;                                                 //PSYS_PART_END_ALPHA, transparency
            /*prules.PartStartScaleX = 0.5f;                                                //PSYS_PART_START_SCALE
            prules.PartStartScaleY = 0.5f;
            prules.PartEndScaleX = 0.5f;                                                  //PSYS_PART_END_SCALE
            prules.PartEndScaleY = 0.5f;
            */
            prules.PartStartScaleX = radius;                                                //PSYS_PART_START_SCALE
            prules.PartStartScaleY = radius;
            prules.PartEndScaleX = radius;                                                  //PSYS_PART_END_SCALE
            prules.PartEndScaleY = radius;
            prules.PartMaxAge = age;                                                     //PSYS_PART_MAX_AGE
            prules.PartAcceleration.X = 0.0f;                                             //PSYS_SRC_ACCEL
            prules.PartAcceleration.Y = 0.0f;
            prules.PartAcceleration.Z = 0.0f;
            prules.Pattern = patternFlags;                 //PSYS_SRC_PATTERN
            //prules.Texture = UUID.Zero;//= UUID                                                     //PSYS_SRC_TEXTURE, default used if blank
            prules.BurstRate = burstRate;                                                      //PSYS_SRC_BURST_RATE
            prules.BurstPartCount = 2;                                                   //PSYS_SRC_BURST_PART_COUNT
            //prules.BurstRadius = radius;                                                    //PSYS_SRC_BURST_RADIUS
            prules.BurstRadius = burstRadius;                                                    //PSYS_SRC_BURST_RADIUS
            prules.BurstSpeedMin = 0.001f;                                                  //PSYS_SRC_BURST_SPEED_MIN
            prules.BurstSpeedMax = 0.001f;                                                  //PSYS_SRC_BURST_SPEED_MAX
            prules.MaxAge = 0.0f;                                                         //PSYS_SRC_MAX_AGE
            //prules.Target = To;                                                 //PSYS_SRC_TARGET_KEY
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
