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
using System.Collections;
using System.IO;
using System.Net;
using System.Reflection;
using System.Timers;
using libsecondlife;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Region.Environment.Modules.World.Terrain;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.ApplicationPlugins.GridInfo
{
    public class GridInfoPlugin : IApplicationPlugin
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        private OpenSimBase _app;
        private BaseHttpServer _httpd;

        private string _name = "GridInfoPlugin";
        private string _version = "0.1";
        private Hashtable _info = new Hashtable();

        public string Version { get { return _version; } }
        public string Name { get { return _name; } }

        public void Initialise() 
        { 
            _log.Info("[GridInfo]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException (Name);
        }

        public void Initialise(OpenSimBase openSim)
        {
            try
            {
                _log.Info("[GridInfo]: Grid Info Plugin Enabled");
                
                _app = openSim;
                _httpd = openSim.HttpServer;

                IConfig startupConfig = _app.ConfigSource.Source.Configs["Startup"];
                if (!startupConfig.GetBoolean("gridmode", false))
                {
                    _info["mode"] = "standalone";
                }
                else
                {
                    _info["mode"] = "grid";
                }
                _info["platform"] = "OpenSim";
                
                IConfig gridInfoConfig = _app.ConfigSource.Source.Configs["GridInfo"];
                foreach (string k in gridInfoConfig.GetKeys())
                {
                    _info[k] = gridInfoConfig.GetString(k);
                }
                
                _httpd.AddXmlRPCHandler("get_grid_info", XmlRpcGridInfoMethod);
            }
            catch (NullReferenceException)
            {
                // Ignore.
            }
        }

        

        public XmlRpcResponse XmlRpcGridInfoMethod(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();

            _log.Info("[GridInfo]: Request for grid info");

            foreach (string k in _info.Keys)
            {
                responseData[k] = _info[k];
            }
            response.Value = responseData;

            return response;
        }

        public void Dispose()
        {
        }
    }
}
