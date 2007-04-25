using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Scripting.EmbeddedJVM.Types;
using OpenSim.Scripting.EmbeddedJVM.Types.PrimitiveTypes;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Types;

namespace OpenSim.Scripting.EmbeddedJVM
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
                        short refIndex = (short) ((GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC] << 8) + GlobalMemory.MethodArea.MethodBuffer[this._mThread.PC+1]);
                        //Console.WriteLine("call to method : "+refIndex);
                        if (this._mThread.currentClass._constantsPool[refIndex - 1] is ClassRecord.PoolMethodRef)
                        {
                           // Console.WriteLine("which is " + ((ClassRecord.PoolMethodRef)this._mThread.currentClass._constantsPool[refIndex - 1]).mClass.Name.Value + "." + ((ClassRecord.PoolMethodRef)this._mThread.currentClass._constantsPool[refIndex - 1]).mNameType.Name.Value);
                           // Console.WriteLine("of type " + ((ClassRecord.PoolMethodRef)this._mThread.currentClass._constantsPool[refIndex - 1]).mNameType.Type.Value);
                            string typ = ((ClassRecord.PoolMethodRef)this._mThread.currentClass._constantsPool[refIndex - 1]).mNameType.Type.Value;
                            string typeparam = "";
                            string typereturn = "";
                            int firstbrak = 0;
                            int secondbrak = 0;
                            firstbrak = typ.LastIndexOf('(');
                            secondbrak = typ.LastIndexOf(')');
                            typeparam = typ.Substring(firstbrak + 1, secondbrak - firstbrak - 1);
                            typereturn = typ.Substring(secondbrak + 1, typ.Length - secondbrak - 1);
                            //Console.WriteLine("split is " + typeparam + " which is length " + typeparam.Length + " , " + typereturn);
                            if (((ClassRecord.PoolMethodRef)this._mThread.currentClass._constantsPool[refIndex - 1]).mClass.Name.Value == this._mThread.currentClass.mClass.Name.Value)
                            {
                                //calling a method in this class
                                if (typeparam.Length == 0)
                                {
                                    this._mThread.JumpToStaticVoidMethod(((ClassRecord.PoolMethodRef)this._mThread.currentClass._constantsPool[refIndex - 1]).mNameType.Name.Value, (this._mThread.PC + 2));
                                }
                                else
                                {
                                    this._mThread.JumpToStaticParamMethod(((ClassRecord.PoolMethodRef)this._mThread.currentClass._constantsPool[refIndex - 1]).mNameType.Name.Value, typeparam, (this._mThread.PC + 2));
                                }
                            }
                            else
                            {
                                //calling a method of a different class

                                //for now we will have a built in OpenSimAPI class, but this should be a java class that then calls native methods
                                if (((ClassRecord.PoolMethodRef)this._mThread.currentClass._constantsPool[refIndex - 1]).mClass.Name.Value == "OpenSimAPI")
                                {
                                    switch (((ClassRecord.PoolMethodRef)this._mThread.currentClass._constantsPool[refIndex - 1]).mNameType.Name.Value)
                                    {
                                        case "GetEntityID":
                                            Int entityID = new Int();
                                            entityID.mValue =(int) this._mThread.EntityId;
                                            this._mThread.currentFrame.OpStack.Push(entityID);
                                            this._mThread.PC += 2;
                                            break;
                                        case "GetRandomAvatarID":
                                            entityID = new Int();
                                            entityID.mValue = (int)Thread.OpenSimScriptAPI.GetRandomAvatarID();
                                            this._mThread.currentFrame.OpStack.Push(entityID);
                                            this._mThread.PC += 2;
                                            break;
                                        case "GetEntityPositionX":
                                            BaseType bs1 = this._mThread.currentFrame.OpStack.Pop();
                                            if (bs1 is Int)
                                            {
                                                //Console.WriteLine("get entity pos for " + ((Int)bs1).mValue);
                                                //should get the position of the entity from the IScriptAPI
                                                OSVector3 vec3 = Thread.OpenSimScriptAPI.GetEntityPosition((uint)((Int)bs1).mValue);
                                                Float pos = new Float();
                                                pos.mValue = vec3.X;
                                               // Console.WriteLine("returned x value " + vec3.X.ToString());
                                                this._mThread.currentFrame.OpStack.Push(pos);
                                            }
                                            this._mThread.PC += 2;
                                            break;
                                        case "GetEntityPositionY":
                                            bs1 = this._mThread.currentFrame.OpStack.Pop();
                                            if (bs1 is Int)
                                            {
                                                //should get the position of the entity from the IScriptAPI
                                                OSVector3 vec3 = Thread.OpenSimScriptAPI.GetEntityPosition((uint)((Int)bs1).mValue);
                                                Float pos = new Float();
                                                pos.mValue = vec3.Y;
                                                this._mThread.currentFrame.OpStack.Push(pos);
                                            }
                                            this._mThread.PC += 2;
                                            break;
                                        case "GetEntityPositionZ":
                                            bs1 = this._mThread.currentFrame.OpStack.Pop();
                                            if (bs1 is Int)
                                            {
                                                //should get the position of the entity from the IScriptAPI
                                                OSVector3 vec3 = Thread.OpenSimScriptAPI.GetEntityPosition((uint)((Int)bs1).mValue);
                                                Float pos = new Float();
                                                pos.mValue = vec3.Z;
                                                this._mThread.currentFrame.OpStack.Push(pos);
                                            }
                                            this._mThread.PC += 2;
                                            break;
                                        case "SetEntityPosition":
                                            //pop the three float values and the entity id
                                            BaseType ft3 = this._mThread.currentFrame.OpStack.Pop();
                                            BaseType ft2 = this._mThread.currentFrame.OpStack.Pop();
                                            BaseType ft1 = this._mThread.currentFrame.OpStack.Pop();
                                            BaseType in1 = this._mThread.currentFrame.OpStack.Pop();
                                            if (ft1 is Float && ft2 is Float && ft3 is Float)
                                            {
                                                if(in1 is Int)
                                                {
                                                    //Console.WriteLine("set: " + ((Int)in1).mValue + " , " + ((Float)ft1).mValue + " , " + ((Float)ft2).mValue + " , " + ((Float)ft3).mValue);
                                                    Thread.OpenSimScriptAPI.SetEntityPosition((uint)((Int) in1).mValue, ((Float)ft1).mValue, ((Float)ft2).mValue, ((Float)ft3).mValue);
                                                }
                                            }
                                            this._mThread.PC += 2;
                                            break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            this._mThread.PC += 2;
                        }
                        result = true;
                        break;
                }

                return result;
            }
        }
    }
}
