/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
using System.Text;
using Db4objects.Db4o;
using libsecondlife;
using OpenSim.Framework.Console;
using OpenSim.Framework.Types;

namespace OpenSim.Grid.AssetServer
{
    /// <summary>
    /// An asset server
    /// </summary>
    public class OpenAsset_Main :  conscmd_callback
    {
        private IObjectContainer db;

        public static OpenAsset_Main assetserver;

        private LogBase m_console;

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
            m_console = new LogBase("opengrid-AssetServer-console.log", "OpenAsset", this, false);
            MainLog.Instance = m_console;
        }

        public void Startup()
        {
            m_console.Verbose( "Main.cs:Startup() - Setting up asset DB");
            setupDB();

            m_console.Verbose( "Main.cs:Startup() - Starting HTTP process");
            AssetHttpServer httpServer = new AssetHttpServer(8003);


            httpServer.AddRestHandler("GET", "/assets/", this.assetGetMethod);
            httpServer.AddRestHandler("POST", "/assets/", this.assetPostMethod);

            httpServer.Start();

        }

        public string assetPostMethod(string requestBody, string path, string param)
        {
            AssetBase asset = new AssetBase();
            asset.Name = "";
            asset.FullID = new LLUUID(param);
            Encoding Windows1252Encoding = Encoding.GetEncoding(1252);
            byte[] buffer = Windows1252Encoding.GetBytes(requestBody);
            asset.Data = buffer;
            AssetStorage store = new AssetStorage();
            store.Data = asset.Data;
            store.Name = asset.Name;
            store.UUID = asset.FullID;
            db.Set(store);
            db.Commit();
            return "";
        }

        public string assetGetMethod(string request, string path, string param)
        {
            Console.WriteLine("got a request " + param);
            byte[] assetdata = getAssetData(new LLUUID(param), false);
            if (assetdata != null)
            {
                 Encoding Windows1252Encoding = Encoding.GetEncoding(1252);
                 string ret = Windows1252Encoding.GetString(assetdata);
                //string ret = System.Text.Encoding.Unicode.GetString(assetdata);

                return ret;
               
            }
            else
            {
                return "";
            }

        }

        public byte[] getAssetData(LLUUID assetID, bool isTexture)
        {
            bool found = false;
            AssetStorage foundAsset = null;

            IObjectSet result = db.Get(new AssetStorage(assetID));
            if (result.Count > 0)
            {
                foundAsset = (AssetStorage)result.Next();
                found = true;
            }

            if (found)
            {
                return foundAsset.Data;
            }
            else
            {
                return null;
            }
        }

        public void setupDB()
        {
            bool yapfile = File.Exists("assets.yap");
            try
            {
                db = Db4oFactory.OpenFile("assets.yap");
                MainLog.Instance.Verbose( "Main.cs:setupDB() - creation");
            }
            catch (Exception e)
            {
                db.Close();
                MainLog.Instance.Warn("Main.cs:setupDB() - Exception occured");
                MainLog.Instance.Warn(e.ToString());
            }
            if (!yapfile)
            {
                this.LoadDB();
            }
        }

        public void LoadDB()
        {
            try
            {

                Console.WriteLine("setting up Asset database");

                AssetBase Image = new AssetBase();
                Image.FullID = new LLUUID("00000000-0000-0000-9999-000000000001");
                Image.Name = "Bricks";
                this.LoadAsset(Image, true, "bricks.jp2");
                AssetStorage store = new AssetStorage();
                store.Data = Image.Data;
                store.Name = Image.Name;
                store.UUID = Image.FullID;
                db.Set(store);
                db.Commit();

                Image = new AssetBase();
                Image.FullID = new LLUUID("00000000-0000-0000-9999-000000000002");
                Image.Name = "Plywood";
                this.LoadAsset(Image, true, "plywood.jp2");
                store = new AssetStorage();
                store.Data = Image.Data;
                store.Name = Image.Name;
                store.UUID = Image.FullID;
                db.Set(store);
                db.Commit();

                Image = new AssetBase();
                Image.FullID = new LLUUID("00000000-0000-0000-9999-000000000003");
                Image.Name = "Rocks";
                this.LoadAsset(Image, true, "rocks.jp2");
                store = new AssetStorage();
                store.Data = Image.Data;
                store.Name = Image.Name;
                store.UUID = Image.FullID;
                db.Set(store);
                db.Commit();

                Image = new AssetBase();
                Image.FullID = new LLUUID("00000000-0000-0000-9999-000000000004");
                Image.Name = "Granite";
                this.LoadAsset(Image, true, "granite.jp2");
                store = new AssetStorage();
                store.Data = Image.Data;
                store.Name = Image.Name;
                store.UUID = Image.FullID;
                db.Set(store);
                db.Commit();

                Image = new AssetBase();
                Image.FullID = new LLUUID("00000000-0000-0000-9999-000000000005");
                Image.Name = "Hardwood";
                this.LoadAsset(Image, true, "hardwood.jp2");
                store = new AssetStorage();
                store.Data = Image.Data;
                store.Name = Image.Name;
                store.UUID = Image.FullID;
                db.Set(store);
                db.Commit();

                Image = new AssetBase();
                Image.FullID = new LLUUID("00000000-0000-0000-5005-000000000005");
                Image.Name = "Prim Base Texture";
                this.LoadAsset(Image, true, "plywood.jp2");
                store = new AssetStorage();
                store.Data = Image.Data;
                store.Name = Image.Name;
                store.UUID = Image.FullID;
                db.Set(store);
                db.Commit();

                Image = new AssetBase();
                Image.FullID = new LLUUID("66c41e39-38f9-f75a-024e-585989bfab73");
                Image.Name = "Shape";
                this.LoadAsset(Image, false, "base_shape.dat");
                store = new AssetStorage();
                store.Data = Image.Data;
                store.Name = Image.Name;
                store.UUID = Image.FullID;
                db.Set(store);
                db.Commit();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private void LoadAsset(AssetBase info, bool image, string filename)
        {


            string dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets"); //+ folder;
            string fileName = Path.Combine(dataPath, filename);
            FileInfo fInfo = new FileInfo(fileName);
            long numBytes = fInfo.Length;
            FileStream fStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            byte[] idata = new byte[numBytes];
            BinaryReader br = new BinaryReader(fStream);
            idata = br.ReadBytes((int)numBytes);
            br.Close();
            fStream.Close();
            info.Data = idata;
            //info.loaded=true;
        }

        /*private GridConfig LoadConfigDll(string dllName)
        {
            Assembly pluginAssembly = Assembly.LoadFrom(dllName);
            GridConfig config = null;

            foreach (Type pluginType in pluginAssembly.GetTypes())
            {
                if (pluginType.IsPublic)
                {
                    if (!pluginType.IsAbstract)
                    {
                        Type typeInterface = pluginType.GetInterface("IGridConfig", true);

                        if (typeInterface != null)
                        {
                            IGridConfig plug = (IGridConfig)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                            config = plug.GetConfigObject();
                            break;
                        }

                        typeInterface = null;
                    }
                }
            }
            pluginAssembly = null;
            return config;
        }*/

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
}
