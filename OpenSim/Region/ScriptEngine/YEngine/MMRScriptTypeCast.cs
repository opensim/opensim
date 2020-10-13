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
using System.Reflection;
using System.Reflection.Emit;
using System.Globalization;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

/**
 * @brief Generate script object code to perform type casting
 */

namespace OpenSim.Region.ScriptEngine.Yengine
{

    public class TypeCast
    {
        private delegate void CastDelegate(IScriptCodeGen scg, Token errorAt);

        private static ConstructorInfo floatConstructorStringInfo = typeof(LSL_Float).GetConstructor(new Type[] { typeof(string) });
        private static ConstructorInfo integerConstructorStringInfo = typeof(LSL_Integer).GetConstructor(new Type[] { typeof(string) });
        private static ConstructorInfo lslFloatConstructorInfo = typeof(LSL_Float).GetConstructor(new Type[] { typeof(double) });
        private static ConstructorInfo lslIntegerConstructorInfo = typeof(LSL_Integer).GetConstructor(new Type[] { typeof(int) });
        private static ConstructorInfo lslStringConstructorInfo = typeof(LSL_String).GetConstructor(new Type[] { typeof(string) });
        private static ConstructorInfo rotationConstrucorStringInfo = typeof(LSL_Rotation).GetConstructor(new Type[] { typeof(string) });
        private static ConstructorInfo vectorConstrucorStringInfo = typeof(LSL_Vector).GetConstructor(new Type[] { typeof(string) });
        private static FieldInfo lslFloatValueFieldInfo = typeof(LSL_Float).GetField("value");
        private static FieldInfo lslIntegerValueFieldInfo = typeof(LSL_Integer).GetField("value");
        private static FieldInfo lslStringValueFieldInfo = typeof(LSL_String).GetField("m_string");
        private static FieldInfo sdtcITableFieldInfo = typeof(XMRSDTypeClObj).GetField("sdtcITable");
        private static MethodInfo boolToListMethodInfo = typeof(TypeCast).GetMethod("BoolToList", new Type[] { typeof(bool) });
        private static MethodInfo boolToStringMethodInfo = typeof(TypeCast).GetMethod("BoolToString", new Type[] { typeof(bool) });
        private static MethodInfo charToStringMethodInfo = typeof(TypeCast).GetMethod("CharToString", new Type[] { typeof(char) });
        private static MethodInfo excToStringMethodInfo = typeof(TypeCast).GetMethod("ExceptionToString", new Type[] { typeof(Exception), typeof(XMRInstAbstract) });
        private static MethodInfo floatToStringMethodInfo = typeof(TypeCast).GetMethod("FloatToString", new Type[] { typeof(double) });
        private static MethodInfo intToStringMethodInfo = typeof(TypeCast).GetMethod("IntegerToString", new Type[] { typeof(int) });
        private static MethodInfo keyToBoolMethodInfo = typeof(TypeCast).GetMethod("KeyToBool", new Type[] { typeof(string) });
        private static MethodInfo listToBoolMethodInfo = typeof(TypeCast).GetMethod("ListToBool", new Type[] { typeof(LSL_List) });
        private static MethodInfo listToStringMethodInfo = typeof(TypeCast).GetMethod("ListToString", new Type[] { typeof(LSL_List) });
        private static MethodInfo objectToFloatMethodInfo = typeof(TypeCast).GetMethod("ObjectToFloat", new Type[] { typeof(object) });
        private static MethodInfo objectToIntegerMethodInfo = typeof(TypeCast).GetMethod("ObjectToInteger", new Type[] { typeof(object) });
        private static MethodInfo objectToListMethodInfo = typeof(TypeCast).GetMethod("ObjectToList", new Type[] { typeof(object) });
        private static MethodInfo objectToRotationMethodInfo = typeof(TypeCast).GetMethod("ObjectToRotation", new Type[] { typeof(object) });
        private static MethodInfo objectToStringMethodInfo = typeof(TypeCast).GetMethod("ObjectToString", new Type[] { typeof(object) });
        private static MethodInfo objectToVectorMethodInfo = typeof(TypeCast).GetMethod("ObjectToVector", new Type[] { typeof(object) });
        private static MethodInfo rotationToBoolMethodInfo = typeof(TypeCast).GetMethod("RotationToBool", new Type[] { typeof(LSL_Rotation) });
        private static MethodInfo rotationToStringMethodInfo = typeof(TypeCast).GetMethod("RotationToString", new Type[] { typeof(LSL_Rotation) });
        private static MethodInfo stringToBoolMethodInfo = typeof(TypeCast).GetMethod("StringToBool", new Type[] { typeof(string) });
        private static MethodInfo vectorToBoolMethodInfo = typeof(TypeCast).GetMethod("VectorToBool", new Type[] { typeof(LSL_Vector) });
        private static MethodInfo vectorToStringMethodInfo = typeof(TypeCast).GetMethod("VectorToString", new Type[] { typeof(LSL_Vector) });
        private static MethodInfo sdTypeClassCastClass2ClassMethodInfo = typeof(XMRSDTypeClObj).GetMethod("CastClass2Class", new Type[] { typeof(object), typeof(int) });
        private static MethodInfo sdTypeClassCastIFace2ClassMethodInfo = typeof(XMRSDTypeClObj).GetMethod("CastIFace2Class", new Type[] { typeof(Delegate[]), typeof(int) });
        private static MethodInfo sdTypeClassCastObj2IFaceMethodInfo = typeof(XMRSDTypeClObj).GetMethod("CastObj2IFace", new Type[] { typeof(object), typeof(string) });
        private static MethodInfo charToListMethodInfo = typeof(TypeCast).GetMethod("CharToList", new Type[] { typeof(char) });
        private static MethodInfo excToListMethodInfo = typeof(TypeCast).GetMethod("ExcToList", new Type[] { typeof(Exception) });
        private static MethodInfo vectorToListMethodInfo = typeof(TypeCast).GetMethod("VectorToList", new Type[] { typeof(LSL_Vector) });
        private static MethodInfo floatToListMethodInfo = typeof(TypeCast).GetMethod("FloatToList", new Type[] { typeof(double) });
        private static MethodInfo integerToListMethodInfo = typeof(TypeCast).GetMethod("IntegerToList", new Type[] { typeof(int) });
        private static MethodInfo rotationToListMethodInfo = typeof(TypeCast).GetMethod("RotationToList", new Type[] { typeof(LSL_Rotation) });
        private static MethodInfo stringToListMethodInfo = typeof(TypeCast).GetMethod("StringToList", new Type[] { typeof(string) });

