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
using log4net;

namespace OpenSim.Framework
{

    public class BasicDOSProtector
    {
        public enum ThrottleAction
        {
            DoThrottledMethod,
            DoThrow
        }
        private readonly CircularBuffer<int> _generalRequestTimes; // General request checker
        private readonly BasicDosProtectorOptions _options;
        private readonly Dictionary<string, CircularBuffer<int>> _deeperInspection;   // per client request checker
        private readonly Dictionary<string, int> _tempBlocked;  // blocked list
        private readonly Dictionary<string, int> _sessions;
        private readonly System.Timers.Timer _forgetTimer;  // Cleanup timer
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly System.Threading.ReaderWriterLockSlim _blockLockSlim = new System.Threading.ReaderWriterLockSlim();
        private readonly System.Threading.ReaderWriterLockSlim _sessionLockSlim = new System.Threading.ReaderWriterLockSlim();
        public BasicDOSProtector(BasicDosProtectorOptions options)
        {
            _generalRequestTimes = new CircularBuffer<int>(options.MaxRequestsInTimeframe + 1, true);
            _generalRequestTimes.Put(0);
            _options = options;
            _deeperInspection = new Dictionary<string, CircularBuffer<int>>();
            _tempBlocked = new Dictionary<string, int>();
            _sessions = new Dictionary<string, int>();
            _forgetTimer = new System.Timers.Timer();
            _forgetTimer.Elapsed += delegate
            {
                _forgetTimer.Enabled = false;

                List<string> removes = new List<string>();
                _blockLockSlim.EnterReadLock();
                foreach (string str in _tempBlocked.Keys)
                {
                    if (
                        Util.EnvironmentTickCountSubtract(Util.EnvironmentTickCount(),
                                                          _tempBlocked[str]) > 0)
                        removes.Add(str);
                }
                _blockLockSlim.ExitReadLock();
                lock (_deeperInspection)
                {
                    _blockLockSlim.EnterWriteLock();
                    for (int i = 0; i < removes.Count; i++)
                    {
                        _tempBlocked.Remove(removes[i]);
                        _deeperInspection.Remove(removes[i]);
                        _sessions.Remove(removes[i]);
                    }
                    _blockLockSlim.ExitWriteLock();
                }
                foreach (string str in removes)
                {
                    m_log.InfoFormat("[{0}] client: {1} is no longer blocked.",
                                     _options.ReportingName, str);
                }
                _blockLockSlim.EnterReadLock();
                if (_tempBlocked.Count > 0)
                    _forgetTimer.Enabled = true;
                _blockLockSlim.ExitReadLock();
            };

            _forgetTimer.Interval = _options.ForgetTimeSpan.TotalMilliseconds;
        }

        /// <summary>
        /// Given a string Key, Returns if that context is blocked
        /// </summary>
        /// <param name="key">A Key identifying the context</param>
        /// <returns>bool Yes or No, True or False for blocked</returns>
        public bool IsBlocked(string key)
        {
            bool ret = false;
             _blockLockSlim.EnterReadLock();
            ret = _tempBlocked.ContainsKey(key);
            _blockLockSlim.ExitReadLock();
            return ret;
        }

