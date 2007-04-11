using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Scripting.EmbeddedJVM.Types;
using OpenSim.Scripting.EmbeddedJVM.Types.PrimitiveTypes;

namespace OpenSim.Scripting.EmbeddedJVM
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
                    case 2:
                        Int m_int= new Int();
                        m_int.mValue = -1;
                        this._mThread.currentFrame.OpStack.Push(m_int);
                        result = true;
                        break;
                    case 3:
                        m_int= new Int();
                        m_int.mValue = 0;
                        this._mThread.currentFrame.OpStack.Push(m_int);
                        result = true;
                        break;
                    case 4:
                        m_int = new Int();
                        m_int.mValue = 1;
                        this._mThread.currentFrame.OpStack.Push(m_int);
                        result = true;
                        break;
                    case 5:
                        m_int = new Int();
                        m_int.mValue = 2;
                        this._mThread.currentFrame.OpStack.Push(m_int);
                        result = true;
                        break;
                    case 6:
                        m_int = new Int();
                        m_int.mValue = 3;
                        this._mThread.currentFrame.OpStack.Push(m_int);
                        break;
                    case 7:
                        m_int = new Int();
                        m_int.mValue = 4;
                        this._mThread.currentFrame.OpStack.Push(m_int);
                        result = true;
                        break;
                    case 8:
                        m_int = new Int();
                        m_int.mValue = 5;
                        this._mThread.currentFrame.OpStack.Push(m_int);
                        result = true;
                        break;
                    case 11:
                        Float m_float = new Float();
                        m_float.mValue = 0.0f;
                        this._mThread.currentFrame.OpStack.Push(m_float);
                        result = true;
                        break;
                    case 12:
                        m_float = new Float();
                        m_float.mValue = 1.0f;
                        this._mThread.currentFrame.OpStack.Push(m_float);
                        result = true;
                        break;
                    case 13:
                        m_float = new Float();
                        m_float.mValue = 2.0f;
                        this._mThread.currentFrame.OpStack.Push(m_float);
                        result = true;
                        break;
                    case 16:
                        int pushvalue = (int)GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC];
                        Int pushInt = new Int();
                        pushInt.mValue = pushvalue;
                        this._mThread.currentFrame.OpStack.Push(pushInt);
                        this._mThread.PC++;
                        result = true;
                        break;
                    case 17:
                        short pushvalue2 = (short)((GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC] << 8) + GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC + 1]);
                        Int pushInt2 = new Int();
                        pushInt2.mValue = pushvalue2;
                        this._mThread.currentFrame.OpStack.Push(pushInt2);
                        this._mThread.PC += 2;
                        result = true;
                        break;
                    case 23:
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
                    case 26:
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
                    case 27:
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
                    case 34:
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
                    case 35:
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
                    case 36:
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
                    case 37:
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
                    case 56:
                        short findex = (short)((GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC] ));
                        BaseType fstor = this._mThread.currentFrame.OpStack.Pop();
                        if (fstor is Float)
                        {
                            this._mThread.currentFrame.LocalVariables[findex] = (Float)fstor;
                        }
                        this._mThread.PC++;
                        result = true;
                        break;
                    case 59:
                        BaseType baset = this._mThread.currentFrame.OpStack.Pop();
                        if (baset is Int)
                        {
                            this._mThread.currentFrame.LocalVariables[0] = (Int)baset;
                        }
                        result = true;
                        break;
                    case 60:
                        baset = this._mThread.currentFrame.OpStack.Pop();
                        if (baset is Int)
                        {
                            this._mThread.currentFrame.LocalVariables[1] = (Int)baset;
                        }
                        result = true;
                        break;
                    case 67:
                        baset = this._mThread.currentFrame.OpStack.Pop();
                        if (baset is Float)
                        {
                            this._mThread.currentFrame.LocalVariables[0] = (Float)baset;
                        }
                        result = true;
                        break;
                    case 68:
                        baset = this._mThread.currentFrame.OpStack.Pop();
                        if (baset is Float)
                        {
                            this._mThread.currentFrame.LocalVariables[1] = (Float)baset;
                        }
                        result = true;
                        break;
                    case 69:
                        baset = this._mThread.currentFrame.OpStack.Pop();
                        if (baset is Float)
                        {
                            this._mThread.currentFrame.LocalVariables[2] = (Float)baset;
                        }
                        result = true;
                        break;
                    case 70:
                        baset = this._mThread.currentFrame.OpStack.Pop();
                        if (baset is Float)
                        {
                            this._mThread.currentFrame.LocalVariables[3] = (Float)baset;
                        }
                        result = true;
                        break;
                    case 87:
                        this._mThread.currentFrame.OpStack.Pop();
                        result = true;
                        break;
                    case 98:
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
                    case 102:
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
                    case 104: //check the order of the two values off the stack is correct
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
                    case 132:
                        if (this._mThread.currentFrame.LocalVariables[GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC]] != null)
                        {
                            if (this._mThread.currentFrame.LocalVariables[GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC]] is Int)
                            {
                                ((Int)this._mThread.currentFrame.LocalVariables[GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC]]).mValue += (sbyte) GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC + 1];
                            }
                        }
                        this._mThread.PC += 2;
                        result = true;
                        break;
                    case 139:
                        BaseType conv1 = this._mThread.currentFrame.OpStack.Pop();
                        if (conv1 is Float)
                        {
                            Int newconv = new Int();
                            newconv.mValue = (int)((Float)conv1).mValue;
                            this._mThread.currentFrame.OpStack.Push(newconv);
                        }
                        result = true;
                        break;
                    case 149:
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
                    case 158:
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
                    case 162:
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
                    case 164:
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
                    case 167:
                        short offset = (short)((GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC] << 8) + GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC+1]);
                        this._mThread.PC += -1 + offset;
                        result = true;
                        break;
                }

                return result;
            }
        }
    }
}
