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
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;
using System.Xml;
using OpenSim.Framework.Serialization;
using OpenSim.Framework.Serialization.External;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Region.Framework.Scenes.Serialization;

namespace OpenSim.Region.Framework.Scenes
{
    public class SOPVehicle
    {
        public VehicleData vd;

        public Vehicle Type
        {
            get { return vd.m_type; }
        }

        public SOPVehicle()
        {
            vd = new VehicleData();
            ProcessTypeChange(Vehicle.TYPE_NONE); // is needed?
        }

        public void ProcessFloatVehicleParam(Vehicle pParam, float pValue)
        {
            float len;
            float timestep = 0.01f;
            switch (pParam)
            {
                case Vehicle.ANGULAR_DEFLECTION_EFFICIENCY:
                    if (pValue < 0f) pValue = 0f;
                    if (pValue > 1f) pValue = 1f;
                    vd.m_angularDeflectionEfficiency = pValue;
                    break;
                case Vehicle.ANGULAR_DEFLECTION_TIMESCALE:
                    if (pValue < timestep) pValue = timestep;
                    vd.m_angularDeflectionTimescale = pValue;
                    break;
                case Vehicle.ANGULAR_MOTOR_DECAY_TIMESCALE:
                    if (pValue < timestep) pValue = timestep;
                    else if (pValue > 120) pValue = 120;
                    vd.m_angularMotorDecayTimescale = pValue;
                    break;
                case Vehicle.ANGULAR_MOTOR_TIMESCALE:
                    if (pValue < timestep) pValue = timestep;
                    vd.m_angularMotorTimescale = pValue;
                    break;
                case Vehicle.BANKING_EFFICIENCY:
                    if (pValue < -1f) pValue = -1f;
                    if (pValue > 1f) pValue = 1f;
                    vd.m_bankingEfficiency = pValue;
                    break;
                case Vehicle.BANKING_MIX:
                    if (pValue < 0f) pValue = 0f;
                    if (pValue > 1f) pValue = 1f;
                    vd.m_bankingMix = pValue;
                    break;
                case Vehicle.BANKING_TIMESCALE:
                    if (pValue < timestep) pValue = timestep;
                    vd.m_bankingTimescale = pValue;
                    break;
                case Vehicle.BUOYANCY:
                    if (pValue < -1f) pValue = -1f;
                    if (pValue > 1f) pValue = 1f;
                    vd.m_VehicleBuoyancy = pValue;
                    break;
                case Vehicle.HOVER_EFFICIENCY:
                    if (pValue < 0f) pValue = 0f;
                    if (pValue > 1f) pValue = 1f;
                    vd.m_VhoverEfficiency = pValue;
                    break;
                case Vehicle.HOVER_HEIGHT:
                    vd.m_VhoverHeight = pValue;
                    break;
                case Vehicle.HOVER_TIMESCALE:
                    if (pValue < timestep) pValue = timestep;
                    vd.m_VhoverTimescale = pValue;
                    break;
                case Vehicle.LINEAR_DEFLECTION_EFFICIENCY:
                    if (pValue < 0f) pValue = 0f;
                    if (pValue > 1f) pValue = 1f;
                    vd.m_linearDeflectionEfficiency = pValue;
                    break;
                case Vehicle.LINEAR_DEFLECTION_TIMESCALE:
                    if (pValue < timestep) pValue = timestep;
                    vd.m_linearDeflectionTimescale = pValue;
                    break;
                case Vehicle.LINEAR_MOTOR_DECAY_TIMESCALE:
                    if (pValue < timestep) pValue = timestep;
                    else if (pValue > 120) pValue = 120;
                    vd.m_linearMotorDecayTimescale = pValue;
                    break;
                case Vehicle.LINEAR_MOTOR_TIMESCALE:
                    if (pValue < timestep) pValue = timestep;
                    vd.m_linearMotorTimescale = pValue;
                    break;
                case Vehicle.VERTICAL_ATTRACTION_EFFICIENCY:
                    if (pValue < 0f) pValue = 0f;
                    if (pValue > 1f) pValue = 1f;
                    vd.m_verticalAttractionEfficiency = pValue;
                    break;
                case Vehicle.VERTICAL_ATTRACTION_TIMESCALE:
                    if (pValue < timestep) pValue = timestep;
                    vd.m_verticalAttractionTimescale = pValue;
                    break;

                // These are vector properties but the engine lets you use a single float value to
                // set all of the components to the same value
                case Vehicle.ANGULAR_FRICTION_TIMESCALE:
                    if (pValue < timestep) pValue = timestep;
                    vd.m_angularFrictionTimescale = new Vector3(pValue, pValue, pValue);
                    break;
                case Vehicle.ANGULAR_MOTOR_DIRECTION:
                    vd.m_angularMotorDirection = new Vector3(pValue, pValue, pValue);
                    len = vd.m_angularMotorDirection.Length();
                    if (len > 12.566f)
                        vd.m_angularMotorDirection *= (12.566f / len);
                    break;
                case Vehicle.LINEAR_FRICTION_TIMESCALE:
                    if (pValue < timestep) pValue = timestep;
                    vd.m_linearFrictionTimescale = new Vector3(pValue, pValue, pValue);
                    break;
                case Vehicle.LINEAR_MOTOR_DIRECTION:
                    vd.m_linearMotorDirection = new Vector3(pValue, pValue, pValue);
                    len = vd.m_linearMotorDirection.Length();
                    if (len > 30.0f)
                        vd.m_linearMotorDirection *= (30.0f / len);
                    break;
                case Vehicle.LINEAR_MOTOR_OFFSET:
                    vd.m_linearMotorOffset = new Vector3(pValue, pValue, pValue);
                    len = vd.m_linearMotorOffset.Length();
                    if (len > 100.0f)
                        vd.m_linearMotorOffset *= (100.0f / len);
                    break;
            }
        }//end ProcessFloatVehicleParam

