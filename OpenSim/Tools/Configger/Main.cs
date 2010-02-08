using Nini.Config;
using System;

namespace Careminster
{
    public class Configger
    {
        public static int Main(string[] args)
        {
            ArgvConfigSource argvConfig = new ArgvConfigSource(args);
            argvConfig.AddSwitch("Startup", "format", "f");

            IConfig startupConfig = argvConfig.Configs["Startup"];

            string format = startupConfig.GetString("format", "ini");

            ConfigurationLoader loader = new ConfigurationLoader();

            IConfigSource s = loader.LoadConfigSettings();

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
