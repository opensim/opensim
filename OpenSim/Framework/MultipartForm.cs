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
using System.IO;
using System.Text;

namespace OpenSim.Framework
{
    public static class MultipartForm
    {
        #region Helper Classes

        public abstract class Element
        {
            public string Name;
        }

        public class File : Element
        {
            public string Filename;
            public string ContentType;
            public byte[] Data;

            public File(string name, string filename, string contentType, byte[] data)
            {
                Name = name;
                Filename = filename;
                ContentType = contentType;
                Data = data;
            }
        }

        public class Parameter : Element
        {
            public string Value;

            public Parameter(string name, string value)
            {
                Name = name;
                Value = value;
            }
        }

        #endregion Helper Classes

        public static HttpWebResponse Post(HttpWebRequest request, List<Element> postParameters)
        {
            string boundary = Boundary();

            // Set up the request properties
            request.Method = "POST";
            request.ContentType = "multipart/form-data; boundary=" + boundary;

            #region Stream Writing

            using (MemoryStream formDataStream = new MemoryStream())
            {
                foreach (var param in postParameters)
                {
                    if (param is File)
                    {
                        File file = (File)param;

                        // Add just the first part of this param, since we will write the file data directly to the Stream
                        string header = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"; filename=\"{2}\";\r\nContent-Type: {3}\r\n\r\n",
                            boundary,
                            file.Name,
                            !String.IsNullOrEmpty(file.Filename) ? file.Filename : "tempfile",
                            file.ContentType);

                        formDataStream.Write(Encoding.UTF8.GetBytes(header), 0, header.Length);
                        formDataStream.Write(file.Data, 0, file.Data.Length);
                    }
                    else
                    {
                        Parameter parameter = (Parameter)param;

                        string postData = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"\r\n\r\n{2}\r\n",
                            boundary,
                            parameter.Name,
                            parameter.Value);
                        formDataStream.Write(Encoding.UTF8.GetBytes(postData), 0, postData.Length);
                    }
                }

                // Add the end of the request
                byte[] footer = Encoding.UTF8.GetBytes("\r\n--" + boundary + "--\r\n");
                formDataStream.Write(footer, 0, footer.Length);

                request.ContentLength = formDataStream.Length;

                // Copy the temporary stream to the network stream
                formDataStream.Seek(0, SeekOrigin.Begin);
                using (Stream requestStream = request.GetRequestStream())
                    formDataStream.CopyTo(requestStream, (int)formDataStream.Length);
            }

            #endregion Stream Writing

            return request.GetResponse() as HttpWebResponse;
        }

        private static string Boundary()
        {
            string formDataBoundary = String.Empty;

            while (formDataBoundary.Length < 15)
                formDataBoundary = formDataBoundary + Random.Shared.Next();

            formDataBoundary = formDataBoundary.Substring(0, 15);
            formDataBoundary = "-----------------------------" + formDataBoundary;

            return formDataBoundary;
        }
    }
}
