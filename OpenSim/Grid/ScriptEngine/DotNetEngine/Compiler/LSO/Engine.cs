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

/* Original code: Tedd Hansen */
using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;

namespace OpenSim.Grid.ScriptEngine.DotNetEngine.Compiler.LSO
{
    public class Engine
    {
        //private string LSO_FileName = @"LSO\AdditionTest.lso";
        private string LSO_FileName; // = @"LSO\CloseToDefault.lso";
        private AppDomain appDomain;

        public string Compile(string LSOFileName)
        {
            LSO_FileName = LSOFileName;


            //appDomain = AppDomain.CreateDomain("AlternateAppDomain");
            appDomain = Thread.GetDomain();

            // Create Assembly Name
            AssemblyName asmName = new AssemblyName();
            asmName.Name = Path.GetFileNameWithoutExtension(LSO_FileName);
            //asmName.Name = "TestAssembly";

            string DLL_FileName = asmName.Name + ".dll";
            string DLL_FileName_WithPath = Path.GetDirectoryName(LSO_FileName) + @"\" + DLL_FileName;

            Common.SendToLog("LSO File Name: " + Path.GetFileName(LSO_FileName));
            Common.SendToLog("Assembly name: " + asmName.Name);
            Common.SendToLog("Assembly File Name: " + asmName.Name + ".dll");
            Common.SendToLog("Starting processing of LSL ByteCode...");
            Common.SendToLog("");


            // Create Assembly
            AssemblyBuilder asmBuilder = appDomain.DefineDynamicAssembly(
                asmName,
                AssemblyBuilderAccess.RunAndSave
                );
            //// Create Assembly
            //AssemblyBuilder asmBuilder =
            //    Thread.GetDomain().DefineDynamicAssembly
            //(asmName, AssemblyBuilderAccess.RunAndSave);

            // Create a module (and save to disk)
            ModuleBuilder modBuilder = asmBuilder.DefineDynamicModule
                (asmName.Name,
                 DLL_FileName);

            //Common.SendToDebug("asmName.Name is still \"" + asmName.Name + "\"");
            // Create a Class (/Type)
            TypeBuilder typeBuilder = modBuilder.DefineType(
                "LSL_ScriptObject",
                TypeAttributes.Public | TypeAttributes.BeforeFieldInit,
                typeof (LSL_BaseClass));
            //,
            //                        typeof());
            //, typeof(LSL_BuiltIn_Commands_Interface));
            //,
            //                        typeof(object),
            //                        new Type[] { typeof(LSL_CLRInterface.LSLScript) });


            /*
             * Generate the IL itself
             */

            LSO_Parser LSOP = new LSO_Parser(LSO_FileName, typeBuilder);
            LSOP.OpenFile();
            LSOP.Parse();

            // Constructor has to be created AFTER LSO_Parser because of accumulated variables
            if (Common.IL_CreateConstructor)
                IL_CREATE_CONSTRUCTOR(typeBuilder, LSOP);

            LSOP.CloseFile();
            /*
             * Done generating. Create a type and run it.
             */


            Common.SendToLog("Attempting to compile assembly...");
            // Compile it
            Type type = typeBuilder.CreateType();
            Common.SendToLog("Compilation successful!");

            Common.SendToLog("Saving assembly: " + DLL_FileName);
            asmBuilder.Save(DLL_FileName);

            Common.SendToLog("Returning assembly filename: " + DLL_FileName);


            return DLL_FileName;


            //Common.SendToLog("Creating an instance of new assembly...");
            //// Create an instance we can play with
            ////LSLScript hello = (LSLScript)Activator.CreateInstance(type);
            ////LSL_CLRInterface.LSLScript MyScript = (LSL_CLRInterface.LSLScript)Activator.CreateInstance(type);
            //object MyScript = (object)Activator.CreateInstance(type);


            //System.Reflection.MemberInfo[] Members = type.GetMembers();

            //Common.SendToLog("Members of assembly " + type.ToString() + ":");
            //foreach (MemberInfo member in Members)
            //    Common.SendToLog(member.ToString());


            //// Play with it
            ////MyScript.event_state_entry("Test");
            //object[] args = { null };
            ////System.Collections.Generic.List<string> Functions = (System.Collections.Generic.List<string>)type.InvokeMember("GetFunctions", BindingFlags.InvokeMethod, null, MyScript, null);

            //string[] ret = { };
            //if (Common.IL_CreateFunctionList)
            //    ret = (string[])type.InvokeMember("GetFunctions", BindingFlags.InvokeMethod, null, MyScript, null);

            //foreach (string s in ret)
            //{
            //    Common.SendToLog("");
            //    Common.SendToLog("*** Executing LSL Server Event: " + s);
            //    //object test = type.GetMember(s);
            //    //object runner = type.InvokeMember(s, BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Instance, null, MyScript, args);
            //    //runner();
            //    //objBooks_Late = type.InvokeMember(s, BindingFlags.CreateInstance, null, objApp_Late, null);
            //    type.InvokeMember(s, BindingFlags.InvokeMethod, null, MyScript, new object[] { "Test" });

            //}
        }


