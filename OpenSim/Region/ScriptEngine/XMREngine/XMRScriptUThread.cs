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

/***************************\
 *  Use standard C# code   *
 *  - uses stack smashing  *
\***************************/

namespace OpenSim.Region.ScriptEngine.XMREngine
{

    public class ScriptUThread_Nul : IScriptUThread, IDisposable
    {
        private int active;     // -1: hibernating
                                //  0: exited
                                //  1: running
        private XMRInstance instance;

        public ScriptUThread_Nul (XMRInstance instance)
        {
            this.instance = instance;
        }

        /**
         * @brief Start script event handler from the beginning.
         *        Return when either the script event handler completes
         *        or the script calls Hiber().
         * @returns null: script did not throw any exception so far
         *          else: script threw an exception
         */
        public Exception StartEx ()
        {
             // We should only be called when no event handler running.
            if (active != 0)
                throw new Exception ("active=" + active);

             // Start script event handler from very beginning.
            active = 1;
            Exception except = null;
            instance.callMode = XMRInstance.CallMode_NORMAL;
            try
            {
                instance.CallSEH ();        // run script event handler
                active = 0;
            }
            catch (StackHibernateException)
            {
                if (instance.callMode != XMRInstance.CallMode_SAVE)
                {
                    throw new Exception ("callMode=" + instance.callMode);
                }
                active = -1;                // it is hibernating, can be resumed
            }
            catch (Exception e)
            {
                active = 0;
                except = e;                 // threw exception, save for Start()/Resume()
            }

             // Return whether or not script threw an exception.
            return except;
        }

        /**
         * @brief We now want to run some more script code from where it last hibernated
         *        until it either finishes the script event handler or until the script
         *        calls Hiber() again.
         */
        public Exception ResumeEx ()
        {
             // We should only be called when script is hibernating.
            if (active >= 0)
                throw new Exception ("active=" + active);

             // Resume script from captured stack.
            instance.callMode = XMRInstance.CallMode_RESTORE;
            instance.suspendOnCheckRunTemp = true;
            Exception except = null;
            try
            {
                instance.CallSEH ();        // run script event handler
                active = 0;
            }
            catch (StackHibernateException)
            {
                if (instance.callMode != XMRInstance.CallMode_SAVE)
                {
                    throw new Exception ("callMode=" + instance.callMode);
                }
                active = -1;
            }
            catch (Exception e)
            {
                active = 0;
                except = e;                 // threw exception, save for Start()/Resume()
            }

             // Return whether or not script threw an exception.
            return except;
        }

        /**
         * @brief Script is being closed out.
         *        Terminate thread asap.
         */
        public void Dispose ()
        { }

        /**
         * @brief Determine if script is active.
         * Returns: 0: nothing started or has returned
         *             Resume() must not be called
         *             Start() may be called
         *             Hiber() must not be called
         *         -1: thread has called Hiber()
         *             Resume() may be called
         *             Start() may be called
         *             Hiber() must not be called
         *          1: thread is running
         *             Resume() must not be called
         *             Start() must not be called
         *             Hiber() may be called
         */
        public int Active ()
        {
            return active;
        }

        /**
         * @brief Called by the script event handler whenever it wants to hibernate.
         */
        public void Hiber ()
        {
            if (instance.callMode != XMRInstance.CallMode_NORMAL) {
                throw new Exception ("callMode=" + instance.callMode);
            }

            switch (active) {

                // the stack has been restored as a result of calling ResumeEx()
                // say the microthread is now active and resume processing
                case -1: {
                    active = 1;
                    return;
                }

                // the script event handler wants to hibernate
                // capture stack frames and unwind to Start() or Resume()
                case 1: {
                    instance.callMode = XMRInstance.CallMode_SAVE;
                    instance.stackFrames = null;
                    throw new StackHibernateException ();
                }

                default: throw new Exception ("active=" + active);
            }
        }

        /**
         * @brief Number of remaining stack bytes.
         */
        public int StackLeft ()
        {
            return 0x7FFFFFFF;
        }

        public class StackHibernateException : Exception, IXMRUncatchable { }
    }
}

