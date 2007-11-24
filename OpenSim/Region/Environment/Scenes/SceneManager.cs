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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
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
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Framework.Console;

namespace OpenSim.Region.Environment.Scenes
{
    public class SceneManager
    {
        private readonly List<Scene> m_localScenes;
        private Scene m_currentScene = null;

        public Scene CurrentScene
        {
            get { return m_currentScene; }
        }

        public Scene CurrentOrFirstScene
        {
            get
            {
                if (m_currentScene == null)
                {
                    return m_localScenes[0];
                }
                else
                {
                    return m_currentScene;
                }
            }
        }

        public SceneManager()
        {
            m_localScenes = new List<Scene>();
        }

        public void Close()
        {
            for (int i = 0; i < m_localScenes.Count; i++)
            {
                m_localScenes[i].Close();
            }
        }

        public void Add(Scene scene)
        {
            m_localScenes.Add(scene);
        }

        public void SaveCurrentSceneToXml(string filename)
        {
            CurrentOrFirstScene.SavePrimsToXml(filename);
        }

        public void LoadCurrentSceneFromXml(string filename)
        {
            CurrentOrFirstScene.LoadPrimsFromXml(filename);
        }

        public void SaveCurrentSceneToXml2(string filename)
        {
            CurrentOrFirstScene.SavePrimsToXml2(filename);
        }

        public void LoadCurrentSceneFromXml2(string filename)
        {
            CurrentOrFirstScene.LoadPrimsFromXml2(filename);
        }

        public bool RunTerrainCmdOnCurrentScene(string[] cmdparams, ref string result)
        {
            if (m_currentScene == null)
            {
                bool success = true;
                foreach (Scene scene in m_localScenes)
                {
                    if (!scene.Terrain.RunTerrainCmd(cmdparams, ref result, scene.RegionInfo.RegionName))
                    {
                        success = false;
                    }
                }

                return success;
            }
            else
            {
                return m_currentScene.Terrain.RunTerrainCmd(cmdparams, ref result, m_currentScene.RegionInfo.RegionName);
            }
        }

        public void SendCommandToCurrentSceneScripts(string[] cmdparams)
        {
            ForEachCurrentScene(delegate(Scene scene) { scene.SendCommandToPlugins(cmdparams); });
        }

        public void SetBypassPermissionsOnCurrentScene(bool bypassPermissions)
        {
            ForEachCurrentScene(delegate(Scene scene) { scene.PermissionsMngr.BypassPermissions = bypassPermissions; });
        }

        private void ForEachCurrentScene(Action<Scene> func)
        {
            if (m_currentScene == null)
            {
                m_localScenes.ForEach(func);
            }
            else
            {
                func(m_currentScene);
            }
        }

        public void BackupCurrentScene()
        {
            ForEachCurrentScene(delegate(Scene scene) { scene.Backup(); });
        }
        
        public void HandleAlertCommandOnCurrentScene(string[] cmdparams)
        {
            ForEachCurrentScene(delegate(Scene scene) { scene.HandleAlertCommand(cmdparams); });
        }

        public void SendGeneralMessage(string msg)
        {
            ForEachCurrentScene(delegate(Scene scene) { scene.SendGeneralAlert(msg); });
        }

        public bool TrySetCurrentScene(string regionName)
        {
            if ((String.Compare(regionName, "root") == 0) || (String.Compare(regionName, "..") == 0))
            {
                m_currentScene = null;
                return true;
            }
            else
            {
                Console.WriteLine("Searching for Region: '" + regionName + "'");

                foreach (Scene scene in m_localScenes)
                {
                    if (String.Compare(scene.RegionInfo.RegionName, regionName, true) == 0)
                    {
                        m_currentScene = scene;
                        return true;
                    }
                }

                return false;
            }
        }

