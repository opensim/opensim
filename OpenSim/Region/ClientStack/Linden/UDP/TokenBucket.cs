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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using OpenSim.Framework;

using log4net;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    /// <summary>
    /// A hierarchical token bucket for bandwidth throttling. See
    /// http://en.wikipedia.org/wiki/Token_bucket for more information
    /// </summary>
    public class TokenBucket
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static Int32 m_counter = 0;

//        private Int32 m_identifier;

        protected const float m_timeScale = 1e-3f;

        /// <summary>
        /// This is the number of m_minimumDripRate bytes
        /// allowed in a burst
        /// roughtly, with this settings, the maximum time system will take
        /// to recheck a bucket in ms
        ///
        /// </summary>
        protected const float m_quantumsPerBurst = 5;

        /// <summary>
        /// </summary>
        protected const float m_minimumDripRate = 1500;

        /// <summary>Time of the last drip</summary>
        protected double m_lastDrip;

        /// <summary>
        /// The number of bytes that can be sent at this moment. This is the
        /// current number of tokens in the bucket
        /// </summary>
        protected float m_tokenCount;

        /// <summary>
        /// Map of children buckets and their requested maximum burst rate
        /// </summary>

        protected Dictionary<TokenBucket, float> m_children = new Dictionary<TokenBucket, float>();

#region Properties

        /// <summary>
        /// The parent bucket of this bucket, or null if this bucket has no
        /// parent. The parent bucket will limit the aggregate bandwidth of all
        /// of its children buckets
        /// </summary>
        protected TokenBucket m_parent;
        public TokenBucket Parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }
        /// <summary>
        /// This is the maximum number
        /// of tokens that can accumulate in the bucket at any one time. This
        /// also sets the total request for leaf nodes
        /// </summary>
        protected float m_burst;

        protected float m_maxDripRate = 0;
        public virtual float MaxDripRate
        {
            get { return m_maxDripRate; }
            set { m_maxDripRate = value; }
        }

        public float RequestedBurst
        {
            get { return m_burst; }
            set {
                float rate = (value < 0 ? 0 : value);
                if (rate < 1.5f * m_minimumDripRate)
                    rate = 1.5f * m_minimumDripRate;
                else if (rate > m_minimumDripRate * m_quantumsPerBurst)
                    rate = m_minimumDripRate * m_quantumsPerBurst;

                m_burst = rate;
                }
        }

        public float Burst
        {
            get {
                float rate = RequestedBurst * BurstModifier();
                if (rate < m_minimumDripRate)
                    rate = m_minimumDripRate;
                return (float)rate;
            }
        }

        /// <summary>
        /// The requested drip rate for this particular bucket.
        /// </summary>
        /// <remarks>
        /// 0 then TotalDripRequest is used instead.
        /// Can never be above MaxDripRate.
        /// Tokens are added to the bucket at any time
        /// <seealso cref="RemoveTokens"/> is called, at the granularity of
        /// the system tick interval (typically around 15-22ms)</remarks>
        protected float m_dripRate;

        public float RequestedDripRate
        {
            get { return (m_dripRate == 0 ? m_totalDripRequest : m_dripRate); }
            set {
                m_dripRate = (value < 0 ? 0 : value);
                m_totalDripRequest = m_dripRate;

                if (m_parent != null)
                    m_parent.RegisterRequest(this,m_dripRate);
            }
        }

       public float DripRate
        {
            get {
                float rate = Math.Min(RequestedDripRate,TotalDripRequest);
                if (m_parent == null)
                    return rate;

                rate *= m_parent.DripRateModifier();
                if (rate < m_minimumDripRate)
                    rate = m_minimumDripRate;

                return (float)rate;
            }
        }

        /// <summary>
        /// The current total of the requested maximum burst rates of children buckets.
        /// </summary>
        protected float m_totalDripRequest;
        public float TotalDripRequest
        {
            get { return m_totalDripRequest; }
            set { m_totalDripRequest = value; }
        }

#endregion Properties

#region Constructor


        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="identifier">Identifier for this token bucket</param>
        /// <param name="parent">Parent bucket if this is a child bucket, or
        /// null if this is a root bucket</param>
        /// <param name="maxBurst">Maximum size of the bucket in bytes, or
        /// zero if this bucket has no maximum capacity</param>
        /// <param name="dripRate">Rate that the bucket fills, in bytes per
        /// second. If zero, the bucket always remains full</param>
        public TokenBucket(TokenBucket parent, float dripRate, float MaxBurst)
        {
            m_counter++;

            Parent = parent;
            RequestedDripRate = dripRate;
            RequestedBurst = MaxBurst;
            m_lastDrip = Util.GetTimeStampMS() + 100000.0; // skip first drip
        }

