/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using OpenMetaverse;

namespace OpenSim.Services.Interfaces
{
    public class AgentPrefs
    {
        public AgentPrefs(UUID principalID) 
        {
            PrincipalID = principalID;
        }

        public AgentPrefs(Dictionary<string, string> kvp)
        {
            if (kvp.ContainsKey("PrincipalID"))
                UUID.TryParse(kvp["PrincipalID"], out PrincipalID);
            if (kvp.ContainsKey("AccessPrefs"))
                AccessPrefs = kvp["AccessPrefs"];
            if (kvp.ContainsKey("HoverHeight"))
                HoverHeight = double.Parse(kvp["HoverHeight"]);
            if (kvp.ContainsKey("Language"))
                Language = kvp["Language"];
            if (kvp.ContainsKey("LanguageIsPublic"))
                LanguageIsPublic = bool.Parse(kvp["LanguageIsPublic"]);
            if (kvp.ContainsKey("PermEveryone"))
                PermEveryone = int.Parse(kvp["PermEveryone"]);
            if (kvp.ContainsKey("PermGroup"))
                PermGroup = int.Parse(kvp["PermGroup"]);
            if (kvp.ContainsKey("PermNextOwner"))
                PermNextOwner = int.Parse(kvp["PermNextOwner"]);
        }

        public AgentPrefs(Dictionary<string, object> kvp)
        {
            if (kvp.ContainsKey("PrincipalID"))
                UUID.TryParse(kvp["PrincipalID"].ToString(), out PrincipalID);
            if (kvp.ContainsKey("AccessPrefs"))
                AccessPrefs = kvp["AccessPrefs"].ToString();
            if (kvp.ContainsKey("HoverHeight"))
                HoverHeight = double.Parse(kvp["HoverHeight"].ToString());
            if (kvp.ContainsKey("Language"))
                Language = kvp["Language"].ToString();
            if (kvp.ContainsKey("LanguageIsPublic"))
                LanguageIsPublic = bool.Parse(kvp["LanguageIsPublic"].ToString());
            if (kvp.ContainsKey("PermEveryone"))
                PermEveryone = int.Parse(kvp["PermEveryone"].ToString());
            if (kvp.ContainsKey("PermGroup"))
                PermGroup = int.Parse(kvp["PermGroup"].ToString());
            if (kvp.ContainsKey("PermNextOwner"))
                PermNextOwner = int.Parse(kvp["PermNextOwner"].ToString());
        }

        public Dictionary<string, object> ToKeyValuePairs()
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["PrincipalID"] = PrincipalID.ToString();
            result["AccessPrefs"] = AccessPrefs.ToString();
            result["HoverHeight"] = HoverHeight.ToString();
            result["Language"] = Language.ToString();
            result["LanguageIsPublic"] = LanguageIsPublic.ToString();
            result["PermEveryone"] = PermEveryone.ToString();
            result["PermGroup"] = PermGroup.ToString();
            result["PermNextOwner"] = PermNextOwner.ToString();
            return result;
        }

        public UUID PrincipalID = UUID.Zero;
        public string AccessPrefs = "M";
        //public int GodLevel; // *TODO: Implement GodLevel (Unused by the viewer, afaict - 6/11/2015)
        public double HoverHeight = 0.0;
        public string Language = "en-us";
        public bool LanguageIsPublic = true;
        // DefaultObjectPermMasks
        public int PermEveryone = 0;
        public int PermGroup = 0;
        public int PermNextOwner = 532480;
    }

    public interface IAgentPreferencesService
    {
        AgentPrefs GetAgentPreferences(UUID principalID);
        bool StoreAgentPreferences(AgentPrefs data);

        string GetLang(UUID principalID);
    }
}

