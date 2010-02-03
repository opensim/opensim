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
 * Revised Aug, Sept 2009 by Kitto Flora. ODEDynamics.cs replaces
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
 * 
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
using Ode.NET;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Physics.OdePlugin
{
    public class ODEDynamics
    {
        public Vehicle Type
        {        
            get { return m_type; }
        }

        public IntPtr Body
        {
            get { return m_body; }
        }

        private int frcount = 0;										// Used to limit dynamics debug output to 
        																// every 100th frame

        // private OdeScene m_parentScene = null;
        private IntPtr m_body = IntPtr.Zero;
//        private IntPtr m_jointGroup = IntPtr.Zero;
//        private IntPtr m_aMotor = IntPtr.Zero;

        // Vehicle properties
        private Vehicle m_type = Vehicle.TYPE_NONE;						// If a 'VEHICLE', and what kind
        // private Quaternion m_referenceFrame = Quaternion.Identity;	// Axis modifier
        private VehicleFlag m_flags = (VehicleFlag) 0;					// Boolean settings:
																		// HOVER_TERRAIN_ONLY
																		// HOVER_GLOBAL_HEIGHT
																		// NO_DEFLECTION_UP
																		// HOVER_WATER_ONLY
																		// HOVER_UP_ONLY
																		// LIMIT_MOTOR_UP
																		// LIMIT_ROLL_ONLY
        
        // Linear properties
        private Vector3 m_linearMotorDirection = Vector3.Zero;			// (was m_linearMotorDirectionLASTSET) the (local) Velocity 
        																			//requested by LSL
        private float   m_linearMotorTimescale = 0;						// Motor Attack rate set by LSL
        private float   m_linearMotorDecayTimescale = 0;				// Motor Decay rate set by LSL
        private Vector3 m_linearFrictionTimescale = Vector3.Zero;		// General Friction set by LSL
        
		private Vector3 m_lLinMotorDVel = Vector3.Zero;					// decayed motor
		private Vector3 m_lLinObjectVel = Vector3.Zero;					// local frame object velocity
		private Vector3 m_wLinObjectVel = Vector3.Zero;					// world frame object velocity
        
        //Angular properties
        private Vector3 m_angularMotorDirection = Vector3.Zero;			// angular velocity requested by LSL motor 
        
        private float m_angularMotorTimescale = 0;						// motor angular Attack rate set by LSL
        private float m_angularMotorDecayTimescale = 0;					// motor angular Decay rate set by LSL
        private Vector3 m_angularFrictionTimescale = Vector3.Zero;		// body angular Friction set by LSL

        private Vector3 m_angularMotorDVel = Vector3.Zero;				// decayed angular motor
//        private Vector3 m_angObjectVel = Vector3.Zero;					// current body angular velocity
        private Vector3 m_lastAngularVelocity = Vector3.Zero;			// what was last applied to body

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
        private float m_VehicleBuoyancy = 0f;			// Set by VEHICLE_BUOYANCY, for a vehicle.
        			// Modifies gravity. Slider between -1 (double-gravity) and 1 (full anti-gravity) 
        			// KF: So far I have found no good method to combine a script-requested .Z velocity and gravity.
        			// Therefore only m_VehicleBuoyancy=1 (0g) will use the script-requested .Z velocity. 
        												
		//Attractor properties        												
        private float m_verticalAttractionEfficiency = 1.0f;		// damped
        private float m_verticalAttractionTimescale = 500f;			// Timescale > 300  means no vert attractor.
        
        



        internal void ProcessFloatVehicleParam(Vehicle pParam, float pValue)
        {
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
//                	if (pValue < 0f) pValue = 0f;
//                	if (pValue > 1f) pValue = 1f;
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
                    if (pValue < 0.1f) pValue = 0.1f;	// Less goes unstable
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
                    UpdateAngDecay();
                    break;
                case Vehicle.LINEAR_FRICTION_TIMESCALE:
                    m_linearFrictionTimescale = new Vector3(pValue, pValue, pValue);
                    break;
                case Vehicle.LINEAR_MOTOR_DIRECTION:
                    m_linearMotorDirection = new Vector3(pValue, pValue, pValue);
                    UpdateLinDecay();
                    break;
                case Vehicle.LINEAR_MOTOR_OFFSET:
                    // m_linearMotorOffset = new Vector3(pValue, pValue, pValue);
                    break;

            }
            
        }//end ProcessFloatVehicleParam

        internal void ProcessVectorVehicleParam(Vehicle pParam, Vector3 pValue)
        {
            switch (pParam)
            {
                case Vehicle.ANGULAR_FRICTION_TIMESCALE:
                    m_angularFrictionTimescale = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
                case Vehicle.ANGULAR_MOTOR_DIRECTION:
                    m_angularMotorDirection = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    // Limit requested angular speed to 2 rps= 4 pi rads/sec
                    if(m_angularMotorDirection.X > 12.56f) m_angularMotorDirection.X = 12.56f; 
                    if(m_angularMotorDirection.X < - 12.56f) m_angularMotorDirection.X = - 12.56f; 
                    if(m_angularMotorDirection.Y > 12.56f) m_angularMotorDirection.Y = 12.56f; 
                    if(m_angularMotorDirection.Y < - 12.56f) m_angularMotorDirection.Y = - 12.56f; 
                    if(m_angularMotorDirection.Z > 12.56f) m_angularMotorDirection.Z = 12.56f; 
                    if(m_angularMotorDirection.Z < - 12.56f) m_angularMotorDirection.Z = - 12.56f; 
                    UpdateAngDecay();
                    break;
                case Vehicle.LINEAR_FRICTION_TIMESCALE:
                    m_linearFrictionTimescale = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
                case Vehicle.LINEAR_MOTOR_DIRECTION:
                    m_linearMotorDirection = new Vector3(pValue.X, pValue.Y, pValue.Z);	// velocity requested by LSL, for max limiting
                    UpdateLinDecay();
                    break;
                case Vehicle.LINEAR_MOTOR_OFFSET:
                    // m_linearMotorOffset = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
            }
            
        }//end ProcessVectorVehicleParam

        internal void ProcessRotationVehicleParam(Vehicle pParam, Quaternion pValue)
        {
            switch (pParam)
            {
                case Vehicle.REFERENCE_FRAME:
                    // m_referenceFrame = pValue;
                    break;
            }
            
        }//end ProcessRotationVehicleParam
        
        internal void ProcessFlagsVehicleSet(int flags)
        {
        	m_flags |= (VehicleFlag)flags;
        }

        internal void ProcessFlagsVehicleRemove(int flags)
        {
        	m_flags &= ~((VehicleFlag)flags);         
        }
        
        internal void ProcessTypeChange(Vehicle pType)
        {
			// Set Defaults For Type
            m_type = pType;
            switch (pType)
            {
                case Vehicle.TYPE_SLED:
                    m_linearFrictionTimescale = new Vector3(30, 1, 1000);
                    m_angularFrictionTimescale = new Vector3(1000, 1000, 1000);
//                     m_lLinMotorVel = Vector3.Zero;
                    m_linearMotorTimescale = 1000;
                    m_linearMotorDecayTimescale = 120;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorDVel = Vector3.Zero;
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
                    m_flags &=
                        ~(VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY |
                          VehicleFlag.HOVER_GLOBAL_HEIGHT | VehicleFlag.HOVER_UP_ONLY);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.LIMIT_ROLL_ONLY | VehicleFlag.LIMIT_MOTOR_UP);
                    break;
                case Vehicle.TYPE_CAR:
                    m_linearFrictionTimescale = new Vector3(100, 2, 1000);
                    m_angularFrictionTimescale = new Vector3(1000, 1000, 1000);
//                     m_lLinMotorVel = Vector3.Zero;
                    m_linearMotorTimescale = 1;
                    m_linearMotorDecayTimescale = 60;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorDVel = Vector3.Zero;
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
                    m_flags &= ~(VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY | VehicleFlag.HOVER_GLOBAL_HEIGHT);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.LIMIT_ROLL_ONLY | VehicleFlag.HOVER_UP_ONLY |
                                VehicleFlag.LIMIT_MOTOR_UP);
                    break;
                case Vehicle.TYPE_BOAT:
                    m_linearFrictionTimescale = new Vector3(10, 3, 2);
                    m_angularFrictionTimescale = new Vector3(10,10,10);
//                     m_lLinMotorVel = Vector3.Zero;
                    m_linearMotorTimescale = 5;
                    m_linearMotorDecayTimescale = 60;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorDVel = Vector3.Zero;
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
                    m_flags &= ~(VehicleFlag.HOVER_TERRAIN_ONLY | VehicleFlag.LIMIT_ROLL_ONLY | 
                    		VehicleFlag.HOVER_GLOBAL_HEIGHT | VehicleFlag.HOVER_UP_ONLY);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.HOVER_WATER_ONLY |
                                VehicleFlag.LIMIT_MOTOR_UP);
                    break;
                case Vehicle.TYPE_AIRPLANE:
                    m_linearFrictionTimescale = new Vector3(200, 10, 5);
                    m_angularFrictionTimescale = new Vector3(20, 20, 20);
