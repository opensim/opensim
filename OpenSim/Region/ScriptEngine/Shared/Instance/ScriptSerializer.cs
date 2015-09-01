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
using System.IO;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Security.Policy;
using System.Reflection;
using System.Globalization;
using System.Xml;
using OpenMetaverse;
using log4net;
using Nini.Config;
using Amib.Threading;
using OpenSim.Framework;
using OpenSim.Region.CoreModules;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Region.ScriptEngine.Shared.CodeTools;
using OpenSim.Region.ScriptEngine.Interfaces;

namespace OpenSim.Region.ScriptEngine.Shared.Instance
{
    public class ScriptSerializer
    {
        public static string Serialize(ScriptInstance instance)
        {
            bool running = instance.Running;

            XmlDocument xmldoc = new XmlDocument();

            XmlNode xmlnode = xmldoc.CreateNode(XmlNodeType.XmlDeclaration,
                                                "", "");
            xmldoc.AppendChild(xmlnode);

            XmlElement rootElement = xmldoc.CreateElement("", "ScriptState",
                                                          "");
            xmldoc.AppendChild(rootElement);

            XmlElement state = xmldoc.CreateElement("", "State", "");
            state.AppendChild(xmldoc.CreateTextNode(instance.State));

            rootElement.AppendChild(state);

            XmlElement run = xmldoc.CreateElement("", "Running", "");
            run.AppendChild(xmldoc.CreateTextNode(
                    running.ToString()));

            rootElement.AppendChild(run);

            Dictionary<string, Object> vars = instance.GetVars();

            XmlElement variables = xmldoc.CreateElement("", "Variables", "");

            foreach (KeyValuePair<string, Object> var in vars)
                WriteTypedValue(xmldoc, variables, "Variable", var.Key,
                                var.Value);

            rootElement.AppendChild(variables);

            XmlElement queue = xmldoc.CreateElement("", "Queue", "");

            int count = instance.EventQueue.Count;

            while (count > 0)
            {
                EventParams ep = (EventParams)instance.EventQueue.Dequeue();
                instance.EventQueue.Enqueue(ep);
                count--;

                XmlElement item = xmldoc.CreateElement("", "Item", "");
                XmlAttribute itemEvent = xmldoc.CreateAttribute("", "event",
                                                                "");
                itemEvent.Value = ep.EventName;
                item.Attributes.Append(itemEvent);

                XmlElement parms = xmldoc.CreateElement("", "Params", "");

                foreach (Object o in ep.Params)
                    WriteTypedValue(xmldoc, parms, "Param", String.Empty, o);

                item.AppendChild(parms);

                XmlElement detect = xmldoc.CreateElement("", "Detected", "");

                foreach (DetectParams det in ep.DetectParams)
                {
                    XmlElement objectElem = xmldoc.CreateElement("", "Object",
                                                                 "");
                    XmlAttribute pos = xmldoc.CreateAttribute("", "pos", "");
                    pos.Value = det.OffsetPos.ToString();
                    objectElem.Attributes.Append(pos);

                    XmlAttribute d_linkNum = xmldoc.CreateAttribute("",
                            "linkNum", "");
                    d_linkNum.Value = det.LinkNum.ToString();
                    objectElem.Attributes.Append(d_linkNum);

                    XmlAttribute d_group = xmldoc.CreateAttribute("",
                            "group", "");
                    d_group.Value = det.Group.ToString();
                    objectElem.Attributes.Append(d_group);

                    XmlAttribute d_name = xmldoc.CreateAttribute("",
                            "name", "");
                    d_name.Value = det.Name.ToString();
                    objectElem.Attributes.Append(d_name);

                    XmlAttribute d_owner = xmldoc.CreateAttribute("",
                            "owner", "");
                    d_owner.Value = det.Owner.ToString();
                    objectElem.Attributes.Append(d_owner);

                    XmlAttribute d_position = xmldoc.CreateAttribute("",
                            "position", "");
                    d_position.Value = det.Position.ToString();
                    objectElem.Attributes.Append(d_position);

                    XmlAttribute d_rotation = xmldoc.CreateAttribute("",
                            "rotation", "");
                    d_rotation.Value = det.Rotation.ToString();
                    objectElem.Attributes.Append(d_rotation);

                    XmlAttribute d_type = xmldoc.CreateAttribute("",
                            "type", "");
                    d_type.Value = det.Type.ToString();
                    objectElem.Attributes.Append(d_type);

                    XmlAttribute d_velocity = xmldoc.CreateAttribute("",
                            "velocity", "");
                    d_velocity.Value = det.Velocity.ToString();
                    objectElem.Attributes.Append(d_velocity);

                    objectElem.AppendChild(
                        xmldoc.CreateTextNode(det.Key.ToString()));

                    detect.AppendChild(objectElem);
                }

                item.AppendChild(detect);

                queue.AppendChild(item);
            }

            rootElement.AppendChild(queue);

            XmlNode plugins = xmldoc.CreateElement("", "Plugins", "");
            DumpList(xmldoc, plugins,
                     new LSL_Types.list(instance.PluginData));

            rootElement.AppendChild(plugins);

            if (instance.ScriptTask != null)
            {
                if (instance.ScriptTask.PermsMask != 0 && instance.ScriptTask.PermsGranter != UUID.Zero)
                {
                    XmlNode permissions = xmldoc.CreateElement("", "Permissions", "");
                    XmlAttribute granter = xmldoc.CreateAttribute("", "granter", "");
                    granter.Value = instance.ScriptTask.PermsGranter.ToString();
                    permissions.Attributes.Append(granter);
                    XmlAttribute mask = xmldoc.CreateAttribute("", "mask", "");
                    mask.Value = instance.ScriptTask.PermsMask.ToString();
                    permissions.Attributes.Append(mask);
                    rootElement.AppendChild(permissions);
                }
            }

            if (instance.MinEventDelay > 0.0)
            {
                XmlElement eventDelay = xmldoc.CreateElement("", "MinEventDelay", "");
                eventDelay.AppendChild(xmldoc.CreateTextNode(instance.MinEventDelay.ToString()));
                rootElement.AppendChild(eventDelay);
            }

            return xmldoc.InnerXml;
        }

