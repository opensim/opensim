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

/* RA: June 14, 2011. Copied from ODEDynamics.cs and converted to
 * call the BulletSim system.
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

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Physics.BulletSPlugin
{
    public class BSDynamics
    {
        private int frcount = 0;                                        // Used to limit dynamics debug output to
                                                                        // every 100th frame

        private BSPrim m_prim;      // the prim this dynamic controller belongs to

        // Vehicle properties
        private Vehicle m_type = Vehicle.TYPE_NONE;                     // If a 'VEHICLE', and what kind
        public Vehicle Type
        {
            get { return m_type; }
        }
        // private Quaternion m_referenceFrame = Quaternion.Identity;   // Axis modifier
        private VehicleFlag m_flags = (VehicleFlag) 0;                  // Boolean settings:
                                                                        // HOVER_TERRAIN_ONLY
                                                                        // HOVER_GLOBAL_HEIGHT
                                                                        // NO_DEFLECTION_UP
                                                                        // HOVER_WATER_ONLY
                                                                        // HOVER_UP_ONLY
                                                                        // LIMIT_MOTOR_UP
                                                                        // LIMIT_ROLL_ONLY
        private VehicleFlag m_Hoverflags = (VehicleFlag)0;
        private Vector3 m_BlockingEndPoint = Vector3.Zero;
        private Quaternion m_RollreferenceFrame = Quaternion.Identity;
        // Linear properties
        private Vector3 m_linearMotorDirection = Vector3.Zero;          // velocity requested by LSL, decayed by time
        private Vector3 m_linearMotorDirectionLASTSET = Vector3.Zero;   // velocity requested by LSL
        private Vector3 m_dir = Vector3.Zero;                           // velocity applied to body
        private Vector3 m_linearFrictionTimescale = Vector3.Zero;
        private float m_linearMotorDecayTimescale = 0;
        private float m_linearMotorTimescale = 0;
        private Vector3 m_lastLinearVelocityVector = Vector3.Zero;
        private Vector3 m_lastPositionVector = Vector3.Zero;
        // private bool m_LinearMotorSetLastFrame = false;
        // private Vector3 m_linearMotorOffset = Vector3.Zero;

        //Angular properties
        private Vector3 m_angularMotorDirection = Vector3.Zero;         // angular velocity requested by LSL motor
        private int m_angularMotorApply = 0;                            // application frame counter
        private Vector3 m_angularMotorVelocity = Vector3.Zero;          // current angular motor velocity
        private float m_angularMotorTimescale = 0;                      // motor angular velocity ramp up rate
        private float m_angularMotorDecayTimescale = 0;                 // motor angular velocity decay rate
        private Vector3 m_angularFrictionTimescale = Vector3.Zero;      // body angular velocity  decay rate
        private Vector3 m_lastAngularVelocity = Vector3.Zero;           // what was last applied to body
 //       private Vector3 m_lastVertAttractor = Vector3.Zero;             // what VA was last applied to body

        //Deflection properties
        // private float m_angularDeflectionEfficiency = 0;
        // private float m_angularDeflectionTimescale = 0;
        // private float m_linearDeflectionEfficiency = 0;
        // private float m_linearDeflectionTimescale = 0;

        //Banking properties
        // private float m_bankingEfficiency = 0;
        // private float m_bankingMix = 0;
        // private float m_bankingTimescale = 0;

        //Hover and Buoyancy properties
        private float m_VhoverHeight = 0f;
//        private float m_VhoverEfficiency = 0f;
        private float m_VhoverTimescale = 0f;
        private float m_VhoverTargetHeight = -1.0f;     // if <0 then no hover, else its the current target height
        private float m_VehicleBuoyancy = 0f;           //KF: m_VehicleBuoyancy is set by VEHICLE_BUOYANCY for a vehicle.
                    // Modifies gravity. Slider between -1 (double-gravity) and 1 (full anti-gravity)
                    // KF: So far I have found no good method to combine a script-requested .Z velocity and gravity.
                    // Therefore only m_VehicleBuoyancy=1 (0g) will use the script-requested .Z velocity.

        //Attractor properties
        private float m_verticalAttractionEfficiency = 1.0f;        // damped
        private float m_verticalAttractionTimescale = 500f;         // Timescale > 300  means no vert attractor.

        public BSDynamics(BSPrim myPrim)
        {
            m_prim = myPrim;
            m_type = Vehicle.TYPE_NONE;
        }

        internal void ProcessFloatVehicleParam(Vehicle pParam, float pValue, float timestep)
        {
            DetailLog("{0},ProcessFloatVehicleParam,param={1},val={2}", m_prim.LocalID, pParam, pValue);
            switch (pParam)
            {
                case Vehicle.ANGULAR_DEFLECTION_EFFICIENCY:
                    if (pValue < 0.01f) pValue = 0.01f;
                    // m_angularDeflectionEfficiency = pValue;
                    break;
                case Vehicle.ANGULAR_DEFLECTION_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    // m_angularDeflectionTimescale = pValue;
                    break;
                case Vehicle.ANGULAR_MOTOR_DECAY_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_angularMotorDecayTimescale = pValue;
                    break;
                case Vehicle.ANGULAR_MOTOR_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_angularMotorTimescale = pValue;
                    break;
                case Vehicle.BANKING_EFFICIENCY:
                    if (pValue < 0.01f) pValue = 0.01f;
                    // m_bankingEfficiency = pValue;
                    break;
                case Vehicle.BANKING_MIX:
                    if (pValue < 0.01f) pValue = 0.01f;
                    // m_bankingMix = pValue;
                    break;
                case Vehicle.BANKING_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    // m_bankingTimescale = pValue;
                    break;
                case Vehicle.BUOYANCY:
                    if (pValue < -1f) pValue = -1f;
                    if (pValue > 1f) pValue = 1f;
                    m_VehicleBuoyancy = pValue;
                    break;
//                case Vehicle.HOVER_EFFICIENCY:
//                    if (pValue < 0f) pValue = 0f;
//                    if (pValue > 1f) pValue = 1f;
//                    m_VhoverEfficiency = pValue;
//                    break;
                case Vehicle.HOVER_HEIGHT:
                    m_VhoverHeight = pValue;
                    break;
                case Vehicle.HOVER_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_VhoverTimescale = pValue;
                    break;
                case Vehicle.LINEAR_DEFLECTION_EFFICIENCY:
                    if (pValue < 0.01f) pValue = 0.01f;
                    // m_linearDeflectionEfficiency = pValue;
                    break;
                case Vehicle.LINEAR_DEFLECTION_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    // m_linearDeflectionTimescale = pValue;
                    break;
                case Vehicle.LINEAR_MOTOR_DECAY_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_linearMotorDecayTimescale = pValue;
                    break;
                case Vehicle.LINEAR_MOTOR_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_linearMotorTimescale = pValue;
                    break;
                case Vehicle.VERTICAL_ATTRACTION_EFFICIENCY:
                    if (pValue < 0.1f) pValue = 0.1f;    // Less goes unstable
                    if (pValue > 1.0f) pValue = 1.0f;
                    m_verticalAttractionEfficiency = pValue;
                    break;
                case Vehicle.VERTICAL_ATTRACTION_TIMESCALE:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_verticalAttractionTimescale = pValue;
                    break;

                // These are vector properties but the engine lets you use a single float value to
                // set all of the components to the same value
                case Vehicle.ANGULAR_FRICTION_TIMESCALE:
                    m_angularFrictionTimescale = new Vector3(pValue, pValue, pValue);
                    break;
                case Vehicle.ANGULAR_MOTOR_DIRECTION:
                    m_angularMotorDirection = new Vector3(pValue, pValue, pValue);
                    m_angularMotorApply = 10;
                    break;
                case Vehicle.LINEAR_FRICTION_TIMESCALE:
                    m_linearFrictionTimescale = new Vector3(pValue, pValue, pValue);
                    break;
                case Vehicle.LINEAR_MOTOR_DIRECTION:
                    m_linearMotorDirection = new Vector3(pValue, pValue, pValue);
                    m_linearMotorDirectionLASTSET = new Vector3(pValue, pValue, pValue);
                    break;
                case Vehicle.LINEAR_MOTOR_OFFSET:
                    // m_linearMotorOffset = new Vector3(pValue, pValue, pValue);
                    break;

            }
        }//end ProcessFloatVehicleParam

        internal void ProcessVectorVehicleParam(Vehicle pParam, Vector3 pValue, float timestep)
        {
            DetailLog("{0},ProcessVectorVehicleParam,param={1},val={2}", m_prim.LocalID, pParam, pValue);
            switch (pParam)
            {
                case Vehicle.ANGULAR_FRICTION_TIMESCALE:
                    m_angularFrictionTimescale = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
                case Vehicle.ANGULAR_MOTOR_DIRECTION:
                    m_angularMotorDirection = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    // Limit requested angular speed to 2 rps= 4 pi rads/sec
                    if (m_angularMotorDirection.X > 12.56f) m_angularMotorDirection.X = 12.56f;
                    if (m_angularMotorDirection.X < - 12.56f) m_angularMotorDirection.X = - 12.56f;
                    if (m_angularMotorDirection.Y > 12.56f) m_angularMotorDirection.Y = 12.56f;
                    if (m_angularMotorDirection.Y < - 12.56f) m_angularMotorDirection.Y = - 12.56f;
                    if (m_angularMotorDirection.Z > 12.56f) m_angularMotorDirection.Z = 12.56f;
                    if (m_angularMotorDirection.Z < - 12.56f) m_angularMotorDirection.Z = - 12.56f;
                    m_angularMotorApply = 10;
                    break;
                case Vehicle.LINEAR_FRICTION_TIMESCALE:
                    m_linearFrictionTimescale = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
                case Vehicle.LINEAR_MOTOR_DIRECTION:
                    m_linearMotorDirection = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    m_linearMotorDirectionLASTSET = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
                case Vehicle.LINEAR_MOTOR_OFFSET:
                    // m_linearMotorOffset = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
                case Vehicle.BLOCK_EXIT:
                    m_BlockingEndPoint = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
            }
        }//end ProcessVectorVehicleParam

        internal void ProcessRotationVehicleParam(Vehicle pParam, Quaternion pValue)
        {
            DetailLog("{0},ProcessRotationalVehicleParam,param={1},val={2}", m_prim.LocalID, pParam, pValue);
            switch (pParam)
            {
                case Vehicle.REFERENCE_FRAME:
                    // m_referenceFrame = pValue;
                    break;
                case Vehicle.ROLL_FRAME:
                    m_RollreferenceFrame = pValue;
                    break;
            }
        }//end ProcessRotationVehicleParam

        internal void ProcessVehicleFlags(int pParam, bool remove)
        {
            DetailLog("{0},ProcessVehicleFlags,param={1},remove={2}", m_prim.LocalID, pParam, remove);
            if (remove)
            {
                if (pParam == -1)
                {
                    m_flags = (VehicleFlag)0;
                    m_Hoverflags = (VehicleFlag)0;
                    return;
                }
                if ((pParam & (int)VehicleFlag.HOVER_GLOBAL_HEIGHT) == (int)VehicleFlag.HOVER_GLOBAL_HEIGHT)
                {
                    if ((m_Hoverflags & VehicleFlag.HOVER_GLOBAL_HEIGHT) != (VehicleFlag)0)
                        m_Hoverflags &= ~(VehicleFlag.HOVER_GLOBAL_HEIGHT);
                }
                if ((pParam & (int)VehicleFlag.HOVER_TERRAIN_ONLY) == (int)VehicleFlag.HOVER_TERRAIN_ONLY)
                {
                    if ((m_Hoverflags & VehicleFlag.HOVER_TERRAIN_ONLY) != (VehicleFlag)0)
                        m_Hoverflags &= ~(VehicleFlag.HOVER_TERRAIN_ONLY);
                }
                if ((pParam & (int)VehicleFlag.HOVER_UP_ONLY) == (int)VehicleFlag.HOVER_UP_ONLY)
                {
                    if ((m_Hoverflags & VehicleFlag.HOVER_UP_ONLY) != (VehicleFlag)0)
                        m_Hoverflags &= ~(VehicleFlag.HOVER_UP_ONLY);
                }
                if ((pParam & (int)VehicleFlag.HOVER_WATER_ONLY) == (int)VehicleFlag.HOVER_WATER_ONLY)
                {
                    if ((m_Hoverflags & VehicleFlag.HOVER_WATER_ONLY) != (VehicleFlag)0)
                        m_Hoverflags &= ~(VehicleFlag.HOVER_WATER_ONLY);
                }
                if ((pParam & (int)VehicleFlag.LIMIT_MOTOR_UP) == (int)VehicleFlag.LIMIT_MOTOR_UP)
                {
                    if ((m_flags & VehicleFlag.LIMIT_MOTOR_UP) != (VehicleFlag)0)
                        m_flags &= ~(VehicleFlag.LIMIT_MOTOR_UP);
                }
                if ((pParam & (int)VehicleFlag.LIMIT_ROLL_ONLY) == (int)VehicleFlag.LIMIT_ROLL_ONLY)
                {
                    if ((m_flags & VehicleFlag.LIMIT_ROLL_ONLY) != (VehicleFlag)0)
                        m_flags &= ~(VehicleFlag.LIMIT_ROLL_ONLY);
                }
                if ((pParam & (int)VehicleFlag.MOUSELOOK_BANK) == (int)VehicleFlag.MOUSELOOK_BANK)
                {
                    if ((m_flags & VehicleFlag.MOUSELOOK_BANK) != (VehicleFlag)0)
                        m_flags &= ~(VehicleFlag.MOUSELOOK_BANK);
                }
                if ((pParam & (int)VehicleFlag.MOUSELOOK_STEER) == (int)VehicleFlag.MOUSELOOK_STEER)
                {
                    if ((m_flags & VehicleFlag.MOUSELOOK_STEER) != (VehicleFlag)0)
                        m_flags &= ~(VehicleFlag.MOUSELOOK_STEER);
                }
                if ((pParam & (int)VehicleFlag.NO_DEFLECTION_UP) == (int)VehicleFlag.NO_DEFLECTION_UP)
                {
                    if ((m_flags & VehicleFlag.NO_DEFLECTION_UP) != (VehicleFlag)0)
                        m_flags &= ~(VehicleFlag.NO_DEFLECTION_UP);
                }
                if ((pParam & (int)VehicleFlag.CAMERA_DECOUPLED) == (int)VehicleFlag.CAMERA_DECOUPLED)
                {
                    if ((m_flags & VehicleFlag.CAMERA_DECOUPLED) != (VehicleFlag)0)
                        m_flags &= ~(VehicleFlag.CAMERA_DECOUPLED);
                }
                if ((pParam & (int)VehicleFlag.NO_X) == (int)VehicleFlag.NO_X)
                {
                    if ((m_flags & VehicleFlag.NO_X) != (VehicleFlag)0)
                        m_flags &= ~(VehicleFlag.NO_X);
                }
                if ((pParam & (int)VehicleFlag.NO_Y) == (int)VehicleFlag.NO_Y)
                {
                    if ((m_flags & VehicleFlag.NO_Y) != (VehicleFlag)0)
                        m_flags &= ~(VehicleFlag.NO_Y);
                }
                if ((pParam & (int)VehicleFlag.NO_Z) == (int)VehicleFlag.NO_Z)
                {
                    if ((m_flags & VehicleFlag.NO_Z) != (VehicleFlag)0)
                        m_flags &= ~(VehicleFlag.NO_Z);
                }
                if ((pParam & (int)VehicleFlag.LOCK_HOVER_HEIGHT) == (int)VehicleFlag.LOCK_HOVER_HEIGHT)
                {
                    if ((m_Hoverflags & VehicleFlag.LOCK_HOVER_HEIGHT) != (VehicleFlag)0)
                        m_Hoverflags &= ~(VehicleFlag.LOCK_HOVER_HEIGHT);
                }
                if ((pParam & (int)VehicleFlag.NO_DEFLECTION) == (int)VehicleFlag.NO_DEFLECTION)
                {
                    if ((m_flags & VehicleFlag.NO_DEFLECTION) != (VehicleFlag)0)
                        m_flags &= ~(VehicleFlag.NO_DEFLECTION);
                }
                if ((pParam & (int)VehicleFlag.LOCK_ROTATION) == (int)VehicleFlag.LOCK_ROTATION)
                {
                    if ((m_flags & VehicleFlag.LOCK_ROTATION) != (VehicleFlag)0)
                        m_flags &= ~(VehicleFlag.LOCK_ROTATION);
                }
            }
            else
            {
                if ((pParam & (int)VehicleFlag.HOVER_GLOBAL_HEIGHT) == (int)VehicleFlag.HOVER_GLOBAL_HEIGHT)
                {
                    m_Hoverflags |= (VehicleFlag.HOVER_GLOBAL_HEIGHT | m_flags);
                }
                if ((pParam & (int)VehicleFlag.HOVER_TERRAIN_ONLY) == (int)VehicleFlag.HOVER_TERRAIN_ONLY)
                {
                    m_Hoverflags |= (VehicleFlag.HOVER_TERRAIN_ONLY | m_flags);
                }
                if ((pParam & (int)VehicleFlag.HOVER_UP_ONLY) == (int)VehicleFlag.HOVER_UP_ONLY)
                {
                    m_Hoverflags |= (VehicleFlag.HOVER_UP_ONLY | m_flags);
                }
                if ((pParam & (int)VehicleFlag.HOVER_WATER_ONLY) == (int)VehicleFlag.HOVER_WATER_ONLY)
                {
                    m_Hoverflags |= (VehicleFlag.HOVER_WATER_ONLY | m_flags);
                }
                if ((pParam & (int)VehicleFlag.LIMIT_MOTOR_UP) == (int)VehicleFlag.LIMIT_MOTOR_UP)
                {
                    m_flags |= (VehicleFlag.LIMIT_MOTOR_UP | m_flags);
                }
                if ((pParam & (int)VehicleFlag.MOUSELOOK_BANK) == (int)VehicleFlag.MOUSELOOK_BANK)
                {
                    m_flags |= (VehicleFlag.MOUSELOOK_BANK | m_flags);
                }
                if ((pParam & (int)VehicleFlag.MOUSELOOK_STEER) == (int)VehicleFlag.MOUSELOOK_STEER)
                {
                    m_flags |= (VehicleFlag.MOUSELOOK_STEER | m_flags);
                }
                if ((pParam & (int)VehicleFlag.NO_DEFLECTION_UP) == (int)VehicleFlag.NO_DEFLECTION_UP)
                {
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP | m_flags);
                }
                if ((pParam & (int)VehicleFlag.CAMERA_DECOUPLED) == (int)VehicleFlag.CAMERA_DECOUPLED)
                {
                    m_flags |= (VehicleFlag.CAMERA_DECOUPLED | m_flags);
                }
                if ((pParam & (int)VehicleFlag.NO_X) == (int)VehicleFlag.NO_X)
                {
                    m_flags |= (VehicleFlag.NO_X);
                }
                if ((pParam & (int)VehicleFlag.NO_Y) == (int)VehicleFlag.NO_Y)
                {
                    m_flags |= (VehicleFlag.NO_Y);
                }
                if ((pParam & (int)VehicleFlag.NO_Z) == (int)VehicleFlag.NO_Z)
                {
                    m_flags |= (VehicleFlag.NO_Z);
                }
                if ((pParam & (int)VehicleFlag.LOCK_HOVER_HEIGHT) == (int)VehicleFlag.LOCK_HOVER_HEIGHT)
                {
                    m_Hoverflags |= (VehicleFlag.LOCK_HOVER_HEIGHT);
                }
                if ((pParam & (int)VehicleFlag.NO_DEFLECTION) == (int)VehicleFlag.NO_DEFLECTION)
                {
                    m_flags |= (VehicleFlag.NO_DEFLECTION);
                }
                if ((pParam & (int)VehicleFlag.LOCK_ROTATION) == (int)VehicleFlag.LOCK_ROTATION)
                {
                    m_flags |= (VehicleFlag.LOCK_ROTATION);
                }
            }
        }//end ProcessVehicleFlags

        internal void ProcessTypeChange(Vehicle pType)
        {
            DetailLog("{0},ProcessTypeChange,type={1}", m_prim.LocalID, pType);
            // Set Defaults For Type
            m_type = pType;
            switch (pType)
            {
                    case Vehicle.TYPE_NONE:
                    m_linearFrictionTimescale = new Vector3(0, 0, 0);
                    m_angularFrictionTimescale = new Vector3(0, 0, 0);
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 0;
                    m_linearMotorDecayTimescale = 0;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 0;
                    m_angularMotorDecayTimescale = 0;
                    m_VhoverHeight = 0;
                    m_VhoverTimescale = 0;
                    m_VehicleBuoyancy = 0;
                    m_flags = (VehicleFlag)0;
                    break;

                case Vehicle.TYPE_SLED:
                    m_linearFrictionTimescale = new Vector3(30, 1, 1000);
                    m_angularFrictionTimescale = new Vector3(1000, 1000, 1000);
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 1000;
                    m_linearMotorDecayTimescale = 120;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 1000;
                    m_angularMotorDecayTimescale = 120;
                    m_VhoverHeight = 0;
//                    m_VhoverEfficiency = 1;
                    m_VhoverTimescale = 10;
                    m_VehicleBuoyancy = 0;
                    // m_linearDeflectionEfficiency = 1;
                    // m_linearDeflectionTimescale = 1;
                    // m_angularDeflectionEfficiency = 1;
                    // m_angularDeflectionTimescale = 1000;
                    // m_bankingEfficiency = 0;
                    // m_bankingMix = 1;
                    // m_bankingTimescale = 10;
                    // m_referenceFrame = Quaternion.Identity;
                    m_Hoverflags &=
                         ~(VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY |
                           VehicleFlag.HOVER_GLOBAL_HEIGHT | VehicleFlag.HOVER_UP_ONLY);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.LIMIT_ROLL_ONLY | VehicleFlag.LIMIT_MOTOR_UP);
                    break;
                case Vehicle.TYPE_CAR:
                    m_linearFrictionTimescale = new Vector3(100, 2, 1000);
                    m_angularFrictionTimescale = new Vector3(1000, 1000, 1000);
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 1;
                    m_linearMotorDecayTimescale = 60;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 1;
                    m_angularMotorDecayTimescale = 0.8f;
                    m_VhoverHeight = 0;
//                    m_VhoverEfficiency = 0;
                    m_VhoverTimescale = 1000;
                    m_VehicleBuoyancy = 0;
                    // // m_linearDeflectionEfficiency = 1;
                    // // m_linearDeflectionTimescale = 2;
                    // // m_angularDeflectionEfficiency = 0;
                    // m_angularDeflectionTimescale = 10;
                    m_verticalAttractionEfficiency = 1f;
                    m_verticalAttractionTimescale = 10f;
                    // m_bankingEfficiency = -0.2f;
                    // m_bankingMix = 1;
                    // m_bankingTimescale = 1;
                    // m_referenceFrame = Quaternion.Identity;
                    m_Hoverflags &= ~(VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY | VehicleFlag.HOVER_GLOBAL_HEIGHT);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.LIMIT_ROLL_ONLY |
                                VehicleFlag.LIMIT_MOTOR_UP);
                    m_Hoverflags |= (VehicleFlag.HOVER_UP_ONLY);
                    break;
                case Vehicle.TYPE_BOAT:
                    m_linearFrictionTimescale = new Vector3(10, 3, 2);
                    m_angularFrictionTimescale = new Vector3(10,10,10);
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 5;
                    m_linearMotorDecayTimescale = 60;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 4;
                    m_angularMotorDecayTimescale = 4;
                    m_VhoverHeight = 0;
//                    m_VhoverEfficiency = 0.5f;
                    m_VhoverTimescale = 2;
                    m_VehicleBuoyancy = 1;
                    // m_linearDeflectionEfficiency = 0.5f;
                    // m_linearDeflectionTimescale = 3;
                    // m_angularDeflectionEfficiency = 0.5f;
                    // m_angularDeflectionTimescale = 5;
                    m_verticalAttractionEfficiency = 0.5f;
                    m_verticalAttractionTimescale = 5f;
                    // m_bankingEfficiency = -0.3f;
                    // m_bankingMix = 0.8f;
                    // m_bankingTimescale = 1;
                    // m_referenceFrame = Quaternion.Identity;
                    m_Hoverflags &= ~(VehicleFlag.HOVER_TERRAIN_ONLY |
                            VehicleFlag.HOVER_GLOBAL_HEIGHT | VehicleFlag.HOVER_UP_ONLY);
                    m_flags &= ~(VehicleFlag.LIMIT_ROLL_ONLY);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP |
                                VehicleFlag.LIMIT_MOTOR_UP);
                    m_Hoverflags |= (VehicleFlag.HOVER_WATER_ONLY);
                    break;
                case Vehicle.TYPE_AIRPLANE:
                    m_linearFrictionTimescale = new Vector3(200, 10, 5);
                    m_angularFrictionTimescale = new Vector3(20, 20, 20);
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 2;
                    m_linearMotorDecayTimescale = 60;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 4;
                    m_angularMotorDecayTimescale = 4;
                    m_VhoverHeight = 0;
//                    m_VhoverEfficiency = 0.5f;
                    m_VhoverTimescale = 1000;
                    m_VehicleBuoyancy = 0;
                    // m_linearDeflectionEfficiency = 0.5f;
                    // m_linearDeflectionTimescale = 3;
                    // m_angularDeflectionEfficiency = 1;
                    // m_angularDeflectionTimescale = 2;
                    m_verticalAttractionEfficiency = 0.9f;
                    m_verticalAttractionTimescale = 2f;
                    // m_bankingEfficiency = 1;
                    // m_bankingMix = 0.7f;
                    // m_bankingTimescale = 2;
                    // m_referenceFrame = Quaternion.Identity;
                    m_Hoverflags &= ~(VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY |
                        VehicleFlag.HOVER_GLOBAL_HEIGHT | VehicleFlag.HOVER_UP_ONLY);
                    m_flags &= ~(VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.LIMIT_MOTOR_UP);
                    m_flags |= (VehicleFlag.LIMIT_ROLL_ONLY);
                    break;
                case Vehicle.TYPE_BALLOON:
                    m_linearFrictionTimescale = new Vector3(5, 5, 5);
                    m_angularFrictionTimescale = new Vector3(10, 10, 10);
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 5;
                    m_linearMotorDecayTimescale = 60;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 6;
                    m_angularMotorDecayTimescale = 10;
                    m_VhoverHeight = 5;
//                    m_VhoverEfficiency = 0.8f;
                    m_VhoverTimescale = 10;
                    m_VehicleBuoyancy = 1;
                    // m_linearDeflectionEfficiency = 0;
                    // m_linearDeflectionTimescale = 5;
                    // m_angularDeflectionEfficiency = 0;
                    // m_angularDeflectionTimescale = 5;
                    m_verticalAttractionEfficiency = 1f;
                    m_verticalAttractionTimescale = 100f;
                    // m_bankingEfficiency = 0;
                    // m_bankingMix = 0.7f;
                    // m_bankingTimescale = 5;
                    // m_referenceFrame = Quaternion.Identity;
                    m_Hoverflags &= ~(VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY |
                        VehicleFlag.HOVER_UP_ONLY);
                    m_flags &= ~(VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.LIMIT_MOTOR_UP);
                    m_flags |= (VehicleFlag.LIMIT_ROLL_ONLY);
                    m_Hoverflags |= (VehicleFlag.HOVER_GLOBAL_HEIGHT);
                    break;
            }
        }//end SetDefaultsForType

        internal void Step(float pTimestep)
        {
            if (m_type == Vehicle.TYPE_NONE) return;

            frcount++;  // used to limit debug comment output
            if (frcount > 100)
                frcount = 0;

            MoveLinear(pTimestep);
            MoveAngular(pTimestep);
            LimitRotation(pTimestep);

            DetailLog("{0},BSDynamics.Step,done,pos={1},force={2},velocity={3},angvel={4}", 
                    m_prim.LocalID, m_prim.Position, m_prim.Force, m_prim.Velocity, m_prim.RotationalVelocity);
        }// end Step

        private void MoveLinear(float pTimestep)
        {
            // requested m_linearMotorDirection is significant
            // if (!m_linearMotorDirection.ApproxEquals(Vector3.Zero, 0.01f))
            if (m_linearMotorDirection.LengthSquared() > 0.0001f)
            {
                Vector3 origDir = m_linearMotorDirection;
                Vector3 origVel = m_lastLinearVelocityVector;

                // add drive to body
                // Vector3 addAmount = m_linearMotorDirection/(m_linearMotorTimescale/pTimestep);
                Vector3 addAmount = m_linearMotorDirection/(m_linearMotorTimescale);
                // lastLinearVelocityVector is the current body velocity vector?
                // RA: Not sure what the *10 is for. A correction for pTimestep?
                // m_lastLinearVelocityVector += (addAmount*10);  
                m_lastLinearVelocityVector += addAmount;  

                // This will work temporarily, but we really need to compare speed on an axis
                // KF: Limit body velocity to applied velocity?
                // Limit the velocity vector to less than the last set linear motor direction
                if (Math.Abs(m_lastLinearVelocityVector.X) > Math.Abs(m_linearMotorDirectionLASTSET.X))
                    m_lastLinearVelocityVector.X = m_linearMotorDirectionLASTSET.X;
                if (Math.Abs(m_lastLinearVelocityVector.Y) > Math.Abs(m_linearMotorDirectionLASTSET.Y))
                    m_lastLinearVelocityVector.Y = m_linearMotorDirectionLASTSET.Y;
                if (Math.Abs(m_lastLinearVelocityVector.Z) > Math.Abs(m_linearMotorDirectionLASTSET.Z))
                    m_lastLinearVelocityVector.Z = m_linearMotorDirectionLASTSET.Z;

                // decay applied velocity
                Vector3 decayfraction = ((Vector3.One/(m_linearMotorDecayTimescale/pTimestep)));
                m_linearMotorDirection -= m_linearMotorDirection * decayfraction * 0.5f;

                /*
                Vector3 addAmount = (m_linearMotorDirection - m_lastLinearVelocityVector)/m_linearMotorTimescale;
                m_lastLinearVelocityVector += addAmount;

                float decayfraction = (1.0f - 1.0f / m_linearMotorDecayTimescale);
                m_linearMotorDirection *= decayfraction;

                 */

                DetailLog("{0},MoveLinear,nonZero,origdir={1},origvel={2},add={3},decay={4},dir={5},vel={6}",
                    m_prim.LocalID, origDir, origVel, addAmount, decayfraction, m_linearMotorDirection, m_lastLinearVelocityVector);
            }
            else
            {
                // if what remains of applied is small, zero it.
                // if (m_lastLinearVelocityVector.ApproxEquals(Vector3.Zero, 0.01f))
                //     m_lastLinearVelocityVector = Vector3.Zero;
                m_linearMotorDirection = Vector3.Zero;
                m_lastLinearVelocityVector = Vector3.Zero;
            }

            // convert requested object velocity to world-referenced vector
            Quaternion rotq = m_prim.Orientation;
            m_dir = m_lastLinearVelocityVector * rotq;

            // Add the various forces into m_dir which will be our new direction vector (velocity)

            // add Gravity and Buoyancy
            // KF: So far I have found no good method to combine a script-requested
            // .Z velocity and gravity. Therefore only 0g will used script-requested
            // .Z velocity. >0g (m_VehicleBuoyancy < 1) will used modified gravity only.
            Vector3 grav = Vector3.Zero;
            // There is some gravity, make a gravity force vector that is applied after object velocity.
            // m_VehicleBuoyancy: -1=2g; 0=1g; 1=0g;
            grav.Z = m_prim.Scene.DefaultGravity.Z * m_prim.Mass * (1f - m_VehicleBuoyancy);
            // Preserve the current Z velocity
            Vector3 vel_now = m_prim.Velocity;
            m_dir.Z = vel_now.Z;        // Preserve the accumulated falling velocity

            Vector3 pos = m_prim.Position;
            Vector3 posChange = pos;
