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

using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Region.ScriptEngine.Yengine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using OpenSim.Region.ScriptEngine.Shared;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

namespace OpenSim.Region.ScriptEngine.Yengine
{

    /**
     * @brief This class is used to catalog the code emit routines based on a key string
     *        The key string has the two types (eg, "integer", "rotation") and the operator (eg, "*", "!=")
     */
    public delegate void BinOpStrEmitBO(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result);
    public class BinOpStr
    {
        public static readonly Dictionary<string, BinOpStr> defined = DefineBinOps();

        public Type outtype;           // type of result of computation
        public BinOpStrEmitBO emitBO;  // how to compute result
        public bool rmwOK;             // is the <operator>= form valid?

        public BinOpStr(Type outtype, BinOpStrEmitBO emitBO)
        {
            this.outtype = outtype;
            this.emitBO = emitBO;
            this.rmwOK = false;
        }

        public BinOpStr(Type outtype, BinOpStrEmitBO emitBO, bool rmwOK)
        {
            this.outtype = outtype;
            this.emitBO = emitBO;
            this.rmwOK = rmwOK;
        }

        private static TokenTypeBool tokenTypeBool = new TokenTypeBool(null);
        private static TokenTypeChar tokenTypeChar = new TokenTypeChar(null);
        private static TokenTypeFloat tokenTypeFloat = new TokenTypeFloat(null);
        private static TokenTypeInt tokenTypeInt = new TokenTypeInt(null);
        private static TokenTypeList tokenTypeList = new TokenTypeList(null);
        private static TokenTypeRot tokenTypeRot = new TokenTypeRot(null);
        private static TokenTypeStr tokenTypeStr = new TokenTypeStr(null);
        private static TokenTypeVec tokenTypeVec = new TokenTypeVec(null);

        private static MethodInfo stringAddStringMethInfo = ScriptCodeGen.GetStaticMethod(typeof(string), "Concat", new Type[] { typeof(string), typeof(string) });
        private static MethodInfo stringCmpStringMethInfo = ScriptCodeGen.GetStaticMethod(typeof(string), "Compare", new Type[] { typeof(string), typeof(string), typeof(StringComparison) });

        private static MethodInfo infoMethListAddFloat = GetBinOpsMethod("MethListAddFloat", new Type[] { typeof(LSL_List), typeof(double) });
        private static MethodInfo infoMethListAddInt = GetBinOpsMethod("MethListAddInt", new Type[] { typeof(LSL_List), typeof(int) });
        private static MethodInfo infoMethListAddKey = GetBinOpsMethod("MethListAddKey", new Type[] { typeof(LSL_List), typeof(string) });
        private static MethodInfo infoMethListAddRot = GetBinOpsMethod("MethListAddRot", new Type[] { typeof(LSL_List), typeof(LSL_Rotation) });
        private static MethodInfo infoMethListAddStr = GetBinOpsMethod("MethListAddStr", new Type[] { typeof(LSL_List), typeof(string) });
        private static MethodInfo infoMethListAddVec = GetBinOpsMethod("MethListAddVec", new Type[] { typeof(LSL_List), typeof(LSL_Vector) });
        private static MethodInfo infoMethListAddList = GetBinOpsMethod("MethListAddList", new Type[] { typeof(LSL_List), typeof(LSL_List) });
        private static MethodInfo infoMethFloatAddList = GetBinOpsMethod("MethFloatAddList", new Type[] { typeof(double), typeof(LSL_List) });
        private static MethodInfo infoMethIntAddList = GetBinOpsMethod("MethIntAddList", new Type[] { typeof(int), typeof(LSL_List) });
        private static MethodInfo infoMethKeyAddList = GetBinOpsMethod("MethKeyAddList", new Type[] { typeof(string), typeof(LSL_List) });
        private static MethodInfo infoMethRotAddList = GetBinOpsMethod("MethRotAddList", new Type[] { typeof(LSL_Rotation), typeof(LSL_List) });
        private static MethodInfo infoMethStrAddList = GetBinOpsMethod("MethStrAddList", new Type[] { typeof(string), typeof(LSL_List) });
        private static MethodInfo infoMethVecAddList = GetBinOpsMethod("MethVecAddList", new Type[] { typeof(LSL_Vector), typeof(LSL_List) });
        private static MethodInfo infoMethListEqList = GetBinOpsMethod("MethListEqList", new Type[] { typeof(LSL_List), typeof(LSL_List) });
        private static MethodInfo infoMethListNeList = GetBinOpsMethod("MethListNeList", new Type[] { typeof(LSL_List), typeof(LSL_List) });
        private static MethodInfo infoMethRotEqRot = GetBinOpsMethod("MethRotEqRot", new Type[] { typeof(LSL_Rotation), typeof(LSL_Rotation) });
        private static MethodInfo infoMethRotNeRot = GetBinOpsMethod("MethRotNeRot", new Type[] { typeof(LSL_Rotation), typeof(LSL_Rotation) });
        private static MethodInfo infoMethRotAddRot = GetBinOpsMethod("MethRotAddRot", new Type[] { typeof(LSL_Rotation), typeof(LSL_Rotation) });
        private static MethodInfo infoMethRotSubRot = GetBinOpsMethod("MethRotSubRot", new Type[] { typeof(LSL_Rotation), typeof(LSL_Rotation) });
        private static MethodInfo infoMethRotMulRot = GetBinOpsMethod("MethRotMulRot", new Type[] { typeof(LSL_Rotation), typeof(LSL_Rotation) });
        private static MethodInfo infoMethRotDivRot = GetBinOpsMethod("MethRotDivRot", new Type[] { typeof(LSL_Rotation), typeof(LSL_Rotation) });
        private static MethodInfo infoMethVecEqVec = GetBinOpsMethod("MethVecEqVec", new Type[] { typeof(LSL_Vector), typeof(LSL_Vector) });
        private static MethodInfo infoMethVecNeVec = GetBinOpsMethod("MethVecNeVec", new Type[] { typeof(LSL_Vector), typeof(LSL_Vector) });
        private static MethodInfo infoMethVecAddVec = GetBinOpsMethod("MethVecAddVec", new Type[] { typeof(LSL_Vector), typeof(LSL_Vector) });
        private static MethodInfo infoMethVecSubVec = GetBinOpsMethod("MethVecSubVec", new Type[] { typeof(LSL_Vector), typeof(LSL_Vector) });
        private static MethodInfo infoMethVecMulVec = GetBinOpsMethod("MethVecMulVec", new Type[] { typeof(LSL_Vector), typeof(LSL_Vector) });
        private static MethodInfo infoMethVecModVec = GetBinOpsMethod("MethVecModVec", new Type[] { typeof(LSL_Vector), typeof(LSL_Vector) });
        private static MethodInfo infoMethVecMulFloat = GetBinOpsMethod("MethVecMulFloat", new Type[] { typeof(LSL_Vector), typeof(double) });
        private static MethodInfo infoMethFloatMulVec = GetBinOpsMethod("MethFloatMulVec", new Type[] { typeof(double), typeof(LSL_Vector) });
        private static MethodInfo infoMethVecDivFloat = GetBinOpsMethod("MethVecDivFloat", new Type[] { typeof(LSL_Vector), typeof(double) });
        private static MethodInfo infoMethVecMulInt = GetBinOpsMethod("MethVecMulInt", new Type[] { typeof(LSL_Vector), typeof(int) });
        private static MethodInfo infoMethIntMulVec = GetBinOpsMethod("MethIntMulVec", new Type[] { typeof(int), typeof(LSL_Vector) });
        private static MethodInfo infoMethVecDivInt = GetBinOpsMethod("MethVecDivInt", new Type[] { typeof(LSL_Vector), typeof(int) });
        private static MethodInfo infoMethVecMulRot = GetBinOpsMethod("MethVecMulRot", new Type[] { typeof(LSL_Vector), typeof(LSL_Rotation) });
        private static MethodInfo infoMethVecDivRot = GetBinOpsMethod("MethVecDivRot", new Type[] { typeof(LSL_Vector), typeof(LSL_Rotation) });
        private static MethodInfo infoMethDoubleDivDouble = GetBinOpsMethod("MethDoubleDivDouble", new Type[] { typeof(Double), typeof(Double) });
        private static MethodInfo infoMethLongDivLong = GetBinOpsMethod("MethLongDivLong", new Type[] { typeof(long), typeof(long) });
        private static MethodInfo infoMethDoubleModDouble = GetBinOpsMethod("MethDoubleModDouble", new Type[] { typeof(Double), typeof(Double) });
        private static MethodInfo infoMethLongModLong = GetBinOpsMethod("MethLongModLong", new Type[] { typeof(long), typeof(long) });

