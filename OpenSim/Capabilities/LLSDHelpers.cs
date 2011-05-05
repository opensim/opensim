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
 *     * Neither the name of the OpenSimulator Project nor the
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
 */

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Xml;

namespace OpenSim.Framework.Capabilities
{
    public class LLSDHelpers
    {
//        private static readonly log4net.ILog m_log
//            = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static string SerialiseLLSDReply(object obj)
        {
            StringWriter sw = new StringWriter();
            XmlTextWriter writer = new XmlTextWriter(sw);
            writer.Formatting = Formatting.None;
            writer.WriteStartElement(String.Empty, "llsd", String.Empty);
            SerializeOSDType(writer, obj);
            writer.WriteEndElement();
            writer.Close();

            //m_log.DebugFormat("[LLSD Helpers]: Generated serialized LLSD reply {0}", sw.ToString());

            return sw.ToString();
        }

        private static void SerializeOSDType(XmlTextWriter writer, object obj)
        {
            Type myType = obj.GetType();
            LLSDType[] llsdattributes = (LLSDType[]) myType.GetCustomAttributes(typeof (LLSDType), false);
            if (llsdattributes.Length > 0)
            {
                switch (llsdattributes[0].ObjectType)
                {
                    case "MAP":
                        writer.WriteStartElement(String.Empty, "map", String.Empty);
                        FieldInfo[] fields = myType.GetFields();
                        for (int i = 0; i < fields.Length; i++)
                        {
                            if (fields[i] != null && fields[i].GetValue(obj) != null)
                            {
                                object fieldValue = fields[i].GetValue(obj);
                                LLSDType[] fieldAttributes =
                                    (LLSDType[]) fieldValue.GetType().GetCustomAttributes(typeof (LLSDType), false);
                                if (fieldAttributes.Length > 0)
                                {
                                    writer.WriteStartElement(String.Empty, "key", String.Empty);
                                    string fieldName = fields[i].Name;
                                    fieldName = fieldName.Replace("___", "-");
                                    writer.WriteString(fieldName);
                                    writer.WriteEndElement();
                                    SerializeOSDType(writer, fieldValue);
                                }
                                else
                                {
                                    writer.WriteStartElement(String.Empty, "key", String.Empty);
                                    string fieldName = fields[i].Name;
                                    fieldName = fieldName.Replace("___", "-");
                                    writer.WriteString(fieldName);
                                    writer.WriteEndElement();
                                    LLSD.LLSDWriteOne(writer, fieldValue);
                                    // OpenMetaverse.StructuredData.LLSDParser.SerializeXmlElement(
                                    //    writer, OpenMetaverse.StructuredData.OSD.FromObject(fieldValue));
                                }
                            }
                            else
                            {
                                // TODO from ADAM: There is a nullref being caused by fields[i] being null
                                // on some computers. Unsure what is causing this, but would appreciate
                                // if sdague could take a look at this.
                            }
                        }
                        writer.WriteEndElement();
                        break;
                    case "ARRAY":
                        // OSDArray arrayObject = obj as OSDArray;
                        // ArrayList a = arrayObject.Array;
                        ArrayList a = (ArrayList) obj.GetType().GetField("Array").GetValue(obj);
                        if (a != null)
                        {
                            writer.WriteStartElement(String.Empty, "array", String.Empty);
                            foreach (object item in a)
                            {
                                SerializeOSDType(writer, item);
                            }
                            writer.WriteEndElement();
                        }
                        break;
                }
            }
            else
            {
                LLSD.LLSDWriteOne(writer, obj);
                //OpenMetaverse.StructuredData.LLSDParser.SerializeXmlElement(
                //    writer, OpenMetaverse.StructuredData.OSD.FromObject(obj));
            }
        }

        public static object DeserialiseOSDMap(Hashtable llsd, object obj)
        {
            Type myType = obj.GetType();
            LLSDType[] llsdattributes = (LLSDType[]) myType.GetCustomAttributes(typeof (LLSDType), false);
            if (llsdattributes.Length > 0)
            {
                switch (llsdattributes[0].ObjectType)
                {
                    case "MAP":
                        IDictionaryEnumerator enumerator = llsd.GetEnumerator();
                        while (enumerator.MoveNext())
                        {
                            string keyName = (string)enumerator.Key;
                            keyName = keyName.Replace("-","_");
                            FieldInfo field = myType.GetField(keyName);
                            if (field != null)
                            {
                                // if (enumerator.Value is OpenMetaverse.StructuredData.OSDMap)
                                if (enumerator.Value is Hashtable)
                                {
                                    object fieldValue = field.GetValue(obj);
                                    DeserialiseOSDMap((Hashtable) enumerator.Value, fieldValue);
                                    //  DeserialiseOSDMap((OpenMetaverse.StructuredData.OSDMap) enumerator.Value, fieldValue);
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
}
