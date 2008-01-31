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
* 
*/

using System;
using System.Collections.Generic;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules
{
    public class AvatarFactoryModule : IAvatarFactory
    {
        private Scene m_scene = null;
        private readonly Dictionary<LLUUID, AvatarAppearance> m_avatarsAppearance = new Dictionary<LLUUID, AvatarAppearance>();

        public bool TryGetAvatarAppearance(LLUUID avatarId, out AvatarAppearance appearance)
        {
            if (m_avatarsAppearance.ContainsKey(avatarId))
            {
                appearance = m_avatarsAppearance[avatarId];
                return true;
            }
            else
            {
                AvatarWearable[] wearables;
                byte[] visualParams;
                GetDefaultAvatarAppearance(out wearables, out visualParams);
                appearance = new AvatarAppearance(avatarId, wearables, visualParams);
                try
                {
                    m_avatarsAppearance[avatarId] = appearance;
                }
                catch (NullReferenceException)
                {
                    MainLog.Instance.Error("AVATAR", "Unable to load appearance for uninitialized avatar");
                }
                return true;
            }
        }

        public void Initialise(Scene scene, IConfigSource source)
        {
            scene.RegisterModuleInterface<IAvatarFactory>(this);
            scene.EventManager.OnNewClient += NewClient;

            if (m_scene == null)
            {
                m_scene = scene;
            }
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "Default Avatar Factory"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        public void NewClient(IClientAPI client)
        {
            client.OnAvatarNowWearing += AvatarIsWearing;
        }

        public void RemoveClient(IClientAPI client)
        {
            // client.OnAvatarNowWearing -= AvatarIsWearing;
        }

        public void AvatarIsWearing(Object sender, AvatarWearingArgs e)
        {
            IClientAPI clientView = (IClientAPI) sender;
            CachedUserInfo profile = m_scene.CommsManager.UserProfileCacheService.GetUserDetails(clientView.AgentId);
            if (profile != null)
            {
                if (profile.RootFolder != null)
                {
                    //Todo look up the assetid from the inventory cache for each itemId that is in AvatarWearingArgs
                    // then store assetid and itemId and wearable type in a database
                    foreach (AvatarWearingArgs.Wearable wear in e.NowWearing)
                    {
                        if (wear.Type < 13)
                        {
                            LLUUID assetId;

                            InventoryItemBase baseItem = profile.RootFolder.HasItem(wear.ItemID);
                            if (baseItem != null)
                            {
                                assetId = baseItem.assetID;
                                //temporary dictionary storage. This should be storing to a database

                                if (m_avatarsAppearance.ContainsKey(clientView.AgentId))
                                {
                                    AvatarAppearance avatAppearance = m_avatarsAppearance[clientView.AgentId];
                                    avatAppearance.Wearables[wear.Type].AssetID = assetId;
                                    avatAppearance.Wearables[wear.Type].ItemID = wear.ItemID;
                                }
                            }
                        }
                    }
                }
            }
        }

        public static void GetDefaultAvatarAppearance(out AvatarWearable[] wearables, out byte[] visualParams)
        {
            visualParams = GetDefaultVisualParams();
            wearables = AvatarWearable.DefaultWearables;
        }

        private static byte[] GetDefaultVisualParams()
        {
            byte[] visualParams;
            visualParams = new byte[218];
            for (int i = 0; i < 218; i++)
            {
                visualParams[i] = 100;
            }
            return visualParams;
        }
    }
}