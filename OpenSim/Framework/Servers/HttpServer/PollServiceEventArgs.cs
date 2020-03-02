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
using System.Collections;
using OpenMetaverse;

namespace OpenSim.Framework.Servers.HttpServer
{
    public delegate void RequestMethod(UUID requestID, Hashtable request);
    public delegate bool HasEventsMethod(UUID requestID, UUID pId);

    public delegate Hashtable GetEventsMethod(UUID requestID, UUID pId);

    public delegate Hashtable NoEventsMethod(UUID requestID, UUID pId);
    public delegate void DropMethod(UUID requestID, UUID pId);

    public class PollServiceEventArgs : EventArgs
    {
        public HasEventsMethod HasEvents;
        public GetEventsMethod GetEvents;
        public NoEventsMethod NoEvents;
        public RequestMethod Request;
        public DropMethod Drop;
        public UUID Id;
        public int TimeOutms;
        //public EventType Type;

        public enum EventType : int
        {
            Poll = 0,
            LslHttp = 1,
            Inventory = 2,
            Texture = 3,
            Mesh = 4,
            Mesh2 = 5,
            Asset = 6
        }

        public string Url { get; set; }

        /// <summary>
        /// Number of requests received for this poll service.
        /// </summary>
        public int RequestsReceived { get; set; }

        /// <summary>
        /// Number of requests handled by this poll service.
        /// </summary>
        public int RequestsHandled { get; set; }

        public PollServiceEventArgs(
            RequestMethod pRequest,
            string pUrl,
            HasEventsMethod pHasEvents, GetEventsMethod pGetEvents, NoEventsMethod pNoEvents,
            DropMethod pDrop, UUID pId, int pTimeOutms)
        {
            Request = pRequest;
            Url = pUrl;
            HasEvents = pHasEvents;
            GetEvents = pGetEvents;
            NoEvents = pNoEvents;
            Drop = pDrop;
            Id = pId;
            TimeOutms = pTimeOutms;
            //Type = EventType.Poll;
        }
    }
}
