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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using log4net;
using Nini.Config;

namespace OpenSim.Tools.Configger
{
    public static class Util
    {
        public static string[] Glob(string path)
        {
            string vol=String.Empty;

            if (Path.VolumeSeparatorChar != Path.DirectorySeparatorChar)
            {
                string[] vcomps = path.Split(new char[] {Path.VolumeSeparatorChar}, 2, StringSplitOptions.RemoveEmptyEntries);

                if (vcomps.Length > 1)
                {
                    path = vcomps[1];
                    vol = vcomps[0];
                }
            }

            string[] comps = path.Split(new char[] {Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar}, StringSplitOptions.RemoveEmptyEntries);

            // Glob

            path = vol;
            if (vol != String.Empty)
                path += new String(new char[] {Path.VolumeSeparatorChar, Path.DirectorySeparatorChar});
            else
                path = new String(new char[] {Path.DirectorySeparatorChar});

            List<string> paths = new List<string>();
            List<string> found = new List<string>();
            paths.Add(path);

            int compIndex = -1;
            foreach (string c in comps)
            {
                compIndex++;

                List<string> addpaths = new List<string>();
                foreach (string p in paths)
                {
                    string[] dirs = Directory.GetDirectories(p, c);

                    if (dirs.Length != 0)
                    {
                        foreach (string dir in dirs)
                            addpaths.Add(Path.Combine(path, dir));
                    }

                    // Only add files if that is the last path component
                    if (compIndex == comps.Length - 1)
                    {
                        string[] files = Directory.GetFiles(p, c);
                        foreach (string f in files)
                            found.Add(f);
                    }
                }
                paths = addpaths;
            }

            return found.ToArray();
        }

        public static string configDir()
        {
            return ".";
        }

    }
}
