using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework.Types;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules
{
    public class AvatarFactoryModule : IAvatarFactory
    {
        public bool TryGetIntialAvatarAppearance(LLUUID avatarId, out AvatarWearable[] wearables, out byte[] visualParams)
        {
            GetDefaultAvatarAppearance(out wearables, out visualParams);
            return true;
        }

        public void Initialise(Scene scene, IConfigSource source)
        {
            scene.RegisterModuleInterface<IAvatarFactory>(this);
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
