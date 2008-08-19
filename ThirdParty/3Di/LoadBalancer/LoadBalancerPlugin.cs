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
 *     * Neither the name of the OpenSim Project nor the
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
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using libsecondlife;
using libsecondlife.Packets;
using log4net;
using Mono.Addins;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Region.ClientStack;
using OpenSim.Region.ClientStack.LindenUDP;
using OpenSim.Region.Environment.Scenes;

// TODO: remove LindenUDP dependency

namespace OpenSim.ApplicationPlugins.LoadBalancer
{
    public class LoadBalancerPlugin : IApplicationPlugin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private BaseHttpServer commandServer;
        private bool[] isLocalNeighbour;
        private bool isSplit;
        private TcpServer mTcpServer;
        private readonly object padlock = new object();

        private int proxyOffset;
        private string proxyURL;
        private List<RegionInfo> regionData;
        private int[] regionPortList;
        private SceneManager sceneManager;
        private string[] sceneURL;
        private string serializeDir;
        private OpenSimBase simMain;
        private TcpClient[] tcpClientList;
        private List<IClientNetworkServer> m_clientServers;

        #region IApplicationPlugin Members
        // TODO: required by IPlugin, but likely not at all right
        string m_name = "LoadBalancer";
        string m_version = "0.1";

        public string Version { get { return m_version; } }
        public string Name { get { return m_name; } }

