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
using System.Text;
using System.Threading;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenMetaverse.Imaging;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.Agent.TextureSender
{
    public delegate void J2KDecodeDelegate(UUID AssetId);

    public class J2KDecoderModule : IRegionModule, IJ2KDecoder
    {
        #region IRegionModule Members

        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Cached Decoded Layers
        /// </summary>
        private readonly Dictionary<UUID, OpenJPEG.J2KLayerInfo[]> m_cacheddecode = new Dictionary<UUID, OpenJPEG.J2KLayerInfo[]>();
        private bool OpenJpegFail = false;
        private string CacheFolder = Util.dataDir() + "/j2kDecodeCache";
        private int CacheTimeout = 720;
        private J2KDecodeFileCache fCache = null;
        private Thread CleanerThread = null;
        private IAssetService AssetService = null;
        private Scene m_Scene = null;

        /// <summary>
        /// List of client methods to notify of results of decode
        /// </summary>
        private readonly Dictionary<UUID, List<DecodedCallback>> m_notifyList = new Dictionary<UUID, List<DecodedCallback>>();

        public J2KDecoderModule()
        {
        }

        public void Initialise(Scene scene, IConfigSource source)
        {
            if (m_Scene == null)
                m_Scene = scene;

            IConfig j2kConfig = source.Configs["J2KDecoder"];
            if (j2kConfig != null)
            {
                CacheFolder = j2kConfig.GetString("CacheDir", CacheFolder);
                CacheTimeout = j2kConfig.GetInt("CacheTimeout", CacheTimeout);
            }

            if (fCache == null)
                fCache = new J2KDecodeFileCache(CacheFolder, CacheTimeout);

            scene.RegisterModuleInterface<IJ2KDecoder>(this);

            if (CleanerThread == null && CacheTimeout != 0)
            {
                CleanerThread = new Thread(CleanCache);
                CleanerThread.Name = "J2KCleanerThread";
                CleanerThread.IsBackground = true;
                CleanerThread.Start();
            }
        }

        public void PostInitialise()
        {
            AssetService = m_Scene.AssetService;
        }

        public void Close()
        {
            
        }

        public string Name
        {
            get { return "J2KDecoderModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        #region IJ2KDecoder Members


        public void decode(UUID AssetId, byte[] assetData, DecodedCallback decodedReturn)
        {
            // Dummy for if decoding fails.
            OpenJPEG.J2KLayerInfo[] result = new OpenJPEG.J2KLayerInfo[0];

            // Check if it's cached
            bool cached = false;
            lock (m_cacheddecode)
            {
                if (m_cacheddecode.ContainsKey(AssetId))
                {
                    cached = true;
                    result = m_cacheddecode[AssetId];
                }
            }

            // If it's cached, return the cached results
            if (cached)
            {
                decodedReturn(AssetId, result);
            }
            else
            {
                // not cached, so we need to decode it
                // Add to notify list and start decoding.
                // Next request for this asset while it's decoding will only be added to the notify list
                // once this is decoded, requests will be served from the cache and all clients in the notifylist will be updated
                bool decode = false;
                lock (m_notifyList)
                {
                    if (m_notifyList.ContainsKey(AssetId))
                    {
                        m_notifyList[AssetId].Add(decodedReturn);
                    }
                    else
                    {
                        List<DecodedCallback> notifylist = new List<DecodedCallback>();
                        notifylist.Add(decodedReturn);
                        m_notifyList.Add(AssetId, notifylist);
                        decode = true;
                    }
                }
                // Do Decode!
                if (decode)
                {
                    doJ2kDecode(AssetId, assetData);
                }
            }
        }

        /// <summary>
        /// Provides a synchronous decode so that caller can be assured that this executes before the next line
        /// </summary>
        /// <param name="AssetId"></param>
        /// <param name="j2kdata"></param>
        public void syncdecode(UUID AssetId, byte[] j2kdata)
        {
            doJ2kDecode(AssetId, j2kdata);
        }

        #endregion

        /// <summary>
        /// Decode Jpeg2000 Asset Data
        /// </summary>
        /// <param name="AssetId">UUID of Asset</param>
        /// <param name="j2kdata">Byte Array Asset Data </param>
        private void doJ2kDecode(UUID AssetId, byte[] j2kdata)
        {
            int DecodeTime = 0;
            DecodeTime = Environment.TickCount;
            OpenJPEG.J2KLayerInfo[] layers = new OpenJPEG.J2KLayerInfo[0]; // Dummy result for if it fails.  Informs that there's only full quality

            if (!OpenJpegFail)
            {
                if (!fCache.TryLoadCacheForAsset(AssetId, out layers))
                {
                    try
                    {

                        AssetTexture texture = new AssetTexture(AssetId, j2kdata);
                        if (texture.DecodeLayerBoundaries())
                        {
                            bool sane = true;

                            // Sanity check all of the layers
                            for (int i = 0; i < texture.LayerInfo.Length; i++)
                            {
                                if (texture.LayerInfo[i].End > texture.AssetData.Length)
                                {
                                    sane = false;
                                    break;
                                }
                            }

                            if (sane)
                            {
                                layers = texture.LayerInfo;
                                fCache.SaveFileCacheForAsset(AssetId, layers);
                               

                                    // Write out decode time
                                    m_log.InfoFormat("[J2KDecoderModule]: {0} Decode Time: {1}", Environment.TickCount - DecodeTime,
                                                     AssetId);
                               
                            }
                            else
                            {
                                m_log.WarnFormat(
                                    "[J2KDecoderModule]: JPEG2000 texture decoding succeeded, but sanity check failed for {0}",
                                    AssetId);
                            }
                        }

                        else
                        {
                            /*
                            Random rnd = new Random();
                             // scramble ends for test
                            for (int i = 0; i < texture.LayerInfo.Length; i++)
                            {
                                texture.LayerInfo[i].End = rnd.Next(999999);
                            }
                            */

                            // Try to do some heuristics error correction!  Yeah.
                            bool sane2Heuristics = true;


                            if (texture.Image == null)
                                sane2Heuristics = false;

                            if (texture.LayerInfo == null)
                                sane2Heuristics = false;

                            if (sane2Heuristics)
                            {


                                if (texture.LayerInfo.Length == 0)
                                    sane2Heuristics = false;
                            }

                            if (sane2Heuristics)
                            {
                                // Last layer start is less then the end of the file and last layer start is greater then 0
                                if (texture.LayerInfo[texture.LayerInfo.Length - 1].Start < texture.AssetData.Length && texture.LayerInfo[texture.LayerInfo.Length - 1].Start > 0)
                                {
                                }
                                else
                                {
                                    sane2Heuristics = false;
                                }

                            }

                            if (sane2Heuristics)
                            {
                                int start = 0;
                                
                                // try to fix it by using consistant data in the start field
                                for (int i = 0; i < texture.LayerInfo.Length; i++)
                                {
                                    if (i == 0)
                                        start = 0;

                                    if (i == texture.LayerInfo.Length - 1)
                                        texture.LayerInfo[i].End = texture.AssetData.Length;
                                    else
                                        texture.LayerInfo[i].End = texture.LayerInfo[i + 1].Start - 1;

                                    // in this case, the end of the next packet is less then the start of the last packet
                                    // after we've attempted to fix it which means the start of the last packet is borked
                                    // there's no recovery from this
                                    if (texture.LayerInfo[i].End < start)
                                    {
                                        sane2Heuristics = false;
                                        break;
                                    }
 
                                    if (texture.LayerInfo[i].End < 0 || texture.LayerInfo[i].End > texture.AssetData.Length)
                                    {
                                        sane2Heuristics = false;
                                        break;
                                    }

                                    if (texture.LayerInfo[i].Start < 0 || texture.LayerInfo[i].Start > texture.AssetData.Length)
                                    {
                                        sane2Heuristics = false;
                                        break;
                                    }

                                    start = texture.LayerInfo[i].Start;
                                }
                            }

                            if (sane2Heuristics)
                            {
                                layers = texture.LayerInfo;
                                fCache.SaveFileCacheForAsset(AssetId, layers);


                                // Write out decode time
                                m_log.InfoFormat("[J2KDecoderModule]: HEURISTICS SUCCEEDED {0} Decode Time: {1}", Environment.TickCount - DecodeTime,
                                                 AssetId);

                            }
                            else
                            {
                                m_log.WarnFormat("[J2KDecoderModule]: JPEG2000 texture decoding failed for {0}.   Is this a texture?  is it J2K?", AssetId);
                            }
                        }
                        texture = null; // dereference and dispose of ManagedImage
                    }
                    catch (DllNotFoundException)
                    {
                        m_log.Error(
                            "[J2KDecoderModule]: OpenJpeg is not installed properly. Decoding disabled!  This will slow down texture performance!  Often times this is because of an old version of GLIBC.  You must have version 2.4 or above!");
                        OpenJpegFail = true;
                    }
                    catch (Exception ex)
                    {
                        m_log.WarnFormat(
                            "[J2KDecoderModule]: JPEG2000 texture decoding threw an exception for {0}, {1}",
                            AssetId, ex);
                    }
                }
               
            }

            // Cache Decoded layers
            lock (m_cacheddecode)
            {
                if (m_cacheddecode.ContainsKey(AssetId))
                    m_cacheddecode.Remove(AssetId);
                m_cacheddecode.Add(AssetId, layers);

            }

            // Notify Interested Parties
            lock (m_notifyList)
            {
                if (m_notifyList.ContainsKey(AssetId))
                {
                    foreach (DecodedCallback d in m_notifyList[AssetId])
                    {
                        if (d != null)
                            d.DynamicInvoke(AssetId, layers);
                    }
                    m_notifyList.Remove(AssetId);
                }
            }
        }
        
        private void CleanCache()
        {
            m_log.Info("[J2KDecoderModule]: Cleaner thread started");

            while (true)
            {
                if (AssetService != null)
                    fCache.ScanCacheFiles(RedecodeTexture);

                System.Threading.Thread.Sleep(600000);
            }
        }

        private void RedecodeTexture(UUID assetID)
        {
            AssetBase texture = AssetService.Get(assetID.ToString());
            if (texture == null)
                return;

            doJ2kDecode(assetID, texture.Data);
        }
    }

    public class J2KDecodeFileCache
    {
        private readonly string m_cacheDecodeFolder;
        private readonly int m_cacheTimeout;
        private bool enabled = true;
        
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Creates a new instance of a file cache
        /// </summary>
        /// <param name="pFolder">base folder for the cache.  Will be created if it doesn't exist</param>
        public J2KDecodeFileCache(string pFolder, int timeout)
        {
            m_cacheDecodeFolder = pFolder;
            m_cacheTimeout = timeout;
            if (!Directory.Exists(pFolder))
            {
                Createj2KCacheFolder(pFolder);
            }
        }

        /// <summary>
        /// Save Layers to Disk Cache
        /// </summary>
        /// <param name="AssetId">Asset to Save the layers. Used int he file name by default</param>
        /// <param name="Layers">The Layer Data from OpenJpeg</param>
        /// <returns></returns>
        public bool SaveFileCacheForAsset(UUID AssetId, OpenJPEG.J2KLayerInfo[] Layers)
        {
            if (Layers.Length > 0 && enabled)
            {
                FileStream fsCache =
                    new FileStream(String.Format("{0}/{1}", m_cacheDecodeFolder, FileNameFromAssetId(AssetId)),
                                   FileMode.Create);
                StreamWriter fsSWCache = new StreamWriter(fsCache);
                StringBuilder stringResult = new StringBuilder();
                string strEnd = "\n";
                for (int i = 0; i < Layers.Length; i++)
                {
                    if (i == (Layers.Length - 1))
                        strEnd = "";

                    stringResult.AppendFormat("{0}|{1}|{2}{3}", Layers[i].Start, Layers[i].End, Layers[i].End - Layers[i].Start, strEnd);
                }
                fsSWCache.Write(stringResult.ToString());
                fsSWCache.Close();
                fsSWCache.Dispose();
                fsCache.Dispose();
                return true;
            }


            return false;
        }

        
        /// <summary>
        /// Loads the Layer data from the disk cache
        /// Returns true if load succeeded
        /// </summary>
        /// <param name="AssetId">AssetId that we're checking the cache for</param>
        /// <param name="Layers">out layers to save to</param>
        /// <returns>true if load succeeded</returns>
        public bool TryLoadCacheForAsset(UUID AssetId, out OpenJPEG.J2KLayerInfo[] Layers)
        {
            string filename = String.Format("{0}/{1}", m_cacheDecodeFolder, FileNameFromAssetId(AssetId));
            Layers = new OpenJPEG.J2KLayerInfo[0];

            if (!File.Exists(filename))
                return false;

            if (!enabled)
            {
                return false;
            }

            string readResult = string.Empty;

            try
            {
                FileStream fsCachefile =
                    new FileStream(filename,
                                   FileMode.Open);

                StreamReader sr = new StreamReader(fsCachefile);
                readResult = sr.ReadToEnd();

                sr.Close();
                sr.Dispose();
                fsCachefile.Dispose();

            }
            catch (IOException ioe)
            {
                if (ioe is PathTooLongException)
                {
                    m_log.Error(
                        "[J2KDecodeCache]: Cache Read failed. Path is too long.");
                }
                else if (ioe is DirectoryNotFoundException)
                {
                    m_log.Error(
                        "[J2KDecodeCache]: Cache Read failed. Cache Directory does not exist!");
                    enabled = false;
                }
                else
                {
                    m_log.Error(
                        "[J2KDecodeCache]: Cache Read failed. IO Exception.");
                }
                return false;

            }
            catch (UnauthorizedAccessException)
            {
                m_log.Error(
                    "[J2KDecodeCache]: Cache Read failed. UnauthorizedAccessException Exception. Do you have the proper permissions on this file?");
                return false;
            }
            catch (ArgumentException ae)
            {
                if (ae is ArgumentNullException)
                {
                    m_log.Error(
                        "[J2KDecodeCache]: Cache Read failed. No Filename provided");
                }
                else
                {
                    m_log.Error(
                   "[J2KDecodeCache]: Cache Read failed. Filname was invalid");
                }
                return false;
            }
            catch (NotSupportedException)
            {
                m_log.Error(
                    "[J2KDecodeCache]: Cache Read failed, not supported. Cache disabled!");
                enabled = false;

                return false;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[J2KDecodeCache]: Cache Read failed, unknown exception.  Error: {0}",
                    e.ToString());
                return false;
            }

            string[] lines = readResult.Split('\n');

            if (lines.Length <= 0)
                return false;

            Layers = new OpenJPEG.J2KLayerInfo[lines.Length];
            
            for (int i = 0; i < lines.Length; i++)
            {
                string[] elements = lines[i].Split('|');
                if (elements.Length == 3)
                {
                    int element1, element2;

                    try
                    {
                        element1 = Convert.ToInt32(elements[0]);
                        element2 = Convert.ToInt32(elements[1]);
                    }
                    catch (FormatException)
                    {
                        m_log.WarnFormat("[J2KDecodeCache]: Cache Read failed with ErrorConvert for {0}", AssetId);
                        Layers = new OpenJPEG.J2KLayerInfo[0];
                        return false;
                    }

                    Layers[i] = new OpenJPEG.J2KLayerInfo();
                    Layers[i].Start = element1;
                    Layers[i].End = element2;

                }
                else
                {
                    // reading failed
                    m_log.WarnFormat("[J2KDecodeCache]: Cache Read failed for {0}", AssetId);
                    Layers = new OpenJPEG.J2KLayerInfo[0];
                    return false;
                }
            }


                

            return true;
        }

        /// <summary>
        /// Routine which converts assetid to file name
        /// </summary>
        /// <param name="AssetId">asset id of the image</param>
        /// <returns>string filename</returns>
        public string FileNameFromAssetId(UUID AssetId)
        {
            return String.Format("j2kCache_{0}.cache", AssetId);
        }

        public UUID AssetIdFromFileName(string fileName)
        {
            string rawId = fileName.Replace("j2kCache_", "").Replace(".cache", "");
            UUID asset;
            if (!UUID.TryParse(rawId, out asset))
                return UUID.Zero;

            return asset;
        }

        /// <summary>
        /// Creates the Cache Folder
        /// </summary>
        /// <param name="pFolder">Folder to Create</param>
        public void Createj2KCacheFolder(string pFolder)
        {
            try
            {
                Directory.CreateDirectory(pFolder);
            }
            catch (IOException ioe)
            {
                if (ioe is PathTooLongException)
                {
                    m_log.Error(
                        "[J2KDecodeCache]: Cache Directory does not exist and create failed because the path to the cache folder is too long.  Cache disabled!");
                }
                else if (ioe is DirectoryNotFoundException)
                {
                    m_log.Error(
                        "[J2KDecodeCache]: Cache Directory does not exist and create failed because the supplied base of the directory folder does not exist.  Cache disabled!");
                }
                else
                {
                    m_log.Error(
                        "[J2KDecodeCache]: Cache Directory does not exist and create failed because of an IO Exception.  Cache disabled!");
                }
                enabled = false;

            }
            catch (UnauthorizedAccessException)
            {
                m_log.Error(
                    "[J2KDecodeCache]: Cache Directory does not exist and create failed because of an UnauthorizedAccessException Exception.  Cache disabled!");
                enabled = false;
            }
            catch (ArgumentException ae)
            {
                if (ae is ArgumentNullException)
                {
                    m_log.Error(
                        "[J2KDecodeCache]: Cache Directory does not exist and create failed because the folder provided is invalid!  Cache disabled!");
                }
                else
                {
                    m_log.Error(
                   "[J2KDecodeCache]: Cache Directory does not exist and create failed because no cache folder was provided!  Cache disabled!");
                }
                enabled = false;
            }
            catch (NotSupportedException)
            {
                m_log.Error(
                    "[J2KDecodeCache]: Cache Directory does not exist and create failed because it's not supported.  Cache disabled!");
                enabled = false;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[J2KDecodeCache]: Cache Directory does not exist and create failed because of an unknown exception.  Cache disabled!  Error: {0}",
                    e.ToString());
                enabled = false;
            }
        }

        public void ScanCacheFiles(J2KDecodeDelegate decode)
        {
            DirectoryInfo dir = new DirectoryInfo(m_cacheDecodeFolder);
            FileInfo[] files = dir.GetFiles("j2kCache_*.cache");

            foreach (FileInfo f in files)
            {
                TimeSpan fileAge = DateTime.Now - f.CreationTime;

                if (m_cacheTimeout != 0 && fileAge >= TimeSpan.FromMinutes(m_cacheTimeout))
                {
                    File.Delete(f.Name);
                    decode(AssetIdFromFileName(f.Name));
                    System.Threading.Thread.Sleep(5000);
                }
            }
        }
    }
}
