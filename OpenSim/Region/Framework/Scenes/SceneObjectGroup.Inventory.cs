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
using PermissionMask = OpenSim.Framework.PermissionMask;

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
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                parts[i].Inventory.ForceInventoryPersistence();
        }

        /// <summary>
        /// Start the scripts contained in all the prims in this group.
        /// </summary>
        /// <param name="startParam"></param>
        /// <param name="postOnRez"></param>
        /// <param name="engine"></param>
        /// <param name="stateSource"></param>
        /// <returns>
        /// Number of scripts that were valid for starting.  This does not guarantee that all these scripts
        /// were actually started, but just that the start could be attempt (e.g. the asset data for the script could be found)
        /// </returns>
        public int CreateScriptInstances(int startParam, bool postOnRez, string engine, int stateSource)
        {
            int scriptsStarted = 0;

            if (m_scene == null)
            {
                m_log.DebugFormat("[PRIM INVENTORY]: m_scene is null. Unable to create script instances");
                return 0;
            }

            // Don't start scripts if they're turned off in the region!
            if (!m_scene.RegionInfo.RegionSettings.DisableScripts)
            {
                SceneObjectPart[] parts = m_parts.GetArray();
                for (int i = 0; i < parts.Length; i++)
                    scriptsStarted
                        += parts[i].Inventory.CreateScriptInstances(startParam, postOnRez, engine, stateSource);
            }

            return scriptsStarted;
        }

        /// <summary>
        /// Stop and remove the scripts contained in all the prims in this group
        /// </summary>
        public void RemoveScriptInstances(bool sceneObjectBeingDeleted)
        {
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                parts[i].Inventory.RemoveScriptInstances(sceneObjectBeingDeleted);
        }

        /// <summary>
        /// Stop the scripts contained in all the prims in this group
        /// </summary>
        public void StopScriptInstances()
        {
            Array.ForEach<SceneObjectPart>(m_parts.GetArray(), p => p.Inventory.StopScriptInstances());
        }

        public void SendReleaseScriptsControl()
        {
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                parts[i].Inventory.SendReleaseScriptsControl();
        }

        public void RemoveScriptsPermissions(int permissions)
        {
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                parts[i].Inventory.RemoveScriptsPermissions(permissions);
        }

        public void RemoveScriptsPermissions(ScenePresence sp, int permissions)
        {
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                parts[i].Inventory.RemoveScriptsPermissions(sp, permissions);
        }

        /// <summary>
        /// Add an inventory item from a user's inventory to a prim in this scene object.
        /// </summary>
        /// <param name="agentID">The agent adding the item.</param>
        /// <param name="localID">The local ID of the part receiving the add.</param>
        /// <param name="item">The user inventory item being added.</param>
        /// <param name="copyItemID">The item UUID that should be used by the new item.</param>
        /// <returns></returns>
        public bool AddInventoryItem(UUID agentID, uint localID, InventoryItemBase item, UUID copyItemID, bool withModRights = true)
        {
//            m_log.DebugFormat(
//                "[PRIM INVENTORY]: Adding inventory item {0} from {1} to part with local ID {2}",
//                item.Name, remoteClient.Name, localID);

            UUID newItemId = (copyItemID != UUID.Zero) ? copyItemID : item.ID;

            SceneObjectPart part = GetPart(localID);
            if (part == null)
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: " +
                    "Couldn't find prim local ID {0} in group {1}, {2} to add inventory item ID {3}",
                    localID, Name, UUID, newItemId);
                return false;
            }

            TaskInventoryItem taskItem = new TaskInventoryItem();

            taskItem.ItemID = newItemId;
            taskItem.AssetID = item.AssetID;
            taskItem.Name = item.Name;
            taskItem.Description = item.Description;
            taskItem.OwnerID = part.OwnerID; // Transfer ownership
            taskItem.CreatorID = item.CreatorIdAsUuid;
            taskItem.Type = item.AssetType;
            taskItem.InvType = item.InvType;
            taskItem.Flags = item.Flags;

            if (agentID != part.OwnerID && m_scene.Permissions.PropagatePermissions())
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
                // We're adding this to a prim we don't own. Force
                // owner change
                taskItem.Flags |= (uint)InventoryItemFlags.ObjectSlamPerm;
                taskItem.LastOwnerID = item.Owner;
            }
            else
            {
                taskItem.BasePermissions = item.BasePermissions;
                taskItem.CurrentPermissions = item.CurrentPermissions;
                taskItem.EveryonePermissions = item.EveryOnePermissions;
                taskItem.GroupPermissions = item.GroupPermissions;
                taskItem.NextPermissions = item.NextPermissions;
            }


            // m_log.DebugFormat(
            //      "[PRIM INVENTORY]: Flags are 0x{0:X} for item {1} added to part {2} by {3}",
            //       taskItem.Flags, taskItem.Name, localID, remoteClient.Name);

            // TODO: These are pending addition of those fields to TaskInventoryItem
            // taskItem.SalePrice = item.SalePrice;
            // taskItem.SaleType = item.SaleType;
            taskItem.CreationDate = (uint)item.CreationDate;
                
            bool addFromAllowedDrop;
            if(withModRights)
                addFromAllowedDrop = false;
            else
                addFromAllowedDrop = (part.ParentGroup.RootPart.GetEffectiveObjectFlags() & (uint)PrimFlags.AllowInventoryDrop) != 0;

            part.Inventory.AddInventoryItem(taskItem, addFromAllowedDrop);
            part.ParentGroup.InvalidateEffectivePerms();
            return true;

        }

        /// <summary>
        /// Returns an existing inventory item.  Returns the original, so any changes will be live.
        /// </summary>
        /// <param name="primID"></param>
        /// <param name="itemID"></param>
        /// <returns>null if the item does not exist</returns>
        public TaskInventoryItem GetInventoryItem(uint primID, UUID itemID)
        {
            SceneObjectPart part = GetPart(primID);
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
            SceneObjectPart part = GetPart(item.ParentPartID);
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
            SceneObjectPart part = GetPart(localID);
            if (part != null)
            {
                int type = part.Inventory.RemoveInventoryItem(itemID);

                return type;
            }

            return -1;
        }

        // new test code, to place in better place later
        private object m_PermissionsLock = new object();
        private bool m_EffectivePermsInvalid = true;
        private bool m_DeepEffectivePermsInvalid = true;

        // should called when parts chanced  by their contents did not, so we know their cacche is valid
        // in case of doubt call InvalidateDeepEffectivePerms(), it only costs a bit more cpu time
        public void InvalidateEffectivePerms()
        {
            lock(m_PermissionsLock)
                m_EffectivePermsInvalid = true;
        }

        // should called when parts chanced and their contents where accounted for
        public void InvalidateDeepEffectivePerms()
        {
            lock(m_PermissionsLock)
            {
                m_DeepEffectivePermsInvalid = true;
                m_EffectivePermsInvalid = true;
            }
        }

        private uint m_EffectiveEveryOnePerms;
        public uint EffectiveEveryOnePerms
        {
            get
            {
                lock(m_PermissionsLock)
                {
                    if(m_EffectivePermsInvalid)
                        AggregatePerms();
                    return m_EffectiveEveryOnePerms;
                }
            }
        }

        private uint m_EffectiveGroupPerms;
        public uint EffectiveGroupPerms
        {
            get
            {
                lock(m_PermissionsLock)
                {
                    if(m_EffectivePermsInvalid)
                        AggregatePerms();
                    return m_EffectiveGroupPerms;
                }
            }
        }

        private uint m_EffectiveGroupOrEveryOnePerms;
        public uint EffectiveGroupOrEveryOnePerms
        {
            get
            {
                lock(m_PermissionsLock)
                {
                    if(m_EffectivePermsInvalid)
                        AggregatePerms();
                    return m_EffectiveGroupOrEveryOnePerms;
                }
            }
        }

        private uint m_EffectiveOwnerPerms;
        public uint EffectiveOwnerPerms
        {
            get
            {
                lock(m_PermissionsLock)
                {
                    if(m_EffectivePermsInvalid)
                        AggregatePerms();
                    return m_EffectiveOwnerPerms;
                }
            }
        }

        public void AggregatePerms()
        {
            lock(m_PermissionsLock)
            {
                // aux
                const uint allmask = (uint)PermissionMask.AllEffective;
                const uint movemodmask = (uint)(PermissionMask.Move | PermissionMask.Modify);
                const uint copytransfermast = (uint)(PermissionMask.Copy | PermissionMask.Transfer);

                uint basePerms = (RootPart.BaseMask & allmask) | (uint)PermissionMask.Move;
                bool noBaseTransfer = (basePerms & (uint)PermissionMask.Transfer) == 0;

                uint rootOwnerPerms = RootPart.OwnerMask;
                uint owner = rootOwnerPerms;
                uint rootGroupPerms = RootPart.GroupMask;
                uint group = rootGroupPerms;
                uint rootEveryonePerms = RootPart.EveryoneMask;
                uint everyone = rootEveryonePerms;

                bool needUpdate = false;
                // date is time of writing april 30th 2017
                bool newobj = (RootPart.CreationDate == 0 || RootPart.CreationDate > 1493574994);
                SceneObjectPart[] parts = m_parts.GetArray();
                for (int i = 0; i < parts.Length; i++)
                {
                    SceneObjectPart part = parts[i];

                    if(m_DeepEffectivePermsInvalid)
                        part.AggregatedInnerPermsForGroup();

                    owner &= part.AggregatedInnerOwnerPerms; 
                    group &= part.AggregatedInnerGroupPerms;
                    if(newobj)
                        group &= part.AggregatedInnerGroupPerms;
                    if(newobj)
                        everyone &= part.AggregatedInnerEveryonePerms;
                }
                // recover modify and move
                rootOwnerPerms &= movemodmask;
                owner |= rootOwnerPerms;
                if((owner & copytransfermast) == 0)
                    owner |= (uint)PermissionMask.Transfer;

                owner &= basePerms;
                if(owner != m_EffectiveOwnerPerms)
                {
                    needUpdate = true;
                    m_EffectiveOwnerPerms = owner;
                }

                uint ownertransfermask = owner & (uint)PermissionMask.Transfer;

                // recover modify and move
                rootGroupPerms &= movemodmask;
                group |= rootGroupPerms;
                if(noBaseTransfer)
                    group &=~(uint)PermissionMask.Copy;
                else
                    group |= ownertransfermask;

                uint groupOrEveryone = group;
                uint tmpPerms = group & owner;
                if(tmpPerms != m_EffectiveGroupPerms)
                {
                    needUpdate = true;
                    m_EffectiveGroupPerms = tmpPerms;
                }

                // recover move
                rootEveryonePerms &= (uint)PermissionMask.Move;
                everyone |= rootEveryonePerms;
                everyone &= ~(uint)PermissionMask.Modify;
                if(noBaseTransfer)
                    everyone &=~(uint)PermissionMask.Copy;
                else
                    everyone |= ownertransfermask;

                groupOrEveryone |= everyone;

                tmpPerms = everyone  & owner;
                if(tmpPerms != m_EffectiveEveryOnePerms)
                {
                    needUpdate = true;
                    m_EffectiveEveryOnePerms = tmpPerms;
                }

                tmpPerms = groupOrEveryone  & owner;
                if(tmpPerms != m_EffectiveGroupOrEveryOnePerms)
                {
                    needUpdate = true;
                    m_EffectiveGroupOrEveryOnePerms = tmpPerms;
                }

                m_DeepEffectivePermsInvalid = false;
                m_EffectivePermsInvalid = false;
              
                if(needUpdate)
                    RootPart.ScheduleFullUpdate();
            }
        }

        public uint CurrentAndFoldedNextPermissions()
        {
            uint perms=(uint)(PermissionMask.Modify |
                              PermissionMask.Copy |
                              PermissionMask.Move |
                              PermissionMask.Transfer |
                              PermissionMask.FoldedMask);

            uint ownerMask = RootPart.OwnerMask;

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                ownerMask &= part.BaseMask;
                perms &= part.Inventory.MaskEffectivePermissions();
            }

            if ((ownerMask & (uint)PermissionMask.Modify) == 0)
                perms &= ~(uint)PermissionMask.Modify;
            if ((ownerMask & (uint)PermissionMask.Copy) == 0)
                perms &= ~(uint)PermissionMask.Copy;
            if ((ownerMask & (uint)PermissionMask.Transfer) == 0)
                perms &= ~(uint)PermissionMask.Transfer;
            if ((ownerMask & (uint)PermissionMask.Export) == 0)
                perms &= ~(uint)PermissionMask.Export;

            return perms;
        }

        public void ApplyNextOwnerPermissions()
        {
//            m_log.DebugFormat("[PRIM INVENTORY]: Applying next owner permissions to {0} {1}", Name, UUID);

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                parts[i].ApplyNextOwnerPermissions();
        }

        public string GetStateSnapshot()
        {
            Dictionary<UUID, string> states = new Dictionary<UUID, string>();

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                foreach (KeyValuePair<UUID, string> s in part.Inventory.GetScriptStates())
                    states[s.Key] = s.Value;
            }

            if (states.Count < 1)
                return String.Empty;

            XmlDocument xmldoc = new XmlDocument();

            XmlNode xmlnode = xmldoc.CreateNode(XmlNodeType.XmlDeclaration,
                    String.Empty, String.Empty);

            xmldoc.AppendChild(xmlnode);
            XmlElement rootElement = xmldoc.CreateElement("", "ScriptData",
                    String.Empty);

            xmldoc.AppendChild(rootElement);


            XmlElement wrapper = xmldoc.CreateElement("", "ScriptStates",
                    String.Empty);

            rootElement.AppendChild(wrapper);

            foreach (KeyValuePair<UUID, string> state in states)
            {
                XmlDocument sdoc = new XmlDocument();
                sdoc.LoadXml(state.Value);
                XmlNodeList rootL = sdoc.GetElementsByTagName("State");
                XmlNode rootNode = rootL[0];

                XmlNode newNode = xmldoc.ImportNode(rootNode, true);
                wrapper.AppendChild(newNode);
            }

            return xmldoc.InnerXml;
        }

        public void SetState(string objXMLData, IScene ins)
        {
            if (!(ins is Scene))
                return;

            Scene s = (Scene)ins;

            if (objXMLData == String.Empty)
                return;

            IScriptModule scriptModule = null;

            foreach (IScriptModule sm in s.RequestModuleInterfaces<IScriptModule>())
            {
                if (sm.ScriptEngineName == s.DefaultScriptEngine)
                    scriptModule = sm;
                else if (scriptModule == null)
                    scriptModule = sm;
            }

            if (scriptModule == null)
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
            if (rootL.Count != 1)
                return;

            XmlElement rootE = (XmlElement)rootL[0];

            XmlNodeList dataL = rootE.GetElementsByTagName("ScriptStates");
            if (dataL.Count != 1)
                return;

            XmlElement dataE = (XmlElement)dataL[0];

            foreach (XmlNode n in dataE.ChildNodes)
            {
                XmlElement stateE = (XmlElement)n;
                UUID itemID = new UUID(stateE.GetAttribute("UUID"));

                scriptModule.SetXMLState(itemID, n.OuterXml);
            }
        }

        public void ResumeScripts()
        {
            if (m_scene.RegionInfo.RegionSettings.DisableScripts)
                return;

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                parts[i].Inventory.ResumeScripts();
        }

        /// <summary>
        /// Returns true if any part in the scene object contains scripts, false otherwise.
        /// </summary>
        /// <returns></returns>
        public bool ContainsScripts()
        {
            foreach (SceneObjectPart part in Parts)
                if (part.Inventory.ContainsScripts())
                    return true;

            return false;
        }
    }
}
