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
using System.Xml;
using OpenSim.Framework;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api;
using log4net;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

namespace OpenSim.Region.ScriptEngine.Yengine
{
    public partial class XMRInstance
    {
        /********************************************************************************\
         *  The only method of interest to outside this module is GetExecutionState()   *
         *  which captures the current state of the script into an XML document.        *
         *                                                                              *
         *  The rest of this module contains support routines for GetExecutionState().  *
        \********************************************************************************/

        /**
         * @brief Create an XML element that gives the current state of the script.
         *   <ScriptState Engine="YEngine" SourceHash=m_ObjCode.sourceHash Asset=m_Item.AssetID>
         *     <Snapshot>globalsandstackdump</Snapshot>
         *     <Running>m_Running</Running>
         *     <DetectArray ...
         *     <EventQueue ...
         *     <Permissions ...
         *     <Plugins />
         *   </ScriptState>
         * Updates the .state file while we're at it.
         */
        public XmlElement GetExecutionState(XmlDocument doc)
        {
            // When we're detaching an attachment, we need to wait here.

            // Change this to a 5 second timeout. If things do mess up,
            // we don't want to be stuck forever.
            //
            m_DetachReady.WaitOne(5000, false);

            XmlElement scriptStateN = doc.CreateElement("", "ScriptState", "");
            scriptStateN.SetAttribute("Engine", m_Engine.ScriptEngineName);
            scriptStateN.SetAttribute("Asset", m_Item.AssetID.ToString());
            scriptStateN.SetAttribute("SourceHash", m_ObjCode.sourceHash);

            // Make sure we aren't executing part of the script so it stays 
            // stable.  Setting suspendOnCheckRun tells CheckRun() to suspend
            // and return out so RunOne() will release the lock asap.
            suspendOnCheckRunHold = true;
            lock(m_RunLock)
            {
                //m_RunOnePhase = "GetExecutionState enter";
                //CheckRunLockInvariants(true);

                // Get copy of script globals and stack in relocateable form.
                Byte[] snapshotBytes;
                using (MemoryStream snapshotStream = new MemoryStream())
                {
                    MigrateOutEventHandler(snapshotStream);
                    snapshotBytes = snapshotStream.ToArray();
                }

                string snapshotString = Convert.ToBase64String(snapshotBytes);
                XmlElement snapshotN = doc.CreateElement("", "Snapshot", "");
                snapshotN.AppendChild(doc.CreateTextNode(snapshotString));
                scriptStateN.AppendChild(snapshotN);
                //m_RunOnePhase = "GetExecutionState B";

                // "Running" says whether or not we are accepting new events.
                XmlElement runningN = doc.CreateElement("", "Running", "");
                runningN.AppendChild(doc.CreateTextNode(m_Running.ToString()));
                scriptStateN.AppendChild(runningN);
                //m_RunOnePhase = "GetExecutionState C";

                // "DoGblInit" says whether or not default:state_entry() will init global vars.
                XmlElement doGblInitN = doc.CreateElement("", "DoGblInit", "");
                doGblInitN.AppendChild(doc.CreateTextNode(doGblInit.ToString()));
                scriptStateN.AppendChild(doGblInitN);
                //m_RunOnePhase = "GetExecutionState D";

                if(m_XMRLSLApi is not null)
                {
                    double scriptTime = Util.GetTimeStampMS() - m_XMRLSLApi.getLSLTimerMS();
                    XmlElement scriptTimeN = doc.CreateElement("", "scrpTime", "");
                    scriptTimeN.AppendChild(doc.CreateTextNode(scriptTime.ToString()));
                    scriptStateN.AppendChild(scriptTimeN);
                }

                if (m_minEventDelay != 0.0)
                {
                    XmlElement minEventDelayN = doc.CreateElement("", "mEvtDly", "");
                    minEventDelayN.AppendChild(doc.CreateTextNode(m_minEventDelay.ToString()));
                    scriptStateN.AppendChild(minEventDelayN);
                    //m_RunOnePhase = "GetExecutionState D";
                }

                // More misc data.
                XmlNode permissionsN = doc.CreateElement("", "Permissions", "");
                scriptStateN.AppendChild(permissionsN);

                XmlAttribute granterA = doc.CreateAttribute("", "granter", "");
                granterA.Value = m_Item.PermsGranter.ToString();
                permissionsN.Attributes.Append(granterA);

                XmlAttribute maskA = doc.CreateAttribute("", "mask", "");
                maskA.Value = m_Item.PermsMask.ToString();
                permissionsN.Attributes.Append(maskA);
                //m_RunOnePhase = "GetExecutionState E";

                // "DetectParams" are returned by llDetected...() script functions
                // for the currently active event, if any.
                var detectParams = m_DetectParams;
                if (detectParams is not null)
                {
                    XmlElement detParArrayN = doc.CreateElement("", "DetectArray", "");
                    AppendXMLDetectArray(doc, detParArrayN, detectParams);
                    scriptStateN.AppendChild(detParArrayN);
                }
                //m_RunOnePhase = "GetExecutionState F";

                // Save any events we have in the queue.
                // <EventQueue>
                //   <Event Name="...">
                //     <param>...</param> ...
                //     <DetectParams>...</DetectParams> ...
                //   </Event>
                //   ...
                // </EventQueue>
                XmlElement queuedEventsN = doc.CreateElement("", "EventQueue", "");
                lock(m_QueueLock)
                {
                    foreach(EventParams evt in m_EventQueue)
                    {
                        XmlElement singleEventN = doc.CreateElement("", "Event", "");
                        singleEventN.SetAttribute("Name", evt.EventName);
                        AppendXMLObjectArray(doc, singleEventN, evt.Params, "param");
                        AppendXMLDetectArray(doc, singleEventN, evt.DetectParams);
                        queuedEventsN.AppendChild(singleEventN);
                    }
                }
                scriptStateN.AppendChild(queuedEventsN);
                //m_RunOnePhase = "GetExecutionState G";

                // "Plugins" indicate enabled timers and listens, etc.
                Object[] pluginData = AsyncCommandManager.GetSerializationData(m_Engine, m_ItemID);

                XmlNode plugins = doc.CreateElement("", "Plugins", "");
                AppendXMLObjectArray(doc, plugins, pluginData, "plugin");
                scriptStateN.AppendChild(plugins);
                //m_RunOnePhase = "GetExecutionState H";

                if(m_localsHeapUsed > 0)
                {
                    XmlElement lheap = doc.CreateElement("", "LHeapUse", "");
                    lheap.AppendChild(doc.CreateTextNode(m_localsHeapUsed.ToString()));
                    scriptStateN.AppendChild(lheap);
                }

                /*
                if(m_StackLeft < m_StackSize)
                {
                    XmlElement stk = doc.CreateElement("", "stkLft", "");
                    stk.AppendChild(doc.CreateTextNode(m_StackLeft.ToString()));
                    scriptStateN.AppendChild(stk);
                }
                */

                // Let script run again.
                suspendOnCheckRunHold = false;

                //m_RunOnePhase = "GetExecutionState leave";
            }

            // scriptStateN represents the contents of the .state file so
            // write the .state file while we are here.
            using(FileStream fs = File.Create(m_StateFileName))
            {
                using(StreamWriter sw = new StreamWriter(fs))
                    sw.Write(scriptStateN.OuterXml);
            }

            return scriptStateN;
        }

