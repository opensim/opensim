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
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using log4net;
using OpenSim.Region.DataSnapshot.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.DataSnapshot
{
    public class SnapshotStore
    {
        #region Class Members
        private String m_directory = "unyuu"; //not an attempt at adding RM references to core SVN, honest
        private Dictionary<Scene, bool> m_scenes = null;
        private List<IDataSnapshotProvider> m_providers = null;
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private Dictionary<String, String> m_gridinfo = null;
        private bool m_cacheEnabled = true;
        private string m_listener_port = "9000"; //TODO: Set default port over 9000
        private string m_hostname = "127.0.0.1";
        #endregion

        public SnapshotStore(string directory, Dictionary<String, String> gridinfo, string port, string hostname) {
            m_directory = directory;
            m_scenes = new Dictionary<Scene, bool>();
            m_providers = new List<IDataSnapshotProvider>();
            m_gridinfo = gridinfo;
            m_listener_port = port;
            m_hostname = hostname;

            if (Directory.Exists(m_directory))
            {
                m_log.Info("[DATASNAPSHOT]: Response and fragment cache directory already exists.");
            }
            else
            {
                // Try to create the directory.
                m_log.Info("[DATASNAPSHOT]: Creating directory " + m_directory);
                try
                {
                    Directory.CreateDirectory(m_directory);
                }
                catch (Exception e)
                {
                    m_log.Error("[DATASNAPSHOT]: Failed to create directory " + m_directory, e);

                    //This isn't a horrible problem, just disable cacheing.
                    m_cacheEnabled = false;
                    m_log.Error("[DATASNAPSHOT]: Could not create directory, response cache has been disabled.");
                }
            }
        }

        public void ForceSceneStale(Scene scene) {
            m_scenes[scene] = true;
        }

        #region Fragment storage
        public XmlNode GetFragment(IDataSnapshotProvider provider, XmlDocument factory)
        {
            XmlNode data = null;

            if (provider.Stale || !m_cacheEnabled)
            {
                data = provider.RequestSnapshotData(factory);

                if (m_cacheEnabled)
                {
                    String path = DataFileNameFragment(provider.GetParentScene, provider.Name);

                    try
                    {
                        using (XmlTextWriter snapXWriter = new XmlTextWriter(path, Encoding.Default))
                        {
                            snapXWriter.Formatting = Formatting.Indented;
                            snapXWriter.WriteStartDocument();
                            data.WriteTo(snapXWriter);
                            snapXWriter.WriteEndDocument();
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.WarnFormat("[DATASNAPSHOT]: Exception on writing to file {0}: {1}", path, e.Message);
                    }

                }

                //mark provider as not stale, parent scene as stale
                provider.Stale = false;
                m_scenes[provider.GetParentScene] = true;

                m_log.Debug("[DATASNAPSHOT]: Generated fragment response for provider type " + provider.Name);
            }
            else
            {
                String path = DataFileNameFragment(provider.GetParentScene, provider.Name);

                XmlDocument fragDocument = new XmlDocument();
                fragDocument.PreserveWhitespace = true;
                fragDocument.Load(path);
                foreach (XmlNode node in fragDocument)
                {
                    data = factory.ImportNode(node, true);
                }

                m_log.Debug("[DATASNAPSHOT]: Retrieved fragment response for provider type " + provider.Name);
            }

            return data;
        }
        #endregion

        #region Response storage
        public XmlNode GetScene(Scene scene, XmlDocument factory)
        {
            m_log.Debug("[DATASNAPSHOT]: Data requested for scene " + scene.RegionInfo.RegionName);

            if (!m_scenes.ContainsKey(scene)) {
                m_scenes.Add(scene, true); //stale by default
            }

            XmlNode regionElement = null;

            if (!m_scenes[scene])
            {
                m_log.Debug("[DATASNAPSHOT]: Attempting to retrieve snapshot from cache.");
                //get snapshot from cache
                String path = DataFileNameScene(scene);

                XmlDocument fragDocument = new XmlDocument();
                fragDocument.PreserveWhitespace = true;

                fragDocument.Load(path);

                foreach (XmlNode node in fragDocument)
                {
                    regionElement = factory.ImportNode(node, true);
                }

                m_log.Debug("[DATASNAPSHOT]: Obtained snapshot from cache for " + scene.RegionInfo.RegionName);
            }
            else
            {
                m_log.Debug("[DATASNAPSHOT]: Attempting to generate snapshot.");
                //make snapshot
                regionElement = MakeRegionNode(scene, factory);

                regionElement.AppendChild(GetGridSnapshotData(factory));
                XmlNode regionData = factory.CreateNode(XmlNodeType.Element, "data", "");

                foreach (IDataSnapshotProvider dataprovider in m_providers)
                {
                    if (dataprovider.GetParentScene == scene)
                    {
                        regionData.AppendChild(GetFragment(dataprovider, factory));
                    }
                }

                regionElement.AppendChild(regionData);

                factory.AppendChild(regionElement);

                //save snapshot
                String path = DataFileNameScene(scene);

                try
                {
                    using (XmlTextWriter snapXWriter = new XmlTextWriter(path, Encoding.Default))
                    {
                        snapXWriter.Formatting = Formatting.Indented;
                        snapXWriter.WriteStartDocument();
                        regionElement.WriteTo(snapXWriter);
                        snapXWriter.WriteEndDocument();
                    }
                }
                catch (Exception e)
                {
                    m_log.WarnFormat("[DATASNAPSHOT]: Exception on writing to file {0}: {1}", path, e.Message);
                }

                m_scenes[scene] = false;

                m_log.Debug("[DATASNAPSHOT]: Generated new snapshot for " + scene.RegionInfo.RegionName);
            }

            return regionElement;
        }

        #endregion

        #region Helpers
        private string DataFileNameFragment(Scene scene, String fragmentName)
        {
            return Path.Combine(m_directory, Path.ChangeExtension(Sanitize(scene.RegionInfo.RegionName + "_" + fragmentName), "xml"));
        }

        private string DataFileNameScene(Scene scene)
        {
            return Path.Combine(m_directory, Path.ChangeExtension(Sanitize(scene.RegionInfo.RegionName), "xml"));
            //return (m_snapsDir + Path.DirectorySeparatorChar + scene.RegionInfo.RegionName + ".xml");
        }

        private static string Sanitize(string name)
        {
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string invalidReStr = string.Format(@"[{0}]", invalidChars);
            string newname = Regex.Replace(name, invalidReStr, "_");
            return newname.Replace('.', '_');
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

            infopiece = basedoc.CreateNode(XmlNodeType.Element, "handle", "");
            infopiece.InnerText = scene.RegionInfo.RegionHandle.ToString();
            infoblock.AppendChild(infopiece);

            docElement.AppendChild(infoblock);

            m_log.Debug("[DATASNAPSHOT]: Generated region node");
            return docElement;
        }

        private String GetRegionCategory(Scene scene)
        {
            if (scene.RegionInfo.RegionSettings.Maturity == 0)
                return "PG";

            if (scene.RegionInfo.RegionSettings.Maturity == 1)
                return "Mature";

            if (scene.RegionInfo.RegionSettings.Maturity == 2)
                return "Adult";

            return "Unknown";
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

            m_log.Debug("[DATASNAPSHOT]: Got grid snapshot data");

            return griddata;
        }
        #endregion

        #region Manage internal collections
        public void AddScene(Scene newScene)
        {
            m_scenes.Add(newScene, true);
        }

        public void RemoveScene(Scene deadScene)
        {
            m_scenes.Remove(deadScene);
        }

        public void AddProvider(IDataSnapshotProvider newProvider)
        {
            m_providers.Add(newProvider);
        }

        public void RemoveProvider(IDataSnapshotProvider deadProvider)
        {
            m_providers.Remove(deadProvider);
        }
        #endregion
    }
}
