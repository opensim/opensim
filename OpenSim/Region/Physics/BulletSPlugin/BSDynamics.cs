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
 * The quotations from http://wiki.secondlife.com/wiki/Linden_Vehicle_Tutorial
 * are Copyright (c) 2009 Linden Research, Inc and are used under their license
 * of Creative Commons Attribution-Share Alike 3.0
 * (http://creativecommons.org/licenses/by-sa/3.0/).
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
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
        private Vector3 m_lastAngularVelocity = Vector3.Zero;
        private Vector3 m_lastVertAttractor = Vector3.Zero;             // what VA was last applied to body

        //Deflection properties
        private BSVMotor m_angularDeflectionMotor = new BSVMotor("AngularDeflection");
        private float m_angularDeflectionEfficiency = 0;
        private float m_angularDeflectionTimescale = 0;
        private float m_linearDeflectionEfficiency = 0;
        private float m_linearDeflectionTimescale = 0;

        //Banking properties
        private float m_bankingEfficiency = 0;
        private float m_bankingMix = 0;
        private float m_bankingTimescale = 0;

        //Hover and Buoyancy properties
        private BSVMotor m_hoverMotor = new BSVMotor("Hover");
        private float m_VhoverHeight = 0f;
        private float m_VhoverEfficiency = 0f;
        private float m_VhoverTimescale = 0f;
        private float m_VhoverTargetHeight = -1.0f;     // if <0 then no hover, else its the current target height
        // Modifies gravity. Slider between -1 (double-gravity) and 1 (full anti-gravity)
        private float m_VehicleBuoyancy = 0f;
        private Vector3 m_VehicleGravity = Vector3.Zero;    // Gravity computed when buoyancy set

        //Attractor properties
        private BSVMotor m_verticalAttractionMotor = new BSVMotor("VerticalAttraction");
        private float m_verticalAttractionEfficiency = 1.0f; // damped
        private float m_verticalAttractionCutoff = 500f;     // per the documentation
        // Timescale > cutoff  means no vert attractor.
        private float m_verticalAttractionTimescale = 510f;

        // Just some recomputed constants:
        static readonly float PIOverFour = ((float)Math.PI) / 4f;
        static readonly float PIOverTwo = ((float)Math.PI) / 2f;

        // For debugging, flags to turn on and off individual corrections.
        public bool enableAngularVerticalAttraction;
        public bool enableAngularDeflection;
        public bool enableAngularBanking;

        public BSDynamics(BSScene myScene, BSPrim myPrim)
        {
            PhysicsScene = myScene;
            Prim = myPrim;
            Type = Vehicle.TYPE_NONE;
            SetupVehicleDebugging();
        }

        // Stopgap debugging enablement. Allows source level debugging but still checking
        //    in changes by making enablement of debugging flags from INI file.
        public void SetupVehicleDebugging()
        {
            enableAngularVerticalAttraction = true;
            enableAngularDeflection = false;
            enableAngularBanking = true;
            if (BSParam.VehicleDebuggingEnabled)
            {
                enableAngularVerticalAttraction = true;
                enableAngularDeflection = false;
                enableAngularBanking = false;
            }
        }

        // Return 'true' if this vehicle is doing vehicle things
        public bool IsActive
        {
            get { return (Type != Vehicle.TYPE_NONE && Prim.IsPhysicallyActive); }
        }

        // Return 'true' if this a vehicle that should be sitting on the ground
        public bool IsGroundVehicle
        {
            get { return (Type == Vehicle.TYPE_CAR || Type == Vehicle.TYPE_SLED); }
        }

        #region Vehicle parameter setting
        public void ProcessFloatVehicleParam(Vehicle pParam, float pValue)
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
                    m_angularMotorDecayTimescale = ClampInRange(0.01f, pValue, 120);
                    m_angularMotor.TargetValueDecayTimeScale = m_angularMotorDecayTimescale;
                    break;
                case Vehicle.ANGULAR_MOTOR_TIMESCALE:
                    m_angularMotorTimescale = Math.Max(pValue, 0.01f);
                    m_angularMotor.TimeScale = m_angularMotorTimescale;
                    break;
                case Vehicle.BANKING_EFFICIENCY:
                    m_bankingEfficiency = ClampInRange(-1f, pValue, 1f);
                    break;
                case Vehicle.BANKING_MIX:
                    m_bankingMix = Math.Max(pValue, 0.01f);
                    break;
                case Vehicle.BANKING_TIMESCALE:
                    m_bankingTimescale = Math.Max(pValue, 0.01f);
                    break;
                case Vehicle.BUOYANCY:
                    m_VehicleBuoyancy = ClampInRange(-1f, pValue, 1f);
                    m_VehicleGravity = Prim.ComputeGravity(m_VehicleBuoyancy);
                    break;
                case Vehicle.HOVER_EFFICIENCY:
                    m_VhoverEfficiency = ClampInRange(0f, pValue, 1f);
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
                    m_linearMotorDecayTimescale = ClampInRange(0.01f, pValue, 120);
                    m_linearMotor.TargetValueDecayTimeScale = m_linearMotorDecayTimescale;
                    break;
                case Vehicle.LINEAR_MOTOR_TIMESCALE:
                    m_linearMotorTimescale = Math.Max(pValue, 0.01f);
                    m_linearMotor.TimeScale = m_linearMotorTimescale;
                    break;
                case Vehicle.VERTICAL_ATTRACTION_EFFICIENCY:
                    m_verticalAttractionEfficiency = ClampInRange(0.1f, pValue, 1f);
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
                    m_angularMotor.Zero();
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
                    pValue.X = ClampInRange(-12.56f, pValue.X, 12.56f);
                    pValue.Y = ClampInRange(-12.56f, pValue.Y, 12.56f);
                    pValue.Z = ClampInRange(-12.56f, pValue.Z, 12.56f);
                    m_angularMotorDirection = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    m_angularMotor.Zero();
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

        public void ProcessTypeChange(Vehicle pType)
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
                                    | VehicleFlag.HOVER_UP_ONLY);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP
                                    | VehicleFlag.LIMIT_MOTOR_UP
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

            /*  Not implemented
            m_verticalAttractionMotor = new BSVMotor("VerticalAttraction", m_verticalAttractionTimescale,
                                BSMotor.Infinite, BSMotor.InfiniteVector,
                                m_verticalAttractionEfficiency);
            // Z goes away and we keep X and Y
            m_verticalAttractionMotor.FrictionTimescale = new Vector3(BSMotor.Infinite, BSMotor.Infinite, 0.1f);
            m_verticalAttractionMotor.PhysicsScene = PhysicsScene;  // DEBUG DEBUG DEBUG (enables detail logging)
             */
        }
        #endregion // Vehicle parameter setting

        public void Refresh()
        {
            // If asking for a refresh, reset the physical parameters before the next simulation step.
            PhysicsScene.PostTaintObject("BSDynamics.Refresh", Prim.LocalID, delegate()
            {
                SetPhysicalParameters();
            });
        }

        // Some of the properties of this prim may have changed.
        // Do any updating needed for a vehicle
        private void SetPhysicalParameters()
        {
            if (IsActive)
            {
                // Remember the mass so we don't have to fetch it every step
                m_vehicleMass = Prim.TotalMass;

                // Friction affects are handled by this vehicle code
                PhysicsScene.PE.SetFriction(Prim.PhysBody, BSParam.VehicleFriction);
                PhysicsScene.PE.SetRestitution(Prim.PhysBody, BSParam.VehicleRestitution);

                // Moderate angular movement introduced by Bullet.
                // TODO: possibly set AngularFactor and LinearFactor for the type of vehicle.
                //     Maybe compute linear and angular factor and damping from params.
                PhysicsScene.PE.SetAngularDamping(Prim.PhysBody, BSParam.VehicleAngularDamping);
                PhysicsScene.PE.SetLinearFactor(Prim.PhysBody, BSParam.VehicleLinearFactor);
                PhysicsScene.PE.SetAngularFactorV(Prim.PhysBody, BSParam.VehicleAngularFactor);

                // Vehicles report collision events so we know when it's on the ground
                PhysicsScene.PE.AddToCollisionFlags(Prim.PhysBody, CollisionFlags.BS_VEHICLE_COLLISIONS);

                Prim.Inertia = PhysicsScene.PE.CalculateLocalInertia(Prim.PhysShape, m_vehicleMass);
                PhysicsScene.PE.SetMassProps(Prim.PhysBody, m_vehicleMass, Prim.Inertia);
                PhysicsScene.PE.UpdateInertiaTensor(Prim.PhysBody);

                // Set the gravity for the vehicle depending on the buoyancy
                // TODO: what should be done if prim and vehicle buoyancy differ?
                m_VehicleGravity = Prim.ComputeGravity(m_VehicleBuoyancy);
                // The actual vehicle gravity is set to zero in Bullet so we can do all the application of same.
                PhysicsScene.PE.SetGravity(Prim.PhysBody, Vector3.Zero);

                VDetailLog("{0},BSDynamics.SetPhysicalParameters,mass={1},inert={2},vehGrav={3},aDamp={4},frict={5},rest={6},lFact={7},aFact={8}",
                        Prim.LocalID, m_vehicleMass, Prim.Inertia, m_VehicleGravity,
                        BSParam.VehicleAngularDamping, BSParam.VehicleFriction, BSParam.VehicleRestitution,
                        BSParam.VehicleLinearFactor, BSParam.VehicleAngularFactor
                        );
            }
            else
            {
                if (Prim.PhysBody.HasPhysicalBody)
                    PhysicsScene.PE.RemoveFromCollisionFlags(Prim.PhysBody, CollisionFlags.BS_VEHICLE_COLLISIONS);
            }
        }

        public bool RemoveBodyDependencies(BSPhysObject prim)
        {
            Refresh();
            return IsActive;
        }

        #region Known vehicle value functions
        // Vehicle physical parameters that we buffer from constant getting and setting.
        // The "m_known*" values are unknown until they are fetched and the m_knownHas flag is set.
        //      Changing is remembered and the parameter is stored back into the physics engine only if updated.
        //      This does two things: 1) saves continuious calls into unmanaged code, and
        //      2) signals when a physics property update must happen back to the simulator
        //      to update values modified for the vehicle.
        private int m_knownChanged;
        private int m_knownHas;
        private float m_knownTerrainHeight;
        private float m_knownWaterLevel;
        private Vector3 m_knownPosition;
        private Vector3 m_knownVelocity;
        private Vector3 m_knownForce;
        private Vector3 m_knownForceImpulse;
        private Quaternion m_knownOrientation;
        private Vector3 m_knownRotationalVelocity;
        private Vector3 m_knownRotationalForce;
        private Vector3 m_knownRotationalImpulse;
        private Vector3 m_knownForwardVelocity;    // vehicle relative forward speed

        private const int m_knownChangedPosition           = 1 << 0;
        private const int m_knownChangedVelocity           = 1 << 1;
        private const int m_knownChangedForce              = 1 << 2;
        private const int m_knownChangedForceImpulse       = 1 << 3;
        private const int m_knownChangedOrientation        = 1 << 4;
        private const int m_knownChangedRotationalVelocity = 1 << 5;
        private const int m_knownChangedRotationalForce    = 1 << 6;
        private const int m_knownChangedRotationalImpulse  = 1 << 7;
        private const int m_knownChangedTerrainHeight      = 1 << 8;
        private const int m_knownChangedWaterLevel         = 1 << 9;
        private const int m_knownChangedForwardVelocity    = 1 <<10;

        public void ForgetKnownVehicleProperties()
        {
            m_knownHas = 0;
            m_knownChanged = 0;
        }
        // Push all the changed values back into the physics engine
        public void PushKnownChanged()
        {
            if (m_knownChanged != 0)
            {
                if ((m_knownChanged & m_knownChangedPosition) != 0)
                    Prim.ForcePosition = m_knownPosition;

                if ((m_knownChanged & m_knownChangedOrientation) != 0)
                    Prim.ForceOrientation = m_knownOrientation;

                if ((m_knownChanged & m_knownChangedVelocity) != 0)
                {
                    Prim.ForceVelocity = m_knownVelocity;
                    // Fake out Bullet by making it think the velocity is the same as last time.
                    // Bullet does a bunch of smoothing for changing parameters.
                    //    Since the vehicle is demanding this setting, we override Bullet's smoothing
                    //    by telling Bullet the value was the same last time.
                    // PhysicsScene.PE.SetInterpolationLinearVelocity(Prim.PhysBody, m_knownVelocity);
                }

                if ((m_knownChanged & m_knownChangedForce) != 0)
                    Prim.AddForce((Vector3)m_knownForce, false /*pushForce*/, true /*inTaintTime*/);

                if ((m_knownChanged & m_knownChangedForceImpulse) != 0)
                    Prim.AddForceImpulse((Vector3)m_knownForceImpulse, false /*pushforce*/, true /*inTaintTime*/);

                if ((m_knownChanged & m_knownChangedRotationalVelocity) != 0)
                {
                    Prim.ForceRotationalVelocity = m_knownRotationalVelocity;
                    // PhysicsScene.PE.SetInterpolationAngularVelocity(Prim.PhysBody, m_knownRotationalVelocity);
                }

                if ((m_knownChanged & m_knownChangedRotationalImpulse) != 0)
                    Prim.ApplyTorqueImpulse((Vector3)m_knownRotationalImpulse, true /*inTaintTime*/);

                if ((m_knownChanged & m_knownChangedRotationalForce) != 0)
                {
                    Prim.AddAngularForce((Vector3)m_knownRotationalForce, false /*pushForce*/, true /*inTaintTime*/);
                }

                // If we set one of the values (ie, the physics engine didn't do it) we must force
                //      an UpdateProperties event to send the changes up to the simulator.
                PhysicsScene.PE.PushUpdate(Prim.PhysBody);
            }
            m_knownChanged = 0;
        }

        // Since the computation of terrain height can be a little involved, this routine
        //    is used to fetch the height only once for each vehicle simulation step.
        Vector3 lastRememberedHeightPos;
        private float GetTerrainHeight(Vector3 pos)
        {
            if ((m_knownHas & m_knownChangedTerrainHeight) == 0 || pos != lastRememberedHeightPos)
            {
                lastRememberedHeightPos = pos;
                m_knownTerrainHeight = Prim.PhysicsScene.TerrainManager.GetTerrainHeightAtXYZ(pos);
                m_knownHas |= m_knownChangedTerrainHeight;
            }
            return m_knownTerrainHeight;
        }

        // Since the computation of water level can be a little involved, this routine
        //    is used ot fetch the level only once for each vehicle simulation step.
        private float GetWaterLevel(Vector3 pos)
        {
            if ((m_knownHas & m_knownChangedWaterLevel) == 0)
            {
                m_knownWaterLevel = Prim.PhysicsScene.TerrainManager.GetWaterLevelAtXYZ(pos);
                m_knownHas |= m_knownChangedWaterLevel;
            }
            return (float)m_knownWaterLevel;
        }

        private Vector3 VehiclePosition
        {
            get
            {
                if ((m_knownHas & m_knownChangedPosition) == 0)
                {
                    m_knownPosition = Prim.ForcePosition;
                    m_knownHas |= m_knownChangedPosition;
                }
                return m_knownPosition;
            }
            set
            {
                m_knownPosition = value;
                m_knownChanged |= m_knownChangedPosition;
                m_knownHas |= m_knownChangedPosition;
            }
        }

        private Quaternion VehicleOrientation
        {
            get
            {
                if ((m_knownHas & m_knownChangedOrientation) == 0)
                {
                    m_knownOrientation = Prim.ForceOrientation;
                    m_knownHas |= m_knownChangedOrientation;
                }
                return m_knownOrientation;
            }
            set
            {
                m_knownOrientation = value;
                m_knownChanged |= m_knownChangedOrientation;
                m_knownHas |= m_knownChangedOrientation;
            }
        }

        private Vector3 VehicleVelocity
        {
            get
            {
                if ((m_knownHas & m_knownChangedVelocity) == 0)
                {
                    m_knownVelocity = Prim.ForceVelocity;
                    m_knownHas |= m_knownChangedVelocity;
                }
                return m_knownVelocity;
            }
            set
            {
                m_knownVelocity = value;
                m_knownChanged |= m_knownChangedVelocity;
                m_knownHas |= m_knownChangedVelocity;
            }
        }

        private void VehicleAddForce(Vector3 pForce)
        {
            if ((m_knownHas & m_knownChangedForce) == 0)
            {
                m_knownForce = Vector3.Zero;
                m_knownHas |= m_knownChangedForce;
            }
            m_knownForce += pForce;
            m_knownChanged |= m_knownChangedForce;
        }

        private void VehicleAddForceImpulse(Vector3 pImpulse)
        {
            if ((m_knownHas & m_knownChangedForceImpulse) == 0)
            {
                m_knownForceImpulse = Vector3.Zero;
                m_knownHas |= m_knownChangedForceImpulse;
            }
            m_knownForceImpulse += pImpulse;
            m_knownChanged |= m_knownChangedForceImpulse;
        }

        private Vector3 VehicleRotationalVelocity
        {
            get
            {
                if ((m_knownHas & m_knownChangedRotationalVelocity) == 0)
                {
                    m_knownRotationalVelocity = Prim.ForceRotationalVelocity;
                    m_knownHas |= m_knownChangedRotationalVelocity;
                }
                return (Vector3)m_knownRotationalVelocity;
            }
            set
            {
                m_knownRotationalVelocity = value;
                m_knownChanged |= m_knownChangedRotationalVelocity;
                m_knownHas |= m_knownChangedRotationalVelocity;
            }
        }
        private void VehicleAddAngularForce(Vector3 aForce)
        {
            if ((m_knownHas & m_knownChangedRotationalForce) == 0)
            {
                m_knownRotationalForce = Vector3.Zero;
            }
            m_knownRotationalForce += aForce;
            m_knownChanged |= m_knownChangedRotationalForce;
            m_knownHas |= m_knownChangedRotationalForce;
        }
        private void VehicleAddRotationalImpulse(Vector3 pImpulse)
        {
            if ((m_knownHas & m_knownChangedRotationalImpulse) == 0)
            {
                m_knownRotationalImpulse = Vector3.Zero;
                m_knownHas |= m_knownChangedRotationalImpulse;
            }
            m_knownRotationalImpulse += pImpulse;
            m_knownChanged |= m_knownChangedRotationalImpulse;
        }

        // Vehicle relative forward velocity
        private Vector3 VehicleForwardVelocity
        {
            get
            {
                if ((m_knownHas & m_knownChangedForwardVelocity) == 0)
                {
                    m_knownForwardVelocity = VehicleVelocity * Quaternion.Inverse(Quaternion.Normalize(VehicleOrientation));
                    m_knownHas |= m_knownChangedForwardVelocity;
                }
                return m_knownForwardVelocity;
            }
        }
        private float VehicleForwardSpeed
        {
            get
            {
                return VehicleForwardVelocity.X;
            }
        }

        #endregion // Known vehicle value functions

        // One step of the vehicle properties for the next 'pTimestep' seconds.
        internal void Step(float pTimestep)
        {
            if (!IsActive) return;

            ForgetKnownVehicleProperties();

            MoveLinear(pTimestep);
            MoveAngular(pTimestep);

            LimitRotation(pTimestep);

            // remember the position so next step we can limit absolute movement effects
            m_lastPositionVector = VehiclePosition;

            // If we forced the changing of some vehicle parameters, update the values and
            //      for the physics engine to note the changes so an UpdateProperties event will happen.
            PushKnownChanged();

            if (PhysicsScene.VehiclePhysicalLoggingEnabled)
                PhysicsScene.PE.DumpRigidBody(PhysicsScene.World, Prim.PhysBody);

            VDetailLog("{0},BSDynamics.Step,done,pos={1}, force={2},velocity={3},angvel={4}",
                    Prim.LocalID, VehiclePosition, m_knownForce, VehicleVelocity, VehicleRotationalVelocity);
        }

        // Called after the simulation step
        internal void PostStep(float pTimestep)
        {
            if (!IsActive) return;

            if (PhysicsScene.VehiclePhysicalLoggingEnabled)
                PhysicsScene.PE.DumpRigidBody(PhysicsScene.World, Prim.PhysBody);
        }

        // Apply the effect of the linear motor and other linear motions (like hover and float).
        private void MoveLinear(float pTimestep)
        {
            ComputeLinearVelocity(pTimestep);

            ComputeLinearTerrainHeightCorrection(pTimestep);

            ComputeLinearHover(pTimestep);

            ComputeLinearBlockingEndPoint(pTimestep);

            ComputeLinearMotorUp(pTimestep);

            ApplyGravity(pTimestep);

            // If not changing some axis, reduce out velocity
            if ((m_flags & (VehicleFlag.NO_X | VehicleFlag.NO_Y | VehicleFlag.NO_Z)) != 0)
            {
                Vector3 vel = VehicleVelocity;
                if ((m_flags & (VehicleFlag.NO_X)) != 0)
                    vel.X = 0;
                if ((m_flags & (VehicleFlag.NO_Y)) != 0)
                    vel.Y = 0;
                if ((m_flags & (VehicleFlag.NO_Z)) != 0)
                    vel.Z = 0;
                VehicleVelocity = vel;
            }

            // ==================================================================
            // Clamp high or low velocities
            float newVelocityLengthSq = VehicleVelocity.LengthSquared();
            if (newVelocityLengthSq > BSParam.VehicleMaxLinearVelocitySquared)
            {
                Vector3 origVelW = VehicleVelocity;         // DEBUG DEBUG
                VehicleVelocity /= VehicleVelocity.Length();
                VehicleVelocity *= BSParam.VehicleMaxLinearVelocity;
                VDetailLog("{0},  MoveLinear,clampMax,origVelW={1},lenSq={2},maxVelSq={3},,newVelW={4}", 
                            Prim.LocalID, origVelW, newVelocityLengthSq, BSParam.VehicleMaxLinearVelocitySquared, VehicleVelocity);
            }
            else if (newVelocityLengthSq < 0.001f)
                VehicleVelocity = Vector3.Zero;

            VDetailLog("{0},  MoveLinear,done,isColl={1},newVel={2}", Prim.LocalID, Prim.IsColliding, VehicleVelocity );

        } // end MoveLinear()

        public void ComputeLinearVelocity(float pTimestep)
        {
            // Step the motor from the current value. Get the correction needed this step.
            Vector3 origVelW = VehicleVelocity;             // DEBUG
            Vector3 currentVelV = VehicleVelocity * Quaternion.Inverse(VehicleOrientation);
            Vector3 linearMotorCorrectionV = m_linearMotor.Step(pTimestep, currentVelV);

            // Motor is vehicle coordinates. Rotate it to world coordinates
            Vector3 linearMotorVelocityW = linearMotorCorrectionV * VehicleOrientation;

            // If we're a ground vehicle, don't add any upward Z movement
            if ((m_flags & VehicleFlag.LIMIT_MOTOR_UP) != 0)
            {
                if (linearMotorVelocityW.Z > 0f)
                    linearMotorVelocityW.Z = 0f;
            }

            // Add this correction to the velocity to make it faster/slower.
            VehicleVelocity += linearMotorVelocityW;

            VDetailLog("{0},  MoveLinear,velocity,origVelW={1},velV={2},correctV={3},correctW={4},newVelW={5}",
                        Prim.LocalID, origVelW, currentVelV, linearMotorCorrectionV, linearMotorVelocityW, VehicleVelocity);
        }

        public void ComputeLinearTerrainHeightCorrection(float pTimestep)
        {
            // If below the terrain, move us above the ground a little.
            // TODO: Consider taking the rotated size of the object or possibly casting a ray.
            if (VehiclePosition.Z < GetTerrainHeight(VehiclePosition))
            {
                // Force position because applying force won't get the vehicle through the terrain
                Vector3 newPosition = VehiclePosition;
                newPosition.Z = GetTerrainHeight(VehiclePosition) + 1f;
                VehiclePosition = newPosition;
                VDetailLog("{0},  MoveLinear,terrainHeight,terrainHeight={1},pos={2}",
                        Prim.LocalID, GetTerrainHeight(VehiclePosition), VehiclePosition);
            }
        }

        public void ComputeLinearHover(float pTimestep)
        {
            // m_VhoverEfficiency: 0=bouncy, 1=totally damped
            // m_VhoverTimescale: time to achieve height
            if ((m_flags & (VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY | VehicleFlag.HOVER_GLOBAL_HEIGHT)) != 0)
            {
                // We should hover, get the target height
                if ((m_flags & VehicleFlag.HOVER_WATER_ONLY) != 0)
                {
                    m_VhoverTargetHeight = GetWaterLevel(VehiclePosition) + m_VhoverHeight;
                }
                if ((m_flags & VehicleFlag.HOVER_TERRAIN_ONLY) != 0)
                {
                    m_VhoverTargetHeight = GetTerrainHeight(VehiclePosition) + m_VhoverHeight;
                }
                if ((m_flags & VehicleFlag.HOVER_GLOBAL_HEIGHT) != 0)
                {
                    m_VhoverTargetHeight = m_VhoverHeight;
                }

                if ((m_flags & VehicleFlag.HOVER_UP_ONLY) != 0)
                {
                    // If body is already heigher, use its height as target height
                    if (VehiclePosition.Z > m_VhoverTargetHeight)
                        m_VhoverTargetHeight = VehiclePosition.Z;
                }
                
                if ((m_flags & VehicleFlag.LOCK_HOVER_HEIGHT) != 0)
                {
                    if (Math.Abs(VehiclePosition.Z - m_VhoverTargetHeight) > 0.2f)
                    {
                        Vector3 pos = VehiclePosition;
                        pos.Z = m_VhoverTargetHeight;
                        VehiclePosition = pos;

                        VDetailLog("{0},  MoveLinear,hover,pos={1},lockHoverHeight", Prim.LocalID, pos);
                    }
                }
                else
                {
                    // Error is positive if below the target and negative if above.
                    Vector3 hpos = VehiclePosition;
                    float verticalError = m_VhoverTargetHeight - hpos.Z;
                    float verticalCorrection = verticalError / m_VhoverTimescale;
                    verticalCorrection *= m_VhoverEfficiency;

                    hpos.Z += verticalCorrection;
                    VehiclePosition = hpos;

                    // Since we are hovering, we need to do the opposite of falling -- get rid of world Z
                    Vector3 vel = VehicleVelocity;
                    vel.Z = 0f;
                    VehicleVelocity = vel;

                    /*
                    float verticalCorrectionVelocity = verticalError / m_VhoverTimescale;
                    Vector3 verticalCorrection = new Vector3(0f, 0f, verticalCorrectionVelocity);
                    verticalCorrection *= m_vehicleMass;

                    // TODO: implement m_VhoverEfficiency correctly
                    VehicleAddForceImpulse(verticalCorrection);
                     */

                    VDetailLog("{0},  MoveLinear,hover,pos={1},eff={2},hoverTS={3},height={4},target={5},err={6},corr={7}",
                                    Prim.LocalID, VehiclePosition, m_VhoverEfficiency,
                                    m_VhoverTimescale, m_VhoverHeight, m_VhoverTargetHeight,
                                    verticalError, verticalCorrection);
                }

            }
        }

        public bool ComputeLinearBlockingEndPoint(float pTimestep)
        {
            bool changed = false;

            Vector3 pos = VehiclePosition;
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
                    VehiclePosition = pos;
                    VDetailLog("{0},  MoveLinear,blockingEndPoint,block={1},origPos={2},pos={3}",
                                Prim.LocalID, m_BlockingEndPoint, posChange, pos);
                }
            }
            return changed;
        }

        // From http://wiki.secondlife.com/wiki/LlSetVehicleFlags :
        //    Prevent ground vehicles from motoring into the sky. This flag has a subtle effect when
        //    used with conjunction with banking: the strength of the banking will decay when the
        //    vehicle no longer experiences collisions. The decay timescale is the same as
        //    VEHICLE_BANKING_TIMESCALE. This is to help prevent ground vehicles from steering
        //    when they are in mid jump. 
        // TODO: this code is wrong. Also, what should it do for boats (height from water)?
        //    This is just using the ground and a general collision check. Should really be using
        //    a downward raycast to find what is below.
        public void ComputeLinearMotorUp(float pTimestep)
        {
            if ((m_flags & (VehicleFlag.LIMIT_MOTOR_UP)) != 0)
            {
                // This code tries to decide if the object is not on the ground and then pushing down
                /*
                float targetHeight = Type == Vehicle.TYPE_BOAT ? GetWaterLevel(VehiclePosition) : GetTerrainHeight(VehiclePosition);
                distanceAboveGround = VehiclePosition.Z - targetHeight;
                // Not colliding if the vehicle is off the ground
                if (!Prim.IsColliding)
                {
                    // downForce = new Vector3(0, 0, -distanceAboveGround / m_bankingTimescale);
                    VehicleVelocity += new Vector3(0, 0, -distanceAboveGround);
                }
                // TODO: this calculation is wrong. From the description at
                //     (http://wiki.secondlife.com/wiki/Category:LSL_Vehicle), the downForce
                //     has a decay factor. This says this force should
                //     be computed with a motor.
                // TODO: add interaction with banking.
                VDetailLog("{0},  MoveLinear,limitMotorUp,distAbove={1},colliding={2},ret={3}",
                                Prim.LocalID, distanceAboveGround, Prim.IsColliding, ret);
                 */

                // Another approach is to measure if we're going up. If going up and not colliding,
                //     the vehicle is in the air.  Fix that by pushing down.
                if (!Prim.IsColliding && VehicleVelocity.Z > 0.1)
                {
                    // Get rid of any of the velocity vector that is pushing us up.
                    float upVelocity = VehicleVelocity.Z;
                    VehicleVelocity += new Vector3(0, 0, -upVelocity);

                    /*
                    // If we're pointed up into the air, we should nose down
                    Vector3 pointingDirection = Vector3.UnitX * VehicleOrientation;
                    // The rotation around the Y axis is pitch up or down
                    if (pointingDirection.Y > 0.01f)
                    {
                        float angularCorrectionForce = -(float)Math.Asin(pointingDirection.Y);
                        Vector3 angularCorrectionVector = new Vector3(0f, angularCorrectionForce, 0f);
                        // Rotate into world coordinates and apply to vehicle
                        angularCorrectionVector *= VehicleOrientation;
                        VehicleAddAngularForce(angularCorrectionVector);
                        VDetailLog("{0},  MoveLinear,limitMotorUp,newVel={1},pntDir={2},corrFrc={3},aCorr={4}",
                                    Prim.LocalID, VehicleVelocity, pointingDirection, angularCorrectionForce, angularCorrectionVector);
                    }
                        */
                    VDetailLog("{0},  MoveLinear,limitMotorUp,collide={1},upVel={2},newVel={3}",
                                    Prim.LocalID, Prim.IsColliding, upVelocity, VehicleVelocity);
                }
            }
        }

        private void ApplyGravity(float pTimeStep)
        {
            Vector3 appliedGravity = m_VehicleGravity * m_vehicleMass;

            // Hack to reduce downward force if the vehicle is probably sitting on the ground
            if (Prim.IsColliding && IsGroundVehicle)
                appliedGravity *= BSParam.VehicleGroundGravityFudge;

            VehicleAddForce(appliedGravity);

            VDetailLog("{0},  MoveLinear,applyGravity,vehGrav={1},collid={2},fudge={3},mass={4},appliedForce={3}", 
                            Prim.LocalID, m_VehicleGravity,
                            Prim.IsColliding, BSParam.VehicleGroundGravityFudge, m_vehicleMass, appliedGravity);
        }

        // =======================================================================
        // =======================================================================
        // Apply the effect of the angular motor.
        // The 'contribution' is how much angular correction velocity each function wants.
        //     All the contributions are added together and the resulting velocity is
        //     set directly on the vehicle.
        private void MoveAngular(float pTimestep)
        {
            ComputeAngularTurning(pTimestep);

            ComputeAngularVerticalAttraction();

            ComputeAngularDeflection();

            ComputeAngularBanking();

            // ==================================================================
            if (VehicleRotationalVelocity.ApproxEquals(Vector3.Zero, 0.0001f))
            {
                // The vehicle is not adding anything angular wise.
                VehicleRotationalVelocity = Vector3.Zero;
                VDetailLog("{0},  MoveAngular,done,zero", Prim.LocalID);
            }
            else
            {
                VDetailLog("{0},  MoveAngular,done,nonZero,angVel={1}", Prim.LocalID, VehicleRotationalVelocity);
            }

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

                VehicleAddAngularForce(torqueFromOffset * m_vehicleMass);
                VDetailLog("{0},  BSDynamic.MoveAngular,motorOffset,applyTorqueImpulse={1}", Prim.LocalID, torqueFromOffset);
            }

        }

        private void ComputeAngularTurning(float pTimestep)
        {
            // The user wants this many radians per second angular change?
            Vector3 currentAngularV = VehicleRotationalVelocity * Quaternion.Inverse(VehicleOrientation);
            Vector3 angularMotorContributionV = m_angularMotor.Step(pTimestep, currentAngularV);

            // ==================================================================
            // From http://wiki.secondlife.com/wiki/LlSetVehicleFlags :
            //    This flag prevents linear deflection parallel to world z-axis. This is useful
            //    for preventing ground vehicles with large linear deflection, like bumper cars,
            //    from climbing their linear deflection into the sky. 
            // That is, NO_DEFLECTION_UP says angular motion should not add any pitch or roll movement
            // TODO: This is here because this is where ODE put it but documentation says it
            //    is a linear effect. Where should this check go?
            //if ((m_flags & (VehicleFlag.NO_DEFLECTION_UP)) != 0)
           // {
            //    angularMotorContributionV.X = 0f;
            //    angularMotorContributionV.Y = 0f;
          //  }

            VehicleRotationalVelocity += angularMotorContributionV * VehicleOrientation;
            VDetailLog("{0},  MoveAngular,angularTurning,angularMotorContrib={1}", Prim.LocalID, angularMotorContributionV);
        }

        // From http://wiki.secondlife.com/wiki/Linden_Vehicle_Tutorial:
        //      Some vehicles, like boats, should always keep their up-side up. This can be done by
        //      enabling the "vertical attractor" behavior that springs the vehicle's local z-axis to
        //      the world z-axis (a.k.a. "up"). To take advantage of this feature you would set the
        //      VEHICLE_VERTICAL_ATTRACTION_TIMESCALE to control the period of the spring frequency,
        //      and then set the VEHICLE_VERTICAL_ATTRACTION_EFFICIENCY to control the damping. An
        //      efficiency of 0.0 will cause the spring to wobble around its equilibrium, while an
        //      efficiency of 1.0 will cause the spring to reach its equilibrium with exponential decay.
        public void ComputeAngularVerticalAttraction()
        {

            // If vertical attaction timescale is reasonable
            if (enableAngularVerticalAttraction && m_verticalAttractionTimescale < m_verticalAttractionCutoff)
            {
                // Possible solution derived from a discussion at:
                // http://stackoverflow.com/questions/14939657/computing-vector-from-quaternion-works-computing-quaternion-from-vector-does-no

                // Create a rotation that is only the vehicle's rotation around Z
                Vector3 currentEuler = Vector3.Zero;
                VehicleOrientation.GetEulerAngles(out currentEuler.X, out currentEuler.Y, out currentEuler.Z);
                Quaternion justZOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, currentEuler.Z);

                // Create the axis that is perpendicular to the up vector and the rotated up vector.
                Vector3 differenceAxis = Vector3.Cross(Vector3.UnitZ * justZOrientation, Vector3.UnitZ * VehicleOrientation);
                // Compute the angle between those to vectors.
                double differenceAngle = Math.Acos((double)Vector3.Dot(Vector3.UnitZ, Vector3.Normalize(Vector3.UnitZ * VehicleOrientation)));
                // 'differenceAngle' is the angle to rotate and 'differenceAxis' is the plane to rotate in to get the vehicle vertical

                // Reduce the change by the time period it is to change in. Timestep is handled when velocity is applied.
                // TODO: add 'efficiency'.
                differenceAngle /= m_verticalAttractionTimescale;

                // Create the quaterian representing the correction angle
                Quaternion correctionRotation = Quaternion.CreateFromAxisAngle(differenceAxis, (float)differenceAngle);

                // Turn that quaternion into Euler values to make it into velocities to apply.
                Vector3 vertContributionV = Vector3.Zero;
                correctionRotation.GetEulerAngles(out vertContributionV.X, out vertContributionV.Y, out vertContributionV.Z);
                vertContributionV *= -1f;

                VehicleRotationalVelocity += vertContributionV;

                VDetailLog("{0},  MoveAngular,verticalAttraction,diffAxis={1},diffAng={2},corrRot={3},contrib={4}",
                                Prim.LocalID,
                                differenceAxis,
                                differenceAngle,
                                correctionRotation,
                                vertContributionV);

                // ===================================================================
                /*
                Vector3 vertContributionV = Vector3.Zero;
                Vector3 origRotVelW = VehicleRotationalVelocity;        // DEBUG DEBUG

                // Take a vector pointing up and convert it from world to vehicle relative coords.
                Vector3 verticalError = Vector3.Normalize(Vector3.UnitZ * VehicleOrientation);

                // If vertical attraction correction is needed, the vector that was pointing up (UnitZ)
                //    is now:
                //    leaning to one side: rotated around the X axis with the Y value going
                //        from zero (nearly straight up) to one (completely to the side)) or
                //    leaning front-to-back: rotated around the Y axis with the value of X being between
                //         zero and one.
                // The value of Z is how far the rotation is off with 1 meaning none and 0 being 90 degrees.

                // Y error means needed rotation around X axis and visa versa.
                // Since the error goes from zero to one, the asin is the corresponding angle.
                vertContributionV.X = (float)Math.Asin(verticalError.Y);
                // (Tilt forward (positive X) needs to tilt back (rotate negative) around Y axis.)
                vertContributionV.Y = -(float)Math.Asin(verticalError.X);

                // If verticalError.Z is negative, the vehicle is upside down. Add additional push.
                if (verticalError.Z < 0f)
                {
                    vertContributionV.X += Math.Sign(vertContributionV.X) * PIOverFour;
                    // vertContribution.Y -= PIOverFour;
                }

                // 'vertContrbution' is now the necessary angular correction to correct tilt in one second.
                //     Correction happens over a number of seconds.
                Vector3 unscaledContribVerticalErrorV = vertContributionV;     // DEBUG DEBUG

                // The correction happens over the user's time period
                vertContributionV /= m_verticalAttractionTimescale;

                // Rotate the vehicle rotation to the world coordinates.
                VehicleRotationalVelocity += (vertContributionV * VehicleOrientation);

                VDetailLog("{0},  MoveAngular,verticalAttraction,,origRotVW={1},vertError={2},unscaledV={3},eff={4},ts={5},vertContribV={6}",
                                Prim.LocalID, origRotVelW, verticalError, unscaledContribVerticalErrorV, 
                                m_verticalAttractionEfficiency, m_verticalAttractionTimescale, vertContributionV);
                */
            }
        }

        // Angular correction to correct the direction the vehicle is pointing to be
        //      the direction is should want to be pointing.
        // The vehicle is moving in some direction and correct its orientation to it is pointing
        //     in that direction.
        // TODO: implement reference frame.
        public void ComputeAngularDeflection()
        {
            // Since angularMotorUp and angularDeflection are computed independently, they will calculate
            //     approximately the same X or Y correction. When added together (when contributions are combined)
            //     this creates an over-correction and then wabbling as the target is overshot.
            // TODO: rethink how the different correction computations inter-relate.

            if (enableAngularDeflection && m_angularDeflectionEfficiency != 0 && VehicleForwardSpeed > 0.2)
            {
                Vector3 deflectContributionV = Vector3.Zero;

                // The direction the vehicle is moving
                Vector3 movingDirection = VehicleVelocity;
                movingDirection.Normalize();

                // If the vehicle is going backward, it is still pointing forward
                movingDirection *= Math.Sign(VehicleForwardSpeed);

                // The direction the vehicle is pointing
                Vector3 pointingDirection = Vector3.UnitX * VehicleOrientation;
                pointingDirection.Normalize();

                // The difference between what is and what should be.
                Vector3 deflectionError = movingDirection - pointingDirection;

                // Don't try to correct very large errors (not our job)
                // if (Math.Abs(deflectionError.X) > PIOverFour) deflectionError.X = PIOverTwo * Math.Sign(deflectionError.X);
                // if (Math.Abs(deflectionError.Y) > PIOverFour) deflectionError.Y = PIOverTwo * Math.Sign(deflectionError.Y);
                // if (Math.Abs(deflectionError.Z) > PIOverFour) deflectionError.Z = PIOverTwo * Math.Sign(deflectionError.Z);
                if (Math.Abs(deflectionError.X) > PIOverFour) deflectionError.X = 0f;
                if (Math.Abs(deflectionError.Y) > PIOverFour) deflectionError.Y = 0f;
                if (Math.Abs(deflectionError.Z) > PIOverFour) deflectionError.Z = 0f;

                // ret = m_angularDeflectionCorrectionMotor(1f, deflectionError);

                // Scale the correction by recovery timescale and efficiency
                deflectContributionV = (-deflectionError) * m_angularDeflectionEfficiency;
                deflectContributionV /= m_angularDeflectionTimescale;

                VehicleRotationalVelocity += deflectContributionV * VehicleOrientation;

                VDetailLog("{0},  MoveAngular,Deflection,movingDir={1},pointingDir={2},deflectError={3},ret={4}",
                    Prim.LocalID, movingDirection, pointingDirection, deflectionError, deflectContributionV);
                VDetailLog("{0},  MoveAngular,Deflection,fwdSpd={1},defEff={2},defTS={3}",
                    Prim.LocalID, VehicleForwardSpeed, m_angularDeflectionEfficiency, m_angularDeflectionTimescale);
            }
        }

        // Angular change to rotate the vehicle around the Z axis when the vehicle
        //     is tipped around the X axis.
        // From http://wiki.secondlife.com/wiki/Linden_Vehicle_Tutorial:
        //      The vertical attractor feature must be enabled in order for the banking behavior to
        //      function. The way banking works is this: a rotation around the vehicle's roll-axis will
        //      produce a angular velocity around the yaw-axis, causing the vehicle to turn. The magnitude
        //      of the yaw effect will be proportional to the
        //          VEHICLE_BANKING_EFFICIENCY, the angle of the roll rotation, and sometimes the vehicle's
        //                 velocity along its preferred axis of motion. 
        //          The VEHICLE_BANKING_EFFICIENCY can vary between -1 and +1. When it is positive then any
        //                  positive rotation (by the right-hand rule) about the roll-axis will effect a
        //                  (negative) torque around the yaw-axis, making it turn to the right--that is the
        //                  vehicle will lean into the turn, which is how real airplanes and motorcycle's work.
        //                  Negating the banking coefficient will make it so that the vehicle leans to the
        //                  outside of the turn (not very "physical" but might allow interesting vehicles so why not?). 
        //           The VEHICLE_BANKING_MIX is a fake (i.e. non-physical) parameter that is useful for making
        //                  banking vehicles do what you want rather than what the laws of physics allow.
        //                  For example, consider a real motorcycle...it must be moving forward in order for
        //                  it to turn while banking, however video-game motorcycles are often configured
        //                  to turn in place when at a dead stop--because they are often easier to control
        //                  that way using the limited interface of the keyboard or game controller. The
        //                  VEHICLE_BANKING_MIX enables combinations of both realistic and non-realistic
        //                  banking by functioning as a slider between a banking that is correspondingly
        //                  totally static (0.0) and totally dynamic (1.0). By "static" we mean that the
        //                  banking effect depends only on the vehicle's rotation about its roll-axis compared
        //                  to "dynamic" where the banking is also proportional to its velocity along its
        //                  roll-axis. Finding the best value of the "mixture" will probably require trial and error. 
        //      The time it takes for the banking behavior to defeat a preexisting angular velocity about the
        //      world z-axis is determined by the VEHICLE_BANKING_TIMESCALE. So if you want the vehicle to
        //      bank quickly then give it a banking timescale of about a second or less, otherwise you can
        //      make a sluggish vehicle by giving it a timescale of several seconds. 
        public void ComputeAngularBanking()
        {
            if (enableAngularBanking && m_bankingEfficiency != 0 && m_verticalAttractionTimescale < m_verticalAttractionCutoff)
            {
                Vector3 bankingContributionV = Vector3.Zero;

                // Rotate a UnitZ vector (pointing up) to how the vehicle is oriented.
                // As the vehicle rolls to the right or left, the Y value will increase from
                //     zero (straight up) to 1 or -1 (full tilt right  or left)
                Vector3 rollComponents = Vector3.UnitZ * VehicleOrientation;

                // Figure out the yaw value for this much roll.
                float yawAngle = m_angularMotorDirection.X * m_bankingEfficiency;
                //        actual error  =       static turn error            +           dynamic turn error
                float mixedYawAngle =(yawAngle * (1f - m_bankingMix)) + ((yawAngle * m_bankingMix) * VehicleForwardSpeed);

                // TODO: the banking effect should not go to infinity but what to limit it to?
                //     And what should happen when this is being added to a user defined yaw that is already PI*4?
                mixedYawAngle = ClampInRange(-12, mixedYawAngle, 12);

                // Build the force vector to change rotation from what it is to what it should be
                bankingContributionV.Z = -mixedYawAngle;

                // Don't do it all at once. Fudge because 1 second is too fast with most user defined roll as PI*4.
                bankingContributionV /= m_bankingTimescale * BSParam.VehicleAngularBankingTimescaleFudge;

                //VehicleRotationalVelocity += bankingContributionV * VehicleOrientation;
                VehicleRotationalVelocity += bankingContributionV;
                

                VDetailLog("{0},  MoveAngular,Banking,rollComp={1},speed={2},rollComp={3},yAng={4},mYAng={5},ret={6}",
                            Prim.LocalID, rollComponents, VehicleForwardSpeed, rollComponents, yawAngle, mixedYawAngle, bankingContributionV);
            }
        }

        // This is from previous instantiations of XXXDynamics.cs.
        // Applies roll reference frame.
        // TODO: is this the right way to separate the code to do this operation?
        //    Should this be in MoveAngular()?
        internal void LimitRotation(float timestep)
        {
            Quaternion rotq = VehicleOrientation;
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
                VehicleOrientation = m_rot;
                VDetailLog("{0},  LimitRotation,done,orig={1},new={2}", Prim.LocalID, rotq, m_rot);
            }

        }

        private float ClampInRange(float low, float val, float high)
        {
            return Math.Max(low, Math.Min(val, high));
            // return Utils.Clamp(val, low, high);
        }

        // Invoke the detailed logger and output something if it's enabled.
        private void VDetailLog(string msg, params Object[] args)
        {
            if (Prim.PhysicsScene.VehicleLoggingEnabled)
                Prim.PhysicsScene.DetailLog(msg, args);
        }
    }
}
