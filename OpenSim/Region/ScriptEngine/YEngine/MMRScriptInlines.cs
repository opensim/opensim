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
using System.Reflection.Emit;
using System.Text;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

/**
 * @brief Generate code for the backend API calls.
 */
namespace OpenSim.Region.ScriptEngine.Yengine
{
    public abstract class TokenDeclInline: TokenDeclVar
    {
        public static VarDict inlineFunctions = CreateDictionary();

        public abstract void CodeGen(ScriptCodeGen scg, Token errorAt, CompValuTemp result, CompValu[] args);

        private static string[] noCheckRuns;
        private static string[] keyReturns;

        protected bool isTaggedCallsCheckRun;

        /**
         * @brief Create a dictionary of inline backend API functions.
         */
        private static VarDict CreateDictionary()
        {
            /*
             * For those listed in noCheckRun, we just generate the call (simple computations).
             * For all others, we generate the call then a call to CheckRun().
             */
            noCheckRuns = new string[] {
                "llBase64ToString",
                "llCSV2List",
                "llDeleteSubList",
                "llDeleteSubString",
                "llDumpList2String",
                "llEscapeURL",
                "llEuler2Rot",
                "llGetListEntryType",
                "llGetListLength",
                "llGetSubString",
                "llGetUnixTime",
                "llInsertString",
                "llList2CSV",
                "llList2Float",
                "llList2Integer",
                "llList2Key",
                "llList2List",
                "llList2ListStrided",
                "llList2Rot",
                "llList2String",
                "llList2Vector",
                "llListFindList",
                "llListInsertList",
                "llListRandomize",
                "llListReplaceList",
                "llListSort",
                "llListStatistics",
                "llMD5String",
                "llParseString2List",
                "llParseStringKeepNulls",
                "llRot2Euler",
                "llStringLength",
                "llStringToBase64",
                "llStringTrim",
                "llSubStringIndex",
                "llUnescapeURL"
            };

            /*
             * These functions really return a 'key' even though we see them as
             * returning 'string' because OpenSim has key and string as same type.
             */
            keyReturns = new string[] {
                "llAvatarOnLinkSitTarget",
                "llAvatarOnSitTarget",
                "llDetectedKey",
                "llDetectedOwner",
                "llGenerateKey",
                "llGetCreator",
                "llGetInventoryCreator",
                "llGetInventoryKey",
                "llGetKey",
                "llGetLandOwnerAt",
                "llGetLinkKey",
                "llGetNotecardLine",
                "llGetNumberOfNotecardLines",
                "llGetOwner",
                "llGetOwnerKey",
                "llGetPermissionsKey",
                "llHTTPRequest",
                "llList2Key",
                "llRequestAgentData",
                "llRequestDisplayName",
                "llRequestInventoryData",
                "llRequestSecureURL",
                "llRequestSimulatorData",
                "llRequestURL",
                "llRequestUsername",
                "llSendRemoteData",
                "llTransferLindenDollars"
            };

            VarDict ifd = new VarDict(false);
            Type[] oneDoub = new Type[] { typeof(double) };
            Type[] twoDoubs = new Type[] { typeof(double), typeof(double) };

            /*
             * Mono generates an FPU instruction for many math calls.
             */

            new TokenDeclInline_LLAbs(ifd);
            new TokenDeclInline_Math(ifd, "llAcos(float)", "Acos", oneDoub);
            new TokenDeclInline_Math(ifd, "llAsin(float)", "Asin", oneDoub);
            new TokenDeclInline_Math(ifd, "llAtan2(float,float)", "Atan2", twoDoubs);
            new TokenDeclInline_Math(ifd, "llCos(float)", "Cos", oneDoub);
            new TokenDeclInline_Math(ifd, "llFabs(float)", "Abs", oneDoub);
            new TokenDeclInline_Math(ifd, "llLog(float)", "Log", oneDoub);
            new TokenDeclInline_Math(ifd, "llLog10(float)", "Log10", oneDoub);
            new TokenDeclInline_Math(ifd, "llPow(float,float)", "Pow", twoDoubs);
            new TokenDeclInline_LLRound(ifd);
            new TokenDeclInline_Math(ifd, "llSin(float)", "Sin", oneDoub);
            new TokenDeclInline_Math(ifd, "llSqrt(float)", "Sqrt", oneDoub);
            new TokenDeclInline_Math(ifd, "llTan(float)", "Tan", oneDoub);

            /*
             * Something weird about the code generation for these calls, so they all have their own handwritten code generators.
             */
            new TokenDeclInline_GetFreeMemory(ifd);
            new TokenDeclInline_GetUsedMemory(ifd);

            /*
             * These are all the xmr...() calls directly in XMRInstAbstract.
             * Includes the calls from ScriptBaseClass that has all the stubs
             * which convert XMRInstAbstract to the various <NAME>_Api contexts.
             */
            MethodInfo[] absmeths = typeof(XMRInstAbstract).GetMethods();
            AddInterfaceMethods(ifd, absmeths, null);

            return ifd;
        }

