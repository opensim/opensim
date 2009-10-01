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

namespace OpenSim.Framework
{
    /// <summary>
    /// Inventory Item - contains all the properties associated with an individual inventory piece.
    /// </summary>
    public class InventoryItemBase : InventoryNodeBase, ICloneable
    {
        /// <value>
        /// The inventory type of the item.  This is slightly different from the asset type in some situations.
        /// </value>
        public int InvType 
        { 
            get
            {
                return m_invType;
            }
            
            set
            {
                m_invType = value;
            }
        }
        protected int m_invType;

        /// <value>
        /// The folder this item is contained in
        /// </value>
        public UUID Folder 
        { 
            get
            {
                return m_folder;
            }
            
            set
            {
                m_folder = value;
            }
        }
        protected UUID m_folder;

        /// <value>
        /// The creator of this item
        /// </value>
        public string CreatorId 
        { 
            get
            {
                return m_creatorId; 
            }
            
            set
            {
                m_creatorId = value;
            }
        }
        protected string m_creatorId;

        /// <value>
        /// The UUID for the creator.  This may be different from the canonical CreatorId.  This property is used
        /// for communication with the client over the Second Life protocol, since that protocol can only understand
        /// UUIDs.  As this is a basic framework class, this means that both the string creator id and the uuid
        /// reference have to be settable separately
        ///
        /// Database plugins don't need to set this, it will be set by
        /// upstream code (or set by the get accessor if left unset).
        ///
        /// XXX: An alternative to having a separate uuid property would be to hash the CreatorId appropriately
        /// every time there was communication with a UUID-only client.  This may be much more expensive.
        /// </value>
        public UUID CreatorIdAsUuid 
        {
            get
            {
                if (UUID.Zero == m_creatorIdAsUuid)
                {
                    UUID.TryParse(CreatorId, out m_creatorIdAsUuid);
                }

                return m_creatorIdAsUuid;
            }
            
            set
            {
                m_creatorIdAsUuid = value;
            }
        }
        protected UUID m_creatorIdAsUuid = UUID.Zero;

        /// <value>
        /// The description of the inventory item (must be less than 64 characters)
        /// </value>
        public string Description 
        { 
            get
            {
                return m_description;
            }
            
            set
            {
                m_description = value;
            }
        }
        protected string m_description = String.Empty;

        /// <value>
        ///
        /// </value>
        public uint NextPermissions 
        { 
            get
            {
                return m_nextPermissions;
            }
            
            set
            {
                m_nextPermissions = value;
            }
        }
        protected uint m_nextPermissions;

        /// <value>
        /// A mask containing permissions for the current owner (cannot be enforced)
        /// </value>
        public uint CurrentPermissions 
        { 
            get
            {
                return m_currentPermissions;
            }
            
            set
            {
                m_currentPermissions = value;
            }
        }
        protected uint m_currentPermissions;

        /// <value>
        ///
        /// </value>
        public uint BasePermissions 
        { 
            get
            {
                return m_basePermissions;
            }
            
            set
            {
                m_basePermissions = value;
            }
        }
        protected uint m_basePermissions;

        /// <value>
        ///
        /// </value>
        public uint EveryOnePermissions 
        { 
            get
            {
                return m_everyonePermissions;
            }
            
            set
            {
                m_everyonePermissions = value;
            }
        }
        protected uint m_everyonePermissions;

        /// <value>
        ///
        /// </value>
        public uint GroupPermissions 
        { 
            get
            {
                return m_groupPermissions;
            }
            
            set
            {
                m_groupPermissions = value;
            }
        }
        protected uint m_groupPermissions;

        /// <value>
        /// This is an enumerated value determining the type of asset (eg Notecard, Sound, Object, etc)
        /// </value>
        public int AssetType 
        { 
            get
            {
                return m_assetType;
            }
            
            set
            {
                m_assetType = value;
            }
        }
        protected int m_assetType;

        /// <value>
        /// The UUID of the associated asset on the asset server
        /// </value>
        public UUID AssetID 
        { 
            get
            {
                return m_assetID;
            }
            
            set
            {
                m_assetID = value;
            }
        }
        protected UUID m_assetID;

        /// <value>
        ///
        /// </value>
        public UUID GroupID 
        { 
            get
            {
                return m_groupID;
            }
            
            set
            {
                m_groupID = value;
            }
        }
        protected UUID m_groupID;

        /// <value>
        ///
        /// </value>
        public bool GroupOwned 
        { 
            get
            {
                return m_groupOwned;
            }
                
            set
            {
                m_groupOwned = value;
            }
        }
        protected bool m_groupOwned;

        /// <value>
        ///
        /// </value>
        public int SalePrice 
        { 
            get
            {
                return m_salePrice;
            }
            
            set
            {
                m_salePrice = value;
            }
        }
        protected int m_salePrice;

        /// <value>
        ///
        /// </value>
        public byte SaleType 
        { 
            get
            {
                return m_saleType;
            }
            
            set
            {
                m_saleType = value;
            }
        }
        protected byte m_saleType;

        /// <value>
        ///
        /// </value>
        public uint Flags 
        { 
            get
            {
                return m_flags;
            }
            
            set
            {
                m_flags = value;
            }
        }
        protected uint m_flags;

        /// <value>
        ///
        /// </value>
        public int CreationDate 
        { 
            get
            {
                return m_creationDate;
            }
            
            set
            {
                m_creationDate = value;
            }
        }
        protected int m_creationDate = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

        public InventoryItemBase()
        {
        }

        public InventoryItemBase(UUID id)
        {
            ID = id;
        }

        public InventoryItemBase(UUID id, UUID owner)
        {
            ID = id;
            Owner = owner;
        }

        public object Clone()
        {
            return MemberwiseClone();
        }
    }
}
