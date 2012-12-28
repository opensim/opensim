/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyrightD
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
using System.Text;

using OpenSim.Region.Physics.Manager;

using OpenMetaverse;
using Nini.Config;

namespace OpenSim.Region.Physics.BulletSPlugin
{
public static class BSParam
{
    // Level of Detail values kept as float because that's what the Meshmerizer wants
    public static float MeshLOD { get; private set; }
    public static float MeshMegaPrimLOD { get; private set; }
    public static float MeshMegaPrimThreshold { get; private set; }
    public static float SculptLOD { get; private set; }

    public static float MinimumObjectMass { get; private set; }
    public static float MaximumObjectMass { get; private set; }

    public static float LinearDamping { get; private set; }
    public static float AngularDamping { get; private set; }
    public static float DeactivationTime { get; private set; }
    public static float LinearSleepingThreshold { get; private set; }
    public static float AngularSleepingThreshold { get; private set; }
	public static float CcdMotionThreshold { get; private set; }
	public static float CcdSweptSphereRadius { get; private set; }
    public static float ContactProcessingThreshold { get; private set; }

    public static bool ShouldMeshSculptedPrim { get; private set; }   // cause scuplted prims to get meshed
    public static bool ShouldForceSimplePrimMeshing { get; private set; }   // if a cube or sphere, let Bullet do internal shapes
    public static bool ShouldUseHullsForPhysicalObjects { get; private set; }   // 'true' if should create hulls for physical objects

    public static float TerrainImplementation { get; private set; }
    public static float TerrainFriction { get; private set; }
    public static float TerrainHitFraction { get; private set; }
    public static float TerrainRestitution { get; private set; }
    public static float TerrainCollisionMargin { get; private set; }

    // Avatar parameters
    public static float AvatarFriction { get; private set; }
    public static float AvatarStandingFriction { get; private set; }
    public static float AvatarAlwaysRunFactor { get; private set; }
    public static float AvatarDensity { get; private set; }
    public static float AvatarRestitution { get; private set; }
    public static float AvatarCapsuleWidth { get; private set; }
    public static float AvatarCapsuleDepth { get; private set; }
    public static float AvatarCapsuleHeight { get; private set; }
	public static float AvatarContactProcessingThreshold { get; private set; }

    public static float VehicleAngularDamping { get; private set; }

    public static float LinksetImplementation { get; private set; }
    public static float LinkConstraintUseFrameOffset { get; private set; }
    public static float LinkConstraintEnableTransMotor { get; private set; }
    public static float LinkConstraintTransMotorMaxVel { get; private set; }
    public static float LinkConstraintTransMotorMaxForce { get; private set; }
    public static float LinkConstraintERP { get; private set; }
    public static float LinkConstraintCFM { get; private set; }
    public static float LinkConstraintSolverIterations { get; private set; }

    public static float PID_D { get; private set; }    // derivative
    public static float PID_P { get; private set; }    // proportional

    // Various constants that come from that other virtual world that shall not be named
    public const float MinGravityZ = -1f;
    public const float MaxGravityZ = 28f;
    public const float MinFriction = 0f;
    public const float MaxFriction = 255f;
    public const float MinDensity = 0f;
    public const float MaxDensity = 22587f;
    public const float MinRestitution = 0f;
    public const float MaxRestitution = 1f;
    public const float MaxAddForceMagnitude = 20000f;

    // ===========================================================================
    public delegate void ParamUser(BSScene scene, IConfig conf, string paramName, float val);
    public delegate float ParamGet(BSScene scene);
    public delegate void ParamSet(BSScene scene, string paramName, uint localID, float val);
    public delegate void SetOnObject(BSScene scene, BSPhysObject obj, float val);

    public struct ParameterDefn
    {
        public string name;         // string name of the parameter
        public string desc;         // a short description of what the parameter means
        public float defaultValue;  // default value if not specified anywhere else
        public ParamUser userParam; // get the value from the configuration file
        public ParamGet getter;     // return the current value stored for this parameter
        public ParamSet setter;     // set the current value for this parameter
        public SetOnObject onObject;    // set the value on an object in the physical domain
        public ParameterDefn(string n, string d, float v, ParamUser u, ParamGet g, ParamSet s)
        {
            name = n;
            desc = d;
            defaultValue = v;
            userParam = u;
            getter = g;
            setter = s;
            onObject = null;
        }
        public ParameterDefn(string n, string d, float v, ParamUser u, ParamGet g, ParamSet s, SetOnObject o)
        {
            name = n;
            desc = d;
            defaultValue = v;
            userParam = u;
            getter = g;
            setter = s;
            onObject = o;
        }
    }

