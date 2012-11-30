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
 *

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
    public sealed class BSDynamics
    {
        private static string LogHeader = "[BULLETSIM VEHICLE]";

        private BSScene PhysicsScene { get; set; }
        // the prim this dynamic controller belongs to
        private BSPrim Prim { get; set; }

        // mass of the vehicle fetched each time we're calles
        private float m_vehicleMass;

        // Vehicle properties
        public Vehicle Type { get; set; }

        // private Quaternion m_referenceFrame = Quaternion.Identity;   // Axis modifier
        private VehicleFlag m_flags = (VehicleFlag) 0;                  // Boolean settings:
                                                                        // HOVER_TERRAIN_ONLY
                                                                        // HOVER_GLOBAL_HEIGHT
                                                                        // NO_DEFLECTION_UP
                                                                        // HOVER_WATER_ONLY
                                                                        // HOVER_UP_ONLY
                                                                        // LIMIT_MOTOR_UP
                                                                        // LIMIT_ROLL_ONLY
        private Vector3 m_BlockingEndPoint = Vector3.Zero;
        private Quaternion m_RollreferenceFrame = Quaternion.Identity;
        private Quaternion m_referenceFrame = Quaternion.Identity;

        // Linear properties
        private BSVMotor m_linearMotor = new BSVMotor("LinearMotor");
        private Vector3 m_linearMotorDirection = Vector3.Zero;          // velocity requested by LSL, decayed by time
        private Vector3 m_linearMotorOffset = Vector3.Zero;             // the point of force can be offset from the center
        private Vector3 m_linearMotorDirectionLASTSET = Vector3.Zero;   // velocity requested by LSL
        private Vector3 m_linearFrictionTimescale = Vector3.Zero;
        private float m_linearMotorDecayTimescale = 0;
        private float m_linearMotorTimescale = 0;
        private Vector3 m_lastLinearVelocityVector = Vector3.Zero;
        private Vector3 m_lastPositionVector = Vector3.Zero;
        // private bool m_LinearMotorSetLastFrame = false;
        // private Vector3 m_linearMotorOffset = Vector3.Zero;

        //Angular properties
        private BSVMotor m_angularMotor = new BSVMotor("AngularMotor");
        private Vector3 m_angularMotorDirection = Vector3.Zero;         // angular velocity requested by LSL motor
        // private int m_angularMotorApply = 0;                            // application frame counter
        private Vector3 m_angularMotorVelocity = Vector3.Zero;          // current angular motor velocity
        private float m_angularMotorTimescale = 0;                      // motor angular velocity ramp up rate
        private float m_angularMotorDecayTimescale = 0;                 // motor angular velocity decay rate
        private Vector3 m_angularFrictionTimescale = Vector3.Zero;      // body angular velocity  decay rate
        private Vector3 m_lastAngularVelocity = Vector3.Zero;           // what was last applied to body
        private Vector3 m_lastVertAttractor = Vector3.Zero;             // what VA was last applied to body

        //Deflection properties
        private float m_angularDeflectionEfficiency = 0;
        private float m_angularDeflectionTimescale = 0;
        private float m_linearDeflectionEfficiency = 0;
        private float m_linearDeflectionTimescale = 0;

        //Banking properties
        private float m_bankingEfficiency = 0;
        private float m_bankingMix = 0;
        private float m_bankingTimescale = 0;

        //Hover and Buoyancy properties
        private float m_VhoverHeight = 0f;
        private float m_VhoverEfficiency = 0f;
        private float m_VhoverTimescale = 0f;
        private float m_VhoverTargetHeight = -1.0f;     // if <0 then no hover, else its the current target height
        private float m_VehicleBuoyancy = 0f;           //KF: m_VehicleBuoyancy is set by VEHICLE_BUOYANCY for a vehicle.
                    // Modifies gravity. Slider between -1 (double-gravity) and 1 (full anti-gravity)
                    // KF: So far I have found no good method to combine a script-requested .Z velocity and gravity.
                    // Therefore only m_VehicleBuoyancy=1 (0g) will use the script-requested .Z velocity.

        //Attractor properties
        private BSVMotor m_verticalAttractionMotor = new BSVMotor("VerticalAttraction");
        private float m_verticalAttractionEfficiency = 1.0f;        // damped
        private float m_verticalAttractionTimescale = 500f;         // Timescale > 300  means no vert attractor.

        public BSDynamics(BSScene myScene, BSPrim myPrim)
        {
            PhysicsScene = myScene;
            Prim = myPrim;
            Type = Vehicle.TYPE_NONE;
        }

        // Return 'true' if this vehicle is doing vehicle things
        public bool IsActive
        {
            get { return Type != Vehicle.TYPE_NONE; }
        }

        internal void ProcessFloatVehicleParam(Vehicle pParam, float pValue)
        {
            VDetailLog("{0},ProcessFloatVehicleParam,param={1},val={2}", Prim.LocalID, pParam, pValue);
            switch (pParam)
            {
                case Vehicle.ANGULAR_DEFLECTION_EFFICIENCY:
                    m_angularDeflectionEfficiency = Math.Max(pValue, 0.01f);
                    break;
                case Vehicle.ANGULAR_DEFLECTION_TIMESCALE:
                    m_angularDeflectionTimescale = Math.Max(pValue, 0.01f);
                    break;
                case Vehicle.ANGULAR_MOTOR_DECAY_TIMESCALE:
                    m_angularMotorDecayTimescale = Math.Max(0.01f, Math.Min(pValue,120));
                    m_angularMotor.TargetValueDecayTimeScale = m_angularMotorDecayTimescale;
                    break;
                case Vehicle.ANGULAR_MOTOR_TIMESCALE:
                    m_angularMotorTimescale = Math.Max(pValue, 0.01f);
                    m_angularMotor.TimeScale = m_angularMotorTimescale;
                    break;
                case Vehicle.BANKING_EFFICIENCY:
                    m_bankingEfficiency = Math.Max(-1f, Math.Min(pValue, 1f));
                    break;
                case Vehicle.BANKING_MIX:
                    m_bankingMix = Math.Max(pValue, 0.01f);
                    break;
                case Vehicle.BANKING_TIMESCALE:
                    m_bankingTimescale = Math.Max(pValue, 0.01f);
                    break;
                case Vehicle.BUOYANCY:
                    m_VehicleBuoyancy = Math.Max(-1f, Math.Min(pValue, 1f));
                    break;
                case Vehicle.HOVER_EFFICIENCY:
                    m_VhoverEfficiency = Math.Max(0f, Math.Min(pValue, 1f));
                    break;
                case Vehicle.HOVER_HEIGHT:
                    m_VhoverHeight = pValue;
                    break;
                case Vehicle.HOVER_TIMESCALE:
                    m_VhoverTimescale = Math.Max(pValue, 0.01f);
                    break;
                case Vehicle.LINEAR_DEFLECTION_EFFICIENCY:
                    m_linearDeflectionEfficiency = Math.Max(pValue, 0.01f);
                    break;
                case Vehicle.LINEAR_DEFLECTION_TIMESCALE:
                    m_linearDeflectionTimescale = Math.Max(pValue, 0.01f);
                    break;
                case Vehicle.LINEAR_MOTOR_DECAY_TIMESCALE:
                    m_linearMotorDecayTimescale = Math.Max(0.01f, Math.Min(pValue,120));
                    m_linearMotor.TargetValueDecayTimeScale = m_linearMotorDecayTimescale;
                    break;
                case Vehicle.LINEAR_MOTOR_TIMESCALE:
                    m_linearMotorTimescale = Math.Max(pValue, 0.01f);
                    m_linearMotor.TimeScale = m_linearMotorTimescale;
                    break;
                case Vehicle.VERTICAL_ATTRACTION_EFFICIENCY:
                    m_verticalAttractionEfficiency = Math.Max(0.1f, Math.Min(pValue, 1f));
                    m_verticalAttractionMotor.Efficiency = m_verticalAttractionEfficiency;
                    break;
                case Vehicle.VERTICAL_ATTRACTION_TIMESCALE:
                    m_verticalAttractionTimescale = Math.Max(pValue, 0.01f);
                    m_verticalAttractionMotor.TimeScale = m_verticalAttractionTimescale;
                    break;

                // These are vector properties but the engine lets you use a single float value to
                // set all of the components to the same value
                case Vehicle.ANGULAR_FRICTION_TIMESCALE:
                    m_angularFrictionTimescale = new Vector3(pValue, pValue, pValue);
                    m_angularMotor.FrictionTimescale = m_angularFrictionTimescale;
                    break;
                case Vehicle.ANGULAR_MOTOR_DIRECTION:
                    m_angularMotorDirection = new Vector3(pValue, pValue, pValue);
                    m_angularMotor.SetTarget(m_angularMotorDirection);
                    break;
                case Vehicle.LINEAR_FRICTION_TIMESCALE:
                    m_linearFrictionTimescale = new Vector3(pValue, pValue, pValue);
                    m_linearMotor.FrictionTimescale = m_linearFrictionTimescale;
                    break;
                case Vehicle.LINEAR_MOTOR_DIRECTION:
                    m_linearMotorDirection = new Vector3(pValue, pValue, pValue);
                    m_linearMotorDirectionLASTSET = new Vector3(pValue, pValue, pValue);
                    m_linearMotor.SetTarget(m_linearMotorDirection);
                    break;
                case Vehicle.LINEAR_MOTOR_OFFSET:
                    m_linearMotorOffset = new Vector3(pValue, pValue, pValue);
                    break;

            }
        }//end ProcessFloatVehicleParam

        internal void ProcessVectorVehicleParam(Vehicle pParam, Vector3 pValue)
        {
            VDetailLog("{0},ProcessVectorVehicleParam,param={1},val={2}", Prim.LocalID, pParam, pValue);
            switch (pParam)
            {
                case Vehicle.ANGULAR_FRICTION_TIMESCALE:
                    m_angularFrictionTimescale = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    m_angularMotor.FrictionTimescale = m_angularFrictionTimescale;
                    break;
                case Vehicle.ANGULAR_MOTOR_DIRECTION:
                    // Limit requested angular speed to 2 rps= 4 pi rads/sec
                    pValue.X = Math.Max(-12.56f, Math.Min(pValue.X, 12.56f));
                    pValue.Y = Math.Max(-12.56f, Math.Min(pValue.Y, 12.56f));
                    pValue.Z = Math.Max(-12.56f, Math.Min(pValue.Z, 12.56f));
                    m_angularMotorDirection = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    m_angularMotor.SetTarget(m_angularMotorDirection);
                    break;
                case Vehicle.LINEAR_FRICTION_TIMESCALE:
                    m_linearFrictionTimescale = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    m_linearMotor.FrictionTimescale = m_linearFrictionTimescale;
                    break;
                case Vehicle.LINEAR_MOTOR_DIRECTION:
                    m_linearMotorDirection = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    m_linearMotorDirectionLASTSET = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    m_linearMotor.SetTarget(m_linearMotorDirection);
                    break;
                case Vehicle.LINEAR_MOTOR_OFFSET:
                    m_linearMotorOffset = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
                case Vehicle.BLOCK_EXIT:
                    m_BlockingEndPoint = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
            }
        }//end ProcessVectorVehicleParam

        internal void ProcessRotationVehicleParam(Vehicle pParam, Quaternion pValue)
        {
            VDetailLog("{0},ProcessRotationalVehicleParam,param={1},val={2}", Prim.LocalID, pParam, pValue);
            switch (pParam)
            {
                case Vehicle.REFERENCE_FRAME:
                    m_referenceFrame = pValue;
                    break;
                case Vehicle.ROLL_FRAME:
                    m_RollreferenceFrame = pValue;
                    break;
            }
        }//end ProcessRotationVehicleParam

        internal void ProcessVehicleFlags(int pParam, bool remove)
        {
            VDetailLog("{0},ProcessVehicleFlags,param={1},remove={2}", Prim.LocalID, pParam, remove);
            VehicleFlag parm = (VehicleFlag)pParam;
            if (pParam == -1)
                m_flags = (VehicleFlag)0;
            else
            {
                if (remove)
                    m_flags &= ~parm;
                else
                    m_flags |= parm;
            }
        }

        internal void ProcessTypeChange(Vehicle pType)
        {
            VDetailLog("{0},ProcessTypeChange,type={1}", Prim.LocalID, pType);
            // Set Defaults For Type
            Type = pType;
            switch (pType)
            {
                case Vehicle.TYPE_NONE:
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 0;
                    m_linearMotorDecayTimescale = 0;
                    m_linearFrictionTimescale = new Vector3(0, 0, 0);

                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorDecayTimescale = 0;
                    m_angularMotorTimescale = 0;
                    m_angularFrictionTimescale = new Vector3(0, 0, 0);

                    m_VhoverHeight = 0;
                    m_VhoverEfficiency = 0;
                    m_VhoverTimescale = 0;
                    m_VehicleBuoyancy = 0;

                    m_linearDeflectionEfficiency = 1;
                    m_linearDeflectionTimescale = 1;

                    m_angularDeflectionEfficiency = 0;
                    m_angularDeflectionTimescale = 1000;

                    m_verticalAttractionEfficiency = 0;
                    m_verticalAttractionTimescale = 0;

                    m_bankingEfficiency = 0;
                    m_bankingTimescale = 1000;
                    m_bankingMix = 1;

                    m_referenceFrame = Quaternion.Identity;
                    m_flags = (VehicleFlag)0;

                    break;

                case Vehicle.TYPE_SLED:
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 1000;
                    m_linearMotorDecayTimescale = 120;
                    m_linearFrictionTimescale = new Vector3(30, 1, 1000);

                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 1000;
                    m_angularMotorDecayTimescale = 120;
                    m_angularFrictionTimescale = new Vector3(1000, 1000, 1000);

                    m_VhoverHeight = 0;
                    m_VhoverEfficiency = 10;    // TODO: this looks wrong!!
                    m_VhoverTimescale = 10;
                    m_VehicleBuoyancy = 0;

                    m_linearDeflectionEfficiency = 1;
                    m_linearDeflectionTimescale = 1;

                    m_angularDeflectionEfficiency = 1;
                    m_angularDeflectionTimescale = 1000;

                    m_verticalAttractionEfficiency = 0;
                    m_verticalAttractionTimescale = 0;

                    m_bankingEfficiency = 0;
                    m_bankingTimescale = 10;
                    m_bankingMix = 1;

                    m_referenceFrame = Quaternion.Identity;
                    m_flags &= ~(VehicleFlag.HOVER_WATER_ONLY
                                | VehicleFlag.HOVER_TERRAIN_ONLY
                                | VehicleFlag.HOVER_GLOBAL_HEIGHT
                                | VehicleFlag.HOVER_UP_ONLY);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP
                            | VehicleFlag.LIMIT_ROLL_ONLY
                            | VehicleFlag.LIMIT_MOTOR_UP);
                    break;
                case Vehicle.TYPE_CAR:
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 1;
                    m_linearMotorDecayTimescale = 60;
                    m_linearFrictionTimescale = new Vector3(100, 2, 1000);

                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 1;
                    m_angularMotorDecayTimescale = 0.8f;
                    m_angularFrictionTimescale = new Vector3(1000, 1000, 1000);

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

                    m_referenceFrame = Quaternion.Identity;
                    m_flags &= ~(VehicleFlag.HOVER_WATER_ONLY
                                | VehicleFlag.HOVER_TERRAIN_ONLY
                                | VehicleFlag.HOVER_GLOBAL_HEIGHT);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP
                                | VehicleFlag.LIMIT_ROLL_ONLY
                                | VehicleFlag.LIMIT_MOTOR_UP
                                | VehicleFlag.HOVER_UP_ONLY);
                    break;
                case Vehicle.TYPE_BOAT:
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 5;
                    m_linearMotorDecayTimescale = 60;
                    m_linearFrictionTimescale = new Vector3(10, 3, 2);

                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 4;
                    m_angularMotorDecayTimescale = 4;
                    m_angularFrictionTimescale = new Vector3(10,10,10);

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

                    m_referenceFrame = Quaternion.Identity;
                    m_flags &= ~(VehicleFlag.HOVER_TERRAIN_ONLY
                                    | VehicleFlag.HOVER_GLOBAL_HEIGHT
                                    | VehicleFlag.LIMIT_ROLL_ONLY
                                    | VehicleFlag.LIMIT_MOTOR_UP
                                    | VehicleFlag.HOVER_UP_ONLY);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP
                                    | VehicleFlag.HOVER_WATER_ONLY);
                    break;
                case Vehicle.TYPE_AIRPLANE:
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 2;
                    m_linearMotorDecayTimescale = 60;
                    m_linearFrictionTimescale = new Vector3(200, 10, 5);

                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 4;
                    m_angularMotorDecayTimescale = 4;
                    m_angularFrictionTimescale = new Vector3(20, 20, 20);

                    m_VhoverHeight = 0;
                    m_VhoverEfficiency = 0.5f;
                    m_VhoverTimescale = 1000;
                    m_VehicleBuoyancy = 0;

                    m_linearDeflectionEfficiency = 0.5f;
                    m_linearDeflectionTimescale = 3;

                    m_angularDeflectionEfficiency = 1;
                    m_angularDeflectionTimescale = 2;

                    m_verticalAttractionEfficiency = 0.9f;
                    m_verticalAttractionTimescale = 2f;

                    m_bankingEfficiency = 1;
                    m_bankingMix = 0.7f;
                    m_bankingTimescale = 2;

                    m_referenceFrame = Quaternion.Identity;
                    m_flags &= ~(VehicleFlag.HOVER_WATER_ONLY
                                    | VehicleFlag.HOVER_TERRAIN_ONLY
                                    | VehicleFlag.HOVER_GLOBAL_HEIGHT
                                    | VehicleFlag.HOVER_UP_ONLY
                                    | VehicleFlag.NO_DEFLECTION_UP
                                    | VehicleFlag.LIMIT_MOTOR_UP);
                    m_flags |= (VehicleFlag.LIMIT_ROLL_ONLY);
                    break;
                case Vehicle.TYPE_BALLOON:
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 5;
                    m_linearFrictionTimescale = new Vector3(5, 5, 5);
                    m_linearMotorDecayTimescale = 60;

                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 6;
                    m_angularFrictionTimescale = new Vector3(10, 10, 10);
                    m_angularMotorDecayTimescale = 10;

                    m_VhoverHeight = 5;
                    m_VhoverEfficiency = 0.8f;
                    m_VhoverTimescale = 10;
                    m_VehicleBuoyancy = 1;

                    m_linearDeflectionEfficiency = 0;
                    m_linearDeflectionTimescale = 5;

                    m_angularDeflectionEfficiency = 0;
                    m_angularDeflectionTimescale = 5;

                    m_verticalAttractionEfficiency = 1f;
                    m_verticalAttractionTimescale = 100f;

                    m_bankingEfficiency = 0;
                    m_bankingMix = 0.7f;
                    m_bankingTimescale = 5;
                    m_referenceFrame = Quaternion.Identity;

                    m_referenceFrame = Quaternion.Identity;
                    m_flags &= ~(VehicleFlag.HOVER_WATER_ONLY
                                    | VehicleFlag.HOVER_TERRAIN_ONLY
                                    | VehicleFlag.HOVER_UP_ONLY
                                    | VehicleFlag.NO_DEFLECTION_UP
                                    | VehicleFlag.LIMIT_MOTOR_UP);
                    m_flags |= (VehicleFlag.LIMIT_ROLL_ONLY
                                    | VehicleFlag.HOVER_GLOBAL_HEIGHT);
                    break;
            }

            // Update any physical parameters based on this type.
            Refresh();

            m_linearMotor = new BSVMotor("LinearMotor", m_linearMotorTimescale,
                                m_linearMotorDecayTimescale, m_linearFrictionTimescale,
                                1f);
            m_linearMotor.PhysicsScene = PhysicsScene;  // DEBUG DEBUG DEBUG (enables detail logging)

            m_angularMotor = new BSVMotor("AngularMotor", m_angularMotorTimescale,
                                m_angularMotorDecayTimescale, m_angularFrictionTimescale,
                                1f);
            m_angularMotor.PhysicsScene = PhysicsScene;  // DEBUG DEBUG DEBUG (enables detail logging)

            m_verticalAttractionMotor = new BSVMotor("VerticalAttraction", m_verticalAttractionTimescale,
                                BSMotor.Infinite, BSMotor.InfiniteVector,
                                m_verticalAttractionEfficiency);
            // Z goes away and we keep X and Y
            m_verticalAttractionMotor.FrictionTimescale = new Vector3(BSMotor.Infinite, BSMotor.Infinite, 0.1f);
            m_verticalAttractionMotor.PhysicsScene = PhysicsScene;  // DEBUG DEBUG DEBUG (enables detail logging)

            // m_bankingMotor = new BSVMotor("BankingMotor", ...);
        }

        // Some of the properties of this prim may have changed.
        // Do any updating needed for a vehicle
        public void Refresh()
        {
            if (IsActive)
            {
                m_vehicleMass = Prim.Linkset.LinksetMass;

                // Friction effects are handled by this vehicle code
                float friction = 0f;
                BulletSimAPI.SetFriction2(Prim.PhysBody.ptr, friction);

                // Moderate angular movement introduced by Bullet.
                // TODO: possibly set AngularFactor and LinearFactor for the type of vehicle.
                //     Maybe compute linear and angular factor and damping from params.
                float angularDamping = PhysicsScene.Params.vehicleAngularDamping;
                BulletSimAPI.SetAngularDamping2(Prim.PhysBody.ptr, angularDamping);

                // DEBUG DEBUG DEBUG: use uniform inertia to smooth movement added by Bullet
                // Vector3 localInertia = new Vector3(1f, 1f, 1f);
                Vector3 localInertia = new Vector3(m_vehicleMass, m_vehicleMass, m_vehicleMass);
                BulletSimAPI.SetMassProps2(Prim.PhysBody.ptr, m_vehicleMass, localInertia);

                VDetailLog("{0},BSDynamics.Refresh,frict={1},inert={2},aDamp={3}",
                                Prim.LocalID, friction, localInertia, angularDamping);
            }
        }

        public bool RemoveBodyDependencies(BSPhysObject prim)
        {
            // If active, we need to add our properties back when the body is rebuilt.
            return IsActive;
        }

        public void RestoreBodyDependencies(BSPhysObject prim)
        {
            if (Prim.LocalID != prim.LocalID)
            {
                // The call should be on us by our prim. Error if not.
                PhysicsScene.Logger.ErrorFormat("{0} RestoreBodyDependencies: called by not my prim. passedLocalID={1}, vehiclePrimLocalID={2}",
                                LogHeader, prim.LocalID, Prim.LocalID);
                return;
            }
            Refresh();
        }

        // One step of the vehicle properties for the next 'pTimestep' seconds.
        internal void Step(float pTimestep)
        {
            if (!IsActive) return;

            MoveLinear(pTimestep);
            MoveAngular(pTimestep);

            LimitRotation(pTimestep);

            // remember the position so next step we can limit absolute movement effects
            m_lastPositionVector = Prim.ForcePosition;

            VDetailLog("{0},BSDynamics.Step,done,pos={1},force={2},velocity={3},angvel={4}",
                    Prim.LocalID, Prim.ForcePosition, Prim.Force, Prim.ForceVelocity, Prim.RotationalVelocity);
        }

        // Apply the effect of the linear motor.
        // Also does hover and float.
        private void MoveLinear(float pTimestep)
        {
            Vector3 linearMotorContribution = m_linearMotor.Step(pTimestep);

            // Rotate new object velocity from vehicle relative to world coordinates
            linearMotorContribution *= Prim.ForceOrientation;

            // ==================================================================
            // Gravity and Buoyancy
            // There is some gravity, make a gravity force vector that is applied after object velocity.
            // m_VehicleBuoyancy: -1=2g; 0=1g; 1=0g;
            Vector3 grav = Prim.PhysicsScene.DefaultGravity * (1f - m_VehicleBuoyancy);

            Vector3 pos = Prim.ForcePosition;
            float terrainHeight = Prim.PhysicsScene.TerrainManager.GetTerrainHeightAtXYZ(pos);

            Vector3 terrainHeightContribution = ComputeLinearTerrainHeightCorrection(pTimestep, ref pos, terrainHeight);

            Vector3 hoverContribution = ComputeLinearHover(pTimestep, ref pos, terrainHeight);

            ComputeLinearBlockingEndPoint(pTimestep, ref pos);

            Vector3 limitMotorUpContribution = ComputeLinearMotorUp(pTimestep, pos, terrainHeight);

            // ==================================================================
            Vector3 newVelocity = linearMotorContribution
                            + terrainHeightContribution
                            + hoverContribution
                            + limitMotorUpContribution;

            // If not changing some axis, reduce out velocity
            if ((m_flags & (VehicleFlag.NO_X)) != 0)
                newVelocity.X = 0;
            if ((m_flags & (VehicleFlag.NO_Y)) != 0)
                newVelocity.Y = 0;
            if ((m_flags & (VehicleFlag.NO_Z)) != 0)
                newVelocity.Z = 0;

            // ==================================================================
            // Clamp REALLY high or low velocities
            float newVelocityLengthSq = newVelocity.LengthSquared();
            if (newVelocityLengthSq > 1e6f)
            {
                newVelocity /= newVelocity.Length();
                newVelocity *= 1000f;
            }
            else if (newVelocityLengthSq < 1e-6f)
                newVelocity = Vector3.Zero;

            // ==================================================================
            // Stuff new linear velocity into the vehicle
            Prim.ForceVelocity = newVelocity;
            // Prim.ApplyForceImpulse((m_newVelocity - Prim.Velocity) * m_vehicleMass, false);    // DEBUG DEBUG

            // Other linear forces are applied as forces.
            Vector3 totalDownForce = grav * m_vehicleMass;
            if (totalDownForce != Vector3.Zero)
            {
                Prim.AddForce(totalDownForce, false);
            }

            VDetailLog("{0},MoveLinear,done,lmDir={1},lmVel={2},newVel={3},primVel={4},totalDown={5}",
                                Prim.LocalID, m_linearMotorDirection, m_lastLinearVelocityVector,
                                newVelocity, Prim.Velocity, totalDownForce);

        } // end MoveLinear()

        public Vector3 ComputeLinearTerrainHeightCorrection(float pTimestep, ref Vector3 pos, float terrainHeight)
        {
            Vector3 ret = Vector3.Zero;
            // If below the terrain, move us above the ground a little.
            // Taking the rotated size doesn't work here because m_prim.Size is the size of the root prim and not the linkset.
            //     TODO: Add a m_prim.LinkSet.Size similar to m_prim.LinkSet.Mass.
            // Vector3 rotatedSize = m_prim.Size * m_prim.ForceOrientation;
            // if (rotatedSize.Z < terrainHeight)
            if (pos.Z < terrainHeight)
            {
                // TODO: correct position by applying force rather than forcing position.
                pos.Z = terrainHeight + 2;
                Prim.ForcePosition = pos;
                VDetailLog("{0},MoveLinear,terrainHeight,terrainHeight={1},pos={2}", Prim.LocalID, terrainHeight, pos);
            }
            return ret;
        }

        public Vector3 ComputeLinearHover(float pTimestep, ref Vector3 pos, float terrainHeight)
        {
            Vector3 ret = Vector3.Zero;

            // m_VhoverEfficiency: 0=bouncy, 1=totally damped
            // m_VhoverTimescale: time to achieve height
            if ((m_flags & (VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY | VehicleFlag.HOVER_GLOBAL_HEIGHT)) != 0)
            {
                // We should hover, get the target height
                if ((m_flags & VehicleFlag.HOVER_WATER_ONLY) != 0)
                {
                    m_VhoverTargetHeight = Prim.PhysicsScene.TerrainManager.GetWaterLevelAtXYZ(pos) + m_VhoverHeight;
                }
                if ((m_flags & VehicleFlag.HOVER_TERRAIN_ONLY) != 0)
                {
                    m_VhoverTargetHeight = terrainHeight + m_VhoverHeight;
                }
                if ((m_flags & VehicleFlag.HOVER_GLOBAL_HEIGHT) != 0)
                {
                    m_VhoverTargetHeight = m_VhoverHeight;
                }

                if ((m_flags & VehicleFlag.HOVER_UP_ONLY) != 0)
                {
                    // If body is already heigher, use its height as target height
                    if (pos.Z > m_VhoverTargetHeight)
                        m_VhoverTargetHeight = pos.Z;
                }
                
                if ((m_flags & VehicleFlag.LOCK_HOVER_HEIGHT) != 0)
                {
                    if (Math.Abs(pos.Z - m_VhoverTargetHeight) > 0.2f)
                    {
                        pos.Z = m_VhoverTargetHeight;
                        Prim.ForcePosition = pos;
                    }
                }
                else
                {
                    float verticalError = pos.Z - m_VhoverTargetHeight;
                    float verticalCorrectionVelocity = pTimestep * (verticalError / m_VhoverTimescale);

                    // TODO: implement m_VhoverEfficiency
                    if (verticalError > 0.01f)
                    {
                        // If error is positive (we're above the target height), push down
                        ret = new Vector3(0f, 0f, -verticalCorrectionVelocity);
                    }
                    else if (verticalError < -0.01)
                    {
                        ret = new Vector3(0f, 0f, verticalCorrectionVelocity);
                    }
                }

                VDetailLog("{0},MoveLinear,hover,pos={1},ret={2},hoverTS={3},height={4},target={5}",
                                Prim.LocalID, pos, ret, m_VhoverTimescale, m_VhoverHeight, m_VhoverTargetHeight);
            }

            return ret;
        }

        public bool ComputeLinearBlockingEndPoint(float pTimestep, ref Vector3 pos)
        {
            bool changed = false;

            Vector3 posChange = pos - m_lastPositionVector;
            if (m_BlockingEndPoint != Vector3.Zero)
            {
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
                    Prim.ForcePosition = pos;
                    VDetailLog("{0},MoveLinear,blockingEndPoint,block={1},origPos={2},pos={3}",
                                Prim.LocalID, m_BlockingEndPoint, posChange, pos);
                }
            }
            return changed;
        }

        // From http://wiki.secondlife.com/wiki/LlSetVehicleFlags :
        //    Prevent ground vehicles from motoring into the sky.This flag has a subtle effect when
        //    used with conjunction with banking: the strength of the banking will decay when the
        //    vehicle no longer experiences collisions. The decay timescale is the same as
        //    VEHICLE_BANKING_TIMESCALE. This is to help prevent ground vehicles from steering
        //    when they are in mid jump. 
        // TODO: this code is wrong. Also, what should it do for boats?
        public Vector3 ComputeLinearMotorUp(float pTimestep, Vector3 pos, float terrainHeight)
        {
            Vector3 ret = Vector3.Zero;
            if ((m_flags & (VehicleFlag.LIMIT_MOTOR_UP)) != 0)
            {
                // If the vehicle is motoring into the sky, get it going back down.
                float distanceAboveGround = pos.Z - terrainHeight;
                if (distanceAboveGround > 1f)
                {
                    // downForce = new Vector3(0, 0, (-distanceAboveGround / m_bankingTimescale) * pTimestep);
                    // downForce = new Vector3(0, 0, -distanceAboveGround / m_bankingTimescale);
                    ret = new Vector3(0, 0, -distanceAboveGround);
                }
                // TODO: this calculation is wrong. From the description at
                //     (http://wiki.secondlife.com/wiki/Category:LSL_Vehicle), the downForce
                //     has a decay factor. This says this force should
                //     be computed with a motor.
                // TODO: add interaction with banking.
                VDetailLog("{0},MoveLinear,limitMotorUp,distAbove={1},downForce={2}",
                                    Prim.LocalID, distanceAboveGround, ret);
            }
            return ret;
        }

        // =======================================================================
        // =======================================================================
        // Apply the effect of the angular motor.
        private void MoveAngular(float pTimestep)
        {
            // m_angularMotorDirection         // angular velocity requested by LSL motor
            // m_angularMotorVelocity          // current angular motor velocity (ramps up and down)
            // m_angularMotorTimescale         // motor angular velocity ramp up time
            // m_angularMotorDecayTimescale    // motor angular velocity decay rate
            // m_angularFrictionTimescale      // body angular velocity  decay rate
            // m_lastAngularVelocity           // what was last applied to body

            /*
            if (m_angularMotorDirection.LengthSquared() > 0.0001)
            {
                Vector3 origVel = m_angularMotorVelocity;
                Vector3 origDir = m_angularMotorDirection;

                //       new velocity    +=                      error                       /  (  time to get there   / step interval)
                //                           requested direction   - current vehicle direction
                m_angularMotorVelocity += (m_angularMotorDirection - m_angularMotorVelocity) /  (m_angularMotorTimescale / pTimestep);
                // decay requested direction
                m_angularMotorDirection *= (1.0f - (pTimestep * 1.0f/m_angularMotorDecayTimescale));

                VDetailLog("{0},MoveAngular,angularMotorApply,angTScale={1},timeStep={2},origvel={3},origDir={4},vel={5}",
                        Prim.LocalID, m_angularMotorTimescale, pTimestep, origVel, origDir, m_angularMotorVelocity);
            }
            else
            {
                m_angularMotorVelocity = Vector3.Zero;
            }
            */

            Vector3 angularMotorContribution = m_angularMotor.Step(pTimestep);

            // ==================================================================
            // From http://wiki.secondlife.com/wiki/LlSetVehicleFlags :
            //    This flag prevents linear deflection parallel to world z-axis. This is useful
            //    for preventing ground vehicles with large linear deflection, like bumper cars,
            //    from climbing their linear deflection into the sky. 
            // That is, NO_DEFLECTION_UP says angular motion should not add any pitch or roll movement
            if ((m_flags & (VehicleFlag.NO_DEFLECTION_UP)) != 0)
            {
                angularMotorContribution.X = 0f;
                angularMotorContribution.Y = 0f;
                VDetailLog("{0},MoveAngular,noDeflectionUp,angularMotorContrib={1}", Prim.LocalID, angularMotorContribution);
            }

            Vector3 verticalAttractionContribution = ComputeAngularVerticalAttraction(pTimestep);

            Vector3 deflectionContribution = ComputeAngularDeflection(pTimestep);

            Vector3 bankingContribution = ComputeAngularBanking(pTimestep);

            // ==================================================================
            m_lastVertAttractor = verticalAttractionContribution;

            // Sum velocities
            m_lastAngularVelocity = angularMotorContribution
                                    + verticalAttractionContribution
                                    + deflectionContribution
                                    + bankingContribution;

            // ==================================================================
            //Offset section
            if (m_linearMotorOffset != Vector3.Zero)
            {
                //Offset of linear velocity doesn't change the linear velocity,
                //   but causes a torque to be applied, for example...
                //
                //      IIIII     >>>   IIIII
                //      IIIII     >>>    IIIII
                //      IIIII     >>>     IIIII
                //          ^
                //          |  Applying a force at the arrow will cause the object to move forward, but also rotate
                //
                //
                // The torque created is the linear velocity crossed with the offset

                // TODO: this computation should be in the linear section
                //    because that is where we know the impulse being applied.
                Vector3 torqueFromOffset = Vector3.Zero;
                // torqueFromOffset = Vector3.Cross(m_linearMotorOffset, appliedImpulse);
                if (float.IsNaN(torqueFromOffset.X))
                    torqueFromOffset.X = 0;
                if (float.IsNaN(torqueFromOffset.Y))
                    torqueFromOffset.Y = 0;
                if (float.IsNaN(torqueFromOffset.Z))
                    torqueFromOffset.Z = 0;
                torqueFromOffset *= m_vehicleMass;
                Prim.ApplyTorqueImpulse(torqueFromOffset, true);
                VDetailLog("{0},BSDynamic.MoveAngular,motorOffset,applyTorqueImpulse={1}", Prim.LocalID, torqueFromOffset);
            }

            // ==================================================================
            if (m_lastAngularVelocity.ApproxEquals(Vector3.Zero, 0.01f))
            {
                m_lastAngularVelocity = Vector3.Zero; // Reduce small value to zero.
                // TODO: zeroing is good but it also sets values in unmanaged code. Remove the stores when idle.
                VDetailLog("{0},MoveAngular,zeroAngularMotion,lastAngular={1}", Prim.LocalID, m_lastAngularVelocity);
                Prim.ZeroAngularMotion(true);
            }
            else
            {
                // Apply to the body.
                // The above calculates the absolute angular velocity needed. Angular velocity is massless.
                // Since we are stuffing the angular velocity directly into the object, the computed
                //     velocity needs to be scaled by the timestep.
                // Also remove any motion that is on the object so added motion is only from vehicle.
                Vector3 applyAngularForce = ((m_lastAngularVelocity * pTimestep)
                                                - Prim.ForceRotationalVelocity);
                // Unscale the force by the angular factor so it overwhelmes the Bullet additions.
                Prim.ForceRotationalVelocity = applyAngularForce;

                VDetailLog("{0},MoveAngular,done,angMotor={1},vertAttr={2},bank={3},deflect={4},newAngForce={5},lastAngular={6}",
                                    Prim.LocalID,
                                    angularMotorContribution, verticalAttractionContribution,
                                    bankingContribution, deflectionContribution,
                                    applyAngularForce, m_lastAngularVelocity
                                    );
            }
        }

        public Vector3 ComputeAngularVerticalAttraction(float pTimestep)
        {
            Vector3 ret = Vector3.Zero;

            // If vertical attaction timescale is reasonable and we applied an angular force last time...
            if (m_verticalAttractionTimescale < 500)
            {
                Vector3 verticalError = Vector3.UnitZ * Prim.ForceOrientation;
                verticalError.Normalize();
                m_verticalAttractionMotor.SetCurrent(verticalError);
                m_verticalAttractionMotor.SetTarget(Vector3.UnitZ);
                ret = m_verticalAttractionMotor.Step(pTimestep);
                /*
                // Take a vector pointing up and convert it from world to vehicle relative coords.
                Vector3 verticalError = Vector3.UnitZ * Prim.ForceOrientation;
                verticalError.Normalize();

                // If vertical attraction correction is needed, the vector that was pointing up (UnitZ)
                //    is now leaning to one side (rotated around the X axis) and the Y value will
                //    go from zero (nearly straight up) to one (completely to the side) or leaning
                //    front-to-back (rotated around the Y axis) and the value of X will be between
                //    zero and one.
                // The value of Z is how far the rotation is off with 1 meaning none and 0 being 90 degrees.

                // If verticalError.Z is negative, the vehicle is upside down. Add additional push.
                if (verticalError.Z < 0f)
                {
                    verticalError.X = 2f - verticalError.X;
                    verticalError.Y = 2f - verticalError.Y;
                }

                // Y error means needed rotation around X axis and visa versa.
                verticalAttractionContribution.X =    verticalError.Y;
                verticalAttractionContribution.Y =  - verticalError.X;
                verticalAttractionContribution.Z = 0f;

                // scale by the time scale and timestep
                Vector3 unscaledContrib = verticalAttractionContribution;
                verticalAttractionContribution /= m_verticalAttractionTimescale;
                verticalAttractionContribution *= pTimestep;

                // apply efficiency
                Vector3 preEfficiencyContrib = verticalAttractionContribution;
                float efficencySquared = m_verticalAttractionEfficiency * m_verticalAttractionEfficiency;
                verticalAttractionContribution *= (m_verticalAttractionEfficiency * m_verticalAttractionEfficiency);

                VDetailLog("{0},MoveAngular,verticalAttraction,,verticalError={1},unscaled={2},preEff={3},eff={4},effSq={5},vertAttr={6}",
                                            Prim.LocalID, verticalError, unscaledContrib, preEfficiencyContrib,
                                            m_verticalAttractionEfficiency, efficencySquared,
                                            verticalAttractionContribution);
                 */

            }
            return ret;
        }

        public Vector3 ComputeAngularDeflection(float pTimestep)
        {
            Vector3 ret = Vector3.Zero;

            if (m_angularDeflectionEfficiency != 0)
            {
                // Compute a scaled vector that points in the preferred axis (X direction)
                Vector3 scaledDefaultDirection =
                    new Vector3((pTimestep * 10 * (m_angularDeflectionEfficiency / m_angularDeflectionTimescale)), 0, 0);
                // Adding the current vehicle orientation and reference frame displaces the orientation to the frame.
                // Rotate the scaled default axix relative to the actual vehicle direction giving where it should point.
                Vector3 preferredAxisOfMotion = scaledDefaultDirection * Quaternion.Add(Prim.ForceOrientation, m_referenceFrame);

                // Scale by efficiency and timescale
                ret = (preferredAxisOfMotion * (m_angularDeflectionEfficiency) / m_angularDeflectionTimescale) * pTimestep;

                VDetailLog("{0},MoveAngular,Deflection,perfAxis={1},deflection={2}", Prim.LocalID, preferredAxisOfMotion, ret);

                // This deflection computation is not correct.
                ret = Vector3.Zero;
            }
            return ret;
        }

        public Vector3 ComputeAngularBanking(float pTimestep)
        {
            Vector3 ret = Vector3.Zero;

            if (m_bankingEfficiency != 0)
            {
                Vector3 dir = Vector3.One * Prim.ForceOrientation;
                float mult = (m_bankingMix * m_bankingMix) * -1 * (m_bankingMix < 0 ? -1 : 1);
                //Changes which way it banks in and out of turns

                //Use the square of the efficiency, as it looks much more how SL banking works
                float effSquared = (m_bankingEfficiency * m_bankingEfficiency);
                if (m_bankingEfficiency < 0)
                    effSquared *= -1; //Keep the negative!

                float mix = Math.Abs(m_bankingMix);
                if (m_angularMotorVelocity.X == 0)
                {
                    // The vehicle is stopped
                    /*if (!parent.Orientation.ApproxEquals(this.m_referenceFrame, 0.25f))
                    {
                        Vector3 axisAngle;
                        float angle;
                        parent.Orientation.GetAxisAngle(out axisAngle, out angle);
                        Vector3 rotatedVel = parent.Velocity * parent.Orientation;
                        if ((rotatedVel.X < 0 && axisAngle.Y > 0) || (rotatedVel.X > 0 && axisAngle.Y < 0))
                            m_angularMotorVelocity.X += (effSquared * (mult * mix)) * (1f) * 10;
                        else
                            m_angularMotorVelocity.X += (effSquared * (mult * mix)) * (-1f) * 10;
                    }*/
                }
                else
                {
                    ret.Z += (effSquared * (mult * mix)) * (m_angularMotorVelocity.X) * 4;
                }

                //If they are colliding, we probably shouldn't shove the prim around... probably
                if (!Prim.IsColliding && Math.Abs(m_angularMotorVelocity.X) > mix)
                {
                    float angVelZ = m_angularMotorVelocity.X * -1;
                    /*if(angVelZ > mix)
                        angVelZ = mix;
                    else if(angVelZ < -mix)
                        angVelZ = -mix;*/
                    //This controls how fast and how far the banking occurs
                    Vector3 bankingRot = new Vector3(angVelZ * (effSquared * mult), 0, 0);
                    if (bankingRot.X > 3)
                        bankingRot.X = 3;
                    else if (bankingRot.X < -3)
                        bankingRot.X = -3;
                    bankingRot *= Prim.ForceOrientation;
                    ret += bankingRot;
                }
                m_angularMotorVelocity.X *= m_bankingEfficiency == 1 ? 0.0f : 1 - m_bankingEfficiency;
                VDetailLog("{0},MoveAngular,Banking,bEff={1},angMotVel={2},effSq={3},mult={4},mix={5},banking={6}",
                                Prim.LocalID, m_bankingEfficiency, m_angularMotorVelocity, effSquared, mult, mix, ret);
            }
            return ret;
        }


        // This is from previous instantiations of XXXDynamics.cs.
        // Applies roll reference frame.
        // TODO: is this the right way to separate the code to do this operation?
        //    Should this be in MoveAngular()?
        internal void LimitRotation(float timestep)
        {
            Quaternion rotq = Prim.ForceOrientation;
            Quaternion m_rot = rotq;
            if (m_RollreferenceFrame != Quaternion.Identity)
            {
                if (rotq.X >= m_RollreferenceFrame.X)
                {
                    m_rot.X = rotq.X - (m_RollreferenceFrame.X / 2);
                }
                if (rotq.Y >= m_RollreferenceFrame.Y)
                {
                    m_rot.Y = rotq.Y - (m_RollreferenceFrame.Y / 2);
                }
                if (rotq.X <= -m_RollreferenceFrame.X)
                {
                    m_rot.X = rotq.X + (m_RollreferenceFrame.X / 2);
                }
                if (rotq.Y <= -m_RollreferenceFrame.Y)
                {
                    m_rot.Y = rotq.Y + (m_RollreferenceFrame.Y / 2);
                }
            }
            if ((m_flags & VehicleFlag.LOCK_ROTATION) != 0)
            {
                m_rot.X = 0;
                m_rot.Y = 0;
            }
            if (rotq != m_rot)
            {
                Prim.ForceOrientation = m_rot;
                VDetailLog("{0},LimitRotation,done,orig={1},new={2}", Prim.LocalID, rotq, m_rot);
            }

        }

        // Invoke the detailed logger and output something if it's enabled.
        private void VDetailLog(string msg, params Object[] args)
        {
            if (Prim.PhysicsScene.VehicleLoggingEnabled)
                Prim.PhysicsScene.DetailLog(msg, args);
        }
    }
}
