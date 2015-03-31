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
using System.Net;

namespace OpenSim.Framework
{
    public class ClientInfo
    {
        public readonly DateTime StartedTime = DateTime.Now;
        public AgentCircuitData agentcircuit = null;

        public Dictionary<uint, byte[]> needAck;

        public List<byte[]> out_packets = new List<byte[]>();
        public Dictionary<uint, uint> pendingAcks = new Dictionary<uint,uint>();
        public EndPoint proxyEP;

        public uint sequence;
        public byte[] usecircuit;
        public EndPoint userEP;

        public int resendThrottle;
        public int landThrottle;
        public int windThrottle;
        public int cloudThrottle;
        public int taskThrottle;
        public int assetThrottle;
        public int textureThrottle;
        public int totalThrottle;

        // Used by adaptive only
        public int targetThrottle;

        public int maxThrottle;

        public Dictionary<string, int> SyncRequests = new Dictionary<string,int>();
        public Dictionary<string, int> AsyncRequests = new Dictionary<string,int>();
        public Dictionary<string, int> GenericRequests = new Dictionary<string,int>();
    }
}
