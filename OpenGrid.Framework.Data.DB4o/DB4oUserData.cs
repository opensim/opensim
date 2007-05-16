using System;
using System.Collections.Generic;
using System.Text;
using OpenGrid.Framework.Data;
using libsecondlife;

namespace OpenGrid.Framework.Data.DB4o
{
    public class DB4oUserData : IUserData
    {
        DB4oUserManager manager;

        public void Initialise()
        {
            manager = new DB4oUserManager("userprofiles.yap");
        }

        public UserProfileData getUserByUUID(LLUUID uuid)
        {
            if(manager.userProfiles.ContainsKey(uuid))
                return manager.userProfiles[uuid];
            return null;
        }

        public UserProfileData getUserByName(string name)
        {
            return getUserByName(name.Split(' ')[0], name.Split(' ')[1]);
        }

        public UserProfileData getUserByName(string fname, string lname)
        {
            foreach (UserProfileData profile in manager.userProfiles.Values)
            {
                if (profile.username == fname && profile.surname == lname)
                    return profile;
            }
            return null;
        }

        public UserAgentData getAgentByUUID(LLUUID uuid)
        {   
            try
            {
                return getUserByUUID(uuid).currentAgent;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public UserAgentData getAgentByName(string name)
        {
            return getAgentByName(name.Split(' ')[0], name.Split(' ')[1]);
        }

        public UserAgentData getAgentByName(string fname, string lname)
        {
            try
            {
                return getUserByName(fname,lname).currentAgent;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public bool moneyTransferRequest(LLUUID from, LLUUID to, uint amount)
        {
            return true;
        }

        public bool inventoryTransferRequest(LLUUID from, LLUUID to, LLUUID item)
        {
            return true;
        }


        public string getName()
        {
            return "DB4o Userdata";
        }

        public string getVersion()
        {
            return "0.1";
        }
    }
}
