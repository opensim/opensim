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
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data
{
    public class XInventoryFolder
    {
        public string folderName;
        public int type;
        public int version;
        public UUID folderID;
        public UUID agentID;
        public UUID parentFolderID;
    }

    public class XInventoryItem
    {
        public UUID assetID;
        public int assetType;
        public string inventoryName;
        public string inventoryDescription;
        public int inventoryNextPermissions;
        public int inventoryCurrentPermissions;
        public int invType;
        public string creatorID;
        public int inventoryBasePermissions;
        public int inventoryEveryOnePermissions;
        public int salePrice;
        public int saleType;
        public int creationDate;
        public UUID groupID;
        public int groupOwned;
        public int flags;
        public UUID inventoryID;
        public UUID avatarID;
        public UUID parentFolderID;
        public int inventoryGroupPermissions;
    }

    public interface IXInventoryData
    {
        XInventoryFolder[] GetFolders(string[] fields, string[] vals);
        XInventoryItem[] GetItems(string[] fields, string[] vals);

        bool StoreFolder(XInventoryFolder folder);
        bool StoreItem(XInventoryItem item);

        /// <summary>
        /// Delete folders where field == val
        /// </summary>
        /// <param name="field"></param>
        /// <param name="val"></param>
        /// <returns>true if the delete was successful, false if it was not</returns>
        bool DeleteFolders(string field, string val);

        /// <summary>
        /// Delete folders where field1 == val1, field2 == val2...
        /// </summary>
        /// <param name="fields"></param>
        /// <param name="vals"></param>
        /// <returns>true if the delete was successful, false if it was not</returns>
        bool DeleteFolders(string[] fields, string[] vals);

        /// <summary>
        /// Delete items where field == val
        /// </summary>
        /// <param name="field"></param>
        /// <param name="val"></param>
        /// <returns>true if the delete was successful, false if it was not</returns>
        bool DeleteItems(string field, string val);

        /// <summary>
        /// Delete items where field1 == val1, field2 == val2...
        /// </summary>
        /// <param name="fields"></param>
        /// <param name="vals"></param>
        /// <returns>true if the delete was successful, false if it was not</returns>
        bool DeleteItems(string[] fields, string[] vals);

        bool MoveItem(string id, string newParent);
        XInventoryItem[] GetActiveGestures(UUID principalID);
        int GetAssetPermissions(UUID principalID, UUID assetID);
    }
}
