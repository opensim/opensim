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
using System.IO;
using Nini.Config;
using OpenSim;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment;
using Mono.Addins;
using Mono.Addins.Description;

namespace OpenSim.Tools.Export
{
    public class OpenSimExport
    {
        private IniConfigSource config;
        private StorageManager sman;
        
        public OpenSimExport(IniConfigSource config)
        {
            this.config = config;
            IConfig startup = config.Configs["Startup"];
            sman = new StorageManager(
                                      startup.GetString("storage_plugin", "OpenSim.DataStore.NullStorage.dll"),
                                      startup.GetString("storage_connection_string","")
                                      );
            
        }

        public static void Main(string[] args)
        {
            OpenSimExport export = new OpenSimExport(InitConfig(args));

            System.Console.WriteLine("This application does nothing useful yet");
        }

        

        private static IniConfigSource InitConfig(string[] args)
        {
            System.Console.WriteLine("Good");
            ArgvConfigSource configSource = new ArgvConfigSource(args);
            configSource.AddSwitch("Startup", "inifile");

//             AddinManager.Initialize(".");
//             AddinManager.Registry.Update(null);

            IConfig startupConfig = configSource.Configs["Startup"];
            string iniFilePath = startupConfig.GetString("inifile", "OpenSim.ini");
            
            IniConfigSource config = new IniConfigSource();
            //check for .INI file (either default or name passed in command line)
            if (File.Exists(iniFilePath))
            {
                config.Merge(new IniConfigSource(iniFilePath));
                config.Merge(configSource);
            }
            else
            {
                iniFilePath = Path.Combine(Util.configDir(), iniFilePath);
                if (File.Exists(iniFilePath))
                {
                    config.Merge(new IniConfigSource(iniFilePath));
                    config.Merge(configSource);
                }
                else
                {
                    // no default config files, so set default values, and save it
                    // SetDefaultConfig();
                    config.Merge(OpenSim.OpenSimMain.DefaultConfig());
                    config.Merge(configSource);
                }
            }
            
            // ReadConfigSettings();
            
            return config;
        }
    }
}