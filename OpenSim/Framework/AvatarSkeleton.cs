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
// Ubit 2012 
using System;
using System.Collections;
using System.Collections.Generic;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;
using VPElement = OpenSim.Framework.AvatarAppearance.VPElement;



namespace OpenSim.Framework
{
    /// <summary>
    /// Contains the Avatar's Skeleton
    /// </summary>
    public class AvatarSkeleton
    {
        const int NBONES = 26;
        const float BOXAJUST = 0.2f;

        public enum Bones : int
        {
            EyeLeft,
            Eyeright,
            Skull,
            Head,
            Neck,
            CollarRight,
            CollarLeft,
            Shoulderright,
            ShoulderLeft,
            ElbowRight,
            ElbowLeft,
            WristRight,
            WristLeft,
            Chest,
            Torso,
            Pelvis,
            Hipright,
            HipLeft,
            KneeRight,
            KneeLeft,
            AnkleRight,
            AnkleLeft,
            FootRight,
            FootLeft,
            ToeRight,
            ToeLeft
        }

        public struct bone
        {
            public Vector3 Offset;
            public Vector3 Scale;
            public bone(float x, float y, float z)
            {
                Offset = new Vector3(x, y, z);
                Scale = new Vector3(1f, 1f, 1f);
            }

            public void addScale(float x, float y, float z, float factor)
            {
                Scale.X += x * factor;
                Scale.Y += y * factor;
                Scale.Y += z * factor;
            }

            public void addOffset(float x, float y, float z, float factor)
            {
                Offset.X += x * factor;
                Offset.Y += y * factor;
                Offset.Y += z * factor;
            }
        }

        private bone[] DefaultBones = new bone[]
        {
            new bone(0.098f, 0.036f, 0.079f), // EyeLeft
            new bone(0.098f, -0.036f, 0.079f), // Eyeright          
            new bone(0.0f, 0.0f, 0.079f), // Skull
            new bone(0.0f, 0.0f, 0.076f), // Head
            new bone(-0.1f, 0.0f, 0.251f), // Neck
            new bone(-0.021f, -0.085f, 0.165f), // CollarRight
            new bone(-0.021f, 0.085f, 0.165f), // CollarLeft
            new bone(0.0f, -0.79f, 0.0f), // Shoulderright
            new bone(0.0f, 0.79f, 0.0f), // ShoulderLeft
            new bone(0.0f, -0.248f, 0.0f), // ElbowRight
            new bone(0.0f, 0.248f, 0.0f), // ElbowLeft
            new bone(0.0f, -0.205f, 0.0f), // WristRight
            new bone(0.0f, 0.205f, 0.0f), // WristLeft
            new bone(-0.015f, 0.000f, 0.205f), // Chest
            new bone(0.0f, 0.0f, 0.084f), // Torso
            new bone(0.0f, 0.0f, 1.067f), // Pelvis
            new bone(0.034f, -0.129f, -0.041f), // Hipright
            new bone(0.034f, 0.127f, -0.041f), // HipLeft
            new bone(-0.001f, 0.049f, -0.491f), // KneeRight
            new bone(-0.001f, -0.046f, -0.491f), // KneeLeft
            new bone(-0.029f, 0.0f, -0.468f), // AnkleRight
            new bone(-0.029f, 0.001f, -0.468f), // AnkleLeft
            new bone(0.112f, 0.0f, -0.061f), // FootRight
            new bone(0.112f, 0.0f, -0.061f), // FootLeft
            new bone(0.109f, 0.0f, 0.0f), // ToeRight
            new bone(0.109f, 0.0f, 0.0f) // ToeLeft
        };

        private bone[] m_bones = null;
        private byte[] m_visualParams = null;

        const float bytescale = 1.0f / 255.0f;

        private float convertVP(AvatarAppearance.VPElement vp)
        {
            return (float)m_visualParams[(int)vp] * bytescale;
        }

        private Vector3 m_standSize;
        private float m_feetOffset = 0f;

        public Vector3 StandSize
        {
            get
            {
                if (m_bones == null || m_visualParams == null)
                    return new Vector3(0.45f, 0.6f, 1.9f);
                else
                    return m_standSize;
            }
        }

        public Vector3 StandBoxSize
        {
            get
            {
                if (m_bones == null || m_visualParams == null)
                    return new Vector3(0.45f, 0.6f, 1.9f + BOXAJUST);
                else
                {
                    Vector3 r = m_standSize;
                    r.Z += BOXAJUST;
                    return r;
                }
            }
        }

        public float FeetOffset
        {
            get
            {
                if (m_bones == null || m_visualParams == null)
                    return 0.0f;
                else
                {
                    return m_feetOffset;
                }
            }
        }

        /// <summary>
        /// Set avatar height by a calculation based on their visual parameters.
        /// </summary>