        /**
         * @brief Add API functions from the given interface to list of built-in functions.
         *        Only functions beginning with a lower-case letter are entered, all others ignored.
         * @param ifd = internal function dictionary to add them to
         * @param ifaceMethods = list of API functions
         * @param acf = which field in XMRInstanceSuperType holds method's 'this' pointer
         */
        // this one accepts only names beginning with a lower-case letter
        public static void AddInterfaceMethods(VarDict ifd, MethodInfo[] ifaceMethods, FieldInfo acf)
        {
            List<MethodInfo> lcms = new List<MethodInfo>(ifaceMethods.Length);
            foreach(MethodInfo meth in ifaceMethods)
            {
                string name = meth.Name;
                if((name[0] >= 'a') && (name[0] <= 'z'))
                {
                    lcms.Add(meth);
                }
            }
            AddInterfaceMethods(ifd, lcms.GetEnumerator(), acf);
        }

        // this one accepts all methods given to it
        public static void AddInterfaceMethods(VarDict ifd, IEnumerator<MethodInfo> ifaceMethods, FieldInfo acf)
        {
            if(ifd == null)
                ifd = inlineFunctions;

            for(ifaceMethods.Reset(); ifaceMethods.MoveNext();)
            {
                MethodInfo ifaceMethod = ifaceMethods.Current;
                string key = ifaceMethod.Name;

                try
                {
                    /*
                     * See if we will generate a call to CheckRun() right 
                     * after we generate a call to the function.
                     * If function begins with xmr, assume we will not call CheckRun()
                     * Otherwise, assume we will call CheckRun()
                     */
                    bool dcr = !key.StartsWith("xmr");
                    foreach(string ncr in noCheckRuns)
                    {
                        if(ncr == key)
                        {
                            dcr = false;
                            break;
                        }
                    }

                    /*
                     * Add function to dictionary.
                     */
                    new TokenDeclInline_BEApi(ifd, dcr, ifaceMethod, acf);
                }
                catch
                {
                    ///??? IGNORE ANY THAT FAIL - LIKE UNRECOGNIZED TYPE ???///
                    ///???                          and OVERLOADED NAMES ???///
                }
            }
        }

        /**
         * @brief Add an inline function definition to the dictionary.
         * @param ifd        = dictionary to add inline definition to
         * @param doCheckRun = true iff the generated code or the function itself can possibly call CheckRun()
         * @param nameArgSig = inline function signature string, in form <name>(<arglsltypes>,...)
         * @param retType    = return type, use TokenTypeVoid if no return value
         */
        protected TokenDeclInline(VarDict ifd,
                                   bool doCheckRun,
                                   string nameArgSig,
                                   TokenType retType)
                : base(null, null, null)
        {
            this.retType = retType;
            this.triviality = doCheckRun ? Triviality.complex : Triviality.trivial;

            int j = nameArgSig.IndexOf('(');
            this.name = new TokenName(null, nameArgSig.Substring(0, j++));

            this.argDecl = new TokenArgDecl(null);
            if(nameArgSig[j] != ')')
            {
                int i;
                TokenName name;
                TokenType type;

                for(i = j; nameArgSig[i] != ')'; i++)
                {
                    if(nameArgSig[i] == ',')
                    {
                        type = TokenType.FromLSLType(null, nameArgSig.Substring(j, i - j));
                        name = new TokenName(null, "arg" + this.argDecl.varDict.Count);
                        this.argDecl.AddArg(type, name);
                        j = i + 1;
                    }
                }

                type = TokenType.FromLSLType(null, nameArgSig.Substring(j, i - j));
                name = new TokenName(null, "arg" + this.argDecl.varDict.Count);
                this.argDecl.AddArg(type, name);
            }

            this.location = new CompValuInline(this);
            if(ifd == null)
                ifd = inlineFunctions;
            ifd.AddEntry(this);
        }

