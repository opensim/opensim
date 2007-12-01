using System;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules
{
    public class AvatarFactoryModule : IAvatarFactory
    {
        private Scene m_scene = null;

        public bool TryGetIntialAvatarAppearance(LLUUID avatarId, out AvatarWearable[] wearables,
                                                 out byte[] visualParams)
        {
            GetDefaultAvatarAppearance(out wearables, out visualParams);
            return true;
        }

        public void Initialise(Scene scene, IConfigSource source)
        {
            scene.RegisterModuleInterface<IAvatarFactory>(this);
           // scene.EventManager.OnNewClient += NewClient;

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
         //  client.OnAvatarNowWearing += AvatarIsWearing;
        }

        public void RemoveClient(IClientAPI client)
        {
           // client.OnAvatarNowWearing -= AvatarIsWearing;
        }

        public void AvatarIsWearing(Object sender, AvatarWearingArgs e)
        {
            IClientAPI clientView = (IClientAPI) sender;
            //Todo look up the assetid from the inventory cache (or something) for each itemId that is in AvatarWearingArgs
            // then store assetid and itemId and wearable type in a database
            foreach (AvatarWearingArgs.Wearable wear in e.NowWearing)
            {
                LLUUID assetID = m_scene.CommsManager.UserProfileCache.GetUserDetails(clientView.AgentId).RootFolder.HasItem(wear.ItemID).assetID;
            }
        }

        public static void GetDefaultAvatarAppearance(out AvatarWearable[] wearables, out byte[] visualParams)
        {
            visualParams = new byte[218];
            for (int i = 0; i < 218; i++)
            {
                visualParams[i] = 100;
            }

            wearables = AvatarWearable.DefaultWearables;
        }
    }
}