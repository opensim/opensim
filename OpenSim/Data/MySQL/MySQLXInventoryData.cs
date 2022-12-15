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
using System.Data;
using System.Text;
using System.Reflection;
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
        private MySqlFolderHandler m_Folders;
        private MySqlItemHandler m_Items;


        public MySQLXInventoryData(string conn, string realm)
        {
            m_Folders = new MySqlFolderHandler(
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

        public XInventoryItem[] GetItems(string field, string[] vals)
        {
            return m_Items.Get(field, vals);
        }

        public XInventoryItem[] GetItems(string fields, string val)
        {
            return m_Items.Get(fields, val);
        }

        public bool StoreFolder(XInventoryFolder folder)
        {
            if (folder.folderName.Length > 64)
                folder.folderName = folder.folderName.Substring(0, 64);

            return m_Folders.Store(folder);
        }

        public bool StoreItem(XInventoryItem item)
        {
            if (item.inventoryName.Length > 64)
                item.inventoryName = item.inventoryName.Substring(0, 64);
            if (item.inventoryDescription.Length > 128)
                item.inventoryDescription = item.inventoryDescription.Substring(0, 128);

            return m_Items.Store(item);
        }

        public bool DeleteFolders(string field, string val)
        {
            return m_Folders.Delete(field, val);
        }

        public bool DeleteFolders(string[] fields, string[] vals)
        {
            return m_Folders.Delete(fields, vals);
        }

        public bool DeleteItems(string field, string val)
        {
            return m_Items.Delete(field, val);
        }

        public bool DeleteItems(string[] fields, string[] vals)
        {
            return m_Items.Delete(fields, vals);
        }

        public bool MoveItem(string id, string newParent)
        {
            return m_Items.MoveItem(id, newParent);
        }

        public bool MoveItems(string[] ids, string[] newParents)
        {
            return m_Items.MoveItems(ids, newParents);
        }

        public bool MoveFolder(string id, string newParent)
        {
            return m_Folders.MoveFolder(id, newParent);
        }

        public XInventoryItem[] GetActiveGestures(UUID principalID)
        {
            return m_Items.GetActiveGestures(principalID);
        }

        public int GetAssetPermissions(UUID principalID, UUID assetID)
        {
            return m_Items.GetAssetPermissions(principalID, assetID);
        }
    }

    public class MySqlItemHandler : MySqlInventoryHandler<XInventoryItem>
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public MySqlItemHandler(string c, string t, string m) :
                base(c, t, m)
        {
        }

        public override bool Delete(string field, string val)
        {
            XInventoryItem[] retrievedItems = Get(new string[] { field }, new string[] { val });
            if (retrievedItems.Length == 0)
                return false;

            if (!base.Delete(field, val))
                return false;

            IncrementFolderVersion(retrievedItems[0].parentFolderID);

            return true;
        }

        public override bool Delete(string[] fields, string[] vals)
        {
            XInventoryItem[] retrievedItems = Get(fields, vals);
            if (retrievedItems.Length == 0)
                return false;

            if (!base.Delete(fields, vals))
                return false;

            HashSet<UUID> deletedItemFolderUUIDs = new HashSet<UUID>();

            Array.ForEach<XInventoryItem>(retrievedItems, i => deletedItemFolderUUIDs.Add(i.parentFolderID));

            foreach (UUID deletedItemFolderUUID in deletedItemFolderUUIDs)
                IncrementFolderVersion(deletedItemFolderUUID);

            return true;
        }

        public bool MoveItem(string id, string newParent)
        {
            XInventoryItem[] retrievedItems = Get("inventoryID", id);
            if (retrievedItems.Length == 0)
                return false;

            UUID oldParent = retrievedItems[0].parentFolderID;

            using (MySqlCommand cmd = new MySqlCommand())
            {
                cmd.CommandText = String.Format("update {0} set parentFolderID = ?ParentFolderID where inventoryID = ?InventoryID", m_Realm);
                cmd.Parameters.AddWithValue("?ParentFolderID", newParent);
                cmd.Parameters.AddWithValue("?InventoryID", id);

                if (ExecuteNonQuery(cmd) == 0)
                    return false;
            }

            IncrementFolderVersion(newParent);
            if(oldParent.ToString() != newParent)
                IncrementFolderVersion(oldParent);

            return true;
        }

        public bool MoveItems(string[] ids, string[] newParents)
        {
            int len = ids.Length;
            if(len == 0)
                return false;

            MySqlConnection dbcon;
            MySqlCommand cmd;
            MySqlDataReader rdr;
            try
            {
                dbcon = new MySqlConnection(m_connectionString);
                dbcon.Open();
            }
            catch
            {
                return false;
            }

            HashSet<string> changedfolders = new HashSet<string>();
            try
            {
                UUID utmp;

                int flast = len - 1;
                StringBuilder sb = new StringBuilder(1024);
                sb.AppendFormat("select parentFolderID from {0} where inventoryID IN ('", m_Realm);
                for (int i = 0 ; i < len ; ++i)
                {
                    sb.Append(ids[i]);
                    if(i < flast)
                        sb.Append("','");
                    else
                        sb.Append("')");
                }

                string[] oldparents = new string[len];
                int l = 0;
                using (cmd = new MySqlCommand())
                {
                    cmd.CommandText = sb.ToString();
                    cmd.Connection = dbcon;
                    rdr = cmd.ExecuteReader();
                    while (rdr.Read() && l < len)
                    {
                        if(!(rdr[0] is string))
                            oldparents[l++] = null;
                        else
                            oldparents[l++] = (string)rdr[0];
                    }
                    rdr.Close();
                }

                if(l == 0)
                    return false;

                l = 0;
                sb = new StringBuilder(1024);
                string sbformat = String.Format("insert into {0} (inventoryID,parentFolderID) values{1} on duplicate key update parentFolderID = values(parentFolderID)", m_Realm,"{0}");

                for(int k = 0; k < len; k++)
                {
                    string oldid = oldparents[k];
                    if(String.IsNullOrWhiteSpace(oldid) || !UUID.TryParse(oldid, out utmp))
                        continue;

                    string newParent = newParents[k];
                    if(String.IsNullOrWhiteSpace(newParent) || !UUID.TryParse(newParent, out utmp))
                        continue;

                    string id = ids[k];
                    if(id == oldid)
                        continue;

                    sb.AppendFormat("(\'{0}\',\'{1}\')",id, newParent);
                    if(k < flast)
                        sb.Append(",");
                    if(!changedfolders.Contains(newParent))
                        changedfolders.Add(newParent);

                    if(!changedfolders.Contains(oldid))
                        changedfolders.Add(oldid);

                    ++l;
                }


                if(l == 0)
                    return false;

                oldparents = null;
                newParents = null;
                ids = null;

                using (cmd = new MySqlCommand())
                {
                    using(MySqlTransaction trans = dbcon.BeginTransaction())
                    {
                        cmd.Connection = dbcon;
                        cmd.Transaction = trans;
                    
                        try
                        {
                            cmd.CommandText = string.Format(sbformat,sb.ToString());
                            int r = cmd.ExecuteNonQuery();

                            if(r == 2 * l)
                                trans.Commit();
                            else
                            {
                                // we did got insertions so need to bail out
                                trans.Rollback();
                                return false;
                            }
                        }
                        catch
                        {
                            trans.Rollback();
                            return false;
                        }
                    }
                }

                if(changedfolders.Count == 0)
                    return true;

                sb = new StringBuilder(256);
                sb.Append("insert into inventoryfolders (folderID) values");

                l = 0;
                flast = changedfolders.Count - 1;
                foreach(UUID uu in changedfolders)
                {
                    sb.AppendFormat("(\'{0}\')",uu);
                    if(l < flast)
                        sb.Append(",");
                    ++l;
                }

                changedfolders = null;

                sb.Append(" on duplicate key update version = version + 1");
                using (cmd = new MySqlCommand())
                {
                    using(MySqlTransaction trans = dbcon.BeginTransaction())
                    {
                        cmd.Connection = dbcon;
                        cmd.Transaction = trans;
                        cmd.CommandText = sb.ToString();
                        try
                        {
                            int r = cmd.ExecuteNonQuery();
                            if(r == 2 * l)
                                trans.Commit();
                            else
                            {
                                // we did got insertions so need to bail out
                                trans.Rollback();
                            }
                        }
                        catch
                        {
                            trans.Rollback();
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                    dbcon.Close();
            }    

            return true;
        }

        public XInventoryItem[] GetActiveGestures(UUID principalID)
        {
            using (MySqlCommand cmd  = new MySqlCommand())
            {
//                cmd.CommandText = String.Format("select * from inventoryitems where avatarId = ?uuid and assetType = ?type and flags & 1", m_Realm);

                cmd.CommandText = String.Format("select * from inventoryitems where avatarId = ?uuid and assetType = ?type and flags & 1");

                cmd.Parameters.AddWithValue("?uuid", principalID.ToString());
                cmd.Parameters.AddWithValue("?type", (int)AssetType.Gesture);

                return DoQuery(cmd);
            }
        }

        public int GetAssetPermissions(UUID principalID, UUID assetID)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand())
                {
                    cmd.Connection = dbcon;

//                    cmd.CommandText = String.Format("select bit_or(inventoryCurrentPermissions) as inventoryCurrentPermissions from inventoryitems where avatarID = ?PrincipalID and assetID = ?AssetID group by assetID", m_Realm);

                    cmd.CommandText = String.Format("select bit_or(inventoryCurrentPermissions) as inventoryCurrentPermissions from inventoryitems where avatarID = ?PrincipalID and assetID = ?AssetID group by assetID");

                    cmd.Parameters.AddWithValue("?PrincipalID", principalID.ToString());
                    cmd.Parameters.AddWithValue("?AssetID", assetID.ToString());

                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        int perms = 0;

                        if (reader.Read())
                        {
                            perms = Convert.ToInt32(reader["inventoryCurrentPermissions"]);
                        }

                        return perms;
                    }
                }
            }
        }

        public override bool Store(XInventoryItem item)
        {
            if (!base.Store(item))
                return false;

            IncrementFolderVersion(item.parentFolderID);

            return true;
        }
    }

    public class MySqlFolderHandler : MySqlInventoryHandler<XInventoryFolder>
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public MySqlFolderHandler(string c, string t, string m) :
                base(c, t, m)
        {
        }

        public bool MoveFolder(string id, string newParentFolderID)
        {
            XInventoryFolder[] folders = Get(new string[] { "folderID" }, new string[] { id });

            if (folders.Length == 0)
                return false;

            UUID oldParentFolderUUID = folders[0].parentFolderID;

            using (MySqlCommand cmd = new MySqlCommand())
            {
                cmd.CommandText
                    = String.Format(
                        "update {0} set parentFolderID = ?ParentFolderID where folderID = ?folderID", m_Realm);
                cmd.Parameters.AddWithValue("?ParentFolderID", newParentFolderID);
                cmd.Parameters.AddWithValue("?folderID", id);

                if (ExecuteNonQuery(cmd) == 0)
                    return false;
            }

            IncrementFolderVersion(oldParentFolderUUID);
            IncrementFolderVersion(newParentFolderID);

            return true;
        }

        public override bool Store(XInventoryFolder folder)
        {
            if (!base.Store(folder))
                return false;

            IncrementFolderVersion(folder.parentFolderID);

            return true;
        }
    }

    public class MySqlInventoryHandler<T> : MySQLGenericTableHandler<T> where T: class, new()
    {
        public MySqlInventoryHandler(string c, string t, string m) : base(c, t, m) {}

        protected bool IncrementFolderVersion(UUID folderID)
        {
            return IncrementFolderVersion(folderID.ToString());
        }

        protected bool IncrementFolderVersion(string folderID)
        {
//            m_log.DebugFormat("[MYSQL FOLDER HANDLER]: Incrementing version on folder {0}", folderID);
//            Util.PrintCallStack();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand())
                {
                    cmd.Connection = dbcon;

                    cmd.CommandText = String.Format("update inventoryfolders set version=version+1 where folderID = ?folderID");
                    cmd.Parameters.AddWithValue("?folderID", folderID);

                    try
                    {
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }

                dbcon.Close();
            }

            return true;
        }
    }
}