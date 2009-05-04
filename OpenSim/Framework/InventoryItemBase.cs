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
using OpenMetaverse;

namespace OpenSim.Framework
{
    /// <summary>
    /// Inventory Item - contains all the properties associated with an individual inventory piece.
    /// </summary>
    public class InventoryItemBase : InventoryNodeBase, ICloneable
    {       
        /// <summary>
        /// The inventory type of the item.  This is slightly different from the asset type in some situations.
        /// </summary>        
        public int InvType;

        /// <summary>
        /// The folder this item is contained in
        /// </summary>        
        public UUID Folder;

        /// <value>
        /// The creator of this item
        /// </value>        
        public string CreatorId
        {
            get { return m_creatorId; }
            set 
            { 
                m_creatorId = value;
                UUID creatorIdAsUuid;
                
                // For now, all IDs are UUIDs
                UUID.TryParse(m_creatorId, out creatorIdAsUuid);
                CreatorIdAsUuid = creatorIdAsUuid;
            }
        }

        private string m_creatorId = String.Empty;        
        
        /// <value>
        /// The creator of this item expressed as a UUID
        /// </value>
        public UUID CreatorIdAsUuid { get; private set; }       

        /// <summary>
        /// The description of the inventory item (must be less than 64 characters)
        /// </summary>
        public string Description = String.Empty;

        /// <value>
        ///
        /// </value>          
        public uint NextPermissions;

        /// <summary>
        /// A mask containing permissions for the current owner (cannot be enforced)
        /// </summary>        
        public uint CurrentPermissions;

        /// <summary>
        ///
        /// </summary>        
        public uint BasePermissions;

        /// <summary>
        ///
        /// </summary>        
        public uint EveryOnePermissions;
        
        /// <summary>
        ///
        /// </summary>        
        public uint GroupPermissions;

        /// <summary>
        /// This is an enumerated value determining the type of asset (eg Notecard, Sound, Object, etc)
        /// </summary>        
        public int AssetType;

        /// <summary>
        /// The UUID of the associated asset on the asset server
        /// </summary>        
        public UUID AssetID;

        /// <summary>
        ///
        /// </summary>        
        public UUID GroupID;

        /// <summary>
        ///
        /// </summary>        
        public bool GroupOwned;

        /// <summary>
        ///
        /// </summary>        
        public int SalePrice;

        /// <summary>
        ///
        /// </summary>        
        public byte SaleType;

        /// <summary>
        ///
        /// </summary>        
        public uint Flags;

        /// <summary>
        ///
        /// </summary>        
        public int CreationDate;
        
        public InventoryItemBase()
        {
            CreationDate = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
        }
		
		public object Clone()
		{
			InventoryItemBase clone = new InventoryItemBase();
			clone.AssetID = AssetID;
			clone.AssetType = AssetType;
			clone.BasePermissions = BasePermissions;
			clone.CreationDate = CreationDate;
			clone.CreatorId = CreatorId;
			
		}
    }
}
