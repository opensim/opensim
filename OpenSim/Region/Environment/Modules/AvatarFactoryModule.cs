using System;
using libsecondlife;
using System.Collections.Generic;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules
{
    public class AvatarFactoryModule : IAvatarFactory
    {
        private Scene m_scene = null;
        private Dictionary<LLUUID, AvatarAppearance> m_avatarsClothes = new Dictionary<LLUUID, AvatarAppearance>();

        public bool TryGetInitialAvatarAppearance(LLUUID avatarId, out AvatarWearable[] wearables,
                                                  out byte[] visualParams)
        {
            if (m_avatarsClothes.ContainsKey(avatarId))
            {
                visualParams = GetDefaultVisualParams();
                wearables = m_avatarsClothes[avatarId].IsWearing;
                return true;
            }
            else
            {
                GetDefaultAvatarAppearance(out wearables, out visualParams);
                AvatarAppearance wearing = new AvatarAppearance(wearables);
                m_avatarsClothes[avatarId] = wearing;
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
            IClientAPI clientView = (IClientAPI)sender;
            //Todo look up the assetid from the inventory cache (or something) for each itemId that is in AvatarWearingArgs
            // then store assetid and itemId and wearable type in a database
            foreach (AvatarWearingArgs.Wearable wear in e.NowWearing)
            {
                if (wear.Type < 13)
                {
                    LLUUID assetId;
                    CachedUserInfo profile = m_scene.CommsManager.UserProfileCacheService.GetUserDetails(clientView.AgentId);
                    if (profile != null)
                    {
                        InventoryItemBase baseItem = profile.RootFolder.HasItem(wear.ItemID);
                        if (baseItem != null)
                        {
                            assetId = baseItem.assetID;
                            //temporary dictionary storage. This should be storing to a database
                            if (m_avatarsClothes.ContainsKey(clientView.AgentId))
                            {
                                AvatarAppearance avWearing = m_avatarsClothes[clientView.AgentId];
                                avWearing.IsWearing[wear.Type].AssetID = assetId;
                                avWearing.IsWearing[wear.Type].ItemID = wear.ItemID;
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

        public class AvatarAppearance
        {
            public AvatarWearable[] IsWearing;
            public byte[] VisualParams;

            public AvatarAppearance()
            {
                IsWearing = new AvatarWearable[13];
                for (int i = 0; i < 13; i++)
                {
                    IsWearing[i] = new AvatarWearable();
                }
            }

            public AvatarAppearance(AvatarWearable[] wearing)
            {
                if (wearing.Length == 13)
                {
                    IsWearing = new AvatarWearable[13];
                    for (int i = 0; i < 13; i++)
                    {
                        IsWearing[i] = new AvatarWearable();
                        IsWearing[i].AssetID = wearing[i].AssetID;
                        IsWearing[i].ItemID = wearing[i].ItemID;
                    }
                }
                else
                {
                    IsWearing = new AvatarWearable[13];
                    for (int i = 0; i < 13; i++)
                    {
                        IsWearing[i] = new AvatarWearable();
                    }
                }
            }
        }
    }
}
