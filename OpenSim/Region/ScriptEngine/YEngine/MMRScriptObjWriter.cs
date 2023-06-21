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
using System;
using System.Collections.Generic;
using System.IO;
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
 * @brief Wrapper class for ILGenerator.
 *        It writes the object code to a file and can then make real ILGenerator calls
 *        based on the file's contents.
 */
namespace OpenSim.Region.ScriptEngine.Yengine
{
    public enum ScriptObjWriterCode: byte
    {
        BegMethod, EndMethod, TheEnd,
        DclLabel, DclLocal, DclMethod, MarkLabel,
        EmitNull, EmitField, EmitLocal, EmitType, EmitLabel, EmitMethodExt,
        EmitMethodInt, EmitCtor, EmitDouble, EmitFloat, EmitInteger, EmitString,
        EmitLabels,
        BegExcBlk, BegCatBlk, BegFinBlk, EndExcBlk
    }

    public class ScriptObjWriter: ScriptMyILGen
    {
        private static readonly Dictionary<short, OpCode> opCodes = PopulateOpCodes();
        private static readonly Dictionary<string, Type> string2Type = PopulateS2T();
        private static readonly Dictionary<Type, string> type2String = PopulateT2S();

        private static readonly MethodInfo monoGetCurrentOffset = typeof(ILGenerator).GetMethod("Mono_GetCurrentOffset",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
                        new Type[] { typeof(ILGenerator) }, null);

        private static readonly OpCode[] opCodesLdcI4M1P8 = new OpCode[] {
            OpCodes.Ldc_I4_M1, OpCodes.Ldc_I4_0, OpCodes.Ldc_I4_1, OpCodes.Ldc_I4_2, OpCodes.Ldc_I4_3,
            OpCodes.Ldc_I4_4,  OpCodes.Ldc_I4_5, OpCodes.Ldc_I4_6, OpCodes.Ldc_I4_7, OpCodes.Ldc_I4_8
        };

        private BinaryWriter objFileWriter;
        private string lastErrorAtFile = "";
        private int lastErrorAtLine = 0;
        private int lastErrorAtPosn = 0;

        private readonly Dictionary<Type, string> sdTypesRev = new ();
        public int labelNumber = 0;
        public int localNumber = 0;

        public string methName { get; private set;}

        public Type retType;
        public Type[] argTypes;

        /**
         * @brief Begin function declaration
         * @param sdTypes    = script-defined types
         * @param methName   = name of the method being declared, eg, "Verify(array,list,string)"
         * @param retType    = its return value type
         * @param argTypes[] = its argument types
         * @param objFileWriter  = file to write its object code to
         *
         * After calling this function, the following functions should be called:
         *    this.BegMethod ();
         *      this.<as required> ();
         *    this.EndMethod ();
         *
         * The design of this object is such that many constructors may be called,
         * but once a BegMethod() is called for one of the objects, no method may
         * called for any of the other objects until EndMethod() is called (or it 
         * would break up the object stream for that method).  But we need to have
         * many constructors possible so we get function headers at the beginning
         * of the object file in case there are forward references to the functions.
         */
        public ScriptObjWriter(TokenScript tokenScript, string methName, Type retType, Type[] argTypes, string[] argNames, BinaryWriter objFileWriter)
        {
            this.methName = methName;
            this.retType = retType;
            this.argTypes = argTypes;
            this.objFileWriter = objFileWriter;

            // Build list that translates system-defined types to script defined types.
            foreach(TokenDeclSDType sdt in tokenScript.sdSrcTypesValues)
            {
                Type sys = sdt.GetSysType();
                if(sys != null)
                    sdTypesRev[sys] = sdt.longName.val;
            }

            // This tells the reader to call 'new DynamicMethod()' to create
            // the function header.  Then any forward reference calls to this
            // method will have a MethodInfo struct to call.
            objFileWriter.Write((byte)ScriptObjWriterCode.DclMethod);
            objFileWriter.Write(methName);
            objFileWriter.Write(GetStrFromType(retType));

            int nArgs = argTypes.Length;
            objFileWriter.Write(nArgs);
            for(int i = 0; i < nArgs; i++)
            {
                objFileWriter.Write(GetStrFromType(argTypes[i]));
                objFileWriter.Write(argNames[i]);
            }
        }

