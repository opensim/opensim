using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Examples.SimpleModule
{
    public class RegionModule : IRegionModule
    {
        #region IRegionModule Members

        private Scene m_scene;


        public void Initialise(Scene scene, IConfigSource source)
        {
            m_scene = scene;
        }

        public void PostInitialise()
        {
            RegionInfo regionInfo = m_scene.RegionInfo;

            LLVector3 pos = new LLVector3(110, 129, 27);

            AddCpuCounter(regionInfo, pos);
            AddComplexObjects(regionInfo, pos);
            AddAvatars();
            AddFileSystemObjects();
        }

        private void AddFileSystemObjects()
        {
            DirectoryInfo dirInfo = new DirectoryInfo(".");

            float x = 0;
            float z = 0;

            foreach (FileInfo fileInfo in dirInfo.GetFiles())
            {
                LLVector3 filePos = new LLVector3(100 + x, 129, 27 + z);
                x = x + 2;
                if (x > 50)
                {
                    x = 0;
                    z = z + 2;
                }

                FileSystemObject fileObject = new FileSystemObject(m_scene, fileInfo, filePos);
                m_scene.AddEntity(fileObject);
            }
        }

        private void AddAvatars()
        {
            for (int i = 0; i < 2; i++)
            {
                MyNpcCharacter m_character = new MyNpcCharacter(m_scene.EventManager);
                m_scene.AddNewClient(m_character, false);
            }

            List<ScenePresence> avatars = m_scene.GetAvatars();
            foreach (ScenePresence avatar in avatars)
            {
                avatar.AbsolutePosition =
                    new LLVector3((float)Util.RandomClass.Next(100, 200), (float)Util.RandomClass.Next(30, 200), 2);
            }
        }

        private void AddComplexObjects(RegionInfo regionInfo, LLVector3 pos)
        {
            int objs = 3;

            for (int i = 0; i < (objs*objs*objs); i++)
            {
                LLVector3 posOffset = new LLVector3((i % objs) * 4, ((i % (objs*objs)) / ( objs )) * 4, (i / (objs*objs)) * 4);
                ComplexObject complexObject =
                    new ComplexObject(m_scene, regionInfo.RegionHandle, LLUUID.Zero, m_scene.PrimIDAllocate(),
                                      pos + posOffset);
                m_scene.AddEntity(complexObject);
            }
        }

        private void AddCpuCounter(RegionInfo regionInfo, LLVector3 pos)
        {
            SceneObjectGroup sceneObject =
                new CpuCounterObject(m_scene, regionInfo.RegionHandle, LLUUID.Zero, m_scene.PrimIDAllocate(),
                                     pos + new LLVector3(1f, 1f, 1f));
            m_scene.AddEntity(sceneObject);
        }

        public void Close()
        {
            m_scene = null;
        }

        public string Name
        {
            get { return GetType().AssemblyQualifiedName; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        #endregion
    }
}
