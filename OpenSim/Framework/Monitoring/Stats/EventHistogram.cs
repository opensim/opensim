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
using System.Linq;
using System.Text;

using OpenMetaverse.StructuredData;

namespace OpenSim.Framework.Monitoring
{
// Create a time histogram of events. The histogram is built in a wrap-around
//   array of equally distributed buckets.
// For instance, a minute long histogram of second sized buckets would be:
//          new EventHistogram(60, 1000)
public class EventHistogram
{
    private int m_timeBase;
    private int m_numBuckets;
    private int m_bucketMilliseconds;
    private int m_lastBucket;
    private int m_totalHistogramMilliseconds;
    private long[] m_histogram;
    private object histoLock = new object();

    public EventHistogram(int numberOfBuckets, int millisecondsPerBucket)
    {
        m_numBuckets = numberOfBuckets;
        m_bucketMilliseconds = millisecondsPerBucket;
        m_totalHistogramMilliseconds = m_numBuckets * m_bucketMilliseconds;

        m_histogram = new long[m_numBuckets];
        Zero();
        m_lastBucket = 0;
        m_timeBase = Util.EnvironmentTickCount();
    }

    public void Event()
    {
        this.Event(1);
    }

    // Record an event at time 'now' in the histogram.
    public void Event(int cnt)
    {
        lock (histoLock)
        {
            // The time as displaced from the base of the histogram
            int bucketTime = Util.EnvironmentTickCountSubtract(m_timeBase);

            // If more than the total time of the histogram, we just start over
            if (bucketTime > m_totalHistogramMilliseconds)
            {
                Zero();
                m_lastBucket = 0;
                m_timeBase = Util.EnvironmentTickCount();
            }
            else
            {
                // To which bucket should we add this event?
                int bucket = bucketTime / m_bucketMilliseconds;

                // Advance m_lastBucket to the new bucket. Zero any buckets skipped over.
                while (bucket != m_lastBucket)
                {
                    // Zero from just after the last bucket to the new bucket or the end
                    for (int jj = m_lastBucket + 1; jj <= Math.Min(bucket, m_numBuckets - 1); jj++)
                    {
                        m_histogram[jj] = 0;
                    }
                    m_lastBucket = bucket;
                    // If the new bucket is off the end, wrap around to the beginning
                    if (bucket > m_numBuckets)
                    {
                        bucket -= m_numBuckets;
                        m_lastBucket = 0;
                        m_histogram[m_lastBucket] = 0;
                        m_timeBase += m_totalHistogramMilliseconds;
                    }
                }
            }
            m_histogram[m_lastBucket] += cnt;
        }
    }

    // Get a copy of the current histogram
    public long[] GetHistogram()
    {
        long[] ret = new long[m_numBuckets];
        lock (histoLock)
        {
            int indx = m_lastBucket + 1;
            for (int ii = 0; ii < m_numBuckets; ii++, indx++)
            {
                if (indx >= m_numBuckets)
                    indx = 0;
                ret[ii] = m_histogram[indx];
            }
        }
        return ret;
    }

    public OSDMap GetHistogramAsOSDMap()
    {
        OSDMap ret = new OSDMap();

        ret.Add("Buckets", OSD.FromInteger(m_numBuckets));
        ret.Add("BucketMilliseconds", OSD.FromInteger(m_bucketMilliseconds));
        ret.Add("TotalMilliseconds", OSD.FromInteger(m_totalHistogramMilliseconds));

        // Compute a number for the first bucket in the histogram.
        // This will allow readers to know how this histogram relates to any previously read histogram.
        int baseBucketNum = (m_timeBase / m_bucketMilliseconds) + m_lastBucket + 1;
        ret.Add("BaseNumber", OSD.FromInteger(baseBucketNum));

        ret.Add("Values", GetHistogramAsOSDArray());

        return ret;
    }
    // Get a copy of the current histogram
    public OSDArray GetHistogramAsOSDArray()
    {
        OSDArray ret = new OSDArray(m_numBuckets);
        lock (histoLock)
        {
            int indx = m_lastBucket + 1;
            for (int ii = 0; ii < m_numBuckets; ii++, indx++)
            {
                if (indx >= m_numBuckets)
                    indx = 0;
                ret[ii] = OSD.FromLong(m_histogram[indx]);
            }
        }
        return ret;
    }

    // Zero out the histogram
    public void Zero()
    {
        lock (histoLock)
        {
            for (int ii = 0; ii < m_numBuckets; ii++)
                m_histogram[ii] = 0;
        }
    }
}

}
