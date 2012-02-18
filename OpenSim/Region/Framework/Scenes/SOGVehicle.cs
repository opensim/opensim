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
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;


namespace OpenSim.Region.Framework.Scenes
{
    public class SOGVehicle
    {
        public Vehicle Type
        {
            get { return m_type; }
        }
        private Quaternion m_referenceFrame = Quaternion.Identity;      // Axis modifier
        private Quaternion m_RollreferenceFrame = Quaternion.Identity;  // what hell is this ?

        private Vehicle m_type = Vehicle.TYPE_NONE;                     // If a 'VEHICLE', and what kind

        private VehicleFlag m_flags = (VehicleFlag)0;
        private Vector3 m_BlockingEndPoint = Vector3.Zero;              // not sl

        // Linear properties
        private Vector3 m_linearMotorDirection = Vector3.Zero;          // velocity requested by LSL, decayed by time
        private Vector3 m_linearFrictionTimescale = new Vector3(1000, 1000, 1000);
        private float m_linearMotorDecayTimescale = 120;
        private float m_linearMotorTimescale = 1000;
        private Vector3 m_linearMotorOffset = Vector3.Zero;

        //Angular properties
        private Vector3 m_angularMotorDirection = Vector3.Zero;         // angular velocity requested by LSL motor
        private float m_angularMotorTimescale = 1000;                      // motor angular velocity ramp up rate
        private float m_angularMotorDecayTimescale = 120;                 // motor angular velocity decay rate
        private Vector3 m_angularFrictionTimescale = new Vector3(1000, 1000, 1000);      // body angular velocity  decay rate

        //Deflection properties
        private float m_angularDeflectionEfficiency = 0;
        private float m_angularDeflectionTimescale = 1000;
        private float m_linearDeflectionEfficiency = 0;
        private float m_linearDeflectionTimescale = 1000;

        //Banking properties
        private float m_bankingEfficiency = 0;
        private float m_bankingMix = 0;
        private float m_bankingTimescale = 0;

        //Hover and Buoyancy properties
        private float m_VhoverHeight = 0f;
        private float m_VhoverEfficiency = 0f;
        private float m_VhoverTimescale = 1000f;
        private float m_VehicleBuoyancy = 0f;

        //Attractor properties
        private float m_verticalAttractionEfficiency = 1.0f;        // damped
        private float m_verticalAttractionTimescale = 1000f;        // Timescale > 300  means no vert attractor.

