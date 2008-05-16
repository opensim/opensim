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
*
*/

using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Timers;
using libsecondlife;
using Mono.Addins;
using Nwc.XmlRpc;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Communications;
using OpenSim.Region.Environment.Scenes;

// [assembly : Addin]
// [assembly : AddinDependency("OpenSim", "0.5")]

namespace OpenSim.ApplicationPlugins.Rest
{

    // [Extension("/OpenSim/Startup")]
    public abstract class RestPlugin : IApplicationPlugin
    {
        #region properties

        protected static readonly log4net.ILog  m_log =
            log4net.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IConfig        _config;       // Configuration source: Rest Plugins
        private IConfig        _pluginConfig; // Configuration source: Plugin specific
        private OpenSimMain    _app;          // The 'server'
        private BaseHttpServer _httpd;        // The server's RPC interface
        private string         _prefix;       // URL prefix below which all REST URLs are living

        private string _godkey;
        private int    _reqk;

        [ThreadStaticAttribute]
        private static string  _threadRequestID = String.Empty;

        /// <summary>
        /// Return an ever increasing request ID for logging
        /// </summary>
        protected string RequestID
        {
            get { return _reqk++.ToString(); }
            set { _reqk = Convert.ToInt32(value); }
        }

        /// <summary>
        /// Thread-constant message IDs for logging.
        /// </summary>
        protected string MsgID
        {
            get { return String.Format("[REST-{0}] #{1}", Name, _threadRequestID); }
            set { _threadRequestID = value; }
        }

        /// <summary>
        /// Returns true if Rest Plugins are enabled.
        /// </summary>
        public bool PluginsAreEnabled
        {
            get { return null != _config; }
        }

        /// <summary>
        /// Returns true if specific Rest Plugin is enabled.
        /// </summary>
        public bool IsEnabled
        {
            get
            {
                return (null != _pluginConfig) && _pluginConfig.GetBoolean("enabled", false);
            }
        }

        /// <summary>
        /// OpenSimMain application
        /// </summary>
        public OpenSimMain App
        {
            get { return _app; }
        }

        /// <summary>
        /// RPC server
        /// </summary>
        public BaseHttpServer HttpServer
        {
            get { return _httpd; }
        }

        /// <summary>
        /// URL prefix to use for all REST handlers
        /// </summary>
        public string Prefix
        {
            get { return _prefix; }
        }

        /// <summary>
        /// Configuration of the plugin
        /// </summary>
        public IConfig Config
        {
            get { return _pluginConfig; }
        }

        /// <summary>
        /// Name of the plugin
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Return the config section name
        /// </summary>
        public abstract string ConfigName { get; }
        #endregion properties


        #region methods
        /// <summary>
        /// This method is called by OpenSimMain immediately after loading the
        /// plugin and after basic server setup,  but before running any server commands.
        /// </summary>
        /// <remarks>
        /// Note that entries MUST be added to the active configuration files before
        /// the plugin can be enabled.
        /// </remarks>
        public virtual void Initialise(OpenSimMain openSim)
        {
            RequestID = "0";
            MsgID = RequestID;

            try
            {
                if ((_config = openSim.ConfigSource.Configs["RestPlugins"]) == null) {
                    m_log.WarnFormat("{0} Rest Plugins not configured", MsgID);
                    return;
                }

                if (!_config.GetBoolean("enabled", false))
                {
                    m_log.WarnFormat("{0} Rest Plugins are disabled", MsgID);
                    return;
                }

                _app = openSim;
                _httpd = openSim.HttpServer;

                // Retrieve GOD key value, if any.
                _godkey = _config.GetString("god_key", String.Empty);
                // Retrive prefix if any.
                _prefix = _config.GetString("prefix", "/admin");

                // Get plugin specific config
                _pluginConfig = openSim.ConfigSource.Configs[ConfigName];

                m_log.InfoFormat("{0} Rest Plugins Enabled", MsgID);
            }
            catch (Exception e)
            {
                // we can safely ignore this, as it just means that
                // the key lookup in Configs failed, which signals to
                // us that noone is interested in our services...they
                // don't know what they are missing out on...
                // NOTE: Under the present OpenSim implementation it is
                // not possible for the openSim pointer to be null. However
                // were the implementation to be changed, this could
                // result in a silent initialization failure. Harmless
                // except for lack of function and lack of any
                // diagnostic indication as to why. The same is true if
                // the HTTP server reference is bad.
                // We should at least issue a message...
                m_log.WarnFormat("{0} Initialization failed: {1}", MsgID, e.Message);
                m_log.DebugFormat("{0} Initialization failed: {1}", MsgID, e.ToString());
            }
        }


        private List<RestStreamHandler> _handlers = new List<RestStreamHandler>();

        public void AddRestStreamHandler(string httpMethod, string path, RestMethod method)
        {
            if (!path.StartsWith(_prefix))
            {
                path = String.Format("{0}{1}", _prefix, path);
            }

            RestStreamHandler h = new RestStreamHandler(httpMethod, path, method);
            _httpd.AddStreamHandler(h);
            _handlers.Add(h);

            m_log.DebugFormat("{0} Added REST handler {1} {2}", MsgID, httpMethod, path);
        }


        public bool VerifyGod(string key)
        {
            if (String.IsNullOrEmpty(key)) return false;
            return key == _godkey;
        }

        public virtual void Close()
        {
            foreach (RestStreamHandler h in _handlers)
            {
                _httpd.RemoveStreamHandler(h.HttpMethod, h.Path);
            }
            _handlers = null;
        }
        #endregion methods
    }
}
