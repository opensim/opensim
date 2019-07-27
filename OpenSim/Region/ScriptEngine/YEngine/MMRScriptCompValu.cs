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

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

/**
 * @brief Compute values used during code generation to keep track of where computed values are stored.
 *
 *        Conceptually holds the memory address and type of the value
 *        such as that used for a local variable, global variable, temporary variable.
 *        Also used for things like constants and function/method entrypoints,
 *        they are basically treated as read-only variables.
 *
 *            cv.type - type of the value
 *
 *            cv.PushVal() - pushes the value on the CIL stack
 *            cv.PushRef() - pushes address of the value on the CIL stack
 *
 *            cv.PopPre()  - gets ready to pop from the CIL stack
 *                           ...by possibly pushing something
 *                <push value to be popped>
 *            cv.PushPre() - pops value from the CIL stack
 *
 *        If the type is a TokenTypeSDTypeDelegate, the location is callable, 
 *        so you get these additional functions:
 *
 *            cv.GetRetType()  - gets function/method's return value type
 *                               TokenTypeVoid if void
 *                               null if not a delegate
 *            cv.GetArgTypes() - gets array of argument types
 *                               as seen by script level, ie, 
 *                               does not include any hidden 'this' type
 *            cv.GetArgSig()   - gets argument signature eg, "(integer,list)"
 *                               null if not a delegate
 *
 *            cv.CallPre()     - gets ready to call the function/method
 *                               ...by possibly pushing something
 *                                  such as a 'this' pointer
 *                <push call args left-to-right>
 *            cv.CallPost()    - calls the function/method
 */

namespace OpenSim.Region.ScriptEngine.Yengine
{

    /**
     * @brief Location of a value
     *        Includes constants, expressions and temp variables.
     */
    public abstract class CompValu
    {
        protected static readonly MethodInfo gsmdMethodInfo =
                typeof(XMRInstAbstract).GetMethod("GetScriptMethodDelegate",
                                                    new Type[] { typeof(string), typeof(string), typeof(object) });

        private static readonly MethodInfo avpmListMethInfo = typeof(XMRInstArrays).GetMethod("PopList", new Type[] { typeof(int), typeof(LSL_List) });
        private static readonly MethodInfo avpmObjectMethInfo = typeof(XMRInstArrays).GetMethod("PopObject", new Type[] { typeof(int), typeof(object) });
        private static readonly MethodInfo avpmStringMethInfo = typeof(XMRInstArrays).GetMethod("PopString", new Type[] { typeof(int), typeof(string) });

        public TokenType type;        // type of the value and where in the source it was used

        public CompValu(TokenType type)
        {
            this.type = type;
        }

        public Type ToSysType()
        {
            return (type.ToLSLWrapType() != null) ? type.ToLSLWrapType() : type.ToSysType();
        }

        /*
         * if a field of an XMRInstArrays array cannot be directly written,
         * get the method that can write it
         */
        private static MethodInfo ArrVarPopMeth(FieldInfo fi)
        {
            if(fi.Name == "iarLists")
                return avpmListMethInfo;
            if(fi.Name == "iarObjects")
                return avpmObjectMethInfo;
            if(fi.Name == "iarStrings")
                return avpmStringMethInfo;
            return null;
        }

        /*
         * emit code to push value onto stack
         */
        public void PushVal(ScriptCodeGen scg, Token errorAt, TokenType stackType)
        {
            this.PushVal(scg, errorAt, stackType, false);
        }
        public void PushVal(ScriptCodeGen scg, Token errorAt, TokenType stackType, bool explicitAllowed)
        {
            this.PushVal(scg, errorAt);
            TypeCast.CastTopOfStack(scg, errorAt, this.type, stackType, explicitAllowed);
        }
        public abstract void PushVal(ScriptCodeGen scg, Token errorAt);
        public abstract void PushRef(ScriptCodeGen scg, Token errorAt);

        /*
         * emit code to pop value from stack
         */
        public void PopPost(ScriptCodeGen scg, Token errorAt, TokenType stackType)
        {
            TypeCast.CastTopOfStack(scg, errorAt, stackType, this.type, false);
            this.PopPost(scg, errorAt);
        }
        public virtual void PopPre(ScriptCodeGen scg, Token errorAt)
        {
        }

        /*
         * call this before pushing value to be popped
         */   
        public abstract void PopPost(ScriptCodeGen scg, Token errorAt);   // call this after pushing value to be popped


        /*
         * return true: doing a PushVal() does not involve CheckRun()
         *       false: otherwise
         */
        public virtual bool IsReadTrivial(ScriptCodeGen scg, Token readAt)
        {
            return true;
        }

        /*
         * These additional functions are available if the type is a delegate
         */
        public TokenType GetRetType()
        {
            if(!(type is TokenTypeSDTypeDelegate))
                return null;
            return ((TokenTypeSDTypeDelegate)type).decl.GetRetType();
        }
        public TokenType[] GetArgTypes()
        {
            if(!(type is TokenTypeSDTypeDelegate))
                return null;
            return ((TokenTypeSDTypeDelegate)type).decl.GetArgTypes();
        }
        public string GetArgSig()
        {
            if(!(type is TokenTypeSDTypeDelegate))
                return null;
            return ((TokenTypeSDTypeDelegate)type).decl.GetArgSig();
        }

        /*
         * These are used only if type is a delegate too
         * - but it is a real delegate pointer in a global or local variable or a field, etc
         * - ie, PushVal() pushes a delegate pointer
         * - so we must have CallPre() push the delegate pointer as a 'this' for this.Invoke(...)
         * - and CallPost() call the delegate's Invoke() method
         * - we assume the target function is non-trivial so we always use a call label
         */
        public virtual void CallPre(ScriptCodeGen scg, Token errorAt)   // call this before pushing arguments
        {
            new ScriptCodeGen.CallLabel(scg, errorAt);
            this.PushVal(scg, errorAt);
        }
        public virtual void CallPost(ScriptCodeGen scg, Token errorAt)  // call this after pushing arguments
        {
            TokenTypeSDTypeDelegate ttd = (TokenTypeSDTypeDelegate)type;
            MethodInfo invokeMethodInfo = ttd.decl.GetInvokerInfo();
            scg.ilGen.Emit(errorAt, OpCodes.Callvirt, invokeMethodInfo);
            scg.openCallLabel = null;
        }

