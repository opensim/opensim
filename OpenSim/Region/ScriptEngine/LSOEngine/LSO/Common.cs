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
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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

/* Original code: Tedd Hansen */
using System;

namespace OpenSim.Region.ScriptEngine.LSOEngine.LSO
{
    public static class Common
    {
        public static bool Debug = true;
        public static bool IL_UseTryCatch = true;
        public static bool IL_CreateConstructor = true;
        public static bool IL_CreateFunctionList = true;
        public static bool IL_ProcessCodeChunks = true;

        public delegate void SendToDebugEventDelegate(string Message);

        public delegate void SendToLogEventDelegate(string Message);

        public static event SendToDebugEventDelegate SendToDebugEvent;
        public static event SendToLogEventDelegate SendToLogEvent;

        public static void SendToDebug(string Message)
        {
            //if (Debug == true)
            Console.WriteLine("COMPILER:Debug: " + Message);
            SendToDebugEvent("\r\n" + DateTime.Now.ToString("[HH:mm:ss] ") + Message);
        }

        public static void SendToLog(string Message)
        {
            //if (Debug == true)
            Console.WriteLine("COMPILER:LOG: " + Message);
            SendToLogEvent("\r\n" + DateTime.Now.ToString("[HH:mm:ss] ") + Message);
        }
    }

    public static class IL_Helper
    {
        public static string ReverseFormatString(string text1, string format)
        {
            Common.SendToDebug("ReverseFormatString text1: " + text1);
            Common.SendToDebug("ReverseFormatString format: " + format);
            return string.Format(format, text1);
        }

        public static string ReverseFormatString(string text1, UInt32 text2, string format)
        {
            Common.SendToDebug("ReverseFormatString text1: " + text1);
            Common.SendToDebug("ReverseFormatString text2: " + text2.ToString());
            Common.SendToDebug("ReverseFormatString format: " + format);
            return string.Format(format, text1, text2.ToString());
        }

        public static string Cast_ToString(object obj)
        {
            Common.SendToDebug("OBJECT TO BE CASTED: " + obj.GetType().ToString());
            return "ABCDEFGIHJKLMNOPQ123";
        }
    }
}
