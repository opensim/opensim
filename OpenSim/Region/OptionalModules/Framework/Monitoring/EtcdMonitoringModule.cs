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
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using netcd;
using netcd.Serialization;
using netcd.Advanced;
using netcd.Advanced.Requests;

namespace OpenSim.Region.OptionalModules.Framework.Monitoring
{
    /// <summary>
    /// Allows to store monitoring data in etcd, a high availability
    /// name-value store.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "EtcdMonitoringModule")]
    public class EtcdMonitoringModule : INonSharedRegionModule, IEtcdModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected Scene m_scene;
        protected IEtcdClient m_client;
        protected bool m_enabled = false;
        protected string m_etcdBasePath = String.Empty;
        protected bool m_appendRegionID = true;

        public string Name
        {
            get { return "EtcdMonitoringModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialise(IConfigSource source)
        {
            if (source.Configs["Etcd"] == null)
                return;

            IConfig etcdConfig = source.Configs["Etcd"];

            string etcdUrls = etcdConfig.GetString("EtcdUrls", String.Empty);
            if (etcdUrls == String.Empty)
                return;

            m_etcdBasePath = etcdConfig.GetString("BasePath", m_etcdBasePath);
            m_appendRegionID = etcdConfig.GetBoolean("AppendRegionID", m_appendRegionID);

            if (!m_etcdBasePath.EndsWith("/"))
                m_etcdBasePath += "/";

            try
            {
                string[] endpoints = etcdUrls.Split(new char[] {','});
                List<Uri> uris = new List<Uri>();
                foreach (string endpoint in endpoints)
                    uris.Add(new Uri(endpoint.Trim()));

                m_client = new EtcdClient(uris.ToArray(), new DefaultSerializer(), new DefaultSerializer());
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[ETCD]: Error initializing connection: " + e.ToString());
                return;
            }

            m_log.DebugFormat("[ETCD]: Etcd module configured");
            m_enabled = true;
        }

        public void Close()
        {
            //m_client = null;
            m_scene = null;
        }

        public void AddRegion(Scene scene)
        {
            m_scene = scene;

            if (m_enabled)
            {
                if (m_appendRegionID)
                    m_etcdBasePath += m_scene.RegionInfo.RegionID.ToString() + "/";

                m_log.DebugFormat("[ETCD]: Using base path {0} for all keys", m_etcdBasePath);

                try
                {
                    m_client.Advanced.CreateDirectory(new CreateDirectoryRequest() {Key = m_etcdBasePath});
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("Exception trying to create base path {0}: " + e.ToString(), m_etcdBasePath);
                }

                scene.RegisterModuleInterface<IEtcdModule>(this);
            }
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public bool Store(string k, string v)
        {
            return Store(k, v, 0);
        }

        public bool Store(string k, string v, int ttl)
        {
            Response resp = m_client.Advanced.SetKey(new SetKeyRequest() { Key = m_etcdBasePath + k, Value = v, TimeToLive = ttl });

            if (resp == null)
                return false;

            if (resp.ErrorCode.HasValue)
            {
                m_log.DebugFormat("[ETCD]: Error {0} ({1}) storing {2} => {3}", resp.Cause, (int)resp.ErrorCode, m_etcdBasePath + k, v);

                return false;
            }

            return true;
        }

        public string Get(string k)
        {
            Response resp = m_client.Advanced.GetKey(new GetKeyRequest() { Key = m_etcdBasePath + k });

            if (resp == null)
                return String.Empty;

            if (resp.ErrorCode.HasValue)
            {
                m_log.DebugFormat("[ETCD]: Error {0} ({1}) getting {2}", resp.Cause, (int)resp.ErrorCode, m_etcdBasePath + k);

                return String.Empty;
            }

            return resp.Node.Value;
        }

        public void Delete(string k)
        {
            m_client.Advanced.DeleteKey(new DeleteKeyRequest() { Key = m_etcdBasePath + k });
        }

        public void Watch(string k, Action<string> callback)
        {
            m_client.Advanced.WatchKey(new WatchKeyRequest() { Key = m_etcdBasePath + k, Callback = (x) => { callback(x.Node.Value); } });
        }
    }
}
