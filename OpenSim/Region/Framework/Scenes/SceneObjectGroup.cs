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

using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.PhysicsModules.SharedBase;
using OpenSim.Region.Framework.Scenes.Serialization;
using PermissionMask = OpenSim.Framework.PermissionMask;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.Framework.Scenes
{

    [Flags]
    public enum scriptEvents
    {
        None = 0,
        attach = 1,
        collision = 16,
        collision_end = 32,
        collision_start = 64,
        control = 128,
        dataserver = 256,
        email = 512,
        http_response = 1024,
        land_collision = 2048,
        land_collision_end = 4096,
        land_collision_start = 8192,
        at_target = 16384,
        at_rot_target = 16777216,
        listen = 32768,
        money = 65536,
        moving_end = 131072,
        moving_start = 262144,
        not_at_rot_target = 524288,
        not_at_target = 1048576,
        remote_data = 8388608,
        run_time_permissions = 268435456,
        state_entry = 1073741824,
        state_exit = 2,
        timer = 4,
        touch = 8,
        touch_end = 536870912,
        touch_start = 2097152,
        transaction_result = 33554432,
        object_rez = 4194304,
        anytouch = touch | touch_end | touch_start
    }

    public struct scriptPosTarget
    {
        public Vector3 targetPos;
        public float tolerance;
        public uint handle;
    }

    public struct scriptRotTarget
    {
        public Quaternion targetRot;
        public float tolerance;
        public uint handle;
    }

    public delegate void PrimCountTaintedDelegate();

    /// <summary>
    /// A scene object group is conceptually an object in the scene.  The object is constituted of SceneObjectParts
    /// (often known as prims), one of which is considered the root part.
    /// </summary>
    public partial class SceneObjectGroup : EntityBase, ISceneObject
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

        /// <summary>
        /// Signal whether the non-inventory attributes of any prims in the group have changed
        /// since the group's last persistent backup
        /// </summary>
        private bool m_hasGroupChanged = false;
        private long timeFirstChanged = 0;
        private long timeLastChanged = 0;
        private long m_maxPersistTime = 0;
        private long m_minPersistTime = 0;
//        private Random m_rand;

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
                    {
                        m_scene.SceneGraph.FireChangeBackup(this);
                    }
                    timeLastChanged = DateTime.UtcNow.Ticks;
                    if (!m_hasGroupChanged)
                        timeFirstChanged = DateTime.UtcNow.Ticks;
                    if (m_rootPart != null && m_rootPart.UUID != null && m_scene != null)
                    {
/*
                        if (m_rand == null)
                        {
                            byte[] val = new byte[16];
                            m_rootPart.UUID.ToBytes(val, 0);
                            m_rand = new Random(BitConverter.ToInt32(val, 0));
                        }
 */
                        if (m_scene.GetRootAgentCount() == 0)
                        {
                            //If the region is empty, this change has been made by an automated process
                            //and thus we delay the persist time by a random amount between 1.5 and 2.5.

//                            float factor = 1.5f + (float)(m_rand.NextDouble());
                            float factor = 2.0f;
                            m_maxPersistTime = (long)((float)m_scene.m_persistAfter * factor);
                            m_minPersistTime = (long)((float)m_scene.m_dontPersistBefore * factor);
                        }
                        else
                        {
                            //If the region is not empty, we want to obey the minimum and maximum persist times
                            //but add a random factor so we stagger the object persistance a little
//                            m_maxPersistTime = (long)((float)m_scene.m_persistAfter * (1.0d - (m_rand.NextDouble() / 5.0d))); //Multiply by 1.0-1.5
//                            m_minPersistTime = (long)((float)m_scene.m_dontPersistBefore * (1.0d + (m_rand.NextDouble() / 2.0d))); //Multiply by 0.8-1.0
                            m_maxPersistTime = m_scene.m_persistAfter;
                            m_minPersistTime = m_scene.m_dontPersistBefore;
                        }
                    }
                }
                m_hasGroupChanged = value;

//                m_log.DebugFormat(
//                    "[SCENE OBJECT GROUP]: HasGroupChanged set to {0} for {1} {2}", m_hasGroupChanged, Name, LocalId);
            }

            get { return m_hasGroupChanged; }
        }

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

            if (currentTime - timeLastChanged > m_minPersistTime || currentTime - timeFirstChanged > m_maxPersistTime)
                return true;
            return false;
        }

        /// <summary>
        /// Is this scene object acting as an attachment?
        /// </summary>
        public bool IsAttachment { get; set; }

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
            get { return (RootPart.Flags & PrimFlags.TemporaryOnRez) != 0; }
        }

        public bool IsVolumeDetect
        {
            get { return RootPart.VolumeDetectActive; }
        }

        /// <summary>
        /// Is this entity set to be saved in persistent storage?
        /// </summary>
        public bool Backup { get; private set; }

        protected MapAndArray<UUID, SceneObjectPart> m_parts = new MapAndArray<UUID, SceneObjectPart>();

        protected ulong m_regionHandle;
        protected SceneObjectPart m_rootPart;
        // private Dictionary<UUID, scriptEvents> m_scriptEvents = new Dictionary<UUID, scriptEvents>();

        private SortedDictionary<uint, scriptPosTarget> m_targets = new SortedDictionary<uint, scriptPosTarget>();
        private SortedDictionary<uint, scriptRotTarget> m_rotTargets = new SortedDictionary<uint, scriptRotTarget>();

        public SortedDictionary<uint, scriptPosTarget> AtTargets
        {
            get { return m_targets; }
        }

        public SortedDictionary<uint, scriptRotTarget> RotTargets
        {
            get { return m_rotTargets; }
        }

        private bool m_scriptListens_atTarget;
        private bool m_scriptListens_notAtTarget;
        private bool m_scriptListens_atRotTarget;
        private bool m_scriptListens_notAtRotTarget;

        public bool m_dupeInProgress = false;
        internal Dictionary<UUID, string> m_savedScriptState;

        #region Properties

        /// <summary>
        /// The name of an object grouping is always the same as its root part
        /// </summary>
        public override string Name
        {
            get { return RootPart.Name; }
            set { RootPart.Name = value; }
        }

        public string Description
        {
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
            get { return m_parts.Count; }
        }

