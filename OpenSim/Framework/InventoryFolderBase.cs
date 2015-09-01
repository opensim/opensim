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

using OpenMetaverse;

namespace OpenSim.Framework
{
    /// <summary>
    /// User inventory folder
    /// </summary>
    public class InventoryFolderBase : InventoryNodeBase
    {
        public static readonly string ROOT_FOLDER_NAME = "My Inventory";
        public static readonly string SUITCASE_FOLDER_NAME = "My Suitcase";

        /// <summary>
        /// The folder this folder is contained in
        /// </summary>
        private UUID _parentID;

        /// <summary>
        /// Type of items normally stored in this folder
        /// </summary>
        private short _type;

        /// <summary>
        /// This is used to denote the version of the client, needed
        /// because of the changes clients have with inventory from
        /// time to time (1.19.1 caused us some fits there).
        /// </summary>
        private ushort _version;

        public virtual UUID ParentID
        {
            get { return _parentID; }
            set { _parentID = value; }
        }

        public virtual short Type
        {
            get { return _type; }
            set { _type = value; }
        }

        public virtual ushort Version
        {
            get { return _version; }
            set { _version = value; }
        }

        public InventoryFolderBase()
        {
        }

        public InventoryFolderBase(UUID id) : this()
        {
            ID = id;
        }

        public InventoryFolderBase(UUID id, UUID owner) : this(id)
        {
            Owner = owner;
        }

        public InventoryFolderBase(UUID id, string name, UUID owner, UUID parent) : this(id, owner)
        {
            Name = name;
            ParentID = parent;
        }

        public InventoryFolderBase(
            UUID id, string name, UUID owner, short type, UUID parent, ushort version) : this(id, name, owner, parent)
        {
            Type = type;
            Version = version;
        }
    }
}