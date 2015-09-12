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

/* Revised Aug, Sept 2009 by Kitto Flora. ODEDynamics.cs replaces
 * ODEVehicleSettings.cs. It and ODEPrim.cs are re-organised:
 * ODEPrim.cs contains methods dealing with Prim editing, Prim
 * characteristics and Kinetic motion.
 * ODEDynamics.cs contains methods dealing with Prim Physical motion
 * (dynamics) and the associated settings. Old Linear and angular
 * motors for dynamic motion have been replace with  MoveLinear()
 * and MoveAngular(); 'Physical' is used only to switch ODE dynamic
 * simualtion on/off; VEHICAL_TYPE_NONE/VEHICAL_TYPE_<other> is to
 * switch between 'VEHICLE' parameter use and general dynamics
 * settings use.
 */

// Extensive change Ubit 2012

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using log4net;
using OpenMetaverse;
using OdeAPI;
using OpenSim.Framework;
using OpenSim.Region.PhysicsModules.SharedBase;

namespace OpenSim.Region.PhysicsModule.ubOde
{
    public class ODEDynamics
    {
        public Vehicle Type
        {
            get { return m_type; }
        }

        private OdePrim rootPrim;
        private ODEScene _pParentScene;

        // Vehicle properties
        // WARNING this are working copies for internel use
        // their values may not be the corresponding parameter

        private Quaternion m_referenceFrame = Quaternion.Identity;      // Axis modifier
        private Quaternion m_RollreferenceFrame = Quaternion.Identity;  // what hell is this ?

        private Vehicle m_type = Vehicle.TYPE_NONE;                     // If a 'VEHICLE', and what kind

        private VehicleFlag m_flags = (VehicleFlag) 0;                  // Boolean settings:
                                                                        // HOVER_TERRAIN_ONLY
                                                                        // HOVER_GLOBAL_HEIGHT
                                                                        // NO_DEFLECTION_UP
                                                                        // HOVER_WATER_ONLY
                                                                        // HOVER_UP_ONLY
                                                                        // LIMIT_MOTOR_UP
                                                                        // LIMIT_ROLL_ONLY
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
        private float m_bankingTimescale = 1000;

        //Hover and Buoyancy properties
        private float m_VhoverHeight = 0f;
        private float m_VhoverEfficiency = 0f;
        private float m_VhoverTimescale = 1000f;
        private float m_VehicleBuoyancy = 0f;           //KF: m_VehicleBuoyancy is set by VEHICLE_BUOYANCY for a vehicle.
                    // Modifies gravity. Slider between -1 (double-gravity) and 1 (full anti-gravity)
                    // KF: So far I have found no good method to combine a script-requested .Z velocity and gravity.
                    // Therefore only m_VehicleBuoyancy=1 (0g) will use the script-requested .Z velocity.

        //Attractor properties
        private float m_verticalAttractionEfficiency = 1.0f;        // damped
        private float m_verticalAttractionTimescale = 1000f;        // Timescale > 300  means no vert attractor.


        // auxiliar
        private float m_lmEfect = 0f;                                            // current linear motor eficiency
        private float m_lmDecay = 0f;                                            // current linear decay

        private float m_amEfect = 0;                                            // current angular motor eficiency
        private float m_amDecay = 0f;                                            // current linear decay

        private float m_ffactor = 1.0f;

        private float m_timestep = 0.02f;
        private float m_invtimestep = 50;


        float m_ampwr;
        float m_amdampX;
        float m_amdampY;
        float m_amdampZ;

        float m_gravmod;

        public float FrictionFactor
        {
            get
            {
                return m_ffactor;
            }
        }

        public float GravMod
        {
            set
            {
                m_gravmod = value;
            }
        }


        public ODEDynamics(OdePrim rootp)
        {
            rootPrim = rootp;
            _pParentScene = rootPrim._parent_scene;
            m_timestep = _pParentScene.ODE_STEPSIZE;
            m_invtimestep = 1.0f / m_timestep;
            m_gravmod = rootPrim.GravModifier;
        }

