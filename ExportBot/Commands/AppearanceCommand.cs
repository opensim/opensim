using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class AppearanceCommand : Command
    {
        Utilities.Assets.AssetManager Assets;
        Utilities.Appearance.AppearanceManager Appearance;

		public AppearanceCommand(TestClient testClient)
        {
            Name = "appearance";
            Description = "Set your current appearance to your last saved appearance";

            Assets = new libsecondlife.Utilities.Assets.AssetManager(testClient);
            Appearance = new libsecondlife.Utilities.Appearance.AppearanceManager(testClient, Assets);
        }

        public override string Execute(string[] args, LLUUID fromAgentID)
        {
            Appearance.SetPreviousAppearance();
            return "Done.";
        }
    }
}
