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
using System.IO;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.ExtensionsScriptModule.JVMEngine.Types;
using OpenSim.Region.ExtensionsScriptModule.JVMEngine.Types.PrimitiveTypes;

namespace OpenSim.Region.ExtensionsScriptModule.JVMEngine.JVM
{
    public class ClassRecord
    {
        private ushort m_majorVersion;
        private ushort m_minorVersion;
        private ushort m_constantPoolCount;
        private ushort m_accessFlags;
        private ushort m_thisClass;
        private ushort m_supperClass;
        private ushort m_interfaceCount;
        private ushort m_fieldCount;
        private ushort m_methodCount;
        //private ushort _attributeCount;
        //private string _name;
        public Dictionary<string, BaseType> StaticFields = new Dictionary<string, BaseType>();
        public PoolClass MClass;

        public List<PoolItem> m_constantsPool = new List<PoolItem>();
        private List<MethodInfo> m_methodsList = new List<MethodInfo>();
        private List<FieldInfo> m_fieldList = new List<FieldInfo>();

        public ClassRecord()
        {

        }

        public ClassInstance CreateNewInstance()
        {
            ClassInstance classInst = new ClassInstance();
            classInst.ClassRec = this;
            //TODO: set fields

            return classInst;
        }

        public void LoadClassFromFile(string fileName)
        {
            Console.WriteLine("loading script " + fileName);
            FileStream fs = File.OpenRead(fileName);
            this.LoadClassFromBytes(ReadFully(fs));
            fs.Close();
        }

        public void LoadClassFromBytes(byte[] data)
        {
            int i = 0;
            i += 4;
            m_minorVersion = (ushort)((data[i++] << 8) + data[i++]);
            m_majorVersion = (ushort)((data[i++] << 8) + data[i++]);
            m_constantPoolCount = (ushort)((data[i++] << 8) + data[i++]);
            Console.WriteLine("there should be " + m_constantPoolCount + " items in the pool");
            for (int count = 0; count < (m_constantPoolCount - 1); count++)
            {
                //read in the constant pool
                byte pooltype = data[i++];
                Console.WriteLine("#" + count + ": new constant type = " + pooltype);
                //Console.WriteLine("start position is: " + i);
                switch (pooltype)
                {
                    case 1:  //Utf8
                        ushort uLength = (ushort)((data[i++] << 8) + data[i++]);

                        // Console.WriteLine("new utf8 type, length is " + uLength);
                        PoolUtf8 utf8 = new PoolUtf8();
                        utf8.readValue(data, ref i, uLength);
                        this.m_constantsPool.Add(utf8);
                        break;
                    case 3: //Int
                        break;
                    case 4: //Float
                        break;
                    case 7: //Class
                        PoolClass pClass = new PoolClass(this);
                        pClass.readValue(data, ref i);
                        this.m_constantsPool.Add(pClass);
                        break;
                    case 9: //FieldRef
                        PoolFieldRef pField = new PoolFieldRef(this);
                        pField.readValue(data, ref i);
                        this.m_constantsPool.Add(pField);
                        break;
                    case 10: //Method
                        PoolMethodRef pMeth = new PoolMethodRef(this);
                        pMeth.readValue(data, ref i);
                        this.m_constantsPool.Add(pMeth);
                        break;
                    case 12:  //NamedType
                        PoolNamedType pNamed = new PoolNamedType(this);
                        pNamed.readValue(data, ref i);
                        this.m_constantsPool.Add(pNamed);
                        break;
                }
            }

            m_accessFlags = (ushort)((data[i++] << 8) + data[i++]);
            m_thisClass = (ushort)((data[i++] << 8) + data[i++]);
            m_supperClass = (ushort)((data[i++] << 8) + data[i++]);

            if (this.m_constantsPool[this.m_thisClass - 1] is PoolClass)
            {
                this.MClass = ((PoolClass)this.m_constantsPool[this.m_thisClass - 1]);
            }

            m_interfaceCount = (ushort)((data[i++] << 8) + data[i++]);
            //should now read in the info for each interface

            m_fieldCount = (ushort)((data[i++] << 8) + data[i++]);
            //should now read in the info for each field
            for (int count = 0; count < m_fieldCount; count++)
            {
                FieldInfo fieldInf = new FieldInfo(this);
                fieldInf.ReadData(data, ref i);
                this.m_fieldList.Add(fieldInf);
            }

            m_methodCount = (ushort)((data[i++] << 8) + data[i++]);
            for (int count = 0; count < m_methodCount; count++)
            {
                MethodInfo methInf = new MethodInfo(this);
                methInf.ReadData(data, ref i);
                this.m_methodsList.Add(methInf);
            }
        }