        /**
         * @brief Begin outputting object code for the function
         */
        public void BegMethod()
        {
            // This tells the reader to call methodInfo.GetILGenerator()
            // so it can start writing CIL code for the method.
            objFileWriter.Write((byte)ScriptObjWriterCode.BegMethod);
            objFileWriter.Write(methName);
        }

        /**
         * @brief End of object code for the function
         */
        public void EndMethod()
        {
            // This tells the reader that all code for the method has
            // been written and so it will typically call CreateDelegate()
            // to finalize the method and create an entrypoint.
            objFileWriter.Write((byte)ScriptObjWriterCode.EndMethod);

            objFileWriter = null;
        }

        /**
         * @brief Declare a local variable for use by the function
         */
        public ScriptMyLocal DeclareLocal(Type type, string name)
        {
            ScriptMyLocal myLocal = new ()
            {
                type = type,
                name = name,
                number = localNumber++,
                isReferenced = true  // so ScriptCollector won't optimize references away
            };
            return DeclareLocal(myLocal);
        }
        public ScriptMyLocal DeclareLocal(ScriptMyLocal myLocal)
        {
            objFileWriter.Write((byte)ScriptObjWriterCode.DclLocal);
            objFileWriter.Write(myLocal.number);
            objFileWriter.Write(myLocal.name);
            objFileWriter.Write(GetStrFromType(myLocal.type));
            return myLocal;
        }

        /**
         * @brief Define a label for use by the function
         */
        public ScriptMyLabel DefineLabel(string name)
        {
            ScriptMyLabel myLabel = new()
            {
                name = name,
                number = labelNumber++
            };
            return DefineLabel(myLabel);
        }
        public ScriptMyLabel DefineLabel(ScriptMyLabel myLabel)
        {
            objFileWriter.Write((byte)ScriptObjWriterCode.DclLabel);
            objFileWriter.Write(myLabel.number);
            objFileWriter.Write(myLabel.name);
            return myLabel;
        }

        /**
         * @brief try/catch blocks.
         */
        public void BeginExceptionBlock()
        {
            objFileWriter.Write((byte)ScriptObjWriterCode.BegExcBlk);
        }

        public void BeginCatchBlock(Type excType)
        {
            objFileWriter.Write((byte)ScriptObjWriterCode.BegCatBlk);
            objFileWriter.Write(GetStrFromType(excType));
        }

        public void BeginFinallyBlock()
        {
            objFileWriter.Write((byte)ScriptObjWriterCode.BegFinBlk);
        }

        public void EndExceptionBlock()
        {
            objFileWriter.Write((byte)ScriptObjWriterCode.EndExcBlk);
        }

        public void Emit(Token errorAt, OpCode opcode)
        {
            objFileWriter.Write((byte)ScriptObjWriterCode.EmitNull);
            WriteOpCode(errorAt, opcode);
        }

        public void Emit(Token errorAt, OpCode opcode, FieldInfo field)
        {
            objFileWriter.Write((byte)ScriptObjWriterCode.EmitField);
            WriteOpCode(errorAt, opcode);
            objFileWriter.Write(GetStrFromType(field.ReflectedType));
            objFileWriter.Write(field.Name);
        }

        public void Emit(Token errorAt, OpCode opcode, ScriptMyLocal myLocal)
        {
            objFileWriter.Write((byte)ScriptObjWriterCode.EmitLocal);
            WriteOpCode(errorAt, opcode);
            objFileWriter.Write(myLocal.number);
        }

        public void Emit(Token errorAt, OpCode opcode, Type type)
        {
            objFileWriter.Write((byte)ScriptObjWriterCode.EmitType);
            WriteOpCode(errorAt, opcode);
            objFileWriter.Write(GetStrFromType(type));
        }

        public void Emit(Token errorAt, OpCode opcode, ScriptMyLabel myLabel)
        {
            objFileWriter.Write((byte)ScriptObjWriterCode.EmitLabel);
            WriteOpCode(errorAt, opcode);
            objFileWriter.Write(myLabel.number);
        }

