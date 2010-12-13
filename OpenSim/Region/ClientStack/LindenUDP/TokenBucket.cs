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

namespace OpenSim.Region.ClientStack.LindenUDP
{
    /// <summary>
    /// A hierarchical token bucket for bandwidth throttling. See
    /// http://en.wikipedia.org/wiki/Token_bucket for more information
    /// </summary>
    public class TokenBucket
    {
        /// <summary>Parent bucket to this bucket, or null if this is a root
        /// bucket</summary>
        TokenBucket parent;
        /// <summary>Size of the bucket in bytes. If zero, the bucket has 
        /// infinite capacity</summary>
        int maxBurst;
        /// <summary>Rate that the bucket fills, in bytes per millisecond. If
        /// zero, the bucket always remains full</summary>
        int tokensPerMS;
        /// <summary>Number of tokens currently in the bucket</summary>
        int content;
        /// <summary>Time of the last drip, in system ticks</summary>
        int lastDrip;

        #region Properties

        /// <summary>
        /// The parent bucket of this bucket, or null if this bucket has no
        /// parent. The parent bucket will limit the aggregate bandwidth of all
        /// of its children buckets
        /// </summary>
        public TokenBucket Parent
        {
            get { return parent; }
        }

        /// <summary>
        /// Maximum burst rate in bytes per second. This is the maximum number
        /// of tokens that can accumulate in the bucket at any one time
        /// </summary>
        public int MaxBurst
        {
            get { return maxBurst; }
            set { maxBurst = (value >= 0 ? value : 0); }
        }

        /// <summary>
        /// The speed limit of this bucket in bytes per second. This is the
        /// number of tokens that are added to the bucket per second
        /// </summary>
        /// <remarks>Tokens are added to the bucket any time 
        /// <seealso cref="RemoveTokens"/> is called, at the granularity of
        /// the system tick interval (typically around 15-22ms)</remarks>
        public int DripRate
        {
            get { return tokensPerMS * 1000; }
            set
            {
                if (value == 0)
                    tokensPerMS = 0;
                else
                {
                    int bpms = (int)((float)value / 1000.0f);

                    if (bpms <= 0)
                        tokensPerMS = 1; // 1 byte/ms is the minimum granularity
                    else
                        tokensPerMS = bpms;
                }
            }
        }

        /// <summary>
        /// The speed limit of this bucket in bytes per millisecond
        /// </summary>
        public int DripPerMS
        {
            get { return tokensPerMS; }
        }

        /// <summary>
        /// The number of bytes that can be sent at this moment. This is the
        /// current number of tokens in the bucket
        /// <remarks>If this bucket has a parent bucket that does not have
        /// enough tokens for a request, <seealso cref="RemoveTokens"/> will 
        /// return false regardless of the content of this bucket</remarks>
        /// </summary>
        public int Content
        {
            get { return content; }
        }

        #endregion Properties

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="parent">Parent bucket if this is a child bucket, or
        /// null if this is a root bucket</param>
        /// <param name="maxBurst">Maximum size of the bucket in bytes, or
        /// zero if this bucket has no maximum capacity</param>
        /// <param name="dripRate">Rate that the bucket fills, in bytes per
        /// second. If zero, the bucket always remains full</param>
        public TokenBucket(TokenBucket parent, int maxBurst, int dripRate)
        {
            this.parent = parent;
            MaxBurst = maxBurst;
            DripRate = dripRate;
            lastDrip = Environment.TickCount & Int32.MaxValue;
        }

        /// <summary>
        /// Remove a given number of tokens from the bucket
        /// </summary>
        /// <param name="amount">Number of tokens to remove from the bucket</param>
        /// <returns>True if the requested number of tokens were removed from
        /// the bucket, otherwise false</returns>
        public bool RemoveTokens(int amount)
        {
            bool dummy;
            return RemoveTokens(amount, out dummy);
        }

        /// <summary>
        /// Remove a given number of tokens from the bucket
        /// </summary>
        /// <param name="amount">Number of tokens to remove from the bucket</param>
        /// <param name="dripSucceeded">True if tokens were added to the bucket
        /// during this call, otherwise false</param>
        /// <returns>True if the requested number of tokens were removed from
        /// the bucket, otherwise false</returns>
        public bool RemoveTokens(int amount, out bool dripSucceeded)
        {
            if (maxBurst == 0)
            {
                dripSucceeded = true;
                return true;
            }

            dripSucceeded = Drip();

            if (content - amount >= 0)
            {
                if (parent != null && !parent.RemoveTokens(amount))
                    return false;

                content -= amount;
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Add tokens to the bucket over time. The number of tokens added each
        /// call depends on the length of time that has passed since the last 
        /// call to Drip
        /// </summary>
        /// <returns>True if tokens were added to the bucket, otherwise false</returns>
        public bool Drip()
        {
            if (tokensPerMS == 0)
            {
                content = maxBurst;
                return true;
            }
            else
            {
                int now = Environment.TickCount & Int32.MaxValue;
                int deltaMS = now - lastDrip;

                if (deltaMS <= 0)
                {
                    if (deltaMS < 0)
                        lastDrip = now;
                    return false;
                }

                int dripAmount = deltaMS * tokensPerMS;

                content = Math.Min(content + dripAmount, maxBurst);
                lastDrip = now;

                return true;
            }
        }
    }
}
