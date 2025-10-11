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

namespace OpenSim.Framework.Security.DOSProtector.Options
{
    /// <summary>
    /// Configuration options for TokenBucketDOSProtector.
    /// Implements Token Bucket algorithm for smooth rate limiting with burst support.
    /// </summary>
    public class TokenBucketDosProtectorOptions : BasicDosProtectorOptions
    {
        /// <summary>
        /// Maximum number of tokens the bucket can hold (burst capacity).
        /// Allows short bursts of requests up to this limit.
        /// Default: 10 tokens
        /// </summary>
        public double BucketCapacity { get; set; } = 10.0;

        /// <summary>
        /// Rate at which tokens are added to the bucket (tokens per second).
        /// Determines the sustained request rate allowed.
        /// Default: 2 tokens/second (120 requests/minute)
        /// </summary>
        public double RefillRate { get; set; } = 2.0;

        /// <summary>
        /// Number of tokens consumed per request.
        /// Can be used to assign different costs to different request types.
        /// Default: 1 token per request
        /// </summary>
        public double TokenCost { get; set; } = 1.0;

        /// <summary>
        /// Constructor with sensible defaults for typical API protection
        /// </summary>
        public TokenBucketDosProtectorOptions()
        {
            ReportingName = "TokenBucket";
        }
    }
}
