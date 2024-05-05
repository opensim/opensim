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
using System.Net;
using System.Reflection;
using System.Text;
using log4net;
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
    public class FetchLibDescHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly byte[] EmptyResponse = Util.UTF8NBGetbytes("<llsd><map><key>folders</key><array /></map></llsd>");
        private readonly ILibraryService m_LibraryService;
        private readonly UUID libOwner;
        private readonly IScene m_Scene;

        public FetchLibDescHandler(ILibraryService libService, IScene s)
        {
            m_LibraryService = libService;
            libOwner = m_LibraryService.LibraryRootFolder.Owner;
            m_Scene = s;
        }

        public void FetchRequest(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, ExpiringKey<UUID> BadRequests, UUID agentID)
        {
            //m_log.DebugFormat("[XXX]: FetchLibDescendentsRequest in {0}, {1}", (m_Scene == null) ? "none" : m_Scene.Name, request);
            if (m_LibraryService == null || m_LibraryService.LibraryRootFolder == null)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                return;
            }
            httpResponse.StatusCode = (int)HttpStatusCode.OK;

            List<LLSDFetchInventoryDescendents> folders;
            List<UUID> bad_folders = new List<UUID>();
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
                        LLSDFetchInventoryDescendents llsdRequest = new LLSDFetchInventoryDescendents();
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
                map.Clear();
                map = null;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[FETCH LIB DESC]: fail parsing request: {0}", e.Message);
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
                osu.AppendASCII("[FETCH LIB DESC HANDLER]: Unable to fetch folders:");
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
            if (invcollSet != null)
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
                    LLSDxmlEncode2.AddElem_agent_id(agentID, lastresponse);
                    LLSDxmlEncode2.AddElem_owner_id(thiscoll.OwnerID, lastresponse);
                    LLSDxmlEncode2.AddElem("descendents", thiscoll.Descendents, lastresponse);
                    LLSDxmlEncode2.AddElem_version(thiscoll.Version, lastresponse);

                    if (thiscoll.Folders == null || thiscoll.Folders.Count == 0)
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

                    if (thiscoll.Items == null || thiscoll.Items.Count == 0)
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

        private List<InventoryCollection> Fetch(List<LLSDFetchInventoryDescendents> fetchFolders, List<UUID> bad_folders)
        {
            //m_log.DebugFormat(
            //    "[FETCH LIB DESC HANDLER]: Fetching {0} folders", fetchFolders.Count);
            // FIXME MAYBE: We're not handling sortOrder!
            int cntr = fetchFolders.Count;
            List<InventoryCollection> result = new List<InventoryCollection>(cntr);
            List<LLSDFetchInventoryDescendents> libFolders = new List<LLSDFetchInventoryDescendents>(cntr);
            HashSet<UUID> libIDs = new HashSet<UUID>();
  
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
                    InventoryCollection Collection = new InventoryCollection()
                    {
                        OwnerID = f.owner_id,
                        Version = -1,
                        FolderID = f.folder_id,
                        Descendents = 0
                    };
                    result.Add(Collection);
                    continue;
                }
                if(f.owner_id.Equals(libOwner))
                {
                    if(libIDs.Contains(f.folder_id))
                        continue;
                    libIDs.Add(f.folder_id);
                    libFolders.Add(f);
                    continue;
                }
            }

            if (libFolders.Count > 0)
            {
                foreach (LLSDFetchInventoryDescendents f in libFolders)
                {
                    InventoryFolderImpl fold = m_LibraryService.LibraryRootFolder.FindFolder(f.folder_id);
                    if (fold != null)
                    {
                        InventoryCollection Collection = new InventoryCollection()
                        {
                            Folders = fold.RequestListOfFolders(),
                            Items = fold.RequestListOfItems(),
                            OwnerID = m_LibraryService.LibraryRootFolder.Owner,
                            FolderID = f.folder_id,
                            Version = fold.Version
                        };
                        Collection.Descendents = Collection.Items.Count + Collection.Folders.Count;
                        result.Add(Collection);
                        //m_log.DebugFormat("[XXX]: Added libfolder {0} ({1}) {2}", ret.Collection.FolderID, ret.Collection.OwnerID);
                    }
                    else
                        bad_folders.Add(f.folder_id);
                }
            }
            return result;
        }
    }
}
