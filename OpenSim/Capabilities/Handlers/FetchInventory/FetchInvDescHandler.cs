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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Capabilities.Handlers
{
    public class FetchInvDescHandler
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IInventoryService m_InventoryService;
        private ILibraryService m_LibraryService;
        private IScene m_Scene;
//        private object m_fetchLock = new Object();

        public FetchInvDescHandler(IInventoryService invService, ILibraryService libService, IScene s)
        {
            m_InventoryService = invService;
            m_LibraryService = libService;
            m_Scene = s;
        }

        public string FetchInventoryDescendentsRequest(string request, string path, string param, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            //m_log.DebugFormat("[XXX]: FetchInventoryDescendentsRequest in {0}, {1}", (m_Scene == null) ? "none" : m_Scene.Name, request);

            ArrayList foldersrequested = null;
            try
            {
                Hashtable hash = (Hashtable)LLSD.LLSDDeserialize(Utils.StringToBytes(request));
                foldersrequested = (ArrayList)hash["folders"];
                hash = null;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[FETCH INV DESC]: fail parsing request: '{0}'; path: '{1}'; exception: '{2}'", request, path, e.Message);
                foldersrequested = null;
            }

            if(foldersrequested == null || foldersrequested.Count == 0)
                return "<llsd><map><key>folders</key><array /></map></llsd>";
 
            List<LLSDFetchInventoryDescendents> folders = new List<LLSDFetchInventoryDescendents>();
            for (int i = 0; i < foldersrequested.Count; i++)
            {
                Hashtable inventoryhash = (Hashtable)foldersrequested[i];
                LLSDFetchInventoryDescendents llsdRequest = new LLSDFetchInventoryDescendents();

                try
                {
                    LLSDHelpers.DeserialiseOSDMap(inventoryhash, llsdRequest);
                }
                catch (Exception e)
                {
                    m_log.Debug("[WEB FETCH INV DESC HANDLER]: caught exception doing OSD deserialize" + e.Message);
                    continue;
                }

                folders.Add(llsdRequest);
            }

            foldersrequested.Clear();

            if(folders.Count == 0)
                return "<llsd><map><key>folders</key><array /></map></llsd>";

            List<UUID> bad_folders = new List<UUID>();

            int total_folders = 0;
            int total_items = 0;
            List<InventoryCollection> invcollSet = Fetch(folders, bad_folders, ref total_folders, ref total_items);
            //m_log.DebugFormat("[XXX]: Got {0} folders from a request of {1}", invcollSet.Count, folders.Count);

            int invcollSetCount = 0;
            if (invcollSet != null)
                invcollSetCount = invcollSet.Count;

            int mem = 8192 + ((256 * invcollSetCount +
                                384 * total_folders +
                                1024 * total_items +
                                128 * bad_folders.Count) & 0x7ffff000);

            StringBuilder lastresponse = new StringBuilder(mem);
            lastresponse.Append("<llsd>");

            if(invcollSetCount > 0)
            {
                lastresponse.Append("<map><key>folders</key><array>");
                int i = 0;
                InventoryCollection thiscoll;
                for(i = 0; i < invcollSetCount; i++)
                {
                    thiscoll = invcollSet[i];
                    invcollSet[i] = null;

                    LLSDxmlEncode.AddMap(lastresponse);
                        LLSDxmlEncode.AddElem("agent_id", thiscoll.OwnerID, lastresponse);
                        LLSDxmlEncode.AddElem("descendents", thiscoll.Descendents, lastresponse);
                        LLSDxmlEncode.AddElem("folder_id", thiscoll.FolderID, lastresponse);

                        if(thiscoll.Folders == null || thiscoll.Folders.Count == 0)
                            LLSDxmlEncode.AddEmptyArray("categories", lastresponse);
                        else
                        {
                            LLSDxmlEncode.AddArray("categories", lastresponse);
                            foreach (InventoryFolderBase invFolder in thiscoll.Folders)
                            {
                                LLSDxmlEncode.AddMap(lastresponse);

                                LLSDxmlEncode.AddElem("folder_id", invFolder.ID, lastresponse);
                                LLSDxmlEncode.AddElem("parent_id", invFolder.ParentID, lastresponse);
                                LLSDxmlEncode.AddElem("name", invFolder.Name, lastresponse);
                                LLSDxmlEncode.AddElem("type", invFolder.Type, lastresponse);
                                LLSDxmlEncode.AddElem("preferred_type", (int)-1, lastresponse);
                                LLSDxmlEncode.AddElem("version", invFolder.Version, lastresponse);

                                LLSDxmlEncode.AddEndMap(lastresponse);
                            }  
                            LLSDxmlEncode.AddEndArray(lastresponse);
                        }

                        if(thiscoll.Items == null || thiscoll.Items.Count == 0)
                            LLSDxmlEncode.AddEmptyArray("items", lastresponse);
                        else
                        {
                            LLSDxmlEncode.AddArray("items", lastresponse);
                            foreach (InventoryItemBase invItem in thiscoll.Items)
                            {
                                invItem.ToLLSDxml(lastresponse);
                            }

                            LLSDxmlEncode.AddEndArray(lastresponse);
                        }

                        LLSDxmlEncode.AddElem("owner_id", thiscoll.OwnerID, lastresponse);
                        LLSDxmlEncode.AddElem("version", thiscoll.Version, lastresponse);

                    LLSDxmlEncode.AddEndMap(lastresponse);
                    invcollSet[i] = null;
                }
                lastresponse.Append("</array></map>");
                thiscoll = null;
            }
            else
            {
                lastresponse.Append("<map><key>folders</key><array /></map>");
            }

            //m_log.DebugFormat("[WEB FETCH INV DESC HANDLER]: Bad folders {0}", string.Join(", ", bad_folders));
            if(bad_folders.Count > 0)
            {
                lastresponse.Append("<map><key>bad_folders</key><array>");
                foreach (UUID bad in bad_folders)
                {
                    lastresponse.Append("<map><key>folder_id</key><uuid>");
                    lastresponse.Append(bad.ToString());
                    lastresponse.Append("</uuid><key>error</key><string>Unknown</string></map>");
                }
                lastresponse.Append("</array></map>");
            }
            lastresponse.Append("</llsd>");

            return lastresponse.ToString();
        }

        private void AddLibraryFolders(List<LLSDFetchInventoryDescendents> libFolders, List<InventoryCollection> result, ref int total_folders, ref int total_items)
        {
            InventoryFolderImpl fold;
            if (m_LibraryService == null || m_LibraryService.LibraryRootFolder == null)
                return;
            
            foreach (LLSDFetchInventoryDescendents f in libFolders)
            {
                if ((fold = m_LibraryService.LibraryRootFolder.FindFolder(f.folder_id)) != null)
                {
                    InventoryCollection Collection = new InventoryCollection();
//                        ret.Collection.Folders = new List<InventoryFolderBase>();
                    Collection.Folders = fold.RequestListOfFolders();
                    Collection.Items = fold.RequestListOfItems();
                    Collection.OwnerID = m_LibraryService.LibraryRootFolder.Owner;
                    Collection.FolderID = f.folder_id;
                    Collection.Version = fold.Version;

                    Collection.Descendents = Collection.Items.Count + Collection.Folders.Count;
                    total_folders += Collection.Folders.Count;
                    total_items += Collection.Items.Count;
                    result.Add(Collection);

                    //m_log.DebugFormat("[XXX]: Added libfolder {0} ({1}) {2}", ret.Collection.FolderID, ret.Collection.OwnerID);
                }
            }
        }

        private List<InventoryCollection> Fetch(List<LLSDFetchInventoryDescendents> fetchFolders, List<UUID> bad_folders, ref int total_folders, ref int total_items)
        {
            //m_log.DebugFormat(
            //    "[WEB FETCH INV DESC HANDLER]: Fetching {0} folders for owner {1}", fetchFolders.Count, fetchFolders[0].owner_id);

            // FIXME MAYBE: We're not handling sortOrder!

            List<InventoryCollection> result = new List<InventoryCollection>(32);
            List<LLSDFetchInventoryDescendents> libFolders = new List<LLSDFetchInventoryDescendents>(32);
            List<LLSDFetchInventoryDescendents> otherFolders = new List<LLSDFetchInventoryDescendents>(32);
            HashSet<UUID> libIDs = new HashSet<UUID>();
            HashSet<UUID> otherIDs = new HashSet<UUID>();

            bool dolib = (m_LibraryService != null && m_LibraryService.LibraryRootFolder != null);
            UUID libOwner = UUID.Zero;
            if(dolib)
                libOwner = m_LibraryService.LibraryRootFolder.Owner;

            // Filter folder Zero right here. Some viewers (Firestorm) send request for folder Zero, which doesn't make sense
            // and can kill the sim (all root folders have parent_id Zero)
            // send something.
            bool doneZeroID = false;
            foreach(LLSDFetchInventoryDescendents f in fetchFolders)
            {
                if (f.folder_id == UUID.Zero)
                {
                    if(doneZeroID)
                        continue;
                    doneZeroID = true;
                    InventoryCollection Collection = new InventoryCollection();
                    Collection.OwnerID = f.owner_id;
                    Collection.Version = 0;
                    Collection.FolderID = f.folder_id;
                    Collection.Descendents = 0;
                    result.Add(Collection);
                    continue;
                }
                if(dolib && f.owner_id == libOwner)
                {
                    if(libIDs.Contains(f.folder_id))
                        continue;
                    libIDs.Add(f.folder_id);
                    libFolders.Add(f);
                    continue;
                }
                if(otherIDs.Contains(f.folder_id))
                    continue;

                otherIDs.Add(f.folder_id);
                otherFolders.Add(f);
            }

            fetchFolders.Clear();

            if(otherFolders.Count > 0)
            { 
                int i = 0;

                //m_log.DebugFormat("[XXX]: {0}", string.Join(",", fids));

                InventoryCollection[] fetchedContents = m_InventoryService.GetMultipleFoldersContent(otherFolders[0].owner_id, otherIDs.ToArray());

                if (fetchedContents == null)
                     return null;
 
                if (fetchedContents.Length == 0)
                {
                    foreach (LLSDFetchInventoryDescendents freq in otherFolders)
                        BadFolder(freq, null, bad_folders);
                }
                else
                {
                    i = 0;
                    // Do some post-processing. May need to fetch more from inv server for links
                    foreach (InventoryCollection contents in fetchedContents)
                    {
                        // Find the original request
                        LLSDFetchInventoryDescendents freq = otherFolders[i];
                        otherFolders[i]=null;
                        i++;

                        if (BadFolder(freq, contents, bad_folders))
                            continue;

                        if(!freq.fetch_folders)
                            contents.Folders.Clear();
                        if(!freq.fetch_items)
                            contents.Items.Clear();

                        contents.Descendents = contents.Items.Count + contents.Folders.Count;
 
                        // Next: link management
                        ProcessLinks(freq, contents);

                        total_folders += contents.Folders.Count;
                        total_items += contents.Items.Count;
                        result.Add(contents);
                    }
                }
            }

            if(dolib && libFolders.Count > 0)
            {
                AddLibraryFolders(libFolders, result, ref total_folders, ref total_items);           
            }

            return result;
        }

        private bool BadFolder(LLSDFetchInventoryDescendents freq, InventoryCollection contents, List<UUID> bad_folders)
        {
            if (contents == null)
            {
                bad_folders.Add(freq.folder_id);
                return true;
            }

            // The inventory server isn't sending FolderID in the collection...
            // Must fetch it individually
            if (contents.FolderID == UUID.Zero)
            {
                InventoryFolderBase containingFolder = m_InventoryService.GetFolder(freq.owner_id, freq.folder_id);
                if (containingFolder == null)
                {
                    m_log.WarnFormat("[WEB FETCH INV DESC HANDLER]: Unable to fetch folder {0}", freq.folder_id);
                    bad_folders.Add(freq.folder_id);
                    return true;
                }
                contents.FolderID = containingFolder.ID;
                contents.OwnerID = containingFolder.Owner;
                contents.Version = containingFolder.Version;
            }

            return false;
        }

        private void ProcessLinks(LLSDFetchInventoryDescendents freq, InventoryCollection contents)
        {
            if (contents.Items == null || contents.Items.Count == 0)
                return;

            // viewers are lasy and want a copy of the linked item sent before the link to it

            // look for item links
            List<UUID> itemIDs = new List<UUID>();
            foreach (InventoryItemBase item in contents.Items)
            {
                //m_log.DebugFormat("[XXX]:   {0} {1}", item.Name, item.AssetType);
                if (item.AssetType == (int)AssetType.Link)
                    itemIDs.Add(item.AssetID);
            }

            // get the linked if any
            if (itemIDs.Count > 0)
            {
                InventoryItemBase[] linked = m_InventoryService.GetMultipleItems(freq.owner_id, itemIDs.ToArray());
                    
                if (linked != null)
                {
                    List<InventoryItemBase> linkedItems = new List<InventoryItemBase>();
                    // check for broken
                    foreach (InventoryItemBase linkedItem in linked)
                    {
                        // Take care of genuinely broken links where the target doesn't exist
                        // HACK: Also, don't follow up links that just point to other links.  In theory this is legitimate,
                        // but no viewer has been observed to set these up and this is the lazy way of avoiding cycles
                        // rather than having to keep track of every folder requested in the recursion.
                        if (linkedItem != null && linkedItem.AssetType != (int)AssetType.Link)
                        {
                            linkedItems.Add(linkedItem);
                            //m_log.DebugFormat("[WEB FETCH INV DESC HANDLER]: Added {0} {1} {2}", linkedItem.Name, linkedItem.AssetType, linkedItem.Folder);
                        }
                    }
                    // insert them
                    if(linkedItems.Count > 0)
                        contents.Items.InsertRange(0, linkedItems);
                }
            }
        }
    }
}
