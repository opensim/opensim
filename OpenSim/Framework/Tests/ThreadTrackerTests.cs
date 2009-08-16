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
using NUnit.Framework;
using System.Threading;
using System.Collections.Generic;

namespace OpenSim.Framework.Tests
{
    [TestFixture]
    public class ThreadTrackerTests
    {
        private bool running = true;
        private bool running2 = true;

        [Test]
        public void DefaultThreadTrackerTest()
        {
            List<Thread> lThread = ThreadTracker.GetThreads();
            
            /*
            foreach (Thread t in lThread)
            {
                System.Console.WriteLine(t.Name);
            }
            */

            Assert.That(lThread.Count == 1);
            Assert.That(lThread[0].Name == "ThreadTrackerThread");
        }

        /// <summary>
        /// Validate that adding a thread to the thread tracker works
        /// Validate that removing a thread from the thread tracker also works.
        /// </summary>
        [Test]
        public void AddThreadToThreadTrackerTestAndRemoveTest()
        {
            Thread t = new Thread(run);
            t.Name = "TestThread";
            t.Priority = ThreadPriority.BelowNormal;
            t.IsBackground = true;
            t.SetApartmentState(ApartmentState.MTA);
            t.Start();
            ThreadTracker.Add(t);

            List<Thread> lThread = ThreadTracker.GetThreads();

            Assert.That(lThread.Count == 2);

            foreach (Thread tr in lThread)
            {
                Assert.That((tr.Name == "ThreadTrackerThread" || tr.Name == "TestThread"));
            }
            running = false;
            ThreadTracker.Remove(t);

            lThread = ThreadTracker.GetThreads();

            Assert.That(lThread.Count == 1);

            foreach (Thread tr in lThread)
            {
                Assert.That((tr.Name == "ThreadTrackerThread"));
            }


        }

        /// <summary>
        /// Test a dead thread removal by aborting it and setting it's last seen active date to 50 seconds
        /// </summary>
        [Test]
        public void DeadThreadTest()
        {
            Thread t = new Thread(run2);
            t.Name = "TestThread";
            t.Priority = ThreadPriority.BelowNormal;
            t.IsBackground = true;
            t.SetApartmentState(ApartmentState.MTA);
            t.Start();
            ThreadTracker.Add(t);
            t.Abort();
            Thread.Sleep(5000);
            ThreadTracker.m_Threads[1].LastSeenActive = DateTime.Now.Ticks - (50*10000000);
            ThreadTracker.CleanUp();
            List<Thread> lThread = ThreadTracker.GetThreads();

            Assert.That(lThread.Count == 1);

            foreach (Thread tr in lThread)
            {
                Assert.That((tr.Name == "ThreadTrackerThread"));
            }
        }

        [Test]
        public void UnstartedThreadTest()
        {
            Thread t = new Thread(run2);
            t.Name = "TestThread";
            t.Priority = ThreadPriority.BelowNormal;
            t.IsBackground = true;
            t.SetApartmentState(ApartmentState.MTA);
            ThreadTracker.Add(t);
            ThreadTracker.m_Threads[1].LastSeenActive = DateTime.Now.Ticks - (50 * 10000000);
            ThreadTracker.CleanUp();
            List<Thread> lThread = ThreadTracker.GetThreads();

            Assert.That(lThread.Count == 1);

            foreach (Thread tr in lThread)
            {
                Assert.That((tr.Name == "ThreadTrackerThread"));
            }
        }

        [Test]
        public void NullThreadTest()
        {
            Thread t = null;
            ThreadTracker.Add(t);
            
            List<Thread> lThread = ThreadTracker.GetThreads();

            Assert.That(lThread.Count == 1);

            foreach (Thread tr in lThread)
            {
                Assert.That((tr.Name == "ThreadTrackerThread"));
            }
        }


        /// <summary>
        /// Worker thread 0
        /// </summary>
        /// <param name="o"></param>
        public void run(object o)
        {
            while (running)
            {
                Thread.Sleep(5000);
            }
        }

        /// <summary>
        /// Worker thread 1
        /// </summary>
        /// <param name="o"></param>
        public void run2(object o)
        {
            try
            {
                while (running2)
                {
                    Thread.Sleep(5000);
                }

            } 
            catch (ThreadAbortException)
            {
            }
        }

    }
}
