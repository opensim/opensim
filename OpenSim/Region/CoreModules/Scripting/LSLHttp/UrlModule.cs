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
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Scripting.LSLHttp
{
    public class UrlData
    {
        public UUID hostID;
        public UUID itemID;
        Scene scene;
        IScriptModule engine;
        string url;
        Dictionary<UUID, RequestData> requests;
    }

    public class RequestData
    {
        UUID requestID;
        Dictionary<string, string> headers;
        string body;
    }

    public class UrlModule : ISharedRegionModule, IUrlModule
    {
        private Dictionary<UUID, UrlData> m_RequestMap =
                new Dictionary<UUID, UrlData>();

        private Dictionary<string, UrlData> m_UrlMap =
                new Dictionary<string, UrlData>();

        private int m_TotalUrls = 0;

        public string Name
        {
            get { return "UrlModule"; }
        }

        public void Initialise(IConfigSource config)
        {
        }

        public void PostInitialise()
        {
        }

        public void AddRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void Close()
        {
        }

        public UUID RequestURL(IScriptModule engine, SceneObjectPart host, UUID itemID)
        {
            return UUID.Zero;
        }

        public UUID RequestSecureURL(IScriptModule engine, SceneObjectPart host, UUID itemID)
        {
            return UUID.Zero;
        }

        public void ReleaseURL(string url)
        {
        }

        public void HttpResponse(UUID request, int status, string body)
        {
        }

        public string GetHttpHeader(UUID request, string header)
        {
            return String.Empty;
        }

        public int GetFreeUrls()
        {
            return 0;
        }
    }
}