        private static MethodInfo GetBinOpsMethod(string name, Type[] types)
        {
            return ScriptCodeGen.GetStaticMethod(typeof(BinOpStr), name, types);
        }

        /**
         * @brief Create a dictionary for processing binary operators.
         *        This tells us, for a given type, an operator and another type,
         *        is the operation permitted, and if so, what is the type of the result?
         * The key is <lefttype><opcode><righttype>,
         *   where <lefttype> and <righttype> are strings returned by (TokenType...).ToString()
         *   and <opcode> is string returned by (TokenKw...).ToString()
         * The value is a BinOpStr struct giving the resultant type and a method to generate the code.
         */
        private static Dictionary<string, BinOpStr> DefineBinOps()
        {
            Dictionary<string, BinOpStr> bos = new Dictionary<string, BinOpStr>();

            string[] booltypes = new string[] { "bool", "char", "float", "integer", "key", "list", "string" };

            /*
             * Get the && and || all out of the way...
             * Simply cast their left and right operands to boolean then process.
             */
            for(int i = 0; i < booltypes.Length; i++)
            {
                for(int j = 0; j < booltypes.Length; j++)
                {
                    bos.Add(booltypes[i] + "&&" + booltypes[j],
                             new BinOpStr(typeof(bool), BinOpStrAndAnd));
                    bos.Add(booltypes[i] + "||" + booltypes[j],
                             new BinOpStr(typeof(bool), BinOpStrOrOr));
                }
            }

            /*
             * Pound through all the other combinations we support.
             */

            // boolean : somethingelse
            DefineBinOpsBoolX(bos, "bool");
            DefineBinOpsBoolX(bos, "char");
            DefineBinOpsBoolX(bos, "float");
            DefineBinOpsBoolX(bos, "integer");
            DefineBinOpsBoolX(bos, "key");
            DefineBinOpsBoolX(bos, "list");
            DefineBinOpsBoolX(bos, "string");

            // stuff with chars
            DefineBinOpsChar(bos);

            // somethingelse : boolean
            DefineBinOpsXBool(bos, "char");
            DefineBinOpsXBool(bos, "float");
            DefineBinOpsXBool(bos, "integer");
            DefineBinOpsXBool(bos, "key");
            DefineBinOpsXBool(bos, "list");
            DefineBinOpsXBool(bos, "string");

            // float : somethingelse
            DefineBinOpsFloatX(bos, "float");
            DefineBinOpsFloatX(bos, "integer");

            // integer : float
            DefineBinOpsXFloat(bos, "integer");

            // anything else with integers
            DefineBinOpsInteger(bos);

            // key : somethingelse
            DefineBinOpsKeyX(bos, "key");
            DefineBinOpsKeyX(bos, "string");

            // string : key
            DefineBinOpsXKey(bos, "string");

            // things with lists
            DefineBinOpsList(bos);

            // things with rotations
            DefineBinOpsRotation(bos);

            // things with strings
            DefineBinOpsString(bos);

            // things with vectors
            DefineBinOpsVector(bos);

            // Contrary to some beliefs, scripts do things like string+integer and integer+string
            bos.Add("bool+string", new BinOpStr(typeof(string), BinOpStrStrAddStr));
            bos.Add("char+string", new BinOpStr(typeof(string), BinOpStrStrAddStr));
            bos.Add("float+string", new BinOpStr(typeof(string), BinOpStrStrAddStr));
            bos.Add("integer+string", new BinOpStr(typeof(string), BinOpStrStrAddStr));
            bos.Add("string+bool", new BinOpStr(typeof(string), BinOpStrStrAddStr, true));
            bos.Add("string+char", new BinOpStr(typeof(string), BinOpStrStrAddStr, true));
            bos.Add("string+float", new BinOpStr(typeof(string), BinOpStrStrAddStr, true));
            bos.Add("string+integer", new BinOpStr(typeof(string), BinOpStrStrAddStr, true));

            // Now for our final slight-of-hand, we're going to scan through all those.
            // And wherever we see an 'integer' in the key, we are going to make another
            // entry with 'bool', as we want to accept a bool as having a value of 0 or 1.
            // This lets us do things like 3.5 * (x > 0).

            Dictionary<string, BinOpStr> bos2 = new Dictionary<string, BinOpStr>();
            foreach(KeyValuePair<string, BinOpStr> kvp in bos)
            {
                string key = kvp.Key;
                BinOpStr val = kvp.Value;
                bos2.Add(key, val);
            }
            Regex wordReg = new Regex("\\w+");
            Regex opReg = new Regex("\\W+");
            foreach(KeyValuePair<string, BinOpStr> kvp in bos)
            {
                string key = kvp.Key;
                BinOpStr val = kvp.Value;
                MatchCollection matches = wordReg.Matches(key);
                if(matches.Count != 2)
                    continue;
                Match opM = opReg.Match(key);
                if(!opM.Success)
                    continue;
                string left = matches[0].Value;
                string right = matches[1].Value;
                string op = opM.Value;
                string key2;
                if(left == "integer" && right == "integer")
                {
                    key2 = "bool" + op + "bool";
                    if(!bos2.ContainsKey(key2))
                        bos2.Add(key2, val);
                    key2 = "bool" + op + "integer";
                    if(!bos2.ContainsKey(key2))
                        bos2.Add(key2, val);
                    key2 = "integer" + op + "bool";
                    if(!bos2.ContainsKey(key2))
                        bos2.Add(key2, val);
                }
                else
                {
                    key2 = key.Replace("integer", "bool");
                    if(!bos2.ContainsKey(key2))
                        bos2.Add(key2, val);
                }
            }
            return bos2;
        }

