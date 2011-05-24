using System;
using System.Collections.Generic;

using OpenMetaverse;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface IUserManagement
    {
        string GetUserName(UUID uuid);
        string GetUserHomeURL(UUID uuid);
        string GetUserServerURL(UUID uuid, string serverType);
        void AddUser(UUID uuid, string userData);
        void AddUser(UUID uuid, string firstName, string lastName, string profileURL);
    }
}
