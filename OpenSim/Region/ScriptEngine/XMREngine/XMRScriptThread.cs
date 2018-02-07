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

using OpenSim.Framework.Monitoring;
using System;
using System.Collections.Generic;
using System.Threading;

namespace OpenSim.Region.ScriptEngine.XMREngine
{
    /**
     * @brief There are NUMSCRIPTHREADWKRS of these.
     *        Each sits in a loop checking the Start and Yield queues for 
     *        a script to run and calls the script as a microthread.
     */
    public class XMRScriptThread
    {
        public bool         m_WakeUpThis = false;
        public  DateTime    m_LastRanAt = DateTime.MinValue;
        public  int         m_ScriptThreadTID = 0;
        public  long        m_ScriptExecTime = 0;
        private Thread      thd;
        private XMREngine   engine;
        public  XMRInstance m_RunInstance = null;

        public XMRScriptThread(XMREngine eng, int i)
        {
            engine = eng;
            if(i < 0)
                thd = XMREngine.StartMyThread(RunScriptThread, "xmrengine script", ThreadPriority.Normal);
            else
                thd = XMREngine.StartMyThread(RunScriptThread, "xmrengineExec" + i.ToString(), ThreadPriority.Normal);
            engine.AddThread(thd, this);
            m_ScriptThreadTID = thd.ManagedThreadId;
        }

        public void Terminate()
        {
            m_WakeUpThis = true;
            if(!thd.Join(250))
                thd.Abort();

            engine.RemoveThread(thd);

            thd = null;
        }
 
        /**
         * @brief Wake up this XMRScriptThread instance.
         */
        public void WakeUpScriptThread()
        {
                m_WakeUpThis = true;
        }

        /**
         * @brief A script instance was just removed from the Start or Yield Queue.
         *        So run it for a little bit then stick in whatever queue it should go in.
         */

        private void RunScriptThread()
        {
            engine.RunScriptThread(this);
        }

        public void RunInstance (XMRInstance inst)
        {
            m_LastRanAt = DateTime.UtcNow;
            m_ScriptExecTime -= (long)(m_LastRanAt - DateTime.MinValue).TotalMilliseconds;
            inst.m_IState = XMRInstState.RUNNING;
            m_RunInstance = inst;
            XMRInstState newIState = inst.RunOne();
            m_RunInstance = null;
            engine.HandleNewIState(inst, newIState);
            m_ScriptExecTime += (long)(DateTime.UtcNow - DateTime.MinValue).TotalMilliseconds;
        }
    }
}
