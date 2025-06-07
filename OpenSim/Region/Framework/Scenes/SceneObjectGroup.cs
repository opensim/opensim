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

using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Region.PhysicsModules.SharedBase;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Xml;
using PermissionMask = OpenSim.Framework.PermissionMask;

namespace OpenSim.Region.Framework.Scenes
{
    // this is not the right place for this
    // for now it must match similar enums duplicated on scritp engines
    public enum ScriptEventCode : int
    {
        None = -1,

        attach = 0,
        state_exit = 1,
        timer = 2,
        touch = 3,
        collision = 4,
        collision_end = 5,
        collision_start = 6,
        control = 7,
        dataserver = 8,
        email = 9,
        http_response = 10,
        land_collision = 11,
        land_collision_end = 12,
        land_collision_start = 13,
        at_target = 14,
        listen = 15,
        money = 16,
        moving_end = 17,
        moving_start = 18,
        not_at_rot_target = 19,
        not_at_target = 20,
        touch_start = 21,
        object_rez = 22,
        remote_data = 23,
        at_rot_target = 24,
        transaction_result = 25,
        //
        //
        run_time_permissions = 28,
        touch_end = 29,
        state_entry = 30,
        //
        //
        changed = 33,
        link_message = 34,
        no_sensor = 35,
        on_rez = 36,
        sensor = 37,
        http_request = 38,

        path_update = 40,
        linkset_data = 41,

        // marks highest numbered event
        Size = 42
    }

    // this is not the right place for this
    // for now it must match similar enums duplicated on scritp engines
    // ignore this flags, do not use .net flags handle, it is bad
    [Flags]
    public enum scriptEvents : ulong
    {
        None = 0,
        attach = 1,
        state_exit = 1UL << 1,
        timer = 1UL << 2,
        touch = 1UL << 3,
        collision = 1UL << 4,
        collision_end = 1UL << 5,
        collision_start = 1UL << 6,
        control = 1UL << 7,
        dataserver = 1UL << 8,
        email = 1UL << 9,
        http_response = 1UL << 10,
        land_collision = 1UL << 11,
        land_collision_end = 1UL << 12,
        land_collision_start = 1UL << 13,
        at_target = 1UL << 14,
        listen = 1UL << 15,
        money = 1UL << 16,
        moving_end = 1UL << 17,
        moving_start = 1UL << 18,
        not_at_rot_target = 1UL << 19,
        not_at_target = 1UL << 20,
        touch_start = 1UL << 21,
        object_rez = 1UL << 22,
        remote_data = 1UL << 23,
        at_rot_target = 1UL << 24,
        transaction_result = 1UL << 25,
        //
        //
        run_time_permissions = 1UL << 28,
        touch_end = 1UL << 29,
        state_entry = 1UL << 30,
        //
        //
        changed = 1UL << 33,
        link_message = 1UL << 34,
        no_sensor = 1UL << 35,
        on_rez = 1UL << 36,
        sensor = 1UL << 37,
        http_request = 1UL << 38,

        path_update = 1UL << 40,
        linkset_data = 1UL << 41,

        anytouch = touch | touch_end | touch_start,
        anyTarget = at_target | not_at_target | at_rot_target | not_at_rot_target,
        anyobjcollision = collision | collision_end | collision_start,
        anylandcollision = land_collision | land_collision_end | land_collision_start
    }

    public struct scriptPosTarget
    {
        public Vector3 targetPos;
        public float tolerance;
        public int handle;
        public UUID scriptID;
    }

    public struct scriptRotTarget
    {
        public Quaternion targetRot;
        public float tolerance;
        public int handle;
        public UUID scriptID;
    }

    public delegate void PrimCountTaintedDelegate();

    /// <summary>
    /// A scene object group is conceptually an object in the scene.  The object is constituted of SceneObjectParts
    /// (often known as prims), one of which is considered the root part.
    /// </summary>
    public partial class SceneObjectGroup : EntityBase, ISceneObject, IDisposable
    {
        // Axis selection bitmask used by SetAxisRotation()
        // Just happen to be the same bits used by llSetStatus() and defined in ScriptBaseClass.
        public enum axisSelect : int
        {
            STATUS_ROTATE_X = 0x002,
            STATUS_ROTATE_Y = 0x004,
            STATUS_ROTATE_Z = 0x008,
            NOT_STATUS_ROTATE_X = 0xFD,
            NOT_STATUS_ROTATE_Y = 0xFB,
            NOT_STATUS_ROTATE_Z = 0xF7
        }

        // private PrimCountTaintedDelegate handlerPrimCountTainted = null;

        public bool IsViewerCachable
        {
            get
            {
                // needs more exclusion ?
                return(Backup && !IsTemporary && !inTransit && !UsesPhysics && !IsSelected && !IsAttachmentCheckFull() &&
                    !RootPart.Shape.MeshFlagEntry && // animations are not sent correctly for now
                    RootPart.KeyframeMotion is null &&
                    (DateTime.UtcNow.Ticks - timeLastChanged > 3000000000) //&& //3000000000 is 5min
                    //(DateTime.UtcNow.Ticks - timeLastChanged > 36000000000) //&& //36000000000 is one hour
                );
            }
        }

        /// <summary>
        /// Signal whether the non-inventory attributes of any prims in the group have changed
        /// since the group's last persistent backup
        /// </summary>
        private bool m_hasGroupChanged = false;
        private long timeFirstChanged = 0;
        private long timeLastChanged = 0;
        private long m_maxPersistTime = 0;
        private long m_minPersistTime = 0;

        /// <summary>
        /// This indicates whether the object has changed such that it needs to be repersisted to permenant storage
        /// (the database).
        /// </summary>
        /// <remarks>
        /// Ultimately, this should be managed such that region modules can change it at the end of a set of operations
        /// so that either all changes are preserved or none at all.  However, currently, a large amount of internal
        /// code will set this anyway when some object properties are changed.
        /// </remarks>
        public bool HasGroupChanged
        {
            set
            {
                if (value)
                {
                    if (Backup)
                        m_scene.SceneGraph.FireChangeBackup(this);

                    timeLastChanged = DateTime.UtcNow.Ticks;
                    if (!m_hasGroupChanged)
                        timeFirstChanged = timeLastChanged;
                    if (m_rootPart is not null && m_scene is not null)
                    {
                        if (m_scene.GetRootAgentCount() == 0)
                        {
                            //If the region is empty, this change has been made by an automated process
                            //and thus we delay the persist time by a random amount between 1.5 and 2.5.

                            float factor = 2.0f;
                            m_maxPersistTime = (long)(m_scene.m_persistAfter * factor);
                            m_minPersistTime = (long)(m_scene.m_dontPersistBefore * factor);
                        }
                        else
                        {
                            m_maxPersistTime = m_scene.m_persistAfter;
                            m_minPersistTime = m_scene.m_dontPersistBefore;
                        }
                    }
                }
                m_hasGroupChanged = value;

                //m_log.DebugFormat(
                //    "[SCENE OBJECT GROUP]: HasGroupChanged set to {0} for {1} {2}", m_hasGroupChanged, Name, LocalId);
            }

            get { return m_hasGroupChanged; }
        }

        public bool TemporaryInstance = false;

        private bool m_groupContainsForeignPrims = false;

        /// <summary>
        /// Whether the group contains prims that came from a different group. This happens when
        /// linking or delinking groups. The implication is that until the group is persisted,
        /// the prims in the database still use the old SceneGroupID. That's a problem if the group
        /// is deleted, because we delete groups by searching for prims by their SceneGroupID.
        /// </summary>
        public bool GroupContainsForeignPrims
        {
            private set
            {
                m_groupContainsForeignPrims = value;
                if (m_groupContainsForeignPrims)
                    HasGroupChanged = true;
            }

            get { return m_groupContainsForeignPrims; }
        }

        public bool HasGroupChangedDueToDelink { get; set; }

        private bool isTimeToPersist()
        {
            if (IsSelected || IsDeleted || IsAttachment)
                return false;
            if (!m_hasGroupChanged)
                return false;
            if (m_scene.ShuttingDown)
                return true;

            if (m_minPersistTime == 0 || m_maxPersistTime == 0)
            {
                m_maxPersistTime = m_scene.m_persistAfter;
                m_minPersistTime = m_scene.m_dontPersistBefore;
            }

            long currentTime = DateTime.UtcNow.Ticks;

            if (timeLastChanged == 0) timeLastChanged = currentTime;
            if (timeFirstChanged == 0) timeFirstChanged = currentTime;

            return currentTime - timeLastChanged > m_minPersistTime || currentTime - timeFirstChanged > m_maxPersistTime;
        }

        /// <summary>
        /// Is this scene object acting as an attachment?
        /// </summary>
        public bool IsAttachment
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set;
        }

        /// <summary>
        /// The avatar to which this scene object is attached.
        /// </summary>
        /// <remarks>
        /// If we're not attached to an avatar then this is UUID.Zero
        /// </remarks>
        public UUID AttachedAvatar { get; set; }

