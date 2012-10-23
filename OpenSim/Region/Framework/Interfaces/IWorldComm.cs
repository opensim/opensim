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
        /// <summary>
        /// Total number of listeners
        /// </summary>
        int ListenerCount { get; }

        /// <summary>
        /// Create a listen event callback with the specified filters.
        /// The parameters localID,itemID are needed to uniquely identify
        /// the script during 'peek' time. Parameter hostID is needed to
        /// determine the position of the script.
        /// </summary>
        /// <param name="LocalID">localID of the script engine</param>
        /// <param name="itemID">UUID of the script engine</param>
        /// <param name="hostID">UUID of the SceneObjectPart</param>
        /// <param name="channel">channel to listen on</param>
        /// <param name="name">name to filter on</param>
        /// <param name="id">key to filter on (user given, could be totally faked)</param>
        /// <param name="msg">msg to filter on</param>
        /// <returns>number of the scripts handle</returns>
        int Listen(uint LocalID, UUID itemID, UUID hostID, int channel, string name, UUID id, string msg);

        /// <summary>
        /// This method scans over the objects which registered an interest in listen callbacks.
        /// For everyone it finds, it checks if it fits the given filter. If it does,  then
        /// enqueue the message for delivery to the objects listen event handler.
        /// The enqueued ListenerInfo no longer has filter values, but the actually trigged values.
        /// Objects that do an llSay have their messages delivered here and for nearby avatars,
        /// the OnChatFromClient event is used.
        /// </summary>
        /// <param name="type">type of delvery (whisper,say,shout or regionwide)</param>
        /// <param name="channel">channel to sent on</param>
        /// <param name="name">name of sender (object or avatar)</param>
        /// <param name="id">key of sender (object or avatar)</param>
        /// <param name="msg">msg to sent</param>
        void DeliverMessage(ChatTypeEnum type, int channel, string name, UUID id, string msg);

        /// <summary>
        /// Delivers the message to a specified object in the region.
        /// </summary>
        /// <param name='target'>
        /// Target.
        /// </param>
        /// <param name='channel'>
        /// Channel.
        /// </param>
        /// <param name='name'>
        /// Name.
        /// </param>
        /// <param name='id'>
        /// Identifier.
        /// </param>
        /// <param name='msg'>
        /// Message.
        /// </param>
        void DeliverMessageTo(UUID target, int channel, Vector3 pos, string name, UUID id, string msg);

        /// <summary>
        /// Are there any listen events ready to be dispatched?
        /// </summary>
        /// <returns>boolean indication</returns>
        bool HasMessages();

        /// <summary>
        /// Pop the first availlable listen event from the queue
        /// </summary>
        /// <returns>ListenerInfo with filter filled in</returns>
        IWorldCommListenerInfo GetNextMessage();
        
        void ListenControl(UUID itemID, int handle, int active);
        void ListenRemove(UUID itemID, int handle);
        void DeleteListener(UUID itemID);
        Object[] GetSerializationData(UUID itemID);
        void CreateFromData(uint localID, UUID itemID, UUID hostID,
                            Object[] data);
    }
}
