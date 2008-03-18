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
            Prim = new OSSLPrim(this);
        }

        public OSSLPrim Prim;

        [Serializable]
        public class OSSLPrim
        {
            internal OSSL_BuilIn_Commands OSSL;
            public OSSLPrim(OSSL_BuilIn_Commands bc)
            {
                OSSL = bc;
                Position = new OSSLPrim_Position(this);
                Rotation = new OSSLPrim_Rotation(this);
            }

            public OSSLPrim_Position Position;
            public OSSLPrim_Rotation Rotation;
            //public LSL_Types.Vector3 Position
            //{
            //    get { return OSSL.llGetPos(); }
            //    set { OSSL.llSetPos(value); }
            //}
            //public LSL_Types.Quaternion Rotation
            //{
            //    get { return OSSL.llGetRot(); }
            //    set { OSSL.llSetRot(value); }
            //}
            private TextStruct _text;
            public TextStruct Text
            {
                get { return _text; }
                set
                {
                    _text = value;
                    OSSL.llSetText(_text.Text, _text.color, _text.alpha);
                }
            }

            [Serializable]
            public struct TextStruct
            {
                public string Text;
                public LSL_Types.Vector3 color;
                public double alpha;
            }
        }

        [Serializable]
        public class OSSLPrim_Position
        {
            private OSSLPrim prim;
            private LSL_Types.Vector3 Position;
            public OSSLPrim_Position(OSSLPrim _prim)
            {
                prim = _prim;
            }
            private void Load()
            {
                Position = prim.OSSL.llGetPos();
            }
            private void Save()
            {
                if (Position.x > 255)
                    Position.x = 255;
                if (Position.x < 0)
                    Position.x = 0;
                if (Position.y > 255)
                    Position.y = 255;
                if (Position.y < 0)
                    Position.y = 0;
                if (Position.z > 768)
                    Position.z = 768;
                if (Position.z < 0)
                    Position.z = 0;
                prim.OSSL.llSetPos(Position);
            }

            public double x
            {
                get
                {
                    Load();
                    return Position.x;
                }
                set
                {
                    Load();
                    Position.x = value;
                    Save();
                }
            }
            public double y
            {
                get
                {
                    Load();
                    return Position.y;
                }
                set
                {
                    Load();
                    Position.y = value;
                    Save();
                }
            }
            public double z
            {
                get
                {
                    Load();
                    return Position.z;
                }
                set
                {
                    Load();
                    Position.z = value;
                    Save();
                }
            }
        }

        [Serializable]
        public class OSSLPrim_Rotation
        {
            private OSSLPrim prim;
            private LSL_Types.Quaternion Rotation;
            public OSSLPrim_Rotation(OSSLPrim _prim)
            {
                prim = _prim;
            }
            private void Load()
            {
                Rotation = prim.OSSL.llGetRot();
            }
            private void Save()
            {
                prim.OSSL.llSetRot(Rotation);
            }

            public double x
            {
                get
                {
                    Load();
                    return Rotation.x;
                }
                set
                {
                    Load();
                    Rotation.x = value;
                    Save();
                }
            }
            public double y
            {
                get
                {
                    Load();
                    return Rotation.y;
                }
                set
                {
                    Load();
                    Rotation.y = value;
                    Save();
                }
            }
            public double z
            {
                get
                {
                    Load();
                    return Rotation.z;
                }
                set
                {
                    Load();
                    Rotation.z = value;
                    Save();
                }
            }
            public double s
            {
                get
                {
                    Load();
                    return Rotation.s;
                }
                set
                {
                    Load();
                    Rotation.s = value;
                    Save();
                }
            }
        }
        //public struct OSSLPrim_Rotation
        //{
        //    public double X;
        //    public double Y;
        //    public double Z;
        //    public double R;
        //}
    }
}