        /*
         * List of all allowed type casts and how to perform the casting.
         */
        private static Dictionary<string, CastDelegate> legalTypeCasts = CreateLegalTypeCasts();

        /**
         * @brief create a dictionary of legal type casts.
         * Defines what EXPLICIT type casts are allowed in addition to the IMPLICIT ones.
         * Key is of the form <oldtype> <newtype> for IMPLICIT casting.
         * Key is of the form <oldtype>*<newtype> for EXPLICIT casting.
         * Value is a delegate that generates code to perform the type cast.
         */
        private static Dictionary<string, CastDelegate> CreateLegalTypeCasts()
        {
            Dictionary<string, CastDelegate> ltc = new Dictionary<string, CastDelegate>();

            // IMPLICIT type casts (a space is in middle of the key)
            // EXPLICIT type casts (an * is in middle of the key)
            // In general, only mark explicit if it might throw an exception
            ltc.Add("array object", TypeCastArray2Object);
            ltc.Add("bool float", TypeCastBool2Float);
            ltc.Add("bool integer", TypeCastBool2Integer);
            ltc.Add("bool list", TypeCastBool2List);
            ltc.Add("bool object", TypeCastBool2Object);
            ltc.Add("bool string", TypeCastBool2String);
            ltc.Add("char integer", TypeCastChar2Integer);
            ltc.Add("char list", TypeCastChar2List);
            ltc.Add("char object", TypeCastChar2Object);
            ltc.Add("char string", TypeCastChar2String);
            ltc.Add("exception list", TypeCastExc2List);
            ltc.Add("exception object", TypeCastExc2Object);
            ltc.Add("exception string", TypeCastExc2String);
            ltc.Add("float bool", TypeCastFloat2Bool);
            ltc.Add("float integer", TypeCastFloat2Integer);
            ltc.Add("float list", TypeCastFloat2List);
            ltc.Add("float object", TypeCastFloat2Object);
            ltc.Add("float string", TypeCastFloat2String);
            ltc.Add("integer bool", TypeCastInteger2Bool);
            ltc.Add("integer char", TypeCastInteger2Char);
            ltc.Add("integer float", TypeCastInteger2Float);
            ltc.Add("integer list", TypeCastInteger2List);
            ltc.Add("integer object", TypeCastInteger2Object);
            ltc.Add("integer string", TypeCastInteger2String);
            ltc.Add("list bool", TypeCastList2Bool);
            ltc.Add("list object", TypeCastList2Object);
            ltc.Add("list string", TypeCastList2String);
            ltc.Add("object*array", TypeCastObject2Array);
            ltc.Add("object*bool", TypeCastObject2Bool);
            ltc.Add("object*char", TypeCastObject2Char);
            ltc.Add("object*exception", TypeCastObject2Exc);
            ltc.Add("object*float", TypeCastObject2Float);
            ltc.Add("object*integer", TypeCastObject2Integer);
            ltc.Add("object*list", TypeCastObject2List);
            ltc.Add("object*rotation", TypeCastObject2Rotation);
            ltc.Add("object string", TypeCastObject2String);
            ltc.Add("object*vector", TypeCastObject2Vector);
            ltc.Add("rotation bool", TypeCastRotation2Bool);
            ltc.Add("rotation list", TypeCastRotation2List);
            ltc.Add("rotation object", TypeCastRotation2Object);
            ltc.Add("rotation string", TypeCastRotation2String);
            ltc.Add("string bool", TypeCastString2Bool);
            ltc.Add("string float", TypeCastString2Float);
            ltc.Add("string integer", TypeCastString2Integer);
            ltc.Add("string list", TypeCastString2List);
            ltc.Add("string object", TypeCastString2Object);
            ltc.Add("string rotation", TypeCastString2Rotation);
            ltc.Add("string vector", TypeCastString2Vector);
            ltc.Add("vector bool", TypeCastVector2Bool);
            ltc.Add("vector list", TypeCastVector2List);
            ltc.Add("vector object", TypeCastVector2Object);
            ltc.Add("vector string", TypeCastVector2String);

            return ltc;
        }

