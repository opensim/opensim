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
 * 
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.ApplicationPlugins.Rest;
using Mono.Addins;

[assembly : Addin]
[assembly : AddinDependency("OpenSim", "0.5")]

namespace OpenSim.ApplicationPlugins.Rest.Inventory
{

    [Extension("/OpenSim/Startup")]

    public class RestHandler : RestPlugin, IHttpAgentHandler
    {

        #region local static state

        /// <summary>
        /// This static initializer scans the assembly for classes that
        /// export the IRest interface and builds a list of them. These
        /// are later activated by the handler. To add a new handler it 
        /// is only necessary to create a new services class that implements
        /// the IRest interface, and recompile the handler. This gives
        /// all of the build-time flexibility of a modular approach 
        /// while not introducing yet-another module loader. Note that
        /// multiple assembles can still be built, each with its own set
        /// of handlers.
        /// </summary>

        private static bool  handlersLoaded = false;
        private static List<Type>  classes  = new List<Type>();
        private static List<IRest> handlers = new List<IRest>();
        private static Type[]         parms = new Type[1];
        private static Object[]       args  = new Object[1];

        static RestHandler()
        {
            Module[] mods = Assembly.GetExecutingAssembly().GetModules();
            foreach (Module m in mods)
            {
                Type[] types = m.GetTypes();
                foreach (Type t in types) 
                {
                    if (t.GetInterface("IRest") != null)
                    {
                        classes.Add(t);
                    }
                }
            }
        }

        #endregion local static state

        #region local instance state

        /// <remarks>
        /// The handler delegate is not noteworthy. The allocator allows
        /// a given handler to optionally subclass the base RequestData
        /// structure to carry any locally required per-request state
        /// needed.
        /// </remarks>
        internal delegate void RestMethodHandler(RequestData rdata);
        internal delegate RequestData RestMethodAllocator(OSHttpRequest request, OSHttpResponse response);

        // Handler tables: both stream and REST are supported

        internal Dictionary<string,RestMethodHandler>   pathHandlers   = new Dictionary<string,RestMethodHandler>();
        internal Dictionary<string,RestMethodAllocator> pathAllocators = new Dictionary<string,RestMethodAllocator>();
        internal Dictionary<string,RestStreamHandler>   streamHandlers = new Dictionary<string,RestStreamHandler>();

        /// <summary>
        /// This routine loads all of the handlers discovered during
        /// instance initialization. Each handler is responsible for
        /// registering itself with this handler.
        /// I was not able to make this code work in a constructor.
        /// </summary>
        private void LoadHandlers()
        {
            lock(handlers)
            {
                if (!handlersLoaded)
                {
                    parms[0]       = this.GetType();
                    args[0]        = this;

                    ConstructorInfo ci;
                    Object          ht;

                    foreach (Type t in classes)
                    {
                        ci = t.GetConstructor(parms);
                        ht = ci.Invoke(args);
                        handlers.Add((IRest)ht);
                    }
                    handlersLoaded = true;
                }
            }
        }

        #endregion local instance state

        #region overriding properties

        // Used to differentiate the message header.

        public override string Name 
        { 
            get { return "HANDLER"; }
        }

        // Used to partition the configuration space.

        public override string ConfigName
        {
            get { return "RestHandler"; }
        }

        // We have to rename these because we want
        // to be able to share the values with other
        // classes in our assembly and the base 
        // names are protected.

        internal string MsgId
        {
            get { return base.MsgID; }
        }

        internal string RequestId
        {
            get { return base.RequestID; }
        }

        #endregion overriding properties

        #region overriding methods