        private static void DefineBinOpsBoolX(Dictionary<string, BinOpStr> bos, string x)
        {
            bos.Add("bool|" + x, new BinOpStr(typeof(int), BinOpStrBoolOrX));
            bos.Add("bool^" + x, new BinOpStr(typeof(int), BinOpStrBoolXorX));
            bos.Add("bool&" + x, new BinOpStr(typeof(int), BinOpStrBoolAndX));
            bos.Add("bool==" + x, new BinOpStr(typeof(bool), BinOpStrBoolEqX));
            bos.Add("bool!=" + x, new BinOpStr(typeof(bool), BinOpStrBoolNeX));
        }

        private static void DefineBinOpsXBool(Dictionary<string, BinOpStr> bos, string x)
        {
            bos.Add(x + "|bool", new BinOpStr(typeof(int), BinOpStrBoolOrX));
            bos.Add(x + "^bool", new BinOpStr(typeof(int), BinOpStrBoolXorX));
            bos.Add(x + "&bool", new BinOpStr(typeof(int), BinOpStrBoolAndX));
            bos.Add(x + "==bool", new BinOpStr(typeof(bool), BinOpStrBoolEqX));
            bos.Add(x + "!=bool", new BinOpStr(typeof(bool), BinOpStrBoolNeX));
        }

        private static void DefineBinOpsFloatX(Dictionary<string, BinOpStr> bos, string x)
        {
            bos.Add("float==" + x, new BinOpStr(typeof(bool), BinOpStrFloatEqX));
            bos.Add("float!=" + x, new BinOpStr(typeof(bool), BinOpStrFloatNeX));
            bos.Add("float<" + x, new BinOpStr(typeof(bool), BinOpStrFloatLtX));
            bos.Add("float<=" + x, new BinOpStr(typeof(bool), BinOpStrFloatLeX));
            bos.Add("float>" + x, new BinOpStr(typeof(bool), BinOpStrFloatGtX));
            bos.Add("float>=" + x, new BinOpStr(typeof(bool), BinOpStrFloatGeX));
            bos.Add("float+" + x, new BinOpStr(typeof(double), BinOpStrFloatAddX, true));
            bos.Add("float-" + x, new BinOpStr(typeof(double), BinOpStrFloatSubX, true));
            bos.Add("float*" + x, new BinOpStr(typeof(double), BinOpStrFloatMulX, true));
            bos.Add("float/" + x, new BinOpStr(typeof(double), BinOpStrFloatDivX, true));
            bos.Add("float%" + x, new BinOpStr(typeof(double), BinOpStrFloatModX, true));
        }

        private static void DefineBinOpsXFloat(Dictionary<string, BinOpStr> bos, string x)
        {
            bos.Add(x + "==float", new BinOpStr(typeof(bool), BinOpStrXEqFloat));
            bos.Add(x + "!=float", new BinOpStr(typeof(bool), BinOpStrXNeFloat));
            bos.Add(x + "<float", new BinOpStr(typeof(bool), BinOpStrXLtFloat));
            bos.Add(x + "<=float", new BinOpStr(typeof(bool), BinOpStrXLeFloat));
            bos.Add(x + ">float", new BinOpStr(typeof(bool), BinOpStrXGtFloat));
            bos.Add(x + ">=float", new BinOpStr(typeof(bool), BinOpStrXGeFloat));
            bos.Add(x + "+float", new BinOpStr(typeof(double), BinOpStrXAddFloat, true));
            bos.Add(x + "-float", new BinOpStr(typeof(double), BinOpStrXSubFloat, true));
            bos.Add(x + "*float", new BinOpStr(typeof(double), BinOpStrXMulFloat, true));
            bos.Add(x + "/float", new BinOpStr(typeof(double), BinOpStrXDivFloat, true));
            bos.Add(x + "%float", new BinOpStr(typeof(double), BinOpStrXModFloat, true));
        }

        private static void DefineBinOpsChar(Dictionary<string, BinOpStr> bos)
        {
            bos.Add("char==char", new BinOpStr(typeof(bool), BinOpStrCharEqChar));
            bos.Add("char!=char", new BinOpStr(typeof(bool), BinOpStrCharNeChar));
            bos.Add("char<char", new BinOpStr(typeof(bool), BinOpStrCharLtChar));
            bos.Add("char<=char", new BinOpStr(typeof(bool), BinOpStrCharLeChar));
            bos.Add("char>char", new BinOpStr(typeof(bool), BinOpStrCharGtChar));
            bos.Add("char>=char", new BinOpStr(typeof(bool), BinOpStrCharGeChar));
            bos.Add("char+integer", new BinOpStr(typeof(char), BinOpStrCharAddInt, true));
            bos.Add("char-integer", new BinOpStr(typeof(char), BinOpStrCharSubInt, true));
            bos.Add("char-char", new BinOpStr(typeof(int), BinOpStrCharSubChar));
        }

        private static void DefineBinOpsInteger(Dictionary<string, BinOpStr> bos)
        {
            bos.Add("integer==integer", new BinOpStr(typeof(bool), BinOpStrIntEqInt));
            bos.Add("integer!=integer", new BinOpStr(typeof(bool), BinOpStrIntNeInt));
            bos.Add("integer<integer", new BinOpStr(typeof(bool), BinOpStrIntLtInt));
            bos.Add("integer<=integer", new BinOpStr(typeof(bool), BinOpStrIntLeInt));
            bos.Add("integer>integer", new BinOpStr(typeof(bool), BinOpStrIntGtInt));
            bos.Add("integer>=integer", new BinOpStr(typeof(bool), BinOpStrIntGeInt));
            bos.Add("integer|integer", new BinOpStr(typeof(int), BinOpStrIntOrInt, true));
            bos.Add("integer^integer", new BinOpStr(typeof(int), BinOpStrIntXorInt, true));
            bos.Add("integer&integer", new BinOpStr(typeof(int), BinOpStrIntAndInt, true));
            bos.Add("integer+integer", new BinOpStr(typeof(int), BinOpStrIntAddInt, true));
            bos.Add("integer-integer", new BinOpStr(typeof(int), BinOpStrIntSubInt, true));
            bos.Add("integer*integer", new BinOpStr(typeof(int), BinOpStrIntMulInt, true));
            bos.Add("integer/integer", new BinOpStr(typeof(int), BinOpStrIntDivInt, true));
            bos.Add("integer%integer", new BinOpStr(typeof(int), BinOpStrIntModInt, true));
            bos.Add("integer<<integer", new BinOpStr(typeof(int), BinOpStrIntShlInt, true));
            bos.Add("integer>>integer", new BinOpStr(typeof(int), BinOpStrIntShrInt, true));
        }

