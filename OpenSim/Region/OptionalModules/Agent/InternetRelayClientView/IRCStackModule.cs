using System;
using System.Collections.Generic;
using System.Text;
using Nini.Config;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Agent.InternetRelayClientView
{
    class IRCStackModule : IRegionModule 
    {
        #region Implementation of IRegionModule

        public void Initialise(Scene scene, IConfigSource source)
        {
            throw new System.NotImplementedException();
        }

        public void PostInitialise()
        {
            throw new System.NotImplementedException();
        }

        public void Close()
        {
            throw new System.NotImplementedException();
        }

        public string Name
        {
            get { throw new System.NotImplementedException(); }
        }

        public bool IsSharedModule
        {
            get { throw new System.NotImplementedException(); }
        }

        #endregion
    }
}