    // List of all of the externally visible parameters.
    // For each parameter, this table maps a text name to getter and setters.
    // To add a new externally referencable/settable parameter, add the paramter storage
    //    location somewhere in the program and make an entry in this table with the
    //    getters and setters.
    // It is easiest to find an existing definition and copy it.
    // Parameter values are floats. Booleans are converted to a floating value.
    //
    // A ParameterDefn() takes the following parameters:
    //    -- the text name of the parameter. This is used for console input and ini file.
    //    -- a short text description of the parameter. This shows up in the console listing.
    //    -- a default value (float)
    //    -- a delegate for fetching the parameter from the ini file.
    //          Should handle fetching the right type from the ini file and converting it.
    //    -- a delegate for getting the value as a float
    //    -- a delegate for setting the value from a float
    //    -- an optional delegate to update the value in the world. Most often used to
    //          push the new value to an in-world object.
    //
    // The single letter parameters for the delegates are:
    //    s = BSScene
    //    o = BSPhysObject
    //    p = string parameter name
    //    l = localID of referenced object
    //    v = value (float)
    //    cf = parameter configuration class (for fetching values from ini file)
    private static ParameterDefn[] ParameterDefinitions =
    {
        new ParameterDefn("MeshSculptedPrim", "Whether to create meshes for sculpties",
            ConfigurationParameters.numericTrue,
            (s,cf,p,v) => { ShouldMeshSculptedPrim = cf.GetBoolean(p, BSParam.BoolNumeric(v)); },
            (s) => { return BSParam.NumericBool(ShouldMeshSculptedPrim); },
            (s,p,l,v) => { ShouldMeshSculptedPrim = BSParam.BoolNumeric(v); } ),
        new ParameterDefn("ForceSimplePrimMeshing", "If true, only use primitive meshes for objects",
            ConfigurationParameters.numericFalse,
            (s,cf,p,v) => { ShouldForceSimplePrimMeshing = cf.GetBoolean(p, BSParam.BoolNumeric(v)); },
            (s) => { return BSParam.NumericBool(ShouldForceSimplePrimMeshing); },
            (s,p,l,v) => { ShouldForceSimplePrimMeshing = BSParam.BoolNumeric(v); } ),
        new ParameterDefn("UseHullsForPhysicalObjects", "If true, create hulls for physical objects",
            ConfigurationParameters.numericTrue,
            (s,cf,p,v) => { ShouldUseHullsForPhysicalObjects = cf.GetBoolean(p, BSParam.BoolNumeric(v)); },
            (s) => { return BSParam.NumericBool(ShouldUseHullsForPhysicalObjects); },
            (s,p,l,v) => { ShouldUseHullsForPhysicalObjects = BSParam.BoolNumeric(v); } ),

        new ParameterDefn("MeshLevelOfDetail", "Level of detail to render meshes (32, 16, 8 or 4. 32=most detailed)",
            8f,
            (s,cf,p,v) => { MeshLOD = (float)cf.GetInt(p, (int)v); },
            (s) => { return MeshLOD; },
            (s,p,l,v) => { MeshLOD = v; } ),
        new ParameterDefn("MeshLevelOfDetailMegaPrim", "Level of detail to render meshes larger than threshold meters",
            16f,
            (s,cf,p,v) => { MeshMegaPrimLOD = (float)cf.GetInt(p, (int)v); },
            (s) => { return MeshMegaPrimLOD; },
            (s,p,l,v) => { MeshMegaPrimLOD = v; } ),
        new ParameterDefn("MeshLevelOfDetailMegaPrimThreshold", "Size (in meters) of a mesh before using MeshMegaPrimLOD",
            10f,
            (s,cf,p,v) => { MeshMegaPrimThreshold = (float)cf.GetInt(p, (int)v); },
            (s) => { return MeshMegaPrimThreshold; },
            (s,p,l,v) => { MeshMegaPrimThreshold = v; } ),
        new ParameterDefn("SculptLevelOfDetail", "Level of detail to render sculpties (32, 16, 8 or 4. 32=most detailed)",
            32f,
            (s,cf,p,v) => { SculptLOD = (float)cf.GetInt(p, (int)v); },
            (s) => { return SculptLOD; },
            (s,p,l,v) => { SculptLOD = v; } ),

        new ParameterDefn("MaxSubStep", "In simulation step, maximum number of substeps",
            10f,
            (s,cf,p,v) => { s.m_maxSubSteps = cf.GetInt(p, (int)v); },
            (s) => { return (float)s.m_maxSubSteps; },
            (s,p,l,v) => { s.m_maxSubSteps = (int)v; } ),
        new ParameterDefn("FixedTimeStep", "In simulation step, seconds of one substep (1/60)",
            1f / 60f,
            (s,cf,p,v) => { s.m_fixedTimeStep = cf.GetFloat(p, v); },
            (s) => { return (float)s.m_fixedTimeStep; },
            (s,p,l,v) => { s.m_fixedTimeStep = v; } ),
        new ParameterDefn("NominalFrameRate", "The base frame rate we claim",
            55f,
            (s,cf,p,v) => { s.NominalFrameRate = cf.GetInt(p, (int)v); },
            (s) => { return (float)s.NominalFrameRate; },
            (s,p,l,v) => { s.NominalFrameRate = (int)v; } ),
        new ParameterDefn("MaxCollisionsPerFrame", "Max collisions returned at end of each frame",
            2048f,
            (s,cf,p,v) => { s.m_maxCollisionsPerFrame = cf.GetInt(p, (int)v); },
            (s) => { return (float)s.m_maxCollisionsPerFrame; },
            (s,p,l,v) => { s.m_maxCollisionsPerFrame = (int)v; } ),
        new ParameterDefn("MaxUpdatesPerFrame", "Max updates returned at end of each frame",
            8000f,
            (s,cf,p,v) => { s.m_maxUpdatesPerFrame = cf.GetInt(p, (int)v); },
            (s) => { return (float)s.m_maxUpdatesPerFrame; },
            (s,p,l,v) => { s.m_maxUpdatesPerFrame = (int)v; } ),
        new ParameterDefn("MaxTaintsToProcessPerStep", "Number of update taints to process before each simulation step",
            500f,
            (s,cf,p,v) => { s.m_taintsToProcessPerStep = cf.GetInt(p, (int)v); },
            (s) => { return (float)s.m_taintsToProcessPerStep; },
            (s,p,l,v) => { s.m_taintsToProcessPerStep = (int)v; } ),
        new ParameterDefn("MinObjectMass", "Minimum object mass (0.0001)",
            0.0001f,
            (s,cf,p,v) => { MinimumObjectMass = cf.GetFloat(p, v); },
            (s) => { return (float)MinimumObjectMass; },
            (s,p,l,v) => { MinimumObjectMass = v; } ),
        new ParameterDefn("MaxObjectMass", "Maximum object mass (10000.01)",
            10000.01f,
            (s,cf,p,v) => { MaximumObjectMass = cf.GetFloat(p, v); },
            (s) => { return (float)MaximumObjectMass; },
            (s,p,l,v) => { MaximumObjectMass = v; } ),

        new ParameterDefn("PID_D", "Derivitive factor for motion smoothing",
            2200f,
            (s,cf,p,v) => { PID_D = cf.GetFloat(p, v); },
            (s) => { return (float)PID_D; },
            (s,p,l,v) => { PID_D = v; } ),
        new ParameterDefn("PID_P", "Parameteric factor for motion smoothing",
            900f,
            (s,cf,p,v) => { PID_P = cf.GetFloat(p, v); },
            (s) => { return (float)PID_P; },
            (s,p,l,v) => { PID_P = v; } ),

        new ParameterDefn("DefaultFriction", "Friction factor used on new objects",
            0.2f,
            (s,cf,p,v) => { s.UnmanagedParams[0].defaultFriction = cf.GetFloat(p, v); },
            (s) => { return s.UnmanagedParams[0].defaultFriction; },
            (s,p,l,v) => { s.UnmanagedParams[0].defaultFriction = v; } ),
        new ParameterDefn("DefaultDensity", "Density for new objects" ,
            10.000006836f,  // Aluminum g/cm3
            (s,cf,p,v) => { s.UnmanagedParams[0].defaultDensity = cf.GetFloat(p, v); },
            (s) => { return s.UnmanagedParams[0].defaultDensity; },
            (s,p,l,v) => { s.UnmanagedParams[0].defaultDensity = v; } ),
        new ParameterDefn("DefaultRestitution", "Bouncyness of an object" ,
            0f,
            (s,cf,p,v) => { s.UnmanagedParams[0].defaultRestitution = cf.GetFloat(p, v); },
            (s) => { return s.UnmanagedParams[0].defaultRestitution; },
            (s,p,l,v) => { s.UnmanagedParams[0].defaultRestitution = v; } ),
        new ParameterDefn("CollisionMargin", "Margin around objects before collisions are calculated (must be zero!)",
            0.04f,
            (s,cf,p,v) => { s.UnmanagedParams[0].collisionMargin = cf.GetFloat(p, v); },
            (s) => { return s.UnmanagedParams[0].collisionMargin; },
            (s,p,l,v) => { s.UnmanagedParams[0].collisionMargin = v; } ),
        new ParameterDefn("Gravity", "Vertical force of gravity (negative means down)",
            -9.80665f,
            (s,cf,p,v) => { s.UnmanagedParams[0].gravity = cf.GetFloat(p, v); },
            (s) => { return s.UnmanagedParams[0].gravity; },
            (s,p,l,v) => { s.UpdateParameterObject((x)=>{s.UnmanagedParams[0].gravity=x;}, p, PhysParameterEntry.APPLY_TO_NONE, v); },
            (s,o,v) => { BulletSimAPI.SetGravity2(s.World.ptr, new Vector3(0f,0f,v)); } ),


        new ParameterDefn("LinearDamping", "Factor to damp linear movement per second (0.0 - 1.0)",
            0f,
            (s,cf,p,v) => { LinearDamping = cf.GetFloat(p, v); },
            (s) => { return LinearDamping; },
            (s,p,l,v) => { s.UpdateParameterObject((x)=>{LinearDamping=x;}, p, l, v); },
            (s,o,v) => { BulletSimAPI.SetDamping2(o.PhysBody.ptr, v, AngularDamping); } ),
        new ParameterDefn("AngularDamping", "Factor to damp angular movement per second (0.0 - 1.0)",
            0f,
            (s,cf,p,v) => { AngularDamping = cf.GetFloat(p, v); },
            (s) => { return AngularDamping; },
            (s,p,l,v) => { s.UpdateParameterObject((x)=>{AngularDamping=x;}, p, l, v); },
            (s,o,v) => { BulletSimAPI.SetDamping2(o.PhysBody.ptr, LinearDamping, v); } ),
        new ParameterDefn("DeactivationTime", "Seconds before considering an object potentially static",
            0.2f,
            (s,cf,p,v) => { DeactivationTime = cf.GetFloat(p, v); },
            (s) => { return DeactivationTime; },
            (s,p,l,v) => { s.UpdateParameterObject((x)=>{DeactivationTime=x;}, p, l, v); },
            (s,o,v) => { BulletSimAPI.SetDeactivationTime2(o.PhysBody.ptr, v); } ),
        new ParameterDefn("LinearSleepingThreshold", "Seconds to measure linear movement before considering static",
            0.8f,
            (s,cf,p,v) => { LinearSleepingThreshold = cf.GetFloat(p, v); },
            (s) => { return LinearSleepingThreshold; },
            (s,p,l,v) => { s.UpdateParameterObject((x)=>{LinearSleepingThreshold=x;}, p, l, v); },
            (s,o,v) => { BulletSimAPI.SetSleepingThresholds2(o.PhysBody.ptr, v, v); } ),
        new ParameterDefn("AngularSleepingThreshold", "Seconds to measure angular movement before considering static",
            1.0f,
            (s,cf,p,v) => { AngularSleepingThreshold = cf.GetFloat(p, v); },
            (s) => { return AngularSleepingThreshold; },
            (s,p,l,v) => { s.UpdateParameterObject((x)=>{AngularSleepingThreshold=x;}, p, l, v); },
            (s,o,v) => { BulletSimAPI.SetSleepingThresholds2(o.PhysBody.ptr, v, v); } ),
        new ParameterDefn("CcdMotionThreshold", "Continuious collision detection threshold (0 means no CCD)" ,
            0f,     // set to zero to disable
            (s,cf,p,v) => { CcdMotionThreshold = cf.GetFloat(p, v); },
            (s) => { return CcdMotionThreshold; },
            (s,p,l,v) => { s.UpdateParameterObject((x)=>{CcdMotionThreshold=x;}, p, l, v); },
            (s,o,v) => { BulletSimAPI.SetCcdMotionThreshold2(o.PhysBody.ptr, v); } ),
        new ParameterDefn("CcdSweptSphereRadius", "Continuious collision detection test radius" ,
            0f,
            (s,cf,p,v) => { CcdSweptSphereRadius = cf.GetFloat(p, v); },
            (s) => { return CcdSweptSphereRadius; },
            (s,p,l,v) => { s.UpdateParameterObject((x)=>{CcdSweptSphereRadius=x;}, p, l, v); },
            (s,o,v) => { BulletSimAPI.SetCcdSweptSphereRadius2(o.PhysBody.ptr, v); } ),
        new ParameterDefn("ContactProcessingThreshold", "Distance between contacts before doing collision check" ,
            0.1f,
            (s,cf,p,v) => { ContactProcessingThreshold = cf.GetFloat(p, v); },
            (s) => { return ContactProcessingThreshold; },
            (s,p,l,v) => { s.UpdateParameterObject((x)=>{ContactProcessingThreshold=x;}, p, l, v); },
            (s,o,v) => { BulletSimAPI.SetContactProcessingThreshold2(o.PhysBody.ptr, v); } ),

	    new ParameterDefn("TerrainImplementation", "Type of shape to use for terrain (0=heightmap, 1=mesh)",
            (float)BSTerrainPhys.TerrainImplementation.Mesh,
            (s,cf,p,v) => { TerrainImplementation = cf.GetFloat(p,v); },
            (s) => { return TerrainImplementation; },
            (s,p,l,v) => { TerrainImplementation = v; } ),
        new ParameterDefn("TerrainFriction", "Factor to reduce movement against terrain surface" ,
            0.3f,
            (s,cf,p,v) => { TerrainFriction = cf.GetFloat(p, v); },
            (s) => { return TerrainFriction; },
            (s,p,l,v) => { TerrainFriction = v;  /* TODO: set on real terrain */} ),
        new ParameterDefn("TerrainHitFraction", "Distance to measure hit collisions" ,
            0.8f,
            (s,cf,p,v) => { TerrainHitFraction = cf.GetFloat(p, v); },
            (s) => { return TerrainHitFraction; },
            (s,p,l,v) => { TerrainHitFraction = v; /* TODO: set on real terrain */ } ),
        new ParameterDefn("TerrainRestitution", "Bouncyness" ,
            0f,
            (s,cf,p,v) => { TerrainRestitution = cf.GetFloat(p, v); },
            (s) => { return TerrainRestitution; },
            (s,p,l,v) => { TerrainRestitution = v;  /* TODO: set on real terrain */ } ),
        new ParameterDefn("TerrainCollisionMargin", "Margin where collision checking starts" ,
            0.04f,
            (s,cf,p,v) => { TerrainCollisionMargin = cf.GetFloat(p, v); },
            (s) => { return TerrainCollisionMargin; },
            (s,p,l,v) => { TerrainCollisionMargin = v;  /* TODO: set on real terrain */ } ),

        new ParameterDefn("AvatarFriction", "Factor to reduce movement against an avatar. Changed on avatar recreation.",
            0.2f,
            (s,cf,p,v) => { AvatarFriction = cf.GetFloat(p, v); },
            (s) => { return AvatarFriction; },
            (s,p,l,v) => { s.UpdateParameterObject((x)=>{AvatarFriction=x;}, p, l, v); } ),
        new ParameterDefn("AvatarStandingFriction", "Avatar friction when standing. Changed on avatar recreation.",
            10.0f,
            (s,cf,p,v) => { AvatarStandingFriction = cf.GetFloat(p, v); },
            (s) => { return AvatarStandingFriction; },
            (s,p,l,v) => { AvatarStandingFriction = v; } ),
        new ParameterDefn("AvatarAlwaysRunFactor", "Speed multiplier if avatar is set to always run",
            1.3f,
            (s,cf,p,v) => { AvatarAlwaysRunFactor = cf.GetFloat(p, v); },
            (s) => { return AvatarAlwaysRunFactor; },
            (s,p,l,v) => { AvatarAlwaysRunFactor = v; } ),
        new ParameterDefn("AvatarDensity", "Density of an avatar. Changed on avatar recreation.",
            3.5f,
            (s,cf,p,v) => { AvatarDensity = cf.GetFloat(p, v); },
            (s) => { return AvatarDensity; },
            (s,p,l,v) => { s.UpdateParameterObject((x)=>{AvatarDensity=x;}, p, l, v); } ),
        new ParameterDefn("AvatarRestitution", "Bouncyness. Changed on avatar recreation.",
            0f,
            (s,cf,p,v) => { AvatarRestitution = cf.GetFloat(p, v); },
            (s) => { return AvatarRestitution; },
            (s,p,l,v) => { s.UpdateParameterObject((x)=>{AvatarRestitution=x;}, p, l, v); } ),
        new ParameterDefn("AvatarCapsuleWidth", "The distance between the sides of the avatar capsule",
            0.6f,
            (s,cf,p,v) => { AvatarCapsuleWidth = cf.GetFloat(p, v); },
            (s) => { return AvatarCapsuleWidth; },
            (s,p,l,v) => { s.UpdateParameterObject((x)=>{AvatarCapsuleWidth=x;}, p, l, v); } ),
        new ParameterDefn("AvatarCapsuleDepth", "The distance between the front and back of the avatar capsule",
            0.45f,
            (s,cf,p,v) => { AvatarCapsuleDepth = cf.GetFloat(p, v); },
            (s) => { return AvatarCapsuleDepth; },
            (s,p,l,v) => { s.UpdateParameterObject((x)=>{AvatarCapsuleDepth=x;}, p, l, v); } ),
        new ParameterDefn("AvatarCapsuleHeight", "Default height of space around avatar",
            1.5f,
            (s,cf,p,v) => { AvatarCapsuleHeight = cf.GetFloat(p, v); },
            (s) => { return AvatarCapsuleHeight; },
            (s,p,l,v) => { s.UpdateParameterObject((x)=>{AvatarCapsuleHeight=x;}, p, l, v); } ),
	    new ParameterDefn("AvatarContactProcessingThreshold", "Distance from capsule to check for collisions",
            0.1f,
            (s,cf,p,v) => { AvatarContactProcessingThreshold = cf.GetFloat(p, v); },
            (s) => { return AvatarContactProcessingThreshold; },
            (s,p,l,v) => { s.UpdateParameterObject((x)=>{AvatarContactProcessingThreshold=x;}, p, l, v); } ),

        new ParameterDefn("VehicleAngularDamping", "Factor to damp vehicle angular movement per second (0.0 - 1.0)",
            0.95f,
            (s,cf,p,v) => { VehicleAngularDamping = cf.GetFloat(p, v); },
            (s) => { return VehicleAngularDamping; },
            (s,p,l,v) => { VehicleAngularDamping = v; } ),

	    new ParameterDefn("MaxPersistantManifoldPoolSize", "Number of manifolds pooled (0 means default of 4096)",
            0f,
            (s,cf,p,v) => { s.UnmanagedParams[0].maxPersistantManifoldPoolSize = cf.GetFloat(p, v); },
            (s) => { return s.UnmanagedParams[0].maxPersistantManifoldPoolSize; },
            (s,p,l,v) => { s.UnmanagedParams[0].maxPersistantManifoldPoolSize = v; } ),
	    new ParameterDefn("MaxCollisionAlgorithmPoolSize", "Number of collisions pooled (0 means default of 4096)",
            0f,
            (s,cf,p,v) => { s.UnmanagedParams[0].maxCollisionAlgorithmPoolSize = cf.GetFloat(p, v); },
            (s) => { return s.UnmanagedParams[0].maxCollisionAlgorithmPoolSize; },
            (s,p,l,v) => { s.UnmanagedParams[0].maxCollisionAlgorithmPoolSize = v; } ),
	    new ParameterDefn("ShouldDisableContactPoolDynamicAllocation", "Enable to allow large changes in object count",
            ConfigurationParameters.numericFalse,
            (s,cf,p,v) => { s.UnmanagedParams[0].shouldDisableContactPoolDynamicAllocation = BSParam.NumericBool(cf.GetBoolean(p, BSParam.BoolNumeric(v))); },
            (s) => { return s.UnmanagedParams[0].shouldDisableContactPoolDynamicAllocation; },
            (s,p,l,v) => { s.UnmanagedParams[0].shouldDisableContactPoolDynamicAllocation = v; } ),
	    new ParameterDefn("ShouldForceUpdateAllAabbs", "Enable to recomputer AABBs every simulator step",
            ConfigurationParameters.numericFalse,
            (s,cf,p,v) => { s.UnmanagedParams[0].shouldForceUpdateAllAabbs = BSParam.NumericBool(cf.GetBoolean(p, BSParam.BoolNumeric(v))); },
            (s) => { return s.UnmanagedParams[0].shouldForceUpdateAllAabbs; },
            (s,p,l,v) => { s.UnmanagedParams[0].shouldForceUpdateAllAabbs = v; } ),
	    new ParameterDefn("ShouldRandomizeSolverOrder", "Enable for slightly better stacking interaction",
            ConfigurationParameters.numericTrue,
            (s,cf,p,v) => { s.UnmanagedParams[0].shouldRandomizeSolverOrder = BSParam.NumericBool(cf.GetBoolean(p, BSParam.BoolNumeric(v))); },
            (s) => { return s.UnmanagedParams[0].shouldRandomizeSolverOrder; },
            (s,p,l,v) => { s.UnmanagedParams[0].shouldRandomizeSolverOrder = v; } ),
	    new ParameterDefn("ShouldSplitSimulationIslands", "Enable splitting active object scanning islands",
            ConfigurationParameters.numericTrue,
            (s,cf,p,v) => { s.UnmanagedParams[0].shouldSplitSimulationIslands = BSParam.NumericBool(cf.GetBoolean(p, BSParam.BoolNumeric(v))); },
            (s) => { return s.UnmanagedParams[0].shouldSplitSimulationIslands; },
            (s,p,l,v) => { s.UnmanagedParams[0].shouldSplitSimulationIslands = v; } ),
	    new ParameterDefn("ShouldEnableFrictionCaching", "Enable friction computation caching",
            ConfigurationParameters.numericFalse,
            (s,cf,p,v) => { s.UnmanagedParams[0].shouldEnableFrictionCaching = BSParam.NumericBool(cf.GetBoolean(p, BSParam.BoolNumeric(v))); },
            (s) => { return s.UnmanagedParams[0].shouldEnableFrictionCaching; },
            (s,p,l,v) => { s.UnmanagedParams[0].shouldEnableFrictionCaching = v; } ),
	    new ParameterDefn("NumberOfSolverIterations", "Number of internal iterations (0 means default)",
            0f,     // zero says use Bullet default
            (s,cf,p,v) => { s.UnmanagedParams[0].numberOfSolverIterations = cf.GetFloat(p, v); },
            (s) => { return s.UnmanagedParams[0].numberOfSolverIterations; },
            (s,p,l,v) => { s.UnmanagedParams[0].numberOfSolverIterations = v; } ),

	    new ParameterDefn("LinksetImplementation", "Type of linkset implementation (0=Constraint, 1=Compound, 2=Manual)",
            (float)BSLinkset.LinksetImplementation.Compound,
            (s,cf,p,v) => { LinksetImplementation = cf.GetFloat(p,v); },
            (s) => { return LinksetImplementation; },
            (s,p,l,v) => { LinksetImplementation = v; } ),
	    new ParameterDefn("LinkConstraintUseFrameOffset", "For linksets built with constraints, enable frame offsetFor linksets built with constraints, enable frame offset.",
            ConfigurationParameters.numericFalse,
            (s,cf,p,v) => { LinkConstraintUseFrameOffset = BSParam.NumericBool(cf.GetBoolean(p, BSParam.BoolNumeric(v))); },
            (s) => { return LinkConstraintUseFrameOffset; },
            (s,p,l,v) => { LinkConstraintUseFrameOffset = v; } ),
	    new ParameterDefn("LinkConstraintEnableTransMotor", "Whether to enable translational motor on linkset constraints",
            ConfigurationParameters.numericTrue,
            (s,cf,p,v) => { LinkConstraintEnableTransMotor = BSParam.NumericBool(cf.GetBoolean(p, BSParam.BoolNumeric(v))); },
            (s) => { return LinkConstraintEnableTransMotor; },
            (s,p,l,v) => { LinkConstraintEnableTransMotor = v; } ),
	    new ParameterDefn("LinkConstraintTransMotorMaxVel", "Maximum velocity to be applied by translational motor in linkset constraints",
            5.0f,
            (s,cf,p,v) => { LinkConstraintTransMotorMaxVel = cf.GetFloat(p, v); },
            (s) => { return LinkConstraintTransMotorMaxVel; },
            (s,p,l,v) => { LinkConstraintTransMotorMaxVel = v; } ),
	    new ParameterDefn("LinkConstraintTransMotorMaxForce", "Maximum force to be applied by translational motor in linkset constraints",
            0.1f,
            (s,cf,p,v) => { LinkConstraintTransMotorMaxForce = cf.GetFloat(p, v); },
            (s) => { return LinkConstraintTransMotorMaxForce; },
            (s,p,l,v) => { LinkConstraintTransMotorMaxForce = v; } ),
	    new ParameterDefn("LinkConstraintCFM", "Amount constraint can be violated. 0=no violation, 1=infinite. Default=0.1",
            0.1f,
            (s,cf,p,v) => { LinkConstraintCFM = cf.GetFloat(p, v); },
            (s) => { return LinkConstraintCFM; },
            (s,p,l,v) => { LinkConstraintCFM = v; } ),
	    new ParameterDefn("LinkConstraintERP", "Amount constraint is corrected each tick. 0=none, 1=all. Default = 0.2",
            0.1f,
            (s,cf,p,v) => { LinkConstraintERP = cf.GetFloat(p, v); },
            (s) => { return LinkConstraintERP; },
            (s,p,l,v) => { LinkConstraintERP = v; } ),
	    new ParameterDefn("LinkConstraintSolverIterations", "Number of solver iterations when computing constraint. (0 = Bullet default)",
            40,
            (s,cf,p,v) => { LinkConstraintSolverIterations = cf.GetFloat(p, v); },
            (s) => { return LinkConstraintSolverIterations; },
            (s,p,l,v) => { LinkConstraintSolverIterations = v; } ),

        new ParameterDefn("LogPhysicsStatisticsFrames", "Frames between outputting detailed phys stats. (0 is off)",
            0f,
            (s,cf,p,v) => { s.UnmanagedParams[0].physicsLoggingFrames = cf.GetInt(p, (int)v); },
            (s) => { return (float)s.UnmanagedParams[0].physicsLoggingFrames; },
            (s,p,l,v) => { s.UnmanagedParams[0].physicsLoggingFrames = (int)v; } ),
    };

