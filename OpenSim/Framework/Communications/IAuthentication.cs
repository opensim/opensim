using System;

using OpenMetaverse;


namespace OpenSim.Framework.Communications
{
    public interface IAuthentication
    {
        string GetNewKey(string url, UUID userID, UUID authToken);
        bool VerifyKey(UUID userID, string key);
    }
}
