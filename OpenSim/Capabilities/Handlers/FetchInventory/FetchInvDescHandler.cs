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
using System.Linq;
using System.Reflection;
using System.Text;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;
using OSDArray = OpenMetaverse.StructuredData.OSDArray;

namespace OpenSim.Capabilities.Handlers
{
    public class FetchInvDescHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly byte[] EmptyResponse = Util.UTF8NBGetbytes("<llsd><map><key>folders</key><array /></map></llsd>");
        private readonly IInventoryService m_InventoryService;
        private readonly ILibraryService m_LibraryService;
        private readonly UUID libOwner;
        private readonly IScene m_Scene;

        public FetchInvDescHandler(IInventoryService invService, ILibraryService libService, IScene s)
        {
            m_InventoryService = invService;
            if(libService != null && libService.LibraryRootFolder != null)
            {
                m_LibraryService = libService;
                libOwner = libService.LibraryRootFolder.Owner;
            }
            m_Scene = s;
        }

        public void FetchInventoryDescendentsRequest(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, ExpiringKey<UUID> BadRequests)
        {
            //m_log.DebugFormat("[XXX]: FetchInventoryDescendentsRequest in {0}, {1}", (m_Scene == null) ? "none" : m_Scene.Name, request);

            List<LLSDFetchInventoryDescendents> folders;
            List<UUID> bad_folders = new();

            try
            {
                OSDArray foldersrequested = null;
                OSD tmp = OSDParser.DeserializeLLSDXml(httpRequest.InputStream);
                httpRequest.InputStream.Dispose();

                OSDMap map = (OSDMap)tmp;
                if(map.TryGetValue("folders", out tmp) && tmp is OSDArray frtmp)
                    foldersrequested = frtmp;

                if (foldersrequested is null || foldersrequested.Count == 0)
                {
                    httpResponse.RawBuffer = EmptyResponse;
                    return;
                }

                folders = new List<LLSDFetchInventoryDescendents>(foldersrequested.Count);
                for (int i = 0; i < foldersrequested.Count; i++)
                {
                    OSDMap mfolder = foldersrequested[i] as OSDMap;
                    UUID id = mfolder["folder_id"].AsUUID();
                    if(BadRequests.ContainsKey(id))
                    {
                        bad_folders.Add(id);
                    }
                    else
                    {
                        LLSDFetchInventoryDescendents llsdRequest = new();
                        try
                        {
                            llsdRequest.folder_id = id;
                            llsdRequest.owner_id = mfolder["owner_id"].AsUUID();
                            llsdRequest.sort_order = mfolder["sort_order"].AsInteger();
                            llsdRequest.fetch_folders = mfolder["fetch_folders"].AsBoolean();
                            llsdRequest.fetch_items = mfolder["fetch_items"].AsBoolean();
                        }
                        catch (Exception e)
                        {
                            m_log.Debug("[WEB FETCH INV DESC HANDLER]: caught exception doing OSD deserialize" + e.Message);
                            continue;
                        }
                        folders.Add(llsdRequest);
                    }
                }
                foldersrequested = null;
                map = null;
            }
            catch (Exception e)
            {
                m_log.Error("[FETCH INV DESC]: fail parsing request: " + e.Message);
                httpResponse.RawBuffer = EmptyResponse;
                return;
            }

            if (folders is null || folders.Count == 0)
            {
                if(bad_folders.Count == 0)
                {
                    httpResponse.RawBuffer = EmptyResponse;
                    return;
                }

                osUTF8 osu = OSUTF8Cached.Acquire();
                osu.AppendASCII("[WEB FETCH INV DESC HANDLER]: Unable to fetch folders owned by Unknown user:");
                int limit = 5;
                int count = 0;
                foreach (UUID bad in bad_folders)
                {
                    if (BadRequests.ContainsKey(bad))
                        continue;
                    osu.Append((byte)' ');
                    osu.AppendASCII(bad.ToString());
                    ++count;
                    if (--limit < 0)
                        break;
                }

                if(count > 0)
                {
                    if (limit < 0)
                        osu.AppendASCII(" ...");
                    m_log.Warn(osu.ToString());
                }

                osu.Clear();

                osu.AppendASCII("<llsd><map><key>folders</key><array /></map><map><key>bad_folders</key><array>");
                foreach (UUID bad in bad_folders)
                {
                    osu.AppendASCII("<map><key>folder_id</key><uuid>");
                    osu.AppendASCII(bad.ToString());
                    osu.AppendASCII("</uuid><key>error</key><string>Unknown</string></map>");
                }
                osu.AppendASCII("</array></map></llsd>");
                httpResponse.RawBuffer = OSUTF8Cached.GetArrayAndRelease(osu);
                return;
            }

            UUID requester = folders[0].owner_id;

            List<InventoryCollection> invcollSet = Fetch(folders, bad_folders);
            //m_log.DebugFormat("[XXX]: Got {0} folders from a request of {1}", invcollSet.Count, folders.Count);

            int invcollSetCount = 0;
            if (invcollSet is not null)
                invcollSetCount = invcollSet.Count;

            osUTF8 lastresponse = LLSDxmlEncode2.Start();

            if (invcollSetCount > 0)
            {
                lastresponse.AppendASCII("<map><key>folders</key><array>");
                int i = 0;
                InventoryCollection thiscoll;
                for (i = 0; i < invcollSetCount; i++)
                {
                    thiscoll = invcollSet[i];
                    invcollSet[i] = null;

                    LLSDxmlEncode2.AddMap(lastresponse);
                    LLSDxmlEncode2.AddElem_folder_id(thiscoll.FolderID, lastresponse);
                    LLSDxmlEncode2.AddElem_agent_id(thiscoll.OwnerID, lastresponse);
                    LLSDxmlEncode2.AddElem_owner_id(thiscoll.OwnerID, lastresponse);
                    LLSDxmlEncode2.AddElem("descendents", thiscoll.Descendents, lastresponse);
                    LLSDxmlEncode2.AddElem_version(thiscoll.Version, lastresponse);

                    if (thiscoll.Folders is null || thiscoll.Folders.Count == 0)
                        LLSDxmlEncode2.AddEmptyArray("categories", lastresponse);
                    else
                    {
                        LLSDxmlEncode2.AddArray("categories", lastresponse);
                        foreach (InventoryFolderBase invFolder in thiscoll.Folders)
                        {
                            LLSDxmlEncode2.AddMap(lastresponse);

                            LLSDxmlEncode2.AddElem_category_id(invFolder.ID, lastresponse);
                            LLSDxmlEncode2.AddElem_parent_id(invFolder.ParentID, lastresponse);
                            LLSDxmlEncode2.AddElem_name(invFolder.Name, lastresponse);
                            LLSDxmlEncode2.AddElem("type_default", invFolder.Type, lastresponse);
                            LLSDxmlEncode2.AddElem_version( invFolder.Version, lastresponse);

                            LLSDxmlEncode2.AddEndMap(lastresponse);
                        }
                        LLSDxmlEncode2.AddEndArray(lastresponse);
                    }

                    if (thiscoll.Items is null || thiscoll.Items.Count == 0)
                        LLSDxmlEncode2.AddEmptyArray("items", lastresponse);
                    else
                    {
                        LLSDxmlEncode2.AddArray("items", lastresponse);
                        foreach (InventoryItemBase invItem in thiscoll.Items)
                        {
                            invItem.ToLLSDxml(lastresponse);
                        }

                        LLSDxmlEncode2.AddEndArray(lastresponse);
                    }


                    LLSDxmlEncode2.AddEndMap(lastresponse);
                    invcollSet[i] = null;
                }
                LLSDxmlEncode2.AddEndArrayAndMap(lastresponse);
            }
            else
            {
                lastresponse.AppendASCII("<map><key>folders</key><array /></map>");
            }

            if (bad_folders.Count > 0)
            {
                lastresponse.AppendASCII("<map><key>bad_folders</key><array>");
                foreach (UUID bad in bad_folders)
                {
                    BadRequests.Add(bad);
                    lastresponse.AppendASCII("<map><key>folder_id</key><uuid>");
                    lastresponse.AppendASCII(bad.ToString());
                    lastresponse.AppendASCII("</uuid><key>error</key><string>Unknown</string></map>");
                }
                lastresponse.AppendASCII("</array></map>");

                StringBuilder sb = osStringBuilderCache.Acquire();
                sb.Append("[WEB FETCH INV DESC HANDLER]: Unable to fetch folders owned by ");
                sb.Append(requester.ToString());
                sb.Append(" :");
                int limit = 9;
                foreach (UUID bad in bad_folders)
                {
                    sb.Append(' ');
                    sb.Append(bad.ToString());
                    if(--limit < 0)
                        break;
                }
                if(limit < 0)
                    sb.Append(" ...");
                m_log.Warn(osStringBuilderCache.GetStringAndRelease(sb));
            }

            httpResponse.RawBuffer = LLSDxmlEncode2.EndToBytes(lastresponse);
        }