        public void AddMethodsToMemory(MethodMemory memory)
        {
            for (int count = 0; count < m_methodCount; count++)
            {
                this.m_methodsList[count].AddMethodCode(memory);
            }
        }

        public bool StartMethod(Thread thread, string methodName)
        {
            for (int count = 0; count < m_methodCount; count++)
            {
                if (this.m_constantsPool[this.m_methodsList[count].NameIndex - 1] is PoolUtf8)
                {
                    if (((PoolUtf8)this.m_constantsPool[this.m_methodsList[count].NameIndex - 1]).Value == methodName)
                    {
                        //Console.WriteLine("found method: " + ((PoolUtf8)this._constantsPool[this._methodsList[count].NameIndex - 1]).Value);
                        thread.SetPC(this.m_methodsList[count].CodePointer);
                        return true;
                    }
                }
            }
            return false;
        }

        public void PrintToConsole()
        {
            Console.WriteLine("Class File:");
            Console.WriteLine("Major version: " + m_majorVersion);
            Console.WriteLine("Minor version: " + m_minorVersion);
            Console.WriteLine("Pool size: " + m_constantPoolCount);

            for (int i = 0; i < m_constantsPool.Count; i++)
            {
                this.m_constantsPool[i].Print();
            }

            Console.WriteLine("Access flags: " + m_accessFlags);
            Console.WriteLine("This class: " + m_thisClass);
            Console.WriteLine("Super class: " + m_supperClass);

            for (int count = 0; count < m_fieldCount; count++)
            {
                Console.WriteLine();
                this.m_fieldList[count].Print();
            }

            for (int count = 0; count < m_methodCount; count++)
            {
                Console.WriteLine();
                this.m_methodsList[count].Print();
            }

            Console.WriteLine("class name is " + this.MClass.Name.Value);
        }

        public static byte[] ReadFully(Stream stream)
        {
            byte[] buffer = new byte[1024];
            using (MemoryStream ms = new MemoryStream())
            {
                while (true)
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                        return ms.ToArray();
                    ms.Write(buffer, 0, read);
                }
            }
        }

        #region nested classes
        public class PoolItem
        {
            public virtual void Print()
            {

            }
        }

        public class PoolUtf8 : PoolItem
        {
            public string Value = "";

            public void readValue(byte[] data, ref int pointer, int length)
            {
                for (int i = 0; i < length; i++)
                {
                    int a = (int)data[pointer++];
                    if ((a & 0x80) == 0)
                    {
                        Value = Value + (char)a;
                    }
                    else if ((a & 0x20) == 0)
                    {
                        int b = (int)data[pointer++];
                        Value = Value + (char)(((a & 0x1f) << 6) + (b & 0x3f));
                    }
                    else
                    {
                        int b = (int)data[pointer++];
                        int c = (int)data[pointer++];
                        Value = Value + (char)(((a & 0xf) << 12) + ((b & 0x3f) << 6) + (c & 0x3f));
                    }
                }
            }

            public override void Print()
            {
                Console.WriteLine("Utf8 type: " + Value);
            }
        }

        private class PoolInt : PoolItem
        {

        }

        public class PoolClass : PoolItem
        {
            //public string name = "";
            public ushort namePointer = 0;
            private ClassRecord parent;
            public PoolUtf8 Name;

            public PoolClass(ClassRecord paren)
            {
                parent = paren;
            }

            public void readValue(byte[] data, ref int pointer)
            {
                namePointer = (ushort)((data[pointer++] << 8) + data[pointer++]);
            }

