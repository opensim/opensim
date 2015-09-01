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
using System.IO;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Region.Framework.Interfaces
{
    /// <summary>
    /// Used for the OnInventoryArchiveSaved event.
    /// </summary>
    /// <param name="id">Request id</param>
    /// <param name="succeeded">true if the save succeeded, false otherwise</param>
    /// <param name="userInfo">The user for whom the save was conducted</param>
    /// <param name="invPath">The inventory path saved</param>
    /// <param name="savePath">The stream to which the archive was saved</param>
    /// <param name="reportedException">Contains the exception generated if the save did not succeed</param>
    /// <param name="saveCount">Number of inventory items saved to archive</param>
    /// <param name="filterCount">Number of inventory items skipped due to perm filter option</param>
    public delegate void InventoryArchiveSaved(
        UUID id, bool succeeded, UserAccount userInfo, string invPath, Stream saveStream, Exception reportedException, int saveCount, int filterCount);

    /// <summary>
    /// Used for the OnInventoryArchiveLoaded event.
    /// </summary>
    /// <param name="id">Request id</param>
    /// <param name="succeeded">true if the load succeeded, false otherwise</param>
    /// <param name="userInfo">The user for whom the load was conducted</param>
    /// <param name="invPath">The inventory path loaded</param>
    /// <param name="savePath">The stream from which the archive was loaded</param>
    /// <param name="reportedException">Contains the exception generated if the load did not succeed</param>
    /// <param name="loadCount">Number of inventory items loaded from archive</param>
    public delegate void InventoryArchiveLoaded(
        UUID id, bool succeeded, UserAccount userInfo, string invPath, Stream loadStream, Exception reportedException, int loadCount);


    public interface IInventoryArchiverModule
    {
        /// <summary>
        /// Fired when an archive inventory save has been completed.
        /// </summary>
        event InventoryArchiveSaved OnInventoryArchiveSaved;

        /// <summary>
        /// Fired when an archive inventory load has been completed.
        /// </summary>
        event InventoryArchiveLoaded OnInventoryArchiveLoaded;

        /// <summary>
        /// Dearchive a user's inventory folder from the given stream
        /// </summary>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="invPath">The inventory path in which to place the loaded folders and items</param>
        /// <param name="loadStream">The stream from which the inventory archive will be loaded</param>
        /// <returns>true if the first stage of the operation succeeded, false otherwise</returns>
        bool DearchiveInventory(UUID id, string firstName, string lastName, string invPath, string pass, Stream loadStream);

        /// <summary>
        /// Dearchive a user's inventory folder from the given stream
        /// </summary>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="invPath">The inventory path in which to place the loaded folders and items</param>
        /// <param name="loadStream">The stream from which the inventory archive will be loaded</param>
        /// <param name="options">Dearchiving options.  At the moment, the only option is ("merge", true).  This merges
        /// the loaded IAR with existing folders where possible.</param>
        /// <returns>true if the first stage of the operation succeeded, false otherwise</returns>
        bool DearchiveInventory(
            UUID id, string firstName, string lastName, string invPath, string pass, Stream loadStream,
            Dictionary<string, object> options);

        /// <summary>
        /// Archive a user's inventory folder to the given stream
        /// </summary>
        /// <param name="id">ID representing this request.  This will later be returned in the save event</param>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="invPath">The inventory path from which the inventory should be saved.</param>
        /// <param name="saveStream">The stream to which the inventory archive will be saved</param>
        /// <returns>true if the first stage of the operation succeeded, false otherwise</returns>
        bool ArchiveInventory(UUID id, string firstName, string lastName, string invPath, string pass, Stream saveStream);

        /// <summary>
        /// Archive a user's inventory folder to the given stream
        /// </summary>
        /// <param name="id">ID representing this request.  This will later be returned in the save event</param>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="invPath">The inventory path from which the inventory should be saved.</param>
        /// <param name="saveStream">The stream to which the inventory archive will be saved</param>
        /// <param name="options">Archiving options.  Currently, there are none.</param>
        /// <returns>true if the first stage of the operation succeeded, false otherwise</returns>
        bool ArchiveInventory(
            UUID id, string firstName, string lastName, string invPath, string pass, Stream saveStream,
            Dictionary<string, object> options);
    }
}
