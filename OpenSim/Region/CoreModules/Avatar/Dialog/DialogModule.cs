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
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using Mono.Addins;

using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.Avatar.Dialog
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "DialogModule")]
    public class DialogModule : IDialogModule, INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected Scene m_scene;

        public void Initialise(IConfigSource source) { }

        public Type ReplaceableInterface { get { return null; } }

        public void AddRegion(Scene scene)
        {
            m_scene = scene;
            m_scene.RegisterModuleInterface<IDialogModule>(this);
        }

        public void RegionLoaded(Scene scene)
        {
            if (scene != m_scene)
                return;

            m_scene.AddCommand(
                "Users", this, "alert", "alert <message>",
                "Send an alert to everyone",
                HandleAlertConsoleCommand);

            m_scene.AddCommand(
                "Users", this, "alert-user",
                "alert-user <first> <last> <message>",
                "Send an alert to a user",
                HandleAlertConsoleCommand);
        }

        public void RemoveRegion(Scene scene)
        {
            if (scene != m_scene)
                return;

            m_scene.UnregisterModuleInterface<IDialogModule>(this);
        }

        public void Close() { }
        public string Name { get { return "Dialog Module"; } }

        public void SendAlertToUser(IClientAPI client, string message)
        {
            client?.SendAgentAlertMessage(message, false);
        }

        public void SendAlertToUser(IClientAPI client, string message, bool modal)
        {
            client?.SendAgentAlertMessage(message, modal);
        }

        public void SendAlertToUser(UUID agentID, string message)
        {
            ScenePresence sp = m_scene.GetScenePresence(agentID);
            sp?.ControllingClient.SendAgentAlertMessage(message, false);
        }

        public void SendAlertToUser(UUID agentID, string message, bool modal)
        {
            ScenePresence sp = m_scene.GetScenePresence(agentID);
            sp?.ControllingClient.SendAgentAlertMessage(message, modal);
        }

        public void SendAlertToUser(string firstName, string lastName, string message, bool modal)
        {
            ScenePresence sp= m_scene.GetScenePresence(firstName, lastName);
            sp?.ControllingClient.SendAgentAlertMessage(message, modal);
        }

        public void SendGeneralAlert(string message)
        {
            m_scene.ForEachRootClient(delegate(IClientAPI client)
            {
                client.SendAlertMessage(message);
            });
        }

        private bool GetOwnerName(UUID partID, out string ownerFirstName, out string ownerLastName)
        {
            SceneObjectPart sop = m_scene.GetSceneObjectPart(partID);
            if(sop != null)
                return sop.GetOwnerName(out ownerFirstName, out ownerLastName);

            ownerFirstName = string.Empty;
            ownerLastName = string.Empty;
            return false;
        }

        // legacy
        public void SendDialogToUser(UUID avatarID, string objectName,
                UUID objectID, UUID ownerID, string message, UUID textureID,
                int ch, string[] buttonlabels)
        {
            ScenePresence sp = m_scene.GetScenePresence(avatarID);
            if (sp != null)
            {
                if(GetOwnerName(objectID, out string ownerFirstName, out string ownerLastName))
                {
                    sp.ControllingClient.SendDialog(objectName, objectID, ownerID,
                        ownerFirstName, ownerLastName, message, textureID, ch, buttonlabels);
                }
            }
        }

        public void SendDialogToUser(UUID avatarID, string objectName,
                UUID objectID, UUID ownerID, string ownerFirstName, string ownerLastName,string message, UUID textureID,
                int ch, string[] buttonlabels)
        {
            ScenePresence sp = m_scene.GetScenePresence(avatarID);
            sp?.ControllingClient.SendDialog(objectName, objectID, ownerID,
                        ownerFirstName, ownerLastName, message, textureID, ch, buttonlabels);
        }

        public void SendUrlToUser(UUID avatarID, string objectName,
                UUID objectID, UUID ownerID, bool groupOwned, string message,
                string url)
        {
            ScenePresence sp = m_scene.GetScenePresence(avatarID);
            sp?.ControllingClient.SendLoadURL(objectName, objectID,
                        ownerID, groupOwned, message, url);
        }

        // legacy
        public void SendTextBoxToUser(UUID avatarid, string message,
                int chatChannel, string name, UUID objectid, UUID ownerID)
        {
            ScenePresence sp = m_scene.GetScenePresence(avatarid);
            if (sp != null)
            {
                if (GetOwnerName(objectid, out string ownerFirstName, out string ownerLastName))
                {
                    sp.ControllingClient.SendTextBoxRequest(message, chatChannel,
                        name, ownerID, ownerFirstName, ownerLastName, objectid);
                }
            }
        }

        public void SendTextBoxToUser(UUID avatarid, string message,
                int chatChannel, string name, UUID objectid, string ownerFirstName, string ownerLastName, UUID ownerID)
        {
            ScenePresence sp = m_scene.GetScenePresence(avatarid);
            sp?.ControllingClient.SendTextBoxRequest(message, chatChannel,
                        name, ownerID, ownerFirstName, ownerLastName,
                        objectid);
        }

        public void SendNotificationToUsersInRegion(UUID fromAvatarID, string fromAvatarName, string message)
        {
            m_scene.ForEachRootClient(delegate(IClientAPI client)
            {
                client.SendAgentAlertMessage(message, false);
            });
        }

        /// <summary>
        /// Handle an alert command from the console.
        /// </summary>
        /// <param name="module"></param>
        /// <param name="cmdparams"></param>
        public void HandleAlertConsoleCommand(string module, string[] cmdparams)
        {
            if (m_scene.ConsoleScene() != null && m_scene.ConsoleScene() != m_scene)
            {
                return;
            }

            string message = string.Empty;

            if (cmdparams[0].ToLower().Equals("alert"))
            {
                message = CombineParams(cmdparams, 1);
                m_log.InfoFormat("[DIALOG]: Sending general alert in region {0} with message {1}",
                        m_scene.RegionInfo.RegionName, message);
                SendGeneralAlert(message);
            }
            else if (cmdparams.Length > 3)
            {
                string firstName = cmdparams[1];
                string lastName = cmdparams[2];
                message = CombineParams(cmdparams, 3);
                m_log.InfoFormat("[DIALOG]: Sending alert in region {0} to {1} {2} with message {3}",
                        m_scene.RegionInfo.RegionName, firstName, lastName,
                        message);
                SendAlertToUser(firstName, lastName, message, false);
            }
            else
            {
                MainConsole.Instance.Output(
                    "Usage: alert <message> | alert-user <first> <last> <message>");
                return;
            }
        }

        private string CombineParams(string[] commandParams, int pos)
        {
            string result = string.Empty;
            for (int i = pos; i < commandParams.Length; i++)
            {
                result += commandParams[i] + " ";
            }

            return result;
        }
    }
}