        /**
         * @brief See if the given type can be cast to the other implicitly.
         * @param dstType = type being cast to
         * @param srcType = type being cast from
         * @returns false: implicit cast not allowed
         *           true: implicit cast allowed
         */
        public static bool IsAssignableFrom(TokenType dstType, TokenType srcType)
        {
             // Do a 'dry run' of the casting operation, discarding any emits and not printing any errors.
             // But if the casting tries to print error(s), return false.
             // Otherwise assume the cast is allowed and return true.
            SCGIAF scg = new SCGIAF();
            scg.ok = true;
            scg._ilGen = migiaf;
            CastTopOfStack(scg, null, srcType, dstType, false);
            return scg.ok;
        }

        private struct SCGIAF: IScriptCodeGen
        {
            public bool ok;
            public ScriptMyILGen _ilGen;

            // IScriptCodeGen
            public ScriptMyILGen ilGen
            {
                get
                {
                    return _ilGen;
                }
            }
            public void ErrorMsg(Token token, string message)
            {
                ok = false;
            }
            public void PushDefaultValue(TokenType type)
            {
            }
            public void PushXMRInst()
            {
            }
        }

        private static readonly MIGIAF migiaf = new MIGIAF();
        private struct MIGIAF: ScriptMyILGen
        {
            // ScriptMyILGen
            public string methName
            {
                get
                {
                    return null;
                }
            }
            public ScriptMyLocal DeclareLocal(Type type, string name)
            {
                return null;
            }
            public ScriptMyLabel DefineLabel(string name)
            {
                return null;
            }
            public void BeginExceptionBlock()
            {
            }
            public void BeginCatchBlock(Type excType)
            {
            }
            public void BeginFinallyBlock()
            {
            }
            public void EndExceptionBlock()
            {
            }
            public void Emit(Token errorAt, OpCode opcode)
            {
            }
            public void Emit(Token errorAt, OpCode opcode, FieldInfo field)
            {
            }
            public void Emit(Token errorAt, OpCode opcode, ScriptMyLocal myLocal)
            {
            }
            public void Emit(Token errorAt, OpCode opcode, Type type)
            {
            }
            public void Emit(Token errorAt, OpCode opcode, ScriptMyLabel myLabel)
            {
            }
            public void Emit(Token errorAt, OpCode opcode, ScriptMyLabel[] myLabels)
            {
            }
            public void Emit(Token errorAt, OpCode opcode, ScriptObjWriter method)
            {
            }
            public void Emit(Token errorAt, OpCode opcode, MethodInfo method)
            {
            }
            public void Emit(Token errorAt, OpCode opcode, ConstructorInfo ctor)
            {
            }
            public void Emit(Token errorAt, OpCode opcode, double value)
            {
            }
            public void Emit(Token errorAt, OpCode opcode, float value)
            {
            }
            public void Emit(Token errorAt, OpCode opcode, int value)
            {
            }
            public void Emit(Token errorAt, OpCode opcode, string value)
            {
            }
            public void MarkLabel(ScriptMyLabel myLabel)
            {
            }
        }