#endregion Constructor

        /// <summary>
        /// Compute a modifier for the MaxBurst rate. This is 1.0, meaning
        /// no modification if the requested bandwidth is less than the
        /// max burst bandwidth all the way to the root of the throttle
        /// hierarchy. However, if any of the parents is over-booked, then
        /// the modifier will be less than 1.
        /// </summary>
        protected float DripRateModifier()
        {
            float driprate = DripRate;
            return driprate >= TotalDripRequest ? 1.0f : (driprate / TotalDripRequest);
        }

        /// <summary>
        /// </summary>
        protected float BurstModifier()
        {
            // for now... burst rate is always m_quantumsPerBurst (constant)
            // larger than drip rate so the ratio of burst requests is the
            // same as the drip ratio
            return DripRateModifier();
        }

        /// <summary>
        /// Register drip rate requested by a child of this throttle. Pass the
        /// changes up the hierarchy.
        /// </summary>
        public void RegisterRequest(TokenBucket child, float request)
        {
            lock (m_children)
            {
                m_children[child] = request;

                m_totalDripRequest = 0;
                foreach (KeyValuePair<TokenBucket, float> cref in m_children)
                    m_totalDripRequest += cref.Value;
            }

            // Pass the new values up to the parent
            if (m_parent != null)
                m_parent.RegisterRequest(this, Math.Min(RequestedDripRate, TotalDripRequest));
        }

        /// <summary>
        /// Remove the rate requested by a child of this throttle. Pass the
        /// changes up the hierarchy.
        /// </summary>
        public void UnregisterRequest(TokenBucket child)
        {
            lock (m_children)
            {
                m_children.Remove(child);

                m_totalDripRequest = 0;
                foreach (KeyValuePair<TokenBucket, float> cref in m_children)
                    m_totalDripRequest += cref.Value;
            }

            // Pass the new values up to the parent
            if (Parent != null)
                Parent.RegisterRequest(this,Math.Min(RequestedDripRate, TotalDripRequest));
        }

        /// <summary>
        /// Remove a given number of tokens from the bucket
        /// </summary>
        /// <param name="amount">Number of tokens to remove from the bucket</param>
        /// <returns>True if the requested number of tokens were removed from
        /// the bucket, otherwise false</returns>
        public bool RemoveTokens(int amount)
        {
            // Deposit tokens for this interval
            Drip();

            // If we have enough tokens then remove them and return
            if (m_tokenCount - amount >= 0)
            {
                // we don't have to remove from the parent, the drip rate is already
                // reflective of the drip rate limits in the parent
                m_tokenCount -= amount;
                return true;
            }

            return false;
        }

        public bool CheckTokens(int amount)
        {
            return  (m_tokenCount - amount >= 0);
        }

        public int GetCatBytesCanSend(int timeMS)
        {
//            return (int)(m_tokenCount + timeMS * m_dripRate * 1e-3);
            return (int)(timeMS * DripRate * 1e-3);
        }

        /// <summary>
        /// Add tokens to the bucket over time. The number of tokens added each
        /// call depends on the length of time that has passed since the last
        /// call to Drip
        /// </summary>
        /// <returns>True if tokens were added to the bucket, otherwise false</returns>
        protected void Drip()
        {
            // This should never happen... means we are a leaf node and were created
            // with no drip rate...
            if (DripRate == 0)
            {
                m_log.WarnFormat("[TOKENBUCKET] something odd is happening and drip rate is 0 for {0}", m_counter);
                return;
            }

            double now = Util.GetTimeStampMS();
            double deltaMS = now - m_lastDrip;
            m_lastDrip = now;

            if (deltaMS <= 0)
                return;

            m_tokenCount += (float)deltaMS * DripRate * m_timeScale;

            float burst = Burst;
            if (m_tokenCount > burst)
                m_tokenCount = burst;
        }
    }

    public class AdaptiveTokenBucket : TokenBucket
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public bool AdaptiveEnabled { get; set; }

        /// <summary>
        /// The minimum rate for flow control. Minimum drip rate is one
        /// packet per second.
        /// </summary>

        protected const float m_minimumFlow = 50000;

        // <summary>
        // The maximum rate for flow control. Drip rate can never be
        // greater than this.
        // </summary>

        public override float MaxDripRate
        {
            get { return (m_maxDripRate == 0 ? m_totalDripRequest : m_maxDripRate); }
            set
            {
                m_maxDripRate = (value == 0 ? m_totalDripRequest : Math.Max(value, m_minimumFlow));
            }
        }

        private bool m_enabled = false;

        // <summary>
        // Adjust drip rate in response to network conditions.
        // </summary>
        public float AdjustedDripRate
        {
            get { return m_dripRate; }
            set
            {
                m_dripRate = OpenSim.Framework.Util.Clamp<float>(value, m_minimumFlow, MaxDripRate);

                if (m_parent != null)
                    m_parent.RegisterRequest(this, m_dripRate);
            }
        }


        // <summary>
        //
        // </summary>
        public AdaptiveTokenBucket(TokenBucket parent, float maxDripRate, float maxBurst, bool enabled)
            : base(parent, maxDripRate, maxBurst)
        {
            m_enabled = enabled;

            m_maxDripRate = (maxDripRate == 0 ? m_totalDripRequest : Math.Max(maxDripRate, m_minimumFlow));

            if (enabled)
                m_dripRate = m_maxDripRate * .5f;
            else
                m_dripRate = m_maxDripRate;
            if (m_parent != null)
                m_parent.RegisterRequest(this, m_dripRate);
        }

        /// <summary>
        /// Reliable packets sent to the client for which we never received an ack adjust the drip rate down.
        /// <param name="packets">Number of packets that expired without successful delivery</param>
        /// </summary>
        public void ExpirePackets(Int32 count)
        {
            // m_log.WarnFormat("[ADAPTIVEBUCKET] drop {0} by {1} expired packets",AdjustedDripRate,count);
            if (m_enabled)
                AdjustedDripRate = (Int64)(AdjustedDripRate / Math.Pow(2, count));
        }

        // <summary>
        //
        // </summary>
        public void AcknowledgePackets(Int32 count)
        {
            if (m_enabled)
                AdjustedDripRate = AdjustedDripRate + count;
        }
    }
}
