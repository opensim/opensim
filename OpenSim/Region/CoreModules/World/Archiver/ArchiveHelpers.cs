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
using System.Net;
using OpenMetaverse;
using OpenSim.Framework.Serialization;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.World.Archiver
{
    /// <summary>
    /// Helper methods for archive manipulation
    /// </summary>
    /// This is a separate class from ArchiveConstants because we need to bring in very OpenSim specific classes.
    public static class ArchiveHelpers
    {
        /// <summary>
        /// Create the filename used for objects in OpenSim Archives.
        /// </summary>
        /// <param name="objectName"></param>
        /// <param name="uuid"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        public static string CreateObjectFilename(SceneObjectGroup sog)
        {
            return ArchiveConstants.CreateOarObjectFilename(sog.Name, sog.UUID, sog.AbsolutePosition);
        }

        /// <summary>
        /// Create the path used to store an object in an OpenSim Archive.
        /// </summary>
        /// <param name="objectName"></param>
        /// <param name="uuid"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        public static string CreateObjectPath(SceneObjectGroup sog)
        {
            return ArchiveConstants.CreateOarObjectPath(sog.Name, sog.UUID, sog.AbsolutePosition);
        }

        /// <summary>
        /// Resolve path to a working FileStream
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static Stream GetStream(string path)
        {
            if (File.Exists(path))
            {
                return new FileStream(path, FileMode.Open, FileAccess.Read);
            }
            else
            {
                try
                {
                    Uri uri = new Uri(path);
                    if (uri.Scheme == "file")
                    {
                        return new FileStream(uri.AbsolutePath, FileMode.Open, FileAccess.Read);
                    }
                    else
                    {
                        if (uri.Scheme != "http")
                            throw new Exception(String.Format("Unsupported URI scheme ({0})", path));

                        // OK, now we know we have an HTTP URI to work with
                        return URIFetch(uri);
                    }
                }
                catch (UriFormatException)
                {
                    // In many cases the user will put in a plain old filename that cannot be found so assume that
                    // this is the problem rather than confusing the issue with a UriFormatException
                    throw new Exception(String.Format("Cannot find file {0}", path));
                }
            }
        }

        public static Stream URIFetch(Uri uri)
        {
            HttpWebRequest request  = (HttpWebRequest)WebRequest.Create(uri);

            // request.Credentials = credentials;

            request.ContentLength = 0;
            request.KeepAlive     = false;

            WebResponse response = request.GetResponse();
            Stream file = response.GetResponseStream();

            // justincc: gonna ignore the content type for now and just try anything
            //if (response.ContentType != "application/x-oar")
            //    throw new Exception(String.Format("{0} does not identify an OAR file", uri.ToString()));

            if (response.ContentLength == 0)
                throw new Exception(String.Format("{0} returned an empty file", uri.ToString()));

            // return new BufferedStream(file, (int) response.ContentLength);
            return new BufferedStream(file, 1000000);
        }
    }
}