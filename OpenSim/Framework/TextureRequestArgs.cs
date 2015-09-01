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
using OpenMetaverse;

namespace OpenSim.Framework
{
    public class TextureRequestArgs : EventArgs
    {
        private sbyte m_discardLevel;
        private uint m_packetNumber;
        private float m_priority;
        private int m_requestType;
        private uint m_requestsequence;
        protected UUID m_requestedAssetID;

        public float Priority
        {
            get { return m_priority; }
            set { m_priority = value; }
        }

        /// <summary>
        ///
        /// </summary>
        public uint PacketNumber
        {
            get { return m_packetNumber; }
            set { m_packetNumber = value; }
        }

        public uint requestSequence
        {
            get { return m_requestsequence; }
            set { m_requestsequence = value; }
        }

        /// <summary>
        ///
        /// </summary>
        public sbyte DiscardLevel
        {
            get { return m_discardLevel; }
            set { m_discardLevel = value; }
        }

        /// <summary>
        ///
        /// </summary>
        public UUID RequestedAssetID
        {
            get { return m_requestedAssetID; }
            set { m_requestedAssetID = value; }
        }

        public int RequestType
        {
            get { return m_requestType; }
            set { m_requestType = value; }
        }

        public override string ToString()
        {
            return String.Format("DiscardLevel: {0}, Priority: {1}, PacketNumber: {2}, AssetId:{3}, RequestType:{4}",
                                 m_discardLevel,
                                 m_priority, m_packetNumber, m_requestedAssetID, m_requestType);
        }
    }
}
