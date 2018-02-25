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
using System.Text;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

namespace OpenSim.Region.ScriptEngine.Yengine
{

    public class ScriptConst
    {

        public static Dictionary<string, ScriptConst> scriptConstants = Init();

        /**
         * @brief look up the value of a given built-in constant.
         * @param name = name of constant
         * @returns null: no constant by that name defined
         *          else: pointer to ScriptConst struct
         */
        public static ScriptConst Lookup(string name)
        {
            ScriptConst sc;
            if(!scriptConstants.TryGetValue(name, out sc))
                sc = null;
            return sc;
        }

        private static Dictionary<string, ScriptConst> Init()
        {
            Dictionary<string, ScriptConst> sc = new Dictionary<string, ScriptConst>();

             // For every event code, define XMREVENTCODE_<eventname> and XMREVENTMASKn_<eventname> symbols.
            for(int i = 0; i < 64; i++)
            {
                try
                {
                    string s = ((ScriptEventCode)i).ToString();
                    if((s.Length > 0) && (s[0] >= 'a') && (s[0] <= 'z'))
                    {
                        new ScriptConst(sc,
                                         "XMREVENTCODE_" + s,
                                         new CompValuInteger(new TokenTypeInt(null), i));
                        int n = i / 32 + 1;
                        int m = 1 << (i % 32);
                        new ScriptConst(sc,
                                         "XMREVENTMASK" + n + "_" + s,
                                         new CompValuInteger(new TokenTypeInt(null), m));
                    }
                }
                catch { }
            }

             // Also get all the constants from XMRInstAbstract and ScriptBaseClass etc as well.
            for(Type t = typeof(XMRInstAbstract); t != typeof(object); t = t.BaseType)
            {
                AddInterfaceConstants(sc, t.GetFields());
            }

            return sc;
        }

        /**
         * @brief Add all constants defined by the given interface.
         */
        // this one accepts only upper-case named fields
        public static void AddInterfaceConstants(Dictionary<string, ScriptConst> sc, FieldInfo[] allFields)
        {
            List<FieldInfo> ucfs = new List<FieldInfo>(allFields.Length);
            foreach(FieldInfo f in allFields)
            {
                string fieldName = f.Name;
                int i;
                for(i = fieldName.Length; --i >= 0;)
                {
                    if("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_".IndexOf(fieldName[i]) < 0)
                        break;
                }
                if(i < 0)
                    ucfs.Add(f);
            }
            AddInterfaceConstants(sc, ucfs.GetEnumerator());
        }

        // this one accepts all fields given to it
        public static void AddInterfaceConstants(Dictionary<string, ScriptConst> sc, IEnumerator<FieldInfo> fields)
        {
            if(sc == null)
                sc = scriptConstants;

            for(fields.Reset(); fields.MoveNext();)
            {
                FieldInfo constField = fields.Current;
                Type fieldType = constField.FieldType;
                CompValu cv;

                 // The location of a simple number is the number itself.
                 // Access to the value gets compiled as an ldc instruction.
                if(fieldType == typeof(double))
                {
                    cv = new CompValuFloat(new TokenTypeFloat(null),
                                            (double)(double)constField.GetValue(null));
                }
                else if(fieldType == typeof(int))
                {
                    cv = new CompValuInteger(new TokenTypeInt(null),
                                              (int)constField.GetValue(null));
                }
                else if(fieldType == typeof(LSL_Integer))
                {
                    cv = new CompValuInteger(new TokenTypeInt(null),
                                              ((LSL_Integer)constField.GetValue(null)).value);
                }

                 // The location of a string is the string itself.
                 // Access to the value gets compiled as an ldstr instruction.
                else if(fieldType == typeof(string))
                {
                    cv = new CompValuString(new TokenTypeStr(null),
                                             (string)constField.GetValue(null));
                }
                else if(fieldType == typeof(LSL_String))
                {
                    cv = new CompValuString(new TokenTypeStr(null),
                                             (string)(LSL_String)constField.GetValue(null));
                }

                 // The location of everything else (objects) is the static field in the interface definition.
                 // Access to the value gets compiled as an ldsfld instruction.
                else
                {
                    cv = new CompValuSField(TokenType.FromSysType(null, fieldType), constField);
                }

                 // Add to dictionary.
                new ScriptConst(sc, constField.Name, cv);
            }
        }