            public override void Print()
            {
                this.Name = ((PoolUtf8)this.parent.m_constantsPool[namePointer - 1]);
                Console.Write("Class type: " + namePointer);
                Console.WriteLine(" // " + ((PoolUtf8)this.parent.m_constantsPool[namePointer - 1]).Value);

            }
        }

        public class PoolFieldRef : PoolItem
        {
            public ushort classPointer = 0;
            public ushort nameTypePointer = 0;
            public PoolNamedType mNameType;
            public PoolClass mClass;
            private ClassRecord parent;

            public PoolFieldRef(ClassRecord paren)
            {
                parent = paren;
            }

            public void readValue(byte[] data, ref int pointer)
            {
                classPointer = (ushort)((data[pointer++] << 8) + data[pointer++]);
                nameTypePointer = (ushort)((data[pointer++] << 8) + data[pointer++]);
            }

            public override void Print()
            {
                this.mNameType = ((PoolNamedType)this.parent.m_constantsPool[nameTypePointer - 1]);
                this.mClass = ((PoolClass)this.parent.m_constantsPool[classPointer - 1]);
                Console.WriteLine("FieldRef type: " + classPointer + " , " + nameTypePointer);
            }
        }

        public class PoolMethodRef : PoolItem
        {
            public ushort classPointer = 0;
            public ushort nameTypePointer = 0;
            public PoolNamedType mNameType;
            public PoolClass mClass;
            private ClassRecord parent;

            public PoolMethodRef(ClassRecord paren)
            {
                parent = paren;
            }

            public void readValue(byte[] data, ref int pointer)
            {
                classPointer = (ushort)((data[pointer++] << 8) + data[pointer++]);
                nameTypePointer = (ushort)((data[pointer++] << 8) + data[pointer++]);
            }

            public override void Print()
            {
                this.mNameType = ((PoolNamedType)this.parent.m_constantsPool[nameTypePointer - 1]);
                this.mClass = ((PoolClass)this.parent.m_constantsPool[classPointer - 1]);
                Console.WriteLine("MethodRef type: " + classPointer + " , " + nameTypePointer);
            }
        }

        public class PoolNamedType : PoolItem
        {
            public ushort namePointer = 0;
            public ushort typePointer = 0;
            private ClassRecord parent;
            public PoolUtf8 Name;
            public PoolUtf8 Type;

            public PoolNamedType(ClassRecord paren)
            {
                parent = paren;
            }

            public void readValue(byte[] data, ref int pointer)
            {
                namePointer = (ushort)((data[pointer++] << 8) + data[pointer++]);
                typePointer = (ushort)((data[pointer++] << 8) + data[pointer++]);
            }

            public override void Print()
            {
                Name = ((PoolUtf8)this.parent.m_constantsPool[namePointer - 1]);
                Type = ((PoolUtf8)this.parent.m_constantsPool[typePointer - 1]);
                Console.Write("Named type: " + namePointer + " , " + typePointer);
                Console.WriteLine(" // " + ((PoolUtf8)this.parent.m_constantsPool[namePointer - 1]).Value);
            }
        }

        //***********************
        public class MethodInfo
        {
            public ushort AccessFlags = 0;
            public ushort NameIndex = 0;
            public string Name = "";
            public ushort DescriptorIndex = 0;
            public ushort AttributeCount = 0;
            public List<MethodAttribute> Attributes = new List<MethodAttribute>();
            private ClassRecord parent;
            public int CodePointer = 0;

            public MethodInfo(ClassRecord paren)
            {
                parent = paren;
            }

            public void AddMethodCode(MethodMemory memory)
            {
                Array.Copy(this.Attributes[0].Code, 0, memory.MethodBuffer, memory.NextMethodPC, this.Attributes[0].Code.Length);
                memory.Methodcount++;
                this.CodePointer = memory.NextMethodPC;
                memory.NextMethodPC += this.Attributes[0].Code.Length;
            }

            public void ReadData(byte[] data, ref int pointer)
            {
                AccessFlags = (ushort)((data[pointer++] << 8) + data[pointer++]);
                NameIndex = (ushort)((data[pointer++] << 8) + data[pointer++]);
                DescriptorIndex = (ushort)((data[pointer++] << 8) + data[pointer++]);
                AttributeCount = (ushort)((data[pointer++] << 8) + data[pointer++]);
                for (int i = 0; i < AttributeCount; i++)
                {
                    MethodAttribute attri = new MethodAttribute(this.parent);
                    attri.ReadData(data, ref pointer);
                    this.Attributes.Add(attri);
                }
            }