        public void DoSetVehicle(VehicleData vd)
        {
            m_type = vd.m_type;
            m_flags = vd.m_flags;

            
            // Linear properties
            m_linearMotorDirection = vd.m_linearMotorDirection;

            m_linearFrictionTimescale = vd.m_linearFrictionTimescale;
            if (m_linearFrictionTimescale.X < m_timestep) m_linearFrictionTimescale.X = m_timestep;
            if (m_linearFrictionTimescale.Y < m_timestep) m_linearFrictionTimescale.Y = m_timestep;
            if (m_linearFrictionTimescale.Z < m_timestep) m_linearFrictionTimescale.Z = m_timestep;

            m_linearMotorDecayTimescale = vd.m_linearMotorDecayTimescale;
            if (m_linearMotorDecayTimescale < m_timestep) m_linearMotorDecayTimescale = m_timestep;
            m_linearMotorDecayTimescale += 0.2f;
            m_linearMotorDecayTimescale *= m_invtimestep;

            m_linearMotorTimescale = vd.m_linearMotorTimescale;
            if (m_linearMotorTimescale < m_timestep) m_linearMotorTimescale = m_timestep;

            m_linearMotorOffset = vd.m_linearMotorOffset;

            //Angular properties
            m_angularMotorDirection = vd.m_angularMotorDirection;
            m_angularMotorTimescale = vd.m_angularMotorTimescale;
            if (m_angularMotorTimescale < m_timestep) m_angularMotorTimescale = m_timestep;

            m_angularMotorDecayTimescale = vd.m_angularMotorDecayTimescale;
            if (m_angularMotorDecayTimescale < m_timestep) m_angularMotorDecayTimescale = m_timestep;
            m_angularMotorDecayTimescale *= m_invtimestep;

            m_angularFrictionTimescale = vd.m_angularFrictionTimescale;
            if (m_angularFrictionTimescale.X < m_timestep) m_angularFrictionTimescale.X = m_timestep;
            if (m_angularFrictionTimescale.Y < m_timestep) m_angularFrictionTimescale.Y = m_timestep;
            if (m_angularFrictionTimescale.Z < m_timestep) m_angularFrictionTimescale.Z = m_timestep;

            //Deflection properties
            m_angularDeflectionEfficiency = vd.m_angularDeflectionEfficiency;
            m_angularDeflectionTimescale = vd.m_angularDeflectionTimescale;
            if (m_angularDeflectionTimescale < m_timestep) m_angularDeflectionTimescale = m_timestep;

            m_linearDeflectionEfficiency = vd.m_linearDeflectionEfficiency;
            m_linearDeflectionTimescale = vd.m_linearDeflectionTimescale;
            if (m_linearDeflectionTimescale < m_timestep) m_linearDeflectionTimescale = m_timestep;

            //Banking properties
            m_bankingEfficiency = vd.m_bankingEfficiency;
            m_bankingMix = vd.m_bankingMix;
            m_bankingTimescale = vd.m_bankingTimescale;
            if (m_bankingTimescale < m_timestep) m_bankingTimescale = m_timestep;

            //Hover and Buoyancy properties
            m_VhoverHeight = vd.m_VhoverHeight;
            m_VhoverEfficiency = vd.m_VhoverEfficiency;
            m_VhoverTimescale = vd.m_VhoverTimescale;
            if (m_VhoverTimescale < m_timestep) m_VhoverTimescale = m_timestep;

            m_VehicleBuoyancy = vd.m_VehicleBuoyancy;

            //Attractor properties
            m_verticalAttractionEfficiency = vd.m_verticalAttractionEfficiency;
            m_verticalAttractionTimescale = vd.m_verticalAttractionTimescale;
            if (m_verticalAttractionTimescale < m_timestep) m_verticalAttractionTimescale = m_timestep;

            // Axis
            m_referenceFrame = vd.m_referenceFrame;

            m_lmEfect = 0;
            m_lmDecay = (1.0f - 1.0f / m_linearMotorDecayTimescale);
            m_amEfect = 0;
            m_ffactor = 1.0f;
        }