        public void Emit(Token errorAt, OpCode opcode, ScriptMyLabel[] myLabels)
        {
            objFileWriter.Write((byte)ScriptObjWriterCode.EmitLabels);
            WriteOpCode(errorAt, opcode);
            int nLabels = myLabels.Length;
            objFileWriter.Write(nLabels);
            for(int i = 0; i < nLabels; i++)
            {
                objFileWriter.Write(myLabels[i].number);
            }
        }

        public void Emit(Token errorAt, OpCode opcode, ScriptObjWriter method)
        {
            if(method == null)
                throw new ArgumentNullException(nameof(method));
            objFileWriter.Write((byte)ScriptObjWriterCode.EmitMethodInt);
            WriteOpCode(errorAt, opcode);
            objFileWriter.Write(method.methName);
        }

        public void Emit(Token errorAt, OpCode opcode, MethodInfo method)
        {
            objFileWriter.Write((byte)ScriptObjWriterCode.EmitMethodExt);
            WriteOpCode(errorAt, opcode);
            objFileWriter.Write(method.Name);
            objFileWriter.Write(GetStrFromType(method.ReflectedType));
            ParameterInfo[] parms = method.GetParameters();
            int nArgs = parms.Length;
            objFileWriter.Write(nArgs);
            for(int i = 0; i < nArgs; i++)
            {
                objFileWriter.Write(GetStrFromType(parms[i].ParameterType));
            }
        }

        public void Emit(Token errorAt, OpCode opcode, ConstructorInfo ctor)
        {
            objFileWriter.Write((byte)ScriptObjWriterCode.EmitCtor);
            WriteOpCode(errorAt, opcode);
            objFileWriter.Write(GetStrFromType(ctor.ReflectedType));
            ParameterInfo[] parms = ctor.GetParameters();
            int nArgs = parms.Length;
            objFileWriter.Write(nArgs);
            for(int i = 0; i < nArgs; i++)
            {
                objFileWriter.Write(GetStrFromType(parms[i].ParameterType));
            }
        }

        public void Emit(Token errorAt, OpCode opcode, double value)
        {
            if(opcode != OpCodes.Ldc_R8)
            {
                throw new Exception("bad opcode " + opcode.ToString());
            }
            objFileWriter.Write((byte)ScriptObjWriterCode.EmitDouble);
            WriteOpCode(errorAt, opcode);
            objFileWriter.Write(value);
        }

        public void Emit(Token errorAt, OpCode opcode, float value)
        {
            if(opcode != OpCodes.Ldc_R4)
            {
                throw new Exception("bad opcode " + opcode.ToString());
            }
            objFileWriter.Write((byte)ScriptObjWriterCode.EmitFloat);
            WriteOpCode(errorAt, opcode);
            objFileWriter.Write(value);
        }

        public void Emit(Token errorAt, OpCode opcode, int value)
        {
            objFileWriter.Write((byte)ScriptObjWriterCode.EmitInteger);
            WriteOpCode(errorAt, opcode);
            objFileWriter.Write(value);
        }

        public void Emit(Token errorAt, OpCode opcode, string value)
        {
            objFileWriter.Write((byte)ScriptObjWriterCode.EmitString);
            WriteOpCode(errorAt, opcode);
            objFileWriter.Write(value);
        }

        /**
         * @brief Declare that the target of a label is the next instruction.
         */
        public void MarkLabel(ScriptMyLabel myLabel)
        {
            objFileWriter.Write((byte)ScriptObjWriterCode.MarkLabel);
            objFileWriter.Write(myLabel.number);
        }

        /**
         * @brief Write end-of-file marker to binary file.
         */
        public static void TheEnd(BinaryWriter objFileWriter)
        {
            objFileWriter.Write((byte)ScriptObjWriterCode.TheEnd);
        }

