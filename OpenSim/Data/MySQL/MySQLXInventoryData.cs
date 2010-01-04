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
using System.Data;
using System.Reflection;
using System.Collections.Generic;
using log4net;
using MySql.Data.MySqlClient;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data.MySQL
{
    /// <summary>
    /// A MySQL Interface for the Asset Server
    /// </summary>
    public class MySQLXInventoryData : IXInventoryData
    {
        private static readonly ILog m_log = LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private MySQLGenericTableHandler<XInventoryFolder> m_Folders;
        private MySqlItemHandler m_Items;

        public MySQLXInventoryData(string conn, string realm)
        {
            m_Folders = new MySQLGenericTableHandler<XInventoryFolder>(
                    conn, "inventoryfolders", "InventoryStore");
            m_Items = new MySqlItemHandler(
                    conn, "inventoryitems", String.Empty);
        }

        public XInventoryFolder[] GetFolders(string[] fields, string[] vals)
        {
            return m_Folders.Get(fields, vals);
        }

        public XInventoryItem[] GetItems(string[] fields, string[] vals)
        {
            return m_Items.Get(fields, vals);
        }

        public bool StoreFolder(XInventoryFolder folder)
        {
            return m_Folders.Store(folder);
        }

        public bool StoreItem(XInventoryItem item)
        {
            return m_Items.Store(item);
        }

        public bool DeleteFolders(string field, string val)
        {
            return m_Folders.Delete(field, val);
        }

        public bool DeleteItems(string field, string val)
        {
            return m_Items.Delete(field, val);
        }

        public bool MoveItem(string principalID, string id, string newParent)
        {
            return m_Items.MoveItem(principalID, id, newParent);
        }
    }

    public class MySqlItemHandler : MySQLGenericTableHandler<XInventoryItem>
    {
        public MySqlItemHandler(string c, string t, string m) :
                base(c, t, m)
        {
        }

        public bool MoveItem(string principalID, string id, string newParent)
        {
            MySqlCommand cmd = new MySqlCommand();

            cmd.CommandText = String.Format("update {0} set parentFolderID = ?ParentFolderID where agentID = ?AgentID and folderID = ?FolderID");
            cmd.Parameters.AddWithValue("?ParentFolderID", newParent);
            cmd.Parameters.AddWithValue("?FolderID", id);
            cmd.Parameters.AddWithValue("?AgentID", principalID);

            return ExecuteNonQuery(cmd) == 0 ? false : true;
        }
    }
}
