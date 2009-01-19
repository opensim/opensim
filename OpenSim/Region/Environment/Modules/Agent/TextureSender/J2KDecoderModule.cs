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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Reflection;
using System.Threading;
using System.Collections.Generic;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.Agent.TextureSender
{
    public class J2KDecoderModule : IRegionModule, IJ2KDecoder
    {
        #region IRegionModule Members

        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Cached Decoded Layers
        /// </summary>
        private readonly Dictionary<UUID, OpenJPEG.J2KLayerInfo[]> m_cacheddecode = new Dictionary<UUID, OpenJPEG.J2KLayerInfo[]>();

        /// <summary>
        /// List of client methods to notify of results of decode
        /// </summary>
        private readonly Dictionary<UUID, List<DecodedCallback>> m_notifyList = new Dictionary<UUID, List<DecodedCallback>>();

        public void Initialise(Scene scene, IConfigSource source)
        {
            scene.RegisterModuleInterface<IJ2KDecoder>(this);
        }

        public void PostInitialise()
        {
            
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

        #endregion

        /// <summary>
        /// Decode Jpeg2000 Asset Data
        /// </summary>
        /// <param name="AssetId">UUID of Asset</param>
        /// <param name="j2kdata">Byte Array Asset Data </param>
        private void doJ2kDecode(UUID AssetId, byte[] j2kdata)
        {
            int DecodeTime = 0;
            DecodeTime = System.Environment.TickCount;
            OpenJPEG.J2KLayerInfo[] layers = new OpenJPEG.J2KLayerInfo[0]; // Dummy result for if it fails.  Informs that there's only full quality
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
                    }
                    else
                    {
                        m_log.WarnFormat("[J2KDecoderModule]: JPEG2000 texture decoding succeeded, but sanity check failed for {0}",
                            AssetId);
                    }
                }
                
               else
               {
                   m_log.WarnFormat("[J2KDecoderModule]: JPEG2000 texture decoding failed for {0}", AssetId);
               }
               texture = null; // dereference and dispose of ManagedImage
            }
            catch (Exception ex)
            {
                m_log.WarnFormat("[J2KDecoderModule]: JPEG2000 texture decoding threw an exception for {0}, {1}", AssetId, ex);
            }

            // Write out decode time
            m_log.InfoFormat("[J2KDecoderModule]: {0} Decode Time: {1}", System.Environment.TickCount - DecodeTime, AssetId);
            
            // Cache Decoded layers
            lock (m_cacheddecode)
            {
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
    }
}
