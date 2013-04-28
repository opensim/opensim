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
using System.Linq;
using System.Reflection;
using System.Text;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.ClientStack.LindenUDP;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Avatar.Appearance
{
    /// <summary>
    /// A module that just holds commands for inspecting avatar appearance.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "AppearanceInfoModule")]
    public class AppearanceInfoModule : ISharedRegionModule
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Dictionary<UUID, Scene> m_scenes = new Dictionary<UUID, Scene>();
//        private IAvatarFactoryModule m_avatarFactory;
        
        public string Name { get { return "Appearance Information Module"; } }        
        
        public Type ReplaceableInterface { get { return null; } }
        
        public void Initialise(IConfigSource source)
        {
//            m_log.DebugFormat("[APPEARANCE INFO MODULE]: INITIALIZED MODULE");
        }
        
        public void PostInitialise()
        {
//            m_log.DebugFormat("[APPEARANCE INFO MODULE]: POST INITIALIZED MODULE");
        }
        
        public void Close()
        {
//            m_log.DebugFormat("[APPEARANCE INFO MODULE]: CLOSED MODULE");
        }
        
        public void AddRegion(Scene scene)
        {
//            m_log.DebugFormat("[APPEARANCE INFO MODULE]: REGION {0} ADDED", scene.RegionInfo.RegionName);                                     
        }
        
        public void RemoveRegion(Scene scene)
        {
//            m_log.DebugFormat("[APPEARANCE INFO MODULE]: REGION {0} REMOVED", scene.RegionInfo.RegionName);
            
            lock (m_scenes)
                m_scenes.Remove(scene.RegionInfo.RegionID);
        }        
        
        public void RegionLoaded(Scene scene)
        {
//            m_log.DebugFormat("[APPEARANCE INFO MODULE]: REGION {0} LOADED", scene.RegionInfo.RegionName);
            
            lock (m_scenes)
                m_scenes[scene.RegionInfo.RegionID] = scene;

            scene.AddCommand(
                "Users", this, "show appearance",
                "show appearance [<first-name> <last-name>]",
                "Synonym for 'appearance show'",
                HandleShowAppearanceCommand);
            
            scene.AddCommand(
                "Users", this, "appearance show",
                "appearance show [<first-name> <last-name>]",
                "Show appearance information for each avatar in the simulator.",
                "This command checks whether the simulator has all the baked textures required to display an avatar to other viewers.  "
                    + "\nIf not, then appearance is 'corrupt' and other avatars will continue to see it as a cloud."
                    + "\nOptionally, you can view just a particular avatar's appearance information."
                    + "\nIn this case, the texture UUID for each bake type is also shown and whether the simulator can find the referenced texture.",
                HandleShowAppearanceCommand);

            scene.AddCommand(
                "Users", this, "appearance send",
                "appearance send [<first-name> <last-name>]",
                "Send appearance data for each avatar in the simulator to other viewers.",
                "Optionally, you can specify that only a particular avatar's appearance data is sent.",
                HandleSendAppearanceCommand);

            scene.AddCommand(
                "Users", this, "appearance rebake",
                "appearance rebake <first-name> <last-name>",
                "Send a request to the user's viewer for it to rebake and reupload its appearance textures.",
                "This is currently done for all baked texture references previously received, whether the simulator can find the asset or not."
                    + "\nThis will only work for texture ids that the viewer has already uploaded."
                    + "\nIf the viewer has not yet sent the server any texture ids then nothing will happen"
                    + "\nsince requests can only be made for ids that the client has already sent us",
                HandleRebakeAppearanceCommand);

            scene.AddCommand(
                "Users", this, "appearance find",
                "appearance find <uuid-or-start-of-uuid>",
                "Find out which avatar uses the given asset as a baked texture, if any.",
                "You can specify just the beginning of the uuid, e.g. 2008a8d.  A longer UUID must be in dashed format.",
                HandleFindAppearanceCommand);
        }

        private void HandleSendAppearanceCommand(string module, string[] cmd)
        {
            if (cmd.Length != 2 && cmd.Length < 4)
            {
                MainConsole.Instance.OutputFormat("Usage: appearance send [<first-name> <last-name>]");
                return;
            }

            bool targetNameSupplied = false;
            string optionalTargetFirstName = null;
            string optionalTargetLastName = null;

            if (cmd.Length >= 4)
            {
                targetNameSupplied = true;
                optionalTargetFirstName = cmd[2];
                optionalTargetLastName = cmd[3];
            }

            lock (m_scenes)
            {
                foreach (Scene scene in m_scenes.Values)
                {
                    if (targetNameSupplied)
                    {
                        ScenePresence sp = scene.GetScenePresence(optionalTargetFirstName, optionalTargetLastName);
                        if (sp != null && !sp.IsChildAgent)
                        {
                            MainConsole.Instance.OutputFormat(
                                "Sending appearance information for {0} to all other avatars in {1}",
                                sp.Name, scene.RegionInfo.RegionName);

                            scene.AvatarFactory.SendAppearance(sp.UUID);
                        }
                    }
                    else
                    {
                        scene.ForEachRootScenePresence(
                            sp =>
                            {
                                MainConsole.Instance.OutputFormat(
                                    "Sending appearance information for {0} to all other avatars in {1}",
                                    sp.Name, scene.RegionInfo.RegionName);

                                scene.AvatarFactory.SendAppearance(sp.UUID);
                            }
                        );
                    }
                }
            }
        }

        protected void HandleShowAppearanceCommand(string module, string[] cmd)
        {
            if (cmd.Length != 2 && cmd.Length < 4)
            {
                MainConsole.Instance.OutputFormat("Usage: appearance show [<first-name> <last-name>]");
                return;
            }

            bool targetNameSupplied = false;
            string optionalTargetFirstName = null;
            string optionalTargetLastName = null;

            if (cmd.Length >= 4)
            {
                targetNameSupplied = true;
                optionalTargetFirstName = cmd[2];
                optionalTargetLastName = cmd[3];
            }

            lock (m_scenes)
            {   
                foreach (Scene scene in m_scenes.Values)
                {
                    if (targetNameSupplied)
                    {
                        ScenePresence sp = scene.GetScenePresence(optionalTargetFirstName, optionalTargetLastName);
                        if (sp != null && !sp.IsChildAgent)
                            scene.AvatarFactory.WriteBakedTexturesReport(sp, MainConsole.Instance.OutputFormat);
                    }
                    else
                    {
                        scene.ForEachRootScenePresence(
                            sp =>
                            {
                                bool bakedTextureValid = scene.AvatarFactory.ValidateBakedTextureCache(sp);
                                MainConsole.Instance.OutputFormat(
                                    "{0} baked appearance texture is {1}", sp.Name, bakedTextureValid ? "OK" : "incomplete");
                            }
                        );
                    }
                }
            }
        }

        private void HandleRebakeAppearanceCommand(string module, string[] cmd)
        {
            if (cmd.Length != 4)
            {
                MainConsole.Instance.OutputFormat("Usage: appearance rebake <first-name> <last-name>");
                return;
            }

            string firstname = cmd[2];
            string lastname = cmd[3];

            lock (m_scenes)
            {
                foreach (Scene scene in m_scenes.Values)
                {
                    ScenePresence sp = scene.GetScenePresence(firstname, lastname);
                    if (sp != null && !sp.IsChildAgent)
                    {
                        int rebakesRequested = scene.AvatarFactory.RequestRebake(sp, false);

                        if (rebakesRequested > 0)
                            MainConsole.Instance.OutputFormat(
                                "Requesting rebake of {0} uploaded textures for {1} in {2}",
                                rebakesRequested, sp.Name, scene.RegionInfo.RegionName);
                        else
                            MainConsole.Instance.OutputFormat(
                                "No texture IDs available for rebake request for {0} in {1}",
                                sp.Name, scene.RegionInfo.RegionName);
                    }
                }
            }
        }

        protected void HandleFindAppearanceCommand(string module, string[] cmd)
        {
            if (cmd.Length != 3)
            {
                MainConsole.Instance.OutputFormat("Usage: appearance find <uuid-or-start-of-uuid>");
                return;
            }

            string rawUuid = cmd[2];

            HashSet<ScenePresence> matchedAvatars = new HashSet<ScenePresence>();

            lock (m_scenes)
            {
                foreach (Scene scene in m_scenes.Values)
                {
                    scene.ForEachRootScenePresence(
                        sp =>
                        {
                            Dictionary<BakeType, Primitive.TextureEntryFace> bakedFaces = scene.AvatarFactory.GetBakedTextureFaces(sp.UUID);
                            foreach (Primitive.TextureEntryFace face in bakedFaces.Values)
                            {
                                if (face != null && face.TextureID.ToString().StartsWith(rawUuid))
                                    matchedAvatars.Add(sp);
                            }
                        });
                }
            }

            if (matchedAvatars.Count == 0)
            {
                MainConsole.Instance.OutputFormat("{0} did not match any baked avatar textures in use", rawUuid);
            }
            else
            {
                MainConsole.Instance.OutputFormat(
                    "{0} matched {1}",
                    rawUuid,
                    string.Join(", ", matchedAvatars.ToList().ConvertAll<string>(sp => sp.Name).ToArray()));
            }
        }
    }
}