        public static void Deserialize(string xml, ScriptInstance instance)
        {
            XmlDocument doc = new XmlDocument();

            Dictionary<string, object> vars = instance.GetVars();

            instance.PluginData = new Object[0];

            doc.LoadXml(xml);

            XmlNodeList rootL = doc.GetElementsByTagName("ScriptState");
            if (rootL.Count != 1)
            {
                return;
            }
            XmlNode rootNode = rootL[0];

            if (rootNode != null)
            {
                object varValue;
                XmlNodeList partL = rootNode.ChildNodes;

                foreach (XmlNode part in partL)
                {
                    switch (part.Name)
                    {
                    case "State":
                        instance.State=part.InnerText;
                        break;
                    case "Running":
                        instance.Running=bool.Parse(part.InnerText);
                        break;
                    case "Variables":
                        XmlNodeList varL = part.ChildNodes;
                        foreach (XmlNode var in varL)
                        {
                            string varName;
                            varValue=ReadTypedValue(var, out varName);

                            if (vars.ContainsKey(varName))
                                vars[varName] = varValue;
                        }
                        instance.SetVars(vars);
                        break;
                    case "Queue":
                        XmlNodeList itemL = part.ChildNodes;
                        foreach (XmlNode item in itemL)
                        {
                            List<Object> parms = new List<Object>();
                            List<DetectParams> detected =
                                    new List<DetectParams>();

                            string eventName =
                                    item.Attributes.GetNamedItem("event").Value;
                            XmlNodeList eventL = item.ChildNodes;
                            foreach (XmlNode evt in eventL)
                            {
                                switch (evt.Name)
                                {
                                case "Params":
                                    XmlNodeList prms = evt.ChildNodes;
                                    foreach (XmlNode pm in prms)
                                        parms.Add(ReadTypedValue(pm));

                                    break;
                                case "Detected":
                                    XmlNodeList detL = evt.ChildNodes;
                                    foreach (XmlNode det in detL)
                                    {
                                        string vect =
                                                det.Attributes.GetNamedItem(
                                                "pos").Value;
                                        LSL_Types.Vector3 v =
                                                new LSL_Types.Vector3(vect);

                                        int d_linkNum=0;
                                        UUID d_group = UUID.Zero;
                                        string d_name = String.Empty;
                                        UUID d_owner = UUID.Zero;
                                        LSL_Types.Vector3 d_position =
                                            new LSL_Types.Vector3();
                                        LSL_Types.Quaternion d_rotation =
                                            new LSL_Types.Quaternion();
                                        int d_type = 0;
                                        LSL_Types.Vector3 d_velocity =
                                            new LSL_Types.Vector3();

                                        try
                                        {
                                            string tmp;

                                            tmp = det.Attributes.GetNamedItem(
                                                    "linkNum").Value;
                                            int.TryParse(tmp, out d_linkNum);

                                            tmp = det.Attributes.GetNamedItem(
                                                    "group").Value;
                                            UUID.TryParse(tmp, out d_group);

                                            d_name = det.Attributes.GetNamedItem(
                                                    "name").Value;

                                            tmp = det.Attributes.GetNamedItem(
                                                    "owner").Value;
                                            UUID.TryParse(tmp, out d_owner);

                                            tmp = det.Attributes.GetNamedItem(
                                                    "position").Value;
                                            d_position =
                                                new LSL_Types.Vector3(tmp);

                                            tmp = det.Attributes.GetNamedItem(
                                                    "rotation").Value;
                                            d_rotation =
                                                new LSL_Types.Quaternion(tmp);

                                            tmp = det.Attributes.GetNamedItem(
                                                    "type").Value;
                                            int.TryParse(tmp, out d_type);

                                            tmp = det.Attributes.GetNamedItem(
                                                    "velocity").Value;
                                            d_velocity =
                                                new LSL_Types.Vector3(tmp);

                                        }
                                        catch (Exception) // Old version XML
                                        {
                                        }

                                        UUID uuid = new UUID();
                                        UUID.TryParse(det.InnerText,
                                                out uuid);

                                        DetectParams d = new DetectParams();
                                        d.Key = uuid;
                                        d.OffsetPos = v;
                                        d.LinkNum = d_linkNum;
                                        d.Group = d_group;
                                        d.Name = d_name;
                                        d.Owner = d_owner;
                                        d.Position = d_position;
                                        d.Rotation = d_rotation;
                                        d.Type = d_type;
                                        d.Velocity = d_velocity;

                                        detected.Add(d);
                                    }
                                    break;
                                }
                            }
                            EventParams ep = new EventParams(
                                    eventName, parms.ToArray(),
                                    detected.ToArray());
                            instance.EventQueue.Enqueue(ep);
                        }
                        break;
                    case "Plugins":
                        instance.PluginData = ReadList(part).Data;
                        break;
                    case "Permissions":
                        string tmpPerm;
                        int mask = 0;
                        tmpPerm = part.Attributes.GetNamedItem("mask").Value;
                        if (tmpPerm != null)
                        {
                            int.TryParse(tmpPerm, out mask);
                            if (mask != 0)
                            {
                                tmpPerm = part.Attributes.GetNamedItem("granter").Value;
                                if (tmpPerm != null)
                                {
                                    UUID granter = new UUID();
                                    UUID.TryParse(tmpPerm, out granter);
                                    if (granter != UUID.Zero)
                                    {
                                        instance.ScriptTask.PermsMask = mask;
                                        instance.ScriptTask.PermsGranter = granter;
                                    }
                                }
                            }
                        }
                        break;
                    case "MinEventDelay":
                        double minEventDelay = 0.0;
                        double.TryParse(part.InnerText, NumberStyles.Float, Culture.NumberFormatInfo, out minEventDelay);
                        instance.MinEventDelay = minEventDelay;
                        break;
                }
              }
            }
        }

