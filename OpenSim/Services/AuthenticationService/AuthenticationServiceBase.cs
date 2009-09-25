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
using OpenMetaverse;
using log4net;
using Nini.Config;
using System.Reflection;
using OpenSim.Services.Base;
using OpenSim.Data;

namespace OpenSim.Services.AuthenticationService
{
    // Generic Authentication service used for identifying
    // and authenticating principals.
    // Principals may be clients acting on users' behalf,
    // or any other components that need 
    // verifiable identification.
    //
    public class AuthenticationServiceBase : ServiceBase
    {
//        private static readonly ILog m_log =
//                LogManager.GetLogger(
//                MethodBase.GetCurrentMethod().DeclaringType);
 
        protected IAuthenticationData m_Database;

        public AuthenticationServiceBase(IConfigSource config) : base(config)
        {
            string dllName = String.Empty;
            string connString = String.Empty;
            string realm = "auth";

            //
            // Try reading the [AuthenticationService] section first, if it exists
            //
            IConfig authConfig = config.Configs["AuthenticationService"];
            if (authConfig != null)
            {
                dllName = authConfig.GetString("StorageProvider", dllName);
                connString = authConfig.GetString("ConnectionString", connString);
                realm = authConfig.GetString("Realm", realm);
            }

            //
            // Try reading the [DatabaseService] section, if it exists
            //
            IConfig dbConfig = config.Configs["DatabaseService"];
            if (dbConfig != null)
            {
                if (dllName == String.Empty)
                    dllName = dbConfig.GetString("StorageProvider", String.Empty);
                if (connString == String.Empty)
                    connString = dbConfig.GetString("ConnectionString", String.Empty);
            }

            //
            // We tried, but this doesn't exist. We can't proceed.
            //
            if (dllName == String.Empty || realm == String.Empty)
                throw new Exception("No StorageProvider configured");

            m_Database = LoadPlugin<IAuthenticationData>(dllName,
                    new Object[] {connString, realm});
            if (m_Database == null)
                throw new Exception("Could not find a storage interface in the given module");
        }

        public bool Verify(UUID principalID, string token, int lifetime)
        {
            return m_Database.CheckToken(principalID, token, lifetime);
        }

        public virtual bool Release(UUID principalID, string token)
        {
            return m_Database.CheckToken(principalID, token, 0);
        }

        protected string GetToken(UUID principalID, int lifetime)
        {
            UUID token = UUID.Random();

            if (m_Database.SetToken(principalID, token.ToString(), lifetime))
                return token.ToString();

            return String.Empty;
        }
    }
}
