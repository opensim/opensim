using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Console;

namespace OpenSim.Grid.GridServer
{
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();

            OpenGrid_Main app = new OpenGrid_Main();

            if (args.Length > 0 && args[0] == "-setuponly")
            {
                app.Config();
            }
            else
            {
                app.Startup();
                app.Work();
            }
        }
    }
}
