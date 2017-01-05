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
using System.Text;
using log4net;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

using OpenSim.Framework;

using Animation = OpenSim.Framework.Animation;

namespace OpenSim.Region.Framework.Scenes.Animation
{
    [Serializable]
    public class AnimationSet
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private OpenSim.Framework.Animation m_implicitDefaultAnimation = new OpenSim.Framework.Animation();
        private OpenSim.Framework.Animation m_defaultAnimation = new OpenSim.Framework.Animation();
        private List<OpenSim.Framework.Animation> m_animations = new List<OpenSim.Framework.Animation>();

        public OpenSim.Framework.Animation DefaultAnimation
        {
            get { return m_defaultAnimation; }
        }

        public OpenSim.Framework.Animation ImplicitDefaultAnimation
        {
            get { return m_implicitDefaultAnimation; }
        }

        public AnimationSet()
        {
            ResetDefaultAnimation();
        }

        public AnimationSet(OSDArray pArray)
        {
            ResetDefaultAnimation();
            FromOSDArray(pArray);
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

        /// <summary>
        /// Remove the specified animation
        /// </summary>
        /// <param name='animID'></param>
        /// <param name='allowNoDefault'>
        /// If true, then the default animation can be entirely removed.
        /// If false, then removing the default animation will reset it to the simulator default (currently STAND).
        /// </param>
        public bool Remove(UUID animID, bool allowNoDefault)
        {
            lock (m_animations)
            {
                if (m_defaultAnimation.AnimID == animID)
                {
                    if (allowNoDefault)
                        m_defaultAnimation = new OpenSim.Framework.Animation(UUID.Zero, 1, UUID.Zero);
                    else
                        ResetDefaultAnimation();
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
                m_implicitDefaultAnimation = m_defaultAnimation;
                return true;
            }
            return false;
        }

        // Called from serialization only
        public void SetImplicitDefaultAnimation(UUID animID, int sequenceNum, UUID objectID)
        {
            m_implicitDefaultAnimation = new OpenSim.Framework.Animation(animID, sequenceNum, objectID);
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

        // Create representation of this AnimationSet as an OSDArray.
        // First two entries in the array are the default and implicitDefault animations
        //    followed by the other animations.
        public OSDArray ToOSDArray()
        {
            OSDArray ret = new OSDArray();
            ret.Add(DefaultAnimation.PackUpdateMessage());
            ret.Add(ImplicitDefaultAnimation.PackUpdateMessage());

            foreach (OpenSim.Framework.Animation anim in m_animations)
                ret.Add(anim.PackUpdateMessage());

            return ret;
        }

        public void FromOSDArray(OSDArray pArray)
        {
            this.Clear();

            if (pArray.Count >= 1)
            {
                m_defaultAnimation = new OpenSim.Framework.Animation((OSDMap)pArray[0]);
            }
            if (pArray.Count >= 2)
            {
                m_implicitDefaultAnimation = new OpenSim.Framework.Animation((OSDMap)pArray[1]);
            }
            for (int ii = 2; ii < pArray.Count; ii++)
            {
                m_animations.Add(new OpenSim.Framework.Animation((OSDMap)pArray[ii]));
            }
        }

        // Compare two AnimationSets and return 'true' if the default animations are the same
        //     and all of the animations in the list are equal.
        public override bool Equals(object obj)
        {
            AnimationSet other = obj as AnimationSet;
            if (other != null)
            {
                if (this.DefaultAnimation.Equals(other.DefaultAnimation)
                    && this.ImplicitDefaultAnimation.Equals(other.ImplicitDefaultAnimation))
                {
                    // The defaults are the same. Is the list of animations the same?
                    OpenSim.Framework.Animation[] thisAnims = this.ToArray();
                    OpenSim.Framework.Animation[] otherAnims = other.ToArray();
                    if (thisAnims.Length == 0 && otherAnims.Length == 0)
                        return true;    // the common case
                    if (thisAnims.Length == otherAnims.Length)
                    {
                        // Do this the hard way but since the list is usually short this won't take long.
                        foreach (OpenSim.Framework.Animation thisAnim in thisAnims)
                        {
                            bool found = false;
                            foreach (OpenSim.Framework.Animation otherAnim in otherAnims)
                            {
                                if (thisAnim.Equals(otherAnim))
                                {
                                    found = true;
                                    break;
                                }
                            }
                            if (!found)
                            {
                                // If anything is not in the other list, these are not equal
                                return false;
                            }
                        }
                        // Found everything in the other list. Since lists are equal length, they must be equal.
                        return true;
                    }
                }
                return false;
            }
            // Don't know what was passed, but the base system will figure it out for me.
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            StringBuilder buff = new StringBuilder();
            buff.Append("dflt=");
            buff.Append(DefaultAnimation.ToString());
            buff.Append(",iDflt=");
            if (DefaultAnimation.Equals(ImplicitDefaultAnimation))
                buff.Append("same");
            else
                buff.Append(ImplicitDefaultAnimation.ToString());
            if (m_animations.Count > 0)
            {
                buff.Append(",anims=");
                bool firstTime = true;
                foreach (OpenSim.Framework.Animation anim in m_animations)
                {
                    if (!firstTime)
                        buff.Append(",");
                    buff.Append("<");
                    buff.Append(anim.ToString());
                    buff.Append(">");
                    firstTime = false;
                }
            }
            return buff.ToString();
        }
    }
}