        /**
         * @brief Emit code that converts the top stack item from 'oldType' to 'newType'
         * @param scg = what script we are compiling
         * @param errorAt = token used for source location for error messages
         * @param oldType = type of item currently on the stack
         * @param newType = type to convert it to
         * @param explicitAllowed = false: only consider implicit casts
         *                           true: consider both implicit and explicit casts
         * @returns with code emitted for conversion (or error message output if not allowed, and stack left unchanged)
         */
        public static void CastTopOfStack(IScriptCodeGen scg, Token errorAt, TokenType oldType, TokenType newType, bool explicitAllowed)
        {
            CastDelegate castDelegate;
            string oldString = oldType.ToString();
            string newString = newType.ToString();

             // 'key' -> 'bool' is the only time we care about key being different than string.
            if((oldString == "key") && (newString == "bool"))
            {
                LSLUnwrap(scg, errorAt, oldType);
                scg.ilGen.Emit(errorAt, OpCodes.Call, keyToBoolMethodInfo);
                LSLWrap(scg, errorAt, newType);
                return;
            }

             // Treat key and string as same type for all other type casts.
            if(oldString == "key")
                oldString = "string";
            if(newString == "key")
                newString = "string";

             // If the types are the same, there is no conceptual casting needed.
             // However, there may be wraping/unwraping to/from the LSL wrappers.
            if(oldString == newString)
            {
                if(oldType.ToLSLWrapType() != newType.ToLSLWrapType())
                {
                    LSLUnwrap(scg, errorAt, oldType);
                    LSLWrap(scg, errorAt, newType);
                }
                return;
            }

             // Script-defined classes can be cast up and down the tree.
            if((oldType is TokenTypeSDTypeClass) && (newType is TokenTypeSDTypeClass))
            {
                TokenDeclSDTypeClass oldSDTC = ((TokenTypeSDTypeClass)oldType).decl;
                TokenDeclSDTypeClass newSDTC = ((TokenTypeSDTypeClass)newType).decl;

                // implicit cast allowed from leaf toward root
                for(TokenDeclSDTypeClass sdtc = oldSDTC; sdtc != null; sdtc = sdtc.extends)
                {
                    if(sdtc == newSDTC)
                        return;
                }

                // explicit cast allowed from root toward leaf
                for(TokenDeclSDTypeClass sdtc = newSDTC; sdtc != null; sdtc = sdtc.extends)
                {
                    if(sdtc == oldSDTC)
                    {
                        ExplCheck(scg, errorAt, explicitAllowed, oldString, newString);
                        scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4, newSDTC.sdTypeIndex);
                        scg.ilGen.Emit(errorAt, OpCodes.Call, sdTypeClassCastClass2ClassMethodInfo);
                        return;
                    }
                }

                // not on same branch
                goto illcast;
            }

             // One script-defined interface type cannot be cast to another script-defined interface type, 
             // unless the old interface declares that it implements the new interface.  That proves that 
             // the underlying object, no matter what type, implements the new interface.
            if((oldType is TokenTypeSDTypeInterface) && (newType is TokenTypeSDTypeInterface))
            {
                TokenDeclSDTypeInterface oldDecl = ((TokenTypeSDTypeInterface)oldType).decl;
                TokenDeclSDTypeInterface newDecl = ((TokenTypeSDTypeInterface)newType).decl;
                if(!oldDecl.Implements(newDecl))
                    goto illcast;
                scg.ilGen.Emit(errorAt, OpCodes.Ldstr, newType.ToString());
                scg.ilGen.Emit(errorAt, OpCodes.Call, sdTypeClassCastObj2IFaceMethodInfo);
                return;
            }

             // A script-defined class type can be implicitly cast to a script-defined interface type that it 
             // implements.  The result is an array of delegates that give the class's implementation of the 
             // various methods defined by the interface.
            if((oldType is TokenTypeSDTypeClass) && (newType is TokenTypeSDTypeInterface))
            {
                TokenDeclSDTypeClass oldSDTC = ((TokenTypeSDTypeClass)oldType).decl;
                int intfIndex;
                if(!oldSDTC.intfIndices.TryGetValue(newType.ToString(), out intfIndex))
                    goto illcast;
                scg.ilGen.Emit(errorAt, OpCodes.Ldfld, sdtcITableFieldInfo);
                scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4, intfIndex);
                scg.ilGen.Emit(errorAt, OpCodes.Ldelem, typeof(Delegate[]));
                return;
            }

