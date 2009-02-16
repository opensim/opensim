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
using System.IO;
using OpenMetaverse;

namespace OpenSim.Grid.AssetInventoryServer.Plugins.Simple
{
    public static class SimpleUtils
    {
        public static string ParseNameFromFilename(string filename)
        {
            filename = Path.GetFileName(filename);

            int dot = filename.LastIndexOf('.');
            int firstDash = filename.IndexOf('-');

            if (dot - 37 > 0 && firstDash > 0)
                return filename.Substring(0, firstDash);
            else
                return String.Empty;
        }

        public static UUID ParseUUIDFromFilename(string filename)
        {
            int dot = filename.LastIndexOf('.');

            if (dot > 35)
            {
                // Grab the last 36 characters of the filename
                string uuidString = filename.Substring(dot - 36, 36);
                UUID uuid;
                UUID.TryParse(uuidString, out uuid);
                return uuid;
            }
            else
            {
                UUID uuid;
                if (UUID.TryParse(Path.GetFileName(filename), out uuid))
                    return uuid;
                else
                    return UUID.Zero;
            }
        }
    }
}