        /**
         * @brief Write script state to output stream.
         * Input:
         *  stream = stream to write event handler state information to
         */
        private void MigrateOutEventHandler(Stream stream)
        {
            // Write script state out, frames and all, to the stream.
            // Does not change script state.
            stream.WriteByte(migrationVersion);
            stream.WriteByte((byte)16);
            MigrateOut(new BinaryWriter(stream));
        }

        /**
         * @brief Convert an DetectParams[] to corresponding XML.
         *        DetectParams[] holds the values retrievable by llDetected...() for
         *        a given event.
         */
        private static void AppendXMLDetectArray(XmlDocument doc, XmlElement parent, DetectParams[] detect)
        {
            try
            {
                foreach(DetectParams d in detect)
                {
                    XmlElement detectParamsN = GetXMLDetect(doc, d);
                    parent.AppendChild(detectParamsN);
                }
            }
            catch { }
        }

        private static XmlElement GetXMLDetect(XmlDocument doc, DetectParams d)
        {
            XmlElement detectParamsN = doc.CreateElement("", "DetectParams", "");

            XmlAttribute d_key = doc.CreateAttribute("", "key", "");
            d_key.Value = d.Key.ToString();
            detectParamsN.Attributes.Append(d_key);

            XmlAttribute pos = doc.CreateAttribute("", "pos", "");
            pos.Value = d.OffsetPos.ToString();
            detectParamsN.Attributes.Append(pos);

            XmlAttribute d_linkNum = doc.CreateAttribute("", "linkNum", "");
            d_linkNum.Value = d.LinkNum.ToString();
            detectParamsN.Attributes.Append(d_linkNum);

            XmlAttribute d_group = doc.CreateAttribute("", "group", "");
            d_group.Value = d.Group.ToString();
            detectParamsN.Attributes.Append(d_group);

            XmlAttribute d_name = doc.CreateAttribute("", "name", "");
            d_name.Value = d.Name.ToString();
            detectParamsN.Attributes.Append(d_name);

            XmlAttribute d_owner = doc.CreateAttribute("", "owner", "");
            d_owner.Value = d.Owner.ToString();
            detectParamsN.Attributes.Append(d_owner);

            XmlAttribute d_position = doc.CreateAttribute("", "position", "");
            d_position.Value = d.Position.ToString();
            detectParamsN.Attributes.Append(d_position);

            XmlAttribute d_rotation = doc.CreateAttribute("", "rotation", "");
            d_rotation.Value = d.Rotation.ToString();
            detectParamsN.Attributes.Append(d_rotation);

            XmlAttribute d_type = doc.CreateAttribute("", "type", "");
            d_type.Value = d.Type.ToString();
            detectParamsN.Attributes.Append(d_type);

            XmlAttribute d_velocity = doc.CreateAttribute("", "velocity", "");
            d_velocity.Value = d.Velocity.ToString();
            detectParamsN.Attributes.Append(d_velocity);

            return detectParamsN;
        }

