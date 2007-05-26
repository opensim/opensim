using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using OpenSim.world;
using OpenSim.UserServer;
using OpenSim.Servers;
using OpenSim.Assets;
using OpenSim.Framework.Inventory;
using libsecondlife;
using OpenSim.RegionServer.world.scripting;
using Avatar=libsecondlife.Avatar;

namespace OpenSim.CAPS
{
    public class AdminWebFront
    {
        private string AdminPage;
        private string NewAccountForm;
        private string LoginForm;
        private string passWord = "Admin";


        public AdminWebFront(string password)
        {
            passWord = password;
        }

        public void LoadMethods( BaseHttpServer server )
        {
           
        }
    }
}
