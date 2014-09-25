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
using OpenSim.Region.Framework.Scenes.Animation;
using OpenSim.Services.Interfaces;
using AnimationSet = OpenSim.Region.Framework.Scenes.Animation.AnimationSet;

namespace OpenSim.Region.OptionalModules.Avatar.Animations
{
    /// <summary>
    /// A module that just holds commands for inspecting avatar animations.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "AnimationsCommandModule")]
    public class AnimationsCommandModule : ISharedRegionModule
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private List<Scene> m_scenes = new List<Scene>();

        public string Name { get { return "Animations Command Module"; } }
        
        public Type ReplaceableInterface { get { return null; } }
        
        public void Initialise(IConfigSource source)
        {
//            m_log.DebugFormat("[ANIMATIONS COMMAND MODULE]: INITIALIZED MODULE");
        }
        
        public void PostInitialise()
        {
//            m_log.DebugFormat("[ANIMATIONS COMMAND MODULE]: POST INITIALIZED MODULE");
        }
        
        public void Close()
        {
//            m_log.DebugFormat("[ANIMATIONS COMMAND MODULE]: CLOSED MODULE");
        }
        
        public void AddRegion(Scene scene)
        {
//            m_log.DebugFormat("[ANIMATIONS COMMAND MODULE]: REGION {0} ADDED", scene.RegionInfo.RegionName);
        }
        
        public void RemoveRegion(Scene scene)
        {
//            m_log.DebugFormat("[ATTACHMENTS COMMAND MODULE]: REGION {0} REMOVED", scene.RegionInfo.RegionName);
            
            lock (m_scenes)
                m_scenes.Remove(scene);
        }        
        
        public void RegionLoaded(Scene scene)
        {
//            m_log.DebugFormat("[ANIMATIONS COMMAND MODULE]: REGION {0} LOADED", scene.RegionInfo.RegionName);
            
            lock (m_scenes)
                m_scenes.Add(scene);

            scene.AddCommand(
                "Users", this, "show animations",
                "show animations [<first-name> <last-name>]",
                "Show animation information for avatars in this simulator.",
                "If no name is supplied then information for all avatars is shown.\n"
                + "Please note that for inventory animations, the animation name is the name under which the animation was originally uploaded\n"
                + ", which is not necessarily the current inventory name.",
                HandleShowAnimationsCommand);
        }

        protected void HandleShowAnimationsCommand(string module, string[] cmd)
        {
            if (cmd.Length != 2 && cmd.Length < 4)
            {
                MainConsole.Instance.OutputFormat("Usage: show animations [<first-name> <last-name>]");
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

            StringBuilder sb = new StringBuilder();

            lock (m_scenes)
            {
                foreach (Scene scene in m_scenes)
                {
                    if (targetNameSupplied)
                    {
                        ScenePresence sp = scene.GetScenePresence(optionalTargetFirstName, optionalTargetLastName);
                        if (sp != null && !sp.IsChildAgent)
                            GetAttachmentsReport(sp, sb);
                    }
                    else
                    {
                        scene.ForEachRootScenePresence(sp => GetAttachmentsReport(sp, sb));
                    }
                }
            }

            MainConsole.Instance.Output(sb.ToString());
        }

        private void GetAttachmentsReport(ScenePresence sp, StringBuilder sb)
        {
            sb.AppendFormat("Animations for {0}\n", sp.Name);

            ConsoleDisplayList cdl = new ConsoleDisplayList() { Indent = 2 };
            ScenePresenceAnimator spa = sp.Animator;
            AnimationSet anims = sp.Animator.Animations;

            string cma = spa.CurrentMovementAnimation;
            cdl.AddRow(
                "Current movement anim", 
                string.Format("{0}, {1}", DefaultAvatarAnimations.GetDefaultAnimation(cma), cma));

            UUID defaultAnimId = anims.DefaultAnimation.AnimID;
            cdl.AddRow(
                "Default anim", 
                string.Format("{0}, {1}", defaultAnimId, sp.Animator.GetAnimName(defaultAnimId)));

            UUID implicitDefaultAnimId = anims.ImplicitDefaultAnimation.AnimID;
            cdl.AddRow(
                "Implicit default anim", 
                string.Format("{0}, {1}", 
                    implicitDefaultAnimId, sp.Animator.GetAnimName(implicitDefaultAnimId)));

            cdl.AddToStringBuilder(sb);

            ConsoleDisplayTable cdt = new ConsoleDisplayTable() { Indent = 2 };
            cdt.AddColumn("Animation ID", 36);
            cdt.AddColumn("Name", 20);
            cdt.AddColumn("Seq", 3);
            cdt.AddColumn("Object ID", 36);

            UUID[] animIds;
            int[] sequenceNumbers;
            UUID[] objectIds;

            sp.Animator.Animations.GetArrays(out animIds, out sequenceNumbers, out objectIds);

            for (int i = 0; i < animIds.Length; i++)
            {
                UUID animId = animIds[i];
                string animName = sp.Animator.GetAnimName(animId);
                int seq = sequenceNumbers[i];
                UUID objectId = objectIds[i];

                cdt.AddRow(animId, animName, seq, objectId);
            }

            cdt.AddToStringBuilder(sb);
            sb.Append("\n");
        }
    }
}
