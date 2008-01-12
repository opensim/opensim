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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
/* Original code: Tedd Hansen */
using System;
using System.Reflection;
using System.Reflection.Emit;
using OpenSim.Region.ScriptEngine.Common;

namespace OpenSim.Region.ScriptEngine.LSOEngine.LSO
{
    internal partial class LSO_Parser
    {
        //internal Stack<Type> ILStack = new Stack<Type>();
        //LSO_Enums MyLSO_Enums = new LSO_Enums();

        internal bool LSL_PROCESS_OPCODE(ILGenerator il)
        {
            byte bp1;
            UInt32 u32p1;
            float fp1;
            UInt16 opcode = br_read(1)[0];
            Common.SendToDebug("OPCODE: " + ((LSO_Enums.Operation_Table) opcode).ToString());
            string idesc = ((LSO_Enums.Operation_Table) opcode).ToString();
            switch ((LSO_Enums.Operation_Table) opcode)
            {
                    /***************
                 * IMPLEMENTED *
                 ***************/
                case LSO_Enums.Operation_Table.NOOP:
                    break;
                case LSO_Enums.Operation_Table.PUSHSP:
                    // Push Stack Top (Memory Address) to stack
                    Common.SendToDebug("Instruction " + idesc);
                    Common.SendToDebug("Instruction " + idesc +
                                       ": Description: Pushing Stack Top (Memory Address from header) to stack");
                    IL_Push(il, (UInt32) myHeader.SP);
                    break;
                    // BYTE
                case LSO_Enums.Operation_Table.PUSHARGB:
                    Common.SendToDebug("Param1: " + br_read(1)[0]);
                    break;
                    // INTEGER
                case LSO_Enums.Operation_Table.PUSHARGI:
                    u32p1 = BitConverter.ToUInt32(br_read(4), 0);
                    Common.SendToDebug("Instruction " + idesc + ", Param1: " + u32p1);
                    IL_Push(il, u32p1);
                    break;
                    // FLOAT
                case LSO_Enums.Operation_Table.PUSHARGF:
                    fp1 = BitConverter.ToUInt32(br_read(4), 0);
                    Common.SendToDebug("Instruction " + idesc + ", Param1: " + fp1);
                    IL_Push(il, fp1);
                    break;
                    // STRING
                case LSO_Enums.Operation_Table.PUSHARGS:
                    string s = Read_String();
                    Common.SendToDebug("Instruction " + idesc + ", Param1: " + s);
                    IL_Debug(il, "OPCODE: " + idesc + ":" + s);
                    IL_Push(il, s);
                    break;
                    // VECTOR z,y,x
                case LSO_Enums.Operation_Table.PUSHARGV:
                    LSO_Enums.Vector v = new LSO_Enums.Vector();
                    v.Z = BitConverter.ToUInt32(br_read(4), 0);
                    v.Y = BitConverter.ToUInt32(br_read(4), 0);
                    v.X = BitConverter.ToUInt32(br_read(4), 0);
                    Common.SendToDebug("Param1 Z: " + v.Z);
                    Common.SendToDebug("Param1 Y: " + v.Y);
                    Common.SendToDebug("Param1 X: " + v.X);
                    IL_Push(il, v);
                    break;
                    // ROTATION s,z,y,x
                case LSO_Enums.Operation_Table.PUSHARGQ:
                    LSO_Enums.Rotation r = new LSO_Enums.Rotation();
                    r.S = BitConverter.ToUInt32(br_read(4), 0);
                    r.Z = BitConverter.ToUInt32(br_read(4), 0);
                    r.Y = BitConverter.ToUInt32(br_read(4), 0);
                    r.X = BitConverter.ToUInt32(br_read(4), 0);
                    Common.SendToDebug("Param1 S: " + r.S);
                    Common.SendToDebug("Param1 Z: " + r.Z);
                    Common.SendToDebug("Param1 Y: " + r.Y);
                    Common.SendToDebug("Param1 X: " + r.X);
                    IL_Push(il, r);
                    break;

                case LSO_Enums.Operation_Table.PUSHE:
                    IL_Push(il, (UInt32) 0);
                    break;

                case LSO_Enums.Operation_Table.PUSHARGE:
                    u32p1 = BitConverter.ToUInt32(br_read(4), 0);
                    Common.SendToDebug("Param1: " + u32p1);
                    //IL_Push(il, new string(" ".ToCharArray()[0], Convert.ToInt32(u32p1)));
                    IL_Push(il, u32p1);
                    break;
                    // BYTE
                case LSO_Enums.Operation_Table.ADD:
                case LSO_Enums.Operation_Table.SUB:
                case LSO_Enums.Operation_Table.MUL:
                case LSO_Enums.Operation_Table.DIV:
                case LSO_Enums.Operation_Table.EQ:
                case LSO_Enums.Operation_Table.NEQ:
                case LSO_Enums.Operation_Table.LEQ:
                case LSO_Enums.Operation_Table.GEQ:
                case LSO_Enums.Operation_Table.LESS:
                case LSO_Enums.Operation_Table.GREATER:
                case LSO_Enums.Operation_Table.NEG:
                case LSO_Enums.Operation_Table.MOD:
                    bp1 = br_read(1)[0];
                    Common.SendToDebug("Param1: " + bp1);
                    IL_CallBaseFunction(il, idesc, (UInt32) bp1);
                    break;

                    // NO ARGUMENTS
                case LSO_Enums.Operation_Table.BITAND:
                case LSO_Enums.Operation_Table.BITOR:
                case LSO_Enums.Operation_Table.BITXOR:
                case LSO_Enums.Operation_Table.BOOLAND:
                case LSO_Enums.Operation_Table.BOOLOR:
                case LSO_Enums.Operation_Table.BITNOT:
                case LSO_Enums.Operation_Table.BOOLNOT:
                    IL_CallBaseFunction(il, idesc);
                    break;
                    // SHORT
                case LSO_Enums.Operation_Table.CALLLIB_TWO_BYTE:
                    // TODO: What is size of short?
                    UInt16 U16p1 = BitConverter.ToUInt16(br_read(2), 0);
                    Common.SendToDebug("Instruction " + idesc + ": Builtin Command: " +
                                       ((LSO_Enums.BuiltIn_Functions) U16p1).ToString());
                    //Common.SendToDebug("Param1: " + U16p1);
                    string fname = ((LSO_Enums.BuiltIn_Functions) U16p1).ToString();

                    bool cmdFound = false;
                    foreach (MethodInfo mi in typeof (LSL_BuiltIn_Commands_Interface).GetMethods())
                    {
                        // Found command
                        if (mi.Name == fname)
                        {
                            il.Emit(OpCodes.Ldarg_0);
                            il.Emit(OpCodes.Call, typeof (LSL_BaseClass).GetMethod("GetLSL_BuiltIn", new Type[] {}));
                            // Pop required number of items from my stack to .Net stack
                            IL_PopToStack(il, mi.GetParameters().Length);
                            il.Emit(OpCodes.Callvirt, mi);
                            cmdFound = true;
                            break;
                        }
                    }
                    if (cmdFound == false)
                    {
                        Common.SendToDebug("ERROR: UNABLE TO LOCATE OPCODE " + idesc + " IN BASECLASS");
                    }

                    break;

                    // RETURN
                case LSO_Enums.Operation_Table.RETURN:

                    Common.SendToDebug("OPCODE: RETURN");
                    return true;

                case LSO_Enums.Operation_Table.POP:
                case LSO_Enums.Operation_Table.POPS:
                case LSO_Enums.Operation_Table.POPL:
                case LSO_Enums.Operation_Table.POPV:
                case LSO_Enums.Operation_Table.POPQ:
                    // Pops a specific datatype from the stack
                    // We just ignore the datatype for now
                    IL_Pop(il);
                    break;

                    // LONG
                case LSO_Enums.Operation_Table.STORE:
                case LSO_Enums.Operation_Table.STORES:
                case LSO_Enums.Operation_Table.STOREL:
                case LSO_Enums.Operation_Table.STOREV:
                case LSO_Enums.Operation_Table.STOREQ:
                    u32p1 = BitConverter.ToUInt32(br_read(4), 0);
                    Common.SendToDebug("Param1: " + u32p1.ToString());
                    IL_CallBaseFunction(il, "StoreToLocal", u32p1);
                    break;

                case LSO_Enums.Operation_Table.STOREG:
                case LSO_Enums.Operation_Table.STOREGS:
                case LSO_Enums.Operation_Table.STOREGL:
                case LSO_Enums.Operation_Table.STOREGV:
                case LSO_Enums.Operation_Table.STOREGQ:
                    u32p1 = BitConverter.ToUInt32(br_read(4), 0);
                    Common.SendToDebug("Param1: " + u32p1.ToString());
                    IL_CallBaseFunction(il, "StoreToGlobal", u32p1);
                    break;

                case LSO_Enums.Operation_Table.LOADP:
                case LSO_Enums.Operation_Table.LOADSP:
                case LSO_Enums.Operation_Table.LOADLP:
                case LSO_Enums.Operation_Table.LOADVP:
                case LSO_Enums.Operation_Table.LOADQP:
                    u32p1 = BitConverter.ToUInt32(br_read(4), 0);
                    Common.SendToDebug("Param1: " + u32p1.ToString());
                    IL_CallBaseFunction(il, "StoreToLocal", u32p1);
                    IL_Pop(il);
                    break;

                case LSO_Enums.Operation_Table.LOADGP:
                case LSO_Enums.Operation_Table.LOADGSP:
                case LSO_Enums.Operation_Table.LOADGLP:
                case LSO_Enums.Operation_Table.LOADGVP:
                case LSO_Enums.Operation_Table.LOADGQP:
                    u32p1 = BitConverter.ToUInt32(br_read(4), 0);
                    Common.SendToDebug("Param1: " + u32p1.ToString());
                    IL_CallBaseFunction(il, "StoreToStatic", u32p1 - 6 + myHeader.GVR);
                    IL_Pop(il);
                    break;

                    // PUSH FROM LOCAL FRAME
                case LSO_Enums.Operation_Table.PUSH:
                case LSO_Enums.Operation_Table.PUSHS:
                case LSO_Enums.Operation_Table.PUSHL:
                case LSO_Enums.Operation_Table.PUSHV:
                case LSO_Enums.Operation_Table.PUSHQ:
                    u32p1 = BitConverter.ToUInt32(br_read(4), 0);
                    Common.SendToDebug("Param1: " + u32p1.ToString());
                    IL_CallBaseFunction(il, "GetFromLocal", u32p1);

                    break;

                    // PUSH FROM STATIC FRAME
                case LSO_Enums.Operation_Table.PUSHG:
                case LSO_Enums.Operation_Table.PUSHGS:
                case LSO_Enums.Operation_Table.PUSHGL:
                case LSO_Enums.Operation_Table.PUSHGV:
                case LSO_Enums.Operation_Table.PUSHGQ:
                    u32p1 = BitConverter.ToUInt32(br_read(4), 0);
                    Common.SendToDebug("Param1: " + u32p1.ToString());
                    IL_CallBaseFunction(il, "GetFromStatic", u32p1 - 6 + myHeader.GVR);
                    break;


                    /***********************
                 * NOT IMPLEMENTED YET *
                 ***********************/


                case LSO_Enums.Operation_Table.POPIP:
                case LSO_Enums.Operation_Table.POPSP:
                case LSO_Enums.Operation_Table.POPSLR:
                case LSO_Enums.Operation_Table.POPARG:
                case LSO_Enums.Operation_Table.POPBP:
                    //Common.SendToDebug("Instruction " + idesc + ": Ignored");
                    Common.SendToDebug("Instruction " + idesc +
                                       ": Description: Drop x bytes from the stack (TODO: Only popping 1)");
                    //Common.SendToDebug("Param1: " + BitConverter.ToUInt32(br_read(4), 0));
                    IL_Pop(il);
                    break;


                    // None
                case LSO_Enums.Operation_Table.PUSHIP:
                    // PUSH INSTRUCTION POINTER
                    break;
                case LSO_Enums.Operation_Table.PUSHBP:

                case LSO_Enums.Operation_Table.PUSHEV:
                    break;
                case LSO_Enums.Operation_Table.PUSHEQ:
                    break;


                    // LONG
                case LSO_Enums.Operation_Table.JUMP:
                    Common.SendToDebug("Param1: " + BitConverter.ToUInt32(br_read(4), 0));
                    break;
                    // BYTE, LONG
                case LSO_Enums.Operation_Table.JUMPIF:
                case LSO_Enums.Operation_Table.JUMPNIF:
                    Common.SendToDebug("Param1: " + br_read(1)[0]);
                    Common.SendToDebug("Param2: " + BitConverter.ToUInt32(br_read(4), 0));
                    break;
                    // LONG
                case LSO_Enums.Operation_Table.STATE:
                    bp1 = br_read(1)[0];
                    //il.Emit(OpCodes.Ld);                            // Load local variable 0 onto stack
                    //il.Emit(OpCodes.Ldc_I4, 0);                    // Push index position
                    //il.Emit(OpCodes.Ldstr, EventList[p1]);          // Push value
                    //il.Emit(OpCodes.Stelem_Ref);                    // Perform array[index] = value
                    break;
                case LSO_Enums.Operation_Table.CALL:
                    Common.SendToDebug("Param1: " + BitConverter.ToUInt32(br_read(4), 0));
                    Common.SendToDebug("ERROR: Function CALL not implemented yet.");
                    break;
                    // BYTE
                case LSO_Enums.Operation_Table.CAST:
                    bp1 = br_read(1)[0];
                    Common.SendToDebug("Instruction " + idesc + ": Cast to type: " +
                                       ((LSO_Enums.OpCode_Cast_TypeDefs) bp1));
                    Common.SendToDebug("Param1: " + bp1);
                    switch ((LSO_Enums.OpCode_Cast_TypeDefs) bp1)
                    {
                        case LSO_Enums.OpCode_Cast_TypeDefs.String:
                            Common.SendToDebug("Instruction " + idesc + ": il.Emit(OpCodes.Box, ILStack.Pop());");
                            break;
                        default:
                            Common.SendToDebug("Instruction " + idesc + ": Unknown cast type!");
                            break;
                    }
                    break;
                    // LONG
                case LSO_Enums.Operation_Table.STACKTOS:
                case LSO_Enums.Operation_Table.STACKTOL:
                    Common.SendToDebug("Param1: " + BitConverter.ToUInt32(br_read(4), 0));
                    break;
                    // BYTE
                case LSO_Enums.Operation_Table.PRINT:
                case LSO_Enums.Operation_Table.CALLLIB:
                    Common.SendToDebug("Param1: " + br_read(1)[0]);
                    break;
            }
            return false;
        }