        public void ProcessFloatVehicleParam(Vehicle pParam, float pValue)
        {
            float len;
            float timestep = 0.01f;
            switch (pParam)
            {
                case Vehicle.ANGULAR_DEFLECTION_EFFICIENCY:
                    if (pValue < 0f) pValue = 0f;
                    if (pValue > 1f) pValue = 1f;
                    m_angularDeflectionEfficiency = pValue;
                    break;
                case Vehicle.ANGULAR_DEFLECTION_TIMESCALE:
                    if (pValue < timestep) pValue = timestep;
                    m_angularDeflectionTimescale = pValue;
                    break;
                case Vehicle.ANGULAR_MOTOR_DECAY_TIMESCALE:
                    if (pValue < timestep) pValue = timestep;
                    else if (pValue > 120) pValue = 120;
                    m_angularMotorDecayTimescale = pValue;
                    break;
                case Vehicle.ANGULAR_MOTOR_TIMESCALE:
                    if (pValue < timestep) pValue = timestep;
                    m_angularMotorTimescale = pValue;
                    break;
                case Vehicle.BANKING_EFFICIENCY:
                    if (pValue < -1f) pValue = -1f;
                    if (pValue > 1f) pValue = 1f;
                    m_bankingEfficiency = pValue;
                    break;
                case Vehicle.BANKING_MIX:
                    if (pValue < 0f) pValue = 0f;
                    if (pValue > 1f) pValue = 1f;
                    m_bankingMix = pValue;
                    break;
                case Vehicle.BANKING_TIMESCALE:
                    if (pValue < timestep) pValue = timestep;
                    m_bankingTimescale = pValue;
                    break;
                case Vehicle.BUOYANCY:
                    if (pValue < -1f) pValue = -1f;
                    if (pValue > 1f) pValue = 1f;
                    m_VehicleBuoyancy = pValue;
                    break;
                case Vehicle.HOVER_EFFICIENCY:
                    if (pValue < 0f) pValue = 0f;
                    if (pValue > 1f) pValue = 1f;
                    m_VhoverEfficiency = pValue;
                    break;
                case Vehicle.HOVER_HEIGHT:
                    m_VhoverHeight = pValue;
                    break;
                case Vehicle.HOVER_TIMESCALE:
                    if (pValue < timestep) pValue = timestep;
                    m_VhoverTimescale = pValue;
                    break;
                case Vehicle.LINEAR_DEFLECTION_EFFICIENCY:
                    if (pValue < 0f) pValue = 0f;
                    if (pValue > 1f) pValue = 1f;
                    m_linearDeflectionEfficiency = pValue;
                    break;
                case Vehicle.LINEAR_DEFLECTION_TIMESCALE:
                    if (pValue < timestep) pValue = timestep;
                    m_linearDeflectionTimescale = pValue;
                    break;
                case Vehicle.LINEAR_MOTOR_DECAY_TIMESCALE:
                    //                    if (pValue < timestep) pValue = timestep;
                    // try to make impulses to work a bit better
                    if (pValue < timestep) pValue = timestep;
                    else if (pValue > 120) pValue = 120;
                    m_linearMotorDecayTimescale = pValue;
                    break;
                case Vehicle.LINEAR_MOTOR_TIMESCALE:
                    if (pValue < timestep) pValue = timestep;
                    m_linearMotorTimescale = pValue;
                    break;
                case Vehicle.VERTICAL_ATTRACTION_EFFICIENCY:
                    if (pValue < 0f) pValue = 0f;
                    if (pValue > 1f) pValue = 1f;
                    m_verticalAttractionEfficiency = pValue;
                    break;
                case Vehicle.VERTICAL_ATTRACTION_TIMESCALE:
                    if (pValue < timestep) pValue = timestep;
                    m_verticalAttractionTimescale = pValue;
                    break;

                // These are vector properties but the engine lets you use a single float value to
                // set all of the components to the same value
                case Vehicle.ANGULAR_FRICTION_TIMESCALE:
                    if (pValue < timestep) pValue = timestep;
                    m_angularFrictionTimescale = new Vector3(pValue, pValue, pValue);
                    break;
                case Vehicle.ANGULAR_MOTOR_DIRECTION:
                    m_angularMotorDirection = new Vector3(pValue, pValue, pValue);
                    len = m_angularMotorDirection.Length();
                    if (len > 12.566f)
                        m_angularMotorDirection *= (12.566f / len);
                    break;
                case Vehicle.LINEAR_FRICTION_TIMESCALE:
                    if (pValue < timestep) pValue = timestep;
                    m_linearFrictionTimescale = new Vector3(pValue, pValue, pValue);
                    break;
                case Vehicle.LINEAR_MOTOR_DIRECTION:
                    m_linearMotorDirection = new Vector3(pValue, pValue, pValue);
                    len = m_linearMotorDirection.Length();
                    if (len > 30.0f)
                        m_linearMotorDirection *= (30.0f / len);
                    break;
                case Vehicle.LINEAR_MOTOR_OFFSET:
                    m_linearMotorOffset = new Vector3(pValue, pValue, pValue);
                    len = m_linearMotorOffset.Length();
                    if (len > 100.0f)
                        m_linearMotorOffset *= (100.0f / len);
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

                    m_angularFrictionTimescale = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
                case Vehicle.ANGULAR_MOTOR_DIRECTION:
                    m_angularMotorDirection = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    // Limit requested angular speed to 2 rps= 4 pi rads/sec
                    len = m_angularMotorDirection.Length();
                    if (len > 12.566f)
                        m_angularMotorDirection *= (12.566f / len);
                    break;
                case Vehicle.LINEAR_FRICTION_TIMESCALE:
                    if (pValue.X < timestep) pValue.X = timestep;
                    if (pValue.Y < timestep) pValue.Y = timestep;
                    if (pValue.Z < timestep) pValue.Z = timestep;
                    m_linearFrictionTimescale = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
                case Vehicle.LINEAR_MOTOR_DIRECTION:
                    m_linearMotorDirection = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    len = m_linearMotorDirection.Length();
                    if (len > 30.0f)
                        m_linearMotorDirection *= (30.0f / len);
                    break;
                case Vehicle.LINEAR_MOTOR_OFFSET:
                    m_linearMotorOffset = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    len = m_linearMotorOffset.Length();
                    if (len > 100.0f)
                        m_linearMotorOffset *= (100.0f / len);
                    break;
                case Vehicle.BLOCK_EXIT:
                    m_BlockingEndPoint = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
            }
        }//end ProcessVectorVehicleParam

