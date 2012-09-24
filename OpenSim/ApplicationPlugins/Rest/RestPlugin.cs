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
using System.Xml;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.ApplicationPlugins.Rest
{
    public abstract class RestPlugin : IApplicationPlugin
    {
        #region properties

        protected static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IConfig _config; // Configuration source: Rest Plugins
        private IConfig _pluginConfig; // Configuration source: Plugin specific
        private OpenSimBase _app; // The 'server'
        private BaseHttpServer _httpd; // The server's RPC interface
        private string _prefix; // URL prefix below
        // which all REST URLs
        // are living
        // private StringWriter _sw = null;
        // private RestXmlWriter _xw = null;

        private string _godkey;
        private int _reqk;

        [ThreadStatic]
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
        public OpenSimBase App
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
        /// Access to GOD password string
        /// </summary>
        protected string GodKey
        {
            get { return _godkey; }
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

        // public XmlTextWriter XmlWriter
        // {
        //     get
        //     {
        //         if (null == _xw)
        //         {
        //             _sw = new StringWriter();
        //             _xw = new RestXmlWriter(_sw);
        //             _xw.Formatting = Formatting.Indented;
        //         }
        //         return _xw;
        //     }
        // }

        // public string XmlWriterResult
        // {
        //     get
        //     {
        //         _xw.Flush();
        //         _xw.Close();
        //         _xw = null;

        //         return _sw.ToString();
        //     }
        // }

        #endregion properties

        #region methods

        // TODO: required by IPlugin, but likely not at all right
        private string m_version = "0.0";

        public string Version
        {
            get { return m_version; }
        }

        public void Initialise()
        {
            m_log.Info("[RESTPLUGIN]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException(Name);
        }

        /// <summary>
        /// This method is called by OpenSimMain immediately after loading the
        /// plugin and after basic server setup,  but before running any server commands.
        /// </summary>
        /// <remarks>
        /// Note that entries MUST be added to the active configuration files before
        /// the plugin can be enabled.
        /// </remarks>
        public virtual void Initialise(OpenSimBase openSim)
        {
            RequestID = "0";
            MsgID = RequestID;

            try
            {
                if ((_config = openSim.ConfigSource.Source.Configs["RestPlugins"]) == null)
                {
                    m_log.WarnFormat("{0} Rest Plugins not configured", MsgID);
                    return;
                }

                if (!_config.GetBoolean("enabled", false))
                {
                    //m_log.WarnFormat("{0} Rest Plugins are disabled", MsgID);
                    return;
                }

                _app = openSim;
                _httpd = openSim.HttpServer;

                // Retrieve GOD key value, if any.
                _godkey = _config.GetString("god_key", String.Empty);

                // Retrive prefix if any.
                _prefix = _config.GetString("prefix", "/admin");

                // Get plugin specific config
                _pluginConfig = openSim.ConfigSource.Source.Configs[ConfigName];

                m_log.InfoFormat("{0} Rest Plugins Enabled", MsgID);
            }
            catch (Exception e)
            {
                // we can safely ignore this, as it just means that
                // the key lookup in Configs failed, which signals to
                // us that noone is interested in our services...they
                // don't know what they are missing out on...
                // NOTE: Under the present OpenSimulator implementation it is
                // not possible for the openSimulator pointer to be null. However
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

        public virtual void PostInitialise()
        {
        }

        private List<RestStreamHandler> _handlers = new List<RestStreamHandler>();
        private Dictionary<string, IHttpAgentHandler> _agents = new Dictionary<string, IHttpAgentHandler>();

        /// <summary>
        /// Add a REST stream handler to the underlying HTTP server.
        /// </summary>
        /// <param name="httpMethod">GET/PUT/POST/DELETE or
        /// similar</param>
        /// <param name="path">URL prefix</param>
        /// <param name="method">RestMethod handler doing the actual work</param>
        public virtual void AddRestStreamHandler(string httpMethod, string path, RestMethod method)
        {
            if (!IsEnabled) return;

            if (!path.StartsWith(_prefix))
            {
                path = String.Format("{0}{1}", _prefix, path);
            }

            RestStreamHandler h = new RestStreamHandler(httpMethod, path, method);
            _httpd.AddStreamHandler(h);
            _handlers.Add(h);

            m_log.DebugFormat("{0} Added REST handler {1} {2}", MsgID, httpMethod, path);
        }

        /// <summary>
        /// Add a powerful Agent handler to the underlying HTTP
        /// server.
        /// </summary>
        /// <param name="agentName">name of agent handler</param>
        /// <param name="handler">agent handler method</param>
        /// <returns>false when the plugin is disabled or the agent
        /// handler could not be added. Any generated exceptions are
        /// allowed to drop through to the caller, i.e. ArgumentException.
        /// </returns>
        public bool AddAgentHandler(string agentName, IHttpAgentHandler handler)
        {
            if (!IsEnabled) return false;
            _agents.Add(agentName, handler);
//            return _httpd.AddAgentHandler(agentName, handler);
            
            return false;
        }

        /// <summary>
        /// Remove a powerful Agent handler from the underlying HTTP
        /// server.
        /// </summary>
        /// <param name="agentName">name of agent handler</param>
        /// <param name="handler">agent handler method</param>
        /// <returns>false when the plugin is disabled or the agent
        /// handler could not be removed. Any generated exceptions are
        /// allowed to drop through to the caller, i.e. KeyNotFound.
        /// </returns>
        public bool RemoveAgentHandler(string agentName, IHttpAgentHandler handler)
        {
            if (!IsEnabled) return false;
            if (_agents[agentName] == handler)
            {
                _agents.Remove(agentName);
//                return _httpd.RemoveAgentHandler(agentName, handler);
            }
            return false;
        }

        /// <summary>
        /// Check whether the HTTP request came from god; that is, is
        /// the god_key as configured in the config section supplied
        /// via X-OpenSim-Godkey?
        /// </summary>
        /// <param name="request">HTTP request header</param>
        /// <returns>true when the HTTP request came from god.</returns>
        protected bool IsGod(IOSHttpRequest request)
        {
            string[] keys = request.Headers.GetValues("X-OpenSim-Godkey");
            if (null == keys) return false;

            // we take the last key supplied
            return keys[keys.Length - 1] == _godkey;
        }

        /// <summary>
        /// Checks wether the X-OpenSim-Password value provided in the
        /// HTTP header is indeed the password on file for the avatar
        /// specified by the UUID
        /// </summary>
        protected bool IsVerifiedUser(IOSHttpRequest request, UUID uuid)
        {
            // XXX under construction
            return false;
        }

        /// <summary>
        /// Clean up and remove all handlers that were added earlier.
        /// </summary>
        public virtual void Close()
        {
            foreach (RestStreamHandler h in _handlers)
            {
                _httpd.RemoveStreamHandler(h.HttpMethod, h.Path);
            }
            _handlers = null;
//            foreach (KeyValuePair<string, IHttpAgentHandler> h in _agents)
//            {
//                _httpd.RemoveAgentHandler(h.Key, h.Value);
//            }
            _agents = null;
        }

        public virtual void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Return a failure message.
        /// </summary>
        /// <param name="method">origin of the failure message</param>
        /// <param name="message">failure message</param>
        /// <remarks>This should probably set a return code as
        /// well. (?)</remarks>
        protected string Failure(IOSHttpResponse response, OSHttpStatusCode status,
                                 string method, string format, params string[] msg)
        {
            string m = String.Format(format, msg);

            response.StatusCode = (int) status;
            response.StatusDescription = m;

            m_log.ErrorFormat("{0} {1} failed: {2}", MsgID, method, m);
            return String.Format("<error>{0}</error>", m);
        }

        /// <summary>
        /// Return a failure message.
        /// </summary>
        /// <param name="method">origin of the failure message</param>
        /// <param name="e">exception causing the failure message</param>
        /// <remarks>This should probably set a return code as
        /// well. (?)</remarks>
        public string Failure(IOSHttpResponse response, OSHttpStatusCode status,
                              string method, Exception e)
        {
            string m = String.Format("exception occurred: {0}", e.Message);

            response.StatusCode = (int) status;
            response.StatusDescription = m;

            m_log.DebugFormat("{0} {1} failed: {2}", MsgID, method, e.ToString());
            m_log.ErrorFormat("{0} {1} failed: {2}", MsgID, method, e.Message);

            return String.Format("<error>{0}</error>", e.Message);
        }

        #endregion methods
    }
}
