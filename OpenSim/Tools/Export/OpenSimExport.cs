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
using System.IO;
using log4net.Config;
using Nini.Config;
using OpenSim;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment;
using OpenSim.Region.Environment.Scenes;

namespace OpenSimExport
{
    public class OpenSimExport
    {
        public IniConfigSource config;
        public StorageManager sman;

        public OpenSimExport(IniConfigSource config)
        {
            this.config = config;
            IConfig startup = config.Configs["Startup"];
            // AddinManager.Initialize(".");
            // AddinManager.Registry.Update(null);

            MainConsole.Instance = CreateConsole();

            sman = new StorageManager(
                startup.GetString("storage_plugin", "OpenSim.DataStore.NullStorage.dll"),
                startup.GetString("storage_connection_string", String.Empty),
                startup.GetString("estate_connection_string", String.Empty)
                );
        }

        public static void Main(string[] args)
        {
            XmlConfigurator.Configure();

            OpenSimExport export = new OpenSimExport(InitConfig(args));
            RegionInfo reg = new RegionInfo("Sara Jane", "Regions/1000-1000.xml",false);

            Console.WriteLine("This application does nothing useful yet: " + reg.RegionID);
            foreach (SceneObjectGroup group in export.sman.DataStore.LoadObjects(reg.RegionID))
            {
                Console.WriteLine("{0} -> {1}", reg.RegionID, group.UUID);
            }
        }

        protected static ConsoleBase CreateConsole()
        {
            return new ConsoleBase("Export", null);
        }

        private static IniConfigSource InitConfig(string[] args)
        {
            Console.WriteLine("Good");
            ArgvConfigSource configSource = new ArgvConfigSource(args);
            configSource.AddSwitch("Startup", "inifile");

            IConfig startupConfig = configSource.Configs["Startup"];
            string iniFilePath = startupConfig.GetString("inifile", "OpenSim.ini");
            Console.WriteLine(iniFilePath);
            IniConfigSource config = new IniConfigSource();
            //check for .INI file (either default or name passed in command line)
            if (! File.Exists(iniFilePath))
            {
                iniFilePath = Path.Combine(Util.configDir(), iniFilePath);
            }

            if (File.Exists(iniFilePath))
            {
                config.Merge(new IniConfigSource(iniFilePath));
                config.Merge(configSource);
            }
            else
            {
                // no default config files, so set default values, and save it
                Console.WriteLine("We didn't find a config!");
                config.Merge(OpenSimBase.DefaultConfig());
                config.Merge(configSource);
            }

            return config;
        }
    }
}
