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
using System.Reflection;
using System.Text;

using OpenSim.Region.PhysicsModules.SharedBase;

using OpenMetaverse;
using Nini.Config;

namespace OpenSim.Region.PhysicsModule.BulletS
{
public static class BSParam
{
    private static string LogHeader = "[BULLETSIM PARAMETERS]";

    // Tuning notes:
    // From: http://bulletphysics.org/Bullet/phpBB3/viewtopic.php?t=6575
    //    Contact points can be added even if the distance is positive. The constraint solver can deal with
    //    contacts with positive distances as well as negative (penetration). Contact points are discarded
    //    if the distance exceeds a certain threshold.
    //    Bullet has a contact processing threshold and a contact breaking threshold.
    //    If the distance is larger than the contact breaking threshold, it will be removed after one frame.
    //    If the distance is larger than the contact processing threshold, the constraint solver will ignore it.

    //    This is separate/independent from the collision margin. The collision margin increases the object a bit
    //    to improve collision detection performance and accuracy.
    // ===================
    // From:

    /// <summary>
    /// Set whether physics is active or not.
    /// </summary>
    /// <remarks>
    /// Can be enabled and disabled to start and stop physics.
    /// </remarks>
    public static bool Active { get; private set; }

    public static bool UseSeparatePhysicsThread { get; private set; }
    public static float PhysicsTimeStep { get; private set; }

    // Level of Detail values kept as float because that's what the Meshmerizer wants
    public static float MeshLOD { get; private set; }
    public static float MeshCircularLOD { get; private set; }
    public static float MeshMegaPrimLOD { get; private set; }
    public static float MeshMegaPrimThreshold { get; private set; }
    public static float SculptLOD { get; private set; }

    public static int CrossingFailuresBeforeOutOfBounds { get; private set; }
    public static float UpdateVelocityChangeThreshold { get; private set; }

    public static float MinimumObjectMass { get; private set; }
    public static float MaximumObjectMass { get; private set; }
    public static float MaxLinearVelocity { get; private set; }
    public static float MaxLinearVelocitySquared { get; private set; }
    public static float MaxAngularVelocity { get; private set; }
    public static float MaxAngularVelocitySquared { get; private set; }
    public static float MaxAddForceMagnitude { get; private set; }
    public static float MaxAddForceMagnitudeSquared { get; private set; }
    public static float DensityScaleFactor { get; private set; }

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
    public static bool ShouldRemoveZeroWidthTriangles { get; private set; }
    public static bool ShouldUseBulletHACD { get; set; }
    public static bool ShouldUseSingleConvexHullForPrims { get; set; }
    public static bool ShouldUseGImpactShapeForPrims { get; set; }
    public static bool ShouldUseAssetHulls { get; set; }

    public static float TerrainImplementation { get; set; }
    public static int TerrainMeshMagnification { get; private set; }
    public static float TerrainGroundPlane { get; private set; }
    public static float TerrainFriction { get; private set; }
    public static float TerrainHitFraction { get; private set; }
    public static float TerrainRestitution { get; private set; }
    public static float TerrainContactProcessingThreshold { get; private set; }
    public static float TerrainCollisionMargin { get; private set; }

    public static float DefaultFriction { get; private set; }
    public static float DefaultDensity { get; private set; }
    public static float DefaultRestitution { get; private set; }
    public static float CollisionMargin { get; private set; }
    public static float Gravity { get; private set; }

    // Physics Engine operation
	public static float MaxPersistantManifoldPoolSize { get; private set; }
	public static float MaxCollisionAlgorithmPoolSize { get; private set; }
	public static bool ShouldDisableContactPoolDynamicAllocation { get; private set; }
	public static bool ShouldForceUpdateAllAabbs { get; private set; }
	public static bool ShouldRandomizeSolverOrder { get; private set; }
	public static bool ShouldSplitSimulationIslands { get; private set; }
	public static bool ShouldEnableFrictionCaching { get; private set; }
	public static float NumberOfSolverIterations { get; private set; }
    public static bool UseSingleSidedMeshes { get; private set; }
    public static float GlobalContactBreakingThreshold { get; private set; }
    public static float PhysicsUnmanLoggingFrames { get; private set; }

    // Avatar parameters
    public static bool AvatarToAvatarCollisionsByDefault { get; private set; }
    public static float AvatarFriction { get; private set; }
    public static float AvatarStandingFriction { get; private set; }
    public static float AvatarAlwaysRunFactor { get; private set; }
    public static float AvatarDensity { get; private set; }
    public static float AvatarRestitution { get; private set; }
    public static int AvatarShape { get; private set; }
    public static float AvatarCapsuleWidth { get; private set; }
    public static float AvatarCapsuleDepth { get; private set; }
    public static float AvatarCapsuleHeight { get; private set; }
    public static float AvatarHeightLowFudge { get; private set; }
    public static float AvatarHeightMidFudge { get; private set; }
    public static float AvatarHeightHighFudge { get; private set; }
    public static float AvatarFlyingGroundMargin { get; private set; }
    public static float AvatarFlyingGroundUpForce { get; private set; }
    public static float AvatarTerminalVelocity { get; private set; }
	public static float AvatarContactProcessingThreshold { get; private set; }
    public static float AvatarStopZeroThreshold { get; private set; }
	public static int AvatarJumpFrames { get; private set; }
	public static float AvatarBelowGroundUpCorrectionMeters { get; private set; }
	public static float AvatarStepHeight { get; private set; }
	public static float AvatarStepAngle { get; private set; }
	public static float AvatarStepGroundFudge { get; private set; }
	public static float AvatarStepApproachFactor { get; private set; }
	public static float AvatarStepForceFactor { get; private set; }
	public static float AvatarStepUpCorrectionFactor { get; private set; }
	public static int AvatarStepSmoothingSteps { get; private set; }

