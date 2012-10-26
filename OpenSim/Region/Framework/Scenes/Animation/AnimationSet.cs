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
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;

using Animation = OpenSim.Framework.Animation;

namespace OpenSim.Region.Framework.Scenes.Animation
{
    [Serializable]
    public class AnimationSet
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private OpenSim.Framework.Animation m_defaultAnimation = new OpenSim.Framework.Animation();
        private List<OpenSim.Framework.Animation> m_animations = new List<OpenSim.Framework.Animation>();

        public OpenSim.Framework.Animation DefaultAnimation 
        {
            get { return m_defaultAnimation; } 
        }
        
        public AnimationSet()
        {
            ResetDefaultAnimation();
        }

        public bool HasAnimation(UUID animID)
        {
            if (m_defaultAnimation.AnimID == animID)
                return true;

            for (int i = 0; i < m_animations.Count; ++i)
            {
                if (m_animations[i].AnimID == animID)
                    return true;
            }

            return false;
        }

        public bool Add(UUID animID, int sequenceNum, UUID objectID)
        {
            lock (m_animations)
            {
                if (!HasAnimation(animID))
                {
                    m_animations.Add(new OpenSim.Framework.Animation(animID, sequenceNum, objectID));
                    return true;
                }
            }
            return false;
        }

        public bool Remove(UUID animID)
        {
            lock (m_animations)
            {
                if (m_defaultAnimation.AnimID == animID)
                {
                    m_defaultAnimation = new OpenSim.Framework.Animation(UUID.Zero, 1, UUID.Zero);
                }
                else if (HasAnimation(animID))
                {
                    for (int i = 0; i < m_animations.Count; i++)
                    {
                        if (m_animations[i].AnimID == animID)
                        {
                            m_animations.RemoveAt(i);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public void Clear()
        {
            ResetDefaultAnimation();
            m_animations.Clear();
        }

        /// <summary>
        /// The default animation is reserved for "main" animations
        /// that are mutually exclusive, e.g. flying and sitting.
        /// </summary>
        public bool SetDefaultAnimation(UUID animID, int sequenceNum, UUID objectID)
        {
            if (m_defaultAnimation.AnimID != animID)
            {
                m_defaultAnimation = new OpenSim.Framework.Animation(animID, sequenceNum, objectID);
                return true;
            }
            return false;
        }

        protected bool ResetDefaultAnimation()
        {
            return TrySetDefaultAnimation("STAND", 1, UUID.Zero);
        }

        /// <summary>
        /// Set the animation as the default animation if it's known
        /// </summary>
        public bool TrySetDefaultAnimation(string anim, int sequenceNum, UUID objectID)
        {
//            m_log.DebugFormat(
//                "[ANIMATION SET]: Setting default animation {0}, sequence number {1}, object id {2}",
//                anim, sequenceNum, objectID);

            if (DefaultAvatarAnimations.AnimsUUID.ContainsKey(anim))
            {
                return SetDefaultAnimation(DefaultAvatarAnimations.AnimsUUID[anim], sequenceNum, objectID);
            }
            return false;
        }

        public void GetArrays(out UUID[] animIDs, out int[] sequenceNums, out UUID[] objectIDs)
        {
            lock (m_animations)
            {
                int defaultSize = 0;
                if (m_defaultAnimation.AnimID != UUID.Zero)
                    defaultSize++;

                animIDs = new UUID[m_animations.Count + defaultSize];
                sequenceNums = new int[m_animations.Count + defaultSize];
                objectIDs = new UUID[m_animations.Count + defaultSize];

                if (m_defaultAnimation.AnimID != UUID.Zero)
                {
                    animIDs[0] = m_defaultAnimation.AnimID;
                    sequenceNums[0] = m_defaultAnimation.SequenceNum;
                    objectIDs[0] = m_defaultAnimation.ObjectID;
                }

                for (int i = 0; i < m_animations.Count; ++i)
                {
                    animIDs[i + defaultSize] = m_animations[i].AnimID;
                    sequenceNums[i + defaultSize] = m_animations[i].SequenceNum;
                    objectIDs[i + defaultSize] = m_animations[i].ObjectID;
                }
            }
        }

        public OpenSim.Framework.Animation[] ToArray()
        {
            OpenSim.Framework.Animation[] theArray = new OpenSim.Framework.Animation[m_animations.Count];
            uint i = 0;
            try
            {
                foreach (OpenSim.Framework.Animation anim in m_animations)
                    theArray[i++] = anim;
            }
            catch 
            {
                /* S%^t happens. Ignore. */ 
            }
            return theArray;
        }

        public void FromArray(OpenSim.Framework.Animation[] theArray)
        {
            foreach (OpenSim.Framework.Animation anim in theArray)
                m_animations.Add(anim);
        }
    }
}
