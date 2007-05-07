using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenGrid.Framework.Data
{
    public interface IUserData
    {
        // Retrieval
        // User Profiles
        UserProfileData getUserByUUID(LLUUID user);
        UserProfileData getUserByName(string name);
        UserProfileData getUserByName(string fname, string lname);
        // User Agents
        UserAgentData getAgentByUUID(LLUUID user);
        UserAgentData getAgentByName(string name);
        UserAgentData getAgentByName(string fname, string lname);

        // Transactional
        bool moneyTransferRequest(LLUUID from, LLUUID to, uint amount);
        bool inventoryTransferRequest(LLUUID from, LLUUID to, LLUUID inventory);
    }
}
