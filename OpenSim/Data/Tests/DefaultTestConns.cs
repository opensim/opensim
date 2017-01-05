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
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using Nini.Config;

namespace OpenSim.Data.Tests
{
    /// <summary>This static class looks for TestDataConnections.ini file in the /bin directory to obtain
    /// a connection string for testing one of the supported databases.
    /// The connections must be in the section [TestConnections] with names matching the connection class
    /// name for the specific database, e.g.:
    ///
    /// [TestConnections]
    /// MySqlConnection="..."
    /// SqlConnection="..."
    /// SqliteConnection="..."
    ///
    /// Note that the conn string may also be set explicitly in the [TestCase()] attribute of test classes
    /// based on BasicDataServiceTest.cs.
    /// </summary>

    static class DefaultTestConns
    {
        private static Dictionary<Type, string> conns = new Dictionary<Type, string>();

        public static string Get(Type connType)
        {
            string sConn;

            if (conns.TryGetValue(connType, out sConn))
                return sConn;

            Assembly asm = Assembly.GetExecutingAssembly();
            string sType = connType.Name;

            // Note: when running from NUnit, the DLL is located in some temp dir, so how do we get
            // to the INI file? Ok, so put it into the resources!
            // string iniName = Path.Combine(Path.GetDirectoryName(asm.Location), "TestDataConnections.ini");

            string[] allres = asm.GetManifestResourceNames();
            string sResFile = Array.Find(allres, s => s.Contains("TestDataConnections.ini"));

            if (String.IsNullOrEmpty(sResFile))
                throw new Exception(String.Format("Please add resource TestDataConnections.ini, with section [TestConnections] and settings like {0}=\"...\"",
                    sType));

            using (Stream resource = asm.GetManifestResourceStream(sResFile))
            {
                IConfigSource source = new IniConfigSource(resource);
                var cfg = source.Configs["TestConnections"];
                sConn = cfg.Get(sType, "");
            }

            if (!String.IsNullOrEmpty(sConn))
                conns[connType] = sConn;

            return sConn;
        }
    }
}
