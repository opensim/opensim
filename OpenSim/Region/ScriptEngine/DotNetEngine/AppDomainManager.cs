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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;

namespace OpenSim.Region.ScriptEngine.DotNetEngine
{
    public class AppDomainManager : iScriptEngineFunctionModule
    {
        //
        // This class does AppDomain handling and loading/unloading of scripts in it.
        // It is instanced in "ScriptEngine" and controlled from "ScriptManager"
        //
        // 1. Create a new AppDomain if old one is full (or doesn't exist)
        // 2. Load scripts into AppDomain
        // 3. Unload scripts from AppDomain (stopping them and marking them as inactive)
        // 4. Unload AppDomain completely when all scripts in it has stopped
        //

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

        private ScriptEngine m_scriptEngine;
        //public AppDomainManager(ScriptEngine scriptEngine)
        public AppDomainManager(ScriptEngine scriptEngine)
        {
            m_scriptEngine = scriptEngine;
            ReadConfig();
        }

        public void ReadConfig()
        {
            maxScriptsPerAppDomain = m_scriptEngine.ScriptConfigSource.GetInt("ScriptsPerAppDomain", 1);
        }

        /// <summary>
        /// Find a free AppDomain, creating one if necessary
        /// </summary>
        /// <returns>Free AppDomain</returns>
        private AppDomainStructure GetFreeAppDomain()
        {
            //            Console.WriteLine("Finding free AppDomain");
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

                //                Console.WriteLine("Scripts loaded in this Appdomain: " + currentAD.ScriptsLoaded);
                return currentAD;
            }
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
            ads.DisallowBindingRedirects = true;
            ads.DisallowCodeDownload = true;
            ads.LoaderOptimization = LoaderOptimization.MultiDomainHost;
            ads.ShadowCopyFiles = "false"; // Disable shadowing
            ads.ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;


            AppDomain AD = AppDomain.CreateDomain("ScriptAppDomain_" + AppDomainNameCount, null, ads);
            m_scriptEngine.Log.Info("[" + m_scriptEngine.ScriptEngineName + "]: AppDomain Loading: " +
                                       AssemblyName.GetAssemblyName("OpenSim.Region.ScriptEngine.Shared.dll").ToString());
            AD.Load(AssemblyName.GetAssemblyName("OpenSim.Region.ScriptEngine.Shared.dll"));

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
                            // Remove from internal list
                            appDomains.Remove(ads);
//#if DEBUG
                            //Console.WriteLine("Found empty AppDomain, unloading");
                            //long m = GC.GetTotalMemory(true); // This force a garbage collect that freezes some windows plateforms
//#endif
                            // Unload
                            AppDomain.Unload(ads.CurrentAppDomain);
//#if DEBUG
                            //m_scriptEngine.Log.Info("[" + m_scriptEngine.ScriptEngineName + "]: AppDomain unload freed " + (m - GC.GetTotalMemory(true)) + " bytes of memory");
//#endif
                        }
                    }
                }
            }
        }

        public IScript LoadScript(string FileName, out AppDomain ad)
        {
            // Find next available AppDomain to put it in
            AppDomainStructure FreeAppDomain = GetFreeAppDomain();

#if DEBUG
            m_scriptEngine.Log.Info("[" + m_scriptEngine.ScriptEngineName + "]: Loading into AppDomain: " + FileName);
#endif
            IScript mbrt =
                (IScript)
                FreeAppDomain.CurrentAppDomain.CreateInstanceFromAndUnwrap(FileName, "SecondLife.Script");
            //Console.WriteLine("ScriptEngine AppDomainManager: is proxy={0}", RemotingServices.IsTransparentProxy(mbrt));
            FreeAppDomain.ScriptsLoaded++;
            ad = FreeAppDomain.CurrentAppDomain;

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
#if DEBUG
                m_scriptEngine.Log.Info("[" + m_scriptEngine.ScriptEngineName + "]: Stopping script in AppDomain");
#endif
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
                }
            }

            UnloadAppDomains(); // Outsite lock, has its own GetLock
        }

        /// <summary>
        /// If set to true then threads and stuff should try to make a graceful exit
        /// </summary>
        public bool PleaseShutdown
        {
            get { return _PleaseShutdown; }
            set { _PleaseShutdown = value; }
        }
        private bool _PleaseShutdown = false;
    }
}