        public void ApplyVisualParameters(byte[] vPs)
        {
            m_visualParams = vPs;          

            if (m_bones == null)
            {
                m_bones = new bone[NBONES];
                for (int i = 0; i < NBONES; i++)
                    m_bones[i] = DefaultBones[i];
            }

            float bone_skull = m_bones[(int)Bones.Skull].Offset.Z;
            float bone_head = m_bones[(int)Bones.Head].Offset.Z;
            float bone_neck = m_bones[(int)Bones.Neck].Offset.Z;
            float bone_chest = m_bones[(int)Bones.Chest].Offset.Z;
            float bone_torso = m_bones[(int)Bones.Torso].Offset.Z;
            float bone_hip = m_bones[(int)Bones.Hipright].Offset.Z;
            float bone_knee = m_bones[(int)Bones.KneeRight].Offset.Z;
            float bone_ank = m_bones[(int)Bones.AnkleRight].Offset.Z;
            float bone_foot = m_bones[(int)Bones.FootRight].Offset.Z;

            float sbone_skull = m_bones[(int)Bones.Skull].Scale.Z;
            float sbone_head = m_bones[(int)Bones.Head].Scale.Z;
            float sbone_neck = m_bones[(int)Bones.Neck].Scale.Z;
            float sbone_chest = m_bones[(int)Bones.Chest].Scale.Z;
            float sbone_torso = m_bones[(int)Bones.Torso].Scale.Z;
            float sbone_pelvis = m_bones[(int)Bones.Pelvis].Scale.Z;
            float sbone_hip = m_bones[(int)Bones.Hipright].Scale.Z;
            float sbone_knee = m_bones[(int)Bones.KneeRight].Scale.Z;
            float sbone_ank = m_bones[(int)Bones.AnkleRight].Scale.Z;
            float sbone_foot = m_bones[(int)Bones.FootRight].Scale.Z;

            float v_male = (m_visualParams[(int)VPElement.SHAPE_MALE] == 0) ? 0.0f : 1.0f;          
            sbone_neck += v_male * 0.2f;
            sbone_chest += v_male * 0.05f;
            sbone_torso += v_male * 0.05f;
            sbone_knee += v_male * 0.1f;

            float v_height = convertVP(VPElement.SHAPE_HEIGHT) * 4.3f - 2.3f;
            sbone_neck += v_height * 0.02f;
            sbone_chest += v_height * 0.05f;
            sbone_torso += v_height * 0.05f;
            sbone_hip += v_height * 0.1f;
            sbone_knee += v_height * 0.1f;

            float v_hip_len = convertVP(VPElement.SHAPE_HIP_LENGTH) * 2f - 1f;
            sbone_pelvis += v_hip_len * 0.3f;

            float v_torso_len = convertVP(VPElement.SHAPE_TORSO_LENGTH) * 2f - 1f;
            sbone_torso += v_torso_len * 0.3f;
            sbone_pelvis += v_torso_len * 0.1f;
            sbone_hip += v_torso_len * -0.1f;
            sbone_knee += v_torso_len * -0.05f;

            float v_head_size = convertVP(VPElement.SHAPE_HEAD_SIZE) * 0.35f - 0.25f;
            bone_skull += v_head_size * 0.1f;
            sbone_skull += v_head_size;
            sbone_head += v_head_size;

            float v_shoes_heel = convertVP(VPElement.SHOES_HEEL_HEIGHT);
            bone_foot += v_shoes_heel * -0.08f;

            float v_shoes_plat = convertVP(VPElement.SHOES_PLATFORM_HEIGHT);
            bone_foot += v_shoes_plat * -0.07f;

            float v_leg_lenght = convertVP(VPElement.SHAPE_LEG_LENGTH) * 2f - 1f;
            sbone_hip += v_leg_lenght * 0.2f;
            sbone_knee += v_leg_lenght * 0.2f;

            float v_neck_len = convertVP(VPElement.SHAPE_NECK_LENGTH) * 2f - 1f;
            sbone_neck += v_neck_len * 0.5f;

            float hipmess = bone_hip * sbone_pelvis;

            float pelvisToFoot = hipmess -
                                bone_knee * sbone_hip -
                                bone_ank * sbone_knee -
                                bone_foot * sbone_ank;


            float size = 1.4142f * bone_skull * sbone_head +
                        bone_head * sbone_neck +
                        bone_neck * sbone_chest +
                        bone_chest * sbone_torso +
                        bone_torso * sbone_pelvis;

            size += pelvisToFoot;

            m_standSize = new Vector3(0.45f, 0.6f, size);
            //            m_feetOffset = 0.5f * size - pelvisToFoot;
            m_feetOffset = 0.0f;
        }
    }
}
