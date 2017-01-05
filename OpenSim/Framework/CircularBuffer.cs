/*
Copyright (c) 2012, Alex Regueiro
All rights reserved.
Redistribution and use in source and binary forms, with or without modification, are permitted provided that the
following conditions are met:

Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer
in the documentation and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING,
BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY,
OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA,
OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
POSSIBILITY OF SUCH DAMAGE.
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace OpenSim.Framework
{
    public class CircularBuffer<T> : ICollection<T>, IEnumerable<T>, ICollection, IEnumerable
    {
        private int capacity;
        private int size;
        private int head;
        private int tail;
        private T[] buffer;

        [NonSerialized()]
        private object syncRoot;

        public CircularBuffer(int capacity)
            : this(capacity, false)
        {
        }

        public CircularBuffer(int capacity, bool allowOverflow)
        {
            if (capacity < 0)
                throw new ArgumentException("Needs to have at least 1","capacity");

            this.capacity = capacity;
            size = 0;
            head = 0;
            tail = 0;
            buffer = new T[capacity];
            AllowOverflow = allowOverflow;
        }

        public bool AllowOverflow
        {
            get;
            set;
        }

        public int Capacity
        {
            get { return capacity; }
            set
            {
                if (value == capacity)
                    return;

                if (value < size)
                    throw new ArgumentOutOfRangeException("value","Capacity is too small.");

                var dst = new T[value];
                if (size > 0)
                    CopyTo(dst);
                buffer = dst;

                capacity = value;
            }
        }

        public int Size
        {
            get { return size; }
        }

        public bool Contains(T item)
        {
            int bufferIndex = head;
            var comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < size; i++, bufferIndex++)
            {
                if (bufferIndex == capacity)
                    bufferIndex = 0;

                if (item == null && buffer[bufferIndex] == null)
                    return true;
                else if ((buffer[bufferIndex] != null) &&
                    comparer.Equals(buffer[bufferIndex], item))
                    return true;
            }

            return false;
        }

        public void Clear()
        {
            size = 0;
            head = 0;
            tail = 0;
        }

        public int Put(T[] src)
        {
            return Put(src, 0, src.Length);
        }

        public int Put(T[] src, int offset, int count)
        {
            if (!AllowOverflow && count > capacity - size)
                throw new InvalidOperationException("Buffer Overflow");

            int srcIndex = offset;
            for (int i = 0; i < count; i++, tail++, srcIndex++)
            {
                if (tail == capacity)
                    tail = 0;
                buffer[tail] = src[srcIndex];
            }
            size = Math.Min(size + count, capacity);
            return count;
        }

        public void Put(T item)
        {
            if (!AllowOverflow && size == capacity)
                throw new InvalidOperationException("Buffer Overflow");

            buffer[tail] = item;
            if (++tail == capacity)
                tail = 0;
            size++;
        }

        public void Skip(int count)
        {
            head += count;
            if (head >= capacity)
                head -= capacity;
        }

        public T[] Get(int count)
        {
            var dst = new T[count];
            Get(dst);
            return dst;
        }

        public int Get(T[] dst)
        {
            return Get(dst, 0, dst.Length);
        }

        public int Get(T[] dst, int offset, int count)
        {
            int realCount = Math.Min(count, size);
            int dstIndex = offset;
            for (int i = 0; i < realCount; i++, head++, dstIndex++)
            {
                if (head == capacity)
                    head = 0;
                dst[dstIndex] = buffer[head];
            }
            size -= realCount;
            return realCount;
        }

        public T Get()
        {
            if (size == 0)
                throw new InvalidOperationException("Buffer Empty");

            var item = buffer[head];
            if (++head == capacity)
                head = 0;
            size--;
            return item;
        }

        public void CopyTo(T[] array)
        {
            CopyTo(array, 0);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            CopyTo(0, array, arrayIndex, size);
        }

        public void CopyTo(int index, T[] array, int arrayIndex, int count)
        {
            if (count > size)
                throw new ArgumentOutOfRangeException("count", "Count Too Large");

            int bufferIndex = head;
            for (int i = 0; i < count; i++, bufferIndex++, arrayIndex++)
            {
                if (bufferIndex == capacity)
                    bufferIndex = 0;
                array[arrayIndex] = buffer[bufferIndex];
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            int bufferIndex = head;
            for (int i = 0; i < size; i++, bufferIndex++)
            {
                if (bufferIndex == capacity)
                    bufferIndex = 0;

                yield return buffer[bufferIndex];
            }
        }

        public T[] GetBuffer()
        {
            return buffer;
        }

        public T[] ToArray()
        {
            var dst = new T[size];
            CopyTo(dst);
            return dst;
        }

        #region ICollection<T> Members

        int ICollection<T>.Count
        {
            get { return Size; }
        }

        bool ICollection<T>.IsReadOnly
        {
            get { return false; }
        }

        void ICollection<T>.Add(T item)
        {
            Put(item);
        }

        bool ICollection<T>.Remove(T item)
        {
            if (size == 0)
                return false;

            Get();
            return true;
        }

        #endregion

        #region IEnumerable<T> Members

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region ICollection Members

        int ICollection.Count
        {
            get { return Size; }
        }

        bool ICollection.IsSynchronized
        {
            get { return false; }
        }

        object ICollection.SyncRoot
        {
            get
            {
                if (syncRoot == null)
                    Interlocked.CompareExchange(ref syncRoot, new object(), null);
                return syncRoot;
            }
        }

        void ICollection.CopyTo(Array array, int arrayIndex)
        {
            CopyTo((T[])array, arrayIndex);
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (IEnumerator)GetEnumerator();
        }

        #endregion
    }
}
