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
using OpenSim.Region.CoreModules;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

using Mono.Addins;
using Nini.Config;
using log4net;
using OpenMetaverse;

namespace OpenSim.Region.OptionalModules.Scripting
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
    public const string PhysFunctChangeLinkHinge = "BulletSim.ChangeLinkHinge";
    public const string PhysFunctChangeLinkSpring = "BulletSim.ChangeLinkSpring";
    public const string PhysFunctChangeLinkSlider = "BulletSim.ChangeLinkSlider";

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
    public static int PHYS_CENTER_OF_MASS =     1 << 0;

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

    [ScriptConstant]
    public static int PHYS_LINKSET_TYPE_CONSTRAINT  = 0;
    [ScriptConstant]
    public static int PHYS_LINKSET_TYPE_COMPOUND    = 1;
    [ScriptConstant]
    public static int PHYS_LINKSET_TYPE_MANUAL      = 2;

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
                Physics.Manager.PhysicsActor rootPhysActor = rootPart.PhysActor;
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

                        ret = (int)rootPhysActor.Extension(PhysFunctSetLinksetType, linksetType);
                        Thread.Sleep(150);  // longer than one heartbeat tick

                        containingGroup.ScriptSetPhysicsStatus(true);
                    }
                    else
                    {
                        // Non-physical linksets don't have a physical instantiation so there is no state to
                        //    worry about being updated.
                        ret = (int)rootPhysActor.Extension(PhysFunctSetLinksetType, linksetType);
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

        // The part that is requesting the change.
        SceneObjectPart requestingPart = BaseScene.GetSceneObjectPart(hostID);

        if (requestingPart != null)
        {
            // The type is is always on the root of a linkset.
            SceneObjectGroup containingGroup = requestingPart.ParentGroup;
            SceneObjectPart rootPart = containingGroup.RootPart;

            if (rootPart != null)
            {
                Physics.Manager.PhysicsActor rootPhysActor = rootPart.PhysActor;
                if (rootPhysActor != null)
                {
                    ret = (int)rootPhysActor.Extension(PhysFunctGetLinksetType);
                }
                else
                {
                    m_log.WarnFormat("{0} physGetLinksetType: root part does not have a physics actor. rootName={1}, hostID={2}",
                                        LogHeader, rootPart.Name, hostID);
                }
            }
            else
            {
                m_log.WarnFormat("{0} physGetLinksetType: root part does not exist. RequestingPartName={1}, hostID={2}",
                                    LogHeader, requestingPart.Name, hostID);
            }
        }
        else
        {
            m_log.WarnFormat("{0} physGetLinsetType: cannot find script object in scene. hostID={1}", LogHeader, hostID);
        }
        return ret;
    }

    // physChangeLinkFixed(integer linkNum)
    // Change the link between the root and the linkNum into a fixed, static physical connection.
    // This needs to change 'linkNum' into the physical object because lower level code has
    //     no access to the link numbers.
    [ScriptInvocation]
    public int physChangeLinkFixed(UUID hostID, UUID scriptID, int linkNum)
    {
        int ret = -1;
        if (!Enabled) return ret;

        // The part that is requesting the change.
        SceneObjectPart requestingPart = BaseScene.GetSceneObjectPart(hostID);

        if (requestingPart != null)
        {
            // The type is is always on the root of a linkset.
            SceneObjectGroup containingGroup = requestingPart.ParentGroup;
            SceneObjectPart rootPart = containingGroup.RootPart;

            if (rootPart != null)
            {
                Physics.Manager.PhysicsActor rootPhysActor = rootPart.PhysActor;
                if (rootPhysActor != null)
                {
                    SceneObjectPart linkPart = containingGroup.GetLinkNumPart(linkNum);
                    if (linkPart != null)
                    {
                        Physics.Manager.PhysicsActor linkPhysActor = linkPart.PhysActor;
                        if (linkPhysActor != null)
                        {
                            ret = (int)rootPhysActor.Extension(PhysFunctChangeLinkFixed, linkNum, linkPhysActor);
                        }
                        else
                        {
                            m_log.WarnFormat("{0} physChangeLinkFixed: Link part has no physical actor. rootName={1}, hostID={2}, linknum={3}",
                                                LogHeader, rootPart.Name, hostID, linkNum);
                        }
                    }
                    else
                    {
                        m_log.WarnFormat("{0} physChangeLinkFixed: Could not find linknum part. rootName={1}, hostID={2}, linknum={3}",
                                            LogHeader, rootPart.Name, hostID, linkNum);
                    }
                }
                else
                {
                    m_log.WarnFormat("{0} physChangeLinkFixed: Root part does not have a physics actor. rootName={1}, hostID={2}",
                                        LogHeader, rootPart.Name, hostID);
                }
            }
            else
            {
                m_log.WarnFormat("{0} physChangeLinkFixed: Root part does not exist. RequestingPartName={1}, hostID={2}",
                                    LogHeader, requestingPart.Name, hostID);
            }
        }
        else
        {
            m_log.WarnFormat("{0} physGetLinsetType: cannot find script object in scene. hostID={1}", LogHeader, hostID);
        }
        return ret;
    }

    [ScriptInvocation]
    public int physChangeLinkHinge(UUID hostID, UUID scriptID, int linkNum)
    {
        return -1;
    }

    [ScriptInvocation]
    public int physChangeLinkSpring(UUID hostID, UUID scriptID, int linkNum,
                        Vector3 frameInAloc, Quaternion frameInArot,
                        Vector3 frameInBloc, Quaternion frameInBrot,
                        Vector3 linearLimitLow, Vector3 linearLimitHigh,
                        Vector3 angularLimitLow, Vector3 angularLimitHigh
        )
    {
        int ret = -1;
        if (!Enabled) return ret;

        // The part that is requesting the change.
        SceneObjectPart requestingPart = BaseScene.GetSceneObjectPart(hostID);

        if (requestingPart != null)
        {
            // The type is is always on the root of a linkset.
            SceneObjectGroup containingGroup = requestingPart.ParentGroup;
            SceneObjectPart rootPart = containingGroup.RootPart;

            if (rootPart != null)
            {
                Physics.Manager.PhysicsActor rootPhysActor = rootPart.PhysActor;
                if (rootPhysActor != null)
                {
                    SceneObjectPart linkPart = containingGroup.GetLinkNumPart(linkNum);
                    if (linkPart != null)
                    {
                        Physics.Manager.PhysicsActor linkPhysActor = linkPart.PhysActor;
                        if (linkPhysActor != null)
                        {
                            ret = (int)rootPhysActor.Extension(PhysFunctChangeLinkSpring, linkNum, linkPhysActor,
                                                                    frameInAloc, frameInArot,
                                                                    frameInBloc, frameInBrot,
                                                                    linearLimitLow, linearLimitHigh,
                                                                    angularLimitLow, angularLimitHigh
                                                                        );
                        }
                        else
                        {
                            m_log.WarnFormat("{0} physChangeLinkFixed: Link part has no physical actor. rootName={1}, hostID={2}, linknum={3}",
                                                LogHeader, rootPart.Name, hostID, linkNum);
                        }
                    }
                    else
                    {
                        m_log.WarnFormat("{0} physChangeLinkFixed: Could not find linknum part. rootName={1}, hostID={2}, linknum={3}",
                                            LogHeader, rootPart.Name, hostID, linkNum);
                    }
                }
                else
                {
                    m_log.WarnFormat("{0} physChangeLinkFixed: Root part does not have a physics actor. rootName={1}, hostID={2}",
                                        LogHeader, rootPart.Name, hostID);
                }
            }
            else
            {
                m_log.WarnFormat("{0} physChangeLinkFixed: Root part does not exist. RequestingPartName={1}, hostID={2}",
                                    LogHeader, requestingPart.Name, hostID);
            }
        }
        else
        {
            m_log.WarnFormat("{0} physGetLinsetType: cannot find script object in scene. hostID={1}", LogHeader, hostID);
        }
        return ret;
    }

    [ScriptInvocation]
    public int physChangeLinkSlider(UUID hostID, UUID scriptID, int linkNum)
    {
        return 0;
    }
}
}
