using System;
using System.Collections.Generic;
using OpenSim.Framework.Console;
using OpenSim.Framework.Types;
using libsecondlife;

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
            ForEachCurrentScene(delegate(Scene scene) { scene.SendCommandToScripts(cmdparams); });
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

        public void SetDebugPacketOnCurrentScene(LogBase log, int newDebug)
        {
            ForEachCurrentScene(delegate(Scene scene)
                        {
                            foreach (EntityBase entity in scene.Entities.Values)
                            {
                                if (entity is ScenePresence)
                                {
                                    ScenePresence scenePrescence = entity as ScenePresence;
                                    if (!scenePrescence.childAgent)
                                    {
                                        log.Error(String.Format("Packet debug for {0} {1} set to {2}",
                                                                scenePrescence.Firstname, scenePrescence.Lastname,
                                                                newDebug));

                                        scenePrescence._ControllingClient.SetDebug(newDebug);
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
                                    if (!scenePrescence.childAgent)
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

        public bool TryGetAvatar( LLUUID avatarId, out ScenePresence avatar )
        {
            foreach (Scene scene in m_localScenes)
            {
                if( scene.TryGetAvatar( avatarId, out avatar ))
                {
                    return true;
                }
            }

            avatar = null;
            return false;
        }

        public void CloseScene(Scene scene)
        {
            m_localScenes.Remove(scene);
            scene.Close();
        }
    }
}