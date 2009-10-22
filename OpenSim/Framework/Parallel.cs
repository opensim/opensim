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
using System.Threading;

namespace OpenSim.Framework
{
    /// <summary>
    /// Provides helper methods for parallelizing loops
    /// </summary>
    public static class Parallel
    {
        public static readonly int ProcessorCount = System.Environment.ProcessorCount;

        /// <summary>
        /// Executes a for loop in which iterations may run in parallel
        /// </summary>
        /// <param name="fromInclusive">The loop will be started at this index</param>
        /// <param name="toExclusive">The loop will be terminated before this index is reached</param>
        /// <param name="body">Method body to run for each iteration of the loop</param>
        public static void For(int fromInclusive, int toExclusive, Action<int> body)
        {
            For(ProcessorCount, fromInclusive, toExclusive, body);
        }

        /// <summary>
        /// Executes a for loop in which iterations may run in parallel
        /// </summary>
        /// <param name="threadCount">The number of concurrent execution threads to run</param>
        /// <param name="fromInclusive">The loop will be started at this index</param>
        /// <param name="toExclusive">The loop will be terminated before this index is reached</param>
        /// <param name="body">Method body to run for each iteration of the loop</param>
        public static void For(int threadCount, int fromInclusive, int toExclusive, Action<int> body)
        {
            int counter = threadCount;
            AutoResetEvent threadFinishEvent = new AutoResetEvent(false);
            Exception exception = null;

            --fromInclusive;

            for (int i = 0; i < threadCount; i++)
            {
                Util.FireAndForget(
                    delegate(object o)
                    {
                        int threadIndex = (int)o;

                        while (exception == null)
                        {
                            int currentIndex = Interlocked.Increment(ref fromInclusive);

                            if (currentIndex >= toExclusive)
                                break;

                            try { body(currentIndex); }
                            catch (Exception ex) { exception = ex; break; }
                        }

                        if (Interlocked.Decrement(ref counter) == 0)
                            threadFinishEvent.Set();
                    }, i
                );
            }

            threadFinishEvent.WaitOne();
            threadFinishEvent.Close();

            if (exception != null)
                throw new Exception(exception.Message, exception);
        }

        /// <summary>
        /// Executes a foreach loop in which iterations may run in parallel
        /// </summary>
        /// <typeparam name="T">Object type that the collection wraps</typeparam>
        /// <param name="enumerable">An enumerable collection to iterate over</param>
        /// <param name="body">Method body to run for each object in the collection</param>
        public static void ForEach<T>(IEnumerable<T> enumerable, Action<T> body)
        {
            ForEach<T>(ProcessorCount, enumerable, body);
        }

        /// <summary>
        /// Executes a foreach loop in which iterations may run in parallel
        /// </summary>
        /// <typeparam name="T">Object type that the collection wraps</typeparam>
        /// <param name="threadCount">The number of concurrent execution threads to run</param>
        /// <param name="enumerable">An enumerable collection to iterate over</param>
        /// <param name="body">Method body to run for each object in the collection</param>
        public static void ForEach<T>(int threadCount, IEnumerable<T> enumerable, Action<T> body)
        {
            int counter = threadCount;
            AutoResetEvent threadFinishEvent = new AutoResetEvent(false);
            IEnumerator<T> enumerator = enumerable.GetEnumerator();
            Exception exception = null;

            for (int i = 0; i < threadCount; i++)
            {
                Util.FireAndForget(
                    delegate(object o)
                    {
                        int threadIndex = (int)o;

                        while (exception == null)
                        {
                            T entry;

                            lock (enumerator)
                            {
                                if (!enumerator.MoveNext())
                                    break;
                                entry = (T)enumerator.Current; // Explicit typecast for Mono's sake
                            }

                            try { body(entry); }
                            catch (Exception ex) { exception = ex; break; }
                        }

                        if (Interlocked.Decrement(ref counter) == 0)
                            threadFinishEvent.Set();
                    }, i
                );
            }

            threadFinishEvent.WaitOne();
            threadFinishEvent.Close();

            if (exception != null)
                throw new Exception(exception.Message, exception);
        }

        /// <summary>
        /// Executes a series of tasks in parallel
        /// </summary>
        /// <param name="actions">A series of method bodies to execute</param>
        public static void Invoke(params Action[] actions)
        {
            Invoke(ProcessorCount, actions);
        }

        /// <summary>
        /// Executes a series of tasks in parallel
        /// </summary>
        /// <param name="threadCount">The number of concurrent execution threads to run</param>
        /// <param name="actions">A series of method bodies to execute</param>
        public static void Invoke(int threadCount, params Action[] actions)
        {
            int counter = threadCount;
            AutoResetEvent threadFinishEvent = new AutoResetEvent(false);
            int index = -1;
            Exception exception = null;

            for (int i = 0; i < threadCount; i++)
            {
                Util.FireAndForget(
                    delegate(object o)
                    {
                        int threadIndex = (int)o;

                        while (exception == null)
                        {
                            int currentIndex = Interlocked.Increment(ref index);

                            if (currentIndex >= actions.Length)
                                break;

                            try { actions[currentIndex](); }
                            catch (Exception ex) { exception = ex; break; }
                        }

                        if (Interlocked.Decrement(ref counter) == 0)
                            threadFinishEvent.Set();
                    }, i
                );
            }

            threadFinishEvent.WaitOne();
            threadFinishEvent.Close();

            if (exception != null)
                throw new Exception(exception.Message, exception);
        }
    }
}
