using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.UserServer;

namespace OpenSim
{
    public class RegionServer : OpenSimMain
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Console.WriteLine("OpenSim " + VersionInfo.Version + "\n");
            Console.WriteLine("Starting...\n");
            OpenSim.Framework.Console.MainConsole.Instance = new SimConsole(OpenSim.Framework.Console.ConsoleBase.ConsoleType.Local, "", 0);

            //OpenSimRoot.instance = new OpenSimRoot();
            OpenSimMain sim = new OpenSimMain();
            OpenSimRoot.Instance.Application = sim;

            sim.sandbox = false;
            sim.loginserver = false;
            sim._physicsEngine = "basicphysics";

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-sandbox")
                {
                    sim.sandbox = true;
                    OpenSimRoot.Instance.Sandbox = true;
                }

                if (args[i] == "-loginserver")
                {
                    sim.loginserver = true;
                }
                if (args[i] == "-realphysx")
                {
                    sim._physicsEngine = "RealPhysX";
                    OpenSim.world.Avatar.PhysicsEngineFlying = true;
                }
                if (args[i] == "-ode")
		{
		    sim._physicsEngine = "OpenDynamicsEngine";
		    OpenSim.world.Avatar.PhysicsEngineFlying = true;
		}
	    }


            OpenSimRoot.Instance.GridServers = new Grid();
            if (sim.sandbox)
            {
                OpenSimRoot.Instance.GridServers.AssetDll = "OpenSim.GridInterfaces.Local.dll";
                OpenSimRoot.Instance.GridServers.GridDll = "OpenSim.GridInterfaces.Local.dll";
                OpenSimRoot.Instance.GridServers.Initialise();
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Starting in Sandbox mode");
            }
            else
            {
                OpenSimRoot.Instance.GridServers.AssetDll = "OpenSim.GridInterfaces.Remote.dll";
                OpenSimRoot.Instance.GridServers.GridDll = "OpenSim.GridInterfaces.Remote.dll";
                OpenSimRoot.Instance.GridServers.Initialise();
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Starting in Grid mode");
            }

            if (sim.loginserver && sim.sandbox)
            {
                LoginServer loginServer = new LoginServer(OpenSimRoot.Instance.GridServers.GridServer);
                loginServer.Startup();
            }

            OpenSimRoot.Instance.StartUp();

            while (true)
            {
                OpenSim.Framework.Console.MainConsole.Instance.MainConsolePrompt();
            }
        }
    }
}
