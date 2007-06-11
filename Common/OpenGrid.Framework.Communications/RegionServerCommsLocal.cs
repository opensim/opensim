using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;

namespace OpenGrid.Framework.Communications
{
    public class RegionServerCommsLocal : RegionServerCommsManager
    {
        public LocalBackEndServices SandManager = new LocalBackEndServices();
        public RegionServerCommsLocal()
        {
            UserServer = new UserServer.UserCommsManagerLocal(); //Local User Server
            GridServer = new GridServer.GridCommsManagerLocal(SandManager); //Locl Grid Server
            InterSims = new InterSimsCommsLocal(SandManager);
        }

       
    }
}
