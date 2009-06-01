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
using OpenSim.Framework;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface IWorldCommListenerInfo
    {
        Object[] GetSerializationData();
        UUID GetItemID();
        UUID GetHostID();
        int GetChannel();
        uint GetLocalID();
        int GetHandle();
        string GetMessage();
        string GetName();
        bool IsActive();
        void Deactivate();
        void Activate();
        UUID GetID();
    }

    public interface IWorldComm
    {
        int Listen(uint LocalID, UUID itemID, UUID hostID, int channel, string name, UUID id, string msg);
        void DeliverMessage(ChatTypeEnum type, int channel, string name, UUID id, string msg);
        bool HasMessages();
        IWorldCommListenerInfo GetNextMessage();
        void ListenControl(UUID itemID, int handle, int active);
        void ListenRemove(UUID itemID, int handle);
        void DeleteListener(UUID itemID);
        Object[] GetSerializationData(UUID itemID);
        void CreateFromData(uint localID, UUID itemID, UUID hostID,
                            Object[] data);
    }
}