        public void ProcessRotationVehicleParam(Vehicle pParam, Quaternion pValue)
        {
            switch (pParam)
            {
                case Vehicle.REFERENCE_FRAME:
                    m_referenceFrame = Quaternion.Inverse(pValue);
                    break;
                case Vehicle.ROLL_FRAME:
                    m_RollreferenceFrame = pValue;
                    break;
            }
        }//end ProcessRotationVehicleParam

        public void ProcessVehicleFlags(int pParam, bool remove)
        {
            if (remove)
            {
                m_flags &= ~((VehicleFlag)pParam);
            }
            else
            {
                m_flags |= (VehicleFlag)pParam;
            }
        }//end ProcessVehicleFlags

        public void ProcessTypeChange(Vehicle pType)
        {
            m_linearMotorDirection = Vector3.Zero;
            m_angularMotorDirection = Vector3.Zero;

            m_BlockingEndPoint = Vector3.Zero;
            m_RollreferenceFrame = Quaternion.Identity;
            m_linearMotorOffset = Vector3.Zero;

            m_referenceFrame = Quaternion.Identity;

            // Set Defaults For Type
            m_type = pType;
            switch (pType)
            {
                case Vehicle.TYPE_NONE: // none sense this will never exist
                    m_linearFrictionTimescale = new Vector3(1000, 1000, 1000);
                    m_angularFrictionTimescale = new Vector3(1000, 1000, 1000);
                    m_linearMotorTimescale = 1000;
                    m_linearMotorDecayTimescale = 120;
                    m_angularMotorTimescale = 1000;
                    m_angularMotorDecayTimescale = 1000;
                    m_VhoverHeight = 0;
                    m_VhoverTimescale = 1000;
                    m_VehicleBuoyancy = 0;
                    m_flags = (VehicleFlag)0;
                    break;

                case Vehicle.TYPE_SLED:
                    m_linearFrictionTimescale = new Vector3(30, 1, 1000);
                    m_angularFrictionTimescale = new Vector3(1000, 1000, 1000);
                    m_linearMotorTimescale = 1000;
                    m_linearMotorDecayTimescale = 120;
                    m_angularMotorTimescale = 1000;
                    m_angularMotorDecayTimescale = 120;
                    m_VhoverHeight = 0;
                    m_VhoverEfficiency = 1;
                    m_VhoverTimescale = 10;
                    m_VehicleBuoyancy = 0;
                    m_linearDeflectionEfficiency = 1;
                    m_linearDeflectionTimescale = 1;
                    m_angularDeflectionEfficiency = 0;
                    m_angularDeflectionTimescale = 1000;
                    m_bankingEfficiency = 0;
                    m_bankingMix = 1;
                    m_bankingTimescale = 10;
                    m_flags &=
                         ~(VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY |
                           VehicleFlag.HOVER_GLOBAL_HEIGHT | VehicleFlag.HOVER_UP_ONLY);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.LIMIT_ROLL_ONLY | VehicleFlag.LIMIT_MOTOR_UP);
                    break;
                case Vehicle.TYPE_CAR:
                    m_linearFrictionTimescale = new Vector3(100, 2, 1000);
                    m_angularFrictionTimescale = new Vector3(1000, 1000, 1000);
                    m_linearMotorTimescale = 1;
                    m_linearMotorDecayTimescale = 60;
                    m_angularMotorTimescale = 1;
                    m_angularMotorDecayTimescale = 0.8f;
                    m_VhoverHeight = 0;
                    m_VhoverEfficiency = 0;
                    m_VhoverTimescale = 1000;
                    m_VehicleBuoyancy = 0;
                    m_linearDeflectionEfficiency = 1;
                    m_linearDeflectionTimescale = 2;
                    m_angularDeflectionEfficiency = 0;
                    m_angularDeflectionTimescale = 10;
                    m_verticalAttractionEfficiency = 1f;
                    m_verticalAttractionTimescale = 10f;
                    m_bankingEfficiency = -0.2f;
                    m_bankingMix = 1;
                    m_bankingTimescale = 1;
                    m_flags &= ~(VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY | VehicleFlag.HOVER_GLOBAL_HEIGHT);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.LIMIT_ROLL_ONLY |
                                VehicleFlag.LIMIT_MOTOR_UP | VehicleFlag.HOVER_UP_ONLY);
                    break;
                case Vehicle.TYPE_BOAT:
                    m_linearFrictionTimescale = new Vector3(10, 3, 2);
                    m_angularFrictionTimescale = new Vector3(10, 10, 10);
                    m_linearMotorTimescale = 5;
                    m_linearMotorDecayTimescale = 60;
                    m_angularMotorTimescale = 4;
                    m_angularMotorDecayTimescale = 4;
                    m_VhoverHeight = 0;
                    m_VhoverEfficiency = 0.5f;
                    m_VhoverTimescale = 2;
                    m_VehicleBuoyancy = 1;
                    m_linearDeflectionEfficiency = 0.5f;
                    m_linearDeflectionTimescale = 3;
                    m_angularDeflectionEfficiency = 0.5f;
                    m_angularDeflectionTimescale = 5;
                    m_verticalAttractionEfficiency = 0.5f;
                    m_verticalAttractionTimescale = 5f;
                    m_bankingEfficiency = -0.3f;
                    m_bankingMix = 0.8f;
                    m_bankingTimescale = 1;
                    m_flags &= ~(VehicleFlag.HOVER_TERRAIN_ONLY |
                            VehicleFlag.HOVER_GLOBAL_HEIGHT |
                            VehicleFlag.HOVER_UP_ONLY |
                            VehicleFlag.LIMIT_ROLL_ONLY);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP |
                                VehicleFlag.LIMIT_MOTOR_UP |
                                VehicleFlag.HOVER_WATER_ONLY);
                    break;
                case Vehicle.TYPE_AIRPLANE:
                    m_linearFrictionTimescale = new Vector3(200, 10, 5);
                    m_angularFrictionTimescale = new Vector3(20, 20, 20);
                    m_linearMotorTimescale = 2;
                    m_linearMotorDecayTimescale = 60;
                    m_angularMotorTimescale = 4;
                    m_angularMotorDecayTimescale = 8;
                    m_VhoverHeight = 0;
                    m_VhoverEfficiency = 0.5f;
                    m_VhoverTimescale = 1000;
                    m_VehicleBuoyancy = 0;
                    m_linearDeflectionEfficiency = 0.5f;
                    m_linearDeflectionTimescale = 0.5f;
                    m_angularDeflectionEfficiency = 1;
                    m_angularDeflectionTimescale = 2;
                    m_verticalAttractionEfficiency = 0.9f;
                    m_verticalAttractionTimescale = 2f;
                    m_bankingEfficiency = 1;
                    m_bankingMix = 0.7f;
                    m_bankingTimescale = 2;
                    m_flags &= ~(VehicleFlag.HOVER_WATER_ONLY |
                        VehicleFlag.HOVER_TERRAIN_ONLY |
                        VehicleFlag.HOVER_GLOBAL_HEIGHT |
                        VehicleFlag.HOVER_UP_ONLY |
                        VehicleFlag.NO_DEFLECTION_UP |
                        VehicleFlag.LIMIT_MOTOR_UP);
                    m_flags |= (VehicleFlag.LIMIT_ROLL_ONLY);
                    break;
                case Vehicle.TYPE_BALLOON:
                    m_linearFrictionTimescale = new Vector3(5, 5, 5);
                    m_angularFrictionTimescale = new Vector3(10, 10, 10);
                    m_linearMotorTimescale = 5;
                    m_linearMotorDecayTimescale = 60;
                    m_angularMotorTimescale = 6;
                    m_angularMotorDecayTimescale = 10;
                    m_VhoverHeight = 5;
                    m_VhoverEfficiency = 0.8f;
                    m_VhoverTimescale = 10;
                    m_VehicleBuoyancy = 1;
                    m_linearDeflectionEfficiency = 0;
                    m_linearDeflectionTimescale = 5;
                    m_angularDeflectionEfficiency = 0;
                    m_angularDeflectionTimescale = 5;
                    m_verticalAttractionEfficiency = 0f;
                    m_verticalAttractionTimescale = 1000f;
                    m_bankingEfficiency = 0;
                    m_bankingMix = 0.7f;
                    m_bankingTimescale = 5;
                    m_flags &= ~(VehicleFlag.HOVER_WATER_ONLY |
                        VehicleFlag.HOVER_TERRAIN_ONLY |
                        VehicleFlag.HOVER_UP_ONLY |
                        VehicleFlag.NO_DEFLECTION_UP |
                        VehicleFlag.LIMIT_MOTOR_UP);
                    m_flags |= (VehicleFlag.LIMIT_ROLL_ONLY |
                        VehicleFlag.HOVER_GLOBAL_HEIGHT);
                    break;
            }
        }
    }
}