        private static void DumpList(XmlDocument doc, XmlNode parent,
                LSL_Types.list l)
        {
            foreach (Object o in l.Data)
                WriteTypedValue(doc, parent, "ListItem", "", o);
        }

        private static LSL_Types.list ReadList(XmlNode parent)
        {
            List<Object> olist = new List<Object>();

            XmlNodeList itemL = parent.ChildNodes;
            foreach (XmlNode item in itemL)
                olist.Add(ReadTypedValue(item));

            return new LSL_Types.list(olist.ToArray());
        }

        private static void WriteTypedValue(XmlDocument doc, XmlNode parent,
                string tag, string name, object value)
        {
            Type t=value.GetType();
            XmlAttribute typ = doc.CreateAttribute("", "type", "");
            XmlNode n = doc.CreateElement("", tag, "");

            if (value is LSL_Types.list)
            {
                typ.Value = "list";
                n.Attributes.Append(typ);

                DumpList(doc, n, (LSL_Types.list) value);

                if (name != String.Empty)
                {
                    XmlAttribute nam = doc.CreateAttribute("", "name", "");
                    nam.Value = name;
                    n.Attributes.Append(nam);
                }

                parent.AppendChild(n);
                return;
            }

            n.AppendChild(doc.CreateTextNode(value.ToString()));

            typ.Value = t.ToString();
            n.Attributes.Append(typ);
            if (name != String.Empty)
            {
                XmlAttribute nam = doc.CreateAttribute("", "name", "");
                nam.Value = name;
                n.Attributes.Append(nam);
            }

            parent.AppendChild(n);
        }

        private static object ReadTypedValue(XmlNode tag, out string name)
        {
            name = tag.Attributes.GetNamedItem("name").Value;

            return ReadTypedValue(tag);
        }

        private static object ReadTypedValue(XmlNode tag)
        {
            Object varValue;
            string assembly;

            string itemType = tag.Attributes.GetNamedItem("type").Value;

            if (itemType == "list")
                return ReadList(tag);

            if (itemType == "OpenMetaverse.UUID")
            {
                UUID val = new UUID();
                UUID.TryParse(tag.InnerText, out val);

                return val;
            }

            Type itemT = Type.GetType(itemType);
            if (itemT == null)
            {
                Object[] args =
                    new Object[] { tag.InnerText };

                assembly = itemType+", OpenSim.Region.ScriptEngine.Shared";
                itemT = Type.GetType(assembly);
                if (itemT == null)
                    return null;

                varValue = Activator.CreateInstance(itemT, args);

                if (varValue == null)
                    return null;
            }
            else
            {
                varValue = Convert.ChangeType(tag.InnerText, itemT);
            }
            return varValue;
        }
    }
}
