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
using System.Reflection;
using log4net;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.Framework
{
    public class StorageManager
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected ISimulationDataStore m_dataStore;

        public ISimulationDataStore DataStore
        {
            get { return m_dataStore; }
        }

        private IEstateDataStore m_estateDataStore;

        public IEstateDataStore EstateDataStore
        {
            get { return m_estateDataStore; }
        }

        public StorageManager(ISimulationDataStore storage)
        {
            m_dataStore = storage;
        }

        public StorageManager(string dllName, string connectionstring, string estateconnectionstring)
        {
            m_log.Info("[DATASTORE]: Attempting to load " + dllName);
            Assembly pluginAssembly = Assembly.LoadFrom(dllName);

            foreach (Type pluginType in pluginAssembly.GetTypes())
            {
                if (pluginType.IsPublic)
                {
                    Type typeInterface = pluginType.GetInterface("IRegionDataStore", true);

                    if (typeInterface != null)
                    {
                        ISimulationDataStore plug =
                            (ISimulationDataStore)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                        plug.Initialise(connectionstring);

                        m_dataStore = plug;

                        m_log.Info("[DATASTORE]: Added IRegionDataStore Interface");
                    }

                    typeInterface = pluginType.GetInterface("IEstateDataStore", true);

                    if (typeInterface != null)
                    {
                        IEstateDataStore estPlug =
                            (IEstateDataStore) Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                        estPlug.Initialise(estateconnectionstring);

                        m_estateDataStore = estPlug;
                    }
                }
            }

            //TODO: Add checking and warning to make sure it initialised.
        }
    }
}
