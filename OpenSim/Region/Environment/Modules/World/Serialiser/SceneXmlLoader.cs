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
using System.Xml;
using Axiom.Math;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Environment.Scenes
{
    /// <summary>
    /// Static methods to serialize and deserialize scene objects to and from XML
    /// </summary>
    public class SceneXmlLoader
    {
        public static void LoadPrimsFromXml(Scene scene, string fileName, bool newIDS, LLVector3 loadOffset)
        {
            XmlDocument doc = new XmlDocument();
            XmlNode rootNode;
            int primCount = 0;
            if (fileName.StartsWith("http:") || File.Exists(fileName))
            {
                XmlTextReader reader = new XmlTextReader(fileName);
                reader.WhitespaceHandling = WhitespaceHandling.None;
                doc.Load(reader);
                reader.Close();
                rootNode = doc.FirstChild;
                foreach (XmlNode aPrimNode in rootNode.ChildNodes)
                {
                    SceneObjectGroup obj = new SceneObjectGroup(scene, scene.RegionInfo.RegionHandle, aPrimNode.OuterXml);

                    if (newIDS)
                    {
                        obj.ResetIDs();
                    }
                    //if we want this to be a import method then we need new uuids for the object to avoid any clashes
                    //obj.RegenerateFullIDs();

                    scene.AddSceneObject(obj, true);

                    SceneObjectPart rootPart = obj.GetChildPart(obj.UUID);
                    // Apply loadOffsets for load/import and move combinations
                    rootPart.GroupPosition = rootPart.AbsolutePosition + loadOffset;
                    bool UsePhysics = (((rootPart.GetEffectiveObjectFlags() & (uint) LLObject.ObjectFlags.Physics) > 0) &&
                                       scene.m_physicalPrim);
                    if ((rootPart.GetEffectiveObjectFlags() & (uint) LLObject.ObjectFlags.Phantom) == 0)
                    {
                        rootPart.PhysActor = scene.PhysicsScene.AddPrimShape(
                            rootPart.Name,
                            rootPart.Shape,
                            new PhysicsVector(rootPart.AbsolutePosition.X + loadOffset.X,
                                              rootPart.AbsolutePosition.Y + loadOffset.Y,
                                              rootPart.AbsolutePosition.Z + loadOffset.Z),
                            new PhysicsVector(rootPart.Scale.X, rootPart.Scale.Y, rootPart.Scale.Z),
                            new Quaternion(rootPart.RotationOffset.W, rootPart.RotationOffset.X,
                                           rootPart.RotationOffset.Y, rootPart.RotationOffset.Z), UsePhysics);

                        // to quote from SceneObjectPart: Basic
                        // Physics returns null..  joy joy joy.
                        if (rootPart.PhysActor != null)
                        {
                            rootPart.PhysActor.LocalID = rootPart.LocalId;
                            rootPart.DoPhysicsPropertyUpdate(UsePhysics, true);
                        }
                    }
                    primCount++;
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

            List<EntityBase> EntityList = scene.GetEntities();

            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    stream.WriteLine(((SceneObjectGroup) ent).ToXmlString());
                    primCount++;
                }
            }
            stream.WriteLine("</scene>\n");
            stream.Close();
            file.Close();
        }

        public static string SavePrimGroupToXML2String(SceneObjectGroup grp)
        {
            string returnstring = "";
            returnstring += "<scene>\n";
            returnstring += grp.ToXmlString2();
            returnstring += "</scene>\n";
            return returnstring;
        }

        public static void LoadGroupFromXml2String(Scene scene, string xmlString)
        {
            XmlDocument doc = new XmlDocument();
            XmlNode rootNode;

            XmlTextReader reader = new XmlTextReader(new StringReader(xmlString));
            reader.WhitespaceHandling = WhitespaceHandling.None;
            doc.Load(reader);
            reader.Close();
            rootNode = doc.FirstChild;
            foreach (XmlNode aPrimNode in rootNode.ChildNodes)
            {
                CreatePrimFromXml2(scene, aPrimNode.OuterXml);
            }
        }

        /// <summary>
        /// Load prims from the xml2 format
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="fileName"></param>
        public static void LoadPrimsFromXml2(Scene scene, string fileName)
        {
            LoadPrimsFromXml2(scene, new XmlTextReader(fileName));
        }

        /// <summary>
        /// Load prims from the xml2 format
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="reader"></param>
        public static void LoadPrimsFromXml2(Scene scene, TextReader reader)
        {
            LoadPrimsFromXml2(scene, new XmlTextReader(reader));
        }

        /// <summary>
        /// Load prims from the xml2 format.  This method will close the reader
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="reader"></param>
        protected static void LoadPrimsFromXml2(Scene scene, XmlTextReader reader)
        {
            XmlDocument doc = new XmlDocument();
            reader.WhitespaceHandling = WhitespaceHandling.None;
            doc.Load(reader);
            reader.Close();
            XmlNode rootNode = doc.FirstChild;

            foreach (XmlNode aPrimNode in rootNode.ChildNodes)
            {
                CreatePrimFromXml2(scene, aPrimNode.OuterXml);
            }
        }

        /// <summary>
        /// Create a prim from the xml2 representation.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="xmlData"></param>
        protected static void CreatePrimFromXml2(Scene scene, string xmlData)
        {
            SceneObjectGroup obj = new SceneObjectGroup(xmlData);            
            
            LLVector3 receivedVelocity = obj.RootPart.Velocity;
            //System.Console.WriteLine(obj.RootPart.Velocity.ToString());
            scene.AddSceneObjectFromStorage(obj);

            SceneObjectPart rootPart = obj.GetChildPart(obj.UUID);
            bool UsePhysics = (((rootPart.GetEffectiveObjectFlags() & (uint) LLObject.ObjectFlags.Physics) > 0) &&
                               scene.m_physicalPrim);
            if ((rootPart.GetEffectiveObjectFlags() & (uint) LLObject.ObjectFlags.Phantom) == 0)
            {
                rootPart.PhysActor = scene.PhysicsScene.AddPrimShape(
                    rootPart.Name,
                    rootPart.Shape,
                    new PhysicsVector(rootPart.AbsolutePosition.X, rootPart.AbsolutePosition.Y,
                                      rootPart.AbsolutePosition.Z),
                    new PhysicsVector(rootPart.Scale.X, rootPart.Scale.Y, rootPart.Scale.Z),
                    new Quaternion(rootPart.RotationOffset.W, rootPart.RotationOffset.X,
                                   rootPart.RotationOffset.Y, rootPart.RotationOffset.Z), UsePhysics);

                // to quote from SceneObjectPart: Basic
                // Physics returns null..  joy joy joy.
                if (rootPart.PhysActor != null)
                {
                    rootPart.PhysActor.LocalID = rootPart.LocalId;
                    rootPart.DoPhysicsPropertyUpdate(UsePhysics, true);
                }
                rootPart.Velocity = receivedVelocity;
            }

            obj.ScheduleGroupForFullUpdate();
        }

        public static void SavePrimsToXml2(Scene scene, string fileName)
        {
            FileStream file = new FileStream(fileName, FileMode.Create);
            StreamWriter stream = new StreamWriter(file);
            int primCount = 0;
            stream.WriteLine("<scene>\n");

            List<EntityBase> EntityList = scene.GetEntities();

            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    stream.WriteLine(((SceneObjectGroup) ent).ToXmlString2());
                    primCount++;
                }
            }
            stream.WriteLine("</scene>\n");
            stream.Close();
            file.Close();
        }
    }
}
