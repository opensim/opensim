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
using System.Reflection;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.ApplicationPlugins.Rest.Inventory
{
    /// <remarks>
    /// The class signature reveals the roles that RestHandler plays.
    ///
    /// [1] It is a sub-class of RestPlugin. It inherits and extends
    ///     the functionality of this class, constraining it to the
    ///     specific needs of this REST implementation. This relates
    ///     to the plug-in mechanism supported by OpenSim, the specifics
    ///     of which are mostly hidden by RestPlugin.
    /// [2] IRestHandler describes the interface that this class
    ///     exports to service implementations. This is the services
    ///     management interface.
    /// [3] IHttpAgentHandler describes the interface that is exported
    ///     to the BaseHttpServer in support of this particular HTTP
    ///     processing model. This is the request interface of the
    ///     handler.
    /// </remarks>

    public class RestHandler : RestPlugin, IRestHandler, IHttpAgentHandler
    {
        // Handler tables: both stream and REST are supported. The path handlers and their
        // respective allocators are stored in separate tables.

        internal Dictionary<string,RestMethodHandler>   pathHandlers   = new Dictionary<string,RestMethodHandler>();
        internal Dictionary<string,RestMethodAllocator> pathAllocators = new Dictionary<string,RestMethodAllocator>();
        internal Dictionary<string,RestStreamHandler>   streamHandlers = new Dictionary<string,RestStreamHandler>();

        #region local static state

        private static bool  handlersLoaded = false;
        private static List<Type>  classes  = new List<Type>();
        private static List<IRest> handlers = new List<IRest>();
        private static Type[]         parms = new Type[0];
        private static Object[]       args  = new Object[0];

        /// <summary>
        /// This static initializer scans the ASSEMBLY for classes that
        /// export the IRest interface and builds a list of them. These
        /// are later activated by the handler. To add a new handler it
        /// is only necessary to create a new services class that implements
        /// the IRest interface, and recompile the handler. This gives
        /// all of the build-time flexibility of a modular approach
        /// while not introducing yet-another module loader. Note that
        /// multiple assembles can still be built, each with its own set
        /// of handlers. Examples of services classes are RestInventoryServices
        /// and RestSkeleton.
        /// </summary>

        static RestHandler()
        {
            Module[] mods = Assembly.GetExecutingAssembly().GetModules();

            foreach (Module m in mods)
            {
                Type[] types = m.GetTypes();
                foreach (Type t in types)
                {
                    try
                    {
                        if (t.GetInterface("IRest") != null)
                        {
                            classes.Add(t);
                        }
                    }
                    catch (Exception)
                    {
                        Rest.Log.WarnFormat("[STATIC-HANDLER]: #0 Error scanning {1}", t);
                        Rest.Log.InfoFormat("[STATIC-HANDLER]: #0 {1} is not included", t);
                    }
                }
            }
        }

        #endregion local static state

        #region local instance state

        /// <summary>
        /// This routine loads all of the handlers discovered during
        /// instance initialization.
        /// A table of all loaded and successfully constructed handlers
        /// is built, and this table is then used by the constructor to
        /// initialize each of the handlers in turn.
        /// NOTE: The loading process does not automatically imply that
        /// the handler has registered any kind of an interface, that
        /// may be (optionally) done by the handler either during
        /// construction, or during initialization.
        ///
        /// I was not able to make this code work within a constructor
        /// so it is isolated within this method.
        /// </summary>

        private void LoadHandlers()
        {
            lock (handlers)
            {
                if (!handlersLoaded)
                {
                    ConstructorInfo ci;
                    Object          ht;

                    foreach (Type t in classes)
                    {
                        try
                        {
                            ci = t.GetConstructor(parms);
                            ht = ci.Invoke(args);
                            handlers.Add((IRest)ht);
                        }
                        catch (Exception e)
                        {
                            Rest.Log.WarnFormat("{0} Unable to load {1} : {2}", MsgId, t, e.Message);
                        }
                    }
                    handlersLoaded = true;
                }
            }
        }

        #endregion local instance state

        #region overriding properties

        // These properties override definitions
        // in the base class.

        // Name is used to differentiate the message header.

        public override string Name
        {
            get { return "HANDLER"; }
        }

        // Used to partition the .ini configuration space.

        public override string ConfigName
        {
            get { return "RestHandler"; }
        }

        // We have to rename these because we want
        // to be able to share the values with other
        // classes in our assembly and the base
        // names are protected.

        public string MsgId
        {
            get { return base.MsgID; }
        }

        public string RequestId
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
                // This plugin will only be enabled if the broader
                // REST plugin mechanism is enabled.

                //Rest.Log.InfoFormat("{0}  Plugin is initializing", MsgId);

                base.Initialise(openSim);

                // IsEnabled is implemented by the base class and
                // reflects an overall RestPlugin status

                if (!IsEnabled)
                {
                    //Rest.Log.WarnFormat("{0} Plugins are disabled", MsgId);
                    return;
                }

                Rest.Log.InfoFormat("{0} Rest <{1}> plugin will be enabled", MsgId, Name);
                Rest.Log.InfoFormat("{0} Configuration parameters read from <{1}>", MsgId, ConfigName);

                // These are stored in static variables to make
                // them easy to reach from anywhere in the assembly.

                Rest.main              = openSim;
                if (Rest.main == null)
                    throw new Exception("OpenSim base pointer is null");

                Rest.Plugin            = this;
                Rest.Config            = Config;
                Rest.Prefix            = Prefix;
                Rest.GodKey            = GodKey;
                Rest.Authenticate      = Rest.Config.GetBoolean("authenticate", Rest.Authenticate);
                Rest.Scheme            = Rest.Config.GetString("auth-scheme", Rest.Scheme);
                Rest.Secure            = Rest.Config.GetBoolean("secured", Rest.Secure);
                Rest.ExtendedEscape    = Rest.Config.GetBoolean("extended-escape", Rest.ExtendedEscape);
                Rest.Realm             = Rest.Config.GetString("realm", Rest.Realm);
                Rest.DumpAsset         = Rest.Config.GetBoolean("dump-asset", Rest.DumpAsset);
                Rest.Fill              = Rest.Config.GetBoolean("path-fill", Rest.Fill);
                Rest.DumpLineSize      = Rest.Config.GetInt("dump-line-size", Rest.DumpLineSize);
                Rest.FlushEnabled      = Rest.Config.GetBoolean("flush-on-error", Rest.FlushEnabled);

                // Note: Odd spacing is required in the following strings

                Rest.Log.InfoFormat("{0} Authentication is {1}required", MsgId,
                                    (Rest.Authenticate ? "" : "not "));

                Rest.Log.InfoFormat("{0} Security is {1}enabled", MsgId,
                                    (Rest.Secure ? "" : "not "));

                Rest.Log.InfoFormat("{0} Extended URI escape processing is {1}enabled", MsgId,
                                    (Rest.ExtendedEscape ? "" : "not "));

                Rest.Log.InfoFormat("{0} Dumping of asset data is {1}enabled", MsgId,
                                    (Rest.DumpAsset ? "" : "not "));

                // The supplied prefix MUST be absolute

                if (Rest.Prefix.Substring(0,1) != Rest.UrlPathSeparator)
                {
                    Rest.Log.WarnFormat("{0} Prefix <{1}> is not absolute and must be", MsgId, Rest.Prefix);
                    Rest.Log.InfoFormat("{0} Prefix changed to </{1}>", MsgId, Rest.Prefix);
                    Rest.Prefix = String.Format("{0}{1}", Rest.UrlPathSeparator, Rest.Prefix);
                }

                // If data dumping is requested, report on the chosen line
                // length.

                if (Rest.DumpAsset)
                {
                    Rest.Log.InfoFormat("{0} Dump {1} bytes per line", MsgId, Rest.DumpLineSize);
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

                // The intention of a post construction initializer
                // is to allow for setup that is dependent upon other
                // activities outside of the agency.

                foreach (IRest handler in handlers)
                {
                    try
                    {
                        handler.Initialize();
                    }
                    catch (Exception e)
                    {
                        Rest.Log.ErrorFormat("{0} initialization error: {1}", MsgId, e.Message);
                    }
                }

                // Now that everything is setup we can proceed to
                // add THIS agent to the HTTP server's handler list

                // FIXME: If this code is ever to be re-enabled (most of it is disabled already) then this will
                // have to be handled through the AddHttpHandler interface.
//                if (!AddAgentHandler(Rest.Name,this))
//                {
//                    Rest.Log.ErrorFormat("{0} Unable to activate handler interface", MsgId);
//                    foreach (IRest handler in handlers)
//                    {
//                        handler.Close();
//                    }
//                }

            }
            catch (Exception e)
            {
                Rest.Log.ErrorFormat("{0} Plugin initialization has failed: {1}", MsgId, e.Message);
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
            Rest.Log.InfoFormat("{0} Plugin is terminating", MsgId);

            // FIXME: If this code is ever to be re-enabled (most of it is disabled already) then this will
            // have to be handled through the AddHttpHandler interface.
//            try
//            {
//                RemoveAgentHandler(Rest.Name, this);
//            }
//            catch (KeyNotFoundException){}

            foreach (IRest handler in handlers)
            {
                handler.Close();
            }
        }

        #endregion overriding methods

        #region interface methods

        /// <summary>
        /// This method is called by the HTTP server to match an incoming
        /// request. It scans all of the strings registered by the
        /// underlying handlers and looks for the best match. It returns
        /// true if a match is found.
        /// The matching process could be made arbitrarily complex.
        /// Note: The match is case-insensitive.
        /// </summary>

        public bool Match(OSHttpRequest request, OSHttpResponse response)
        {

            string path = request.RawUrl.ToLower();

            // Rest.Log.DebugFormat("{0} Match ENTRY", MsgId);

            try
            {
                foreach (string key in pathHandlers.Keys)
                {
                    // Rest.Log.DebugFormat("{0} Match testing {1} against agent prefix <{2}>", MsgId, path, key);

                    // Note that Match will not necessarily find the handler that will
                    // actually be used - it does no test for the "closest" fit. It
                    // simply reflects that at least one possible handler exists.

                    if (path.StartsWith(key))
                    {
                        // Rest.Log.DebugFormat("{0} Matched prefix <{1}>", MsgId, key);

                        // This apparently odd evaluation is needed to prevent a match
                        // on anything other than a URI token boundary. Otherwise we
                        // may match on URL's that were not intended for this handler.

                        return (path.Length == key.Length ||
                                path.Substring(key.Length, 1) == Rest.UrlPathSeparator);
                    }
                }

                path = String.Format("{0}{1}{2}", request.HttpMethod, Rest.UrlMethodSeparator, path);

                foreach (string key in streamHandlers.Keys)
                {
                    // Rest.Log.DebugFormat("{0} Match testing {1} against stream prefix <{2}>", MsgId, path, key);

                    // Note that Match will not necessarily find the handler that will
                    // actually be used - it does no test for the "closest" fit. It
                    // simply reflects that at least one possible handler exists.

                    if (path.StartsWith(key))
                    {
                        // Rest.Log.DebugFormat("{0} Matched prefix <{1}>", MsgId, key);

                        // This apparently odd evaluation is needed to prevent a match
                        // on anything other than a URI token boundary. Otherwise we
                        // may match on URL's that were not intended for this handler.

                        return (path.Length == key.Length ||
                                path.Substring(key.Length, 1) == Rest.UrlPathSeparator);
                    }
                }
            }
            catch (Exception e)
            {
                Rest.Log.ErrorFormat("{0} matching exception for path <{1}> : {2}", MsgId, path, e.Message);
            }

            return false;
        }

        /// <summary>
        /// This is called by the HTTP server once the handler has indicated
        /// that it is able to handle the request.
        /// Preconditions:
        ///  [1] request  != null and is a valid request object
        ///  [2] response != null and is a valid response object
        /// Behavior is undefined if preconditions are not satisfied.
        /// </summary>

        public bool Handle(OSHttpRequest request, OSHttpResponse response)
        {
            bool handled;
            base.MsgID = base.RequestID;

            // Debug only

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
                handled = (FindPathHandler(request, response) ||
                    FindStreamHandler(request, response));
            }
            catch (Exception e)
            {
                // A raw exception indicates that something we weren't expecting has
                // happened. This should always reflect a shortcoming in the plugin,
                // or a failure to satisfy the preconditions. It should not reflect
                // an error in the request itself. Under such circumstances the state
                // of the request cannot be determined and we are obliged to mark it
                // as 'handled'.

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
        /// Note: The selection is case-insensitive
        /// </summary>

        private bool FindStreamHandler(OSHttpRequest request, OSHttpResponse response)
        {
            RequestData rdata = new RequestData(request, response, String.Empty);

            string bestMatch = String.Empty;
            string path      = String.Format("{0}:{1}", rdata.method, rdata.path).ToLower();

            Rest.Log.DebugFormat("{0} Checking for stream handler for <{1}>", MsgId, path);

            if (!IsEnabled)
            {
                return false;
            }

            foreach (string pattern in streamHandlers.Keys)
            {
                if (path.StartsWith(pattern))
                {
                    if (pattern.Length > bestMatch.Length)
                    {
                        bestMatch = pattern;
                    }
                }
            }

            // Handle using the best match available

            if (bestMatch.Length > 0)
            {
                Rest.Log.DebugFormat("{0} Stream-based handler matched with <{1}>", MsgId, bestMatch);
                RestStreamHandler handler = streamHandlers[bestMatch];
                rdata.buffer = handler.Handle(rdata.path, rdata.request.InputStream, rdata.request, rdata.response);
                rdata.AddHeader(rdata.response.ContentType,handler.ContentType);
                rdata.Respond("FindStreamHandler Completion");
            }

            return rdata.handled;
        }

        /// <summary>
        /// Add a stream handler for the designated HTTP method and path prefix.
        /// If the handler is not enabled, the request is ignored. If the path
        /// does not start with the REST prefix, it is added. If method-qualified
        /// path has not already been registered, the method is added to the active
        /// handler table.
        /// </summary>
        public void AddStreamHandler(string httpMethod, string path, RestMethod method)
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
                Rest.Log.DebugFormat("{0} Added handler for {1}", MsgId, path);
            }
            else
            {
                Rest.Log.WarnFormat("{0} Ignoring duplicate handler for {1}", MsgId, path);
            }
        }

        /// <summary>
        /// Given the supplied request/response, if the handler is enabled, the inbound
        /// information is used to match an entry in the active path handler tables, using
        /// the method-qualified path information. If a match is found, then the handler is
        /// invoked. The result is the boolean result of the handler, or false if no
        /// handler was located. The boolean indicates whether or not the request has been
        /// handled, not whether or not the request was successful - that information is in
        /// the response.
        /// Note: The selection process is case-insensitive
        /// </summary>

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
                if (request.RawUrl.ToLower().StartsWith(pattern))
                {
                    if (String.IsNullOrEmpty(bestMatch) || pattern.Length > bestMatch.Length)
                    {
                        bestMatch = pattern;
                    }
                }
            }

            if (!String.IsNullOrEmpty(bestMatch))
            {
                rdata = pathAllocators[bestMatch](request, response, bestMatch);

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

        /// <summary>
        /// A method handler and a request allocator are stored using the designated
        /// path as a key. If an entry already exists, it is replaced by the new one.
        /// </summary>

        public void AddPathHandler(RestMethodHandler mh, string path, RestMethodAllocator ra)
        {
            if (!IsEnabled)
            {
                return;
            }

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
