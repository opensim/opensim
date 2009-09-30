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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.Text;
using log4net;
using OpenSim.ScriptEngine.Shared;
using IScript=OpenSim.Region.ScriptEngine.Shared.ScriptBase.IScript;

namespace OpenSim.ScriptEngine.Components.DotNetEngine.Scheduler
{
    public class ScriptLoader : IScriptLoader
    {
        //
        // This class does AppDomain handling and loading/unloading of
        // scripts in it. It is instanced in "ScriptEngine" and controlled
        // from "ScriptManager"
        //
        // 1. Create a new AppDomain if old one is full (or doesn't exist)
        // 2. Load scripts into AppDomain
        // 3. Unload scripts from AppDomain (stopping them and marking
        //    them as inactive)
        // 4. Unload AppDomain completely when all scripts in it has stopped
        //

        public string Name { get { return "SECS.DotNetEngine.Scheduler.ScriptLoader"; } }
        private int maxScriptsPerAppDomain = 10;

        // Internal list of all AppDomains
        private List<AppDomainStructure> appDomains =
            new List<AppDomainStructure>();
        private Dictionary<string, AppDomainStructure> AppDomainFiles = new Dictionary<string, AppDomainStructure>();
        public readonly string[] AssembliesInAppDomain = new string[] { "OpenSim.ScriptEngine.Shared.Script.dll", "OpenSim.Region.ScriptEngine.Shared.dll" };

        internal static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // Structure to keep track of data around AppDomain
        private class AppDomainStructure
        {
            public AppDomain CurrentAppDomain;      // The AppDomain itself
            public int ScriptsLoaded;               // Number of scripts loaded into AppDomain
            public int ScriptsWaitingUnload;        // Number of dead scripts
        }

        // Current AppDomain
        private AppDomainStructure currentAD;

        private object getLock = new object(); // Mutex
        private object freeLock = new object(); // Mutex

        // Find a free AppDomain, creating one if necessary
        private AppDomainStructure GetFreeAppDomain()
        {
            lock (getLock)
            {
                // Current full?
                if (currentAD != null &&
                    currentAD.ScriptsLoaded >= maxScriptsPerAppDomain)
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

                return currentAD;
            }
        }

        private int AppDomainNameCount;
        public ScriptAssemblies.IScript LoadScript(ScriptStructure script)
        {
            // Find next available AppDomain to put it in
            AppDomainStructure FreeAppDomain;

            // If we already have loaded file, then reuse that AppDomains
            if (AppDomainFiles.ContainsKey(script.AssemblyFileName))
                FreeAppDomain = AppDomainFiles[script.AssemblyFileName];
            else
                FreeAppDomain = GetFreeAppDomain();

            // Set script object AppDomain
            script.AppDomain = FreeAppDomain.CurrentAppDomain;

            // Create instance of script
            ScriptAssemblies.IScript mbrt = (ScriptAssemblies.IScript)
                FreeAppDomain.CurrentAppDomain.CreateInstanceFromAndUnwrap(
                               script.AssemblyFileName, "ScriptAssemblies.Script");
            //, true, BindingFlags.CreateInstance, null);
            FreeAppDomain.ScriptsLoaded++;

            return mbrt;
        }

        // Create and prepare a new AppDomain for scripts
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
            ads.ConfigurationFile =
                AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;

            AppDomain AD = AppDomain.CreateDomain("ScriptAppDomain_" +
                                                  AppDomainNameCount, null, ads);

            foreach (string file in AssembliesInAppDomain)
            {
                m_log.InfoFormat("[{0}] AppDomain Loading: \"{1}\"->\"{2}\".", Name, file,
                                 AssemblyName.GetAssemblyName(file).ToString());
                AD.Load(AssemblyName.GetAssemblyName(file));
            }

            // Return the new AppDomain
            return AD;
        }

        // Unload appdomains that are full and have only dead scripts
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

                            // Unload
                            AppDomain.Unload(ads.CurrentAppDomain);
                        }
                    }
                }
            }
        }

        // Increase "dead script" counter for an AppDomain
        public void StopScript(AppDomain ad)
        {
            lock (freeLock)
            {
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


            
    }
}