        protected TokenDeclInline(VarDict ifd,
                                   bool doCheckRun,
                                   MethodInfo methInfo)
                : base(null, null, null)
        {
            TokenType retType = TokenType.FromSysType(null, methInfo.ReturnType);

            this.isTaggedCallsCheckRun = IsTaggedCallsCheckRun(methInfo);
            this.name = new TokenName(null, methInfo.Name);
            this.retType = GetRetType(methInfo, retType);
            this.argDecl = GetArgDecl(methInfo.GetParameters());
            this.triviality = (doCheckRun || this.isTaggedCallsCheckRun) ? Triviality.complex : Triviality.trivial;
            this.location = new CompValuInline(this);

            if(ifd == null)
                ifd = inlineFunctions;
            ifd.AddEntry(this);
        }

        private static TokenArgDecl GetArgDecl(ParameterInfo[] parameters)
        {
            TokenArgDecl argDecl = new TokenArgDecl(null);
            foreach(ParameterInfo pi in parameters)
            {
                TokenType type = TokenType.FromSysType(null, pi.ParameterType);
                TokenName name = new TokenName(null, pi.Name);
                argDecl.AddArg(type, name);
            }
            return argDecl;
        }

        /**
         * @brief The above code assumes all methods beginning with 'xmr' are trivial, ie, 
         *        they do not call CheckRun() and also we do not generate a CheckRun() 
         *        call after they return.  So if an 'xmr' method does call CheckRun(), it 
         *        must be tagged with attribute 'xmrMethodCallsCheckRunAttribute' so we know 
         *        the method is not trivial.  But in neither case do we emit our own call 
         *        to CheckRun(), the 'xmr' method must do its own.  We do however set up a
         *        call label before the call to the non-trivial 'xmr' method so when we are
         *        restoring the call stack, the restore will call directly in to the 'xmr'
         *        method without re-executing any code before the call to the 'xmr' method.
         */
        private static bool IsTaggedCallsCheckRun(MethodInfo methInfo)
        {
            return (methInfo != null) &&
                Attribute.IsDefined(methInfo, typeof(xmrMethodCallsCheckRunAttribute));
        }

        /**
         * @brief The dumbass OpenSim has key and string as the same type so non-ll
         *        methods must be tagged with xmrMethodReturnsKeyAttribute if we
         *        are to think they return a key type, otherwise we will think they
         *        return string.
         */
        private static TokenType GetRetType(MethodInfo methInfo, TokenType retType)
        {
            if((methInfo != null) && (retType != null) && (retType is TokenTypeStr))
            {
                if(Attribute.IsDefined(methInfo, typeof(xmrMethodReturnsKeyAttribute)))
                {
                    return ChangeToKeyType(retType);
                }

                string mn = methInfo.Name;
                foreach(string kr in keyReturns)
                {
                    if(kr == mn)
                        return ChangeToKeyType(retType);
                }

            }
            return retType;
        }
        private static TokenType ChangeToKeyType(TokenType retType)
        {
            if(retType is TokenTypeLSLString)
            {
                retType = new TokenTypeLSLKey(null);
            }
            else
            {
                retType = new TokenTypeKey(null);
            }
            return retType;
        }

        public virtual MethodInfo GetMethodInfo()
        {
            return null;
        }

