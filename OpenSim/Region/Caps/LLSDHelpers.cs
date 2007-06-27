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
                                if (enumerator.Value is Hashtable)
                                {
                                    object fieldValue = field.GetValue(obj);
                                    DeserialiseLLSDMap((Hashtable) enumerator.Value, fieldValue);
                                }
                                else if (enumerator.Value is ArrayList)
                                {
                                    object fieldValue = field.GetValue(obj);
                                    fieldValue.GetType().GetField("Array").SetValue(fieldValue, enumerator.Value);
                                    //TODO
                                    // the LLSD map/array types in the array need to be deserialised
                                    // but first we need to know the right class to deserialise them into. 
                                }
                                else
                                {
                                    field.SetValue(obj, enumerator.Value);
                                }
                            }
                        }
                        break;
                }
            }
            return obj;
        }
    }

    [LLSDType("MAP")]
    public class LLSDMapLayerResponse
    {
        public LLSDMapRequest AgentData = new LLSDMapRequest();
        public LLSDArray LayerData = new LLSDArray();

        public LLSDMapLayerResponse()
        {

        }
    }

    [LLSDType("MAP")]
    public class LLSDCapsDetails
    {
        public string MapLayer = "";
        public string NewFileAgentInventory = "";
        //public string EventQueueGet = "";

        public LLSDCapsDetails()
        {

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
    public class LLSDUploadReply
    {
        public string new_asset = "";
        public LLUUID new_inventory_item = LLUUID.Zero;
        public string state = "";

        public LLSDUploadReply()
        {

        }
    }

    [LLSDType("MAP")]
    public class LLSDCapEvent
    {
        public int id = 0;
        public LLSDArray events = new LLSDArray();

        public LLSDCapEvent()
        {

        }
    }

    [LLSDType("MAP")]
    public class LLSDEmpty
    {
        public LLSDEmpty()
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
        private string myType;

        public LLSDType(string type)
        {
            myType = type;

        }

        public string ObjectType
        {
            get
            {
                return myType;
            }
        }
    }
}
