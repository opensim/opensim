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
using System.Net;
using OpenMetaverse.StructuredData;

namespace OpenSim.Framework.Servers.HttpServer
{
    public sealed class ErrorCode
    {
        private ErrorCode() {}

        public const int ParseError = -32700;
        public const int InvalidRequest = -32600;
        public const int MethodNotFound = -32601;
        public const int InvalidParams = -32602;
        public const int InternalError = -32604;

    }

    public class JsonRpcError
    {
        internal OSDMap Error = new OSDMap();

        public int Code
        {
            get
            {
                if (Error.ContainsKey("code"))
                    return Error["code"].AsInteger();
                else
                    return 0;
            }
            set
            {
                Error["code"] = OSD.FromInteger(value);
            }
        }

        public string Message
        {
            get
            {
                if (Error.ContainsKey("message"))
                    return Error["message"].AsString();
                else
                    return null;
            }
            set
            {
                Error["message"] = OSD.FromString(value);
            }
        }

        public OSD Data
        {
            get; set;
        }
    }

    public class JsonRpcResponse
    {
        public string JsonRpc
        {
            get
            {
                return Reply["jsonrpc"].AsString();
            }
            set
            {
                Reply["jsonrpc"] = OSD.FromString(value);
            }
        }

        public string Id
        {
            get
            {
                return Reply["id"].AsString();
            }
            set
            {
                Reply["id"] = OSD.FromString(value);
            }
        }

        public OSD Result
        {
            get; set;
        }

        public JsonRpcError Error
        {
            get; set;
        }

        public OSDMap Reply = new OSDMap();

        public JsonRpcResponse()
        {
            Error = new JsonRpcError();
        }

        public string Serialize()
        {
            if (Result != null)
                Reply["result"] = Result;

            if (Error.Code != 0)
            {
                Reply["error"] = (OSD)Error.Error;
            }

            string result = string.Empty;
            try
            {
                result = OSDParser.SerializeJsonString(Reply);
            }
            catch
            {

            }
            return result;
        }
    }
}
