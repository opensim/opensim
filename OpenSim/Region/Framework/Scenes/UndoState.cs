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
using OpenSim.Region.Framework.Interfaces;
using System;

namespace OpenSim.Region.Framework.Scenes
{
    [Flags]
    public enum UndoType
    {
        STATE_PRIM_POSITION = 1,
        STATE_PRIM_ROTATION = 2,
        STATE_PRIM_SCALE = 4,
        STATE_PRIM_ALL = 7,
        STATE_GROUP_POSITION = 8,
        STATE_GROUP_ROTATION = 16,
        STATE_GROUP_SCALE = 32,
        STATE_GROUP_ALL = 56,
        STATE_ALL = 63       
    }

    public class UndoState
    {
        public Vector3 Position = Vector3.Zero;
        public Vector3 Scale = Vector3.Zero;
        public Quaternion Rotation = Quaternion.Identity;
        public Vector3 GroupPosition = Vector3.Zero;
        public Quaternion GroupRotation = Quaternion.Identity;
        public Vector3 GroupScale = Vector3.Zero;
        public DateTime LastUpdated = DateTime.Now;
        public UndoType Type;

        public UndoState(SceneObjectPart part, UndoType type)
        {
            Type = type;
            if (part != null)
            {
                if (part.ParentID == 0)
                {
                    GroupScale = part.ParentGroup.RootPart.Shape.Scale;

                    //FUBAR WARNING: Do NOT get the group's absoluteposition here 
                    //or you'll experience a loop and/or a stack issue
                    GroupPosition = part.ParentGroup.RootPart.AbsolutePosition;
                    GroupRotation = part.ParentGroup.Rotation;
                    Position = part.ParentGroup.RootPart.AbsolutePosition;
                    Rotation = part.RotationOffset;
                    Scale = part.Shape.Scale;
                    LastUpdated = DateTime.Now;
                }
                else
                {
                    GroupScale = part.Shape.Scale;

                    //FUBAR WARNING: Do NOT get the group's absoluteposition here 
                    //or you'll experience a loop and/or a stack issue
                    GroupPosition = part.ParentGroup.RootPart.AbsolutePosition;
                    GroupRotation = part.ParentGroup.Rotation;
                    Position = part.OffsetPosition;
                    Rotation = part.RotationOffset;
                    Scale = part.Shape.Scale;
                    LastUpdated = DateTime.Now;
                }
            }
        }
        public void Merge(UndoState last)
        {
            if ((Type & UndoType.STATE_GROUP_POSITION) == 0 || ((last.Type & UndoType.STATE_GROUP_POSITION) >= (Type & UndoType.STATE_GROUP_POSITION)))
            {
                GroupPosition = last.GroupPosition;
                Position = last.Position;
            }
            if ((Type & UndoType.STATE_GROUP_SCALE) == 0 || ((last.Type & UndoType.STATE_GROUP_SCALE) >= (Type & UndoType.STATE_GROUP_SCALE)))
            {
                Console.WriteLine("Setting groupscale to " + last.GroupScale.ToString());
                GroupScale = last.GroupScale;
                Scale = last.Scale;
            }
            if ((Type & UndoType.STATE_GROUP_ROTATION) == 0 || ((last.Type & UndoType.STATE_GROUP_ROTATION) >= (Type & UndoType.STATE_GROUP_ROTATION)))
            {
                GroupRotation = last.GroupRotation;
                Rotation = last.Rotation;
            }
            if ((Type & UndoType.STATE_PRIM_POSITION) == 0 || ((last.Type & UndoType.STATE_PRIM_POSITION) >= (Type & UndoType.STATE_PRIM_POSITION)))
            {
                Position = last.Position;
            }
            if ((Type & UndoType.STATE_PRIM_SCALE) == 0 || ((last.Type & UndoType.STATE_PRIM_SCALE) >= (Type & UndoType.STATE_PRIM_SCALE)))
            {
                Scale = last.Scale;
            }
            if ((Type & UndoType.STATE_PRIM_ROTATION) == 0 || ((last.Type & UndoType.STATE_PRIM_ROTATION) >= (Type & UndoType.STATE_PRIM_ROTATION)))
            {
                Rotation = last.Rotation;
            }
            Type = Type | last.Type;
        }
        public bool Compare(UndoState undo)
        {
            if (undo == null || Position == null) return false;
            if (undo.Position == Position && undo.Rotation == Rotation  && undo.Scale == Scale && undo.GroupPosition == GroupPosition && undo.GroupScale == GroupScale && undo.GroupRotation == GroupRotation)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public bool Compare(SceneObjectPart part)
        {
            if (part != null)
            {
                if (part.ParentID == 0)
                {
                    if (Position == part.ParentGroup.RootPart.AbsolutePosition && Rotation == part.ParentGroup.Rotation && GroupPosition == part.ParentGroup.RootPart.AbsolutePosition && part.ParentGroup.Rotation == GroupRotation && part.Shape.Scale == GroupScale)
                        return true;
                    else
                        return false;
                }
                else
                {
                    if (Position == part.OffsetPosition && Rotation == part.RotationOffset && Scale == part.Shape.Scale && GroupPosition == part.ParentGroup.RootPart.AbsolutePosition && part.ParentGroup.Rotation == GroupRotation && part.Shape.Scale == GroupScale)
                        return true;
                    else
                        return false;

                }
            }
            return false;
        }

        public void PlaybackState(SceneObjectPart part)
        {
            bool GroupChange = false;
            if ((Type & UndoType.STATE_GROUP_POSITION) != 0
                || (Type & UndoType.STATE_GROUP_ROTATION) != 0
                || (Type & UndoType.STATE_GROUP_SCALE) != 0)
            {
                GroupChange = true;
            }

            if (part != null)
            {
                part.Undoing = true;

                if (part.ParentID == 0 && GroupChange == false)
                {
                    if (Position != Vector3.Zero)
                    part.ParentGroup.AbsolutePosition = Position;
                    part.RotationOffset = Rotation;
                    if (Scale != Vector3.Zero)
                        part.Resize(Scale);
                    part.ParentGroup.ScheduleGroupForTerseUpdate();
                }
                else
                {
                    if (GroupChange)
                    {
                        part.ParentGroup.RootPart.Undoing = true;
                        if (Position != Vector3.Zero)
                        {
                            //Calculate the scale...
                            Vector3 gs = part.Shape.Scale;
                            float scale = GroupScale.Z / gs.Z;

                            //Scale first since it can affect our position
                            part.ParentGroup.GroupResize(gs * scale, part.LocalId);
                            part.ParentGroup.AbsolutePosition = GroupPosition;
                            part.ParentGroup.Rotation = GroupRotation;
                           
                        }
                        part.ParentGroup.RootPart.Undoing = false;
                    }
                    else
                    {
                        if (Position != Vector3.Zero) //We can use this for all the updates since all are set
                        {
                            part.OffsetPosition = Position;
                            part.UpdateRotation(Rotation);
                            part.Resize(Scale); part.ScheduleTerseUpdate();
                        }
                    }
                }
                part.Undoing = false;

            }
        }
        public void PlayfwdState(SceneObjectPart part)
        {
            if (part != null)
            {
                part.Undoing = true;

                if (part.ParentID == 0)
                {
                    if (Position != Vector3.Zero)
                        part.ParentGroup.AbsolutePosition = Position;
                    if (Rotation != Quaternion.Identity)
                        part.UpdateRotation(Rotation);
                    if (Scale != Vector3.Zero)
                        part.Resize(Scale);
                    part.ParentGroup.ScheduleGroupForTerseUpdate();
                }
                else
                {
                    if (Position != Vector3.Zero)
                        part.OffsetPosition = Position;
                    if (Rotation != Quaternion.Identity)
                        part.UpdateRotation(Rotation);
                    if (Scale != Vector3.Zero)
                        part.Resize(Scale);
                    part.ScheduleTerseUpdate();
                }
                part.Undoing = false;

            }
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
            if (m_terrainChannel != terrainChannel)
                return false;
            else
                return false;
        }

        public void PlaybackState()
        {
            m_terrainModule.UndoTerrain(m_terrainChannel);
        }
    }
}
