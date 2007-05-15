using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework.Inventory;
using System.Security.Cryptography;

namespace OpenSim.Framework.User
{
    public class UserProfile
    {

        public string firstname;
        public string lastname;
        public ulong homeregionhandle;
        public LLVector3 homepos;
        public LLVector3 homelookat;

        public bool IsGridGod = false;
        public bool IsLocal = true;	// will be used in future for visitors from foreign grids
        public string AssetURL;
        public string MD5passwd;

        public LLUUID CurrentSessionID;
        public LLUUID CurrentSecureSessionID;
        public LLUUID UUID;
        public Dictionary<LLUUID, uint> Circuits = new Dictionary<LLUUID, uint>();	// tracks circuit codes

        public AgentInventory Inventory;

        public UserProfile()
        {
            Circuits = new Dictionary<LLUUID, uint>();
            Inventory = new AgentInventory();
            homeregionhandle = Helpers.UIntsToLong((997 * 256), (996 * 256));
            homepos = new LLVector3();
	    homelookat = new LLVector3();
        }

        public void InitSessionData()
        {
            RNGCryptoServiceProvider rand = new RNGCryptoServiceProvider();

            byte[] randDataS = new byte[16];
            byte[] randDataSS = new byte[16];

            rand.GetBytes(randDataS);
            rand.GetBytes(randDataSS);

            CurrentSecureSessionID = new LLUUID(randDataSS,0);
            CurrentSessionID = new LLUUID(randDataS,0);
        }

        public void AddSimCircuit(uint circuitCode, LLUUID regionUUID)
        {
            if (this.Circuits.ContainsKey(regionUUID) == false)
                this.Circuits.Add(regionUUID, circuitCode);
        }

    }
}
