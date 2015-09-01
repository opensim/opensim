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

using Nini.Config;
using System;

namespace OpenSim.Tools.Configger
{
    public class Configger
    {
        public static int Main(string[] args)
        {
            ArgvConfigSource argvConfig = new ArgvConfigSource(args);

            argvConfig.AddSwitch("Startup", "format", "f");
            argvConfig.AddSwitch("Startup", "inifile");

            IConfig startupConfig = argvConfig.Configs["Startup"];

            string format = startupConfig.GetString("format", "ini");

            ConfigurationLoader loader = new ConfigurationLoader();
            IConfigSource s = loader.LoadConfigSettings(startupConfig);

            if (format == "mysql")
            {
                foreach (IConfig c in s.Configs)
                {
                    foreach (string k in c.GetKeys())
                    {
                        string v = c.GetString(k);

                        if (k.StartsWith("Include-"))
                            continue;
                        Console.WriteLine("insert ignore into config (section, name, value) values ('{0}', '{1}', '{2}');", c.Name, k, v);
                    }
                }
            }
            else if (format == "xml")
            {
                Console.WriteLine("<Nini>");

                foreach (IConfig c in s.Configs)
                {
                    int count = 0;

                    foreach (string k in c.GetKeys())
                    {
                        if (k.StartsWith("Include-"))
                            continue;

                        count++;
                    }

                    if (count > 0)
                    {
                        Console.WriteLine("<Section Name=\"{0}\">", c.Name);

                        foreach (string k in c.GetKeys())
                        {
                            string v = c.GetString(k);

                            if (k.StartsWith("Include-"))
                                continue;
                            Console.WriteLine("    <Key Name=\"{0}\" Value=\"{1}\" />", k, v);
                        }

                        Console.WriteLine("</Section>");
                    }
                }
                Console.WriteLine("</Nini>");
            }
            else if (format == "ini")
            {
                foreach (IConfig c in s.Configs)
                {
                    int count = 0;

                    foreach (string k in c.GetKeys())
                    {
                        if (k.StartsWith("Include-"))
                            continue;

                        count++;
                    }

                    if (count > 0)
                    {
                        Console.WriteLine("[{0}]", c.Name);

                        foreach (string k in c.GetKeys())
                        {
                            string v = c.GetString(k);

                            if (k.StartsWith("Include-"))
                                continue;
                            Console.WriteLine("{0} = \"{1}\"", k, v);
                        }

                        Console.WriteLine();
                    }
                }
            }
            else
            {
                Console.WriteLine("Error: unknown format: {0}", format);
            }

            return 0;
        }
    }
}
