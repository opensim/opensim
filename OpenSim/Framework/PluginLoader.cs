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
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Mono.Addins;

namespace OpenSim.Framework
{
    /// <summary>
    /// Exception thrown if an incorrect number of plugins are loaded
    /// </summary>
    public class PluginCountInvalidException : Exception
    {
        public PluginCountInvalidException () : base() {}
        public PluginCountInvalidException (string msg) : base(msg) {}
        public PluginCountInvalidException (string msg, Exception e) : base(msg, e) {}
    }

    /// <summary>
    /// Generic Plugin Loader
    /// </summary>
    public class PluginLoader <T> : IDisposable where T : IPlugin
    {
        private struct Range 
        { 
            public int min; 
            public int max; 
            public Range (int n, int x) { min=n; max=x; } 
        }

        private const int max_loadable_plugins = 10000;

        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private List<T> loaded = new List<T>();
        private List<string> extpoints = new List<string>();
        private Dictionary<string,Range> constraints = new Dictionary<string,Range>();
        
        public delegate void Initialiser (IPlugin p);
        private void default_initialiser_ (IPlugin p) { p.Initialise(); }

        public PluginLoader (string dir)
        {            
            AddPluginDir (dir);
        }

        public void AddPluginDir (string dir)
        {
            suppress_console_output_ (true);
            AddinManager.Initialize (dir);
            AddinManager.Registry.Update (null);            
            suppress_console_output_ (false);
        }

        public void AddExtensionPoint (string extpoint)
        {
            extpoints.Add (extpoint);
        }

        public void AddConstrainedExtensionPoint (string extpoint, int min, int max)
        {
            constraints.Add (extpoint, new Range (min, max));
            AddExtensionPoint (extpoint);
        }
        
        public void LoadAll (Initialiser initialise)
        {
            foreach (string pt in extpoints)
                Load (pt, initialise);
        }

        public void Load (string extpoint)
        {
            Load (extpoint, default_initialiser_);
        }

        public void Load (string extpoint, Initialiser initialise)
        {
            int min = 0;
            int max = max_loadable_plugins;

            if (constraints.ContainsKey (extpoint))
            {
                min = constraints[extpoint].min; 
                max = constraints[extpoint].max;
            }
                
            Load (extpoint, initialise, min, max);
        }

        public void Load (string extpoint, Initialiser initialise, int min, int max)
        {            
            suppress_console_output_ (true);
            AddinManager.Registry.Update (null);            
            suppress_console_output_ (false);

            ExtensionNodeList ns = AddinManager.GetExtensionNodes(extpoint);

            if ((ns.Count < min) || (ns.Count > max))
                throw new PluginCountInvalidException 
                    ("The number of plugins for " + extpoint + 
                     " is constrained to the interval [" + min + ", " + max + "]");

            foreach (TypeExtensionNode n in ns)
            {
                T p = (T) n.CreateInstance();
                initialise (p);
                Plugins.Add (p);

                log.Info("[PLUGINS]: Loading plugin " + n.Path);
            }
        }

        public List<T> Plugins
        {
            get { return loaded; } 
        }

        public void Dispose ()
        {
            foreach (T p in Plugins)
                p.Dispose ();
        }

        public void ClearCache()
        {
            // The Mono addin manager (in Mono.Addins.dll version 0.2.0.0) occasionally seems to corrupt its addin cache
            // Hence, as a temporary solution we'll remove it before each startup
            if (Directory.Exists("addin-db-000"))
                Directory.Delete("addin-db-000", true);

            if (Directory.Exists("addin-db-001"))
                Directory.Delete("addin-db-001", true);
        }

        private static TextWriter prev_console_;        
        public void suppress_console_output_ (bool save)
        {
            if (save)
            {
                prev_console_ = System.Console.Out;
                System.Console.SetOut(new StreamWriter(Stream.Null));
            }
            else
            {
                if (prev_console_ != null) 
                    System.Console.SetOut(prev_console_);
            }
        }

    }
}