        /**
         * @brief Take an object file created by ScriptObjWriter() and convert it to a series of dynamic methods.
         * @param sdTypes   = script-defined types
         * @param objReader = where to read object file from (as written by ScriptObjWriter above).
         * @param scriptObjCode.EndMethod = called for each method defined at the end of the methods definition
         * @param objectTokens = write disassemble/decompile data (or null if not wanted)
         */
        public static void CreateObjCode(Dictionary<string, TokenDeclSDType> sdTypes, BinaryReader objReader,
                ScriptObjCode scriptObjCode, ObjectTokens objectTokens)
        {
            Dictionary<string, DynamicMethod> methods = new ();
            DynamicMethod method = null;
            ILGenerator ilGen = null;
            Dictionary<int, Label> labels = new ();
            Dictionary<int, LocalBuilder> locals = new ();
            Dictionary<int, string> labelNames = new ();
            Dictionary<int, string> localNames = new ();
            object[] ilGenArg = new object[1];
            int offset;
            Dictionary<int, ScriptSrcLoc> srcLocs = null;
            string srcFile = "";
            int srcLine = 0;
            int srcPosn = 0;

            while(true)
            {
                // Get IL instruction offset at beginning of instruction.
                offset = 0;
                if((ilGen != null) && (monoGetCurrentOffset != null))
                {
                    offset = (int)monoGetCurrentOffset.Invoke(null, ilGenArg);
                }

                // Read and decode next internal format code from input file (.xmrobj file).
                ScriptObjWriterCode code = (ScriptObjWriterCode)objReader.ReadByte();
                switch(code)
                {
                    // Reached end-of-file so we are all done.
                    case ScriptObjWriterCode.TheEnd:
                        return;

                    // Beginning of method's contents.
                    // Method must have already been declared via DclMethod
                    // so all we need is its name to retrieve from methods[].
                    case ScriptObjWriterCode.BegMethod:
                    {
                        string methName = objReader.ReadString();

                        method = methods[methName];
                        ilGen = method.GetILGenerator();
                        ilGenArg[0] = ilGen;

                        labels.Clear();
                        locals.Clear();
                        labelNames.Clear();
                        localNames.Clear();

                        srcLocs = new Dictionary<int, ScriptSrcLoc>();
                        objectTokens?.BegMethod(method);
                        break;
                    }

                    // End of method's contents (ie, an OpCodes.Ret was probably just output).
                    // Call the callback to tell it the method is complete, and it can do whatever
                    // it wants with the method.
                    case ScriptObjWriterCode.EndMethod:
                    {
                        ilGen = null;
                        ilGenArg[0] = null;
                        scriptObjCode.EndMethod(method, srcLocs);
                        srcLocs = null;
                        objectTokens?.EndMethod();
                        break;
                    }

                     // Declare a label for branching to.
                    case ScriptObjWriterCode.DclLabel:
                    {
                        int number = objReader.ReadInt32();
                        string name = objReader.ReadString();

                        labels.Add(number, ilGen.DefineLabel());
                        labelNames.Add(number, name + "_" + number.ToString());
                        objectTokens?.DefineLabel(number, name);
                        break;
                    }

                     // Declare a local variable to store into.
                    case ScriptObjWriterCode.DclLocal:
                    {
                        int number = objReader.ReadInt32();
                        string name = objReader.ReadString();
                        string type = objReader.ReadString();
                        Type syType = GetTypeFromStr(sdTypes, type);

                        locals.Add(number, ilGen.DeclareLocal(syType));
                        localNames.Add(number, name + "_" + number.ToString());
                        objectTokens?.DefineLocal(number, name, type, syType);
                        break;
                    }

                     // Declare a method that will subsequently be defined.
                     // We create the DynamicMethod object at this point in case there
                     // are forward references from other method bodies.
                    case ScriptObjWriterCode.DclMethod:
                    {
                        string methName = objReader.ReadString();
                        Type retType = GetTypeFromStr(sdTypes, objReader.ReadString());
                        int nArgs = objReader.ReadInt32();

                        Type[] argTypes = new Type[nArgs];
                        string[] argNames = new string[nArgs];
                        for(int i = 0; i < nArgs; i++)
                        {
                            argTypes[i] = GetTypeFromStr(sdTypes, objReader.ReadString());
                            argNames[i] = objReader.ReadString();
                        }
                        methods.Add(methName, new DynamicMethod(methName, retType, argTypes));
                        objectTokens?.DefineMethod(methName, retType, argTypes, argNames);
                        break;
                    }

                     // Mark a previously declared label at this spot.
                    case ScriptObjWriterCode.MarkLabel:
                    {
                        int number = objReader.ReadInt32();

                        ilGen.MarkLabel(labels[number]);

                        objectTokens?.MarkLabel(offset, number);
                        break;
                    }

                     // Try/Catch blocks.
                    case ScriptObjWriterCode.BegExcBlk:
                    {
                        ilGen.BeginExceptionBlock();
                        objectTokens?.BegExcBlk(offset);
                        break;
                    }

                    case ScriptObjWriterCode.BegCatBlk:
                    {
                        Type excType = GetTypeFromStr(sdTypes, objReader.ReadString());
                        ilGen.BeginCatchBlock(excType);
                        objectTokens?.BegCatBlk(offset, excType);
                        break;
                    }

                    case ScriptObjWriterCode.BegFinBlk:
                    {
                        ilGen.BeginFinallyBlock();
                        objectTokens?.BegFinBlk(offset);
                        break;
                    }

                    case ScriptObjWriterCode.EndExcBlk:
                    {
                        ilGen.EndExceptionBlock();
                        objectTokens?.EndExcBlk(offset);
                        break;
                    }

                     // Emit an opcode with no operand.
                    case ScriptObjWriterCode.EmitNull:
                    {
                        OpCode opCode = ReadOpCode(objReader, ref srcFile, ref srcLine, ref srcPosn);

                        SaveSrcLoc(srcLocs, offset, srcFile, srcLine, srcPosn);
                        ilGen.Emit(opCode);

                        objectTokens?.EmitNull(offset, opCode);
                        break;
                    }

                     // Emit an opcode with a FieldInfo operand.
                    case ScriptObjWriterCode.EmitField:
                    {
                        OpCode opCode = ReadOpCode(objReader, ref srcFile, ref srcLine, ref srcPosn);
                        Type reflectedType = GetTypeFromStr(sdTypes, objReader.ReadString());
                        string fieldName = objReader.ReadString();

                        FieldInfo field = reflectedType.GetField(fieldName);
                        SaveSrcLoc(srcLocs, offset, srcFile, srcLine, srcPosn);
                        ilGen.Emit(opCode, field);

                        objectTokens?.EmitField(offset, opCode, field);
                        break;
                    }

                     // Emit an opcode with a LocalBuilder operand.
                    case ScriptObjWriterCode.EmitLocal:
                    {
                        OpCode opCode = ReadOpCode(objReader, ref srcFile, ref srcLine, ref srcPosn);
                        int number = objReader.ReadInt32();
                        SaveSrcLoc(srcLocs, offset, srcFile, srcLine, srcPosn);
                        ilGen.Emit(opCode, locals[number]);

                        objectTokens?.EmitLocal(offset, opCode, number);
                        break;
                    }

                     // Emit an opcode with a Type operand.
                    case ScriptObjWriterCode.EmitType:
                    {
                        OpCode opCode = ReadOpCode(objReader, ref srcFile, ref srcLine, ref srcPosn);
                        string name = objReader.ReadString();
                        Type type = GetTypeFromStr(sdTypes, name);

                        SaveSrcLoc(srcLocs, offset, srcFile, srcLine, srcPosn);
                        ilGen.Emit(opCode, type);

                        objectTokens?.EmitType(offset, opCode, type);
                        break;
                    }

                     // Emit an opcode with a Label operand.
                    case ScriptObjWriterCode.EmitLabel:
                    {
                        OpCode opCode = ReadOpCode(objReader, ref srcFile, ref srcLine, ref srcPosn);
                        int number = objReader.ReadInt32();

                        SaveSrcLoc(srcLocs, offset, srcFile, srcLine, srcPosn);
                        ilGen.Emit(opCode, labels[number]);

                        objectTokens?.EmitLabel(offset, opCode, number);
                        break;
                    }

                     // Emit an opcode with a Label array operand.
                    case ScriptObjWriterCode.EmitLabels:
                    {
                        OpCode opCode = ReadOpCode(objReader, ref srcFile, ref srcLine, ref srcPosn);
                        int nLabels = objReader.ReadInt32();
                        Label[] lbls = new Label[nLabels];
                        int[] nums = new int[nLabels];
                        for(int i = 0; i < nLabels; i++)
                        {
                            nums[i] = objReader.ReadInt32();
                            lbls[i] = labels[nums[i]];
                        }

                        SaveSrcLoc(srcLocs, offset, srcFile, srcLine, srcPosn);
                        ilGen.Emit(opCode, lbls);

                        objectTokens?.EmitLabels(offset, opCode, nums);
                        break;
                    }

                     // Emit an opcode with a MethodInfo operand (such as a call) of an external function.
                    case ScriptObjWriterCode.EmitMethodExt:
                    {
                        OpCode opCode = ReadOpCode(objReader, ref srcFile, ref srcLine, ref srcPosn);
                        string methName = objReader.ReadString();
                        Type methType = GetTypeFromStr(sdTypes, objReader.ReadString());
                        int nArgs = objReader.ReadInt32();

                        Type[] argTypes = new Type[nArgs];
                        for(int i = 0; i < nArgs; i++)
                        {
                            argTypes[i] = GetTypeFromStr(sdTypes, objReader.ReadString());
                        }
                        MethodInfo methInfo = methType.GetMethod(methName, argTypes);
                        SaveSrcLoc(srcLocs, offset, srcFile, srcLine, srcPosn);
                        ilGen.Emit(opCode, methInfo);

                        objectTokens?.EmitMethod(offset, opCode, methInfo);
                        break;
                    }

                     // Emit an opcode with a MethodInfo operand of an internal function
                     // (previously declared via DclMethod).
                    case ScriptObjWriterCode.EmitMethodInt:
                    {
                        OpCode opCode = ReadOpCode(objReader, ref srcFile, ref srcLine, ref srcPosn);
                        string methName = objReader.ReadString();

                        MethodInfo methInfo = methods[methName];
                        SaveSrcLoc(srcLocs, offset, srcFile, srcLine, srcPosn);
                        ilGen.Emit(opCode, methInfo);

                        objectTokens?.EmitMethod(offset, opCode, methInfo);
                        break;
                    }

                     // Emit an opcode with a ConstructorInfo operand.
                    case ScriptObjWriterCode.EmitCtor:
                    {
                        OpCode opCode = ReadOpCode(objReader, ref srcFile, ref srcLine, ref srcPosn);
                        Type ctorType = GetTypeFromStr(sdTypes, objReader.ReadString());
                        int nArgs = objReader.ReadInt32();
                        Type[] argTypes = new Type[nArgs];
                        for(int i = 0; i < nArgs; i++)
                        {
                            argTypes[i] = GetTypeFromStr(sdTypes, objReader.ReadString());
                        }

                        ConstructorInfo ctorInfo = ctorType.GetConstructor(argTypes);
                        SaveSrcLoc(srcLocs, offset, srcFile, srcLine, srcPosn);
                        ilGen.Emit(opCode, ctorInfo);

                        objectTokens?.EmitCtor(offset, opCode, ctorInfo);
                        break;
                    }

                     // Emit an opcode with a constant operand of various types.
                    case ScriptObjWriterCode.EmitDouble:
                    {
                        OpCode opCode = ReadOpCode(objReader, ref srcFile, ref srcLine, ref srcPosn);
                        double value = objReader.ReadDouble();

                        if(opCode != OpCodes.Ldc_R8)
                        {
                            throw new Exception("bad opcode " + opCode.ToString());
                        }
                        SaveSrcLoc(srcLocs, offset, srcFile, srcLine, srcPosn);
                        ilGen.Emit(opCode, value);

                        objectTokens?.EmitDouble(offset, opCode, value);
                        break;
                    }

                    case ScriptObjWriterCode.EmitFloat:
                    {
                        OpCode opCode = ReadOpCode(objReader, ref srcFile, ref srcLine, ref srcPosn);
                        float value = objReader.ReadSingle();

                        if(opCode != OpCodes.Ldc_R4)
                        {
                            throw new Exception("bad opcode " + opCode.ToString());
                        }
                        SaveSrcLoc(srcLocs, offset, srcFile, srcLine, srcPosn);
                        ilGen.Emit(opCode, value);

                        objectTokens?.EmitFloat(offset, opCode, value);
                        break;
                    }

                    case ScriptObjWriterCode.EmitInteger:
                    {
                        OpCode opCode = ReadOpCode(objReader, ref srcFile, ref srcLine, ref srcPosn);
                        int value = objReader.ReadInt32();

                        SaveSrcLoc(srcLocs, offset, srcFile, srcLine, srcPosn);

                        if(opCode == OpCodes.Ldc_I4)
                        {
                            if((value >= -1) && (value <= 8))
                            {
                                opCode = opCodesLdcI4M1P8[value + 1];
                                ilGen.Emit(opCode);
                                objectTokens?.EmitNull(offset, opCode);
                                break;
                            }
                            if((value >= 0) && (value <= 127))
                            {
                                opCode = OpCodes.Ldc_I4_S;
                                ilGen.Emit(OpCodes.Ldc_I4_S, (sbyte)value);
                                goto pemitint;
                            }
                        }

                        ilGen.Emit(opCode, value);

                     pemitint:
                        objectTokens?.EmitInteger(offset, opCode, value);
                        break;
                    }

                    case ScriptObjWriterCode.EmitString:
                    {
                        OpCode opCode = ReadOpCode(objReader, ref srcFile, ref srcLine, ref srcPosn);
                        string value = objReader.ReadString();

                        SaveSrcLoc(srcLocs, offset, srcFile, srcLine, srcPosn);
                        ilGen.Emit(opCode, value);

                        objectTokens?.EmitString(offset, opCode, value);
                        break;
                    }

                     // Who knows what?
                    default:
                        throw new Exception("bad ScriptObjWriterCode " + ((byte)code).ToString());
                }
            }
        }