        public bool TryGetScene(string regionName, out Scene scene)
        {
            foreach (Scene mscene in m_localScenes)
            {
                if (String.Compare(mscene.RegionInfo.RegionName, regionName, true) == 0)
                {
                    scene = mscene;
                    return true;
                }
            }
            scene = null;
            return false;
        }

        public bool TryGetScene(LLUUID regionID, out Scene scene)
        {
            foreach (Scene mscene in m_localScenes)
            {
                if (mscene.RegionInfo.RegionID == regionID)
                {
                    scene = mscene;
                    return true;
                }
            }
            scene = null;
            return false;
        }


        public void SetDebugPacketOnCurrentScene(LogBase log, int newDebug)
        {
            ForEachCurrentScene(delegate(Scene scene)
                                    {
                                        foreach (EntityBase entity in scene.Entities.Values)
                                        {
                                            if (entity is ScenePresence)
                                            {
                                                ScenePresence scenePrescence = entity as ScenePresence;
                                                if (!scenePrescence.IsChildAgent)
                                                {
                                                    log.Error(String.Format("Packet debug for {0} {1} set to {2}",
                                                                            scenePrescence.Firstname,
                                                                            scenePrescence.Lastname,
                                                                            newDebug));

                                                    scenePrescence.ControllingClient.SetDebug(newDebug);
                                                }
                                            }
                                        }
                                    });
        }

        public List<ScenePresence> GetCurrentSceneAvatars()
        {
            List<ScenePresence> avatars = new List<ScenePresence>();

            ForEachCurrentScene(delegate(Scene scene)
                                    {
                                        foreach (EntityBase entity in scene.Entities.Values)
                                        {
                                            if (entity is ScenePresence)
                                            {
                                                ScenePresence scenePrescence = entity as ScenePresence;
                                                if (!scenePrescence.IsChildAgent)
                                                {
                                                    avatars.Add(scenePrescence);
                                                }
                                            }
                                        }
                                    });

            return avatars;
        }

        public RegionInfo GetRegionInfo(ulong regionHandle)
        {
            foreach (Scene scene in m_localScenes)
            {
                if (scene.RegionInfo.RegionHandle == regionHandle)
                {
                    return scene.RegionInfo;
                }
            }

            return null;
        }

        public void SetCurrentSceneTimePhase(int timePhase)
        {
            ForEachCurrentScene(delegate(Scene scene)
                                    {
                                        scene.SetTimePhase(
                                            timePhase)
                                            ;
                                    });
        }


        public void ForceCurrentSceneClientUpdate()
        {
            ForEachCurrentScene(delegate(Scene scene) { scene.ForceClientUpdate(); });
        }

        public void HandleEditCommandOnCurrentScene(string[] cmdparams)
        {
            ForEachCurrentScene(delegate(Scene scene) { scene.HandleEditCommand(cmdparams); });
        }

        public bool TryGetAvatar(LLUUID avatarId, out ScenePresence avatar)
        {
            foreach (Scene scene in m_localScenes)
            {
                if (scene.TryGetAvatar(avatarId, out avatar))
                {
                    return true;
                }
            }

            avatar = null;
            return false;
        }

        public bool TryGetAvatarsScene(LLUUID avatarId, out Scene scene)
        {
            ScenePresence avatar = null;
            foreach (Scene mScene in m_localScenes)
            {
                if (mScene.TryGetAvatar(avatarId, out avatar))
                {
                    scene = mScene;
                    return true;
                }
            }

            scene = null;
            return false;
        }


        public void CloseScene(Scene scene)
        {
            m_localScenes.Remove(scene);
            scene.Close();
        }

        public bool TryGetAvatarByName(string avatarName, out ScenePresence avatar)
        {
            foreach (Scene scene in m_localScenes)
            {
                if (scene.TryGetAvatarByName(avatarName, out avatar))
                {
                    return true;
                }
            }

            avatar = null;
            return false;
        }

        public void ForEachScene(Action<Scene> action)
        {
            m_localScenes.ForEach(action);
        }
    }
}