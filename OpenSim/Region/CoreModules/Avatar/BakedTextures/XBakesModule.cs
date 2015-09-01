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

using OpenMetaverse;
using Nini.Config;
using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.ServiceAuth;
using OpenSim.Framework.Communications;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using Mono.Addins;

namespace OpenSim.Region.CoreModules.Avatar.BakedTextures
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "XBakes.Module")]
    public class XBakesModule : INonSharedRegionModule, IBakedTextureModule
    {
        protected Scene m_Scene;
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private UTF8Encoding enc = new UTF8Encoding();
        private string m_URL = String.Empty;
        private static XmlSerializer m_serializer = new XmlSerializer(typeof(AssetBase));

        private static IServiceAuth m_Auth;

        public void Initialise(IConfigSource configSource)
        {
            IConfig config = configSource.Configs["XBakes"];
            if (config == null)
                return;

            m_URL = config.GetString("URL", String.Empty);
            m_Auth = ServiceAuth.Create(configSource, "XBakes");
        }

        public void AddRegion(Scene scene)
        {
            // m_log.InfoFormat("[XBakes]: Enabled for region {0}", scene.RegionInfo.RegionName);
            m_Scene = scene;

            scene.RegisterModuleInterface<IBakedTextureModule>(this);
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "XBakes.Module"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public WearableCacheItem[] Get(UUID id)
        {
            if (m_URL == String.Empty)
                return null;

            int size = 0;

            using (RestClient rc = new RestClient(m_URL))
            {
                List<WearableCacheItem> ret = new List<WearableCacheItem>();
                rc.AddResourcePath("bakes");
                rc.AddResourcePath(id.ToString());

                rc.RequestMethod = "GET";

                try
                {
                    Stream s = rc.Request(m_Auth);

                    using (XmlTextReader sr = new XmlTextReader(s))
                    {
                        sr.ReadStartElement("BakedAppearance");
                        while (sr.LocalName == "BakedTexture")
                        {
                            string sTextureIndex = sr.GetAttribute("TextureIndex");
                            int lTextureIndex = Convert.ToInt32(sTextureIndex);
                            string sCacheId = sr.GetAttribute("CacheId");
                            UUID lCacheId = UUID.Zero;
                            if (!(UUID.TryParse(sCacheId, out lCacheId)))
                            {
                                // ??  Nothing here
                            }

                            ++size;

                            sr.ReadStartElement("BakedTexture");
                            AssetBase a = (AssetBase)m_serializer.Deserialize(sr);
                            ret.Add(new WearableCacheItem() { CacheId = lCacheId, TextureIndex = (uint)lTextureIndex, TextureAsset = a, TextureID = a.FullID });

                            sr.ReadEndElement();
                        }

                        m_log.DebugFormat("[XBakes]: read {0} textures for user {1}", ret.Count, id);
                    }

                    return ret.ToArray();
                }
                catch (XmlException)
                {
                    return null;
                }
            }
        }

        public void Store(UUID agentId, WearableCacheItem[] data)
        {
            if (m_URL == String.Empty)
                return;

            MemoryStream reqStream;

            using (MemoryStream bakeStream = new MemoryStream())
            using (XmlTextWriter bakeWriter = new XmlTextWriter(bakeStream, null))
            {
                bakeWriter.WriteStartElement(String.Empty, "BakedAppearance", String.Empty);

                for (int i = 0; i < data.Length; i++)
                {
                    if (data[i] != null)
                    {
                        bakeWriter.WriteStartElement(String.Empty, "BakedTexture", String.Empty);
                        bakeWriter.WriteAttributeString(String.Empty, "TextureIndex", String.Empty, data[i].TextureIndex.ToString());
                        bakeWriter.WriteAttributeString(String.Empty, "CacheId", String.Empty, data[i].CacheId.ToString());
                        if (data[i].TextureAsset != null)
                            m_serializer.Serialize(bakeWriter, data[i].TextureAsset);

                        bakeWriter.WriteEndElement();
                    }
                }

                bakeWriter.WriteEndElement();
                bakeWriter.Flush();

                reqStream = new MemoryStream(bakeStream.ToArray());
            }

            RestClient rc = new RestClient(m_URL);
            rc.AddResourcePath("bakes");
            rc.AddResourcePath(agentId.ToString());

            rc.RequestMethod = "POST";

            Util.FireAndForget(
                delegate
                {
                    rc.Request(reqStream, m_Auth);
                    m_log.DebugFormat("[XBakes]: stored {0} textures for user {1}", data.Length, agentId);
                }, null, "XBakesModule.Store"
            );
        }
    }
}