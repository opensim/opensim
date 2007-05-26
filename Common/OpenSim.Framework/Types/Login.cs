using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Framework.Types
{
    public class Login
    {
        public string First = "Test";
        public string Last = "User";
        public LLUUID Agent;
        public LLUUID Session;
        public LLUUID SecureSession = LLUUID.Zero;
        public LLUUID InventoryFolder;
        public LLUUID BaseFolder;
        public uint CircuitCode;

        public Login()
        {

        }
    }
}
