using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Interfaces;


namespace OpenSim.Region.Environment.Modules
{
    public class FriendsModule : IRegionModule
    {
        private Scene m_scene;

        public void Initialise(Scene scene)
        {
            m_scene = scene;
        }

        public void PostInitialise()
        {

        }

        public void CloseDown()
        {
        }

        public string GetName()
        {
            return "FriendsModule";
        }

        public bool IsSharedModule()
        {
            return false;
        }
    }
}
