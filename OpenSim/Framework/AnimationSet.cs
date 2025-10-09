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
using System.Linq;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Framework
{
//    public delegate bool AnimationSetValidator(UUID animID);
    public delegate uint AnimationSetValidator(UUID animID);

    public class AnimationSet
    {
        private bool m_parseError = false;

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
        private Dictionary<string, KeyValuePair<string, UUID>> m_animations = new Dictionary<string, KeyValuePair<string, UUID>>();

        public UUID GetAnimation(string index)
        {
            return m_animations.TryGetValue(index, out var val) 
                ? val.Value : UUID.Zero;
        }

        public string GetAnimationName(string index)
        {
            return m_animations.TryGetValue(index, out var val) 
                ? val.Key : string.Empty;
        }

        public void SetAnimation(string index, string name, UUID anim)
        {
            if (anim.IsZero())
            {
                m_animations.Remove(index);
                return;
            }

            m_animations[index] = new KeyValuePair<string, UUID>(name, anim);
        }

        public AnimationSet(byte[] data)
        {
            var assetData = Encoding.ASCII.GetString(data);
            Console.WriteLine("--------------------");
            Console.WriteLine("AnimationSet length {0} bytes", assetData.Length);
            Console.WriteLine(assetData);
            Console.WriteLine("--------------------");
        }

        public static readonly byte[] ZeroCountBytesReply = osUTF8.GetASCIIBytes("version 1\ncount 0\n");
        public byte[] ToBytes()
        {
            // If there was an error parsing the input, we give back an
            // empty set rather than the original data.
            if (m_parseError || m_animations.Count == 0)
                return ZeroCountBytesReply;

            var sb = OSUTF8Cached.Acquire();
            sb.AppendASCII("$version 1\ncount {m_animations.Count}\n");
            foreach (var kvp in m_animations)
                sb.AppendASCII($"{kvp.Key} {kvp.Value.Value} {kvp.Value.Key}\n");
            return OSUTF8Cached.GetArrayAndRelease(sb);
        }

/*
        public bool Validate(AnimationSetValidator val)
        {
            if (m_parseError)
                return false;

            List<string> badAnims = new List<string>();

            bool allOk = true;
            foreach (KeyValuePair<string, KeyValuePair<string, UUID>> kvp in m_animations)
            {
                if (!val(kvp.Value.Value))
                {
                    allOk = false;
                    badAnims.Add(kvp.Key);
                }
            }

            foreach (string idx in badAnims)
                m_animations.Remove(idx);

            return allOk;
        }
*/
        public uint Validate(AnimationSetValidator val)
        {
            if (m_parseError)
                return 0;

            uint ret = 0x7fffffff;
            foreach (var t in m_animations.Select(kvp => val(kvp.Value.Value)))
            {
                if (t == 0)
                    return 0;
                ret &= t;
            }
            return ret;
        }
    }
}
