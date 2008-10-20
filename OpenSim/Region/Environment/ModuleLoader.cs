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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Modules.Agent.AssetTransaction;
using OpenSim.Region.Environment.Modules.Avatar.Chat;
using OpenSim.Region.Environment.Modules.Avatar.InstantMessage;
using OpenSim.Region.Environment.Modules.Scripting.DynamicTexture;
using OpenSim.Region.Environment.Modules.Scripting.LoadImageURL;
using OpenSim.Region.Environment.Modules.Scripting.XMLRPC;
using OpenSim.Region.Environment.Scenes;

//TODO: Why do we reference InstantMessageModule here?

namespace OpenSim.Region.Environment
{
    public class ModuleLoader
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Dictionary<string, Assembly> LoadedAssemblys = new Dictionary<string, Assembly>();

        private readonly List<IRegionModule> m_loadedModules = new List<IRegionModule>();
        private readonly Dictionary<string, IRegionModule> m_loadedSharedModules = new Dictionary<string, IRegionModule>();
        private readonly IConfigSource m_config;

        public ModuleLoader(IConfigSource config)
        {
            m_config = config;
        }

        public IRegionModule[] GetLoadedSharedModules
        {
            get
            {
                IRegionModule[] regionModules = new IRegionModule[m_loadedSharedModules.Count];
                m_loadedSharedModules.Values.CopyTo(regionModules, 0);
                return regionModules;
            }
        }

        public List<IRegionModule> PickupModules(Scene scene, string moduleDir)
        {
            DirectoryInfo dir = new DirectoryInfo(moduleDir);
            List<IRegionModule> modules = new List<IRegionModule>();

            foreach (FileInfo fileInfo in dir.GetFiles("*.dll"))
            {
                modules.AddRange(LoadRegionModules(fileInfo.FullName, scene));
            }
            return modules;
        }

        public void LoadDefaultSharedModules()
        {
            DynamicTextureModule dynamicModule = new DynamicTextureModule();
            if (m_loadedSharedModules.ContainsKey(dynamicModule.Name))
            {
                m_log.ErrorFormat("[MODULES]: Module name \"{0}\" already exists in module list. Module type {1} not added!", dynamicModule.Name, "DynamicTextureModule");
            }
            else
            {
                m_loadedSharedModules.Add(dynamicModule.Name, dynamicModule);
            }

            // ChatModule chat = new ChatModule();
            // if (m_loadedSharedModules.ContainsKey(chat.Name))
            // {
            //     m_log.ErrorFormat("[MODULES]: Module name \"{0}\" already exists in module list. Module type {1} not added!", chat.Name, "ChatModule");
            // }
            // else
            // {
            //     m_loadedSharedModules.Add(chat.Name, chat);
            // }

            InstantMessageModule imMod = new InstantMessageModule();
            if (m_loadedSharedModules.ContainsKey(imMod.Name))
            {
                m_log.ErrorFormat("[MODULES]: Module name \"{0}\" already exists in module list. Module type {1} not added!", imMod.Name, "InstantMessageModule");
            }
            else
            {
                m_loadedSharedModules.Add(imMod.Name, imMod);
            }

            LoadImageURLModule loadMod = new LoadImageURLModule();
            if (m_loadedSharedModules.ContainsKey(loadMod.Name))
            {
                m_log.ErrorFormat("[MODULES]: Module name \"{0}\" already exists in module list. Module type {1} not added!", loadMod.Name, "LoadImageURLModule");
            }
            else
            {
                m_loadedSharedModules.Add(loadMod.Name, loadMod);
            }

           // AvatarFactoryModule avatarFactory = new AvatarFactoryModule();
           // if (m_loadedSharedModules.ContainsKey(avatarFactory.Name))
           // {
           //     m_log.ErrorFormat("[MODULES]: Module name \"{0}\" already exists in module list. Module type {1} not added!", avatarFactory.Name, "AvarFactoryModule");
           // }
           // else
           // {
           //     m_loadedSharedModules.Add(avatarFactory.Name, avatarFactory);
           // }

            XMLRPCModule xmlRpcMod = new XMLRPCModule();
            if (m_loadedSharedModules.ContainsKey(xmlRpcMod.Name))
            {
                m_log.ErrorFormat("[MODULES]: Module name \"{0}\" already exists in module list. Module type {1} not added!", xmlRpcMod.Name, "XMLRPCModule");
            }
            else
            {
                m_loadedSharedModules.Add(xmlRpcMod.Name, xmlRpcMod);
            }
            //TextureDownloadModule textureModule = new TextureDownloadModule();

            //LoadedSharedModules.Add(textureModule.Name, textureModule);

            AssetTransactionModule transactionsModule = new AssetTransactionModule();
            if (m_loadedSharedModules.ContainsKey(transactionsModule.Name))
            {
                m_log.ErrorFormat("[MODULES]: Module name \"{0}\" already exists in module list. Module type {1} not added!", transactionsModule.Name, "TransactionModule");
            }
            else
            {
                m_loadedSharedModules.Add(transactionsModule.Name, transactionsModule);
            }

        }

