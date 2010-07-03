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
using System.Collections.Generic;

namespace OpenSim.Framework
{
    /// <summary>
    /// Undo stack.  Deletes entries beyond a certain capacity
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class UndoStack<T>
    {
        private List<T> m_undolist;
        private int m_max;

        public UndoStack(int capacity)
        {
            m_undolist = new List<T>();
            m_max = capacity;
        }

        public bool IsFull
        {
            get { return m_undolist.Count >= m_max; }
        }

        public int Capacity
        {
            get { return m_max; }
        }

        public int Count
        {
            get
            {
                return m_undolist.Count;
            }
        }

        public void Push(T item)
        {
            if (IsFull)
            {
                m_undolist.RemoveAt(0);
            }
            m_undolist.Add(item);
        }

        public T Pop()
        {
            if (m_undolist.Count > 0)
            {
                int ind = m_undolist.Count - 1;
                T item = m_undolist[ind];
                m_undolist.RemoveAt(ind);
                return item;
            }
            else
                throw new InvalidOperationException("Cannot pop from empty stack");
        }

        public T Peek()
        {
            return m_undolist[m_undolist.Count - 1];
        }

        public void Clear()
        {
            m_undolist.Clear();
        }
    }
}
