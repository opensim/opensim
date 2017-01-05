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
using System.Net;
using System.Reflection;
using Nini.Config;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Data;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Services.AvatarService
{
    public class AvatarService : AvatarServiceBase, IAvatarService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        public AvatarService(IConfigSource config)
            : base(config)
        {
            m_log.Debug("[AVATAR SERVICE]: Starting avatar service");
        }

        public AvatarAppearance GetAppearance(UUID principalID)
        {
            AvatarData avatar = GetAvatar(principalID);
            return avatar.ToAvatarAppearance();
        }

        public bool SetAppearance(UUID principalID, AvatarAppearance appearance)
        {
            AvatarData avatar = new AvatarData(appearance);
            return SetAvatar(principalID,avatar);
        }

        public AvatarData GetAvatar(UUID principalID)
        {
            AvatarBaseData[] av = m_Database.Get("PrincipalID", principalID.ToString());
            AvatarData ret = new AvatarData();
            ret.Data = new Dictionary<string,string>();

            if (av.Length == 0)
            {
                ret.AvatarType = 1; // SL avatar
                return ret;
            }

            foreach (AvatarBaseData b in av)
            {
                if (b.Data["Name"] == "AvatarType")
                    ret.AvatarType = Convert.ToInt32(b.Data["Value"]);
                else
                    ret.Data[b.Data["Name"]] = b.Data["Value"];
            }

            return ret;
        }

        public bool SetAvatar(UUID principalID, AvatarData avatar)
        {
            int count = 0;
            foreach (KeyValuePair<string, string> kvp in avatar.Data)
                if (kvp.Key.StartsWith("_"))
                    count++;

//            m_log.DebugFormat("[AVATAR SERVICE]: SetAvatar for {0}, attachs={1}", principalID, count);
            m_Database.Delete("PrincipalID", principalID.ToString());

            AvatarBaseData av = new AvatarBaseData();
            av.Data = new Dictionary<string,string>();

            av.PrincipalID = principalID;
            av.Data["Name"] = "AvatarType";
            av.Data["Value"] = avatar.AvatarType.ToString();

            if (!m_Database.Store(av))
                return false;

            foreach (KeyValuePair<string,string> kvp in avatar.Data)
            {
                av.Data["Name"] = kvp.Key;

                // justincc 20110730.  Yes, this is a hack to get around the fact that a bug in OpenSim is causing
                // various simulators on osgrid to inject bad values.  Since these simulators might be around for a
                // long time, we are going to manually police the value.
                //
                // It should be possible to remove this in half a year if we don't want to police values server side.
                if (kvp.Key == "AvatarHeight")
                {
                    float height;
                    if (!float.TryParse(kvp.Value, out height) || height < 0 || height > 10)
                    {
                        string rawHeight = kvp.Value.Replace(",", ".");

                        if (!float.TryParse(rawHeight, out height) || height < 0 || height > 10)
                            height = 1.771488f;

                        m_log.DebugFormat(
                            "[AVATAR SERVICE]: Rectifying height of avatar {0} from {1} to {2}",
                            principalID, kvp.Value, height);
                    }

                    av.Data["Value"] = height.ToString();
                }
                else
                {
                    av.Data["Value"] = kvp.Value;
                }

                if (!m_Database.Store(av))
                {
                    m_Database.Delete("PrincipalID", principalID.ToString());
                    return false;
                }
            }

            return true;
        }

        public bool ResetAvatar(UUID principalID)
        {
            return m_Database.Delete("PrincipalID", principalID.ToString());
        }

        public bool SetItems(UUID principalID, string[] names, string[] values)
        {
            AvatarBaseData av = new AvatarBaseData();
            av.Data = new Dictionary<string,string>();
            av.PrincipalID = principalID;

            if (names.Length != values.Length)
                return false;

            for (int i = 0 ; i < names.Length ; i++)
            {
                av.Data["Name"] = names[i];
                av.Data["Value"] = values[i];

                if (!m_Database.Store(av))
                    return false;
            }

            return true;
        }

        public bool RemoveItems(UUID principalID, string[] names)
        {
            foreach (string name in names)
            {
                m_Database.Delete(principalID, name);
            }
            return true;
        }
    }
}
