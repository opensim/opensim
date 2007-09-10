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
using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Types;
using OpenSim.Region.ExtensionsScriptModule.JVMEngine.Types;
using OpenSim.Region.ExtensionsScriptModule.JVMEngine.Types.PrimitiveTypes;

namespace OpenSim.Region.ExtensionsScriptModule.JVMEngine.JVM
{
    partial class Thread
    {
        private partial class Interpreter
        {
            private bool IsMethodOpCode(byte opcode)
            {
                bool result = false;
                switch (opcode)
                {
                    case 184:
                        short refIndex = (short) ((GlobalMemory.MethodArea.MethodBuffer[this.m_thread.PC] << 8) + GlobalMemory.MethodArea.MethodBuffer[this.m_thread.PC+1]);
                        if (this.m_thread.currentClass.m_constantsPool[refIndex - 1] is ClassRecord.PoolMethodRef)
                        {
                            string typ = ((ClassRecord.PoolMethodRef)this.m_thread.currentClass.m_constantsPool[refIndex - 1]).mNameType.Type.Value;
                            string typeparam = "";
                            string typereturn = "";
                            int firstbrak = 0;
                            int secondbrak = 0;
                            firstbrak = typ.LastIndexOf('(');
                            secondbrak = typ.LastIndexOf(')');
                            typeparam = typ.Substring(firstbrak + 1, secondbrak - firstbrak - 1);
                            typereturn = typ.Substring(secondbrak + 1, typ.Length - secondbrak - 1);
                            if (((ClassRecord.PoolMethodRef)this.m_thread.currentClass.m_constantsPool[refIndex - 1]).mClass.Name.Value == this.m_thread.currentClass.MClass.Name.Value)
                            {
                                //calling a method in this class
                                if (typeparam.Length == 0)
                                {
                                    this.m_thread.JumpToStaticVoidMethod(((ClassRecord.PoolMethodRef)this.m_thread.currentClass.m_constantsPool[refIndex - 1]).mNameType.Name.Value, (this.m_thread.PC + 2));
                                }
                                else
                                {
                                    this.m_thread.JumpToStaticParamMethod(((ClassRecord.PoolMethodRef)this.m_thread.currentClass.m_constantsPool[refIndex - 1]).mNameType.Name.Value, typeparam, (this.m_thread.PC + 2));
                                }
                            }
                            else
                            {
                                //calling a method of a different class

                                // OpenSimAPI Class
                                if (((ClassRecord.PoolMethodRef)this.m_thread.currentClass.m_constantsPool[refIndex - 1]).mClass.Name.Value == "OpenSimAPI")
                                {
                                    this.m_thread.scriptInfo.api.CallMethod(((ClassRecord.PoolMethodRef)this.m_thread.currentClass.m_constantsPool[refIndex - 1]).mNameType.Name.Value, null);
                                }
                            }
                        }
                        else
                        {
                            this.m_thread.PC += 2;
                        }
                        result = true;
                        break;
                }

                return result;
            }
        }
    }
}
