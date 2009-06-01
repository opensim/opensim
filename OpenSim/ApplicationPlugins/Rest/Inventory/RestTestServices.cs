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
 * 
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.ApplicationPlugins.Rest.Inventory
{
    public class RestTestServices : IRest
    {
        private bool    enabled = false;
        private string  qPrefix = "test";

        // A simple constructor is used to handle any once-only
        // initialization of working classes.

        public RestTestServices()
        {
            Rest.Log.InfoFormat("{0} Test services initializing", MsgId);
            Rest.Log.InfoFormat("{0} Using REST Implementation Version {1}", MsgId, Rest.Version);

            // If a relative path was specified, make it absolute by adding
            // the standard prefix, e.g. /admin

            if (!qPrefix.StartsWith(Rest.UrlPathSeparator))
            {
                Rest.Log.InfoFormat("{0} Domain is relative, adding absolute prefix", MsgId);
                qPrefix = String.Format("{0}{1}{2}", Rest.Prefix, Rest.UrlPathSeparator, qPrefix);
                Rest.Log.InfoFormat("{0} Domain is now <{1}>", MsgId, qPrefix);
            }

            // Load test cases

            loadTests();
            foreach (ITest test in tests)
            {
                test.Initialize();
            }

            // Register interface

            Rest.Plugin.AddPathHandler(DoTests,qPrefix,Allocate);

            // Activate

            enabled = true;

            Rest.Log.InfoFormat("{0} Test services initialization complete", MsgId);
        }

        // Post-construction, pre-enabled initialization opportunity
        // Not currently exploited.

        public void Initialize()
        {
        }

        // Called by the plug-in to halt REST processing. Local processing is
        // disabled, and control blocks until all current processing has 
        // completed. No new processing will be started

        public void Close()
        {
            enabled = false;
            foreach (ITest test in tests)
            {
                test.Close();
            }
            Rest.Log.InfoFormat("{0} Test services closing down", MsgId);
        }

        // Properties

        internal string MsgId
        {
            get { return Rest.MsgId; }
        }

        #region Interface

        private RequestData Allocate(OSHttpRequest request, OSHttpResponse response, string prefix)
        {
            return new RequestData(request, response, prefix);
        }

        // Inventory Handler

        private void DoTests(RequestData rdata)
        {
            if (!enabled)
                return;

            // Now that we know this is a serious attempt to 
            // access inventory data, we should find out who
            // is asking, and make sure they are authorized
            // to do so. We need to validate the caller's
            // identity before revealing anything about the
            // status quo. Authenticate throws an exception
            // via Fail if no identity information is present.
            //
            // With the present HTTP server we can't use the
            // builtin authentication mechanisms because they
            // would be enforced for all in-bound requests.
            // Instead we look at the headers ourselves and 
            // handle authentication directly.
 
            try
            {
                if (!rdata.IsAuthenticated)
                {
                    rdata.Fail(Rest.HttpStatusCodeNotAuthorized, 
                          String.Format("user \"{0}\" could not be authenticated", rdata.userName));
                }
            }
            catch (RestException e)
            {
                if (e.statusCode == Rest.HttpStatusCodeNotAuthorized)
                {
                    Rest.Log.WarnFormat("{0} User not authenticated", MsgId);
                    Rest.Log.DebugFormat("{0} Authorization header: {1}", MsgId, rdata.request.Headers.Get("Authorization"));
                }
                else
                {
                    Rest.Log.ErrorFormat("{0} User authentication failed", MsgId);
                    Rest.Log.DebugFormat("{0} Authorization header: {1}", MsgId, rdata.request.Headers.Get("Authorization"));
                }
                throw (e);
            }

            // Check that a test was specified

            if (rdata.Parameters.Length < 1)
            {
                Rest.Log.DebugFormat("{0} Insufficient parameters", MsgId);
                rdata.Fail(Rest.HttpStatusCodeBadRequest, "not enough parameters");
            }

            // Select the test

            foreach (ITest test in tests)
            {
                if (!rdata.handled)
                    test.Execute(rdata);
            }
        }

        #endregion Interface

        private static bool    testsLoaded = false;
        private static List<Type> classes  = new List<Type>();
        private static List<ITest>   tests = new List<ITest>();
        private static Type[]        parms = new Type[0];
        private static Object[]      args  = new Object[0];

        static RestTestServices()
        {
            Module[] mods = Assembly.GetExecutingAssembly().GetModules();
            foreach (Module m in mods)
            {
                Type[] types = m.GetTypes();
                foreach (Type t in types) 
                {
                    try
                    {
                        if (t.GetInterface("ITest") != null)
                        {
                            classes.Add(t);
                        }
                    }
                    catch (Exception e)
                    {
                        Rest.Log.WarnFormat("[STATIC-TEST] Unable to include test {0} : {1}", t, e.Message);
                    }
                }
            }
        }

        /// <summary>
        /// This routine loads all of the handlers discovered during
        /// instance initialization. Each handler is responsible for
        /// registering itself with this handler.
        /// I was not able to make this code work in a constructor.
        /// </summary>

        private void loadTests()
        {
            lock (tests)
            {
                if (!testsLoaded)
                {

                    ConstructorInfo ci;
                    Object          ht;

                    foreach (Type t in classes)
                    {
                        try
                        {
                            if (t.GetInterface("ITest") != null)
                            {
                                ci = t.GetConstructor(parms);
                                ht = ci.Invoke(args);
                                tests.Add((ITest)ht);
                                Rest.Log.InfoFormat("{0} Test {1} added", MsgId, t);
                            }
                        }
                        catch (Exception e)
                        {
                            Rest.Log.WarnFormat("{0} Unable to load test {1} : {2}", MsgId, t, e.Message);
                        }
                    }
                    testsLoaded = true;
                }
            }
        }

    }
}
