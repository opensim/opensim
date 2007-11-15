using System;
using System.Collections.Generic;
using System.Text;
using OpenSim;
using OpenSim.Framework.Console;
using OpenSim.Framework;
using OpenSim.Framework.RegionLoader.Filesystem;
using OpenSim.Framework.RegionLoader.Web;
using Mono.Addins;
using Mono.Addins.Description;
using Nini;
using Nini.Config;

[assembly:Addin]
[assembly:AddinDependency ("OpenSim", "0.4")]

namespace OpenSim.ApplicationPlugins.LoadRegions
{
    [Extension("/OpenSim/Startup")]
    public class LoadRegionsPlugin : IApplicationPlugin
    {
        public void Initialise(OpenSimMain openSim)
        {
            System.Console.WriteLine("Load Regions addin being initialised");

            IRegionLoader regionLoader;
            if (openSim.ConfigSource.Configs["Startup"].GetString("region_info_source", "filesystem") == "filesystem")
            {
                MainLog.Instance.Notice("Loading Region Info from filesystem");
                regionLoader = new RegionLoaderFileSystem();
            }
            else
            {
                MainLog.Instance.Notice("Loading Region Info from web");
                regionLoader = new RegionLoaderWebServer();
            }

            regionLoader.SetIniConfigSource(openSim.ConfigSource);
            RegionInfo[] regionsToLoad = regionLoader.LoadRegions();

            openSim.ModuleLoader.LoadDefaultSharedModules();

            for (int i = 0; i < regionsToLoad.Length; i++)
            {
                MainLog.Instance.Debug("Creating Region: " + regionsToLoad[i].RegionName);
                openSim.CreateRegion(regionsToLoad[i]);
            }

            openSim.ModuleLoader.PostInitialise();
            openSim.ModuleLoader.ClearCache();
        }

        public void Close()
        {

        }
    }
}