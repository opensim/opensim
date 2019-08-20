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

using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Region.Framework.Interfaces
{
    public delegate void ReportOutputAction(string format, string level, params object[] args);

    public interface IAvatarFactoryModule
    {
        void SetAppearance(IScenePresence sp, AvatarAppearance appearance, WearableCacheItem[] cacheItems);
        void SetAppearance(IScenePresence sp, Primitive.TextureEntry textureEntry, byte[] visualParams, WearableCacheItem[] cacheItems);

        /// <summary>
        /// Send the appearance of an avatar to others in the scene.
        /// </summary>
        /// <param name="agentId"></param>
        /// <returns></returns>
        bool SendAppearance(UUID agentId);

        /// <summary>
        /// Return the baked texture ids of the given agent.
        /// </summary>
        /// <param name="agentId"></param>
        /// <returns>An empty list if this agent has no baked textures (e.g. because it's a child agent)</returns>
        Dictionary<BakeType, Primitive.TextureEntryFace> GetBakedTextureFaces(UUID agentId);


        WearableCacheItem[] GetCachedItems(UUID agentId);
        /// <summary>
        /// Save the baked textures for the given agent permanently in the asset database.
        /// </summary>
        /// <remarks>
        /// This is used to preserve apperance textures for NPCs
        /// </remarks>
        /// <param name="agentId"></param>
        /// <returns>true if a valid agent was found, false otherwise</returns>
        bool SaveBakedTextures(UUID agentId);

        /// <summary>
        /// Validate that OpenSim can find the baked textures need to display a given avatar
        /// </summary>
        /// <param name="client"></param>
        /// <param name="checkonly"></param>
        /// <returns>
        /// true if all the baked textures referenced by the texture IDs exist or the appearance is only using default textures.  false otherwise.
        /// </returns>
        bool ValidateBakedTextureCache(IScenePresence sp);

        /// <summary>
        /// Request a rebake of textures for an avatar.
        /// </summary>
        /// <remarks>
        /// This will send the request to the viewer, since it's there that the rebake is done.
        /// </remarks>
        /// <param name="sp">Avatar to rebake.</param>
        /// <param name="missingTexturesOnly">
        /// If true, only request a rebake for the textures that are missing.
        /// If false then we request a rebake of all textures for which we already have references.
        /// </param>
        /// <returns>
        /// Number of rebake requests made.  This will depend upon whether we've previously received texture IDs.
        /// </returns>
        int RequestRebake(IScenePresence sp, bool missingTexturesOnly);

        void QueueAppearanceSend(UUID agentid);
        void QueueAppearanceSave(UUID agentid);

        /// <summary>
        /// Get a report about the current state of a scene presence's baked appearance textures.
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="reportOutputAction"></param>
        /// <returns></returns>
        void WriteBakedTexturesReport(IScenePresence sp, ReportOutputAction reportOutputAction);
    }
}