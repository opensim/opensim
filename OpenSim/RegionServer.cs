using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.UserServer;
using OpenSim.Framework.Console;

namespace OpenSim
{
    public class RegionServer
    {        
        [STAThread]
        public static void Main(string[] args)
        {
            Console.WriteLine("OpenSim " + VersionInfo.Version + "\n");
            Console.WriteLine("Starting...\n");
            
            bool sandBoxMode = false;
            bool startLoginServer = false;
            string physicsEngine = "basicphysics";
            bool allowFlying = false;
            bool userAccounts = false;
            
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-sandbox")
                {
                    sandBoxMode = true;
                }

                if (args[i] == "-loginserver")
                {
                    startLoginServer = true;
                }
                if (args[i] == "-accounts")
                {
                    userAccounts = true;
                }
                if (args[i] == "-realphysx")
                {
                    physicsEngine = "RealPhysX";
                    allowFlying = true;
                }
                if (args[i] == "-ode")
                {
                    physicsEngine = "OpenDynamicsEngine";
                    allowFlying = true;
                }
            }

            OpenSimMain sim = new OpenSimMain( sandBoxMode, startLoginServer, physicsEngine );
           // OpenSimRoot.Instance.Application = sim;
            sim.m_sandbox = sandBoxMode;
            sim.user_accounts = userAccounts;
            OpenSim.world.Avatar.PhysicsEngineFlying = allowFlying;

            sim.StartUp();

            while (true)
            {
                OpenSim.Framework.Console.MainConsole.Instance.MainConsolePrompt();
            }
        }
    }
}