    // Vehicle parameters
    public static float VehicleMaxLinearVelocity { get; private set; }
    public static float VehicleMaxLinearVelocitySquared { get; private set; }
    public static float VehicleMinLinearVelocity { get; private set; }
    public static float VehicleMinLinearVelocitySquared { get; private set; }
    public static float VehicleMaxAngularVelocity { get; private set; }
    public static float VehicleMaxAngularVelocitySq { get; private set; }
    public static float VehicleAngularDamping { get; private set; }
    public static float VehicleFriction { get; private set; }
    public static float VehicleRestitution { get; private set; }
    public static Vector3 VehicleLinearFactor { get; private set; }
    public static Vector3 VehicleAngularFactor { get; private set; }
    public static Vector3 VehicleInertiaFactor { get; private set; }
    public static float VehicleGroundGravityFudge { get; private set; }
    public static float VehicleAngularBankingTimescaleFudge { get; private set; }
    public static bool VehicleEnableLinearDeflection { get; private set; }
    public static bool VehicleLinearDeflectionNotCollidingNoZ { get; private set; }
    public static bool VehicleEnableAngularVerticalAttraction { get; private set; }
    public static int VehicleAngularVerticalAttractionAlgorithm { get; private set; }
    public static bool VehicleEnableAngularDeflection { get; private set; }
    public static bool VehicleEnableAngularBanking { get; private set; }

    // Convex Hulls
    // Parameters for convex hull routine that ships with Bullet
    public static int CSHullMaxDepthSplit { get; private set; }
    public static int CSHullMaxDepthSplitForSimpleShapes { get; private set; }
    public static float CSHullConcavityThresholdPercent { get; private set; }
    public static float CSHullVolumeConservationThresholdPercent { get; private set; }
    public static int CSHullMaxVertices { get; private set; }
    public static float CSHullMaxSkinWidth { get; private set; }
	public static float BHullMaxVerticesPerHull { get; private set; }		// 100
	public static float BHullMinClusters { get; private set; }				// 2
	public static float BHullCompacityWeight { get; private set; }			// 0.1
	public static float BHullVolumeWeight { get; private set; }				// 0.0
	public static float BHullConcavity { get; private set; }				    // 100
	public static bool BHullAddExtraDistPoints { get; private set; }		// false
	public static bool BHullAddNeighboursDistPoints { get; private set; }	// false
	public static bool BHullAddFacesPoints { get; private set; }			// false
	public static bool BHullShouldAdjustCollisionMargin { get; private set; }	// false
	public static float WhichHACD { get; private set; }				    // zero if Bullet HACD, non-zero says VHACD
    // Parameters for VHACD 2.0: http://code.google.com/p/v-hacd
    // To enable, set both ShouldUseBulletHACD=true and WhichHACD=1
	// http://kmamou.blogspot.ca/2014/12/v-hacd-20-parameters-description.html
	public static float VHACDresolution { get; private set; }			// 100,000 max number of voxels generated during voxelization stage
	public static float VHACDdepth { get; private set; }				// 20 max number of clipping stages
	public static float VHACDconcavity { get; private set; }			// 0.0025 maximum concavity
	public static float VHACDplaneDownsampling { get; private set; }	// 4 granularity of search for best clipping plane
	public static float VHACDconvexHullDownsampling { get; private set; }	// 4 precision of hull gen process
	public static float VHACDalpha { get; private set; }				// 0.05 bias toward clipping along symmetry planes
	public static float VHACDbeta { get; private set; }				    // 0.05 bias toward clipping along revolution axis
	public static float VHACDgamma { get; private set; }				// 0.00125 max concavity when merging
	public static float VHACDpca { get; private set; }					// 0 on/off normalizing mesh before decomp
	public static float VHACDmode { get; private set; }				    // 0 0:voxel based, 1: tetrahedron based
	public static float VHACDmaxNumVerticesPerCH { get; private set; }	// 64 max triangles per convex hull
	public static float VHACDminVolumePerCH { get; private set; }		// 0.0001 sampling of generated convex hulls

    // Linkset implementation parameters
    public static float LinksetImplementation { get; private set; }
    public static bool LinksetOffsetCenterOfMass { get; private set; }
    public static bool LinkConstraintUseFrameOffset { get; private set; }
    public static bool LinkConstraintEnableTransMotor { get; private set; }
    public static float LinkConstraintTransMotorMaxVel { get; private set; }
    public static float LinkConstraintTransMotorMaxForce { get; private set; }
    public static float LinkConstraintERP { get; private set; }
    public static float LinkConstraintCFM { get; private set; }
    public static float LinkConstraintSolverIterations { get; private set; }

    public static float PID_D { get; private set; }    // derivative
    public static float PID_P { get; private set; }    // proportional

    // Various constants that come from that other virtual world that shall not be named.
    public const float MinGravityZ = -1f;
    public const float MaxGravityZ = 28f;
    public const float MinFriction = 0f;
    public const float MaxFriction = 255f;
    public const float MinDensity = 0.01f;
    public const float MaxDensity = 22587f;
    public const float MinRestitution = 0f;
    public const float MaxRestitution = 1f;

    // =====================================================================================
    // =====================================================================================

    // Base parameter definition that gets and sets parameter values via a string
    public abstract class ParameterDefnBase
    {
        public string name;         // string name of the parameter
        public string desc;         // a short description of what the parameter means
        public ParameterDefnBase(string pName, string pDesc)
        {
            name = pName;
            desc = pDesc;
        }
        // Set the parameter value to the default
        public abstract void AssignDefault(BSScene s);
        // Get the value as a string
        public abstract string GetValue(BSScene s);
        // Set the value to this string value
        public abstract void SetValue(BSScene s, string valAsString);
        // set the value on a particular object (usually sets in physics engine)
        public abstract void SetOnObject(BSScene s, BSPhysObject obj);
        public abstract bool HasSetOnObject { get; }
    }