        /**
         * @brief Generate array to quickly translate OpCode.Value to full OpCode struct.
         */
        private static Dictionary<short, OpCode> PopulateOpCodes()
        {
            Dictionary<short, OpCode> opCodeDict = new ();
            FieldInfo[] fields = typeof(OpCodes).GetFields();
            for(int i = 0; i < fields.Length; i++)
            {
                OpCode opcode = (OpCode)fields[i].GetValue(null);
                opCodeDict.Add(opcode.Value, opcode);
            }
            return opCodeDict;
        }

        /**
         * @brief Write opcode out to file.
         */
        private void WriteOpCode(Token errorAt, OpCode opcode)
        {
            if(errorAt == null)
            {
                objFileWriter.Write("");
                objFileWriter.Write(lastErrorAtLine);
                objFileWriter.Write(lastErrorAtPosn);
            }
            else
            {
                if(errorAt.file != lastErrorAtFile)
                {
                    objFileWriter.Write(errorAt.file);
                    lastErrorAtFile = errorAt.file;
                }
                else
                {
                    objFileWriter.Write("");
                }
                objFileWriter.Write(errorAt.line);
                objFileWriter.Write(errorAt.posn);
                lastErrorAtLine = errorAt.line;
                lastErrorAtPosn = errorAt.posn;
            }
            objFileWriter.Write(opcode.Value);
        }

