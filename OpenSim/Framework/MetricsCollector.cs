using System;
using System.Diagnostics;

namespace OpenSim.Framework
{
    /// <summary>
    /// A MetricsCollector for 'long' values.
    /// </summary>
    public class MetricsCollectorLong : MetricsCollector<long>
    {
        public MetricsCollectorLong(int windowSize, int numBuckets)
            : base(windowSize, numBuckets)
        {
        }

        protected override long GetZero() { return 0; }

        protected override long Add(long a, long b) { return a + b; }
    }


    /// <summary>
    /// A MetricsCollector for time spans.
    /// </summary>
    public class MetricsCollectorTime : MetricsCollectorLong
    {
        public MetricsCollectorTime(int windowSize, int numBuckets)
            : base(windowSize, numBuckets)
        {
        }

        public void AddSample(Stopwatch timer)
        {
            long ticks = timer.ElapsedTicks;
            if (ticks > 0)
                AddSample(ticks);
        }

        public TimeSpan GetSumTime()
        {
            return TimeSpan.FromMilliseconds((GetSum() * 1000) / Stopwatch.Frequency);
        }
    }


    struct MetricsBucket<T>
    {
        public T value;
        public int count;
    }


    /// <summary>
    /// Collects metrics in a sliding window.
    /// </summary>
    /// <remarks>
    /// MetricsCollector provides the current Sum of the metrics that it collects. It can easily be extended
    /// to provide the Average, too. It uses a sliding window to keep these values current.
    ///
    /// This class is not thread-safe.
    ///
    /// Subclass MetricsCollector to have it use a concrete value type. Override the abstract methods.
    /// </remarks>
    public abstract class MetricsCollector<T>
    {
        private int bucketSize;     // e.g. 3,000 ms

        private MetricsBucket<T>[] buckets;

        private int NumBuckets { get { return buckets.Length; } }


        // The number of the current bucket, if we had an infinite number of buckets and didn't have to wrap around
        long curBucketGlobal;

        // The total of all the buckets
        T totalSum;
        int totalCount;


        /// <summary>
        /// Returns the default (zero) value.
        /// </summary>
        /// <returns></returns>
        protected abstract T GetZero();

        /// <summary>
        /// Adds two values.
        /// </summary>
        protected abstract T Add(T a, T b);


        /// <summary>
        /// Creates a MetricsCollector.
        /// </summary>
        /// <param name="windowSize">The period of time over which to collect the metrics, in ms. E.g.: 30,000.</param>
        /// <param name="numBuckets">The number of buckets to divide the samples into. E.g.: 10. Using more buckets
        /// smooths the jarring that occurs whenever we drop an old bucket, but uses more memory.</param>
        public MetricsCollector(int windowSize, int numBuckets)
        {
            bucketSize = windowSize / numBuckets;
            buckets = new MetricsBucket<T>[numBuckets];
            Reset();
        }

        public void Reset()
        {
            ZeroBuckets(0, NumBuckets);
            curBucketGlobal = GetNow() / bucketSize;
            totalSum = GetZero();
            totalCount = 0;
        }

        public void AddSample(T sample)
        {
            MoveWindow();

            int curBucket = (int)(curBucketGlobal % NumBuckets);
            buckets[curBucket].value = Add(buckets[curBucket].value, sample);
            buckets[curBucket].count++;

            totalSum = Add(totalSum, sample);
            totalCount++;
        }

        /// <summary>
        /// Returns the total values in the collection window.
        /// </summary>
        public T GetSum()
        {
            // It might have been a while since we last added a sample, so we may need to adjust the window
            MoveWindow();

            return totalSum;
        }

        /// <summary>
        /// Returns the current time in ms.
        /// </summary>
        private long GetNow()
        {
            return DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        }

        /// <summary>
        /// Clears the values in buckets [offset, offset+num)
        /// </summary>
        private void ZeroBuckets(int offset, int num)
        {
            for (int i = 0; i < num; i++)
            {
                buckets[offset + i].value = GetZero();
                buckets[offset + i].count = 0;
            }
        }

        /// <summary>
        /// Adjusts the buckets so that the "current bucket" corresponds to the current time.
        /// This may require dropping old buckets.
        /// </summary>
        /// <remarks>
        /// This method allows for the possibility that we don't get new samples for each bucket, so the
        /// new bucket may be some distance away from the last used bucket.
        /// </remarks>
        private void MoveWindow()
        {
            long newBucketGlobal = GetNow() / bucketSize;
            long bucketsDistance = newBucketGlobal - curBucketGlobal;

            if (bucketsDistance == 0)
            {
                // We're still on the same bucket as before
                return;
            }

            if (bucketsDistance >= NumBuckets)
            {
                // Discard everything
                Reset();
                return;
            }

            int curBucket = (int)(curBucketGlobal % NumBuckets);
            int newBucket = (int)(newBucketGlobal % NumBuckets);


            // Clear all the buckets in this range: (cur, new]
            int numToClear = (int)bucketsDistance;

            if (curBucket < NumBuckets - 1)
            {
                // Clear buckets at the end of the window
                int num = Math.Min((int)bucketsDistance, NumBuckets - (curBucket + 1));
                ZeroBuckets(curBucket + 1, num);
                numToClear -= num;
            }

            if (numToClear > 0)
            {
                // Clear buckets at the beginning of the window
                ZeroBuckets(0, numToClear);
            }

            // Move the "current bucket" pointer
            curBucketGlobal = newBucketGlobal;

            RecalcTotal();
        }

        private void RecalcTotal()
        {
            totalSum = GetZero();
            totalCount = 0;

            for (int i = 0; i < NumBuckets; i++)
            {
                totalSum = Add(totalSum, buckets[i].value);
                totalCount += buckets[i].count;
            }
        }

    }
}
