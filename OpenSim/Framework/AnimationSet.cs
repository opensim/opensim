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
using OpenMetaverse;

namespace OpenSim.Framework
{
    public delegate bool AnimationSetValidator(UUID animID);

    public class AnimationSet
    {
        private readonly int m_maxAnimations = 255;

        public const uint createBasePermitions = (uint)(PermissionMask.All); // no export ?
        public const uint createNextPermitions = (uint)(PermissionMask.Copy | PermissionMask.Modify);

        public const uint allowedBasePermitions = (uint)(PermissionMask.Copy | PermissionMask.Modify);
        public const uint allowedNextPermitions = 0;

        public static void setCreateItemPermitions(InventoryItemBase it)
        {
            if (it == null)
                return;

            it.BasePermissions = createBasePermitions;
            it.CurrentPermissions = createBasePermitions;
            //            it.GroupPermissions &= allowedPermitions;
            it.NextPermissions = createNextPermitions;
            //            it.EveryOnePermissions &= allowedPermitions;
            it.GroupPermissions = 0;
            it.EveryOnePermissions = 0;
        }

        public static void enforceItemPermitions(InventoryItemBase it, bool IsCreator)
        {
            if (it == null)
                return;

            uint bp;
            uint np;

            if (IsCreator)
            {
                bp = createBasePermitions;
                np = createNextPermitions;
            }
            else
            {
                bp = allowedBasePermitions;
                np = allowedNextPermitions;
            }

            it.BasePermissions &= bp;
            it.CurrentPermissions &= bp;
            //            it.GroupPermissions &= allowedPermitions;
            it.NextPermissions &= np;
            //            it.EveryOnePermissions &= allowedPermitions;
            it.GroupPermissions = 0;
            it.EveryOnePermissions = 0;
        }
        
        public int AnimationCount { get; private set; }
        private Dictionary<int, UUID> m_animations = new Dictionary<int, UUID>();

        public UUID AnimationAt(int index)
        {
            if (m_animations.ContainsKey(index))
                return m_animations[index];
            return UUID.Zero;
        }

        public void SetAnimation(int index, UUID animation)
        {
            if (index < 0 || index > m_maxAnimations)
                return;

            m_animations[index] = animation;
        }

        public AnimationSet(Byte[] assetData)
        {
            if (assetData.Length < 2)
                throw new System.ArgumentException();

            if (assetData[0] != 1) // Only version 1 is supported
                throw new System.ArgumentException();

            AnimationCount = assetData[1];
            if (assetData.Length - 2 != 16 * AnimationCount)
                throw new System.ArgumentException();

            // TODO: Read anims from blob
        }

        public Byte[] ToBytes()
        {
            // TODO: Make blob from anims
            return new Byte[0];
        }

        public bool Validate(AnimationSetValidator val)
        {
            List<int> badAnims = new List<int>();

            bool allOk = true;
            foreach (KeyValuePair<int, UUID> kvp in m_animations)
            {
                if (!val(kvp.Value))
                {
                    allOk = false;
                    badAnims.Add(kvp.Key);
                }
            }

            foreach (int idx in badAnims)
                m_animations.Remove(idx);

            return allOk;
        }
    }
}