        /**
         * @brief Read opcode in from file.
         */
        private static OpCode ReadOpCode(BinaryReader objReader, ref string srcFile, ref int srcLine, ref int srcPosn)
        {
            string f = objReader.ReadString();
            if(f != "")
                srcFile = f;
            srcLine = objReader.ReadInt32();
            srcPosn = objReader.ReadInt32();

            short value = objReader.ReadInt16();
            return opCodes[value];
        }

        /**
         * @brief Save an IL_offset -> source location translation entry
         * @param srcLocs = saved entries for the current function
         * @param offset = offset in IL object code for next instruction
         * @param src{File,Line,Posn} = location in source file corresponding to opcode
         * @returns with entry added to srcLocs
         */
        private static void SaveSrcLoc(Dictionary<int, ScriptSrcLoc> srcLocs, int offset, string srcFile, int srcLine, int srcPosn)
        {
            srcLocs[offset] = new ScriptSrcLoc()
            {
                file = srcFile,
                line = srcLine,
                posn = srcPosn
            };
        }

        /**
         * @brief Create type<->string conversions.
         *        Using Type.AssemblyQualifiedName is horribly inefficient
         *        and all our types should be known.
         */
        private static Dictionary<string, Type> PopulateS2T()
        {
            return new Dictionary<string, Type>
            {
                { "badcallx", typeof(ScriptBadCallNoException) },
                { "binopstr", typeof(BinOpStr) },
                { "bool", typeof(bool) },
                { "char", typeof(char) },
                { "delegate", typeof(Delegate) },
                { "delarr[]", typeof(Delegate[]) },
                { "double", typeof(double) },
                { "exceptn", typeof(Exception) },
                { "float", typeof(float) },
                { "htlist", typeof(HeapTrackerList) },
                { "htobject", typeof(HeapTrackerObject) },
                { "htstring", typeof(HeapTrackerString) },
                { "inlfunc", typeof(CompValuInline) },
                { "int", typeof(int) },
                { "int*", typeof(int).MakeByRefType() },
                { "intrlokd", typeof(System.Threading.Interlocked) },
                { "lslfloat", typeof(LSL_Float) },
                { "lslint", typeof(LSL_Integer) },
                { "lsllist", typeof(LSL_List) },
                { "lslrot", typeof(LSL_Rotation) },
                { "lslstr", typeof(LSL_String) },
                { "lslvec", typeof(LSL_Vector) },
                { "math", typeof(Math) },
                { "midround", typeof(MidpointRounding) },
                { "object", typeof(object) },
                { "object*", typeof(object).MakeByRefType() },
                { "object[]", typeof(object[]) },
                { "scrbase", typeof(ScriptBaseClass) },
                { "scrcode", typeof(ScriptCodeGen) },
                { "sdtclobj", typeof(XMRSDTypeClObj) },
                { "string", typeof(string) },
                { "typecast", typeof(TypeCast) },
                { "undstatx", typeof(ScriptUndefinedStateException) },
                { "void", typeof(void) },
                { "xmrarray", typeof(XMR_Array) },
                { "xmrinst", typeof(XMRInstAbstract) }
            };
        }