        /// <summary>
        /// This method is called by OpenSimMain immediately after loading the
        /// plugin and after basic server setup,  but before running any server commands.
        /// </summary>
        /// <remarks>
        /// Note that entries MUST be added to the active configuration files before
        /// the plugin can be enabled.
        /// </remarks>
        public override void Initialise(OpenSimBase openSim)
        {
            try
            {

                /// <remarks>
                /// This plugin will only be enabled if the broader
                /// REST plugin mechanism is enabled.
                /// </remarks>

                Rest.Log.InfoFormat("{0}  Plugin is initializing", MsgID);

                base.Initialise(openSim);

                if (!IsEnabled)                     
                {
                    Rest.Log.WarnFormat("{0} Plugins are disabled", MsgID);
                    return;
                }

                Rest.Log.InfoFormat("{0} Plugin will be enabled", MsgID);

                /// <remarks>
                /// These are stored in static variables to make
                /// them easy to reach from anywhere in the assembly.
                /// </remarks>

                Rest.main              = openSim;
                Rest.Plugin            = this;
                Rest.Comms             = App.CommunicationsManager;
                Rest.UserServices      = Rest.Comms.UserService;
                Rest.InventoryServices = Rest.Comms.InventoryService;
                Rest.AssetServices     = Rest.Comms.AssetCache;
                Rest.Config            = Config;
                Rest.Prefix            = Prefix;
                Rest.GodKey            = GodKey;

                Rest.Authenticate      = Rest.Config.GetBoolean("authenticate",true);
                Rest.Secure            = Rest.Config.GetBoolean("secured",true);
                Rest.ExtendedEscape    = Rest.Config.GetBoolean("extended-escape",true);
                Rest.Realm             = Rest.Config.GetString("realm","OpenSim REST");
                Rest.DumpAsset         = Rest.Config.GetBoolean("dump-asset",false);
                Rest.DumpLineSize      = Rest.Config.GetInt("dump-line-size",32);

                Rest.Log.InfoFormat("{0} Authentication is {1}required", MsgId,
                                    (Rest.Authenticate ? "" : "not "));

                Rest.Log.InfoFormat("{0} Security is {1}enabled", MsgId,
                                    (Rest.Authenticate ? "" : "not "));

                Rest.Log.InfoFormat("{0} Extended URI escape processing is {1}enabled", MsgId,
                                    (Rest.ExtendedEscape ? "" : "not "));

                Rest.Log.InfoFormat("{0} Dumping of asset data is {1}enabled", MsgId,
                                    (Rest.DumpAsset ? "" : "not "));

                if (Rest.DumpAsset)
                {
                    Rest.Log.InfoFormat("{0} Dump {1} bytes per line", MsgId,
                                        Rest.DumpLineSize);
                }

                // Load all of the handlers present in the 
                // assembly

                // In principle, as we're an application plug-in,
                // most of what needs to be done could be done using
                // static resources, however the Open Sim plug-in 
                // model makes this an instance, so that's what we 
                // need to be.
                // There is only one Communications manager per 
                // server, and by inference, only one each of the
                // user, asset, and inventory servers. So we can cache
                // those using a static initializer.
                // We move all of this processing off to another
                // services class to minimize overlap between function
                // and infrastructure.

                LoadHandlers();

                /// <remarks>
                /// The intention of a post construction initializer
                /// is to allow for setup that is dependent upon other
                /// activities outside of the agency. We don't currently
                /// have any, but the design allows for it.
                /// </remarks>

                foreach (IRest handler in handlers)
                {
                    handler.Initialize();
                }

                /// <remarks>
                /// Now that everything is setup we can proceed and
                /// add this agent to the HTTP server's handler list
                /// </remarks>

                if (!AddAgentHandler(Rest.Name,this))
                {
                    Rest.Log.ErrorFormat("{0} Unable to activate handler interface", MsgId);
                    foreach (IRest handler in handlers)
                    {
                        handler.Close();
                    }
                }

            }
            catch (Exception e)
            {
                Rest.Log.ErrorFormat("{0} Plugin initialization has failed: {1}", MsgID, e.Message);
            }

        }

        /// <summary>
        /// In the interests of efficiency, and because we cannot determine whether
        /// or not this instance will actually be harvested, we clobber the only
        /// anchoring reference to the working state for this plug-in. What the 
        /// call to close does is irrelevant to this class beyond knowing that it
        /// can nullify the reference when it returns.
        /// To make sure everything is copacetic we make sure the primary interface
        /// is disabled by deleting the handler from the HTTP server tables.
        /// </summary>
        public override void Close()
        {

            Rest.Log.InfoFormat("{0} Plugin is terminating", MsgID);

            try
            {
                RemoveAgentHandler(Rest.Name, this);
            }
            catch (KeyNotFoundException){}
            
            foreach (IRest handler in handlers)
            {
                handler.Close();
            }

        }

        #endregion overriding methods

        #region interface methods

