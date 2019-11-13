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
using System.Threading;

namespace OpenSim.Region.ScriptEngine.Yengine
{

    public partial class XMRInstance
    {
        /**
        * @brief Start script event handler from the beginning.
        *        Return when either the script event handler completes
        *        or the script calls Hiber().
        * @returns null: script did not throw any exception so far
        *          else: script threw an exception
        */
        public Exception StartEx()
        {
            // Start script event handler from very beginning.
            callMode = XMRInstance.CallMode_NORMAL;
            try
            {
                CallSEH();                 // run script event handler
            }
            catch(StackHibernateException)
            {
                if(callMode != XMRInstance.CallMode_SAVE)
                    throw new Exception("callMode=" + callMode);
            }
            catch(Exception e)
            {
                return e;
            }

            return null;
        }

        /**
         * @brief We now want to run some more script code from where it last hibernated
         *        until it either finishes the script event handler or until the script
         *        calls Hiber() again.
         */
        public Exception ResumeEx()
        {
            // Resume script from captured stack.
            callMode = XMRInstance.CallMode_RESTORE;
            suspendOnCheckRunTemp = true;
            try
            {
                CallSEH();                 // run script event handler
            }
            catch(StackHibernateException)
            {
                if(callMode != XMRInstance.CallMode_SAVE)
                    throw new Exception("callMode=" + callMode);
            }
            catch (Exception e)
            {
                return e;
            }

            return null;
        }

        public class StackHibernateException: Exception, IXMRUncatchable
        {
        }
    }
}