    // Specific parameter definition for a parameter of a specific type.
    public delegate T PGetValue<T>(BSScene s);
    public delegate void PSetValue<T>(BSScene s, T val);
    public delegate void PSetOnObject<T>(BSScene scene, BSPhysObject obj);
    public sealed class ParameterDefn<T> : ParameterDefnBase
    {
        private T defaultValue;
        private PSetValue<T> setter;
        private PGetValue<T> getter;
        private PSetOnObject<T> objectSet;
        public ParameterDefn(string pName, string pDesc, T pDefault, PGetValue<T> pGetter, PSetValue<T> pSetter)
            : base(pName, pDesc)
        {
            defaultValue = pDefault;
            setter = pSetter;
            getter = pGetter;
            objectSet = null;
        }
        public ParameterDefn(string pName, string pDesc, T pDefault, PGetValue<T> pGetter, PSetValue<T> pSetter, PSetOnObject<T> pObjSetter)
            : base(pName, pDesc)
        {
            defaultValue = pDefault;
            setter = pSetter;
            getter = pGetter;
            objectSet = pObjSetter;
        }
        // Simple parameter variable where property name is the same as the INI file name
        //     and the value is only a simple get and set.
        public ParameterDefn(string pName, string pDesc, T pDefault)
            : base(pName, pDesc)
        {
            defaultValue = pDefault;
            setter = (s, v) => { SetValueByName(s, name, v); };
            getter = (s) => { return GetValueByName(s, name); };
            objectSet = null;
        }
        // Use reflection to find the property named 'pName' in BSParam and assign 'val' to same.
        private void SetValueByName(BSScene s, string pName, T val)
        {
            PropertyInfo prop = typeof(BSParam).GetProperty(pName, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (prop == null)
            {
                // This should only be output when someone adds a new INI parameter and misspells the name.
                s.Logger.ErrorFormat("{0} SetValueByName: did not find '{1}'. Verify specified property name is the same as the given INI parameters name.", LogHeader, pName);
            }
            else
            {
                prop.SetValue(null, val, null);
            }
        }
        // Use reflection to find the property named 'pName' in BSParam and return the value in same.
        private T GetValueByName(BSScene s, string pName)
        {
            PropertyInfo prop = typeof(BSParam).GetProperty(pName, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (prop == null)
            {
                // This should only be output when someone adds a new INI parameter and misspells the name.
                s.Logger.ErrorFormat("{0} GetValueByName: did not find '{1}'. Verify specified property name is the same as the given INI parameter name.", LogHeader, pName);
            }
            return (T)prop.GetValue(null, null);
        }
        public override void AssignDefault(BSScene s)
        {
            setter(s, defaultValue);
        }
        public override string GetValue(BSScene s)
        {
            return getter(s).ToString();
        }
        public override void SetValue(BSScene s, string valAsString)
        {
            // Get the generic type of the setter
            Type genericType = setter.GetType().GetGenericArguments()[0];
            // Find the 'Parse' method on that type
            System.Reflection.MethodInfo parser = null;
            try
            {
                parser = genericType.GetMethod("Parse", new Type[] { typeof(String) } );
            }
            catch (Exception e)
            {
                s.Logger.ErrorFormat("{0} Exception getting parser for type '{1}': {2}", LogHeader, genericType, e);
                parser = null;
            }
            if (parser != null)
            {
                // Parse the input string
                try
                {
                    T setValue = (T)parser.Invoke(genericType, new Object[] { valAsString });
                    // Store the parsed value
                    setter(s, setValue);
                    // s.Logger.DebugFormat("{0} Parameter {1} = {2}", LogHeader, name, setValue);
                }
                catch
                {
                    s.Logger.ErrorFormat("{0} Failed parsing parameter value '{1}' as type '{2}'", LogHeader, valAsString, genericType);
                }
            }
            else
            {
                s.Logger.ErrorFormat("{0} Could not find parameter parser for type '{1}'", LogHeader, genericType);
            }
        }
        public override bool HasSetOnObject
        {
            get { return objectSet != null; }
        }
        public override void SetOnObject(BSScene s, BSPhysObject obj)
        {
            if (objectSet != null)
                objectSet(s, obj);
        }
    }

    // List of all of the externally visible parameters.
    // For each parameter, this table maps a text name to getter and setters.
    // To add a new externally referencable/settable parameter, add the paramter storage
    //    location somewhere in the program and make an entry in this table with the
    //    getters and setters.
    // It is easiest to find an existing definition and copy it.
    //
    // A ParameterDefn<T>() takes the following parameters:
    //    -- the text name of the parameter. This is used for console input and ini file.
    //    -- a short text description of the parameter. This shows up in the console listing.
    //    -- a default value
    //    -- a delegate for getting the value
    //    -- a delegate for setting the value
    //    -- an optional delegate to update the value in the world. Most often used to
    //          push the new value to an in-world object.
    //
    // The single letter parameters for the delegates are:
    //    s = BSScene
    //    o = BSPhysObject
    //    v = value (appropriate type)
    private static ParameterDefnBase[] ParameterDefinitions =
    {
        new ParameterDefn<bool>("Active", "If 'true', false then physics is not active",
            false ),
        new ParameterDefn<bool>("UseSeparatePhysicsThread", "If 'true', the physics engine runs independent from the simulator heartbeat",
            false ),
        new ParameterDefn<float>("PhysicsTimeStep", "If separate thread, seconds to simulate each interval",
            0.089f ),

        new ParameterDefn<bool>("MeshSculptedPrim", "Whether to create meshes for sculpties",
            true,
            (s) => { return ShouldMeshSculptedPrim; },
            (s,v) => { ShouldMeshSculptedPrim = v; } ),
        new ParameterDefn<bool>("ForceSimplePrimMeshing", "If true, only use primitive meshes for objects",
            false,
            (s) => { return ShouldForceSimplePrimMeshing; },
            (s,v) => { ShouldForceSimplePrimMeshing = v; } ),
        new ParameterDefn<bool>("UseHullsForPhysicalObjects", "If true, create hulls for physical objects",
            true,
            (s) => { return ShouldUseHullsForPhysicalObjects; },
            (s,v) => { ShouldUseHullsForPhysicalObjects = v; } ),
        new ParameterDefn<bool>("ShouldRemoveZeroWidthTriangles", "If true, remove degenerate triangles from meshes",
            true ),
        new ParameterDefn<bool>("ShouldUseBulletHACD", "If true, use the Bullet version of HACD",
            false ),
        new ParameterDefn<bool>("ShouldUseSingleConvexHullForPrims", "If true, use a single convex hull shape for physical prims",
            true ),
        new ParameterDefn<bool>("ShouldUseGImpactShapeForPrims", "If true, use a GImpact shape for prims with cuts and twists",
            false ),
        new ParameterDefn<bool>("ShouldUseAssetHulls", "If true, use hull if specified in the mesh asset info",
            true ),

        new ParameterDefn<int>("CrossingFailuresBeforeOutOfBounds", "How forgiving we are about getting into adjactent regions",
            5 ),
        new ParameterDefn<float>("UpdateVelocityChangeThreshold", "Change in updated velocity required before reporting change to simulator",
            0.1f ),

        new ParameterDefn<float>("MeshLevelOfDetail", "Level of detail to render meshes (32, 16, 8 or 4. 32=most detailed)",
            32f,
            (s) => { return MeshLOD; },
            (s,v) => { MeshLOD = v; } ),
        new ParameterDefn<float>("MeshLevelOfDetailCircular", "Level of detail for prims with circular cuts or shapes",
            32f,
            (s) => { return MeshCircularLOD; },
            (s,v) => { MeshCircularLOD = v; } ),
        new ParameterDefn<float>("MeshLevelOfDetailMegaPrimThreshold", "Size (in meters) of a mesh before using MeshMegaPrimLOD",
            10f,
            (s) => { return MeshMegaPrimThreshold; },
            (s,v) => { MeshMegaPrimThreshold = v; } ),
        new ParameterDefn<float>("MeshLevelOfDetailMegaPrim", "Level of detail to render meshes larger than threshold meters",
            32f,
            (s) => { return MeshMegaPrimLOD; },
            (s,v) => { MeshMegaPrimLOD = v; } ),
        new ParameterDefn<float>("SculptLevelOfDetail", "Level of detail to render sculpties (32, 16, 8 or 4. 32=most detailed)",
            32f,
            (s) => { return SculptLOD; },
            (s,v) => { SculptLOD = v; } ),

        new ParameterDefn<int>("MaxSubStep", "In simulation step, maximum number of substeps",
            10,
            (s) => { return s.m_maxSubSteps; },
            (s,v) => { s.m_maxSubSteps = (int)v; } ),
        new ParameterDefn<float>("FixedTimeStep", "In simulation step, seconds of one substep (1/60)",
            1f / 60f,
            (s) => { return s.m_fixedTimeStep; },
            (s,v) => { s.m_fixedTimeStep = v; } ),
        new ParameterDefn<float>("NominalFrameRate", "The base frame rate we claim",
            55f,
            (s) => { return s.NominalFrameRate; },
            (s,v) => { s.NominalFrameRate = (int)v; } ),
        new ParameterDefn<int>("MaxCollisionsPerFrame", "Max collisions returned at end of each frame",
            2048,
            (s) => { return s.m_maxCollisionsPerFrame; },
            (s,v) => { s.m_maxCollisionsPerFrame = (int)v; } ),
        new ParameterDefn<int>("MaxUpdatesPerFrame", "Max updates returned at end of each frame",
            8000,
            (s) => { return s.m_maxUpdatesPerFrame; },
            (s,v) => { s.m_maxUpdatesPerFrame = (int)v; } ),

        new ParameterDefn<float>("MinObjectMass", "Minimum object mass (0.0001)",
            0.0001f,
            (s) => { return MinimumObjectMass; },
            (s,v) => { MinimumObjectMass = v; } ),
        new ParameterDefn<float>("MaxObjectMass", "Maximum object mass (10000.01)",
            10000.01f,
            (s) => { return MaximumObjectMass; },
            (s,v) => { MaximumObjectMass = v; } ),
        new ParameterDefn<float>("MaxLinearVelocity", "Maximum velocity magnitude that can be assigned to an object",
            1000.0f,
            (s) => { return MaxLinearVelocity; },
            (s,v) => { MaxLinearVelocity = v; MaxLinearVelocitySquared = v * v; } ),
        new ParameterDefn<float>("MaxAngularVelocity", "Maximum rotational velocity magnitude that can be assigned to an object",
            1000.0f,
            (s) => { return MaxAngularVelocity; },
            (s,v) => { MaxAngularVelocity = v;  MaxAngularVelocitySquared = v * v; } ),
        // LL documentation says thie number should be 20f for llApplyImpulse and 200f for llRezObject
        new ParameterDefn<float>("MaxAddForceMagnitude", "Maximum force that can be applied by llApplyImpulse (SL says 20f)",
            20000.0f,
            (s) => { return MaxAddForceMagnitude; },
            (s,v) => { MaxAddForceMagnitude = v; MaxAddForceMagnitudeSquared = v * v; } ),
        // Density is passed around as 100kg/m3. This scales that to 1kg/m3.
        // Reduce by power of 100 because Bullet doesn't seem to handle objects with large mass very well
        new ParameterDefn<float>("DensityScaleFactor", "Conversion for simulator/viewer density (100kg/m3) to physical density (1kg/m3)",
            0.01f ),

        new ParameterDefn<float>("PID_D", "Derivitive factor for motion smoothing",
            2200f ),
        new ParameterDefn<float>("PID_P", "Parameteric factor for motion smoothing",
            900f ),

        new ParameterDefn<float>("DefaultFriction", "Friction factor used on new objects",
            0.2f,
            (s) => { return DefaultFriction; },
            (s,v) => { DefaultFriction = v; s.UnmanagedParams[0].defaultFriction = v; } ),
        // For historical reasons, the viewer and simulator multiply the density by 100
        new ParameterDefn<float>("DefaultDensity", "Density for new objects" ,
            1000.0006836f,  // Aluminum g/cm3 * 100
            (s) => { return DefaultDensity; },
            (s,v) => { DefaultDensity = v; s.UnmanagedParams[0].defaultDensity = v; } ),
        new ParameterDefn<float>("DefaultRestitution", "Bouncyness of an object" ,
            0f,
            (s) => { return DefaultRestitution; },
            (s,v) => { DefaultRestitution = v; s.UnmanagedParams[0].defaultRestitution = v; } ),
        new ParameterDefn<float>("CollisionMargin", "Margin around objects before collisions are calculated (must be zero!)",
            0.04f,
            (s) => { return CollisionMargin; },
            (s,v) => { CollisionMargin = v; s.UnmanagedParams[0].collisionMargin = v; } ),
        new ParameterDefn<float>("Gravity", "Vertical force of gravity (negative means down)",
            -9.80665f,
            (s) => { return Gravity; },
            (s,v) => { Gravity = v; s.UnmanagedParams[0].gravity = v; },
            (s,o) => { s.PE.SetGravity(o.PhysBody, new Vector3(0f,0f,Gravity)); } ),


        new ParameterDefn<float>("LinearDamping", "Factor to damp linear movement per second (0.0 - 1.0)",
            0f,
            (s) => { return LinearDamping; },
            (s,v) => { LinearDamping = v; },
            (s,o) => { s.PE.SetDamping(o.PhysBody, LinearDamping, AngularDamping); } ),
        new ParameterDefn<float>("AngularDamping", "Factor to damp angular movement per second (0.0 - 1.0)",
            0f,
            (s) => { return AngularDamping; },
            (s,v) => { AngularDamping = v; },
            (s,o) => { s.PE.SetDamping(o.PhysBody, LinearDamping, AngularDamping); } ),
        new ParameterDefn<float>("DeactivationTime", "Seconds before considering an object potentially static",
            0.2f,
            (s) => { return DeactivationTime; },
            (s,v) => { DeactivationTime = v; },
            (s,o) => { s.PE.SetDeactivationTime(o.PhysBody, DeactivationTime); } ),
        new ParameterDefn<float>("LinearSleepingThreshold", "Seconds to measure linear movement before considering static",
            0.8f,
            (s) => { return LinearSleepingThreshold; },
            (s,v) => { LinearSleepingThreshold = v;},
            (s,o) => { s.PE.SetSleepingThresholds(o.PhysBody, LinearSleepingThreshold, AngularSleepingThreshold); } ),
        new ParameterDefn<float>("AngularSleepingThreshold", "Seconds to measure angular movement before considering static",
            1.0f,
            (s) => { return AngularSleepingThreshold; },
            (s,v) => { AngularSleepingThreshold = v;},
            (s,o) => { s.PE.SetSleepingThresholds(o.PhysBody, LinearSleepingThreshold, AngularSleepingThreshold); } ),
        new ParameterDefn<float>("CcdMotionThreshold", "Continuious collision detection threshold (0 means no CCD)" ,
            0.0f,     // set to zero to disable
            (s) => { return CcdMotionThreshold; },
            (s,v) => { CcdMotionThreshold = v;},
            (s,o) => { s.PE.SetCcdMotionThreshold(o.PhysBody, CcdMotionThreshold); } ),
        new ParameterDefn<float>("CcdSweptSphereRadius", "Continuious collision detection test radius" ,
            0.2f,
            (s) => { return CcdSweptSphereRadius; },
            (s,v) => { CcdSweptSphereRadius = v;},
            (s,o) => { s.PE.SetCcdSweptSphereRadius(o.PhysBody, CcdSweptSphereRadius); } ),
        new ParameterDefn<float>("ContactProcessingThreshold", "Distance above which contacts can be discarded (0 means no discard)" ,
            0.0f,
            (s) => { return ContactProcessingThreshold; },
            (s,v) => { ContactProcessingThreshold = v;},
            (s,o) => { s.PE.SetContactProcessingThreshold(o.PhysBody, ContactProcessingThreshold); } ),

	    new ParameterDefn<float>("TerrainImplementation", "Type of shape to use for terrain (0=heightmap, 1=mesh)",
            (float)BSTerrainPhys.TerrainImplementation.Heightmap ),
        new ParameterDefn<int>("TerrainMeshMagnification", "Number of times the 256x256 heightmap is multiplied to create the terrain mesh" ,
            2 ),
        new ParameterDefn<float>("TerrainGroundPlane", "Altitude of ground plane used to keep things from falling to infinity" ,
            -500.0f ),
        new ParameterDefn<float>("TerrainFriction", "Factor to reduce movement against terrain surface" ,
            0.3f ),
        new ParameterDefn<float>("TerrainHitFraction", "Distance to measure hit collisions" ,
            0.8f ),
        new ParameterDefn<float>("TerrainRestitution", "Bouncyness" ,
            0f ),
        new ParameterDefn<float>("TerrainContactProcessingThreshold", "Distance from terrain to stop processing collisions" ,
            0.0f ),
        new ParameterDefn<float>("TerrainCollisionMargin", "Margin where collision checking starts" ,
            0.04f ),

        new ParameterDefn<bool>("AvatarToAvatarCollisionsByDefault", "Should avatars collide with other avatars by default?",
            true),
        new ParameterDefn<float>("AvatarFriction", "Factor to reduce movement against an avatar. Changed on avatar recreation.",
            0.2f ),
        new ParameterDefn<float>("AvatarStandingFriction", "Avatar friction when standing. Changed on avatar recreation.",
            0.95f ),
        new ParameterDefn<float>("AvatarAlwaysRunFactor", "Speed multiplier if avatar is set to always run",
            1.3f ),
            // For historical reasons, density is reported  * 100
        new ParameterDefn<float>("AvatarDensity", "Density of an avatar. Changed on avatar recreation. Scaled times 100.",
            3500f) ,    // 3.5 * 100
        new ParameterDefn<float>("AvatarRestitution", "Bouncyness. Changed on avatar recreation.",
            0f ),
        new ParameterDefn<int>("AvatarShape", "Code for avatar physical shape: 0:capsule, 1:cube, 2:ovoid, 2:mesh",
            BSShapeCollection.AvatarShapeCube ) ,
        new ParameterDefn<float>("AvatarCapsuleWidth", "The distance between the sides of the avatar capsule",
            0.6f ) ,
        new ParameterDefn<float>("AvatarCapsuleDepth", "The distance between the front and back of the avatar capsule",
            0.45f ),
        new ParameterDefn<float>("AvatarCapsuleHeight", "Default height of space around avatar",
            1.5f ),
        new ParameterDefn<float>("AvatarHeightLowFudge", "A fudge factor to make small avatars stand on the ground",
            0f ),
        new ParameterDefn<float>("AvatarHeightMidFudge", "A fudge distance to adjust average sized avatars to be standing on ground",
            0f ),
        new ParameterDefn<float>("AvatarHeightHighFudge", "A fudge factor to make tall avatars stand on the ground",
            0f ),
        new ParameterDefn<float>("AvatarFlyingGroundMargin", "Meters avatar is kept above the ground when flying",
            5f ),
        new ParameterDefn<float>("AvatarFlyingGroundUpForce", "Upward force applied to the avatar to keep it at flying ground margin",
            2.0f ),
        new ParameterDefn<float>("AvatarTerminalVelocity", "Terminal Velocity of falling avatar",
            -54.0f ),
	    new ParameterDefn<float>("AvatarContactProcessingThreshold", "Distance from capsule to check for collisions",
            0.1f ),
	    new ParameterDefn<float>("AvatarStopZeroThreshold", "Movement velocity below which avatar is assumed to be stopped",
            0.1f ),
	    new ParameterDefn<float>("AvatarBelowGroundUpCorrectionMeters", "Meters to move avatar up if it seems to be below ground",
            1.0f ),
	    new ParameterDefn<int>("AvatarJumpFrames", "Number of frames to allow jump forces. Changes jump height.",
            4 ),
	    new ParameterDefn<float>("AvatarStepHeight", "Height of a step obstacle to consider step correction",
            0.999f ) ,
	    new ParameterDefn<float>("AvatarStepAngle", "The angle (in radians) for a vertical surface to be considered a step",
            0.3f ) ,
	    new ParameterDefn<float>("AvatarStepGroundFudge", "Fudge factor subtracted from avatar base when comparing collision height",
            0.1f ) ,
	    new ParameterDefn<float>("AvatarStepApproachFactor", "Factor to control angle of approach to step (0=straight on)",
            2f ),
	    new ParameterDefn<float>("AvatarStepForceFactor", "Controls the amount of force up applied to step up onto a step",
            0f ),
	    new ParameterDefn<float>("AvatarStepUpCorrectionFactor", "Multiplied by height of step collision to create up movement at step",
            0.8f ),
	    new ParameterDefn<int>("AvatarStepSmoothingSteps", "Number of frames after a step collision that we continue walking up stairs",
            1 ),

        new ParameterDefn<float>("VehicleMaxLinearVelocity", "Maximum velocity magnitude that can be assigned to a vehicle",
            1000.0f,
            (s) => { return (float)VehicleMaxLinearVelocity; },
            (s,v) => { VehicleMaxLinearVelocity = v; VehicleMaxLinearVelocitySquared = v * v; } ),
        new ParameterDefn<float>("VehicleMinLinearVelocity", "Maximum velocity magnitude that can be assigned to a vehicle",
            0.001f,
            (s) => { return (float)VehicleMinLinearVelocity; },
            (s,v) => { VehicleMinLinearVelocity = v; VehicleMinLinearVelocitySquared = v * v; } ),
        new ParameterDefn<float>("VehicleMaxAngularVelocity", "Maximum rotational velocity magnitude that can be assigned to a vehicle",
            12.0f,
            (s) => { return (float)VehicleMaxAngularVelocity; },
            (s,v) => { VehicleMaxAngularVelocity = v; VehicleMaxAngularVelocitySq = v * v; } ),
        new ParameterDefn<float>("VehicleAngularDamping", "Factor to damp vehicle angular movement per second (0.0 - 1.0)",
            0.0f ),
        new ParameterDefn<Vector3>("VehicleLinearFactor", "Fraction of physical linear changes applied to vehicle (<0,0,0> to <1,1,1>)",
            new Vector3(1f, 1f, 1f) ),
        new ParameterDefn<Vector3>("VehicleAngularFactor", "Fraction of physical angular changes applied to vehicle (<0,0,0> to <1,1,1>)",
            new Vector3(1f, 1f, 1f) ),
        new ParameterDefn<Vector3>("VehicleInertiaFactor", "Fraction of physical inertia applied (<0,0,0> to <1,1,1>)",
            new Vector3(1f, 1f, 1f) ),
        new ParameterDefn<float>("VehicleFriction", "Friction of vehicle on the ground (0.0 - 1.0)",
            0.0f ),
        new ParameterDefn<float>("VehicleRestitution", "Bouncyness factor for vehicles (0.0 - 1.0)",
            0.0f ),
        new ParameterDefn<float>("VehicleGroundGravityFudge", "Factor to multiply gravity if a ground vehicle is probably on the ground (0.0 - 1.0)",
            0.2f ),
        new ParameterDefn<float>("VehicleAngularBankingTimescaleFudge", "Factor to multiple angular banking timescale. Tune to increase realism.",
            60.0f ),
        new ParameterDefn<bool>("VehicleEnableLinearDeflection", "Turn on/off vehicle linear deflection effect",
            true ),
        new ParameterDefn<bool>("VehicleLinearDeflectionNotCollidingNoZ", "Turn on/off linear deflection Z effect on non-colliding vehicles",
            true ),
        new ParameterDefn<bool>("VehicleEnableAngularVerticalAttraction", "Turn on/off vehicle angular vertical attraction effect",
            true ),
        new ParameterDefn<int>("VehicleAngularVerticalAttractionAlgorithm", "Select vertical attraction algo. You need to look at the source.",
            0 ),
        new ParameterDefn<bool>("VehicleEnableAngularDeflection", "Turn on/off vehicle angular deflection effect",
            true ),
        new ParameterDefn<bool>("VehicleEnableAngularBanking", "Turn on/off vehicle angular banking effect",
            true ),

	    new ParameterDefn<float>("MaxPersistantManifoldPoolSize", "Number of manifolds pooled (0 means default of 4096)",
            0f,
            (s) => { return MaxPersistantManifoldPoolSize; },
            (s,v) => { MaxPersistantManifoldPoolSize = v; s.UnmanagedParams[0].maxPersistantManifoldPoolSize = v; } ),
	    new ParameterDefn<float>("MaxCollisionAlgorithmPoolSize", "Number of collisions pooled (0 means default of 4096)",
            0f,
            (s) => { return MaxCollisionAlgorithmPoolSize; },
            (s,v) => { MaxCollisionAlgorithmPoolSize = v; s.UnmanagedParams[0].maxCollisionAlgorithmPoolSize = v; } ),
	    new ParameterDefn<bool>("ShouldDisableContactPoolDynamicAllocation", "Enable to allow large changes in object count",
            false,
            (s) => { return ShouldDisableContactPoolDynamicAllocation; },
            (s,v) => { ShouldDisableContactPoolDynamicAllocation = v;
                        s.UnmanagedParams[0].shouldDisableContactPoolDynamicAllocation = NumericBool(v); } ),
	    new ParameterDefn<bool>("ShouldForceUpdateAllAabbs", "Enable to recomputer AABBs every simulator step",
            false,
            (s) => { return ShouldForceUpdateAllAabbs; },
            (s,v) => { ShouldForceUpdateAllAabbs = v; s.UnmanagedParams[0].shouldForceUpdateAllAabbs = NumericBool(v); } ),
	    new ParameterDefn<bool>("ShouldRandomizeSolverOrder", "Enable for slightly better stacking interaction",
            true,
            (s) => { return ShouldRandomizeSolverOrder; },
            (s,v) => { ShouldRandomizeSolverOrder = v; s.UnmanagedParams[0].shouldRandomizeSolverOrder = NumericBool(v); } ),
	    new ParameterDefn<bool>("ShouldSplitSimulationIslands", "Enable splitting active object scanning islands",
            true,
            (s) => { return ShouldSplitSimulationIslands; },
            (s,v) => { ShouldSplitSimulationIslands = v; s.UnmanagedParams[0].shouldSplitSimulationIslands = NumericBool(v); } ),
	    new ParameterDefn<bool>("ShouldEnableFrictionCaching", "Enable friction computation caching",
            true,
            (s) => { return ShouldEnableFrictionCaching; },
            (s,v) => { ShouldEnableFrictionCaching = v; s.UnmanagedParams[0].shouldEnableFrictionCaching = NumericBool(v); } ),
	    new ParameterDefn<float>("NumberOfSolverIterations", "Number of internal iterations (0 means default)",
            0f,     // zero says use Bullet default
            (s) => { return NumberOfSolverIterations; },
            (s,v) => { NumberOfSolverIterations = v; s.UnmanagedParams[0].numberOfSolverIterations = v; } ),
	    new ParameterDefn<bool>("UseSingleSidedMeshes", "Whether to compute collisions based on single sided meshes.",
            true,
            (s) => { return UseSingleSidedMeshes; },
            (s,v) => { UseSingleSidedMeshes = v; s.UnmanagedParams[0].useSingleSidedMeshes = NumericBool(v); } ),
	    new ParameterDefn<float>("GlobalContactBreakingThreshold", "Amount of shape radius before breaking a collision contact (0 says Bullet default (0.2))",
            0f,
            (s) => { return GlobalContactBreakingThreshold; },
            (s,v) => { GlobalContactBreakingThreshold = v; s.UnmanagedParams[0].globalContactBreakingThreshold = v; } ),
	    new ParameterDefn<float>("PhysicsUnmanLoggingFrames", "If non-zero, frames between output of detailed unmanaged physics statistics",
            0f,
            (s) => { return PhysicsUnmanLoggingFrames; },
            (s,v) => { PhysicsUnmanLoggingFrames = v; s.UnmanagedParams[0].physicsLoggingFrames = v; } ),

	    new ParameterDefn<int>("CSHullMaxDepthSplit", "CS impl: max depth to split for hull. 1-10 but > 7 is iffy",
            7 ),
	    new ParameterDefn<int>("CSHullMaxDepthSplitForSimpleShapes", "CS impl: max depth setting for simple prim shapes",
            2 ),
	    new ParameterDefn<float>("CSHullConcavityThresholdPercent", "CS impl: concavity threshold percent (0-20)",
            5f ),
	    new ParameterDefn<float>("CSHullVolumeConservationThresholdPercent", "percent volume conservation to collapse hulls (0-30)",
            5f ),
	    new ParameterDefn<int>("CSHullMaxVertices", "CS impl: maximum number of vertices in output hulls. Keep < 50.",
            32 ),
	    new ParameterDefn<float>("CSHullMaxSkinWidth", "CS impl: skin width to apply to output hulls.",
            0f ),

	    new ParameterDefn<float>("BHullMaxVerticesPerHull", "Bullet impl: max number of vertices per created hull",
            200f ),
	    new ParameterDefn<float>("BHullMinClusters", "Bullet impl: minimum number of hulls to create per mesh",
            10f ),
	    new ParameterDefn<float>("BHullCompacityWeight", "Bullet impl: weight factor for how compact to make hulls",
            20f ),
	    new ParameterDefn<float>("BHullVolumeWeight", "Bullet impl: weight factor for volume in created hull",
            0.1f ),
	    new ParameterDefn<float>("BHullConcavity", "Bullet impl: weight factor for how convex a created hull can be",
            10f ),
	    new ParameterDefn<bool>("BHullAddExtraDistPoints", "Bullet impl: whether to add extra vertices for long distance vectors",
            true ),
	    new ParameterDefn<bool>("BHullAddNeighboursDistPoints", "Bullet impl: whether to add extra vertices between neighbor hulls",
            true ),
	    new ParameterDefn<bool>("BHullAddFacesPoints", "Bullet impl: whether to add extra vertices to break up hull faces",
            true ),
	    new ParameterDefn<bool>("BHullShouldAdjustCollisionMargin", "Bullet impl: whether to shrink resulting hulls to account for collision margin",
            false ),

	    new ParameterDefn<float>("WhichHACD", "zero if Bullet HACD, non-zero says VHACD",
            0f ),
	    new ParameterDefn<float>("VHACDresolution", "max number of voxels generated during voxelization stage",
            100000f ),
	    new ParameterDefn<float>("VHACDdepth", "max number of clipping stages",
            20f ),
	    new ParameterDefn<float>("VHACDconcavity", "maximum concavity",
            0.0025f ),
	    new ParameterDefn<float>("VHACDplaneDownsampling", "granularity of search for best clipping plane",
            4f ),
	    new ParameterDefn<float>("VHACDconvexHullDownsampling", "precision of hull gen process",
            4f ),
	    new ParameterDefn<float>("VHACDalpha", "bias toward clipping along symmetry planes",
            0.05f ),
	    new ParameterDefn<float>("VHACDbeta", "bias toward clipping along revolution axis",
            0.05f ),
	    new ParameterDefn<float>("VHACDgamma", "max concavity when merging",
            0.00125f ),
	    new ParameterDefn<float>("VHACDpca", "on/off normalizing mesh before decomp",
            0f ),
	    new ParameterDefn<float>("VHACDmode", "0:voxel based, 1: tetrahedron based",
            0f ),
	    new ParameterDefn<float>("VHACDmaxNumVerticesPerCH", "max triangles per convex hull",
            64f ),
	    new ParameterDefn<float>("VHACDminVolumePerCH",	"sampling of generated convex hulls",
            0.0001f ),

	    new ParameterDefn<float>("LinksetImplementation", "Type of linkset implementation (0=Constraint, 1=Compound, 2=Manual)",
            (float)BSLinkset.LinksetImplementation.Compound ),
	    new ParameterDefn<bool>("LinksetOffsetCenterOfMass", "If 'true', compute linkset center-of-mass and offset linkset position to account for same",
            true ),
	    new ParameterDefn<bool>("LinkConstraintUseFrameOffset", "For linksets built with constraints, enable frame offsetFor linksets built with constraints, enable frame offset.",
            false ),
	    new ParameterDefn<bool>("LinkConstraintEnableTransMotor", "Whether to enable translational motor on linkset constraints",
            true ),
	    new ParameterDefn<float>("LinkConstraintTransMotorMaxVel", "Maximum velocity to be applied by translational motor in linkset constraints",
            5.0f ),
	    new ParameterDefn<float>("LinkConstraintTransMotorMaxForce", "Maximum force to be applied by translational motor in linkset constraints",
            0.1f ),
	    new ParameterDefn<float>("LinkConstraintCFM", "Amount constraint can be violated. 0=no violation, 1=infinite. Default=0.1",
            0.1f ),
	    new ParameterDefn<float>("LinkConstraintERP", "Amount constraint is corrected each tick. 0=none, 1=all. Default = 0.2",
            0.1f ),
	    new ParameterDefn<float>("LinkConstraintSolverIterations", "Number of solver iterations when computing constraint. (0 = Bullet default)",
            40 ),

        new ParameterDefn<int>("PhysicsMetricFrames", "Frames between outputting detailed phys metrics. (0 is off)",
            0,
            (s) => { return s.PhysicsMetricDumpFrames; },
            (s,v) => { s.PhysicsMetricDumpFrames = v; } ),
        new ParameterDefn<float>("ResetBroadphasePool", "Setting this is any value resets the broadphase collision pool",
            0f,
            (s) => { return 0f; },
            (s,v) => { BSParam.ResetBroadphasePoolTainted(s, v, false /* inTaintTime */); } ),
        new ParameterDefn<float>("ResetConstraintSolver", "Setting this is any value resets the constraint solver",
            0f,
            (s) => { return 0f; },
            (s,v) => { BSParam.ResetConstraintSolverTainted(s, v); } ),
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
    internal static bool TryGetParameter(string paramName, out ParameterDefnBase defn)
    {
        bool ret = false;
        ParameterDefnBase foundDefn = null;
        string pName = paramName.ToLower();

        foreach (ParameterDefnBase parm in ParameterDefinitions)
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
        foreach (ParameterDefnBase parm in ParameterDefinitions)
        {
            parm.AssignDefault(physicsScene);
        }
    }

    // Get user set values out of the ini file.
    internal static void SetParameterConfigurationValues(BSScene physicsScene, IConfig cfg)
    {
        foreach (ParameterDefnBase parm in ParameterDefinitions)
        {
            parm.SetValue(physicsScene, cfg.GetString(parm.name, parm.GetValue(physicsScene)));
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
                ParameterDefnBase pd = ParameterDefinitions[ii];
                entries.Add(new PhysParameterEntry(pd.name, pd.desc));
            }

            // make the list alphabetical for ease of finding anything
            entries.Sort((ppe1, ppe2) => { return ppe1.name.CompareTo(ppe2.name); });

            SettableParameters = entries.ToArray();
        }
    }

    // =====================================================================
    // =====================================================================
    // There are parameters that, when set, cause things to happen in the physics engine.
    // This causes the broadphase collision cache to be cleared.
    private static void ResetBroadphasePoolTainted(BSScene pPhysScene, float v, bool inTaintTime)
    {
        BSScene physScene = pPhysScene;
        physScene.TaintedObject(inTaintTime, "BSParam.ResetBroadphasePoolTainted", delegate()
        {
            physScene.PE.ResetBroadphasePool(physScene.World);
        });
    }

    // This causes the constraint solver cache to be cleared and reset.
    private static void ResetConstraintSolverTainted(BSScene pPhysScene, float v)
    {
        BSScene physScene = pPhysScene;
        physScene.TaintedObject(BSScene.DetailLogZero, "BSParam.ResetConstraintSolver", delegate()
        {
            physScene.PE.ResetConstraintSolver(physScene.World);
        });
    }
}
}