        /// <summary>
        /// This method is called by the server to match the client, it could
        /// just return true if we only want one such handler. For now we
        /// match any explicitly specified client.
        /// </summary>
        public bool Match(OSHttpRequest request, OSHttpResponse response)
        {
            string path = request.RawUrl;
            foreach (string key in pathHandlers.Keys)
            {
                if (path.StartsWith(key))
                {
                    return ( path.Length == key.Length ||
                             path.Substring(key.Length,1) == Rest.UrlPathSeparator);
                }
            }

            path = String.Format("{0}{1}{2}", request.HttpMethod, Rest.UrlMethodSeparator, path);
            foreach (string key in streamHandlers.Keys)
            {
                if (path.StartsWith(key))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Preconditions:
        ///  [1] request  != null and is a valid request object
        ///  [2] response != null and is a valid response object
        /// Behavior is undefined if preconditions are not satisfied.
        /// </summary>
        public bool Handle(OSHttpRequest request, OSHttpResponse response)
        {
            bool handled;
            base.MsgID = base.RequestID;

            if (Rest.DEBUG)
            {
                Rest.Log.DebugFormat("{0} ENTRY", MsgId);
                Rest.Log.DebugFormat("{0}  Agent: {1}", MsgId, request.UserAgent);
                Rest.Log.DebugFormat("{0} Method: {1}", MsgId, request.HttpMethod);

                for (int i = 0; i < request.Headers.Count; i++)
                {
                    Rest.Log.DebugFormat("{0} Header [{1}] : <{2}> = <{3}>", 
                                         MsgId, i, request.Headers.GetKey(i), request.Headers.Get(i));
                }
                Rest.Log.DebugFormat("{0}    URI: {1}", MsgId, request.RawUrl);
            }

            // If a path handler worked we're done, otherwise try any
            // available stream handlers too.

            try
            {
                handled = FindPathHandler(request, response) ||
                    FindStreamHandler(request, response);
            }
            catch (Exception e)
            {
                // A raw exception indicates that something we weren't expecting has
                // happened. This should always reflect a shortcoming in the plugin,
                // or a failure to satisfy the preconditions.
                Rest.Log.ErrorFormat("{0} Plugin error: {1}", MsgId, e.Message);
                handled = true;
            }

            Rest.Log.DebugFormat("{0} EXIT", MsgId);

            return handled;

        }

        #endregion interface methods

        /// <summary>
        /// If there is a stream handler registered that can handle the
        /// request, then fine. If the request is not matched, do
        /// nothing.
        /// </summary>

        private bool FindStreamHandler(OSHttpRequest request, OSHttpResponse response)
        {
            RequestData rdata = new RequestData(request, response, String.Empty);

            string bestMatch = null;
            string path      = String.Format("{0}:{1}", rdata.method, rdata.path);

            Rest.Log.DebugFormat("{0} Checking for stream handler for <{1}>", MsgId, path);

            foreach (string pattern in streamHandlers.Keys)
            {
                if (path.StartsWith(pattern))
                {
                    if (String.IsNullOrEmpty(bestMatch) || pattern.Length > bestMatch.Length)
                    {
                        bestMatch = pattern;
                    }
                }
            }

            // Handle using the best match available

            if (!String.IsNullOrEmpty(bestMatch))
            {
                Rest.Log.DebugFormat("{0} Stream-based handler matched with <{1}>", MsgId, bestMatch);
                RestStreamHandler handler = streamHandlers[bestMatch];
                rdata.buffer = handler.Handle(rdata.path, rdata.request.InputStream, rdata.request, rdata.response);
                rdata.AddHeader(rdata.response.ContentType,handler.ContentType);
                rdata.Respond("FindStreamHandler Completion");
            }

            return rdata.handled;

        }

        // Preserves the original handler's semantics

        public new void AddStreamHandler(string httpMethod, string path, RestMethod method)
        {

            if (!IsEnabled)
            {
                return;
            }

            if (!path.StartsWith(Rest.Prefix)) 
            {
                path = String.Format("{0}{1}", Rest.Prefix, path);
            }

            path = String.Format("{0}{1}{2}", httpMethod, Rest.UrlMethodSeparator, path);

            // Conditionally add to the list

            if (!streamHandlers.ContainsKey(path))
            {
                streamHandlers.Add(path, new RestStreamHandler(httpMethod, path, method));
                Rest.Log.DebugFormat("{0} Added handler for {1}", MsgID, path);
            }
            else
            {
                Rest.Log.WarnFormat("{0} Ignoring duplicate handler for {1}", MsgID, path);
            }

        }


            internal bool FindPathHandler(OSHttpRequest request, OSHttpResponse response)
        {

            RequestData rdata = null;
            string bestMatch = null;
            
            if (!IsEnabled)
            {
                return false;
            }

            // Conditionally add to the list

            Rest.Log.DebugFormat("{0} Checking for path handler for <{1}>", MsgId, request.RawUrl);

            foreach (string pattern in pathHandlers.Keys)
            {
                if (request.RawUrl.StartsWith(pattern))
                {
                    if (String.IsNullOrEmpty(bestMatch) || pattern.Length > bestMatch.Length)
                    {
                        bestMatch = pattern;
                    }
                }
            }

            if (!String.IsNullOrEmpty(bestMatch))
            {

                rdata = pathAllocators[bestMatch](request, response);

                Rest.Log.DebugFormat("{0} Path based REST handler matched with <{1}>", MsgId, bestMatch);

                try
                {
                    pathHandlers[bestMatch](rdata);
                }
 
                // A plugin generated error indicates a request-related error
                // that has been handled by the plugin.

                catch (RestException r)
                {
                    Rest.Log.WarnFormat("{0} Request failed: {1}", MsgId, r.Message);
                }

            }

            return (rdata == null) ? false : rdata.handled;

        }

        internal void AddPathHandler(RestMethodHandler mh, string path, RestMethodAllocator ra)
        {
            if (pathHandlers.ContainsKey(path))
            {
                Rest.Log.DebugFormat("{0} Replacing handler for <${1}>", MsgId, path);
                pathHandlers.Remove(path);
            }

            if (pathAllocators.ContainsKey(path))
            {
                Rest.Log.DebugFormat("{0} Replacing allocator for <${1}>", MsgId, path);
                pathAllocators.Remove(path);
            }

            Rest.Log.DebugFormat("{0} Adding path handler for {1}", MsgId, path);

            pathHandlers.Add(path, mh);
            pathAllocators.Add(path, ra);

        }
    }
}
