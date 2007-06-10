using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.UserServer;
using OpenSim.Framework.Console;

namespace OpenSim
{
    public class Application
    {   
        //could move our main function into OpenSimMain and kill this class
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
            bool gridLocalAsset = false;
            bool useConfigFile = false;
            bool silent = false;
            string configFile = "simconfig.xml";
            
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-sandbox")
                {
                    sandBoxMode = true;
                    startLoginServer = true;
                }
                /*
                if (args[i] == "-loginserver")
                {
                    startLoginServer = true;
                }*/
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
                if (args[i] == "-localasset")
                {
                    gridLocalAsset = true;
                }
                if (args[i] == "-configfile")
                {
                    useConfigFile = true;
                }
                if (args[i] == "-noverbose")
                {
                    silent = true;
                }
                if (args[i] == "-config")
                {
                    try
                    {
                        i++;
                        configFile = args[i];
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("-config: Please specify a config file. (" + e.ToString() + ")");
                    }
                }
            }

            OpenSimMain sim = new OpenSimMain(sandBoxMode, startLoginServer, physicsEngine, useConfigFile, silent, configFile);
           // OpenSimRoot.Instance.Application = sim;
            sim.m_sandbox = sandBoxMode;
            sim.user_accounts = userAccounts;
            sim.gridLocalAsset = gridLocalAsset;
            OpenSim.Region.Avatar.PhysicsEngineFlying = allowFlying;

            sim.StartUp();

            while (true)
            {
                OpenSim.Framework.Console.MainConsole.Instance.MainConsolePrompt();
            }
        }
    }
}
