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

namespace OpenSim.GridLaunch
{
    internal class Settings
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private Dictionary<string, string> Config = new Dictionary<string, string>();
        public Dictionary<string, bool> Components = new Dictionary<string, bool>();

        public static string[] defaultComponents = new string[]
                {
                    "OpenSim.Grid.UserServer.exe",
                    "OpenSim.Grid.GridServer.exe",
                    "OpenSim.Grid.AssetServer.exe",
                    "OpenSim.Grid.InventoryServer.exe",
                    "OpenSim.Grid.MessagingServer.exe",
                    "OpenSim.32BitLaunch.exe"
                };


        private static readonly char[] confSplit = new char[] { '=' };
        private static readonly char[] comaSplit = new char[] { ',' };
        private static readonly char[] colonSplit = new char[] { ';' };

        private string configFile = "";

        public Settings()
        {
        }
        public Settings(string ConfigFile)
        {
            LoadConfig(ConfigFile);
        }


        public void LoadConfig(string ConfigFile)
        {
            configFile = ConfigFile;
            m_log.Info("Reading config file: " + ConfigFile);
            try
            {
                // Read config file
                foreach (string line in System.IO.File.ReadAllLines(ConfigFile))
                {
                    string[] s = line.Split(confSplit, 2);
                    if (s.Length >= 2)
                        Config.Add(s[0], s[1]);
                }

                // Process Components section
                string cmp = Config["Components"];
                Config.Remove("Components");
                foreach (string c in cmp.Split(comaSplit))
                {
                    string[] cs = c.Split(colonSplit);
                    if (cs.Length >= 2)
                    {
                        bool status = false;
                        bool.TryParse(cs[1], out status);
                        Components.Add(cs[0], status);
                    }
                }
            }
            catch (Exception ex)
            {
                m_log.Error("Exception reading config file: " + ex.ToString());
            }
            // No components? Add default components
            if (Components.Count == 0)
                foreach (string c in defaultComponents)
                {
                    Components.Add(c, true);
                }
        }

        public void SaveConfig(string ConfigFile)
        {
            configFile = ConfigFile;
            SaveConfig();
        }

        public void SaveConfig()
        {
            m_log.Info("Writing config file: " + configFile);
            try
            {
                System.IO.File.WriteAllText(configFile, ToString());
            }
            catch (Exception ex)
            {
                m_log.Error("Exception writing config file: " + ex.ToString());
            }

        }

        public new string ToString()
        {
            StringBuilder ret = new StringBuilder();

            Dictionary<string, string> config = new Dictionary<string, string>(Config);

            // Add Components key
            StringBuilder _Components = new StringBuilder();
            foreach (string c in Components.Keys)
            {
                if (_Components.Length > 0)
                    _Components.Append(",");
                _Components.Append(c + ";" + Components[c].ToString());
            }
            config["Components"] = _Components.ToString();

            // Make return string
            foreach (string key in config.Keys)
            {
                ret.AppendLine(key + "=" + config[key]);
            }

            // Return it
            return ret.ToString();
        }

        public string this[string Key]
        {
            get
            {
                if (Config.ContainsKey(Key))
                    return Config[Key];
                return "";
            }
            set { Config[Key] = value; }
        }

        public void ParseCommandArguments(string[] args)
        {
            string key = null;
            foreach (string a in args)
            {
                if (a.StartsWith("--"))
                    key = a.Remove(0, 2);
                else
                {
                    if (key != null)
                        Config[key] = a;
                    key = null;
                }
            }

        }
    }
}
