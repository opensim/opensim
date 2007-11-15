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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Region.ClientStack;
using OpenSim.Region.Communications.Local;
using OpenSim.Region.Environment;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Physics.Manager;

namespace SimpleApp
{
    internal class Program : RegionApplicationBase, conscmd_callback
    {
        private ModuleLoader m_moduleLoader;
        private IConfigSource m_config;

        protected override LogBase CreateLog()
        {
            return new LogBase(null, "SimpleApp", this, true);
        }

        protected override void Initialize()
        {
            StartLog();

            m_networkServersInfo = new NetworkServersInfo(1000, 1000);

            LocalAssetServer assetServer = new LocalAssetServer();

            m_assetCache = new AssetCache(assetServer, m_log);
        }

        public void Run()
        {
            base.StartUp();

            LocalInventoryService inventoryService = new LocalInventoryService();
            LocalUserServices userService =
                new LocalUserServices(m_networkServersInfo, m_networkServersInfo.DefaultHomeLocX,
                                      m_networkServersInfo.DefaultHomeLocY, inventoryService);
            LocalBackEndServices backendService = new LocalBackEndServices();

            CommunicationsLocal localComms =
                new CommunicationsLocal(m_networkServersInfo, m_httpServer, m_assetCache, userService, inventoryService,
                                        backendService, backendService, false);
            m_commsManager = localComms;

            LocalLoginService loginService =
                new LocalLoginService(userService, "", localComms, m_networkServersInfo, false);
            loginService.OnLoginToRegion += backendService.AddNewSession;

            m_httpServer.AddXmlRPCHandler("login_to_simulator", loginService.XmlRpcLoginMethod);

            m_log.Notice(m_log.LineInfo);

            IPEndPoint internalEndPoint =
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), m_networkServersInfo.HttpListenerPort);

            RegionInfo regionInfo = new RegionInfo(1000, 1000, internalEndPoint, "localhost");
            regionInfo.DataStore = "simpleapp_datastore.yap";

            UDPServer udpServer;

            m_moduleLoader = new ModuleLoader(m_log, m_config);
            m_moduleLoader.LoadDefaultSharedModules();

            Scene scene = SetupScene(regionInfo, out udpServer);

            m_moduleLoader.InitialiseSharedModules(scene);

            scene.SetModuleInterfaces();

            scene.StartTimer();

            m_sceneManager.Add(scene);

            m_moduleLoader.PostInitialise();
            m_moduleLoader.ClearCache();

            udpServer.ServerListener();

            LLVector3 pos = new LLVector3(110, 129, 27);

            SceneObjectGroup sceneObject =
                new CpuCounterObject(scene, regionInfo.RegionHandle, LLUUID.Zero, scene.PrimIDAllocate(),
                                     pos + new LLVector3(1f, 1f, 1f));
            scene.AddEntity(sceneObject);

            for (int i = 0; i < 27; i++)
            {
                LLVector3 posOffset = new LLVector3((i%3)*4, (i%9)/3*4, (i/9)*4);
                ComplexObject complexObject =
                    new ComplexObject(scene, regionInfo.RegionHandle, LLUUID.Zero, scene.PrimIDAllocate(),
                                      pos + posOffset);
                scene.AddEntity(complexObject);
            }

            for (int i = 0; i < 2; i++)
            {
                MyNpcCharacter m_character = new MyNpcCharacter(scene.EventManager);
                scene.AddNewClient(m_character, false);
            }

            List<ScenePresence> avatars = scene.GetAvatars();
            foreach (ScenePresence avatar in avatars)
            {
                avatar.AbsolutePosition =
                    new LLVector3((float) Util.RandomClass.Next(100, 200), (float) Util.RandomClass.Next(30, 200), 2);
            }


            DirectoryInfo dirInfo = new DirectoryInfo(".");

            float x = 0;
            float z = 0;

            foreach (FileInfo fileInfo in dirInfo.GetFiles())
            {
                LLVector3 filePos = new LLVector3(100 + x, 129, 27 + z);
                x = x + 2;
                if (x > 50)
                {
                    x = 0;
                    z = z + 2;
                }

                FileSystemObject fileObject = new FileSystemObject(scene, fileInfo, filePos);
                scene.AddEntity(fileObject);
            }

            m_log.Notice("Press enter to quit.");
            m_log.ReadLine();
        }

        protected override Scene CreateScene(RegionInfo regionInfo, StorageManager storageManager,
                                             AgentCircuitManager circuitManager)
        {
            SceneCommunicationService sceneGridService = new SceneCommunicationService(m_commsManager);
            return
                new MyWorld(regionInfo, circuitManager, m_commsManager, sceneGridService, m_assetCache, storageManager, m_httpServer,
                            new ModuleLoader(m_log, m_config), true);
        }

        protected override StorageManager CreateStorageManager(string connectionstring)
        {
            return new StorageManager("OpenSim.DataStore.NullStorage.dll", "simpleapp.yap");
        }

        protected override PhysicsScene GetPhysicsScene()
        {
            return GetPhysicsScene("basicphysics", "Meshmerizer");
        }

        #region conscmd_callback Members

        public void RunCmd(string cmd, string[] cmdparams)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void Show(string ShowWhat)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion

        private static void Main(string[] args)
        {
            Program app = new Program();

            app.Run();
        }
    }
}
