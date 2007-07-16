
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.ScriptEngines.LSL
{
    static class LSO_Struct
    {

        public struct Header
        {
            public UInt32 TM;
            public UInt32 IP;
            public UInt32 VN;
            public UInt32 BP;
            public UInt32 SP;
            public UInt32 HR;
            public UInt32 HP;
            public UInt32 CS;
            public UInt32 NS;
            public UInt32 CE;
            public UInt32 IE;
            public UInt32 ER;
            public UInt32 FR;
            public UInt32 SLR;
            public UInt32 GVR;
            public UInt32 GFR;
            public UInt32 PR;
            public UInt32 ESR;
            public UInt32 SR;
            public UInt64 NCE;
            public UInt64 NIE;
            public UInt64 NER;
        }

        public struct StaticBlock
        {
            public UInt32 Static_Chunk_Header_Size;
            public byte ObjectType;
            public byte Unknown;
            public byte[] BlockVariable;
        }
        /* Not actually a structure
        public struct StaticBlockVariable
        {
            public UInt32 Integer1;
            public UInt32 Float1;
            public UInt32 HeapPointer_String;
            public UInt32 HeapPointer_Key;
            public byte[] Vector_12;
            public byte[] Rotation_16;
            public UInt32 Pointer_List_Structure;
        } */
        public struct HeapBlock
        {
            public UInt32 DataBlockSize;
            public byte ObjectType;
            public UInt16 ReferenceCount;
            public byte[] Data;
        }
        public struct StateFrameBlock
        {
            public UInt32 StateCount;
            public StatePointerBlock[] StatePointer;
        }
        public struct StatePointerBlock
        {
            public UInt32 Location;
            public System.Collections.BitArray EventMask;
            public StateBlock StateBlock;
        }
        public struct StateBlock
        {
            public UInt32 StartPos;
            public UInt32 EndPos;
            public UInt32 HeaderSize;
            public byte Unknown;
            public StateBlockHandler[] StateBlockHandlers;
        }
        public struct StateBlockHandler
        {
            public UInt32 CodeChunkPointer;
            public UInt32 CallFrameSize;
        }
        public struct FunctionBlock
        {
            public UInt32 FunctionCount;
            public UInt32[] CodeChunkPointer;
        }
        public struct CodeChunk
        {
            public UInt32 CodeChunkHeaderSize;
            public string Comment;
            public System.Collections.Generic.List<CodeChunkArgument> CodeChunkArguments;
            public byte EndMarker;
            public byte ReturnType;
        }
        public struct CodeChunkArgument
        {
            public byte FunctionReturnType;
            public byte NullString;
        }
    }
}