        internal void ProcessFloatVehicleParam(Vehicle pParam, float pValue)
        {
            float len;

            switch (pParam)
            {
                case Vehicle.ANGULAR_DEFLECTION_EFFICIENCY:
                    if (pValue < 0f) pValue = 0f;
                    if (pValue > 1f) pValue = 1f;
                    m_angularDeflectionEfficiency = pValue;
                    break;
                case Vehicle.ANGULAR_DEFLECTION_TIMESCALE:
                    if (pValue < m_timestep) pValue = m_timestep;
                    m_angularDeflectionTimescale = pValue;
                    break;
                case Vehicle.ANGULAR_MOTOR_DECAY_TIMESCALE:
                    if (pValue < m_timestep) pValue = m_timestep;
                    else if (pValue > 120) pValue = 120;
                    m_angularMotorDecayTimescale = pValue * m_invtimestep;
                    m_amDecay = 1.0f - 1.0f / m_angularMotorDecayTimescale;
                    break;
                case Vehicle.ANGULAR_MOTOR_TIMESCALE:
                    if (pValue < m_timestep) pValue = m_timestep;
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
                    if (pValue < m_timestep) pValue = m_timestep;
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
                    if (pValue < m_timestep) pValue = m_timestep;
                    m_VhoverTimescale = pValue;
                    break;
                case Vehicle.LINEAR_DEFLECTION_EFFICIENCY:
                    if (pValue < 0f) pValue = 0f;
                    if (pValue > 1f) pValue = 1f;
                    m_linearDeflectionEfficiency = pValue;
                    break;
                case Vehicle.LINEAR_DEFLECTION_TIMESCALE:
                    if (pValue < m_timestep) pValue = m_timestep;
                    m_linearDeflectionTimescale = pValue;
                    break;
                case Vehicle.LINEAR_MOTOR_DECAY_TIMESCALE:
                    if (pValue < m_timestep) pValue = m_timestep;
                    else if (pValue > 120) pValue = 120;
                    m_linearMotorDecayTimescale = (0.2f +pValue) * m_invtimestep;
                    m_lmDecay = (1.0f - 1.0f / m_linearMotorDecayTimescale);
                    break;
                case Vehicle.LINEAR_MOTOR_TIMESCALE:
                    if (pValue < m_timestep) pValue = m_timestep;
                    m_linearMotorTimescale = pValue;
                    break;
                case Vehicle.VERTICAL_ATTRACTION_EFFICIENCY:
                    if (pValue < 0f) pValue = 0f;
                    if (pValue > 1f) pValue = 1f;
                    m_verticalAttractionEfficiency = pValue;
                    break;
                case Vehicle.VERTICAL_ATTRACTION_TIMESCALE:
                    if (pValue < m_timestep) pValue = m_timestep;
                    m_verticalAttractionTimescale = pValue;
                    break;

                // These are vector properties but the engine lets you use a single float value to
                // set all of the components to the same value
                case Vehicle.ANGULAR_FRICTION_TIMESCALE:
                    if (pValue < m_timestep) pValue = m_timestep;
                    m_angularFrictionTimescale = new Vector3(pValue, pValue, pValue);
                    break;
                case Vehicle.ANGULAR_MOTOR_DIRECTION:
                    m_angularMotorDirection = new Vector3(pValue, pValue, pValue);
                    len = m_angularMotorDirection.Length();
                    if (len > 12.566f)
                        m_angularMotorDirection *= (12.566f / len);

                    m_amEfect = 1.0f ; // turn it on
                    m_amDecay = 1.0f - 1.0f / m_angularMotorDecayTimescale;

                    if (rootPrim.Body != IntPtr.Zero && !d.BodyIsEnabled(rootPrim.Body)
                            && !rootPrim.m_isSelected && !rootPrim.m_disabled)
                        d.BodyEnable(rootPrim.Body);
                    break;
                case Vehicle.LINEAR_FRICTION_TIMESCALE:
                    if (pValue < m_timestep) pValue = m_timestep;
                    m_linearFrictionTimescale = new Vector3(pValue, pValue, pValue);
                    break;
                case Vehicle.LINEAR_MOTOR_DIRECTION:
                    m_linearMotorDirection = new Vector3(pValue, pValue, pValue);
                    len = m_linearMotorDirection.Length();
                    if (len > 100.0f)
                        m_linearMotorDirection *= (100.0f / len);

                    m_lmDecay = 1.0f - 1.0f / m_linearMotorDecayTimescale;
                    m_lmEfect = 1.0f; // turn it on

                    m_ffactor = 0.0f;
                    if (rootPrim.Body != IntPtr.Zero && !d.BodyIsEnabled(rootPrim.Body)
                            && !rootPrim.m_isSelected && !rootPrim.m_disabled)
                        d.BodyEnable(rootPrim.Body);
                    break;
                case Vehicle.LINEAR_MOTOR_OFFSET:
                    m_linearMotorOffset = new Vector3(pValue, pValue, pValue);
                    len = m_linearMotorOffset.Length();
                    if (len > 100.0f)
                        m_linearMotorOffset *= (100.0f / len);
                    break;
            }
        }//end ProcessFloatVehicleParam

