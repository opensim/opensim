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
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

using OpenSim.Framework;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.PhysicsModules.SharedBase;

using Mono.Addins;
using Nini.Config;
using log4net;
using OpenMetaverse;

namespace OpenSim.Region.PhysicsModule.BulletS
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class ExtendedPhysics : INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static string LogHeader = "[EXTENDED PHYSICS]";

        // =============================================================
        // Since BulletSim is a plugin, this these values aren't defined easily in one place.
        // This table must correspond to an identical table in BSScene.

        // Per scene functions. See BSScene.

        // Per avatar functions. See BSCharacter.

        // Per prim functions. See BSPrim.
        public const string PhysFunctGetLinksetType = "BulletSim.GetLinksetType";
        public const string PhysFunctSetLinksetType = "BulletSim.SetLinksetType";
        public const string PhysFunctChangeLinkFixed = "BulletSim.ChangeLinkFixed";
        public const string PhysFunctChangeLinkType = "BulletSim.ChangeLinkType";
        public const string PhysFunctGetLinkType = "BulletSim.GetLinkType";
        public const string PhysFunctChangeLinkParams = "BulletSim.ChangeLinkParams";
        public const string PhysFunctAxisLockLimits = "BulletSim.AxisLockLimits";

        // =============================================================

        private IConfig Configuration { get; set; }
        private bool Enabled { get; set; }
        private Scene BaseScene { get; set; }
        private IScriptModuleComms Comms { get; set; }

        #region INonSharedRegionModule

        public string Name { get { return this.GetType().Name; } }

        public void Initialise(IConfigSource config)
        {
            BaseScene = null;
            Enabled = false;
            Configuration = null;
            Comms = null;

            try
            {
                if ((Configuration = config.Configs["ExtendedPhysics"]) != null)
                {
                    Enabled = Configuration.GetBoolean("Enabled", Enabled);
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("{0} Initialization error: {0}", LogHeader, e);
            }

            m_log.InfoFormat("{0} module {1} enabled", LogHeader, (Enabled ? "is" : "is not"));
        }

        public void Close()
        {
            if (BaseScene != null)
            {
                BaseScene.EventManager.OnObjectAddedToScene -= EventManager_OnObjectAddedToScene;
                BaseScene.EventManager.OnSceneObjectPartUpdated -= EventManager_OnSceneObjectPartUpdated;
                BaseScene = null;
            }
        }

        public void AddRegion(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
            if (BaseScene != null && BaseScene == scene)
            {
                Close();
            }
        }

        public void RegionLoaded(Scene scene)
        {
            if (!Enabled) return;

            BaseScene = scene;

            Comms = BaseScene.RequestModuleInterface<IScriptModuleComms>();
            if (Comms == null)
            {
                m_log.WarnFormat("{0} ScriptModuleComms interface not defined", LogHeader);
                Enabled = false;

                return;
            }
 
            // Register as LSL functions all the [ScriptInvocation] marked methods.
            Comms.RegisterScriptInvocations(this);
            Comms.RegisterConstants(this);

            // When an object is modified, we might need to update its extended physics parameters
            BaseScene.EventManager.OnObjectAddedToScene += EventManager_OnObjectAddedToScene;
            BaseScene.EventManager.OnSceneObjectPartUpdated += EventManager_OnSceneObjectPartUpdated;

        }

        public Type ReplaceableInterface { get { return null; } }

        #endregion // INonSharedRegionModule

        private void EventManager_OnObjectAddedToScene(SceneObjectGroup obj)
        {
        }

        // Event generated when some property of a prim changes.
        private void EventManager_OnSceneObjectPartUpdated(SceneObjectPart sop, bool isFullUpdate)
        {
        }

        [ScriptConstant]
        public const int PHYS_CENTER_OF_MASS =     1 << 0;

        [ScriptInvocation]
        public string physGetEngineType(UUID hostID, UUID scriptID)
        {
            string ret = string.Empty;

            if (BaseScene.PhysicsScene != null)
            {
                ret = BaseScene.PhysicsScene.EngineType;
            }

            return ret;
        }

        // Code for specifying params.
        // The choice if 14700 is arbitrary and only serves to catch parameter code misuse.
        [ScriptConstant]
        public const int PHYS_AXIS_LOCK_LINEAR     = 14700;
        [ScriptConstant]
        public const int PHYS_AXIS_LOCK_LINEAR_X   = 14701;
        [ScriptConstant]
        public const int PHYS_AXIS_LIMIT_LINEAR_X  = 14702;
        [ScriptConstant]
        public const int PHYS_AXIS_LOCK_LINEAR_Y   = 14703;
        [ScriptConstant]
        public const int PHYS_AXIS_LIMIT_LINEAR_Y  = 14704;
        [ScriptConstant]
        public const int PHYS_AXIS_LOCK_LINEAR_Z   = 14705;
        [ScriptConstant]
        public const int PHYS_AXIS_LIMIT_LINEAR_Z  = 14706;
        [ScriptConstant]
        public const int PHYS_AXIS_LOCK_ANGULAR    = 14707;
        [ScriptConstant]
        public const int PHYS_AXIS_LOCK_ANGULAR_X  = 14708;
        [ScriptConstant]
        public const int PHYS_AXIS_LIMIT_ANGULAR_X = 14709;
        [ScriptConstant]
        public const int PHYS_AXIS_LOCK_ANGULAR_Y  = 14710;
        [ScriptConstant]
        public const int PHYS_AXIS_LIMIT_ANGULAR_Y = 14711;
        [ScriptConstant]
        public const int PHYS_AXIS_LOCK_ANGULAR_Z  = 14712;
        [ScriptConstant]
        public const int PHYS_AXIS_LIMIT_ANGULAR_Z = 14713;
        [ScriptConstant]
        public const int PHYS_AXIS_UNLOCK_LINEAR   = 14714;
        [ScriptConstant]
        public const int PHYS_AXIS_UNLOCK_LINEAR_X = 14715;
        [ScriptConstant]
        public const int PHYS_AXIS_UNLOCK_LINEAR_Y = 14716;
        [ScriptConstant]
        public const int PHYS_AXIS_UNLOCK_LINEAR_Z = 14717;
        [ScriptConstant]
        public const int PHYS_AXIS_UNLOCK_ANGULAR  = 14718;
        [ScriptConstant]
        public const int PHYS_AXIS_UNLOCK_ANGULAR_X = 14719;
        [ScriptConstant]
        public const int PHYS_AXIS_UNLOCK_ANGULAR_Y = 14720;
        [ScriptConstant]
        public const int PHYS_AXIS_UNLOCK_ANGULAR_Z = 14721;
        [ScriptConstant]
        public const int PHYS_AXIS_UNLOCK           = 14722;
        // physAxisLockLimits()
        [ScriptInvocation]
        public int physAxisLock(UUID hostID, UUID scriptID, object[] parms)
        {
            int ret = -1;
            if (!Enabled) return ret;

            PhysicsActor rootPhysActor;
            if (GetRootPhysActor(hostID, out rootPhysActor))
            {
                object[] parms2 = AddToBeginningOfArray(rootPhysActor, null, parms);
                ret = MakeIntError(rootPhysActor.Extension(PhysFunctAxisLockLimits, parms2));
            }

            return ret;
        }

        [ScriptConstant]
        public const int PHYS_LINKSET_TYPE_CONSTRAINT  = 0;
        [ScriptConstant]
        public const int PHYS_LINKSET_TYPE_COMPOUND    = 1;
        [ScriptConstant]
        public const int PHYS_LINKSET_TYPE_MANUAL      = 2;

        [ScriptInvocation]
        public int physSetLinksetType(UUID hostID, UUID scriptID, int linksetType)
        {
            int ret = -1;
            if (!Enabled) return ret;

            // The part that is requesting the change.
            SceneObjectPart requestingPart = BaseScene.GetSceneObjectPart(hostID);

            if (requestingPart != null)
            {
                // The change is always made to the root of a linkset.
                SceneObjectGroup containingGroup = requestingPart.ParentGroup;
                SceneObjectPart rootPart = containingGroup.RootPart;

                if (rootPart != null)
                {
                    PhysicsActor rootPhysActor = rootPart.PhysActor;
                    if (rootPhysActor != null)
                    {
                        if (rootPhysActor.IsPhysical)
                        {
                            // Change a physical linkset by making non-physical, waiting for one heartbeat so all
                            //    the prim and linkset state is updated, changing the type and making the
                            //    linkset physical again.
                            containingGroup.ScriptSetPhysicsStatus(false);
                            Thread.Sleep(150);  // longer than one heartbeat tick

                            // A kludge for the moment.
                            // Since compound linksets move the children but don't generate position updates to the
                            //     simulator, it is possible for compound linkset children to have out-of-sync simulator
                            //     and physical positions. The following causes the simulator to push the real child positions
                            //     down into the physics engine to get everything synced.
                            containingGroup.UpdateGroupPosition(containingGroup.AbsolutePosition);
                            containingGroup.UpdateGroupRotationR(containingGroup.GroupRotation);

                            object[] parms2 = { rootPhysActor, null, linksetType };
                            ret = MakeIntError(rootPhysActor.Extension(PhysFunctSetLinksetType, parms2));
                            Thread.Sleep(150);  // longer than one heartbeat tick

                            containingGroup.ScriptSetPhysicsStatus(true);
                        }
                        else
                        {
                            // Non-physical linksets don't have a physical instantiation so there is no state to
                            //    worry about being updated.
                            object[] parms2 = { rootPhysActor, null, linksetType };
                            ret = MakeIntError(rootPhysActor.Extension(PhysFunctSetLinksetType, parms2));
                        }
                    }
                    else
                    {
                        m_log.WarnFormat("{0} physSetLinksetType: root part does not have a physics actor. rootName={1}, hostID={2}",
                                            LogHeader, rootPart.Name, hostID);
                    }
                }
                else
                {
                    m_log.WarnFormat("{0} physSetLinksetType: root part does not exist. RequestingPartName={1}, hostID={2}",
                                        LogHeader, requestingPart.Name, hostID);
                }
            }
            else
            {
                m_log.WarnFormat("{0} physSetLinsetType: cannot find script object in scene. hostID={1}", LogHeader, hostID);
            }
            return ret;
        }

        [ScriptInvocation]
        public int physGetLinksetType(UUID hostID, UUID scriptID)
        {
            int ret = -1;
            if (!Enabled) return ret;

            PhysicsActor rootPhysActor;
            if (GetRootPhysActor(hostID, out rootPhysActor))
            {
                object[] parms2 = { rootPhysActor, null };
                ret = MakeIntError(rootPhysActor.Extension(PhysFunctGetLinksetType, parms2));
            }
            else
            {
                m_log.WarnFormat("{0} physGetLinsetType: cannot find script object in scene. hostID={1}", LogHeader, hostID);
            }
            return ret;
        }

        [ScriptConstant]
        public const int PHYS_LINK_TYPE_FIXED  = 1234;
        [ScriptConstant]
        public const int PHYS_LINK_TYPE_HINGE  = 4;
        [ScriptConstant]
        public const int PHYS_LINK_TYPE_SPRING = 9;
        [ScriptConstant]
        public const int PHYS_LINK_TYPE_6DOF   = 6;
        [ScriptConstant]
        public const int PHYS_LINK_TYPE_SLIDER = 7;

        // physChangeLinkType(integer linkNum, integer typeCode)
        [ScriptInvocation]
        public int physChangeLinkType(UUID hostID, UUID scriptID, int linkNum, int typeCode)
        {
            int ret = -1;
            if (!Enabled) return ret;

            PhysicsActor rootPhysActor;
            PhysicsActor childPhysActor;

            if (GetRootAndChildPhysActors(hostID, linkNum, out rootPhysActor, out childPhysActor))
            {
                object[] parms2 = { rootPhysActor, childPhysActor, typeCode };
                ret = MakeIntError(rootPhysActor.Extension(PhysFunctChangeLinkType, parms2));
            }

            return ret;
        }

        // physGetLinkType(integer linkNum)
        [ScriptInvocation]
        public int physGetLinkType(UUID hostID, UUID scriptID, int linkNum)
        {
            int ret = -1;
            if (!Enabled) return ret;

            PhysicsActor rootPhysActor;
            PhysicsActor childPhysActor;

            if (GetRootAndChildPhysActors(hostID, linkNum, out rootPhysActor, out childPhysActor))
            {
                object[] parms2 = { rootPhysActor, childPhysActor };
                ret = MakeIntError(rootPhysActor.Extension(PhysFunctGetLinkType, parms2));
            }

            return ret;
        }

        // physChangeLinkFixed(integer linkNum)
        // Change the link between the root and the linkNum into a fixed, static physical connection.
        [ScriptInvocation]
        public int physChangeLinkFixed(UUID hostID, UUID scriptID, int linkNum)
        {
            int ret = -1;
            if (!Enabled) return ret;

            PhysicsActor rootPhysActor;
            PhysicsActor childPhysActor;

            if (GetRootAndChildPhysActors(hostID, linkNum, out rootPhysActor, out childPhysActor))
            {
                object[] parms2 = { rootPhysActor, childPhysActor , PHYS_LINK_TYPE_FIXED };
                ret = MakeIntError(rootPhysActor.Extension(PhysFunctChangeLinkType, parms2));
            }

            return ret;
        }

        // Code for specifying params.
        // The choice if 14400 is arbitrary and only serves to catch parameter code misuse.
        public const int PHYS_PARAM_MIN                    = 14401;

        [ScriptConstant]
        public const int PHYS_PARAM_FRAMEINA_LOC           = 14401;
        [ScriptConstant]
        public const int PHYS_PARAM_FRAMEINA_ROT           = 14402;
        [ScriptConstant]
        public const int PHYS_PARAM_FRAMEINB_LOC           = 14403;
        [ScriptConstant]
        public const int PHYS_PARAM_FRAMEINB_ROT           = 14404;
        [ScriptConstant]
        public const int PHYS_PARAM_LINEAR_LIMIT_LOW       = 14405;
        [ScriptConstant]
        public const int PHYS_PARAM_LINEAR_LIMIT_HIGH      = 14406;
        [ScriptConstant]
        public const int PHYS_PARAM_ANGULAR_LIMIT_LOW      = 14407;
        [ScriptConstant]
        public const int PHYS_PARAM_ANGULAR_LIMIT_HIGH     = 14408;
        [ScriptConstant]
        public const int PHYS_PARAM_USE_FRAME_OFFSET       = 14409;
        [ScriptConstant]
        public const int PHYS_PARAM_ENABLE_TRANSMOTOR      = 14410;
        [ScriptConstant]
        public const int PHYS_PARAM_TRANSMOTOR_MAXVEL      = 14411;
        [ScriptConstant]
        public const int PHYS_PARAM_TRANSMOTOR_MAXFORCE    = 14412;
        [ScriptConstant]
        public const int PHYS_PARAM_CFM                    = 14413;
        [ScriptConstant]
        public const int PHYS_PARAM_ERP                    = 14414;
        [ScriptConstant]
        public const int PHYS_PARAM_SOLVER_ITERATIONS      = 14415;
        [ScriptConstant]
        public const int PHYS_PARAM_SPRING_AXIS_ENABLE     = 14416;
        [ScriptConstant]
        public const int PHYS_PARAM_SPRING_DAMPING         = 14417;
        [ScriptConstant]
        public const int PHYS_PARAM_SPRING_STIFFNESS       = 14418;
        [ScriptConstant]
        public const int PHYS_PARAM_LINK_TYPE              = 14419;
        [ScriptConstant]
        public const int PHYS_PARAM_USE_LINEAR_FRAMEA      = 14420;
        [ScriptConstant]
        public const int PHYS_PARAM_SPRING_EQUILIBRIUM_POINT = 14421;

        public const int PHYS_PARAM_MAX                    = 14421;

        // Used when specifying a parameter that has settings for the three linear and three angular axis
        [ScriptConstant]
        public const int PHYS_AXIS_ALL = -1;
        [ScriptConstant]
        public const int PHYS_AXIS_LINEAR_ALL = -2;
        [ScriptConstant]
        public const int PHYS_AXIS_ANGULAR_ALL = -3;
        [ScriptConstant]
        public const int PHYS_AXIS_LINEAR_X  = 0;
        [ScriptConstant]
        public const int PHYS_AXIS_LINEAR_Y  = 1;
        [ScriptConstant]
        public const int PHYS_AXIS_LINEAR_Z  = 2;
        [ScriptConstant]
        public const int PHYS_AXIS_ANGULAR_X = 3;
        [ScriptConstant]
        public const int PHYS_AXIS_ANGULAR_Y = 4;
        [ScriptConstant]
        public const int PHYS_AXIS_ANGULAR_Z = 5;

        // physChangeLinkParams(integer linkNum, [ PHYS_PARAM_*, value, PHYS_PARAM_*, value, ...])
        [ScriptInvocation]
        public int physChangeLinkParams(UUID hostID, UUID scriptID, int linkNum, object[] parms)
        {
            int ret = -1;
            if (!Enabled) return ret;

            PhysicsActor rootPhysActor;
            PhysicsActor childPhysActor;

            if (GetRootAndChildPhysActors(hostID, linkNum, out rootPhysActor, out childPhysActor))
            {
                object[] parms2 = AddToBeginningOfArray(rootPhysActor, childPhysActor, parms);
                ret = MakeIntError(rootPhysActor.Extension(PhysFunctChangeLinkParams, parms2));
            }

            return ret;
        }

        private bool GetRootPhysActor(UUID hostID, out PhysicsActor rootPhysActor)
        {
            SceneObjectGroup containingGroup;
            SceneObjectPart rootPart;
            return GetRootPhysActor(hostID, out containingGroup, out rootPart, out rootPhysActor);
        }

        private bool GetRootPhysActor(UUID hostID, out SceneObjectGroup containingGroup, out SceneObjectPart rootPart, out PhysicsActor rootPhysActor)
        {
            bool ret = false;
            rootPhysActor = null;
            containingGroup = null;
            rootPart = null;

            SceneObjectPart requestingPart;

            requestingPart = BaseScene.GetSceneObjectPart(hostID);
            if (requestingPart != null)
            {
                // The type is is always on the root of a linkset.
                containingGroup = requestingPart.ParentGroup;
                if (containingGroup != null && !containingGroup.IsDeleted)
                {
                    rootPart = containingGroup.RootPart;
                    if (rootPart != null)
                    {
                        rootPhysActor = rootPart.PhysActor;
                        if (rootPhysActor != null)
                        {
                            ret = true;
                        }
                        else
                        {
                            m_log.WarnFormat("{0} GetRootAndChildPhysActors: Root part does not have a physics actor. rootName={1}, hostID={2}",
                                            LogHeader, rootPart.Name, hostID);
                        }
                    }
                    else
                    {
                        m_log.WarnFormat("{0} GetRootAndChildPhysActors: Root part does not exist. RequestingPartName={1}, hostID={2}",
                                        LogHeader, requestingPart.Name, hostID);
                    }
                }
                else
                {
                    m_log.WarnFormat("{0} GetRootAndChildPhysActors: Containing group missing or deleted. hostID={1}", LogHeader, hostID);
                }
            }
            else
            {
                m_log.WarnFormat("{0} GetRootAndChildPhysActors: cannot find script object in scene. hostID={1}", LogHeader, hostID);
            }

            return ret;
        }

        // Find the root and child PhysActors based on the linkNum.
        // Return 'true' if both are found and returned.
        private bool GetRootAndChildPhysActors(UUID hostID, int linkNum, out PhysicsActor rootPhysActor, out PhysicsActor childPhysActor)
        {
            bool ret = false;
            rootPhysActor = null;
            childPhysActor = null;

            SceneObjectGroup containingGroup;
            SceneObjectPart rootPart;

            if (GetRootPhysActor(hostID, out containingGroup, out rootPart, out rootPhysActor))
            {
                SceneObjectPart linkPart = containingGroup.GetLinkNumPart(linkNum);
                if (linkPart != null)
                {
                    childPhysActor = linkPart.PhysActor;
                    if (childPhysActor != null)
                    {
                        ret = true;
                    }
                    else
                    {
                        m_log.WarnFormat("{0} GetRootAndChildPhysActors: Link part has no physical actor. rootName={1}, hostID={2}, linknum={3}",
                                            LogHeader, rootPart.Name, hostID, linkNum);
                    }
                }
                else
                {
                    m_log.WarnFormat("{0} GetRootAndChildPhysActors: Could not find linknum part. rootName={1}, hostID={2}, linknum={3}",
                                        LogHeader, rootPart.Name, hostID, linkNum);
                }
            }
            else
            {
                m_log.WarnFormat("{0} GetRootAndChildPhysActors: Root part does not have a physics actor. rootName={1}, hostID={2}",
                                LogHeader, rootPart.Name, hostID);
            }

            return ret;
        }

        // Return an array of objects with the passed object as the first object of a new array
        private object[] AddToBeginningOfArray(object firstOne, object secondOne, object[] prevArray)
        {
            object[] newArray = new object[2 + prevArray.Length];
            newArray[0] = firstOne;
            newArray[1] = secondOne;
            prevArray.CopyTo(newArray, 2);
            return newArray;
        }

        // Extension() returns an object. Convert that object into the integer error we expect to return.
        private int MakeIntError(object extensionRet)
        {
            int ret = -1;
            if (extensionRet != null)
            {
                try
                {
                    ret = (int)extensionRet;
                }
                catch
                {
                    ret = -1;
                }
            }
            return ret;
        }
    }
}
