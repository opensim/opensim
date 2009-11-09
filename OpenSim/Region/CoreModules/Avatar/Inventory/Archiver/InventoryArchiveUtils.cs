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
using System.Reflection;
using System.Text;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.Avatar.Inventory.Archiver
{
    /// <summary>
    /// Utility methods for inventory archiving
    /// </summary>
    public static class InventoryArchiveUtils
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // Character used for escaping the path delimter ("\/") and itself ("\\") in human escaped strings
        public static readonly char ESCAPE_CHARACTER = '\\';

        // The character used to separate inventory path components (different folders and items)
        public static readonly char PATH_DELIMITER = '/';

        /// <summary>
        /// Find a folder given a PATH_DELIMITER delimited path starting from a user's root folder
        /// </summary>
        ///
        /// This method does not handle paths that contain multiple delimitors
        ///
        /// FIXME: We do not yet handle situations where folders have the same name.  We could handle this by some
        /// XPath like expression
        ///
        /// FIXME: Delimitors which occur in names themselves are not currently escapable.
        ///
        /// <param name="inventoryService">
        /// Inventory service to query
        /// </param>
        /// <param name="userId">
        /// User id to search
        /// </param>
        /// <param name="path">
        /// The path to the required folder.
        /// It this is empty or consists only of the PATH_DELIMTER then this folder itself is returned.
        /// </param>
        /// <returns>null if the folder is not found</returns>
        public static InventoryFolderBase FindFolderByPath(
            IInventoryService inventoryService, UUID userId, string path)
        {
            InventoryFolderBase rootFolder = inventoryService.GetRootFolder(userId);

            if (null == rootFolder)
                return null;

            return FindFolderByPath(inventoryService, rootFolder, path);
        }
        
        /// <summary>
        /// Find a folder given a PATH_DELIMITER delimited path starting from this folder
        /// </summary>
        ///
        /// This method does not handle paths that contain multiple delimitors
        ///
        /// FIXME: We do not yet handle situations where folders have the same name.  We could handle this by some
        /// XPath like expression
        ///
        /// FIXME: Delimitors which occur in names themselves are not currently escapable.
        ///
        /// <param name="inventoryService">
        /// Inventory service to query
        /// </param>
        /// <param name="startFolder">
        /// The folder from which the path starts
        /// </param>
        /// <param name="path">
        /// The path to the required folder.
        /// It this is empty or consists only of the PATH_DELIMTER then this folder itself is returned.
        /// </param>
        /// <returns>null if the folder is not found</returns>
        public static InventoryFolderBase FindFolderByPath(
            IInventoryService inventoryService, InventoryFolderBase startFolder, string path)
        {
            if (path == string.Empty)
                return startFolder;

            path = path.Trim();

            if (path == PATH_DELIMITER.ToString())
                return startFolder;

            string[] components = SplitEscapedPath(path);
            components[0] = UnescapePath(components[0]);            

            //string[] components = path.Split(new string[] { PATH_DELIMITER.ToString() }, 2, StringSplitOptions.None);
            
            InventoryCollection contents = inventoryService.GetFolderContent(startFolder.Owner, startFolder.ID);

            foreach (InventoryFolderBase folder in contents.Folders)
            {
                if (folder.Name == components[0])
                {
                    if (components.Length > 1)
                        return FindFolderByPath(inventoryService, folder, components[1]);
                    else
                        return folder;
                }
            }

            // We didn't find a folder with the right name
            return null;
        }

        /// <summary>
        /// Find an item given a PATH_DELIMITOR delimited path starting from the user's root folder.
        ///
        /// This method does not handle paths that contain multiple delimitors
        ///
        /// FIXME: We do not yet handle situations where folders or items have the same name.  We could handle this by some
        /// XPath like expression
        ///
        /// FIXME: Delimitors which occur in names themselves are not currently escapable.
        /// </summary>
        /// 
        /// <param name="inventoryService">
        /// Inventory service to query
        /// </param>
        /// <param name="userId">
        /// The user to search
        /// </param>
        /// <param name="path">
        /// The path to the required item.
        /// </param>
        /// <returns>null if the item is not found</returns>
        public static InventoryItemBase FindItemByPath(
            IInventoryService inventoryService, UUID userId, string path)
        {
            InventoryFolderBase rootFolder = inventoryService.GetRootFolder(userId);

            if (null == rootFolder)
                return null;

            return FindItemByPath(inventoryService, rootFolder, path);
        }
        
        /// <summary>
        /// Find an item given a PATH_DELIMITOR delimited path starting from this folder.
        ///
        /// This method does not handle paths that contain multiple delimitors
        ///
        /// FIXME: We do not yet handle situations where folders or items have the same name.  We could handle this by some
        /// XPath like expression
        ///
        /// FIXME: Delimitors which occur in names themselves are not currently escapable.
        /// </summary>
        /// 
        /// <param name="inventoryService">
        /// Inventory service to query
        /// </param>
        /// <param name="startFolder">
        /// The folder from which the path starts
        /// </param>
        /// <param name="path">
        /// <param name="path">
        /// The path to the required item.
        /// </param>
        /// <returns>null if the item is not found</returns>
        public static InventoryItemBase FindItemByPath(
            IInventoryService inventoryService, InventoryFolderBase startFolder, string path)
        {
            string[] components = SplitEscapedPath(path);
            components[0] = UnescapePath(components[0]);
                            
            //string[] components = path.Split(new string[] { PATH_DELIMITER }, 2, StringSplitOptions.None);

            if (components.Length == 1)
            {
//                m_log.DebugFormat("FOUND SINGLE COMPONENT [{0}]", components[0]);
                
                List<InventoryItemBase> items = inventoryService.GetFolderItems(startFolder.Owner, startFolder.ID);
                foreach (InventoryItemBase item in items)
                {
                    if (item.Name == components[0])
                        return item;
                }
            }
            else
            {
//                m_log.DebugFormat("FOUND COMPONENTS [{0}] and [{1}]", components[0], components[1]);
                
                InventoryCollection contents = inventoryService.GetFolderContent(startFolder.Owner, startFolder.ID);
                
                foreach (InventoryFolderBase folder in contents.Folders)
                {
                    if (folder.Name == components[0])
                        return FindItemByPath(inventoryService, folder, components[1]);
                }
            }

            // We didn't find an item or intermediate folder with the given name
            return null;
        }

        /// <summary>
        /// Split a human escaped path into two components if it contains an unescaped path delimiter, or one component
        /// if no delimiter is present
        /// </summary>
        /// <param name="path"></param>
        /// <returns>
        /// The split path.  We leave the components in their originally unescaped state (though we remove the delimiter
        /// which originally split them if applicable).
        /// </returns>
        public static string[] SplitEscapedPath(string path)
        {
//            m_log.DebugFormat("SPLITTING PATH {0}", path);
            
            bool singleEscapeChar = false;
            
            for (int i = 0; i < path.Length; i++)
            {
                if (path[i] == ESCAPE_CHARACTER && !singleEscapeChar)
                {
                    singleEscapeChar = true;
                }
                else
                {
                    if (PATH_DELIMITER == path[i] && !singleEscapeChar)
                        return new string[2] { path.Remove(i), path.Substring(i + 1) };
                    else
                        singleEscapeChar = false;
                }
            }

            // We didn't find a delimiter
            return new string[1] { path };
        }

        /// <summary>
        /// Unescapes a human escaped path.  This means that "\\" goes to "\", and "\/" goes to "/"
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string UnescapePath(string path)
        {
//            m_log.DebugFormat("ESCAPING PATH {0}", path);
            
            StringBuilder sb = new StringBuilder();

            bool singleEscapeChar = false;
            for (int i = 0; i < path.Length; i++)
            {
                if (path[i] == ESCAPE_CHARACTER && !singleEscapeChar)
                    singleEscapeChar = true;
                else
                    singleEscapeChar = false;

                if (singleEscapeChar)
                {
                    if (PATH_DELIMITER == path[i])
                        sb.Append(PATH_DELIMITER);
                }
                else
                {
                    sb.Append(path[i]);
                }
            }

//            m_log.DebugFormat("ESCAPED PATH TO {0}", sb);
            
            return sb.ToString();
        }
    }
}