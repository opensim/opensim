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
using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.Scripting.EmbeddedJVM.Types;
using OpenSim.Region.Scripting.EmbeddedJVM.Types.PrimitiveTypes;

namespace OpenSim.Region.Scripting.EmbeddedJVM
{
    partial class Thread
    {
        private partial class Interpreter
        {
            private bool IsLogicOpCode(byte opcode)
            {
                bool result = false;
                switch (opcode)
                {
                    case (byte)(byte)OpCode.iconst_m1:
                        Int m_int = new Int();
                        m_int.mValue = -1;
                        this._mThread.currentFrame.OpStack.Push(m_int);
                        result = true;
                        break;
                    case (byte)(byte)OpCode.iconst_0:
                        m_int = new Int();
                        m_int.mValue = 0;
                        this._mThread.currentFrame.OpStack.Push(m_int);
                        result = true;
                        break;
                    case (byte)(byte)OpCode.iconst_1:
                        m_int = new Int();
                        m_int.mValue = 1;
                        this._mThread.currentFrame.OpStack.Push(m_int);
                        result = true;
                        break;
                    case (byte)(byte)OpCode.iconst_2:
                        m_int = new Int();
                        m_int.mValue = 2;
                        this._mThread.currentFrame.OpStack.Push(m_int);
                        result = true;
                        break;
                    case (byte)(byte)OpCode.iconst_3:
                        m_int = new Int();
                        m_int.mValue = 3;
                        this._mThread.currentFrame.OpStack.Push(m_int);
                        break;
                    case (byte)(byte)OpCode.iconst_4:
                        m_int = new Int();
                        m_int.mValue = 4;
                        this._mThread.currentFrame.OpStack.Push(m_int);
                        result = true;
                        break;
                    case (byte)OpCode.iconst_5:
                        m_int = new Int();
                        m_int.mValue = 5;
                        this._mThread.currentFrame.OpStack.Push(m_int);
                        result = true;
                        break;
                    case (byte)OpCode.fconst_0:
                        Float m_float = new Float();
                        m_float.mValue = 0.0f;
                        this._mThread.currentFrame.OpStack.Push(m_float);
                        result = true;
                        break;
                    case (byte)OpCode.fconst_1:
                        m_float = new Float();
                        m_float.mValue = 1.0f;
                        this._mThread.currentFrame.OpStack.Push(m_float);
                        result = true;
                        break;
                    case (byte)OpCode.fconst_2:
                        m_float = new Float();
                        m_float.mValue = 2.0f;
                        this._mThread.currentFrame.OpStack.Push(m_float);
                        result = true;
                        break;
                    case (byte)OpCode.bipush:  //is this right? this should be pushing a byte onto stack not int?
                        int pushvalue = (int)GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC];
                        Int pushInt = new Int();
                        pushInt.mValue = pushvalue;
                        this._mThread.currentFrame.OpStack.Push(pushInt);
                        this._mThread.PC++;
                        result = true;
                        break;
                    case (byte)OpCode.sipush:
                        short pushvalue2 = (short)((GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC] << 8) + GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC + 1]);
                        Int pushInt2 = new Int();
                        pushInt2.mValue = pushvalue2;
                        this._mThread.currentFrame.OpStack.Push(pushInt2);
                        this._mThread.PC += 2;
                        result = true;
                        break;
                    case (byte)OpCode.fload:
                        short findex1 = (short)((GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC]));
                        Float fload = new Float();
                        if (this._mThread.currentFrame.LocalVariables[findex1] != null)
                        {
                            if (this._mThread.currentFrame.LocalVariables[findex1] is Float)
                            {
                                fload.mValue = ((Float)this._mThread.currentFrame.LocalVariables[findex1]).mValue;
                                this._mThread.currentFrame.OpStack.Push(fload);
                            }
                        }
                        this._mThread.PC++;
                        result = true;
                        break;
                    case (byte)OpCode.iload_0:
                        if (this._mThread.currentFrame.LocalVariables[0] != null)
                        {
                            if (this._mThread.currentFrame.LocalVariables[0] is Int)
                            {
                                Int newInt = new Int();
                                newInt.mValue = ((Int)this._mThread.currentFrame.LocalVariables[0]).mValue;
                                this._mThread.currentFrame.OpStack.Push(newInt);
                            }
                        }
                        result = true;
                        break;
                    case (byte)OpCode.iload_1:
                        if (this._mThread.currentFrame.LocalVariables[1] != null)
                        {
                            if (this._mThread.currentFrame.LocalVariables[1] is Int)
                            {
                                Int newInt = new Int();
                                newInt.mValue = ((Int)this._mThread.currentFrame.LocalVariables[1]).mValue;
                                this._mThread.currentFrame.OpStack.Push(newInt);
                            }
                        }
                        result = true;
                        break;
                    case (byte)OpCode.fload_0:
                        if (this._mThread.currentFrame.LocalVariables[0] != null)
                        {
                            if (this._mThread.currentFrame.LocalVariables[0] is Float)
                            {
                                Float newfloat = new Float();
                                newfloat.mValue = ((Float)this._mThread.currentFrame.LocalVariables[0]).mValue;
                                this._mThread.currentFrame.OpStack.Push(newfloat);
                            }
                        }
                        result = true;
                        break;
                    case (byte)OpCode.fload_1:
                        if (this._mThread.currentFrame.LocalVariables[1] != null)
                        {
                            if (this._mThread.currentFrame.LocalVariables[1] is Float)
                            {
                                Float newfloat = new Float();
                                newfloat.mValue = ((Float)this._mThread.currentFrame.LocalVariables[1]).mValue;
                                this._mThread.currentFrame.OpStack.Push(newfloat);
                            }
                        }
                        result = true;
                        break;
                    case (byte)OpCode.fload_2:
                        if (this._mThread.currentFrame.LocalVariables[2] != null)
                        {
                            if (this._mThread.currentFrame.LocalVariables[2] is Float)
                            {
                                Float newfloat = new Float();
                                newfloat.mValue = ((Float)this._mThread.currentFrame.LocalVariables[2]).mValue;
                                this._mThread.currentFrame.OpStack.Push(newfloat);
                            }
                        }
                        result = true;
                        break;
                    case (byte)OpCode.fload_3:
                        if (this._mThread.currentFrame.LocalVariables[3] != null)
                        {
                            if (this._mThread.currentFrame.LocalVariables[3] is Float)
                            {
                                Float newfloat = new Float();
                                newfloat.mValue = ((Float)this._mThread.currentFrame.LocalVariables[3]).mValue;
                                this._mThread.currentFrame.OpStack.Push(newfloat);
                            }
                        }
                        result = true;
                        break;
                    case (byte)OpCode.istore:
                        short findex3 = (short)((GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC]));
                        BaseType istor = this._mThread.currentFrame.OpStack.Pop();
                        if (istor is Int)
                        {
                            this._mThread.currentFrame.LocalVariables[findex3] = (Int)istor;
                        }
                        this._mThread.PC++;
                        result = true;
                        break;
                    case (byte)OpCode.fstore:
                        short findex = (short)((GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC]));
                        BaseType fstor = this._mThread.currentFrame.OpStack.Pop();
                        if (fstor is Float)
                        {
                            this._mThread.currentFrame.LocalVariables[findex] = (Float)fstor;
                        }
                        this._mThread.PC++;
                        result = true;
                        break;
                    case (byte)OpCode.istore_0:
                        BaseType baset = this._mThread.currentFrame.OpStack.Pop();
                        if (baset is Int)
                        {
                            this._mThread.currentFrame.LocalVariables[0] = (Int)baset;
                        }
                        result = true;
                        break;
                    case (byte)OpCode.istore_1:
                        baset = this._mThread.currentFrame.OpStack.Pop();
                        if (baset is Int)
                        {
                            this._mThread.currentFrame.LocalVariables[1] = (Int)baset;
                        }
                        result = true;
                        break;
                    case (byte)OpCode.fstore_0:
                        baset = this._mThread.currentFrame.OpStack.Pop();
                        if (baset is Float)
                        {
                            this._mThread.currentFrame.LocalVariables[0] = (Float)baset;
                        }
                        result = true;
                        break;
                    case (byte)OpCode.fstore_1:
                        baset = this._mThread.currentFrame.OpStack.Pop();
                        if (baset is Float)
                        {
                            this._mThread.currentFrame.LocalVariables[1] = (Float)baset;
                        }
                        result = true;
                        break;
                    case (byte)OpCode.fstore_2:
                        baset = this._mThread.currentFrame.OpStack.Pop();
                        if (baset is Float)
                        {
                            this._mThread.currentFrame.LocalVariables[2] = (Float)baset;
                        }
                        result = true;
                        break;
                    case (byte)OpCode.fstore_3:
                        baset = this._mThread.currentFrame.OpStack.Pop();
                        if (baset is Float)
                        {
                            this._mThread.currentFrame.LocalVariables[3] = (Float)baset;
                        }
                        result = true;
                        break;
                    case (byte)OpCode.pop:
                        this._mThread.currentFrame.OpStack.Pop();
                        result = true;
                        break;
                    case (byte)OpCode.fadd:
                        BaseType bf2 = this._mThread.currentFrame.OpStack.Pop();
                        BaseType bf1 = this._mThread.currentFrame.OpStack.Pop();
                        if (bf1 is Float && bf2 is Float)
                        {
                            Float nflt = new Float();
                            nflt.mValue = ((Float)bf1).mValue + ((Float)bf2).mValue;
                            this._mThread.currentFrame.OpStack.Push(nflt);
                        }
                        result = true;
                        break;
                    case (byte)OpCode.fsub:
                        BaseType bsf2 = this._mThread.currentFrame.OpStack.Pop();
                        BaseType bsf1 = this._mThread.currentFrame.OpStack.Pop();
                        if (bsf1 is Float && bsf2 is Float)
                        {
                            Float resf = new Float();
                            resf.mValue = ((Float)bsf1).mValue - ((Float)bsf2).mValue;
                            this._mThread.currentFrame.OpStack.Push(resf);
                        }
                        result = true;
                        break;
                    case (byte)OpCode.imul: //check the order of the two values off the stack is correct
                        BaseType bs2 = this._mThread.currentFrame.OpStack.Pop();
                        BaseType bs1 = this._mThread.currentFrame.OpStack.Pop();
                        if (bs1 is Int && bs2 is Int)
                        {
                            Int nInt = new Int();
                            nInt.mValue = ((Int)bs1).mValue * ((Int)bs2).mValue;
                            this._mThread.currentFrame.OpStack.Push(nInt);
                        }
                        result = true;
                        break;
                    case (byte)OpCode.iinc:
                        if (this._mThread.currentFrame.LocalVariables[GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC]] != null)
                        {
                            if (this._mThread.currentFrame.LocalVariables[GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC]] is Int)
                            {
                                ((Int)this._mThread.currentFrame.LocalVariables[GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC]]).mValue += (sbyte)GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC + 1];
                            }
                        }
                        this._mThread.PC += 2;
                        result = true;
                        break;
                    case (byte)OpCode.f2i:
                        BaseType conv1 = this._mThread.currentFrame.OpStack.Pop();
                        if (conv1 is Float)
                        {
                            Int newconv = new Int();
                            newconv.mValue = (int)((Float)conv1).mValue;
                            this._mThread.currentFrame.OpStack.Push(newconv);
                        }
                        result = true;
                        break;
                    case (byte)OpCode.fcmpl:
                        BaseType flcom2 = this._mThread.currentFrame.OpStack.Pop();
                        BaseType flcom1 = this._mThread.currentFrame.OpStack.Pop();
                        if (flcom1 is Float && flcom2 is Float)
                        {
                            Int compres = new Int();
                            if (((Float)flcom1).mValue < ((Float)flcom2).mValue)
                            {
                                compres.mValue = -1;
                            }
                            else if (((Float)flcom1).mValue > ((Float)flcom2).mValue)
                            {
                                compres.mValue = 1;
                            }
                            else
                            {
                                compres.mValue = 0;
                            }
                            this._mThread.currentFrame.OpStack.Push(compres);
                        }
                        result = true;
                        break;
                    case (byte)OpCode.fcmpg:
                        flcom2 = this._mThread.currentFrame.OpStack.Pop();
                        flcom1 = this._mThread.currentFrame.OpStack.Pop();
                        if (flcom1 is Float && flcom2 is Float)
                        {
                            Int compres = new Int();
                            if (((Float)flcom1).mValue < ((Float)flcom2).mValue)
                            {
                                compres.mValue = -1;
                            }
                            else if (((Float)flcom1).mValue > ((Float)flcom2).mValue)
                            {
                                compres.mValue = 1;
                            }
                            else
                            {
                                compres.mValue = 0;
                            }
                            this._mThread.currentFrame.OpStack.Push(compres);
                        }
                        result = true;
                        break;
                    case (byte)OpCode.ifge:
                        short compareoffset2 = (short)((GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC] << 8) + GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC + 1]);
                        BaseType compe1 = this._mThread.currentFrame.OpStack.Pop();
                        if (compe1 is Int)
                        {
                            if (((Int)compe1).mValue >= 0)
                            {
                                this._mThread.PC += -1 + compareoffset2;
                            }
                            else
                            {
                                this._mThread.PC += 2;
                            }
                        }
                        else
                        {
                            this._mThread.PC += 2;
                        }
                        result = true;
                        break;
                    case (byte)OpCode.ifle:
                        short compareoffset1 = (short)((GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC] << 8) + GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC + 1]);
                        BaseType comp1 = this._mThread.currentFrame.OpStack.Pop();
                        if (comp1 is Int)
                        {
                            if (((Int)comp1).mValue <= 0)
                            {
                                this._mThread.PC += -1 + compareoffset1;
                            }
                            else
                            {
                                this._mThread.PC += 2;
                            }
                        }
                        else
                        {
                            this._mThread.PC += 2;
                        }
                        result = true;
                        break;
                    case (byte)OpCode.if_icmpge:
                        short compareoffset = (short)((GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC] << 8) + GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC + 1]);
                        BaseType bc2 = this._mThread.currentFrame.OpStack.Pop();
                        BaseType bc1 = this._mThread.currentFrame.OpStack.Pop();
                        if (bc1 is Int && bc2 is Int)
                        {
                            //Console.WriteLine("comparing " + ((Int)bc1).mValue + " and " + ((Int)bc2).mValue);
                            if (((Int)bc1).mValue >= ((Int)bc2).mValue)
                            {
                                // Console.WriteLine("branch compare true , offset is " +compareoffset);
                                // Console.WriteLine("current PC is " + this._mThread.PC);
                                this._mThread.PC += -1 + compareoffset;
                                //Console.WriteLine("new PC is " + this._mThread.PC);
                            }
                            else
                            {
                                //Console.WriteLine("branch compare false");
                                this._mThread.PC += 2;
                            }
                        }
                        else
                        {
                            this._mThread.PC += 2;
                        }
                        result = true;
                        break;
                    case (byte)OpCode.if_icmple:
                        short compareloffset = (short)((GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC] << 8) + GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC + 1]);
                        BaseType bcl2 = this._mThread.currentFrame.OpStack.Pop();
                        BaseType bcl1 = this._mThread.currentFrame.OpStack.Pop();
                        if (bcl1 is Int && bcl2 is Int)
                        {
                            //Console.WriteLine("comparing " + ((Int)bcl1).mValue + " and " + ((Int)bcl2).mValue);
                            if (((Int)bcl1).mValue <= ((Int)bcl2).mValue)
                            {
                                // Console.WriteLine("branch compare true , offset is " + compareloffset);
                                // Console.WriteLine("current PC is " + this._mThread.PC);
                                this._mThread.PC += -1 + compareloffset;
                                // Console.WriteLine("new PC is " + this._mThread.PC);
                            }
                            else
                            {
                                //Console.WriteLine("branch compare false");
                                this._mThread.PC += 2;
                            }
                        }
                        else
                        {
                            this._mThread.PC += 2;
                        }
                        result = true;
                        break;
                    case (byte)OpCode._goto:
                        short offset = (short)((GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC] << 8) + GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC + 1]);
                        this._mThread.PC += -1 + offset;
                        result = true;
                        break;
                    case (byte)OpCode.getstatic:
                        short fieldrefIndex = (short)((GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC] << 8) + GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC + 1]);
                        if (this._mThread.currentClass._constantsPool[fieldrefIndex - 1] is ClassRecord.PoolFieldRef)
                        {
                            if (((ClassRecord.PoolFieldRef)this._mThread.currentClass._constantsPool[fieldrefIndex - 1]).mClass.Name.Value == this._mThread.currentClass.mClass.Name.Value)
                            {
                                //from this class
                                if (this._mThread.currentClass.StaticFields.ContainsKey(((ClassRecord.PoolFieldRef)this._mThread.currentClass._constantsPool[fieldrefIndex - 1]).mNameType.Name.Value))
                                {
                                    if (this._mThread.currentClass.StaticFields[((ClassRecord.PoolFieldRef)this._mThread.currentClass._constantsPool[fieldrefIndex - 1]).mNameType.Name.Value] is Float)
                                    {
                                        Float retFloat = new Float();
                                        retFloat.mValue = ((Float)this._mThread.currentClass.StaticFields[((ClassRecord.PoolFieldRef)this._mThread.currentClass._constantsPool[fieldrefIndex - 1]).mNameType.Name.Value]).mValue;
                                        this._mThread.currentFrame.OpStack.Push(retFloat);
                                    }
                                    else if (this._mThread.currentClass.StaticFields[((ClassRecord.PoolFieldRef)this._mThread.currentClass._constantsPool[fieldrefIndex - 1]).mNameType.Name.Value] is Int)
                                    {
                                        Int retInt = new Int();
                                        retInt.mValue = ((Int)this._mThread.currentClass.StaticFields[((ClassRecord.PoolFieldRef)this._mThread.currentClass._constantsPool[fieldrefIndex - 1]).mNameType.Name.Value]).mValue;
                                        // Console.WriteLine("getting static field, " + retInt.mValue);
                                        this._mThread.currentFrame.OpStack.Push(retInt);
                                    }
                                }
                            }
                            else
                            {
                                //get from a different class
                            }
                        }
                        this._mThread.PC += 2;
                        result = true;
                        break;
                    case (byte)OpCode.putstatic:
                        fieldrefIndex = (short)((GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC] << 8) + GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC + 1]);
                        BaseType addstatic = this._mThread.currentFrame.OpStack.Pop();
                        if (this._mThread.currentClass._constantsPool[fieldrefIndex - 1] is ClassRecord.PoolFieldRef)
                        {
                            if (((ClassRecord.PoolFieldRef)this._mThread.currentClass._constantsPool[fieldrefIndex - 1]).mClass.Name.Value == this._mThread.currentClass.mClass.Name.Value)
                            {
                                // this class
                                if (this._mThread.currentClass.StaticFields.ContainsKey(((ClassRecord.PoolFieldRef)this._mThread.currentClass._constantsPool[fieldrefIndex - 1]).mNameType.Name.Value))
                                {
                                    if (addstatic is Float)
                                    {
                                        if (this._mThread.currentClass.StaticFields[((ClassRecord.PoolFieldRef)this._mThread.currentClass._constantsPool[fieldrefIndex - 1]).mNameType.Name.Value] is Float)
                                        {
                                            Float newf = new Float();
                                            newf.mValue = ((Float)addstatic).mValue;
                                            this._mThread.currentClass.StaticFields[((ClassRecord.PoolFieldRef)this._mThread.currentClass._constantsPool[fieldrefIndex - 1]).mNameType.Name.Value] = newf;
                                        }
                                    }
                                    else if (addstatic is Int)
                                    {
                                        if (this._mThread.currentClass.StaticFields[((ClassRecord.PoolFieldRef)this._mThread.currentClass._constantsPool[fieldrefIndex - 1]).mNameType.Name.Value] is Int)
                                        {
                                            //Console.WriteLine("setting static field  to " + ((Int)addstatic).mValue);
                                            Int newi = new Int();
                                            newi.mValue = ((Int)addstatic).mValue;
                                            this._mThread.currentClass.StaticFields[((ClassRecord.PoolFieldRef)this._mThread.currentClass._constantsPool[fieldrefIndex - 1]).mNameType.Name.Value] = newi;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // a different class
                            }
                        }
                        this._mThread.PC += 2;
                        result = true;
                        break;

                }

                return result;
            }
        }
    }
}