        public void ProcessVectorVehicleParam(Vehicle pParam, Vector3 pValue)
        {
            float len;
            float timestep = 0.01f;
            switch (pParam)
            {
                case Vehicle.ANGULAR_FRICTION_TIMESCALE:
                    if (pValue.X < timestep) pValue.X = timestep;
                    if (pValue.Y < timestep) pValue.Y = timestep;
                    if (pValue.Z < timestep) pValue.Z = timestep;

                    vd.m_angularFrictionTimescale = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
                case Vehicle.ANGULAR_MOTOR_DIRECTION:
                    vd.m_angularMotorDirection = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    // Limit requested angular speed to 2 rps= 4 pi rads/sec
                    len = vd.m_angularMotorDirection.Length();
                    if (len > 12.566f)
                        vd.m_angularMotorDirection *= (12.566f / len);
                    break;
                case Vehicle.LINEAR_FRICTION_TIMESCALE:
                    if (pValue.X < timestep) pValue.X = timestep;
                    if (pValue.Y < timestep) pValue.Y = timestep;
                    if (pValue.Z < timestep) pValue.Z = timestep;
                    vd.m_linearFrictionTimescale = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
                case Vehicle.LINEAR_MOTOR_DIRECTION:
                    vd.m_linearMotorDirection = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    len = vd.m_linearMotorDirection.Length();
                    if (len > 30.0f)
                        vd.m_linearMotorDirection *= (30.0f / len);
                    break;
                case Vehicle.LINEAR_MOTOR_OFFSET:
                    vd.m_linearMotorOffset = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    len = vd.m_linearMotorOffset.Length();
                    if (len > 100.0f)
                        vd.m_linearMotorOffset *= (100.0f / len);
                    break;
            }
        }//end ProcessVectorVehicleParam