        /// <summary>
        /// Process the velocity of this context
        /// </summary>
        /// <param name="key"></param>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        public bool Process(string key, string endpoint)
        {
            if (_options.MaxRequestsInTimeframe < 1 || _options.RequestTimeSpan.TotalMilliseconds < 1)
                return true;

            string clientstring = key;

            _blockLockSlim.EnterReadLock();
            if (_tempBlocked.ContainsKey(clientstring))
            {
                _blockLockSlim.ExitReadLock();

                if (_options.ThrottledAction == ThrottleAction.DoThrottledMethod)
                    return false;
                else
                    throw new System.Security.SecurityException("Throttled");
            }

            _blockLockSlim.ExitReadLock();

            lock (_generalRequestTimes)
                _generalRequestTimes.Put(Util.EnvironmentTickCount());

            if (_options.MaxConcurrentSessions > 0)
            {
                int sessionscount = 0;

                _sessionLockSlim.EnterReadLock();
                if (_sessions.ContainsKey(key))
                    sessionscount = _sessions[key];
                _sessionLockSlim.ExitReadLock();

                if (sessionscount > _options.MaxConcurrentSessions)
                {
                    // Add to blocking and cleanup methods
                    lock (_deeperInspection)
                    {
                        _blockLockSlim.EnterWriteLock();
                        if (!_tempBlocked.ContainsKey(clientstring))
                        {
                            _tempBlocked.Add(clientstring,
                                             Util.EnvironmentTickCount() +
                                             (int) _options.ForgetTimeSpan.TotalMilliseconds);
                            _forgetTimer.Enabled = true;
                            m_log.WarnFormat("[{0}]: client: {1} is blocked for {2} milliseconds based on concurrency, X-ForwardedForAllowed status is {3}, endpoint:{4}", _options.ReportingName, clientstring, _options.ForgetTimeSpan.TotalMilliseconds, _options.AllowXForwardedFor, endpoint);

                        }
                        else
                            _tempBlocked[clientstring] = Util.EnvironmentTickCount() +
                                                         (int) _options.ForgetTimeSpan.TotalMilliseconds;
                        _blockLockSlim.ExitWriteLock();

                    }


                }
                else
                    ProcessConcurrency(key, endpoint);
            }
            if (_generalRequestTimes.Size == _generalRequestTimes.Capacity &&
                (Util.EnvironmentTickCountSubtract(Util.EnvironmentTickCount(), _generalRequestTimes.Get()) <
                 _options.RequestTimeSpan.TotalMilliseconds))
            {
                //Trigger deeper inspection
                if (DeeperInspection(key, endpoint))
                    return true;
                if (_options.ThrottledAction == ThrottleAction.DoThrottledMethod)
                    return false;
                else
                    throw new System.Security.SecurityException("Throttled");
            }
            return true;
        }
        private void ProcessConcurrency(string key, string endpoint)
        {
            _sessionLockSlim.EnterWriteLock();
            if (_sessions.ContainsKey(key))
                _sessions[key] = _sessions[key] + 1;
            else
                _sessions.Add(key,1);
            _sessionLockSlim.ExitWriteLock();
        }
        public void ProcessEnd(string key, string endpoint)
        {
            _sessionLockSlim.EnterWriteLock();
            if (_sessions.ContainsKey(key))
            {
                _sessions[key]--;
                if (_sessions[key] <= 0)
                    _sessions.Remove(key);
            }
            else
                _sessions.Add(key, 1);

            _sessionLockSlim.ExitWriteLock();
        }

        /// <summary>
        /// At this point, the rate limiting code needs to track 'per user' velocity.
        /// </summary>
        /// <param name="key">Context Key, string representing a rate limiting context</param>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        private bool DeeperInspection(string key, string endpoint)
        {
            lock (_deeperInspection)
            {
                string clientstring = key;


                if (_deeperInspection.ContainsKey(clientstring))
                {
                    _deeperInspection[clientstring].Put(Util.EnvironmentTickCount());
                    if (_deeperInspection[clientstring].Size == _deeperInspection[clientstring].Capacity &&
                        (Util.EnvironmentTickCountSubtract(Util.EnvironmentTickCount(), _deeperInspection[clientstring].Get()) <
                         _options.RequestTimeSpan.TotalMilliseconds))
                    {
                        //Looks like we're over the limit
                        _blockLockSlim.EnterWriteLock();
                        if (!_tempBlocked.ContainsKey(clientstring))
                            _tempBlocked.Add(clientstring, Util.EnvironmentTickCount() + (int)_options.ForgetTimeSpan.TotalMilliseconds);
                        else
                            _tempBlocked[clientstring] = Util.EnvironmentTickCount() + (int)_options.ForgetTimeSpan.TotalMilliseconds;
                        _blockLockSlim.ExitWriteLock();

                        m_log.WarnFormat("[{0}]: client: {1} is blocked for {2} milliseconds, X-ForwardedForAllowed status is {3}, endpoint:{4}", _options.ReportingName, clientstring, _options.ForgetTimeSpan.TotalMilliseconds, _options.AllowXForwardedFor, endpoint);

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

    }


    public class BasicDosProtectorOptions
    {
        public int MaxRequestsInTimeframe;
        public TimeSpan RequestTimeSpan;
        public TimeSpan ForgetTimeSpan;
        public bool AllowXForwardedFor;
        public string ReportingName = "BASICDOSPROTECTOR";
        public BasicDOSProtector.ThrottleAction ThrottledAction = BasicDOSProtector.ThrottleAction.DoThrottledMethod;
        public int MaxConcurrentSessions;
    }
}
