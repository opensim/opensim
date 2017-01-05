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
using System.Collections.Generic;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.Framework.Scenes
{
    public class UndoState
    {
        const int UNDOEXPIRESECONDS = 300; // undo expire time   (nice to have it came from a ini later)

        public ObjectChangeData data;
        public DateTime creationtime;
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="change">bit field with what is changed</param>
        ///
        public UndoState(SceneObjectPart part, ObjectChangeType change)
        {
            data = new ObjectChangeData();
            data.change = change;
            creationtime = DateTime.UtcNow;

            if (part.ParentGroup.RootPart == part)
            {
                if ((change & ObjectChangeType.Position) != 0)
                    data.position = part.ParentGroup.AbsolutePosition;
                if ((change & ObjectChangeType.Rotation) != 0)
                    data.rotation = part.RotationOffset;
                if ((change & ObjectChangeType.Scale) != 0)
                    data.scale = part.Shape.Scale;
            }
            else
            {
                if ((change & ObjectChangeType.Position) != 0)
                    data.position = part.OffsetPosition;
                if ((change & ObjectChangeType.Rotation) != 0)
                    data.rotation = part.RotationOffset;
                if ((change & ObjectChangeType.Scale) != 0)
                    data.scale = part.Shape.Scale;
            }
        }
        /// <summary>
        /// check if undo or redo is too old
        /// </summary>

        public bool checkExpire()
        {
            TimeSpan t = DateTime.UtcNow - creationtime;
            if (t.Seconds > UNDOEXPIRESECONDS)
                return true;
            return false;
        }

        /// <summary>
        /// updates undo or redo creation time to now
        /// </summary>
        public void updateExpire()
        {
            creationtime = DateTime.UtcNow;
        }

        /// <summary>
        /// Compare the relevant state in the given part to this state.
        /// </summary>
        /// <param name="part"></param>
        /// <returns>true what fiels and related data are equal, False otherwise.</returns>
        ///
        public bool Compare(SceneObjectPart part, ObjectChangeType change)
        {
            if (data.change != change) // if diferent targets, then they are diferent
                return false;

            if (part != null)
            {
                if (part.ParentID == 0)
                {
                    if ((change & ObjectChangeType.Position) != 0 && data.position != part.ParentGroup.AbsolutePosition)
                        return false;
                }
                else
                {
                    if ((change & ObjectChangeType.Position) != 0 && data.position != part.OffsetPosition)
                        return false;
                }

                if ((change & ObjectChangeType.Rotation) != 0 && data.rotation != part.RotationOffset)
                    return false;
                if ((change & ObjectChangeType.Rotation) != 0 && data.scale == part.Shape.Scale)
                    return false;
                return true;

            }
            return false;
        }

        /// <summary>
        /// executes the undo or redo to a part or its group
        /// </summary>
        /// <param name="part"></param>
        ///

        public void PlayState(SceneObjectPart part)
        {
            part.Undoing = true;

            SceneObjectGroup grp = part.ParentGroup;

            if (grp != null)
            {
                grp.doChangeObject(part, data);
            }
            part.Undoing = false;
        }
    }

    public class UndoRedoState
    {
        int size;
        public LinkedList<UndoState> m_redo = new LinkedList<UndoState>();
        public LinkedList<UndoState> m_undo = new LinkedList<UndoState>();

        /// <summary>
        /// creates a new UndoRedoState with default states memory size
        /// </summary>

        public UndoRedoState()
        {
            size = 5;
        }

        /// <summary>
        /// creates a new UndoRedoState with states memory having indicated size
        /// </summary>
        /// <param name="size"></param>

        public UndoRedoState(int _size)
        {
            if (_size < 3)
                size = 3;
            else
                size = _size;
        }

        /// <summary>
        /// returns number of undo entries in memory
        /// </summary>

        public int Count
        {
            get { return m_undo.Count; }
        }

        /// <summary>
        /// clears all undo and redo entries
        /// </summary>

        public void Clear()
        {
            m_undo.Clear();
            m_redo.Clear();
        }

        /// <summary>
        /// adds a new state undo to part or its group, with changes indicated by what bits
        /// </summary>
        /// <param name="part"></param>
        /// <param name="change">bit field with what is changed</param>

        public void StoreUndo(SceneObjectPart part, ObjectChangeType change)
        {
            lock (m_undo)
            {
                UndoState last;

                if (m_redo.Count > 0) // last code seems to clear redo on every new undo
                {
                    m_redo.Clear();
                }

                if (m_undo.Count > 0)
                {
                    // check expired entry
                    last = m_undo.First.Value;
                    if (last != null && last.checkExpire())
                        m_undo.Clear();
                    else
                    {
                        // see if we actually have a change
                        if (last != null)
                        {
                            if (last.Compare(part, change))
                                return;
                        }
                    }
                }

                // limite size
                while (m_undo.Count >= size)
                    m_undo.RemoveLast();

                UndoState nUndo = new UndoState(part, change);
                m_undo.AddFirst(nUndo);
            }
        }

        /// <summary>
        /// executes last state undo to part or its group
        /// current state is pushed into redo
        /// </summary>
        /// <param name="part"></param>

        public void Undo(SceneObjectPart part)
        {
            lock (m_undo)
            {
                UndoState nUndo;

                // expire redo
                if (m_redo.Count > 0)
                {
                    nUndo = m_redo.First.Value;
                    if (nUndo != null && nUndo.checkExpire())
                        m_redo.Clear();
                }

                if (m_undo.Count > 0)
                {
                    UndoState goback = m_undo.First.Value;
                    // check expired
                    if (goback != null && goback.checkExpire())
                    {
                        m_undo.Clear();
                        return;
                    }

                    if (goback != null)
                    {
                        m_undo.RemoveFirst();

                        // redo limite size
                        while (m_redo.Count >= size)
                            m_redo.RemoveLast();

                        nUndo = new UndoState(part, goback.data.change); // new value in part should it be full goback copy?
                        m_redo.AddFirst(nUndo);

                        goback.PlayState(part);
                    }
                }
            }
        }

        /// <summary>
        /// executes last state redo to part or its group
        /// current state is pushed into undo
        /// </summary>
        /// <param name="part"></param>

        public void Redo(SceneObjectPart part)
        {
            lock (m_undo)
            {
                UndoState nUndo;

                // expire undo
                if (m_undo.Count > 0)
                {
                    nUndo = m_undo.First.Value;
                    if (nUndo != null && nUndo.checkExpire())
                        m_undo.Clear();
                }

                if (m_redo.Count > 0)
                {
                    UndoState gofwd = m_redo.First.Value;
                    // check expired
                    if (gofwd != null && gofwd.checkExpire())
                    {
                        m_redo.Clear();
                        return;
                    }

                    if (gofwd != null)
                    {
                        m_redo.RemoveFirst();

                        // limite undo size
                        while (m_undo.Count >= size)
                            m_undo.RemoveLast();

                        nUndo = new UndoState(part, gofwd.data.change);   // new value in part should it be full gofwd copy?
                        m_undo.AddFirst(nUndo);

                        gofwd.PlayState(part);
                    }
                }
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
            return m_terrainChannel == terrainChannel;
        }

        public void PlaybackState()
        {
            m_terrainModule.UndoTerrain(m_terrainChannel);
        }
    }
}