        public void ProcessRotationVehicleParam(Vehicle pParam, Quaternion pValue)
        {
            switch (pParam)
            {
                case Vehicle.REFERENCE_FRAME:
                    vd.m_referenceFrame = Quaternion.Inverse(pValue);
                    break;
            }
        }//end ProcessRotationVehicleParam

        public void ProcessVehicleFlags(int pParam, bool remove)
        {
            if (remove)
            {
                vd.m_flags &= ~((VehicleFlag)pParam);
            }
            else
            {
                vd.m_flags |= (VehicleFlag)pParam;
            }
        }//end ProcessVehicleFlags

        public void ProcessTypeChange(Vehicle pType)
        {
            vd.m_linearMotorDirection = Vector3.Zero;
            vd.m_angularMotorDirection = Vector3.Zero;
            vd.m_linearMotorOffset = Vector3.Zero;
            vd.m_referenceFrame = Quaternion.Identity;

            // Set Defaults For Type
            vd.m_type = pType;
            switch (pType)
            {
                case Vehicle.TYPE_NONE:
                    vd.m_linearFrictionTimescale = new Vector3(1000, 1000, 1000);
                    vd.m_angularFrictionTimescale = new Vector3(1000, 1000, 1000);
                    vd.m_linearMotorTimescale = 1000;
                    vd.m_linearMotorDecayTimescale = 120;
                    vd.m_angularMotorTimescale = 1000;
                    vd.m_angularMotorDecayTimescale = 1000;
                    vd.m_VhoverHeight = 0;
                    vd.m_VhoverEfficiency = 1;
                    vd.m_VhoverTimescale = 1000;
                    vd.m_VehicleBuoyancy = 0;
                    vd.m_linearDeflectionEfficiency = 0;
                    vd.m_linearDeflectionTimescale = 1000;
                    vd.m_angularDeflectionEfficiency = 0;
                    vd.m_angularDeflectionTimescale = 1000;
                    vd.m_bankingEfficiency = 0;
                    vd.m_bankingMix = 1;
                    vd.m_bankingTimescale = 1000;
                    vd.m_verticalAttractionEfficiency = 0;
                    vd.m_verticalAttractionTimescale = 1000;

                    vd.m_flags = (VehicleFlag)0;
                    break;

                case Vehicle.TYPE_SLED:
                    vd.m_linearFrictionTimescale = new Vector3(30, 1, 1000);
                    vd.m_angularFrictionTimescale = new Vector3(1000, 1000, 1000);
                    vd.m_linearMotorTimescale = 1000;
                    vd.m_linearMotorDecayTimescale = 120;
                    vd.m_angularMotorTimescale = 1000;
                    vd.m_angularMotorDecayTimescale = 120;
                    vd.m_VhoverHeight = 0;
                    vd.m_VhoverEfficiency = 1;
                    vd.m_VhoverTimescale = 10;
                    vd.m_VehicleBuoyancy = 0;
                    vd.m_linearDeflectionEfficiency = 1;
                    vd.m_linearDeflectionTimescale = 1;
                    vd.m_angularDeflectionEfficiency = 0;
                    vd.m_angularDeflectionTimescale = 1000;
                    vd.m_bankingEfficiency = 0;
                    vd.m_bankingMix = 1;
                    vd.m_bankingTimescale = 10;
                    vd.m_flags &=
                         ~(VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY |
                           VehicleFlag.HOVER_GLOBAL_HEIGHT | VehicleFlag.HOVER_UP_ONLY);
                    vd.m_flags |= (VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.LIMIT_ROLL_ONLY | VehicleFlag.LIMIT_MOTOR_UP);
                    break;
                case Vehicle.TYPE_CAR:
                    vd.m_linearFrictionTimescale = new Vector3(100, 2, 1000);
                    vd.m_angularFrictionTimescale = new Vector3(1000, 1000, 1000);
                    vd.m_linearMotorTimescale = 1;
                    vd.m_linearMotorDecayTimescale = 60;
                    vd.m_angularMotorTimescale = 1;
                    vd.m_angularMotorDecayTimescale = 0.8f;
                    vd.m_VhoverHeight = 0;
                    vd.m_VhoverEfficiency = 0;
                    vd.m_VhoverTimescale = 1000;
                    vd.m_VehicleBuoyancy = 0;
                    vd.m_linearDeflectionEfficiency = 1;
                    vd.m_linearDeflectionTimescale = 2;
                    vd.m_angularDeflectionEfficiency = 0;
                    vd.m_angularDeflectionTimescale = 10;
                    vd.m_verticalAttractionEfficiency = 1f;
                    vd.m_verticalAttractionTimescale = 10f;
                    vd.m_bankingEfficiency = -0.2f;
                    vd.m_bankingMix = 1;
                    vd.m_bankingTimescale = 1;
                    vd.m_flags &= ~(VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY | VehicleFlag.HOVER_GLOBAL_HEIGHT);
                    vd.m_flags |= (VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.LIMIT_ROLL_ONLY |
                                VehicleFlag.LIMIT_MOTOR_UP | VehicleFlag.HOVER_UP_ONLY);
                    break;
                case Vehicle.TYPE_BOAT:
                    vd.m_linearFrictionTimescale = new Vector3(10, 3, 2);
                    vd.m_angularFrictionTimescale = new Vector3(10, 10, 10);
                    vd.m_linearMotorTimescale = 5;
                    vd.m_linearMotorDecayTimescale = 60;
                    vd.m_angularMotorTimescale = 4;
                    vd.m_angularMotorDecayTimescale = 4;
                    vd.m_VhoverHeight = 0;
                    vd.m_VhoverEfficiency = 0.5f;
                    vd.m_VhoverTimescale = 2;
                    vd.m_VehicleBuoyancy = 1;
                    vd.m_linearDeflectionEfficiency = 0.5f;
                    vd.m_linearDeflectionTimescale = 3;
                    vd.m_angularDeflectionEfficiency = 0.5f;
                    vd.m_angularDeflectionTimescale = 5;
                    vd.m_verticalAttractionEfficiency = 0.5f;
                    vd.m_verticalAttractionTimescale = 5f;
                    vd.m_bankingEfficiency = -0.3f;
                    vd.m_bankingMix = 0.8f;
                    vd.m_bankingTimescale = 1;
                    vd.m_flags &= ~(VehicleFlag.HOVER_TERRAIN_ONLY |
                            VehicleFlag.HOVER_GLOBAL_HEIGHT |
                            VehicleFlag.HOVER_UP_ONLY |
                            VehicleFlag.LIMIT_ROLL_ONLY);
                    vd.m_flags |= (VehicleFlag.NO_DEFLECTION_UP |
                                VehicleFlag.LIMIT_MOTOR_UP |
                                VehicleFlag.HOVER_WATER_ONLY);
                    break;
                case Vehicle.TYPE_AIRPLANE:
                    vd.m_linearFrictionTimescale = new Vector3(200, 10, 5);
                    vd.m_angularFrictionTimescale = new Vector3(20, 20, 20);
                    vd.m_linearMotorTimescale = 2;
                    vd.m_linearMotorDecayTimescale = 60;
                    vd.m_angularMotorTimescale = 4;
                    vd.m_angularMotorDecayTimescale = 8;
                    vd.m_VhoverHeight = 0;
                    vd.m_VhoverEfficiency = 0.5f;
                    vd.m_VhoverTimescale = 1000;
                    vd.m_VehicleBuoyancy = 0;
                    vd.m_linearDeflectionEfficiency = 0.5f;
                    vd.m_linearDeflectionTimescale = 0.5f;
                    vd.m_angularDeflectionEfficiency = 1;
                    vd.m_angularDeflectionTimescale = 2;
                    vd.m_verticalAttractionEfficiency = 0.9f;
                    vd.m_verticalAttractionTimescale = 2f;
                    vd.m_bankingEfficiency = 1;
                    vd.m_bankingMix = 0.7f;
                    vd.m_bankingTimescale = 2;
                    vd.m_flags &= ~(VehicleFlag.HOVER_WATER_ONLY |
                        VehicleFlag.HOVER_TERRAIN_ONLY |
                        VehicleFlag.HOVER_GLOBAL_HEIGHT |
                        VehicleFlag.HOVER_UP_ONLY |
                        VehicleFlag.NO_DEFLECTION_UP |
                        VehicleFlag.LIMIT_MOTOR_UP);
                    vd.m_flags |= (VehicleFlag.LIMIT_ROLL_ONLY);
                    break;
                case Vehicle.TYPE_BALLOON:
                    vd.m_linearFrictionTimescale = new Vector3(5, 5, 5);
                    vd.m_angularFrictionTimescale = new Vector3(10, 10, 10);
                    vd.m_linearMotorTimescale = 5;
                    vd.m_linearMotorDecayTimescale = 60;
                    vd.m_angularMotorTimescale = 6;
                    vd.m_angularMotorDecayTimescale = 10;
                    vd.m_VhoverHeight = 5;
                    vd.m_VhoverEfficiency = 0.8f;
                    vd.m_VhoverTimescale = 10;
                    vd.m_VehicleBuoyancy = 1;
                    vd.m_linearDeflectionEfficiency = 0;
                    vd.m_linearDeflectionTimescale = 5;
                    vd.m_angularDeflectionEfficiency = 0;
                    vd.m_angularDeflectionTimescale = 5;
                    vd.m_verticalAttractionEfficiency = 0f;
                    vd.m_verticalAttractionTimescale = 1000f;
                    vd.m_bankingEfficiency = 0;
                    vd.m_bankingMix = 0.7f;
                    vd.m_bankingTimescale = 5;
                    vd.m_flags &= ~(VehicleFlag.HOVER_WATER_ONLY |
                        VehicleFlag.HOVER_TERRAIN_ONLY |
                        VehicleFlag.HOVER_UP_ONLY |
                        VehicleFlag.NO_DEFLECTION_UP |
                        VehicleFlag.LIMIT_MOTOR_UP);
                    vd.m_flags |= (VehicleFlag.LIMIT_ROLL_ONLY |
                        VehicleFlag.HOVER_GLOBAL_HEIGHT);
                    break;
            }
        }
        public void SetVehicle(PhysicsActor ph)
        {
            if (ph == null)
                return;
            ph.SetVehicle(vd);
        }