        /*
         * Utilities used by CompValuGlobalVar and CompValuInstField
         * where the value is located in a type-dependent array.
         */
        protected void EmitFieldPushVal(ScriptCodeGen scg, Token errorAt, TokenDeclVar var)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Ldfld, var.vTableArray);   // which array
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4, var.vTableIndex);  // which array element
            if(type is TokenTypeFloat)
            {
                scg.ilGen.Emit(errorAt, OpCodes.Ldelem_R8);
            }
            else if(type is TokenTypeInt)
            {
                scg.ilGen.Emit(errorAt, OpCodes.Ldelem_I4);
            }
            else if(type is TokenTypeSDTypeDelegate)
            {
                scg.ilGen.Emit(errorAt, OpCodes.Ldelem, typeof(object));
                scg.ilGen.Emit(errorAt, OpCodes.Castclass, ToSysType());
            }
            else
            {
                scg.ilGen.Emit(errorAt, OpCodes.Ldelem, ToSysType());
            }
        }

        protected void EmitFieldPushRef(ScriptCodeGen scg, Token errorAt, TokenDeclVar var)
        {
            if(ArrVarPopMeth(var.vTableArray) != null)
            {
                scg.ErrorMsg(errorAt, "can't take address of this variable");
            }
            scg.ilGen.Emit(errorAt, OpCodes.Ldfld, var.vTableArray);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4, var.vTableIndex);
            scg.ilGen.Emit(errorAt, OpCodes.Ldelema, ToSysType());
        }

        protected void EmitFieldPopPre(ScriptCodeGen scg, Token errorAt, TokenDeclVar var)
        {
            if(ArrVarPopMeth(var.vTableArray) != null)
            {
                scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4, var.vTableIndex);
            }
            else
            {
                scg.ilGen.Emit(errorAt, OpCodes.Ldfld, var.vTableArray);
                scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4, var.vTableIndex);
            }
        }

        protected void EmitFieldPopPost(ScriptCodeGen scg, Token errorAt, TokenDeclVar var)
        {
            if(ArrVarPopMeth(var.vTableArray) != null)
            {
                scg.ilGen.Emit(errorAt, OpCodes.Call, ArrVarPopMeth(var.vTableArray));
            }
            else if(type is TokenTypeFloat)
            {
                scg.ilGen.Emit(errorAt, OpCodes.Stelem_R8);
            }
            else if(type is TokenTypeInt)
            {
                scg.ilGen.Emit(errorAt, OpCodes.Stelem_I4);
            }
            else if(type is TokenTypeSDTypeDelegate)
            {
                scg.ilGen.Emit(errorAt, OpCodes.Stelem, typeof(object));
            }
            else
            {
                scg.ilGen.Emit(errorAt, OpCodes.Stelem, ToSysType());
            }
        }

        /**
         * @brief With value pushed on stack, emit code to set a property by calling its setter() method.
         * @param scg = which script is being compiled
         * @param errorAt = for error messages
         * @param type = property type
         * @param setProp = setter() method
         */
        protected void EmitPopPostProp(ScriptCodeGen scg, Token errorAt, TokenType type, CompValu setProp)
        {
            ScriptMyLocal temp = scg.ilGen.DeclareLocal(type.ToSysType(), "__spr_" + errorAt.Unique);
            scg.ilGen.Emit(errorAt, OpCodes.Stloc, temp);
            setProp.CallPre(scg, errorAt);
            scg.ilGen.Emit(errorAt, OpCodes.Ldloc, temp);
            setProp.CallPost(scg, errorAt);
        }
    }

    // The value is kept in an (XMR_Array) array element
    public class CompValuArEle: CompValu
    {
        public CompValu arr;
        private CompValu idx;
        private TokenTypeObject tto;

        private static readonly MethodInfo getByKeyMethodInfo = typeof(XMR_Array).GetMethod("GetByKey",
                                                                                              new Type[] { typeof(object) });
        private static readonly MethodInfo setByKeyMethodInfo = typeof(XMR_Array).GetMethod("SetByKey",
                                                                                              new Type[] { typeof (object),
                                                                                                           typeof (object) });

        // type = TokenTypeObject always, as our array elements are always of type 'object'
        // arr  = where the array object itself is stored
        // idx  = where the index value is stored
        public CompValuArEle(TokenType type, CompValu arr, CompValu idx) : base(type)
        {
            this.arr = arr;
            this.idx = idx;
            this.tto = new TokenTypeObject(this.type);
        }
        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
            arr.PushVal(scg, errorAt);   // array
            idx.PushVal(scg, errorAt, this.tto);  // key
            scg.ilGen.Emit(errorAt, OpCodes.Call, getByKeyMethodInfo);
        }
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            scg.ErrorMsg(errorAt, "array element not allowed here");
            scg.ilGen.Emit(errorAt, OpCodes.Ldnull);
        }
        public override void PopPre(ScriptCodeGen scg, Token errorAt)
        {
            arr.PushVal(scg, errorAt);            // array
            idx.PushVal(scg, errorAt, this.tto);  // key
        }
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, setByKeyMethodInfo);
        }

        // non-trivial because it needs to be copied into a temp
        // in case the idiot does dumb-ass side effects tricks
        //   eg,  (x = 0) + x + 2
        //   should read old value of x not 0
        // but if 'xmroption norighttoleft;' in effect,
        // we can read it in any order so reading an
        // XMR_Array element is trivial
        public override bool IsReadTrivial(ScriptCodeGen scg, Token readAt)
        {
            return readAt.nr2l;
        }
    }

    // The value is kept in the current function's argument list
    public class CompValuArg: CompValu
    {
        public int index;
        public bool readOnly;

        private static OpCode[] ldargs = { OpCodes.Ldarg_0, OpCodes.Ldarg_1,
                                           OpCodes.Ldarg_2, OpCodes.Ldarg_3 };

        public CompValuArg(TokenType type, int index) : base(type)
        {
            this.index = index;
        }
        public CompValuArg(TokenType type, int index, bool ro) : base(type)
        {
            this.index = index;
            this.readOnly = ro;
        }
        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
            if(index < ldargs.Length)
                scg.ilGen.Emit(errorAt, ldargs[index]);
            else if(index <= 255)
                scg.ilGen.Emit(errorAt, OpCodes.Ldarg_S, index);
            else
                scg.ilGen.Emit(errorAt, OpCodes.Ldarg, index);
        }
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            if(readOnly)
            {
                scg.ErrorMsg(errorAt, "location cannot be written to");
            }
            if(index <= 255)
                scg.ilGen.Emit(errorAt, OpCodes.Ldarga_S, index);
            else
                scg.ilGen.Emit(errorAt, OpCodes.Ldarga, index);
        }
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
            if(readOnly)
            {
                scg.ErrorMsg(errorAt, "location cannot be written to");
            }
            scg.ilGen.Emit(errorAt, OpCodes.Starg, index);
        }

        // non-trivial because it needs to be copied into a temp
        // in case the idiot does dumb-ass side effects tricks
        //   eg,  (x = 0) + x + 2
        //   should read old value of x not 0
        // but if 'xmroption norighttoleft;' in effect,
        // we can read it in any order so reading an
        // argument is trivial
        public override bool IsReadTrivial(ScriptCodeGen scg, Token readAt)
        {
            return readAt.nr2l;
        }
    }

    // The value is a character constant
    public class CompValuChar: CompValu
    {
        public char x;

        public CompValuChar(TokenType type, char x) : base(type)
        {
            if(!(this.type is TokenTypeChar))
            {
                this.type = new TokenTypeChar(this.type);
            }
            this.x = x;
        }
        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4, (int)x);
        }
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            throw new Exception("cannot get constant's address");
        }
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
            throw new Exception("cannot store into contant");
        }
    }

    // The value is kept in a struct/class field of an internal struct/class
    public class CompValuField: CompValu
    {
        CompValu obj;
        FieldInfo field;

        public CompValuField(TokenType type, CompValu obj, FieldInfo field) : base(type)
        {
            this.obj = obj;
            this.field = field;
        }
        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
            if(field.ReflectedType.IsValueType)
            {
                obj.PushRef(scg, errorAt);
            }
            else
            {
                obj.PushVal(scg, errorAt);
            }
            scg.ilGen.Emit(errorAt, OpCodes.Ldfld, field);
        }
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            if(field.ReflectedType.IsValueType)
            {
                obj.PushRef(scg, errorAt);
            }
            else
            {
                obj.PushVal(scg, errorAt);
            }
            scg.ilGen.Emit(errorAt, OpCodes.Ldflda, field);
        }
        public override void PopPre(ScriptCodeGen scg, Token errorAt)
        {
            if(field.ReflectedType.IsValueType)
            {
                obj.PushRef(scg, errorAt);
            }
            else
            {
                obj.PushVal(scg, errorAt);
            }
        }
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Stfld, field);
        }

        // non-trivial because it needs to be copied into a temp
        // in case the idiot does dumb-ass side effects tricks
        //   eg,  (x = 0) + x + 2
        //   should read old value of x not 0
        // but if 'xmroption norighttoleft;' in effect,
        // we can read it in any order so reading an
        // field of a class/struct is trivial
        public override bool IsReadTrivial(ScriptCodeGen scg, Token readAt)
        {
            return readAt.nr2l;
        }
    }

    // Accessing an element of a fixed-dimension array
    public class CompValuFixArEl: CompValu
    {
        private CompValu baseRVal;
        private CompValu[] subRVals;

        private int nSubs;
        private TokenDeclVar getFunc;
        private TokenDeclVar setFunc;
        private TokenTypeInt tokenTypeInt;

        /**
         * @brief Set up to access an element of an array.
         * @param scg = what script we are compiling
         * @param baseRVal = what array we are accessing
         * @param subRVals = the subscripts being applied
         */
        public CompValuFixArEl(ScriptCodeGen scg, CompValu baseRVal, CompValu[] subRVals) : base(GetElementType(scg, baseRVal, subRVals))
        {
            this.baseRVal = baseRVal;  // location of the array itself
            this.subRVals = subRVals;  // subscript values
            this.nSubs = subRVals.Length;

            TokenTypeSDTypeClass sdtType = (TokenTypeSDTypeClass)baseRVal.type;
            TokenDeclSDTypeClass sdtDecl = sdtType.decl;
            tokenTypeInt = new TokenTypeInt(sdtType);

            TokenName name = new TokenName(sdtType, "Get");
            TokenType[] argsig = new TokenType[nSubs];
            for(int i = 0; i < nSubs; i++)
            {
                argsig[i] = tokenTypeInt;
            }
            getFunc = scg.FindThisMember(sdtDecl, name, argsig);

            name = new TokenName(sdtType, "Set");
            argsig = new TokenType[nSubs + 1];
            for(int i = 0; i < nSubs; i++)
            {
                argsig[i] = tokenTypeInt;
            }
            argsig[nSubs] = getFunc.retType;
            setFunc = scg.FindThisMember(sdtDecl, name, argsig);
        }

        /**
         * @brief Read array element and push value on stack.
         */
        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
            // call script-defined class' Get() method to fetch the value
            baseRVal.PushVal(scg, errorAt);
            for(int i = 0; i < nSubs; i++)
            {
                subRVals[i].PushVal(scg, errorAt, tokenTypeInt);
            }
            scg.ilGen.Emit(errorAt, OpCodes.Call, getFunc.ilGen);
        }

        /**
         * @brief Push address of array element on stack.
         */
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            throw new Exception("tu stOOpid to get array element address");
        }

        /**
         * @brief Prepare to write array element.
         */
        public override void PopPre(ScriptCodeGen scg, Token errorAt)
        {
            // set up call to script-defined class' Set() method to write the value
            baseRVal.PushVal(scg, errorAt);
            for(int i = 0; i < nSubs; i++)
            {
                subRVals[i].PushVal(scg, errorAt, tokenTypeInt);
            }
        }

        /**
         * @brief Pop value from stack and write array element.
         */
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
            // call script-defined class' Set() method to write the value
            scg.ilGen.Emit(errorAt, OpCodes.Call, setFunc.ilGen);
        }

        /**
         * @brief Get the array element type by getting the Get() functions return type.
         *        Crude but effective.
         * @param scg = what script we are compiling
         * @param baseRVal = what array we are accessing
         * @param subRVals = the subscripts being applied
         * @returns array element type
         */
        private static TokenType GetElementType(ScriptCodeGen scg, CompValu baseRVal, CompValu[] subRVals)
        {
            TokenTypeSDTypeClass sdtType = (TokenTypeSDTypeClass)baseRVal.type;
            TokenDeclSDTypeClass sdtDecl = sdtType.decl;
            TokenName name = new TokenName(sdtType, "Get");
            int nSubs = subRVals.Length;
            TokenType[] argsig = new TokenType[nSubs];
            argsig[0] = new TokenTypeInt(sdtType);
            for(int i = 0; ++i < nSubs;)
            {
                argsig[i] = argsig[0];
            }
            TokenDeclVar getFunc = scg.FindThisMember(sdtDecl, name, argsig);
            return getFunc.retType;
        }

        // non-trivial because it needs to be copied into a temp
        // in case the idiot does dumb-ass side effects tricks
        //   eg,  (x = 0) + x + 2
        //   should read old value of x not 0
        // but if 'xmroption norighttoleft;' in effect,
        // we can read it in any order so reading an
        // fixed-dimension array element is trivial
        public override bool IsReadTrivial(ScriptCodeGen scg, Token readAt)
        {
            return readAt.nr2l;
        }
    }

    // The value is a float constant
    public class CompValuFloat: CompValu
    {
        public double x;

        public CompValuFloat(TokenType type, double x) : base(type)
        {
            if(!(this.type is TokenTypeFloat))
            {
                this.type = new TokenTypeFloat(this.type);
            }
            this.x = x;
        }
        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_R8, x);
        }
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            throw new Exception("cannot get constant's address");
        }
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
            throw new Exception("cannot store into constant");
        }
    }

    // The value is the entrypoint of a script-defined global function.
    // These are also used for script-defined type static methods as the calling convention is the same,
    // ie, the XMRInstance pointer is a hidden first argument.
    // There is just one of these created when the function is being compiled as there is only one value
    // of the function.
    public class CompValuGlobalMeth: CompValu
    {
        private TokenDeclVar func;

        public CompValuGlobalMeth(TokenDeclVar declFunc) : base(declFunc.GetDelType())
        {
            this.func = declFunc;
        }

        /**
         * @brief PushVal for a function/method means push a delegate on the stack.
         *        We build a call to the DynamicMethod's CreateDelegate() function 
         *        to create the delegate.  Slip the scriptinstance pointer as the 
         *        function's arg 0 so it will get passed to the function when called.
         */
        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
            string dtn = type.ToString();
            if(dtn.StartsWith("delegate "))
                dtn = dtn.Substring(9);

            // delegateinstance = (signature)scriptinstance.GetScriptMethodDelegate (methName, signature, arg0);
            //   where methName = [<sdtclass>.]<methname>(<argtypes>)
            //        signature = <rettype>(<argtypes>)
            //             arg0 = scriptinstance (XMRInstance)
            scg.PushXMRInst();                                     // [0] scriptinstance
            scg.ilGen.Emit(errorAt, OpCodes.Ldstr, func.ilGen.methName);    // [1] method name
            scg.ilGen.Emit(errorAt, OpCodes.Ldstr, dtn);                    // [2] delegate type name
            scg.PushXMRInst();                                     // [3] scriptinstance
            scg.ilGen.Emit(errorAt, OpCodes.Callvirt, gsmdMethodInfo);      // [0] delegate instance
            scg.ilGen.Emit(errorAt, OpCodes.Castclass, type.ToSysType());  // [0] cast to correct delegate class
        }
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            throw new Exception("cannot get ref to global method");
        }
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
            throw new Exception("cannot store into global method");
        }

        /**
         * @brief A direct call is much simpler than pushing a delegate.
         *        Just push the XMRInstance pointer, push the args and finally call the function.
         */
        public override void CallPre(ScriptCodeGen scg, Token errorAt)
        {
            if(!this.func.IsFuncTrivial(scg))
                new ScriptCodeGen.CallLabel(scg, errorAt);

            // all script-defined global functions are static methods created by DynamicMethod()
            // and the first argument is always the XMR_Instance pointer
            scg.PushXMRInst();
        }
        public override void CallPost(ScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, func.ilGen);
            if(!this.func.IsFuncTrivial(scg))
                scg.openCallLabel = null;
        }
    }

    // The value is in a script-global variable = ScriptModule instance variable
    // It could also be a script-global property
    public class CompValuGlobalVar: CompValu
    {
        private static readonly FieldInfo glblVarsFieldInfo = typeof(XMRInstAbstract).GetField("glblVars");

        private TokenDeclVar declVar;

        public CompValuGlobalVar(TokenDeclVar declVar, XMRInstArSizes glblSizes) : base(declVar.type)
        {
            this.declVar = declVar;
            if((declVar.getProp == null) && (declVar.setProp == null))
            {
                declVar.type.AssignVarSlot(declVar, glblSizes);
            }
        }
        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
            if((declVar.getProp == null) && (declVar.setProp == null))
            {
                scg.PushXMRInst();
                scg.ilGen.Emit(errorAt, OpCodes.Ldfld, glblVarsFieldInfo);
                EmitFieldPushVal(scg, errorAt, declVar);
            }
            else if(declVar.getProp != null)
            {
                declVar.getProp.location.CallPre(scg, errorAt);
                declVar.getProp.location.CallPost(scg, errorAt);
            }
            else
            {
                scg.ErrorMsg(errorAt, "property not readable");
                scg.PushDefaultValue(declVar.type);
            }
        }
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            if((declVar.getProp == null) && (declVar.setProp == null))
            {
                scg.PushXMRInst();
                scg.ilGen.Emit(errorAt, OpCodes.Ldfld, glblVarsFieldInfo);
                EmitFieldPushRef(scg, errorAt, declVar);
            }
            else
            {
                scg.ErrorMsg(errorAt, "cannot get address of property");
            }
        }
        public override void PopPre(ScriptCodeGen scg, Token errorAt)
        {
            if((declVar.getProp == null) && (declVar.setProp == null))
            {
                scg.PushXMRInst();
                scg.ilGen.Emit(errorAt, OpCodes.Ldfld, glblVarsFieldInfo);
                EmitFieldPopPre(scg, errorAt, declVar);
            }
            else if(declVar.setProp == null)
            {
                scg.ErrorMsg(errorAt, "property not writable");
            }
        }
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
            if((declVar.getProp == null) && (declVar.setProp == null))
            {
                EmitFieldPopPost(scg, errorAt, declVar);
            }
            else if(declVar.setProp != null)
            {
                EmitPopPostProp(scg, errorAt, declVar.type, declVar.setProp.location);
            }
            else
            {
                scg.ilGen.Emit(errorAt, OpCodes.Pop);
            }
        }

        // non-trivial because it needs to be copied into a temp
        // in case the idiot does dumb-ass side effects tricks
        //   eg,  (x = 0) + x + 2
        //   should read old value of x not 0
        // but if 'xmroption norighttoleft;' in effect,
        // we can read it in any order so reading an
        // global variable is trivial provided it is 
        // not a property or the property function is
        // trivial.
        public override bool IsReadTrivial(ScriptCodeGen scg, Token readAt)
        {
            return readAt.nr2l && ((declVar.getProp == null) || declVar.getProp.IsFuncTrivial(scg));
        }
    }

    // The value is in an $idxprop property of a script-defined type class or interface instance.
    // Reading and writing is via a method call.
    public class CompValuIdxProp: CompValu
    {
        private TokenDeclVar idxProp;  // $idxprop property within baseRVal
        private CompValu baseRVal;     // pointer to class or interface object containing property
        private TokenType[] argTypes;  // argument types as required by $idxprop declaration
        private CompValu[] indices;    // actual index values to pass to getter/setter method
        private CompValu setProp;      // location of setter method

        public CompValuIdxProp(TokenDeclVar idxProp, CompValu baseRVal, TokenType[] argTypes, CompValu[] indices) : base(idxProp.type)
        {
            this.idxProp = idxProp;
            this.baseRVal = baseRVal;
            this.argTypes = argTypes;
            this.indices = indices;
        }

        /**
         * @brief Pushing the property's value is a matter of calling the getter method
         *        with the supplied argument list as is.
         */
        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
            if(idxProp.getProp != null)
            {
                if(!idxProp.getProp.IsFuncTrivial(scg))
                {
                    for(int i = indices.Length; --i >= 0;)
                    {
                        indices[i] = scg.Trivialize(indices[i], errorAt);
                    }
                }
                CompValu getProp = GetIdxPropMeth(idxProp.getProp);
                getProp.CallPre(scg, errorAt);
                for(int i = 0; i < indices.Length; i++)
                {
                    indices[i].PushVal(scg, errorAt, argTypes[i]);
                }
                getProp.CallPost(scg, errorAt);
            }
            else
            {
                // write-only property
                scg.ErrorMsg(errorAt, "member not readable");
                scg.PushDefaultValue(idxProp.type);
            }
        }

        /**
         * @brief A property does not have a memory address.
         */
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            scg.ErrorMsg(errorAt, "member has no address");
            scg.ilGen.Emit(errorAt, OpCodes.Ldnull);
        }

        /**
         * @brief Preparing to write a property consists of preparing to call the setter method
         *        then pushing the index arguments.
         */
        public override void PopPre(ScriptCodeGen scg, Token errorAt)
        {
            if(idxProp.setProp != null)
            {
                if(!idxProp.setProp.IsFuncTrivial(scg))
                {
                    for(int i = indices.Length; --i >= 0;)
                    {
                        indices[i] = scg.Trivialize(indices[i], errorAt);
                    }
                }
                this.setProp = GetIdxPropMeth(idxProp.setProp);
                this.setProp.CallPre(scg, errorAt);
                for(int i = 0; i < indices.Length; i++)
                {
                    indices[i].PushVal(scg, errorAt, argTypes[i]);
                }
            }
            else
            {
                // read-only property
                scg.ErrorMsg(errorAt, "member not writable");
            }
        }

        /**
         * @brief Finishing writing a property consists of finishing the call to the setter method
         *        now that the value to be written has been pushed by our caller.
         */
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
            if(idxProp.setProp != null)
            {
                this.setProp.CallPost(scg, errorAt);
            }
            else
            {
                scg.ilGen.Emit(errorAt, OpCodes.Pop);
            }
        }

        public override bool IsReadTrivial(ScriptCodeGen scg, Token readAt)
        {
            // if no getter, reading would throw an error, so doesn't really matter what we say
            if(idxProp.getProp == null)
                return true;

            // assume interface methods are always non-trivial because we don't know anything about the actual implementation
            if(baseRVal.type is TokenTypeSDTypeInterface)
                return false;

            // accessing it in any way can't be trivial if reading the pointer isn't trivial
            if(!baseRVal.IsReadTrivial(scg, readAt))
                return false;

            // likewise with the indices
            foreach(CompValu idx in indices)
            {
                if(!idx.IsReadTrivial(scg, readAt))
                    return false;
            }

            // now the only way it can be non-trivial to read is if the getter() method itself is non-trivial.
            return idxProp.getProp.IsFuncTrivial(scg);
        }

        /**
         * @brief Get how to call the getter or setter method.
         */
        private CompValu GetIdxPropMeth(TokenDeclVar meth)
        {
            if(baseRVal.type is TokenTypeSDTypeClass)
            {
                return new CompValuInstMember(meth, baseRVal, false);
            }
            return new CompValuIntfMember(meth, baseRVal);
        }
    }

    // This represents the type and location of an internally-defined function
    // that a script can call
    public class CompValuInline: CompValu
    {
        public TokenDeclInline declInline;

        public CompValuInline(TokenDeclInline declInline) : base(declInline.GetDelType())
        {
            this.declInline = declInline;
        }

        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
            scg.ErrorMsg(errorAt, "cannot use built-in for delegate, wrap it");
            scg.ilGen.Emit(errorAt, OpCodes.Ldnull);
        }
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            scg.ErrorMsg(errorAt, "cannot use built-in for delegate, wrap it");
            scg.ilGen.Emit(errorAt, OpCodes.Ldnull);
        }
        public override void PopPre(ScriptCodeGen scg, Token errorAt)
        {
            scg.ErrorMsg(errorAt, "cannot use built-in for delegate, wrap it");
        }
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
            scg.ErrorMsg(errorAt, "cannot use built-in for delegate, wrap it");
            scg.ilGen.Emit(errorAt, OpCodes.Pop);
        }
    }

    // The value is the entrypoint of a script-defined type's interface method combined with
    // the pointer used to access the method.  Thus there is one of these per call site.
    // They also handle accessing interface properties.
    public class CompValuIntfMember: CompValu
    {
        private TokenDeclVar declVar;
        private CompValu baseRVal;

        public CompValuIntfMember(TokenDeclVar declVar, CompValu baseRVal) : base(declVar.type)
        {
            if(this.type == null)
                throw new Exception("interface member type is null");
            this.declVar = declVar;   // which element of the baseRVal vector to be accessed
            this.baseRVal = baseRVal;  // the vector of delegates implementing the interface
        }

        /**
         * @brief Reading a method's value means getting a delegate to that method.
         *        Reading a property's value means calling the getter method for that property.
         */
        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
            if(declVar.retType != null)
            {
                baseRVal.PushVal(scg, errorAt);                        // push pointer to delegate array on stack
                scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4, declVar.vTableIndex);   // select which delegate to access
                scg.ilGen.Emit(errorAt, OpCodes.Ldelem, typeof(Delegate));     // push delegate on stack
                scg.ilGen.Emit(errorAt, OpCodes.Castclass, type.ToSysType());  // cast to correct delegate class
            }
            else if(declVar.getProp != null)
            {
                CompValu getProp = new CompValuIntfMember(declVar.getProp, baseRVal);
                getProp.CallPre(scg, errorAt);                        // reading property, call its getter
                getProp.CallPost(scg, errorAt);                        // ... with no arguments
            }
            else
            {
                scg.ErrorMsg(errorAt, "member not readable");
                scg.PushDefaultValue(declVar.type);
            }
        }

        /**
         * @brief Can't get the address of either a method or a property.
         */
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            scg.ErrorMsg(errorAt, "member has no address");
            scg.ilGen.Emit(errorAt, OpCodes.Ldnull);
        }

        /**
         * @brief Can't write a method.
         *        For property, it means calling the setter method for that property.
         */
        public override void PopPre(ScriptCodeGen scg, Token errorAt)
        {
            if(declVar.setProp == null)
            {
                // read-only property
                scg.ErrorMsg(errorAt, "member not writable");
            }
        }
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
            if(declVar.setProp != null)
            {
                CompValu setProp = new CompValuIntfMember(declVar.setProp, baseRVal);
                EmitPopPostProp(scg, errorAt, declVar.type, setProp);
            }
            else
            {
                scg.ilGen.Emit(errorAt, OpCodes.Pop);
            }
        }

        /**
         * @brief Reading a method (ie, it's delegate) is always trivial, it's just retrieving
         *        an element from the delegate array that make up the interface object.
         *
         *        Reading a property is always non-trivial because we don't know which implementation 
         *        the interface is pointing to, so we don't know if it's trivial or not, so assume 
         *        the worst, ie, that it is non-trivial and might call CheckRun().
         *
         *        But all that assumes that locating the interface object in the first place is 
         *        trivial, ie, baseRVal.PushVal() must not call CheckRun() either.
         */
        public override bool IsReadTrivial(ScriptCodeGen scg, Token readAt)
        {
            return baseRVal.IsReadTrivial(scg, readAt) && (declVar.getProp == null);
        }

        /**
         * @brief We just defer to the default CallPre() and CallPost() methods.
         *        They expect this.PushVal() to push a delegate to the method to be called.
         *        If this member is a method, our PushVal() will read the correct element 
         *        of the iTable array and push it on the stack, ready for Invoke() to be
         *        called.  If this member is a property, the only way it can be called is 
         *        if the property is a delegate, in which case PushVal() will retrieve the 
         *        delegate by calling the property's getter method.
         */
    }

    // The value is the entrypoint of an internal instance method
    // such as XMR_Array.index()
    public class CompValuIntInstMeth: CompValu
    {
        private TokenTypeSDTypeDelegate delType;
        private CompValu baseRVal;
        private MethodInfo methInfo;

        public CompValuIntInstMeth(TokenTypeSDTypeDelegate delType, CompValu baseRVal, MethodInfo methInfo) : base(delType)
        {
            this.delType = delType;
            this.baseRVal = baseRVal;
            this.methInfo = methInfo;
        }

        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
            // its value, ie, without applying the (arglist), is a delegate...
            baseRVal.PushVal(scg, errorAt);
            scg.ilGen.Emit(errorAt, OpCodes.Ldftn, methInfo);
            scg.ilGen.Emit(errorAt, OpCodes.Newobj, delType.decl.GetConstructorInfo());
        }
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            throw new Exception("cannot get ref to instance method");
        }
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
            throw new Exception("cannot store into instance method");
        }

        public override void CallPre(ScriptCodeGen scg, Token errorAt)
        {
            // internal instance methods are always trivial so never need a CallLabel.
            baseRVal.PushVal(scg, errorAt);
        }
        public override void CallPost(ScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, methInfo);
        }
    }

    // The value is fetched by calling an internal instance method
    // such as XMR_Array.count
    public class CompValuIntInstROProp: CompValu
    {
        private CompValu baseRVal;
        private MethodInfo methInfo;

        public CompValuIntInstROProp(TokenType valType, CompValu baseRVal, MethodInfo methInfo) : base(valType)
        {
            this.baseRVal = baseRVal;
            this.methInfo = methInfo;
        }

        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
            baseRVal.PushVal(scg, errorAt);
            scg.ilGen.Emit(errorAt, OpCodes.Call, methInfo);
        }
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            scg.ErrorMsg(errorAt, "cannot get ref to read-only property");
            scg.ilGen.Emit(errorAt, OpCodes.Ldnull);
        }
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
            scg.ErrorMsg(errorAt, "cannot store into read-only property");
            scg.ilGen.Emit(errorAt, OpCodes.Pop);
        }
    }

    // The value is in a member of a script-defined type class instance.
    //       field: value is in one of the arrays contained within XMRSDTypeClObj.instVars
    //      method: value is a delegate; can be called
    //    property: reading and writing is via a method call
    public class CompValuInstMember: CompValu
    {
        private static readonly FieldInfo instVarsFieldInfo = typeof(XMRSDTypeClObj).GetField("instVars");
        private static readonly FieldInfo vTableFieldInfo = typeof(XMRSDTypeClObj).GetField("sdtcVTable");

        private TokenDeclVar declVar;  // member being accessed
        private CompValu baseRVal;     // pointer to particular object instance
        private bool ignoreVirt;       // ignore virtual attribute; use declVar's non-virtual method/property

        public CompValuInstMember(TokenDeclVar declVar, CompValu baseRVal, bool ignoreVirt) : base(declVar.type)
        {
            this.declVar = declVar;
            this.baseRVal = baseRVal;
            this.ignoreVirt = ignoreVirt;
        }

        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
            if(declVar.retType != null)
            {
                // a method's value, ie, without applying the (arglist), is a delegate...
                PushValMethod(scg, errorAt);
            }
            else if(declVar.vTableArray != null)
            {
                // a field's value is its XMRSDTypeClObj.instVars array element
                baseRVal.PushVal(scg, errorAt);
                scg.ilGen.Emit(errorAt, OpCodes.Ldfld, instVarsFieldInfo);
                EmitFieldPushVal(scg, errorAt, declVar);
            }
            else if(declVar.getProp != null)
            {
                // a property's value is calling its get method with no arguments
                CompValu getProp = new CompValuInstMember(declVar.getProp, baseRVal, ignoreVirt);
                getProp.CallPre(scg, errorAt);
                getProp.CallPost(scg, errorAt);
            }
            else
            {
                // write-only property
                scg.ErrorMsg(errorAt, "member not readable");
                scg.PushDefaultValue(declVar.type);
            }
        }
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            if(declVar.vTableArray != null)
            {
                // a field's value is its XMRSDTypeClObj.instVars array element
                baseRVal.PushVal(scg, errorAt);
                scg.ilGen.Emit(errorAt, OpCodes.Ldfld, instVarsFieldInfo);
                EmitFieldPushRef(scg, errorAt, declVar);
            }
            else
            {
                scg.ErrorMsg(errorAt, "member has no address");
                scg.ilGen.Emit(errorAt, OpCodes.Ldnull);
            }
        }
        public override void PopPre(ScriptCodeGen scg, Token errorAt)
        {
            if(declVar.vTableArray != null)
            {
                // a field's value is its XMRSDTypeClObj.instVars array element
                baseRVal.PushVal(scg, errorAt);
                scg.ilGen.Emit(errorAt, OpCodes.Ldfld, instVarsFieldInfo);
                EmitFieldPopPre(scg, errorAt, declVar);
            }
            else if(declVar.setProp == null)
            {
                // read-only property
                scg.ErrorMsg(errorAt, "member not writable");
            }
        }
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
            if(declVar.vTableArray != null)
            {
                EmitFieldPopPost(scg, errorAt, declVar);
            }
            else if(declVar.setProp != null)
            {
                CompValu setProp = new CompValuInstMember(declVar.setProp, baseRVal, ignoreVirt);
                EmitPopPostProp(scg, errorAt, declVar.type, setProp);
            }
            else
            {
                scg.ilGen.Emit(errorAt, OpCodes.Pop);
            }
        }

        public override bool IsReadTrivial(ScriptCodeGen scg, Token readAt)
        {
            // accessing it in any way can't be trivial if reading the pointer isn't trivial.
            // this also handles strict right-to-left mode detection as the side-effect can
            // only apply to the pointer (it can't change which field or method we access).
            if(!baseRVal.IsReadTrivial(scg, readAt))
                return false;

            // now the only way it can be non-trivial to read is if it is a property and the 
            // getter() method is non-trivial.  reading a method means getting a delegate 
            // which is always trivial, and reading a simple field is always trivial, ie, no 
            // CheckRun() call can possibly be involved.
            if(declVar.retType != null)
            {
                // a method's value, ie, without applying the (arglist), is a delegate...
                return true;
            }
            if(declVar.vTableArray != null)
            {
                // a field's value is its XMRSDTypeClObj.instVars array element
                return true;
            }
            if(declVar.getProp != null)
            {
                // a property's value is calling its get method with no arguments
                return declVar.getProp.IsFuncTrivial(scg);
            }

            // write-only property
            return true;
        }

        public override void CallPre(ScriptCodeGen scg, Token errorAt)
        {
            if(declVar.retType != null)
            {
                CallPreMethod(scg, errorAt);
            }
            else
            {
                base.CallPre(scg, errorAt);
            }
        }
        public override void CallPost(ScriptCodeGen scg, Token errorAt)
        {
            if(declVar.retType != null)
            {
                CallPostMethod(scg, errorAt);
            }
            else
            {
                base.CallPost(scg, errorAt);
            }
        }

        /**
         * @brief A PushVal() for a method means to push a delegate for the method on the stack.
         */
        private void PushValMethod(ScriptCodeGen scg, Token errorAt)
        {
            if((declVar.sdtFlags & ScriptReduce.SDT_STATIC) != 0)
                throw new Exception("dont use for statics");

            if(ignoreVirt || (declVar.vTableIndex < 0))
            {

                /*
                 * Non-virtual instance method, create a delegate that references the method.
                 */
                string dtn = type.ToString();

                // delegateinstance = (signature)scriptinstance.GetScriptMethodDelegate (methName, signature, arg0);
                //   where methName = <sdtclass>.<methname>(<argtypes>)
                //        signature = <rettype>(<argtypes>)
                //             arg0 = sdt istance (XMRSDTypeClObj) 'this' value
                scg.PushXMRInst();                                     // [0] scriptinstance
                scg.ilGen.Emit(errorAt, OpCodes.Ldstr, declVar.ilGen.methName); // [1] method name
                scg.ilGen.Emit(errorAt, OpCodes.Ldstr, dtn);                    // [2] delegate type name
                baseRVal.PushVal(scg, errorAt);                        // [3] sdtinstance
                scg.ilGen.Emit(errorAt, OpCodes.Callvirt, gsmdMethodInfo);      // [0] delegate instance
                scg.ilGen.Emit(errorAt, OpCodes.Castclass, type.ToSysType());  // [0] cast to correct delegate class
            }
            else
            {

                /*
                 * Virtual instance method, get the delegate from the vtable.
                 */
                baseRVal.PushVal(scg, errorAt);                                 // 'this' selecting the instance
                scg.ilGen.Emit(errorAt, OpCodes.Ldfld, vTableFieldInfo);        // get pointer to instance's vtable array
                scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4, declVar.vTableIndex);   // select vtable element
                scg.ilGen.Emit(errorAt, OpCodes.Ldelem, typeof(Delegate));     // get delegate pointer = 'this' for 'Invoke()'
                scg.ilGen.Emit(errorAt, OpCodes.Castclass, type.ToSysType());  // cast to correct delegate class
            }
        }

        private void CallPreMethod(ScriptCodeGen scg, Token errorAt)
        {
            if((declVar.sdtFlags & ScriptReduce.SDT_STATIC) != 0)
                throw new Exception("dont use for statics");

            if(!this.declVar.IsFuncTrivial(scg))
                new ScriptCodeGen.CallLabel(scg, errorAt);

            if(ignoreVirt || (declVar.vTableIndex < 0))
            {
                baseRVal.PushVal(scg, errorAt);                                 // 'this' being passed directly to method
            }
            else
            {
                baseRVal.PushVal(scg, errorAt);                                 // 'this' selecting the instance
                scg.ilGen.Emit(errorAt, OpCodes.Ldfld, vTableFieldInfo);        // get pointer to instance's vtable array
                scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4, declVar.vTableIndex);   // select vtable element
                scg.ilGen.Emit(errorAt, OpCodes.Ldelem, typeof(Delegate));     // get delegate pointer = 'this' for 'Invoke()'
                scg.ilGen.Emit(errorAt, OpCodes.Castclass, type.ToSysType());  // cast to correct delegate class
            }
        }
        private void CallPostMethod(ScriptCodeGen scg, Token errorAt)
        {
            if(ignoreVirt || (declVar.vTableIndex < 0))
            {
                // non-virt instance, just call function directly
                scg.ilGen.Emit(errorAt, OpCodes.Call, declVar.ilGen);
            }
            else
            {
                // virtual, call via delegate Invoke(...) method
                TokenTypeSDTypeDelegate ttd = (TokenTypeSDTypeDelegate)type;
                MethodInfo invokeMethodInfo = ttd.decl.GetInvokerInfo();
                scg.ilGen.Emit(errorAt, OpCodes.Callvirt, invokeMethodInfo);
            }

            if(!this.declVar.IsFuncTrivial(scg))
                scg.openCallLabel = null;
        }
    }

    // The value is an integer constant
    public class CompValuInteger: CompValu
    {
        public int x;

        public CompValuInteger(TokenType type, int x) : base(type)
        {
            if(!(this.type is TokenTypeInt))
            {
                this.type = new TokenTypeInt(this.type);
            }
            this.x = x;
        }
        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4, x);
        }
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            throw new Exception("cannot get constant's address");
        }
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
            throw new Exception("cannot store into constant");
        }
    }

    // The value is an element of a list
    public class CompValuListEl: CompValu
    {
        private static readonly MethodInfo getElementFromListMethodInfo =
                 typeof(CompValuListEl).GetMethod("GetElementFromList", new Type[] { typeof(LSL_List), typeof(int) });

        private CompValu theList;
        private CompValu subscript;

        public CompValuListEl(TokenType type, CompValu theList, CompValu subscript) : base(type)
        {
            this.theList = theList;
            this.subscript = subscript;
        }
        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
            theList.PushVal(scg, errorAt, new TokenTypeList(type));
            subscript.PushVal(scg, errorAt, new TokenTypeInt(type));
            scg.ilGen.Emit(errorAt, OpCodes.Call, getElementFromListMethodInfo);
        }
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            throw new Exception("cannot get list element's address");
        }
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
            scg.ErrorMsg(errorAt, "cannot store into list element");
            scg.ilGen.Emit(errorAt, OpCodes.Pop);
        }

        public static object GetElementFromList(LSL_List lis, int idx)
        {
            object element = lis.Data[idx];
            if(element is LSL_Float)
                return TypeCast.EHArgUnwrapFloat(element);
            if(element is LSL_Integer)
                return TypeCast.EHArgUnwrapInteger(element);
            if(element is LSL_String)
                return TypeCast.EHArgUnwrapString(element);
            if(element is OpenMetaverse.Quaternion)
                return TypeCast.EHArgUnwrapRotation(element);
            if(element is OpenMetaverse.Vector3)
                return TypeCast.EHArgUnwrapVector(element);
            return element;
        }
    }

    // The value is kept in a script-addressable local variable
    public class CompValuLocalVar: CompValu
    {
        private static int htpopseq = 0;

        private ScriptMyLocal localBuilder;

        public CompValuLocalVar(TokenType type, string name, ScriptCodeGen scg) : base(type)
        {
            if(type.ToHeapTrackerType() != null)
            {
                localBuilder = scg.ilGen.DeclareLocal(type.ToHeapTrackerType(), name);
                scg.HeapLocals.Add(localBuilder);
                scg.PushXMRInst();
                scg.ilGen.Emit(type, OpCodes.Newobj, type.GetHeapTrackerCtor());
                scg.ilGen.Emit(type, OpCodes.Stloc, localBuilder);
            }
            else
            {
                this.localBuilder = scg.ilGen.DeclareLocal(ToSysType(), name);
            }
        }

        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Ldloc, localBuilder);
            if(type.ToHeapTrackerType() != null)
            {
                type.CallHeapTrackerPushMeth(errorAt, scg.ilGen);
            }
        }
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            if(type.ToHeapTrackerType() != null)
            {
                scg.ErrorMsg(errorAt, "can't take ref of heap-tracked type " + type.ToString());
                scg.ilGen.Emit(errorAt, OpCodes.Ldnull);
            }
            else
            {
                scg.ilGen.Emit(errorAt, OpCodes.Ldloca, localBuilder);
            }
        }

        public override void PopPre(ScriptCodeGen scg, Token errorAt)
        {
            if(type.ToHeapTrackerType() != null)
            {
                scg.ilGen.Emit(errorAt, OpCodes.Ldloc, localBuilder);
            }
        }
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
            if(type.ToHeapTrackerType() != null)
            {
                type.CallHeapTrackerPopMeth(errorAt, scg.ilGen);
            }
            else
            {
                scg.ilGen.Emit(errorAt, OpCodes.Stloc, localBuilder);
            }
        }

        public void Pop(ScriptCodeGen scg, Token errorAt)
        {
            if(type.ToHeapTrackerType() != null)
            {
                /*
                 * Popping into a heap tracker wrapped local variable.
                 * First pop value into a temp var, then call the heap tracker's pop method.
                 */
                ScriptMyLocal htpop = scg.ilGen.DeclareLocal(type.ToSysType(), "htpop$" + (++htpopseq).ToString());
                scg.ilGen.Emit(errorAt, OpCodes.Stloc, htpop);
                scg.ilGen.Emit(errorAt, OpCodes.Ldloc, localBuilder);
                scg.ilGen.Emit(errorAt, OpCodes.Ldloc, htpop);
                type.CallHeapTrackerPopMeth(errorAt, scg.ilGen);
                scg.HeapLocals.Add(htpop);
            }
            else
            {

                /*
                 * Not a heap-tracked local var, just pop directly into it.
                 */
                scg.ilGen.Emit(errorAt, OpCodes.Stloc, localBuilder);
            }
        }

        // non-trivial because it needs to be copied into a temp
        // in case the idiot does dumb-ass side effects tricks
        //   eg,  (x = 0) + x + 2
        //   should read old value of x not 0
        // but if 'xmroption norighttoleft;' in effect,
        // we can read it in any order so reading a
        // local variable is trivial.
        public override bool IsReadTrivial(ScriptCodeGen scg, Token readAt)
        {
            return readAt.nr2l;
        }
    }

    // The value is a null
    public class CompValuNull: CompValu
    {
        public CompValuNull(TokenType type) : base(type) { }
        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Ldnull);
        }
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            throw new Exception("cannot get null's address");
        }
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
            throw new Exception("cannot store into null");
        }
    }

    // The value is a rotation
    public class CompValuRot: CompValu
    {
        public CompValu x;
        public CompValu y;
        public CompValu z;
        public CompValu w;

        private static readonly ConstructorInfo lslRotConstructorInfo =
                typeof(LSL_Rotation).GetConstructor(new Type[] { typeof (double),
                                                                   typeof (double),
                                                                   typeof (double),
                                                                   typeof (double) });

        public CompValuRot(TokenType type, CompValu x, CompValu y, CompValu z, CompValu w) :
                base(type)
        {
            if(!(type is TokenTypeRot))
            {
                this.type = new TokenTypeRot(type);
            }
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }
        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
            this.x.PushVal(scg, errorAt, new TokenTypeFloat(this.x.type));
            this.y.PushVal(scg, errorAt, new TokenTypeFloat(this.y.type));
            this.z.PushVal(scg, errorAt, new TokenTypeFloat(this.z.type));
            this.w.PushVal(scg, errorAt, new TokenTypeFloat(this.w.type));
            scg.ilGen.Emit(errorAt, OpCodes.Newobj, lslRotConstructorInfo);
        }
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            throw new Exception("cannot get constant's address");
        }
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
            throw new Exception("cannot store into constant");
        }

        public override bool IsReadTrivial(ScriptCodeGen scg, Token readAt)
        {
            // the supplied values must be trivial because when we call their PushVal()s
            // there will be stuff on the stack for all but the first PushVal() and so
            // they would have a non-empty stack at their call label.
            if(!this.w.IsReadTrivial(scg, readAt) ||
                !this.x.IsReadTrivial(scg, readAt) ||
                !this.y.IsReadTrivial(scg, readAt) ||
                !this.z.IsReadTrivial(scg, readAt))
            {
                throw new Exception("rotation values must be trivial");
            }

            return true;
        }
    }

    // The value is in a static field of an internally defined struct/class
    public class CompValuSField: CompValu
    {
        public FieldInfo field;

        public CompValuSField(TokenType type, FieldInfo field) : base(type)
        {
            this.field = field;
        }
        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
            if((field.Attributes & FieldAttributes.Literal) == 0)
            {
                scg.ilGen.Emit(errorAt, OpCodes.Ldsfld, field);
                return;
            }
            if(field.FieldType == typeof(LSL_Rotation))
            {
                LSL_Rotation rot = (LSL_Rotation)field.GetValue(null);
                scg.ilGen.Emit(errorAt, OpCodes.Ldc_R8, rot.x);
                scg.ilGen.Emit(errorAt, OpCodes.Ldc_R8, rot.y);
                scg.ilGen.Emit(errorAt, OpCodes.Ldc_R8, rot.z);
                scg.ilGen.Emit(errorAt, OpCodes.Ldc_R8, rot.s);
                scg.ilGen.Emit(errorAt, OpCodes.Newobj, ScriptCodeGen.lslRotationConstructorInfo);
                return;
            }
            if(field.FieldType == typeof(LSL_Vector))
            {
                LSL_Vector vec = (LSL_Vector)field.GetValue(null);
                scg.ilGen.Emit(errorAt, OpCodes.Ldc_R8, vec.x);
                scg.ilGen.Emit(errorAt, OpCodes.Ldc_R8, vec.y);
                scg.ilGen.Emit(errorAt, OpCodes.Ldc_R8, vec.z);
                scg.ilGen.Emit(errorAt, OpCodes.Newobj, ScriptCodeGen.lslRotationConstructorInfo);
                return;
            }
            if(field.FieldType == typeof(string))
            {
                string str = (string)field.GetValue(null);
                scg.ilGen.Emit(errorAt, OpCodes.Ldstr, str);
                return;
            }
            throw new Exception("unsupported literal type " + field.FieldType.Name);
        }
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            if((field.Attributes & FieldAttributes.Literal) != 0)
            {
                throw new Exception("can't write a constant");
            }
            scg.ilGen.Emit(errorAt, OpCodes.Ldflda, field);
        }
        public override void PopPre(ScriptCodeGen scg, Token errorAt)
        {
        }
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
            if((field.Attributes & FieldAttributes.Literal) != 0)
            {
                throw new Exception("can't write a constant");
            }
            scg.ilGen.Emit(errorAt, OpCodes.Stsfld, field);
        }

        // non-trivial because it needs to be copied into a temp
        // in case the idiot does dumb-ass side effects tricks
        //   eg,  (x = 0) + x + 2
        //   should read old value of x not 0
        // but if 'xmroption norighttoleft;' in effect,
        // we can read it in any order so reading a
        // local variable is trivial.
        public override bool IsReadTrivial(ScriptCodeGen scg, Token readAt)
        {
            return readAt.nr2l;
        }
    }

    // The value is a character within a string
    public class CompValuStrChr: CompValu
    {
        private static readonly MethodInfo getCharFromStringMethodInfo =
                 typeof(CompValuStrChr).GetMethod("GetCharFromString", new Type[] { typeof(string), typeof(int) });

        private CompValu theString;
        private CompValu subscript;

        public CompValuStrChr(TokenType type, CompValu theString, CompValu subscript) : base(type)
        {
            this.theString = theString;
            this.subscript = subscript;
        }
        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
            theString.PushVal(scg, errorAt, new TokenTypeStr(type));
            subscript.PushVal(scg, errorAt, new TokenTypeInt(type));
            scg.ilGen.Emit(errorAt, OpCodes.Call, getCharFromStringMethodInfo);
        }
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            throw new Exception("cannot get string character's address");
        }
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
            scg.ErrorMsg(errorAt, "cannot store into string character");
            scg.ilGen.Emit(errorAt, OpCodes.Pop);
        }

        public static char GetCharFromString(string s, int i)
        {
            return s[i];
        }
    }

    // The value is a key or string constant
    public class CompValuString: CompValu
    {
        public string x;

        public CompValuString(TokenType type, string x) : base(type)
        {
            if(!(type is TokenTypeKey) && !(this.type is TokenTypeStr))
            {
                throw new Exception("bad type " + type.ToString());
            }
            this.x = x;
        }
        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Ldstr, x);
        }
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            throw new Exception("cannot get constant's address");
        }
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
            throw new Exception("cannot store into constant");
        }
    }

    // The value is kept in a temp local variable
    public class CompValuTemp: CompValu
    {
        public ScriptMyLocal localBuilder;

        public CompValuTemp(TokenType type, ScriptCodeGen scg) : base(type)
        {
            string name = "tmp$" + (++scg.tempCompValuNum);
            this.localBuilder = scg.ilGen.DeclareLocal(ToSysType(), name);
        }
        protected CompValuTemp(TokenType type) : base(type) { }  // CompValuVoid uses this

        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Ldloc, localBuilder);
        }
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Ldloca, localBuilder);
        }
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Stloc, localBuilder);
        }
        public void Pop(ScriptCodeGen scg, Token errorAt, TokenType stackType)
        {
            TypeCast.CastTopOfStack(scg, errorAt, stackType, this.type, false);
            this.PopPost(scg, errorAt);  // in case PopPost() overridden eg by CompValuVoid
        }
        public void Pop(ScriptCodeGen scg, Token errorAt)
        {
            this.PopPost(scg, errorAt);  // in case PopPost() overridden eg by CompValuVoid
        }
    }

    // The value is a vector
    public class CompValuVec: CompValu
    {
        public CompValu x;
        public CompValu y;
        public CompValu z;

        private static readonly ConstructorInfo lslVecConstructorInfo =
                typeof(LSL_Vector).GetConstructor(new Type[] { typeof (double),
                                                                 typeof (double),
                                                                 typeof (double) });

        public CompValuVec(TokenType type, CompValu x, CompValu y, CompValu z) : base(type)
        {
            if(!(type is TokenTypeVec))
            {
                this.type = new TokenTypeVec(type);
            }
            this.x = x;
            this.y = y;
            this.z = z;
        }
        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
            this.x.PushVal(scg, errorAt, new TokenTypeFloat(this.x.type));
            this.y.PushVal(scg, errorAt, new TokenTypeFloat(this.y.type));
            this.z.PushVal(scg, errorAt, new TokenTypeFloat(this.z.type));
            scg.ilGen.Emit(errorAt, OpCodes.Newobj, lslVecConstructorInfo);
        }
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            throw new Exception("cannot get constant's address");
        }
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
            throw new Exception("cannot store into constant");
        }

        public override bool IsReadTrivial(ScriptCodeGen scg, Token readAt)
        {
            // the supplied values must be trivial because when we call their PushVal()s
            // there will be stuff on the stack for all but the first PushVal() and so
            // they would have a non-empty stack at their call label.
            if(!this.x.IsReadTrivial(scg, readAt) ||
                !this.y.IsReadTrivial(scg, readAt) ||
                !this.z.IsReadTrivial(scg, readAt))
            {
                throw new Exception("vector values must be trivial");
            }

            return true;
        }
    }

    // Used to indicate value will be discarded (eg, where to put return value from a call)
    public class CompValuVoid: CompValuTemp
    {
        public CompValuVoid(Token token) : base((token is TokenTypeVoid) ? (TokenTypeVoid)token : new TokenTypeVoid(token))
        {
        }
        public override void PushVal(ScriptCodeGen scg, Token errorAt)
        {
        }
        public override void PushRef(ScriptCodeGen scg, Token errorAt)
        {
            throw new Exception("cannot get void address");
        }
        public override void PopPost(ScriptCodeGen scg, Token errorAt)
        {
        }
    }
}
