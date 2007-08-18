using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Threading;
using System.Runtime.Remoting;
using System.IO;

namespace OpenSim.Region.ScriptEngine.DotNetEngine
{
    public class AppDomainManager
    {
        private int MaxScriptsPerAppDomain = 1;
        /// <summary>
        /// List of all AppDomains
        /// </summary>
        private List<AppDomainStructure> AppDomains = new List<AppDomainStructure>();
        private struct AppDomainStructure
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
        private AppDomainStructure CurrentAD;
        private object GetLock = new object(); // Mutex
        private object FreeLock = new object(); // Mutex

                private ScriptEngine m_scriptEngine;
        public AppDomainManager(ScriptEngine scriptEngine)
        {
            m_scriptEngine = scriptEngine;
        }

        /// <summary>
        /// Find a free AppDomain, creating one if necessary
        /// </summary>
        /// <returns>Free AppDomain</returns>
        internal AppDomain GetFreeAppDomain()
        {
            FreeAppDomains();
            lock(GetLock) {
                // Current full?
                if (CurrentAD.ScriptsLoaded >= MaxScriptsPerAppDomain)
                {
                    AppDomains.Add(CurrentAD);
                    CurrentAD = new AppDomainStructure();   
                }
                // No current
                if (CurrentAD.CurrentAppDomain == null)
                {
                    // Create a new current AppDomain
                    CurrentAD = new AppDomainStructure();
                    CurrentAD.ScriptsWaitingUnload = 0; // to avoid compile warning for not in use
                    CurrentAD.CurrentAppDomain = PrepareNewAppDomain();
                    

                }

                // Increase number of scripts loaded
                CurrentAD.ScriptsLoaded++;
                // Return AppDomain
                return CurrentAD.CurrentAppDomain;
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
            // TODO: Currently security and configuration match current appdomain

            // Construct and initialize settings for a second AppDomain.
            AppDomainSetup ads = new AppDomainSetup();
            ads.ApplicationBase = AppDomain.CurrentDomain.BaseDirectory;
                //Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ScriptEngines");
            //ads.ApplicationName = "DotNetScriptEngine";
            //ads.DynamicBase = ads.ApplicationBase;
            
            //Console.WriteLine("AppDomain BaseDirectory: " + ads.ApplicationBase);
            ads.DisallowBindingRedirects = false;
            ads.DisallowCodeDownload = true;
            ads.ShadowCopyFiles = "true";
            
            ads.ConfigurationFile =
                AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;

            AppDomain AD = AppDomain.CreateDomain("ScriptAppDomain_" + AppDomainNameCount, null, ads);
            //foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            //{
            //    //Console.WriteLine("Loading: " + a.GetName(true));
            //    try
            //    {
            //        //AD.Load(a.GetName(true));
                    
            //    }
            //    catch (Exception e)
            //    {
            //        //Console.WriteLine("FAILED load");
            //    }
                
            //}

            //Console.WriteLine("Assembly file: " + this.GetType().Assembly.CodeBase);
            //Console.WriteLine("Assembly name: " + this.GetType().ToString());
            //AD.CreateInstanceFrom(this.GetType().Assembly.CodeBase, "OpenSim.Region.ScriptEngine.DotNetEngine.ScriptEngine");

            //AD.Load(this.GetType().Assembly.CodeBase);

            Console.WriteLine("Done preparing new AppDomain.");
            return AD;

        }

        /// <summary>
        /// Unload appdomains that are full and have only dead scripts
        /// </summary>
        private void FreeAppDomains()
        {
            lock (FreeLock)
            {
                foreach (AppDomainStructure ads in new System.Collections.ArrayList(AppDomains))
                {
                    if (ads.CurrentAppDomain != CurrentAD.CurrentAppDomain)
                    {
                        // Not current AppDomain
                        if (ads.ScriptsLoaded == ads.ScriptsWaitingUnload)
                        {
                            AppDomains.Remove(ads);
                            AppDomain.Unload(ads.CurrentAppDomain);
                        }
                    }
                } // foreach
            } // lock
        }

        /// <summary>
        /// Increase "dead script" counter for an AppDomain
        /// </summary>
        /// <param name="ad"></param>
        [Obsolete("Needs optimizing!!!")]
        public void StopScriptInAppDomain(AppDomain ad)
        {
            lock (FreeLock)
            {
                if (CurrentAD.CurrentAppDomain == ad)
                {
                    CurrentAD.ScriptsWaitingUnload++;
                    return;
                }

                foreach (AppDomainStructure ads in new System.Collections.ArrayList(AppDomains))
                {
                    if (ads.CurrentAppDomain == ad)
                    {
                        AppDomainStructure ads2 = ads;
                        ads2.ScriptsWaitingUnload++;
                        AppDomains.Remove(ads);
                        AppDomains.Add(ads2);
                        return;
                    }
                } // foreach
            } // lock
        }
    }
}
