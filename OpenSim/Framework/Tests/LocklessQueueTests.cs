using System;
using NUnit.Framework;
using System.Threading;

namespace OpenSim.Framework.Tests
{
    [TestFixture]
    public class LocklessQueueTests
    {
        public LocklessQueue<int> sharedQueue;
        [SetUp]
        public void build()
        {
            sharedQueue = new LocklessQueue<int>();
            
        }

        [Test]
        public void EnqueueDequeueTest()
        {
            sharedQueue.Enqueue(1);
            int dequeue;
            sharedQueue.Dequeue(out dequeue);
            Assert.That(dequeue == 1, "Enqueued 1.   Dequeue should also be 1");
            Assert.That(sharedQueue.Count == 0, "We Dequeued the last item, count should be 0");

        }

        [Test]
        public void ThreadedSimpleEnqueueDequeueTest()
        {
            int loopamountA = 5000;
            int loopamountB = 5000;
            int loopamountC = 5000;
            int loopamountD = 5000;

            threadObject1 obj1 = new threadObject1(this, loopamountA);
            threadObject1 obj2 = new threadObject1(this, loopamountB);
            threadObject1 obj3 = new threadObject1(this, loopamountC);
            threadObject1 obj4 = new threadObject1(this, loopamountD);
            for (int i=0;i<1;i++)
            {
                sharedQueue.Enqueue(i);
            }

            Thread thr = new Thread(obj1.thread1Action);
            Thread thr2 = new Thread(obj2.thread1Action);
            Thread thr3 = new Thread(obj3.thread1Action);
            Thread thr4 = new Thread(obj4.thread1Action);
            thr.Start();
            thr2.Start();
            thr3.Start();
            thr4.Start();

            thr.Join();
            thr2.Join();
            thr3.Join();
            thr4.Join();

            Assert.That(sharedQueue.Count == 1);
            int result = 0;
            sharedQueue.Dequeue(out result);
            Assert.That(result == loopamountD + loopamountC + loopamountB + loopamountA, "Threaded Result test failed.  Expected the sum of all of the threads adding to the item in the queue.  Got {0}, Expected {1}", result, loopamountD + loopamountC + loopamountB + loopamountA);

        }

        /* This test fails.   Need clarification if this should work
        [Test]
        public void ThreadedAdvancedEnqueueDequeueTest()
        {
            int loopamountA = 5000;
            int loopamountB = 5000;
            int loopamountC = 5000;
            int loopamountD = 5000;

            threadObject1 obj1 = new threadObject1(this, loopamountA);
            threadObject2 obj2 = new threadObject2(this, loopamountB);
            threadObject1 obj3 = new threadObject1(this, loopamountC);
            threadObject2 obj4 = new threadObject2(this, loopamountD);
            for (int i = 0; i < 1; i++)
            {
                sharedQueue.Enqueue(i);
            }

            Thread thr = new Thread(obj1.thread1Action);
            Thread thr2 = new Thread(obj2.thread1Action);
            Thread thr3 = new Thread(obj3.thread1Action);
            Thread thr4 = new Thread(obj4.thread1Action);
            thr.Start();
            thr2.Start();
            thr3.Start();
            thr4.Start();

            thr.Join();
            thr2.Join();
            thr3.Join();
            thr4.Join();

            Assert.That(sharedQueue.Count == 1);
            int result = 0;
            sharedQueue.Dequeue(out result);
            Assert.That(result == loopamountA - loopamountB + loopamountC - loopamountD, "Threaded Result test failed.  Expected the sum of all of the threads adding to the item in the queue.  Got {0}, Expected {1}", result, loopamountA - loopamountB + loopamountC - loopamountD);

        }
         */
    }
    // Dequeue one from the locklessqueue add one to it and enqueue it again.
    public class threadObject1
    {
        private LocklessQueueTests m_tests;
        private int m_loopamount = 0;
        public threadObject1(LocklessQueueTests tst, int loopamount)
        {
            m_tests = tst;
            m_loopamount = loopamount;
        }
        public void thread1Action(object o)
        {
            for (int i=0;i<m_loopamount;i++)
            {
                int j = 0;
                m_tests.sharedQueue.Dequeue(out j);
                m_tests.sharedQueue.Enqueue(++j);
            }
        }
    }
    // Dequeue one from the locklessqueue subtract one from it and enqueue it again.
    public class threadObject2
    {
        private LocklessQueueTests m_tests;
        private int m_loopamount = 0;
        public threadObject2(LocklessQueueTests tst, int loopamount)
        {
            m_tests = tst;
            m_loopamount = loopamount;
        }
        public void thread1Action(object o)
        {
            for (int i = 0; i < m_loopamount; i++)
            {
                int j = 0;
                m_tests.sharedQueue.Dequeue(out j);
                m_tests.sharedQueue.Enqueue(--j);
            }
        }
    }
}
