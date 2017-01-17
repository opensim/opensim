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
using System.Xml;
using OpenMetaverse;
using log4net;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.PhysicsModules.SharedBase;

namespace OpenSim.Region.Framework.Scenes.Serialization
{
    /// <summary>
    /// Static methods to serialize and deserialize scene objects to and from XML
    /// </summary>
    public class SceneXmlLoader
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region old xml format
        public static void LoadPrimsFromXml(Scene scene, string fileName, bool newIDS, Vector3 loadOffset)
        {
            XmlDocument doc = new XmlDocument();
            XmlNode rootNode;

            if (fileName.StartsWith("http:") || File.Exists(fileName))
            {
                XmlTextReader reader = new XmlTextReader(fileName);
                reader.WhitespaceHandling = WhitespaceHandling.None;
                doc.Load(reader);
                reader.Close();
                rootNode = doc.FirstChild;
                foreach (XmlNode aPrimNode in rootNode.ChildNodes)
                {
                    SceneObjectGroup obj = SceneObjectSerializer.FromOriginalXmlFormat(aPrimNode.OuterXml);

                    if (newIDS)
                    {
                        obj.ResetIDs();
                    }
                    //if we want this to be a import method then we need new uuids for the object to avoid any clashes
                    //obj.RegenerateFullIDs();

                    scene.AddNewSceneObject(obj, true);
                    obj.AggregateDeepPerms();
                }
            }
            else
            {
                throw new Exception("Could not open file " + fileName + " for reading");
            }
        }

        public static void SavePrimsToXml(Scene scene, string fileName)
        {
            FileStream file = new FileStream(fileName, FileMode.Create);
            StreamWriter stream = new StreamWriter(file);
            int primCount = 0;
            stream.WriteLine("<scene>\n");

            EntityBase[] entityList = scene.GetEntities();
            foreach (EntityBase ent in entityList)
            {
                if (ent is SceneObjectGroup)
                {
                    stream.WriteLine(SceneObjectSerializer.ToOriginalXmlFormat((SceneObjectGroup)ent));
                    primCount++;
                }
            }
            stream.WriteLine("</scene>\n");
            stream.Close();
            file.Close();
        }

        #endregion

        #region XML2 serialization

        // Called by archives (save oar)
        public static string SaveGroupToXml2(SceneObjectGroup grp, Dictionary<string, object> options)
        {
            //return SceneObjectSerializer.ToXml2Format(grp);
            using (MemoryStream mem = new MemoryStream())
            {
                using (XmlTextWriter writer = new XmlTextWriter(mem, System.Text.Encoding.UTF8))
                {
                    SceneObjectSerializer.SOGToXml2(writer, grp, options);
                    writer.Flush();

                    using (StreamReader reader = new StreamReader(mem))
                    {
                        mem.Seek(0, SeekOrigin.Begin);
                        return reader.ReadToEnd();
                    }
                }
            }
        }

        // Called by scene serializer (save xml2)
        public static void SavePrimsToXml2(Scene scene, string fileName)
        {
            EntityBase[] entityList = scene.GetEntities();
            SavePrimListToXml2(entityList, fileName);
        }

        // Called by scene serializer (save xml2)
        public static void SaveNamedPrimsToXml2(Scene scene, string primName, string fileName)
        {
            m_log.InfoFormat(
                "[SERIALISER]: Saving prims with name {0} in xml2 format for region {1} to {2}",
                primName, scene.RegionInfo.RegionName, fileName);

            EntityBase[] entityList = scene.GetEntities();
            List<EntityBase> primList = new List<EntityBase>();

            foreach (EntityBase ent in entityList)
            {
                if (ent is SceneObjectGroup)
                {
                    if (ent.Name == primName)
                    {
                        primList.Add(ent);
                    }
                }
            }

            SavePrimListToXml2(primList.ToArray(), fileName);
        }

