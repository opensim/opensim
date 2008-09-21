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
using System.Reflection;
using System.Text;
using log4net;
using Nini.Config;
using OpenSim.ApplicationPlugins.ScriptEngine.Components;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.ApplicationPlugins.ScriptEngine
{
    public abstract class RegionScriptEngineBase
    {

        /// <summary>
        /// Called on region initialize
        /// </summary>
        public abstract void Initialize();
        public abstract string Name { get; }
        /// <summary>
        /// Called before components receive Close()
        /// </summary>
        public abstract void PreClose();
        // Hold list of all the different components we have working for us
        public List<ComponentBase> Components = new List<ComponentBase>();

        public Scene m_Scene;
        public IConfigSource m_ConfigSource;
        public readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public void Initialize(Scene scene, IConfigSource source)
        {
            // Region started
            m_Scene = scene;
            m_ConfigSource = source;
            Initialize();
        }

        /// <summary>
        /// Creates instances of script engine components inside "components" List
        /// </summary>
        /// <param name="ComponentList">Array of comonent names to initialize</param>
        public void InitializeComponents(string[] ComponentList)
        {
            // Take list of providers to initialize and make instances of them
            foreach (string c in ComponentList)
            {
                m_log.Info("[" + Name + "]: Loading: " + c);
                lock (Components)
                {
                    lock (ComponentRegistry.providers)
                    {
                        try
                        {
                            if (ComponentRegistry.providers.ContainsKey(c))
                                Components.Add(Activator.CreateInstance(ComponentRegistry.providers[c]) as ComponentBase);
                            else
                                m_log.Error("[" + Name + "]: Component \"" + c + "\" not found, can not load");
                        }
                        catch (Exception ex)
                        {
                            m_log.Error("[" + Name + "]: Exception loading \"" + c + "\": " + ex.ToString());
                        }
                    }
                }
            }


            // Run Initialize on all our providers, hand over a reference of ourself.
            foreach (ComponentBase p in Components.ToArray())
            {
                try
                {
                    p.Initialize(this);
                }
                catch (Exception ex)
                {
                    lock (Components)
                    {
                        m_log.Error("[" + Name + "]: Error initializing \"" + p.GetType().FullName + "\": " +
                                    ex.ToString());
                        if (Components.Contains(p))
                            Components.Remove(p);
                    }
                }
            }
            // All modules has been initialized, call Start() on them.
            foreach (ComponentBase p in Components.ToArray())
            {
                try
                {
                    p.Start();
                }
                catch (Exception ex)
                {
                    lock (Components)
                    {
                        m_log.Error("[" + Name + "]: Error starting \"" + p.GetType().FullName + "\": " + ex.ToString());
                        if (Components.Contains(p))
                            Components.Remove(p);
                    }
                }
            }

        }

        #region Functions to return lists based on type
        // Predicate for searching List for a certain type
        private static bool FindType<T>(ComponentBase pb)
        {
            if (pb.GetType() is T)
                return true;
            return false;
        }
        public List<ComponentBase> GetCommandComponentList()
        {
            return Components.FindAll(FindType<CommandBase>);
        }
        public List<ComponentBase> GetCompilerComponentList()
        {
            return Components.FindAll(FindType<CompilerBase>);
        }
        public List<ComponentBase> GetEventComponentList()
        {
            return Components.FindAll(FindType<EventBase>);
        }
        public List<ComponentBase> GetScheduleComponentList()
        {
            return Components.FindAll(FindType<SchedulerBase>);
        }

        #endregion

        public void Close()
        {
            // We need to shut down

            // First call abstracted PreClose()
            PreClose();

            // Then Call Close() on all components
            foreach (ComponentBase p in Components.ToArray())
            {
                try
                {
                    p.Close();
                }
                catch (Exception)
                {
                    // TODO: Print error to console
                }
            }
        }
    }
}

