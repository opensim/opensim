using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;
using libsecondlife;

namespace OpenSim.Framework
{
    public class LLSDHelpers
    {
        public static string SerialiseLLSDReply(object obj)
        {
            StringWriter sw = new StringWriter();
            XmlTextWriter writer = new XmlTextWriter(sw);
            writer.Formatting = Formatting.None;
            writer.WriteStartElement(String.Empty, "llsd", String.Empty);
            LLSDHelpers.SerializeLLSDType(writer, obj);
            writer.WriteEndElement();
            writer.Close();
            return sw.ToString();
        }

        public static void SerializeLLSDType(XmlTextWriter writer, object obj)
        {
            Type myType = obj.GetType();
            LLSDType[] llsdattributes = (LLSDType[])myType.GetCustomAttributes(typeof(LLSDType), false);
            if (llsdattributes.Length > 0)
            {
                switch (llsdattributes[0].ObjectType)
                {
                    case "MAP":
                        writer.WriteStartElement(String.Empty, "map", String.Empty);
                        System.Reflection.FieldInfo[] fields = myType.GetFields();
                        for (int i = 0; i < fields.Length; i++)
                        {
                            object fieldValue = fields[i].GetValue(obj);
                            LLSDType[] fieldAttributes = (LLSDType[])fieldValue.GetType().GetCustomAttributes(typeof(LLSDType), false);
                            if (fieldAttributes.Length > 0)
                            {
                                writer.WriteStartElement(String.Empty, "key", String.Empty);
                                writer.WriteString(fields[i].Name);
                                writer.WriteEndElement();
                                SerializeLLSDType(writer, fieldValue);
                            }
                            else
                            {
                                //Console.WriteLine("LLSD field name" + fields[i].Name + " , " + fields[i].GetValue(obj).GetType());
                                writer.WriteStartElement(String.Empty, "key", String.Empty);
                                writer.WriteString(fields[i].Name);
                                writer.WriteEndElement();
                                LLSD.LLSDWriteOne(writer, fieldValue);
                            }
                        }
                        writer.WriteEndElement();
                        break;
                    case "ARRAY":
                        // LLSDArray arrayObject = obj as LLSDArray;
                        // ArrayList a = arrayObject.Array;
                        ArrayList a = (ArrayList)obj.GetType().GetField("Array").GetValue(obj);
                        writer.WriteStartElement(String.Empty, "array", String.Empty);
                        foreach (object item in a)
                        {
                            SerializeLLSDType(writer, item);
                        }
                        writer.WriteEndElement();
                        break;
                }
            }
            else
            {
                LLSD.LLSDWriteOne(writer, obj);
            }
        }

        public static object DeserialiseLLSDMap(Hashtable llsd, object obj)
        {
            Type myType = obj.GetType();
            LLSDType[] llsdattributes = (LLSDType[])myType.GetCustomAttributes(typeof(LLSDType), false);
            if (llsdattributes.Length > 0)
            {
                switch (llsdattributes[0].ObjectType)
                {
                    case "MAP":
                        IDictionaryEnumerator enumerator = llsd.GetEnumerator();
                        while (enumerator.MoveNext())
                        {
                            System.Reflection.FieldInfo field = myType.GetField((string)enumerator.Key);
                            if (field != null)
                            {
                                field.SetValue(obj, enumerator.Value);
                            }
                        }
                        break;
                }
            }
            return obj;
        }
    }

    [LLSDType("MAP")]
    public class LLSDMapLayer
    {
        public int Left = 0;
        public int Right = 0;
        public int Top = 0;
        public int Bottom = 0;
        public LLUUID ImageID = LLUUID.Zero;

        public LLSDArray TestArray = new LLSDArray();
        public LLSDMapLayer()
        {

        }
    }

    [LLSDType("ARRAY")]
    public class LLSDArray
    {
        public ArrayList Array = new ArrayList();

        public LLSDArray()
        {

        }
    }

    [LLSDType("MAP")]
    public class LLSDMapRequest
    {
        public int Flags = 0;

        public LLSDMapRequest()
        {

        }
    }

    [LLSDType("MAP")]
    public class LLSDTest
    {
        public int Test1 = 20;
        public int Test2 = 10;

        public LLSDTest()
        {

        }
    }


    [AttributeUsage(AttributeTargets.Class)]
    public class LLSDType : Attribute
    {
        private string myHandler;


        public LLSDType(string type)
        {
            myHandler = type;

        }

        public string ObjectType
        {
            get
            {
                return myHandler;
            }
        }
    }
}