        private void IL_PopToStack(ILGenerator il)
        {
            IL_PopToStack(il, 1);
        }

        private void IL_PopToStack(ILGenerator il, int count)
        {
            Common.SendToDebug("IL_PopToStack();");
            for (int i = 0; i < count; i++)
            {
                IL_CallBaseFunction(il, "POPToStack");
                //il.Emit(OpCodes.Ldarg_0);
                //il.Emit(OpCodes.Call,
                //    typeof(LSL_BaseClass).GetMethod("POPToStack",
                //    new Type[] { }));
            }
        }

        private void IL_Pop(ILGenerator il)
        {
            Common.SendToDebug("IL_Pop();");
            IL_CallBaseFunction(il, "POP");
        }

        private void IL_Debug(ILGenerator il, string text)
        {
            il.Emit(OpCodes.Ldstr, text);
            il.Emit(OpCodes.Call, typeof (Common).GetMethod("SendToDebug",
                                                            new Type[] {typeof (string)}
                                      ));
        }

        private void IL_CallBaseFunction(ILGenerator il, string methodname)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof (LSL_BaseClass).GetMethod(methodname, new Type[] {}));
        }

        private void IL_CallBaseFunction(ILGenerator il, string methodname, object data)
        {
            il.Emit(OpCodes.Ldarg_0);
            if (data.GetType() == typeof (string))
                il.Emit(OpCodes.Ldstr, (string) data);
            if (data.GetType() == typeof (UInt32))
                il.Emit(OpCodes.Ldc_I4, (UInt32) data);
            il.Emit(OpCodes.Call, typeof (LSL_BaseClass).GetMethod(methodname, new Type[] {data.GetType()}));
        }

        private void IL_Push(ILGenerator il, object data)
        {
            il.Emit(OpCodes.Ldarg_0);
            Common.SendToDebug("PUSH datatype: " + data.GetType());

            IL_PushDataTypeToILStack(il, data);

            il.Emit(OpCodes.Call, typeof (LSL_BaseClass).GetMethod("PUSH", new Type[] {data.GetType()}));
        }

        private void IL_PushDataTypeToILStack(ILGenerator il, object data)
        {
            if (data.GetType() == typeof (UInt16))
            {
                il.Emit(OpCodes.Ldc_I4, (UInt16) data);
                il.Emit(OpCodes.Box, data.GetType());
            }
            if (data.GetType() == typeof (UInt32))
            {
                il.Emit(OpCodes.Ldc_I4, (UInt32) data);
                il.Emit(OpCodes.Box, data.GetType());
            }
            if (data.GetType() == typeof (Int32))
            {
                il.Emit(OpCodes.Ldc_I4, (Int32) data);
                il.Emit(OpCodes.Box, data.GetType());
            }
            if (data.GetType() == typeof (float))
            {
                il.Emit(OpCodes.Ldc_I4, (float) data);
                il.Emit(OpCodes.Box, data.GetType());
            }
            if (data.GetType() == typeof (string))
                il.Emit(OpCodes.Ldstr, (string) data);
            //if (data.GetType() == typeof(LSO_Enums.Rotation))
            //    il.Emit(OpCodes.Ldobj, (LSO_Enums.Rotation)data);
            //if (data.GetType() == typeof(LSO_Enums.Vector))
            //    il.Emit(OpCodes.Ldobj, (LSO_Enums.Vector)data);
            //if (data.GetType() == typeof(LSO_Enums.Key))
            //    il.Emit(OpCodes.Ldobj, (LSO_Enums.Key)data);
        }
    }
}