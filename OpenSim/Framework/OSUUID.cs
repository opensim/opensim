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
 *     * Neither the name of the OpenSim Project nor the
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
using libsecondlife;

namespace OpenSim.Framework
{
    [Serializable]
    public class OSUUID : IComparable
    {
        public static readonly OSUUID Zero = new OSUUID();
        public Guid UUID;

        public OSUUID()
        {
        }

        /* Constructors */

        public OSUUID(string s)
        {
            if (s == null)
                UUID = new Guid();
            else
                UUID = new Guid(s);
        }

        public OSUUID(Guid g)
        {
            UUID = g;
        }

        public OSUUID(LLUUID l)
        {
            UUID = l.UUID;
        }

        public OSUUID(ulong u)
        {
            UUID = new Guid(0, 0, 0, BitConverter.GetBytes(u));
        }

        #region IComparable Members

        public int CompareTo(object obj)
        {
            if (obj is OSUUID)
            {
                OSUUID ID = (OSUUID) obj;
                return UUID.CompareTo(ID.UUID);
            }

            throw new ArgumentException("object is not a OSUUID");
        }

        #endregion

        // out conversion
        public override string ToString()
        {
            return UUID.ToString();
        }

        public LLUUID ToLLUUID()
        {
            return new LLUUID(UUID);
        }

        // for comparison bits
        public override int GetHashCode()
        {
            return UUID.GetHashCode();
        }

        public override bool Equals(object o)
        {
            if (!(o is LLUUID)) return false;

            OSUUID uuid = (OSUUID) o;
            return UUID == uuid.UUID;
        }

        // Static methods
        public static OSUUID Random()
        {
            return new OSUUID(Guid.NewGuid());
        }
    }
}