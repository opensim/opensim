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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
using System.IO;

using libsecondlife;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Configuration;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Configuration;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Utilities;

/*
using System.Text;
using Db4objects.Db4o;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Communications.Caches;
*/
namespace OpenSim.Grid.AssetServer
{
    /// <summary>
    /// An asset server
    /// </summary>
    public class OpenAsset_Main : conscmd_callback
    {
        public AssetConfig m_config;

        public static OpenAsset_Main assetserver;

        private LogBase m_console;
        private IAssetServer m_assetServer;

        [STAThread]
        public static void Main(string[] args)
        {
            Console.WriteLine("Starting...\n");

            assetserver = new OpenAsset_Main();
            assetserver.Startup();

            assetserver.Work();
        }

        private void Work()
        {
            m_console.Notice("Enter help for a list of commands");

            while (true)
            {
                m_console.MainLogPrompt();
            }
        }

        private OpenAsset_Main()
        {
            if(!Directory.Exists(Util.logDir()))
            {
                Directory.CreateDirectory(Util.logDir());
            }
            m_console = new LogBase((Path.Combine(Util.logDir(),"opengrid-AssetServer-console.log")), "OpenAsset", this, true);
            MainLog.Instance = m_console;
        }

        public void Startup()
        {
            m_config = new AssetConfig("ASSET SERVER", (Path.Combine(Util.configDir(), "AssetServer_Config.xml")));

            m_console.Verbose("ASSET", "Setting up asset DB");
            setupDB(m_config);

            m_console.Verbose("ASSET", "Starting HTTP process");
            BaseHttpServer httpServer = new BaseHttpServer((int)m_config.HttpPort);

            httpServer.AddStreamHandler(new GetAssetStreamHandler(this));
            httpServer.AddStreamHandler(new PostAssetStreamHandler( this ));

            httpServer.Start();
        }

        public byte[] GetAssetData(LLUUID assetID, bool isTexture)
        {
            return null;
        }

        public void setupDB(AssetConfig config)
        {
            try
            {
                SQLAssetServer assetServer = new SQLAssetServer(config.DatabaseProvider );
                assetServer.LoadDefaultAssets();

                m_assetServer = assetServer;
            }
            catch (Exception e)
            {
                MainLog.Instance.Warn("ASSET", "setupDB() - Exception occured");
                MainLog.Instance.Warn("ASSET", e.ToString());
            }
        }

        public void RunCmd(string cmd, string[] cmdparams)
        {
            switch (cmd)
            {
                case "help":
                    m_console.Notice("shutdown - shutdown this asset server (USE CAUTION!)");
                    break;

                case "shutdown":
                    m_console.Close();
                    Environment.Exit(0);
                    break;
            }
        }

        public void Show(string ShowWhat)
        {
        }
    }

    public class GetAssetStreamHandler : BaseStreamHandler
    {
        OpenAsset_Main m_assetManager;

        override public byte[] Handle(string path, Stream request)
        {
            string param = GetParam(path);

            byte[] assetdata = m_assetManager.GetAssetData(new LLUUID(param), false);
            if (assetdata != null)
            {
                return assetdata;
            }
            else
            {
                return new byte[]{};
            }
        }

        public GetAssetStreamHandler(OpenAsset_Main assetManager) : base("/assets/", "GET")
        {
            m_assetManager = assetManager;
        }
    }

    public class PostAssetStreamHandler : BaseStreamHandler
    {
        OpenAsset_Main m_assetManager;

        override public byte[] Handle(string path, Stream request)
        {
            string param = GetParam(path);
            LLUUID assetId = new LLUUID(param);
            byte[] txBuffer = new byte[4096];
                
            using( BinaryReader binReader = new BinaryReader( request ) )
            {
                using (MemoryStream memoryStream = new MemoryStream(4096))
                {
                    int count;
                    while ((count = binReader.Read(txBuffer, 0, 4096)) > 0)
                    {
                        memoryStream.Write(txBuffer, 0, count);
                    }                    

                    byte[] assetData = memoryStream.ToArray();

//                    m_assetManager.CreateAsset(assetId, assetData);
                }
            }
            
            return new byte[]{};
        }

        public PostAssetStreamHandler( OpenAsset_Main assetManager )
            : base("/assets/", "POST")
        {
            m_assetManager = assetManager;
        }
    }
}
