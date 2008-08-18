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

using Nwc.XmlRpc;
using System;
using System.Net;
using System.IO;
using System.Xml;
using System.Collections;
using System.Collections.Generic;
using libsecondlife;

namespace OpenGridServices.Manager
{
    public class GridServerConnectionManager
    {
        private string ServerURL;
        public LLUUID SessionID;
        public bool connected=false;

        public RegionBlock[][] WorldMap;

        public bool Connect(string GridServerURL, string username, string password)
        {
            try
            {
                this.ServerURL=GridServerURL;
                Hashtable LoginParamsHT = new Hashtable();
                LoginParamsHT["username"]=username;
                LoginParamsHT["password"]=password;
                ArrayList LoginParams = new ArrayList();
                LoginParams.Add(LoginParamsHT);
                XmlRpcRequest GridLoginReq = new XmlRpcRequest("manager_login",LoginParams);
                XmlRpcResponse GridResp = GridLoginReq.Send(ServerURL,3000);
                if (GridResp.IsFault)
                {
                    connected=false;
                    return false;
                }
                else
                {
                    Hashtable gridrespData = (Hashtable)GridResp.Value;
                    this.SessionID = new LLUUID((string)gridrespData["session_id"]);
                    connected=true;
                    return true;
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
                connected=false;
                return false;
            }
        }

        public void DownloadMap()
        {
            System.Net.WebClient mapdownloader = new WebClient();
            Stream regionliststream = mapdownloader.OpenRead(ServerURL + "/regionlist");

            RegionBlock TempRegionData;

            XmlDocument doc = new XmlDocument();
            doc.Load(regionliststream);
            regionliststream.Close();
            XmlNode rootnode = doc.FirstChild;
            if (rootnode.Name != "regions")
            {
                // TODO - ERROR!
            }

            for (int i = 0; i <= rootnode.ChildNodes.Count; i++)
            {
                if (rootnode.ChildNodes.Item(i).Name != "region")
                {
                    // TODO - ERROR!
                }
                else
                {
                    TempRegionData = new RegionBlock();
                }
            }
        }

        public bool RestartServer()
        {
            return true;
        }

        public bool ShutdownServer()
        {
            try
            {
                Hashtable ShutdownParamsHT = new Hashtable();
                ArrayList ShutdownParams = new ArrayList();
                ShutdownParamsHT["session_id"]=this.SessionID.ToString();
                ShutdownParams.Add(ShutdownParamsHT);
                XmlRpcRequest GridShutdownReq = new XmlRpcRequest("shutdown",ShutdownParams);
                XmlRpcResponse GridResp = GridShutdownReq.Send(this.ServerURL, 3000);
                if (GridResp.IsFault)
                {
                    return false;
                }
                else
                {
                    connected=false;
                    return true;
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
        }

        public void DisconnectServer()
        {
            this.connected=false;
        }
    }
}