        private void AddLibraryFolders(List<LLSDFetchInventoryDescendents> libFolders, List<InventoryCollection> result)
        {
            InventoryFolderImpl fold;
            if (m_LibraryService is null || m_LibraryService.LibraryRootFolder is null)
                return;
            
            foreach (LLSDFetchInventoryDescendents f in libFolders)
            {
                if ((fold = m_LibraryService.LibraryRootFolder.FindFolder(f.folder_id)) is not null)
                {
                    InventoryCollection Collection = new();
                    //ret.Collection.Folders = new List<InventoryFolderBase>();
                    Collection.Folders = fold.RequestListOfFolders();
                    Collection.Items = fold.RequestListOfItems();
                    Collection.OwnerID = m_LibraryService.LibraryRootFolder.Owner;
                    Collection.FolderID = f.folder_id;
                    Collection.Version = fold.Version;

                    Collection.Descendents = Collection.Items.Count + Collection.Folders.Count;
                    result.Add(Collection);

                    //m_log.DebugFormat("[XXX]: Added libfolder {0} ({1}) {2}", ret.Collection.FolderID, ret.Collection.OwnerID);
                }
            }
        }

        private List<InventoryCollection> Fetch(List<LLSDFetchInventoryDescendents> fetchFolders, List<UUID> bad_folders)
        {
            //m_log.DebugFormat(
            //    "[WEB FETCH INV DESC HANDLER]: Fetching {0} folders for owner {1}", fetchFolders.Count, fetchFolders[0].owner_id);

            // FIXME MAYBE: We're not handling sortOrder!

            List<InventoryCollection> result = new(32);
            List<LLSDFetchInventoryDescendents> libFolders = new(32);
            List<LLSDFetchInventoryDescendents> otherFolders = new(32);
            HashSet<UUID> libIDs = new();
            HashSet<UUID> otherIDs = new();

            bool dolib = m_LibraryService != null;

            // Filter folder Zero right here. Some viewers (Firestorm) send request for folder Zero, which doesn't make sense
            // and can kill the sim (all root folders have parent_id Zero)
            // send something.
            bool doneZeroID = false;
            foreach(LLSDFetchInventoryDescendents f in fetchFolders)
            {
                if (f.folder_id.IsZero())
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
                if(dolib && f.owner_id.Equals(libOwner))
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
                //m_log.DebugFormat("[XXX]: {0}", string.Join(",", fids));

                InventoryCollection[] fetchedContents = m_InventoryService.GetMultipleFoldersContent(otherFolders[0].owner_id, otherIDs.ToArray());

                if (fetchedContents is null)
                     return null;
 
                if (fetchedContents.Length == 0)
                {
                    foreach (LLSDFetchInventoryDescendents freq in otherFolders)
                        BadFolder(freq, null, bad_folders);
                }
                else
                {
                    int i = 0;
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

                        result.Add(contents);
                    }
                }
            }