        /**
         * @brief Print out a list of all the built-in functions and constants.
         */
        public delegate void WriteLine(string str);
        public static void PrintBuiltins(bool inclNoisyTag, WriteLine writeLine)
        {
            writeLine("\nBuilt-in functions:\n");
            SortedDictionary<string, TokenDeclInline> bifs = new SortedDictionary<string, TokenDeclInline>();
            foreach(TokenDeclVar bif in TokenDeclInline.inlineFunctions)
            {
                bifs.Add(bif.fullName, (TokenDeclInline)bif);
            }
            foreach(TokenDeclInline bif in bifs.Values)
            {
                char noisy = (!inclNoisyTag || !IsTaggedNoisy(bif.GetMethodInfo())) ? ' ' : (bif.retType is TokenTypeVoid) ? 'N' : 'R';
                writeLine(noisy + "   " + bif.retType.ToString().PadLeft(8) + " " + bif.fullName);
            }
            if(inclNoisyTag)
            {
                writeLine("\nN - stub that writes name and arguments to stdout");
                writeLine("R - stub that writes name and arguments to stdout then reads return value from stdin");
                writeLine("    format is:  function_name : return_value");
                writeLine("      example:  llKey2Name:\"Kunta Kinte\"");
            }

            writeLine("\nBuilt-in constants:\n");
            SortedDictionary<string, ScriptConst> scs = new SortedDictionary<string, ScriptConst>();
            int widest = 0;
            foreach(ScriptConst sc in ScriptConst.scriptConstants.Values)
            {
                if(widest < sc.name.Length)
                    widest = sc.name.Length;
                scs.Add(sc.name, sc);
            }
            foreach(ScriptConst sc in scs.Values)
            {
                writeLine("    " + sc.rVal.type.ToString().PadLeft(8) + " " + sc.name.PadRight(widest) + " = " + BuiltInConstVal(sc.rVal));
            }
        }

        public static bool IsTaggedNoisy(MethodInfo methInfo)
        {
            return (methInfo != null) && Attribute.IsDefined(methInfo, typeof(xmrMethodIsNoisyAttribute));
        }

        public static string BuiltInConstVal(CompValu rVal)
        {
            if(rVal is CompValuInteger)
            {
                int x = ((CompValuInteger)rVal).x;
                return "0x" + x.ToString("X8") + " = " + x.ToString().PadLeft(11);
            }
            if(rVal is CompValuFloat)
                return ((CompValuFloat)rVal).x.ToString();
            if(rVal is CompValuString)
            {
                StringBuilder sb = new StringBuilder();
                PrintParam(sb, ((CompValuString)rVal).x);
                return sb.ToString();
            }
            if(rVal is CompValuSField)
            {
                FieldInfo fi = ((CompValuSField)rVal).field;
                StringBuilder sb = new StringBuilder();
                PrintParam(sb, fi.GetValue(null));
                return sb.ToString();
            }
            return rVal.ToString();  // just prints the type
        }

        public static void PrintParam(StringBuilder sb, object p)
        {
            if(p == null)
            {
                sb.Append("null");
            }
            else if(p is LSL_List)
            {
                sb.Append('[');
                object[] d = ((LSL_List)p).Data;
                for(int i = 0; i < d.Length; i++)
                {
                    if(i > 0)
                        sb.Append(',');
                    PrintParam(sb, d[i]);
                }
                sb.Append(']');
            }
            else if(p is LSL_Rotation)
            {
                LSL_Rotation r = (LSL_Rotation)p;
                sb.Append('<');
                sb.Append(r.x);
                sb.Append(',');
                sb.Append(r.y);
                sb.Append(',');
                sb.Append(r.z);
                sb.Append(',');
                sb.Append(r.s);
                sb.Append('>');
            }
            else if(p is LSL_String)
            {
                PrintParamString(sb, (string)(LSL_String)p);
            }
            else if(p is LSL_Vector)
            {
                LSL_Vector v = (LSL_Vector)p;
                sb.Append('<');
                sb.Append(v.x);
                sb.Append(',');
                sb.Append(v.y);
                sb.Append(',');
                sb.Append(v.z);
                sb.Append('>');
            }
            else if(p is string)
            {
                PrintParamString(sb, (string)p);
            }
            else
            {
                sb.Append(p.ToString());
            }
        }