        /// <summary>
        /// Attachment point of this scene object to an avatar.
        /// </summary>
        /// <remarks>
        /// 0 if we're not attached to anything
        /// </remarks>
        public uint AttachmentPoint
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_rootPart.Shape.State;
            }

            set
            {
                IsAttachment = value != 0;
                m_rootPart.Shape.State = (byte)value;
            }
        }

        /// <summary>
        /// If this scene object has an attachment point then indicate whether there is a point where
        /// attachments are perceivable by avatars other than the avatar to which this object is attached.
        /// </summary>
        /// <remarks>
        /// HUDs are not perceivable by other avatars.
        /// </remarks>
        public bool HasPrivateAttachmentPoint
        {
            get
            {
                return AttachmentPoint >= (uint)OpenMetaverse.AttachmentPoint.HUDCenter2
                    && AttachmentPoint <= (uint)OpenMetaverse.AttachmentPoint.HUDBottomRight;
            }
        }

        public void ClearPartAttachmentData()
        {
            AttachmentPoint = 0;

            // Don't zap trees
            if (RootPart.Shape.PCode == (byte)PCode.Tree ||
                RootPart.Shape.PCode == (byte)PCode.NewTree)
                return;

            // Even though we don't use child part state parameters for attachments any more, we still need to set
            // these to zero since having them non-zero in rezzed scene objects will crash some clients.  Even if
            // we store them correctly, scene objects that we receive from elsewhere might not.
            foreach (SceneObjectPart part in Parts)
                part.Shape.State = 0;
        }

        /// <summary>
        /// Is this scene object phantom?
        /// </summary>
        /// <remarks>
        /// Updating must currently take place through UpdatePrimFlags()
        /// </remarks>
        public bool IsPhantom
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (RootPart.Flags & PrimFlags.Phantom) != 0; }
        }

        /// <summary>
        /// Does this scene object use physics?
        /// </summary>
        /// <remarks>
        /// Updating must currently take place through UpdatePrimFlags()
        /// </remarks>
        public bool UsesPhysics
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (RootPart.Flags & PrimFlags.Physics) != 0; }
        }


        /// <summary>
        /// Is this scene object temporary?
        /// </summary>
        /// <remarks>
        /// Updating must currently take place through UpdatePrimFlags()
        /// </remarks>
        public bool IsTemporary
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (RootPart.Flags & PrimFlags.TemporaryOnRez) != 0; }
        }

        public bool IsVolumeDetect
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return RootPart.VolumeDetectActive; }
        }

        /// <summary>
        /// Is this entity set to be saved in persistent storage?
        /// </summary>
        public bool Backup { get; private set; }

        protected MapAndArray<UUID, SceneObjectPart> m_parts = new();

        protected ulong m_regionHandle;
        protected SceneObjectPart m_rootPart;
        // private Dictionary<UUID, scriptEvents> m_scriptEvents = new Dictionary<UUID, scriptEvents>();

        private Dictionary<int, scriptPosTarget> m_targets = new();
        private Dictionary<int, scriptRotTarget> m_rotTargets = new();
        private Dictionary<UUID, List<int>> m_targetsByScript = new();

        public Dictionary<int, scriptPosTarget> AtTargets
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_targets; }
        }

        public Dictionary<int, scriptRotTarget> RotTargets
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_rotTargets; }
        }

        private bool m_scriptListens_atTarget;
        private bool m_scriptListens_notAtTarget;
        private bool m_scriptListens_atRotTarget;
        private bool m_scriptListens_notAtRotTarget;

        public bool m_dupeInProgress = false;
        internal Dictionary<UUID, string> m_savedScriptState;

        public string RezStringParameter = null;
        public UUID MonitoringObject { get; set; }

        #region Properties

        /// <summary>
        /// The name of an object grouping is always the same as its root part
        /// </summary>
        public override string Name
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return RootPart.Name; }
            set { RootPart.Name = value; }
        }

        public string Description
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return RootPart.Description; }
            set { RootPart.Description = value; }
        }

        /// <summary>
        /// Added because the Parcel code seems to use it
        /// but not sure a object should have this
        /// as what does it tell us? that some avatar has selected it (but not what Avatar/user)
        /// think really there should be a list (or whatever) in each scenepresence
        /// saying what prim(s) that user has selected.
        /// </summary>
        protected bool m_isSelected = false;

        /// <summary>
        /// Number of prims in this group
        /// </summary>
        public int PrimCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_parts.Count; }
        }

        //protected Quaternion m_rotation = Quaternion.Identity;

        //public virtual Quaternion Rotation
        //{
        //   get { return m_rotation; }
        //   set { m_rotation = value; }
        //}

        public Quaternion GroupRotation
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_rootPart.RotationOffset; }
        }

        public Vector3 GroupScale
        {
            get
            {
                SceneObjectPart[] parts = m_parts.GetArray();
                SceneObjectPart part;
                Vector3 partscale;

                float ftmp;
                float minScaleX = float.MaxValue;
                float minScaleY = float.MaxValue;
                float minScaleZ = float.MaxValue;
                float maxScaleX = 0f;
                float maxScaleY = 0f;
                float maxScaleZ = 0f;

                for (int i = 0; i < parts.Length; i++)
                {
                    part = parts[i];
                    partscale = part.Scale + part.OffsetPosition;

                    ftmp = partscale.X;
                    if (ftmp < minScaleX)
                        minScaleX = ftmp;
                    if (ftmp > maxScaleX)
                        maxScaleX = ftmp;

                    ftmp = partscale.Y;
                    if (ftmp < minScaleY)
                        minScaleY = ftmp;
                    if (ftmp > maxScaleY)
                        maxScaleY = ftmp;

                    ftmp = partscale.Z;
                    if (ftmp < minScaleZ)
                        minScaleZ = ftmp;
                    if (ftmp > maxScaleZ)
                        maxScaleZ = ftmp;
                }

                partscale.X = (minScaleX > maxScaleX) ? minScaleX : maxScaleX;
                partscale.Y = (minScaleY > maxScaleY) ? minScaleY : maxScaleY;
                partscale.Z = (minScaleZ > maxScaleZ) ? minScaleZ : maxScaleZ;

                return partscale;
            }
        }

        public UUID GroupID
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_rootPart.GroupID; }
            set { m_rootPart.GroupID = value; }
        }

        public SceneObjectPart[] Parts
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_parts.GetArray(); }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsPart(UUID partID)
        {
            return m_parts.ContainsKey(partID);
        }

        /// <summary>
        /// Does this group contain the given part?
        /// <param name="localID"></param>
        /// <returns></returns>
        public bool ContainsPart(uint localID)
        {
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].LocalId == localID)
                    return true;
            }

            return false;
        }

        /// <value>
        /// The root part of this scene object
        /// </value>
        public SceneObjectPart RootPart
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_rootPart; }
        }

        public ulong RegionHandle
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_regionHandle; }
            set
            {
                m_regionHandle = value;
                SceneObjectPart[] parts = m_parts.GetArray();
                for (int i = 0; i < parts.Length; i++)
                    parts[i].RegionHandle = value;
            }
        }

        /// <summary>
        /// Check both the attachment property and the relevant properties of the underlying root part.
        /// </summary>
        /// <remarks>
        /// This is necessary in some cases, particularly when a scene object has just crossed into a region and doesn't
        /// have the IsAttachment property yet checked.
        ///
        /// FIXME: However, this should be fixed so that this property
        /// propertly reflects the underlying status.
        /// </remarks>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAttachmentCheckFull()
        {
            return (IsAttachment ||
                (m_rootPart.Shape.PCode == (byte)PCodeEnum.Primitive && m_rootPart.Shape.State != 0));
        }

        private struct avtocrossInfo
        {
            public ScenePresence av;
            public uint ParentID;
        }

        public LinksetData LinksetData;

        public bool inTransit = false;
        private delegate SceneObjectGroup SOGCrossDelegate(SceneObjectGroup sog,Vector3 pos, TeleportObjectData tpData);

        /// <summary>
        /// The absolute position of this scene object in the scene
        /// </summary>
        public override Vector3 AbsolutePosition
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_rootPart.GroupPosition; }
            set
            {
                Vector3 val = value;
                if (Scene is not null
                        && !Scene.PositionIsInCurrentRegion(val)
                        && !IsAttachmentCheckFull()
                        && !Scene.LoadingPrims
                        && !Scene.DisableObjectTransfer
                    )
                {
                    if (!inTransit)
                    {
                        inTransit = true;
                        SceneObjectGroup sog = this;

                        Util.FireAndForget(delegate
                        {
                            sog = CrossAsync(sog, val, null);
                            CrossAsyncCompleted(sog);
                        }, null, "ObjCross-"+sog.UUID.ToString(), false);
                    }
                    return;
                }

                if (RootPart.GetStatusSandbox())
                {
                    if (Vector3.DistanceSquared(RootPart.StatusSandboxPos, value) > 100)
                    {
                        RootPart.ScriptSetPhysicsStatus(false);
                        Scene?.SimChat(Utils.StringToBytes("Hit Sandbox Limit"),
                                  ChatTypeEnum.DebugChannel, 0x7FFFFFFF, RootPart.AbsolutePosition, Name, UUID, false);
                        return;
                    }
                }

                bool triggerScriptEvent;
                if (m_dupeInProgress || IsDeleted)
                    triggerScriptEvent = false;
                else
                    triggerScriptEvent = !m_rootPart.GroupPosition.ApproxEquals(val, 1e-3f);

                m_rootPart.GroupPosition = val;

                // Restuff the new GroupPosition into each child SOP of the linkset.
                // this is needed because physics may not have linksets but just loose SOPs in world

                SceneObjectPart[] parts = m_parts.GetArray();
                foreach (SceneObjectPart part in parts)
                {
                    if (part != m_rootPart)
                        part.GroupPosition = val;
                }

                foreach (ScenePresence av in m_sittingAvatars)
                {
                    av.sitSOGmoved();
                }

                // now that position is changed tell it to scripts
                if (triggerScriptEvent && (ScriptEvents & scriptEvents.changed) != 0)
                {
                    foreach (SceneObjectPart part in parts)
                    {
                        part.TriggerScriptChangedEvent(Changed.POSITION);
                    }
                }

                Scene?.EventManager.TriggerParcelPrimCountTainted();
            }
        }

        private SceneObjectGroup CrossAsync(SceneObjectGroup sog, Vector3 val, TeleportObjectData tpdata)
        {
            Scene sogScene = sog.m_scene;
            SceneObjectPart root = sog.RootPart;

            bool isTeleport = tpdata is not null;

            if(!isTeleport)
            {
                if (root.DIE_AT_EDGE)
                {
                    try
                    {
                        sogScene.DeleteSceneObject(sog, false);
                    }
                    catch (Exception)
                    {
                        m_log.Warn("[SCENE]: exception when trying to remove the prim that crossed the border.");
                    }
                    return sog;
                }

                if (root.RETURN_AT_EDGE)
                {
                    // We remove the object here
                    try
                    {
                        List<uint> localIDs = new(){root.LocalId};
                        sogScene.AddReturn(sog.OwnerID, sog.Name, sog.AbsolutePosition,
                            "Returned at region cross");
                        sogScene.DeRezObjects(null, localIDs, UUID.Zero, DeRezAction.Return, UUID.Zero, false);
                    }
                    catch (Exception)
                    {
                        m_log.Warn("[SCENE]: exception when trying to return the prim that crossed the border.");
                    }
                    return sog;
                }
            }

            //if(!m_scene.IsRunning)
            //    return sog;

            root.KeyframeMotion?.StartCrossingCheck();

            root.PhysActor?.CrossingStart();

            IEntityTransferModule entityTransfer = sogScene.RequestModuleInterface<IEntityTransferModule>();

            if (entityTransfer is null)
                return sog;

            Vector3 newpos = Vector3.Zero;
            OpenSim.Services.Interfaces.GridRegion destination = null;

            destination = entityTransfer.GetObjectDestination(sog, val, out newpos);
            if (destination is null)
                return sog;

            if (sog.m_sittingAvatars.Count == 0)
            {
                if(entityTransfer.CrossPrimGroupIntoNewRegion(destination, newpos, sog, !isTeleport, true))
                    return null;
                return sog;
            }

            string reason = string.Empty;
            EntityTransferContext ctx = new();

            Vector3 curPos = root.GroupPosition;
            foreach (ScenePresence av in sog.m_sittingAvatars)
            {
                // We need to cross these agents. First, let's find
                // out if any of them can't cross for some reason.
                // We have to deny the crossing entirely if any
                // of them are banned. Alternatively, we could
                // unsit banned agents....

                // We set the avatar position as being the object
                // position to get the region to send to
                if(av.IsNPC)
                    continue;

                if(av.IsInTransit)
                    return sog;

                if(!entityTransfer.checkAgentAccessToRegion(av, destination, newpos, ctx, out reason))
                    return sog;

                m_log.Debug($"[SCENE OBJECT]: Avatar {av.Name} needs to be crossed to {destination.RegionName}");
            }

            // We unparent the SP quietly so that it won't
            // be made to stand up

            List<avtocrossInfo> avsToCross = new();
            List<ScenePresence> avsToCrossFar = new();
            ulong destHandle = destination.RegionHandle;
            List<ScenePresence> sittingAvatars = GetSittingAvatars();
            foreach (ScenePresence av in sittingAvatars)
            {
                byte cflags = 1;

                avtocrossInfo avinfo = new();
                SceneObjectPart parentPart = sogScene.GetSceneObjectPart(av.ParentID);
                if (parentPart is not null)
                {
                    av.ParentUUID = parentPart.UUID;
                    if(parentPart.SitTargetAvatar.Equals(av.UUID))
                        cflags = 7; // low 3 bits set
                    else
                        cflags = 3;
                }
                if(!av.knowsNeighbourRegion(destHandle))
                    cflags |= 8;

                // 1 is crossing
                // 2 is sitting
                // 4 is sitting at sittarget
                // 8 far crossing

                avinfo.av = av;
                avinfo.ParentID = av.ParentID;
                avsToCross.Add(avinfo);

                if(!av.knowsNeighbourRegion(destHandle))
                {
                   cflags |= 8;
                    avsToCrossFar.Add(av);
                }

                if(av.IsNPC)
                    av.m_crossingFlags = 0;
                else
                    av.m_crossingFlags = cflags;

                av.PrevSitOffset = av.OffsetPosition;
                av.ParentID = 0;
            }

            Vector3 vel = root.Velocity;
            Vector3 avel = root.AngularVelocity;
            Vector3 acc = root.Acceleration;
            Quaternion ori = root.RotationOffset;

            if(isTeleport)
            {
                root.Stop();
                sogScene.ForEachScenePresence(delegate(ScenePresence av)
                {
                    av.ControllingClient.SendEntityUpdate(root,PrimUpdateFlags.SendInTransit);
                    av.ControllingClient.SendEntityTerseUpdateImmediate(root);
                });

                root.Velocity = tpdata.vel;
                root.AngularVelocity = tpdata.avel;
                root.Acceleration = tpdata.acc;
                root.RotationOffset = tpdata.ori;
            }

            if (entityTransfer.CrossPrimGroupIntoNewRegion(destination, newpos, sog, true, false))
            {
                if(isTeleport)
                {
                    sogScene.ForEachScenePresence(delegate(ScenePresence oav)
                    {
                        if(sittingAvatars.Contains(oav))
                            return;
                        if(oav.knowsNeighbourRegion(destHandle))
                            return;
                        oav.ControllingClient.SendEntityUpdate(root, PrimUpdateFlags.Kill);
                        foreach (ScenePresence sav in sittingAvatars)
                        {
                            sav.SendKillTo(oav);
                        }
                    });
                }
                bool crossedfar = false;
                foreach (ScenePresence av in avsToCrossFar)
                {
                   if(entityTransfer.CrossAgentCreateFarChild(av,destination, newpos, ctx))
                       crossedfar = true;
                   else
                    av.m_crossingFlags = 0;
                }

                if(crossedfar)
                    Thread.Sleep(1000);

                foreach (avtocrossInfo avinfo in avsToCross)
                {
                    ScenePresence av = avinfo.av;
                    av.IsInLocalTransit = true;
                    av.IsInTransit = true;
                    m_log.Debug($"[SCENE OBJECT]: Crossing avatar {av.Name} to {val}");

                    if(av.m_crossingFlags > 0)
                        entityTransfer.CrossAgentToNewRegionAsync(av, newpos, destination, false, ctx);

                    if (av.IsChildAgent)
                    {
                        // avatar crossed do some extra cleanup
                        if (av.ParentUUID.IsNotZero())
                        {
                            av.ClearControls();
                            av.ParentPart = null;
                        }
                        av.ParentUUID = UUID.Zero;
                        av.ParentPart = null;
                        // In any case
                        av.IsInTransit = false;
                        av.m_crossingFlags = 0;
                        m_log.Debug($"[SCENE OBJECT]: Crossing agent {av.Firstname} {av.Lastname} completed.");
                    }
                    else
                    {
                        // avatar cross failed we need do dedicated standUp
                        // part of it was done at CrossAgentToNewRegionAsync
                        // so for now just remove the sog controls
                        // this may need extra care
                        av.UnRegisterSeatControls(sog.UUID);
                        av.ParentUUID = UUID.Zero;
                        av.ParentPart = null;
                        Vector3 oldp = curPos;
                        oldp.X = Utils.Clamp(oldp.X, 0.5f, sog.m_scene.RegionInfo.RegionSizeX - 0.5f);
                        oldp.Y = Utils.Clamp(oldp.Y, 0.5f, sog.m_scene.RegionInfo.RegionSizeY - 0.5f);
                        av.AbsolutePosition = oldp;
                        av.m_crossingFlags = 0;
                        av.sitAnimation = "SIT";
                        av.IsInTransit = false;

                        av.Animator?.SetMovementAnimations("STAND");
                        av.AddToPhysicalScene(false);
                        sogScene.ForEachScenePresence(delegate(ScenePresence oav)
                            {
                                if(sittingAvatars.Contains(oav))
                                    return;
                                if(oav.knowsNeighbourRegion(destHandle))
                                    av.SendAvatarDataToAgent(oav);
                                else
                                {
                                    av.SendAvatarDataToAgent(oav);
                                    av.SendAppearanceToAgent(oav);
                                    
                                    av.Animator?.SendAnimPackToClient(oav.ControllingClient);
                                    av.SendAttachmentsToAgentNF(oav); // not ok
                                }
                            });
                        m_log.Debug($"[SCENE OBJECT]: Crossing agent {av.Firstname} {av.Lastname} failed.");
                    }
                }

                if(crossedfar)
                {
                    Thread.Sleep(10000);
                    foreach (ScenePresence av in avsToCrossFar)
                    {
                        if(av.IsChildAgent)
                        {
                            av.Scene.CloseAgent(av.UUID, false);
                        }
                        else
                            av.RemoveNeighbourRegion(destHandle);
                    }
                }
                avsToCrossFar.Clear();
                avsToCross.Clear();
                sog.RemoveScriptInstances(true);
                sog.Dispose();
                return null;
            }
            else
            {
                if(isTeleport)
                {
                    if((tpdata.flags & OSTPOBJ_STOPONFAIL) == 0)
                    {
                        root.Velocity = vel;
                        root.AngularVelocity = avel;
                        root.Acceleration = acc;
                    }
                    root.RotationOffset = ori;
                }
                foreach (avtocrossInfo avinfo in avsToCross)
                {
                    ScenePresence av = avinfo.av;
                    av.ParentUUID = UUID.Zero;
                    av.ParentID = avinfo.ParentID;
                    av.m_crossingFlags = 0;
                }
            }
            avsToCross.Clear();
            return sog;
        }

        public void CrossAsyncCompleted(SceneObjectGroup sog)
        {
            if (sog is null || sog.IsDeleted)
                return;

            SceneObjectPart rootp = sog.m_rootPart;

            Vector3 oldp = rootp.GroupPosition;
            oldp.X = Utils.Clamp(oldp.X, 0.5f, sog.m_scene.RegionInfo.RegionSizeX - 0.5f);
            oldp.Y = Utils.Clamp(oldp.Y, 0.5f, sog.m_scene.RegionInfo.RegionSizeY - 0.5f);
            rootp.GroupPosition = oldp;

            rootp.Stop();

            SceneObjectPart[] parts = sog.m_parts.GetArray();
            foreach (SceneObjectPart part in parts)
            {
                if (part != rootp)
                    part.GroupPosition = oldp;
            }

            foreach (ScenePresence av in sog.m_sittingAvatars)
            {
                av.sitSOGmoved();
            }

            sog.m_rootPart.KeyframeMotion?.CrossingFailure();
            sog.RootPart.PhysActor?.CrossingFailure();

            sog.inTransit = false;
            AttachToBackup();
            sog.ScheduleGroupForUpdate(PrimUpdateFlags.FullUpdatewithAnimMatOvr);
        }

        private class TeleportObjectData
        {
            public int flags;
            public Vector3 vel;
            public Vector3 avel;
            public Vector3 acc;
            public Quaternion ori;
            public UUID sourceID;
        }

        // copy from LSL_constants.cs
        const int OSTPOBJ_STOPATTARGET   = 0x1; // stops at destination
        const int OSTPOBJ_STOPONFAIL     = 0x2; // stops at start if tp fails
        const int OSTPOBJ_SETROT         = 0x4; // the rotation is the final rotation, otherwise is a added rotation

        public int TeleportObject(UUID sourceID, Vector3 targetPosition, Quaternion rotation, int flags)
        {
            if(inTransit || IsDeleted || IsAttachmentCheckFull() || IsSelected || Scene is null)
                return -1;

            inTransit = true;
            
            PhysicsActor pa = RootPart.PhysActor;
            if(/*pa is null  ||*/ RootPart.KeyframeMotion is not null /*|| m_sittingAvatars.Count == 0*/)
            {
                inTransit = false;
                return -1;
            }

            bool stop = (flags & OSTPOBJ_STOPATTARGET) != 0;
            bool setrot = (flags & OSTPOBJ_SETROT) != 0;

            rotation.Normalize();

            Quaternion currentRot = RootPart.RotationOffset;
            if(setrot)
                rotation = Quaternion.Conjugate(currentRot) * rotation;

            bool dorot = setrot || (Math.Abs(rotation.W) < 0.99999);

            Vector3 vel = Vector3.Zero;
            Vector3 avel = Vector3.Zero;
            Vector3 acc = Vector3.Zero;

            if(!stop)
            {
                vel = RootPart.Velocity;
                avel = RootPart.AngularVelocity;
                acc = RootPart.Acceleration;
            }
            Quaternion ori = RootPart.RotationOffset;

            if(dorot)
            {
                if(!stop)
                {
                    vel *= rotation;
                    avel *= rotation;
                    acc *= rotation;
                }
                ori *= rotation;
            }

            if(Scene.PositionIsInCurrentRegion(targetPosition))
            {
                if(Scene.InTeleportTargetsCoolDown(UUID, sourceID, 1000)) 
                {
                    inTransit = false;
                    return -2;
                }

                Vector3 curPos = AbsolutePosition;
                ILandObject curLand = Scene.LandChannel.GetLandObject(curPos.X, curPos.Y);
                float posX = targetPosition.X;
                float posY = targetPosition.Y;
                ILandObject land = Scene.LandChannel.GetLandObject(posX, posY);
                if(land is not null && land != curLand)
                {
                    if(!Scene.Permissions.CanObjectEnterWithScripts(this, land))
                    {
                        inTransit = false;
                        return -3;
                    }

                    UUID agentID;
                    foreach (ScenePresence av in m_sittingAvatars)
                    {
                        agentID = av.UUID;
                        if(land.IsRestrictedFromLand(agentID) || land.IsBannedFromLand(agentID))
                        {
                            inTransit = false;
                            return -4;
                        }
                    }
                }

                RootPart.Velocity = vel;
                RootPart.AngularVelocity = avel;
                RootPart.Acceleration = acc;
                RootPart.RotationOffset = ori;

                Vector3 s = RootPart.Scale * RootPart.RotationOffset;
                float h = Scene.GetGroundHeight(posX, posY) + 0.5f * (float)Math.Abs(s.Z) + 0.01f;
                if(targetPosition.Z < h)
                    targetPosition.Z = h;

                inTransit = false;
                AbsolutePosition = targetPosition;
                RootPart.ScheduleTerseUpdate();
                return 1;
            }

            if(Scene.InTeleportTargetsCoolDown(UUID, sourceID, 20000)) 
            {
                inTransit = false;
                return -1;
            }

            TeleportObjectData tdata = new()
            {
                flags = flags,
                vel = vel,
                avel = avel,
                acc = acc,
                ori = ori,
                sourceID = sourceID
            };

            SceneObjectGroup sog = this;
            Util.FireAndForget(delegate
            {
                sog = CrossAsync(sog, targetPosition, tdata);
                CrossAsyncCompleted(sog);
            }, null, "ObjTeleport-" + sog.UUID.ToString(), false);
            return 0;
        }

        public override Vector3 Velocity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return RootPart.Velocity; }
            set { RootPart.Velocity = value; }
        }

        public override uint LocalId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_rootPart.LocalId; }
            set { m_rootPart.LocalId = value; }
        }

        public override UUID UUID
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_rootPart.UUID; }
            set
            {
                lock (m_parts.SyncRoot)
                {
                    m_parts.Remove(m_rootPart.UUID);
                    m_rootPart.UUID = value;
                    m_parts.Add(value, m_rootPart);
                }
            }
        }

        public UUID LastOwnerID
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_rootPart.LastOwnerID; }
            set { m_rootPart.LastOwnerID = value; }
        }

        public UUID RezzerID
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_rootPart.RezzerID; }
            set { m_rootPart.RezzerID = value; }
        }

        public UUID OwnerID
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_rootPart.OwnerID; }
            set { m_rootPart.OwnerID = value; }
        }

        public float Damage
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_rootPart.Damage; }
            set { m_rootPart.Damage = value; }
        }

        public Color Color
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_rootPart.Color; }
            set { m_rootPart.Color = value; }
        }

        public string Text
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_rootPart.Text.Length > 255 ? m_rootPart.Text[..255] : m_rootPart.Text;
            }
            set { m_rootPart.Text = value; }
        }

        /// <summary>
        /// If set to true then the scene object can be backed up in principle, though this will only actually occur
        /// if Backup is set.  If false then the scene object will never be backed up, Backup will always be false.
        /// </summary>
        protected virtual bool CanBeBackedUp
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return true; }
        }

        public bool IsSelected
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_isSelected; }
            set
            {
                m_isSelected = value;
                // Tell physics engine that group is selected

                // this is not right
                // but ode engines should only really need to know about root part
                // so they can put entire object simulation on hold and not colliding
                // keep as was for now

                PhysicsActor pa = m_rootPart.PhysActor;
                if (pa is not null)
                {
                    pa.Selected = value;

                    // Pass it on to the children.
                    SceneObjectPart[] parts = m_parts.GetArray();
                    for (int i = 0; i < parts.Length; i++)
                    {
                        SceneObjectPart child = parts[i];

                        PhysicsActor childPa = child.PhysActor;
                        if (childPa is not null)
                            childPa.Selected = value;
                    }
                }
                if (RootPart.KeyframeMotion is not null)
                    RootPart.KeyframeMotion.Selected = value;
            }
        }

        public void PartSelectChanged(bool partSelect)
        {
            // any part selected makes group selected
            if (m_isSelected == partSelect)
                return;

            if (partSelect)
            {
                IsSelected = partSelect;
                //if (!IsAttachment)
                //    ScheduleGroupForFullUpdate();
            }
            else
            {
                // bad bad bad 2 heavy for large linksets
                // since viewer does send lot of (un)selects
                // this needs to be replaced by a specific list or count ?
                // but that will require extra code in several places

                SceneObjectPart[] parts = m_parts.GetArray();
                for (int i = 0; i < parts.Length; i++)
                {
                    SceneObjectPart part = parts[i];
                    if (part.IsSelected)
                        return;
                }
                IsSelected = partSelect;
                //if (!IsAttachment)
                //{
                //    ScheduleGroupForFullUpdate();
                //}
            }
        }

        private double m_lastCollisionSoundMS;
        
        /// <summary>
        /// The UUID for the region this object is in.
        /// </summary>
        public UUID RegionUUID
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_scene is not null ? m_scene.RegionInfo.RegionID : UUID.Zero;
            }
        }

        /// <summary>
        /// The item ID that this object was rezzed from, if applicable.
        /// </summary>
        /// <remarks>
        /// If not applicable will be UUID.Zero
        /// </remarks>
        public UUID FromItemID { get; set; }

        /// <summary>
        /// Refers to the SceneObjectPart.UUID property of the object that this object was rezzed from, if applicable.
        /// </summary>
        /// <remarks>
        /// If not applicable will be UUID.Zero
        /// </remarks>
        /// obsolete use RezzerID
        public UUID FromPartID
        {
            get { return RezzerID; }
            set {RezzerID = value; }
        }

        /// <summary>
        /// The folder ID that this object was rezzed from, if applicable.
        /// </summary>
        /// <remarks>
        /// If not applicable will be UUID.Zero
        /// </remarks>
        public UUID FromFolderID { get; set; }

        /// <summary>
        /// If true then grabs are blocked no matter what the individual part BlockGrab setting.
        /// </summary>
        /// <value><c>true</c> if block grab override; otherwise, <c>false</c>.</value>
        public bool BlockGrabOverride { get; set; }

        /// <summary>
        /// IDs of all avatars sat on this scene object.
        /// </summary>
        /// <remarks>
        /// We need this so that we can maintain a linkset wide ordering of avatars sat on different parts.
        /// This must be locked before it is read or written.
        /// SceneObjectPart sitting avatar add/remove code also locks on this object to avoid race conditions.
        /// No avatar should appear more than once in this list.
        /// Do not manipulate this list directly - use the Add/Remove sitting avatar methods on SceneObjectPart.
        /// </remarks>
        protected internal List<ScenePresence> m_sittingAvatars = new();

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        public SceneObjectGroup()
        {
            m_lastCollisionSoundMS = Util.GetTimeStampMS() + 1000.0;
        }

        /// <summary>
        /// This constructor creates a SceneObjectGroup using a pre-existing SceneObjectPart.
        /// The original SceneObjectPart will be used rather than a copy, preserving
        /// its existing localID and UUID.
        /// </summary>
        /// <param name='part'>Root part for this scene object.</param>
        public SceneObjectGroup(SceneObjectPart part) : this()
        {
            SetRootPart(part);
        }

        /// <summary>
        /// Constructor.  This object is added to the scene later via AttachToScene()
        /// </summary>
        public SceneObjectGroup(UUID ownerID, Vector3 pos, Quaternion rot, PrimitiveBaseShape shape)
        {
            SetRootPart(new SceneObjectPart(ownerID, shape, pos, rot, Vector3.Zero));
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public SceneObjectGroup(UUID ownerID, Vector3 pos, PrimitiveBaseShape shape)
            : this(ownerID, pos, Quaternion.Identity, shape)
        {
        }

        ~SceneObjectGroup()
        {
            Dispose(false);
        }
        private bool disposed = false;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!disposed)
            {
                IsDeleted = true;
                disposed = true;

                SceneObjectPart[] parts = m_parts.GetArray();
                for(int i= 0; i < parts.Length; ++i)
                    parts[i].Dispose();
            }
        }

        public void LoadScriptState(XmlDocument doc)
        {
            XmlNodeList nodes = doc.GetElementsByTagName("SavedScriptState");
            if (nodes.Count > 0)
            {
                m_savedScriptState ??= new Dictionary<UUID, string>();
                foreach (XmlNode node in nodes)
                {
                    if (node.Attributes["UUID"] is not null)
                    {
                        UUID itemid = new(node.Attributes["UUID"].Value);
                        if (itemid.IsNotZero())
                            m_savedScriptState[itemid] = node.InnerXml;
                    }
                }
            }
        }

        public void LoadScriptState(XmlReader reader)
        {
            //m_log.DebugFormat("[SCENE OBJECT GROUP]: Looking for script state for {0}", Name);

            while (true)
            {
                if (reader.Name.Equals("SavedScriptState") && reader.NodeType == XmlNodeType.Element)
                {
                    //m_log.DebugFormat("[SCENE OBJECT GROUP]: Loading script state for {0}", Name);
                    m_savedScriptState ??= new Dictionary<UUID, string>();

                    string uuid = reader.GetAttribute("UUID");

                    // Even if there is no UUID attribute for some strange reason, we must always read the inner XML
                    // so we don't continually keep checking the same SavedScriptedState element.
                    string innerXml = reader.ReadInnerXml();

                    if (!string.IsNullOrEmpty(uuid))
                    {
                        //m_log.DebugFormat("[SCENE OBJECT GROUP]: Found state for item ID {0} in object {1}", uuid, Name);
                        if (UUID.TryParse(uuid, out UUID itemid) && itemid.IsNotZero())
                            m_savedScriptState[itemid] = innerXml;
                    }
                    else
                    {
                        m_log.Warn($"[SCENE OBJECT GROUP]: SavedScriptState element had no UUID in object {Name} id: {UUID}");
                    }
                }
                else
                {
                    if (!reader.Read())
                        break;
                }
            }
        }

        /// <summary>
        /// Hooks this object up to the backup event so that it is persisted to the database when the update thread executes.
        /// </summary>
        public virtual void AttachToBackup()
        {
            if (IsAttachment)
                return;

            if (!Backup)
            { 
                m_scene.SceneGraph.FireAttachToBackup(this);
                m_scene.EventManager.OnBackup += ProcessBackup;
            }

            Backup = true;
        }

        /// <summary>
        /// Attach this object to a scene.  It will also now appear to agents.
        /// </summary>
        /// <param name="scene"></param>
        public void AttachToScene(Scene scene)
        {
            m_scene = scene;
            RegionHandle = m_scene.RegionInfo.RegionHandle;

            if (m_rootPart.Shape.PCode != 9 || m_rootPart.Shape.State == 0)
                m_rootPart.ParentID = 0;
            if (m_rootPart.LocalId == 0)
                m_rootPart.LocalId = m_scene.AllocateLocalId();

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                part.KeyframeMotion?.UpdateSceneObject(this);

                if (Object.ReferenceEquals(part, m_rootPart))
                    continue;

                if (part.LocalId == 0)
                    part.LocalId = m_scene.AllocateLocalId();

                part.ParentID = m_rootPart.LocalId;
                //m_log.DebugFormat("[SCENE]: Given local id {0} to part {1}, linknum {2}, parent {3} {4}", part.LocalId, part.UUID, part.LinkNum, part.ParentID, part.ParentUUID);
            }

            ApplyPhysics();

            // Don't trigger the update here - otherwise some client issues occur when multiple updates are scheduled
            // for the same object with very different properties.  The caller must schedule the update.
            //ScheduleGroupForFullUpdate();
        }

        public EntityIntersection TestIntersection(Ray hRay, bool frontFacesOnly, bool faceCenters)
        {
            // We got a request from the inner_scene to raytrace along the Ray hRay
            // We're going to check all of the prim in this group for intersection with the ray
            // If we get a result, we're going to find the closest result to the origin of the ray
            // and send back the intersection information back to the innerscene.

            EntityIntersection result = new();

            SceneObjectPart[] parts = m_parts.GetArray();

            // Find closest hit here
            float idist = float.MaxValue;

            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];

                // Temporary commented to stop compiler warning
                //Vector3 partPosition =
                //    new Vector3(part.AbsolutePosition.X, part.AbsolutePosition.Y, part.AbsolutePosition.Z);
                Quaternion parentrotation = GroupRotation;

                // Telling the prim to raytrace.
                //EntityIntersection inter = part.TestIntersection(hRay, parentrotation);

                EntityIntersection inter = part.TestIntersectionOBB(hRay, parentrotation, frontFacesOnly, faceCenters);

                if (inter.HitTF)
                {
                    // We need to find the closest prim to return to the testcaller along the ray
                    if (inter.distance < idist)
                    {
                        result.HitTF = true;
                        result.ipoint = inter.ipoint;
                        result.obj = part;
                        result.normal = inter.normal;
                        result.distance = inter.distance;

                        idist = inter.distance;
                    }
                }
            }
            return result;
        }

        public void GetBoundingBox(out float minX, out float maxX, out float minY, out float maxY, out float minZ, out float maxZ)
        {
            uint rootid = RootPart.LocalId;
            Vector3 scale = RootPart.Scale * 0.5f;

            minX = -scale.X;
            maxX = scale.X;
            minY = -scale.Y;
            maxY = scale.Y;
            minZ = -scale.Z;
            maxZ = scale.Z;

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; ++i)
            {
                SceneObjectPart part = parts[i];
                if(part.LocalId == rootid)
                    continue;

                Vector3 offset = part.OffsetPosition;
                scale = part.Scale * 0.5f;

                Matrix4 m = Matrix4.CreateFromQuaternion(part.RotationOffset);
                Vector3 a = m.AtAxis;
                a.X = Math.Abs(a.X);
                a.Y = Math.Abs(a.Y);
                a.Z = Math.Abs(a.Z);

                float tmpS = Vector3.Dot(a, scale);
                float tmp = offset.X - tmpS;
                if (tmp < minX)
                    minX = tmp;

                tmp = offset.X + tmpS;
                if (tmp > maxX)
                    maxX = tmp;

                a = m.LeftAxis;
                a.X = Math.Abs(a.X);
                a.Y = Math.Abs(a.Y);
                a.Z = Math.Abs(a.Z);
                tmpS = Vector3.Dot(a, scale);

                tmp = offset.Y - tmpS;
                if (tmp < minY)
                    minY = tmp;

                tmp = offset.Y + tmpS;
                if (tmp > maxY)
                    maxY = tmp;

                a = m.UpAxis;
                a.X = Math.Abs(a.X);
                a.Y = Math.Abs(a.Y);
                a.Z = Math.Abs(a.Z);

                tmpS = Vector3.Dot(a, scale);
                tmp = offset.Z - tmpS;
                if (tmp < minZ)
                    minZ = tmp;

                tmp = offset.Z + tmpS;
                if (tmp > maxZ)
                    maxZ = tmp;
            }
        }
    /// <summary>
    /// Gets a vector representing the size of the bounding box containing all the prims in the group
    /// Treats all prims as rectangular, so no shape (cut etc) is taken into account
    /// </summary>
    /// <returns></returns>

    public void GetAxisAlignedBoundingBoxRaw(out float minX, out float maxX, out float minY, out float maxY, out float minZ, out float maxZ)
        {
            maxX = float.MinValue;
            maxY = float.MinValue;
            maxZ = float.MinValue;
            minX = float.MaxValue;
            minY = float.MaxValue;
            minZ = float.MaxValue;

            Vector3 absPos = AbsolutePosition;
            SceneObjectPart[] parts = m_parts.GetArray();
            for(int i = 0; i< parts.Length; ++i)
            {
                SceneObjectPart part = parts[i];
                Vector3 offset = part.GetWorldPosition() - absPos;
                Vector3 scale = part.Scale * 0.5f;

                Matrix4 m = Matrix4.CreateFromQuaternion(part.GetWorldRotation());
                Vector3 a = m.AtAxis;
                a.X = MathF.Abs(a.X);
                a.Y = MathF.Abs(a.Y);
                a.Z = MathF.Abs(a.Z);

                float tmpS = Vector3.Dot(a, scale);
                float tmp = offset.X - tmpS;
                if (tmp < minX)
                    minX = tmp;

                tmp = offset.X + tmpS;
                if (tmp > maxX)
                    maxX = tmp;

                a = m.LeftAxis;
                a.X = MathF.Abs(a.X);
                a.Y = MathF.Abs(a.Y);
                a.Z = MathF.Abs(a.Z);
                tmpS = Vector3.Dot(a, scale);

                tmp = offset.Y - tmpS;
                if (tmp < minY)
                    minY = tmp;

                tmp = offset.Y + tmpS;
                if (tmp > maxY)
                    maxY = tmp;

                a = m.UpAxis;
                a.X = MathF.Abs(a.X);
                a.Y = MathF.Abs(a.Y);
                a.Z = MathF.Abs(a.Z);

                tmpS = Vector3.Dot(a, scale);
                tmp = offset.Z - tmpS;
                if (tmp < minZ)
                    minZ = tmp;

                tmp = offset.Z + tmpS;
                if (tmp > maxZ)
                    maxZ = tmp;
            }
        }

        /// <summary>
        /// Gets a vector representing the size of the bounding box containing all the prims in the group
        /// Treats all prims as rectangular, so no shape (cut etc) is taken into account
        /// offsetHeight is the offset in the Z axis from the centre of the bounding box to the centre of the root prim
        /// </summary>
        /// <returns></returns>
        public Vector3 GetAxisAlignedBoundingBox(out float offsetHeight)
        {
            GetAxisAlignedBoundingBoxRaw(out float minX, out float maxX, out float minY, out float maxY, out float minZ, out float maxZ);
            Vector3 boundingBox = new(maxX - minX, maxY - minY, maxZ - minZ);

            offsetHeight = 0;
            float lower = -minZ;
            if (lower > maxZ)
                offsetHeight = lower - 0.5f * boundingBox.Z;
            else if (maxZ > lower)
                offsetHeight = 0.5f * boundingBox.Z - maxZ;

           // m_log.InfoFormat("BoundingBox is {0} , {1} , {2} ", boundingBox.X, boundingBox.Y, boundingBox.Z);
            return boundingBox;
        }

        #endregion

        private float? m_boundsRadius = null;
        public void InvalidBoundsRadius()
        {
            m_boundsRadius = null;
        }

        private Vector3 m_boundsCenter;
        private Vector3 m_LastCenterOffset;
        private Vector3 last_boundsRot = new(-10, -10, -10);
        public Vector3 getCenterOffset()
        {
            // math is done in GetBoundsRadius();
            if(m_boundsRadius is null)
                GetBoundsRadius();

            Quaternion rot = m_rootPart.RotationOffset;
            if (last_boundsRot.X != rot.X ||
                last_boundsRot.Y != rot.Y ||
                last_boundsRot.Z != rot.Z)
            {
                m_LastCenterOffset = m_boundsCenter * rot;
                last_boundsRot.X = rot.X;
                last_boundsRot.Y = rot.Y;
                last_boundsRot.Z = rot.Z;
            }

            return m_rootPart.GroupPosition + m_LastCenterOffset;
        }

        private float m_areaFactor;
        public float getAreaFactor()
        {
            // math is done in GetBoundsRadius();
            if(m_boundsRadius is null)
                GetBoundsRadius();
            return m_areaFactor;
        }

        public float GetBoundsRadius()
        {
        // this may need more threading work
            if(m_boundsRadius is null)
            {
                float res = 0;
                float areaF = 0;
                float partR;
                Vector3 offset = Vector3.Zero;

                SceneObjectPart[] parts = m_parts.GetArray();

                for (int i = 0; i < parts.Length; i++)
                {
                    SceneObjectPart p = parts[i];
                    partR = 0.5f * p.Scale.Length();
                    if(p != RootPart)
                    {
                        partR += p.OffsetPosition.Length();
                        offset += p.OffsetPosition;
                    }
                    if(partR > res)
                        res = partR;
                    if(p.maxSimpleArea() > areaF)
                        areaF = p.maxSimpleArea();
                }
                if(parts.Length > 1)
                {
                    offset /= parts.Length; // basicly geometric center
                }

                areaF = 0.5f / areaF;  // scale it
                areaF = Utils.Clamp(areaF, 0.05f, 100f); // clamp it

                m_areaFactor = MathF.Sqrt(areaF);
                m_boundsCenter = offset;
                m_boundsRadius = res;
                return res;
            }

            return m_boundsRadius.Value;
        }

        public void GetResourcesCosts(SceneObjectPart apart,
            out float linksetResCost, out float linksetPhysCost, out float partCost, out float partPhysCost)
        {
            // this information may need to be cached

            float cost;
            float tmpcost;

            bool ComplexCost = false;

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].UsesComplexCost)
                {
                    ComplexCost = true;
                    break;
                }
            }

            if (ComplexCost)
            {
                linksetResCost = 0;
                linksetPhysCost = 0;
                partCost = 0;
                partPhysCost = 0;

                for (int i = 0; i < parts.Length; i++)
                {
                    SceneObjectPart p = parts[i];

                    cost = p.StreamingCost;
                    tmpcost = p.SimulationCost;
                    if (tmpcost > cost)
                        cost = tmpcost;
                    tmpcost = p.PhysicsCost;
                    if (tmpcost > cost)
                        cost = tmpcost;

                    linksetPhysCost += tmpcost;
                    linksetResCost += cost;

                    if (p == apart)
                    {
                        partCost = cost;
                        partPhysCost = tmpcost;
                    }
                }
            }
            else
            {
                partPhysCost = 1.0f;
                partCost = 1.0f;
                linksetResCost = parts.Length;
                linksetPhysCost = linksetResCost;
            }
        }

        public void GetSelectedCosts(out float PhysCost, out float StreamCost, out float SimulCost)
        {
            SceneObjectPart p;
            SceneObjectPart[] parts = m_parts.GetArray();

            PhysCost = 0;
            StreamCost = 0;
            SimulCost = 0;

            for (int i = 0; i < parts.Length; i++)
            {
                p = parts[i];

                StreamCost += p.StreamingCost;
                SimulCost += p.SimulationCost;
                PhysCost += p.PhysicsCost;
            }
        }

        public void SaveScriptedState(XmlTextWriter writer)
        {
            SaveScriptedState(writer, false);
        }

        public void SaveScriptedState(XmlTextWriter writer, bool oldIDs)
        {
            XmlDocument doc = new();
            Dictionary<UUID,string> states = new();

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                Dictionary<UUID, string> pstates = parts[i].Inventory.GetScriptStates(oldIDs);
                foreach (KeyValuePair<UUID, string> kvp in pstates)
                    states[kvp.Key] = kvp.Value;
            }

            if (states.Count > 0)
            {
                // Now generate the necessary XML wrappings
                writer.WriteStartElement(String.Empty, "GroupScriptStates", String.Empty);
                foreach (UUID itemid in states.Keys)
                {
                    doc.LoadXml(states[itemid]);
                    writer.WriteStartElement(String.Empty, "SavedScriptState", String.Empty);
                    writer.WriteAttributeString(String.Empty, "UUID", String.Empty, itemid.ToString());
                    writer.WriteRaw(doc.DocumentElement.OuterXml); // Writes ScriptState element
                    writer.WriteEndElement(); // End of SavedScriptState
                }
                writer.WriteEndElement(); // End of GroupScriptStates
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetAttachmentPoint()
        {
            return m_rootPart.Shape.State;
        }

        public void DetachToGround()
        {
            ScenePresence avatar = m_scene.GetScenePresence(AttachedAvatar);
            if (avatar is null)
                return;
            m_rootPart.Shape.LastAttachPoint = m_rootPart.Shape.State;
            m_rootPart.AttachedPos = m_rootPart.OffsetPosition;
            avatar.RemoveAttachment(this);

            if (avatar is null)
                return;

            Vector3 detachedpos = avatar.AbsolutePosition;
            FromItemID = UUID.Zero;

            AbsolutePosition = detachedpos;
            AttachedAvatar = UUID.Zero;

            //SceneObjectPart[] parts = m_parts.GetArray();
            //for (int i = 0; i < parts.Length; i++)
            //    parts[i].AttachedAvatar = UUID.Zero;

            m_rootPart.SetParentLocalId(0);
            AttachmentPoint = 0;
            // must check if buildind should be true or false here
            //m_rootPart.ApplyPhysics(m_rootPart.GetEffectiveObjectFlags(), m_rootPart.VolumeDetectActive,false);
            ApplyPhysics();

            HasGroupChanged = true;
            RootPart.Rezzed = DateTime.Now;
            RootPart.RemFlag(PrimFlags.TemporaryOnRez);
            AttachToBackup();
            m_scene.EventManager.TriggerParcelPrimCountTainted();
            m_rootPart.ScheduleFullUpdate();
            m_rootPart.ClearUndoState();
        }

        public void DetachToInventoryPrep()
        {
            ScenePresence avatar = m_scene.GetScenePresence(AttachedAvatar);
            //Vector3 detachedpos = new Vector3(127f, 127f, 127f);
            //detachedpos = avatar.AbsolutePosition;
            avatar?.RemoveAttachment(this);

            AttachedAvatar = UUID.Zero;

            /*SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                parts[i].AttachedAvatar = UUID.Zero;*/

            m_rootPart.SetParentLocalId(0);
            //m_rootPart.SetAttachmentPoint((byte)0);
            IsAttachment = false;
            AbsolutePosition = m_rootPart.AttachedPos;
            //m_rootPart.ApplyPhysics(m_rootPart.GetEffectiveObjectFlags(), m_scene.m_physicalPrim);
            //AttachToBackup();
            //m_rootPart.ScheduleFullUpdate();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="part"></param>
        private void SetPartAsNonRoot(SceneObjectPart part)
        {
            part.ParentID = m_rootPart.LocalId;
            part.ClearUndoState();
        }

        /// <summary>
        /// Set a part to act as the root part for this scene object
        /// </summary>
        /// <param name="part"></param>
        public void SetRootPart(SceneObjectPart part)
        {
            if (part is null)
                throw new ArgumentNullException("Cannot give SceneObjectGroup a null root SceneObjectPart");

            part.SetParent(this);
            m_rootPart = part;
            if (!IsAttachment)
                part.ParentID = 0;
            part.LinkNum = 0;

            m_parts.Add(m_rootPart.UUID, m_rootPart);
        }

        /// <summary>
        /// Add a new part to this scene object.  The part must already be correctly configured.
        /// </summary>
        /// <param name="part"></param>
        public void AddPart(SceneObjectPart part)
        {
            part.SetParent(this);
            m_parts.Add(part.UUID, part);

            part.LinkNum = m_parts.Count;

            if (part.LinkNum == 2)
                RootPart.LinkNum = 1;
            InvalidatePartsLinkMaps();
        }

        /// <summary>
        /// Make sure that every non root part has the proper parent root part local id
        /// </summary>
        private void UpdateParentIDs()
        {
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                if (part.UUID.NotEqual(m_rootPart.UUID))
                    part.ParentID = m_rootPart.LocalId;
            }
        }

        public void RegenerateFullIDs()
        {
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                parts[i].UUID = UUID.Random();
        }

        // helper provided for parts.
        public int GetSceneMaxUndo()
        {
            if (m_scene is not null)
                return m_scene.MaxUndoCount;
            return 5;
        }

         public void ResetChildPrimPhysicsPositions()
        {
            // Setting this SOG's absolute position also loops through and sets the positions
            //    of the SOP's in this SOG's linkset. This has the side affect of making sure
            //    the physics world matches the simulated world.

            Vector3 groupPosition = m_rootPart.GroupPosition;
            SceneObjectPart[] parts = m_parts.GetArray();

            foreach (SceneObjectPart part in parts)
            {
                if (part != m_rootPart)
                    part.GroupPosition = groupPosition;
            }
        }

        public UUID GetPartsFullID(uint localID)
        {
            SceneObjectPart part = GetPart(localID);
            return part is not null ? part.UUID : UUID.Zero;
        }

        public void ObjectGrabHandler(uint localId, Vector3 offsetPos, IClientAPI remoteClient)
        {

            if (m_rootPart.LocalId == localId)
            {
                if((RootPart.ScriptEvents & scriptEvents.anytouch) != 0)
                    lastTouchTime = Util.GetTimeStampMS();
                OnGrabGroup(offsetPos, remoteClient);
            }
            else
            {
                SceneObjectPart part = GetPart(localId);

                if (((part.ScriptEvents & scriptEvents.anytouch) != 0) || (RootPart.ScriptEvents & scriptEvents.anytouch) != 0)
                    lastTouchTime = Util.GetTimeStampMS();
                OnGrabPart(part, offsetPos, remoteClient);
            }
        }

        public virtual void OnGrabPart(SceneObjectPart part, Vector3 offsetPos, IClientAPI remoteClient)
        {
            //m_log.DebugFormat(
            //    "[SCENE OBJECT GROUP]: Processing OnGrabPart for {0} on {1} {2}, offsetPos {3}",
            //    remoteClient.Name, part.Name, part.LocalId, offsetPos);

            //part.StoreUndoState();
            part.OnGrab(offsetPos, remoteClient);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void OnGrabGroup(Vector3 offsetPos, IClientAPI remoteClient)
        {
            m_scene.EventManager.TriggerGroupGrab(UUID, offsetPos, remoteClient.AgentId);
        }

        /// <summary>
        /// Delete this group from its scene.
        /// </summary>
        /// <remarks>
        /// This only handles the in-world consequences of deletion (e.g. any avatars sitting on it are forcibly stood
        /// up and all avatars receive notification of its removal.  Removal of the scene object from database backup
        /// must be handled by the caller.
        /// </remarks>
        /// <param name="silent">If true then deletion is not broadcast to clients</param>
        public void DeleteGroupFromScene(bool silent)
        {
            // We need to keep track of this state in case this group is still queued for backup.
            IsDeleted = true;

            DetachFromBackup();

            if(Scene is null)  // should not happen unless restart/shutdown ?
                return;

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];

                Scene.ForEachScenePresence(delegate(ScenePresence avatar)
                {
                    if (!avatar.IsChildAgent && avatar.ParentID == part.LocalId && avatar.ParentUUID.IsZero())
                        avatar.StandUp();

                    if (!silent)
                    {
                        part.ClearUpdateSchedule();
                        if (part == m_rootPart)
                        {
                            if (!IsAttachment
                                || AttachedAvatar == avatar.ControllingClient.AgentId
                                || !HasPrivateAttachmentPoint)
                            {
                                // Send a kill object immediately
                                avatar.ControllingClient.SendKillObject(new List<uint> { part.LocalId });
                                //direct enqueue another delayed kill
                                avatar.ControllingClient.SendEntityUpdate(part,PrimUpdateFlags.Kill);
                            }
                        }
                    }
                });
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddScriptLPS(int count)
        {
            //legacy
            //m_scene.SceneGraph.AddToScriptLPS(count);
        }

       [MethodImpl(MethodImplOptions.AggressiveInlining)]
       public void AddActiveScriptCount(int count)
        {
            SceneGraph d = m_scene.SceneGraph;
            d.AddActiveScripts(count);
        }

        private const scriptEvents PhysicsNeeedSubsEvents = (
            scriptEvents.collision | scriptEvents.collision_start | scriptEvents.collision_end |
            scriptEvents.land_collision | scriptEvents.land_collision_start | scriptEvents.land_collision_end);

        private scriptEvents lastRootPartPhysEvents = 0;

        public scriptEvents ScriptEvents;

        public void aggregateScriptEvents()
        {
            PrimFlags objectflagupdate = (PrimFlags)RootPart.GetEffectiveObjectFlags();
            scriptEvents aggregatedScriptEvents = 0;

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                if (part != RootPart)
                    part.Flags = objectflagupdate;
                aggregatedScriptEvents |= part.AggregatedScriptEvents;
            }

            m_scriptListens_atTarget = ((aggregatedScriptEvents & scriptEvents.at_target) != 0);
            m_scriptListens_notAtTarget = ((aggregatedScriptEvents & scriptEvents.not_at_target) != 0);
            if (!m_scriptListens_atTarget && !m_scriptListens_notAtTarget)
            {
                lock (m_targets)
                {
                    if (m_targets.Count > 0)
                    {
                        m_targets.Clear();
                        m_scene.RemoveGroupTarget(this);
                    }
                }
            }

            m_scriptListens_atRotTarget = ((aggregatedScriptEvents & scriptEvents.at_rot_target) != 0);
            m_scriptListens_notAtRotTarget = ((aggregatedScriptEvents & scriptEvents.not_at_rot_target) != 0);
            if (!m_scriptListens_atRotTarget && !m_scriptListens_notAtRotTarget)
            {
                lock (m_rotTargets)
                {
                    if (m_rotTargets.Count > 0)
                    {
                        m_rotTargets.Clear();
                        m_scene.RemoveGroupTarget(this);
                    }
                }
            }

            scriptEvents rootPartPhysEvents = RootPart.AggregatedScriptEvents;
            rootPartPhysEvents &= PhysicsNeeedSubsEvents;
            if (rootPartPhysEvents != lastRootPartPhysEvents)
            {
                lastRootPartPhysEvents = rootPartPhysEvents;
                for (int i = 0; i < parts.Length; i++)
                    parts[i].UpdatePhysicsSubscribedEvents();
            }

            ScriptEvents = aggregatedScriptEvents;
            ScheduleGroupForFullUpdate();
        }

        public void SetText(string text, Vector3 color, double alpha)
        {
            Color = Color.FromArgb(0xff - (int) (alpha * 0xff),
                                   (int) (color.X * 0xff),
                                   (int) (color.Y * 0xff),
                                   (int) (color.Z * 0xff));
            Text = text;

            HasGroupChanged = true;
            m_rootPart.ScheduleFullUpdate();
        }

        /// <summary>
        /// Apply physics to this group
        /// </summary>
        public void ApplyPhysics()
        {
            SceneObjectPart[] parts = m_parts.GetArray();
            if (parts.Length > 1)
            {
                ResetChildPrimPhysicsPositions();

                // Apply physics to the root prim
                m_rootPart.ApplyPhysics(m_rootPart.GetEffectiveObjectFlags(), m_rootPart.VolumeDetectActive, true);

                for (int i = 0; i < parts.Length; i++)
                {
                    SceneObjectPart part = parts[i];
                    if (part.LocalId != m_rootPart.LocalId)
                        part.ApplyPhysics(m_rootPart.GetEffectiveObjectFlags(), part.VolumeDetectActive, true);
                }

                if (m_rootPart.PhysActor is not null)
                    m_rootPart.PhysActor.Building = false;
            }
            else
            {
                // Apply physics to the root prim
                m_rootPart.ApplyPhysics(m_rootPart.GetEffectiveObjectFlags(), m_rootPart.VolumeDetectActive, false);
            }
        }

        public void SetOwnerId(UUID userId)
        {
            ForEachPart(delegate(SceneObjectPart part)
            {
                if (part.OwnerID.NotEqual(userId))
                {
                    if(part.GroupID.NotEqual(part.OwnerID))
                        part.LastOwnerID = part.OwnerID;
                    part.OwnerID = userId;
                }
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ForEachPart(Action<SceneObjectPart> whatToDo)
        {
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                whatToDo(parts[i]);
        }

        #region Events

        /// <summary>
        /// Processes backup.
        /// </summary>
        /// <param name="datastore"></param>
        public virtual void ProcessBackup(ISimulationDataService datastore, bool forcedBackup)
        {
            if (!Backup)
            {
                //m_log.DebugFormat(
                //    "[WATER WARS]: Ignoring backup of {0} {1} since object is not marked to be backed up", Name, UUID);
                return;
            }

            if (IsDeleted || inTransit || UUID.IsZero())
            {
                //m_log.DebugFormat(
                //    "[WATER WARS]: Ignoring backup of {0} {1} since object is marked as already deleted", Name, UUID);
                return;
            }

            if ((RootPart.Flags & PrimFlags.TemporaryOnRez) != 0)
                return;

            // Since this is the top of the section of call stack for backing up a particular scene object, don't let
            // any exception propogate upwards.
            try
            {
                 // if shutting down then there will be nothing to handle the return so leave till next restart
                if (!m_scene.ShuttingDown &&
                        m_scene.LoginsEnabled && // We're starting up or doing maintenance, don't mess with things
                        !m_scene.LoadingPrims) // Land may not be valid yet
                {
                    ILandObject parcel = m_scene.LandChannel.GetLandObject(
                            m_rootPart.GroupPosition.X, m_rootPart.GroupPosition.Y);

                    if (parcel is not null && parcel.LandData is not null &&
                            parcel.LandData.OtherCleanTime != 0)
                    {
                        if (parcel.LandData.OwnerID.NotEqual(OwnerID) &&
                                (parcel.LandData.GroupID.NotEqual(GroupID) ||
                                parcel.LandData.GroupID.IsZero()))
                        {
                            if ((DateTime.UtcNow - RootPart.Rezzed).TotalMinutes >
                                    parcel.LandData.OtherCleanTime)
                            {
                                // don't autoreturn if we have a sitting avatar
                                // mantis 7828 (but none the provided patchs)

                                if(GetSittingAvatarsCount() > 0)
                                {
                                    // do not respect npcs
                                    List<ScenePresence> sitters = GetSittingAvatars();
                                    foreach(ScenePresence sp in sitters)
                                    {
                                        if(!sp.IsDeleted && !sp.IsNPC && sp.IsSatOnObject)
                                            return;
                                    }
                                }

                                DetachFromBackup();
                                m_log.Debug(
                                    $"[SCENE OBJECT GROUP]: Returning object {RootPart.UUID} due to parcel autoreturn");
                                m_scene.AddReturn(OwnerID.Equals(GroupID) ? LastOwnerID : OwnerID, Name, AbsolutePosition, "parcel autoreturn");
                                m_scene.DeRezObjects(null, new List<uint>() { RootPart.LocalId }, UUID.Zero,
                                        DeRezAction.Return, UUID.Zero, false);

                                return;
                            }
                        }
                    }

                }

                if (HasGroupChanged && m_scene.UseBackup)
                {
                    // don't backup while it's selected or you're asking for changes mid stream.
                    if (isTimeToPersist() || forcedBackup)
                    {
                        if (RootPart.Shape.PCode == 9 && RootPart.Shape.State != 0)
                        {
                            RootPart.Shape.LastAttachPoint = RootPart.Shape.State;
                            RootPart.Shape.State = 0;
                            ScheduleGroupForFullUpdate();
                        }

                        SceneObjectGroup backup_group = Copy(false);
                        backup_group.RootPart.Velocity = RootPart.Velocity;
                        backup_group.RootPart.Acceleration = RootPart.Acceleration;
                        backup_group.RootPart.AngularVelocity = RootPart.AngularVelocity;
                        HasGroupChanged = false;
                        GroupContainsForeignPrims = false;

                        m_scene.EventManager.TriggerOnSceneObjectPreSave(backup_group, this);

                        datastore.StoreObject(backup_group, m_scene.RegionInfo.RegionID);

                        backup_group.ForEachPart(delegate(SceneObjectPart part)
                        {
                            part.Inventory.ProcessInventoryBackup(datastore);

                            if(part.KeyframeMotion is not null)
                            {
                                part.KeyframeMotion.Delete();
                                part.KeyframeMotion = null;
                            }
                        });

                        backup_group.Dispose();
                        backup_group = null;
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error($"[SCENE]: Storing of {Name}, {UUID} in {m_scene.RegionInfo.RegionName} failed: {e.Message}");
            }
        }

        #endregion

        /// <summary>
        /// Send the parts of this SOG to a single client
        /// </summary>
        /// <remarks>
        /// Used when the client initially connects and when client sends RequestPrim packet
        /// </remarks>
        /// <param name="remoteClient"></param>
        public void SendFullAnimUpdateToClient(IClientAPI remoteClient)
        {
            PrimUpdateFlags update = RootPart.Shape.MeshFlagEntry ?
                        PrimUpdateFlags.FullUpdatewithAnim :
                        PrimUpdateFlags.FullUpdate;

            if (RootPart.Shape.RenderMaterials is not null &&
                        RootPart.Shape.ReflectionProbe is null &&
                        RootPart.Shape.RenderMaterials.overrides is not null &&
                        RootPart.Shape.RenderMaterials.overrides.Length > 0)
                RootPart.SendUpdate(remoteClient, update | PrimUpdateFlags.MaterialOvr);
            else
                RootPart.SendUpdate(remoteClient, update);

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                if (part.LocalId != RootPart.LocalId)
                {
                    if (part.Shape.RenderMaterials is not null &&
                                part.Shape.ReflectionProbe is null &&
                                part.Shape.RenderMaterials.overrides is not null &&
                                part.Shape.RenderMaterials.overrides.Length > 0)
                        part.SendUpdate(remoteClient, update | PrimUpdateFlags.MaterialOvr);
                    else
                        part.SendUpdate(remoteClient, update);
                }
            }
        }

        public void SendUpdateProbes(IClientAPI remoteClient)
        {
            PrimUpdateFlags update = PrimUpdateFlags.UpdateProbe;

            RootPart.SendUpdate(remoteClient, update);

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                if (part.LocalId != RootPart.LocalId)
                    part.SendUpdate(remoteClient, update);
            }
        }

        #region Copying

        /// <summary>
        /// Duplicates this object, including operations such as physics set up and attaching to the backup event.
        /// </summary>
        /// <param name="userExposed">True if the duplicate will immediately be in the scene, false otherwise</param>
        /// <returns></returns>
        public SceneObjectGroup Copy(bool userExposed)
        {
            m_dupeInProgress = true;
            SceneObjectGroup dupe = (SceneObjectGroup)MemberwiseClone();

            dupe.m_parts = new MapAndArray<UUID, SceneObjectPart>();

            dupe.m_targets = new Dictionary<int, scriptPosTarget>();
            dupe.m_rotTargets = new Dictionary<int, scriptRotTarget>();
            dupe.m_targetsByScript = new Dictionary<UUID, List<int>>();

            // a copy isnt backedup
            dupe.Backup = false;
            dupe.InvalidBoundsRadius();

            // a copy is not in transit hopefully
            dupe.inTransit = false;

            // new group as no sitting avatars
            dupe.m_sittingAvatars = new List<ScenePresence>();

            if(LinksetData is not null)
                dupe.LinksetData = LinksetData.Copy();

            dupe.CopyRootPart(m_rootPart, OwnerID, GroupID, userExposed);
            dupe.m_rootPart.LinkNum = m_rootPart.LinkNum;

            if (userExposed)
                dupe.m_rootPart.TrimPermissions();

            List<SceneObjectPart> partList = new(m_parts.GetArray());

            partList.Sort(delegate(SceneObjectPart p1, SceneObjectPart p2)
                    {
                        return p1.LinkNum.CompareTo(p2.LinkNum);
                    }
                );

            foreach (SceneObjectPart part in partList)
            {
                SceneObjectPart newPart;
                if (part.UUID.NotEqual(m_rootPart.UUID))
                {
                    newPart = dupe.CopyPart(part, OwnerID, GroupID, userExposed);
                    newPart.LinkNum = part.LinkNum;
                    //if (userExposed)
                        newPart.ParentID = dupe.m_rootPart.LocalId;
                 }
                else
                {
                    newPart = dupe.m_rootPart;
                }

                if (userExposed)
                    newPart.ApplyPhysics((uint)newPart.Flags,newPart.VolumeDetectActive,true);

                // copy keyframemotion
                if (part.KeyframeMotion is not null)
                    newPart.KeyframeMotion = part.KeyframeMotion.Copy(dupe);
            }

            if (userExposed)
            {
                if (dupe.m_rootPart.PhysActor is not null)
                    dupe.m_rootPart.PhysActor.Building = false; // tell physics to finish building

                dupe.InvalidateDeepEffectivePerms();

                dupe.HasGroupChanged = true;
                dupe.AttachToBackup();

                dupe.ScheduleGroupForUpdate(PrimUpdateFlags.FullUpdatewithAnimMatOvr);
            }

            dupe.InvalidatePartsLinkMaps();
            
            m_dupeInProgress = false;
            return dupe;
        }

        /// <summary>
        /// Copy the given part as the root part of this scene object.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="cAgentID"></param>
        /// <param name="cGroupID"></param>
        public void CopyRootPart(SceneObjectPart part, UUID cAgentID, UUID cGroupID, bool userExposed)
        {
            SceneObjectPart newpart = part.Copy(m_scene.AllocateLocalId(), OwnerID, GroupID, 0, userExposed);
            //SceneObjectPart newpart = part.Copy(part.LocalId, OwnerID, GroupID, 0, userExposed);
            //newpart.LocalId = m_scene.AllocateLocalId();

            SetRootPart(newpart);
            if (userExposed)
                RootPart.Velocity = Vector3.Zero; // In case source is moving
        }

        public void ScriptSetPhysicsStatus(bool usePhysics)
        {
            if (usePhysics)
            {
                RootPart.KeyframeMotion?.Stop();
                RootPart.KeyframeMotion = null;
            }
            UpdateFlags(usePhysics, IsTemporary, IsPhantom, IsVolumeDetect);
        }

        public void ScriptSetTemporaryStatus(bool makeTemporary)
        {
            UpdateFlags(UsesPhysics, makeTemporary, IsPhantom, IsVolumeDetect);
        }

        public void ScriptSetPhantomStatus(bool makePhantom)
        {
            UpdateFlags(UsesPhysics, IsTemporary, makePhantom, IsVolumeDetect);
        }

        public void ScriptSetVolumeDetect(bool makeVolumeDetect)
        {
            UpdateFlags(UsesPhysics, IsTemporary, IsPhantom, makeVolumeDetect);
        }

        public void applyImpulse(Vector3 impulse)
        {
            if (IsAttachment)
            {
                ScenePresence avatar = m_scene.GetScenePresence(AttachedAvatar);
                avatar?.PushForce(impulse);
            }
            else
            {
                PhysicsActor pa = RootPart.PhysActor;
                if (pa is not null)
                {
                    // false to be applied as a impulse
                    pa.AddForce(impulse, false);
                }
            }
        }

        public void ApplyAngularImpulse(Vector3 impulse)
        {
            PhysicsActor pa = RootPart.PhysActor;
            if (pa is not null)
            {
                if (!IsAttachment)
                {
                    // false to be applied as a impulse
                    pa.AddAngularForce(impulse, false);
                }
            }
        }

        public Vector3 GetTorque()
        {
            return RootPart.Torque;
        }

         // This is used by both Double-Click Auto-Pilot and llMoveToTarget() in an attached object
        public void MoveToTarget(Vector3 target, float tau)
        {
            if(tau > 0)
            {
                if (IsAttachment)
                {
                    ScenePresence avatar = m_scene.GetScenePresence(AttachedAvatar);
                    if (avatar is not null && !avatar.IsSatOnObject)
                        avatar.MoveToTarget(target, false, true, false, tau);
                }
                else
                {
                    PhysicsActor pa = RootPart.PhysActor;
                    if (pa is not null)
                    {
                        pa.PIDTarget = target;
                        pa.PIDTau = tau;
                        pa.PIDActive = true;
                    }
                }
            }
            else
                StopMoveToTarget();
        }

        public void StopMoveToTarget()
        {
            if (IsAttachment)
            {
                ScenePresence avatar = m_scene.GetScenePresence(AttachedAvatar);
                avatar?.ResetMoveToTarget();
            }
            else
            {
                PhysicsActor pa = RootPart.PhysActor;
                if (pa is not null)
                    pa.PIDActive = false;

                RootPart.ScheduleTerseUpdate(); // send a stop information
            }
        }

        public void RotLookAt(Quaternion target, float strength, float damping)
        {
            if(IsDeleted)
                return;

            // non physical is handle in LSL api
            if(!UsesPhysics || IsAttachment)
                return;

            SceneObjectPart rootpart = m_rootPart;
            if (rootpart is not null)
            {
                /* physics still doesnt suport this
                if (rootpart.PhysActor is not null)
                {
                    rootpart.PhysActor.APIDTarget = new Quaternion(target.X, target.Y, target.Z, target.W);
                    rootpart.PhysActor.APIDStrength = strength;
                    rootpart.PhysActor.APIDDamping = damping;
                    rootpart.PhysActor.APIDActive = true;
                }
                */
                // so do it in rootpart
                rootpart.RotLookAt(target, strength, damping);
            }
        }

       public void StartLookAt(Quaternion target, float strength, float damping)
        {
            if(IsDeleted)
                return;

            // non physical is done by LSL APi
            if(!UsesPhysics || IsAttachment)
                return;

            m_rootPart?.RotLookAt(target, strength, damping);
        }

        public void StopLookAt()
        {
            SceneObjectPart rootpart = m_rootPart;
            if (rootpart is not null)
            {
                if (rootpart.PhysActor is not null)
                {
                    rootpart.PhysActor.APIDActive = false;
                }

                rootpart.StopLookAt();
            }
        }
        /// <summary>
        /// Uses a PID to attempt to clamp the object on the Z axis at the given height over tau seconds.
        /// </summary>
        /// <param name="height">Height to hover.  Height of zero disables hover.</param>
        /// <param name="hoverType">Determines what the height is relative to </param>
        /// <param name="tau">Number of seconds over which to reach target</param>
        public void SetHoverHeight(float height, PIDHoverType hoverType, float tau)
        {
            PhysicsActor pa = null;
            if(IsAttachment)
                {
                    ScenePresence avatar = m_scene.GetScenePresence(AttachedAvatar);
                    if (avatar is not null)
                        pa = avatar.PhysicsActor;
                }
            else
                pa = RootPart.PhysActor;

            if (pa is not null)
            {
                if (height != 0f)
                {
                    pa.PIDHoverHeight = height;
                    pa.PIDHoverType = hoverType;
                    pa.PIDHoverTau = tau;
                    pa.PIDHoverActive = true;
                }
                else
                {
                    pa.PIDHoverActive = false;
                }
            }
        }

        /// <summary>
        /// Set the owner of all linkset.
        /// </summary>
        /// <param name="cAgentID"></param>
        /// <param name="cGroupID"></param>
        public void SetOwner(UUID cAgentID, UUID cGroupID)
        {
            SceneObjectPart rpart = RootPart;
            UUID oldowner = rpart.OwnerID;
            ForEachPart(delegate(SceneObjectPart part)
            {
                if(part.GroupID.NotEqual(part.OwnerID))
                    part.LastOwnerID = part.OwnerID;
                part.OwnerID = cAgentID;
                part.GroupID = cGroupID;
                });

            if (oldowner.NotEqual(cAgentID))
            {
                // Apply Next Owner Permissions if we're not bypassing permissions
                if (!m_scene.Permissions.BypassPermissions())
                {
                    ApplyNextOwnerPermissions();
                    InvalidateEffectivePerms();
                }
            }

            rpart.ScheduleFullUpdate();
        }

        /// <summary>
        /// Make a copy of the given part.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="cAgentID"></param>
        /// <param name="cGroupID"></param>
        public SceneObjectPart CopyPart(SceneObjectPart part, UUID cAgentID, UUID cGroupID, bool userExposed)
        {
            SceneObjectPart newPart = part.Copy(m_scene.AllocateLocalId(), OwnerID, GroupID, m_parts.Count, userExposed);
            //SceneObjectPart newPart = part.Copy(part.LocalId, OwnerID, GroupID, m_parts.Count, userExposed);
            //newPart.LocalId = m_scene.AllocateLocalId();

            AddPart(newPart);

            SetPartAsNonRoot(newPart);
            return newPart;
        }

        /// <summary>
        /// Reset the UUIDs for all the prims that make up this group.
        /// </summary>
        /// <remarks>
        /// This is called by methods which want to add a new group to an existing scene, in order
        /// to ensure that there are no clashes with groups already present.
        /// </remarks>
        public void ResetIDs()
        {
            lock (m_parts.SyncRoot)
            {
                SceneObjectPart[] partsList = m_parts.GetArray();
                m_parts.Clear();
                foreach (SceneObjectPart part in partsList)
                {
                    part.ResetIDs(part.LinkNum); // Don't change link nums
                    m_parts.Add(part.UUID, part);
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="part"></param>
        public void ServiceObjectPropertiesFamilyRequest(IClientAPI remoteClient, UUID AgentID, uint RequestFlags)
        {
            remoteClient.SendObjectPropertiesFamilyData(RootPart, RequestFlags);
            //remoteClient.SendObjectPropertiesFamilyData(RequestFlags, RootPart.UUID, RootPart.OwnerID, RootPart.GroupID, RootPart.BaseMask,
            //                                             RootPart.OwnerMask, RootPart.GroupMask, RootPart.EveryoneMask, RootPart.NextOwnerMask,
            //                                             RootPart.OwnershipCost, RootPart.ObjectSaleType, RootPart.SalePrice, RootPart.Category,
            //                                             RootPart.CreatorID, RootPart.Name, RootPart.Description);
        }

        public void SetPartOwner(SceneObjectPart part, UUID cAgentID, UUID cGroupID)
        {
            part.OwnerID = cAgentID;
            part.GroupID = cGroupID;
        }

        #endregion


        public override void Update()
        {
            // Check that the group was not deleted before the scheduled update
            // FIXME: This is merely a temporary measure to reduce the incidence of failure when
            // an object has been deleted from a scene before update was processed.
            // A more fundamental overhaul of the update mechanism is required to eliminate all
            // the race conditions.
            if (IsDeleted || inTransit)
                return;
 
            if (IsAttachment)
            {
                ScenePresence sp = m_scene.GetScenePresence(AttachedAvatar);
                sp?.SendAttachmentScheduleUpdate(this);
                return;
            }

            // while physics doesn't suports LookAt, we do it in RootPart
            if (!IsSelected)
                RootPart.UpdateLookAt();

            double now = Util.GetTimeStampMS();
            RootPart.SendScheduledUpdates(now);
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                if(part != RootPart)
                    part.SendScheduledUpdates(now);
            }
        }

        /// <summary>
        /// Schedule a full update for this scene object to all interested viewers.
        /// </summary>
        /// <remarks>
        /// Ultimately, this should be managed such that region modules can invoke it at the end of a set of operations
        /// so that either all changes are sent at once.  However, currently, a large amount of internal
        /// code will set this anyway when some object properties are changed.
        /// </remarks>
        public void ScheduleGroupForFullUpdate()
        {
            //if (IsAttachment)
            //    m_log.DebugFormat("[SOG]: Scheduling full update for {0} {1}", Name, LocalId);
            if (Scene.GetNumberOfClients() == 0)
                return;

            RootPart.ScheduleFullUpdate();

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                if (part != RootPart)
                    part.ScheduleFullUpdate();
            }
        }

        public void ScheduleGroupForFullAnimUpdate()
        {
            //if (IsAttachment)
            //    m_log.DebugFormat("[SOG]: Scheduling full update for {0} {1}", Name, LocalId);
            if (Scene.GetNumberOfClients() == 0)
                return;

            SceneObjectPart[] parts = m_parts.GetArray();
            if (!RootPart.Shape.MeshFlagEntry)
            {
                RootPart.ScheduleFullUpdate();

                for (int i = 0; i < parts.Length; i++)
                {
                    SceneObjectPart part = parts[i];
                    if (part != RootPart)
                        part.ScheduleFullUpdate();
                }
                return;
            }

            RootPart.ScheduleFullAnimUpdate();

            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                if (part != RootPart)
                    part.ScheduleFullAnimUpdate();
            }
        }

        public void ScheduleGroupForUpdate(PrimUpdateFlags update)
        {
            //if (IsAttachment)
            //    m_log.DebugFormat("[SOG]: Scheduling full update for {0} {1}", Name, LocalId);
            if (Scene.GetNumberOfClients() == 0)
                return;

            SceneObjectPart[] parts = m_parts.GetArray();
            if (!RootPart.Shape.MeshFlagEntry)
                update &= ~PrimUpdateFlags.Animations;

            RootPart.ScheduleUpdate(update);

            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                if (part != RootPart)
                    part.ScheduleUpdate(update);
            }
        }
        /// <summary>
        /// Schedule a terse update for this scene object to all interested viewers.
        /// </summary>
        /// <remarks>
        /// Ultimately, this should be managed such that region modules can invoke it at the end of a set of operations
        /// so that either all changes are sent at once.  However, currently, a large amount of internal
        /// code will set this anyway when some object properties are changed.
        /// </remarks>
        public void ScheduleGroupForTerseUpdate()
        {
            //m_log.DebugFormat("[SOG]: Scheduling terse update for {0} {1}", Name, UUID);

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                parts[i].ScheduleTerseUpdate();
        }

        /// <summary>
        /// Immediately send an update for this scene object's root prim only.
        /// This is for updates regarding the object as a whole, and none of its parts in particular.
        /// Note: this may not be used by opensim (it probably should) but it's used by
        /// external modules.
        /// </summary>
        public void SendGroupRootTerseUpdate()
        {
            if (IsDeleted || inTransit)
                return;

            RootPart.SendTerseUpdateToAllClients();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void QueueForUpdateCheck()
        {
            m_scene?.SceneGraph.AddToUpdateList(this);
        }

        /// <summary>
        /// Immediately send a terse update for this scene object.
        /// </summary>
        public void SendGroupTerseUpdate()
        {
            if (IsDeleted || inTransit)
                return;

            if (IsAttachment)
            {
                ScenePresence sp = m_scene.GetScenePresence(AttachedAvatar);
                if (sp is not null)
                {
                    sp.SendAttachmentUpdate(this, PrimUpdateFlags.TerseUpdate);
                    return;
                }
            }

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                parts[i].SendTerseUpdateToAllClientsInternal();
        }

        /// <summary>
        /// Send metadata about the root prim (name, description, sale price, permissions, etc.) to a client.
        /// </summary>
        /// <param name="client"></param>
        public void SendPropertiesToClient(IClientAPI client)
        {
            m_rootPart.SendPropertiesToClient(client);
        }

        #region SceneGroupPart Methods

        /// <summary>
        /// Get the child part by LinkNum
        /// </summary>
        /// <param name="linknum"></param>
        /// <returns>null if no child part with that linknum or child part</returns>
        public SceneObjectPart GetLinkNumPart(int linknum)
        {
            if (linknum < 2)
            {
                // unlike SL 0 or 1 will mean root
                // one reason is that we do not consider siting avatars on root linknumber
                return linknum < 0 ? null : RootPart;
            }

            Span<SceneObjectPart> parts = m_parts.GetArray().AsSpan();
            if (linknum <= parts.Length)
            {
                SceneObjectPart sop = parts[linknum - 1];
                if (sop.LinkNum == linknum)
                    return sop;
            }

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].LinkNum == linknum)
                    return parts[i];
            }

            return null;
        }

        /// <summary>
        /// Get a part with a given UUID
        /// </summary>
        /// <param name="primID"></param>
        /// <returns>null if a part with the primID was not found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SceneObjectPart GetPart(UUID primID)
        {
            if(m_parts.TryGetValue(primID, out SceneObjectPart childPart))
                return childPart;
            return null;
        }

        /// <summary>
        /// Get a part with a given local ID
        /// </summary>
        /// <param name="localID"></param>
        /// <returns>null if a part with the local ID was not found</returns>
        public SceneObjectPart GetPart(uint localID)
        {
            SceneObjectPart sop = m_scene.GetSceneObjectPart(localID);
            if(sop.ParentGroup.LocalId == LocalId)
                return sop;
            return null;
        }

        #endregion

        #region Packet Handlers

        /// <summary>
        /// Link the prims in a given group to this group
        /// </summary>
        /// <remarks>
        /// Do not call this method directly - use Scene.LinkObjects() instead to avoid races between threads.
        /// FIXME: There are places where scripts call these methods directly without locking.  This is a potential race condition.
        /// </remarks>
        /// <param name="objectGroup">The group of prims which should be linked to this group</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LinkToGroup(SceneObjectGroup objectGroup)
        {
            LinkToGroup(objectGroup, false);
        }

        // Link an existing group to this group.
        // The group being linked need not be a linkset -- it can have just one prim.
        public void LinkToGroup(SceneObjectGroup objectGroup, bool insert)
        {
            //m_log.DebugFormat(
            //    "[SCENE OBJECT GROUP]: Linking group with root part {0}, {1} to group with root part {2}, {3}",
            //    objectGroup.RootPart.Name, objectGroup.RootPart.UUID, RootPart.Name, RootPart.UUID);

            // Linking to ourselves is not a valid operation.
            if (objectGroup == this)
                return;

            // If the configured linkset capacity is greater than zero,
            // and the new linkset would have a prim count higher than this
            // value, do not link it.
            if (m_scene.m_linksetCapacity > 0 &&
                    (PrimCount + objectGroup.PrimCount) >
                    m_scene.m_linksetCapacity)
            {
                m_log.DebugFormat(
                    "[SCENE OBJECT GROUP]: Cannot link group with root" +
                    " part {0}, {1} ({2} prims) to group with root part" +
                    " {3}, {4} ({5} prims) because the new linkset" +
                    " would exceed the configured maximum of {6}",
                    objectGroup.RootPart.Name, objectGroup.RootPart.UUID,
                    objectGroup.PrimCount, RootPart.Name, RootPart.UUID,
                    PrimCount, m_scene.m_linksetCapacity);

                return;
            }

            // physical prims count limit
            // not very eficient :(

            if (UsesPhysics && m_scene.m_linksetPhysCapacity > 0 && (PrimCount + objectGroup.PrimCount) >
                    m_scene.m_linksetPhysCapacity)
            {
                int cntr = 0;
                foreach (SceneObjectPart part in Parts)
                {
                    if (part.PhysicsShapeType != (byte)PhysicsShapeType.None)
                        cntr++;
                }
                foreach (SceneObjectPart part in objectGroup.Parts)
                {
                    if (part.PhysicsShapeType != (byte)PhysicsShapeType.None)
                        cntr++;
                }

                if (cntr > m_scene.m_linksetPhysCapacity)
                {
                    // cancel physics
                    RootPart.Flags &= ~PrimFlags.Physics;
                    ApplyPhysics();
                }
            }

            if(objectGroup.LinksetData is not null)
            {
                LinksetData ??= new LinksetData(m_scene.m_LinkSetDataLimit);
                LinksetData.MergeOther(objectGroup.LinksetData);
                objectGroup.LinksetData = null;
            }

            // 'linkPart' == the root of the group being linked into this group
            SceneObjectPart linkPart = objectGroup.m_rootPart;

            if (m_rootPart.PhysActor is not null)
                m_rootPart.PhysActor.Building = true;
            if (linkPart.PhysActor is not null)
                linkPart.PhysActor.Building = true;

            // physics flags from group to be applied to linked parts
            bool grpusephys = UsesPhysics;
            bool grptemporary = IsTemporary;

            // Remember where the group being linked thought it was
            Vector3 oldGroupPosition = linkPart.GroupPosition;
            Quaternion oldRootRotation = linkPart.RotationOffset;

            // A linked SOP remembers its location and rotation relative to the root of a group.
            // Convert the root of the group being linked to be relative to the
            //   root of the group being linked to.
            // Note: Some of the assignments have complex side effects.

            // First move the new group's root SOP's position to be relative to ours
            // (radams1: Not sure if the multiple setting of OffsetPosition is required. If not,
            //   this code can be reordered to have a more logical flow.)
            linkPart.setOffsetPosition(linkPart.GroupPosition - AbsolutePosition);
            // Assign the new parent to the root of the old group
            linkPart.ParentID = m_rootPart.LocalId;
            // Now that it's a child, it's group position is our root position
            linkPart.setGroupPosition(AbsolutePosition);

            // Rotate the linking root SOP's position to be relative to the new root prim
            Quaternion parentRot = m_rootPart.RotationOffset;

            // Make the linking root SOP's rotation relative to the new root prim
            Quaternion oldRot = linkPart.RotationOffset;
            Quaternion newRot = Quaternion.Conjugate(parentRot) * oldRot;
            linkPart.setRotationOffset(newRot);

            Vector3 axPos = linkPart.OffsetPosition;
            axPos *= Quaternion.Conjugate(parentRot);
            linkPart.OffsetPosition = axPos;

            // If there is only one SOP in a SOG, the LinkNum is zero. I.e., not a linkset.
            // Now that we know this SOG has at least two SOPs in it, the new root
            //    SOP becomes the first in the linkset.
            if (m_rootPart.LinkNum == 0)
                m_rootPart.LinkNum = 1;

            lock (m_parts.SyncRoot)
            {
                // Calculate the new link number for the old root SOP
                int linkNum;
                if (insert)
                {
                    linkNum = 2;
                    int insertSize = objectGroup.PrimCount;
                    foreach (SceneObjectPart part in Parts)
                    {
                        if (part.LinkNum > 1)
                            part.LinkNum += insertSize;
                    }
                }
                else
                {
                    linkNum = PrimCount + 1;
                }

                // Add the old root SOP as a part in our group's list
                m_parts.Add(linkPart.UUID, linkPart);

                linkPart.SetParent(this);

                //linkPart.CreateSelected = true;

                linkPart.UpdatePrimFlags(grpusephys, grptemporary, (IsPhantom || (linkPart.Flags & PrimFlags.Phantom) != 0), linkPart.VolumeDetectActive || RootPart.VolumeDetectActive, true);

                // If the added SOP is physical, also tell the physics engine about the link relationship.
                if (linkPart.PhysActor is not null && m_rootPart.PhysActor is not null && m_rootPart.PhysActor.IsPhysical)
                {
                    linkPart.PhysActor.link(m_rootPart.PhysActor);
                }

                linkPart.LinkNum = linkNum++;
                linkPart.UpdatePrimFlags(UsesPhysics, IsTemporary, IsPhantom, IsVolumeDetect, false);

                // Get a list of the SOP's in the source group in order of their linknum's.
                SceneObjectPart[] ogParts = objectGroup.Parts;
                Array.Sort(ogParts, delegate(SceneObjectPart a, SceneObjectPart b)
                        {
                            return a.LinkNum - b.LinkNum;
                        });

                // Add each of the SOP's from the source linkset to our linkset
                for (int i = 0; i < ogParts.Length; i++)
                {
                    SceneObjectPart part = ogParts[i];
                    if (part.UUID != objectGroup.m_rootPart.UUID)
                    {
                        LinkNonRootPart(part, oldGroupPosition, oldRootRotation, linkNum++);

                        // Update the physics flags for the newly added SOP
                        // (Is this necessary? LinkNonRootPart() has already called UpdatePrimFlags but with different flags!??)
                        part.UpdatePrimFlags(grpusephys, grptemporary, (IsPhantom || (part.Flags & PrimFlags.Phantom) != 0), part.VolumeDetectActive, true);

                        // If the added SOP is physical, also tell the physics engine about the link relationship.
                        if (part.PhysActor is not null && m_rootPart.PhysActor is not null && m_rootPart.PhysActor.IsPhysical)
                        {
                            part.PhysActor.link(m_rootPart.PhysActor);
                        }
                    }
                    part.ClearUndoState();
                }
            }

            // Now that we've aquired all of the old SOG's parts, remove the old SOG from the scene.
            m_scene.UnlinkSceneObject(objectGroup, true);
            objectGroup.m_parts.Clear(); // do not dispose the parts moved to new group
            objectGroup.Dispose();

            // Can't do this yet since backup still makes use of the root part without any synchronization
//            objectGroup.m_rootPart = null;

            // If linking prims with different permissions, fix them
            AdjustChildPrimPermissions(false);

            GroupContainsForeignPrims = true;

            AttachToBackup();

            // Here's the deal, this is ABSOLUTELY CRITICAL so the physics scene gets the update about the
            // position of linkset prims.  IF YOU CHANGE THIS, YOU MUST TEST colliding with just linked and
            // unmoved prims!
            ResetChildPrimPhysicsPositions();

            InvalidBoundsRadius();
            InvalidatePartsLinkMaps();

            if (m_rootPart.PhysActor is not null)
                m_rootPart.PhysActor.Building = false;

            //HasGroupChanged = true;
            //ScheduleGroupForFullUpdate();
        }

        /// <summary>
        /// Delink the given prim from this group.  The delinked prim is established as
        /// an independent SceneObjectGroup.
        /// </summary>
        /// <remarks>
        /// FIXME: This method should not be called directly since it bypasses update locking, allowing a potential race
        /// condition.  But currently there is no
        /// alternative method that does take a lonk to delink a single prim.
        /// </remarks>
        /// <param name="partID"></param>
        /// <returns>The object group of the newly delinked prim.  Null if part could not be found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SceneObjectGroup DelinkFromGroup(uint partID)
        {
            return DelinkFromGroup(partID, true);
        }

        /// <summary>
        /// Delink the given prim from this group.  The delinked prim is established as
        /// an independent SceneObjectGroup.
        /// </summary>
        /// <remarks>
        /// FIXME: This method should not be called directly since it bypasses update locking, allowing a potential race
        /// condition.  But currently there is no
        /// alternative method that does take a lonk to delink a single prim.
        /// </remarks>
        /// <param name="partID"></param>
        /// <param name="sendEvents"></param>
        /// <returns>The object group of the newly delinked prim.  Null if part could not be found</returns>
        public SceneObjectGroup DelinkFromGroup(uint partID, bool sendEvents)
        {
            SceneObjectPart linkPart = GetPart(partID);

            if (linkPart is not null)
            {
                return DelinkFromGroup(linkPart, sendEvents);
            }
            else
            {
                m_log.Warn($"[SCENE OBJECT GROUP]: DelinkFromGroup(): prim {partID} not found in object {UUID}");
                return null;
            }
        }

        /// <summary>
        /// Delink the given prim from this group.  The delinked prim is established as
        /// an independent SceneObjectGroup.
        /// </summary>
        /// <remarks>
        /// FIXME: This method should not be called directly since it bypasses update locking, allowing a potential race
        /// condition.  But currently there is no
        /// alternative method that does take a lock to delink a single prim.
        /// </remarks>
        /// <param name="partID"></param>
        /// <param name="sendEvents"></param>
        /// <returns>The object group of the newly delinked prim.</returns>
        public SceneObjectGroup DelinkFromGroup(SceneObjectPart linkPart, bool sendEvents)
        {
            //m_log.DebugFormat(
            //        "[SCENE OBJECT GROUP]: Delinking part {0}, {1} from group with root part {2}, {3}",
            //        linkPart.Name, linkPart.UUID, RootPart.Name, RootPart.UUID);

            if (m_rootPart.PhysActor is not null)
                m_rootPart.PhysActor.Building = true;

            linkPart.ClearUndoState();

            Vector3 worldPos = linkPart.GetWorldPosition();
            Quaternion worldRot = linkPart.GetWorldRotation();

            // Remove the part from this object
            lock (m_parts.SyncRoot)
            {
                m_parts.Remove(linkPart.UUID);

                SceneObjectPart[] parts = m_parts.GetArray();

                // Rejigger the linknum's of the remaining SOP's to fill any gap
                if (parts.Length == 1 && RootPart is not null)
                {
                    // Single prim left
                    RootPart.LinkNum = 0;
                }
                else
                {
                    for (int i = 0; i < parts.Length; i++)
                    {
                        SceneObjectPart part = parts[i];
                        if (part.LinkNum > linkPart.LinkNum)
                            part.LinkNum--;
                    }
                }
            }

            linkPart.ParentID = 0;
            linkPart.LinkNum = 0;

            PhysicsActor linkPartPa = linkPart.PhysActor;

            // Remove the SOP from the physical scene.
            // If the new SOG is physical, it is re-created later.
            // (There is a problem here in that we have not yet told the physics
            //    engine about the delink. Someday, linksets should be made first
            //    class objects in the physics engine interface).
            if (linkPartPa is not null)
            {
                m_scene.PhysicsScene.RemovePrim(linkPartPa);
                m_scene.RemovePhysicalPrim(1);
                linkPart.PhysActor = null;
            }

            // We need to reset the child part's position
            // ready for life as a separate object after being a part of another object

            /* This commented out code seems to recompute what GetWorldPosition already does.
             * Replace with a call to GetWorldPosition (before unlinking)
            Quaternion parentRot = m_rootPart.RotationOffset;
            Vector3 axPos = linkPart.OffsetPosition;
            axPos *= parentRot;
            linkPart.OffsetPosition = new Vector3(axPos.X, axPos.Y, axPos.Z);
            linkPart.GroupPosition = AbsolutePosition + linkPart.OffsetPosition;
            linkPart.OffsetPosition = new Vector3(0, 0, 0);
             */
            linkPart.setGroupPosition(worldPos);
            linkPart.setOffsetPosition(Vector3.Zero);
            linkPart.setRotationOffset(worldRot);

            // Create a new SOG to go around this unlinked and unattached SOP
            SceneObjectGroup objectGroup = new(linkPart);
            m_scene.AddNewSceneObject(objectGroup, true);
            linkPart.Rezzed = RootPart.Rezzed;

            InvalidBoundsRadius();
            InvalidatePartsLinkMaps();
            InvalidateEffectivePerms();

            if (m_rootPart.PhysActor is not null)
                m_rootPart.PhysActor.Building = false;

            objectGroup.HasGroupChangedDueToDelink = true;
            
            if (sendEvents)
                linkPart.TriggerScriptChangedEvent(Changed.LINK);

            return objectGroup;
        }

/* working on it
        public void DelinkFromGroup(List<SceneObjectPart> linkParts, bool sendEvents)
        {
            //m_log.DebugFormat(
            //  "[SCENE OBJECT GROUP]: Delinking part {0}, {1} from group with root part {2}, {3}",
            //      linkPart.Name, linkPart.UUID, RootPart.Name, RootPart.UUID);

            if(PrimCount == 1)
                return;

            if (m_rootPart.PhysActor is not null)
                m_rootPart.PhysActor.Building = true;

            bool unlinkroot = false;
            foreach(SceneObjectPart linkPart in linkParts)
            {
                // first we only remove child parts
                if(linkPart.LocalId == m_rootPart.LocalId)
                {
                    unlinkroot = true;
                    continue;
                }

                lock (m_parts.SyncRoot)
                    if(!m_parts.Remove(linkPart.UUID))
                        continue;

                linkPart.ClearUndoState();

                Vector3 worldPos = linkPart.GetWorldPosition();
                Quaternion worldRot = linkPart.GetWorldRotation();

                linkPart.ParentID = 0;
                linkPart.LinkNum = 0;

                PhysicsActor linkPartPa = linkPart.PhysActor;

                // Remove the SOP from the physical scene.
                // If the new SOG is physical, it is re-created later.
                // (There is a problem here in that we have not yet told the physics
                //    engine about the delink. Someday, linksets should be made first
                //    class objects in the physics engine interface).
                if (linkPartPa is not null)
                {
                    m_scene.PhysicsScene.RemovePrim(linkPartPa);
                    linkPart.PhysActor = null;
                }

                linkPart.setGroupPosition(worldPos);
                linkPart.setOffsetPosition(Vector3.Zero);
                linkPart.setRotationOffset(worldRot);

                // Create a new SOG to go around this unlinked and unattached SOP
                SceneObjectGroup objectGroup = new SceneObjectGroup(linkPart);

                m_scene.AddNewSceneObject(objectGroup, true);

                linkPart.Rezzed = RootPart.Rezzed;

                // this is as it seems to be in sl now
                if(linkPart.PhysicsShapeType == (byte)PhysShapeType.none)
                    linkPart.PhysicsShapeType = linkPart.DefaultPhysicsShapeType(); // root prims can't have type none for now

                objectGroup.HasGroupChangedDueToDelink = true;
                if (sendEvents)
                   linkPart.TriggerScriptChangedEvent(Changed.LINK);
            }

            if(unlinkroot)
            {
                //TODO
            }

            lock (m_parts.SyncRoot)
            {
                SceneObjectPart[] parts = m_parts.GetArray();
                if (parts.Length == 1)
                {
                    // Single prim left
                    m_rootPart.LinkNum = 0;
                }
                else
                {
                    m_rootPart.LinkNum = 1;
                    int linknum = 2;
                    for (int i = 1; i < parts.Length; i++)
                        parts[i].LinkNum = linknum++;
                }
            }

            InvalidBoundsRadius();

            if (m_rootPart.PhysActor is not null)
                m_rootPart.PhysActor.Building = false;

            // When we delete a group, we currently have to force persist to the database if the object id has changed
            // (since delete works by deleting all rows which have a given object id)

            Scene.SimulationDataService.RemoveObject(UUID, Scene.RegionInfo.RegionID);
            HasGroupChangedDueToDelink = true;
            TriggerScriptChangedEvent(Changed.LINK);
            return;
        }
*/
        /// <summary>
        /// Stop this object from being persisted over server restarts.
        /// </summary>
        /// <param name="objectGroup"></param>
        public virtual void DetachFromBackup()
        {
            if (m_scene is not null)
            {
                m_scene.SceneGraph.FireDetachFromBackup(this);
                if (Backup)
                    m_scene.EventManager.OnBackup -= ProcessBackup;
            }
            Backup = false;
        }

        // This links an SOP from a previous linkset into my linkset.
        // The trick is that the SOP's position and rotation are relative to the old root SOP's
        //    so we are passed in the position and rotation of the old linkset so this can
        //    unjigger this SOP's position and rotation from the previous linkset and
        //    then make them relative to my linkset root.
        private void LinkNonRootPart(SceneObjectPart part, Vector3 oldGroupPosition, Quaternion oldGroupRotation, int linkNum)
        {
            Quaternion parentRot = oldGroupRotation;
            Quaternion oldRot = part.RotationOffset;

            // Move our position in world
            Vector3 axPos = part.OffsetPosition;
            axPos *= parentRot;
            Vector3 newPos = oldGroupPosition + axPos;
            part.setGroupPosition(newPos);
            part.setOffsetPosition(Vector3.Zero);

            // Compution our rotation in world
            Quaternion worldRot = parentRot * oldRot;
            part.RotationOffset = worldRot;

            // Add this SOP to our linkset
            part.SetParent(this);
            part.ParentID = m_rootPart.LocalId;
            m_parts.Add(part.UUID, part);

            part.LinkNum = linkNum;

            // Compute the new position of this SOP relative to the group position
            part.setOffsetPosition(newPos - AbsolutePosition);

            // (radams1 20120711: I don't know why part.OffsetPosition is set multiple times.
            //   It would have the affect of setting the physics engine position multiple
            //   times. In theory, that is not necessary but I don't have a good linkset
            //   test to know that cleaning up this code wouldn't break things.)

            // Compute the SOP's rotation relative to the rotation of the group.
            parentRot = m_rootPart.RotationOffset;
 
            Quaternion newRot = Quaternion.Conjugate(parentRot) * worldRot;
            part.setRotationOffset(newRot);

            Vector3 pos = part.OffsetPosition;
            pos *= Quaternion.Conjugate(parentRot);

            part.OffsetPosition = pos; // update position and orientation on physics also

            // Since this SOP's state has changed, push those changes into the physics engine
            //    and the simulator.
            // done on caller
            //part.UpdatePrimFlags(UsesPhysics, IsTemporary, IsPhantom, IsVolumeDetect, false);
        }

        double lastTouchTime = 0;

        /// <summary>
        /// If object is physical, apply force to move it around
        /// If object is not physical, just put it at the resulting location
        /// </summary>
        /// <param name="partID">Part ID to check for grab</param>
        /// <param name="offset">Always seems to be 0,0,0, so ignoring</param>
        /// <param name="pos">New position.  We do the math here to turn it into a force</param>
        /// <param name="remoteClient"></param>
        public void GrabMovement(UUID partID, Vector3 offset, Vector3 pos, IClientAPI remoteClienth)
        {
            if (m_scene.EventManager.TriggerGroupMove(UUID, pos))
            {
                if (BlockGrabOverride)
                    return;

                SceneObjectPart part = GetPart(partID);

                if (part is null)
                    return;

                if (part.BlockGrab)
                    return;

                PhysicsActor pa = m_rootPart.PhysActor;

                if (pa is not null && pa.IsPhysical)
                {
                    // empirically convert distance diference to a impulse
                    Vector3 grabforce = pos - AbsolutePosition;
                    grabforce *= pa.Mass * 0.1f;
                    pa.AddForce(grabforce, false);
                }
                else
                {
                    if(IsAttachment)
                        return;

                    // block movement if there was a touch at start
                    double now = Util.GetTimeStampMS();
                    if (now - lastTouchTime < 250)
                    {
                        lastTouchTime = now;
                        return;
                    }

                    // a touch or pass may had become active ??
                    if (((part.ScriptEvents & scriptEvents.anytouch) != 0) || (RootPart.ScriptEvents & scriptEvents.anytouch) != 0)
                    {
                        lastTouchTime = now;
                        return;
                    }

                    lastTouchTime = 0;
                    UpdateGroupPosition(pos);
                }
            }
        }

        /// <summary>
        /// If object is physical, prepare for spinning torques (set flag to save old orientation)
        /// </summary>
        /// <param name="rotation">Rotation.  We do the math here to turn it into a torque</param>
        /// <param name="remoteClient"></param>
        public void SpinStart(IClientAPI remoteClient)
        {
            if (BlockGrabOverride || m_rootPart.BlockGrab)
                    return;
            if (m_scene.EventManager.TriggerGroupSpinStart(UUID))
            {
                PhysicsActor pa = m_rootPart.PhysActor;
                if (pa is not null)
                {
                    if (pa.IsPhysical)
                    {
                        m_rootPart.IsWaitingForFirstSpinUpdatePacket = true;
                    }
                }
            }
        }

        /// <summary>
        /// If object is physical, apply torque to spin it around
        /// </summary>
        /// <param name="rotation">Rotation.  We do the math here to turn it into a torque</param>
        /// <param name="remoteClient"></param>
        public void SpinMovement(Quaternion newOrientation, IClientAPI remoteClient)
        {
            // The incoming newOrientation, sent by the client, "seems" to be the
            // desired target orientation. This needs further verification; in particular,
            // one would expect that the initial incoming newOrientation should be
            // fairly close to the original prim's physical orientation,
            // m_rootPart.PhysActor.Orientation. This however does not seem to be the
            // case (might just be an issue with different quaternions representing the
            // same rotation, or it might be a coordinate system issue).
            //
            // Since it's not clear what the relationship is between the PhysActor.Orientation
            // and the incoming orientations sent by the client, we take an alternative approach
            // of calculating the delta rotation between the orientations being sent by the
            // client. (Since a spin is invoked by ctrl+shift+drag in the client, we expect
            // a steady stream of several new orientations coming in from the client.)
            // This ensures that the delta rotations are being calculated from self-consistent
            // pairs of old/new rotations. Given the delta rotation, we apply a torque around
            // the delta rotation axis, scaled by the object mass times an arbitrary scaling
            // factor (to ensure the resulting torque is not "too strong" or "too weak").
            //
            // Ideally we need to calculate (probably iteratively) the exact torque or series
            // of torques needed to arrive exactly at the destination orientation. However, since
            // it is not yet clear how to map the destination orientation (provided by the viewer)
            // into PhysActor orientations (needed by the physics engine), we omit this step.
            // This means that the resulting torque will at least be in the correct direction,
            // but it will result in over-shoot or under-shoot of the target orientation.
            // For the end user, this means that ctrl+shift+drag can be used for relative,
            // but not absolute, adjustments of orientation for physical prims.

            if (BlockGrabOverride || m_rootPart.BlockGrab)
                    return;

            if (m_scene.EventManager.TriggerGroupSpin(UUID, newOrientation))
            {
                PhysicsActor pa = m_rootPart.PhysActor;

                if (pa is not null && pa.IsPhysical)
                {
                    if (m_rootPart.IsWaitingForFirstSpinUpdatePacket)
                    {
                      // first time initialization of "old" orientation for calculation of delta rotations
                        m_rootPart.SpinOldOrientation = newOrientation;
                        m_rootPart.IsWaitingForFirstSpinUpdatePacket = false;
                    }
                    else
                    {
                        // save and update old orientation
                        Quaternion old = m_rootPart.SpinOldOrientation;
                        m_rootPart.SpinOldOrientation = newOrientation;
                        //m_log.Error("[SCENE OBJECT GROUP]: Old orientation is " + old);
                        //m_log.Error("[SCENE OBJECT GROUP]: Incoming new orientation is " + newOrientation);

                        // compute difference between previous old rotation and new incoming rotation
                        Quaternion minimalRotationFromQ1ToQ2 = newOrientation * Quaternion.Inverse(old);

                        minimalRotationFromQ1ToQ2.GetAxisAngle(out Vector3 spinforce, out float rotationAngle);
                        if(Math.Abs(rotationAngle)< 0.001)
                            return;

                        spinforce.Normalize();

                        //m_log.Error("SCENE OBJECT GROUP]: rotation axis is " + rotationAxis);
                        if(rotationAngle > 0)
                            spinforce = spinforce * pa.Mass * 0.1f; // 0.1 is an arbitrary torque scaling factor
                        else
                           spinforce = spinforce * pa.Mass * -0.1f; // 0.1 is an arbitrary torque scaling
                        pa.AddAngularForce(spinforce,true);
                    }
                }
                else
                {
                    NonPhysicalSpinMovement(newOrientation);
                }
            }
        }

        /// <summary>
        /// Apply rotation for spinning non-physical linksets (Ctrl+Shift+Drag)
        /// As with dragging, scripted objects must be blocked from spinning
        /// </summary>
        /// <param name="newOrientation">New Rotation</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void NonPhysicalSpinMovement(Quaternion newOrientation)
        {
            if(!IsAttachment && ScriptCount() == 0)
                UpdateGroupRotationR(newOrientation);
        }

        /// <summary>
        /// Set the name of a prim
        /// </summary>
        /// <param name="name"></param>
        /// <param name="localID"></param>
        public void SetPartName(string name, uint localID)
        {
            SceneObjectPart part = GetPart(localID);
            if (part is not null)
            {
                part.Name = name;
            }
        }

        public void SetPartDescription(string des, uint localID)
        {
            SceneObjectPart part = GetPart(localID);
            if (part is not null)
            {
                part.Description = des;
            }
        }

        public void SetPartText(string text, uint localID)
        {
            SceneObjectPart part = GetPart(localID);
            part?.SetText(text);
        }

        public void SetPartText(string text, UUID partID)
        {
            SceneObjectPart part = GetPart(partID);
            part?.SetText(text);
        }

       [MethodImpl(MethodImplOptions.AggressiveInlining)]
       public string GetPartName(uint localID)
        {
            SceneObjectPart part = GetPart(localID);
            return part is not null ? part.Name : string.Empty;
        }

        public string GetPartDescription(uint localID)
        {
            SceneObjectPart part = GetPart(localID);
            return part is not null ? part.Description : string.Empty;
        }

        /// <summary>
        /// Update prim flags for this group.
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="UsePhysics"></param>
        /// <param name="SetTemporary"></param>
        /// <param name="SetPhantom"></param>
        /// <param name="SetVolumeDetect"></param>
        public void UpdateFlags(bool UsePhysics, bool SetTemporary, bool SetPhantom, bool SetVolumeDetect)
        {
            if (m_scene is null || IsDeleted)
                return;

            HasGroupChanged = true;

            if (SetTemporary)
            {
                DetachFromBackup();
                // Remove from database and parcel prim count
                //
                m_scene.DeleteFromStorage(UUID);
            }
            else if (!Backup)
            {
                // Previously been temporary now switching back so make it
                // available for persisting again
                AttachToBackup();
            }

            SceneObjectPart[] parts = m_parts.GetArray();

            if (UsePhysics)
            {
                int maxprims = m_scene.m_linksetPhysCapacity;
                bool checkShape = maxprims > 0 && parts.Length > maxprims;

                for (int i = 0; i < parts.Length; i++)
                {
                    SceneObjectPart part = parts[i];

                    if(part.PhysicsShapeType == (byte)PhysicsShapeType.None)
                        continue; // assuming root type was checked elsewhere

                    if (checkShape)
                    {
                        if (--maxprims < 0)
                        {
                            UsePhysics = false;
                            break;
                        }
                    }

                    if (part.Scale.X > m_scene.m_maxPhys ||
                        part.Scale.Y > m_scene.m_maxPhys ||
                        part.Scale.Z > m_scene.m_maxPhys )
                    {
                        UsePhysics = false; // Reset physics
                        break;
                    }
                }
            }

            if (parts.Length > 1)
            {
                m_rootPart.UpdatePrimFlags(UsePhysics, SetTemporary, SetPhantom, SetVolumeDetect, true);

                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i].UUID != m_rootPart.UUID)
                        parts[i].UpdatePrimFlags(UsePhysics, SetTemporary, SetPhantom, SetVolumeDetect, true);
                }

                if (m_rootPart.PhysActor is not null)
                    m_rootPart.PhysActor.Building = false;
            }
            else
                m_rootPart.UpdatePrimFlags(UsePhysics, SetTemporary, SetPhantom, SetVolumeDetect, false);

            m_scene.EventManager.TriggerParcelPrimCountTainted();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateExtraParam(uint localID, ushort type, bool inUse, byte[] data)
        {
            SceneObjectPart part = GetPart(localID);
            part?.UpdateExtraParam(type, inUse, data);
        }

        /// <summary>
        /// Gets the number of parts
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetPartCount()
        {
            return m_parts.Count;
        }

        public void AdjustChildPrimPermissions(bool forceTaskInventoryPermissive)
        {
            uint newOwnerMask = (uint)(PermissionMask.All | PermissionMask.Export) & 0xfffffff0; // Mask folded bits
            uint foldedPerms = RootPart.OwnerMask & (uint)PermissionMask.FoldedMask;

            ForEachPart(part =>
            {
                newOwnerMask &= part.BaseMask;
                if (part != RootPart)
                    part.ClonePermissions(RootPart);
                if (forceTaskInventoryPermissive)
                    part.Inventory.ApplyGodPermissions(part.BaseMask);
            });

            uint lockMask = ~(uint)(PermissionMask.Move);
            uint lockBit = RootPart.OwnerMask & (uint)(PermissionMask.Move);
            RootPart.OwnerMask = (RootPart.OwnerMask & lockBit) | ((newOwnerMask | foldedPerms) & lockMask);

            //m_log.DebugFormat(
            //    "[SCENE OBJECT GROUP]: RootPart.OwnerMask now {0} for {1} in {2}",
            //    (OpenMetaverse.PermissionMask)RootPart.OwnerMask, Name, Scene.Name);
            InvalidateEffectivePerms();
            RootPart.ScheduleFullUpdate();
        }

        public void UpdatePermissions(UUID AgentID, byte field, uint localID,
                uint mask, byte addRemTF)
        {
            RootPart.UpdatePermissions(AgentID, field, localID, mask, addRemTF);

            bool god = Scene.Permissions.IsGod(AgentID);

            if (field == 1 && god)
            {
                ForEachPart(part =>
                {
                    part.BaseMask = RootPart.BaseMask;
                });
            }

            AdjustChildPrimPermissions(false);

            if (field == 1 && god) // Base mask was set. Update all child part inventories
            {
                foreach (SceneObjectPart part in Parts)
                    part.Inventory.ApplyGodPermissions(RootPart.BaseMask);
                InvalidateEffectivePerms();
            }

            HasGroupChanged = true;

            // Send the group's properties to all clients once all parts are updated
            if (Scene.TryGetClient(AgentID, out IClientAPI client))
                SendPropertiesToClient(client);
        }

        #endregion

        #region Shape

        /// <summary>
        ///
        /// </summary>
        /// <param name="shapeBlock"></param>
        public void UpdateShape(ObjectShapePacket.ObjectDataBlock shapeBlock, uint localID)
        {
            SceneObjectPart part = GetPart(localID);
            part?.UpdateShape(shapeBlock);
            InvalidBoundsRadius();
        }

        #endregion

        #region Resize

        /// <summary>
        /// Resize the entire group of prims.
        /// </summary>
        /// <param name="scale"></param>
        public void GroupResize(Vector3 scale)
        {
            //m_log.DebugFormat(
            //    "[SCENE OBJECT GROUP]: Group resizing {0} {1} from {2} to {3}", Name, LocalId, RootPart.Scale, scale);

            if (Scene is null)
                return;

            PhysicsActor pa = m_rootPart.PhysActor;

            float minsize = Scene.m_minNonphys;
            float maxsize = Scene.m_maxNonphys;

            if (pa is not null && pa.IsPhysical)
            {
                minsize = Scene.m_minPhys;
                maxsize = Scene.m_maxPhys;
            }

            scale.X = Utils.Clamp(scale.X, minsize, maxsize);
            scale.Y = Utils.Clamp(scale.Y, minsize, maxsize);
            scale.Z = Utils.Clamp(scale.Z, minsize, maxsize);

            // requested scaling factors
            float x = scale.X / RootPart.Scale.X;
            float y = scale.Y / RootPart.Scale.Y;
            float z = scale.Z / RootPart.Scale.Z;

            SceneObjectPart[] parts = m_parts.GetArray();

            // fix scaling factors so parts don't violate dimensions
            for(int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart obPart = parts[i];
                if(obPart.UUID != m_rootPart.UUID)
                {
                    Vector3 oldSize = obPart.Scale;

                    float f;
                    if(oldSize.X * x > maxsize)
                    {
                        f = maxsize / oldSize.X;
                        f /= x;
                        x *= f;
                        y *= f;
                        z *= f;
                    }
                    else if(oldSize.X * x < minsize)
                    {
                        f = minsize / oldSize.X;
                        f /= x;
                        x *= f;
                        y *= f;
                        z *= f;
                    }

                    if(oldSize.Y * y > maxsize)
                    {
                        f = maxsize / oldSize.Y;
                        f /= y;
                        x *= f;
                        y *= f;
                        z *= f;
                    }
                    else if(oldSize.Y * y < minsize)
                    {
                        f = minsize / oldSize.Y;
                        f /= y;
                        x *= f;
                        y *= f;
                        z *= f;
                    }

                    if(oldSize.Z * z > maxsize)
                    {
                        f = maxsize / oldSize.Z;
                        f /= z;
                        x *= f;
                        y *= f;
                        z *= f;
                    }
                    else if(oldSize.Z * z < minsize)
                    {
                        f = minsize / oldSize.Z;
                        f /= z;
                        x *= f;
                        y *= f;
                        z *= f;
                    }
                }
            }

            Vector3 rootScale = RootPart.Scale;
            rootScale.X *= x;
            rootScale.Y *= y;
            rootScale.Z *= z;

            RootPart.Scale = rootScale;

            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart obPart = parts[i];

                if (obPart != m_rootPart)
                {
                    Vector3 currentpos = obPart.OffsetPosition;
                    currentpos.X *= x;
                    currentpos.Y *= y;
                    currentpos.Z *= z;

                    Vector3 newSize = obPart.Scale;
                    newSize.X *= x;
                    newSize.Y *= y;
                    newSize.Z *= z;

                    obPart.Scale = newSize;
                    obPart.UpdateOffSet(currentpos);
                }
            }

            InvalidBoundsRadius();
            HasGroupChanged = true;
            m_rootPart.TriggerScriptChangedEvent(Changed.SCALE);
            ScheduleGroupForFullUpdate();

        }

        public bool GroupResize(double fscale)
        {
            //m_log.DebugFormat(
            //    "[SCENE OBJECT GROUP]: Group resizing {0} {1} from {2} to {3}", Name, LocalId, RootPart.Scale, fscale);

            if (Scene is null || IsDeleted || inTransit || fscale < 0)
                return false;

            // ignore lsl restrictions. let them be done a LSL
            PhysicsActor pa = m_rootPart.PhysActor;

            RootPart.KeyframeMotion?.Suspend();

            float minsize = Scene.m_minNonphys;
            float maxsize = Scene.m_maxNonphys;

            // assuming physics is more restrictive
            if (pa is not null && pa.IsPhysical)
            {
                minsize = Scene.m_minPhys;
                maxsize = Scene.m_maxPhys;
            }

            SceneObjectPart[] parts = m_parts.GetArray();
            float tmp;
            // check scaling factor so parts don't violate dimensions
            for(int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart obPart = parts[i];
                Vector3 oldSize = obPart.Scale;
                tmp = (float)(oldSize.X * fscale);
                if(tmp > maxsize)
                    return false;
                if(tmp < minsize)
                    return false;

                tmp = (float)(oldSize.Y * fscale);
                if(tmp > maxsize)
                    return false;
                if(tmp < minsize)
                    return false;

                tmp = (float)(oldSize.Z * fscale);
                if(tmp > maxsize)
                    return false;
                if(tmp < minsize)
                    return false;
            }

            Vector3 newSize = RootPart.Scale * (float)fscale;
            if(pa is not null)
                pa.Building = true;

            RootPart.Scale = newSize;

            Vector3 currentpos;
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart obPart = parts[i];
                if (obPart != m_rootPart)
                {
                    obPart.Scale *= (float)fscale;
                    currentpos = obPart.OffsetPosition * (float)fscale;
                    obPart.UpdateOffSet(currentpos);
                }
            }

            if(pa is not null)
                pa.Building = false;

            InvalidBoundsRadius();

            HasGroupChanged = true;
            m_rootPart.TriggerScriptChangedEvent(Changed.SCALE);
            ScheduleGroupForFullUpdate();

            RootPart.KeyframeMotion?.Resume();

            return true;
        }

        public float GetMaxGroupResizeScale()
        {
            if (Scene is null || IsDeleted || inTransit)
                return 1.0f;

            float maxsize = Scene.m_maxNonphys;
            PhysicsActor pa = m_rootPart.PhysActor;
            // assuming physics is more restrictive
            if (pa is not null && pa.IsPhysical)
                maxsize = Scene.m_maxPhys;

            SceneObjectPart[] parts = m_parts.GetArray();
            float larger = float.MinValue;

            for(int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart obPart = parts[i];
                Vector3 oldSize = obPart.Scale;
                if(larger < oldSize.X)
                   larger = oldSize.X;

                if(larger < oldSize.Y)
                   larger = oldSize.Y;

                if(larger < oldSize.Z)
                   larger = oldSize.Z;
            }

            if(larger >=  maxsize)
                return 1.0f;

            larger += 1e-3f;
            float fscale = maxsize / larger;

            return fscale;
        }

        public float GetMinGroupResizeScale()
        {
            if (Scene is null || IsDeleted || inTransit)
                return 1.0f;

            float minsize = Scene.m_minNonphys;
            PhysicsActor pa = m_rootPart.PhysActor;
            // assuming physics is more restrictive
            if (pa is not null && pa.IsPhysical)
                minsize = Scene.m_minPhys;

            SceneObjectPart[] parts = m_parts.GetArray();
            float smaller = float.MaxValue;

            for(int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart obPart = parts[i];
                Vector3 oldSize = obPart.Scale;
                if(smaller > oldSize.X)
                   smaller = oldSize.X;

                if(smaller > oldSize.Y)
                   smaller = oldSize.Y;

                if(smaller > oldSize.Z)
                   smaller = oldSize.Z;
            }

            if(smaller <= minsize)
                return 1.0f;

            if(smaller > 2e-3f)
                smaller -= 1e-3f;
            float fscale = minsize / smaller;
            if(fscale < 1e-8f)
                fscale = 1e-8f;

            return fscale;
        }

        #endregion

        #region Position

        /// <summary>
        /// Move this scene object
        /// </summary>
        /// <param name="pos"></param>
        public void UpdateGroupPosition(Vector3 pos)
        {
            if (m_scene.EventManager.TriggerGroupMove(UUID, pos))
            {
                if (IsAttachment)
                {
                    m_rootPart.AttachedPos = pos;
                }
                else if (RootPart.GetStatusSandbox())
                {
                    Vector3 mov = pos - RootPart.StatusSandboxPos;
                    float movLenSQ = mov.LengthSquared();
                    if (movLenSQ > 100.5f)
                    {
                        mov *= 10.0f / MathF.Sqrt(movLenSQ);
                        AbsolutePosition = RootPart.StatusSandboxPos + mov;
                        RootPart.ScriptSetPhysicsStatus(false);
                        Scene.SimChat(Utils.StringToBytes("Hit Sandbox Limit"),
                              ChatTypeEnum.DebugChannel, 0x7FFFFFFF, RootPart.AbsolutePosition, Name, UUID, false);
                        HasGroupChanged = true;
                        return;
                    }
                }

                AbsolutePosition = pos;
                HasGroupChanged = true;
            }

            //we need to do a terse update even if the move wasn't allowed
            // so that the position is reset in the client (the object snaps back)
            RootPart.ScheduleTerseUpdate();
        }

        /// <summary>
        /// Update the position of a single part of this scene object
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="localID"></param>
        ///

        public void UpdateSinglePosition(Vector3 pos, uint localID)
        {
            SceneObjectPart part = GetPart(localID);

            if (part is not null)
            {
                // unlock parts position change
                if (m_rootPart.PhysActor is not null)
                    m_rootPart.PhysActor.Building = true;

                if (part.UUID == m_rootPart.UUID)
                {
                    UpdateRootPosition(pos);
                }
                else
                {
                    part.UpdateOffSet(pos);
                }

                if (m_rootPart.PhysActor is not null)
                    m_rootPart.PhysActor.Building = false;

                HasGroupChanged = true;
            }
        }

        /// <summary>
        /// Update just the root prim position in a linkset
        /// </summary>
        /// <param name="newPos"></param>
        public void UpdateRootPosition(Vector3 newPos)
        {
            // needs to be called with phys building true
            Vector3 oldPos;

            if (IsAttachment)
                oldPos = m_rootPart.AttachedPos + m_rootPart.OffsetPosition;  // OffsetPosition should always be 0 in an attachments's root prim
            else
                oldPos = AbsolutePosition + m_rootPart.OffsetPosition;

            Vector3 diff = oldPos - newPos;
            Quaternion partRotation = m_rootPart.RotationOffset;
            diff *= Quaternion.Inverse(partRotation);

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart obPart = parts[i];
                if (obPart != m_rootPart)
                    obPart.OffsetPosition += diff;
            }

            AbsolutePosition = newPos;

            if (IsAttachment)
                m_rootPart.AttachedPos = newPos;

            HasGroupChanged = true;
            if (m_rootPart.Undoing)
            {
                ScheduleGroupForFullUpdate();
            }
            else
            {
                ScheduleGroupForTerseUpdate();
            }
        }

        #endregion

        #region Rotation

        /// <summary>
        /// Update the rotation of the group.
        /// </summary>
        /// <param name="rot"></param>
        public void UpdateGroupRotationR(Quaternion rot)
        {
            m_rootPart.UpdateRotation(rot);

            /* this is done by rootpart RotationOffset set called by UpdateRotation
            PhysicsActor actor = m_rootPart.PhysActor;
            if (actor is not null)
            {
                actor.Orientation = m_rootPart.RotationOffset;
                m_scene.PhysicsScene.AddPhysicsActorTaint(actor);
            }
            */
            HasGroupChanged = true;
            ScheduleGroupForTerseUpdate();
        }

        /// <summary>
        /// Update the position and rotation of a group simultaneously.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="rot"></param>
        public void UpdateGroupRotationPR(Vector3 pos, Quaternion rot)
        {
            m_rootPart.UpdateRotation(rot);

            //already done above
            //PhysicsActor actor = m_rootPart.PhysActor;
            //if (actor is not null)
            //{
            //    actor.Orientation = m_rootPart.RotationOffset;
            //}

            if (IsAttachment)
            {
                m_rootPart.AttachedPos = pos;
            }

            AbsolutePosition = pos;

            HasGroupChanged = true;
            ScheduleGroupForTerseUpdate();
        }

        /// <summary>
        /// Update the rotation of a single prim within the group.
        /// </summary>
        /// <param name="rot"></param>
        /// <param name="localID"></param>
        public void UpdateSingleRotation(Quaternion rot, uint localID)
        {
            SceneObjectPart part = GetPart(localID);
            if (part is not null)
            {
                if (m_rootPart.PhysActor is not null)
                    m_rootPart.PhysActor.Building = true;

                if (part == m_rootPart)
                {
                    UpdateRootRotation(rot);
                }
                else
                {
                    part.UpdateRotation(rot);
                }

                if (m_rootPart.PhysActor is not null)
                    m_rootPart.PhysActor.Building = false;
            }
        }

        /// <summary>
        /// Update the position and rotation simultaneously of a single prim within the group.
        /// </summary>
        /// <param name="rot"></param>
        /// <param name="localID"></param>
        public void UpdateSingleRotation(Quaternion rot, Vector3 pos, uint localID)
        {
            SceneObjectPart part = GetPart(localID);
            if (part is not null)
            {
                if (m_rootPart.PhysActor is not null)
                    m_rootPart.PhysActor.Building = true;

                if (part.UUID == m_rootPart.UUID)
                {
                    UpdateRootRotation(rot);
                    AbsolutePosition = pos;
                }
                else
                {
                    part.UpdateRotation(rot);
                    part.OffsetPosition = pos;
                }

                if (m_rootPart.PhysActor is not null)
                    m_rootPart.PhysActor.Building = false;
            }
        }

        /// <summary>
        /// Update the rotation of just the root prim of a linkset.
        /// </summary>
        /// <param name="rot"></param>
        public void UpdateRootRotation(Quaternion rot)
        {
            // needs to be called with phys building true

            Quaternion transformRot = Quaternion.Inverse(rot) * m_rootPart.RotationOffset;

            //Don't use UpdateRotation because it schedules an update prematurely
            m_rootPart.RotationOffset = rot;

            PhysicsActor pa = m_rootPart.PhysActor;
            if (pa is not null)
                pa.Orientation = rot;

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart prim = parts[i];
                if (prim != m_rootPart)
                {
                    prim.RotationOffset = transformRot * prim.RotationOffset;
                    prim.OffsetPosition *= transformRot;
                }
            }

            HasGroupChanged = true;
            ScheduleGroupForFullUpdate();
        }

        private enum updatetype :int
        {
            none = 0,
            partterse = 1,
            partfull = 2,
            groupterse = 3,
            groupfull = 4
        }

        public void doChangeObject(SceneObjectPart part, ObjectChangeData data)
        {
            // TODO  this still as excessive *.Schedule*Update()s

            if (part is not null && part.ParentGroup is not null)
            {
                ObjectChangeType change = data.change;
                bool togroup = (change & ObjectChangeType.Group) != 0;
 
                SceneObjectGroup group = part.ParentGroup;
                PhysicsActor pha = group.RootPart.PhysActor;

                updatetype updateType = updatetype.none;

                if (togroup)
                {
                    // related to group
                    if ((change & (ObjectChangeType.Rotation | ObjectChangeType.Position)) != 0)
                    {
                        if ((change & ObjectChangeType.Rotation) != 0)
                        {
                            group.RootPart.UpdateRotation(data.rotation);
                            updateType = updatetype.none;
                        }
                        if ((change & ObjectChangeType.Position) != 0)
                        {
                            if (IsAttachment || m_scene.Permissions.CanObjectEntry(group, false, data.position))
                                UpdateGroupPosition(data.position);
                            updateType = updatetype.groupterse;
                        }
                        else
                        // ugly rotation update of all parts
                        {
                            group.ResetChildPrimPhysicsPositions();
                        }

                    }
                    if ((change & ObjectChangeType.Scale) != 0)
                    {
                        if (pha is not null)
                            pha.Building = true;

                        group.GroupResize(data.scale);
                        updateType = updatetype.none;

                        if (pha is not null)
                            pha.Building = false;
                    }
                }
                else
                {
                    // related to single prim in a link-set ( ie group)
                    if (pha is not null)
                        pha.Building = true;

                    // root part is special
                    // parts offset positions or rotations need to change also

                    if (part == group.RootPart)
                    {
                        if ((change & ObjectChangeType.Rotation) != 0)
                            group.UpdateRootRotation(data.rotation);
                        if ((change & ObjectChangeType.Position) != 0)
                            group.UpdateRootPosition(data.position);
                        if ((change & ObjectChangeType.Scale) != 0)
                            part.Resize(data.scale);
                    }
                    else
                    {
                        if ((change & ObjectChangeType.Position) != 0)
                        {
                            part.OffsetPosition = data.position;
                            updateType = updatetype.partterse;
                        }
                        if ((change & ObjectChangeType.Rotation) != 0)
                        {
                            part.UpdateRotation(data.rotation);
                            updateType = updatetype.none;
                        }
                        if ((change & ObjectChangeType.Scale) != 0)
                        {
                            part.Resize(data.scale);
                            updateType = updatetype.none;
                        }
                    }

                    if (pha is not null)
                        pha.Building = false;
                }

                if (updateType != updatetype.none)
                {
                    group.HasGroupChanged = true;

                    switch (updateType)
                    {
                        case updatetype.partterse:
                            part.ScheduleTerseUpdate();
                            break;
                        case updatetype.partfull:
                            part.ScheduleFullUpdate();
                            break;
                        case updatetype.groupterse:
                            group.ScheduleGroupForTerseUpdate();
                            break;
                        case updatetype.groupfull:
                            group.ScheduleGroupForFullUpdate();
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        #endregion

        public void SetAxisRotation(int axis, int rotate10)
        {
            if((axis & (int)(axisSelect.STATUS_ROTATE_X | axisSelect.STATUS_ROTATE_Y | axisSelect.STATUS_ROTATE_Z)) == 0)
                return;

            bool lockaxis = rotate10 == 0; // zero means axis locked

            byte locks = RootPart.RotationAxisLocks;

            if ((axis & (int)axisSelect.STATUS_ROTATE_X) != 0)
            {
                if (lockaxis)
                    locks |= (byte)axisSelect.STATUS_ROTATE_X;
                else
                    locks &= (byte)axisSelect.NOT_STATUS_ROTATE_X;
            }

            if ((axis & (int)axisSelect.STATUS_ROTATE_Y) != 0)
            {
                if (lockaxis)
                    locks |= (byte)axisSelect.STATUS_ROTATE_Y;
                else
                    locks &= (byte)axisSelect.NOT_STATUS_ROTATE_Y;
            }

            if ((axis & (int)axisSelect.STATUS_ROTATE_Z) != 0)
            {
                if (lockaxis)
                    locks |= (byte)axisSelect.STATUS_ROTATE_Z;
                else
                    locks &= (byte)axisSelect.NOT_STATUS_ROTATE_Z;
            }

            RootPart.RotationAxisLocks = locks;
            RootPart.SetPhysicsAxisRotation();
        }

        public int GetAxisRotation(int axis)
        {
            byte  rotAxislocks = RootPart.RotationAxisLocks;

            // if multiple return the one with higher id
            if (axis == (int)axisSelect.STATUS_ROTATE_Z)
                return (rotAxislocks & (byte)axisSelect.STATUS_ROTATE_Z) == 0 ? 1:0;
            if (axis == (int)axisSelect.STATUS_ROTATE_Y)
                return (rotAxislocks & (byte)axisSelect.STATUS_ROTATE_Y) == 0 ? 1:0;
            if (axis == (int)axisSelect.STATUS_ROTATE_X)
                return (rotAxislocks & (byte)SceneObjectGroup.axisSelect.STATUS_ROTATE_X) == 0 ? 1:0;

            return 0;
        }

        public int RegisterRotTargetWaypoint(UUID scriptID, Quaternion target, float tolerance)
        {
            int handle = m_scene.AllocateIntId();
            scriptRotTarget waypoint = new()
            {
                targetRot = target,
                tolerance = tolerance,
                scriptID = scriptID,
                handle = handle
            };

            lock (m_targets)
            {
                if(m_targetsByScript.TryGetValue(scriptID, out List<int> handles))
                {
                    if (handles.Count >= 8)
                    {
                        int todel = handles[0];
                        handles.RemoveAt(0);
                        if(!m_rotTargets.Remove(todel))
                            m_targets.Remove(todel);
                    }
                    handles.Add(handle);
                }
                else
                    m_targetsByScript[scriptID] = new List<int>(){handle};

                m_rotTargets.Add(handle, waypoint);
                m_scene.AddGroupTarget(this);
            }
            return handle;
        }

        public void UnRegisterRotTargetWaypoint(int handle)
        {
            lock (m_targets)
            {
                if(m_rotTargets.TryGetValue(handle, out scriptRotTarget waypoint))
                {
                    if(m_targetsByScript.TryGetValue(waypoint.scriptID, out List<int>handles))
                    {
                        handles.Remove(handle);
                        if(handles.Count == 0)
                            m_targetsByScript.Remove(waypoint.scriptID);
                    }
                    m_rotTargets.Remove(handle);
                }
                if (m_targets.Count == 0 && m_rotTargets.Count == 0)
                    m_scene.RemoveGroupTarget(this);
            }
        }

        public int RegisterTargetWaypoint(UUID scriptID, Vector3 target, float tolerance)
        {
            int handle = m_scene.AllocateIntId();
            scriptPosTarget waypoint = new()
            {
                targetPos = target,
                tolerance = tolerance * tolerance,
                scriptID = scriptID,
                handle = handle
            };

            lock (m_targets)
            {
                if (m_targetsByScript.TryGetValue(scriptID, out List<int> handles))
                {
                    if (handles.Count >= 8)
                    {
                        int todel = handles[0];
                        handles.RemoveAt(0);
                        if(!m_targets.Remove(todel))
                            m_rotTargets.Remove(todel);
                    }
                    handles.Add(handle);
                }
                else
                    m_targetsByScript[scriptID] = new List<int>() { handle };

                m_targets.Add(handle, waypoint);
                m_scene.AddGroupTarget(this);
            }
            return handle;
        }

        public void UnregisterTargetWaypoint(int handle)
        {
            lock (m_targets)
            {
                if (m_targets.TryGetValue(handle, out scriptPosTarget waypoint))
                {
                    if (m_targetsByScript.TryGetValue(waypoint.scriptID, out List<int> handles))
                    {
                        handles.Remove(handle);
                        if (handles.Count == 0)
                            m_targetsByScript.Remove(waypoint.scriptID);
                    }
                    m_targets.Remove(handle);
                }

                if (m_targets.Count == 0 && m_rotTargets.Count == 0)
                    m_scene.RemoveGroupTarget(this);
            }
        }

        public void RemoveScriptTargets(UUID scriptID)
        {
            lock (m_targets)
            {
                if(m_targetsByScript.TryGetValue(scriptID, out List<int> toremove))
                {
                    m_targetsByScript.Remove(scriptID);
                    if (toremove.Count > 0)
                    {
                        for (int i = 0; i < toremove.Count; ++i)
                        {
                            if(!m_targets.Remove(toremove[i]))
                                m_rotTargets.Remove(toremove[i]);
                        }
                    }
                }
                m_scene.RemoveGroupTarget(this);
            }
        }

        public void CheckAtTargets()
        {
            int targetsCount = m_targets.Count;
            if (targetsCount > 0 && (m_scriptListens_atTarget || m_scriptListens_notAtTarget))
            {
                List<scriptPosTarget> atTargets = new();
                HashSet<UUID> notatTargets = new();
                Vector3 pos = m_rootPart.GroupPosition;
                lock (m_targets)
                {
                    foreach (scriptPosTarget target in m_targets.Values)
                    {
                        if (Vector3.DistanceSquared(target.targetPos, pos) <= target.tolerance)
                        {
                            if (m_scriptListens_atTarget)
                                atTargets.Add(target);
                            notatTargets.Remove(target.scriptID);
                        }
                        else
                        {
                            if (m_scriptListens_notAtTarget)
                                notatTargets.Add(target.scriptID);
                        }
                    }
                }

                if (atTargets.Count > 0)
                {
                    for (int target = 0; target < atTargets.Count; ++target)
                    {
                        scriptPosTarget att = atTargets[target];
                        m_scene.EventManager.TriggerAtTargetEvent(att.scriptID, (uint)att.handle, att.targetPos, pos);
                    }
                }

                if (notatTargets.Count > 0)
                {
                    foreach (UUID id in notatTargets)
                    {
                        m_scene.EventManager.TriggerNotAtTargetEvent(id);
                    }
                }
            }

            targetsCount = m_rotTargets.Count;
            if (targetsCount > 0 && (m_scriptListens_atRotTarget || m_scriptListens_notAtRotTarget))
            {
                List<scriptRotTarget> atRotTargets = new(targetsCount);
                HashSet<UUID> notatRotTargets = new();
                Quaternion rot = m_rootPart.RotationOffset;
                lock (m_targets)
                {
                    foreach (scriptRotTarget target in m_rotTargets.Values)
                    {
                        double angle = 2 * Math.Acos(Quaternion.Dot(target.targetRot, rot));
                        if (angle < 0)
                            angle = -angle;
                        if (angle > Math.PI)
                            angle = (2 * Math.PI - angle);
                        if (angle <= target.tolerance)
                        {
                            if (m_scriptListens_atRotTarget)
                                atRotTargets.Add(target);
                            notatRotTargets.Remove(target.scriptID);
                        }
                        else
                        {
                            if (m_scriptListens_notAtRotTarget)
                                notatRotTargets.Add(target.scriptID);
                        }
                    }
                }

                if (atRotTargets.Count > 0)
                {
                    for (int target = 0; target < atRotTargets.Count; ++target)
                    {
                        scriptRotTarget att = atRotTargets[target];
                        m_scene.EventManager.TriggerAtRotTargetEvent(att.scriptID, (uint)att.handle, att.targetRot, rot);
                    }
                }

                if (notatRotTargets.Count > 0)
                {
                    foreach (UUID id in notatRotTargets)
                    {
                        m_scene.EventManager.TriggerNotAtRotTargetEvent(id);
                    }
                }
            }
        }

        public Vector3 GetGeometricCenter()
        {
            // this is not real geometric center but a average of positions relative to root prim acording to
            // http://wiki.secondlife.com/wiki/llGetGeometricCenter
            // ignoring tortured prims details since sl also seems to ignore
            // so no real use in doing it on physics

            SceneObjectPart[] parts = m_parts.GetArray();
            if (parts.Length < 2)
                return Vector3.Zero;

            Vector3 gc = Vector3.Zero;
            // average all parts positions
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != RootPart)
                    gc += parts[i].OffsetPosition;
            }
            gc /= parts.Length;

            return gc;
        }

        public float GetMass()
        {
            float retmass = 0f;
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                retmass += parts[i].GetMass();

            return retmass;
        }

        // center of mass of full object
        public Vector3 GetCenterOfMass()
        {
            PhysicsActor pa = RootPart.PhysActor;

            if(((RootPart.Flags & PrimFlags.Physics) !=0) && pa !=null)
            {
                // physics knows better about center of mass of physical prims
                Vector3 tmp = pa.CenterOfMass;
                return tmp;
            }

            Vector3 Ptot = Vector3.Zero;
            float totmass = 0f;
            float m;

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                m = parts[i].GetMass();
                Ptot += parts[i].GetPartCenterOfMass() * m;
                totmass += m;
            }

            if (totmass == 0)
                totmass = 0;
            else
                totmass = 1 / totmass;
            Ptot *= totmass;

            return Ptot;
        }

        public void GetInertiaData(out float TotalMass, out Vector3 CenterOfMass, out Vector3 Inertia, out Vector4 aux )
        {
            PhysicsActor pa = RootPart.PhysActor;

            if(((RootPart.Flags & PrimFlags.Physics) !=0) && pa !=null)
            {
                PhysicsInertiaData inertia;

                inertia = pa.GetInertiaData();

                TotalMass = inertia.TotalMass;
                CenterOfMass = inertia.CenterOfMass;
                Inertia = inertia.Inertia;
                aux = inertia.InertiaRotation;

                return;
            }

            TotalMass = GetMass();
            CenterOfMass = GetCenterOfMass() - AbsolutePosition;
            CenterOfMass *= Quaternion.Conjugate(RootPart.RotationOffset);
            Inertia = Vector3.Zero;
            aux =  Vector4.Zero;
        }

        public void SetInertiaData(float TotalMass, Vector3 CenterOfMass, Vector3 Inertia, Vector4 aux )
        {
            PhysicsInertiaData inertiaData = new()
            {
                TotalMass = TotalMass,
                CenterOfMass = CenterOfMass,
                Inertia = Inertia,
                InertiaRotation = aux
            };

            if(TotalMass < 0)
                RootPart.PhysicsInertia = null;
            else
                RootPart.PhysicsInertia = inertiaData;

            PhysicsActor pa = RootPart.PhysActor;
            pa?.SetInertiaData(inertiaData);
        }

        /// <summary>
        /// Set the user group to which this scene object belongs.
        /// </summary>
        /// <param name="GroupID"></param>
        /// <param name="client"></param>
        public void SetGroup(UUID GroupID, IClientAPI client)
        {
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                part.SetGroup(GroupID, client);
                part.Inventory.ChangeInventoryGroup(GroupID);
            }

            HasGroupChanged = true;

            // Don't trigger the update here - otherwise some client issues occur when multiple updates are scheduled
            // for the same object with very different properties.  The caller must schedule the update.
            //ScheduleGroupForFullUpdate();
        }

        public void TriggerScriptChangedEvent(Changed val)
        {
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                parts[i].TriggerScriptChangedEvent(val);
        }

        /// <summary>
        /// Returns a count of the number of scripts in this groups parts.
        /// </summary>
        public int ScriptCount()
        {
            int count = 0;
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                count += parts[i].Inventory.ScriptCount();

            return count;
        }

        /// <summary>
        /// A float the value is a representative execution time in milliseconds of all scripts in the link set.
        /// </summary>
        public float ScriptExecutionTime()
        {
            IScriptModule[] engines = Scene.RequestModuleInterfaces<IScriptModule>();

            if (engines.Length == 0) // No engine at all
                return 0.0f;

            try
            {
                float time = 0.0f;

                // get all the scripts in all parts
                SceneObjectPart[] parts = m_parts.GetArray();
                List<TaskInventoryItem> scripts = new();
                for (int i = 0; i < parts.Length; i++)
                {
                    IEntityInventory inv = parts[i].Inventory;
                    if (inv is not null)
                        scripts.AddRange(parts[i].Inventory.GetInventoryItems(InventoryType.LSL));
                }

                // extract the UUIDs
                HashSet<UUID> unique = new();
                foreach (TaskInventoryItem script in scripts)
                    unique.Add(script.ItemID);

                List<UUID> ids = unique.ToList();

                // Offer the list of script UUIDs to each engine found and accumulate the time
                foreach (IScriptModule e in engines)
                {
                    if (e is not null)
                    {
                        time += e.GetScriptExecutionTime(ids);
                    }
                }
                return time;
            }
            catch
            {
                return 0.0f;
            }
        }

        public bool ScriptsMemory(out int memory)
        {
            memory = 0;
            IScriptModule[] engines = Scene.RequestModuleInterfaces<IScriptModule>();
            if (engines.Length == 0) // No engine at all
                return false;

            try
            {
                // get all the scripts in all parts
                SceneObjectPart[] parts = m_parts.GetArray();
                List<TaskInventoryItem> scripts = new();
                for (int i = 0; i < parts.Length; i++)
                {
                    IEntityInventory inv = parts[i].Inventory;
                    if(inv is not null)
                        scripts.AddRange(inv.GetInventoryItems(InventoryType.LSL));
                }

                if (scripts.Count == 0)
                    return false;

                // extract the UUIDs
                HashSet<UUID> unique = new();
                foreach (TaskInventoryItem script in scripts)
                    unique.Add(script.ItemID);

                List<UUID> ids = unique.ToList();
                // Offer the list of script UUIDs to each engine found and accumulate the memory
                foreach (IScriptModule e in engines)
                {
                    if (e is not null)
                    {
                        memory += e.GetScriptsMemory(ids);
                    }
                }
                return true;
            }
            catch
            { 
                return false;
            }

        }

        /// <summary>
        /// Returns a count of the number of running scripts in this groups parts.
        /// </summary>
        public int RunningScriptCount()
        {
            int count = 0;
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                count += parts[i].Inventory.RunningScriptCount();

            return count;
        }

        /// <summary>
        /// Get a copy of the list of sitting avatars on all prims of this object.
        /// </summary>
        /// <remarks>
        /// This is sorted by the order in which avatars sat down.  If an avatar stands up then all avatars that sat
        /// down after it move one place down the list.
        /// </remarks>
        /// <returns>A list of the sitting avatars.  Returns an empty list if there are no sitting avatars.</returns>
        public List<ScenePresence> GetSittingAvatars()
        {
            lock (m_sittingAvatars)
                return new List<ScenePresence>(m_sittingAvatars);
        }

        public bool HasSittingAvatar(UUID avatarID)
        {
            // locked O(n) :(
            lock (m_sittingAvatars)
            {
                for(int i = 0; i < m_sittingAvatars.Count; ++i)
                {
                    if(m_sittingAvatars[i].UUID.Equals(avatarID))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the number of sitting avatars.
        /// </summary>
        /// <remarks>This applies to all sitting avatars whether there is a sit target set or not.</remarks>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetSittingAvatarsCount()
        {
            lock (m_sittingAvatars)
                return m_sittingAvatars.Count;
        }

        public ScenePresence GetLinkSitingAvatar(int linknumber)
        {
            lock(m_parts)
                linknumber -= (m_parts.Count + 1);
            lock (m_sittingAvatars)
            {
                if (linknumber < m_sittingAvatars.Count && linknumber >= 0)
                    return  m_sittingAvatars[linknumber];
            }
            return null;
        }

        public override string ToString()
        {
            return $"{Name} {UUID} ({AbsolutePosition})";
        }

        #region ISceneObject

        public virtual ISceneObject CloneForNewScene()
        {
            SceneObjectGroup sog = Copy(false);
            sog.IsDeleted = false;
            return sog;
        }

        public virtual string ToXml2()
        {
            return SceneObjectSerializer.ToXml2Format(this);
        }

        public virtual string ExtraToXmlString()
        {
            return $"<ExtraFromItemID>{FromItemID}</ExtraFromItemID>";
        }

        public virtual void ExtraFromXmlString(string xmlstr)
        {
            if (string.IsNullOrEmpty(xmlstr))
            {
                FromItemID = UUID.Zero;
                return;
            }

            int indx = xmlstr.IndexOf("<ExtraFromItemID>");
            if (indx < 0)
            {
                FromItemID = UUID.Zero;
                return;
            }
            indx += 17;
            if(indx >= xmlstr.Length)
            {
                FromItemID = UUID.Zero;
                return;
            }

            int indx2 = xmlstr.IndexOf("</ExtraFromItemID>", indx);
            UUID uuid;
            if (indx2 < 0)
                _ = UUID.TryParse(xmlstr.AsSpan()[indx..], out uuid);
            else
                _ = UUID.TryParse(xmlstr.AsSpan()[indx..indx2], out uuid);

            FromItemID = uuid;
        }

        public void ResetOwnerChangeFlag()
        {
            ForEachPart(delegate(SceneObjectPart part)
            {
                part.ResetOwnerChangeFlag();
            });
            InvalidateEffectivePerms();
        }

        private readonly Dictionary<string,int> m_partsNameToLinkMap = new();
        private string GetLinkNumber_lastname;
        private int GetLinkNumber_lastnumber;

        public int GetLinkNumber(string name)
        {
            if(string.IsNullOrEmpty(name) || name == "Object" || name == "Primitive")
                return -1;

            lock(m_partsNameToLinkMap)
            {
                if (name == GetLinkNumber_lastname)
                    return GetLinkNumber_lastnumber;

                if (m_partsNameToLinkMap.Count == 0)
                {
                    GetLinkNumber_lastname = string.Empty;
                    GetLinkNumber_lastnumber = -1;

                    SceneObjectPart[] parts = m_parts.GetArray();
                    for (int i = 0; i < parts.Length; i++)
                    {
                        string s = parts[i].Name;
                        if(string.IsNullOrEmpty(s) || s == "Object" || s == "Primitive")
                            continue;

                        if(m_partsNameToLinkMap.ContainsKey(s))
                        {
                            int ol = parts[i].LinkNum;
                            if(ol < m_partsNameToLinkMap[s])
                                m_partsNameToLinkMap[s] = ol;
                        }
                        else
                            m_partsNameToLinkMap[s] = parts[i].LinkNum;
                    }
                }

                if(m_partsNameToLinkMap.ContainsKey(name))
                {
                    GetLinkNumber_lastname = name;
                    GetLinkNumber_lastnumber = m_partsNameToLinkMap[name];
                    return GetLinkNumber_lastnumber;
                }
            }

            if(m_sittingAvatars.Count > 0)
            {
                int j = m_parts.Count + 1;

                ScenePresence[] avs = m_sittingAvatars.ToArray();
                for (int i = 0; i < avs.Length; i++, j++)
                {
                    if (avs[i].Name == name)
                    {
                        GetLinkNumber_lastname = name;
                        GetLinkNumber_lastnumber = j;
                        return j;
                    }
                }
            }

            return -1;
        }

        public void InvalidatePartsLinkMaps(bool all = true)
        {
            lock(m_partsNameToLinkMap)
            {
                if(all)
                    m_partsNameToLinkMap.Clear();
                GetLinkNumber_lastname = string.Empty;
                GetLinkNumber_lastnumber = -1;
            }
        }

        public bool CollisionSoundThrottled(int collisionSoundType)
        {
            double time = m_lastCollisionSoundMS;
            //m_lastCollisionSoundMS = Util.GetTimeStampMS();
            //time = m_lastCollisionSoundMS - time;
            double now  = Util.GetTimeStampMS();
            time = now - time;
            switch (collisionSoundType)
            {
                case 0: // default sounds
                case 2: // default sounds with volume set by script
                    if(time < 300.0)                    
                        return true;
                    break;
                case 1: // selected sound
                    if(time < 200.0)                    
                        return true;
                    break;
                default:
                    break;
            }
            m_lastCollisionSoundMS = now;
            return false;
        }

        public bool GetOwnerName(out string FirstName, out string LastName)
        {
            if (RootPart is not null)
            {
                if(RootPart.OwnerID.Equals(RootPart.GroupID))
                {
                    IGroupsModule groups = m_scene.RequestModuleInterface<IGroupsModule>();
                    if (groups is not null)
                    {
                        GroupRecord grprec = groups.GetGroupRecord(RootPart.OwnerID);
                        if (grprec is not null)
                        {
                            FirstName = string.Empty;
                            LastName = grprec.GroupName;
                            return true;
                        }
                    }
                }
                else
                    return m_scene.UserManagementModule.GetUserName(RootPart.OwnerID, out FirstName, out LastName);
            }

            FirstName = string.Empty;
            LastName = string.Empty;
            return false;
        }
        #endregion
    }


}