    // Convert a boolean to our numeric true and false values
    public static float NumericBool(bool b)
    {
        return (b ? ConfigurationParameters.numericTrue : ConfigurationParameters.numericFalse);
    }

    // Convert numeric true and false values to a boolean
    public static bool BoolNumeric(float b)
    {
        return (b == ConfigurationParameters.numericTrue ? true : false);
    }

    // Search through the parameter definitions and return the matching
    //    ParameterDefn structure.
    // Case does not matter as names are compared after converting to lower case.
    // Returns 'false' if the parameter is not found.
    internal static bool TryGetParameter(string paramName, out ParameterDefn defn)
    {
        bool ret = false;
        ParameterDefn foundDefn = new ParameterDefn();
        string pName = paramName.ToLower();

        foreach (ParameterDefn parm in ParameterDefinitions)
        {
            if (pName == parm.name.ToLower())
            {
                foundDefn = parm;
                ret = true;
                break;
            }
        }
        defn = foundDefn;
        return ret;
    }

    // Pass through the settable parameters and set the default values
    internal static void SetParameterDefaultValues(BSScene physicsScene)
    {
        foreach (ParameterDefn parm in ParameterDefinitions)
        {
            parm.setter(physicsScene, parm.name, PhysParameterEntry.APPLY_TO_NONE, parm.defaultValue);
        }
    }

    // Get user set values out of the ini file.
    internal static void SetParameterConfigurationValues(BSScene physicsScene, IConfig cfg)
    {
        foreach (ParameterDefn parm in ParameterDefinitions)
        {
            parm.userParam(physicsScene, cfg, parm.name, parm.defaultValue);
        }
    }

    internal static PhysParameterEntry[] SettableParameters = new PhysParameterEntry[1];

    // This creates an array in the correct format for returning the list of
    //    parameters. This is used by the 'list' option of the 'physics' command.
    internal static void BuildParameterTable()
    {
        if (SettableParameters.Length < ParameterDefinitions.Length)
        {
            List<PhysParameterEntry> entries = new List<PhysParameterEntry>();
            for (int ii = 0; ii < ParameterDefinitions.Length; ii++)
            {
                ParameterDefn pd = ParameterDefinitions[ii];
                entries.Add(new PhysParameterEntry(pd.name, pd.desc));
            }

            // make the list in alphabetical order for estetic reasons
            entries.Sort(delegate(PhysParameterEntry ppe1, PhysParameterEntry ppe2)
            {
                return ppe1.name.CompareTo(ppe2.name);
            });

            SettableParameters = entries.ToArray();
        }
    }


}
}
