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

using System.Collections.Generic;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.Examples.SimpleModule
{
    /// <summary>
    /// Example region module.
    /// </summary>
    /// <remarks>
    /// This is an old and unmaintained region module which uses the old style module interface.  It is not loaded into
    /// OpenSim by default.  If you want to try enabling it, look in the bin folder of this project.
    /// Please see the README.txt in this project on the filesystem for some more information.  
    /// Nonetheless, it may contain some useful example code so has been left here for now.
    /// 
    /// You can see bare bones examples of the more modern region module system in OpenSim/Region/OptionalModules/Example
    /// </remarks>
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
            // RegionInfo regionInfo = m_scene.RegionInfo;

            // Vector3 pos = new Vector3(110, 129, 27);

            //AddCpuCounter(regionInfo, pos);
          //  AddComplexObjects(regionInfo, pos);
            AddAvatars();
          //  AddFileSystemObjects();
        }

        // private void AddFileSystemObjects()
        // {
        //     DirectoryInfo dirInfo = new DirectoryInfo(".");

        //     float x = 0;
        //     float z = 0;

        //     foreach (FileInfo fileInfo in dirInfo.GetFiles())
        //     {
        //         Vector3 filePos = new Vector3(100 + x, 129, 27 + z);
        //         x = x + 2;
        //         if (x > 50)
        //         {
        //             x = 0;
        //             z = z + 2;
        //         }

        //         FileSystemObject fileObject = new FileSystemObject(m_scene, fileInfo, filePos);
        //         m_scene.AddNewSceneObject(fileObject, true);
        //     }
        // }

        private void AddAvatars()
        {
            for (int i = 0; i < 1; i++)
            {
                MyNpcCharacter m_character = new MyNpcCharacter(m_scene);
                m_scene.AddNewClient(m_character, PresenceType.Npc);
                m_scene.AgentCrossing(m_character.AgentId, Vector3.Zero, false);
            }

            m_scene.ForEachScenePresence(delegate(ScenePresence sp)
            {
                if (!sp.IsChildAgent)
                    sp.AbsolutePosition =
                        new Vector3((float)Util.RandomClass.Next(100, 200), (float)Util.RandomClass.Next(30, 200), 2);
            });
        }

        // private void AddComplexObjects(RegionInfo regionInfo, Vector3 pos)
        // {
        //     int objs = 3;

        //     for (int i = 0; i < (objs*objs*objs); i++)
        //     {
        //         Vector3 posOffset = new Vector3((i % objs) * 4, ((i % (objs*objs)) / (objs)) * 4, (i / (objs*objs)) * 4);
        //         ComplexObject complexObject =
        //             new ComplexObject(m_scene, regionInfo.RegionHandle, UUID.Zero, pos + posOffset);
        //         m_scene.AddNewSceneObject(complexObject, true);
        //     }
        // }

        // private void AddCpuCounter(RegionInfo regionInfo, Vector3 pos)
        // {
        //     SceneObjectGroup sceneObject =
        //         new CpuCounterObject(m_scene, regionInfo.RegionHandle, UUID.Zero, pos + new Vector3(1f, 1f, 1f));
        //     m_scene.AddNewSceneObject(sceneObject, true);
        // }

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