        private static void IL_CREATE_CONSTRUCTOR(TypeBuilder typeBuilder, LSO_Parser LSOP)
        {
            Common.SendToDebug("IL_CREATE_CONSTRUCTOR()");
            //ConstructorBuilder constructor = typeBuilder.DefineConstructor(
            //            MethodAttributes.Public,
            //            CallingConventions.Standard,
            //            new Type[0]);
            ConstructorBuilder constructor = typeBuilder.DefineConstructor(
                MethodAttributes.Public |
                MethodAttributes.SpecialName |
                MethodAttributes.RTSpecialName,
                CallingConventions.Standard,
                new Type[0]);

            //Define the reflection ConstructorInfor for System.Object
            ConstructorInfo conObj = typeof (LSL_BaseClass).GetConstructor(new Type[0]);

            //call constructor of base object
            ILGenerator il = constructor.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, conObj);


            //Common.SendToDebug("IL_CREATE_CONSTRUCTOR: Creating global: UInt32 State = 0;");
            //string FieldName;
            //// Create state object
            //FieldName = "State";
            //FieldBuilder State_fb = typeBuilder.DefineField(
            //    FieldName,
            //    typeof(UInt32),
            //    FieldAttributes.Public);
            //il.Emit(OpCodes.Ldarg_0);
            //il.Emit(OpCodes.Ldc_I4, 0);
            //il.Emit(OpCodes.Stfld, State_fb);


            //Common.SendToDebug("IL_CREATE_CONSTRUCTOR: Creating global: LSL_BuiltIn_Commands_TestImplementation LSL_BuiltIns = New LSL_BuiltIn_Commands_TestImplementation();");
            ////Type objType1 = typeof(object);
            //Type objType1 = typeof(LSL_BuiltIn_Commands_TestImplementation);

            //FieldName = "LSL_BuiltIns";
            //FieldBuilder LSL_BuiltIns_fb = typeBuilder.DefineField(
            //    FieldName,
            //    objType1,
            //    FieldAttributes.Public);

            ////LSL_BuiltIn_Commands_TestImplementation _ti = new LSL_BuiltIn_Commands_TestImplementation();
            //il.Emit(OpCodes.Ldarg_0);
            ////il.Emit(OpCodes.Ldstr, "Test 123");
            //il.Emit(OpCodes.Newobj, objType1.GetConstructor(new Type[] { }));
            //il.Emit(OpCodes.Stfld, LSL_BuiltIns_fb);

            foreach (UInt32 pos in LSOP.StaticBlocks.Keys)
            {
                LSO_Struct.StaticBlock sb;
                LSOP.StaticBlocks.TryGetValue(pos, out sb);

                if (sb.ObjectType > 0 && sb.ObjectType < 8)
                {
                    // We don't want void or null's

                    il.Emit(OpCodes.Ldarg_0);
                    // Push position to stack
                    il.Emit(OpCodes.Ldc_I4, pos);
                    //il.Emit(OpCodes.Box, typeof(UInt32));


                    Type datatype = null;

                    // Push data to stack
                    Common.SendToDebug("Adding to static (" + pos + ") type: " +
                                       ((LSO_Enums.Variable_Type_Codes) sb.ObjectType).ToString() + " (" + sb.ObjectType +
                                       ")");
                    switch ((LSO_Enums.Variable_Type_Codes) sb.ObjectType)
                    {
                        case LSO_Enums.Variable_Type_Codes.Float:
                        case LSO_Enums.Variable_Type_Codes.Integer:
                            //UInt32
                            il.Emit(OpCodes.Ldc_I4, BitConverter.ToUInt32(sb.BlockVariable, 0));
                            datatype = typeof (UInt32);
                            il.Emit(OpCodes.Box, datatype);
                            break;
                        case LSO_Enums.Variable_Type_Codes.String:
                        case LSO_Enums.Variable_Type_Codes.Key:
                            //String
                            LSO_Struct.HeapBlock hb =
                                LSOP.GetHeap(LSOP.myHeader.HR + BitConverter.ToUInt32(sb.BlockVariable, 0) - 1);
                            il.Emit(OpCodes.Ldstr, Encoding.UTF8.GetString(hb.Data));
                            datatype = typeof (string);
                            break;
                        case LSO_Enums.Variable_Type_Codes.Vector:
                            datatype = typeof (LSO_Enums.Vector);
                            //TODO: Not implemented
                            break;
                        case LSO_Enums.Variable_Type_Codes.Rotation:
                            //Object
                            //TODO: Not implemented
                            datatype = typeof (LSO_Enums.Rotation);
                            break;
                        default:
                            datatype = typeof (object);
                            break;
                    }


                    // Make call
                    il.Emit(OpCodes.Call,
                            typeof (LSL_BaseClass).GetMethod("AddToStatic", new Type[] {typeof (UInt32), datatype}));
                }
            }


            ////il.Emit(OpCodes.Newobj, typeof(UInt32));
            //il.Emit(OpCodes.Starg_0);
            //// Create LSL function library
            //FieldBuilder LSL_BuiltIns_fb = typeBuilder.DefineField("LSL_BuiltIns", typeof(LSL_BuiltIn_Commands_Interface), FieldAttributes.Public);
            //il.Emit(OpCodes.Newobj, typeof(LSL_BuiltIn_Commands_Interface));
            //il.Emit(OpCodes.Stloc_1);

            il.Emit(OpCodes.Ret);
        }


        // End of class
    }
}