        public void Initialise()
        {
            m_log.Info("[BALANCER]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException (Name);
        }

        public void Initialise(OpenSimBase openSim)
        {
            m_log.Info("[BALANCER] " + "Entering Initialize()");

            proxyURL = openSim.ConfigSource.Source.Configs["Network"].GetString("proxy_url", "");
            if (proxyURL.Length == 0) return;

            StartTcpServer();

            LLClientView.SynchronizeClient = SynchronizePackets;
            AsynchronousSocketListener.PacketHandler = SynchronizePacketRecieve;

            sceneManager = openSim.SceneManager;
            m_clientServers = openSim.ClientServers;
            regionData = openSim.RegionData;
            simMain = openSim;
            commandServer = openSim.HttpServer;

            proxyOffset = Int32.Parse(openSim.ConfigSource.Source.Configs["Network"].GetString("proxy_offset", "0"));
            serializeDir = openSim.ConfigSource.Source.Configs["Network"].GetString("serialize_dir", "/tmp/");

            commandServer.AddXmlRPCHandler("SerializeRegion", SerializeRegion);
            commandServer.AddXmlRPCHandler("DeserializeRegion_Move", DeserializeRegion_Move);
            commandServer.AddXmlRPCHandler("DeserializeRegion_Clone", DeserializeRegion_Clone);
            commandServer.AddXmlRPCHandler("TerminateRegion", TerminateRegion);

            commandServer.AddXmlRPCHandler("SplitRegion", SplitRegion);
            commandServer.AddXmlRPCHandler("MergeRegions", MergeRegions);
            commandServer.AddXmlRPCHandler("UpdatePhysics", UpdatePhysics);
            commandServer.AddXmlRPCHandler("GetStatus", GetStatus);

            m_log.Info("[BALANCER] " + "Exiting Initialize()");
        }

        public void Dispose()
        {
        }

        #endregion

        private void StartTcpServer()
        {
            Thread server_thread = new Thread(new ThreadStart(
                                                  delegate
                                                      {
                                                          mTcpServer = new TcpServer(10001);
                                                          mTcpServer.start();
                                                      }));
            server_thread.Start();
        }

        private XmlRpcResponse GetStatus(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            try
            {
                m_log.Info("[BALANCER] " + "Entering RegionStatus()");

                int src_port = (int) request.Params[0];
                Scene scene;
                // try to get the scene object
                RegionInfo src_region = SearchRegionFromPortNum(src_port);
                if (sceneManager.TryGetScene(src_region.RegionID, out scene) == false)
                {
                    m_log.Error("[BALANCER] " + "The Scene is not found");
                    return response;
                }
                // serialization of client's informations
                List<ScenePresence> presences = scene.GetScenePresences();
                int get_scene_presence = presences.Count;
                int get_scene_presence_filter = 0;
                foreach (ScenePresence pre in presences)
                {
                    IClientAPI client = pre.ControllingClient;
                    //if (pre.MovementFlag!=0 && client.PacketProcessingEnabled==true)
                    if (client.IsActive)
                    {
                        get_scene_presence_filter++;
                    }
                }
                List<ScenePresence> avatars = scene.GetAvatars();
                int get_avatar = avatars.Count;
                int get_avatar_filter = 0;
                string avatar_names = "";
                foreach (ScenePresence pre in avatars)
                {
                    IClientAPI client = pre.ControllingClient;
                    //if (pre.MovementFlag!=0 && client.PacketProcessingEnabled==true)
                    if (client.IsActive)
                    {
                        get_avatar_filter++;
                        avatar_names += pre.Firstname + " " + pre.Lastname + "; ";
                    }
                }

                Hashtable responseData = new Hashtable();
                responseData["get_scene_presence_filter"] = get_scene_presence_filter;
                responseData["get_scene_presence"] = get_scene_presence;
                responseData["get_avatar_filter"] = get_avatar_filter;
                responseData["get_avatar"] = get_avatar;
                responseData["avatar_names"] = avatar_names;
                response.Value = responseData;

                m_log.Info("[BALANCER] " + "Exiting RegionStatus()");
            }
            catch (Exception e)
            {
                m_log.Error("[BALANCER] " + e);
                m_log.Error("[BALANCER] " + e.StackTrace);
            }
            return response;
        }

        private XmlRpcResponse SerializeRegion(XmlRpcRequest request)
        {
            try
            {
                m_log.Info("[BALANCER] " + "Entering SerializeRegion()");

                string src_url = (string) request.Params[0];
                int src_port = (int) request.Params[1];

                SerializeRegion(src_url, src_port);

                m_log.Info("[BALANCER] " + "Exiting SerializeRegion()");
            }
            catch (Exception e)
            {
                m_log.Error("[BALANCER] " + e);
                m_log.Error("[BALANCER] " + e.StackTrace);
            }

            return new XmlRpcResponse();
        }

        private XmlRpcResponse DeserializeRegion_Move(XmlRpcRequest request)
        {
            try
            {
                m_log.Info("[BALANCER] " + "Entering DeserializeRegion_Move()");

                string src_url = (string) request.Params[0];
                int src_port = (int) request.Params[1];
                string dst_url = (string) request.Params[2];
                int dst_port = (int) request.Params[3];

                DeserializeRegion_Move(src_port, dst_port, src_url, dst_url);

                m_log.Info("[BALANCER] " + "Exiting DeserializeRegion_Move()");
            }
            catch (Exception e)
            {
                m_log.Error("[BALANCER] " + e);
                m_log.Error("[BALANCER] " + e.StackTrace);
            }

            return new XmlRpcResponse();
        }

        private XmlRpcResponse DeserializeRegion_Clone(XmlRpcRequest request)
        {
            try
            {
                m_log.Info("[BALANCER] " + "Entering DeserializeRegion_Clone()");

                string src_url = (string) request.Params[0];
                int src_port = (int) request.Params[1];
                string dst_url = (string) request.Params[2];
                int dst_port = (int) request.Params[3];

                DeserializeRegion_Clone(src_port, dst_port, src_url, dst_url);

                m_log.Info("[BALANCER] " + "Exiting DeserializeRegion_Clone()");
            }
            catch (Exception e)
            {
                m_log.Error("[BALANCER] " + e);
                m_log.Error("[BALANCER] " + e.StackTrace);
                throw;
            }

            return new XmlRpcResponse();
        }

        private XmlRpcResponse TerminateRegion(XmlRpcRequest request)
        {
            try
            {
                m_log.Info("[BALANCER] " + "Entering TerminateRegion()");

                int src_port = (int) request.Params[0];

                // backgroud
                WaitCallback callback = TerminateRegion;
                ThreadPool.QueueUserWorkItem(callback, src_port);

                m_log.Info("[BALANCER] " + "Exiting TerminateRegion()");
            }
            catch (Exception e)
            {
                m_log.Error("[BALANCER] " + e);
                m_log.Error("[BALANCER] " + e.StackTrace);
            }

            return new XmlRpcResponse();
        }

        // internal functions

        private void SerializeRegion(string src_url, int src_port)
        {
            //------------------------------------------
            // Processing of origin region
            //------------------------------------------

            // search origin region
            RegionInfo src_region = SearchRegionFromPortNum(src_port);

            if (src_region == null)
            {
                m_log.Error("[BALANCER] " + "Region not found");
                return;
            }

            Util.XmlRpcCommand(src_region.proxyUrl, "BlockClientMessages", src_url, src_port + proxyOffset);

            // serialization of origin region's data
            SerializeRegion(src_region, serializeDir);
        }

        private void DeserializeRegion_Move(int src_port, int dst_port, string src_url, string dst_url)
        {
            //------------------------------------------
            // Processing of destination region
            //------------------------------------------

            // import the source region's data
            RegionInfo dst_region = DeserializeRegion(dst_port, serializeDir);

            Util.XmlRpcCommand(dst_region.proxyUrl, "ChangeRegion", src_port + proxyOffset, src_url, dst_port + proxyOffset, dst_url);
            Util.XmlRpcCommand(dst_region.proxyUrl, "UnblockClientMessages", dst_url, dst_port + proxyOffset);
        }

        private void DeserializeRegion_Clone(int src_port, int dst_port, string src_url, string dst_url)
        {
            //------------------------------------------
            // Processing of destination region
            //------------------------------------------

            // import the source region's data
            RegionInfo dst_region = DeserializeRegion(dst_port, serializeDir);

            // Decide who is in charge for each section
            int[] port = new int[] {src_port, dst_port};
            string[] url = new string[] {"http://" + src_url + ":" + commandServer.Port, "http://" + dst_url + ":" + commandServer.Port};
            for (int i = 0; i < 2; i++) Util.XmlRpcCommand(url[i], "SplitRegion", i, 2, port[0], port[1], url[0], url[1]);

            // Enable the proxy
            Util.XmlRpcCommand(dst_region.proxyUrl, "AddRegion", src_port + proxyOffset, src_url, dst_port + proxyOffset, dst_url);
            Util.XmlRpcCommand(dst_region.proxyUrl, "UnblockClientMessages", dst_url, dst_port + proxyOffset);
        }

        private void TerminateRegion(object param)
        {
            int src_port = (int) param;

            //------------------------------------------
            // Processing of remove region
            //------------------------------------------

            // search origin region
            RegionInfo src_region = SearchRegionFromPortNum(src_port);

            if (src_region == null)
            {
                m_log.Error("[BALANCER] " + "Region not found");
                return;
            }

            isSplit = false;

            // remove client resources
            RemoveAllClientResource(src_region);
            // remove old region
            RemoveRegion(src_region.RegionID, src_region.InternalEndPoint.Port);

            m_log.Info("[BALANCER] " + "Region terminated");
        }

        private RegionInfo SearchRegionFromPortNum(int portnum)
        {
            RegionInfo result = null;

            foreach (RegionInfo rinfo in regionData)
            {
                if (rinfo.InternalEndPoint.Port == portnum)
                {
//                    m_log.Info("BALANCER",
//                            "Region found. Internal Port = {0}, Handle={1}",
//                            rinfo.InternalEndPoint.Port, rinfo.RegionHandle);
                    result = rinfo;
                    break;
                }
            }

            return result;
        }

        private IClientNetworkServer SearchClientServerFromPortNum(int portnum)
        {
            return m_clientServers.Find(
                delegate(IClientNetworkServer server)
                    {
// ReSharper disable PossibleNullReferenceException
                        return (portnum + proxyOffset == ((IPEndPoint) server.Server.LocalEndPoint).Port);
// ReSharper restore PossibleNullReferenceException
                    }
                );
        }

        private void SerializeRegion(RegionInfo src_region, string export_dir)
        {
            Scene scene;
            int i = 0;

            // try to get the scene object
            if (sceneManager.TryGetScene(src_region.RegionID, out scene) == false)
            {
                m_log.Error("[BALANCER] " + "The Scene is not found");
                return;
            }

            // create export directory
            DirectoryInfo dirinfo = new DirectoryInfo(export_dir);
            if (!dirinfo.Exists)
            {
                dirinfo.Create();
            }

            // serialization of client's informations
            List<ScenePresence> presences = scene.GetScenePresences();

            foreach (ScenePresence pre in presences)
            {
                SerializeClient(i, scene, pre, export_dir);
                i++;
            }

            // serialization of region data
            SerializableRegionInfo dst_region = new SerializableRegionInfo(src_region);

            string filename = export_dir + "RegionInfo_" + src_region.RegionID + ".bin";
            Util.SerializeToFile(filename, dst_region);

            // backup current scene's entities
            //scene.Backup();

            m_log.InfoFormat("[BALANCER] " + "region serialization completed [{0}]",
                             src_region.RegionID.ToString());
        }

        private static void SerializeClient(int idx, IScene scene, EntityBase pre, string export_dir)
        {
            string filename;
            IClientAPI controller;

            m_log.InfoFormat("[BALANCER] " + "agent id : {0}", pre.UUID);

            uint[] circuits = scene.ClientManager.GetAllCircuits(pre.UUID);

            foreach (uint code in circuits)
            {
                m_log.InfoFormat("[BALANCER] " + "circuit code : {0}", code);

                if (scene.ClientManager.TryGetClient(code, out controller))
                {
                    ClientInfo info = controller.GetClientInfo();

                    filename = export_dir + "ClientInfo-" + String.Format("{0:0000}", idx) + "_" + controller.CircuitCode + ".bin";

                    Util.SerializeToFile(filename, info);

                    m_log.InfoFormat("[BALANCER] " + "client info serialized [filename={0}]", filename);
                }
            }

            //filename = export_dir + "Presence_" + controller.AgentId.ToString() + ".bin";
            filename = export_dir + "Presence_" + String.Format("{0:0000}", idx) + ".bin";

            Util.SerializeToFile(filename, pre);

            m_log.InfoFormat("[BALANCER] " + "scene presence serialized [filename={0}]", filename);
        }

        private RegionInfo DeserializeRegion(int dst_port, string import_dir)
        {
            RegionInfo dst_region = null;

            try
            {
                // deserialization of region data
                string[] files = Directory.GetFiles(import_dir, "RegionInfo_*.bin");

                foreach (string filename in files)
                {
                    m_log.InfoFormat("[BALANCER] RegionInfo filename = [{0}]", filename);

                    dst_region = new RegionInfo((SerializableRegionInfo) Util.DeserializeFromFile(filename));

                    m_log.InfoFormat("[BALANCER] " + "RegionID = [{0}]", dst_region.RegionID.ToString());
                    m_log.InfoFormat("[BALANCER] " + "RegionHandle = [{0}]", dst_region.RegionHandle);
                    m_log.InfoFormat("[BALANCER] " + "ProxyUrl = [{0}]", dst_region.proxyUrl);
                    m_log.InfoFormat("[BALANCER] " + "OriginRegionID = [{0}]", dst_region.originRegionID.ToString());

                    CreateCloneRegion(dst_region, dst_port, true);

                    File.Delete(filename);

                    m_log.InfoFormat("[BALANCER] " + "region deserialized [{0}]", dst_region.RegionID);
                }

                if (dst_region != null)
                {
                    // deserialization of client data
                    DeserializeClient(dst_region, import_dir);

                    m_log.InfoFormat("[BALANCER] " + "region deserialization completed [{0}]",
                                     dst_region.ToString());
                }
            }
            catch (Exception e)
            {
                m_log.Error("[BALANCER] " + e);
                m_log.Error("[BALANCER] " + e.StackTrace);
                throw;
            }

            return dst_region;
        }

        private void DeserializeClient(SimpleRegionInfo dst_region, string import_dir)
        {
            ScenePresence sp;
            ClientInfo data;
            Scene scene;
            IClientAPI controller;

            if (sceneManager.TryGetScene(dst_region.RegionID, out scene))
            {
                IClientNetworkServer clientserv = SearchClientServerFromPortNum(scene.RegionInfo.InternalEndPoint.Port);

                // restore the scene presence
                for (int i = 0;; i++)
                {
                    string filename = import_dir + "Presence_" + String.Format("{0:0000}", i) + ".bin";

                    if (!File.Exists(filename))
                    {
                        break;
                    }

                    sp = (ScenePresence) Util.DeserializeFromFile(filename);
                    Console.WriteLine("agent id = {0}", sp.UUID);

                    scene.m_restorePresences.Add(sp.UUID, sp);
                    File.Delete(filename);

                    m_log.InfoFormat("[BALANCER] " + "scene presence deserialized [{0}]", sp.UUID);

                    // restore the ClientView

                    string[] files = Directory.GetFiles(import_dir, "ClientInfo-" + String.Format("{0:0000}", i) + "_*.bin");

                    foreach (string fname in files)
                    {
                        int start = fname.IndexOf('_');
                        int end = fname.LastIndexOf('.');
                        uint circuit_code = uint.Parse(fname.Substring(start + 1, end - start - 1));
                        m_log.InfoFormat("[BALANCER] " + "client circuit code = {0}", circuit_code);

                        data = (ClientInfo) Util.DeserializeFromFile(fname);

                        AgentCircuitData agentdata = new AgentCircuitData(data.agentcircuit);
                        scene.AuthenticateHandler.AddNewCircuit(circuit_code, agentdata);

                        // TODO: This needs to be abstracted and converted into IClientNetworkServer
                        if (clientserv is LLUDPServer)
                        {
                            ((LLUDPServer) clientserv).RestoreClient(agentdata, data.userEP, data.proxyEP);
                        }

                        // waiting for the scene-presense restored
                        lock (scene.m_restorePresences)
                        {
                            Monitor.Wait(scene.m_restorePresences, 3000);
                        }

                        if (scene.ClientManager.TryGetClient(circuit_code, out controller))
                        {
                            m_log.InfoFormat("[BALANCER] " + "get client [{0}]", circuit_code);
                            controller.SetClientInfo(data);
                        }

                        File.Delete(fname);

                        m_log.InfoFormat("[BALANCER] " + "client info deserialized [{0}]", circuit_code);
                    }

                    // backup new scene's entities
                    //scene.Backup();
                }
            }
        }

        private void CreateCloneRegion(RegionInfo dst_region, int dst_port, bool createID_flag)
        {
            if (createID_flag)
            {
                dst_region.RegionID = LLUUID.Random();
            }

            // change RegionInfo (memory only)
            dst_region.InternalEndPoint.Port = dst_port;
            dst_region.ExternalHostName = proxyURL.Split(new char[] {'/', ':'})[3];

            // Create new region
            simMain.CreateRegion(dst_region, false);
        }

        private void RemoveRegion(LLUUID regionID, int port)
        {
            Scene killScene;
            if (sceneManager.TryGetScene(regionID, out killScene))
            {
                Console.WriteLine("scene found.");

                if ((sceneManager.CurrentScene != null)
                    && (sceneManager.CurrentScene.RegionInfo.RegionID == killScene.RegionInfo.RegionID))
                {
                    sceneManager.TrySetCurrentScene("..");
                }

                m_log.Info("Removing region : " + killScene.RegionInfo.RegionName);
                regionData.Remove(killScene.RegionInfo);
                sceneManager.CloseScene(killScene);
            }

            // Shutting down the UDP server
            IClientNetworkServer clientsvr = SearchClientServerFromPortNum(port);

            if (clientsvr != null)
            {
                clientsvr.Server.Close();
                m_clientServers.Remove(clientsvr);
            }
        }

        private void RemoveAllClientResource(SimpleRegionInfo src_region)
        {
            Scene scene;
            IClientAPI controller;

            // try to get the scene object
            if (sceneManager.TryGetScene(src_region.RegionID, out scene) == false)
            {
                m_log.Error("[BALANCER] " + "The Scene is not found");
                return;
            }

            // serialization of client's informations
            List<ScenePresence> presences = scene.GetScenePresences();

            // remove all scene presences
            foreach (ScenePresence pre in presences)
            {
                uint[] circuits = scene.ClientManager.GetAllCircuits(pre.UUID);

                foreach (uint code in circuits)
                {
                    m_log.InfoFormat("[BALANCER] " + "circuit code : {0}", code);

                    if (scene.ClientManager.TryGetClient(code, out controller))
                    {
                        // stopping clientview thread
                        if ((controller).IsActive)
                        {
                            controller.Stop();
                            (controller).IsActive = false;
                        }
                        // teminateing clientview thread
                        controller.Terminate();
                        m_log.Info("[BALANCER] " + "client thread stopped");
                    }
                }

                // remove scene presence
                scene.RemoveClient(pre.UUID);
            }
        }

        /*
         * This section implements scene splitting and synchronization
         */

        private XmlRpcResponse SplitRegion(XmlRpcRequest request)
        {
            try
            {
                int myID = (int) request.Params[0];
                int numRegions = (int) request.Params[1];
                regionPortList = new int[numRegions];
                sceneURL = new string[numRegions];
                tcpClientList = new TcpClient[numRegions];

                for (int i = 0; i < numRegions; i++)
                {
                    regionPortList[i] = (int) request.Params[i + 2];
                    sceneURL[i] = (string) request.Params[i + 2 + numRegions];
                }

                for (int i = 0; i < numRegions; i++)
                {
                    string hostname = sceneURL[i].Split(new char[] {'/', ':'})[3];
                    m_log.InfoFormat("[SPLITSCENE] " + "creating tcp client host:{0}", hostname);
                    tcpClientList[i] = new TcpClient(hostname, 10001);
                }

                bool isMaster = (myID == 0);

                isLocalNeighbour = new bool[numRegions];
                for (int i = 0; i < numRegions; i++) isLocalNeighbour[i] = (sceneURL[i] == sceneURL[myID]);

                RegionInfo region = SearchRegionFromPortNum(regionPortList[myID]);

                //Console.WriteLine("\n === SplitRegion {0}\n", region.RegionID);

                Scene scene;
                if (sceneManager.TryGetScene(region.RegionID, out scene))
                {
                    // Disable event updates, backups etc in the slave(s)
                    scene.Region_Status = isMaster ? RegionStatus.Up : RegionStatus.SlaveScene;

                    //Console.WriteLine("=== SplitRegion {0}: Scene found, status {1}", region.RegionID, scene.Region_Status);

                    // Disabling half of the avatars in master, and the other half in slave

                    int i = 0;

                    List<uint> circuits = scene.ClientManager.GetAllCircuitCodes();
                    circuits.Sort();

                    foreach (uint code in circuits)
                    {
                        m_log.InfoFormat("[BALANCER] " + "circuit code : {0}", code);

                        IClientAPI controller;
                        if (scene.ClientManager.TryGetClient(code, out controller))
                        {
                            // Divide the presences evenly over the set of subscenes
                            LLClientView client = (LLClientView) controller;
                            client.IsActive = (((i + myID) % sceneURL.Length) == 0);

                            m_log.InfoFormat("[SPLITSCENE] === SplitRegion {0}: SP.PacketEnabled {1}", region.RegionID, client.IsActive);

                            if (!client.IsActive)
                            {
                                // stopping clientview thread
                                client.Stop();
                            }

                            ++i;
                        }
                    }

                    scene.splitID = myID;
                    scene.SynchronizeScene = SynchronizeScenes;
                    isSplit = true;
                }
                else
                {
                    m_log.Error("[SPLITSCENE] " + String.Format("Scene not found {0}", region.RegionID));
                }
            }
            catch (Exception e)
            {
                m_log.Error("[SPLITSCENE] " + e);
                m_log.Error("[SPLITSCENE] " + e.StackTrace);
            }

            return new XmlRpcResponse();
        }

        private XmlRpcResponse MergeRegions(XmlRpcRequest request)
        {
            // This should only be called for the master scene
            try
            {
                m_log.Info("[BALANCER] " + "Entering MergeRegions()");

                string src_url = (string) request.Params[0];
                int src_port = (int) request.Params[1];

                RegionInfo region = SearchRegionFromPortNum(src_port);

                Util.XmlRpcCommand(region.proxyUrl, "BlockClientMessages", src_url, src_port + proxyOffset);

                Scene scene;
                if (sceneManager.TryGetScene(region.RegionID, out scene))
                {
                    isSplit = false;

                    scene.SynchronizeScene = null;
                    scene.Region_Status = RegionStatus.Up;

                    List<ScenePresence> presences = scene.GetScenePresences();
                    foreach (ScenePresence pre in presences)
                    {
                        LLClientView client = (LLClientView) pre.ControllingClient;
                        if (!client.IsActive)
                        {
                            client.Restart();
                            client.IsActive = true;
                        }
                    }
                }

                // Delete the slave scenes
                for (int i = 1; i < sceneURL.Length; i++)
                {
                    string url = (sceneURL[i].Split('/')[2]).Split(':')[0]; // get URL part from EP
                    Util.XmlRpcCommand(region.proxyUrl, "DeleteRegion", regionPortList[i] + proxyOffset, url);
                    Thread.Sleep(1000);
                    Util.XmlRpcCommand(sceneURL[i], "TerminateRegion", regionPortList[i]); // TODO: need + proxyOffset?
                }

                Util.XmlRpcCommand(region.proxyUrl, "UnblockClientMessages", src_url, src_port + proxyOffset);
            }
            catch (Exception e)
            {
                m_log.Error("[BALANCER] " + e);
                m_log.Error("[BALANCER] " + e.StackTrace);
                throw;
            }

            return new XmlRpcResponse();
        }

        private XmlRpcResponse UpdatePhysics(XmlRpcRequest request)
        {
            // this callback receives physic scene updates from the other sub-scenes (in split mode)

            int regionPort = (int) request.Params[0];
            LLUUID scenePresenceID = new LLUUID((byte[]) request.Params[1], 0);
            LLVector3 position = new LLVector3((byte[]) request.Params[2], 0);
            LLVector3 velocity = new LLVector3((byte[]) request.Params[3], 0);
            bool flying = (bool) request.Params[4];

            LocalUpdatePhysics(regionPort, scenePresenceID, position, velocity, flying);

            return new XmlRpcResponse();
        }

        private void LocalUpdatePhysics(int regionPort, LLUUID scenePresenceID, LLVector3 position, LLVector3 velocity, bool flying)
        {
            //m_log.Info("[SPLITSCENE] "+String.Format("UpdatePhysics called {0}", regionID));

            //m_log.Info("[SPLITSCENE] "+"LocalUpdatePhysics [region port:{0}, client:{1}, position:{2}, velocity:{3}, flying:{4}]",
            //                                       regionPort, scenePresenceID.ToString(), position.ToString(),
            //                                       velocity.ToString(), flying);

            RegionInfo region = SearchRegionFromPortNum(regionPort);

            // Find and update the scene precense
            Scene scene;
            if (sceneManager.TryGetScene(region.RegionID, out scene))
            {
                ScenePresence pre = scene.GetScenePresences().Find(delegate(ScenePresence x) { return x.UUID == scenePresenceID; });

                if (pre == null)
                {
                    m_log.ErrorFormat("[SPLITSCENE] [LocalUpdatePhysics] ScenePresence is missing... ({0})", scenePresenceID.ToString());
                    return;
                }

//                m_log.Info("[SPLITSCENE] "+"LocalUpdatePhysics [region:{0}, client:{1}]",
//                                                                             regionID.ToString(), pre.UUID.ToString());

                pre.AbsolutePosition = position; // will set PhysicsActor.Position
                pre.Velocity = velocity; // will set PhysicsActor.Velocity
                pre.PhysicsActor.Flying = flying;
            }
        }

        private void SynchronizeScenes(Scene scene)
        {
            if (!isSplit)
            {
                return;
            }

            lock (padlock)
            {
                // Callback activated after a physics scene update
//                int i = 0;
                List<ScenePresence> presences = scene.GetScenePresences();
                foreach (ScenePresence pre in presences)
                {
                    LLClientView client = (LLClientView) pre.ControllingClient;

                    // Because data changes by the physics simulation when the client doesn't move,
                    // if MovementFlag is false, It is necessary to synchronize.
                    //if (pre.MovementFlag!=0 && client.PacketProcessingEnabled==true)
                    if (client.IsActive)
                    {
                        //m_log.Info("[SPLITSCENE] "+String.Format("Client moving in {0} {1}", scene.RegionInfo.RegionID, pre.AbsolutePosition));

                        for (int i = 0; i < sceneURL.Length; i++)
                        {
                            if (i == scene.splitID)
                            {
                                continue;
                            }

                            if (isLocalNeighbour[i])
                            {
                                //m_log.Info("[SPLITSCENE] "+"Synchronize ScenePresence (Local) [region:{0}=>{1}, client:{2}]",
                                //                                             scene.RegionInfo.RegionID, regionPortList[i], pre.UUID.ToString());
                                LocalUpdatePhysics(regionPortList[i], pre.UUID, pre.AbsolutePosition, pre.Velocity, pre.PhysicsActor.Flying);
                            }
                            else
                            {
                                //m_log.Info("[SPLITSCENE] "+"Synchronize ScenePresence (Remote) [region port:{0}, client:{1}, position:{2}, velocity:{3}, flying:{4}]",
                                //                                   regionPortList[i], pre.UUID.ToString(), pre.AbsolutePosition.ToString(),
                                //                                   pre.Velocity.ToString(), pre.PhysicsActor.Flying);


                                Util.XmlRpcCommand(sceneURL[i], "UpdatePhysics",
                                                   regionPortList[i], pre.UUID.GetBytes(),
                                                   pre.AbsolutePosition.GetBytes(), pre.Velocity.GetBytes(),
                                                   pre.PhysicsActor.Flying);

/*
  byte[] buff = new byte[12+12+1];

  Buffer.BlockCopy(pre.AbsolutePosition.GetBytes(), 0, buff, 0, 12);
  Buffer.BlockCopy(pre.Velocity.GetBytes(), 0, buff, 12, 12);
  buff[24] = (byte)((pre.PhysicsActor.Flying)?1:0);

  // create header
  InternalPacketHeader header = new InternalPacketHeader();

  header.type = 1;
  header.throttlePacketType = 0;
  header.numbytes = buff.Length;
  header.agent_id = pre.UUID.UUID;
  header.region_port = regionPortList[i];

  //Send
  tcpClientList[i].send(header, buff);
*/
                            }
                        }
                    }
//                    ++i;
                }
            }
        }

        public bool SynchronizePackets(IScene scene, Packet packet, LLUUID agentID, ThrottleOutPacketType throttlePacketType)
        {
            if (!isSplit)
            {
                return false;
            }

            Scene localScene = (Scene) scene;

            for (int i = 0; i < sceneURL.Length; i++)
            {
                if (i == localScene.splitID)
                {
                    continue;
                }

                if (isLocalNeighbour[i])
                {
                    //m_log.Info("[SPLITSCENE] "+"Synchronize Packet (Local) [type:{0}, client:{1}]",
                    //                packet.Type.ToString(), agentID.ToString());
                    LocalUpdatePacket(regionPortList[i], agentID, packet, throttlePacketType);
                }
                else
                {
                    //m_log.Info("[SPLITSCENE] "+"Synchronize Packet (Remote) [type:{0}, client:{1}]",
                    //                            packet.Type.ToString(), agentID.ToString());
                    // to bytes
                    byte[] buff = packet.ToBytes();

                    // create header
                    InternalPacketHeader header = new InternalPacketHeader();

                    header.type = 0;
                    header.throttlePacketType = (int) throttlePacketType;
                    header.numbytes = buff.Length;
                    header.agent_id = agentID.UUID;
                    header.region_port = regionPortList[i];

                    //Send
                    tcpClientList[i].send(header, buff);

                    PacketPool.Instance.ReturnPacket(packet);
                }
            }

            return true;
        }

        private void LocalUpdatePacket(int regionPort, LLUUID agentID, Packet packet, ThrottleOutPacketType throttlePacketType)
        {
            Scene scene;

            RegionInfo region = SearchRegionFromPortNum(regionPort);

//            m_log.Info("[SPLITSCENE] "+"LocalUpdatePacket [region port:{0}, client:{1}, packet type:{2}]",
//                                                          regionPort, agentID.ToString(), packet.GetType().ToString());

            if (sceneManager.TryGetScene(region.RegionID, out scene))
            {
                ScenePresence pre = scene.GetScenePresences().Find(delegate(ScenePresence x) { return x.UUID == agentID; });

                if (pre == null)
                {
                    m_log.ErrorFormat("[SPLITSCENE] [LocalUpdatePacket] ScenePresence is missing... ({0})", agentID.ToString());
                    return;
                }
                if (pre.ControllingClient is LLClientView)
                {
                    if (((LLClientView)pre.ControllingClient).IsActive)
                    {
                        ((LLClientView)pre.ControllingClient).OutPacket(packet, throttlePacketType);
                    }
                    else
                    {
                        PacketPool.Instance.ReturnPacket(packet);
                    }
                }
                else
                {
                    PacketPool.Instance.ReturnPacket(packet);
                }
            }
        }

        public void SynchronizePacketRecieve(InternalPacketHeader header, byte[] buff)
        {
//            m_log.Info("[SPLITSCENE] "+"entering SynchronizePacketRecieve[type={0}]", header.type);

            if (!isSplit)
            {
                return;
            }

            switch (header.type)
            {
                case 0:

                    byte[] zero = new byte[3000];

                    // deserialize packet
                    int packetEnd = buff.Length - 1;
//                    packetEnd = buff.Length;

                    try
                    {
                        //m_log.Info("[SPLITSCENE] "+"PacketPool.Instance : {0}", (PacketPool.Instance == null)?"null":"not null");
                        //m_log.Info("[SPLITSCENE] "+"buff length={0}", buff.Length);

                        Packet packet = PacketPool.Instance.GetPacket(buff, ref packetEnd, zero);

                        LocalUpdatePacket(header.region_port, new LLUUID(header.agent_id),
                                          packet, (ThrottleOutPacketType) header.throttlePacketType);
                    }
                    catch (Exception e)
                    {
                        m_log.Error("[SPLITSCENE] " + e);
                        m_log.Error("[SPLITSCENE] " + e.StackTrace);
                    }

                    break;

                case 1:

                    int regionPort = header.region_port;
                    LLUUID scenePresenceID = new LLUUID(header.agent_id);
                    LLVector3 position = new LLVector3(buff, 0);
                    LLVector3 velocity = new LLVector3(buff, 12);
                    bool flying = ((buff[24] == 1) ? true : false);

                    LocalUpdatePhysics(regionPort, scenePresenceID, position, velocity, flying);

                    break;

                default:
                    m_log.Info("[SPLITSCENE] " + "Invalid type");
                    break;
            }

//            m_log.Info("[SPLITSCENE] "+"exiting SynchronizePacketRecieve");
        }
    }
}
