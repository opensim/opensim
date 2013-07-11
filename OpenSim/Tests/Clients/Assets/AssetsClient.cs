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
using System.Net;
using System.Text;
using System.Reflection;
using System.Threading;

using OpenMetaverse;
using log4net;
using log4net.Appender;
using log4net.Layout;

using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors;

namespace OpenSim.Tests.Clients.AssetsClient
{
    public class AssetsClient
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private static int m_MaxThreadID = 0;
        private static readonly int NREQS = 150;
        private static int m_NReceived = 0;

        public static void Main(string[] args)
        {
            ConsoleAppender consoleAppender = new ConsoleAppender();
            consoleAppender.Layout =
                new PatternLayout("[%thread] - %message%newline");
            log4net.Config.BasicConfigurator.Configure(consoleAppender);

            string serverURI = "http://127.0.0.1:8003";
            if (args.Length > 1)
                serverURI = args[1];
            int max1, max2;
            ThreadPool.GetMaxThreads(out max1, out max2);
            m_log.InfoFormat("[ASSET CLIENT]: Connecting to {0} max threads = {1} - {2}", serverURI, max1, max2);
            ThreadPool.GetMinThreads(out max1, out max2);
            m_log.InfoFormat("[ASSET CLIENT]: Connecting to {0} min threads = {1} - {2}", serverURI, max1, max2);
            ThreadPool.SetMinThreads(1, 1);
            ThreadPool.SetMaxThreads(10, 3);
            ServicePointManager.DefaultConnectionLimit = 12;

            AssetServicesConnector m_Connector = new AssetServicesConnector(serverURI);
            m_Connector.MaxAssetRequestConcurrency = 30;

            for (int i = 0; i < NREQS; i++)
            {
                UUID uuid = UUID.Random();
                m_Connector.Get(uuid.ToString(), null, ResponseReceived);
                m_log.InfoFormat("[ASSET CLIENT]: [{0}] requested asset {1}", i, uuid);
            }

            Thread.Sleep(20 * 1000);
            m_log.InfoFormat("[ASSET CLIENT]: Received responses {0}", m_NReceived);
        }

        private static void ResponseReceived(string id, Object sender, AssetBase asset)
        {
            if (Thread.CurrentThread.ManagedThreadId > m_MaxThreadID)
                m_MaxThreadID = Thread.CurrentThread.ManagedThreadId;
            int max1, max2;
            ThreadPool.GetAvailableThreads(out max1, out max2);
            m_log.InfoFormat("[ASSET CLIENT]: Received asset {0} ({1}) ({2}-{3})", id, m_MaxThreadID, max1, max2);
            m_NReceived++;
        }
    }
}
