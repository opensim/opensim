using System;
using System.Collections.Generic;

using OpenMetaverse;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface IUserManagement
    {
        string GetUserName(UUID uuid);
        void AddUser(UUID uuid, string userData);
        void AddUser(UUID uuid, string firstName, string lastName, string profileURL);
    }
}