        public void InitialiseSharedModules(Scene scene)
        {
            foreach (IRegionModule module in m_loadedSharedModules.Values)
            {
                module.Initialise(scene, m_config);
                scene.AddModule(module.Name, module); //should be doing this?
            }
        }

        public void InitializeModule(IRegionModule module, Scene scene)
        {
            module.Initialise(scene, m_config);
            scene.AddModule(module.Name, module);
            m_loadedModules.Add(module);
        }

        /// <summary>
        ///  Loads/initialises a Module instance that can be used by multiple Regions
        /// </summary>
        /// <param name="dllName"></param>
        /// <param name="moduleName"></param>
        public void LoadSharedModule(string dllName, string moduleName)
        {
            IRegionModule module = LoadModule(dllName, moduleName);

            if (module != null)
                LoadSharedModule(module);
        }

        /// <summary>
        ///  Loads/initialises a Module instance that can be used by multiple Regions
        /// </summary>
        /// <param name="module"></param>
        public void LoadSharedModule(IRegionModule module)
        {
            if (!m_loadedSharedModules.ContainsKey(module.Name))
            {
                m_loadedSharedModules.Add(module.Name, module);
            }
        }

        public List<IRegionModule> LoadRegionModules(string dllName, Scene scene)
        {
            IRegionModule[] modules = LoadModules(dllName);
            List<IRegionModule> initializedModules = new List<IRegionModule>();

            if (modules.Length > 0)
            {
                m_log.InfoFormat("[MODULES]: Found Module Library [{0}]", dllName);
                foreach (IRegionModule module in modules)
                {
                    if (!module.IsSharedModule)
                    {
                        m_log.InfoFormat("[MODULES]:    [{0}]: Initializing.", module.Name);
                        InitializeModule(module, scene);
                        initializedModules.Add(module);
                    }
                    else
                    {
                        m_log.InfoFormat("[MODULES]:    [{0}]: Loading Shared Module.", module.Name);
                        LoadSharedModule(module);
                    }
                }
            }
            return initializedModules;
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
            if (!LoadedAssemblys.TryGetValue(dllName, out pluginAssembly))
            {
                try
                {
                    pluginAssembly = Assembly.LoadFrom(dllName);
                    LoadedAssemblys.Add(dllName, pluginAssembly);
                }
                catch (BadImageFormatException)
                {
                    //m_log.InfoFormat("[MODULES]: The file [{0}] is not a module assembly.", e.FileName);
                }
            }

            if (pluginAssembly != null)
            {
                try
                {
                    foreach (Type pluginType in pluginAssembly.GetTypes())
                    {
                        if (pluginType.IsPublic)
                        {
                            if (!pluginType.IsAbstract)
                            {
                                if (pluginType.GetInterface("IRegionModule") != null)
                                {
                                    modules.Add((IRegionModule)Activator.CreateInstance(pluginType));
                                }
                            }
                        }
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    m_log.InfoFormat("[MODULES]: Could not load types for [{0}].", pluginAssembly.FullName);
                }
            }

            return modules.ToArray();
        }

        public void PostInitialise()
        {
            foreach (IRegionModule module in m_loadedSharedModules.Values)
            {
                module.PostInitialise();
            }

            foreach (IRegionModule module in m_loadedModules)
            {
                module.PostInitialise();
            }
        }

        public void ClearCache()
        {
            LoadedAssemblys.Clear();
        }

        public void UnloadModule(IRegionModule rm)
        {
            rm.Close();

            m_loadedModules.Remove(rm);
        }
    }
}
