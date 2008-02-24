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
* 
*/
using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.ScriptEngine.Common
{
    public class OSSL_BuilIn_Commands : LSL_BuiltIn_Commands, OSSL_BuilIn_Commands_Interface
    {
        public OSSL_BuilIn_Commands(ScriptEngineBase.ScriptEngine scriptEngine, SceneObjectPart host, uint localID,
                                    LLUUID itemID)
            : base(scriptEngine, host, localID, itemID)
        {
        }

        private OSSLPrim Prim;

        public class OSSLPrim
        {
            private OSSL_BuilIn_Commands OSSL;
            public OSSLPrim(OSSL_BuilIn_Commands bc)
            {
                OSSL = bc;
            }

            public LSL_Types.Vector3 Position {
                get { return OSSL.llGetPos(); }
                set { OSSL.llSetPos(value); }
            }
            public LSL_Types.Quaternion Rotation { 
                get { return OSSL.llGetRot(); } 
                set { OSSL.llSetRot(value); }
            }
            private TextStruct _text;
            public TextStruct Text
            {
                get { return _text; }
                set { _text = value;
                    OSSL.llSetText(_text.Text, _text.color, _text.alpha); }
            }

            public struct TextStruct
            {
                public string Text;
                public LSL_Types.Vector3 color;
                public double alpha;
            }
        }
        //public struct OSSLPrim_Position
        //{
        //    public int X;
        //    public int Y;
        //    public int Z;
        //}
        //public struct OSSLPrim_Rotation
        //{
        //    public double X;
        //    public double Y;
        //    public double Z;
        //    public double R;
        //}


    }
}
