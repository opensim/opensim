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

using libsecondlife;

namespace OpenSim.Framework
{
    /// <summary>
    /// A Class for folders which contain users inventory
    /// </summary>
    public class InventoryFolderBase
    {
        /// <summary>
        /// The UUID for this folder
        /// </summary>
        private LLUUID _id;

        /// <summary>
        /// The name of the folder (64 characters or less)
        /// </summary>
        private string _name;

        /// <summary>
        /// The agent who's inventory this is contained by
        /// </summary>
        private LLUUID _owner;

        /// <summary>
        /// The folder this folder is contained in 
        /// </summary>
        private LLUUID _parentID;

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

        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        public LLUUID Owner
        {
            get { return _owner; }
            set { _owner = value; }
        }

        public LLUUID ParentID
        {
            get { return _parentID; }
            set { _parentID = value; }
        }

        public LLUUID ID
        {
            get { return _id; }
            set { _id = value; }
        }

        public short Type
        {
            get { return _type; }
            set { _type = value; }
        }

        public ushort Version
        {
            get { return _version; }
            set { _version = value; }
        }
    }
}