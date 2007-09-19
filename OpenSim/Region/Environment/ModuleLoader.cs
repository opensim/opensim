using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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

        public ModuleLoader()
        {
        }

        /// <summary>
        /// Should have a module factory?
        /// </summary>
        /// <param name="scene"></param>
        public void CreateDefaultModules(Scene scene, string exceptModules)
        {
            IRegionModule module = new XferModule();
            InitialiseModule(module, scene);

            module = new ChatModule();
            InitialiseModule(module, scene);

            module = new AvatarProfilesModule();
            InitialiseModule(module, scene);

            LoadRegionModule("OpenSim.Region.ExtensionsScriptModule.dll", "ExtensionsScriptingModule", scene);

            string lslPath = Path.Combine("ScriptEngines", "OpenSim.Region.ScriptEngine.DotNetEngine.dll");
            LoadRegionModule(lslPath, "LSLScriptingModule", scene);
        }


        public void LoadDefaultSharedModules(string exceptModules)
        {
            DynamicTextureModule dynamicModule = new DynamicTextureModule();
            LoadedSharedModules.Add(dynamicModule.GetName(), dynamicModule);
        }

        public void InitialiseSharedModules(Scene scene)
        {
            foreach (IRegionModule module in LoadedSharedModules.Values)
            {
                module.Initialise(scene);
                scene.AddModule(module.GetName(), module); //should be doing this?
            }
        }

        private void InitialiseModule(IRegionModule module, Scene scene)
        {
            module.Initialise(scene);
            scene.AddModule(module.GetName(), module);
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
                LoadedSharedModules.Add(module.GetName(), module);
            }
        }

        public void LoadRegionModule(string dllName, string moduleName, Scene scene)
        {
            IRegionModule module = LoadModule(dllName, moduleName);
            if (module != null)
            {
                InitialiseModule(module, scene);
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
            Assembly pluginAssembly = null;
            if (LoadedAssemblys.ContainsKey(dllName))
            {
                pluginAssembly = LoadedAssemblys[dllName];
            }
            else
            {
                pluginAssembly = Assembly.LoadFrom(dllName);
                LoadedAssemblys.Add(dllName, pluginAssembly);
            }

            IRegionModule module = null;
            foreach (Type pluginType in pluginAssembly.GetTypes())
            {
                if (pluginType.IsPublic)
                {
                    if (!pluginType.IsAbstract)
                    {
                        Type typeInterface = pluginType.GetInterface("IRegionModule", true);

                        if (typeInterface != null)
                        {
                            module =
                                (IRegionModule) Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                            break;
                        }
                        typeInterface = null;
                    }
                }
            }
            pluginAssembly = null;

            if ((module != null) || (module.GetName() == moduleName))
            {
                return module;
            }

            return null;
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