        private static void DefineBinOpsKeyX(Dictionary<string, BinOpStr> bos, string x)
        {
            bos.Add("key==" + x, new BinOpStr(typeof(bool), BinOpStrKeyEqX));
            bos.Add("key!=" + x, new BinOpStr(typeof(bool), BinOpStrKeyNeX));
        }

        private static void DefineBinOpsXKey(Dictionary<string, BinOpStr> bos, string x)
        {
            bos.Add(x + "==key", new BinOpStr(typeof(bool), BinOpStrKeyEqX));
            bos.Add(x + "!=key", new BinOpStr(typeof(bool), BinOpStrKeyNeX));
        }

        private static void DefineBinOpsList(Dictionary<string, BinOpStr> bos)
        {
            bos.Add("list+float", new BinOpStr(typeof(LSL_List), BinOpStrListAddFloat, true));
            bos.Add("list+integer", new BinOpStr(typeof(LSL_List), BinOpStrListAddInt, true));
            bos.Add("list+key", new BinOpStr(typeof(LSL_List), BinOpStrListAddKey, true));
            bos.Add("list+list", new BinOpStr(typeof(LSL_List), BinOpStrListAddList, true));
            bos.Add("list+rotation", new BinOpStr(typeof(LSL_List), BinOpStrListAddRot, true));
            bos.Add("list+string", new BinOpStr(typeof(LSL_List), BinOpStrListAddStr, true));
            bos.Add("list+vector", new BinOpStr(typeof(LSL_List), BinOpStrListAddVec, true));

            bos.Add("float+list", new BinOpStr(typeof(LSL_List), BinOpStrFloatAddList));
            bos.Add("integer+list", new BinOpStr(typeof(LSL_List), BinOpStrIntAddList));
            bos.Add("key+list", new BinOpStr(typeof(LSL_List), BinOpStrKeyAddList));
            bos.Add("rotation+list", new BinOpStr(typeof(LSL_List), BinOpStrRotAddList));
            bos.Add("string+list", new BinOpStr(typeof(LSL_List), BinOpStrStrAddList));
            bos.Add("vector+list", new BinOpStr(typeof(LSL_List), BinOpStrVecAddList));

            bos.Add("list==list", new BinOpStr(typeof(bool), BinOpStrListEqList));
            bos.Add("list!=list", new BinOpStr(typeof(int), BinOpStrListNeList));
        }

        // all operations allowed by LSL_Rotation definition
        private static void DefineBinOpsRotation(Dictionary<string, BinOpStr> bos)
        {
            bos.Add("rotation==rotation", new BinOpStr(typeof(bool), BinOpStrRotEqRot));
            bos.Add("rotation!=rotation", new BinOpStr(typeof(bool), BinOpStrRotNeRot));
            bos.Add("rotation+rotation", new BinOpStr(typeof(LSL_Rotation), BinOpStrRotAddRot, true));
            bos.Add("rotation-rotation", new BinOpStr(typeof(LSL_Rotation), BinOpStrRotSubRot, true));
            bos.Add("rotation*rotation", new BinOpStr(typeof(LSL_Rotation), BinOpStrRotMulRot, true));
            bos.Add("rotation/rotation", new BinOpStr(typeof(LSL_Rotation), BinOpStrRotDivRot, true));
        }

        private static void DefineBinOpsString(Dictionary<string, BinOpStr> bos)
        {
            bos.Add("string==string", new BinOpStr(typeof(bool), BinOpStrStrEqStr));
            bos.Add("string!=string", new BinOpStr(typeof(bool), BinOpStrStrNeStr));
            bos.Add("string<string", new BinOpStr(typeof(bool), BinOpStrStrLtStr));
            bos.Add("string<=string", new BinOpStr(typeof(bool), BinOpStrStrLeStr));
            bos.Add("string>string", new BinOpStr(typeof(bool), BinOpStrStrGtStr));
            bos.Add("string>=string", new BinOpStr(typeof(bool), BinOpStrStrGeStr));
            bos.Add("string+string", new BinOpStr(typeof(string), BinOpStrStrAddStr, true));
        }

        // all operations allowed by LSL_Vector definition
        private static void DefineBinOpsVector(Dictionary<string, BinOpStr> bos)
        {
            bos.Add("vector==vector", new BinOpStr(typeof(bool), BinOpStrVecEqVec));
            bos.Add("vector!=vector", new BinOpStr(typeof(bool), BinOpStrVecNeVec));
            bos.Add("vector+vector", new BinOpStr(typeof(LSL_Vector), BinOpStrVecAddVec, true));
            bos.Add("vector-vector", new BinOpStr(typeof(LSL_Vector), BinOpStrVecSubVec, true));
            bos.Add("vector*vector", new BinOpStr(typeof(double), BinOpStrVecMulVec));
            bos.Add("vector%vector", new BinOpStr(typeof(LSL_Vector), BinOpStrVecModVec, true));

            bos.Add("vector*float", new BinOpStr(typeof(LSL_Vector), BinOpStrVecMulFloat, true));
            bos.Add("float*vector", new BinOpStr(typeof(LSL_Vector), BinOpStrFloatMulVec));
            bos.Add("vector/float", new BinOpStr(typeof(LSL_Vector), BinOpStrVecDivFloat, true));

            bos.Add("vector*integer", new BinOpStr(typeof(LSL_Vector), BinOpStrVecMulInt, true));
            bos.Add("integer*vector", new BinOpStr(typeof(LSL_Vector), BinOpStrIntMulVec));
            bos.Add("vector/integer", new BinOpStr(typeof(LSL_Vector), BinOpStrVecDivInt, true));

            bos.Add("vector*rotation", new BinOpStr(typeof(LSL_Vector), BinOpStrVecMulRot, true));
            bos.Add("vector/rotation", new BinOpStr(typeof(LSL_Vector), BinOpStrVecDivRot, true));
        }

