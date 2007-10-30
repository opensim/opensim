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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using OpenSim.Grid.ScriptEngine.DotNetEngine.Compiler.LSL;

namespace OpenSim.Grid.ScriptEngine.DotNetEngine
{
    public class AppDomainManager
    {
        private int maxScriptsPerAppDomain = 1;

        /// <summary>
        /// Internal list of all AppDomains
        /// </summary>
        private List<AppDomainStructure> appDomains = new List<AppDomainStructure>();

        /// <summary>
        /// Structure to keep track of data around AppDomain
        /// </summary>
        private class AppDomainStructure
        {
            /// <summary>
            /// The AppDomain itself
            /// </summary>
            public AppDomain CurrentAppDomain;

            /// <summary>
            /// Number of scripts loaded into AppDomain
            /// </summary>
            public int ScriptsLoaded;

            /// <summary>
            /// Number of dead scripts
            /// </summary>
            public int ScriptsWaitingUnload;
        }

        /// <summary>
        /// Current AppDomain
        /// </summary>
        private AppDomainStructure currentAD;

        private object getLock = new object(); // Mutex
        private object freeLock = new object(); // Mutex

        //private ScriptEngine m_scriptEngine;
        //public AppDomainManager(ScriptEngine scriptEngine)
        public AppDomainManager()
        {
            //m_scriptEngine = scriptEngine;
        }

        /// <summary>
        /// Find a free AppDomain, creating one if necessary
        /// </summary>
        /// <returns>Free AppDomain</returns>
        private AppDomainStructure GetFreeAppDomain()
        {
            Console.WriteLine("Finding free AppDomain");
            lock (getLock)
            {
                // Current full?
                if (currentAD != null && currentAD.ScriptsLoaded >= maxScriptsPerAppDomain)
                {
                    // Add it to AppDomains list and empty current
                    appDomains.Add(currentAD);
                    currentAD = null;
                }
                // No current
                if (currentAD == null)
                {
                    // Create a new current AppDomain
                    currentAD = new AppDomainStructure();
                    currentAD.CurrentAppDomain = PrepareNewAppDomain();
                }

                Console.WriteLine("Scripts loaded in this Appdomain: " + currentAD.ScriptsLoaded);
                return currentAD;
            } // lock
        }

        private int AppDomainNameCount;

        /// <summary>
        /// Create and prepare a new AppDomain for scripts
        /// </summary>
        /// <returns>The new AppDomain</returns>
        private AppDomain PrepareNewAppDomain()
        {
            // Create and prepare a new AppDomain
            AppDomainNameCount++;
            // TODO: Currently security match current appdomain

            // Construct and initialize settings for a second AppDomain.
            AppDomainSetup ads = new AppDomainSetup();
            ads.ApplicationBase = AppDomain.CurrentDomain.BaseDirectory;
            ads.DisallowBindingRedirects = false;
            ads.DisallowCodeDownload = true;
            ads.LoaderOptimization = LoaderOptimization.MultiDomain; // Sounds good ;)
            ads.ShadowCopyFiles = "true"; // Enabled shadowing
            ads.ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;

            AppDomain AD = AppDomain.CreateDomain("ScriptAppDomain_" + AppDomainNameCount, null, ads);
            Console.WriteLine("Loading: " +
                              AssemblyName.GetAssemblyName("OpenSim.Region.ScriptEngine.Common.dll").ToString());
            AD.Load(AssemblyName.GetAssemblyName("OpenSim.Region.ScriptEngine.Common.dll"));

            // Return the new AppDomain
            return AD;
        }

        /// <summary>
        /// Unload appdomains that are full and have only dead scripts
        /// </summary>
        private void UnloadAppDomains()
        {
            lock (freeLock)
            {
                // Go through all
                foreach (AppDomainStructure ads in new ArrayList(appDomains))
                {
                    // Don't process current AppDomain
                    if (ads.CurrentAppDomain != currentAD.CurrentAppDomain)
                    {
                        // Not current AppDomain
                        // Is number of unloaded bigger or equal to number of loaded?
                        if (ads.ScriptsLoaded <= ads.ScriptsWaitingUnload)
                        {
                            Console.WriteLine("Found empty AppDomain, unloading");
                            // Remove from internal list
                            appDomains.Remove(ads);
#if DEBUG
                            long m = GC.GetTotalMemory(true);
#endif
                            // Unload
                            AppDomain.Unload(ads.CurrentAppDomain);
#if DEBUG
                            Console.WriteLine("AppDomain unload freed " + (m - GC.GetTotalMemory(true)) +
                                              " bytes of memory");
#endif
                        }
                    }
                } // foreach
            } // lock
        }


        public LSL_BaseClass LoadScript(string FileName)
        {
            // Find next available AppDomain to put it in
            AppDomainStructure FreeAppDomain = GetFreeAppDomain();

            Console.WriteLine("Loading into AppDomain: " + FileName);
            LSL_BaseClass mbrt =
                (LSL_BaseClass)
                FreeAppDomain.CurrentAppDomain.CreateInstanceFromAndUnwrap(FileName, "SecondLife.Script");
            //Console.WriteLine("ScriptEngine AppDomainManager: is proxy={0}", RemotingServices.IsTransparentProxy(mbrt));
            FreeAppDomain.ScriptsLoaded++;

            return mbrt;
        }


        /// <summary>
        /// Increase "dead script" counter for an AppDomain
        /// </summary>
        /// <param name="ad"></param>
        //[Obsolete("Needs fixing, needs a real purpose in life!!!")]
        public void StopScript(AppDomain ad)
        {
            lock (freeLock)
            {
                Console.WriteLine("Stopping script in AppDomain");
                // Check if it is current AppDomain
                if (currentAD.CurrentAppDomain == ad)
                {
                    // Yes - increase
                    currentAD.ScriptsWaitingUnload++;
                    return;
                }

                // Lopp through all AppDomains
                foreach (AppDomainStructure ads in new ArrayList(appDomains))
                {
                    if (ads.CurrentAppDomain == ad)
                    {
                        // Found it
                        ads.ScriptsWaitingUnload++;
                        break;
                    }
                } // foreach
            } // lock

            UnloadAppDomains(); // Outsite lock, has its own GetLock
        }
    }
}