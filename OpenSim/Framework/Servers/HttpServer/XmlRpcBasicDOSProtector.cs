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
using System.Net;
using Nwc.XmlRpc;
using OpenSim.Framework;
using log4net;

namespace OpenSim.Framework.Servers.HttpServer
{
    public enum ThrottleAction
    {
        DoThrottledMethod,
        DoThrow
    }

    public class XmlRpcBasicDOSProtector
    {
        private readonly XmlRpcMethod _normalMethod;
        private readonly XmlRpcMethod _throttledMethod;
        private readonly CircularBuffer<int> _generalRequestTimes; // General request checker
        private readonly BasicDosProtectorOptions _options;
        private readonly Dictionary<string, CircularBuffer<int>> _deeperInspection;   // per client request checker
        private readonly Dictionary<string, int> _tempBlocked;  // blocked list
        private readonly System.Timers.Timer _forgetTimer;  // Cleanup timer
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly System.Threading.ReaderWriterLockSlim _lockSlim = new System.Threading.ReaderWriterLockSlim();

        public XmlRpcBasicDOSProtector(XmlRpcMethod normalMethod, XmlRpcMethod throttledMethod,BasicDosProtectorOptions options)
        {
            _normalMethod = normalMethod;
            _throttledMethod = throttledMethod;
            _generalRequestTimes = new CircularBuffer<int>(options.MaxRequestsInTimeframe + 1,true);
            _generalRequestTimes.Put(0);
            _options = options;
            _deeperInspection = new Dictionary<string, CircularBuffer<int>>();
            _tempBlocked = new Dictionary<string, int>();
            _forgetTimer = new System.Timers.Timer();
            _forgetTimer.Elapsed += delegate
                                        {
                                            _forgetTimer.Enabled = false;

                                            List<string> removes = new List<string>();
                                            _lockSlim.EnterReadLock();
                                            foreach (string str in _tempBlocked.Keys)
                                            {
                                                if (
                                                    Util.EnvironmentTickCountSubtract(Util.EnvironmentTickCount(),
                                                                                      _tempBlocked[str]) > 0)
                                                    removes.Add(str);
                                            }
                                            _lockSlim.ExitReadLock();
                                            lock (_deeperInspection)
                                            {
                                                _lockSlim.EnterWriteLock();
                                                for (int i = 0; i < removes.Count; i++)
                                                {
                                                    _tempBlocked.Remove(removes[i]);
                                                    _deeperInspection.Remove(removes[i]);
                                                }
                                                _lockSlim.ExitWriteLock();
                                            }
                                            foreach (string str in removes)
                                            {
                                                m_log.InfoFormat("[{0}] client: {1} is no longer blocked.",
                                                                 _options.ReportingName, str);
                                            }
                                            _lockSlim.EnterReadLock();
                                            if (_tempBlocked.Count > 0)
                                                _forgetTimer.Enabled = true;
                                            _lockSlim.ExitReadLock();
                                        };
                                        
            _forgetTimer.Interval = _options.ForgetTimeSpan.TotalMilliseconds;
        }
        public XmlRpcResponse Process(XmlRpcRequest request, IPEndPoint client)
        {
            // If these are set like this, this is disabled
            if (_options.MaxRequestsInTimeframe < 1 || _options.RequestTimeSpan.TotalMilliseconds < 1)
                return _normalMethod(request, client);
           
            string clientstring = GetClientString(request, client);

            _lockSlim.EnterReadLock();
            if (_tempBlocked.ContainsKey(clientstring))
            {
                _lockSlim.ExitReadLock();

                if (_options.ThrottledAction == ThrottleAction.DoThrottledMethod)
                    return _throttledMethod(request, client);
                else
                    throw new System.Security.SecurityException("Throttled");
            }
            _lockSlim.ExitReadLock();

            _generalRequestTimes.Put(Util.EnvironmentTickCount());
            
            if (_generalRequestTimes.Size == _generalRequestTimes.Capacity &&
                (Util.EnvironmentTickCountSubtract(Util.EnvironmentTickCount(), _generalRequestTimes.Get()) <
                 _options.RequestTimeSpan.TotalMilliseconds))
            {
                //Trigger deeper inspection
                if (DeeperInspection(request, client))
                    return _normalMethod(request, client);
                if (_options.ThrottledAction == ThrottleAction.DoThrottledMethod)
                    return _throttledMethod(request, client);
                else
                    throw new System.Security.SecurityException("Throttled");
            }
            XmlRpcResponse resp = null;
           
            resp = _normalMethod(request, client);
           
            return resp;
        }

        // If the service is getting more hits per expected timeframe then it starts to separate them out by client
        private bool DeeperInspection(XmlRpcRequest request, IPEndPoint client)
        {
            lock (_deeperInspection)
            {
                string clientstring = GetClientString(request, client);
                

                if (_deeperInspection.ContainsKey(clientstring))
                {
                    _deeperInspection[clientstring].Put(Util.EnvironmentTickCount());
                    if (_deeperInspection[clientstring].Size == _deeperInspection[clientstring].Capacity &&
                        (Util.EnvironmentTickCountSubtract(Util.EnvironmentTickCount(), _deeperInspection[clientstring].Get()) <
                         _options.RequestTimeSpan.TotalMilliseconds))
                    {
                        //Looks like we're over the limit
                        _lockSlim.EnterWriteLock();
                        if (!_tempBlocked.ContainsKey(clientstring))
                            _tempBlocked.Add(clientstring, Util.EnvironmentTickCount() + (int)_options.ForgetTimeSpan.TotalMilliseconds);
                        else
                            _tempBlocked[clientstring] = Util.EnvironmentTickCount() + (int)_options.ForgetTimeSpan.TotalMilliseconds;
                        _lockSlim.ExitWriteLock();

                        m_log.WarnFormat("[{0}]: client: {1} is blocked for {2} milliseconds, X-ForwardedForAllowed status is {3}, endpoint:{4}",_options.ReportingName,clientstring,_options.ForgetTimeSpan.TotalMilliseconds, _options.AllowXForwardedFor, client.Address);
                        
                        return false;
                    }
                    //else
                     //   return true;
                }
                else
                {
                    _deeperInspection.Add(clientstring, new CircularBuffer<int>(_options.MaxRequestsInTimeframe + 1, true));
                    _deeperInspection[clientstring].Put(Util.EnvironmentTickCount());
                    _forgetTimer.Enabled = true;
                }

            }
            return true;
        }
        private string GetClientString(XmlRpcRequest request, IPEndPoint client)
        {
            string clientstring;
            if (_options.AllowXForwardedFor && request.Params.Count > 3)
            {
                object headerstr = request.Params[3];
                if (headerstr != null && !string.IsNullOrEmpty(headerstr.ToString()))
                    clientstring = request.Params[3].ToString();
                else
                    clientstring = client.Address.ToString();
            }
            else
                clientstring = client.Address.ToString();
            return clientstring;
        }

    }

    public class BasicDosProtectorOptions
    {
        public int MaxRequestsInTimeframe;
        public TimeSpan RequestTimeSpan;
        public TimeSpan ForgetTimeSpan;
        public bool AllowXForwardedFor;
        public string ReportingName = "BASICDOSPROTECTOR";
        public ThrottleAction ThrottledAction = ThrottleAction.DoThrottledMethod;
    }
}
