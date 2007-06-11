/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