        private XmlTextWriter writer;

        private void XWint(string name, int i)
        {
            writer.WriteElementString(name, i.ToString());
        }

        private void XWfloat(string name, float f)
        {
            writer.WriteElementString(name, f.ToString(Utils.EnUsCulture));
        }

        private void XWVector(string name, Vector3 vec)
        {
            writer.WriteStartElement(name);
            writer.WriteElementString("X", vec.X.ToString(Utils.EnUsCulture));
            writer.WriteElementString("Y", vec.Y.ToString(Utils.EnUsCulture));
            writer.WriteElementString("Z", vec.Z.ToString(Utils.EnUsCulture));
            writer.WriteEndElement();
        }

        private void XWQuat(string name, Quaternion quat)
        {
            writer.WriteStartElement(name);
            writer.WriteElementString("X", quat.X.ToString(Utils.EnUsCulture));
            writer.WriteElementString("Y", quat.Y.ToString(Utils.EnUsCulture));
            writer.WriteElementString("Z", quat.Z.ToString(Utils.EnUsCulture));
            writer.WriteElementString("W", quat.W.ToString(Utils.EnUsCulture));
            writer.WriteEndElement();
        }

        public void ToXml2(XmlTextWriter twriter)
        {
            writer = twriter;
            writer.WriteStartElement("Vehicle");

            XWint("TYPE", (int)vd.m_type);
            XWint("FLAGS", (int)vd.m_flags);

            // Linear properties
            XWVector("LMDIR", vd.m_linearMotorDirection);
            XWVector("LMFTIME", vd.m_linearFrictionTimescale);
            XWfloat("LMDTIME", vd.m_linearMotorDecayTimescale);
            XWfloat("LMTIME", vd.m_linearMotorTimescale);
            XWVector("LMOFF", vd.m_linearMotorOffset);

            //Angular properties
            XWVector("AMDIR", vd.m_angularMotorDirection);
            XWfloat("AMTIME", vd.m_angularMotorTimescale);
            XWfloat("AMDTIME", vd.m_angularMotorDecayTimescale);
            XWVector("AMFTIME", vd.m_angularFrictionTimescale);

            //Deflection properties
            XWfloat("ADEFF", vd.m_angularDeflectionEfficiency);
            XWfloat("ADTIME", vd.m_angularDeflectionTimescale);
            XWfloat("LDEFF", vd.m_linearDeflectionEfficiency);
            XWfloat("LDTIME", vd.m_linearDeflectionTimescale);

            //Banking properties
            XWfloat("BEFF", vd.m_bankingEfficiency);
            XWfloat("BMIX", vd.m_bankingMix);
            XWfloat("BTIME", vd.m_bankingTimescale);

            //Hover and Buoyancy properties
            XWfloat("HHEI", vd.m_VhoverHeight);
            XWfloat("HEFF", vd.m_VhoverEfficiency);
            XWfloat("HTIME", vd.m_VhoverTimescale);
            XWfloat("VBUO", vd.m_VehicleBuoyancy);

            //Attractor properties
            XWfloat("VAEFF", vd.m_verticalAttractionEfficiency);
            XWfloat("VATIME", vd.m_verticalAttractionTimescale);

            XWQuat("REF_FRAME", vd.m_referenceFrame);

            writer.WriteEndElement();
            writer = null;
        }