             // A script-defined interface type can be explicitly cast to a script-defined class type by 
             // extracting the Target property from element 0 of the delegate array that is the interface
             // object and making sure it casts to the correct script-defined class type.
             //
             // But then only if the class type implements the interface type.
            if((oldType is TokenTypeSDTypeInterface) && (newType is TokenTypeSDTypeClass))
            {
                TokenTypeSDTypeInterface oldSDTI = (TokenTypeSDTypeInterface)oldType;
                TokenTypeSDTypeClass newSDTC = (TokenTypeSDTypeClass)newType;

                if(!newSDTC.decl.CanCastToIntf(oldSDTI.decl))
                    goto illcast;

                ExplCheck(scg, errorAt, explicitAllowed, oldString, newString);
                scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4, newSDTC.decl.sdTypeIndex);
                scg.ilGen.Emit(errorAt, OpCodes.Call, sdTypeClassCastIFace2ClassMethodInfo);
                return;
            }

             // A script-defined interface type can be implicitly cast to object.
            if((oldType is TokenTypeSDTypeInterface) && (newType is TokenTypeObject))
            {
                return;
            }

             // An object can be explicitly cast to a script-defined interface.
            if((oldType is TokenTypeObject) && (newType is TokenTypeSDTypeInterface))
            {
                ExplCheck(scg, errorAt, explicitAllowed, oldString, newString);
                scg.ilGen.Emit(errorAt, OpCodes.Ldstr, newString);
                scg.ilGen.Emit(errorAt, OpCodes.Call, sdTypeClassCastObj2IFaceMethodInfo);
                return;
            }

             // Cast to void is always allowed, such as discarding value from 'i++' or function return value.
            if(newType is TokenTypeVoid)
            {
                scg.ilGen.Emit(errorAt, OpCodes.Pop);
                return;
            }

             // Cast from undef to object or script-defined type is always allowed.
            if((oldType is TokenTypeUndef) &&
                ((newType is TokenTypeObject) ||
                 (newType is TokenTypeSDTypeClass) ||
                 (newType is TokenTypeSDTypeInterface)))
            {
                return;
            }

             // Script-defined classes can be implicitly cast to objects.
            if((oldType is TokenTypeSDTypeClass) && (newType is TokenTypeObject))
            {
                return;
            }

             // Script-defined classes can be explicitly cast from objects and other script-defined classes.
             // Note that we must manually check that it is the correct SDTypeClass however because as far as 
             // mono is concerned, all SDTypeClass's are the same.
            if((oldType is TokenTypeObject) && (newType is TokenTypeSDTypeClass))
            {
                ExplCheck(scg, errorAt, explicitAllowed, oldString, newString);
                scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4, ((TokenTypeSDTypeClass)newType).decl.sdTypeIndex);
                scg.ilGen.Emit(errorAt, OpCodes.Call, sdTypeClassCastClass2ClassMethodInfo);
                return;
            }

             // Delegates can be implicitly cast to/from objects.
            if((oldType is TokenTypeSDTypeDelegate) && (newType is TokenTypeObject))
            {
                return;
            }
            if((oldType is TokenTypeObject) && (newType is TokenTypeSDTypeDelegate))
            {
                scg.ilGen.Emit(errorAt, OpCodes.Castclass, newType.ToSysType());
                return;
            }

             // Some actual conversion is needed, see if it is in table of legal casts.
            string key = oldString + " " + newString;
            if(!legalTypeCasts.TryGetValue(key, out castDelegate))
            {
                key = oldString + "*" + newString;
                if(!legalTypeCasts.TryGetValue(key, out castDelegate))
                    goto illcast;
                ExplCheck(scg, errorAt, explicitAllowed, oldString, newString);
            }

             // Ok, output cast.  But make sure it is in native form without any LSL wrapping
             // before passing to our casting routine.  Then if caller is expecting an LSL-
             // wrapped value on the stack upon return, wrap it up after our casting.
            LSLUnwrap(scg, errorAt, oldType);
            castDelegate(scg, errorAt);
            LSLWrap(scg, errorAt, newType);
            return;

            illcast:
            scg.ErrorMsg(errorAt, "illegal to cast from " + oldString + " to " + newString);
            if(!(oldType is TokenTypeVoid))
                scg.ilGen.Emit(errorAt, OpCodes.Pop);
            scg.PushDefaultValue(newType);
        }
        private static void ExplCheck(IScriptCodeGen scg, Token errorAt, bool explicitAllowed, string oldString, string newString)
        {
            if(!explicitAllowed)
            {
                scg.ErrorMsg(errorAt, "must explicitly cast from " + oldString + " to " + newString);
            }
        }

        /**
         * @brief If value on the stack is an LSL-style wrapped value, unwrap it.
         */
        public static void LSLUnwrap(IScriptCodeGen scg, Token errorAt, TokenType type)
        {
            if(type.ToLSLWrapType() == typeof(LSL_Float))
            {
                scg.ilGen.Emit(errorAt, OpCodes.Ldfld, lslFloatValueFieldInfo);
            }
            if(type.ToLSLWrapType() == typeof(LSL_Integer))
            {
                scg.ilGen.Emit(errorAt, OpCodes.Ldfld, lslIntegerValueFieldInfo);
            }
            if(type.ToLSLWrapType() == typeof(LSL_String))
            {
                scg.ilGen.Emit(errorAt, OpCodes.Ldfld, lslStringValueFieldInfo);
            }
        }

        /**
         * @brief If caller wants the unwrapped value on stack wrapped LSL-style, wrap it.
         */
        private static void LSLWrap(IScriptCodeGen scg, Token errorAt, TokenType type)
        {
            if(type.ToLSLWrapType() == typeof(LSL_Float))
            {
                scg.ilGen.Emit(errorAt, OpCodes.Newobj, lslFloatConstructorInfo);
            }
            if(type.ToLSLWrapType() == typeof(LSL_Integer))
            {
                scg.ilGen.Emit(errorAt, OpCodes.Newobj, lslIntegerConstructorInfo);
            }
            if(type.ToLSLWrapType() == typeof(LSL_String))
            {
                scg.ilGen.Emit(errorAt, OpCodes.Newobj, lslStringConstructorInfo);
            }
        }

        /**
         * @brief These routines output code to perform casting.
         *        They can assume there are no LSL wrapped values on input
         *        and they should not output an LSL wrapped value.
         */
        private static void TypeCastArray2Object(IScriptCodeGen scg, Token errorAt)
        {
        }
        private static void TypeCastBool2Float(IScriptCodeGen scg, Token errorAt)
        {
            if(typeof(double) == typeof(float))
            {
                scg.ilGen.Emit(errorAt, OpCodes.Conv_R4);
            }
            else if(typeof(double) == typeof(double))
            {
                scg.ilGen.Emit(errorAt, OpCodes.Conv_R8);
            }
            else
            {
                throw new Exception("unknown type");
            }
        }
        private static void TypeCastBool2Integer(IScriptCodeGen scg, Token errorAt)
        {
        }
        private static void TypeCastBool2Object(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Box, typeof(bool));
        }
        private static void TypeCastChar2Integer(IScriptCodeGen scg, Token errorAt)
        {
        }
        private static void TypeCastChar2List(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, charToListMethodInfo);
        }
        private static void TypeCastChar2Object(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Box, typeof(char));
        }
        private static void TypeCastChar2String(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, charToStringMethodInfo);
        }
        private static void TypeCastExc2List(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, excToListMethodInfo);
        }
        private static void TypeCastExc2Object(IScriptCodeGen scg, Token errorAt)
        {
        }
        private static void TypeCastExc2String(IScriptCodeGen scg, Token errorAt)
        {
            scg.PushXMRInst();
            scg.ilGen.Emit(errorAt, OpCodes.Call, excToStringMethodInfo);
        }
        private static void TypeCastFloat2Bool(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_R4, 0.0f);
            scg.ilGen.Emit(errorAt, OpCodes.Ceq);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_1);
            scg.ilGen.Emit(errorAt, OpCodes.Xor);
        }
        private static void TypeCastFloat2Integer(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Conv_I4);
        }
        private static void TypeCastFloat2Object(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Box, typeof(double));
        }
        private static void TypeCastInteger2Bool(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_0);
            scg.ilGen.Emit(errorAt, OpCodes.Ceq);
            scg.ilGen.Emit(errorAt, OpCodes.Ldc_I4_1);
            scg.ilGen.Emit(errorAt, OpCodes.Xor);
        }
        private static void TypeCastInteger2Char(IScriptCodeGen scg, Token errorAt)
        {
        }
        private static void TypeCastInteger2Float(IScriptCodeGen scg, Token errorAt)
        {
            if(typeof(double) == typeof(float))
            {
                scg.ilGen.Emit(errorAt, OpCodes.Conv_R4);
            }
            else if(typeof(double) == typeof(double))
            {
                scg.ilGen.Emit(errorAt, OpCodes.Conv_R8);
            }
            else
            {
                throw new Exception("unknown type");
            }
        }
        private static void TypeCastInteger2Object(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Box, typeof(int));
        }
        private static void TypeCastList2Bool(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, listToBoolMethodInfo);
        }
        private static void TypeCastList2Object(IScriptCodeGen scg, Token errorAt)
        {
            if(typeof(LSL_List).IsValueType)
            {
                scg.ilGen.Emit(errorAt, OpCodes.Box, typeof(LSL_List));
            }
        }
        private static void TypeCastObject2Array(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Castclass, typeof(XMR_Array));
        }
        private static void TypeCastObject2Bool(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Unbox_Any, typeof(bool));
        }
        private static void TypeCastObject2Char(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Unbox_Any, typeof(char));
        }
        private static void TypeCastObject2Exc(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Castclass, typeof(Exception));
        }
        private static void TypeCastObject2Float(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, objectToFloatMethodInfo);
        }
        private static void TypeCastObject2Integer(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, objectToIntegerMethodInfo);
        }
        private static void TypeCastObject2List(IScriptCodeGen scg, Token errorAt)
        {
            if(typeof(LSL_List).IsValueType)
            {
                scg.ilGen.Emit(errorAt, OpCodes.Call, objectToListMethodInfo);
            }
            else
            {
                scg.ilGen.Emit(errorAt, OpCodes.Castclass, typeof(LSL_List));
            }
        }
        private static void TypeCastObject2Rotation(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, objectToRotationMethodInfo);
        }
        private static void TypeCastObject2Vector(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, objectToVectorMethodInfo);
        }
        private static void TypeCastRotation2Bool(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, rotationToBoolMethodInfo);
        }
        private static void TypeCastRotation2Object(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Box, typeof(LSL_Rotation));
        }
        private static void TypeCastString2Bool(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, stringToBoolMethodInfo);
        }
        private static void TypeCastString2Object(IScriptCodeGen scg, Token errorAt)
        {
        }
        private static void TypeCastString2Rotation(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Newobj, rotationConstrucorStringInfo);
        }
        private static void TypeCastString2Vector(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Newobj, vectorConstrucorStringInfo);
        }
        private static void TypeCastVector2Bool(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, vectorToBoolMethodInfo);
        }
        private static void TypeCastVector2List(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, vectorToListMethodInfo);
        }
        private static void TypeCastVector2Object(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Box, typeof(LSL_Vector));
        }
        private static void TypeCastBool2List(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, boolToListMethodInfo);
        }
        private static void TypeCastBool2String(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, boolToStringMethodInfo);
        }
        private static void TypeCastFloat2List(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, floatToListMethodInfo);
        }
        private static void TypeCastFloat2String(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, floatToStringMethodInfo);
        }
        private static void TypeCastInteger2List(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, integerToListMethodInfo);
        }
        private static void TypeCastInteger2String(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, intToStringMethodInfo);
        }
        private static void TypeCastList2String(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, listToStringMethodInfo);
        }
        private static void TypeCastObject2String(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, objectToStringMethodInfo);
        }
        private static void TypeCastRotation2List(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, rotationToListMethodInfo);
        }
        private static void TypeCastRotation2String(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, rotationToStringMethodInfo);
        }
        private static void TypeCastString2Float(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Newobj, floatConstructorStringInfo);
            scg.ilGen.Emit(errorAt, OpCodes.Ldfld, lslFloatValueFieldInfo);
        }
        private static void TypeCastString2Integer(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Newobj, integerConstructorStringInfo);
            scg.ilGen.Emit(errorAt, OpCodes.Ldfld, lslIntegerValueFieldInfo);
        }
        private static void TypeCastString2List(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, stringToListMethodInfo);
        }
        private static void TypeCastVector2String(IScriptCodeGen scg, Token errorAt)
        {
            scg.ilGen.Emit(errorAt, OpCodes.Call, vectorToStringMethodInfo);
        }

        /*
         * Because the calls are funky, let the compiler handle them.
         */
        public static bool RotationToBool(LSL_Rotation x)
        {
            return !x.Equals(ScriptBaseClass.ZERO_ROTATION);
        }
        public static bool StringToBool(string x)
        {
            return x.Length > 0;
        }
        public static bool VectorToBool(LSL_Vector x)
        {
            return !x.Equals(ScriptBaseClass.ZERO_VECTOR);
        }
        public static string BoolToString(bool x)
        {
            return x ? "1" : "0";
        }
        public static string CharToString(char x)
        {
            return x.ToString();
        }
        public static string FloatToString(double x)
        {
            return x.ToString("0.000000",CultureInfo.InvariantCulture);
        }
        public static string IntegerToString(int x)
        {
            return x.ToString();
        }
        public static bool KeyToBool(string x)
        {
            return (x != "") && (x != ScriptBaseClass.NULL_KEY);
        }
        public static bool ListToBool(LSL_List x)
        {
            return x.Length != 0;
        }
        public static string ListToString(LSL_List x)
        {
            return x.ToString();
        }
        public static string ObjectToString(object x)
        {
            return (x == null) ? null : x.ToString();
        }
        public static string RotationToString(LSL_Rotation x)
        {
            return x.ToString();
        }
        public static string VectorToString(LSL_Vector x)
        {
            return x.ToString();
        }
        public static LSL_List BoolToList(bool b)
        {
            return new LSL_List(new object[] { new LSL_Integer(b ? 1 : 0) });
        }
        public static LSL_List CharToList(char c)
        {
            return new LSL_List(new object[] { new LSL_Integer(c) });
        }
        public static LSL_List ExcToList(Exception e)
        {
            return new LSL_List(new object[] { e });
        }
        public static LSL_List VectorToList(LSL_Vector v)
        {
            return new LSL_List(new object[] { v });
        }
        public static LSL_List FloatToList(double f)
        {
            return new LSL_List(new object[] { new LSL_Float(f) });
        }
        public static LSL_List IntegerToList(int i)
        {
            return new LSL_List(new object[] { new LSL_Integer(i) });
        }
        public static LSL_List RotationToList(LSL_Rotation r)
        {
            return new LSL_List(new object[] { r });
        }
        public static LSL_List StringToList(string s)
        {
            return new LSL_List(new object[] { new LSL_String(s) });
        }

        public static double ObjectToFloat(object x)
        {
            if(x is LSL_String)
                return double.Parse(((LSL_String)x).m_string);
            if(x is string)
                return double.Parse((string)x);
            if(x is LSL_Float)
                return (double)(LSL_Float)x;
            if(x is LSL_Integer)
                return (double)(int)(LSL_Integer)x;
            if(x is int)
                return (double)(int)x;
            return (double)x;
        }

        public static int ObjectToInteger(object x)
        {
            if(x is LSL_String)
                return int.Parse(((LSL_String)x).m_string);
            if(x is string)
                return int.Parse((string)x);
            if(x is LSL_Integer)
                return (int)(LSL_Integer)x;
            return (int)x;
        }

        public static LSL_List ObjectToList(object x)
        {
            return (LSL_List)x;
        }

        public static LSL_Rotation ObjectToRotation(object x)
        {
            if(x is LSL_String)
                return new LSL_Rotation(((LSL_String)x).m_string);
            if(x is string)
                return new LSL_Rotation((string)x);
            return (LSL_Rotation)x;
        }

        public static LSL_Vector ObjectToVector(object x)
        {
            if(x is LSL_String)
                return new LSL_Vector(((LSL_String)x).m_string);
            if(x is string)
                return new LSL_Vector((string)x);
            return (LSL_Vector)x;
        }

        public static string ExceptionToString(Exception x, XMRInstAbstract inst)
        {
            return XMRInstAbstract.xmrExceptionTypeName(x) + ": " + XMRInstAbstract.xmrExceptionMessage(x) +
                "\n" + inst.xmrExceptionStackTrace(x);
        }

        /*
         * These are used by event handler entrypoints to remove any LSL wrapping
         * from the argument list and return the unboxed/unwrapped value.
         */
        public static double EHArgUnwrapFloat(object x)
        {
            if(x is LSL_Float)
                return (double)(LSL_Float)x;
            return (double)x;
        }

        public static int EHArgUnwrapInteger(object x)
        {
            if(x is LSL_Integer)
                return (int)(LSL_Integer)x;
            return (int)x;
        }

        public static LSL_Rotation EHArgUnwrapRotation(object x)
        {
            if(x is OpenMetaverse.Quaternion)
            {
                OpenMetaverse.Quaternion q = (OpenMetaverse.Quaternion)x;
                return new LSL_Rotation(q.X, q.Y, q.Z, q.W);
            }
            return (LSL_Rotation)x;
        }

        public static string EHArgUnwrapString(object x)
        {
            if(x is LSL_Key)
                return (string)(LSL_Key)x;
            if(x is LSL_String)
                return (string)(LSL_String)x;
            return (string)x;
        }

        public static LSL_Vector EHArgUnwrapVector(object x)
        {
            if(x is OpenMetaverse.Vector3)
            {
                OpenMetaverse.Vector3 v = (OpenMetaverse.Vector3)x;
                return new LSL_Vector(v.X, v.Y, v.Z);
            }
            return (LSL_Vector)x;
        }
    }
}
