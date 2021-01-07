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
using log4net;
using Mono.Addins;
using Nini.Config;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml;

[assembly: Addin("OpenSimMutelist", OpenSim.VersionInfo.VersionNumber + "0.1")]
[assembly: AddinDependency("OpenSim.Region.Framework", OpenSim.VersionInfo.VersionNumber)]
[assembly: AddinDescription("OpenSimMutelist module.")]
[assembly: AddinAuthor("Kevin Cozens")]


namespace OpenSim.Region.CoreModules.Avatar.InstantMessage
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "OpenSimMutelist")]
    public class MuteListModule : ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool enabled = true;
        private List<Scene> m_SceneList = new List<Scene>();
        private string m_MuteListURL = String.Empty;

        IUserManagement m_uMan;
        IUserManagement UserManagementModule
        {
            get
            {
                if (m_uMan == null)
                    m_uMan = m_SceneList[0].RequestModuleInterface<IUserManagement>();
                return m_uMan;
            }
        }

        #region IRegionModuleBase implementation

        public void Initialise(IConfigSource config)
        {
            IConfig cnf = config.Configs["Messaging"];
            if (cnf == null)
            {
                enabled = false;
                return;
            }

            if (cnf != null && cnf.GetString("MuteListModule", "None") !=
                    "OpenSimMutelist")
            {
                enabled = false;
                return;
            }

            m_MuteListURL = cnf.GetString("MuteListURL", "");
            if (m_MuteListURL == "")
            {
                m_log.Error("[OS MUTELIST] Module was enabled, but no URL is given, disabling");
                enabled = false;
                return;
            }

            m_log.Info("[OS MUTELIST] Mute list enabled");
        }

        public void AddRegion(Scene scene)
        {
            if (!enabled)
                return;

            lock (m_SceneList)
            {
                m_SceneList.Add(scene);
            }
        }

        public void RegionLoaded(Scene scene)
        {
            if (!enabled)
                return;

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnClientClosed += OnClientClosed;
        }

        public void RemoveRegion(Scene scene)
        {
            if (!enabled)
                return;

            lock (m_SceneList)
            {
                scene.EventManager.OnNewClient -= OnNewClient;
                scene.EventManager.OnClientClosed -= OnClientClosed;

                m_SceneList.Remove(scene);
            }
        }

        public void PostInitialise()
        {
        }

        public string Name
        {
            get { return "MuteListModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Close()
        {
        }

        #endregion

        private void OnNewClient(IClientAPI client)
        {
            client.OnMuteListRequest += OnMuteListRequest;
            client.OnUpdateMuteListEntry += OnUpdateMuteListEntry;
            client.OnRemoveMuteListEntry += OnRemoveMuteListEntry;
        }

        private void OnClientClosed(UUID agentID, Scene scene)
        {
            ScenePresence scenePresence = scene.GetScenePresence(agentID);
            IClientAPI client = scenePresence.ControllingClient;

            if (client != null)
            {
                client.OnMuteListRequest -= OnMuteListRequest;
                client.OnUpdateMuteListEntry -= OnUpdateMuteListEntry;
                client.OnRemoveMuteListEntry -= OnRemoveMuteListEntry;
            }
        }

        //
        // Make external XMLRPC request
        //
        private Hashtable GenericXMLRPCRequest(Hashtable ReqParams, string method, string server)
        {
            ArrayList SendParams = new ArrayList();
            SendParams.Add(ReqParams);

            // Send Request
            XmlRpcResponse Resp;
            try
            {
                XmlRpcRequest Req = new XmlRpcRequest(method, SendParams);
                Resp = Req.Send(server, 30000);
            }
            catch (WebException ex)
            {
                m_log.ErrorFormat("[OS MUTELIST]: Unable to connect to mutelist " +
                        "server {0}.  Exception {1}", m_MuteListURL, ex);

                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Unable to connect to mutelist server at this time. ";
                ErrorHash["errorURI"] = "";

                return ErrorHash;
            }
            catch (SocketException ex)
            {
                m_log.ErrorFormat(
                        "[OS MUTELIST]: Unable to connect to mutelist server {0}. Method {1}, params {2}. " +
                        "Exception {3}", m_MuteListURL, method, ReqParams, ex);

                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Unable to connect to mutelist server at this time. ";
                ErrorHash["errorURI"] = "";

                return ErrorHash;
            }
            catch (XmlException ex)
            {
                m_log.ErrorFormat(
                        "[OS MUTELIST]: Unable to connect to mutelist server {0}. Method {1}, params {2}. " +
                        "Exception {3}", m_MuteListURL, method, ReqParams, ex);

                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Unable to connect to mutelist server at this time. ";
                ErrorHash["errorURI"] = "";

                return ErrorHash;
            }
            if (Resp.IsFault)
            {
                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Unable to process mutelist response at this time. ";
                ErrorHash["errorURI"] = "";
                return ErrorHash;
            }
            Hashtable RespData = (Hashtable)Resp.Value;

            return RespData;
        }

        private bool GetUserMutelistServerURI(UUID userID, out string serverURI)
        {
            IUserManagement uManage = UserManagementModule;

            if (!uManage.IsLocalGridUser(userID))
            {
                serverURI = uManage.GetUserServerURL(userID, "MutelistServerURI");
                // Is Foreign
                return true;
            }
            else
            {
                serverURI = m_MuteListURL;
                // Is local
                return false;
            }
        }

        private void OnMuteListRequest(IClientAPI client, uint crc)
        {
            string mutelist;

            IXfer xfer = client.Scene.RequestModuleInterface<IXfer>();
            if (xfer == null)
                return;

            m_log.DebugFormat("[OS MUTELIST] Got mute list request");

            string filename = "mutes" + client.AgentId.ToString();

            Hashtable ReqHash = new Hashtable();
            ReqHash["avataruuid"] = client.AgentId.ToString();

            string serverURI = String.Empty;
            GetUserMutelistServerURI(client.AgentId, out serverURI);

            Hashtable result = GenericXMLRPCRequest(ReqHash,
                    "mutelist_request", serverURI);

            //If no mutelist exists PHP sends "<string/>" which results in the
            //mutelist hashtable entry being null rather than an empty string.
            if (!Convert.ToBoolean(result["success"]) || result["mutelist"] == null)
                mutelist = null;
            else
                mutelist = result["mutelist"].ToString();

            if (mutelist == null || mutelist.Length == 0)
            {
                xfer.AddNewFile(filename, new Byte[0]);
            }
            else
            {
                Byte[] filedata = Util.UTF8.GetBytes(mutelist);

                uint dataCrc = Crc32.Compute(filedata);

                if (dataCrc == crc)
                {
                    client.SendUseCachedMuteList();
                    return;
                }

                xfer.AddNewFile(filename, filedata);
            }

            client.SendMuteListUpdate(filename);
        }

        private void OnUpdateMuteListEntry(IClientAPI client, UUID MuteID, string Name, int type, uint flags)
        {
            m_log.DebugFormat("[OS MUTELIST] Got mute list update request");

            Hashtable ReqHash = new Hashtable();
            ReqHash["avataruuid"] = client.AgentId.ToString();
            ReqHash["muteuuid"] = MuteID.ToString();
            ReqHash["name"] = Name.ToString();
            ReqHash["type"] = type.ToString();
            ReqHash["flags"] = flags.ToString();

            string serverURI = String.Empty;
            GetUserMutelistServerURI(client.AgentId, out serverURI);

            Hashtable result = GenericXMLRPCRequest(ReqHash,
                    "mutelist_update", serverURI);

            if (!Convert.ToBoolean(result["success"]))
            {
                client.SendAgentAlertMessage(
                        result["errorMessage"].ToString(), false);
            }
        }

        private void OnRemoveMuteListEntry(IClientAPI client, UUID MuteID, string Name)
        {
            m_log.DebugFormat("[OS MUTELIST] Got mute list removal request");

            Hashtable ReqHash = new Hashtable();
            ReqHash["avataruuid"] = client.AgentId.ToString();
            ReqHash["muteuuid"] = MuteID.ToString();

            string serverURI = String.Empty;
            GetUserMutelistServerURI(client.AgentId, out serverURI);

            Hashtable result = GenericXMLRPCRequest(ReqHash,
                    "mutelist_remove", serverURI);

            if (!Convert.ToBoolean(result["success"]))
            {
                client.SendAgentAlertMessage(
                        result["errorMessage"].ToString(), false);
            }
        }
    }

    public class Crc32 : HashAlgorithm
    {
        public const UInt32 DefaultPolynomial = 0xedb88320;
        public const UInt32 DefaultSeed = 0xffffffff;

        private UInt32 hash;
        private UInt32 seed;
        private UInt32[] table;
        private static UInt32[] defaultTable;

        public Crc32()
        {
            table = InitializeTable(DefaultPolynomial);
            seed = DefaultSeed;
            Initialize();
        }

        public Crc32(UInt32 polynomial, UInt32 seed)
        {
            table = InitializeTable(polynomial);
            this.seed = seed;
            Initialize();
        }

        public override void Initialize()
        {
            hash = seed;
        }

        protected override void HashCore(byte[] buffer, int start, int length)
        {
            hash = CalculateHash(table, hash, buffer, start, length);
        }

        protected override byte[] HashFinal()
        {
            byte[] hashBuffer = UInt32ToBigEndianBytes(~hash);
            this.HashValue = hashBuffer;
            return hashBuffer;
        }

        public override int HashSize
        {
            get { return 32; }
        }

        public static UInt32 Compute(byte[] buffer)
        {
            return ~CalculateHash(InitializeTable(DefaultPolynomial), DefaultSeed, buffer, 0, buffer.Length);
        }

        public static UInt32 Compute(UInt32 seed, byte[] buffer)
        {
            return ~CalculateHash(InitializeTable(DefaultPolynomial), seed, buffer, 0, buffer.Length);
        }

        public static UInt32 Compute(UInt32 polynomial, UInt32 seed, byte[] buffer)
        {
            return ~CalculateHash(InitializeTable(polynomial), seed, buffer, 0, buffer.Length);
        }

        private static UInt32[] InitializeTable(UInt32 polynomial)
        {
            if (polynomial == DefaultPolynomial && defaultTable != null)
                return defaultTable;

            UInt32[] createTable = new UInt32[256];
            for (int i = 0; i < 256; i++)
            {
                UInt32 entry = (UInt32)i;
                for (int j = 0; j < 8; j++)
                    if ((entry & 1) == 1)
                        entry = (entry >> 1) ^ polynomial;
                    else
                        entry = entry >> 1;
                createTable[i] = entry;
            }

            if (polynomial == DefaultPolynomial)
                defaultTable = createTable;

            return createTable;
        }

        private static UInt32 CalculateHash(UInt32[] table, UInt32 seed, byte[] buffer, int start, int size)
        {
            UInt32 crc = seed;
            for (int i = start; i < size; i++)
                unchecked
                {
                    crc = (crc >> 8) ^ table[buffer[i] ^ crc & 0xff];
                }
            return crc;
        }

        private byte[] UInt32ToBigEndianBytes(UInt32 x)
        {
            return new byte[] {
                    (byte)((x >> 24) & 0xff),
                    (byte)((x >> 16) & 0xff),
                    (byte)((x >> 8) & 0xff),
                    (byte)(x & 0xff) };
        }
    }
}