        internal void ProcessVectorVehicleParam(Vehicle pParam, Vector3 pValue)
        {
            float len;

            switch (pParam)
            {
                case Vehicle.ANGULAR_FRICTION_TIMESCALE:
                    if (pValue.X < m_timestep) pValue.X = m_timestep;
                    if (pValue.Y < m_timestep) pValue.Y = m_timestep;
                    if (pValue.Z < m_timestep) pValue.Z = m_timestep;

                    m_angularFrictionTimescale = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
                case Vehicle.ANGULAR_MOTOR_DIRECTION:
                    m_angularMotorDirection = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    // Limit requested angular speed to 2 rps= 4 pi rads/sec
                    len = m_angularMotorDirection.Length();
                    if (len > 12.566f)
                        m_angularMotorDirection *= (12.566f / len);

                    m_amEfect = 1.0f; // turn it on
                    m_amDecay = 1.0f - 1.0f / m_angularMotorDecayTimescale;

                    if (rootPrim.Body != IntPtr.Zero && !d.BodyIsEnabled(rootPrim.Body)
                            && !rootPrim.m_isSelected && !rootPrim.m_disabled)
                        d.BodyEnable(rootPrim.Body);
                    break;
                case Vehicle.LINEAR_FRICTION_TIMESCALE:
                    if (pValue.X < m_timestep) pValue.X = m_timestep;
                    if (pValue.Y < m_timestep) pValue.Y = m_timestep;
                    if (pValue.Z < m_timestep) pValue.Z = m_timestep;
                    m_linearFrictionTimescale = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
                case Vehicle.LINEAR_MOTOR_DIRECTION:
                    m_linearMotorDirection = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    len = m_linearMotorDirection.Length();
                    if (len > 100.0f)
                        m_linearMotorDirection *= (100.0f / len);

                    m_lmEfect = 1.0f; // turn it on
                    m_lmDecay = 1.0f - 1.0f / m_linearMotorDecayTimescale;

                    m_ffactor = 0.0f;
                    if (rootPrim.Body != IntPtr.Zero && !d.BodyIsEnabled(rootPrim.Body)
                            && !rootPrim.m_isSelected && !rootPrim.m_disabled)
                        d.BodyEnable(rootPrim.Body);
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

        internal void ProcessRotationVehicleParam(Vehicle pParam, Quaternion pValue)
        {
            switch (pParam)
            {
                case Vehicle.REFERENCE_FRAME:
                    //                    m_referenceFrame = Quaternion.Inverse(pValue);
                    m_referenceFrame = pValue;
                    break;
                case Vehicle.ROLL_FRAME:
                    m_RollreferenceFrame = pValue;
                    break;
            }
        }//end ProcessRotationVehicleParam

        internal void ProcessVehicleFlags(int pParam, bool remove)
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

        internal void ProcessTypeChange(Vehicle pType)
        {
            m_lmEfect = 0;

            m_amEfect = 0;
            m_ffactor = 1f;

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
                case Vehicle.TYPE_NONE:
                    m_linearFrictionTimescale = new Vector3(1000, 1000, 1000);
                    m_angularFrictionTimescale = new Vector3(1000, 1000, 1000);
                    m_linearMotorTimescale = 1000;
                    m_linearMotorDecayTimescale = 120 * m_invtimestep;
                    m_angularMotorTimescale = 1000;
                    m_angularMotorDecayTimescale = 1000  * m_invtimestep;
                    m_VhoverHeight = 0;
                    m_VhoverEfficiency = 1;
                    m_VhoverTimescale = 1000;
                    m_VehicleBuoyancy = 0;
                    m_linearDeflectionEfficiency = 0;
                    m_linearDeflectionTimescale = 1000;
                    m_angularDeflectionEfficiency = 0;
                    m_angularDeflectionTimescale = 1000;
                    m_bankingEfficiency = 0;
                    m_bankingMix = 1;
                    m_bankingTimescale = 1000;
                    m_verticalAttractionEfficiency = 0;
                    m_verticalAttractionTimescale = 1000;

                    m_flags = (VehicleFlag)0;
                    break;

                case Vehicle.TYPE_SLED:
                    m_linearFrictionTimescale = new Vector3(30, 1, 1000);
                    m_angularFrictionTimescale = new Vector3(1000, 1000, 1000);
                    m_linearMotorTimescale = 1000;
                    m_linearMotorDecayTimescale = 120 * m_invtimestep;
                    m_angularMotorTimescale = 1000;
                    m_angularMotorDecayTimescale = 120 * m_invtimestep;
                    m_VhoverHeight = 0;
                    m_VhoverEfficiency = 1;
                    m_VhoverTimescale = 10;
                    m_VehicleBuoyancy = 0;
                    m_linearDeflectionEfficiency = 1;
                    m_linearDeflectionTimescale = 1;
                    m_angularDeflectionEfficiency = 0;
                    m_angularDeflectionTimescale = 10;
                    m_verticalAttractionEfficiency = 1;
                    m_verticalAttractionTimescale = 1000;
                    m_bankingEfficiency = 0;
                    m_bankingMix = 1;
                    m_bankingTimescale = 10;
                    m_flags &=
                         ~(VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY |
                           VehicleFlag.HOVER_GLOBAL_HEIGHT | VehicleFlag.HOVER_UP_ONLY);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP |
                        VehicleFlag.LIMIT_ROLL_ONLY |
                        VehicleFlag.LIMIT_MOTOR_UP);
                    break;

                case Vehicle.TYPE_CAR:
                    m_linearFrictionTimescale = new Vector3(100, 2, 1000);
                    m_angularFrictionTimescale = new Vector3(1000, 1000, 1000);
                    m_linearMotorTimescale = 1;
                    m_linearMotorDecayTimescale = 60 * m_invtimestep;
                    m_angularMotorTimescale = 1;
                    m_angularMotorDecayTimescale = 0.8f * m_invtimestep;
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
                    m_flags &= ~(VehicleFlag.HOVER_WATER_ONLY |
                                VehicleFlag.HOVER_TERRAIN_ONLY |
                                VehicleFlag.HOVER_GLOBAL_HEIGHT);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP |
                                VehicleFlag.LIMIT_ROLL_ONLY |
                                VehicleFlag.LIMIT_MOTOR_UP |
                                VehicleFlag.HOVER_UP_ONLY);
                    break;
                case Vehicle.TYPE_BOAT:
                    m_linearFrictionTimescale = new Vector3(10, 3, 2);
                    m_angularFrictionTimescale = new Vector3(10, 10, 10);
                    m_linearMotorTimescale = 5;
                    m_linearMotorDecayTimescale = 60 * m_invtimestep;
                    m_angularMotorTimescale = 4;
                    m_angularMotorDecayTimescale = 4 * m_invtimestep;
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
                            VehicleFlag.HOVER_UP_ONLY); // |
//                            VehicleFlag.LIMIT_ROLL_ONLY);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP |
                                VehicleFlag.LIMIT_MOTOR_UP |
                                VehicleFlag.HOVER_UP_ONLY |  // new sl
                                VehicleFlag.HOVER_WATER_ONLY);
                    break;