        /**
         * @brief Add arbitrary constant available to script compilation.
         * CAUTION: These values get compiled-in to a script and must not
         *          change over time as previously compiled scripts will
         *          still have the old values.
         */
        public static ScriptConst AddConstant(string name, object value)
        {
            CompValu cv = null;

            if(value is char)
            {
                cv = new CompValuChar(new TokenTypeChar(null), (char)value);
            }
            if(value is double)
            {
                cv = new CompValuFloat(new TokenTypeFloat(null), (double)(double)value);
            }
            if(value is float)
            {
                cv = new CompValuFloat(new TokenTypeFloat(null), (double)(float)value);
            }
            if(value is int)
            {
                cv = new CompValuInteger(new TokenTypeInt(null), (int)value);
            }
            if(value is string)
            {
                cv = new CompValuString(new TokenTypeStr(null), (string)value);
            }

            if(value is LSL_Float)
            {
                cv = new CompValuFloat(new TokenTypeFloat(null), (double)((LSL_Float)value).value);
            }
            if(value is LSL_Integer)
            {
                cv = new CompValuInteger(new TokenTypeInt(null), ((LSL_Integer)value).value);
            }
            if(value is LSL_Rotation)
            {
                LSL_Rotation r = (LSL_Rotation)value;
                CompValu x = new CompValuFloat(new TokenTypeFloat(null), r.x);
                CompValu y = new CompValuFloat(new TokenTypeFloat(null), r.y);
                CompValu z = new CompValuFloat(new TokenTypeFloat(null), r.z);
                CompValu s = new CompValuFloat(new TokenTypeFloat(null), r.s);
                cv = new CompValuRot(new TokenTypeRot(null), x, y, z, s);
            }
            if(value is LSL_String)
            {
                cv = new CompValuString(new TokenTypeStr(null), (string)(LSL_String)value);
            }
            if(value is LSL_Vector)
            {
                LSL_Vector v = (LSL_Vector)value;
                CompValu x = new CompValuFloat(new TokenTypeFloat(null), v.x);
                CompValu y = new CompValuFloat(new TokenTypeFloat(null), v.y);
                CompValu z = new CompValuFloat(new TokenTypeFloat(null), v.z);
                cv = new CompValuVec(new TokenTypeVec(null), x, y, z);
            }

            if(value is OpenMetaverse.Quaternion)
            {
                OpenMetaverse.Quaternion r = (OpenMetaverse.Quaternion)value;
                CompValu x = new CompValuFloat(new TokenTypeFloat(null), r.X);
                CompValu y = new CompValuFloat(new TokenTypeFloat(null), r.Y);
                CompValu z = new CompValuFloat(new TokenTypeFloat(null), r.Z);
                CompValu s = new CompValuFloat(new TokenTypeFloat(null), r.W);
                cv = new CompValuRot(new TokenTypeRot(null), x, y, z, s);
            }
            if(value is OpenMetaverse.UUID)
            {
                cv = new CompValuString(new TokenTypeKey(null), value.ToString());
            }
            if(value is OpenMetaverse.Vector3)
            {
                OpenMetaverse.Vector3 v = (OpenMetaverse.Vector3)value;
                CompValu x = new CompValuFloat(new TokenTypeFloat(null), v.X);
                CompValu y = new CompValuFloat(new TokenTypeFloat(null), v.Y);
                CompValu z = new CompValuFloat(new TokenTypeFloat(null), v.Z);
                cv = new CompValuVec(new TokenTypeVec(null), x, y, z);
            }

            if(cv == null)
                throw new Exception("bad type " + value.GetType().Name);
            return new ScriptConst(scriptConstants, name, cv);
        }

        /*
         * Instance variables
         */
        public string name;
        public CompValu rVal;

        private ScriptConst(Dictionary<string, ScriptConst> lc, string name, CompValu rVal)
        {
            lc.Add(name, this);
            this.name = name;
            this.rVal = rVal;
        }
    }
}
