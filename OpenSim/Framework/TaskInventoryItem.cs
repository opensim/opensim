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
using System.Reflection;
using log4net;
using OpenMetaverse;

namespace OpenSim.Framework
{
    /// <summary>
    /// Represents an item in a task inventory
    /// </summary>
    public class TaskInventoryItem : ICloneable
    {
        // private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private const uint FULL_MASK_PERMISSIONS_GENERAL = 2147483647;

        public UUID AssetID { get; set; }
        public uint CreationDate { get; set; }

        UUID _creatorID = UUID.Zero;
        public UUID CreatorID
        {
            get { return _creatorID; }
            set { _creatorID = value; }
        }

        private string _creatorData = string.Empty;
        public string CreatorData // = <profile url>;<name>
        {
            get { return _creatorData; }
            set { _creatorData = value; }
        }

        /// <summary>
        /// Used by the DB layer to retrieve / store the entire user identification.
        /// The identification can either be a simple UUID or a string of the form
        /// uuid[;profile_url[;name]]
        /// </summary>
        public string CreatorIdentification
        {
            get
            {
                if (!string.IsNullOrEmpty(_creatorData))
                    return _creatorID.ToString() + ';' + _creatorData;
                else
                    return _creatorID.ToString();
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _creatorData = string.Empty;
                    return;
                }

                if (!value.Contains(';')) // plain UUID
                {
                    _= UUID.TryParse(value, out _creatorID);
                }
                else // <uuid>[;<endpoint>[;name]]
                {
                    string name = "Unknown User";
                    string[] parts = value.Split(';');
                    if (parts.Length >= 1)
                    {
                        _ = UUID.TryParse(parts[0], out _creatorID);
                    }
                    if (parts.Length >= 2)
                        _creatorData = parts[1];
                    if (parts.Length >= 3)
                        name = parts[2];

                    _creatorData += ';' + name;

                }
            }
        }

        public uint Flags { get; set; }
        public int Type { get; set; }
        public int InvType { get; set; }
        public UUID ItemID { get; set; }
        public UUID OldItemID { get; set; }
        public UUID LoadedItemID { get; set; }
        public UUID RezzerID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public UUID OwnerID { get; set; }
        public UUID LastOwnerID { get; set; }
        public UUID GroupID { get; set; }
        public UUID ParentID { get; set; }
        public UUID ParentPartID { get; set; }
        public uint BasePermissions { get; set; } = FULL_MASK_PERMISSIONS_GENERAL;
        public uint CurrentPermissions { get; set; } = FULL_MASK_PERMISSIONS_GENERAL;
        public uint EveryonePermissions { get; set; } = FULL_MASK_PERMISSIONS_GENERAL;
        public uint GroupPermissions { get; set; } = FULL_MASK_PERMISSIONS_GENERAL;
        public uint NextPermissions { get; set; } = FULL_MASK_PERMISSIONS_GENERAL;
        public UUID PermsGranter { get; set; }
        public int PermsMask { get; set; }

        private bool _ownerChanged = false;
        public bool OwnerChanged
        {
            get { return _ownerChanged; }
            set
            {
                _ownerChanged = value;
                //m_log.DebugFormat(
                //    "[TASK INVENTORY ITEM]: Owner changed set {0} for {1} {2} owned by {3}",
                //    _ownerChanged, Name, ItemID, OwnerID);
            }
        }

        /// <summary>
        /// This used ONLY during copy. It can't be relied on at other times!
        /// </summary>
        /// <remarks>
        /// For true script running status, use IEntityInventory.TryGetScriptInstanceRunning() for now.
        /// </remarks>
        public bool ScriptRunning { get; set; }

        // See ICloneable

        #region ICloneable Members

        public Object Clone()
        {
            return MemberwiseClone();
        }

        #endregion

        /// <summary>
        /// Reset the UUIDs for this item.
        /// </summary>
        /// <param name="partID">The new part ID to which this item belongs</param>
        public void ResetIDs(UUID partID)
        {
            LoadedItemID = OldItemID;
            OldItemID = ItemID;
            ItemID = UUID.Random();
            ParentPartID = partID;
            ParentID = partID;
        }

        public TaskInventoryItem()
        {
            ScriptRunning = true;
            CreationDate = (uint)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
        }
    }
}