                case Vehicle.TYPE_AIRPLANE:
                    m_linearFrictionTimescale = new Vector3(200, 10, 5);
                    m_angularFrictionTimescale = new Vector3(20, 20, 20);
                    m_linearMotorTimescale = 2;
                    m_linearMotorDecayTimescale = 60 * m_invtimestep;
                    m_angularMotorTimescale = 4;
                    m_angularMotorDecayTimescale = 8 * m_invtimestep;
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
                    m_linearMotorDecayTimescale = 60 * m_invtimestep;
                    m_angularMotorTimescale = 6;
                    m_angularMotorDecayTimescale = 10 * m_invtimestep;
                    m_VhoverHeight = 5;
                    m_VhoverEfficiency = 0.8f;
                    m_VhoverTimescale = 10;
                    m_VehicleBuoyancy = 1;
                    m_linearDeflectionEfficiency = 0;
                    m_linearDeflectionTimescale = 5 * m_invtimestep;
                    m_angularDeflectionEfficiency = 0;
                    m_angularDeflectionTimescale = 5;
                    m_verticalAttractionEfficiency = 1f;
                    m_verticalAttractionTimescale = 1000f;
                    m_bankingEfficiency = 0;
                    m_bankingMix = 0.7f;
                    m_bankingTimescale = 5;
                    m_flags &= ~(VehicleFlag.HOVER_WATER_ONLY |
                        VehicleFlag.HOVER_TERRAIN_ONLY |
                        VehicleFlag.HOVER_UP_ONLY |
                        VehicleFlag.NO_DEFLECTION_UP |
                        VehicleFlag.LIMIT_MOTOR_UP  | //);
                        VehicleFlag.LIMIT_ROLL_ONLY | // new sl
                        VehicleFlag.HOVER_GLOBAL_HEIGHT); // new sl

//                    m_flags |= (VehicleFlag.LIMIT_ROLL_ONLY |
//                        VehicleFlag.HOVER_GLOBAL_HEIGHT);
                    break;

            }

            m_lmDecay = (1.0f - 1.0f / m_linearMotorDecayTimescale);
            m_amDecay = 1.0f - 1.0f / m_angularMotorDecayTimescale;

        }//end SetDefaultsForType

        internal void Stop()
        {
            m_lmEfect = 0;
            m_lmDecay = 0f;
            m_amEfect = 0;
            m_amDecay = 0;
            m_ffactor = 1f;
        }

        public static Vector3 Xrot(Quaternion rot)
        {
            Vector3 vec;
            rot.Normalize(); // just in case
            vec.X = 2 * (rot.X * rot.X + rot.W * rot.W) - 1;
            vec.Y = 2 * (rot.X * rot.Y + rot.Z * rot.W);
            vec.Z = 2 * (rot.X * rot.Z - rot.Y * rot.W);
            return vec;
        }

        public static Vector3 Zrot(Quaternion rot)
        {
            Vector3 vec;
            rot.Normalize(); // just in case
            vec.X = 2 * (rot.X * rot.Z + rot.Y * rot.W);
            vec.Y = 2 * (rot.Y * rot.Z - rot.X * rot.W);
            vec.Z = 2 * (rot.Z * rot.Z + rot.W * rot.W) - 1;

            return vec;
        }

        private const float pi = (float)Math.PI;
        private const float halfpi = 0.5f * (float)Math.PI;
        private const float twopi = 2.0f * pi;

        public static Vector3 ubRot2Euler(Quaternion rot)
        {
            // returns roll in X
            //         pitch in Y
            //         yaw in Z
            Vector3 vec;

            // assuming rot is normalised
            // rot.Normalize();

            float zX = rot.X * rot.Z + rot.Y * rot.W;

            if (zX < -0.49999f)
            {
                vec.X = 0;
                vec.Y = -halfpi;
                vec.Z = (float)(-2d * Math.Atan(rot.X / rot.W));
            }
            else if (zX > 0.49999f)
            {
                vec.X = 0;
                vec.Y = halfpi;
                vec.Z = (float)(2d * Math.Atan(rot.X / rot.W));
            }
            else
            {
                vec.Y = (float)Math.Asin(2 * zX);

                float sqw = rot.W * rot.W;

                float minuszY = rot.X * rot.W - rot.Y * rot.Z;
                float zZ = rot.Z * rot.Z + sqw - 0.5f;

                vec.X = (float)Math.Atan2(minuszY, zZ);

                float yX = rot.Z * rot.W - rot.X * rot.Y; //( have negative ?)
                float yY = rot.X * rot.X + sqw - 0.5f;
                vec.Z = (float)Math.Atan2(yX, yY);
            }
            return vec;
        }

        public static void GetRollPitch(Quaternion rot, out float roll, out float pitch)
        {
            // assuming rot is normalised
            // rot.Normalize();

            float zX = rot.X * rot.Z + rot.Y * rot.W;

            if (zX < -0.49999f)
            {
                roll = 0;
                pitch = -halfpi;
            }
            else if (zX > 0.49999f)
            {
                roll = 0;
                pitch = halfpi;
            }
            else
            {
                pitch = (float)Math.Asin(2 * zX);

                float minuszY = rot.X * rot.W - rot.Y * rot.Z;
                float zZ = rot.Z * rot.Z + rot.W * rot.W - 0.5f;

                roll = (float)Math.Atan2(minuszY, zZ);
            }
            return ;
        }        
        
        internal void Step()
        {
            IntPtr Body = rootPrim.Body;

            d.Mass dmass;
            d.BodyGetMass(Body, out dmass);

            d.Quaternion rot = d.BodyGetQuaternion(Body);
            Quaternion objrotq = new Quaternion(rot.X, rot.Y, rot.Z, rot.W);    // rotq = rotation of object
            Quaternion rotq = objrotq;    // rotq = rotation of object
            rotq *= m_referenceFrame; // rotq is now rotation in vehicle reference frame
            Quaternion irotq = Quaternion.Inverse(rotq);

            d.Vector3 dvtmp;
            Vector3 tmpV;
            Vector3 curVel; // velocity in world
            Vector3 curAngVel; // angular velocity in world
            Vector3 force = Vector3.Zero; // actually linear aceleration until mult by mass in world frame
            Vector3 torque = Vector3.Zero;// actually angular aceleration until mult by Inertia in vehicle frame
            d.Vector3 dtorque = new d.Vector3();

            dvtmp = d.BodyGetLinearVel(Body);
            curVel.X = dvtmp.X;
            curVel.Y = dvtmp.Y;
            curVel.Z = dvtmp.Z;
            Vector3 curLocalVel = curVel * irotq; // current velocity in  local

            dvtmp = d.BodyGetAngularVel(Body);
            curAngVel.X = dvtmp.X;
            curAngVel.Y = dvtmp.Y;
            curAngVel.Z = dvtmp.Z;
            Vector3 curLocalAngVel = curAngVel * irotq; // current angular velocity in  local

            float ldampZ = 0;
            
            // linear motor
            if (m_lmEfect > 0.01 && m_linearMotorTimescale < 1000)
            {
                tmpV = m_linearMotorDirection - curLocalVel; // velocity error
                tmpV *= m_lmEfect / m_linearMotorTimescale; // error to correct in this timestep
                tmpV *= rotq; // to world

                if ((m_flags & VehicleFlag.LIMIT_MOTOR_UP) != 0)
                    tmpV.Z = 0;

                if (m_linearMotorOffset.X != 0 || m_linearMotorOffset.Y != 0 || m_linearMotorOffset.Z != 0)
                {
                    // have offset, do it now
                    tmpV *= dmass.mass;
                    d.BodyAddForceAtRelPos(Body, tmpV.X, tmpV.Y, tmpV.Z, m_linearMotorOffset.X, m_linearMotorOffset.Y, m_linearMotorOffset.Z);
                }
                else
                {
                    force.X += tmpV.X;
                    force.Y += tmpV.Y;
                    force.Z += tmpV.Z;
                }

                m_lmEfect *= m_lmDecay;
//                m_ffactor = 0.01f + 1e-4f * curVel.LengthSquared();
                m_ffactor = 0.0f;
            }
            else
            {
                m_lmEfect = 0;
                m_ffactor = 1f;
            }
           
            // hover
            if (m_VhoverTimescale < 300 && rootPrim.prim_geom != IntPtr.Zero)
            {
                //                d.Vector3 pos = d.BodyGetPosition(Body);
                d.Vector3 pos = d.GeomGetPosition(rootPrim.prim_geom);
                pos.Z -= 0.21f; // minor offset that seems to be always there in sl

                float t = _pParentScene.GetTerrainHeightAtXY(pos.X, pos.Y);
                float perr;

                // default to global but don't go underground
                perr = m_VhoverHeight - pos.Z;

                if ((m_flags & VehicleFlag.HOVER_GLOBAL_HEIGHT) == 0)
                {
                    if ((m_flags & VehicleFlag.HOVER_WATER_ONLY) != 0)
                    {
                        perr += _pParentScene.GetWaterLevel();
                    }
                    else if ((m_flags & VehicleFlag.HOVER_TERRAIN_ONLY) != 0)
                    {
                        perr += t;
                    }
                    else
                    {
                        float w = _pParentScene.GetWaterLevel();
                        if (t > w)
                            perr += t;
                        else
                            perr += w;
                    }
                }
                else if (t > m_VhoverHeight)
                        perr = t - pos.Z; ;

                if ((m_flags & VehicleFlag.HOVER_UP_ONLY) == 0 || perr > -0.1)
                {
                    ldampZ = m_VhoverEfficiency * m_invtimestep;

                    perr *= (1.0f + ldampZ) / m_VhoverTimescale;

                    //                    force.Z += perr - curVel.Z * tmp;
                    force.Z += perr;
                    ldampZ *= -curVel.Z;

                    force.Z += _pParentScene.gravityz * m_gravmod * (1f - m_VehicleBuoyancy);
                }
                else // no buoyancy
                    force.Z += _pParentScene.gravityz;
            }
            else
            {
                // default gravity and Buoyancy
                force.Z += _pParentScene.gravityz * m_gravmod * (1f - m_VehicleBuoyancy);
            }

            // linear deflection
            if (m_linearDeflectionEfficiency > 0)
            {
                float len = curVel.Length();
                if (len > 0.01) // if moving
                {
                    Vector3 atAxis;
                    atAxis = Xrot(rotq); // where are we pointing to
                    atAxis *= len; // make it same size as world velocity vector

                    tmpV = -atAxis; // oposite direction
                    atAxis -= curVel; // error to one direction
                    len = atAxis.LengthSquared();

                    tmpV -= curVel; // error to oposite
                    float lens = tmpV.LengthSquared();

                    if (len > 0.01 || lens > 0.01) // do nothing if close enougth
                    {
                        if (len < lens)
                            tmpV = atAxis;

                        tmpV *= (m_linearDeflectionEfficiency / m_linearDeflectionTimescale); // error to correct in this timestep
                        force.X += tmpV.X;
                        force.Y += tmpV.Y;
                        if ((m_flags & VehicleFlag.NO_DEFLECTION_UP) == 0)
                            force.Z += tmpV.Z;
                    }
                }
            }

            // linear friction/damping
            if (curLocalVel.X != 0 || curLocalVel.Y != 0 || curLocalVel.Z != 0)
            {
                tmpV.X = -curLocalVel.X / m_linearFrictionTimescale.X;
                tmpV.Y = -curLocalVel.Y / m_linearFrictionTimescale.Y;
                tmpV.Z = -curLocalVel.Z / m_linearFrictionTimescale.Z;
                tmpV *= rotq; // to world

                if(ldampZ != 0 && Math.Abs(ldampZ) > Math.Abs(tmpV.Z))
                    tmpV.Z = ldampZ;
                force.X += tmpV.X;
                force.Y += tmpV.Y;
                force.Z += tmpV.Z;
            }

            // vertical atractor
            if (m_verticalAttractionTimescale < 300)
            {
                float roll;
                float pitch;



                float ftmp = m_invtimestep / m_verticalAttractionTimescale / m_verticalAttractionTimescale;

                float ftmp2;
                ftmp2 = 0.5f * m_verticalAttractionEfficiency * m_invtimestep;
                m_amdampX = ftmp2;

                m_ampwr = 1.0f - 0.8f * m_verticalAttractionEfficiency;

                GetRollPitch(irotq, out roll, out pitch);

                if (roll > halfpi)
                    roll = pi - roll;
                else if (roll < -halfpi)
                    roll = -pi - roll;

                float effroll = pitch / halfpi;
                effroll *= effroll;
                effroll = 1 - effroll;
                effroll *= roll;


                torque.X += effroll * ftmp;

                if ((m_flags & VehicleFlag.LIMIT_ROLL_ONLY) == 0)
                {
                    float effpitch = roll / halfpi;
                    effpitch *= effpitch;
                    effpitch = 1 - effpitch;
                    effpitch *= pitch;

                    torque.Y += effpitch * ftmp;
                }

                if (m_bankingEfficiency != 0 && Math.Abs(effroll) > 0.01)
                {

                    float broll = effroll;
                    /*
                                        if (broll > halfpi)
                                            broll = pi - broll;
                                        else if (broll < -halfpi)
                                            broll = -pi - broll;                           
                    */
                    broll *= m_bankingEfficiency;
                    if (m_bankingMix != 0)
                    {
                        float vfact = Math.Abs(curLocalVel.X) / 10.0f;
                        if (vfact > 1.0f) vfact = 1.0f;

                        if (curLocalVel.X >= 0)
                            broll *= (1 + (vfact - 1) * m_bankingMix);
                        else
                            broll *= -(1 + (vfact - 1) * m_bankingMix);
                    }
                    // make z rot be in world Z not local as seems to be in sl

                    broll = broll / m_bankingTimescale;


                    tmpV = Zrot(irotq);
                    tmpV *= broll;

                    torque.X += tmpV.X;
                    torque.Y += tmpV.Y;
                    torque.Z += tmpV.Z;

                    m_amdampZ = Math.Abs(m_bankingEfficiency) / m_bankingTimescale;
                    m_amdampY = m_amdampZ;

                }
                else
                {
                    m_amdampZ = 1 / m_angularFrictionTimescale.Z;
                    m_amdampY = m_amdampX;
                }
            }
            else
            {
                m_ampwr = 1.0f;
                m_amdampX = 1 / m_angularFrictionTimescale.X;
                m_amdampY = 1 / m_angularFrictionTimescale.Y;
                m_amdampZ = 1 / m_angularFrictionTimescale.Z;
            }

            // angular motor
            if (m_amEfect > 0.01 && m_angularMotorTimescale < 1000)
            {
                tmpV = m_angularMotorDirection - curLocalAngVel; // velocity error
                tmpV *= m_amEfect / m_angularMotorTimescale; // error to correct in this timestep
                torque.X += tmpV.X * m_ampwr;
                torque.Y += tmpV.Y * m_ampwr;
                torque.Z += tmpV.Z;

                m_amEfect *= m_amDecay;
            }
            else
                m_amEfect = 0;

            // angular deflection
            if (m_angularDeflectionEfficiency > 0)
            {
                Vector3 dirv;
                
                if (curLocalVel.X > 0.01f)
                    dirv = curLocalVel;
                else if (curLocalVel.X < -0.01f)
                    // use oposite 
                    dirv = -curLocalVel;
                else
                {
                    // make it fall into small positive x case
                    dirv.X = 0.01f;
                    dirv.Y = curLocalVel.Y;
                    dirv.Z = curLocalVel.Z;
                }

                float ftmp = m_angularDeflectionEfficiency / m_angularDeflectionTimescale;

                if (Math.Abs(dirv.Z) > 0.01)
                {
                    torque.Y += - (float)Math.Atan2(dirv.Z, dirv.X) * ftmp;
                }

                if (Math.Abs(dirv.Y) > 0.01)
                {
                    torque.Z += (float)Math.Atan2(dirv.Y, dirv.X) * ftmp;
                }
            }

            // angular friction
            if (curLocalAngVel.X != 0 || curLocalAngVel.Y != 0 || curLocalAngVel.Z != 0)
            {
                torque.X -= curLocalAngVel.X * m_amdampX;
                torque.Y -= curLocalAngVel.Y * m_amdampY;
                torque.Z -= curLocalAngVel.Z * m_amdampZ;
            }
          

            if (force.X != 0 || force.Y != 0 || force.Z != 0)
            {
                force *= dmass.mass;
                d.BodyAddForce(Body, force.X, force.Y, force.Z);
            }

            if (torque.X != 0 || torque.Y != 0 || torque.Z != 0)
            {
                torque *= m_referenceFrame; // to object frame
                dtorque.X = torque.X ;
                dtorque.Y = torque.Y;
                dtorque.Z = torque.Z;

                d.MultiplyM3V3(out dvtmp, ref dmass.I, ref dtorque);
                d.BodyAddRelTorque(Body, dvtmp.X, dvtmp.Y, dvtmp.Z); // add torque in object frame
            }
        }
    }
}