//            Vector3 accel = new Vector3(-(m_dir.X - m_lastLinearVelocityVector.X / 0.1f), -(m_dir.Y - m_lastLinearVelocityVector.Y / 0.1f), m_dir.Z - m_lastLinearVelocityVector.Z / 0.1f);
            double Zchange = Math.Abs(posChange.Z);
            if (m_BlockingEndPoint != Vector3.Zero)
            {
                bool changed = false;
                if (pos.X >= (m_BlockingEndPoint.X - (float)1))
                {
                    pos.X -= posChange.X + 1;
                    changed = true;
                }
                if (pos.Y >= (m_BlockingEndPoint.Y - (float)1))
                {
                    pos.Y -= posChange.Y + 1;
                    changed = true;
                }
                if (pos.Z >= (m_BlockingEndPoint.Z - (float)1))
                {
                    pos.Z -= posChange.Z + 1;
                    changed = true;
                }
                if (pos.X <= 0)
                {
                    pos.X += posChange.X + 1;
                    changed = true;
                }
                if (pos.Y <= 0)
                {
                    pos.Y += posChange.Y + 1;
                    changed = true;
                }
                if (changed)
                {
                    m_prim.Position = pos;
                    DetailLog("{0},MoveLinear,blockingEndPoint,block={1},origPos={2},pos={3}",
                                m_prim.LocalID, m_BlockingEndPoint, posChange, pos);
                }
            }

            // If below the terrain, move us above the ground a little.
            if (pos.Z < m_prim.Scene.GetTerrainHeightAtXYZ(pos))
            {
                pos.Z = m_prim.Scene.GetTerrainHeightAtXYZ(pos) + 2;
                m_prim.Position = pos;
                DetailLog("{0},MoveLinear,terrainHeight,pos={1}", m_prim.LocalID, pos);
            }

            // Check if hovering
            if ((m_Hoverflags & (VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY | VehicleFlag.HOVER_GLOBAL_HEIGHT)) != 0)
            {
                // We should hover, get the target height
                if ((m_Hoverflags & VehicleFlag.HOVER_WATER_ONLY) != 0)
                {
                    m_VhoverTargetHeight = m_prim.Scene.GetWaterLevel() + m_VhoverHeight;
                }
                if ((m_Hoverflags & VehicleFlag.HOVER_TERRAIN_ONLY) != 0)
                {
                    m_VhoverTargetHeight = m_prim.Scene.GetTerrainHeightAtXY(pos.X, pos.Y) + m_VhoverHeight;
                }
                if ((m_Hoverflags & VehicleFlag.HOVER_GLOBAL_HEIGHT) != 0)
                {
                    m_VhoverTargetHeight = m_VhoverHeight;
                }

                if ((m_Hoverflags & VehicleFlag.HOVER_UP_ONLY) != 0)
                {
                    // If body is aready heigher, use its height as target height
                    if (pos.Z > m_VhoverTargetHeight) m_VhoverTargetHeight = pos.Z;
                }
                if ((m_Hoverflags & VehicleFlag.LOCK_HOVER_HEIGHT) != 0)
                {
                    if ((pos.Z - m_VhoverTargetHeight) > .2 || (pos.Z - m_VhoverTargetHeight) < -.2)
                    {
                        m_prim.Position = pos;
                    }
                }
                else
                {
                    float herr0 = pos.Z - m_VhoverTargetHeight;
                    // Replace Vertical speed with correction figure if significant
                    if (Math.Abs(herr0) > 0.01f)
                    {
                        m_dir.Z = -((herr0 * pTimestep * 50.0f) / m_VhoverTimescale);
                        //KF: m_VhoverEfficiency is not yet implemented
                    }
                    else
                    {
                        m_dir.Z = 0f;
                    }
                }

                DetailLog("{0},MoveLinear,hover,pos={1},dir={2},height={3},target={4}", m_prim.LocalID, pos, m_dir, m_VhoverHeight, m_VhoverTargetHeight);

//                m_VhoverEfficiency = 0f;    // 0=boucy, 1=Crit.damped
//                m_VhoverTimescale = 0f;        // time to acheive height
//                pTimestep  is time since last frame,in secs
            }

            if ((m_flags & (VehicleFlag.LIMIT_MOTOR_UP)) != 0)
            {
                //Start Experimental Values
                if (Zchange > .3)
                {
                    grav.Z = (float)(grav.Z * 3);
                }
                if (Zchange > .15)
                {
                    grav.Z = (float)(grav.Z * 2);
                }
                if (Zchange > .75)
                {
                    grav.Z = (float)(grav.Z * 1.5);
                }
                if (Zchange > .05)
                {
                    grav.Z = (float)(grav.Z * 1.25);
                }
                if (Zchange > .025)
                {
                    grav.Z = (float)(grav.Z * 1.125);
                }
                float terraintemp = m_prim.Scene.GetTerrainHeightAtXYZ(pos);
                float postemp = (pos.Z - terraintemp);
                if (postemp > 2.5f)
                {
                    grav.Z = (float)(grav.Z * 1.037125);
                }
                DetailLog("{0},MoveLinear,limitMotorUp,grav={1}", m_prim.LocalID, grav);
                //End Experimental Values
            }
            if ((m_flags & (VehicleFlag.NO_X)) != 0)
            {
                m_dir.X = 0;
            }
            if ((m_flags & (VehicleFlag.NO_Y)) != 0)
            {
                m_dir.Y = 0;
            }
            if ((m_flags & (VehicleFlag.NO_Z)) != 0)
            {
                m_dir.Z = 0;
            }

            m_lastPositionVector = m_prim.Position;

            // Apply velocity
            m_prim.Velocity = m_dir;
            // apply gravity force
            // Why is this set here? The physics engine already does gravity.
            // m_prim.AddForce(grav, false);
            // m_prim.Force = grav;

            // Apply friction
            Vector3 decayamount = Vector3.One / (m_linearFrictionTimescale / pTimestep);
            m_lastLinearVelocityVector -= m_lastLinearVelocityVector * decayamount;

            DetailLog("{0},MoveLinear,done,pos={1},vel={2},force={3},decay={4}", 
                        m_prim.LocalID, m_lastPositionVector, m_dir, grav, decayamount);

        } // end MoveLinear()

        private void MoveAngular(float pTimestep)
        {
            // m_angularMotorDirection         // angular velocity requested by LSL motor
            // m_angularMotorApply             // application frame counter
            // m_angularMotorVelocity          // current angular motor velocity (ramps up and down)
            // m_angularMotorTimescale         // motor angular velocity ramp up rate
            // m_angularMotorDecayTimescale    // motor angular velocity decay rate
            // m_angularFrictionTimescale      // body angular velocity  decay rate
            // m_lastAngularVelocity           // what was last applied to body

            // Get what the body is doing, this includes 'external' influences
            Vector3 angularVelocity = m_prim.RotationalVelocity;

            if (m_angularMotorApply > 0)
            {
                // Rather than snapping the angular motor velocity from the old value to
                //    a newly set velocity, this routine steps the value from the previous
                //    value (m_angularMotorVelocity) to the requested value (m_angularMotorDirection).
                // There are m_angularMotorApply steps.
                Vector3 origAngularVelocity = m_angularMotorVelocity;
                // ramp up to new value
                //   current velocity  +=                         error                       /    (time to get there / step interval)
                //                               requested speed            -  last motor speed
                m_angularMotorVelocity.X += (m_angularMotorDirection.X - m_angularMotorVelocity.X) /  (m_angularMotorTimescale / pTimestep);
                m_angularMotorVelocity.Y += (m_angularMotorDirection.Y - m_angularMotorVelocity.Y) /  (m_angularMotorTimescale / pTimestep);
                m_angularMotorVelocity.Z += (m_angularMotorDirection.Z - m_angularMotorVelocity.Z) /  (m_angularMotorTimescale / pTimestep);

                DetailLog("{0},MoveAngular,angularMotorApply,apply={1},origvel={2},dir={3},vel={4}", 
                        m_prim.LocalID,m_angularMotorApply,origAngularVelocity, m_angularMotorDirection, m_angularMotorVelocity);

                m_angularMotorApply--;        // This is done so that if script request rate is less than phys frame rate the expected
                                            // velocity may still be acheived.
            }
            else
            {
                // No motor recently applied, keep the body velocity
                // and decay the velocity
                m_angularMotorVelocity -= m_angularMotorVelocity /  (m_angularMotorDecayTimescale / pTimestep);
            } // end motor section

            // Vertical attractor section
            Vector3 vertattr = Vector3.Zero;
            if (m_verticalAttractionTimescale < 300)
            {
                float VAservo = 0.2f / (m_verticalAttractionTimescale * pTimestep);
                // get present body rotation
                Quaternion rotq = m_prim.Orientation;
                // make a vector pointing up
                Vector3 verterr = Vector3.Zero;
                verterr.Z = 1.0f;
                // rotate it to Body Angle
                verterr = verterr * rotq;
                // verterr.X and .Y are the World error ammounts. They are 0 when there is no error (Vehicle Body is 'vertical'), and .Z will be 1.
                // As the body leans to its side |.X| will increase to 1 and .Z fall to 0. As body inverts |.X| will fall and .Z will go
                // negative. Similar for tilt and |.Y|. .X and .Y must be modulated to prevent a stable inverted body.
                if (verterr.Z < 0.0f)
                {
                    verterr.X = 2.0f - verterr.X;
                    verterr.Y = 2.0f - verterr.Y;
                }
                // Error is 0 (no error) to +/- 2 (max error)
                // scale it by VAservo
                verterr = verterr * VAservo;

                // As the body rotates around the X axis, then verterr.Y increases; Rotated around Y then .X increases, so
                // Change  Body angular velocity  X based on Y, and Y based on X. Z is not changed.
                vertattr.X =    verterr.Y;
                vertattr.Y =  - verterr.X;
                vertattr.Z = 0f;

                // scaling appears better usingsquare-law
                float bounce = 1.0f - (m_verticalAttractionEfficiency * m_verticalAttractionEfficiency);
                vertattr.X += bounce * angularVelocity.X;
                vertattr.Y += bounce * angularVelocity.Y;

                DetailLog("{0},MoveAngular,verticalAttraction,verterr={1},bounce={2},vertattr={3}", 
                            m_prim.LocalID, verterr, bounce, vertattr);

            } // else vertical attractor is off

            // m_lastVertAttractor = vertattr;

            // Bank section tba

            // Deflection section tba

            // Sum velocities
            m_lastAngularVelocity = m_angularMotorVelocity + vertattr; // + bank + deflection
            
            if ((m_flags & (VehicleFlag.NO_DEFLECTION_UP)) != 0)
            {
                m_lastAngularVelocity.X = 0;
                m_lastAngularVelocity.Y = 0;
                DetailLog("{0},MoveAngular,noDeflectionUp,lastAngular={1}", m_prim.LocalID, m_lastAngularVelocity);
            }

            if (m_lastAngularVelocity.ApproxEquals(Vector3.Zero, 0.01f))
            {
                m_lastAngularVelocity = Vector3.Zero; // Reduce small value to zero.
                DetailLog("{0},MoveAngular,zeroSmallValues,lastAngular={1}", m_prim.LocalID, m_lastAngularVelocity);
            }

             // apply friction
            Vector3 decayamount = Vector3.One / (m_angularFrictionTimescale / pTimestep);
            m_lastAngularVelocity -= m_lastAngularVelocity * decayamount;

            // Apply to the body
            m_prim.RotationalVelocity = m_lastAngularVelocity;

            DetailLog("{0},MoveAngular,done,decay={1},lastAngular={2}", m_prim.LocalID, decayamount, m_lastAngularVelocity);
        } //end MoveAngular

        internal void LimitRotation(float timestep)
        {
            Quaternion rotq = m_prim.Orientation;
            Quaternion m_rot = rotq;
            bool changed = false;
            if (m_RollreferenceFrame != Quaternion.Identity)
            {
                if (rotq.X >= m_RollreferenceFrame.X)
                {
                    m_rot.X = rotq.X - (m_RollreferenceFrame.X / 2);
                    changed = true;
                }
                if (rotq.Y >= m_RollreferenceFrame.Y)
                {
                    m_rot.Y = rotq.Y - (m_RollreferenceFrame.Y / 2);
                    changed = true;
                }
                if (rotq.X <= -m_RollreferenceFrame.X)
                {
                    m_rot.X = rotq.X + (m_RollreferenceFrame.X / 2);
                    changed = true;
                }
                if (rotq.Y <= -m_RollreferenceFrame.Y)
                {
                    m_rot.Y = rotq.Y + (m_RollreferenceFrame.Y / 2);
                    changed = true;
                }
                changed = true;
            }
            if ((m_flags & VehicleFlag.LOCK_ROTATION) != 0)
            {
                m_rot.X = 0;
                m_rot.Y = 0;
                changed = true;
            }
            if ((m_flags & VehicleFlag.LOCK_ROTATION) != 0)
            {
                m_rot.X = 0;
                m_rot.Y = 0;
                changed = true;
            }
            if (changed)
                m_prim.Orientation = m_rot;

            DetailLog("{0},LimitRotation,done,changed={1},orig={2},new={3}", m_prim.LocalID, changed, rotq, m_rot);
        }

        // Invoke the detailed logger and output something if it's enabled.
        private void DetailLog(string msg, params Object[] args)
        {
            if (m_prim.Scene.VehicleLoggingEnabled)
                m_prim.Scene.PhysicsLogging.Write(msg, args);
        }
    }
}