        /**
         * @brief These methods actually emit the code to perform the arithmetic.
         * @param scg    = what script we are compiling
         * @param left   = left-hand operand location in memory (type as given by BinOpStr entry)
         * @param right  = right-hand operand location in memory (type as given by BinOpStr entry)
         * @param result = result location in memory (type as given by BinOpStr entry)
         */
        private static void BinOpStrAndAnd(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeBool);
            right.PushVal(scg, errorAt, tokenTypeBool);
            scg.ilGen.Emit(errorAt, OpCodes.And);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrOrOr(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeBool);
            right.PushVal(scg, errorAt, tokenTypeBool);
            scg.ilGen.Emit(errorAt, OpCodes.Or);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrBoolOrX(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeInt);
            right.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.Or);
            result.PopPost(scg, errorAt, tokenTypeInt);
        }

        private static void BinOpStrBoolXorX(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeInt);
            right.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.Xor);
            result.PopPost(scg, errorAt, tokenTypeInt);
        }

        private static void BinOpStrBoolAndX(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeInt);
            right.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.And);
            result.PopPost(scg, errorAt, tokenTypeInt);
        }

        private static void BinOpStrBoolEqX(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeBool);
            right.PushVal(scg, errorAt, tokenTypeBool);
            scg.ilGen.Emit(errorAt, OpCodes.Ceq);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrBoolNeX(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeBool);
            right.PushVal(scg, errorAt, tokenTypeBool);
            scg.ilGen.Emit(errorAt, OpCodes.Xor);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrFloatEqX(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeFloat);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            scg.ilGen.Emit(errorAt, OpCodes.Ceq);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrFloatNeX(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeFloat);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            scg.ilGen.Emit(errorAt, OpCodes.Ceq);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_1);
            scg.ilGen.Emit(errorAt, OpCodes.Xor);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrFloatLtX(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeFloat);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            scg.ilGen.Emit(errorAt, OpCodes.Clt);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrFloatLeX(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeFloat);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            scg.ilGen.Emit(errorAt, OpCodes.Cgt);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_1);
            scg.ilGen.Emit(errorAt, OpCodes.Xor);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrFloatGtX(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeFloat);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            scg.ilGen.Emit(errorAt, OpCodes.Cgt);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrFloatGeX(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeFloat);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            scg.ilGen.Emit(errorAt, OpCodes.Clt);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_1);
            scg.ilGen.Emit(errorAt, OpCodes.Xor);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrFloatAddX(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeFloat);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            scg.ilGen.Emit(errorAt, OpCodes.Add);
            result.PopPost(scg, errorAt, tokenTypeFloat);
        }

        private static void BinOpStrFloatSubX(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeFloat);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            scg.ilGen.Emit(errorAt, OpCodes.Sub);
            result.PopPost(scg, errorAt, tokenTypeFloat);
        }

        private static void BinOpStrFloatMulX(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeFloat);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            scg.ilGen.Emit(errorAt, OpCodes.Mul);
            result.PopPost(scg, errorAt, tokenTypeFloat);
        }

        private static void BinOpStrFloatDivX(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeFloat);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            //scg.ilGen.Emit(errorAt, OpCodes.Div);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethDoubleDivDouble);
            result.PopPost(scg, errorAt, tokenTypeFloat);
        }

        private static void BinOpStrFloatModX(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeFloat);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            //scg.ilGen.Emit(errorAt, OpCodes.Rem);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethDoubleModDouble);
            result.PopPost(scg, errorAt, tokenTypeFloat);
        }

        private static void BinOpStrXEqFloat(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeFloat);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            scg.ilGen.Emit(errorAt, OpCodes.Ceq);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrXNeFloat(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeFloat);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            scg.ilGen.Emit(errorAt, OpCodes.Ceq);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_1);
            scg.ilGen.Emit(errorAt, OpCodes.Xor);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrXLtFloat(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeFloat);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            scg.ilGen.Emit(errorAt, OpCodes.Clt);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrXLeFloat(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeFloat);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            scg.ilGen.Emit(errorAt, OpCodes.Cgt);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_1);
            scg.ilGen.Emit(errorAt, OpCodes.Xor);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrXGtFloat(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeFloat);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            scg.ilGen.Emit(errorAt, OpCodes.Cgt);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrXGeFloat(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeFloat);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            scg.ilGen.Emit(errorAt, OpCodes.Clt);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_1);
            scg.ilGen.Emit(errorAt, OpCodes.Xor);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrXAddFloat(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeFloat);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            scg.ilGen.Emit(errorAt, OpCodes.Add);
            result.PopPost(scg, errorAt, tokenTypeFloat);
        }

        private static void BinOpStrXSubFloat(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeFloat);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            scg.ilGen.Emit(errorAt, OpCodes.Sub);
            result.PopPost(scg, errorAt, tokenTypeFloat);
        }

        private static void BinOpStrXMulFloat(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeFloat);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            scg.ilGen.Emit(errorAt, OpCodes.Mul);
            result.PopPost(scg, errorAt, tokenTypeFloat);
        }

        private static void BinOpStrXDivFloat(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeFloat);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            //scg.ilGen.Emit(errorAt, OpCodes.Div);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethDoubleDivDouble);
            result.PopPost(scg, errorAt, tokenTypeFloat);
        }

        private static void BinOpStrXModFloat(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeFloat);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            //scg.ilGen.Emit(errorAt, OpCodes.Rem);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethDoubleModDouble);
            result.PopPost(scg, errorAt, tokenTypeFloat);
        }

        private static void BinOpStrCharEqChar(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeChar);
            right.PushVal(scg, errorAt, tokenTypeChar);
            scg.ilGen.Emit(errorAt, OpCodes.Ceq);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrCharNeChar(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeChar);
            right.PushVal(scg, errorAt, tokenTypeChar);
            scg.ilGen.Emit(errorAt, OpCodes.Ceq);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_1);
            scg.ilGen.Emit(errorAt, OpCodes.Xor);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrCharLtChar(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeChar);
            right.PushVal(scg, errorAt, tokenTypeChar);
            scg.ilGen.Emit(errorAt, OpCodes.Clt);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrCharLeChar(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeChar);
            right.PushVal(scg, errorAt, tokenTypeChar);
            scg.ilGen.Emit(errorAt, OpCodes.Cgt);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_1);
            scg.ilGen.Emit(errorAt, OpCodes.Xor);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrCharGtChar(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeChar);
            right.PushVal(scg, errorAt, tokenTypeChar);
            scg.ilGen.Emit(errorAt, OpCodes.Cgt);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrCharGeChar(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeChar);
            right.PushVal(scg, errorAt, tokenTypeChar);
            scg.ilGen.Emit(errorAt, OpCodes.Clt);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_1);
            scg.ilGen.Emit(errorAt, OpCodes.Xor);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrCharAddInt(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeChar);
            right.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.Add);
            result.PopPost(scg, errorAt, tokenTypeChar);
        }

        private static void BinOpStrCharSubInt(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeChar);
            right.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.Sub);
            result.PopPost(scg, errorAt, tokenTypeChar);
        }

        private static void BinOpStrCharSubChar(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeChar);
            right.PushVal(scg, errorAt, tokenTypeChar);
            scg.ilGen.Emit(errorAt, OpCodes.Sub);
            result.PopPost(scg, errorAt, tokenTypeInt);
        }

        private static void BinOpStrIntEqInt(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeInt);
            right.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.Ceq);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrIntNeInt(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeInt);
            right.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.Ceq);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_1);
            scg.ilGen.Emit(errorAt, OpCodes.Xor);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrIntLtInt(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeInt);
            right.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.Clt);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrIntLeInt(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeInt);
            right.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.Cgt);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_1);
            scg.ilGen.Emit(errorAt, OpCodes.Xor);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrIntGtInt(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeInt);
            right.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.Cgt);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrIntGeInt(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeInt);
            right.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.Clt);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_1);
            scg.ilGen.Emit(errorAt, OpCodes.Xor);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrIntOrInt(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeInt);
            right.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.Or);
            result.PopPost(scg, errorAt, tokenTypeInt);
        }

        private static void BinOpStrIntXorInt(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeInt);
            right.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.Xor);
            result.PopPost(scg, errorAt, tokenTypeInt);
        }

        private static void BinOpStrIntAndInt(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeInt);
            right.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.And);
            result.PopPost(scg, errorAt, tokenTypeInt);
        }

        private static void BinOpStrIntAddInt(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeInt);
            right.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.Add);
            result.PopPost(scg, errorAt, tokenTypeInt);
        }

        private static void BinOpStrIntSubInt(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeInt);
            right.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.Sub);
            result.PopPost(scg, errorAt, tokenTypeInt);
        }

        private static void BinOpStrIntMulInt(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeInt);
            right.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.Mul);
            result.PopPost(scg, errorAt, tokenTypeInt);
        }

        private static void BinOpStrIntDivInt(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            // note that we must allow 0x800000/-1 -> 0x80000000 for lslangtest1.lsl
            // so sign-extend the operands to 64-bit then divide and truncate result
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.Conv_I8);
            right.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.Conv_I8);
            //scg.ilGen.Emit(errorAt, OpCodes.Div);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethLongDivLong);
            scg.ilGen.Emit(errorAt, OpCodes.Conv_I4);
            result.PopPost(scg, errorAt, tokenTypeInt);
        }

        private static void BinOpStrIntModInt(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            // note that we must allow 0x800000%-1 -> 0 for lslangtest1.lsl
            // so sign-extend the operands to 64-bit then mod and truncate result
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.Conv_I8);
            right.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.Conv_I8);
            //scg.ilGen.Emit(errorAt, OpCodes.Rem);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethLongModLong);
            scg.ilGen.Emit(errorAt, OpCodes.Conv_I4);
            result.PopPost(scg, errorAt, tokenTypeInt);
        }

        private static void BinOpStrIntShlInt(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeInt);
            right.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.Shl);
            result.PopPost(scg, errorAt, tokenTypeInt);
        }

        private static void BinOpStrIntShrInt(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeInt);
            right.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.Shr);
            result.PopPost(scg, errorAt, tokenTypeInt);
        }

        private static void BinOpStrKeyEqX(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeStr);
            right.PushVal(scg, errorAt, tokenTypeStr);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4, (int)StringComparison.Ordinal);
            scg.ilGen.Emit(errorAt, OpCodes.Call, stringCmpStringMethInfo);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_0);
            scg.ilGen.Emit(errorAt, OpCodes.Ceq);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrKeyNeX(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeStr);
            right.PushVal(scg, errorAt, tokenTypeStr);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4, (int)StringComparison.Ordinal);
            scg.ilGen.Emit(errorAt, OpCodes.Call, stringCmpStringMethInfo);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_0);
            scg.ilGen.Emit(errorAt, OpCodes.Ceq);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_1);
            scg.ilGen.Emit(errorAt, OpCodes.Xor);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrListAddFloat(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeList);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethListAddFloat);
            result.PopPost(scg, errorAt, tokenTypeList);
        }

        private static void BinOpStrListAddInt(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeList);
            right.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethListAddInt);
            result.PopPost(scg, errorAt, tokenTypeList);
        }

        private static void BinOpStrListAddKey(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeList);
            right.PushVal(scg, errorAt, tokenTypeStr);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethListAddKey);
            result.PopPost(scg, errorAt, tokenTypeList);
        }

        private static void BinOpStrListAddList(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeList);
            right.PushVal(scg, errorAt, tokenTypeList);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethListAddList);
            result.PopPost(scg, errorAt, tokenTypeList);
        }

        private static void BinOpStrListAddRot(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeList);
            right.PushVal(scg, errorAt, tokenTypeRot);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethListAddRot);
            result.PopPost(scg, errorAt, tokenTypeList);
        }

        private static void BinOpStrListAddStr(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeList);
            right.PushVal(scg, errorAt, tokenTypeStr);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethListAddStr);
            result.PopPost(scg, errorAt, tokenTypeList);
        }

        private static void BinOpStrListAddVec(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeList);
            right.PushVal(scg, errorAt, tokenTypeVec);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethListAddVec);
            result.PopPost(scg, errorAt, tokenTypeList);
        }

        private static void BinOpStrFloatAddList(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeFloat);
            right.PushVal(scg, errorAt, tokenTypeList);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethFloatAddList);
            result.PopPost(scg, errorAt, tokenTypeList);
        }

        private static void BinOpStrIntAddList(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeInt);
            right.PushVal(scg, errorAt, tokenTypeList);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethIntAddList);
            result.PopPost(scg, errorAt, tokenTypeList);
        }

        private static void BinOpStrKeyAddList(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeStr);
            right.PushVal(scg, errorAt, tokenTypeList);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethKeyAddList);
            result.PopPost(scg, errorAt, tokenTypeList);
        }

        private static void BinOpStrRotAddList(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeRot);
            right.PushVal(scg, errorAt, tokenTypeList);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethRotAddList);
            result.PopPost(scg, errorAt, tokenTypeList);
        }

        private static void BinOpStrStrAddList(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeStr);
            right.PushVal(scg, errorAt, tokenTypeList);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethStrAddList);
            result.PopPost(scg, errorAt, tokenTypeList);
        }

        private static void BinOpStrVecAddList(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeVec);
            right.PushVal(scg, errorAt, tokenTypeList);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethVecAddList);
            result.PopPost(scg, errorAt, tokenTypeList);
        }

        private static void BinOpStrListEqList(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeList);
            right.PushVal(scg, errorAt, tokenTypeList);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethListEqList);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrListNeList(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeList);
            right.PushVal(scg, errorAt, tokenTypeList);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethListNeList);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrRotEqRot(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeRot);
            right.PushVal(scg, errorAt, tokenTypeRot);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethRotEqRot);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrRotNeRot(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeRot);
            right.PushVal(scg, errorAt, tokenTypeRot);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethRotNeRot);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrRotAddRot(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeRot);
            right.PushVal(scg, errorAt, tokenTypeRot);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethRotAddRot);
            result.PopPost(scg, errorAt, tokenTypeRot);
        }

        private static void BinOpStrRotSubRot(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeRot);
            right.PushVal(scg, errorAt, tokenTypeRot);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethRotSubRot);
            result.PopPost(scg, errorAt, tokenTypeRot);
        }

        private static void BinOpStrRotMulRot(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeRot);
            right.PushVal(scg, errorAt, tokenTypeRot);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethRotMulRot);
            result.PopPost(scg, errorAt, tokenTypeRot);
        }

        private static void BinOpStrRotDivRot(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeRot);
            right.PushVal(scg, errorAt, tokenTypeRot);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethRotDivRot);
            result.PopPost(scg, errorAt, tokenTypeRot);
        }

        private static void BinOpStrStrEqStr(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeStr);
            right.PushVal(scg, errorAt, tokenTypeStr);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4, (int)StringComparison.Ordinal);
            scg.ilGen.Emit(errorAt, OpCodes.Call, stringCmpStringMethInfo);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_0);
            scg.ilGen.Emit(errorAt, OpCodes.Ceq);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrStrNeStr(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeStr);
            right.PushVal(scg, errorAt, tokenTypeStr);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4, (int)StringComparison.Ordinal);
            scg.ilGen.Emit(errorAt, OpCodes.Call, stringCmpStringMethInfo);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_0);
            scg.ilGen.Emit(errorAt, OpCodes.Ceq);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_1);
            scg.ilGen.Emit(errorAt, OpCodes.Xor);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrStrLtStr(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeStr);
            right.PushVal(scg, errorAt, tokenTypeStr);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4, (int)StringComparison.Ordinal);
            scg.ilGen.Emit(errorAt, OpCodes.Call, stringCmpStringMethInfo);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_0);
            scg.ilGen.Emit(errorAt, OpCodes.Clt);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrStrLeStr(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeStr);
            right.PushVal(scg, errorAt, tokenTypeStr);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4, (int)StringComparison.Ordinal);
            scg.ilGen.Emit(errorAt, OpCodes.Call, stringCmpStringMethInfo);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_1);
            scg.ilGen.Emit(errorAt, OpCodes.Clt);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrStrGtStr(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeStr);
            right.PushVal(scg, errorAt, tokenTypeStr);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4, (int)StringComparison.Ordinal);
            scg.ilGen.Emit(errorAt, OpCodes.Call, stringCmpStringMethInfo);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_0);
            scg.ilGen.Emit(errorAt, OpCodes.Cgt);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrStrGeStr(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeStr);
            right.PushVal(scg, errorAt, tokenTypeStr);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4, (int)StringComparison.Ordinal);
            scg.ilGen.Emit(errorAt, OpCodes.Call, stringCmpStringMethInfo);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_M1);
            scg.ilGen.Emit(errorAt, OpCodes.Cgt);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        // Called by many type combinations so both operands need to be cast to strings
        private static void BinOpStrStrAddStr(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeStr);
            right.PushVal(scg, errorAt, tokenTypeStr);
            scg.ilGen.Emit(errorAt, OpCodes.Call, stringAddStringMethInfo);
            result.PopPost(scg, errorAt, tokenTypeStr);
        }

        private static void BinOpStrVecEqVec(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeVec);
            right.PushVal(scg, errorAt, tokenTypeVec);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethVecEqVec);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrVecNeVec(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeVec);
            right.PushVal(scg, errorAt, tokenTypeVec);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethVecNeVec);
            result.PopPost(scg, errorAt, tokenTypeBool);
        }

        private static void BinOpStrVecAddVec(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeVec);
            right.PushVal(scg, errorAt, tokenTypeVec);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethVecAddVec);
            result.PopPost(scg, errorAt, tokenTypeVec);
        }

        private static void BinOpStrVecSubVec(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeVec);
            right.PushVal(scg, errorAt, tokenTypeVec);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethVecSubVec);
            result.PopPost(scg, errorAt, tokenTypeVec);
        }

        private static void BinOpStrVecMulVec(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeVec);
            right.PushVal(scg, errorAt, tokenTypeVec);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethVecMulVec);
            result.PopPost(scg, errorAt, tokenTypeFloat);
        }

        private static void BinOpStrVecModVec(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeVec);
            right.PushVal(scg, errorAt, tokenTypeVec);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethVecModVec);
            result.PopPost(scg, errorAt, tokenTypeVec);
        }

        private static void BinOpStrVecMulFloat(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeVec);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethVecMulFloat);
            result.PopPost(scg, errorAt, tokenTypeVec);
        }

        private static void BinOpStrFloatMulVec(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeFloat);
            right.PushVal(scg, errorAt, tokenTypeVec);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethFloatMulVec);
            result.PopPost(scg, errorAt, tokenTypeVec);
        }

        private static void BinOpStrVecDivFloat(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeVec);
            right.PushVal(scg, errorAt, tokenTypeFloat);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethVecDivFloat);
            result.PopPost(scg, errorAt, tokenTypeVec);
        }

        private static void BinOpStrVecMulInt(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeVec);
            right.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethVecMulInt);
            result.PopPost(scg, errorAt, tokenTypeVec);
        }

        private static void BinOpStrIntMulVec(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeInt);
            right.PushVal(scg, errorAt, tokenTypeVec);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethIntMulVec);
            result.PopPost(scg, errorAt, tokenTypeVec);
        }

        private static void BinOpStrVecDivInt(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeVec);
            right.PushVal(scg, errorAt, tokenTypeInt);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethVecDivInt);
            result.PopPost(scg, errorAt, tokenTypeVec);
        }

        private static void BinOpStrVecMulRot(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeVec);
            right.PushVal(scg, errorAt, tokenTypeRot);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethVecMulRot);
            result.PopPost(scg, errorAt, tokenTypeVec);
        }

        private static void BinOpStrVecDivRot(ScriptCodeGen scg, Token errorAt, CompValu left, CompValu right, CompValu result)
        {
            result.PopPre(scg, errorAt);
            left.PushVal(scg, errorAt, tokenTypeVec);
            right.PushVal(scg, errorAt, tokenTypeRot);
            scg.ilGen.Emit(errorAt, OpCodes.Call, infoMethVecDivRot);
            result.PopPost(scg, errorAt, tokenTypeVec);
        }

        /**
         * @brief These methods are called at runtime as helpers.
         *        Needed to pick up functionality defined by overloaded operators of LSL_ types.
         *        They need to be marked public or runtime says they are inaccessible.
         */
        public static LSL_List MethListAddFloat(LSL_List left, double right)
        {
            return MethListAddObj(left, new LSL_Float(right));
        }
        public static LSL_List MethListAddInt(LSL_List left, int right)
        {
            return MethListAddObj(left, new LSL_Integer(right));
        }
        public static LSL_List MethListAddKey(LSL_List left, string right)
        {
            return MethListAddObj(left, new LSL_Key(right));
        }
        public static LSL_List MethListAddRot(LSL_List left, LSL_Rotation right)
        {
            return MethListAddObj(left, right);
        }
        public static LSL_List MethListAddStr(LSL_List left, string right)
        {
            return MethListAddObj(left, new LSL_String(right));
        }
        public static LSL_List MethListAddVec(LSL_List left, LSL_Vector right)
        {
            return MethListAddObj(left, right);
        }
        public static LSL_List MethListAddObj(LSL_List left, object right)
        {
            int oldlen = left.Length;
            object[] newarr = new object[oldlen + 1];
            Array.Copy(left.Data, newarr, oldlen);
            newarr[oldlen] = right;
            return new LSL_List(newarr);
        }

        public static LSL_List MethListAddList(LSL_List left, LSL_List right)
        {
            int leftlen = left.Length;
            int ritelen = right.Length;
            object[] newarr = new object[leftlen + ritelen];
            Array.Copy(left.Data, newarr, leftlen);
            Array.Copy(right.Data, 0, newarr, leftlen, ritelen);
            return new LSL_List(newarr);
        }

        public static LSL_List MethFloatAddList(double left, LSL_List right)
        {
            return MethObjAddList(new LSL_Float(left), right);
        }
        public static LSL_List MethIntAddList(int left, LSL_List right)
        {
            return MethObjAddList(new LSL_Integer(left), right);
        }
        public static LSL_List MethKeyAddList(string left, LSL_List right)
        {
            return MethObjAddList(new LSL_Key(left), right);
        }
        public static LSL_List MethRotAddList(LSL_Rotation left, LSL_List right)
        {
            return MethObjAddList(left, right);
        }
        public static LSL_List MethStrAddList(string left, LSL_List right)
        {
            return MethObjAddList(new LSL_String(left), right);
        }
        public static LSL_List MethVecAddList(LSL_Vector left, LSL_List right)
        {
            return MethObjAddList(left, right);
        }
        public static LSL_List MethObjAddList(object left, LSL_List right)
        {
            int oldlen = right.Length;
            object[] newarr = new object[oldlen + 1];
            newarr[0] = left;
            Array.Copy(right.Data, 0, newarr, 1, oldlen);
            return new LSL_List(newarr);
        }

        public static bool MethListEqList(LSL_List left, LSL_List right)
        {
            return left == right;
        }

        // According to http://wiki.secondlife.com/wiki/LlGetListLength
        // jackassed LSL allows 'somelist != []' to get the length of a list
        public static int MethListNeList(LSL_List left, LSL_List right)
        {
            int leftlen = left.Length;
            int ritelen = right.Length;
            return leftlen - ritelen;
        }

        public static bool MethRotEqRot(LSL_Rotation left, LSL_Rotation right)
        {
            return left == right;
        }

        public static bool MethRotNeRot(LSL_Rotation left, LSL_Rotation right)
        {
            return left != right;
        }

        public static LSL_Rotation MethRotAddRot(LSL_Rotation left, LSL_Rotation right)
        {
            return left + right;
        }

        public static LSL_Rotation MethRotSubRot(LSL_Rotation left, LSL_Rotation right)
        {
            return left - right;
        }

        public static LSL_Rotation MethRotMulRot(LSL_Rotation left, LSL_Rotation right)
        {
            return left * right;
        }

        public static LSL_Rotation MethRotDivRot(LSL_Rotation left, LSL_Rotation right)
        {
            return left / right;
        }

        public static bool MethVecEqVec(LSL_Vector left, LSL_Vector right)
        {
            return left == right;
        }

        public static bool MethVecNeVec(LSL_Vector left, LSL_Vector right)
        {
            return left != right;
        }

        public static LSL_Vector MethVecAddVec(LSL_Vector left, LSL_Vector right)
        {
            return left + right;
        }

        public static LSL_Vector MethVecSubVec(LSL_Vector left, LSL_Vector right)
        {
            return left - right;
        }

        public static double MethVecMulVec(LSL_Vector left, LSL_Vector right)
        {
            return (double)(left * right).value;
        }

        public static LSL_Vector MethVecModVec(LSL_Vector left, LSL_Vector right)
        {
            return left % right;
        }

        public static LSL_Vector MethVecMulFloat(LSL_Vector left, double right)
        {
            return left * right;
        }

        public static LSL_Vector MethFloatMulVec(double left, LSL_Vector right)
        {
            return left * right;
        }

        public static LSL_Vector MethVecDivFloat(LSL_Vector left, double right)
        {
            return left / right;
        }

        public static LSL_Vector MethVecMulInt(LSL_Vector left, int right)
        {
            return left * right;
        }

        public static LSL_Vector MethIntMulVec(int left, LSL_Vector right)
        {
            return left * right;
        }

        public static LSL_Vector MethVecDivInt(LSL_Vector left, int right)
        {
            return left / right;
        }

        public static LSL_Vector MethVecMulRot(LSL_Vector left, LSL_Rotation right)
        {
            return left * right;
        }

        public static LSL_Vector MethVecDivRot(LSL_Vector left, LSL_Rotation right)
        {
            return left / right;
        }

        public static double MethDoubleDivDouble(double a, double b)
        {
            double r = a / b;
            if (double.IsNaN(r) || double.IsInfinity(r))
                throw new ScriptException("Division by Zero");
            return r;
        }

        public static long MethLongDivLong(long a, long b)
        {
            try
            {
                return a / b;
            }
            catch (DivideByZeroException)
            {
                throw new ScriptException("Division by Zero");
            }
        }

        public static double MethDoubleModDouble(double a, double b)
        {
            double r = a % b;
            if (double.IsNaN(r) || double.IsInfinity(r))
                throw new ScriptException("Division by Zero");
            return r;
        }

        public static long MethLongModLong(long a, long b)
        {
            try
            {
                return a % b;
            }
            catch (DivideByZeroException)
            {
                throw new ScriptException("Division by Zero");
            }
        }
    }
}
