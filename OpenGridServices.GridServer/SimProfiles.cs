/*
Copyright (c) OpenGrid project, http://osgrid.org/


* All rights reserved.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY <copyright holder> ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using libsecondlife;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Sims;

namespace OpenGridServices.GridServer
{
	/// <summary>
	/// </summary>
	public class SimProfileManager {
	
		public Dictionary<LLUUID, SimProfileBase> SimProfiles = new Dictionary<LLUUID, SimProfileBase>();

		public SimProfileManager() {
		}
		
		public void InitSimProfiles() {
			// TODO: need to load from database
		}

		public SimProfileBase GetProfileByHandle(ulong reqhandle) {
			foreach (libsecondlife.LLUUID UUID in SimProfiles.Keys) {
				if(SimProfiles[UUID].regionhandle==reqhandle) return SimProfiles[UUID];
			}
			return null;
		}

		public SimProfileBase GetProfileByLLUUID(LLUUID ProfileLLUUID) {
			return SimProfiles[ProfileLLUUID];
		}
	
		public bool AuthenticateSim(LLUUID RegionUUID, uint regionhandle, string simrecvkey) {
			SimProfileBase TheSim=GetProfileByHandle(regionhandle);
			if(TheSim != null) 
			if(TheSim.recvkey==simrecvkey) {
				return true;
			} else {
				return false;
			} else return false;
			
		}

		public SimProfileBase CreateNewProfile(string regionname, string caps_url, string sim_ip, uint sim_port, uint RegionLocX, uint RegionLocY, string sendkey, string recvkey) {
			SimProfileBase newprofile = new SimProfileBase();
			newprofile.regionname=regionname;
			newprofile.sim_ip=sim_ip;
			newprofile.sim_port=sim_port;
			newprofile.RegionLocX=RegionLocX;
			newprofile.RegionLocY=RegionLocY;
			newprofile.caps_url="http://" + sim_ip + ":9000/";
			newprofile.sendkey=sendkey;
			newprofile.recvkey=recvkey;
			newprofile.regionhandle=Util.UIntsToLong((RegionLocX*256), (RegionLocY*256));
			newprofile.UUID=LLUUID.Random();
			this.SimProfiles.Add(newprofile.UUID,newprofile);
			return newprofile;
		}

	}

    /*  is in OpenSim.Framework
	public class SimProfileBase {
		public LLUUID UUID;
		public ulong regionhandle;
		public string regionname;
		public string sim_ip;
		public uint sim_port;
		public string caps_url;
		public uint RegionLocX;
		public uint RegionLocY;
		public string sendkey;
		public string recvkey;
		
	
		public SimProfileBase() {
		}
	
	
	}*/

}
