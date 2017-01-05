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
using System.Threading;
using Amib.Threading;
using OpenSim.Region.ScriptEngine.Interfaces;

namespace OpenSim.Region.ScriptEngine.XEngine
{
    public class XWorkItem : IScriptWorkItem
    {
        private IWorkItemResult wr;

        public IWorkItemResult WorkItem
        {
            get { return wr; }
        }

        public XWorkItem(IWorkItemResult w)
        {
            wr = w;
        }

        public bool Cancel()
        {
            return wr.Cancel();
        }

        public bool Abort()
        {
            return wr.Cancel(true);
        }

        public bool Wait(int t)
        {
            // We use the integer version of WaitAll because the current version of SmartThreadPool has a bug with the
            // TimeSpan version.  The number of milliseconds in TimeSpan is an int64 so when STP casts it down to an
            // int (32-bit) we can end up with bad values.  This occurs on Windows though curiously not on Mono 2.10.8
            // (or very likely other versions of Mono at least up until 3.0.3).
            return SmartThreadPool.WaitAll(new IWorkItemResult[] {wr}, t, false);
        }
    }
}
