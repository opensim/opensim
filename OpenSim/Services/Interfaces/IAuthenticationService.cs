using System;
using OpenMetaverse;

namespace OpenSim.Services.Interfaces
{
    public interface IAuthenticationService
    {
        string GetNewKey(UUID userID, UUID authToken);

        bool VerifyKey(UUID userID, string key);
        
        bool VerifySession(UUID userID, UUID sessionID);
    }
}
