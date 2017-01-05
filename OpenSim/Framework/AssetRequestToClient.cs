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

using OpenMetaverse;

namespace OpenSim.Framework
{
    /// <summary>
    /// This class was created to refactor OutPacket out of AssetCache
    /// There is a conflict between
    /// OpenSim.Framework.Communications.Cache.AssetRequest and OpenSim.Framework.AssetRequest
    /// and unifying them results in a prebuild chicken and egg problem with OpenSim.Framework requiring
    /// OpenSim.Framework.Communications.Cache while OpenSim.Framework.Communications.Cache
    /// requiring OpenSim.Framework
    /// </summary>
    public class AssetRequestToClient
    {
        public UUID RequestAssetID;
        public AssetBase AssetInf;
        public AssetBase ImageInfo;
        public UUID TransferRequestID;
        public long DataPointer = 0;
        public int NumPackets = 0;
        public int PacketCounter = 0;
        public bool IsTextureRequest;
        public byte AssetRequestSource = 2;
        public byte[] Params = null;
        //public bool AssetInCache;
        //public int TimeRequested;
        public int DiscardLevel = -1;

        public AssetRequestToClient()
        {
        }
    }
}
