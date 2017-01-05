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

using OpenMetaverse;

namespace OpenSim.Framework
{
    /// <summary>
    /// Capabilities utility methods
    /// </summary>
    public class CapsUtil
    {
        /// <summary>
        /// Generate a CAPS seed path using a previously generated CAPS object path component
        /// </summary>
        /// <param name="capsKey"></param>
        /// <returns></returns>
        public static string GetCapsSeedPath(string capsObjectPath)
        {
            return "CAPS/" + capsObjectPath + "0000/";
        }

        /// <summary>
        /// Get a random CAPS object path component that will be used as the identifying part of all future CAPS requests
        /// </summary>
        /// <returns></returns>
        public static string GetRandomCapsObjectPath()
        {
            UUID caps = UUID.Random();
            string capsPath = caps.ToString();
            // I'm commenting this, rather than delete, to keep as historical record.
            // The caps seed is now a full UUID string that gets added four more digits
            // for producing certain CAPs URLs in OpenSim
            //capsPath = capsPath.Remove(capsPath.Length - 4, 4);
            return capsPath;
        }
    }
}