        public static void PrintParamString(StringBuilder sb, string p)
        {
            sb.Append('"');
            foreach(char c in p)
            {
                if(c == '\b')
                {
                    sb.Append("\\b");
                    continue;
                }
                if(c == '\n')
                {
                    sb.Append("\\n");
                    continue;
                }
                if(c == '\r')
                {
                    sb.Append("\\r");
                    continue;
                }
                if(c == '\t')
                {
                    sb.Append("\\t");
                    continue;
                }
                if(c == '"')
                {
                    sb.Append("\\\"");
                    continue;
                }
                if(c == '\\')
                {
                    sb.Append("\\\\");
                    continue;
                }
                sb.Append(c);
            }
            sb.Append('"');
        }
    }

    /**
     * @brief Code generators...
     * @param scg = script we are generating code for
     * @param result = type/location for result (type matches function definition)
     * @param args = type/location of arguments (types match function definition)
     */

    public class TokenDeclInline_LLAbs: TokenDeclInline
    {
        public TokenDeclInline_LLAbs(VarDict ifd)
                : base(ifd, false, "llAbs(integer)", new TokenTypeInt(null)) { }

        public override void CodeGen(ScriptCodeGen scg, Token errorAt, CompValuTemp result, CompValu[] args)
        {
            ScriptMyLabel itsPosLabel = scg.ilGen.DefineLabel("llAbstemp");

            args[0].PushVal(scg, errorAt);
            scg.ilGen.Emit(errorAt, OpCodes.Dup);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_0);
            scg.ilGen.Emit(errorAt, OpCodes.Bge_S, itsPosLabel);
            scg.ilGen.Emit(errorAt, OpCodes.Neg);
            scg.ilGen.MarkLabel(itsPosLabel);
            result.Pop(scg, errorAt, retType);
        }
    }

    public class TokenDeclInline_Math: TokenDeclInline
    {
        private MethodInfo methInfo;

        public TokenDeclInline_Math(VarDict ifd, string sig, string name, Type[] args)
                : base(ifd, false, sig, new TokenTypeFloat(null))
        {
            methInfo = ScriptCodeGen.GetStaticMethod(typeof(System.Math), name, args);
        }

        public override void CodeGen(ScriptCodeGen scg, Token errorAt, CompValuTemp result, CompValu[] args)
        {
            for(int i = 0; i < args.Length; i++)
            {
                args[i].PushVal(scg, errorAt, argDecl.types[i]);
            }
            scg.ilGen.Emit(errorAt, OpCodes.Call, methInfo);
            result.Pop(scg, errorAt, retType);
        }
    }

    public class TokenDeclInline_LLRound: TokenDeclInline
    {

        private static MethodInfo roundMethInfo = ScriptCodeGen.GetStaticMethod(typeof(System.Math), "Round",
                new Type[] { typeof(double), typeof(MidpointRounding) });

        public TokenDeclInline_LLRound(VarDict ifd)
                : base(ifd, false, "llRound(float)", new TokenTypeInt(null)) { }

        public override void CodeGen(ScriptCodeGen scg, Token errorAt, CompValuTemp result, CompValu[] args)
        {
            args[0].PushVal(scg, errorAt, new TokenTypeFloat(null));
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4, (int)System.MidpointRounding.AwayFromZero);
            scg.ilGen.Emit(errorAt, OpCodes.Call, roundMethInfo);
            result.Pop(scg, errorAt, new TokenTypeFloat(null));
        }
    }

    public class TokenDeclInline_GetFreeMemory: TokenDeclInline
    {
        private static readonly MethodInfo getFreeMemMethInfo = typeof(XMRInstAbstract).GetMethod("xmrHeapLeft", new Type[] { });

        public TokenDeclInline_GetFreeMemory(VarDict ifd)
                : base(ifd, false, "llGetFreeMemory()", new TokenTypeInt(null)) { }

        // appears as llGetFreeMemory() in script source code
        // but actually calls xmrHeapLeft()
        public override void CodeGen(ScriptCodeGen scg, Token errorAt, CompValuTemp result, CompValu[] args)
        {
            scg.PushXMRInst();
            scg.ilGen.Emit(errorAt, OpCodes.Call, getFreeMemMethInfo);
            result.Pop(scg, errorAt, new TokenTypeInt(null));
        }
    }

    public class TokenDeclInline_GetUsedMemory: TokenDeclInline
    {
        private static readonly MethodInfo getUsedMemMethInfo = typeof(XMRInstAbstract).GetMethod("xmrHeapUsed", new Type[] { });

        public TokenDeclInline_GetUsedMemory(VarDict ifd)
                : base(ifd, false, "llGetUsedMemory()", new TokenTypeInt(null)) { }

        // appears as llGetUsedMemory() in script source code
        // but actually calls xmrHeapUsed()
        public override void CodeGen(ScriptCodeGen scg, Token errorAt, CompValuTemp result, CompValu[] args)
        {
            scg.PushXMRInst();
            scg.ilGen.Emit(errorAt, OpCodes.Call, getUsedMemMethInfo);
            result.Pop(scg, errorAt, new TokenTypeInt(null));
        }
    }

    /**
     * @brief Generate code for the usual ll...() functions.
     */
    public class TokenDeclInline_BEApi: TokenDeclInline
    {
        //        private static readonly MethodInfo fixLLParcelMediaQuery = ScriptCodeGen.GetStaticMethod 
        //                (typeof (XMRInstAbstract), "FixLLParcelMediaQuery", new Type[] { typeof (LSL_List) });

        //        private static readonly MethodInfo fixLLParcelMediaCommandList = ScriptCodeGen.GetStaticMethod 
        //                (typeof (XMRInstAbstract), "FixLLParcelMediaCommandList", new Type[] { typeof (LSL_List) });

        public bool doCheckRun;
        private FieldInfo apiContextField;
        private MethodInfo methInfo;

        /**
         * @brief Constructor
         * @param ifd = dictionary to add the function to
         * @param dcr = append a call to CheckRun()
         * @param methInfo = ll...() method to be called
         */
        public TokenDeclInline_BEApi(VarDict ifd, bool dcr, MethodInfo methInfo, FieldInfo acf)
                : base(ifd, dcr, methInfo)
        {
            this.methInfo = methInfo;
            doCheckRun = dcr;
            apiContextField = acf;
        }

        public override MethodInfo GetMethodInfo()
        {
            return methInfo;
        }

        /**
         * @brief Generate call to backend API function (eg llSay()) maybe followed by a call to CheckRun().
         * @param scg    = script being compiled
         * @param result = where to place result (might be void)
         * @param args   = script-visible arguments to pass to API function
         */
        public override void CodeGen(ScriptCodeGen scg, Token errorAt, CompValuTemp result, CompValu[] args)
        {
            if(isTaggedCallsCheckRun)
            {                                                   // see if 'xmr' method that calls CheckRun() internally
                new ScriptCodeGen.CallLabel(scg, errorAt);     // if so, put a call label immediately before it
                                                               // .. so restoring the frame will jump immediately to the
                                                               // .. call without re-executing any code before this
            }
            if(!methInfo.IsStatic)
            {
                scg.PushXMRInst();                          // XMRInstanceSuperType pointer
                if(apiContextField != null)                 // 'this' pointer for API function
                    scg.ilGen.Emit(errorAt, OpCodes.Ldfld, apiContextField);

            }
            for(int i = 0; i < args.Length; i++)             // push arguments, boxing/unboxing as needed
                args[i].PushVal(scg, errorAt, argDecl.types[i]);

            // this should not be needed
            //            if (methInfo.Name == "llParcelMediaQuery") {
            //                scg.ilGen.Emit (errorAt, OpCodes.Call, fixLLParcelMediaQuery);
            //            }
            // this should not be needed
            //            if (methInfo.Name == "llParcelMediaCommandList") {
            //                scg.ilGen.Emit (errorAt, OpCodes.Call, fixLLParcelMediaCommandList);
            //            }
            if(methInfo.IsVirtual)                            // call API function
                scg.ilGen.Emit(errorAt, OpCodes.Callvirt, methInfo);
            else
                scg.ilGen.Emit(errorAt, OpCodes.Call, methInfo);

            result.Pop(scg, errorAt, retType);                  // pop result, boxing/unboxing as needed
            if(isTaggedCallsCheckRun)
                scg.openCallLabel = null;

            if(doCheckRun)
                scg.EmitCallCheckRun(errorAt, false);       // maybe call CheckRun()
        }
    }
}