            if(libFolders.Count > 0)
                AddLibraryFolders(libFolders, result);

            return result;
        }

        private bool BadFolder(LLSDFetchInventoryDescendents freq, InventoryCollection contents, List<UUID> bad_folders)
        {
            if (contents is null)
            {
                bad_folders.Add(freq.folder_id);
                return true;
            }

            // The inventory server isn't sending FolderID in the collection...
            // Must fetch it individually
            if (contents.FolderID.IsZero())
            {
                InventoryFolderBase containingFolder = m_InventoryService.GetFolder(freq.owner_id, freq.folder_id);
                if (containingFolder is null)
                {
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
            if (contents.Items is null || contents.Items.Count == 0)
                return;

            // viewers are lasy and want a copy of the linked item sent before the link to it

            // look for item links
            List<UUID> itemIDs = new();
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
                    
                if (linked is not null)
                {
                    List<InventoryItemBase> linkedItems = new List<InventoryItemBase>(linked.Length);
                    // check for broken
                    foreach (InventoryItemBase linkedItem in linked)
                    {
                        // Take care of genuinely broken links where the target doesn't exist
                        // HACK: Also, don't follow up links that just point to other links.  In theory this is legitimate,
                        // but no viewer has been observed to set these up and this is the lazy way of avoiding cycles
                        // rather than having to keep track of every folder requested in the recursion.
                        if (linkedItem is not null && linkedItem.AssetType != (int)AssetType.Link)
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
