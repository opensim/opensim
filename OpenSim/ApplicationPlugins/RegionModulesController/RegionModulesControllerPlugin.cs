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
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Mono.Addins;
using OpenSim;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.ApplicationPlugins.RegionModulesController
{
    public class RegionModulesControllerPlugin : IRegionModulesController, IApplicationPlugin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private OpenSimBase m_openSim; // for getting the config

        private string m_name;

        private List<Type> m_nonSharedModules = new List<Type>();
        private List<Type> m_sharedModules = new List<Type>();

        private List<ISharedRegionModule> m_sharedInstances = new List<ISharedRegionModule>();

#region IApplicationPlugin implementation

        public void Initialise (OpenSimBase openSim)
        {
            m_log.DebugFormat("[REGIONMODULES]: Initializing...");
            m_openSim = openSim;
            openSim.ApplicationRegistry.RegisterInterface<IRegionModulesController>(this);

            string id = AddinManager.CurrentAddin.Id;
            int pos = id.LastIndexOf(".");
            if (pos == -1) m_name = id;
            else m_name = id.Substring(pos + 1);

            //ExtensionNodeList list = AddinManager.GetExtensionNodes("/OpenSim/RegionModules");
            // load all the (new) region-module classes
            foreach (TypeExtensionNode node in AddinManager.GetExtensionNodes("/OpenSim/RegionModules"))
            {
                // TODO why does node.Type.isSubclassOf(typeof(ISharedRegionModule)) not work?
                if (node.Type.GetInterface(typeof(ISharedRegionModule).ToString()) != null)
                {
                    m_log.DebugFormat("[REGIONMODULES]: Found shared region module {0}, class {1}", node.Id, node.Type);
                    m_sharedModules.Add(node.Type);
                }
                else if (node.Type.GetInterface(typeof(INonSharedRegionModule).ToString()) != null)
                {
                    m_log.DebugFormat("[REGIONMODULES]: Found non-shared region module {0}, class {1}", node.Id, node.Type);
                    m_nonSharedModules.Add(node.Type);
                }
                else
                    m_log.DebugFormat("[REGIONMODULES]: Found unknown type of module {0}, class {1}", node.Id, node.Type);
            }

            // now we've got all the region-module classes loaded, create one instance of every ISharedRegionModule,
            // initialize and postinitialize it. This Initialise we are in is called before LoadRegion.PostInitialise
            // is called (which loads the regions), so we don't have any regions in the server yet.
            foreach (Type type in m_sharedModules)
            {
                ISharedRegionModule module = (ISharedRegionModule)Activator.CreateInstance(type);
                m_sharedInstances.Add(module);
                module.Initialise(openSim.ConfigSource.Source);
            }

            foreach (ISharedRegionModule module in m_sharedInstances)
            {
                module.PostInitialise();
            }
        }

        public void PostInitialise ()
        {
        }

#endregion

#region IPlugin implementation

        public void Initialise ()
        {
            throw new System.NotImplementedException();
        }

#endregion

#region IDisposable implementation

        public void Dispose ()
        {
            // we expect that all regions have been removed already
            while (m_sharedInstances.Count > 0)
            {
                m_sharedInstances[0].Close();
                m_sharedInstances.RemoveAt(0);
            }
            m_sharedModules.Clear();
            m_nonSharedModules.Clear();
        }

#endregion


        public string Version
        {
            get
            {
                return AddinManager.CurrentAddin.Version;
            }
        }

        public string Name
        {
            get
            {
                return m_name;
            }
        }

#region IRegionModulesController implementation

        public void AddRegionToModules (Scene scene)
        {
            Dictionary<Type, ISharedRegionModule> deferredSharedModules =
                    new Dictionary<Type, ISharedRegionModule>();
            Dictionary<Type, INonSharedRegionModule> deferredNonSharedModules =
                    new Dictionary<Type, INonSharedRegionModule>();

            Type s = scene.GetType();
            MethodInfo mi = s.GetMethod("RequestModuleInterface");

            List<ISharedRegionModule> sharedlist = new List<ISharedRegionModule>();
            foreach (ISharedRegionModule module in m_sharedInstances)
            {
                Type replaceableInterface = module.ReplaceableInterface;
                if (replaceableInterface != null)
                {
                    MethodInfo mii = mi.MakeGenericMethod(replaceableInterface);

                    if (mii.Invoke(scene, new object[0]) != null)
                    {
                        m_log.DebugFormat("[REGIONMODULE]: Not loading {0} because another module has registered {1}", module.Name, replaceableInterface.ToString());
                        continue;
                    }

                    deferredSharedModules[replaceableInterface] = module;
                    m_log.DebugFormat("[REGIONMODULE]: Deferred load of {0}", module.Name);
                    continue;
                }

                m_log.DebugFormat("[REGIONMODULE]: Adding scene {0} to shared module {1}",
                                  scene.RegionInfo.RegionName, module.Name);

                module.AddRegion(scene);
                scene.AddRegionModule(module.Name, module);

                sharedlist.Add(module);
            }

            List<INonSharedRegionModule> list = new List<INonSharedRegionModule>();
            foreach (Type type in m_nonSharedModules)
            {
                INonSharedRegionModule module = (INonSharedRegionModule)Activator.CreateInstance(type);

                Type replaceableInterface = module.ReplaceableInterface;
                if (replaceableInterface != null)
                {
                    MethodInfo mii = mi.MakeGenericMethod(replaceableInterface);

                    if (mii.Invoke(scene, new object[0]) != null)
                    {
                        m_log.DebugFormat("[REGIONMODULE]: Not loading {0} because another module has registered {1}", module.Name, replaceableInterface.ToString());
                        continue;
                    }

                    deferredNonSharedModules[replaceableInterface] = module;
                    m_log.DebugFormat("[REGIONMODULE]: Deferred load of {0}", module.Name);
                    continue;
                }

                m_log.DebugFormat("[REGIONMODULE]: Adding scene {0} to non-shared module {1}",
                                  scene.RegionInfo.RegionName, module.Name);

                module.Initialise(m_openSim.ConfigSource.Source);

                list.Add(module);
            }

            foreach (INonSharedRegionModule module in list)
            {
                module.AddRegion(scene);
                scene.AddRegionModule(module.Name, module);
            }

            // Now all modules without a replaceable base interface are loaded
            // Replaceable modules have either been skipped, or omitted.
            // Now scan the deferred modules here

            foreach (ISharedRegionModule module in deferredSharedModules.Values)
            {
                Type replaceableInterface = module.ReplaceableInterface;
                MethodInfo mii = mi.MakeGenericMethod(replaceableInterface);

                if (mii.Invoke(scene, new object[0]) != null)
                {
                    m_log.DebugFormat("[REGIONMODULE]: Not loading {0} because another module has registered {1}", module.Name, replaceableInterface.ToString());
                    continue;
                }

                m_log.DebugFormat("[REGIONMODULE]: Adding scene {0} to shared module {1} (deferred)",
                                  scene.RegionInfo.RegionName, module.Name);

                module.AddRegion(scene);
                scene.AddRegionModule(module.Name, module);

                sharedlist.Add(module);
            }

            List<INonSharedRegionModule> deferredlist = new List<INonSharedRegionModule>();
            foreach (INonSharedRegionModule module in deferredNonSharedModules.Values)
            {
                Type replaceableInterface = module.ReplaceableInterface;
                if (replaceableInterface != null)
                {
                    MethodInfo mii = mi.MakeGenericMethod(replaceableInterface);

                    if (mii.Invoke(scene, new object[0]) != null)
                    {
                        m_log.DebugFormat("[REGIONMODULE]: Not loading {0} because another module has registered {1}", module.Name, replaceableInterface.ToString());
                        continue;
                    }
                }

                m_log.DebugFormat("[REGIONMODULE]: Adding scene {0} to non-shared module {1} (deferred)",
                                  scene.RegionInfo.RegionName, module.Name);

                module.Initialise(m_openSim.ConfigSource.Source);

                list.Add(module);
                deferredlist.Add(module);
            }

            foreach (INonSharedRegionModule module in deferredlist)
            {
                module.AddRegion(scene);
                scene.AddRegionModule(module.Name, module);
            }

            // This is needed for all module types. Modules will register
            // Interfaces with scene in AddScene, and will also need a means
            // to access interfaces registered by other modules. Without
            // this extra method, a module attempting to use another modules's
            // interface would be successful only depending on load order,
            // which can't be depended upon, or modules would need to resort
            // to ugly kludges to attempt to request interfaces when needed
            // and unneccessary caching logic repeated in all modules.
            // The extra function stub is just that much cleaner
            //
            foreach (ISharedRegionModule module in sharedlist)
            {
                m_log.Debug("[REGIONMODULE]: Calling RegionLoaded for " + module);
                module.RegionLoaded(scene);
            }

            foreach (INonSharedRegionModule module in list)
            {
                module.RegionLoaded(scene);
            }
        }

        public void RemoveRegionFromModules (Scene scene)
        {
            foreach (IRegionModuleBase module in scene.RegionModules.Values)
            {
                m_log.DebugFormat("[REGIONMODULE]: Removing scene {0} from module {1}",
                                  scene.RegionInfo.RegionName, module.Name);
                module.RemoveRegion(scene);
                if (module is INonSharedRegionModule)
                {
                    // as we were the only user, this instance has to die
                    module.Close();
                }
            }
            scene.RegionModules.Clear();
        }

#endregion

    }
}
