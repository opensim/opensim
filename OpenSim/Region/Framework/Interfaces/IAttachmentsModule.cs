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
using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface IAttachmentsModule
    {
        /// <summary>
        /// Copy attachment data from a ScenePresence into the AgentData structure for transmission to another simulator
        /// </summary>
        /// <param name='sp'></param>
        /// <param name='ad'></param>
        void CopyAttachments(IScenePresence sp, AgentData ad);

        /// <summary>
        /// Copy attachment data from an AgentData structure into a ScenePresence.
        /// </summary>
        /// <param name='ad'></param>
        /// <param name='sp'></param>
        void CopyAttachments(AgentData ad, IScenePresence sp);

        /// <summary>
        /// RezAttachments. This should only be called upon login on the first region.
        /// Attachment rezzings on crossings and TPs are done in a different way.
        /// </summary>
        /// <param name="sp"></param>
        void RezAttachments(IScenePresence sp);

        /// <summary>
        /// Derez the attachements for a scene presence that is closing.
        /// </summary>
        /// <remarks>
        /// Attachment changes are saved.
        /// </remarks>
        /// <param name="sp">The presence closing</param>
        /// <param name="saveChanged">Save changed attachments.</param>
        /// <param name="saveAllScripted">Save attachments with scripts even if they haven't changed.</para>
        void DeRezAttachments(IScenePresence sp);

        /// <summary>
        /// Delete all the presence's attachments from the scene
        /// This is done when a root agent leaves/is demoted to child (for instance, on logout, teleport or region cross).
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="silent"></param>
        void DeleteAttachmentsFromScene(IScenePresence sp, bool silent);

        /// <summary>
        /// Attach an object to an avatar
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="grp"></param>
        /// <param name="AttachmentPt"></param>
        /// <param name="silent"></param>
        /// <returns>true if the object was successfully attached, false otherwise</returns>
        bool AttachObject(IScenePresence sp, SceneObjectGroup grp, uint AttachmentPt, bool silent, bool temp);

        /// <summary>
        /// Rez an attachment from user inventory and change inventory status to match.
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="itemID"></param>
        /// <param name="AttachmentPt"></param>
        /// <returns>The scene object that was attached.  Null if the scene object could not be found</returns>
        SceneObjectGroup RezSingleAttachmentFromInventory(IScenePresence sp, UUID itemID, uint AttachmentPt);

        /// <summary>
        /// Rez multiple attachments from a user's inventory
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="rezlist"></param>
        void RezMultipleAttachmentsFromInventory(IScenePresence sp,List<KeyValuePair<UUID, uint>> rezlist);
            
        /// <summary>
        /// Detach the given item to the ground.
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="objectLocalID"></param>
        void DetachSingleAttachmentToGround(IScenePresence sp, uint objectLocalID);

        /// <summary>
        /// Detach the given item to the ground at the specified coordinates & rotation
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="objectLocalID"></param>
        /// <param name="absolutePos"></param>
        /// <param name="absoluteRot"></param>
        void DetachSingleAttachmentToGround(IScenePresence sp, uint objectLocalID, Vector3 absolutePos, Quaternion absoluteRot);

        /// <summary>
        /// Detach the given attachment so that it remains in the user's inventory.
        /// </summary>
        /// <param name="sp">/param>
        /// <param name="grp">The attachment to detach.</param>
        void DetachSingleAttachmentToInv(IScenePresence sp, SceneObjectGroup grp);
        
        /// <summary>
        /// Update the position of an attachment.
        /// </summary>
        /// <param name="sog"></param>
        /// <param name="pos"></param>
        void UpdateAttachmentPosition(SceneObjectGroup sog, Vector3 pos);
    }
}
