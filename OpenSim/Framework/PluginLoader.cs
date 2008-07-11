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
    public class PluginConstraintViolatedException : Exception
    {
        public PluginConstraintViolatedException () : base() {}
        public PluginConstraintViolatedException (string msg) : base(msg) {}
        public PluginConstraintViolatedException (string msg, Exception e) : base(msg, e) {}
    }

    /// <summary>
    /// Classes wishing to impose constraints on plugin loading must implement 
    /// this class and pass it to PluginLoader AddConstraint()
    /// </summary>
    public interface IPluginConstraint
    {
        bool Fail (string extpoint);
        string Message { get; }
    }

    /// <summary>
    /// Generic Plugin Loader
    /// </summary>
    public class PluginLoader <T> : IDisposable where T : IPlugin
    {
        private const int max_loadable_plugins = 10000;

        private List<T> loaded = new List<T>();
        private List<string> extpoints = new List<string>();
        private PluginInitialiserBase initialiser;
        private Dictionary<string,IPluginConstraint> constraints 
            = new Dictionary<string,IPluginConstraint>();
        
        private static readonly ILog log 
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        public PluginInitialiserBase Initialiser
        { 
            set { initialiser = value; } 
            get { return initialiser; } 
        }
        
        public List<T> Plugins 
        { 
            get { return loaded; } 
        }

        public PluginLoader () 
        {
            Initialiser = new PluginInitialiserBase();
        }

        public PluginLoader (PluginInitialiserBase init)
        {            
           Initialiser = init;
        }

        public PluginLoader (PluginInitialiserBase init, string dir)
        {            
           Initialiser = init;
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

        public void AddConstraint (string extpoint, IPluginConstraint cons)
        {
            constraints.Add (extpoint, cons);
        }
        
        public void Load (string extpoint, string dir)
        {
            AddPluginDir (dir);
            AddExtensionPoint (extpoint);
            Load();
        }

        public void Load ()
        {            
            suppress_console_output_ (true);
            AddinManager.Registry.Update (null);            
            suppress_console_output_ (false);

            foreach (string ext in extpoints)
            {
                if (constraints.ContainsKey (ext))
                {
                    IPluginConstraint cons = constraints [ext];
                    if (cons.Fail (ext))
                        throw new PluginConstraintViolatedException (cons.Message);
                }

                ExtensionNodeList ns = AddinManager.GetExtensionNodes (ext);
                foreach (TypeExtensionNode n in ns)
                {
                    T p = (T) n.CreateInstance();
                    Initialiser.Initialise (p);
                    Plugins.Add (p);

                    log.Info("[PLUGINS]: Loading plugin " + n.Path);
                }
            }
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

    public class PluginCountConstraint : IPluginConstraint
    { 
        private int min; 
        private int max; 

        public PluginCountConstraint (int exact)
        {
            min = exact; 
            max = exact; 
        }

        public PluginCountConstraint (int minimum, int maximum) 
        { 
            min = minimum; 
            max = maximum; 
        } 

        public string Message 
        { 
            get 
            { 
                return "The number of plugins is constrained to the interval [" 
                    + min + ", " + max + "]"; 
            } 
        }

        public bool Fail (string extpoint)
        {
            ExtensionNodeList ns = AddinManager.GetExtensionNodes (extpoint);
            if ((ns.Count < min) || (ns.Count > max))
                return true;
            else
                return false;
        }
    }

    public class PluginFilenameConstraint : IPluginConstraint
    { 
        private string filename; 

        public PluginFilenameConstraint (string name)
        { 
            filename = name; 
            
        } 

        public string Message 
        { 
            get 
            { 
                return "The plugin must have the following name: " + filename; 
            } 
        }

        public bool Fail (string extpoint)
        {
            ExtensionNodeList ns = AddinManager.GetExtensionNodes (extpoint);
            if (ns.Count != 1)
                return true;

            string[] path = ns[0].Path.Split('/');
            if (path [path.Length-1] == filename)
                return false;
                
            return true;
        }
    }
}
