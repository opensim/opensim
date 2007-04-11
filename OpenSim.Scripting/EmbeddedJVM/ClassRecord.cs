using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using OpenSim.Scripting.EmbeddedJVM.Types;

namespace OpenSim.Scripting.EmbeddedJVM
{
    public class ClassRecord
    {
        private ushort _majorVersion;
        private ushort _minorVersion;
        private ushort _constantPoolCount;
        private ushort _accessFlags;
        private ushort _thisClass;
        private ushort _supperClass;
        private ushort _interfaceCount;
        private ushort _fieldCount;
        private ushort _methodCount;
        //private ushort _attributeCount;
        //private string _name;
        public Dictionary<string, BaseType> StaticFields = new Dictionary<string, BaseType>();
        public PoolClass mClass;

        public List<PoolItem> _constantsPool = new List<PoolItem>();
        private List<MethodInfo> _methodsList = new List<MethodInfo>();
        private List<FieldInfo> _fieldList = new List<FieldInfo>();

        public ClassRecord()
        {

        }

        public ClassInstance CreateNewInstance()
        {
            return new ClassInstance();
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
            _minorVersion = (ushort)((data[i++] << 8) + data[i++]  );
            _majorVersion = (ushort)((data[i++] << 8) + data[i++]  );
            _constantPoolCount = (ushort)((data[i++] << 8) + data[i++]  );
           // Console.WriteLine("there should be " + _constantPoolCount + " items in the pool");
            for (int count = 0; count < _constantPoolCount -1 ; count++)
            {
                //read in the constant pool
                byte pooltype = data[i++];
                //Console.WriteLine("#" +count +": new constant type = " +pooltype);
                //Console.WriteLine("start position is: " + i);
                switch (pooltype)
                {
                    case 1:  //Utf8
                        ushort uLength = (ushort)((data[i++] << 8) + data[i++]  );

                       // Console.WriteLine("new utf8 type, length is " + uLength);
                        PoolUtf8 utf8 = new PoolUtf8();
                        utf8.readValue(data, ref i, uLength);
                        this._constantsPool.Add(utf8);
                        break;
                    case 3: //Int
                        break;
                    case 7: //Class
                        PoolClass pClass = new PoolClass(this);
                        pClass.readValue(data, ref i);
                        this._constantsPool.Add(pClass);
                        break;
                    case 10: //Method
                        PoolMethodRef pMeth = new PoolMethodRef(this);
                        pMeth.readValue(data, ref i);
                        this._constantsPool.Add(pMeth);
                        break;
                    case 12:  //NamedType
                        PoolNamedType pNamed = new PoolNamedType(this);
                        pNamed.readValue(data, ref i);
                        this._constantsPool.Add(pNamed);
                        break;
                }
            }

            _accessFlags = (ushort)((data[i++] << 8) + data[i++]  );
            _thisClass = (ushort)((data[i++] << 8) + data[i++]  );
            _supperClass = (ushort)((data[i++] << 8) + data[i++]  );

            if (this._constantsPool[this._thisClass - 1] is PoolClass)
            {
                this.mClass = ((PoolClass)this._constantsPool[this._thisClass - 1]);
            }

            _interfaceCount = (ushort)((data[i++] << 8) + data[i++]);
            //should now read in the info for each interface
            _fieldCount = (ushort)((data[i++] << 8) + data[i++]);
            //should now read in the info for each field
            _methodCount = (ushort)((data[i++] << 8) + data[i++]);
            for (int count = 0; count < _methodCount; count++)
            {
                MethodInfo methInf = new MethodInfo(this);
                methInf.ReadData(data, ref i);
                this._methodsList.Add(methInf);
            }
        }

        public void AddMethodsToMemory(MethodMemory memory)
        {
            for (int count = 0; count < _methodCount; count++)
            {
                this._methodsList[count].AddMethodCode(memory);
            }
        }

        public bool StartMethod(Thread thread, string methodName)
        {
            for (int count = 0; count < _methodCount; count++)
            {
                if (this._constantsPool[this._methodsList[count].NameIndex-1] is PoolUtf8)
                {
                    if (((PoolUtf8)this._constantsPool[this._methodsList[count].NameIndex-1]).Value == methodName)
                    {
                        //Console.WriteLine("found method: " + ((PoolUtf8)this._constantsPool[this._methodsList[count].NameIndex - 1]).Value);
                        thread.SetPC(this._methodsList[count].CodePointer);
                        return true;
                    }
                }
            }
            return false;
        }

        public void PrintToConsole()
        {
            Console.WriteLine("Class File:");
           Console.WriteLine("Major version: " + _majorVersion);
           Console.WriteLine("Minor version: " + _minorVersion);
            Console.WriteLine("Pool size: " + _constantPoolCount);

            for (int i = 0; i < _constantsPool.Count; i++)
            {
                this._constantsPool[i].Print();
            }

           Console.WriteLine("Access flags: " + _accessFlags);
           Console.WriteLine("This class: " + _thisClass );
           Console.WriteLine("Super class: " + _supperClass);

            for (int count = 0; count < _methodCount; count++)
            {
                Console.WriteLine();
                this._methodsList[count].Print();
            }

           Console.WriteLine("class name is " + this.mClass.Name.Value);
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

            public void readValue(byte[] data,ref int pointer , int length)
            {
                for (int i = 0; i < length; i++)
                {
                    int a =(int) data[pointer++];
                    if ((a & 0x80) == 0)
                    {
                        Value = Value + (char)a;
                    }
                    else if ((a & 0x20) == 0)
                    {
                        int b = (int) data[pointer++];
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
                namePointer = (ushort)((data[pointer++] << 8) + data[pointer++] );
            }

            public override void Print()
            {
                this.Name = ((PoolUtf8)this.parent._constantsPool[namePointer - 1]);
                Console.Write("Class type: " + namePointer);
                Console.WriteLine(" // " + ((PoolUtf8)this.parent._constantsPool[namePointer - 1]).Value);
                
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
                this.mNameType = ((PoolNamedType)this.parent._constantsPool[nameTypePointer - 1]);
                this.mClass = ((PoolClass)this.parent._constantsPool[classPointer - 1]);
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
                namePointer = (ushort)((data[pointer++] << 8) + data[pointer++] );
                typePointer = (ushort)((data[pointer++] << 8) + data[pointer++] );
            }

            public override void Print()
            {
                Name = ((PoolUtf8)this.parent._constantsPool[namePointer-1]);
                Type = ((PoolUtf8)this.parent._constantsPool[typePointer-1]);
                Console.Write("Named type: " + namePointer + " , " + typePointer );
                Console.WriteLine(" // "+ ((PoolUtf8)this.parent._constantsPool[namePointer-1]).Value);
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
                for(int i =0; i< AttributeCount; i++)
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
              Console.WriteLine("NameIndex: " + NameIndex +" // "+ ((PoolUtf8)this.parent._constantsPool[NameIndex-1]).Value);
              Console.WriteLine("DescriptorIndex: " + DescriptorIndex + " // "+ ((PoolUtf8)this.parent._constantsPool[DescriptorIndex-1]).Value);
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
                 Console.WriteLine("Name Index: " + NameIndex + " // "+ ((PoolUtf8)this.parent._constantsPool[NameIndex-1]).Value);
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
                        Console.WriteLine("SubAttribute: NameIndex: " + NameIndex + " // " + ((PoolUtf8)this.parent._constantsPool[NameIndex - 1]).Value);
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
        private class FieldInfo
        {
            public void ReadData(byte[] data, ref int i)
            {

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
