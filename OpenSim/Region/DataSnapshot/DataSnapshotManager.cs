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
* 
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Timers;
using System.Xml;
using libsecondlife;
using log4net;
using Nini.Config;
using OpenSim.Framework.Communications;
using OpenSim.Region.DataSnapshot.Interfaces;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.DataSnapshot
{
    public class DataSnapshotManager : IRegionModule
    {
        #region Class members
        private List<Scene> m_scenes = new List<Scene>();
        private ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private bool m_enabled = false;
        private bool m_configLoaded = false;
        internal object m_syncInit = new object();
        private DataRequestHandler m_requests = null;
        private Dictionary<Scene, List<IDataSnapshotProvider>> m_dataproviders = new Dictionary<Scene, List<IDataSnapshotProvider>>();
        private Dictionary<string, string> m_gridinfo = new Dictionary<string, string>();
        //private int m_oldestSnapshot = 0;
        private int m_maxSnapshots = 500;
        private int m_lastSnapshot = 0;
        private string m_snapsDir = "DataSnapshot";
        private string m_dataServices = "noservices";
        private string m_listener_port = "9000"; //TODO: Set default port over 9000
        private string m_hostname = "127.0.0.1";
        private Timer m_periodic = null;
        private int m_period = 60; // in seconds
        private List<string> m_disabledModules = new List<string>();
        #endregion

        #region IRegionModule
        public void Close()
        {
             
        }
        
        public void Initialise(Scene scene, IConfigSource config)
        {
            if (!m_scenes.Contains(scene))
                m_scenes.Add(scene);
            
            if (!m_configLoaded) {
                m_configLoaded = true;
                m_log.Info("[DATASNAPSHOT]: Loading configuration");
                //Read from the config for options
                 lock (m_syncInit) {
                    try {
                        m_enabled = config.Configs["DataSnapshot"].GetBoolean("index_sims", m_enabled);
                        if (config.Configs["Startup"].GetBoolean("gridmode", true))
                        {
                            m_gridinfo.Add("gridserverURL", config.Configs["Network"].GetString("grid_server_url", "harbl"));
                            m_gridinfo.Add("userserverURL", config.Configs["Network"].GetString("user_server_url", "harbl"));
                            m_gridinfo.Add("assetserverURL", config.Configs["Network"].GetString("asset_server_url", "harbl"));
                        }
                        else
                        {
                            //Non gridmode stuff
                        }

                        m_gridinfo.Add("Name", config.Configs["DataSnapshot"].GetString("gridname", "harbl"));
                        m_maxSnapshots = config.Configs["DataSnapshot"].GetInt("max_snapshots", m_maxSnapshots);
                        m_period = config.Configs["DataSnapshot"].GetInt("default_snapshot_period", m_period);
                        m_snapsDir = config.Configs["DataSnapshot"].GetString("snapshot_cache_directory", m_snapsDir);
                        m_dataServices = config.Configs["DataSnapshot"].GetString("data_services", m_dataServices);
                        m_listener_port = config.Configs["Network"].GetString("http_listener_port", m_listener_port);
                        //BUG: Naming a search data module "DESUDESUDESU" will cause it to not get loaded by default.
                        //RESOLUTION: Wontfix, there are no Suiseiseki-loving developers
                        String[] annoying_string_array = config.Configs["DataSnapshot"].GetString("disable_modules", "DESUDESUDESU").Split(".".ToCharArray());
                        foreach (String bloody_wanker in annoying_string_array) {
                            m_disabledModules.Add(bloody_wanker);
                        }

                    } catch (Exception) {
                        m_log.Info("[DATASNAPSHOT]: Could not load configuration. DataSnapshot will be disabled.");
                        m_enabled = false;
                        return;
                    }
      
                }
            }
            if (Directory.Exists(m_snapsDir))
            {
                m_log.Info("[DATASNAPSHOT] DataSnapshot directory already exists.");
            }
            else
            {
                // Try to create the directory.
                m_log.Info("[DATASNAPSHOT] Creating " + m_snapsDir + " directory.");
                try
                {
                    Directory.CreateDirectory(m_snapsDir);
                }
                catch (Exception)
                {
                    m_log.Error("[DATASNAPSHOT] Failed to create " + m_snapsDir + " directory.");
                }
            }


            if (m_enabled)
            {
                m_log.Info("[DATASNAPSHOT]: Scene added to module.");
            }
            else
            {
                m_log.Warn("[DATASNAPSHOT]: Data snapshot disabled, not adding scene to module (or anything else).");
            }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        public string Name
        {
            get { return "External Data Generator"; }
        }

        public void PostInitialise()
        {
            if (m_enabled)
            {
                //Right now, only load ISearchData objects in the current assembly.
                //Eventually allow it to load ISearchData objects from all assemblies.
                Assembly currentasm = Assembly.GetExecutingAssembly();

                //Stolen from ModuleLoader.cs
                foreach (Type pluginType in currentasm.GetTypes())
                {
                    if (pluginType.IsPublic)
                    {
                        if (!pluginType.IsAbstract)
                        {
                            if (pluginType.GetInterface("IDataSnapshotProvider") != null)
                            {
                                foreach (Scene scene in m_scenes)
                                {
                                    IDataSnapshotProvider module = (IDataSnapshotProvider)Activator.CreateInstance(pluginType);
                                    module.Initialize(scene, this);
                                    //module.PrepareData();
                                    List<IDataSnapshotProvider> providerlist = null;
                                    m_dataproviders.TryGetValue(scene, out providerlist);
                                    if (providerlist == null)
                                    {
                                        providerlist = new List<IDataSnapshotProvider>();
                                        m_dataproviders.Add(scene, providerlist);
                                    }
                                    providerlist.Add(module);

                                }
                                m_log.Info("[DATASNAPSHOT]: Added new data provider type: " + pluginType.Name);
                            }
                        }
                    }
                }

                //Hand it the first scene, assuming that all scenes have the same BaseHTTPServer
                m_requests = new DataRequestHandler(m_scenes[0], this);

                //Create timer
                m_periodic = new Timer();
                m_periodic.Interval = m_period * 1000;
                m_periodic.Elapsed += SnapshotTimerCallback;
                m_periodic.Enabled = true;

                m_hostname = m_scenes[0].RegionInfo.ExternalHostName;

                MakeNewSnapshot();  //Make the initial snapshot

                if (m_dataServices != "noservices")
                   NotifyDataServices(m_dataServices);
            }
        }
        #endregion

        #region Associated helper functions

        string DataFileName(Scene scene)
        {
            return Path.Combine(m_snapsDir, Path.ChangeExtension(scene.RegionInfo.RegionName, "xml"));
            //return (m_snapsDir + Path.DirectorySeparatorChar + scene.RegionInfo.RegionName + ".xml");
        }

        Scene SceneForName(string name)
        {
            foreach (Scene scene in m_scenes)
                if (scene.RegionInfo.RegionName == name)
                    return scene;

            return null;
        }

        #endregion

        #region [Private] XML snapshot generator

        private XmlDocument Snapshot(Scene scene)
        {
            XmlDocument basedoc = new XmlDocument();
            XmlNode regionElement = MakeRegionNode(scene, basedoc);

            regionElement.AppendChild(GetGridSnapshotData(basedoc));
            XmlNode regionData = basedoc.CreateNode(XmlNodeType.Element, "data", "");

            foreach (KeyValuePair<Scene, List<IDataSnapshotProvider>> dataprovider in m_dataproviders)
            {
                if (dataprovider.Key == scene)
                {
                    foreach (IDataSnapshotProvider provider in dataprovider.Value)
                    {
                        XmlNode data = provider.RequestSnapshotData(basedoc);
                        regionData.AppendChild(data);
                    }
                }
            }

            regionElement.AppendChild(regionData);

            basedoc.AppendChild(regionElement);

            return basedoc;
        }

        private XmlNode MakeRegionNode(Scene scene, XmlDocument basedoc)
        {
            XmlNode docElement = basedoc.CreateNode(XmlNodeType.Element, "region", "");

            XmlAttribute attr = basedoc.CreateAttribute("category");
            attr.Value = GetRegionCategory(scene);
            docElement.Attributes.Append(attr);

            attr = basedoc.CreateAttribute("entities");
            attr.Value = scene.Entities.Count.ToString();
            docElement.Attributes.Append(attr);

            //attr = basedoc.CreateAttribute("parcels");
            //attr.Value = scene.LandManager.landList.Count.ToString();
            //docElement.Attributes.Append(attr);


            XmlNode infoblock = basedoc.CreateNode(XmlNodeType.Element, "info", "");

            XmlNode infopiece = basedoc.CreateNode(XmlNodeType.Element, "uuid", "");
            infopiece.InnerText = scene.RegionInfo.RegionID.ToString();
            infoblock.AppendChild(infopiece);

            infopiece = basedoc.CreateNode(XmlNodeType.Element, "url", "");
            infopiece.InnerText = "http://" + m_hostname + ":" + m_listener_port;
            infoblock.AppendChild(infopiece);

            infopiece = basedoc.CreateNode(XmlNodeType.Element, "name", "");
            infopiece.InnerText = scene.RegionInfo.RegionName;
            infoblock.AppendChild(infopiece);

            docElement.AppendChild(infoblock);

            return docElement;
        }

        private XmlNode GetGridSnapshotData(XmlDocument factory)
        {
            XmlNode griddata = factory.CreateNode(XmlNodeType.Element, "grid", "");

            foreach (KeyValuePair<String, String> GridData in m_gridinfo)
            {
                //TODO: make it lowercase tag names for diva
                XmlNode childnode = factory.CreateNode(XmlNodeType.Element, GridData.Key, "");
                childnode.InnerText = GridData.Value;
                griddata.AppendChild(childnode);
            }

            return griddata;
        }

        private String GetRegionCategory(Scene scene)
        {
           
            //Boolean choice between:
            //  "PG" - Mormontown
            //  "Mature" - Sodom and Gomorrah
            //  (Depreciated) "Patriotic Nigra Testing Sandbox" - Abandon Hope All Ye Who Enter Here
            if ((scene.RegionInfo.EstateSettings.simAccess & Simulator.SimAccess.Mature) == Simulator.SimAccess.Mature)
            {
                return "Mature";
            }
            else if ((scene.RegionInfo.EstateSettings.simAccess & Simulator.SimAccess.PG) == Simulator.SimAccess.PG)
            {
                return "PG";
            }
            else
            {
                return "Unknown";
            }
        }

        /* Code's closed due to AIDS, See EstateSnapshot.cs for CURE
        private XmlNode GetEstateSnapshotData(Scene scene, XmlDocument factory)
        {
            //Estate data section - contains who owns a set of sims and the name of the set.
            //In Opensim all the estate names are the same as the Master Avatar (owner of the sim)
            XmlNode estatedata = factory.CreateNode(XmlNodeType.Element, "estate", "");

            LLUUID ownerid = scene.RegionInfo.MasterAvatarAssignedUUID;
            String firstname = scene.RegionInfo.MasterAvatarFirstName;
            String lastname = scene.RegionInfo.MasterAvatarLastName;
            String hostname = scene.RegionInfo.ExternalHostName;

            XmlNode user = factory.CreateNode(XmlNodeType.Element, "owner", "");

            XmlNode username = factory.CreateNode(XmlNodeType.Element, "name", "");
            username.InnerText = firstname + " " + lastname;
            user.AppendChild(username);

            XmlNode useruuid = factory.CreateNode(XmlNodeType.Element, "uuid", "");
            useruuid.InnerText = ownerid.ToString();
            user.AppendChild(useruuid);

            estatedata.AppendChild(user);

            return estatedata;
        } */

        #endregion

        #region [Public] Snapshot storage functions

        public void MakeNewSnapshot()
        {
            foreach (Scene scene in m_scenes)
            {
                XmlDocument snapshot = Snapshot(scene);

                string path = DataFileName(scene);

                try
                {
                    using (XmlTextWriter snapXWriter = new XmlTextWriter(path, Encoding.Default))
                    {
                        snapXWriter.Formatting = Formatting.Indented;
                        snapXWriter.WriteStartDocument();
                        snapshot.WriteTo(snapXWriter);
                        snapXWriter.WriteEndDocument();

                        m_lastSnapshot++;
                    }
                }
                catch (Exception e)
                {
                    m_log.Warn("[DATASNAPSHOT]: Caught unknown exception while trying to save snapshot: " + path + "\n" + e.ToString());
                }
                m_log.Info("[DATASNAPSHOT]: Made external data snapshot " + path);
            }
        }

        /**
         * Reply to the http request 
         */
        public XmlDocument GetSnapshot(string regionName)
        {
            XmlDocument requestedSnap = new XmlDocument();
            requestedSnap.AppendChild(requestedSnap.CreateXmlDeclaration("1.0", null, null));
            requestedSnap.AppendChild(requestedSnap.CreateWhitespace("\r\n"));
            XmlNode regiondata = requestedSnap.CreateNode(XmlNodeType.Element, "regiondata", "");
            try
            {
                if (regionName == null || regionName == "")
                {
                    foreach (Scene scene in m_scenes)
                    {
                        string path = DataFileName(scene);
                        XmlDocument regionSnap = new XmlDocument();
                        regionSnap.PreserveWhitespace = true;

                        regionSnap.Load(path);
                        XmlNode nodeOrig = regionSnap["region"];
                        XmlNode nodeDest = requestedSnap.ImportNode(nodeOrig, true);
                        //requestedSnap.AppendChild(nodeDest);

                        regiondata.AppendChild(requestedSnap.CreateWhitespace("\r\n"));
                        regiondata.AppendChild(nodeDest);
                    }
                }
                else
                {
                    Scene scene = SceneForName(regionName);
                    requestedSnap.Load(DataFileName(scene));
                }
                //                requestedSnap.InsertBefore(requestedSnap.CreateXmlDeclaration("1.0", null, null),
//                                           requestedSnap.DocumentElement);
                requestedSnap.AppendChild(regiondata);
                regiondata.AppendChild(requestedSnap.CreateWhitespace("\r\n"));


            }
            catch (XmlException e)
            {
                m_log.Warn("[DATASNAPSHOT]: XmlException while trying to load snapshot: " + e.ToString());
                requestedSnap = GetErrorMessage(regionName, e);
            }
            catch (Exception e)
            {
                m_log.Warn("[DATASNAPSHOT]: Caught unknown exception while trying to load snapshot: " + e.StackTrace);
                requestedSnap = GetErrorMessage(regionName, e);
            }


            return requestedSnap;
        }

        private XmlDocument GetErrorMessage(string regionName, Exception e)
        {
            XmlDocument errorMessage = new XmlDocument();
            XmlNode error = errorMessage.CreateNode(XmlNodeType.Element, "error", "");
            XmlNode region = errorMessage.CreateNode(XmlNodeType.Element, "region", "");
            region.InnerText = regionName;

            XmlNode exception = errorMessage.CreateNode(XmlNodeType.Element, "exception", "");
            exception.InnerText = e.ToString();

            error.AppendChild(region);
            error.AppendChild(exception);
            errorMessage.AppendChild(error);

            return errorMessage;
        }

        #endregion

        #region Event callbacks

        private void SnapshotTimerCallback(object timer, ElapsedEventArgs args)
        {
            MakeNewSnapshot();
            //Add extra calls here
        }

        #endregion

        #region External data services
        private void NotifyDataServices(string servicesStr)
        {
            Stream reply = null;
            string delimStr = ";";
            char [] delimiter = delimStr.ToCharArray();

            string[] services = servicesStr.Split(delimiter);

            for (int i = 0; i < services.Length; i++)
            {
                string url = services[i].Trim();
                RestClient cli = new RestClient(url);
                cli.AddQueryParameter("host", m_hostname);
                cli.AddQueryParameter("port", m_listener_port);
                cli.RequestMethod = "GET";
                try
                {
                    reply = cli.Request();
                }
                catch (WebException)
                {
                    m_log.Warn("[DATASNAPSHOT] Unable to notify " + url);
                }
                catch (Exception e)
                {
                    m_log.Warn("[DATASNAPSHOT] Ignoring unknown exception " + e.ToString());
                }
                byte[] response = new byte[1024];
                int n = 0;
                try
                {
                    n = reply.Read(response, 0, 1024);
                }
                catch (Exception e)
                {
                    m_log.Warn("[DATASNAPSHOT] Unable to decode reply from data service. Ignoring. " + e.StackTrace);
                }
                // This is not quite working, so...
                string responseStr = ASCIIEncoding.UTF8.GetString(response);
                m_log.Info("[DATASNAPSHOT] data service notified: " + url);
            }

        }
        #endregion
    }
}