        private int XRint(XmlTextReader reader, string name)
        {
            return reader.ReadElementContentAsInt(name, String.Empty);
        }

        private float XRfloat(XmlTextReader reader, string name)
        {
            return reader.ReadElementContentAsFloat(name, String.Empty);
        }

        public Vector3 XRvector(XmlTextReader reader, string name)
        {
            Vector3 vec;
            reader.ReadStartElement(name);
            vec.X = reader.ReadElementContentAsFloat(reader.Name, String.Empty); // X or x
            vec.Y = reader.ReadElementContentAsFloat(reader.Name, String.Empty); // Y or y
            vec.Z = reader.ReadElementContentAsFloat(reader.Name, String.Empty); // Z or z
            reader.ReadEndElement();
            return vec;
        }

        public Quaternion XRquat(XmlTextReader reader, string name)
        {
            Quaternion q;
            reader.ReadStartElement(name);
            q.X = reader.ReadElementContentAsFloat(reader.Name, String.Empty);
            q.Y = reader.ReadElementContentAsFloat(reader.Name, String.Empty);
            q.Z = reader.ReadElementContentAsFloat(reader.Name, String.Empty);
            q.W = reader.ReadElementContentAsFloat(reader.Name, String.Empty);
            reader.ReadEndElement();
            return q;
        }

