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
        /// These methods raison d'etre: 
        /// No need to send the entire avatar data (SetAvatar) for changing attachments
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="attach"></param>
        /// <returns></returns>
        bool SetItems(UUID userID, string[] names, string[] values);
        bool RemoveItems(UUID userID, string[] names);
    }

    /// <summary>
    /// Each region/client that uses avatars will have a data structure
    /// of this type representing the avatars.
    /// </summary>
    public interface IAvatarData
    {
        // This pretty much determines which name/value pairs will be
        // present below. The name/value pair describe a part of
        // the avatar. For SL avatars, these would be "shape", "texture1",
        // etc. For other avatars, they might be "mesh", "skin", etc.
        // The value portion is a URL that is expected to resolve to an
        // asset of the type required by the handler for that field.
        // It is required that regions can access these URLs. Allowing
        // direct access by a viewer is not required, and, if provided,
        // may be read-only. A "naked" UUID can be used to refer to an
        // asset int he current region's asset service, which is not
        // portable, but allows legacy appearance to continue to
        // function. Closed, LL-based  grids will never need URLs here.

        int AvatarType { get; set; }
        Dictionary<string,string> Data { get; set; }

        /// <summary>
        /// This MUST at least define a pair "AvatarType" -> "dll:class"
        /// </summary>
        /// <returns></returns>
        Dictionary<string, object> ToKeyValuePairs();
    }
}
