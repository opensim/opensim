/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
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
