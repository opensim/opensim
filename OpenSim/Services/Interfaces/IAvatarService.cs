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

using OpenSim.Framework;

using OpenMetaverse;

namespace OpenSim.Services.Interfaces
{
    public interface IAvatarService
    {
        /// <summary>
        /// Called by the login service
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        IAvatarData GetAvatar(UUID userID);

        /// <summary>
        /// Called by everyone who can change the avatar data (so, regions)
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="avatar"></param>
        /// <returns></returns>
        bool SetAvatar(UUID userID, IAvatarData avatar);

        /// <summary>
        /// Not sure if it's needed
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        bool ResetAvatar(UUID userID);

        /// <summary>
        /// These 3 methods raison d'etre: 
        /// No need to send the entire avatar data (SetAvatar) for changing attachments
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="attach"></param>
        /// <returns></returns>
        bool SetAttachments(UUID userID, AttachmentData[] attachs);
        bool Detach(UUID userID, UUID id);
    }

    /// <summary>
    /// Each region/client that uses avatars will have a data structure
    /// of this type representing the avatars.
    /// </summary>
    public interface IAvatarData
    {
        // Not sure what to do with the non-attachment data
        // That data is highly dependent on the implementation of avatars
        // and I doubt it can be abstracted into this interface.
        // Maybe it will never be here. Maybe that data needs to
        // be processed by a module instead of being processed in 
        // the Scenes core code.

        AttachmentData[] GetAttachments(int[] attachPoints);
        int GetAttachmentPoint(UUID id);

        bool SetAttachments(AttachmentData[] attachs);
        bool Detach(UUID id);
    }
}
