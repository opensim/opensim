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
 */

/* Original code: Tedd Hansen */
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace OpenSim.Region.ScriptEngine.LSOEngine.LSO
{
    internal partial class LSO_Parser
    {
        private string FileName;
        private FileStream fs;
        private BinaryReader br;
        internal LSO_Struct.Header myHeader;
        internal Dictionary<long, LSO_Struct.StaticBlock> StaticBlocks = new Dictionary<long, LSO_Struct.StaticBlock>();
        //private System.Collections.Hashtable StaticBlocks = new System.Collections.Hashtable();

        private TypeBuilder typeBuilder;
        private List<string> EventList = new List<string>();

        public LSO_Parser(string _FileName, TypeBuilder _typeBuilder)
        {
            FileName = _FileName;
            typeBuilder = _typeBuilder;
        }

        internal void OpenFile()
        {
            // Open
            Common.SendToDebug("Opening filename: " + FileName);
            fs = File.Open(FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            br = new BinaryReader(fs, Encoding.BigEndianUnicode);
        }

        internal void CloseFile()
        {
            // Close
            br.Close();
            fs.Close();
        }

        /// <summary>
        /// Parse LSO file.
        /// </summary>
        public void Parse()
        {
            // The LSO Format consist of 6 major blocks: header, statics, functions, states, heap, and stack.

            // HEADER BLOCK
            Common.SendToDebug("Reading HEADER BLOCK at: 0");
            fs.Seek(0, SeekOrigin.Begin);
            myHeader = new LSO_Struct.Header();
            myHeader.TM = BitConverter.ToUInt32(br_read(4), 0);
            myHeader.IP = BitConverter.ToUInt32(br_read(4), 0);
            myHeader.VN = BitConverter.ToUInt32(br_read(4), 0);
            myHeader.BP = BitConverter.ToUInt32(br_read(4), 0);
            myHeader.SP = BitConverter.ToUInt32(br_read(4), 0);
            myHeader.HR = BitConverter.ToUInt32(br_read(4), 0);
            myHeader.HP = BitConverter.ToUInt32(br_read(4), 0);
            myHeader.CS = BitConverter.ToUInt32(br_read(4), 0);
            myHeader.NS = BitConverter.ToUInt32(br_read(4), 0);
            myHeader.CE = BitConverter.ToUInt32(br_read(4), 0);
            myHeader.IE = BitConverter.ToUInt32(br_read(4), 0);
            myHeader.ER = BitConverter.ToUInt32(br_read(4), 0);
            myHeader.FR = BitConverter.ToUInt32(br_read(4), 0);
            myHeader.SLR = BitConverter.ToUInt32(br_read(4), 0);
            myHeader.GVR = BitConverter.ToUInt32(br_read(4), 0);
            myHeader.GFR = BitConverter.ToUInt32(br_read(4), 0);
            myHeader.PR = BitConverter.ToUInt32(br_read(4), 0);
            myHeader.ESR = BitConverter.ToUInt32(br_read(4), 0);
            myHeader.SR = BitConverter.ToUInt32(br_read(4), 0);
            myHeader.NCE = BitConverter.ToUInt64(br_read(8), 0);
            myHeader.NIE = BitConverter.ToUInt64(br_read(8), 0);
            myHeader.NER = BitConverter.ToUInt64(br_read(8), 0);

            // Print Header Block to debug
            Common.SendToDebug("TM - Top of memory (size): " + myHeader.TM);
            Common.SendToDebug("IP - Instruction Pointer (0=not running): " + myHeader.IP);
            Common.SendToDebug("VN - Version number: " + myHeader.VN);
            Common.SendToDebug("BP - Local Frame Pointer: " + myHeader.BP);
            Common.SendToDebug("SP - Stack Pointer: " + myHeader.SP);
            Common.SendToDebug("HR - Heap Register: " + myHeader.HR);
            Common.SendToDebug("HP - Heap Pointer: " + myHeader.HP);
            Common.SendToDebug("CS - Current State: " + myHeader.CS);
            Common.SendToDebug("NS - Next State: " + myHeader.NS);
            Common.SendToDebug("CE - Current Events: " + myHeader.CE);
            Common.SendToDebug("IE - In Event: " + myHeader.IE);
            Common.SendToDebug("ER - Event Register: " + myHeader.ER);
            Common.SendToDebug("FR - Fault Register: " + myHeader.FR);
            Common.SendToDebug("SLR - Sleep Register: " + myHeader.SLR);
            Common.SendToDebug("GVR - Global Variable Register: " + myHeader.GVR);
            Common.SendToDebug("GFR - Global Function Register: " + myHeader.GFR);
            Common.SendToDebug("PR - Parameter Register: " + myHeader.PR);
            Common.SendToDebug("ESR - Energy Supply Register: " + myHeader.ESR);
            Common.SendToDebug("SR - State Register: " + myHeader.SR);
            Common.SendToDebug("NCE - 64-bit Current Events: " + myHeader.NCE);
            Common.SendToDebug("NIE - 64-bit In Events: " + myHeader.NIE);
            Common.SendToDebug("NER - 64-bit Event Register: " + myHeader.NER);
            Common.SendToDebug("Read position when exiting HEADER BLOCK: " + fs.Position);

            // STATIC BLOCK
            Common.SendToDebug("Reading STATIC BLOCK at: " + myHeader.GVR);
            fs.Seek(myHeader.GVR, SeekOrigin.Begin);
            int StaticBlockCount = 0;
            // Read function blocks until we hit GFR
            while (fs.Position < myHeader.GFR)
            {
                StaticBlockCount++;
                long startReadPos = fs.Position;
                Common.SendToDebug("Reading Static Block " + StaticBlockCount + " at: " + startReadPos);

                //fs.Seek(myHeader.GVR, SeekOrigin.Begin);
                LSO_Struct.StaticBlock myStaticBlock = new LSO_Struct.StaticBlock();
                myStaticBlock.Static_Chunk_Header_Size = BitConverter.ToUInt32(br_read(4), 0);
                myStaticBlock.ObjectType = br_read(1)[0];
                Common.SendToDebug("Static Block ObjectType: " +
                                   ((LSO_Enums.Variable_Type_Codes) myStaticBlock.ObjectType).ToString());
                myStaticBlock.Unknown = br_read(1)[0];
                // Size of datatype varies -- what about strings?
                if (myStaticBlock.ObjectType != 0)
                    myStaticBlock.BlockVariable = br_read(getObjectSize(myStaticBlock.ObjectType));

                StaticBlocks.Add((UInt32) startReadPos, myStaticBlock);
            }
            Common.SendToDebug("Number of Static Blocks read: " + StaticBlockCount);

            // FUNCTION BLOCK
            // Always right after STATIC BLOCK
            LSO_Struct.FunctionBlock myFunctionBlock = new LSO_Struct.FunctionBlock();
            if (myHeader.GFR == myHeader.SR)
            {
                // If GFR and SR are at same position then there is no fuction block
                Common.SendToDebug("No FUNCTION BLOCK found");
            }
            else
            {
                Common.SendToDebug("Reading FUNCTION BLOCK at: " + myHeader.GFR);
                fs.Seek(myHeader.GFR, SeekOrigin.Begin);
                myFunctionBlock.FunctionCount = BitConverter.ToUInt32(br_read(4), 0);
                Common.SendToDebug("Number of functions in Fuction Block: " + myFunctionBlock.FunctionCount);
                if (myFunctionBlock.FunctionCount > 0)
                {
                    myFunctionBlock.CodeChunkPointer = new UInt32[myFunctionBlock.FunctionCount];
                    for (int i = 0; i < myFunctionBlock.FunctionCount; i++)
                    {
                        Common.SendToDebug("Reading function " + i + " at: " + fs.Position);
                        // TODO: ADD TO FUNCTION LIST (How do we identify it later?)
                        // Note! Absolute position
                        myFunctionBlock.CodeChunkPointer[i] = BitConverter.ToUInt32(br_read(4), 0) + myHeader.GFR;
                        Common.SendToDebug("Fuction " + i + " code chunk position: " +
                                           myFunctionBlock.CodeChunkPointer[i]);
                    }
                }
            }

            // STATE FRAME BLOCK
            // Always right after FUNCTION BLOCK
            Common.SendToDebug("Reading STATE BLOCK at: " + myHeader.SR);
            fs.Seek(myHeader.SR, SeekOrigin.Begin);
            LSO_Struct.StateFrameBlock myStateFrameBlock = new LSO_Struct.StateFrameBlock();
            myStateFrameBlock.StateCount = BitConverter.ToUInt32(br_read(4), 0);
            if (myStateFrameBlock.StateCount > 0)
            {
                // Initialize array
                myStateFrameBlock.StatePointer = new LSO_Struct.StatePointerBlock[myStateFrameBlock.StateCount];
                for (int i = 0; i < myStateFrameBlock.StateCount; i++)
                {
                    Common.SendToDebug("Reading STATE POINTER BLOCK " + (i + 1) + " at: " + fs.Position);
                    // Position is relative to state frame
                    myStateFrameBlock.StatePointer[i].Location = myHeader.SR + BitConverter.ToUInt32(br_read(4), 0);
                    myStateFrameBlock.StatePointer[i].EventMask = new BitArray(br_read(8));
                    Common.SendToDebug("Pointer: " + myStateFrameBlock.StatePointer[i].Location);
                    Common.SendToDebug("Total potential EventMask bits: " +
                                       myStateFrameBlock.StatePointer[i].EventMask.Count);

                    //// Read STATE BLOCK
                    //long CurPos = fs.Position;
                    //fs.Seek(CurPos, SeekOrigin.Begin);
                }
            }

            // STATE BLOCK
            // For each StateFrameBlock there is one StateBlock with multiple event handlers

            if (myStateFrameBlock.StateCount > 0)
            {
                // Go through all State Frame Pointers found
                for (int i = 0; i < myStateFrameBlock.StateCount; i++)
                {
                    fs.Seek(myStateFrameBlock.StatePointer[i].Location, SeekOrigin.Begin);
                    Common.SendToDebug("Reading STATE BLOCK " + (i + 1) + " at: " + fs.Position);

                    // READ: STATE BLOCK HEADER
                    myStateFrameBlock.StatePointer[i].StateBlock = new LSO_Struct.StateBlock();
                    myStateFrameBlock.StatePointer[i].StateBlock.StartPos = (UInt32) fs.Position; // Note
                    myStateFrameBlock.StatePointer[i].StateBlock.HeaderSize = BitConverter.ToUInt32(br_read(4), 0);
                    myStateFrameBlock.StatePointer[i].StateBlock.Unknown = br_read(1)[0];
                    myStateFrameBlock.StatePointer[i].StateBlock.EndPos = (UInt32) fs.Position; // Note
                    Common.SendToDebug("State block Start Pos: " + myStateFrameBlock.StatePointer[i].StateBlock.StartPos);
                    Common.SendToDebug("State block Header Size: " +
                                       myStateFrameBlock.StatePointer[i].StateBlock.HeaderSize);
                    Common.SendToDebug("State block Header End Pos: " +
                                       myStateFrameBlock.StatePointer[i].StateBlock.EndPos);

                    // We need to count number of bits flagged in EventMask?

                    // for each bit in myStateFrameBlock.StatePointer[i].EventMask

                    // ADDING TO ALL RIGHT NOW, SHOULD LIMIT TO ONLY THE ONES IN USE
                    //TODO: Create event hooks
                    myStateFrameBlock.StatePointer[i].StateBlock.StateBlockHandlers =
                        new LSO_Struct.StateBlockHandler[myStateFrameBlock.StatePointer[i].EventMask.Count - 1];
                    for (int ii = 0; ii < myStateFrameBlock.StatePointer[i].EventMask.Count - 1; ii++)
                    {
                        if (myStateFrameBlock.StatePointer[i].EventMask.Get(ii) == true)
                        {
                            // We got an event
                            //  READ: STATE BLOCK HANDLER
                            Common.SendToDebug("Reading STATE BLOCK " + (i + 1) + " HANDLER matching EVENT MASK " + ii +
                                               " (" + ((LSO_Enums.Event_Mask_Values) ii).ToString() + ") at: " +
                                               fs.Position);
                            myStateFrameBlock.StatePointer[i].StateBlock.StateBlockHandlers[ii].CodeChunkPointer =
                                myStateFrameBlock.StatePointer[i].StateBlock.EndPos +
                                BitConverter.ToUInt32(br_read(4), 0);
                            myStateFrameBlock.StatePointer[i].StateBlock.StateBlockHandlers[ii].CallFrameSize =
                                BitConverter.ToUInt32(br_read(4), 0);
                            Common.SendToDebug("Reading STATE BLOCK " + (i + 1) + " HANDLER EVENT MASK " + ii + " (" +
                                               ((LSO_Enums.Event_Mask_Values) ii).ToString() + ") Code Chunk Pointer: " +
                                               myStateFrameBlock.StatePointer[i].StateBlock.StateBlockHandlers[ii].
                                                   CodeChunkPointer);
                            Common.SendToDebug("Reading STATE BLOCK " + (i + 1) + " HANDLER EVENT MASK " + ii + " (" +
                                               ((LSO_Enums.Event_Mask_Values) ii).ToString() + ") Call Frame Size: " +
                                               myStateFrameBlock.StatePointer[i].StateBlock.StateBlockHandlers[ii].
                                                   CallFrameSize);
                        }
                    }
                }
            }

            //// READ FUNCTION CODE CHUNKS
            //// Functions + Function start pos (GFR)
            //// TODO: Somehow be able to identify and reference this
            //LSO_Struct.CodeChunk[] myFunctionCodeChunk;
            //if (myFunctionBlock.FunctionCount > 0)
            //{
            //    myFunctionCodeChunk = new LSO_Struct.CodeChunk[myFunctionBlock.FunctionCount];
            //    for (int i = 0; i < myFunctionBlock.FunctionCount; i++)
            //    {
            //        Common.SendToDebug("Reading Function Code Chunk " + i);
            //        myFunctionCodeChunk[i] = GetCodeChunk((UInt32)myFunctionBlock.CodeChunkPointer[i]);
            //    }

            //}
            // READ EVENT CODE CHUNKS
            if (myStateFrameBlock.StateCount > 0)
            {
                for (int i = 0; i < myStateFrameBlock.StateCount; i++)
                {
                    // TODO: Somehow organize events and functions so they can be found again,
                    // two level search ain't no good
                    for (int ii = 0; ii < myStateFrameBlock.StatePointer[i].EventMask.Count - 1; ii++)
                    {
                        if (myStateFrameBlock.StatePointer[i].StateBlock.StateBlockHandlers[ii].CodeChunkPointer > 0)
                        {
                            Common.SendToDebug("Reading Event Code Chunk state " + i + ", event " +
                                               (LSO_Enums.Event_Mask_Values) ii);


                            // Override a Method / Function
                            string eventname = i + "_event_" + (LSO_Enums.Event_Mask_Values) ii;
                            Common.SendToDebug("Event Name: " + eventname);
                            if (Common.IL_ProcessCodeChunks)
                            {
                                EventList.Add(eventname);

                                // JUMP TO CODE PROCESSOR
                                ProcessCodeChunk(
                                    myStateFrameBlock.StatePointer[i].StateBlock.StateBlockHandlers[ii].CodeChunkPointer,
                                    typeBuilder, eventname);
                            }
                        }
                    }
                }
            }

            if (Common.IL_CreateFunctionList)
                IL_INSERT_FUNCTIONLIST();
        }

        internal LSO_Struct.HeapBlock GetHeap(UInt32 pos)
        {
            // HEAP BLOCK
            // TODO:? Special read for strings/keys (null terminated) and lists (pointers to other HEAP entries)
            Common.SendToDebug("Reading HEAP BLOCK at: " + pos);
            fs.Seek(pos, SeekOrigin.Begin);

            LSO_Struct.HeapBlock myHeapBlock = new LSO_Struct.HeapBlock();
            myHeapBlock.DataBlockSize = BitConverter.ToInt32(br_read(4), 0);
            myHeapBlock.ObjectType = br_read(1)[0];
            myHeapBlock.ReferenceCount = BitConverter.ToUInt16(br_read(2), 0);
            //myHeapBlock.Data = br_read(getObjectSize(myHeapBlock.ObjectType));
            // Don't read it reversed
            myHeapBlock.Data = new byte[myHeapBlock.DataBlockSize - 1];
            br.Read(myHeapBlock.Data, 0, myHeapBlock.DataBlockSize - 1);


            Common.SendToDebug("Heap Block Data Block Size: " + myHeapBlock.DataBlockSize);
            Common.SendToDebug("Heap Block ObjectType: " +
                               ((LSO_Enums.Variable_Type_Codes) myHeapBlock.ObjectType).ToString());
            Common.SendToDebug("Heap Block Reference Count: " + myHeapBlock.ReferenceCount);

            return myHeapBlock;
        }

        private byte[] br_read(int len)
        {
            if (len <= 0)
                return null;

            try
            {
                byte[] bytes = new byte[len];
                for (int i = len - 1; i > -1; i--)
                    bytes[i] = br.ReadByte();
                return bytes;
            }
            catch (Exception e) // NOTLEGIT: No user related exceptions throwable here?
            {
                Common.SendToDebug("Exception: " + e.ToString());
                throw (e);
            }
        }

        //private byte[] br_read_smallendian(int len)
        //{
        //    byte[] bytes = new byte[len];
        //    br.Read(bytes,0, len);
        //    return bytes;
        //}
        private Type getLLObjectType(byte objectCode)
        {
            switch ((LSO_Enums.Variable_Type_Codes) objectCode)
            {
                case LSO_Enums.Variable_Type_Codes.Void:
                    return typeof (void);
                case LSO_Enums.Variable_Type_Codes.Integer:
                    return typeof (UInt32);
                case LSO_Enums.Variable_Type_Codes.Float:
                    return typeof (float);
                case LSO_Enums.Variable_Type_Codes.String:
                    return typeof (string);
                case LSO_Enums.Variable_Type_Codes.Key:
                    return typeof (string);
                case LSO_Enums.Variable_Type_Codes.Vector:
                    return typeof (LSO_Enums.Vector);
                case LSO_Enums.Variable_Type_Codes.Rotation:
                    return typeof (LSO_Enums.Rotation);
                case LSO_Enums.Variable_Type_Codes.List:
                    Common.SendToDebug("TODO: List datatype not implemented yet!");
                    return typeof (ArrayList);
                case LSO_Enums.Variable_Type_Codes.Null:
                    Common.SendToDebug("TODO: Datatype null is not implemented, using string instead.!");
                    return typeof (string);
                default:
                    Common.SendToDebug("Lookup of LSL datatype " + objectCode +
                                       " to .Net datatype failed: Unknown LSL datatype. Defaulting to object.");
                    return typeof (object);
            }
        }

        private int getObjectSize(byte ObjectType)
        {
            switch ((LSO_Enums.Variable_Type_Codes) ObjectType)
            {
                case LSO_Enums.Variable_Type_Codes.Integer:
                case LSO_Enums.Variable_Type_Codes.Float:
                case LSO_Enums.Variable_Type_Codes.String:
                case LSO_Enums.Variable_Type_Codes.Key:
                case LSO_Enums.Variable_Type_Codes.List:
                    return 4;
                case LSO_Enums.Variable_Type_Codes.Vector:
                    return 12;
                case LSO_Enums.Variable_Type_Codes.Rotation:
                    return 16;
                default:
                    return 0;
            }
        }

        private string Read_String()
        {
            string ret = String.Empty;
            byte reader = br_read(1)[0];
            while (reader != 0x000)
            {
                ret += (char) reader;
                reader = br_read(1)[0];
            }
            return ret;
        }

        /// <summary>
        /// Reads a code chunk and creates IL
        /// </summary>
        /// <param name="pos">Absolute position in file. REMEMBER TO ADD myHeader.GFR!</param>
        /// <param name="typeBuilder">TypeBuilder for assembly</param>
        /// <param name="eventname">Name of event (function) to generate</param>
        private void ProcessCodeChunk(UInt32 pos, TypeBuilder typeBuilder, string eventname)
        {
            LSO_Struct.CodeChunk myCodeChunk = new LSO_Struct.CodeChunk();

            Common.SendToDebug("Reading Function Code Chunk at: " + pos);
            fs.Seek(pos, SeekOrigin.Begin);
            myCodeChunk.CodeChunkHeaderSize = BitConverter.ToUInt32(br_read(4), 0);
            Common.SendToDebug("CodeChunk Header Size: " + myCodeChunk.CodeChunkHeaderSize);
            // Read until null
            myCodeChunk.Comment = Read_String();
            Common.SendToDebug("Function comment: " + myCodeChunk.Comment);
            myCodeChunk.ReturnTypePos = br_read(1)[0];
            myCodeChunk.ReturnType = GetStaticBlock((long) myCodeChunk.ReturnTypePos + (long) myHeader.GVR);
            Common.SendToDebug("Return type #" + myCodeChunk.ReturnType.ObjectType + ": " +
                               ((LSO_Enums.Variable_Type_Codes) myCodeChunk.ReturnType.ObjectType).ToString());

            // TODO: How to determine number of codechunks -- does this method work?
            myCodeChunk.CodeChunkArguments = new List<LSO_Struct.CodeChunkArgument>();
            byte reader = br_read(1)[0];

            // NOTE ON CODE CHUNK ARGUMENTS
            // This determins type definition
            int ccount = 0;
            while (reader != 0x000)
            {
                ccount++;
                Common.SendToDebug("Reading Code Chunk Argument " + ccount);
                LSO_Struct.CodeChunkArgument CCA = new LSO_Struct.CodeChunkArgument();
                CCA.FunctionReturnTypePos = reader;
                reader = br_read(1)[0];
                CCA.NullString = reader;
                CCA.FunctionReturnType = GetStaticBlock(CCA.FunctionReturnTypePos + myHeader.GVR);
                myCodeChunk.CodeChunkArguments.Add(CCA);
                Common.SendToDebug("Code Chunk Argument " + ccount + " type #" + CCA.FunctionReturnType.ObjectType +
                                   ": " + (LSO_Enums.Variable_Type_Codes) CCA.FunctionReturnType.ObjectType);
            }
            // Create string array
            Type[] MethodArgs = new Type[myCodeChunk.CodeChunkArguments.Count];
            for (int _ic = 0; _ic < myCodeChunk.CodeChunkArguments.Count; _ic++)
            {
                MethodArgs[_ic] = getLLObjectType(myCodeChunk.CodeChunkArguments[_ic].FunctionReturnType.ObjectType);
                Common.SendToDebug("Method argument " + _ic + ": " +
                                   getLLObjectType(myCodeChunk.CodeChunkArguments[_ic].FunctionReturnType.ObjectType).
                                       ToString());
            }
            // End marker is 0x000
            myCodeChunk.EndMarker = reader;

            //
            // Emit: START OF METHOD (FUNCTION)
            //

            Common.SendToDebug("CLR:" + eventname + ":MethodBuilder methodBuilder = typeBuilder.DefineMethod...");
            MethodBuilder methodBuilder = typeBuilder.DefineMethod(eventname,
                                                                   MethodAttributes.Public,
                                                                   typeof (void),
                                                                   new Type[] {typeof (object)});
            //MethodArgs);
            //typeof(void), //getLLObjectType(myCodeChunk.ReturnType),
            //                new Type[] { typeof(object) }, //);

            //Common.SendToDebug("CLR:" + eventname + ":typeBuilder.DefineMethodOverride(methodBuilder...");
            //typeBuilder.DefineMethodOverride(methodBuilder,
            //        typeof(LSL_CLRInterface.LSLScript).GetMethod(eventname));

            // Create the IL generator

            Common.SendToDebug("CLR:" + eventname + ":ILGenerator il = methodBuilder.GetILGenerator();");
            ILGenerator il = methodBuilder.GetILGenerator();

            if (Common.IL_UseTryCatch)
                IL_INSERT_TRY(il, eventname);

            // Push Console.WriteLine command to stack ... Console.WriteLine("Hello World!");
            //Common.SendToDebug("CLR:" + eventname + ":il.Emit(OpCodes.Call...");
            //il.Emit(OpCodes.Call, typeof(Console).GetMethod
            //    ("WriteLine", new Type[] { typeof(string) }));

            //Common.SendToDebug("STARTUP: il.Emit(OpCodes.Ldc_I4_S, 0);");

            //il.Emit(OpCodes.Ldc_I4_S, 0);
            for (int _ic = 0; _ic < myCodeChunk.CodeChunkArguments.Count; _ic++)
            {
                Common.SendToDebug("PARAMS: il.Emit(OpCodes.Ldarg, " + _ic + ");");
                il.Emit(OpCodes.Ldarg, _ic);
            }

            //
            // CALLING OPCODE PROCESSOR, one command at the time TO GENERATE IL
            //
            bool FoundRet = false;
            while (FoundRet == false)
            {
                FoundRet = LSL_PROCESS_OPCODE(il);
            }

            if (Common.IL_UseTryCatch)
                IL_INSERT_END_TRY(il, eventname);

            // Emit: RETURN FROM METHOD
            il.Emit(OpCodes.Ret);

            return;
        }

        private void IL_INSERT_FUNCTIONLIST()
        {
            Common.SendToDebug("Creating function list");

            string eventname = "GetFunctions";

            Common.SendToDebug("Creating IL " + eventname);
            // Define a private String field.
            //FieldBuilder myField = myTypeBuilder.DefineField("EventList", typeof(String[]), FieldAttributes.Public);

            //FieldBuilder mem = typeBuilder.DefineField("mem", typeof(Array), FieldAttributes.Private);

            MethodBuilder methodBuilder = typeBuilder.DefineMethod(eventname,
                                                                   MethodAttributes.Public,
                                                                   typeof (string[]),
                                                                   null);

            //typeBuilder.DefineMethodOverride(methodBuilder,
            //                            typeof(LSL_CLRInterface.LSLScript).GetMethod(eventname));

            ILGenerator il = methodBuilder.GetILGenerator();

            //    IL_INSERT_TRY(il, eventname);

            //                // Push string to stack
            //    il.Emit(OpCodes.Ldstr, "Inside " + eventname);

            //// Push Console.WriteLine command to stack ... Console.WriteLine("Hello World!");
            //il.Emit(OpCodes.Call, typeof(Console).GetMethod
            //    ("WriteLine", new Type[] { typeof(string) }));

            //initIL.Emit(OpCodes.Newobj, typeof(string[]));

            //string[] MyArray = new string[2] { "TestItem1" , "TestItem2" };

            ////il.Emit(OpCodes.Ldarg_0);

            il.DeclareLocal(typeof (string[]));

            ////il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldc_I4, EventList.Count); // Specify array length
            il.Emit(OpCodes.Newarr, typeof (String)); // create new string array
            il.Emit(OpCodes.Stloc_0); // Store array as local variable 0 in stack
            ////SetFunctionList

            for (int lv = 0; lv < EventList.Count; lv++)
            {
                il.Emit(OpCodes.Ldloc_0); // Load local variable 0 onto stack
                il.Emit(OpCodes.Ldc_I4, lv); // Push index position
                il.Emit(OpCodes.Ldstr, EventList[lv]); // Push value
                il.Emit(OpCodes.Stelem_Ref); // Perform array[index] = value

                //il.Emit(OpCodes.Ldarg_0);
                //il.Emit(OpCodes.Ldstr, EventList[lv]);         // Push value
                //il.Emit(OpCodes.Call, typeof(LSL_BaseClass).GetMethod("AddFunction", new Type[] { typeof(string) }));
            }

            // IL_INSERT_END_TRY(il, eventname);

            il.Emit(OpCodes.Ldloc_0); // Load local variable 0 onto stack
            //                il.Emit(OpCodes.Call, typeof(LSL_BaseClass).GetMethod("SetFunctionList", new Type[] { typeof(Array) }));

            il.Emit(OpCodes.Ret); // Return
        }

        private void IL_INSERT_TRY(ILGenerator il, string eventname)
        {
            /*
             * CLR TRY
             */
            //Common.SendToDebug("CLR:" + eventname + ":il.BeginExceptionBlock()");
            il.BeginExceptionBlock();

            // Push "Hello World!" string to stack
            //Common.SendToDebug("CLR:" + eventname + ":il.Emit(OpCodes.Ldstr...");
            //il.Emit(OpCodes.Ldstr, "Starting CLR dynamic execution of: " + eventname);
        }

        private void IL_INSERT_END_TRY(ILGenerator il, string eventname)
        {
            /*
             * CATCH
             */
            Common.SendToDebug("CLR:" + eventname + ":il.BeginCatchBlock(typeof(Exception));");
            il.BeginCatchBlock(typeof (Exception));

            // Push "Hello World!" string to stack
            Common.SendToDebug("CLR:" + eventname + ":il.Emit(OpCodes.Ldstr...");
            il.Emit(OpCodes.Ldstr, "Execption executing dynamic CLR function " + eventname + ": ");

            //call void [mscorlib]System.Console::WriteLine(string)
            Common.SendToDebug("CLR:" + eventname + ":il.Emit(OpCodes.Call...");
            il.Emit(OpCodes.Call, typeof (Console).GetMethod
                                      ("Write", new Type[] {typeof (string)}));

            //callvirt instance string [mscorlib]System.Exception::get_Message()
            Common.SendToDebug("CLR:" + eventname + ":il.Emit(OpCodes.Callvirt...");
            il.Emit(OpCodes.Callvirt, typeof (Exception).GetMethod
                                          ("get_Message"));

            //call void [mscorlib]System.Console::WriteLine(string)
            Common.SendToDebug("CLR:" + eventname + ":il.Emit(OpCodes.Call...");
            il.Emit(OpCodes.Call, typeof (Console).GetMethod
                                      ("WriteLine", new Type[] {typeof (string)}));

            /*
             * CLR END TRY
             */
            //Common.SendToDebug("CLR:" + eventname + ":il.EndExceptionBlock();");
            il.EndExceptionBlock();
        }

        private LSO_Struct.StaticBlock GetStaticBlock(long pos)
        {
            long FirstPos = fs.Position;
            try
            {
                UInt32 position = (UInt32) pos;
                // STATIC BLOCK
                Common.SendToDebug("Reading STATIC BLOCK at: " + position);
                fs.Seek(position, SeekOrigin.Begin);

                if (StaticBlocks.ContainsKey(position) == true)
                {
                    Common.SendToDebug("Found cached STATIC BLOCK");

                    return StaticBlocks[pos];
                }

                //int StaticBlockCount = 0;
                // Read function blocks until we hit GFR
                //while (fs.Position < myHeader.GFR)
                //{
                //StaticBlockCount++;

                //Common.SendToDebug("Reading Static Block at: " + position);

                //fs.Seek(myHeader.GVR, SeekOrigin.Begin);
                LSO_Struct.StaticBlock myStaticBlock = new LSO_Struct.StaticBlock();
                myStaticBlock.Static_Chunk_Header_Size = BitConverter.ToUInt32(br_read(4), 0);
                myStaticBlock.ObjectType = br_read(1)[0];
                Common.SendToDebug("Static Block ObjectType: " +
                                   ((LSO_Enums.Variable_Type_Codes) myStaticBlock.ObjectType).ToString());
                myStaticBlock.Unknown = br_read(1)[0];
                // Size of datatype varies
                if (myStaticBlock.ObjectType != 0)
                    myStaticBlock.BlockVariable = br_read(getObjectSize(myStaticBlock.ObjectType));

                StaticBlocks.Add(position, myStaticBlock);
                //}
                Common.SendToDebug("Done reading Static Block.");
                return myStaticBlock;
            }
            finally
            {
                // Go back to original read pos
                fs.Seek(FirstPos, SeekOrigin.Begin);
            }
        }
    }
}
