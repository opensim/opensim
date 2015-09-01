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
using System.Threading;

namespace OpenSim.Framework.Communications
{
    internal class SimpleAsyncResult : IAsyncResult
    {
        private readonly AsyncCallback m_callback;

        /// <summary>
        /// Is process completed?
        /// </summary>
        /// <remarks>Should really be boolean, but VolatileRead has no boolean method</remarks>
        private byte m_completed;

        /// <summary>
        /// Did process complete synchronously?
        /// </summary>
        /// <remarks>I have a hard time imagining a scenario where this is the case, again, same issue about
        /// booleans and VolatileRead as m_completed
        /// </remarks>
        private byte m_completedSynchronously;

        private readonly object m_asyncState;
        private ManualResetEvent m_waitHandle;
        private Exception m_exception;

        internal SimpleAsyncResult(AsyncCallback cb, object state)
        {
            m_callback = cb;
            m_asyncState = state;
            m_completed = 0;
            m_completedSynchronously = 1;
        }

        #region IAsyncResult Members

        public object AsyncState
        {
            get { return m_asyncState; }
        }

        public WaitHandle AsyncWaitHandle
        {
            get
            {
                if (m_waitHandle == null)
                {
                    bool done = IsCompleted;
                    ManualResetEvent mre = new ManualResetEvent(done);
                    if (Interlocked.CompareExchange(ref m_waitHandle, mre, null) != null)
                    {
                        mre.Close();
                    }
                    else
                    {
                        if (!done && IsCompleted)
                        {
                            m_waitHandle.Set();
                        }
                    }
                }
                
                return m_waitHandle;
            }
        }


        public bool CompletedSynchronously
        {
            get { return Thread.VolatileRead(ref m_completedSynchronously) == 1; }
        }


        public bool IsCompleted
        {
            get { return Thread.VolatileRead(ref m_completed) == 1; }
        }

        #endregion

        #region class Methods

        internal void SetAsCompleted(bool completedSynchronously)
        {
            m_completed = 1;
            if (completedSynchronously)
                m_completedSynchronously = 1;
            else
                m_completedSynchronously = 0;

            SignalCompletion();
        }

        internal void HandleException(Exception e, bool completedSynchronously)
        {
            m_completed = 1;
            if (completedSynchronously)
                m_completedSynchronously = 1;
            else
                m_completedSynchronously = 0;
            m_exception = e;

            SignalCompletion();
        }

        private void SignalCompletion()
        {
            if (m_waitHandle != null) m_waitHandle.Set();

            if (m_callback != null) m_callback(this);
        }

        public void EndInvoke()
        {
            // This method assumes that only 1 thread calls EndInvoke
            if (!IsCompleted)
            {
                // If the operation isn't done, wait for it
                AsyncWaitHandle.WaitOne();
                AsyncWaitHandle.Close();
                m_waitHandle.Close();
                m_waitHandle = null; // Allow early GC
            }

            // Operation is done: if an exception occured, throw it
            if (m_exception != null) throw m_exception;
        }

        #endregion
    }

    internal class AsyncResult<T> : SimpleAsyncResult
    {
        private T m_result = default(T);

        public AsyncResult(AsyncCallback asyncCallback, Object state) :
            base(asyncCallback, state)
        {
        }

        public void SetAsCompleted(T result, bool completedSynchronously)
        {
            // Save the asynchronous operation's result
            m_result = result;

            // Tell the base class that the operation completed
            // sucessfully (no exception)
            base.SetAsCompleted(completedSynchronously);
        }

        public new T EndInvoke()
        {
            base.EndInvoke();
            return m_result;
        }
    }
}