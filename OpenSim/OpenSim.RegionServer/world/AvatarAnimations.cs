using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using System.Xml;

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
            XmlTextReader reader = new XmlTextReader("data/avataranimations.xml");
            XmlDocument doc = new XmlDocument();
            doc.Load(reader);

            foreach (XmlNode nod in doc.FirstChild.ChildNodes)
            {
                if (nod.Attributes["name"] != null)
                {
                    AnimsLLUUID.Add(nod.Attributes["name"], nod.Value);
                }
            }

            reader.Close();

            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW,"Loaded " + AnimsLLUUID.Count.ToString() + " animation(s)");

            foreach (KeyValuePair<string, LLUUID> kp in OpenSim.world.Avatar.Animations.AnimsLLUUID)
            {
                AnimsNames.Add(kp.Value, kp.Key);
            }
        }
    }
}
