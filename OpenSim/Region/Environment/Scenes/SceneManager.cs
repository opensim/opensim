using System.Collections.Generic;
using System;
using OpenSim.Framework.Console;
using OpenSim.Framework.Types;

namespace OpenSim.Region.Environment.Scenes
{
    public class SceneManager
    {
        private readonly List<Scene> m_localScenes;
        private Scene m_currentScene = null;
        public Scene CurrentScene
        {
            get
            {
                return m_currentScene;
            }
        }

        private Scene CurrentOrFirstScene
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

        public void SavePrimsToXml(string filename)
        {
            CurrentOrFirstScene.SavePrimsToXml(filename);
        }

        public void LoadPrimsFromXml(string filename)
        {
            CurrentOrFirstScene.LoadPrimsFromXml(filename);
        }

        public bool RunTerrainCmd(string[] cmdparams, ref string result)
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

        public void SendCommandToScripts(string[] cmdparams)
        {
            ForEach(delegate(Scene scene)
             {
                 scene.SendCommandToScripts(cmdparams);
             });
        }

        public void BypassPermissions(bool bypassPermissions)
        {
            ForEach(delegate(Scene scene)
                         {
                             scene.PermissionsMngr.BypassPermissions = bypassPermissions;
                         });
        }

        private void ForEach(Action<Scene> func)
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

        public void Backup()
        {
            ForEach(delegate(Scene scene)
                        {
                            scene.Backup();
                        });
        }

        public void HandleAlertCommand(string[] cmdparams)
        {
            ForEach(delegate(Scene scene)
                        {
                            scene.HandleAlertCommand(cmdparams);
                        });
        }

        public bool TrySetCurrentRegion(string regionName)
        {
            if ((String.Compare(regionName, "root") == 0) || (String.Compare(regionName, "..") == 0))
            {
                m_currentScene = null;
                return true;
            }
            else
            {
                Console.WriteLine("Searching for Region: '" + regionName + "'");
                Scene foundScene = null;

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

        public void DebugPacket(LogBase log, int newDebug)
        {
            ForEach(delegate(Scene scene)
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

                                        scenePrescence.ControllingClient.SetDebug(newDebug);
                                    }
                                }
                            }
                        });
        }

        public List<ScenePresence> GetAvatars()
        {
            List<ScenePresence> avatars = new List<ScenePresence>();

            ForEach(delegate(Scene scene)
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

        public void SetTimePhase(int timePhase)
        {
            ForEach(delegate(Scene scene)
                        {
                            scene.SetTimePhase(
                                timePhase)
                                ;
                        });
        }


        public void ForceClientUpdate()
        {
            ForEach(delegate(Scene scene)
                        {
                            scene.ForceClientUpdate();
                        });
        }

        public void HandleEditCommand(string[] cmdparams)
        {
            ForEach(delegate(Scene scene)
                        {
                            scene.HandleEditCommand(cmdparams);
                        });
        }
    }
}