//                     m_lLinMotorVel = Vector3.Zero;
                    m_linearMotorTimescale = 2;
                    m_linearMotorDecayTimescale = 60;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorDVel = Vector3.Zero;
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
                    m_flags &= ~(VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY |
                        VehicleFlag.HOVER_GLOBAL_HEIGHT | VehicleFlag.HOVER_UP_ONLY | VehicleFlag.LIMIT_MOTOR_UP);
                    m_flags |= (VehicleFlag.LIMIT_ROLL_ONLY);
                    break;
                case Vehicle.TYPE_BALLOON:
                    m_linearFrictionTimescale = new Vector3(5, 5, 5);
                    m_angularFrictionTimescale = new Vector3(10, 10, 10);
                    m_linearMotorTimescale = 5;
                    m_linearMotorDecayTimescale = 60;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorDVel = Vector3.Zero;
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
                    m_flags &= ~(VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY |
                        VehicleFlag.HOVER_UP_ONLY | VehicleFlag.LIMIT_MOTOR_UP);
                    m_flags |= (VehicleFlag.LIMIT_ROLL_ONLY | VehicleFlag.HOVER_GLOBAL_HEIGHT);
                    break;

            }
        }//end SetDefaultsForType

        internal void Enable(IntPtr pBody, OdeScene pParentScene)
        {
            if (m_type == Vehicle.TYPE_NONE)
                return;

            m_body = pBody;
        }

        internal void Step(float pTimestep,  OdeScene pParentScene)
        {
            if (m_body == IntPtr.Zero || m_type == Vehicle.TYPE_NONE)
                return;
            frcount++;					// used to limit debug comment output
            if (frcount > 24)
                frcount = 0;

  			MoveLinear(pTimestep, pParentScene);
            MoveAngular(pTimestep);
        }// end Step

		internal void Halt()
		{	// Kill all motions, when non-physical
			m_linearMotorDirection = Vector3.Zero;
			m_lLinMotorDVel = Vector3.Zero;
			m_lLinObjectVel = Vector3.Zero;						
			m_wLinObjectVel = Vector3.Zero;
			m_angularMotorDirection = Vector3.Zero;		
			m_lastAngularVelocity = Vector3.Zero;
			m_angularMotorDVel = Vector3.Zero;	
		}
		
		private void UpdateLinDecay()
		{
			if (Math.Abs(m_linearMotorDirection.X) > Math.Abs(m_lLinMotorDVel.X)) m_lLinMotorDVel.X = m_linearMotorDirection.X;
			if (Math.Abs(m_linearMotorDirection.Y) > Math.Abs(m_lLinMotorDVel.Y)) m_lLinMotorDVel.Y = m_linearMotorDirection.Y;
			if (Math.Abs(m_linearMotorDirection.Z) > Math.Abs(m_lLinMotorDVel.Z)) m_lLinMotorDVel.Z = m_linearMotorDirection.Z;
		} // else let the motor decay on its own

        private void MoveLinear(float pTimestep, OdeScene _pParentScene)
        {
        	Vector3 acceleration = new Vector3(0f, 0f, 0f);

            d.Quaternion rot = d.BodyGetQuaternion(Body);
	        Quaternion rotq = new Quaternion(rot.X, rot.Y, rot.Z, rot.W);	// rotq = rotation of object
	        Quaternion irotq = Quaternion.Inverse(rotq);
			d.Vector3 velnow = d.BodyGetLinearVel(Body);					// this is in world frame
			Vector3 vel_now = new Vector3(velnow.X, velnow.Y, velnow.Z);
			acceleration = vel_now - m_wLinObjectVel;
	        m_lLinObjectVel = vel_now * irotq;
        	
            if (m_linearMotorDecayTimescale < 300.0f) //setting of 300 or more disables decay rate
            {
            	if ( Vector3.Mag(m_lLinMotorDVel) < 1.0f)
            	{
	           		float decayfactor = m_linearMotorDecayTimescale/pTimestep;
	            	Vector3 decayAmount = (m_lLinMotorDVel/decayfactor);
	            	m_lLinMotorDVel -= decayAmount;
				}
				else
				{
	           		float decayfactor = 3.0f - (0.57f * (float)Math.Log((double)(m_linearMotorDecayTimescale)));
					Vector3 decel = Vector3.Normalize(m_lLinMotorDVel) * decayfactor * pTimestep;
					m_lLinMotorDVel -= decel;
				}
				if (m_lLinMotorDVel.ApproxEquals(Vector3.Zero, 0.01f))
				{
					m_lLinMotorDVel = Vector3.Zero;
				}
				else
            	{
			        if (Math.Abs(m_lLinMotorDVel.X) <  Math.Abs(m_lLinObjectVel.X)) m_lLinObjectVel.X = m_lLinMotorDVel.X;
	    		    if (Math.Abs(m_lLinMotorDVel.Y) <  Math.Abs(m_lLinObjectVel.Y)) m_lLinObjectVel.Y = m_lLinMotorDVel.Y;
			        if (Math.Abs(m_lLinMotorDVel.Z) <  Math.Abs(m_lLinObjectVel.Z)) m_lLinObjectVel.Z = m_lLinMotorDVel.Z;
			    }
			}

            if ( (! m_lLinMotorDVel.ApproxEquals(Vector3.Zero, 0.01f)) || (! m_lLinObjectVel.ApproxEquals(Vector3.Zero, 0.01f)) )
            {
            	if(!d.BodyIsEnabled (Body))  d.BodyEnable (Body);
                if (m_linearMotorTimescale < 300.0f)
                {	
	                Vector3 attack_error = m_lLinMotorDVel - m_lLinObjectVel;	
	                float linfactor = m_linearMotorTimescale/pTimestep;
	                Vector3 attackAmount = (attack_error/linfactor) * 1.3f;
                	m_lLinObjectVel += attackAmount;
                }
		        if (m_linearFrictionTimescale.X < 300.0f)
		        {
			        float fricfactor = m_linearFrictionTimescale.X / pTimestep;
			        float fricX = m_lLinObjectVel.X / fricfactor;
			        m_lLinObjectVel.X -= fricX;
			    }
		        if (m_linearFrictionTimescale.Y < 300.0f)
		        {
			        float fricfactor = m_linearFrictionTimescale.Y / pTimestep;
			        float fricY = m_lLinObjectVel.Y / fricfactor;
			        m_lLinObjectVel.Y -= fricY;
			    }
		        if (m_linearFrictionTimescale.Z < 300.0f)
		        {
			        float fricfactor = m_linearFrictionTimescale.Z / pTimestep;
			        float fricZ = m_lLinObjectVel.Z / fricfactor;
			        m_lLinObjectVel.Z -= fricZ;
			    }
			}
		    m_wLinObjectVel = m_lLinObjectVel * rotq;
			// Add Gravity and Buoyancy
            Vector3 grav = Vector3.Zero;
			if(m_VehicleBuoyancy < 1.0f)
			{
				// There is some gravity, make a gravity force vector
				// that is applied after object velocity.     
	            d.Mass objMass;
	            d.BodyGetMass(Body, out objMass);
	            // m_VehicleBuoyancy: -1=2g; 0=1g; 1=0g; 
	            grav.Z = _pParentScene.gravityz * objMass.mass * (1f - m_VehicleBuoyancy); // Applied later as a force
	        } // else its 1.0, no gravity.
	        
	        // Check if hovering
	        if( (m_flags & (VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY | VehicleFlag.HOVER_GLOBAL_HEIGHT)) != 0)
	        {	
	        	// We should hover, get the target height
        		d.Vector3 pos = d.BodyGetPosition(Body);
	        	if((m_flags & VehicleFlag.HOVER_WATER_ONLY) == VehicleFlag.HOVER_WATER_ONLY)
	        	{
	        		m_VhoverTargetHeight = _pParentScene.GetWaterLevel() + m_VhoverHeight;
	        	}
	        	else if((m_flags & VehicleFlag.HOVER_TERRAIN_ONLY) == VehicleFlag.HOVER_TERRAIN_ONLY)
	        	{
	        		m_VhoverTargetHeight = _pParentScene.GetTerrainHeightAtXY(pos.X, pos.Y) + m_VhoverHeight;
	        	}
	        	else if((m_flags & VehicleFlag.HOVER_GLOBAL_HEIGHT) == VehicleFlag.HOVER_GLOBAL_HEIGHT)
	        	{
	        		m_VhoverTargetHeight = m_VhoverHeight;
	        	}
	        	
				if((m_flags & VehicleFlag.HOVER_UP_ONLY) == VehicleFlag.HOVER_UP_ONLY)
				{
					// If body is aready heigher, use its height as target height
					if(pos.Z > m_VhoverTargetHeight) m_VhoverTargetHeight = pos.Z;
				}
				
//	            m_VhoverEfficiency = 0f;	// 0=boucy, 1=Crit.damped
//				m_VhoverTimescale = 0f;		// time to acheive height
//				pTimestep  is time since last frame,in secs 
				float herr0 = pos.Z - m_VhoverTargetHeight;
				// Replace Vertical speed with correction figure if significant
				if(Math.Abs(herr0) > 0.01f )
				{
		            d.Mass objMass;
		            d.BodyGetMass(Body, out objMass);
					m_wLinObjectVel.Z = - ( (herr0 * pTimestep * 50.0f) / m_VhoverTimescale);
					//KF: m_VhoverEfficiency is not yet implemented
				}
				else
				{
					m_wLinObjectVel.Z = 0f;
				}
			}
			else
			{	// not hovering, Gravity rules
				m_wLinObjectVel.Z = vel_now.Z;
//if(frcount == 0) Console.WriteLine(" Z  {0}      a.Z  {1}", m_wLinObjectVel.Z,	acceleration.Z);			
	        }	
	        // Apply velocity
	        d.BodySetLinearVel(Body, m_wLinObjectVel.X, m_wLinObjectVel.Y, m_wLinObjectVel.Z);        
            // apply gravity force
			d.BodyAddForce(Body, grav.X, grav.Y, grav.Z);		
//if(frcount == 0) Console.WriteLine("Grav {0}", grav);
        } // end MoveLinear()

		private void UpdateAngDecay()
		{
			if (Math.Abs(m_angularMotorDirection.X) > Math.Abs(m_angularMotorDVel.X)) m_angularMotorDVel.X = m_angularMotorDirection.X;
			if (Math.Abs(m_angularMotorDirection.Y) > Math.Abs(m_angularMotorDVel.Y)) m_angularMotorDVel.Y = m_angularMotorDirection.Y;
			if (Math.Abs(m_angularMotorDirection.Z) > Math.Abs(m_angularMotorDVel.Z)) m_angularMotorDVel.Z = m_angularMotorDirection.Z;
		} // else let the motor decay on its own
        
        private void MoveAngular(float pTimestep)
        {
	        /*
        private Vector3 m_angularMotorDirection = Vector3.Zero;			// angular velocity requested by LSL motor 
        
        private float m_angularMotorTimescale = 0;						// motor angular Attack rate set by LSL
        private float m_angularMotorDecayTimescale = 0;					// motor angular Decay rate set by LSL
        private Vector3 m_angularFrictionTimescale = Vector3.Zero;		// body angular Friction set by LSL

        private Vector3 m_angularMotorDVel = Vector3.Zero;				// decayed angular motor
        private Vector3 m_angObjectVel = Vector3.Zero;					// what was last applied to body
			*/
//if(frcount == 0) Console.WriteLine("MoveAngular ");	
        
//#### 
        	// Get what the body is doing, this includes 'external' influences
        	d.Vector3 angularObjectVel = d.BodyGetAngularVel(Body);
        	Vector3 angObjectVel = new Vector3(angularObjectVel.X, angularObjectVel.Y, angularObjectVel.Z);
//if(frcount == 0) Console.WriteLine("V0 = {0}", angObjectVel);        	
//   	Vector3 FrAaccel = m_lastAngularVelocity - angObjectVel;
//    Vector3 initavel =  angObjectVel;       	
        	// Decay Angular Motor 1. In SL this also depends on attack rate! decay ~= 23/Attack.
        	float atk_decayfactor = 23.0f  / (m_angularMotorTimescale * pTimestep); 
        	m_angularMotorDVel -= m_angularMotorDVel / atk_decayfactor;
        	// Decay Angular Motor 2.
        	if (m_angularMotorDecayTimescale < 300.0f)
        	{
				float decayfactor = m_angularMotorDecayTimescale/pTimestep;			// df = Dec / pts
	            Vector3 decayAmount = (m_angularMotorDVel/decayfactor);				// v-da = v-Dvel / df  = v-Dvel * pts / Dec
	            m_angularMotorDVel -= decayAmount;        							// v-Dvel = v-Dvel - (v-Dvel / df  = v-Dvel * pts / Dec)
        	
				if (m_angularMotorDVel.ApproxEquals(Vector3.Zero, 0.01f))
				{
					m_angularMotorDVel = Vector3.Zero;
				}
				else
	           	{
			        if (Math.Abs(m_angularMotorDVel.X) <  Math.Abs(angObjectVel.X)) angObjectVel.X = m_angularMotorDVel.X;
	    		    if (Math.Abs(m_angularMotorDVel.Y) <  Math.Abs(angObjectVel.Y)) angObjectVel.Y = m_angularMotorDVel.Y;
			        if (Math.Abs(m_angularMotorDVel.Z) <  Math.Abs(angObjectVel.Z)) angObjectVel.Z = m_angularMotorDVel.Z;
			    }        	
        	} // end decay angular motor
//if(frcount == 0) Console.WriteLine("MotorDvel {0}    Obj {1}", m_angularMotorDVel, angObjectVel);

//if(frcount == 0) Console.WriteLine("VA = {0}", angObjectVel);   
            // Vertical attractor section
			Vector3 vertattr = Vector3.Zero;
            
			if(m_verticalAttractionTimescale < 300)
			{
	            float VAservo = 1.0f / (m_verticalAttractionTimescale * pTimestep);
	    	    // get present body rotation
	    	    d.Quaternion rot = d.BodyGetQuaternion(Body);
	    	    Quaternion rotq = new Quaternion(rot.X, rot.Y, rot.Z, rot.W);
	    	    // make a vector pointing up
				Vector3 verterr = Vector3.Zero;
				verterr.Z = 1.0f;
				// rotate it to Body Angle
				verterr = verterr * rotq;
				// verterr.X and .Y are the World error ammounts. They are 0 when there is no error (Vehicle Body is 'vertical'), and .Z will be 1.
				// As the body leans to its side |.X| will increase to 1 and .Z fall to 0. As body inverts |.X| will fall and .Z will go
				// negative. Similar for tilt and |.Y|. .X and .Y must be modulated to prevent a stable inverted body.
				
				if (verterr.Z < 0.0f)
				{	// Defelction from vertical exceeds 90-degrees. This method will ensure stable return to
					// vertical, BUT for some reason a z-rotation is imparted to the object. TBI.
//Console.WriteLine("InvertFlip");	
					verterr.X = 2.0f - verterr.X;
					verterr.Y = 2.0f - verterr.Y;
				}
				verterr *= 0.5f;
				// verterror is 0 (no error) to +/- 1 (max error at 180-deg tilt)
	
				if ((!angObjectVel.ApproxEquals(Vector3.Zero, 0.001f)) || (verterr.Z < 0.49f))
				{
//if(frcount == 0) 
					// As the body rotates around the X axis, then verterr.Y increases; Rotated around Y then .X increases, so 
					// Change  Body angular velocity  X based on Y, and Y based on X. Z is not changed.
					vertattr.X =    verterr.Y;
					vertattr.Y =  - verterr.X;
					vertattr.Z = 0f;
//if(frcount == 0) Console.WriteLine("VAerr=" + verterr);	
			
					// scaling appears better usingsquare-law
					float damped = m_verticalAttractionEfficiency * m_verticalAttractionEfficiency;
					float bounce = 1.0f - damped;  
					// 0 = crit damp, 1 = bouncy
					float oavz = angObjectVel.Z;   // retain z velocity
					angObjectVel = (angObjectVel + (vertattr * VAservo * 0.0333f)) * bounce; // The time-scaled correction, which sums, therefore is bouncy
					angObjectVel = angObjectVel + (vertattr * VAservo *  0.0667f * damped); // damped, good @ < 90.
					angObjectVel.Z = oavz;
//if(frcount == 0) Console.WriteLine("VA+");					
//Console.WriteLine("VAttr {0}         OAvel {1}", vertattr, angObjectVel);
				}
				else
				{
					// else error is very small
					angObjectVel.X = 0f;
					angObjectVel.Y = 0f;
//if(frcount == 0) Console.WriteLine("VA0");					
				}
			} // else vertical attractor is off
//if(frcount == 0) Console.WriteLine("V1 = {0}", angObjectVel);        	

            if ( (! m_angularMotorDVel.ApproxEquals(Vector3.Zero, 0.01f)) || (! angObjectVel.ApproxEquals(Vector3.Zero, 0.01f)) )
            {  // if motor or object have motion
            	if(!d.BodyIsEnabled (Body))  d.BodyEnable (Body);
            	
                if (m_angularMotorTimescale < 300.0f)
                {	
	                Vector3 attack_error = m_angularMotorDVel - angObjectVel;	
	                float angfactor = m_angularMotorTimescale/pTimestep;
	                Vector3 attackAmount = (attack_error/angfactor);
                	angObjectVel += attackAmount;
//if(frcount == 0) Console.WriteLine("Accel {0}      Attk {1}",FrAaccel, attackAmount);                	
//if(frcount == 0) Console.WriteLine("V2+= {0}", angObjectVel);        	
                }
                
		        if (m_angularFrictionTimescale.X < 300.0f)
		        {
			        float fricfactor = m_angularFrictionTimescale.X / pTimestep;
			        angObjectVel.X -= angObjectVel.X / fricfactor;
			    }
		        if (m_angularFrictionTimescale.Y < 300.0f)
		        {
			        float fricfactor = m_angularFrictionTimescale.Y / pTimestep;
			        angObjectVel.Y -= angObjectVel.Y / fricfactor;
			    }
		        if (m_angularFrictionTimescale.Z < 300.0f)
		        {
			        float fricfactor = m_angularFrictionTimescale.Z / pTimestep;
			        angObjectVel.Z -= angObjectVel.Z / fricfactor;
			        Console.WriteLine("z fric");
			    }       	
			} // else no signif. motion
			
//if(frcount == 0) Console.WriteLine("Dmotor {0}      Obj {1}", m_angularMotorDVel, angObjectVel);
			// Bank section tba
			// Deflection section tba
//if(frcount == 0) Console.WriteLine("V3 = {0}", angObjectVel);        	
			
			m_lastAngularVelocity = angObjectVel;
/*			
        	if (!m_lastAngularVelocity.ApproxEquals(Vector3.Zero, 0.0001f))
            {
				if(!d.BodyIsEnabled (Body))  d.BodyEnable (Body);
			}
			else
			{
				m_lastAngularVelocity = Vector3.Zero; // Reduce small value to zero.
			}
	*/		
			// Apply to the body
// Vector3 aInc = m_lastAngularVelocity - initavel;
//if(frcount == 0) Console.WriteLine("Inc {0}", aInc);			
			d.BodySetAngularVel (Body, m_lastAngularVelocity.X, m_lastAngularVelocity.Y, m_lastAngularVelocity.Z);
//if(frcount == 0) Console.WriteLine("V4 = {0}", m_lastAngularVelocity);        	
				
	    } //end MoveAngular
	}
}