            public void Print()
            {
                Console.WriteLine("Method Info Struct: ");
                Console.WriteLine("AccessFlags: " + AccessFlags);
                Console.WriteLine("NameIndex: " + NameIndex + " // " + ((PoolUtf8)this.parent.m_constantsPool[NameIndex - 1]).Value);
                Console.WriteLine("DescriptorIndex: " + DescriptorIndex + " // " + ((PoolUtf8)this.parent.m_constantsPool[DescriptorIndex - 1]).Value);
                Console.WriteLine("Attribute Count:" + AttributeCount);
                for (int i = 0; i < AttributeCount; i++)
                {
                    this.Attributes[i].Print();
                }
            }

            public class MethodAttribute
            {
                public ushort NameIndex = 0;
                public string Name = "";
                public Int32 Length = 0;
                //for now only support code attribute
                public ushort MaxStack = 0;
                public ushort MaxLocals = 0;
                public Int32 CodeLength = 0;
                public byte[] Code;
                public ushort ExceptionTableLength = 0;
                public ushort SubAttributeCount = 0;
                public List<SubAttribute> SubAttributes = new List<SubAttribute>();
                private ClassRecord parent;

                public MethodAttribute(ClassRecord paren)
                {
                    parent = paren;
                }

                public void ReadData(byte[] data, ref int pointer)
                {
                    NameIndex = (ushort)((data[pointer++] << 8) + data[pointer++]);
                    Length = (Int32)((data[pointer++] << 24) + (data[pointer++] << 16) + (data[pointer++] << 8) + data[pointer++]);
                    MaxStack = (ushort)((data[pointer++] << 8) + data[pointer++]);
                    MaxLocals = (ushort)((data[pointer++] << 8) + data[pointer++]);
                    CodeLength = (Int32)((data[pointer++] << 24) + (data[pointer++] << 16) + (data[pointer++] << 8) + data[pointer++]);
                    Code = new byte[CodeLength];
                    for (int i = 0; i < CodeLength; i++)
                    {
                        Code[i] = data[pointer++];
                    }
                    ExceptionTableLength = (ushort)((data[pointer++] << 8) + data[pointer++]);
                    SubAttributeCount = (ushort)((data[pointer++] << 8) + data[pointer++]);
                    for (int i = 0; i < SubAttributeCount; i++)
                    {
                        SubAttribute subAttri = new SubAttribute(this.parent);
                        subAttri.ReadData(data, ref pointer);
                        this.SubAttributes.Add(subAttri);
                    }
                }

                public void Print()
                {
                    Console.WriteLine("Method Attribute: ");
                    Console.WriteLine("Name Index: " + NameIndex + " // " + ((PoolUtf8)this.parent.m_constantsPool[NameIndex - 1]).Value);
                    Console.WriteLine("Length: " + Length);
                    Console.WriteLine("MaxStack: " + MaxStack);
                    Console.WriteLine("MaxLocals: " + MaxLocals);
                    Console.WriteLine("CodeLength: " + CodeLength);
                    for (int i = 0; i < Code.Length; i++)
                    {
                        Console.WriteLine("OpCode #" + i + " is: " + Code[i]);
                    }
                    Console.WriteLine("SubAttributes: " + SubAttributeCount);
                    for (int i = 0; i < SubAttributeCount; i++)
                    {
                        this.SubAttributes[i].Print();
                    }
                }

                public class SubAttribute
                {
                    public ushort NameIndex = 0;
                    public string Name = "";
                    public Int32 Length = 0;
                    public byte[] Data;
                    private ClassRecord parent;

                    public SubAttribute(ClassRecord paren)
                    {
                        parent = paren;
                    }

