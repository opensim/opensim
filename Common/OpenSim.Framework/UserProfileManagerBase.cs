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
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Inventory;
using Db4objects.Db4o;

namespace OpenSim.Framework.User
{
    public class UserProfileManagerBase
    {

        public Dictionary<LLUUID, UserProfile> UserProfiles = new Dictionary<LLUUID, UserProfile>();

        public UserProfileManagerBase()
        {
        }

        public virtual void InitUserProfiles()
        {
            IObjectContainer db;
            db = Db4oFactory.OpenFile("userprofiles.yap");
            IObjectSet result = db.Get(typeof(UserProfile));
            foreach (UserProfile userprof in result)
            {
                UserProfiles.Add(userprof.UUID, userprof);
            }
            Console.WriteLine("UserProfiles.Cs:InitUserProfiles() - Successfully loaded " + result.Count.ToString() + " from database");
            db.Close();
        }

        public virtual void SaveUserProfiles()		// ZOMG! INEFFICIENT!
        {
            IObjectContainer db;
            db = Db4oFactory.OpenFile("userprofiles.yap");
            IObjectSet result = db.Get(typeof(UserProfile));
            foreach (UserProfile userprof in result)
            {
                db.Delete(userprof);
                db.Commit();
            }
            foreach (UserProfile userprof in UserProfiles.Values)
            {
                db.Set(userprof);
                db.Commit();
            }
            db.Close();
        }

        public UserProfile GetProfileByName(string firstname, string lastname)
        {
            foreach (libsecondlife.LLUUID UUID in UserProfiles.Keys)
            {
                if (UserProfiles[UUID].firstname.Equals(firstname)) if (UserProfiles[UUID].lastname.Equals(lastname))
                {
                    return UserProfiles[UUID];
                }
            }
            return null;
        }

        public UserProfile GetProfileByLLUUID(LLUUID ProfileLLUUID)
        {
            return UserProfiles[ProfileLLUUID];
        }

        public virtual bool AuthenticateUser(string firstname, string lastname, string passwd)
        {
            UserProfile TheUser = GetProfileByName(firstname, lastname);
            passwd = passwd.Remove(0, 3); //remove $1$
            if (TheUser != null)
            {
                if (TheUser.MD5passwd == passwd)
                {
                    Console.WriteLine("UserProfile - authorised " + firstname + " " + lastname);
                    return true;
                }
                else
                {
                    Console.WriteLine("UserProfile - not authorised, password not match " + TheUser.MD5passwd + " and " + passwd);
                    return false;
                }
            }
            else
            {
                Console.WriteLine("UserProfile - not authorised , unkown: " + firstname + " , " + lastname);
                return false;
            }

        }

        public void SetGod(LLUUID GodID)
        {
            this.UserProfiles[GodID].IsGridGod = true;
        }

        public virtual UserProfile CreateNewProfile(string firstname, string lastname, string MD5passwd)
        {
            Console.WriteLine("creating new profile for : " + firstname + " , " + lastname);
            UserProfile newprofile = new UserProfile();
            newprofile.homeregionhandle = Helpers.UIntsToLong((997 * 256), (996 * 256));
            newprofile.firstname = firstname;
            newprofile.lastname = lastname;
            newprofile.MD5passwd = MD5passwd;
            newprofile.UUID = LLUUID.Random();
	    newprofile.Inventory.CreateRootFolder(newprofile.UUID, true);
            this.UserProfiles.Add(newprofile.UUID, newprofile);
            return newprofile;
        }

        public virtual AgentInventory GetUsersInventory(LLUUID agentID)
        {
            UserProfile user = this.GetProfileByLLUUID(agentID);
            if (user != null)
            {
                return user.Inventory;
            }

            return null;
        }

    }
}
