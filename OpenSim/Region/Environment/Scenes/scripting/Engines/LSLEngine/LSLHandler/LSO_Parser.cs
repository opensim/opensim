    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.IO;
    using System.Reflection;
    using System.Reflection.Emit;
using OpenSim.Region.Scripting;

    namespace OpenSim.ScriptEngines.LSL
    {
        class LSO_Parser
        {
            private bool Debug = true;
            private FileStream fs;
            private BinaryReader br;
            private LSO_Struct.Header myHeader;

            private TypeBuilder typeBuilder;
            private ScriptInfo WorldAPI;

            /// <summary>
            /// Parse LSO file.
            /// Reads LSO ByteCode into memory structures.
            /// TODO: What else does it do?
            /// </summary>
            /// <param name="FileName">FileName of LSO ByteCode file</param>
            public void ParseFile(string FileName, ScriptInfo _WorldAPI, ref TypeBuilder _typeBuilder)
            {
                typeBuilder = _typeBuilder;
                WorldAPI = _WorldAPI;
                // Open
                SendToDebug("Opening filename: " + FileName);
                fs = File.Open(FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                br = new BinaryReader(fs, Encoding.BigEndianUnicode);
                

                // The LSO Format consist of 6 major blocks: header, statics, functions, states, heap, and stack. 


                // HEADER BLOCK
                SendToDebug("Reading HEADER BLOCK at: 0");
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
                SendToDebug("TM - Top of memory (size): " + myHeader.TM);
                SendToDebug("IP - Instruction Pointer (0=not running): " + myHeader.IP);
                SendToDebug("VN - Version number: " + myHeader.VN);
                SendToDebug("BP - Local Frame Pointer: " + myHeader.BP);
                SendToDebug("SP - Stack Pointer: " + myHeader.SP);
                SendToDebug("HR - Heap Register: " + myHeader.HR);
                SendToDebug("HP - Heap Pointer: " + myHeader.HP);
                SendToDebug("CS - Current State: " + myHeader.CS);
                SendToDebug("NS - Next State: " + myHeader.NS);
                SendToDebug("CE - Current Events: " + myHeader.CE);
                SendToDebug("IE - In Event: " + myHeader.IE);
                SendToDebug("ER - Event Register: " + myHeader.ER);
                SendToDebug("FR - Fault Register: " + myHeader.FR);
                SendToDebug("SLR - Sleep Register: " + myHeader.SLR);
                SendToDebug("GVR - Global Variable Register: " + myHeader.GVR);
                SendToDebug("GFR - Global Function Register: " + myHeader.GFR);
                SendToDebug("PR - Parameter Register: " + myHeader.PR);
                SendToDebug("ESR - Energy Supply Register: " + myHeader.ESR);
                SendToDebug("SR - State Register: " + myHeader.SR);
                SendToDebug("NCE - 64-bit Current Events: " + myHeader.NCE);
                SendToDebug("NIE - 64-bit In Events: " + myHeader.NIE);
                SendToDebug("NER - 64-bit Event Register: " + myHeader.NER);
                SendToDebug("Read position when exiting HEADER BLOCK: " + fs.Position);

                // STATIC BLOCK
                SendToDebug("Reading STATIC BLOCK at: " + myHeader.GVR);
                fs.Seek(myHeader.GVR, SeekOrigin.Begin);
                int StaticBlockCount = 0;
                // Read function blocks until we hit GFR
                while (fs.Position < myHeader.GFR)
                {
                    StaticBlockCount++;
                    SendToDebug("Reading Static Block " + StaticBlockCount + " at: " + fs.Position);
                    //fs.Seek(myHeader.GVR, SeekOrigin.Begin);
                    LSO_Struct.StaticBlock myStaticBlock = new LSO_Struct.StaticBlock();
                    myStaticBlock.Static_Chunk_Header_Size = BitConverter.ToUInt32(br_read(4), 0);
                    myStaticBlock.ObjectType = br_read(1)[0];
                    SendToDebug("Static Block ObjectType: " + ((LSO_Enums.Variable_Type_Codes)myStaticBlock.ObjectType).ToString());
                    myStaticBlock.Unknown = br_read(1)[0];
                    // Size of datatype varies
                    if (myStaticBlock.ObjectType != 0)
                        myStaticBlock.BlockVariable = br_read(getObjectSize(myStaticBlock.ObjectType));
                }
                SendToDebug("Number of Static Blocks read: " + StaticBlockCount);


                // FUNCTION BLOCK
                // Always right after STATIC BLOCK
                LSO_Struct.FunctionBlock myFunctionBlock = new LSO_Struct.FunctionBlock();
                if (myHeader.GFR == myHeader.SR)
                {
                    // If GFR and SR are at same position then there is no fuction block
                    SendToDebug("No FUNCTION BLOCK found");
                } else {
                    SendToDebug("Reading FUNCTION BLOCK at: " + myHeader.GFR);
                    fs.Seek(myHeader.GFR, SeekOrigin.Begin);
                    myFunctionBlock.FunctionCount = BitConverter.ToUInt32(br_read(4), 0);
                    SendToDebug("Number of functions in Fuction Block: " + myFunctionBlock.FunctionCount);
                    if (myFunctionBlock.FunctionCount > 0)
                    {
                        myFunctionBlock.CodeChunkPointer = new UInt32[myFunctionBlock.FunctionCount];
                        for (int i = 0; i < myFunctionBlock.FunctionCount; i++)
                        {
                            SendToDebug("Reading function " + i + " at: " + fs.Position);
                            // TODO: ADD TO FUNCTION LIST (How do we identify it later?)
                            // Note! Absolute position
                            myFunctionBlock.CodeChunkPointer[i] = BitConverter.ToUInt32(br_read(4), 0) + myHeader.GFR;
                            SendToDebug("Fuction " + i + " code chunk position: " + myFunctionBlock.CodeChunkPointer[i]);
                        }
                    }
                }


                // STATE FRAME BLOCK
                // Always right after FUNCTION BLOCK
                SendToDebug("Reading STATE BLOCK at: " + myHeader.SR);
                fs.Seek(myHeader.SR, SeekOrigin.Begin);
                LSO_Struct.StateFrameBlock myStateFrameBlock = new LSO_Struct.StateFrameBlock();
                myStateFrameBlock.StateCount = BitConverter.ToUInt32(br_read(4), 0);
                if (myStateFrameBlock.StateCount > 0)
                {
                    // Initialize array
                    myStateFrameBlock.StatePointer = new LSO_Struct.StatePointerBlock[myStateFrameBlock.StateCount];
                    for (int i = 0; i < myStateFrameBlock.StateCount; i++)
                    {
                        SendToDebug("Reading STATE POINTER BLOCK " + (i+1) + " at: " + fs.Position);
                        // Position is relative to state frame
                        myStateFrameBlock.StatePointer[i].Location = myHeader.SR + BitConverter.ToUInt32(br_read(4), 0);
                        myStateFrameBlock.StatePointer[i].EventMask = new System.Collections.BitArray(br_read(8));
                        SendToDebug("Pointer: " + myStateFrameBlock.StatePointer[i].Location);
                        SendToDebug("Total potential EventMask bits: " + myStateFrameBlock.StatePointer[i].EventMask.Count);

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
                        SendToDebug("Reading STATE BLOCK " + (i + 1) + " at: " + fs.Position);

                        // READ: STATE BLOCK HEADER
                        myStateFrameBlock.StatePointer[i].StateBlock = new LSO_Struct.StateBlock();
                        myStateFrameBlock.StatePointer[i].StateBlock.StartPos = (UInt32)fs.Position; // Note
                        myStateFrameBlock.StatePointer[i].StateBlock.HeaderSize = BitConverter.ToUInt32(br_read(4), 0);
                        myStateFrameBlock.StatePointer[i].StateBlock.Unknown = br_read(1)[0];
                        myStateFrameBlock.StatePointer[i].StateBlock.EndPos = (UInt32)fs.Position; // Note
                        SendToDebug("State block Start Pos: " + myStateFrameBlock.StatePointer[i].StateBlock.StartPos);
                        SendToDebug("State block Header Size: " + myStateFrameBlock.StatePointer[i].StateBlock.HeaderSize);
                        SendToDebug("State block Header End Pos: " + myStateFrameBlock.StatePointer[i].StateBlock.EndPos);

                        // We need to count number of bits flagged in EventMask?


                        // for each bit in myStateFrameBlock.StatePointer[i].EventMask

                        // ADDING TO ALL RIGHT NOW, SHOULD LIMIT TO ONLY THE ONES IN USE
                        //TODO: Create event hooks
                        myStateFrameBlock.StatePointer[i].StateBlock.StateBlockHandlers = new LSO_Struct.StateBlockHandler[myStateFrameBlock.StatePointer[i].EventMask.Count - 1];
                        for (int ii = 0; ii < myStateFrameBlock.StatePointer[i].EventMask.Count - 1; ii++)
                        {
                            
                            if (myStateFrameBlock.StatePointer[i].EventMask.Get(ii) == true)
                            {
                                // We got an event
                                //  READ: STATE BLOCK HANDLER
                                SendToDebug("Reading STATE BLOCK " + (i + 1) + " HANDLER matching EVENT MASK " + ii  + " (" + ((LSO_Enums.Event_Mask_Values)ii).ToString() + ") at: " + fs.Position);
                                myStateFrameBlock.StatePointer[i].StateBlock.StateBlockHandlers[ii].CodeChunkPointer = myStateFrameBlock.StatePointer[i].StateBlock.EndPos + BitConverter.ToUInt32(br_read(4), 0);
                                myStateFrameBlock.StatePointer[i].StateBlock.StateBlockHandlers[ii].CallFrameSize = BitConverter.ToUInt32(br_read(4), 0);
                                SendToDebug("Reading STATE BLOCK " + (i + 1) + " HANDLER EVENT MASK " + ii + " (" + ((LSO_Enums.Event_Mask_Values)ii).ToString() + ") Code Chunk Pointer: " + myStateFrameBlock.StatePointer[i].StateBlock.StateBlockHandlers[ii].CodeChunkPointer);
                                SendToDebug("Reading STATE BLOCK " + (i + 1) + " HANDLER EVENT MASK " + ii + " (" + ((LSO_Enums.Event_Mask_Values)ii).ToString() + ") Call Frame Size: " + myStateFrameBlock.StatePointer[i].StateBlock.StateBlockHandlers[ii].CallFrameSize );
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
                //        SendToDebug("Reading Function Code Chunk " + i);
                //        myFunctionCodeChunk[i] = GetCodeChunk((UInt32)myFunctionBlock.CodeChunkPointer[i]);
                //    }

                //}
                // READ EVENT CODE CHUNKS
                LSO_Struct.CodeChunk[] myEventCodeChunk;
                if (myStateFrameBlock.StateCount > 0)
                {
                    myEventCodeChunk = new LSO_Struct.CodeChunk[myStateFrameBlock.StateCount];
                    for (int i = 0; i < myStateFrameBlock.StateCount; i++)
                    {
                        // TODO: Somehow organize events and functions so they can be found again, 
                        // two level search ain't no good
                        for (int ii = 0; ii < myStateFrameBlock.StatePointer[i].EventMask.Count - 1; ii++)
                        {


                            if (myStateFrameBlock.StatePointer[i].StateBlock.StateBlockHandlers[ii].CodeChunkPointer > 0)
                            {
                                    SendToDebug("Reading Event Code Chunk state " + i + ", event " + (LSO_Enums.Event_Mask_Values)ii);


                                    // Override a Method / Function
                                    string eventname = "event_" + (LSO_Enums.Event_Mask_Values)ii;
                                    SendToDebug("CLR:" + eventname + ":MethodBuilder methodBuilder = typeBuilder.DefineMethod...");
                                    MethodBuilder methodBuilder = typeBuilder.DefineMethod(eventname,
                                                 MethodAttributes.Private | MethodAttributes.Virtual,
                                                 typeof(void),
                                                 new Type[] { typeof(object) });

                                    SendToDebug("CLR:" + eventname + ":typeBuilder.DefineMethodOverride(methodBuilder...");
                                    typeBuilder.DefineMethodOverride(methodBuilder,
                                            typeof(LSL_CLRInterface.LSLScript).GetMethod(eventname));

                                    // Create the IL generator

                                    SendToDebug("CLR:" + eventname + ":ILGenerator il = methodBuilder.GetILGenerator();");
                                    ILGenerator il = methodBuilder.GetILGenerator();


                                    LSO_Struct.CodeChunk myECC =
                                        GetCodeChunk(myStateFrameBlock.StatePointer[i].StateBlock.StateBlockHandlers[ii].CodeChunkPointer, il, eventname);
                            }
                            
                        }
                    }

                }


                // Close
                br.Close();
                fs.Close();

            }

            private LSO_Struct.HeapBlock GetHeap(UInt32 pos)
            {
                // HEAP BLOCK
                // TODO:? Special read for strings/keys (null terminated) and lists (pointers to other HEAP entries)
                SendToDebug("Reading HEAP BLOCK at: " + pos);
                fs.Seek(pos, SeekOrigin.Begin);
                
                LSO_Struct.HeapBlock myHeapBlock = new LSO_Struct.HeapBlock();
                myHeapBlock.DataBlockSize = BitConverter.ToUInt32(br_read(4), 0);
                myHeapBlock.ObjectType = br_read(1)[0];
                myHeapBlock.ReferenceCount = BitConverter.ToUInt16(br_read(2), 0);
                myHeapBlock.Data = br_read(getObjectSize(myHeapBlock.ObjectType));

                SendToDebug("Heap Block Data Block Size: " + myHeapBlock.DataBlockSize);
                SendToDebug("Heap Block ObjectType: " + ((LSO_Enums.Variable_Type_Codes)myHeapBlock.ObjectType).ToString());
                SendToDebug("Heap Block Reference Count: " + myHeapBlock.ReferenceCount);

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
                catch (Exception e)
                {
                    SendToDebug("Exception: " + e.ToString());
                    throw (e);
                }
            }
            //private byte[] br_read_smallendian(int len)
            //{
            //    byte[] bytes = new byte[len];    
            //    br.Read(bytes,0, len);
            //    return bytes;
            //}

            private int getObjectSize(byte ObjectType)
            {
                switch (ObjectType)
                {
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                    case 7:
                        return 4;
                    case 5:
                        return 12;
                    case 6:
                        return 16;
                    default:
                        return 0;
                }
            }
            private void SendToDebug(string Message)
            {
                if (Debug == true)
                    Console.WriteLine("Debug: " + Message);
            }


            private string Read_String()
            {
                string ret = "";
                byte reader = br_read(1)[0];
                while (reader != 0x000)
                {
                    ret += (char)reader;
                    reader = br_read(1)[0];
                }
                return ret;
            }

             /// <summary>
            /// Reads a code chunk into structure and returns it.
            /// </summary>
            /// <param name="pos">Absolute position in file. REMEMBER TO ADD myHeader.GFR!</param>
            /// <returns></returns>
            private LSO_Struct.CodeChunk GetCodeChunk(UInt32 pos, ILGenerator il, string eventname)
            {

                /*
                 * CLR TRY
                 */
                //SendToDebug("CLR:" + eventname + ":il.BeginExceptionBlock()");
                il.BeginExceptionBlock();

                // Push "Hello World!" string to stack
                //SendToDebug("CLR:" + eventname + ":il.Emit(OpCodes.Ldstr...");
                il.Emit(OpCodes.Ldstr, "Starting CLR dynamic execution of: " + eventname);

                // Push Console.WriteLine command to stack ... Console.WriteLine("Hello World!");
                //SendToDebug("CLR:" + eventname + ":il.Emit(OpCodes.Call...");
                il.Emit(OpCodes.Call, typeof(Console).GetMethod
                    ("WriteLine", new Type[] { typeof(string) }));


                LSO_Struct.CodeChunk myCodeChunk = new LSO_Struct.CodeChunk();

                SendToDebug("Reading Function Code Chunk at: " + pos);
                fs.Seek(pos, SeekOrigin.Begin);
                myCodeChunk.CodeChunkHeaderSize = BitConverter.ToUInt32(br_read(4), 0);
                SendToDebug("CodeChunk Header Size: " + myCodeChunk.CodeChunkHeaderSize );
                // Read until null
                myCodeChunk.Comment = Read_String();
                SendToDebug("Function comment: " + myCodeChunk.Comment);
                myCodeChunk.ReturnType = br_read(1)[0];
                SendToDebug("Return type: " + (LSO_Enums.Variable_Type_Codes)myCodeChunk.ReturnType);
                // TODO: How to determine number of codechunks -- does this method work?
                myCodeChunk.CodeChunkArguments = new System.Collections.Generic.List<LSO_Struct.CodeChunkArgument>();
                byte reader = br_read(1)[0];
                reader = br_read(1)[0];
                int ccount = 0;
                while (reader != 0x000)
                {
                    ccount++;
                    SendToDebug("Reading Code Chunk Argument " + ccount);
                    LSO_Struct.CodeChunkArgument CCA = new LSO_Struct.CodeChunkArgument();
                    CCA.FunctionReturnType = reader;
                    reader = br_read(1)[0];
                    CCA.NullString = reader;
                    myCodeChunk.CodeChunkArguments.Add(CCA);
                    SendToDebug("Code Chunk Argument " + ccount + " return type: " + (LSO_Enums.Variable_Type_Codes)CCA.FunctionReturnType);
                }
                // End marker is 0x000
                myCodeChunk.EndMarker = reader;
                // TODO: How to read and identify following code
                // TODO: Code is read until a return of some sort is found
                bool FoundRet = false;
                while (FoundRet == false)
                {
                        //reader = br_read(1)[0];
                    //UInt16  opcode = BitConverter.ToUInt16(br_read(1),0);
                    UInt16 opcode = br_read(1)[0];
                    //long rPos = fs.Position;
                    SendToDebug("OPCODE: " + ((LSO_Enums.Operation_Table)opcode).ToString());
                    switch (opcode)
                    {
                            // LONG
                        case (UInt16)LSO_Enums.Operation_Table.POPARG:
                        case (UInt16)LSO_Enums.Operation_Table.STORE:
                        case (UInt16)LSO_Enums.Operation_Table.STORES:
                        case (UInt16)LSO_Enums.Operation_Table.STOREL:
                        case (UInt16)LSO_Enums.Operation_Table.STOREV:
                        case (UInt16)LSO_Enums.Operation_Table.STOREQ:
                        case (UInt16)LSO_Enums.Operation_Table.STOREG:
                        case (UInt16)LSO_Enums.Operation_Table.STOREGS:
                        case (UInt16)LSO_Enums.Operation_Table.STOREGL:
                        case (UInt16)LSO_Enums.Operation_Table.STOREGV:
                        case (UInt16)LSO_Enums.Operation_Table.STOREGQ:
                        case (UInt16)LSO_Enums.Operation_Table.LOADP:
                        case (UInt16)LSO_Enums.Operation_Table.LOADSP:
                        case (UInt16)LSO_Enums.Operation_Table.LOADLP:
                        case (UInt16)LSO_Enums.Operation_Table.LOADVP:
                        case (UInt16)LSO_Enums.Operation_Table.LOADQP:
                        case (UInt16)LSO_Enums.Operation_Table.PUSH:
                        case (UInt16)LSO_Enums.Operation_Table.PUSHS:
                        case (UInt16)LSO_Enums.Operation_Table.PUSHL:
                        case (UInt16)LSO_Enums.Operation_Table.PUSHV:
                        case (UInt16)LSO_Enums.Operation_Table.PUSHQ:
                        case (UInt16)LSO_Enums.Operation_Table.PUSHG:
                        case (UInt16)LSO_Enums.Operation_Table.PUSHGS:
                        case (UInt16)LSO_Enums.Operation_Table.PUSHGL:
                        case (UInt16)LSO_Enums.Operation_Table.PUSHGV:
                        case (UInt16)LSO_Enums.Operation_Table.PUSHGQ:
                            SendToDebug("Param1: " + BitConverter.ToUInt32(br_read(4),0));
                            break;
                            // BYTE
                        case (UInt16)LSO_Enums.Operation_Table.PUSHARGB:
                            SendToDebug("Param1: " + br_read(1)[0]);
                            break;
                            // INTEGER
                        case (UInt16)LSO_Enums.Operation_Table.PUSHARGI:
                            // TODO: What is size of integer?
                            SendToDebug("Param1: " + BitConverter.ToUInt32(br_read(4),0));
                            break;
                            // FLOAT
                        case (UInt16)LSO_Enums.Operation_Table.PUSHARGF:
                            // TODO: What is size of float?
                            SendToDebug("Param1: " + BitConverter.ToUInt32(br_read(4),0));
                            break;
                            // STRING
                        case (UInt16)LSO_Enums.Operation_Table.PUSHARGS:
                            string s = Read_String();
                            SendToDebug("Param1: " + s);
                            il.Emit(OpCodes.Ldstr, s);
                            break;
                            // VECTOR z,y,x
                        case (UInt16)LSO_Enums.Operation_Table.PUSHARGV:
                            SendToDebug("Param1 Z: " + BitConverter.ToUInt32(br_read(4),0));
                            SendToDebug("Param1 Y: " + BitConverter.ToUInt32(br_read(4),0));
                            SendToDebug("Param1 X: " + BitConverter.ToUInt32(br_read(4),0));
                            break;
                            // ROTATION s,z,y,x
                        case (UInt16)LSO_Enums.Operation_Table.PUSHARGQ:
                            SendToDebug("Param1 S: " + BitConverter.ToUInt32(br_read(4),0));
                            SendToDebug("Param1 Z: " + BitConverter.ToUInt32(br_read(4),0));
                            SendToDebug("Param1 Y: " + BitConverter.ToUInt32(br_read(4),0));
                            SendToDebug("Param1 X: " + BitConverter.ToUInt32(br_read(4),0));
                            break;
                            // LONG
                        case (UInt16)LSO_Enums.Operation_Table.PUSHARGE:
                            SendToDebug("Param1: " + BitConverter.ToUInt32(br_read(4),0));
                            break;
                            // BYTE
                        case (UInt16)LSO_Enums.Operation_Table.ADD:
                        case (UInt16)LSO_Enums.Operation_Table.SUB:
                        case (UInt16)LSO_Enums.Operation_Table.MUL:
                        case (UInt16)LSO_Enums.Operation_Table.DIV:
                        case (UInt16)LSO_Enums.Operation_Table.MOD:
                        case (UInt16)LSO_Enums.Operation_Table.EQ:
                        case (UInt16)LSO_Enums.Operation_Table.NEQ:
                        case (UInt16)LSO_Enums.Operation_Table.LEQ:
                        case (UInt16)LSO_Enums.Operation_Table.GEQ:
                        case (UInt16)LSO_Enums.Operation_Table.LESS:
                        case (UInt16)LSO_Enums.Operation_Table.GREATER:
                        case (UInt16)LSO_Enums.Operation_Table.BOOLOR:
                            SendToDebug("Param1: " + br_read(1)[0]);
                            break;
                            // LONG
                        case (UInt16)LSO_Enums.Operation_Table.JUMP:
                            SendToDebug("Param1: " + BitConverter.ToUInt32(br_read(4),0));
                            break;
                            // BYTE, LONG
                        case (UInt16)LSO_Enums.Operation_Table.JUMPIF:
                        case (UInt16)LSO_Enums.Operation_Table.JUMPNIF:
                            SendToDebug("Param1: " + br_read(1)[0]);
                            SendToDebug("Param2: " + BitConverter.ToUInt32(br_read(4),0));
                            break;
                            // LONG
                        case (UInt16)LSO_Enums.Operation_Table.STATE:
                        case (UInt16)LSO_Enums.Operation_Table.CALL:
                            SendToDebug("Param1: " + BitConverter.ToUInt32(br_read(4),0));
                            break;
                            // BYTE
                        case (UInt16)LSO_Enums.Operation_Table.CAST:
                            SendToDebug("Param1: " + br_read(1)[0]);
                            break;
                            // LONG
                        case (UInt16)LSO_Enums.Operation_Table.STACKTOS:
                        case (UInt16)LSO_Enums.Operation_Table.STACKTOL:
                            SendToDebug("Param1: " + BitConverter.ToUInt32(br_read(4),0));
                            break;
                            // BYTE
                        case (UInt16)LSO_Enums.Operation_Table.PRINT:
                        case (UInt16)LSO_Enums.Operation_Table.CALLLIB:
                            SendToDebug("Param1: " + br_read(1)[0]);
                            break;
                            // SHORT
                        case (UInt16)LSO_Enums.Operation_Table.CALLLIB_TWO_BYTE:
                            // TODO: What is size of short?
                            UInt16 _i = BitConverter.ToUInt16(br_read(2), 0);
                            SendToDebug("Param1: " + _i);
                            switch (_i)
                            {
                                case (UInt16)LSO_Enums.BuiltIn_Functions.llSay:
                                    il.Emit(OpCodes.Call, typeof(Console).GetMethod
                                        ("WriteLine", new Type[] { typeof(string) }));
                                    break;
                            }
                            break;


                        // RETURN
                        case (UInt16)LSO_Enums.Operation_Table.RETURN:
                            SendToDebug("Last OPCODE was return command. Code chunk execution complete.");
                            FoundRet = true;
                            break;
                    }
                    //fs.Seek(rPos, SeekOrigin.Begin);

                }


                /*
                 * CATCH
                 */
                SendToDebug("CLR:" + eventname + ":il.BeginCatchBlock(typeof(Exception));");
                il.BeginCatchBlock(typeof(Exception));

                // Push "Hello World!" string to stack
                SendToDebug("CLR:" + eventname + ":il.Emit(OpCodes.Ldstr...");
                il.Emit(OpCodes.Ldstr, "Execption executing dynamic CLR function " + eventname + ": ");

                //call void [mscorlib]System.Console::WriteLine(string)
                SendToDebug("CLR:" + eventname + ":il.Emit(OpCodes.Call...");
                il.Emit(OpCodes.Call, typeof(Console).GetMethod
                    ("Write", new Type[] { typeof(string) }));

                //callvirt instance string [mscorlib]System.Exception::get_Message()
                SendToDebug("CLR:" + eventname + ":il.Emit(OpCodes.Callvirt...");
                il.Emit(OpCodes.Callvirt, typeof(Exception).GetMethod
                    ("get_Message"));

                //call void [mscorlib]System.Console::WriteLine(string)
                SendToDebug("CLR:" + eventname + ":il.Emit(OpCodes.Call...");
                il.Emit(OpCodes.Call, typeof(Console).GetMethod
                    ("WriteLine", new Type[] { typeof(string) }));

                /*
                 * CLR END TRY
                 */
                //SendToDebug("CLR:" + eventname + ":il.EndExceptionBlock();");
                il.EndExceptionBlock();
                // Push "Return from current method, with return value if present" to stack
                il.Emit(OpCodes.Ret);



                return myCodeChunk;

            }
    }
}