                    public void ReadData(byte[] data, ref int pointer)
                    {
                        NameIndex = (ushort)((data[pointer++] << 8) + data[pointer++]);
                        Length = (Int32)((data[pointer++] << 24) + (data[pointer++] << 16) + (data[pointer++] << 8) + data[pointer++]);
                        Data = new byte[Length];
                        for (int i = 0; i < Length; i++)
                        {
                            Data[i] = data[pointer++];
                        }
                    }

                    public void Print()
                    {
                        Console.WriteLine("SubAttribute: NameIndex: " + NameIndex + " // " + ((PoolUtf8)this.parent.m_constantsPool[NameIndex - 1]).Value);
                    }

                }
            }

        }
        private class InterfaceInfo
        {
            public void ReadData(byte[] data, ref int i)
            {

            }
        }

        public class FieldInfo
        {
            public ushort AccessFlags = 0;
            public ushort NameIndex = 0;
            public string Name = "";
            public ushort DescriptorIndex = 0;
            public ushort AttributeCount = 0;
            public List<FieldAttribute> Attributes = new List<FieldAttribute>();
            private ClassRecord parent;

            public FieldInfo(ClassRecord paren)
            {
                parent = paren;
            }

            public void ReadData(byte[] data, ref int pointer)
            {
                AccessFlags = (ushort)((data[pointer++] << 8) + data[pointer++]);
                NameIndex = (ushort)((data[pointer++] << 8) + data[pointer++]);
                DescriptorIndex = (ushort)((data[pointer++] << 8) + data[pointer++]);
                AttributeCount = (ushort)((data[pointer++] << 8) + data[pointer++]);
                for (int i = 0; i < AttributeCount; i++)
                {
                    FieldAttribute attri = new FieldAttribute(this.parent);
                    attri.ReadData(data, ref pointer);
                    this.Attributes.Add(attri);
                }
            }

            public void Print()
            {
                Console.WriteLine("Field Info Struct: ");
                Console.WriteLine("AccessFlags: " + AccessFlags);
                Console.WriteLine("NameIndex: " + NameIndex + " // " + ((PoolUtf8)this.parent.m_constantsPool[NameIndex - 1]).Value);
                Console.WriteLine("DescriptorIndex: " + DescriptorIndex + " // " + ((PoolUtf8)this.parent.m_constantsPool[DescriptorIndex - 1]).Value);
                Console.WriteLine("Attribute Count:" + AttributeCount);
                //if static, add to static field list
                // if (this.AccessFlags == 9) //public and static
                if ((this.AccessFlags & 0x08) != 0)
                {
                    switch (((PoolUtf8)this.parent.m_constantsPool[DescriptorIndex - 1]).Value)
                    {
                        case "I":
                            Int newin = new Int();
                            this.parent.StaticFields.Add(((PoolUtf8)this.parent.m_constantsPool[NameIndex - 1]).Value, newin);
                            break;
                        case "F":
                            Float newfl = new Float();
                            this.parent.StaticFields.Add(((PoolUtf8)this.parent.m_constantsPool[NameIndex - 1]).Value, newfl);
                            break;
                    }

                }
                for (int i = 0; i < AttributeCount; i++)
                {
                    this.Attributes[i].Print();
                }
            }

            public class FieldAttribute
            {
                public ushort NameIndex = 0;
                public string Name = "";
                public Int32 Length = 0;
                public byte[] Data;
                private ClassRecord parent;

                public FieldAttribute(ClassRecord paren)
                {
                    parent = paren;
                }

                public void ReadData(byte[] data, ref int pointer)
                {
                    NameIndex = (ushort)((data[pointer++] << 8) + data[pointer++]);
                    Length = (Int32)((data[pointer++] << 24) + (data[pointer++] << 16) + (data[pointer++] << 8) + data[pointer++]);
                    Data = new byte[Length];
                    for (int i = 0; i < Length; i++)
                    {
                        Data[i] = data[pointer++];
                    }
                }

                public void Print()
                {
                    Console.WriteLine("FieldAttribute: NameIndex: " + NameIndex + " // " + ((PoolUtf8)this.parent.m_constantsPool[NameIndex - 1]).Value);
                }
            }
        }

        private class AttributeInfo
        {
            public void ReadData(byte[] data, ref int i)
            {

            }
        }
        #endregion

    }
}