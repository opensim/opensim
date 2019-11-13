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
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.Framework.Interfaces
{
    /// <summary>
    /// An agent in the scene.
    /// </summary>
    /// <remarks>
    /// Interface is a work in progress.  Please feel free to add other required properties and methods.
    /// </remarks>
    public interface IScenePresence : ISceneAgent
    {
        /// <summary>
        /// Copy of the script states while the agent is in transit. This state may
        /// need to be placed back in case of transfer fail.
        /// </summary>
        List<string> InTransitScriptStates { get; }

        /// <summary>
        /// The AttachmentsModule synchronizes on this to avoid race conditions between commands to add and remove attachments.
        /// </summary>
        /// <remarks>
        /// All add and remove attachment operations must synchronize on this for the lifetime of their operations.
        /// </remarks>
        Object AttachmentsSyncLock { get; }
        int GetAttachmentsCount();
        /// <summary>
        /// The scene objects attached to this avatar.
        /// </summary>
        /// <returns>
        /// A copy of the list.
        /// </returns>
        /// <remarks>
        ///  Do not change this list directly - use the attachments module.
        /// </remarks>
        List<SceneObjectGroup> GetAttachments();

        /// <summary>
        /// The scene objects attached to this avatar at a specific attachment point.
        /// </summary>
        /// <param name="attachmentPoint"></param>
        /// <returns></returns>
        List<SceneObjectGroup> GetAttachments(uint attachmentPoint);

        /// <summary>
        /// Does this avatar have any attachments?
        /// </summary>
        /// <returns></returns>
        bool HasAttachments();

        // Don't use these methods directly.  Instead, use the AttachmentsModule
        void AddAttachment(SceneObjectGroup gobj);
        void RemoveAttachment(SceneObjectGroup gobj);
        void ClearAttachments();
    }
}