        public void FromXml2(XmlTextReader reader, out bool errors)
        {
            errors = false;

            Dictionary<string, Action<VehicleData, XmlTextReader>> m_VehicleXmlProcessors
            = new Dictionary<string, Action<VehicleData, XmlTextReader>>();

            m_VehicleXmlProcessors.Add("TYPE", ProcessXR_type);
            m_VehicleXmlProcessors.Add("FLAGS", ProcessXR_flags);

            // Linear properties
            m_VehicleXmlProcessors.Add("LMDIR", ProcessXR_linearMotorDirection);
            m_VehicleXmlProcessors.Add("LMFTIME", ProcessXR_linearFrictionTimescale);
            m_VehicleXmlProcessors.Add("LMDTIME", ProcessXR_linearMotorDecayTimescale);
            m_VehicleXmlProcessors.Add("LMTIME", ProcessXR_linearMotorTimescale);
            m_VehicleXmlProcessors.Add("LMOFF", ProcessXR_linearMotorOffset);

            //Angular properties
            m_VehicleXmlProcessors.Add("AMDIR", ProcessXR_angularMotorDirection);
            m_VehicleXmlProcessors.Add("AMTIME", ProcessXR_angularMotorTimescale);
            m_VehicleXmlProcessors.Add("AMDTIME", ProcessXR_angularMotorDecayTimescale);
            m_VehicleXmlProcessors.Add("AMFTIME", ProcessXR_angularFrictionTimescale);

            //Deflection properties
            m_VehicleXmlProcessors.Add("ADEFF", ProcessXR_angularDeflectionEfficiency);
            m_VehicleXmlProcessors.Add("ADTIME", ProcessXR_angularDeflectionTimescale);
            m_VehicleXmlProcessors.Add("LDEFF", ProcessXR_linearDeflectionEfficiency);
            m_VehicleXmlProcessors.Add("LDTIME", ProcessXR_linearDeflectionTimescale);

            //Banking properties
            m_VehicleXmlProcessors.Add("BEFF", ProcessXR_bankingEfficiency);
            m_VehicleXmlProcessors.Add("BMIX", ProcessXR_bankingMix);
            m_VehicleXmlProcessors.Add("BTIME", ProcessXR_bankingTimescale);

            //Hover and Buoyancy properties
            m_VehicleXmlProcessors.Add("HHEI", ProcessXR_VhoverHeight);
            m_VehicleXmlProcessors.Add("HEFF", ProcessXR_VhoverEfficiency);
            m_VehicleXmlProcessors.Add("HTIME", ProcessXR_VhoverTimescale);

            m_VehicleXmlProcessors.Add("VBUO", ProcessXR_VehicleBuoyancy);

            //Attractor properties
            m_VehicleXmlProcessors.Add("VAEFF", ProcessXR_verticalAttractionEfficiency);
            m_VehicleXmlProcessors.Add("VATIME", ProcessXR_verticalAttractionTimescale);

            m_VehicleXmlProcessors.Add("REF_FRAME", ProcessXR_referenceFrame);

            vd = new VehicleData();

            reader.ReadStartElement("Vehicle", String.Empty);

            errors = ExternalRepresentationUtils.ExecuteReadProcessors(
                vd,
                m_VehicleXmlProcessors,
                reader,
                (o, nodeName, e)
                    =>
                {
                }
            );

            reader.ReadEndElement();
        }

