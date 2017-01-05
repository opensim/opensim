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
using Nini.Config;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.Server.Handlers.Base
{
    public interface IServiceConnector
    {
    }

    public class ServiceConnector : IServiceConnector
    {
        public virtual string ConfigURL
        {
            get { return String.Empty; }
        }

        public virtual string ConfigName
        {
            get;
            protected set;
        }

        public virtual string ConfigFile
        {
            get;
            protected set;
        }

        public virtual IConfigSource Config
        {
            get;
            protected set;
        }

        public ServiceConnector()
        {
        }

        public ServiceConnector(IConfigSource config, IHttpServer server, string configName)
        {
        }

        // We call this from our plugin module to get our configuration
        public IConfig GetConfig()
        {
            IConfig config = null;
            config = ServerUtils.GetConfig(ConfigFile, ConfigName);

            // Our file is not here? We can get one to bootstrap our plugin module
            if ( config == null )
            {
                IConfigSource remotesource = GetConfigSource();

                if (remotesource != null)
                {
                    IniConfigSource initialconfig = new IniConfigSource();
                    initialconfig.Merge (remotesource);
                    initialconfig.Save(ConfigFile);
                }

                config = remotesource.Configs[ConfigName];
            }

            return config;
        }

        // We get our remote initial configuration for bootstrapping in case
        // we have no configuration in our main file or in an existing
        // modular config file. This is the last resort to bootstrap the
        // configuration, likely a new plugin loading for the first time.
        private IConfigSource GetConfigSource()
        {
            IConfigSource source = null;

            source = ServerUtils.LoadInitialConfig(ConfigURL);

            if (source == null)
                System.Console.WriteLine(String.Format ("Config Url: {0} Not found!", ConfigURL));

            return source;
        }
    }
}