//        protected Quaternion m_rotation = Quaternion.Identity;
//
//        public virtual Quaternion Rotation
//        {
//            get { return m_rotation; }
//            set {
//                m_rotation = value;
//            }
//        }

        public Quaternion GroupRotation
        {
            get { return m_rootPart.RotationOffset; }
        }

        public Vector3 GroupScale
        {
            get
            {
                Vector3 minScale = new Vector3(Constants.MaximumRegionSize, Constants.MaximumRegionSize, Constants.MaximumRegionSize);
                Vector3 maxScale = Vector3.Zero;
                Vector3 finalScale = new Vector3(0.5f, 0.5f, 0.5f);

                SceneObjectPart[] parts = m_parts.GetArray();
                for (int i = 0; i < parts.Length; i++)
                {
                    SceneObjectPart part = parts[i];
                    Vector3 partscale = part.Scale;
                    Vector3 partoffset = part.OffsetPosition;

                    minScale.X = (partscale.X + partoffset.X < minScale.X) ? partscale.X + partoffset.X : minScale.X;
                    minScale.Y = (partscale.Y + partoffset.Y < minScale.Y) ? partscale.Y + partoffset.Y : minScale.Y;
                    minScale.Z = (partscale.Z + partoffset.Z < minScale.Z) ? partscale.Z + partoffset.Z : minScale.Z;

                    maxScale.X = (partscale.X + partoffset.X > maxScale.X) ? partscale.X + partoffset.X : maxScale.X;
                    maxScale.Y = (partscale.Y + partoffset.Y > maxScale.Y) ? partscale.Y + partoffset.Y : maxScale.Y;
                    maxScale.Z = (partscale.Z + partoffset.Z > maxScale.Z) ? partscale.Z + partoffset.Z : maxScale.Z;
                }

                finalScale.X = (minScale.X > maxScale.X) ? minScale.X : maxScale.X;
                finalScale.Y = (minScale.Y > maxScale.Y) ? minScale.Y : maxScale.Y;
                finalScale.Z = (minScale.Z > maxScale.Z) ? minScale.Z : maxScale.Z;

                return finalScale;
            }
        }

        public UUID GroupID
        {
            get { return m_rootPart.GroupID; }
            set { m_rootPart.GroupID = value; }
        }

        public SceneObjectPart[] Parts
        {
            get { return m_parts.GetArray(); }
        }

        public bool ContainsPart(UUID partID)
        {
            return m_parts.ContainsKey(partID);
        }

        /// <summary>
        /// Does this group contain the given part?
        /// should be able to remove these methods once we have a entity index in scene
        /// </summary>
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
            get { return m_rootPart; }
        }

        public ulong RegionHandle
        {
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


        public bool inTransit = false;
        public delegate SceneObjectGroup SOGCrossDelegate(SceneObjectGroup sog,Vector3 pos);

        /// <summary>
        /// The absolute position of this scene object in the scene
        /// </summary>
        public override Vector3 AbsolutePosition
        {
            get { return m_rootPart.GroupPosition; }
            set
            {
                Vector3 val = value;
                if (Scene != null
                        && !Scene.PositionIsInCurrentRegion(val)
                        && !IsAttachmentCheckFull()
                        && !Scene.LoadingPrims
                        && !Scene.DisableObjectTransfer
                    )
                {
                    if (!inTransit)
                    {
                        inTransit = true;
                        SOGCrossDelegate d = CrossAsync;
                        d.BeginInvoke(this, val, CrossAsyncCompleted, d);
                    }
                    return;
                }

                if (RootPart.GetStatusSandbox())
                {
                    if (Util.GetDistanceTo(RootPart.StatusSandboxPos, value) > 10)
                    {
                        RootPart.ScriptSetPhysicsStatus(false);

                        if (Scene != null)
                            Scene.SimChat(Utils.StringToBytes("Hit Sandbox Limit"),
                                  ChatTypeEnum.DebugChannel, 0x7FFFFFFF, RootPart.AbsolutePosition, Name, UUID, false);

                        return;
                    }
                }

                bool triggerScriptEvent = m_rootPart.GroupPosition != val;
                if (m_dupeInProgress || IsDeleted)
                    triggerScriptEvent = false;

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
                if (triggerScriptEvent)
                {
                    foreach (SceneObjectPart part in parts)
                    {
                        part.TriggerScriptChangedEvent(Changed.POSITION);
                    }
                }

                if (Scene != null)
                    Scene.EventManager.TriggerParcelPrimCountTainted();

            }
        }

        public SceneObjectGroup CrossAsync(SceneObjectGroup sog, Vector3 val)
        {
            Scene sogScene = sog.m_scene;
            IEntityTransferModule entityTransfer = sogScene.RequestModuleInterface<IEntityTransferModule>();

            Vector3 newpos = Vector3.Zero;
            OpenSim.Services.Interfaces.GridRegion destination = null;

            if (sog.RootPart.DIE_AT_EDGE)
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

            if (sog.RootPart.RETURN_AT_EDGE)
            {
                // We remove the object here
                try
                {
                    List<uint> localIDs = new List<uint>();
                    localIDs.Add(sog.RootPart.LocalId);
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

            if (sog.m_rootPart.KeyframeMotion != null)
                sog.m_rootPart.KeyframeMotion.StartCrossingCheck();

            if (entityTransfer == null)
                return sog;

            destination = entityTransfer.GetObjectDestination(sog, val, out newpos);
            if (destination == null)
                return sog;

            if (sog.m_sittingAvatars.Count == 0)
            {
                entityTransfer.CrossPrimGroupIntoNewRegion(destination, newpos, sog, true, true);
                return sog;
            }

            string reason = String.Empty;
            EntityTransferContext ctx = new EntityTransferContext();

            foreach (ScenePresence av in sog.m_sittingAvatars)
            {
                // We need to cross these agents. First, let's find
                // out if any of them can't cross for some reason.
                // We have to deny the crossing entirely if any
                // of them are banned. Alternatively, we could
                // unsit banned agents....

                // We set the avatar position as being the object
                // position to get the region to send to
                if(!entityTransfer.checkAgentAccessToRegion(av, destination, newpos, ctx, out reason))
                {
                    return sog;
                }
                m_log.DebugFormat("[SCENE OBJECT]: Avatar {0} needs to be crossed to {1}", av.Name, destination.RegionName);
            }

            // We unparent the SP quietly so that it won't
            // be made to stand up

            List<avtocrossInfo> avsToCross = new List<avtocrossInfo>();

            foreach (ScenePresence av in sog.m_sittingAvatars)
            {
                byte cflags = 1;

                avtocrossInfo avinfo = new avtocrossInfo();
                SceneObjectPart parentPart = sogScene.GetSceneObjectPart(av.ParentID);
                if (parentPart != null)
                {
                    av.ParentUUID = parentPart.UUID;
                    if(parentPart.SitTargetAvatar == av.UUID)
                        cflags = 7; // low 3 bits set
                    else
                        cflags = 3;
                }

                // 1 is crossing
                // 2 is sitting
                // 4 is sitting at sittarget
                av.crossingFlags = cflags;

                avinfo.av = av;
                avinfo.ParentID = av.ParentID;
                avsToCross.Add(avinfo);

                av.PrevSitOffset = av.OffsetPosition;
                av.ParentID = 0;
            }

            if (entityTransfer.CrossPrimGroupIntoNewRegion(destination, newpos, sog, true, false))
            {
                foreach (avtocrossInfo avinfo in avsToCross)
                {
                    ScenePresence av = avinfo.av;
                    if (!av.IsInTransit) // just in case...
                    {
                        m_log.DebugFormat("[SCENE OBJECT]: Crossing avatar {0} to {1}", av.Name, val);

                        av.IsInTransit = true;

//                        CrossAgentToNewRegionDelegate d = entityTransfer.CrossAgentToNewRegionAsync;
//                        d.BeginInvoke(av, val, destination, av.Flying, version, CrossAgentToNewRegionCompleted, d);
                        entityTransfer.CrossAgentToNewRegionAsync(av, newpos, destination, av.Flying, ctx);
                        if (av.IsChildAgent)
                        {
                            // avatar crossed do some extra cleanup
                            if (av.ParentUUID != UUID.Zero)
                            {
                                av.ClearControls();
                                av.ParentPart = null;
                            }
                        }
                        else
                        {
                            // avatar cross failed we need do dedicated standUp
                            // part of it was done at CrossAgentToNewRegionAsync
                            // so for now just remove the sog controls
                            // this may need extra care
                            av.UnRegisterSeatControls(sog.UUID);
                        }

                        av.ParentUUID = UUID.Zero;
                        // In any case
                        av.IsInTransit = false;
                        av.crossingFlags = 0;
                        m_log.DebugFormat("[SCENE OBJECT]: Crossing agent {0} {1} completed.", av.Firstname, av.Lastname);
                    }
                    else
                        m_log.DebugFormat("[SCENE OBJECT]: Crossing avatar already in transit {0} to {1}", av.Name, val);
                }
                avsToCross.Clear();
                sog.RemoveScriptInstances(true);
                sog.Clear();
                return sog;
            }
            else // cross failed, put avas back ??
            {
                foreach (avtocrossInfo avinfo in avsToCross)
                {
                    ScenePresence av = avinfo.av;
                    av.ParentUUID = UUID.Zero;
                    av.ParentID = avinfo.ParentID;
                    av.crossingFlags = 0;
                }
            }
            avsToCross.Clear();

            return sog;
        }

        public void CrossAsyncCompleted(IAsyncResult iar)
        {
            SOGCrossDelegate icon = (SOGCrossDelegate)iar.AsyncState;
            SceneObjectGroup sog = icon.EndInvoke(iar);

            if (!sog.IsDeleted)
            {
                SceneObjectPart rootp = sog.m_rootPart;
                Vector3 oldp = rootp.GroupPosition;
                oldp.X = Util.Clamp<float>(oldp.X, 0.5f, sog.m_scene.RegionInfo.RegionSizeX - 0.5f);
                oldp.Y = Util.Clamp<float>(oldp.Y, 0.5f, sog.m_scene.RegionInfo.RegionSizeY - 0.5f);
                rootp.GroupPosition = oldp;

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

                sog.Velocity = Vector3.Zero;

                if (sog.m_rootPart.KeyframeMotion != null)
                    sog.m_rootPart.KeyframeMotion.CrossingFailure();

                if (sog.RootPart.PhysActor != null)
                {
                    sog.RootPart.PhysActor.CrossingFailure();
                }

                sog.inTransit = false;
                sog.ScheduleGroupForFullUpdate();
            }
        }

/* outdated
        private void CrossAgentToNewRegionCompleted(ScenePresence agent)
        {
            //// If the cross was successful, this agent is a child agent
            if (agent.IsChildAgent)
            {
                if (agent.ParentUUID != UUID.Zero)
                {
                    agent.HandleForceReleaseControls(agent.ControllingClient,agent.UUID);
                    agent.ParentPart = null;
//                    agent.ParentPosition = Vector3.Zero;
//                    agent.ParentUUID = UUID.Zero;
                }
            }

            agent.ParentUUID = UUID.Zero;
//                agent.Reset();
//            else // Not successful
//                agent.RestoreInCurrentScene();

            // In any case
            agent.IsInTransit = false;

            m_log.DebugFormat("[SCENE OBJECT]: Crossing agent {0} {1} completed.", agent.Firstname, agent.Lastname);
        }
*/
        public override Vector3 Velocity
        {
            get { return RootPart.Velocity; }
            set { RootPart.Velocity = value; }
        }

        public override uint LocalId
        {
            get { return m_rootPart.LocalId; }
            set { m_rootPart.LocalId = value; }
        }

        public override UUID UUID
        {
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
            get { return m_rootPart.LastOwnerID; }
            set { m_rootPart.LastOwnerID = value; }
        }

        public UUID RezzerID
        {
            get { return m_rootPart.RezzerID; }
            set { m_rootPart.RezzerID = value; }
        }

        public UUID OwnerID
        {
            get { return m_rootPart.OwnerID; }
            set { m_rootPart.OwnerID = value; }
        }

        public float Damage
        {
            get { return m_rootPart.Damage; }
            set { m_rootPart.Damage = value; }
        }

        public Color Color
        {
            get { return m_rootPart.Color; }
            set { m_rootPart.Color = value; }
        }

        public string Text
        {
            get {
                string returnstr = m_rootPart.Text;
                if (returnstr.Length  > 255)
                {
                    returnstr = returnstr.Substring(0, 255);
                }
                return returnstr;
            }
            set { m_rootPart.Text = value; }
        }

        /// <summary>
        /// If set to true then the scene object can be backed up in principle, though this will only actually occur
        /// if Backup is set.  If false then the scene object will never be backed up, Backup will always be false.
        /// </summary>
        protected virtual bool CanBeBackedUp
        {
            get { return true; }
        }

        public bool IsSelected
        {
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
                if (pa != null)
                {
                    pa.Selected = value;

                    // Pass it on to the children.
                    SceneObjectPart[] parts = m_parts.GetArray();
                    for (int i = 0; i < parts.Length; i++)
                    {
                        SceneObjectPart child = parts[i];

                        PhysicsActor childPa = child.PhysActor;
                        if (childPa != null)
                            childPa.Selected = value;
                    }
                }
                if (RootPart.KeyframeMotion != null)
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
//                if (!IsAttachment)
//                    ScheduleGroupForFullUpdate();
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
//                if (!IsAttachment)
//                {
//                    ScheduleGroupForFullUpdate();
//                }
            }
        }
        // PlaySoundMasterPrim no longer in use  to remove
        private SceneObjectPart m_PlaySoundMasterPrim = null;
        public SceneObjectPart PlaySoundMasterPrim
        {
            get { return m_PlaySoundMasterPrim; }
            set { m_PlaySoundMasterPrim = value; }
        }
        // PlaySoundSlavePrims no longer in use  to remove
        private List<SceneObjectPart> m_PlaySoundSlavePrims = new List<SceneObjectPart>();
        public List<SceneObjectPart> PlaySoundSlavePrims
        {
            get { return m_PlaySoundSlavePrims; }
            set { m_PlaySoundSlavePrims = value; }
        }

        //  LoopSoundMasterPrim no longer in use  to remove
        private SceneObjectPart m_LoopSoundMasterPrim = null;
        public SceneObjectPart LoopSoundMasterPrim
        {
            get { return m_LoopSoundMasterPrim; }
            set { m_LoopSoundMasterPrim = value; }
        }

        //  m_LoopSoundSlavePrims no longer in use  to remove
        private List<SceneObjectPart> m_LoopSoundSlavePrims = new List<SceneObjectPart>();
        public List<SceneObjectPart> LoopSoundSlavePrims
        {
            get { return m_LoopSoundSlavePrims; }
            set { m_LoopSoundSlavePrims = value; }
        }

        /// <summary>
        /// The UUID for the region this object is in.
        /// </summary>
        public UUID RegionUUID
        {
            get
            {
                if (m_scene != null)
                {
                    return m_scene.RegionInfo.RegionID;
                }
                return UUID.Zero;
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
        protected internal List<ScenePresence> m_sittingAvatars = new List<ScenePresence>();

        #endregion

//        ~SceneObjectGroup()
//        {
//            //m_log.DebugFormat("[SCENE OBJECT GROUP]: Destructor called for {0}, local id {1}", Name, LocalId);
//            Console.WriteLine("Destructor called for {0}, local id {1}", Name, LocalId);
//        }

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        public SceneObjectGroup()
        {

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

        public void LoadScriptState(XmlDocument doc)
        {
            XmlNodeList nodes = doc.GetElementsByTagName("SavedScriptState");
            if (nodes.Count > 0)
            {
                if (m_savedScriptState == null)
                    m_savedScriptState = new Dictionary<UUID, string>();
                foreach (XmlNode node in nodes)
                {
                    if (node.Attributes["UUID"] != null)
                    {
                        UUID itemid = new UUID(node.Attributes["UUID"].Value);
                        if (itemid != UUID.Zero)
                            m_savedScriptState[itemid] = node.InnerXml;
                    }
                }
            }
        }

        public void LoadScriptState(XmlReader reader)
        {
//            m_log.DebugFormat("[SCENE OBJECT GROUP]: Looking for script state for {0}", Name);

            while (true)
            {
                if (reader.Name == "SavedScriptState" && reader.NodeType == XmlNodeType.Element)
                {
//                    m_log.DebugFormat("[SCENE OBJECT GROUP]: Loading script state for {0}", Name);

                    if (m_savedScriptState == null)
                        m_savedScriptState = new Dictionary<UUID, string>();

                    string uuid = reader.GetAttribute("UUID");

                    // Even if there is no UUID attribute for some strange reason, we must always read the inner XML
                    // so we don't continually keep checking the same SavedScriptedState element.
                    string innerXml = reader.ReadInnerXml();

                    if (uuid != null)
                    {
//                        m_log.DebugFormat("[SCENE OBJECT GROUP]: Found state for item ID {0} in object {1}", uuid, Name);

                        UUID itemid = new UUID(uuid);
                        if (itemid != UUID.Zero)
                            m_savedScriptState[itemid] = innerXml;
                    }
                    else
                    {
                        m_log.WarnFormat("[SCENE OBJECT GROUP]: SavedScriptState element had no UUID in object {0} id: {1}", Name,UUID);
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
            if (IsAttachment) return;
            m_scene.SceneGraph.FireAttachToBackup(this);

//           if (InSceneBackup)
            {
//                m_log.DebugFormat(
//                    "[SCENE OBJECT GROUP]: Attaching object {0} {1} to scene presistence sweep", Name, UUID);

                if (!Backup)
                    m_scene.EventManager.OnBackup += ProcessBackup;

                Backup = true;
            }
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
                if (part.KeyframeMotion != null)
                {
                    part.KeyframeMotion.UpdateSceneObject(this);
                }

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

            EntityIntersection result = new EntityIntersection();

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

        /// <summary>
        /// Gets a vector representing the size of the bounding box containing all the prims in the group
        /// Treats all prims as rectangular, so no shape (cut etc) is taken into account
        /// offsetHeight is the offset in the Z axis from the centre of the bounding box to the centre of the root prim
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

            SceneObjectPart[] parts = m_parts.GetArray();
            foreach (SceneObjectPart part in parts)
            {
                Vector3 worldPos = part.GetWorldPosition();
                Vector3 offset = worldPos - AbsolutePosition;
                Quaternion worldRot;
                if (part.ParentID == 0)
                {
                    worldRot = part.RotationOffset;
                }
                else
                {
                    worldRot = part.GetWorldRotation();
                }

                Vector3 frontTopLeft;
                Vector3 frontTopRight;
                Vector3 frontBottomLeft;
                Vector3 frontBottomRight;

                Vector3 backTopLeft;
                Vector3 backTopRight;
                Vector3 backBottomLeft;
                Vector3 backBottomRight;

               // Vector3[] corners = new Vector3[8];

                Vector3 orig = Vector3.Zero;

                frontTopLeft.X = orig.X - (part.Scale.X / 2);
                frontTopLeft.Y = orig.Y - (part.Scale.Y / 2);
                frontTopLeft.Z = orig.Z + (part.Scale.Z / 2);

                frontTopRight.X = orig.X - (part.Scale.X / 2);
                frontTopRight.Y = orig.Y + (part.Scale.Y / 2);
                frontTopRight.Z = orig.Z + (part.Scale.Z / 2);

                frontBottomLeft.X = orig.X - (part.Scale.X / 2);
                frontBottomLeft.Y = orig.Y - (part.Scale.Y / 2);
                frontBottomLeft.Z = orig.Z - (part.Scale.Z / 2);

                frontBottomRight.X = orig.X - (part.Scale.X / 2);
                frontBottomRight.Y = orig.Y + (part.Scale.Y / 2);
                frontBottomRight.Z = orig.Z - (part.Scale.Z / 2);

                backTopLeft.X = orig.X + (part.Scale.X / 2);
                backTopLeft.Y = orig.Y - (part.Scale.Y / 2);
                backTopLeft.Z = orig.Z + (part.Scale.Z / 2);

                backTopRight.X = orig.X + (part.Scale.X / 2);
                backTopRight.Y = orig.Y + (part.Scale.Y / 2);
                backTopRight.Z = orig.Z + (part.Scale.Z / 2);

                backBottomLeft.X = orig.X + (part.Scale.X / 2);
                backBottomLeft.Y = orig.Y - (part.Scale.Y / 2);
                backBottomLeft.Z = orig.Z - (part.Scale.Z / 2);

                backBottomRight.X = orig.X + (part.Scale.X / 2);
                backBottomRight.Y = orig.Y + (part.Scale.Y / 2);
                backBottomRight.Z = orig.Z - (part.Scale.Z / 2);



                //m_log.InfoFormat("pre corner 1 is {0} {1} {2}", frontTopLeft.X, frontTopLeft.Y, frontTopLeft.Z);
                //m_log.InfoFormat("pre corner 2 is {0} {1} {2}", frontTopRight.X, frontTopRight.Y, frontTopRight.Z);
                //m_log.InfoFormat("pre corner 3 is {0} {1} {2}", frontBottomRight.X, frontBottomRight.Y, frontBottomRight.Z);
                //m_log.InfoFormat("pre corner 4 is {0} {1} {2}", frontBottomLeft.X, frontBottomLeft.Y, frontBottomLeft.Z);
                //m_log.InfoFormat("pre corner 5 is {0} {1} {2}", backTopLeft.X, backTopLeft.Y, backTopLeft.Z);
                //m_log.InfoFormat("pre corner 6 is {0} {1} {2}", backTopRight.X, backTopRight.Y, backTopRight.Z);
                //m_log.InfoFormat("pre corner 7 is {0} {1} {2}", backBottomRight.X, backBottomRight.Y, backBottomRight.Z);
                //m_log.InfoFormat("pre corner 8 is {0} {1} {2}", backBottomLeft.X, backBottomLeft.Y, backBottomLeft.Z);

                //for (int i = 0; i < 8; i++)
                //{
                //    corners[i] = corners[i] * worldRot;
                //    corners[i] += offset;

                //    if (corners[i].X > maxX)
                //        maxX = corners[i].X;
                //    if (corners[i].X < minX)
                //        minX = corners[i].X;

                //    if (corners[i].Y > maxY)
                //        maxY = corners[i].Y;
                //    if (corners[i].Y < minY)
                //        minY = corners[i].Y;

                //    if (corners[i].Z > maxZ)
                //        maxZ = corners[i].Y;
                //    if (corners[i].Z < minZ)
                //        minZ = corners[i].Z;
                //}

                frontTopLeft = frontTopLeft * worldRot;
                frontTopRight = frontTopRight * worldRot;
                frontBottomLeft = frontBottomLeft * worldRot;
                frontBottomRight = frontBottomRight * worldRot;

                backBottomLeft = backBottomLeft * worldRot;
                backBottomRight = backBottomRight * worldRot;
                backTopLeft = backTopLeft * worldRot;
                backTopRight = backTopRight * worldRot;


                frontTopLeft += offset;
                frontTopRight += offset;
                frontBottomLeft += offset;
                frontBottomRight += offset;

                backBottomLeft += offset;
                backBottomRight += offset;
                backTopLeft += offset;
                backTopRight += offset;

                //m_log.InfoFormat("corner 1 is {0} {1} {2}", frontTopLeft.X, frontTopLeft.Y, frontTopLeft.Z);
                //m_log.InfoFormat("corner 2 is {0} {1} {2}", frontTopRight.X, frontTopRight.Y, frontTopRight.Z);
                //m_log.InfoFormat("corner 3 is {0} {1} {2}", frontBottomRight.X, frontBottomRight.Y, frontBottomRight.Z);
                //m_log.InfoFormat("corner 4 is {0} {1} {2}", frontBottomLeft.X, frontBottomLeft.Y, frontBottomLeft.Z);
                //m_log.InfoFormat("corner 5 is {0} {1} {2}", backTopLeft.X, backTopLeft.Y, backTopLeft.Z);
                //m_log.InfoFormat("corner 6 is {0} {1} {2}", backTopRight.X, backTopRight.Y, backTopRight.Z);
                //m_log.InfoFormat("corner 7 is {0} {1} {2}", backBottomRight.X, backBottomRight.Y, backBottomRight.Z);
                //m_log.InfoFormat("corner 8 is {0} {1} {2}", backBottomLeft.X, backBottomLeft.Y, backBottomLeft.Z);

                if (frontTopRight.X > maxX)
                    maxX = frontTopRight.X;
                if (frontTopLeft.X > maxX)
                    maxX = frontTopLeft.X;
                if (frontBottomRight.X > maxX)
                    maxX = frontBottomRight.X;
                if (frontBottomLeft.X > maxX)
                    maxX = frontBottomLeft.X;

                if (backTopRight.X > maxX)
                    maxX = backTopRight.X;
                if (backTopLeft.X > maxX)
                    maxX = backTopLeft.X;
                if (backBottomRight.X > maxX)
                    maxX = backBottomRight.X;
                if (backBottomLeft.X > maxX)
                    maxX = backBottomLeft.X;

                if (frontTopRight.X < minX)
                    minX = frontTopRight.X;
                if (frontTopLeft.X < minX)
                    minX = frontTopLeft.X;
                if (frontBottomRight.X < minX)
                    minX = frontBottomRight.X;
                if (frontBottomLeft.X < minX)
                    minX = frontBottomLeft.X;

                if (backTopRight.X < minX)
                    minX = backTopRight.X;
                if (backTopLeft.X < minX)
                    minX = backTopLeft.X;
                if (backBottomRight.X < minX)
                    minX = backBottomRight.X;
                if (backBottomLeft.X < minX)
                    minX = backBottomLeft.X;

                //
                if (frontTopRight.Y > maxY)
                    maxY = frontTopRight.Y;
                if (frontTopLeft.Y > maxY)
                    maxY = frontTopLeft.Y;
                if (frontBottomRight.Y > maxY)
                    maxY = frontBottomRight.Y;
                if (frontBottomLeft.Y > maxY)
                    maxY = frontBottomLeft.Y;

                if (backTopRight.Y > maxY)
                    maxY = backTopRight.Y;
                if (backTopLeft.Y > maxY)
                    maxY = backTopLeft.Y;
                if (backBottomRight.Y > maxY)
                    maxY = backBottomRight.Y;
                if (backBottomLeft.Y > maxY)
                    maxY = backBottomLeft.Y;

                if (frontTopRight.Y < minY)
                    minY = frontTopRight.Y;
                if (frontTopLeft.Y < minY)
                    minY = frontTopLeft.Y;
                if (frontBottomRight.Y < minY)
                    minY = frontBottomRight.Y;
                if (frontBottomLeft.Y < minY)
                    minY = frontBottomLeft.Y;

                if (backTopRight.Y < minY)
                    minY = backTopRight.Y;
                if (backTopLeft.Y < minY)
                    minY = backTopLeft.Y;
                if (backBottomRight.Y < minY)
                    minY = backBottomRight.Y;
                if (backBottomLeft.Y < minY)
                    minY = backBottomLeft.Y;

                //
                if (frontTopRight.Z > maxZ)
                    maxZ = frontTopRight.Z;
                if (frontTopLeft.Z > maxZ)
                    maxZ = frontTopLeft.Z;
                if (frontBottomRight.Z > maxZ)
                    maxZ = frontBottomRight.Z;
                if (frontBottomLeft.Z > maxZ)
                    maxZ = frontBottomLeft.Z;

                if (backTopRight.Z > maxZ)
                    maxZ = backTopRight.Z;
                if (backTopLeft.Z > maxZ)
                    maxZ = backTopLeft.Z;
                if (backBottomRight.Z > maxZ)
                    maxZ = backBottomRight.Z;
                if (backBottomLeft.Z > maxZ)
                    maxZ = backBottomLeft.Z;

                if (frontTopRight.Z < minZ)
                    minZ = frontTopRight.Z;
                if (frontTopLeft.Z < minZ)
                    minZ = frontTopLeft.Z;
                if (frontBottomRight.Z < minZ)
                    minZ = frontBottomRight.Z;
                if (frontBottomLeft.Z < minZ)
                    minZ = frontBottomLeft.Z;

                if (backTopRight.Z < minZ)
                    minZ = backTopRight.Z;
                if (backTopLeft.Z < minZ)
                    minZ = backTopLeft.Z;
                if (backBottomRight.Z < minZ)
                    minZ = backBottomRight.Z;
                if (backBottomLeft.Z < minZ)
                    minZ = backBottomLeft.Z;
            }
        }

        public Vector3 GetAxisAlignedBoundingBox(out float offsetHeight)
        {
            float minX;
            float maxX;
            float minY;
            float maxY;
            float minZ;
            float maxZ;

            GetAxisAlignedBoundingBoxRaw(out minX, out maxX, out minY, out maxY, out minZ, out maxZ);
            Vector3 boundingBox = new Vector3(maxX - minX, maxY - minY, maxZ - minZ);

            offsetHeight = 0;
            float lower = (minZ * -1);
            if (lower > maxZ)
            {
                offsetHeight = lower - (boundingBox.Z / 2);

            }
            else if (maxZ > lower)
            {
                offsetHeight = maxZ - (boundingBox.Z / 2);
                offsetHeight *= -1;
            }

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
        public Vector3 getBoundsCenter()
        {
            // math is done in GetBoundsRadius();
            if(m_boundsRadius == null)
                GetBoundsRadius();
            return m_boundsCenter;
        }

        private float m_areaFactor;
        public float getAreaFactor()
        {
            // math is done in GetBoundsRadius();
            if(m_boundsRadius == null)
                GetBoundsRadius();
            return m_areaFactor;
        }

        public float GetBoundsRadius()
        {
        // this may need more threading work
            if(m_boundsRadius == null)
            {
                float res = 0;
                float areaF = 0;
                SceneObjectPart p;
                SceneObjectPart[] parts;
                float partR;
                Vector3 offset = Vector3.Zero;
                lock (m_parts)
                {
                    parts = m_parts.GetArray();
                }

                int nparts = parts.Length;
                for (int i = 0; i < nparts; i++)
                {
                    p = parts[i];
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
                    offset = offset * RootPart.RotationOffset;
                }

                areaF = 10.0f / areaF;  // scale it
                areaF = Util.Clamp(areaF, 0.001f, 1000f); // clamp it

                m_areaFactor = (float)Math.Sqrt(areaF);
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

            SceneObjectPart p;
            SceneObjectPart[] parts;

            lock (m_parts)
            {
                parts = m_parts.GetArray();
            }

            int nparts = parts.Length;


            for (int i = 0; i < nparts; i++)
            {
                p = parts[i];

                if (p.UsesComplexCost)
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

                for (int i = 0; i < nparts; i++)
                {
                    p = parts[i];

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
                linksetResCost = (float)nparts;
                linksetPhysCost = linksetResCost;
            }
        }

        public void GetSelectedCosts(out float PhysCost, out float StreamCost, out float SimulCost)
        {
            SceneObjectPart p;
            SceneObjectPart[] parts;

            lock (m_parts)
            {
                parts = m_parts.GetArray();
            }

            int nparts = parts.Length;

            PhysCost = 0;
            StreamCost = 0;
            SimulCost = 0;

            for (int i = 0; i < nparts; i++)
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
            XmlDocument doc = new XmlDocument();
            Dictionary<UUID,string> states = new Dictionary<UUID,string>();

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

        public byte GetAttachmentPoint()
        {
            return m_rootPart.Shape.State;
        }

        public void DetachToGround()
        {
            ScenePresence avatar = m_scene.GetScenePresence(AttachedAvatar);
            if (avatar == null)
                return;
            m_rootPart.Shape.LastAttachPoint = m_rootPart.Shape.State;
            m_rootPart.AttachedPos = m_rootPart.OffsetPosition;
            avatar.RemoveAttachment(this);

            Vector3 detachedpos = new Vector3(127f,127f,127f);
            if (avatar == null)
                return;

            detachedpos = avatar.AbsolutePosition;
            FromItemID = UUID.Zero;

            AbsolutePosition = detachedpos;
            AttachedAvatar = UUID.Zero;

            //SceneObjectPart[] parts = m_parts.GetArray();
            //for (int i = 0; i < parts.Length; i++)
            //    parts[i].AttachedAvatar = UUID.Zero;

            m_rootPart.SetParentLocalId(0);
            AttachmentPoint = (byte)0;
            // must check if buildind should be true or false here
//            m_rootPart.ApplyPhysics(m_rootPart.GetEffectiveObjectFlags(), m_rootPart.VolumeDetectActive,false);
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
            if (avatar != null)
            {
                //detachedpos = avatar.AbsolutePosition;
                avatar.RemoveAttachment(this);
            }

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

        public ushort GetTimeDilation()
        {
            return Utils.FloatToUInt16(m_scene.TimeDilation, 0.0f, 1.0f);
        }

        /// <summary>
        /// Set a part to act as the root part for this scene object
        /// </summary>
        /// <param name="part"></param>
        public void SetRootPart(SceneObjectPart part)
        {
            if (part == null)
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
                if (part.UUID != m_rootPart.UUID)
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
            if (m_scene != null)
                return m_scene.MaxUndoCount;
            return 5;
        }

        // justincc: I don't believe this hack is needed any longer, especially since the physics
        // parts of set AbsolutePosition were already commented out.  By changing HasGroupChanged to false
        // this method was preventing proper reload of scene objects.

        // dahlia: I had to uncomment it, without it meshing was failing on some prims and objects
        // at region startup

        // teravus: After this was removed from the linking algorithm, Linked prims no longer collided
        // properly when non-physical if they havn't been moved.   This breaks ALL builds.
        // see: http://opensimulator.org/mantis/view.php?id=3108

        // Here's the deal, this is ABSOLUTELY CRITICAL so the physics scene gets the update about the
        // position of linkset prims.  IF YOU CHANGE THIS, YOU MUST TEST colliding with just linked and
        // unmoved prims!  As soon as you move a Prim/group, it will collide properly because Absolute
        // Position has been set!

        public void ResetChildPrimPhysicsPositions()
        {
            // Setting this SOG's absolute position also loops through and sets the positions
            //    of the SOP's in this SOG's linkset. This has the side affect of making sure
            //    the physics world matches the simulated world.
            // AbsolutePosition = AbsolutePosition; // could someone in the know please explain how this works?

            // teravus: AbsolutePosition is NOT a normal property!
            // the code in the getter of AbsolutePosition is significantly different then the code in the setter!
            // jhurliman: Then why is it a property instead of two methods?

            // do only what is supposed to do
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
            if (part != null)
            {
                return part.UUID;
            }
            return UUID.Zero;
        }

        public void ObjectGrabHandler(uint localId, Vector3 offsetPos, IClientAPI remoteClient)
        {
            if (m_rootPart.LocalId == localId)
            {
                OnGrabGroup(offsetPos, remoteClient);
            }
            else
            {
                SceneObjectPart part = GetPart(localId);
                OnGrabPart(part, offsetPos, remoteClient);
            }
        }

        public virtual void OnGrabPart(SceneObjectPart part, Vector3 offsetPos, IClientAPI remoteClient)
        {
//            m_log.DebugFormat(
//                "[SCENE OBJECT GROUP]: Processing OnGrabPart for {0} on {1} {2}, offsetPos {3}",
//                remoteClient.Name, part.Name, part.LocalId, offsetPos);

//            part.StoreUndoState();
            part.OnGrab(offsetPos, remoteClient);
        }

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

            if(Scene == null)  // should not happen unless restart/shutdown ?
                return;

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];

                Scene.ForEachScenePresence(delegate(ScenePresence avatar)
                {
                    if (!avatar.IsChildAgent && avatar.ParentID == part.LocalId && avatar.ParentUUID == UUID.Zero)
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

        public void AddScriptLPS(int count)
        {
            m_scene.SceneGraph.AddToScriptLPS(count);
        }

        public void AddActiveScriptCount(int count)
        {
            SceneGraph d = m_scene.SceneGraph;
            d.AddActiveScripts(count);
        }

        private const scriptEvents PhysicsNeeedSubsEvents = (
            scriptEvents.collision | scriptEvents.collision_start | scriptEvents.collision_end |
            scriptEvents.land_collision | scriptEvents.land_collision_start | scriptEvents.land_collision_end);

        private scriptEvents lastRootPartPhysEvents = 0;

        public void aggregateScriptEvents()
        {
            PrimFlags objectflagupdate = (PrimFlags)RootPart.GetEffectiveObjectFlags();

            scriptEvents aggregateScriptEvents = 0;

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                if (part == null)
                    continue;
                if (part != RootPart)
                    part.Flags = objectflagupdate;
                aggregateScriptEvents |= part.AggregateScriptEvents;
            }

            m_scriptListens_atTarget = ((aggregateScriptEvents & scriptEvents.at_target) != 0);
            m_scriptListens_notAtTarget = ((aggregateScriptEvents & scriptEvents.not_at_target) != 0);

            if (!m_scriptListens_atTarget && !m_scriptListens_notAtTarget)
            {
                lock (m_targets)
                    m_targets.Clear();
                m_scene.RemoveGroupTarget(this);
            }
            m_scriptListens_atRotTarget = ((aggregateScriptEvents & scriptEvents.at_rot_target) != 0);
            m_scriptListens_notAtRotTarget = ((aggregateScriptEvents & scriptEvents.not_at_rot_target) != 0);

            if (!m_scriptListens_atRotTarget && !m_scriptListens_notAtRotTarget)
            {
                lock (m_rotTargets)
                    m_rotTargets.Clear();
                m_scene.RemoveGroupTarget(this);
            }

            scriptEvents rootPartPhysEvents = RootPart.AggregateScriptEvents;
            rootPartPhysEvents &= PhysicsNeeedSubsEvents;
            if (rootPartPhysEvents != lastRootPartPhysEvents)
            {
                lastRootPartPhysEvents = rootPartPhysEvents;
                for (int i = 0; i < parts.Length; i++)
                {
                    SceneObjectPart part = parts[i];
                    if (part == null)
                        continue;
                    part.UpdatePhysicsSubscribedEvents();
                }
            }

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
                // Hack to get the physics scene geometries in the right spot
//                ResetChildPrimPhysicsPositions();

                if (m_rootPart.PhysActor != null)
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
                    if (part.OwnerID != userId)
                    {
                        if(part.GroupID != part.OwnerID)
                            part.LastOwnerID = part.OwnerID;
                        part.OwnerID = userId;
                    }
                });
        }

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
//                m_log.DebugFormat(
//                    "[WATER WARS]: Ignoring backup of {0} {1} since object is not marked to be backed up", Name, UUID);
                return;
            }

            if (IsDeleted || inTransit || UUID == UUID.Zero)
            {
//                m_log.DebugFormat(
//                    "[WATER WARS]: Ignoring backup of {0} {1} since object is marked as already deleted", Name, UUID);
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

                    if (parcel != null && parcel.LandData != null &&
                            parcel.LandData.OtherCleanTime != 0)
                    {
                        if (parcel.LandData.OwnerID != OwnerID &&
                                (parcel.LandData.GroupID != GroupID ||
                                parcel.LandData.GroupID == UUID.Zero))
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
                                m_log.DebugFormat(
                                    "[SCENE OBJECT GROUP]: Returning object {0} due to parcel autoreturn",
                                     RootPart.UUID);
                                m_scene.AddReturn(OwnerID == GroupID ? LastOwnerID : OwnerID, Name, AbsolutePosition, "parcel autoreturn");
                                m_scene.DeRezObjects(null, new List<uint>() { RootPart.LocalId }, UUID.Zero,
                                        DeRezAction.Return, UUID.Zero, false);

                                return;
                            }
                        }
                    }

                }

                if (m_scene.UseBackup && HasGroupChanged)
                {
                    // don't backup while it's selected or you're asking for changes mid stream.
                    if (isTimeToPersist() || forcedBackup)
                    {
                        if (m_rootPart.PhysActor != null &&
                            (!m_rootPart.PhysActor.IsPhysical))
                        {
                            // Possible ghost prim
                            if (m_rootPart.PhysActor.Position != m_rootPart.GroupPosition)
                            {
                                foreach (SceneObjectPart part in m_parts.GetArray())
                                {
                                    // Re-set physics actor positions and
                                    // orientations
                                    part.GroupPosition = m_rootPart.GroupPosition;
                                }
                            }
                        }
//                        m_log.DebugFormat(
//                            "[SCENE]: Storing {0}, {1} in {2}",
//                            Name, UUID, m_scene.RegionInfo.RegionName);

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
                        backup_group.RootPart.ParticleSystem = RootPart.ParticleSystem;
                        HasGroupChanged = false;
                        GroupContainsForeignPrims = false;

                        m_scene.EventManager.TriggerOnSceneObjectPreSave(backup_group, this);

                        datastore.StoreObject(backup_group, m_scene.RegionInfo.RegionID);

                        backup_group.ForEachPart(delegate(SceneObjectPart part)
                        {
                            part.Inventory.ProcessInventoryBackup(datastore);

                            if(part.KeyframeMotion != null)
                            {
                                part.KeyframeMotion.Delete();
                                part.KeyframeMotion = null;
                            }
                        });

                        backup_group.Clear();
                        backup_group = null;
                    }
//                    else
//                    {
//                        m_log.DebugFormat(
//                            "[SCENE]: Did not update persistence of object {0} {1}, selected = {2}",
//                            Name, UUID, IsSelected);
//                    }
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[SCENE]: Storing of {0}, {1} in {2} failed with exception {3}{4}",
                    Name, UUID, m_scene.RegionInfo.RegionName, e.Message, e.StackTrace);
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
        public void SendFullUpdateToClient(IClientAPI remoteClient)
        {
            RootPart.SendFullUpdate(remoteClient);

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                if (part != RootPart)
                    part.SendFullUpdate(remoteClient);
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

            dupe.m_parts = new MapAndArray<OpenMetaverse.UUID, SceneObjectPart>();

            // a copy isnt backedup
            dupe.Backup = false;
            dupe.InvalidBoundsRadius();

            // a copy is not in transit hopefully
            dupe.inTransit = false;

            // new group as no sitting avatars
            dupe.m_sittingAvatars = new List<ScenePresence>();

            dupe.CopyRootPart(m_rootPart, OwnerID, GroupID, userExposed);
            dupe.m_rootPart.LinkNum = m_rootPart.LinkNum;

            if (userExposed)
                dupe.m_rootPart.TrimPermissions();

            List<SceneObjectPart> partList = new List<SceneObjectPart>(m_parts.GetArray());

            partList.Sort(delegate(SceneObjectPart p1, SceneObjectPart p2)
                    {
                        return p1.LinkNum.CompareTo(p2.LinkNum);
                    }
                );

            foreach (SceneObjectPart part in partList)
            {
                SceneObjectPart newPart;
                if (part.UUID != m_rootPart.UUID)
                {
                    newPart = dupe.CopyPart(part, OwnerID, GroupID, userExposed);
                    newPart.LinkNum = part.LinkNum;
//                    if (userExposed)
                        newPart.ParentID = dupe.m_rootPart.LocalId;
                 }
                else
                {
                    newPart = dupe.m_rootPart;
                }
/*
                bool isphys = ((newPart.Flags & PrimFlags.Physics) != 0);
                bool isphan = ((newPart.Flags & PrimFlags.Phantom) != 0);

                // Need to duplicate the physics actor as well
                if (userExposed && (isphys || !isphan || newPart.VolumeDetectActive))
                {
                    PrimitiveBaseShape pbs = newPart.Shape;
                    newPart.PhysActor
                        = m_scene.PhysicsScene.AddPrimShape(
                            string.Format("{0}/{1}", newPart.Name, newPart.UUID),
                            pbs,
                            newPart.AbsolutePosition,
                            newPart.Scale,
                            newPart.GetWorldRotation(),
                            isphys,
                            isphan,
                            newPart.LocalId);

                    newPart.DoPhysicsPropertyUpdate(isphys, true);
 */
                if (userExposed)
                    newPart.ApplyPhysics((uint)newPart.Flags,newPart.VolumeDetectActive,true);
//                }
                // copy keyframemotion
                if (part.KeyframeMotion != null)
                    newPart.KeyframeMotion = part.KeyframeMotion.Copy(dupe);
            }

            if (userExposed)
            {
// done above                dupe.UpdateParentIDs();

                if (dupe.m_rootPart.PhysActor != null)
                    dupe.m_rootPart.PhysActor.Building = false; // tell physics to finish building

                dupe.AggregateDeepPerms();

                dupe.HasGroupChanged = true;
                dupe.AttachToBackup();

                dupe.ScheduleGroupForFullUpdate();
            }

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
//            SceneObjectPart newpart = part.Copy(part.LocalId, OwnerID, GroupID, 0, userExposed);
//            newpart.LocalId = m_scene.AllocateLocalId();

            SetRootPart(newpart);
            if (userExposed)
                RootPart.Velocity = Vector3.Zero; // In case source is moving
        }

        public void ScriptSetPhysicsStatus(bool usePhysics)
        {
            if (usePhysics)
            {
                if (RootPart.KeyframeMotion != null)
                    RootPart.KeyframeMotion.Stop();
                RootPart.KeyframeMotion = null;
            }
            UpdatePrimFlags(RootPart.LocalId, usePhysics, IsTemporary, IsPhantom, IsVolumeDetect);
        }

        public void ScriptSetTemporaryStatus(bool makeTemporary)
        {
            UpdatePrimFlags(RootPart.LocalId, UsesPhysics, makeTemporary, IsPhantom, IsVolumeDetect);
        }

        public void ScriptSetPhantomStatus(bool makePhantom)
        {
            UpdatePrimFlags(RootPart.LocalId, UsesPhysics, IsTemporary, makePhantom, IsVolumeDetect);
        }

        public void ScriptSetVolumeDetect(bool makeVolumeDetect)
        {
            UpdatePrimFlags(RootPart.LocalId, UsesPhysics, IsTemporary, IsPhantom, makeVolumeDetect);

            /*
            ScriptSetPhantomStatus(false);  // What ever it was before, now it's not phantom anymore

            if (PhysActor != null) // Should always be the case now
            {
                PhysActor.SetVolumeDetect(param);
            }
            if (param != 0)
                AddFlag(PrimFlags.Phantom);

            ScheduleFullUpdate();
            */
        }

        public void applyImpulse(Vector3 impulse)
        {
            if (IsAttachment)
            {
                ScenePresence avatar = m_scene.GetScenePresence(AttachedAvatar);
                if (avatar != null)
                {
                    avatar.PushForce(impulse);
                }
            }
            else
            {
                PhysicsActor pa = RootPart.PhysActor;

                if (pa != null)
                {
                    // false to be applied as a impulse
                    pa.AddForce(impulse, false);
                    m_scene.PhysicsScene.AddPhysicsActorTaint(pa);
                }
            }
        }

        public void ApplyAngularImpulse(Vector3 impulse)
        {
            PhysicsActor pa = RootPart.PhysActor;

            if (pa != null)
            {
                if (!IsAttachment)
                {
                    // false to be applied as a impulse
                    pa.AddAngularForce(impulse, false);
                    m_scene.PhysicsScene.AddPhysicsActorTaint(pa);
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
            if (IsAttachment)
            {
                ScenePresence avatar = m_scene.GetScenePresence(AttachedAvatar);

                if (avatar != null)
                    avatar.MoveToTarget(target, false, false);
            }
            else
            {
                PhysicsActor pa = RootPart.PhysActor;

                if (pa != null)
                {
                    pa.PIDTarget = target;
                    pa.PIDTau = tau;
                    pa.PIDActive = true;
                }
            }
        }

        public void StopMoveToTarget()
        {
            if (IsAttachment)
            {
                ScenePresence avatar = m_scene.GetScenePresence(AttachedAvatar);

                if (avatar != null)
                {
                    avatar.ResetMoveToTarget();
                }
            }
            else
            {
                PhysicsActor pa = RootPart.PhysActor;

                if (pa != null)
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
            if (rootpart != null)
            {
/* physics still doesnt suport this
                if (rootpart.PhysActor != null)
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

            if (m_rootPart != null)
                m_rootPart.RotLookAt(target, strength, damping);
        }

        public void StopLookAt()
        {
            SceneObjectPart rootpart = m_rootPart;
            if (rootpart != null)
            {
                if (rootpart.PhysActor != null)
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
                    if (avatar != null)
                        pa = avatar.PhysicsActor;
                }
            else
                pa = RootPart.PhysActor;

            if (pa != null)
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
                if(part.GroupID != part.OwnerID)
                    part.LastOwnerID = part.OwnerID;
                part.OwnerID = cAgentID;
                part.GroupID = cGroupID;
                });

            if (oldowner != cAgentID)
            {
                // Apply Next Owner Permissions if we're not bypassing permissions
                if (!m_scene.Permissions.BypassPermissions())
                {
                    ApplyNextOwnerPermissions();
                    AggregatePerms();
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
//            SceneObjectPart newPart = part.Copy(part.LocalId, OwnerID, GroupID, m_parts.Count, userExposed);
//            newPart.LocalId = m_scene.AllocateLocalId();

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
                List<SceneObjectPart> partsList = new List<SceneObjectPart>(m_parts.GetArray());
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

//             remoteClient.SendObjectPropertiesFamilyData(RequestFlags, RootPart.UUID, RootPart.OwnerID, RootPart.GroupID, RootPart.BaseMask,
//                                                         RootPart.OwnerMask, RootPart.GroupMask, RootPart.EveryoneMask, RootPart.NextOwnerMask,
//                                                         RootPart.OwnershipCost, RootPart.ObjectSaleType, RootPart.SalePrice, RootPart.Category,
//                                                         RootPart.CreatorID, RootPart.Name, RootPart.Description);
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

            // Even temporary objects take part in physics (e.g. temp-on-rez bullets)
            //if ((RootPart.Flags & PrimFlags.TemporaryOnRez) != 0)
            //    return;

            // If we somehow got here to updating the SOG and its root part is not scheduled for update,
            // check to see if the physical position or rotation warrant an update.
            if (m_rootPart.UpdateFlag == UpdateRequired.NONE)
            {
                // rootpart SendScheduledUpdates will check if a update is needed
                m_rootPart.UpdateFlag = UpdateRequired.TERSE;
            }

            if (IsAttachment)
            {
                ScenePresence sp = m_scene.GetScenePresence(AttachedAvatar);
                if (sp != null)
                {
                    sp.SendAttachmentScheduleUpdate(this);
                    return;
                }
            }

            // while physics doesn't suports LookAt, we do it in RootPart
            if (!IsSelected)
                RootPart.UpdateLookAt();

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                part.SendScheduledUpdates();
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
//            if (IsAttachment)
//                m_log.DebugFormat("[SOG]: Scheduling full update for {0} {1}", Name, LocalId);

            checkAtTargets();
            RootPart.ScheduleFullUpdate();

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                if (part != RootPart)
                    part.ScheduleFullUpdate();
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
//            m_log.DebugFormat("[SOG]: Scheduling terse update for {0} {1}", Name, UUID);

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                parts[i].ScheduleTerseUpdate();
        }

        /// <summary>
        /// Immediately send a full update for this scene object.
        /// </summary>
        public void SendGroupFullUpdate()
        {
            if (IsDeleted)
                return;

//            m_log.DebugFormat("[SOG]: Sending immediate full group update for {0} {1}", Name, UUID);

            if (IsAttachment)
            {
                ScenePresence sp = m_scene.GetScenePresence(AttachedAvatar);
                if (sp != null)
                {
                    sp.SendAttachmentUpdate(this,UpdateRequired.FULL);
                    return;
                }
            }

            RootPart.SendFullUpdateToAllClientsInternal();

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                if (part != RootPart)
                    part.SendFullUpdateToAllClientsInternal();
            }
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

        public void QueueForUpdateCheck()
        {
            if (m_scene == null) // Need to check here as it's null during object creation
                return;

            m_scene.SceneGraph.AddToUpdateList(this);
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
                if (sp != null)
                {
                    sp.SendAttachmentUpdate(this, UpdateRequired.TERSE);
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
            SceneObjectPart[] parts = m_parts.GetArray();
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
        public SceneObjectPart GetPart(UUID primID)
        {
            SceneObjectPart childPart;
            m_parts.TryGetValue(primID, out childPart);
            return childPart;
        }

        /// <summary>
        /// Get a part with a given local ID
        /// </summary>
        /// <param name="localID"></param>
        /// <returns>null if a part with the local ID was not found</returns>
        public SceneObjectPart GetPart(uint localID)
        {
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].LocalId == localID)
                    return parts[i];
            }

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
        public void LinkToGroup(SceneObjectGroup objectGroup)
        {
            LinkToGroup(objectGroup, false);
        }

        // Link an existing group to this group.
        // The group being linked need not be a linkset -- it can have just one prim.
        public void LinkToGroup(SceneObjectGroup objectGroup, bool insert)
        {
//            m_log.DebugFormat(
//                "[SCENE OBJECT GROUP]: Linking group with root part {0}, {1} to group with root part {2}, {3}",
//                objectGroup.RootPart.Name, objectGroup.RootPart.UUID, RootPart.Name, RootPart.UUID);

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


            // 'linkPart' == the root of the group being linked into this group
            SceneObjectPart linkPart = objectGroup.m_rootPart;

            if (m_rootPart.PhysActor != null)
                m_rootPart.PhysActor.Building = true;
            if (linkPart.PhysActor != null)
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
                m_scene.updateScenePartGroup(linkPart, this);

                linkPart.CreateSelected = true;

                // let physics know preserve part volume dtc messy since UpdatePrimFlags doesn't look to parent changes for now
                linkPart.UpdatePrimFlags(grpusephys, grptemporary, (IsPhantom || (linkPart.Flags & PrimFlags.Phantom) != 0), linkPart.VolumeDetectActive, true);

                // If the added SOP is physical, also tell the physics engine about the link relationship.
                if (linkPart.PhysActor != null && m_rootPart.PhysActor != null && m_rootPart.PhysActor.IsPhysical)
                {
                    linkPart.PhysActor.link(m_rootPart.PhysActor);
                    this.Scene.PhysicsScene.AddPhysicsActorTaint(linkPart.PhysActor);
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
                        if (part.PhysActor != null && m_rootPart.PhysActor != null && m_rootPart.PhysActor.IsPhysical)
                        {
                            part.PhysActor.link(m_rootPart.PhysActor);
                            this.Scene.PhysicsScene.AddPhysicsActorTaint(part.PhysActor);
                        }
                    }
                    part.ClearUndoState();
                }
            }

            // Now that we've aquired all of the old SOG's parts, remove the old SOG from the scene.
            m_scene.UnlinkSceneObject(objectGroup, true);
            objectGroup.IsDeleted = true;

            objectGroup.m_parts.Clear();

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

            if (m_rootPart.PhysActor != null)
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

            if (linkPart != null)
            {
                return DelinkFromGroup(linkPart, sendEvents);
            }
            else
            {
                m_log.WarnFormat("[SCENE OBJECT GROUP]: " +
                                 "DelinkFromGroup(): Child prim {0} not found in object {1}, {2}",
                                 partID, LocalId, UUID);

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
//                m_log.DebugFormat(
//                    "[SCENE OBJECT GROUP]: Delinking part {0}, {1} from group with root part {2}, {3}",
//                    linkPart.Name, linkPart.UUID, RootPart.Name, RootPart.UUID);

            if (m_rootPart.PhysActor != null)
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
                if (parts.Length == 1 && RootPart != null)
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
            if (linkPartPa != null)
            {
                m_scene.PhysicsScene.RemovePrim(linkPartPa);
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
            SceneObjectGroup objectGroup = new SceneObjectGroup(linkPart);

            m_scene.AddNewSceneObject(objectGroup, true);

            linkPart.Rezzed = RootPart.Rezzed;

            // When we delete a group, we currently have to force persist to the database if the object id has changed
            // (since delete works by deleting all rows which have a given object id)

            // this is as it seems to be in sl now
            if(linkPart.PhysicsShapeType == (byte)PhysShapeType.none)
                linkPart.PhysicsShapeType = linkPart.DefaultPhysicsShapeType(); // root prims can't have type none for now

            if (m_rootPart.PhysActor != null)
                m_rootPart.PhysActor.Building = false;

            objectGroup.HasGroupChangedDueToDelink = true;

            InvalidBoundsRadius();
            objectGroup.AggregatePerms();

            if (sendEvents)
                linkPart.TriggerScriptChangedEvent(Changed.LINK);

            return objectGroup;
        }

/* working on it
        public void DelinkFromGroup(List<SceneObjectPart> linkParts, bool sendEvents)
        {
//                m_log.DebugFormat(
//                    "[SCENE OBJECT GROUP]: Delinking part {0}, {1} from group with root part {2}, {3}",
//                    linkPart.Name, linkPart.UUID, RootPart.Name, RootPart.UUID);

            if(PrimCount == 1)
                return;

            if (m_rootPart.PhysActor != null)
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
                if (linkPartPa != null)
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

            if (m_rootPart.PhysActor != null)
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
            if (m_scene != null)
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

            m_scene.updateScenePartGroup(part, this);

            // Compute the new position of this SOP relative to the group position
            part.setOffsetPosition(newPos - AbsolutePosition);

            // (radams1 20120711: I don't know why part.OffsetPosition is set multiple times.
            //   It would have the affect of setting the physics engine position multiple
            //   times. In theory, that is not necessary but I don't have a good linkset
            //   test to know that cleaning up this code wouldn't break things.)

            // Compute the SOP's rotation relative to the rotation of the group.
            parentRot = m_rootPart.RotationOffset;

            oldRot = part.RotationOffset;
            Quaternion newRot = Quaternion.Conjugate(parentRot) * worldRot;
            part.setRotationOffset(newRot);

            Vector3 pos = part.OffsetPosition;
            pos *= Quaternion.Conjugate(parentRot);

            part.OffsetPosition = pos; // update position and orientation on physics also

            // Since this SOP's state has changed, push those changes into the physics engine
            //    and the simulator.
            // done on caller
//            part.UpdatePrimFlags(UsesPhysics, IsTemporary, IsPhantom, IsVolumeDetect, false);
        }

        /// <summary>
        /// If object is physical, apply force to move it around
        /// If object is not physical, just put it at the resulting location
        /// </summary>
        /// <param name="partID">Part ID to check for grab</param>
        /// <param name="offset">Always seems to be 0,0,0, so ignoring</param>
        /// <param name="pos">New position.  We do the math here to turn it into a force</param>
        /// <param name="remoteClient"></param>
        public void GrabMovement(UUID partID, Vector3 offset, Vector3 pos, IClientAPI remoteClient)
        {
            if (m_scene.EventManager.TriggerGroupMove(UUID, pos))
            {
                if (BlockGrabOverride)
                    return;

                SceneObjectPart part = GetPart(partID);

                if (part == null)
                    return;

                if (part.BlockGrab)
                    return;

                PhysicsActor pa = m_rootPart.PhysActor;

                if (pa != null && pa.IsPhysical)
                {
                    // empirically convert distance diference to a impulse
                    Vector3 grabforce = pos - AbsolutePosition;
                    grabforce = grabforce * (pa.Mass * 0.1f);
                    pa.AddForce(grabforce, false);
                    m_scene.PhysicsScene.AddPhysicsActorTaint(pa);
                }
                else
                {
                    NonPhysicalGrabMovement(pos);
                }
            }
        }

        /// <summary>
        /// Apply possition for grabbing non-physical linksets (Ctrl+Drag)
        /// This MUST be blocked for linksets that contain touch scripts because the viewer triggers grab on the touch
        /// event (Viewer Bug?) This would allow anyone to drag a linkset with a touch script. SL behaviour is also to
        /// block grab on prims with touch events.
        /// </summary>
        /// <param name="pos">New Position</param>
        public void NonPhysicalGrabMovement(Vector3 pos)
        {
            if(!IsAttachment && ScriptCount() == 0)
                UpdateGroupPosition(pos);
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

                if (pa != null)
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

                if (pa != null && pa.IsPhysical)
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

                        float rotationAngle;
                        Vector3 spinforce;
                        minimalRotationFromQ1ToQ2.GetAxisAngle(out spinforce, out rotationAngle);
                        if(Math.Abs(rotationAngle)< 0.001)
                            return;

                        spinforce.Normalize();

                        //m_log.Error("SCENE OBJECT GROUP]: rotation axis is " + rotationAxis);
                        if(rotationAngle > 0)
                            spinforce = spinforce * pa.Mass * 0.1f; // 0.1 is an arbitrary torque scaling factor
                        else
                           spinforce = spinforce * pa.Mass * -0.1f; // 0.1 is an arbitrary torque scaling
                        pa.AddAngularForce(spinforce,true);
                        m_scene.PhysicsScene.AddPhysicsActorTaint(pa);
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
            if (part != null)
            {
                part.Name = name;
            }
        }

        public void SetPartDescription(string des, uint localID)
        {
            SceneObjectPart part = GetPart(localID);
            if (part != null)
            {
                part.Description = des;
            }
        }

        public void SetPartText(string text, uint localID)
        {
            SceneObjectPart part = GetPart(localID);
            if (part != null)
            {
                part.SetText(text);
            }
        }

        public void SetPartText(string text, UUID partID)
        {
            SceneObjectPart part = GetPart(partID);
            if (part != null)
            {
                part.SetText(text);
            }
        }

        public string GetPartName(uint localID)
        {
            SceneObjectPart part = GetPart(localID);
            if (part != null)
            {
                return part.Name;
            }
            return String.Empty;
        }

        public string GetPartDescription(uint localID)
        {
            SceneObjectPart part = GetPart(localID);
            if (part != null)
            {
                return part.Description;
            }
            return String.Empty;
        }

        /// <summary>
        /// Update prim flags for this group.
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="UsePhysics"></param>
        /// <param name="SetTemporary"></param>
        /// <param name="SetPhantom"></param>
        /// <param name="SetVolumeDetect"></param>
        public void UpdatePrimFlags(uint localID, bool UsePhysics, bool SetTemporary, bool SetPhantom, bool SetVolumeDetect)
        {
            HasGroupChanged = true;

            SceneObjectPart selectionPart = GetPart(localID);

            if (Scene != null)
            {
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

                m_scene.EventManager.TriggerParcelPrimCountTainted();
            }

            if (selectionPart != null)
            {
                SceneObjectPart[] parts = m_parts.GetArray();

                if (Scene != null && UsePhysics)
                {
                    int maxprims = m_scene.m_linksetPhysCapacity;
                    bool checkShape = (maxprims > 0 &&
                             parts.Length > maxprims);

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

                    if (m_rootPart.PhysActor != null)
                        m_rootPart.PhysActor.Building = false;
                }
                else
                    m_rootPart.UpdatePrimFlags(UsePhysics, SetTemporary, SetPhantom, SetVolumeDetect, false);
            }
        }

        public void UpdateExtraParam(uint localID, ushort type, bool inUse, byte[] data)
        {
            SceneObjectPart part = GetPart(localID);
            if (part != null)
            {
                part.UpdateExtraParam(type, inUse, data);
            }
        }



        /// <summary>
        /// Gets the number of parts
        /// </summary>
        /// <returns></returns>
        public int GetPartCount()
        {
            return Parts.Count();
        }

        /// <summary>
        /// Update the texture entry for this part
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="textureEntry"></param>
        public void UpdateTextureEntry(uint localID, byte[] textureEntry)
        {
            SceneObjectPart part = GetPart(localID);
            if (part != null)
            {
                part.UpdateTextureEntry(textureEntry);
            }
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

//            m_log.DebugFormat(
//                "[SCENE OBJECT GROUP]: RootPart.OwnerMask now {0} for {1} in {2}",
//                (OpenMetaverse.PermissionMask)RootPart.OwnerMask, Name, Scene.Name);
            AggregatePerms();
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
                AggregatePerms();
            }

            HasGroupChanged = true;

            // Send the group's properties to all clients once all parts are updated
            IClientAPI client;
            if (Scene.TryGetClient(AgentID, out client))
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
            if (part != null)
            {
                part.UpdateShape(shapeBlock);

                PhysicsActor pa = m_rootPart.PhysActor;

                if (pa != null)
                    m_scene.PhysicsScene.AddPhysicsActorTaint(pa);
            }
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
//            m_log.DebugFormat(
//                "[SCENE OBJECT GROUP]: Group resizing {0} {1} from {2} to {3}", Name, LocalId, RootPart.Scale, scale);

            if (Scene == null)
                return;

            PhysicsActor pa = m_rootPart.PhysActor;

            float minsize = Scene.m_minNonphys;
            float maxsize = Scene.m_maxNonphys;

            if (pa != null && pa.IsPhysical)
            {
                minsize = Scene.m_minPhys;
                maxsize = Scene.m_maxPhys;
            }

            scale.X = Util.Clamp(scale.X, minsize, maxsize);
            scale.Y = Util.Clamp(scale.Y, minsize, maxsize);
            scale.Z = Util.Clamp(scale.Z, minsize, maxsize);

            // requested scaling factors
            float x = (scale.X / RootPart.Scale.X);
            float y = (scale.Y / RootPart.Scale.Y);
            float z = (scale.Z / RootPart.Scale.Z);

            SceneObjectPart[] parts = m_parts.GetArray();

            // fix scaling factors so parts don't violate dimensions
            for(int i = 0;i < parts.Length;i++)
            {
                SceneObjectPart obPart = parts[i];
                if(obPart.UUID != m_rootPart.UUID)
                {
                    Vector3 oldSize = new Vector3(obPart.Scale);

                    float f = 1.0f;
                    float a = 1.0f;

                    if(oldSize.X * x > maxsize)
                    {
                        f = maxsize / oldSize.X;
                        a = f / x;
                        x *= a;
                        y *= a;
                        z *= a;
                    }
                    else if(oldSize.X * x < minsize)
                    {
                        f = minsize / oldSize.X;
                        a = f / x;
                        x *= a;
                        y *= a;
                        z *= a;
                    }

                    if(oldSize.Y * y > maxsize)
                    {
                        f = maxsize / oldSize.Y;
                        a = f / y;
                        x *= a;
                        y *= a;
                        z *= a;
                    }
                    else if(oldSize.Y * y < minsize)
                    {
                        f = minsize / oldSize.Y;
                        a = f / y;
                        x *= a;
                        y *= a;
                        z *= a;
                    }

                    if(oldSize.Z * z > maxsize)
                    {
                        f = maxsize / oldSize.Z;
                        a = f / z;
                        x *= a;
                        y *= a;
                        z *= a;
                    }
                    else if(oldSize.Z * z < minsize)
                    {
                        f = minsize / oldSize.Z;
                        a = f / z;
                        x *= a;
                        y *= a;
                        z *= a;
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

                if (obPart.UUID != m_rootPart.UUID)
                {
                    Vector3 currentpos = new Vector3(obPart.OffsetPosition);
                    currentpos.X *= x;
                    currentpos.Y *= y;
                    currentpos.Z *= z;

                    Vector3 newSize = new Vector3(obPart.Scale);
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
//            m_log.DebugFormat(
//                "[SCENE OBJECT GROUP]: Group resizing {0} {1} from {2} to {3}", Name, LocalId, RootPart.Scale, fscale);

            if (Scene == null || IsDeleted || inTransit || fscale < 0)
                return false;

            // ignore lsl restrictions. let them be done a LSL
            PhysicsActor pa = m_rootPart.PhysActor;

            if(RootPart.KeyframeMotion != null)
                RootPart.KeyframeMotion.Suspend();

            float minsize = Scene.m_minNonphys;
            float maxsize = Scene.m_maxNonphys;

            // assuming physics is more restrictive
            if (pa != null && pa.IsPhysical)
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
                Vector3 oldSize = new Vector3(obPart.Scale);
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

            Vector3 newSize = RootPart.Scale;
            newSize.X = (float)(newSize.X * fscale);
            newSize.Y = (float)(newSize.Y * fscale);
            newSize.Z = (float)(newSize.Z * fscale);

            if(pa != null)
                pa.Building = true;

            RootPart.Scale = newSize;

            Vector3 currentpos;
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart obPart = parts[i];

                if (obPart.UUID != m_rootPart.UUID)
                {
                    currentpos = obPart.OffsetPosition;
                    currentpos.X = (float)(currentpos.X * fscale);
                    currentpos.Y = (float)(currentpos.Y * fscale);
                    currentpos.Z = (float)(currentpos.Z * fscale);

                    newSize = obPart.Scale;
                    newSize.X = (float)(newSize.X * fscale);
                    newSize.Y = (float)(newSize.Y * fscale);
                    newSize.Z = (float)(newSize.Z * fscale);

                    obPart.Scale = newSize;
                    obPart.UpdateOffSet(currentpos);
                }
            }

            if(pa != null)
                pa.Building = false;

            InvalidBoundsRadius();

            HasGroupChanged = true;
            m_rootPart.TriggerScriptChangedEvent(Changed.SCALE);
            ScheduleGroupForFullUpdate();

            if(RootPart.KeyframeMotion != null)
                RootPart.KeyframeMotion.Resume();

            return true;
        }

        public float GetMaxGroupResizeScale()
        {
            if (Scene == null || IsDeleted || inTransit)
                return 1.0f;

            float maxsize = Scene.m_maxNonphys;
            PhysicsActor pa = m_rootPart.PhysActor;
            // assuming physics is more restrictive
            if (pa != null && pa.IsPhysical)
                maxsize = Scene.m_maxPhys;

            SceneObjectPart[] parts = m_parts.GetArray();
            float larger = float.MinValue;

            for(int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart obPart = parts[i];
                Vector3 oldSize = new Vector3(obPart.Scale);
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
            if (Scene == null || IsDeleted || inTransit)
                return 1.0f;

            float minsize = Scene.m_minNonphys;
            PhysicsActor pa = m_rootPart.PhysActor;
            // assuming physics is more restrictive
            if (pa != null && pa.IsPhysical)
                minsize = Scene.m_minPhys;

            SceneObjectPart[] parts = m_parts.GetArray();
            float smaller = float.MaxValue;

            for(int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart obPart = parts[i];
                Vector3 oldSize = new Vector3(obPart.Scale);
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

                if (RootPart.GetStatusSandbox())
                {
                    if (Util.GetDistanceTo(RootPart.StatusSandboxPos, pos) > 10)
                    {
                        RootPart.ScriptSetPhysicsStatus(false);
                        pos = AbsolutePosition;
                        Scene.SimChat(Utils.StringToBytes("Hit Sandbox Limit"),
                              ChatTypeEnum.DebugChannel, 0x7FFFFFFF, RootPart.AbsolutePosition, Name, UUID, false);
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

            if (part != null)
            {
// unlock parts position change
                if (m_rootPart.PhysActor != null)
                    m_rootPart.PhysActor.Building = true;

                if (part.UUID == m_rootPart.UUID)
                {
                    UpdateRootPosition(pos);
                }
                else
                {
                    part.UpdateOffSet(pos);
                }

                if (m_rootPart.PhysActor != null)
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
                if (obPart.UUID != m_rootPart.UUID)
                    obPart.OffsetPosition = obPart.OffsetPosition + diff;
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
            if (actor != null)
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

            PhysicsActor actor = m_rootPart.PhysActor;
            if (actor != null)
            {
                actor.Orientation = m_rootPart.RotationOffset;
                m_scene.PhysicsScene.AddPhysicsActorTaint(actor);
            }

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

            SceneObjectPart[] parts = m_parts.GetArray();

            if (part != null)
            {
                if (m_rootPart.PhysActor != null)
                    m_rootPart.PhysActor.Building = true;

                if (part.UUID == m_rootPart.UUID)
                {
                    UpdateRootRotation(rot);
                }
                else
                {
                    part.UpdateRotation(rot);
                }

                if (m_rootPart.PhysActor != null)
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
            if (part != null)
            {
                if (m_rootPart.PhysActor != null)
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

                if (m_rootPart.PhysActor != null)
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
            Quaternion axRot = rot;
            Quaternion oldParentRot = m_rootPart.RotationOffset;

            //Don't use UpdateRotation because it schedules an update prematurely
            m_rootPart.RotationOffset = rot;

            PhysicsActor pa = m_rootPart.PhysActor;

            if (pa != null)
            {
                pa.Orientation = m_rootPart.RotationOffset;
                m_scene.PhysicsScene.AddPhysicsActorTaint(pa);
            }

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart prim = parts[i];
                if (prim.UUID != m_rootPart.UUID)
                {
                    Quaternion NewRot = oldParentRot * prim.RotationOffset;
                    NewRot = Quaternion.Inverse(axRot) * NewRot;
                    prim.RotationOffset = NewRot;

                    Vector3 axPos = prim.OffsetPosition;

                    axPos *= oldParentRot;
                    axPos *= Quaternion.Inverse(axRot);
                    prim.OffsetPosition = axPos;
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

            if (part != null && part.ParentGroup != null)
            {
                ObjectChangeType change = data.change;
                bool togroup = ((change & ObjectChangeType.Group) != 0);
                //                bool uniform = ((what & ObjectChangeType.UniformScale) != 0);  not in use

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
                        if (pha != null)
                            pha.Building = true;

                        group.GroupResize(data.scale);
                        updateType = updatetype.none;

                        if (pha != null)
                            pha.Building = false;
                    }
                }
                else
                {
                    // related to single prim in a link-set ( ie group)
                    if (pha != null)
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

                    if (pha != null)
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

        internal void SetAxisRotation(int axis, int rotate10)
        {
            bool setX = ((axis & (int)SceneObjectGroup.axisSelect.STATUS_ROTATE_X) != 0);
            bool setY = ((axis & (int)SceneObjectGroup.axisSelect.STATUS_ROTATE_Y) != 0);
            bool setZ = ((axis & (int)SceneObjectGroup.axisSelect.STATUS_ROTATE_Z) != 0);

            if (setX || setY || setZ)
            {
                bool lockaxis = (rotate10 == 0); // zero means axis locked

                byte locks = RootPart.RotationAxisLocks;

                if (setX)
                    {
                    if(lockaxis)
                        locks |= (byte)SceneObjectGroup.axisSelect.STATUS_ROTATE_X;
                    else
                        locks &= (byte)SceneObjectGroup.axisSelect.NOT_STATUS_ROTATE_X;
                    }

                if (setY)
                    {
                    if(lockaxis)
                        locks |= (byte)SceneObjectGroup.axisSelect.STATUS_ROTATE_Y;
                    else
                        locks &= (byte)SceneObjectGroup.axisSelect.NOT_STATUS_ROTATE_Y;
                    }

                if (setZ)
                    {
                    if(lockaxis)
                        locks |= (byte)SceneObjectGroup.axisSelect.STATUS_ROTATE_Z;
                    else
                        locks &= (byte)SceneObjectGroup.axisSelect.NOT_STATUS_ROTATE_Z;
                    }

                RootPart.RotationAxisLocks = locks;
                RootPart.SetPhysicsAxisRotation();
                }
        }

        public int GetAxisRotation(int axis)
        {
            byte  rotAxislocks = RootPart.RotationAxisLocks;

            // if multiple return the one with higher id
            if (axis == (int)SceneObjectGroup.axisSelect.STATUS_ROTATE_Z)
                return (rotAxislocks & (byte)SceneObjectGroup.axisSelect.STATUS_ROTATE_Z) == 0 ? 1:0;
            if (axis == (int)SceneObjectGroup.axisSelect.STATUS_ROTATE_Y)
                return (rotAxislocks & (byte)SceneObjectGroup.axisSelect.STATUS_ROTATE_Y) == 0 ? 1:0;
            if (axis == (int)SceneObjectGroup.axisSelect.STATUS_ROTATE_X)
                return (rotAxislocks & (byte)SceneObjectGroup.axisSelect.STATUS_ROTATE_X) == 0 ? 1:0;

            return 0;
        }

        public int registerRotTargetWaypoint(Quaternion target, float tolerance)
        {
            scriptRotTarget waypoint = new scriptRotTarget();
            waypoint.targetRot = target;
            waypoint.tolerance = tolerance;
            uint handle = m_scene.AllocateLocalId();
            waypoint.handle = handle;
            lock (m_rotTargets)
            {
                if (m_rotTargets.Count >= 8)
                    m_rotTargets.Remove(m_rotTargets.ElementAt(0).Key);
                m_rotTargets.Add(handle, waypoint);
            }
            m_scene.AddGroupTarget(this);
            return (int)handle;
        }

        public void unregisterRotTargetWaypoint(int handle)
        {
            lock (m_targets)
            {
                m_rotTargets.Remove((uint)handle);
                if (m_targets.Count == 0)
                    m_scene.RemoveGroupTarget(this);
            }
        }

        public int registerTargetWaypoint(Vector3 target, float tolerance)
        {
            scriptPosTarget waypoint = new scriptPosTarget();
            waypoint.targetPos = target;
            waypoint.tolerance = tolerance;
            uint handle = m_scene.AllocateLocalId();
            waypoint.handle = handle;
            lock (m_targets)
            {
                if (m_targets.Count >= 8)
                    m_targets.Remove(m_targets.ElementAt(0).Key);
                m_targets.Add(handle, waypoint);
            }
            m_scene.AddGroupTarget(this);
            return (int)handle;
        }

        public void unregisterTargetWaypoint(int handle)
        {
            lock (m_targets)
            {
                m_targets.Remove((uint)handle);
                if (m_targets.Count == 0)
                    m_scene.RemoveGroupTarget(this);
            }
        }

        public void checkAtTargets()
        {
            if (m_scriptListens_atTarget || m_scriptListens_notAtTarget)
            {
                if (m_targets.Count > 0)
                {
                    bool at_target = false;
                    //Vector3 targetPos;
                    //uint targetHandle;
                    Dictionary<uint, scriptPosTarget> atTargets = new Dictionary<uint, scriptPosTarget>();
                    lock (m_targets)
                    {
                        foreach (uint idx in m_targets.Keys)
                        {
                            scriptPosTarget target = m_targets[idx];
                            if (Util.GetDistanceTo(target.targetPos, m_rootPart.GroupPosition) <= target.tolerance)
                            {
                                at_target = true;

                                // trigger at_target
                                if (m_scriptListens_atTarget)
                                {
                                    scriptPosTarget att = new scriptPosTarget();
                                    att.targetPos = target.targetPos;
                                    att.tolerance = target.tolerance;
                                    att.handle = target.handle;
                                    atTargets.Add(idx, att);
                                }
                            }
                        }
                    }

                    if (atTargets.Count > 0)
                    {
                        SceneObjectPart[] parts = m_parts.GetArray();
                        uint[] localids = new uint[parts.Length];
                        for (int i = 0; i < parts.Length; i++)
                            localids[i] = parts[i].LocalId;

                        for (int ctr = 0; ctr < localids.Length; ctr++)
                        {
                            foreach (uint target in atTargets.Keys)
                            {
                                scriptPosTarget att = atTargets[target];
                                m_scene.EventManager.TriggerAtTargetEvent(
                                    localids[ctr], att.handle, att.targetPos, m_rootPart.GroupPosition);
                            }
                        }

                        return;
                    }

                    if (m_scriptListens_notAtTarget && !at_target)
                    {
                        //trigger not_at_target
                        SceneObjectPart[] parts = m_parts.GetArray();
                        uint[] localids = new uint[parts.Length];
                        for (int i = 0; i < parts.Length; i++)
                            localids[i] = parts[i].LocalId;

                        for (int ctr = 0; ctr < localids.Length; ctr++)
                        {
                            m_scene.EventManager.TriggerNotAtTargetEvent(localids[ctr]);
                        }
                    }
                }
            }
            if (m_scriptListens_atRotTarget || m_scriptListens_notAtRotTarget)
            {
                if (m_rotTargets.Count > 0)
                {
                    bool at_Rottarget = false;
                    Dictionary<uint, scriptRotTarget> atRotTargets = new Dictionary<uint, scriptRotTarget>();
                    lock (m_rotTargets)
                    {
                        foreach (uint idx in m_rotTargets.Keys)
                        {
                            scriptRotTarget target = m_rotTargets[idx];
                            double angle
                                = Math.Acos(
                                    target.targetRot.X * m_rootPart.RotationOffset.X
                                        + target.targetRot.Y * m_rootPart.RotationOffset.Y
                                        + target.targetRot.Z * m_rootPart.RotationOffset.Z
                                        + target.targetRot.W * m_rootPart.RotationOffset.W)
                                    * 2;
                            if (angle < 0) angle = -angle;
                            if (angle > Math.PI) angle = (Math.PI * 2 - angle);
                            if (angle <= target.tolerance)
                            {
                                // trigger at_rot_target
                                if (m_scriptListens_atRotTarget)
                                {
                                    at_Rottarget = true;
                                    scriptRotTarget att = new scriptRotTarget();
                                    att.targetRot = target.targetRot;
                                    att.tolerance = target.tolerance;
                                    att.handle = target.handle;
                                    atRotTargets.Add(idx, att);
                                }
                            }
                        }
                    }

                    if (atRotTargets.Count > 0)
                    {
                        SceneObjectPart[] parts = m_parts.GetArray();
                        uint[] localids = new uint[parts.Length];
                        for (int i = 0; i < parts.Length; i++)
                            localids[i] = parts[i].LocalId;

                        for (int ctr = 0; ctr < localids.Length; ctr++)
                        {
                            foreach (uint target in atRotTargets.Keys)
                            {
                                scriptRotTarget att = atRotTargets[target];
                                m_scene.EventManager.TriggerAtRotTargetEvent(
                                    localids[ctr], att.handle, att.targetRot, m_rootPart.RotationOffset);
                            }
                        }

                        return;
                    }

                    if (m_scriptListens_notAtRotTarget && !at_Rottarget)
                    {
                        //trigger not_at_target
                        SceneObjectPart[] parts = m_parts.GetArray();
                        uint[] localids = new uint[parts.Length];
                        for (int i = 0; i < parts.Length; i++)
                            localids[i] = parts[i].LocalId;

                        for (int ctr = 0; ctr < localids.Length; ctr++)
                        {
                            m_scene.EventManager.TriggerNotAtRotTargetEvent(localids[ctr]);
                        }
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

            Vector3 gc = Vector3.Zero;

            SceneObjectPart[] parts = m_parts.GetArray();
            int nparts = parts.Length;
            if (nparts < 2)
                return gc;

            // average all parts positions
            for (int i = 0; i < nparts; i++)
            {
                if (parts[i] != RootPart)
                    gc += parts[i].OffsetPosition;
            }
            gc /= nparts;

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

            float time = 0.0f;

            // get all the scripts in all parts
            SceneObjectPart[] parts = m_parts.GetArray();
            List<TaskInventoryItem> scripts = new List<TaskInventoryItem>();
            for (int i = 0; i < parts.Length; i++)
            {
                scripts.AddRange(parts[i].Inventory.GetInventoryItems(InventoryType.LSL));
            }
            // extract the UUIDs
            List<UUID> ids = new List<UUID>(scripts.Count);
            foreach (TaskInventoryItem script in scripts)
            {
                if (!ids.Contains(script.ItemID))
                {
                    ids.Add(script.ItemID);
                }
            }
            // Offer the list of script UUIDs to each engine found and accumulate the time
            foreach (IScriptModule e in engines)
            {
                if (e != null)
                {
                    time += e.GetScriptExecutionTime(ids);
                }
            }
            return time;
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

        /// <summary>
        /// Gets the number of sitting avatars.
        /// </summary>
        /// <remarks>This applies to all sitting avatars whether there is a sit target set or not.</remarks>
        /// <returns></returns>
        public int GetSittingAvatarsCount()
        {
            lock (m_sittingAvatars)
                return m_sittingAvatars.Count;
        }

        public override string ToString()
        {
            return String.Format("{0} {1} ({2})", Name, UUID, AbsolutePosition);
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
            return "<ExtraFromItemID>" + FromItemID.ToString() + "</ExtraFromItemID>";
        }

        public virtual void ExtraFromXmlString(string xmlstr)
        {
            string id = xmlstr.Substring(xmlstr.IndexOf("<ExtraFromItemID>"));
            id = xmlstr.Replace("<ExtraFromItemID>", "");
            id = id.Replace("</ExtraFromItemID>", "");

            UUID uuid = UUID.Zero;
            UUID.TryParse(id, out uuid);

            FromItemID = uuid;
        }

        public void ResetOwnerChangeFlag()
        {
            ForEachPart(delegate(SceneObjectPart part)
            {
                part.ResetOwnerChangeFlag();
            });
            AggregatePerms();
        }

        // clear some references to easy cg
        public void Clear()
        {
            m_parts.Clear();
            m_sittingAvatars.Clear();
//            m_rootPart = null;

            m_PlaySoundMasterPrim = null;
            m_PlaySoundSlavePrims.Clear();
            m_LoopSoundMasterPrim = null;
            m_targets.Clear();
        }

        #endregion
    }

}