        private void ProcessXR_type(VehicleData obj, XmlTextReader reader)
        {
            vd.m_type = (Vehicle)XRint(reader, "TYPE");
        }
        private void ProcessXR_flags(VehicleData obj, XmlTextReader reader)
        {
            vd.m_flags = (VehicleFlag)XRint(reader, "FLAGS");
        }
        // Linear properties
        private void ProcessXR_linearMotorDirection(VehicleData obj, XmlTextReader reader)
        {
            vd.m_linearMotorDirection = XRvector(reader, "LMDIR");
        }

        private void ProcessXR_linearFrictionTimescale(VehicleData obj, XmlTextReader reader)
        {
            vd.m_linearFrictionTimescale = XRvector(reader, "LMFTIME");
        }

        private void ProcessXR_linearMotorDecayTimescale(VehicleData obj, XmlTextReader reader)
        {
            vd.m_linearMotorDecayTimescale = XRfloat(reader, "LMDTIME");
        }
        private void ProcessXR_linearMotorTimescale(VehicleData obj, XmlTextReader reader)
        {
            vd.m_linearMotorTimescale = XRfloat(reader, "LMTIME");
        }
        private void ProcessXR_linearMotorOffset(VehicleData obj, XmlTextReader reader)
        {
            vd.m_linearMotorOffset = XRvector(reader, "LMOFF");
        }


