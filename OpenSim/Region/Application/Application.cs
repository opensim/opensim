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
using System.Net;
using log4net.Config;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;

namespace OpenSim
{
    public class Application
    {
        public static string iniFilePath = "";

        //could move our main function into OpenSimMain and kill this class
        public static void Main(string[] args)
        {
            // First line
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            XmlConfigurator.Configure();

            Console.Write("Performing compatibility checks... ");
            string supported = String.Empty;
            if (Util.IsEnvironmentSupported(ref supported))
            {
                Console.WriteLine(" Environment is compatible.\n");
            }
            else
            {
                Console.WriteLine(" Environment is unsupported (" + supported + ")\n");
            }

            Culture.SetCurrentCulture();

            ArgvConfigSource configSource = new ArgvConfigSource(args);

            configSource.Alias.AddAlias("On", true);
            configSource.Alias.AddAlias("Off", false);
            configSource.Alias.AddAlias("True", true);
            configSource.Alias.AddAlias("False", false);

            configSource.AddSwitch("Startup", "background");
            configSource.AddSwitch("Startup", "inifile");
            configSource.AddSwitch("Startup", "gridmode");
            configSource.AddSwitch("Startup", "physics");
            configSource.AddSwitch("Startup", "useexecutepath");

            configSource.AddConfig("StandAlone");
            configSource.AddConfig("Network");

            bool background = configSource.Configs["Startup"].GetBoolean("background", false);

            if (background)
            {
                OpenSimBase sim = new OpenSimBackground(configSource);
                sim.Startup();
            }
            else
            {
                OpenSimBase sim = new OpenSim(configSource);
                sim.Startup();

                while (true)
                {
                    MainConsole.Instance.Prompt();
                }
            }
        }

        private static bool _IsHandlingException = false; // Make sure we don't go recursive on ourself

        /// <summary>
        /// Global exception handler -- all unhandlet exceptions end up here :)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (_IsHandlingException)
                return;
            _IsHandlingException = true;
            // TODO: Add config option to allow users to turn off error reporting
            // TODO: Post error report (disabled for now)

            string msg = String.Empty;
            msg += "\r\n";
            msg += "APPLICATION EXCEPTION DETECTED: " + e.ToString() + "\r\n";
            msg += "\r\n";

            msg += "Exception: " + e.ExceptionObject.ToString() + "\r\n";
            Exception ex = (Exception)e.ExceptionObject;
            if (ex.InnerException != null)
            {
                msg += "InnerException: " + ex.InnerException.ToString() + "\r\n";
            }

            msg += "\r\n";
            msg += "Application is terminating: " + e.IsTerminating.ToString() + "\r\n";

            // Do we not always want to see exception messages?
//            if (e.IsTerminating)
                MainConsole.Instance.Error("[APPLICATION]: " + msg);

            // Try to post errormessage to an URL
            try
            {
                // DISABLED UNTIL WE CAN DISCUSS IF THIS IS MORALLY RIGHT OR NOT
                // Note! Needs reference to System.Web
                //System.Net.WebClient wc = new WebClient();
                //wc.DownloadData("http://www.opensimulator.org/ErrorReport.php?Msg=" +
                //                System.Web.HttpUtility.UrlEncode(msg));
                //wc.Dispose();
            }
            catch (WebException)
            {
                // Ignore
            }

            _IsHandlingException=false;
        }
    }
}
