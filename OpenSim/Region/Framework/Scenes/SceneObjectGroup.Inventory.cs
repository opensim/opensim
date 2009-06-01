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
using System.IO;
using System.Reflection;
using OpenMetaverse;
using log4net;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using System.Collections.Generic;
using System.Xml;

namespace OpenSim.Region.Framework.Scenes
{
    public partial class SceneObjectGroup : EntityBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Force all task inventories of prims in the scene object to persist
        /// </summary>
        public void ForceInventoryPersistence()
        {
            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    part.Inventory.ForceInventoryPersistence();
                }
            }
        }

        /// <summary>
        /// Start the scripts contained in all the prims in this group.
        /// </summary>
        public void CreateScriptInstances(int startParam, bool postOnRez,
                string engine, int stateSource)
        {
            // Don't start scripts if they're turned off in the region!
            if (!m_scene.RegionInfo.RegionSettings.DisableScripts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    part.Inventory.CreateScriptInstances(startParam, postOnRez, engine, stateSource);
                }
            }
        }

        /// <summary>
        /// Stop the scripts contained in all the prims in this group
        /// </summary>
        public void RemoveScriptInstances()
        {
            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    part.Inventory.RemoveScriptInstances();
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="localID"></param>
        public bool GetPartInventoryFileName(IClientAPI remoteClient, uint localID)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                return part.Inventory.GetInventoryFileName(remoteClient, localID);
            }
            else
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: " +
                    "Couldn't find part {0} in object group {1}, {2} to retreive prim inventory",
                    localID, Name, UUID);
            }
            return false;
        }

        /// <summary>
        /// Return serialized inventory metadata for the given constituent prim
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="xferManager"></param>
        public void RequestInventoryFile(IClientAPI client, uint localID, IXfer xferManager)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                part.Inventory.RequestInventoryFile(client, xferManager);
            }
            else
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: " +
                    "Couldn't find part {0} in object group {1}, {2} to request inventory data",
                    localID, Name, UUID);
            }
        }

        /// <summary>
        /// Add an inventory item to a prim in this group.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="localID"></param>
        /// <param name="item"></param>
        /// <param name="copyItemID">The item UUID that should be used by the new item.</param>
        /// <returns></returns>
        public bool AddInventoryItem(IClientAPI remoteClient, uint localID,
                                     InventoryItemBase item, UUID copyItemID)
        {
            UUID newItemId = (copyItemID != UUID.Zero) ? copyItemID : item.ID;

            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                TaskInventoryItem taskItem = new TaskInventoryItem();

                taskItem.ItemID = newItemId;
                taskItem.AssetID = item.AssetID;
                taskItem.Name = item.Name;
                taskItem.Description = item.Description;
                taskItem.OwnerID = part.OwnerID; // Transfer ownership
                taskItem.CreatorID = item.CreatorIdAsUuid;
                taskItem.Type = item.AssetType;
                taskItem.InvType = item.InvType;

                if (remoteClient != null &&
                        remoteClient.AgentId != part.OwnerID &&
                        m_scene.Permissions.PropagatePermissions())
                {
                    taskItem.BasePermissions = item.BasePermissions &
                            item.NextPermissions;
                    taskItem.CurrentPermissions = item.CurrentPermissions &
                            item.NextPermissions;
                    taskItem.EveryonePermissions = item.EveryOnePermissions &
                            item.NextPermissions;
                    taskItem.GroupPermissions = item.GroupPermissions &
                            item.NextPermissions;
                    taskItem.NextPermissions = item.NextPermissions;
                    taskItem.CurrentPermissions |= 8;
                } else {
                    taskItem.BasePermissions = item.BasePermissions;
                    taskItem.CurrentPermissions = item.CurrentPermissions;
                    taskItem.CurrentPermissions |= 8;
                    taskItem.EveryonePermissions = item.EveryOnePermissions;
                    taskItem.GroupPermissions = item.GroupPermissions;
                    taskItem.NextPermissions = item.NextPermissions;
                }

                taskItem.Flags = item.Flags;
                // TODO: These are pending addition of those fields to TaskInventoryItem
//                taskItem.SalePrice = item.SalePrice;
//                taskItem.SaleType = item.SaleType;
                taskItem.CreationDate = (uint)item.CreationDate;

                bool addFromAllowedDrop = false;
                if (remoteClient!=null) 
                {
                    addFromAllowedDrop = remoteClient.AgentId != part.OwnerID;
                }

                part.Inventory.AddInventoryItem(taskItem, addFromAllowedDrop);

                return true;
            }
            else
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: " +
                    "Couldn't find prim local ID {0} in group {1}, {2} to add inventory item ID {3}",
                    localID, Name, UUID, newItemId);
            }

            return false;
        }

        /// <summary>
        /// Returns an existing inventory item.  Returns the original, so any changes will be live.
        /// </summary>
        /// <param name="primID"></param>
        /// <param name="itemID"></param>
        /// <returns>null if the item does not exist</returns>
        public TaskInventoryItem GetInventoryItem(uint primID, UUID itemID)
        {
            SceneObjectPart part = GetChildPart(primID);
            if (part != null)
            {
                return part.Inventory.GetInventoryItem(itemID);
            }
            else
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: " +
                    "Couldn't find prim local ID {0} in prim {1}, {2} to get inventory item ID {3}",
                    primID, part.Name, part.UUID, itemID);
            }

            return null;
        }

        /// <summary>
        /// Update an existing inventory item.
        /// </summary>
        /// <param name="item">The updated item.  An item with the same id must already exist
        /// in this prim's inventory</param>
        /// <returns>false if the item did not exist, true if the update occurred succesfully</returns>
        public bool UpdateInventoryItem(TaskInventoryItem item)
        {
            SceneObjectPart part = GetChildPart(item.ParentPartID);
            if (part != null)
            {
                part.Inventory.UpdateInventoryItem(item);

                return true;
            }
            else
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: " +
                    "Couldn't find prim ID {0} to update item {1}, {2}",
                    item.ParentPartID, item.Name, item.ItemID);
            }

            return false;
        }

        public int RemoveInventoryItem(uint localID, UUID itemID)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                int type = part.Inventory.RemoveInventoryItem(itemID);

                return type;
            }

            return -1;
        }

        public uint GetEffectivePermissions()
        {
            uint perms=(uint)(PermissionMask.Modify |
                              PermissionMask.Copy |
                              PermissionMask.Move |
                              PermissionMask.Transfer) | 7;

            uint ownerMask = 0x7ffffff;
            foreach (SceneObjectPart part in m_parts.Values)
            {
                ownerMask &= part.OwnerMask;
                perms &= part.Inventory.MaskEffectivePermissions();
            }

            if ((ownerMask & (uint)PermissionMask.Modify) == 0)
                perms &= ~(uint)PermissionMask.Modify;
            if ((ownerMask & (uint)PermissionMask.Copy) == 0)
                perms &= ~(uint)PermissionMask.Copy;
            if ((ownerMask & (uint)PermissionMask.Transfer) == 0)
                perms &= ~(uint)PermissionMask.Transfer;

            if ((ownerMask & RootPart.NextOwnerMask & (uint)PermissionMask.Modify) == 0)
                perms &= ~((uint)PermissionMask.Modify >> 13);
            if ((ownerMask & RootPart.NextOwnerMask & (uint)PermissionMask.Copy) == 0)
                perms &= ~((uint)PermissionMask.Copy >> 13);
            if ((ownerMask & RootPart.NextOwnerMask & (uint)PermissionMask.Transfer) == 0)
                perms &= ~((uint)PermissionMask.Transfer >> 13);

            return perms;
        }

        public void ApplyNextOwnerPermissions()
        {
            foreach (SceneObjectPart part in m_parts.Values)
            {
                part.ApplyNextOwnerPermissions();
            }
        }

        public string GetStateSnapshot()
        {
            //m_log.Debug(" >>> GetStateSnapshot <<<");

            List<string> assemblies = new List<string>();
            Dictionary<UUID, string> states = new Dictionary<UUID, string>();

            foreach (SceneObjectPart part in m_parts.Values)
            {
                foreach (string a in part.Inventory.GetScriptAssemblies())
                {
                    if (a != "" && !assemblies.Contains(a))
                        assemblies.Add(a);
                }

                foreach (KeyValuePair<UUID, string> s in part.Inventory.GetScriptStates())
                {
                    states[s.Key] = s.Value;
                }
            }

            if (states.Count < 1 || assemblies.Count < 1)
                return "";

            XmlDocument xmldoc = new XmlDocument();

            XmlNode xmlnode = xmldoc.CreateNode(XmlNodeType.XmlDeclaration,
                    "", "");

            xmldoc.AppendChild(xmlnode);
            XmlElement rootElement = xmldoc.CreateElement("", "ScriptData",
                    "");
            
            xmldoc.AppendChild(rootElement);

            XmlElement wrapper = xmldoc.CreateElement("", "Assemblies",
                    "");
            
            rootElement.AppendChild(wrapper);

            foreach (string assembly in assemblies)
            {
                string fn = Path.GetFileName(assembly);
                if (fn == String.Empty)
                    continue;

                String filedata = String.Empty;

                if (File.Exists(assembly+".text"))
                {
                    FileInfo tfi = new FileInfo(assembly+".text");

                    if (tfi == null)
                        continue;

                    Byte[] tdata = new Byte[tfi.Length];

                    try
                    {
                        FileStream tfs = File.Open(assembly+".text", FileMode.Open, FileAccess.Read);
                        tfs.Read(tdata, 0, tdata.Length);
                        tfs.Close();
                    }
                    catch (Exception e)
                    {
                        m_log.DebugFormat("[SOG]: Unable to open script textfile {0}, reason: {1}", assembly+".text", e.Message);
                    }

                    filedata = new System.Text.ASCIIEncoding().GetString(tdata);
                }
                else
                {
                    FileInfo fi = new FileInfo(assembly);

                    if (fi == null)
                        continue;

                    Byte[] data = new Byte[fi.Length];

                    try
                    {
                        FileStream fs = File.Open(assembly, FileMode.Open, FileAccess.Read);
                        fs.Read(data, 0, data.Length);
                        fs.Close();
                    }
                    catch (Exception e)
                    {
                        m_log.DebugFormat("[SOG]: Unable to open script assembly {0}, reason: {1}", assembly, e.Message);
                    }

                    filedata = System.Convert.ToBase64String(data);
                }
                XmlElement assemblyData = xmldoc.CreateElement("", "Assembly", "");
                XmlAttribute assemblyName = xmldoc.CreateAttribute("", "Filename", "");
                assemblyName.Value = fn;
                assemblyData.Attributes.Append(assemblyName);

                assemblyData.InnerText = filedata;

                wrapper.AppendChild(assemblyData);
            }

            wrapper = xmldoc.CreateElement("", "ScriptStates",
                    "");
            
            rootElement.AppendChild(wrapper);

            foreach (KeyValuePair<UUID, string> state in states)
            {
                XmlElement stateData = xmldoc.CreateElement("", "State", "");

                XmlAttribute stateID = xmldoc.CreateAttribute("", "UUID", "");
                stateID.Value = state.Key.ToString();
                stateData.Attributes.Append(stateID);

                XmlDocument sdoc = new XmlDocument();
                sdoc.LoadXml(state.Value);
                XmlNodeList rootL = sdoc.GetElementsByTagName("ScriptState");
                XmlNode rootNode = rootL[0];

                XmlNode newNode = xmldoc.ImportNode(rootNode, true);
                stateData.AppendChild(newNode);
                wrapper.AppendChild(stateData);
            }

            return xmldoc.InnerXml;
        }

        public void SetState(string objXMLData, UUID RegionID)
        {
            if (objXMLData == String.Empty)
                return;

            XmlDocument doc = new XmlDocument();
            try
            {
                doc.LoadXml(objXMLData);
            }
            catch (Exception) // (System.Xml.XmlException)
            {
                // We will get here if the XML is invalid or in unit
                // tests. Really should determine which it is and either
                // fail silently or log it
                // Fail silently, for now.
                // TODO: Fix this
                //
                return;
            }

            XmlNodeList rootL = doc.GetElementsByTagName("ScriptData");
            if (rootL.Count == 1)
            {
                XmlNode rootNode = rootL[0];
                if (rootNode != null)
                {
                    XmlNodeList partL = rootNode.ChildNodes;

                    foreach (XmlNode part in partL)
                    {
                        XmlNodeList nodeL = part.ChildNodes;

                        switch (part.Name)
                        {
                            case "Assemblies":
                                foreach (XmlNode asm in nodeL)
                                {
                                    string fn = asm.Attributes.GetNamedItem("Filename").Value;

                                    Byte[] filedata = Convert.FromBase64String(asm.InnerText);
                                    string path = Path.Combine("ScriptEngines", RegionID.ToString());
                                    path = Path.Combine(path, fn);

                                    if (!File.Exists(path))
                                    {
                                        FileStream fs = File.Create(path);
                                        fs.Write(filedata, 0, filedata.Length);
                                        fs.Close();

                                        Byte[] textbytes = new System.Text.ASCIIEncoding().GetBytes(asm.InnerText);
                                        fs = File.Create(path+".text");
                                        fs.Write(textbytes, 0, textbytes.Length);
                                        fs.Close();
                                    }
                                }
                                break;
                            case "ScriptStates":
                                foreach (XmlNode st in nodeL)
                                {
                                    string id = st.Attributes.GetNamedItem("UUID").Value;
                                    UUID uuid = new UUID(id);
                                    XmlNode state = st.ChildNodes[0];

                                    XmlDocument sdoc = new XmlDocument();
                                    XmlNode sxmlnode = sdoc.CreateNode(
                                            XmlNodeType.XmlDeclaration,
                                            "", "");
                                    sdoc.AppendChild(sxmlnode);

                                    XmlNode newnode = sdoc.ImportNode(state, true);
                                    sdoc.AppendChild(newnode);

                                    string spath = Path.Combine("ScriptEngines", RegionID.ToString());
                                    spath = Path.Combine(spath, uuid.ToString());
                                    FileStream sfs = File.Create(spath + ".state");
                                    System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
                                    Byte[] buf = enc.GetBytes(sdoc.InnerXml);
                                    sfs.Write(buf, 0, buf.Length);
                                    sfs.Close();
                                }
                                break;
                        }
                    }
                }
            }
        }
    }
}
