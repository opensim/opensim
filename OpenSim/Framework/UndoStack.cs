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

namespace OpenSim.Framework
{
    /// <summary>
    /// Undo stack.  Deletes entries beyond a certain capacity
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class UndoStack<T>
    {
        private int m_new = 1;
        private int m_old = 0;
        private T[] m_Undos;

        public UndoStack(int capacity)
        {
            m_Undos = new T[capacity + 1];
        }

        public bool IsFull
        {
            get { return m_new == m_old; }
        }

        public int Capacity
        {
            get { return m_Undos.Length - 1; }
        }

        public int Count
        {
            get
            {
                int count = m_new - m_old - 1;
                if (count < 0)
                    count += m_Undos.Length;
                return count;
            }
        }

        public void Push(T item)
        {
            if (IsFull)
            {
                m_old++;
                if (m_old >= m_Undos.Length)
                    m_old -= m_Undos.Length;
            }
            if (++m_new >= m_Undos.Length)
                m_new -= m_Undos.Length;
            m_Undos[m_new] = item;
        }

        public T Pop()
        {
            if (Count > 0)
            {
                T deleted = m_Undos[m_new];
                m_Undos[m_new--] = default(T);
                if (m_new < 0)
                    m_new += m_Undos.Length;
                return deleted;
            }
            else
                throw new InvalidOperationException("Cannot pop from empty stack");
        }

        public T Peek()
        {
            return m_Undos[m_new];
        }

        public void Clear()
        {
            if (Count > 0)
            {
                for (int i = 0; i < m_Undos.Length; i++)
                {
                    m_Undos[i] = default(T);
                }
                m_new = 1;
                m_old = 0;
            }
        }
    }
}
