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
using System.Collections.Generic;
using OpenMetaverse;
using System.Text;
using System.IO;
using System.Xml;

namespace OpenSim.Framework
{
    public class PhysicsInertiaData
    {
        public float TotalMass; // the total mass of a linkset
        public Vector3 CenterOfMass;  // the center of mass position relative to root part position
        public Vector3 Inertia; //  (Ixx, Iyy, Izz) moment of inertia relative to center of mass and principal axis in local coords
        public Vector4 InertiaRotation; // if principal axis don't match local axis, the principal axis rotation
                                        // or the upper triangle of the inertia tensor 
                                        // Ixy (= Iyx), Ixz (= Izx), Iyz (= Izy))

        public PhysicsInertiaData()
        {
        }

        public PhysicsInertiaData(PhysicsInertiaData source)
        {
           TotalMass = source.TotalMass;
           CenterOfMass = source.CenterOfMass;
           Inertia = source.Inertia;
           InertiaRotation = source.InertiaRotation;
        }

        private XmlTextWriter writer;

        private void XWint(string name, int i)
        {
            writer.WriteElementString(name, i.ToString());
        }

        private void XWfloat(string name, float f)
        {
            writer.WriteElementString(name, f.ToString(Culture.FormatProvider));
        }

        private void XWVector(string name, Vector3 vec)
        {
            writer.WriteStartElement(name);
            writer.WriteElementString("X", vec.X.ToString(Culture.FormatProvider));
            writer.WriteElementString("Y", vec.Y.ToString(Culture.FormatProvider));
            writer.WriteElementString("Z", vec.Z.ToString(Culture.FormatProvider));
            writer.WriteEndElement();
        }

        private void XWVector4(string name, Vector4 quat)
        {
            writer.WriteStartElement(name);
            writer.WriteElementString("X", quat.X.ToString(Culture.FormatProvider));
            writer.WriteElementString("Y", quat.Y.ToString(Culture.FormatProvider));
            writer.WriteElementString("Z", quat.Z.ToString(Culture.FormatProvider));
            writer.WriteElementString("W", quat.W.ToString(Culture.FormatProvider));
            writer.WriteEndElement();
        }

        public void ToXml2(XmlTextWriter twriter)
        {
            writer = twriter;
            writer.WriteStartElement("PhysicsInertia");

            XWfloat("MASS", TotalMass);
            XWVector("CM", CenterOfMass);
            XWVector("INERTIA", Inertia);
            XWVector4("IROT", InertiaRotation);

            writer.WriteEndElement();
            writer = null;
        }

        XmlReader reader;

        private int XRint()
        {
            return reader.ReadElementContentAsInt();
        }

        private float XRfloat()
        {
            return reader.ReadElementContentAsFloat();
        }

        public Vector3 XRvector()
        {
            Vector3 vec;
            reader.ReadStartElement();
            vec.X = reader.ReadElementContentAsFloat();
            vec.Y = reader.ReadElementContentAsFloat();
            vec.Z = reader.ReadElementContentAsFloat();
            reader.ReadEndElement();
            return vec;
        }

        public Vector4 XRVector4()
        {
            Vector4 q;
            reader.ReadStartElement();
            q.X = reader.ReadElementContentAsFloat();
            q.Y = reader.ReadElementContentAsFloat();
            q.Z = reader.ReadElementContentAsFloat();
            q.W = reader.ReadElementContentAsFloat();
            reader.ReadEndElement();
            return q;
        }

        public static bool EReadProcessors(
            Dictionary<string, Action> processors,
            XmlReader xtr)
        {
            bool errors = false;

            string nodeName = string.Empty;
            while (xtr.NodeType != XmlNodeType.EndElement)
            {
                nodeName = xtr.Name;

                Action p = null;
                if (processors.TryGetValue(xtr.Name, out p))
                {
                    try
                    {
                        p();
                    }
                    catch
                    {
                        errors = true;
                        if (xtr.NodeType == XmlNodeType.EndElement)
                            xtr.Read();
                    }
                }
                else
                {
                    xtr.ReadOuterXml(); // ignore
                }
            }

            return errors;
        }

        public string ToXml2()
        {
            using (StringWriter sw = new StringWriter())
            {
                using (XmlTextWriter xwriter = new XmlTextWriter(sw))
                {
                    ToXml2(xwriter);
                }

                return sw.ToString();
            }
        }

        public static PhysicsInertiaData FromXml2(string text)
        {
            if (text == String.Empty)
                return null;

            bool error;
            PhysicsInertiaData v;
            UTF8Encoding enc = new UTF8Encoding();
            using(MemoryStream ms = new MemoryStream(enc.GetBytes(text)))
                using(XmlTextReader xreader = new XmlTextReader(ms))
                {
                    v = new PhysicsInertiaData();
                    v.FromXml2(xreader, out error);
                }

            if (error)
                return null;

            return v;
        }

        public static PhysicsInertiaData FromXml2(XmlReader reader)
        {
            PhysicsInertiaData data = new PhysicsInertiaData();

            bool errors = false;

            data.FromXml2(reader, out errors);
            if (errors)
                return null;

            return data;
        }

        private void FromXml2(XmlReader _reader, out bool errors)
        {
            errors = false;
            reader = _reader;

            Dictionary<string, Action> m_XmlProcessors = new Dictionary<string, Action>();

            m_XmlProcessors.Add("MASS", ProcessXR_Mass);
            m_XmlProcessors.Add("CM", ProcessXR_CM);
            m_XmlProcessors.Add("INERTIA", ProcessXR_Inertia);
            m_XmlProcessors.Add("IROT", ProcessXR_InertiaRotation);

            reader.ReadStartElement("PhysicsInertia", String.Empty);

            errors = EReadProcessors(
                m_XmlProcessors,
                reader);

            reader.ReadEndElement();
            reader = null;
        }

        private void ProcessXR_Mass()
        {
            TotalMass = XRfloat();
        }

        private void ProcessXR_CM()
        {
            CenterOfMass = XRvector();
        }

        private void ProcessXR_Inertia()
        {
            Inertia = XRvector();
        }

        private void ProcessXR_InertiaRotation()
        {
            InertiaRotation = XRVector4();
        }
    }
}
