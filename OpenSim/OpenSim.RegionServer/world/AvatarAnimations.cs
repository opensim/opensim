using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.world
{
    public class AvatarAnimations
    {

        public Dictionary<string, LLUUID> AnimsLLUUID = new Dictionary<string, LLUUID>();
        public Dictionary<LLUUID, string> AnimsNames = new Dictionary<LLUUID, string>();
        
        public AvatarAnimations()
        {
        }

        public void LoadAnims()
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW,"Avatar.cs:LoadAnims() - Loading avatar animations");

            foreach (KeyValuePair<string, LLUUID> kp in OpenSim.world.Avatar.Animations.AnimsLLUUID)
            {
                AnimsNames.Add(kp.Value, kp.Key);
            }
        }
    }
}
