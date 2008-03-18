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

using System;
using System.Collections.Generic;
using OpenSim.Region.ScriptEngine.Common;

namespace OpenSim.Grid.ScriptEngine.DotNetEngine.Compiler.LSO
{
    public partial class LSL_BaseClass
    {
        //public MemoryStream LSLStack = new MemoryStream();
        public Stack<object> LSLStack = new Stack<object>();
        public Dictionary<uint, object> StaticVariables = new Dictionary<uint, object>();
        public Dictionary<uint, object> GlobalVariables = new Dictionary<uint, object>();
        public Dictionary<uint, object> LocalVariables = new Dictionary<uint, object>();
        //public System.Collections.Generic.List<string> FunctionList = new System.Collections.Generic.List<string>();
        //public void AddFunction(String x) {
        //    FunctionList.Add(x);
        //}
        //public Stack<StackItemStruct> LSLStack = new Stack<StackItemStruct>;
        //public struct StackItemStruct
        //{
        //    public LSO_Enums.Variable_Type_Codes ItemType;
        //    public object Data;
        //}
        public UInt32 State = 0;
        public LSL_BuiltIn_Commands_Interface LSL_Builtins;

        public LSL_BuiltIn_Commands_Interface GetLSL_BuiltIn()
        {
            return LSL_Builtins;
        }


        public LSL_BaseClass()
        {
        }


        public virtual int OverrideMe()
        {
            return 0;
        }

        public void Start(LSL_BuiltIn_Commands_Interface LSLBuiltins)
        {
            LSL_Builtins = LSLBuiltins;

            Common.SendToLog("OpenSim.Grid.ScriptEngine.DotNetEngine.Compiler.LSO.LSL_BaseClass.Start() called");
            //LSL_Builtins.llSay(0, "Test");
            return;
        }

        public void AddToStatic(UInt32 index, object obj)
        {
            Common.SendToDebug("AddToStatic: " + index + " type: " + obj.GetType());
            StaticVariables.Add(index, obj);
        }
    }
}
