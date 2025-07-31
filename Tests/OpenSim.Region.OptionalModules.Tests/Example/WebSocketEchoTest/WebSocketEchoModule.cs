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
using System.Reflection;
using OpenSim.Framework.Servers;
using Mono.Addins;
using log4net;
using Nini.Config;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

using OpenSim.Framework.Servers.HttpServer;


namespace OpenSim.Region.OptionalModules.WebSocketEchoModule
{

    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "WebSocketEchoModule")]
    public class WebSocketEchoModule : ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool enabled;
        public string Name { get { return "WebSocketEchoModule"; } }

        public Type ReplaceableInterface { get { return null; } }


        private HashSet<WebSocketHttpServerHandler> _activeHandlers = new HashSet<WebSocketHttpServerHandler>();

        public void Initialise(IConfigSource pConfig)
        {
            enabled = (pConfig.Configs["WebSocketEcho"] != null);
//            if (enabled)
//                m_log.DebugFormat("[WebSocketEchoModule]: INITIALIZED MODULE");
        }

        /// <summary>
        /// This method sets up the callback to WebSocketHandlerCallback below when a HTTPRequest comes in for /echo
        /// </summary>
        public void PostInitialise()
        {
            if (enabled)
                MainServer.Instance.AddWebSocketHandler("/echo", WebSocketHandlerCallback);
        }

        // This gets called by BaseHttpServer and gives us an opportunity to set things on the WebSocket handler before we turn it on
        public void WebSocketHandlerCallback(string path, WebSocketHttpServerHandler handler)
        {
            SubscribeToEvents(handler);
            handler.SetChunksize(8192);
            handler.NoDelay_TCP_Nagle = true;
            handler.HandshakeAndUpgrade();
        }

        //These are our normal events
        public void SubscribeToEvents(WebSocketHttpServerHandler handler)
        {
            handler.OnClose += HandlerOnOnClose;
            handler.OnText += HandlerOnOnText;
            handler.OnUpgradeCompleted += HandlerOnOnUpgradeCompleted;
            handler.OnData += HandlerOnOnData;
            handler.OnPong += HandlerOnOnPong;
        }

        public void UnSubscribeToEvents(WebSocketHttpServerHandler handler)
        {
            handler.OnClose -= HandlerOnOnClose;
            handler.OnText -= HandlerOnOnText;
            handler.OnUpgradeCompleted -= HandlerOnOnUpgradeCompleted;
            handler.OnData -= HandlerOnOnData;
            handler.OnPong -= HandlerOnOnPong;
        }

        private void HandlerOnOnPong(object sender, PongEventArgs pongdata)
        {
            m_log.Info("[WebSocketEchoModule]: Got a pong..  ping time: " + pongdata.PingResponseMS);
        }

        private void HandlerOnOnData(object sender, WebsocketDataEventArgs data)
        {
            WebSocketHttpServerHandler obj = sender as WebSocketHttpServerHandler;
            obj.SendData(data.Data);
            m_log.Info("[WebSocketEchoModule]: We received a bunch of ugly non-printable bytes");
            obj.SendPingCheck();
        }


        private void HandlerOnOnUpgradeCompleted(object sender, UpgradeCompletedEventArgs completeddata)
        {
            WebSocketHttpServerHandler obj = sender as WebSocketHttpServerHandler;
            _activeHandlers.Add(obj);
        }

        private void HandlerOnOnText(object sender, WebsocketTextEventArgs text)
        {
            WebSocketHttpServerHandler obj = sender as WebSocketHttpServerHandler;
            obj.SendMessage(text.Data);
            m_log.Info("[WebSocketEchoModule]: We received this: " + text.Data);
        }

        // Remove the references to our handler
        private void HandlerOnOnClose(object sender, CloseEventArgs closedata)
        {
            WebSocketHttpServerHandler obj = sender as WebSocketHttpServerHandler;
            UnSubscribeToEvents(obj);

            lock (_activeHandlers)
                _activeHandlers.Remove(obj);
            obj.Dispose();
        }

        // Shutting down..  so shut down all sockets.
        // Note..    this should be done outside of an ienumerable if you're also hook to the close event.
        public void Close()
        {
            if (!enabled)
                return;

            // We convert this to a for loop so we're not in in an IEnumerable when the close
            //call triggers an event which then removes item from _activeHandlers that we're enumerating
            WebSocketHttpServerHandler[] items = new WebSocketHttpServerHandler[_activeHandlers.Count];
            _activeHandlers.CopyTo(items);

            for (int i = 0; i < items.Length; i++)
            {
                items[i].Close(string.Empty);
                items[i].Dispose();
            }
            _activeHandlers.Clear();
            MainServer.Instance.RemoveWebSocketHandler("/echo");
        }

        public void AddRegion(Scene scene)
        {
//            m_log.DebugFormat("[WebSocketEchoModule]: REGION {0} ADDED", scene.RegionInfo.RegionName);
        }

        public void RemoveRegion(Scene scene)
        {
//            m_log.DebugFormat("[WebSocketEchoModule]: REGION {0} REMOVED", scene.RegionInfo.RegionName);
        }

        public void RegionLoaded(Scene scene)
        {
//            m_log.DebugFormat("[WebSocketEchoModule]: REGION {0} LOADED", scene.RegionInfo.RegionName);
        }
    }
}