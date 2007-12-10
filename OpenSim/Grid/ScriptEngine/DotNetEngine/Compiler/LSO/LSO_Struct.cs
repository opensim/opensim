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
* 
*/
/* Original code: Tedd Hansen */

using System;
using System.Collections;
using System.Collections.Generic;

namespace OpenSim.Grid.ScriptEngine.DotNetEngine.Compiler.LSO
{
    internal static class LSO_Struct
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
            public Int32 DataBlockSize;
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
            public BitArray EventMask;
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
            public List<CodeChunkArgument> CodeChunkArguments;
            public byte EndMarker;
            public byte ReturnTypePos;
            public StaticBlock ReturnType;
        }

        public struct CodeChunkArgument
        {
            public byte FunctionReturnTypePos;
            public byte NullString;
            public StaticBlock FunctionReturnType;
        }
    }
}
