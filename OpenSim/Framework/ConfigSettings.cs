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

namespace OpenSim.Framework
{
    public class ConfigSettings
    {
        private string m_physicsEngine;

        public string PhysicsEngine
        {
            get { return m_physicsEngine; }
            set { m_physicsEngine = value; }
        }
        private string m_meshEngineName;

        public string MeshEngineName
        {
            get { return m_meshEngineName; }
            set { m_meshEngineName = value; }
        }

        private bool m_standalone;

        public bool Standalone
        {
            get { return m_standalone; }
            set { m_standalone = value; }
        }

        private bool m_see_into_region_from_neighbor;

        public bool See_into_region_from_neighbor
        {
            get { return m_see_into_region_from_neighbor; }
            set { m_see_into_region_from_neighbor = value; }
        }

        private string m_storageDll;

        public string StorageDll
        {
            get { return m_storageDll; }
            set { m_storageDll = value; }
        }

        private string m_clientstackDll;

        public string ClientstackDll
        {
            get { return m_clientstackDll; }
            set { m_clientstackDll = value; }
        }

        private bool m_physicalPrim;

        public bool PhysicalPrim
        {
            get { return m_physicalPrim; }
            set { m_physicalPrim = value; }
        }

        private bool m_standaloneAuthenticate = false;

        public bool StandaloneAuthenticate
        {
            get { return m_standaloneAuthenticate; }
            set { m_standaloneAuthenticate = value; }
        }

        private string m_standaloneWelcomeMessage = null;

        public string StandaloneWelcomeMessage
        {
            get { return m_standaloneWelcomeMessage; }
            set { m_standaloneWelcomeMessage = value; }
        }

        private string m_standaloneInventoryPlugin;

        public string StandaloneInventoryPlugin
        {
            get { return m_standaloneInventoryPlugin; }
            set { m_standaloneInventoryPlugin = value; }
        }

        private string m_standaloneUserPlugin;

        public string StandaloneUserPlugin
        {
            get { return m_standaloneUserPlugin; }
            set { m_standaloneUserPlugin = value; }
        }

        private string m_standaloneInventorySource;

        public string StandaloneInventorySource
        {
            get { return m_standaloneInventorySource; }
            set { m_standaloneInventorySource = value; }
        }

        private string m_standaloneUserSource;

        public string StandaloneUserSource
        {
            get { return m_standaloneUserSource; }
            set { m_standaloneUserSource = value; }
        }

        protected string m_storageConnectionString;

        public string StorageConnectionString
        {
            get { return m_storageConnectionString; }
            set { m_storageConnectionString = value; }
        }

        protected string m_estateConnectionString;

        public string EstateConnectionString
        {
            get { return m_estateConnectionString; }
            set { m_estateConnectionString = value; }
        }

        protected string m_librariesXMLFile;
        public string LibrariesXMLFile
        {
            get
            {
                return m_librariesXMLFile;
            }
            set
            {
                m_librariesXMLFile = value;
            }
        }

        public const uint DefaultAssetServerHttpPort = 8003;
        public const uint DefaultRegionHttpPort = 9000;
        public static uint DefaultRegionRemotingPort = 8895; // This is actually assigned to, but then again, the remoting is obsolete, right?
        public const uint DefaultUserServerHttpPort = 8002;
        public const bool DefaultUserServerHttpSSL = false;
        public const uint DefaultMessageServerHttpPort = 8006;
        public const bool DefaultMessageServerHttpSSL = false;
        public const uint DefaultGridServerHttpPort = 8003;
        public const uint DefaultInventoryServerHttpPort = 8003;
    }
}
