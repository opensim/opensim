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
        private int MaxScriptsPerAppDomain = 10;
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
        private AppDomainStructure CurrentAD;
        private object GetLock = new object();

                private ScriptEngine m_scriptEngine;
        public AppDomainManager(ScriptEngine scriptEngine)
        {
            m_scriptEngine = scriptEngine;
        }

        internal AppDomain GetFreeAppDomain()
        {
            lock(GetLock) {
                // No current or current full?
                if (CurrentAD.CurrentAppDomain == null || CurrentAD.ScriptsLoaded >= MaxScriptsPerAppDomain)
                {
                    // Create a new current AppDomain
                    CurrentAD = new AppDomainStructure();
                    CurrentAD.ScriptsWaitingUnload = 0; // to avoid compile warning for not in use
                    CurrentAD.CurrentAppDomain = PrepareNewAppDomain();
                    AppDomains.Add(CurrentAD);

                }

                // Increase number of scripts loaded
                CurrentAD.ScriptsLoaded++;
                // Return AppDomain
                return CurrentAD.CurrentAppDomain;
            } // lock
        }

        private int AppDomainNameCount;
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
            
            Console.WriteLine("AppDomain BaseDirectory: " + ads.ApplicationBase);
            ads.DisallowBindingRedirects = false;
            ads.DisallowCodeDownload = true;
            ads.ShadowCopyFiles = "true";
            
            ads.ConfigurationFile =
                AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;

            AppDomain AD = AppDomain.CreateDomain("ScriptAppDomain_" + AppDomainNameCount, null, ads);
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                //Console.WriteLine("Loading: " + a.GetName(true));
                try
                {
                    AD.Load(a.GetName(true));
                    
                }
                catch (Exception e)
                {
                    //Console.WriteLine("FAILED load");
                }
                
            }

            Console.WriteLine("Assembly file: " + this.GetType().Assembly.CodeBase);
            Console.WriteLine("Assembly name: " + this.GetType().ToString());
            //AD.CreateInstanceFrom(this.GetType().Assembly.CodeBase, "OpenSim.Region.ScriptEngine.DotNetEngine.ScriptEngine");

            //AD.Load(this.GetType().Assembly.CodeBase);

            Console.WriteLine("Done preparing new appdomain.");
            return AD;

        }

        public class NOOP : MarshalByRefType
        {
            public NOOP() {
            }
        }
    }
}
