/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
        using System.Collections.Generic;
        using System.Text;
        using System.Reflection;
        using System.Reflection.Emit;

        namespace OpenSim.Region.Scripting.LSL
        {
            partial class LSO_Parser
            {
                //LSO_Enums MyLSO_Enums = new LSO_Enums();

                internal bool LSL_PROCESS_OPCODE(ILGenerator il)
                {
                    
                    byte bp1;
                    UInt32 u32p1;
                    UInt16 opcode = br_read(1)[0];
                    Common.SendToDebug("OPCODE: " + ((LSO_Enums.Operation_Table)opcode).ToString());
                    string idesc = ((LSO_Enums.Operation_Table)opcode).ToString();
                    switch ((LSO_Enums.Operation_Table)opcode)
                    {

                        case LSO_Enums.Operation_Table.POP:
                        case LSO_Enums.Operation_Table.POPL:
                        case LSO_Enums.Operation_Table.POPV:
                        case LSO_Enums.Operation_Table.POPQ:
                        case LSO_Enums.Operation_Table.POPIP:
                        case LSO_Enums.Operation_Table.POPBP:
                        case LSO_Enums.Operation_Table.POPSP:
                        case LSO_Enums.Operation_Table.POPSLR:
                            // ignore -- builds callframe
                            Common.SendToDebug("Instruction " + idesc + ": il.Emit(OpCodes.Pop);");
                            il.Emit(OpCodes.Pop);
                            break;
                        case LSO_Enums.Operation_Table.POPARG:
                            Common.SendToDebug("Instruction " + idesc + ": Ignored");
                            Common.SendToDebug("Instruction " + idesc + ": Description: Drop x bytes from the stack ");
                            Common.SendToDebug("Param1: " + BitConverter.ToUInt32(br_read(4), 0));
                            break;

                        // LONG
                        case LSO_Enums.Operation_Table.STORE:
                        case LSO_Enums.Operation_Table.STORES:
                        case LSO_Enums.Operation_Table.STOREL:
                        case LSO_Enums.Operation_Table.STOREV:
                        case LSO_Enums.Operation_Table.STOREQ:
                        case LSO_Enums.Operation_Table.STOREG:
                        case LSO_Enums.Operation_Table.STOREGS:
                        case LSO_Enums.Operation_Table.STOREGL:
                        case LSO_Enums.Operation_Table.STOREGV:
                        case LSO_Enums.Operation_Table.STOREGQ:
                        case LSO_Enums.Operation_Table.LOADP:
                        case LSO_Enums.Operation_Table.LOADSP:
                        case LSO_Enums.Operation_Table.LOADLP:
                        case LSO_Enums.Operation_Table.LOADVP:
                        case LSO_Enums.Operation_Table.LOADQP:
                        case LSO_Enums.Operation_Table.PUSH:
                        case LSO_Enums.Operation_Table.PUSHS:
                        case LSO_Enums.Operation_Table.PUSHL:
                        case LSO_Enums.Operation_Table.PUSHV:
                        case LSO_Enums.Operation_Table.PUSHQ:
                        case LSO_Enums.Operation_Table.PUSHG:
                        case LSO_Enums.Operation_Table.PUSHGS:
                        case LSO_Enums.Operation_Table.PUSHGL:
                        case LSO_Enums.Operation_Table.PUSHGV:
                        case LSO_Enums.Operation_Table.PUSHGQ:
                            Common.SendToDebug("Param1: " + BitConverter.ToUInt32(br_read(4), 0));
                            break;
                            // None
                        case LSO_Enums.Operation_Table.PUSHIP:
                        case LSO_Enums.Operation_Table.PUSHBP:
                        case LSO_Enums.Operation_Table.PUSHSP:
                            // Push Stack Top (Memory Address) to stack
                            Common.SendToDebug("Instruction " + idesc + ": il.Emit(OpCodes.Ldc_I4, " + myHeader.SP + ");");
                            Common.SendToDebug("Instruction " + idesc + ": Description: Pushing Stack Top (Memory Address from header) to stack");
                            il.Emit(OpCodes.Ldc_I4, myHeader.SP);
                            break;
                        // BYTE
                        case LSO_Enums.Operation_Table.PUSHARGB:
                            Common.SendToDebug("Param1: " + br_read(1)[0]);
                            break;
                        // INTEGER
                        case LSO_Enums.Operation_Table.PUSHARGI:
                            // TODO: What is size of integer?
                            u32p1 = BitConverter.ToUInt32(br_read(4), 0);
                            Common.SendToDebug("Instruction PUSHSP: il.Emit(OpCodes.Ldc_I4, " + u32p1 + ");");
                            Common.SendToDebug("Param1: " + u32p1);
                            il.Emit(OpCodes.Ldc_I4, u32p1);
                            break;
                        // FLOAT
                        case LSO_Enums.Operation_Table.PUSHARGF:
                            // TODO: What is size of float?
                            Common.SendToDebug("Param1: " + BitConverter.ToUInt32(br_read(4), 0));
                            break;
                        // STRING
                        case LSO_Enums.Operation_Table.PUSHARGS:
                            string s = Read_String();
                            Common.SendToDebug("Instruction " + idesc + ": il.Emit(OpCodes.Ldstr, \"" + s + "\");");
                            Common.SendToDebug("Param1: " + s);
                            il.Emit(OpCodes.Ldstr, s);
                            break;
                        // VECTOR z,y,x
                        case LSO_Enums.Operation_Table.PUSHARGV:
                            Common.SendToDebug("Param1 Z: " + BitConverter.ToUInt32(br_read(4), 0));
                            Common.SendToDebug("Param1 Y: " + BitConverter.ToUInt32(br_read(4), 0));
                            Common.SendToDebug("Param1 X: " + BitConverter.ToUInt32(br_read(4), 0));
                            break;
                        // ROTATION s,z,y,x
                        case LSO_Enums.Operation_Table.PUSHARGQ:
                            Common.SendToDebug("Param1 S: " + BitConverter.ToUInt32(br_read(4), 0));
                            Common.SendToDebug("Param1 Z: " + BitConverter.ToUInt32(br_read(4), 0));
                            Common.SendToDebug("Param1 Y: " + BitConverter.ToUInt32(br_read(4), 0));
                            Common.SendToDebug("Param1 X: " + BitConverter.ToUInt32(br_read(4), 0));
                            break;
                        // LONG
                        case LSO_Enums.Operation_Table.PUSHARGE:
                            u32p1 = BitConverter.ToUInt32(br_read(4), 0);
                            //Common.SendToDebug("Instruction PUSHSP: il.Emit(OpCodes., " + u32p1 + ");");
                            Common.SendToDebug("Instruction " + idesc + ": Ignoring (not in use according to doc)");
                            //Common.SendToDebug("Instruction " + idesc + ": Description: Pushes X bytes of $00 onto the stack (used to put space for local variable memory for a call)");
                            Common.SendToDebug("Param1: " + u32p1);
                            //il.Emit(OpCodes.ldc_i4, u32p1);
                            //if (u32p1 > 0) {
                                //for (int _ic=0; _ic < u32p1; _ic++)
                                //{
                                //    Common.SendToDebug("Instruction " + idesc + ": il.Emit(OpCodes.Ldnull);");
                                //    il.Emit(OpCodes.Ldnull);
                                //}
                            break;
                        // BYTE
                        case LSO_Enums.Operation_Table.ADD:
                            bp1 = br_read(1)[0];
                            Common.SendToDebug("Instruction " + idesc + ": Add type: " + ((LSO_Enums.OpCode_Add_TypeDefs)bp1).ToString());
                            Common.SendToDebug("Param1: " + bp1);
                            switch ((LSO_Enums.OpCode_Add_TypeDefs)bp1)
                            {
                                case LSO_Enums.OpCode_Add_TypeDefs.String:
                                    Common.SendToDebug("Instruction " + idesc
                                        + ": il.Emit(OpCodes.Call, typeof(System.String).GetMethod(\"Concat\", new Type[] { typeof(object), typeof(object) }));");
                                    il.Emit(OpCodes.Call, typeof(System.String).GetMethod
                                            ("Concat", new Type[] { typeof(object), typeof(object) }));

                                    break;
                                case LSO_Enums.OpCode_Add_TypeDefs.UInt32:
                                default:
                                    Common.SendToDebug("Instruction " + idesc + ": il.Emit(OpCodes.Add);");
                                    il.Emit(OpCodes.Add);
                                    break;
                            }

                                
                                //il.Emit(OpCodes.Add, p1);
                            break;
                        case LSO_Enums.Operation_Table.SUB:
                            Common.SendToDebug("Instruction " + idesc + ": il.Emit(OpCodes.Sub);");
                            bp1 = br_read(1)[0];
                            Common.SendToDebug("Param1: " + bp1);
                                il.Emit(OpCodes.Sub);
                                //il.Emit(OpCodes.Sub, p1);
                            break;
                        case LSO_Enums.Operation_Table.MUL:
                            Common.SendToDebug("Instruction " + idesc + ": il.Emit(OpCodes.Mul);");
                    bp1 = br_read(1)[0];
        Common.SendToDebug("Param1: " + bp1);
                                il.Emit(OpCodes.Mul);
                                //il.Emit(OpCodes.Mul, p1);
                            break;
                        case LSO_Enums.Operation_Table.DIV:
                            Common.SendToDebug("Instruction " + idesc + ": il.Emit(OpCodes.Div);");
        bp1 = br_read(1)[0];
        Common.SendToDebug("Param1: " + bp1);
                                il.Emit(OpCodes.Div);
                                //il.Emit(OpCodes.Div, p1);
                            break;
                        case LSO_Enums.Operation_Table.EQ:
                            Common.SendToDebug("Instruction " + idesc + ": il.Emit(OpCodes.Ceq);");
        bp1 = br_read(1)[0];
        Common.SendToDebug("Param1: " + bp1);
                                il.Emit(OpCodes.Ceq);
                                //il.Emit(OpCodes.Ceq, p1);
                            break;
                        case LSO_Enums.Operation_Table.NEQ:
                        case LSO_Enums.Operation_Table.LEQ:
                        case LSO_Enums.Operation_Table.GEQ:
                        case LSO_Enums.Operation_Table.LESS:
                            Common.SendToDebug("Instruction " + idesc + ": il.Emit(OpCodes.Clt_Un);");
        bp1 = br_read(1)[0];
        Common.SendToDebug("Param1: " + bp1);
                                il.Emit(OpCodes.Clt_Un);
                                //il.Emit(OpCodes.Clt, p1);
                            break;
                        case LSO_Enums.Operation_Table.GREATER:
                            Common.SendToDebug("Instruction " + idesc + ": il.Emit(OpCodes.Cgt_Un);");
        bp1 = br_read(1)[0];
        Common.SendToDebug("Param1: " + bp1);
                                il.Emit(OpCodes.Cgt_Un);
                                //il.Emit(OpCodes.Cgt, p1);
                            break;
                        case LSO_Enums.Operation_Table.MOD:
                        case LSO_Enums.Operation_Table.BOOLOR:
        bp1 = br_read(1)[0];
        Common.SendToDebug("Param1: " + bp1);
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
                            break;
                        // BYTE
                        case LSO_Enums.Operation_Table.CAST:
                            bp1 = br_read(1)[0];
                            Common.SendToDebug("Instruction " + idesc + ": Cast to type: " + ((LSO_Enums.OpCode_Cast_TypeDefs)bp1));
                            Common.SendToDebug("Param1: " + bp1);
                            switch ((LSO_Enums.OpCode_Cast_TypeDefs)bp1)
                            {
                                case LSO_Enums.OpCode_Cast_TypeDefs.String:
                                    Common.SendToDebug("Instruction " + idesc + ": il.Emit(OpCodes.Calli, typeof(System.Convert).GetMethod(\"ToString\", new Type[] { typeof(object) }));");
                                    //il.Emit(OpCodes.Box, typeof (UInt32));
                                    il.Emit(OpCodes.Calli, typeof(Common).GetMethod
                                            ("Cast_ToString", new Type[] { typeof(object) }));
                                    
                                    //il.Emit(OpCodes.Box, typeof(System.UInt32) );
                                    //il.Emit(OpCodes.Box, typeof(string));

                                    //il.Emit(OpCodes.Conv_R8);
                                    //il.Emit(OpCodes.Call, typeof(System.Convert).GetMethod
                                    //        ("ToString", new Type[] { typeof(float) }));
                                    
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
                        // SHORT
                        case LSO_Enums.Operation_Table.CALLLIB_TWO_BYTE:
                            // TODO: What is size of short?
                            UInt16 U16p1 = BitConverter.ToUInt16(br_read(2), 0);
                            Common.SendToDebug("Instruction " + idesc + ": Builtin Command: " + ((LSO_Enums.BuiltIn_Functions)U16p1).ToString());
                            Common.SendToDebug("Param1: " + U16p1);
                            switch ((LSO_Enums.BuiltIn_Functions)U16p1)
                                {
                                    case LSO_Enums.BuiltIn_Functions.llSay:
                                        Common.SendToDebug("Instruction " + idesc + " " + ((LSO_Enums.BuiltIn_Functions)U16p1).ToString() 
                                            + ": Mapped to internal function");

                                        //il.Emit(OpCodes.Ldstr, "INTERNAL COMMAND: llSay({0}, \"{1}\"");
                                        //il.Emit(OpCodes.Call, typeof(IL_Helper).GetMethod("ReverseFormatString",
                                        //    new Type[] { typeof(string), typeof(UInt32), typeof(string) }
                                        //));


                                        //il.Emit(OpCodes.Pop);
                                        //il.Emit(OpCodes.Call,
                                        //    typeof(Console).GetMethod("WriteLine",
                                        //    new Type[] { typeof(string) }
                                        //));


                                        il.Emit(OpCodes.Call,
                                            typeof(Common).GetMethod("SendToLog",
                                            new Type[] { typeof(string) }
                                        ));


                                        
                                        //il.Emit(OpCodes.Pop);

                                       //il.Emit(OpCodes.Ldind_I2, 0);

                                        //il.Emit(OpCodes.Call, typeof(string).GetMethod("Format", new Type[] { typeof(string), typeof(object) }));
                                        //il.EmitCalli(OpCodes.Calli, 
                                        //il.Emit(OpCodes.Call, typeof().GetMethod
                                         //   ("llSay", new Type[] { typeof(UInt32), typeof(string) }));
                                        break;
                                default:
                                    Common.SendToDebug("Instruction " + idesc + ": " + ((LSO_Enums.BuiltIn_Functions)U16p1).ToString() + ": INTERNAL COMMAND NOT IMPLEMENTED");
                                    break;
                                }
                            
                                //Common.SendToDebug("Instruction " + idesc + ": DEBUG: Faking return code:");
                                //Common.SendToDebug("Instruction " + idesc + ": il.Emit(OpCodes.Ldc_I4, 0);");
                                //il.Emit(OpCodes.Ldc_I4, 0);
                                break;

                        // RETURN
                        case LSO_Enums.Operation_Table.RETURN:

                            Common.SendToDebug("Last OPCODE was return command. Code chunk execution complete.");
                            return true;
                    }
                    return false;
                }

            }
        }
