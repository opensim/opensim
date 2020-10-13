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

using System.IO;
using Nini.Config;
using NUnit.Framework;
using OpenSim.Framework;
using OpenSim.Tests.Common;

namespace OpenSim.Tests
{
    [TestFixture]
    public class ConfigurationLoaderTests : OpenSimTestCase
    {
        private const string m_testSubdirectory = "test";
        private string m_basePath;
        private string m_workingDirectory;
        private IConfigSource m_config;

        /// <summary>
        /// Set up a test directory.
        /// </summary>
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            m_basePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string path = Path.Combine(m_basePath, m_testSubdirectory);
            Directory.CreateDirectory(path);
            m_workingDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(path);
        }

        /// <summary>
        /// Remove the test directory.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Directory.SetCurrentDirectory(m_workingDirectory);
            Directory.Delete(m_basePath, true);
        }

        /// <summary>
        /// Test the including of ini files with absolute and relative paths.
        /// </summary>
        [Test]
        public void IncludeTests()
        {
            const string mainIniFile = "OpenSimDefaults.ini";
            m_config = new IniConfigSource();

            // Create ini files in a directory structure
            IniConfigSource ini;
            IConfig config;

            ini = new IniConfigSource();
            config = ini.AddConfig("IncludeTest");
            config.Set("Include-absolute", "absolute/one/config/setting.ini");
            config.Set("Include-absolute1", "absolute/two/config/setting1.ini");
            config.Set("Include-absolute2", "absolute/two/config/setting2.ini");
            config.Set("Include-relative", "../" + m_testSubdirectory + "/relative/one/config/setting.ini");
            config.Set("Include-relative1", "../" + m_testSubdirectory + "/relative/two/config/setting1.ini");
            config.Set("Include-relative2", "../" + m_testSubdirectory + "/relative/two/config/setting2.ini");
            CreateIni(mainIniFile, ini);

            ini = new IniConfigSource();
            ini.AddConfig("Absolute1").Set("name1", "value1");
            CreateIni("absolute/one/config/setting.ini", ini);

            ini = new IniConfigSource();
            ini.AddConfig("Absolute2").Set("name2", 2.3);
            CreateIni("absolute/two/config/setting1.ini", ini);

            ini = new IniConfigSource();
            ini.AddConfig("Absolute2").Set("name3", "value3");
            CreateIni("absolute/two/config/setting2.ini", ini);

            ini = new IniConfigSource();
            ini.AddConfig("Relative1").Set("name4", "value4");
            CreateIni("relative/one/config/setting.ini", ini);

            ini = new IniConfigSource();
            ini.AddConfig("Relative2").Set("name5", true);
            CreateIni("relative/two/config/setting1.ini", ini);

            ini = new IniConfigSource();
            ini.AddConfig("Relative2").Set("name6", 6);
            CreateIni("relative/two/config/setting2.ini", ini);

            // Prepare call to ConfigurationLoader.LoadConfigSettings()
            ConfigurationLoader cl = new ConfigurationLoader();
            IConfigSource argvSource = new IniConfigSource();
            EnvConfigSource envConfigSource = new EnvConfigSource();
            argvSource.AddConfig("Startup").Set("inifile", mainIniFile);
            argvSource.AddConfig("Network");
            ConfigSettings configSettings;
            NetworkServersInfo networkInfo;

            OpenSimConfigSource source = cl.LoadConfigSettings(argvSource, envConfigSource,
                out configSettings, out networkInfo);

            // Remove default config
            config = source.Source.Configs["Startup"];
            source.Source.Configs.Remove(config);
            config = source.Source.Configs["Network"];
            source.Source.Configs.Remove(config);

            // Finally, we are able to check the result
            Assert.AreEqual(m_config.ToString(), source.Source.ToString(),
                "Configuration with includes does not contain all settings.");
        }

        private void CreateIni(string filepath, IniConfigSource source)
        {
            string path = Path.GetDirectoryName(filepath);
            if (path != string.Empty)
            {
                Directory.CreateDirectory(path);
            }
            source.Save(filepath);
            m_config.Merge(source);
        }
    }
}
