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
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.Framework.Scenes
{
    public class UndoState
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Vector3 Position = Vector3.Zero;
        public Vector3 Scale = Vector3.Zero;
        public Quaternion Rotation = Quaternion.Identity;

        /// <summary>
        /// Is this undo state for an entire group?
        /// </summary>
        public bool ForGroup;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="forGroup">True if the undo is for an entire group</param>
        public UndoState(SceneObjectPart part, bool forGroup)
        {
            if (part.ParentID == 0)
            {
                ForGroup = forGroup;

//                    if (ForGroup)
                    Position = part.ParentGroup.AbsolutePosition;
//                    else
//                        Position = part.OffsetPosition;

//                    m_log.DebugFormat(
//                        "[UNDO STATE]: Storing undo position {0} for root part", Position);

                Rotation = part.RotationOffset;

//                    m_log.DebugFormat(
//                        "[UNDO STATE]: Storing undo rotation {0} for root part", Rotation);

                Scale = part.Shape.Scale;

//                    m_log.DebugFormat(
//                        "[UNDO STATE]: Storing undo scale {0} for root part", Scale);
            }
            else
            {
                Position = part.OffsetPosition;
//                    m_log.DebugFormat(
//                        "[UNDO STATE]: Storing undo position {0} for child part", Position);

                Rotation = part.RotationOffset;
//                    m_log.DebugFormat(
//                        "[UNDO STATE]: Storing undo rotation {0} for child part", Rotation);

                Scale = part.Shape.Scale;
//                    m_log.DebugFormat(
//                        "[UNDO STATE]: Storing undo scale {0} for child part", Scale);
            }
        }

        /// <summary>
        /// Compare the relevant state in the given part to this state.
        /// </summary>
        /// <param name="part"></param>
        /// <returns>true if both the part's position, rotation and scale match those in this undo state.  False otherwise.</returns>
        public bool Compare(SceneObjectPart part)
        {
            if (part != null)
            {
                if (part.ParentID == 0)
                    return
                        Position == part.ParentGroup.AbsolutePosition
                            && Rotation == part.RotationOffset
                            && Scale == part.Shape.Scale;
                else
                    return
                        Position == part.OffsetPosition
                            && Rotation == part.RotationOffset
                            && Scale == part.Shape.Scale;
            }

            return false;
        }

        public void PlaybackState(SceneObjectPart part)
        {
            part.Undoing = true;

            if (part.ParentID == 0)
            {
//                    m_log.DebugFormat(
//                        "[UNDO STATE]: Undoing position to {0} for root part {1} {2}",
//                        Position, part.Name, part.LocalId);

                if (Position != Vector3.Zero)
                {
                    if (ForGroup)
                        part.ParentGroup.AbsolutePosition = Position;
                    else
                        part.ParentGroup.UpdateRootPosition(Position);
                }

//                    m_log.DebugFormat(
//                        "[UNDO STATE]: Undoing rotation {0} to {1} for root part {2} {3}",
//                        part.RotationOffset, Rotation, part.Name, part.LocalId);

                if (ForGroup)
                    part.UpdateRotation(Rotation);
                else
                    part.ParentGroup.UpdateRootRotation(Rotation);

                if (Scale != Vector3.Zero)
                {
//                        m_log.DebugFormat(
//                            "[UNDO STATE]: Undoing scale {0} to {1} for root part {2} {3}",
//                            part.Shape.Scale, Scale, part.Name, part.LocalId);

                    if (ForGroup)
                        part.ParentGroup.GroupResize(Scale);
                    else
                        part.Resize(Scale);
                }

                part.ParentGroup.ScheduleGroupForTerseUpdate();
            }
            else
            {
                // Note: Updating these properties on sop automatically schedules an update if needed
                if (Position != Vector3.Zero)
                {
//                        m_log.DebugFormat(
//                            "[UNDO STATE]: Undoing position {0} to {1} for child part {2} {3}",
//                            part.OffsetPosition, Position, part.Name, part.LocalId);

                    part.OffsetPosition = Position;
                }

//                    m_log.DebugFormat(
//                        "[UNDO STATE]: Undoing rotation {0} to {1} for child part {2} {3}",
//                        part.RotationOffset, Rotation, part.Name, part.LocalId);

                part.UpdateRotation(Rotation);

                if (Scale != Vector3.Zero)
                {
//                        m_log.DebugFormat(
//                            "[UNDO STATE]: Undoing scale {0} to {1} for child part {2} {3}",
//                            part.Shape.Scale, Scale, part.Name, part.LocalId);

                    part.Resize(Scale);
                }
            }

            part.Undoing = false;
        }

        public void PlayfwdState(SceneObjectPart part)
        {
            part.Undoing = true;

            if (part.ParentID == 0)
            {
                if (Position != Vector3.Zero)
                    part.ParentGroup.AbsolutePosition = Position;

                if (Rotation != Quaternion.Identity)
                    part.UpdateRotation(Rotation);

                if (Scale != Vector3.Zero)
                {
                    if (ForGroup)
                        part.ParentGroup.GroupResize(Scale);
                    else
                        part.Resize(Scale);
                }

                part.ParentGroup.ScheduleGroupForTerseUpdate();
            }
            else
            {
                // Note: Updating these properties on sop automatically schedules an update if needed
                if (Position != Vector3.Zero)
                    part.OffsetPosition = Position;

                if (Rotation != Quaternion.Identity)
                    part.UpdateRotation(Rotation);

                if (Scale != Vector3.Zero)
                    part.Resize(Scale);
            }

            part.Undoing = false;
        }
    }

    public class LandUndoState
    {
        public ITerrainModule m_terrainModule;
        public ITerrainChannel m_terrainChannel;

        public LandUndoState(ITerrainModule terrainModule, ITerrainChannel terrainChannel)
        {
            m_terrainModule = terrainModule;
            m_terrainChannel = terrainChannel;
        }

        public bool Compare(ITerrainChannel terrainChannel)
        {
            return m_terrainChannel == terrainChannel;
        }

        public void PlaybackState()
        {
            m_terrainModule.UndoTerrain(m_terrainChannel);
        }
    }
}