        //Angular properties
        private void ProcessXR_angularMotorDirection(VehicleData obj, XmlTextReader reader)
        {
            vd.m_angularMotorDirection = XRvector(reader, "AMDIR");
        }
        private void ProcessXR_angularMotorTimescale(VehicleData obj, XmlTextReader reader)
        {
            vd.m_angularMotorTimescale = XRfloat(reader, "AMTIME");
        }
        private void ProcessXR_angularMotorDecayTimescale(VehicleData obj, XmlTextReader reader)
        {
            vd.m_angularMotorDecayTimescale = XRfloat(reader, "AMDTIME");
        }
        private void ProcessXR_angularFrictionTimescale(VehicleData obj, XmlTextReader reader)
        {
            vd.m_angularFrictionTimescale = XRvector(reader, "AMFTIME");
        }

        //Deflection properties
        private void ProcessXR_angularDeflectionEfficiency(VehicleData obj, XmlTextReader reader)
        {
            vd.m_angularDeflectionEfficiency = XRfloat(reader, "ADEFF");
        }
        private void ProcessXR_angularDeflectionTimescale(VehicleData obj, XmlTextReader reader)
        {
            vd.m_angularDeflectionTimescale = XRfloat(reader, "ADTIME");
        }
        private void ProcessXR_linearDeflectionEfficiency(VehicleData obj, XmlTextReader reader)
        {
            vd.m_linearDeflectionEfficiency = XRfloat(reader, "LDEFF");
        }
        private void ProcessXR_linearDeflectionTimescale(VehicleData obj, XmlTextReader reader)
        {
            vd.m_linearDeflectionTimescale = XRfloat(reader, "LDTIME");
        }

        //Banking properties
        private void ProcessXR_bankingEfficiency(VehicleData obj, XmlTextReader reader)
        {
            vd.m_bankingEfficiency = XRfloat(reader, "BEFF");
        }
        private void ProcessXR_bankingMix(VehicleData obj, XmlTextReader reader)
        {
            vd.m_bankingMix = XRfloat(reader, "BMIX");
        }
        private void ProcessXR_bankingTimescale(VehicleData obj, XmlTextReader reader)
        {
            vd.m_bankingTimescale = XRfloat(reader, "BTIME");
        }

        //Hover and Buoyancy properties
        private void ProcessXR_VhoverHeight(VehicleData obj, XmlTextReader reader)
        {
            vd.m_VhoverHeight = XRfloat(reader, "HHEI");
        }
        private void ProcessXR_VhoverEfficiency(VehicleData obj, XmlTextReader reader)
        {
            vd.m_VhoverEfficiency = XRfloat(reader, "HEFF");
        }
        private void ProcessXR_VhoverTimescale(VehicleData obj, XmlTextReader reader)
        {
            vd.m_VhoverTimescale = XRfloat(reader, "HTIME");
        }

        private void ProcessXR_VehicleBuoyancy(VehicleData obj, XmlTextReader reader)
        {
            vd.m_VehicleBuoyancy = XRfloat(reader, "VBUO");
        }

        //Attractor properties
        private void ProcessXR_verticalAttractionEfficiency(VehicleData obj, XmlTextReader reader)
        {
            vd.m_verticalAttractionEfficiency = XRfloat(reader, "VAEFF");
        }
        private void ProcessXR_verticalAttractionTimescale(VehicleData obj, XmlTextReader reader)
        {
            vd.m_verticalAttractionTimescale = XRfloat(reader, "VATIME");
        }

        private void ProcessXR_referenceFrame(VehicleData obj, XmlTextReader reader)
        {
            vd.m_referenceFrame = XRquat(reader, "REF_FRAME");

        }
    }
}