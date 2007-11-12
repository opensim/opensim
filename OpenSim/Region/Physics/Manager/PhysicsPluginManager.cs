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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
using Nini.Config;

namespace OpenSim.Region.Physics.Manager
{
    /// <summary>
    /// Description of MyClass.
    /// </summary>
    public class PhysicsPluginManager
    {
        private Dictionary<string, IPhysicsPlugin> _PhysPlugins = new Dictionary<string, IPhysicsPlugin>();
        private Dictionary<string, IMeshingPlugin> _MeshPlugins = new Dictionary<string, IMeshingPlugin>();

        public PhysicsPluginManager()
        {
        }

        public PhysicsScene GetPhysicsScene(string physEngineName, string meshEngineName)
        {

            if (String.IsNullOrEmpty(physEngineName))
            {
                return PhysicsScene.Null;
            }

            if (String.IsNullOrEmpty(meshEngineName))
            {
                return PhysicsScene.Null;
            }


            IMesher meshEngine = null;
            if (_MeshPlugins.ContainsKey(meshEngineName))
            {
                MainLog.Instance.Verbose("PHYSICS", "creating meshing engine " + meshEngineName);
                meshEngine = _MeshPlugins[meshEngineName].GetMesher();
            }
            else
            {
                MainLog.Instance.Warn("PHYSICS", "couldn't find meshingEngine: {0}", meshEngineName);
                throw new ArgumentException(String.Format("couldn't find meshingEngine: {0}", meshEngineName));
            }

            if (_PhysPlugins.ContainsKey(physEngineName))
            {
                MainLog.Instance.Verbose("PHYSICS", "creating " + physEngineName);
                PhysicsScene result = _PhysPlugins[physEngineName].GetScene();
                result.Initialise(meshEngine);
                return result;
            }
            else
            {
                MainLog.Instance.Warn("PHYSICS", "couldn't find physicsEngine: {0}", physEngineName);
                throw new ArgumentException(String.Format("couldn't find physicsEngine: {0}", physEngineName));
            }
        }

        public void LoadPlugins()
        {

            // Load "plugins", that are hard coded and not existing in form of an external lib
            IMeshingPlugin plugHard;
            plugHard = new ZeroMesherPlugin();
            _MeshPlugins.Add(plugHard.GetName(), plugHard);
            MainLog.Instance.Verbose("PHYSICS", "Added meshing engine: " + plugHard.GetName());
            
            // And now walk all assemblies (DLLs effectively) and see if they are home 
            // of a plugin that is of interest for us
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Physics");
            string[] pluginFiles = Directory.GetFiles(path, "*.dll");


            for (int i = 0; i < pluginFiles.Length; i++)
            {
                AddPlugin(pluginFiles[i]);
            }
        }

        private void AddPlugin(string FileName)
        {

            Assembly pluginAssembly = Assembly.LoadFrom(FileName);

            foreach (Type pluginType in pluginAssembly.GetTypes())
            {
                if (pluginType.IsPublic)
                {
                    if (!pluginType.IsAbstract)
                    {
                        Type physTypeInterface = pluginType.GetInterface("IPhysicsPlugin", true);

                        if (physTypeInterface != null)
                        {
                            IPhysicsPlugin plug =
                                (IPhysicsPlugin) Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                            plug.Init();
                            _PhysPlugins.Add(plug.GetName(), plug);
                            MainLog.Instance.Verbose("PHYSICS", "Added physics engine: " + plug.GetName());
                        }

                        Type meshTypeInterface = pluginType.GetInterface("IMeshingPlugin", true);

                        if (meshTypeInterface != null)
                        {
                            IMeshingPlugin plug =
                                (IMeshingPlugin)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                            _MeshPlugins.Add(plug.GetName(), plug);
                            MainLog.Instance.Verbose("PHYSICS", "Added meshing engine: " + plug.GetName());
                        }

                        physTypeInterface = null;
                        meshTypeInterface = null;
                    }
                }
            }

            pluginAssembly = null;
        }

        //---
        public static void PhysicsPluginMessage(string message, bool isWarning)
        {
            if (isWarning)
            {
                MainLog.Instance.Warn("PHYSICS", message);
            }
            else
            {
                MainLog.Instance.Verbose("PHYSICS", message);
            }
        }

        //---
    }

    public interface IPhysicsPlugin
    {
        bool Init();
        PhysicsScene GetScene();
        string GetName();
        void Dispose();
    }

    public interface IMeshingPlugin
    {
        string GetName();
        IMesher GetMesher();
    }

}
