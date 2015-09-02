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
using System.Collections.Specialized;
using System.Reflection;
using System.IO;
using System.Web;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;




namespace OpenSim.Capabilities.Handlers
{
    public class GetMeshHandler
    {
        private static readonly ILog m_log =
                   LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IAssetService m_assetService;

        public const string DefaultFormat = "vnd.ll.mesh";

        public GetMeshHandler(IAssetService assService)
        {
            m_assetService = assService;
        }
        public Hashtable Handle(Hashtable request)
        {
            Hashtable ret = new Hashtable();
            ret["int_response_code"] = (int)System.Net.HttpStatusCode.NotFound;
            ret["content_type"] = "text/plain";
            ret["keepalive"] = false;
            ret["reusecontext"] = false;
            ret["int_bytes"] = 0;
            ret["int_lod"] = 0;
            string MeshStr = (string)request["mesh_id"];


            //m_log.DebugFormat("[GETMESH]: called {0}", MeshStr);

            if (m_assetService == null)
            {
                m_log.Error("[GETMESH]: Cannot fetch mesh " + MeshStr + " without an asset service");
            }

            UUID meshID;
            if (!String.IsNullOrEmpty(MeshStr) && UUID.TryParse(MeshStr, out meshID))
            {
                //                m_log.DebugFormat("[GETMESH]: Received request for mesh id {0}", meshID);


                ret = ProcessGetMesh(request, UUID.Zero, null);


            }
            else
            {
                m_log.Warn("[GETMESH]: Failed to parse a mesh_id from GetMesh request: " + (string)request["uri"]);
            }


            return ret;
        }
        public Hashtable ProcessGetMesh(Hashtable request, UUID AgentId, Caps cap)
        {
            Hashtable responsedata = new Hashtable();
            responsedata["int_response_code"] = 400; //501; //410; //404;
            responsedata["content_type"] = "text/plain";
            responsedata["keepalive"] = false;
            responsedata["str_response_string"] = "Request wasn't what was expected";
            responsedata["reusecontext"] = false;
            responsedata["int_lod"] = 0;
            responsedata["int_bytes"] = 0;

            string meshStr = string.Empty;

            if (request.ContainsKey("mesh_id"))
                meshStr = request["mesh_id"].ToString();

            UUID meshID = UUID.Zero;
            if (!String.IsNullOrEmpty(meshStr) && UUID.TryParse(meshStr, out meshID))
            {
                if (m_assetService == null)
                {
                    responsedata["int_response_code"] = 404; //501; //410; //404;
                    responsedata["content_type"] = "text/plain";
                    responsedata["keepalive"] = false;
                    responsedata["str_response_string"] = "The asset service is unavailable.  So is your mesh.";
                    responsedata["reusecontext"] = false;
                    return responsedata;
                }

                AssetBase mesh = m_assetService.Get(meshID.ToString());

                if (mesh != null)
                {
                    if (mesh.Type == (SByte)AssetType.Mesh)
                    {

                        Hashtable headers = new Hashtable();
                        responsedata["headers"] = headers;

                        string range = String.Empty;

                        if (((Hashtable)request["headers"])["range"] != null)
                            range = (string)((Hashtable)request["headers"])["range"];

                        else if (((Hashtable)request["headers"])["Range"] != null)
                            range = (string)((Hashtable)request["headers"])["Range"];

                        if (!String.IsNullOrEmpty(range)) // Mesh Asset LOD // Physics
                        {
                            // Range request
                            int start, end;
                            if (TryParseRange(range, out start, out end))
                            {
                                // Before clamping start make sure we can satisfy it in order to avoid
                                // sending back the last byte instead of an error status
                                if (start >= mesh.Data.Length)
                                {
                                    responsedata["int_response_code"] = 404; //501; //410; //404;
                                    responsedata["content_type"] = "text/plain";
                                    responsedata["keepalive"] = false;
                                    responsedata["str_response_string"] = "This range doesnt exist.";
                                    responsedata["reusecontext"] = false;
                                    responsedata["int_lod"] = 3;
                                    return responsedata;
                                }
                                else
                                {
                                    end = Utils.Clamp(end, 0, mesh.Data.Length - 1);
                                    start = Utils.Clamp(start, 0, end);
                                    int len = end - start + 1;

                                    //m_log.Debug("Serving " + start + " to " + end + " of " + texture.Data.Length + " bytes for texture " + texture.ID);

                                    if (start > 20000)
                                    {
                                        responsedata["int_lod"] = 3;
                                    }
                                    else if (start < 4097)
                                    {
                                        responsedata["int_lod"] = 1;
                                    }
                                    else
                                    {
                                        responsedata["int_lod"] = 2;
                                    }


                                    if (start == 0 && len == mesh.Data.Length) // well redudante maybe
                                    {
                                        responsedata["int_response_code"] = (int)System.Net.HttpStatusCode.OK;
                                        responsedata["bin_response_data"] = mesh.Data;
                                        responsedata["int_bytes"] = mesh.Data.Length;
                                        responsedata["reusecontext"] = false;
                                        responsedata["int_lod"] = 3;

                                    }
                                    else
                                    {
                                        responsedata["int_response_code"] =
                                            (int)System.Net.HttpStatusCode.PartialContent;
                                        headers["Content-Range"] = String.Format("bytes {0}-{1}/{2}", start, end,
                                                                                 mesh.Data.Length);

                                        byte[] d = new byte[len];
                                        Array.Copy(mesh.Data, start, d, 0, len);
                                        responsedata["bin_response_data"] = d;
                                        responsedata["int_bytes"] = len;
                                        responsedata["reusecontext"] = false;
                                    }
                                }
                            }
                            else
                            {
                                m_log.Warn("[GETMESH]: Failed to parse a range from GetMesh request, sending full asset: " + (string)request["uri"]);
                                responsedata["str_response_string"] = Convert.ToBase64String(mesh.Data);
                                responsedata["content_type"] = "application/vnd.ll.mesh";
                                responsedata["int_response_code"] = 200;
                                responsedata["reusecontext"] = false;
                                responsedata["int_lod"] = 3;
                            }
                        }
                        else
                        {
                            responsedata["str_response_string"] = Convert.ToBase64String(mesh.Data);
                            responsedata["content_type"] = "application/vnd.ll.mesh";
                            responsedata["int_response_code"] = 200;
                            responsedata["reusecontext"] = false;
                            responsedata["int_lod"] = 3;
                        }
                    }
                    // Optionally add additional mesh types here
                    else
                    {
                        responsedata["int_response_code"] = 404; //501; //410; //404;
                        responsedata["content_type"] = "text/plain";
                        responsedata["keepalive"] = false;
                        responsedata["str_response_string"] = "Unfortunately, this asset isn't a mesh.";
                        responsedata["reusecontext"] = false;
                        responsedata["int_lod"] = 1;
                        return responsedata;
                    }
                }
                else
                {
                    responsedata["int_response_code"] = 404; //501; //410; //404;
                    responsedata["content_type"] = "text/plain";
                    responsedata["keepalive"] = false;
                    responsedata["str_response_string"] = "Your Mesh wasn't found.  Sorry!";
                    responsedata["reusecontext"] = false;
                    responsedata["int_lod"] = 0;
                    return responsedata;
                }
            }

            return responsedata;
        }
        private bool TryParseRange(string header, out int start, out int end)
        {
            if (header.StartsWith("bytes="))
            {
                string[] rangeValues = header.Substring(6).Split('-');
                if (rangeValues.Length == 2)
                {
                    if (Int32.TryParse(rangeValues[0], out start) && Int32.TryParse(rangeValues[1], out end))
                        return true;
                }
            }

            start = end = 0;
            return false;
        }
    }
}