        // Called by REST Application plugin
        public static void SavePrimsToXml2(Scene scene, TextWriter stream, Vector3 min, Vector3 max)
        {
            EntityBase[] entityList = scene.GetEntities();
            SavePrimListToXml2(entityList, stream, min, max);
        }

        // Called here only. Should be private?
        public static void SavePrimListToXml2(EntityBase[] entityList, string fileName)
        {
            FileStream file = new FileStream(fileName, FileMode.Create);
            try
            {
                StreamWriter stream = new StreamWriter(file);
                try
                {
                    SavePrimListToXml2(entityList, stream, Vector3.Zero, Vector3.Zero);
                }
                finally
                {
                    stream.Close();
                }
            }
            finally
            {
                file.Close();
            }
        }

        // Called here only. Should be private?
        public static void SavePrimListToXml2(EntityBase[] entityList, TextWriter stream, Vector3 min, Vector3 max)
        {
            XmlTextWriter writer = new XmlTextWriter(stream);

            int primCount = 0;
            stream.WriteLine("<scene>\n");

            foreach (EntityBase ent in entityList)
            {
                if (ent is SceneObjectGroup)
                {
                    SceneObjectGroup g = (SceneObjectGroup)ent;
                    if (!min.Equals(Vector3.Zero) || !max.Equals(Vector3.Zero))
                    {
                        Vector3 pos = g.RootPart.GetWorldPosition();
                        if (min.X > pos.X || min.Y > pos.Y || min.Z > pos.Z)
                            continue;
                        if (max.X < pos.X || max.Y < pos.Y || max.Z < pos.Z)
                            continue;
                    }

                    //stream.WriteLine(SceneObjectSerializer.ToXml2Format(g));
                    SceneObjectSerializer.SOGToXml2(writer, (SceneObjectGroup)ent, new Dictionary<string,object>());
                    stream.WriteLine();

                    primCount++;
                }
            }

            stream.WriteLine("</scene>\n");
            stream.Flush();
        }

        #endregion

        #region XML2 deserialization

        public static SceneObjectGroup DeserializeGroupFromXml2(string xmlString)
        {
            return SceneObjectSerializer.FromXml2Format(xmlString);
        }

        /// <summary>
        /// Load prims from the xml2 format
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="fileName"></param>
        public static void LoadPrimsFromXml2(Scene scene, string fileName)
        {
            LoadPrimsFromXml2(scene, new XmlTextReader(fileName), false);
        }

        /// <summary>
        /// Load prims from the xml2 format
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="reader"></param>
        /// <param name="startScripts"></param>
        public static void LoadPrimsFromXml2(Scene scene, TextReader reader, bool startScripts)
        {
            LoadPrimsFromXml2(scene, new XmlTextReader(reader), startScripts);
        }

        /// <summary>
        /// Load prims from the xml2 format.  This method will close the reader
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="reader"></param>
        /// <param name="startScripts"></param>
        protected static void LoadPrimsFromXml2(Scene scene, XmlTextReader reader, bool startScripts)
        {
            XmlDocument doc = new XmlDocument();
            reader.WhitespaceHandling = WhitespaceHandling.None;
            doc.Load(reader);
            reader.Close();
            XmlNode rootNode = doc.FirstChild;

            ICollection<SceneObjectGroup> sceneObjects = new List<SceneObjectGroup>();
            foreach (XmlNode aPrimNode in rootNode.ChildNodes)
            {
                SceneObjectGroup obj = DeserializeGroupFromXml2(aPrimNode.OuterXml);
                scene.AddNewSceneObject(obj, true);
                if (startScripts)
                    sceneObjects.Add(obj);
            }

            foreach (SceneObjectGroup sceneObject in sceneObjects)
            {
                 sceneObject.CreateScriptInstances(0, true, scene.DefaultScriptEngine, 0);
                 sceneObject.ResumeScripts();
            }
        }

        #endregion
    }
}