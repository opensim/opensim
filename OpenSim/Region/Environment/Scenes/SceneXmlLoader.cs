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
using System.Text;
using System.Xml;
using System.IO;
using libsecondlife;
using Axiom.Math;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Environment.Scenes
{
    public class SceneXmlLoader  // can move to a module?
    {
        protected InnerScene m_innerScene;
        protected RegionInfo m_regInfo;
        protected Scene m_parentScene;

        public SceneXmlLoader(Scene parentScene, InnerScene innerScene, RegionInfo regionInfo)
        {
            m_parentScene = parentScene;
            m_innerScene = innerScene;
            m_regInfo = regionInfo;
        }

        public void LoadPrimsFromXml(string fileName, bool newIDS, LLVector3 loadOffset)
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
                    SceneObjectGroup obj = new SceneObjectGroup(m_parentScene,
                                                                m_regInfo.RegionHandle, aPrimNode.OuterXml);
                    if (newIDS)
                    {
                        obj.GenerateNewIDs();
                    }
                    //if we want this to be a import method then we need new uuids for the object to avoid any clashes
                    //obj.RegenerateFullIDs(); 
                    m_innerScene.AddEntity(obj);

                    SceneObjectPart rootPart = obj.GetChildPart(obj.UUID);
                    // Apply loadOffsets for load/import and move combinations
                    rootPart.GroupPosition = rootPart.AbsolutePosition + loadOffset;
                    bool UsePhysics = (((rootPart.ObjectFlags & (uint)LLObject.ObjectFlags.Physics) > 0) && m_parentScene.m_physicalPrim);
                    if ((rootPart.ObjectFlags & (uint)LLObject.ObjectFlags.Phantom) == 0)
                    {
                        rootPart.PhysActor = m_innerScene.PhysicsScene.AddPrimShape(
                            rootPart.Name,
                            rootPart.Shape,
                            new PhysicsVector(rootPart.AbsolutePosition.X + loadOffset.X,
                                              rootPart.AbsolutePosition.Y + loadOffset.Y,
                                              rootPart.AbsolutePosition.Z + loadOffset.Z),
                            new PhysicsVector(rootPart.Scale.X, rootPart.Scale.Y, rootPart.Scale.Z),
                            new Quaternion(rootPart.RotationOffset.W, rootPart.RotationOffset.X,
                                           rootPart.RotationOffset.Y, rootPart.RotationOffset.Z), UsePhysics);
                        rootPart.DoPhysicsPropertyUpdate(UsePhysics, true);
                        
                    }
                    primCount++;
                }
            }
            else
            {
                throw new Exception("Could not open file " + fileName + " for reading");
            }
        }

        public void SavePrimsToXml(string fileName)
        {
            FileStream file = new FileStream(fileName, FileMode.Create);
            StreamWriter stream = new StreamWriter(file);
            int primCount = 0;
            stream.WriteLine("<scene>\n");

            List<EntityBase> EntityList = m_innerScene.GetEntities();

            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    stream.WriteLine(((SceneObjectGroup)ent).ToXmlString());
                    primCount++;
                }
            }
            stream.WriteLine("</scene>\n");
            stream.Close();
            file.Close();
        }

        public void LoadPrimsFromXml2(string fileName)
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
                    CreatePrimFromXml(aPrimNode.OuterXml);
                }
            }
            else
            {
                throw new Exception("Could not open file " + fileName + " for reading");
            }
        }

        public void CreatePrimFromXml(string xmlData)
        {
            SceneObjectGroup obj = new SceneObjectGroup(xmlData);
            m_innerScene.AddEntityFromStorage(obj);

            SceneObjectPart rootPart = obj.GetChildPart(obj.UUID);
            bool UsePhysics = (((rootPart.ObjectFlags & (uint)LLObject.ObjectFlags.Physics) > 0) && m_parentScene.m_physicalPrim);
            if ((rootPart.ObjectFlags & (uint)LLObject.ObjectFlags.Phantom) == 0)
            {
                rootPart.PhysActor = m_innerScene.PhysicsScene.AddPrimShape(
                    rootPart.Name,
                    rootPart.Shape,
                    new PhysicsVector(rootPart.AbsolutePosition.X, rootPart.AbsolutePosition.Y,
                                      rootPart.AbsolutePosition.Z),
                    new PhysicsVector(rootPart.Scale.X, rootPart.Scale.Y, rootPart.Scale.Z),
                    new Quaternion(rootPart.RotationOffset.W, rootPart.RotationOffset.X,
                                   rootPart.RotationOffset.Y, rootPart.RotationOffset.Z), UsePhysics);
                rootPart.DoPhysicsPropertyUpdate(UsePhysics, true);
            }
        }

        public void SavePrimsToXml2(string fileName)
        {
            FileStream file = new FileStream(fileName, FileMode.Create);
            StreamWriter stream = new StreamWriter(file);
            int primCount = 0;
            stream.WriteLine("<scene>\n");

            List<EntityBase> EntityList = m_innerScene.GetEntities();

            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    stream.WriteLine(((SceneObjectGroup)ent).ToXmlString2());
                    primCount++;
                }
            }
            stream.WriteLine("</scene>\n");
            stream.Close();
            file.Close();
        }

    }
}
