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

        public string Identifier { get; private set; }

        public int DebugLevel { get; set; }
        
        /// <summary>
        /// Number of ticks (ms) per quantum, drip rate and max burst
        /// are defined over this interval.
        /// </summary>
        protected const Int32 m_ticksPerQuantum = 1000;

        /// <summary>
        /// This is the number of quantums worth of packets that can
        /// be accommodated during a burst
        /// </summary>
        protected const Double m_quantumsPerBurst = 1.5;
                
        /// <summary>
        /// </summary>
        protected const Int32 m_minimumDripRate = LLUDPServer.MTU;
        
        /// <summary>Time of the last drip, in system ticks</summary>
        protected Int32 m_lastDrip;

        /// <summary>
        /// The number of bytes that can be sent at this moment. This is the
        /// current number of tokens in the bucket
        /// </summary>
        protected Int64 m_tokenCount;

        /// <summary>
        /// Map of children buckets and their requested maximum burst rate
        /// </summary>
        protected Dictionary<TokenBucket,Int64> m_children = new Dictionary<TokenBucket,Int64>();

        /// <summary>
        /// The parent bucket of this bucket, or null if this bucket has no
        /// parent. The parent bucket will limit the aggregate bandwidth of all
        /// of its children buckets
        /// </summary>
        public TokenBucket Parent { get; protected set; }

        /// <summary>
        /// Maximum burst rate in bytes per second. This is the maximum number
        /// of tokens that can accumulate in the bucket at any one time. This 
        /// also sets the total request for leaf nodes
        /// </summary>
        protected Int64 m_burstRate;
        public Int64 RequestedBurstRate
        {
            get { return m_burstRate; }
            set { m_burstRate = (value < 0 ? 0 : value); }
        }

        public Int64 BurstRate
        {
            get {
                double rate = RequestedBurstRate * BurstRateModifier();
                if (rate < m_minimumDripRate * m_quantumsPerBurst)
                    rate = m_minimumDripRate * m_quantumsPerBurst;
                
                return (Int64) rate;
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
        /// the system tick interval (typically around 15-22ms)
        /// FIXME: It is extremely confusing to be able to set a RequestedDripRate of 0 and then receive a positive
        /// number on get if TotalDripRequest is set.  This also stops us being able to retrieve the fact that
        /// RequestedDripRate is set to 0.  Really, this should always return m_dripRate and then we can get
        /// (m_dripRate == 0 ? TotalDripRequest : m_dripRate) on some other properties.
        /// </remarks>
        public virtual Int64 RequestedDripRate
        {
            get { return (m_dripRate == 0 ? TotalDripRequest : m_dripRate); }
            set 
            {
                if (value <= 0)
                    m_dripRate = 0;
                else if (MaxDripRate > 0 && value > MaxDripRate)
                    m_dripRate = MaxDripRate;
                else
                    m_dripRate = value;

                m_burstRate = (Int64)((double)m_dripRate * m_quantumsPerBurst);

                if (Parent != null)
                    Parent.RegisterRequest(this, m_dripRate);
            }
        }

        /// <summary>
        /// Gets the drip rate.
        /// </summary>
        /// <value>
        /// DripRate can never be above max drip rate or below min drip rate.
        /// If we are a child bucket then the drip rate return is modifed by the total load on the capacity of the
        /// parent bucket.
        /// </value>
        public virtual Int64 DripRate
        {
            get 
            {
                double rate;

                // FIXME: This doesn't properly work if we have a parent and children and a requested drip rate set
                // on ourselves which is not equal to the child drip rates.
                if (Parent == null)
                {
                    if (TotalDripRequest > 0)
                        rate = Math.Min(RequestedDripRate, TotalDripRequest);
                    else
                        rate = RequestedDripRate;
                }   
                else
                {
                    rate = (double)RequestedDripRate * Parent.DripRateModifier();
                }

                if (rate < m_minimumDripRate)
                    rate = m_minimumDripRate;
                else if (MaxDripRate > 0 && rate > MaxDripRate)
                    rate = MaxDripRate;

                return (Int64)rate;
            }
        }
        protected Int64 m_dripRate;

        // <summary>
        // The maximum rate for flow control. Drip rate can never be greater than this.
        // </summary>
        public Int64 MaxDripRate { get; set; }

        /// <summary>
        /// The current total of the requested maximum burst rates of children buckets.
        /// </summary>
        public Int64 TotalDripRequest { get; protected set; }

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="identifier">Identifier for this token bucket</param>
        /// <param name="parent">Parent bucket if this is a child bucket, or
        /// null if this is a root bucket</param>
        /// <param name="requestedDripRate">
        /// Requested rate that the bucket fills, in bytes per
        /// second. If zero, the bucket always remains full.
        /// </param>
        public TokenBucket(string identifier, TokenBucket parent, Int64 requestedDripRate, Int64 maxDripRate) 
        {
            Identifier = identifier;

            Parent = parent;
            RequestedDripRate = requestedDripRate;
            MaxDripRate = maxDripRate;
            m_lastDrip = Util.EnvironmentTickCount();
        }

        /// <summary>
        /// Compute a modifier for the MaxBurst rate. This is 1.0, meaning
        /// no modification if the requested bandwidth is less than the
        /// max burst bandwidth all the way to the root of the throttle
        /// hierarchy. However, if any of the parents is over-booked, then
        /// the modifier will be less than 1.
        /// </summary>
        protected double DripRateModifier()
        {
            Int64 driprate = DripRate;
            double modifier = driprate >= TotalDripRequest ? 1.0 : (double)driprate / (double)TotalDripRequest;

//            if (DebugLevel > 0)
//                m_log.DebugFormat(
//                    "[TOKEN BUCKET]: Returning drip modifier {0}/{1} = {2} from {3}", 
//                    driprate, TotalDripRequest, modifier, Identifier);

            return modifier;
        }

        /// <summary>
        /// </summary>
        protected double BurstRateModifier()
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
        public void RegisterRequest(TokenBucket child, Int64 request)
        {
            lock (m_children)
            {
                m_children[child] = request;

                TotalDripRequest = 0;
                foreach (KeyValuePair<TokenBucket, Int64> cref in m_children)
                    TotalDripRequest += cref.Value;
            }
            
            // Pass the new values up to the parent
            if (Parent != null)
            {
                Int64 effectiveDripRate;

                if (RequestedDripRate > 0)
                    effectiveDripRate = Math.Min(RequestedDripRate, TotalDripRequest);
                else
                    effectiveDripRate = TotalDripRequest;

                Parent.RegisterRequest(this, effectiveDripRate);
            }
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

                TotalDripRequest = 0;
                foreach (KeyValuePair<TokenBucket, Int64> cref in m_children)
                    TotalDripRequest += cref.Value;
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
        public bool RemoveTokens(Int64 amount)
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

        /// <summary>
        /// Deposit tokens into the bucket from a child bucket that did
        /// not use all of its available tokens
        /// </summary>
        protected void Deposit(Int64 count)
        {
            m_tokenCount += count;

            // Deposit the overflow in the parent bucket, this is how we share
            // unused bandwidth
            Int64 burstrate = BurstRate;
            if (m_tokenCount > burstrate)
                m_tokenCount = burstrate;
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
                m_log.WarnFormat("[TOKENBUCKET] something odd is happening and drip rate is 0 for {0}", Identifier);
                return;
            }
            
            // Determine the interval over which we are adding tokens, never add
            // more than a single quantum of tokens
            Int32 deltaMS = Math.Min(Util.EnvironmentTickCountSubtract(m_lastDrip), m_ticksPerQuantum);
            m_lastDrip = Util.EnvironmentTickCount();

            // This can be 0 in the very unusual case that the timer wrapped
            // It can be 0 if we try add tokens at a sub-tick rate
            if (deltaMS <= 0)
                return;

            Deposit(deltaMS * DripRate / m_ticksPerQuantum);
        }
    }

    public class AdaptiveTokenBucket : TokenBucket
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);               

        public bool AdaptiveEnabled { get; set; }

        /// <summary>
        /// Target drip rate for this bucket.
        /// </summary>
        /// <remarks>Usually set by the client.  If adaptive is enabled then throttles will increase until we reach this.</remarks>
        public Int64 TargetDripRate 
        { 
            get { return m_targetDripRate; }
            set 
            {
                m_targetDripRate = Math.Max(value, m_minimumFlow);
            }
        }
        protected Int64 m_targetDripRate;

        // <summary>
        // Adjust drip rate in response to network conditions. 
        // </summary>
        public virtual Int64 AdjustedDripRate
        {
            get { return m_dripRate; }
            set 
            {
                m_dripRate = OpenSim.Framework.Util.Clamp<Int64>(value, m_minimumFlow, TargetDripRate);
                m_burstRate = (Int64)((double)m_dripRate * m_quantumsPerBurst);

                if (Parent != null)
                    Parent.RegisterRequest(this, m_dripRate);
            }
        }
                
        /// <summary>
        /// The minimum rate for adaptive flow control. 
        /// </summary>
        protected Int64 m_minimumFlow = 32000;

        /// <summary>
        /// Constructor for the AdaptiveTokenBucket class
        /// <param name="identifier">Unique identifier for the client</param>
        /// <param name="parent">Parent bucket in the hierarchy</param>
        /// <param name="requestedDripRate"></param>
        /// <param name="maxDripRate">The ceiling rate for adaptation</param>
        /// <param name="minDripRate">The floor rate for adaptation</param>
        /// </summary>
        public AdaptiveTokenBucket(string identifier, TokenBucket parent, Int64 requestedDripRate, Int64 maxDripRate, Int64 minDripRate, bool enabled) 
            : base(identifier, parent, requestedDripRate, maxDripRate)
        {
            AdaptiveEnabled = enabled;

            if (AdaptiveEnabled)
            {
//                m_log.DebugFormat("[TOKENBUCKET]: Adaptive throttle enabled");
                m_minimumFlow = minDripRate;
                TargetDripRate = m_minimumFlow;
                AdjustedDripRate = m_minimumFlow;
            }
        }
                
        /// <summary>
        /// Reliable packets sent to the client for which we never received an ack adjust the drip rate down.
        /// <param name="packets">Number of packets that expired without successful delivery</param>
        /// </summary>
        public void ExpirePackets(Int32 packets)
        {
            if (AdaptiveEnabled)
            {
                if (DebugLevel > 0)
                    m_log.WarnFormat(
                        "[ADAPTIVEBUCKET] drop {0} by {1} expired packets for {2}", 
                        AdjustedDripRate, packets, Identifier);

                // AdjustedDripRate = (Int64) (AdjustedDripRate / Math.Pow(2,packets));

                // Compute the fallback solely on the rate allocated beyond the minimum, this
                // should smooth out the fallback to the minimum rate
                AdjustedDripRate = m_minimumFlow + (Int64) ((AdjustedDripRate - m_minimumFlow) / Math.Pow(2, packets));
            }
        }

        /// <summary>
        /// Reliable packets acked by the client adjust the drip rate up.
        /// <param name="packets">Number of packets successfully acknowledged</param>
        /// </summary>
        public void AcknowledgePackets(Int32 packets)
        {
            if (AdaptiveEnabled)
                AdjustedDripRate = AdjustedDripRate + packets * LLUDPServer.MTU;
        }

        /// <summary>
        /// Adjust the minimum flow level for the adaptive throttle, this will drop adjusted
        /// throttles back to the minimum levels
        /// <param>minDripRate--the new minimum flow</param>
        /// </summary>
        public void ResetMinimumAdaptiveFlow(Int64 minDripRate)
        {
            m_minimumFlow = minDripRate;
            TargetDripRate = m_minimumFlow;
            AdjustedDripRate = m_minimumFlow;
        }
    }
}