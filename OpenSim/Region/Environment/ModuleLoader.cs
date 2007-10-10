using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Modules;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment
{
    public class ModuleLoader
    {
        public Dictionary<string, Assembly> LoadedAssemblys = new Dictionary<string, Assembly>();

        public List<IRegionModule> LoadedModules = new List<IRegionModule>();
        public Dictionary<string, IRegionModule> LoadedSharedModules = new Dictionary<string, IRegionModule>();
        private readonly LogBase m_log;

        public ModuleLoader(LogBase log)
        {
            m_log = log;
        }

        /// <summary>
        /// Should have a module factory?
        /// </summary>
        /// <param name="scene"></param>
        //public void CreateDefaultModules(Scene scene, string exceptModules)
        //{
        //    IRegionModule module = new XferModule();
        //    InitializeModule(module, scene);

        //    module = new ChatModule();
        //    InitializeModule(module, scene);

        //    module = new AvatarProfilesModule();
        //    InitializeModule(module, scene);

        //    module = new XMLRPCModule();
        //    InitializeModule(module, scene);

        //    module = new WorldCommModule();
        //    InitializeModule(module, scene);

        //    LoadRegionModule("OpenSim.Region.ExtensionsScriptModule.dll", "ExtensionsScriptingModule", scene);

        //    string lslPath = Path.Combine("ScriptEngines", "OpenSim.Region.ScriptEngine.DotNetEngine.dll");
        //    LoadRegionModule(lslPath, "LSLScriptingModule", scene);
        //}

        public void PickupModules(Scene scene)
        {
            string moduleDir = ".";

            DirectoryInfo dir = new DirectoryInfo(moduleDir);

            foreach (FileInfo fileInfo in dir.GetFiles("*.dll"))
            {
                LoadRegionModules(fileInfo.FullName, scene);
            }
        }

        public void LoadDefaultSharedModules(string exceptModules)
        {
            DynamicTextureModule dynamicModule = new DynamicTextureModule();
            LoadedSharedModules.Add(dynamicModule.Name, dynamicModule);
        }

        public void InitialiseSharedModules(Scene scene)
        {
            foreach (IRegionModule module in LoadedSharedModules.Values)
            {
                module.Initialise(scene);
                scene.AddModule(module.Name, module); //should be doing this?
            }
        }

        private void InitializeModule(IRegionModule module, Scene scene)
        {
            module.Initialise(scene);
            scene.AddModule(module.Name, module);
            LoadedModules.Add(module);
        }

        /// <summary>
        ///  Loads/initialises a Module instance that can be used by mutliple Regions
        /// </summary>
        /// <param name="dllName"></param>
        /// <param name="moduleName"></param>
        /// <param name="scene"></param>
        public void LoadSharedModule(string dllName, string moduleName)
        {
            IRegionModule module = LoadModule(dllName, moduleName);
            if (module != null)
            {
                LoadedSharedModules.Add(module.Name, module);
            }
        }

        public void LoadRegionModules(string dllName, Scene scene)
        {
            IRegionModule[] modules = LoadModules(dllName);

            if (modules.Length > 0)
            {
                m_log.Verbose("MODULES", "Found Module Library [{0}]", dllName );
                foreach (IRegionModule module in modules)
                {
                    m_log.Verbose("MODULES", "   [{0}]: Initializing.", module.Name);
                    InitializeModule(module, scene);
                }
            }
        }

        public void LoadRegionModule(string dllName, string moduleName, Scene scene)
        {
            IRegionModule module = LoadModule(dllName, moduleName);
            if (module != null)
            {
                InitializeModule(module, scene);
            }
        }

        /// <summary>
        /// Loads a external Module (if not already loaded) and creates a new instance of it.
        /// </summary>
        /// <param name="dllName"></param>
        /// <param name="moduleName"></param>
        /// <param name="scene"></param>
        public IRegionModule LoadModule(string dllName, string moduleName)
        {
            IRegionModule[] modules = LoadModules(dllName);

            foreach (IRegionModule module in modules)
            {
                if ((module != null) && (module.Name == moduleName))
                {
                    return module;
                }
            }

            return null;
        }

        public IRegionModule[] LoadModules(string dllName)
        {
            List<IRegionModule> modules = new List<IRegionModule>();

            Assembly pluginAssembly;
            if (!LoadedAssemblys.TryGetValue(dllName, out pluginAssembly ))
            {
                try
                {
                    pluginAssembly = Assembly.LoadFrom(dllName);
                    LoadedAssemblys.Add(dllName, pluginAssembly);
                }
                catch( BadImageFormatException e )
                {
                    m_log.Error( "MODULES", "The file [{0}] is not a valid assembly.", e.FileName );
                }
            }


            if (pluginAssembly != null)
            {
                foreach (Type pluginType in pluginAssembly.GetTypes())
                {
                    if (pluginType.IsPublic)
                    {
                        if (!pluginType.IsAbstract)
                        {
                            //if (dllName.Contains("OpenSim.Region.Environment"))
                            //{
                            //    int i = 1;
                            //    i++;
                            //}

                            if( pluginType.GetInterface("IRegionModule") != null )
                            {
                                modules.Add((IRegionModule) Activator.CreateInstance(pluginType));
                            }
                        }
                    }
                }
            }

            return modules.ToArray();
        }

        public void PostInitialise()
        {
            foreach (IRegionModule module in LoadedSharedModules.Values)
            {
                module.PostInitialise();
            }

            foreach (IRegionModule module in LoadedModules)
            {
                module.PostInitialise();
            }
        }

        public void ClearCache()
        {
            LoadedAssemblys.Clear();
        }
    }
}
