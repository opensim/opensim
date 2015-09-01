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
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using OpenMetaverse;
using OpenSim.Framework;
using Npgsql;

namespace OpenSim.Data.PGSQL
{
    /// <summary>
    /// A database interface class to a user profile storage system
    /// </summary>
    public class PGSqlFramework
    {
        private static readonly log4net.ILog m_log =
                log4net.LogManager.GetLogger(
                System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        protected string m_connectionString;
        protected object m_dbLock = new object();

        protected PGSqlFramework(string connectionString)
        {
            m_connectionString = connectionString;
            InitializeMonoSecurity();
        }

        public void InitializeMonoSecurity()
        {
            if (!Util.IsPlatformMono)
            {

                if (AppDomain.CurrentDomain.GetData("MonoSecurityPostgresAdded") == null)
                {
                    AppDomain.CurrentDomain.SetData("MonoSecurityPostgresAdded", "true");

                    AppDomain currentDomain = AppDomain.CurrentDomain;
                    currentDomain.AssemblyResolve += new ResolveEventHandler(ResolveEventHandlerMonoSec);
                }
            }
        }

        private System.Reflection.Assembly ResolveEventHandlerMonoSec(object sender, ResolveEventArgs args)
        {
            Assembly MyAssembly = null;

            if (args.Name.Substring(0, args.Name.IndexOf(",")) == "Mono.Security")
            {
                MyAssembly = Assembly.LoadFrom("lib/NET/Mono.Security.dll");
            }

            //Return the loaded assembly.
            return MyAssembly;
        }
        //////////////////////////////////////////////////////////////
        //
        // All non queries are funneled through one connection
        // to increase performance a little
        //
        protected int ExecuteNonQuery(NpgsqlCommand cmd)
        {
            lock (m_dbLock)
            {
                using (NpgsqlConnection dbcon = new NpgsqlConnection(m_connectionString))
                {
                    dbcon.Open();
                    cmd.Connection = dbcon;

                    try
                    {
                        return cmd.ExecuteNonQuery();
                    }
                    catch (Exception e)
                    {
                        m_log.Error(e.Message, e);
                        return 0;
                    }
                }
            }
        }
    }
}
