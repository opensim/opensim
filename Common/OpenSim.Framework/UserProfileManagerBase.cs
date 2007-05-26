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