        /**
         * @brief Append elements of an array of objects to an XML parent.
         * @param doc = document the parent is part of
         * @param parent = parent to append the items to
         * @param array = array of objects
         * @param tag = <tag ..>...</tag> for each element
         */
        private static void AppendXMLObjectArray(XmlDocument doc, XmlNode parent, object[] array, string tag)
        {
            foreach(object o in array)
            {
                XmlElement element = GetXMLObject(doc, o, tag);
                parent.AppendChild(element);
            }
        }

        /**
         * @brief Get and XML representation of an object.
         * @param doc = document the tag will be put in
         * @param o = object to be represented
         * @param tag = <tag ...>...</tag>
         */
        private static XmlElement GetXMLObject(XmlDocument doc, object o, string tag)
        {
            XmlAttribute typ = doc.CreateAttribute("", "type", "");
            XmlElement n = doc.CreateElement("", tag, "");

            if(o is LSL_List)
            {
                typ.Value = "list";
                n.Attributes.Append(typ);
                AppendXMLObjectArray(doc, n, ((LSL_List)o).Data, "item");
            }
            else
            {
                typ.Value = o.GetType().ToString();
                n.Attributes.Append(typ);
                n.AppendChild(doc.CreateTextNode(o.ToString()));
            }
            return n;
        }
    }
}
