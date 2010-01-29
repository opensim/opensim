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
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Client;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Avatar.InstantMessage
{
    public class InstantMessageModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <value>
        /// Is this module enabled?
        /// </value>
        private bool m_enabled = false;
        
        private readonly List<Scene> m_scenes = new List<Scene>();

        #region IRegionModule Members

        private IMessageTransferModule m_TransferModule = null;

        public void Initialise(Scene scene, IConfigSource config)
        {
            if (config.Configs["Messaging"] != null)
            {
                if (config.Configs["Messaging"].GetString(
                        "InstantMessageModule", "InstantMessageModule") !=
                        "InstantMessageModule")
                    return;
            }
            
            m_enabled = true;

            lock (m_scenes)
            {
                if (!m_scenes.Contains(scene))
                {
                    m_scenes.Add(scene);
                    scene.EventManager.OnClientConnect += OnClientConnect;
                    scene.EventManager.OnIncomingInstantMessage += OnGridInstantMessage;
                }
            }
        }

        void OnClientConnect(IClientCore client)
        {
            IClientIM clientIM;
            if (client.TryGet(out clientIM))
            {
                clientIM.OnInstantMessage += OnInstantMessage;
            }
        }

        public void PostInitialise()
        {
            if (!m_enabled)
                return;
            
            m_TransferModule =
                m_scenes[0].RequestModuleInterface<IMessageTransferModule>();

            if (m_TransferModule == null)
                m_log.Error("[INSTANT MESSAGE]: No message transfer module, "+
                "IM will not work!");
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "InstantMessageModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        public void OnInstantMessage(IClientAPI client, GridInstantMessage im)
        {
            byte dialog = im.dialog;

            if (dialog != (byte)InstantMessageDialog.MessageFromAgent
                && dialog != (byte)InstantMessageDialog.StartTyping
                && dialog != (byte)InstantMessageDialog.StopTyping
                && dialog != (byte)InstantMessageDialog.MessageFromObject)
            {
                return;
            }

            if (m_TransferModule != null)
            {
                m_TransferModule.SendInstantMessage(im,
                    delegate(bool success)
                    {
                        if (dialog == (uint)InstantMessageDialog.StartTyping ||
                            dialog == (uint)InstantMessageDialog.StopTyping)
                        {
                            return;
                        }

                        if ((client != null) && !success)
                        {
                            client.SendInstantMessage(
                                    new GridInstantMessage(
                                    null, new UUID(im.fromAgentID), "System",
                                    new UUID(im.toAgentID),
                                    (byte)InstantMessageDialog.BusyAutoResponse,
                                    "Unable to send instant message. "+
                                    "User is not logged in.", false,
                                    new Vector3()));
                        }
                    }
                );
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="msg"></param>
        private void OnGridInstantMessage(GridInstantMessage msg)
        {
            // Just call the Text IM handler above
            // This event won't be raised unless we have that agent,
            // so we can depend on the above not trying to send
            // via grid again
            //
            OnInstantMessage(null, msg);
        }
    }
}
