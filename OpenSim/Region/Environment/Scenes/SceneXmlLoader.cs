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

        public void LoadPrimsFromXml(string fileName, bool newIDS)
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
            foreach (EntityBase ent in m_innerScene.Entities.Values)
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
            foreach (EntityBase ent in m_innerScene.Entities.Values)
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
