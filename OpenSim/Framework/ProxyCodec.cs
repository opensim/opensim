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

namespace OpenSim.Framework
{
    public sealed class ProxyCodec
    {
        public static void EncodeProxyMessage(byte[] bytes, ref int numBytes, EndPoint trueEP)
        {
            if (numBytes > 4090) // max UPD size = 4096
            {
                throw new Exception("ERROR: No space to encode the proxy EP");
            }

            ushort port = (ushort) ((IPEndPoint) trueEP).Port;
            bytes[numBytes++] = (byte) (port % 256);
            bytes[numBytes++] = (byte) (port / 256);

            foreach (byte b in ((IPEndPoint) trueEP).Address.GetAddressBytes())
            {
                bytes[numBytes++] = b;
            }
        }

        public static IPEndPoint DecodeProxyMessage(byte[] bytes, ref int numBytes)
        {
            // IPv4 Only
            byte[] addr = new byte[4];

            addr[3] = bytes[--numBytes];
            addr[2] = bytes[--numBytes];
            addr[1] = bytes[--numBytes];
            addr[0] = bytes[--numBytes];

            ushort port = (ushort) (bytes[--numBytes] * 256);
            port += (ushort) bytes[--numBytes];

            return new IPEndPoint(new IPAddress(addr), (int) port);
        }
    }
}
