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
 */

namespace OpenSim.Region.ScriptEngine.DotNetEngine
{
    public static class Common
    {
        public static bool debug = true;
        public static ScriptEngine mySE;

        // This class just contains some static log stuff used for debugging.

        //public delegate void SendToDebugEventDelegate(string message);
        //public delegate void SendToLogEventDelegate(string message);
        //static public event SendToDebugEventDelegate SendToDebugEvent;
        //static public event SendToLogEventDelegate SendToLogEvent;

        public static void SendToDebug(string message)
        {
            //if (Debug == true)
            mySE.Log.Info("[" + mySE.ScriptEngineName + "]: Debug: " + message);
            //SendToDebugEvent("\r\n" + DateTime.Now.ToString("[HH:mm:ss] ") + message);
        }

        public static void SendToLog(string message)
        {
            //if (Debug == true)
            mySE.Log.Info("[" + mySE.ScriptEngineName + "]: LOG: " + message);
            //SendToLogEvent("\r\n" + DateTime.Now.ToString("[HH:mm:ss] ") + message);
        }
    }
}
