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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Modules;
using OpenSim.Region.Environment.Scenes;
using Nini.Config;

namespace OpenSim.Region.Environment
{
    public class ModuleLoader
    {
        public Dictionary<string, Assembly> LoadedAssemblys = new Dictionary<string, Assembly>();

        public List<IRegionModule> LoadedModules = new List<IRegionModule>();
        public Dictionary<string, IRegionModule> LoadedSharedModules = new Dictionary<string, IRegionModule>();
        private readonly LogBase m_log;
        private IConfigSource m_config;

        public ModuleLoader(LogBase log, IConfigSource config)
        {
            m_log = log;
            m_config = config;
        }

        public void PickupModules(Scene scene, string moduleDir)
        {
            DirectoryInfo dir = new DirectoryInfo(moduleDir);

            foreach (FileInfo fileInfo in dir.GetFiles("*.dll"))
            {
                LoadRegionModules(fileInfo.FullName, scene);
            }
        }

        public void LoadDefaultSharedModules()
        {
            DynamicTextureModule dynamicModule = new DynamicTextureModule();
            LoadedSharedModules.Add(dynamicModule.Name, dynamicModule);
            ChatModule chat = new ChatModule();
            LoadedSharedModules.Add(chat.Name, chat);
            InstantMessageModule imMod = new InstantMessageModule();
            LoadedSharedModules.Add(imMod.Name, imMod);
            LoadImageURLModule loadMod = new LoadImageURLModule();
            LoadedSharedModules.Add(loadMod.Name, loadMod);
        }

        public void InitialiseSharedModules(Scene scene)
        {
            foreach (IRegionModule module in LoadedSharedModules.Values)
            {
                module.Initialise(scene, m_config);
                scene.AddModule(module.Name, module); //should be doing this?
            }
        }

        public void InitializeModule(IRegionModule module, Scene scene)
        {
            module.Initialise(scene, m_config);
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
                    if (!module.IsSharedModule)
                    {
                        m_log.Verbose("MODULES", "   [{0}]: Initializing.", module.Name);
                        InitializeModule(module, scene);
                    }
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
                    m_log.Warn( "MODULES", "The file [{0}] is not a module assembly.", e.FileName );
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