        private static Dictionary<Type, string> PopulateT2S()
        {
            Dictionary<string, Type> s2t = PopulateS2T();
            Dictionary<Type, string> t2s = new ();
            foreach(KeyValuePair<string, Type> kvp in s2t)
            {
                t2s.Add(kvp.Value, kvp.Key);
            }
            return t2s;
        }

        /**
         * @brief Add to list of internally recognized types.
         */
        public static void DefineInternalType(string name, Type type)
        {
            if(!string2Type.ContainsKey(name))
            {
                string2Type.Add(name, type);
                type2String.Add(type, name);
            }
        }

        private string GetStrFromType(Type t)
        {
            string s = GetStrFromTypeWork(t);
            return s;
        }
        private string GetStrFromTypeWork(Type t)
        {
            // internal fixed types like int and xmrarray etc
            if (type2String.TryGetValue(t, out string s))
                return s;

            // script-defined types
            if (sdTypesRev.TryGetValue(t, out s))
                return "sdt$" + s;

            // inline function types
            s = TokenDeclSDTypeDelegate.TryGetInlineName(t);
            if(s != null)
                return s;

            // last resort
            return t.AssemblyQualifiedName;
        }

        private static Type GetTypeFromStr(Dictionary<string, TokenDeclSDType> sdTypes, string s)
        {
            // internal fixed types like int and xmrarray etc
            if (string2Type.TryGetValue(s, out Type t))
                return t;

            // script-defined types
            if (s.StartsWith("sdt$"))
                return sdTypes[s[4..]].GetSysType();

            // inline function types
            t = TokenDeclSDTypeDelegate.TryGetInlineSysType(s);
            if(t != null)
                return t;

            // last resort
            return Type.GetType(s, true);
        }
    }

    public class ScriptSrcLoc
    {
        public string file;
        public int line;
        public int posn;
    }
}
