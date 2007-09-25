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
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Scenes;
using Nini.Config;
using System.Threading;
using OpenSim.Framework;

namespace OpenSim
{
    public class Application
    {   
        //could move our main function into OpenSimMain and kill this class
        [STAThread]
        public static void Main(string[] args)
        {

            Console.WriteLine("OpenSim " + VersionInfo.Version + "\n");
            

            Console.Write("Performing compatibility checks... ");
            string supported = "";
            if (OpenSim.Framework.Utilities.Util.IsEnvironmentSupported(ref supported))
            {
                Console.WriteLine(" Environment is compatible.\n");
            }
            else
            {
                Console.WriteLine(" Environment is unsupported (" + supported + ")\n");
            }
            
            Console.WriteLine("Starting...\n");

           Culture.SetCurrentCulture();

            ArgvConfigSource configSource = new ArgvConfigSource(args);

            configSource.AddSwitch("Startup", "inifile");
            configSource.AddSwitch("Startup", "gridmode");
            configSource.AddSwitch("Startup", "physics");
            configSource.AddSwitch("Startup", "noverbose");

            OpenSimMain sim = new OpenSimMain(configSource);

            sim.StartUp();

            while (true)
            {
                MainLog.Instance.MainLogPrompt();
            }
        }
    }
}
