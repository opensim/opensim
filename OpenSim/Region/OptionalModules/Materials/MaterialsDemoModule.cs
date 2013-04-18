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
using System.IO;
using System.Reflection;
using System.Security.Cryptography; // for computing md5 hash
using log4net;
using Mono.Addins;
using Nini.Config;

using OpenMetaverse;
using OpenMetaverse.StructuredData;

using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

using Ionic.Zlib;

// You will need to uncomment these lines if you are adding a region module to some other assembly which does not already
// specify its assembly.  Otherwise, the region modules in the assembly will not be picked up when OpenSimulator scans
// the available DLLs
//[assembly: Addin("MyModule", "1.0")]
//[assembly: AddinDependency("OpenSim", "0.5")]

namespace OpenSim.Region.OptionalModules.MaterialsDemoModule
{
    /// <summary>
    ///
    //   #    #    ##    #####   #    #     #    #    #   ####
    //   #    #   #  #   #    #  ##   #     #    ##   #  #    #
    //   #    #  #    #  #    #  # #  #     #    # #  #  #
    //   # ## #  ######  #####   #  # #     #    #  # #  #  ###
    //   ##  ##  #    #  #   #   #   ##     #    #   ##  #    #
    //   #    #  #    #  #    #  #    #     #    #    #   ####
    //
    //
    //
    //////////////  WARNING  //////////////////////////////////////////////////////////////////
    /// This is an *Experimental* module for developing support for materials-capable viewers
    /// This module should NOT be used in a production environment! It may cause data corruption and
    /// viewer crashes. It should be only used to evaluate implementations of materials.
    /// 
    /// CURRENTLY NO MATERIALS ARE PERSISTED ACROSS SIMULATOR RESTARTS OR ARE STORED IN ANY INVENTORY OR ASSETS
    /// This may change in future implementations.
    /// 
    /// To enable this module, add this string at the bottom of OpenSim.ini:
    /// [MaterialsDemoModule]
    /// 
    /// </summary>
    /// 

    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "MaterialsDemoModule")]
    public class MaterialsDemoModule : INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        public string Name { get { return "MaterialsDemoModule"; } }        
        
        public Type ReplaceableInterface { get { return null; } }

        private Scene m_scene = null;
        private bool m_enabled = false;
        
        public void Initialise(IConfigSource source)
        {
            m_enabled = (source.Configs["MaterialsDemoModule"] != null);
            if (!m_enabled)
                return;

            m_log.DebugFormat("[MaterialsDemoModule]: INITIALIZED MODULE");

        }
        
        public void Close()
        {
            if (!m_enabled)
                return;

            m_log.DebugFormat("[MaterialsDemoModule]: CLOSED MODULE");
        }
        
        public void AddRegion(Scene scene)
        {
            if (!m_enabled)
                return;

            m_log.DebugFormat("[MaterialsDemoModule]: REGION {0} ADDED", scene.RegionInfo.RegionName);
            m_scene = scene;
            m_scene.EventManager.OnRegisterCaps += new EventManager.RegisterCapsEvent(OnRegisterCaps);
        }

        void OnRegisterCaps(OpenMetaverse.UUID agentID, OpenSim.Framework.Capabilities.Caps caps)
        {
            string capsBase = "/CAPS/" + caps.CapsObjectPath;

            IRequestHandler renderMaterialsPostHandler = new RestStreamHandler("POST", capsBase + "/", RenderMaterialsPostCap);
            caps.RegisterHandler("RenderMaterials", renderMaterialsPostHandler);

            // OpenSimulator CAPs infrastructure seems to be somewhat hostile towards any CAP that requires both GET
            // and POST handlers, (at least at the time this was originally written), so we first set up a POST
            // handler normally and then add a GET handler via MainServer

            IRequestHandler renderMaterialsGetHandler = new RestStreamHandler("GET", capsBase + "/", RenderMaterialsGetCap);
            MainServer.Instance.AddStreamHandler(renderMaterialsGetHandler); 
        }
        
        public void RemoveRegion(Scene scene)
        {
            if (!m_enabled)
                return;

            m_log.DebugFormat("[MaterialsDemoModule]: REGION {0} REMOVED", scene.RegionInfo.RegionName);
        }        
        
        public void RegionLoaded(Scene scene)
        {
            if (!m_enabled)
                return;
            m_log.DebugFormat("[MaterialsDemoModule]: REGION {0} LOADED", scene.RegionInfo.RegionName);
        }

        public string RenderMaterialsPostCap(string request, string path,
                string param, IOSHttpRequest httpRequest,
                IOSHttpResponse httpResponse)
        {
            m_log.Debug("[MaterialsDemoModule]: POST cap handler");

            OSDMap req = (OSDMap)OSDParser.DeserializeLLSDXml(request);
            OSDMap resp = new OSDMap();

            OSDMap materialsFromViewer = null;

            if (req.ContainsKey("Zipped"))
            {
                OSD osd = null;

                byte[] inBytes = req["Zipped"].AsBinary();

                try 
                {
                    osd = ZDecompressBytesToOsd(inBytes);

                    if (osd != null && osd is OSDMap)
                    {
                        materialsFromViewer = osd as OSDMap;

                        if (materialsFromViewer.ContainsKey("FullMaterialsPerFace"))
                        {
                            OSD matsOsd = materialsFromViewer["FullMaterialsPerFace"];
                            if (matsOsd is OSDArray)
                            {
                                OSDArray matsArr = matsOsd as OSDArray;

                                try
                                {
                                    foreach (OSDMap matsMap in matsArr)
                                    {
                                        m_log.Debug("[MaterialsDemoModule]: processing matsMap: " + OSDParser.SerializeJsonString(matsMap));

                                        uint matLocalID = 0;
                                        try { matLocalID = matsMap["ID"].AsUInteger(); }
                                        catch (Exception e) { m_log.Warn("[MaterialsDemoModule]: cannot decode \"ID\" from matsMap: " + e.Message); }
                                        m_log.Debug("[MaterialsDemoModule]: matLocalId: " + matLocalID.ToString());


                                        OSDMap mat = null;
                                        try { mat = matsMap["Material"] as OSDMap; }
                                        catch (Exception e) { m_log.Warn("[MaterialsDemoModule]: cannot decode \"Material\" from matsMap: " + e.Message); }
                                        m_log.Debug("[MaterialsDemoModule]: mat: " + OSDParser.SerializeJsonString(mat));
                                        
                                        UUID id = HashOsd(mat);
                                        m_knownMaterials[id] = mat;
                                        

                                        var sop = m_scene.GetSceneObjectPart(matLocalID);
                                        if (sop == null)
                                            m_log.Debug("[MaterialsDemoModule]: null SOP for localId: " + matLocalID.ToString());
                                        else
                                        {
                                            var te = sop.Shape.Textures;

                                            if (te == null)
                                            {
                                                m_log.Debug("[MaterialsDemoModule]: null TextureEntry for localId: " + matLocalID.ToString());
                                            }
                                            else
                                            {
                                                int face = -1;

                                                if (matsMap.ContainsKey("Face"))
                                                {
                                                    face = matsMap["Face"].AsInteger();
                                                    if (te.FaceTextures == null) // && face == 0)
                                                    {
                                                        if (te.DefaultTexture == null)
                                                            m_log.Debug("[MaterialsDemoModule]: te.DefaultTexture is null");
                                                        else
                                                        {
                                                            if (te.DefaultTexture.MaterialID == null)
                                                                m_log.Debug("[MaterialsDemoModule]: te.DefaultTexture.MaterialID is null");
                                                            else
                                                            {
                                                                te.DefaultTexture.MaterialID = id;
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (te.FaceTextures.Length >= face - 1)
                                                        {
                                                            if (te.FaceTextures[face] == null)
                                                                te.DefaultTexture.MaterialID = id;
                                                            else
                                                                te.FaceTextures[face].MaterialID = id;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    if (te.DefaultTexture != null)
                                                        te.DefaultTexture.MaterialID = id;
                                                }

                                                m_log.Debug("[MaterialsDemoModule]: setting material ID for face " + face.ToString() + " to " + id.ToString());

                                                sop.UpdateTextureEntry(te);
                                            }
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    m_log.Warn("[MaterialsDemoModule]: exception processing received material: " + e.Message);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.Warn("[MaterialsDemoModule]: exception decoding zipped CAP payload: " + e.Message);
                    //return "";
                }
                m_log.Debug("[MaterialsDemoModule]: knownMaterials.Count: " + m_knownMaterials.Count.ToString());
            }


            string response = OSDParser.SerializeLLSDXmlString(resp);

            m_log.Debug("[MaterialsDemoModule]: cap request: " + request);
            m_log.Debug("[MaterialsDemoModule]: cap response: " + response);
            return response;
        }


        public string RenderMaterialsGetCap(string request, string path,
                string param, IOSHttpRequest httpRequest,
                IOSHttpResponse httpResponse)
        {
            m_log.Debug("[MaterialsDemoModule]: GET cap handler");

            OSDMap resp = new OSDMap();

 
            int matsCount = 0;

            OSDArray allOsd = new OSDArray();

            foreach (KeyValuePair<UUID, OSDMap> kvp in m_knownMaterials)
            {
                OSDMap matMap = new OSDMap();

                matMap["ID"] = OSD.FromBinary(kvp.Key.GetBytes());

                matMap["Material"] = kvp.Value;
                allOsd.Add(matMap);
                matsCount++;
            }


            resp["Zipped"] = ZCompressOSD(allOsd, false);
            m_log.Debug("[MaterialsDemoModule]: matsCount: " + matsCount.ToString());

            return OSDParser.SerializeLLSDXmlString(resp);
        }

        public Dictionary<UUID, OSDMap> m_knownMaterials = new Dictionary<UUID, OSDMap>();

        /// <summary>
        /// computes a UUID by hashing a OSD object
        /// </summary>
        /// <param name="osd"></param>
        /// <returns></returns>
        private static UUID HashOsd(OSD osd)
        {
            using (var md5 = MD5.Create())
                using (MemoryStream ms = new MemoryStream(OSDParser.SerializeLLSDBinary(osd, false)))
                    return new UUID(md5.ComputeHash(ms), 0);
        }

        public static OSD ZCompressOSD(OSD inOsd, bool useHeader = true)
        {
            OSD osd = null;

            using (MemoryStream msSinkCompressed = new MemoryStream())
            {
                using (Ionic.Zlib.ZlibStream zOut = new Ionic.Zlib.ZlibStream(msSinkCompressed, 
                    Ionic.Zlib.CompressionMode.Compress, CompressionLevel.BestCompression, true))
                {
                    CopyStream(new MemoryStream(OSDParser.SerializeLLSDBinary(inOsd, useHeader)), zOut);
                    zOut.Close();
                }

                msSinkCompressed.Seek(0L, SeekOrigin.Begin);
                osd = OSD.FromBinary( msSinkCompressed.ToArray());
            }

            return osd;
        }


        public static OSD ZDecompressBytesToOsd(byte[] input)
        {
            OSD osd = null;

            using (MemoryStream msSinkUnCompressed = new MemoryStream())
            {
                using (Ionic.Zlib.ZlibStream zOut = new Ionic.Zlib.ZlibStream(msSinkUnCompressed, CompressionMode.Decompress, true))
                {
                    CopyStream(new MemoryStream(input), zOut);
                    zOut.Close();
                }
                msSinkUnCompressed.Seek(0L, SeekOrigin.Begin);
                osd = OSDParser.DeserializeLLSDBinary(msSinkUnCompressed.ToArray());
            }

            return osd;
        }

        static void CopyStream(System.IO.Stream input, System.IO.Stream output)
        {
            byte[] buffer = new byte[2048];
            int len;
            while ((len = input.Read(buffer, 0, 2048)) > 0)
            {
                output.Write(buffer, 0, len);
            }

            output.Flush();
        }

    }
}