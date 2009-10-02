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

namespace OpenSim.Region.ClientStack.LindenUDP
{
    public class LLPacketThrottle
    {
        private readonly int m_maxAllowableThrottle;
        private readonly int m_minAllowableThrottle;
        private int m_currentThrottle;
        private const int m_throttleTimeDivisor = 7;
        private int m_currentBitsSent;
        private int m_throttleBits;
        
        /// <value>
        /// Value with which to multiply all the throttle fields
        /// </value>
        private float m_throttleMultiplier;
        
        public int Max
        {
            get { return m_maxAllowableThrottle; }
        }

        public int Min
        {
            get { return m_minAllowableThrottle; }
        }
        
        public int Current
        {
            get { return m_currentThrottle; }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="throttle"></param>
        /// <param name="throttleMultiplier">
        /// A parameter that's ends up multiplying all throttle settings.  An alternative solution would have been 
        /// to multiply all the parameters by this before giving them to the constructor.  But doing it this way
        /// represents the fact that the multiplier is a hack that pumps data to clients much faster than the actual
        /// settings that we are given.
        /// </param>
        public LLPacketThrottle(int min, int max, int throttle, float throttleMultiplier)
        {
            m_throttleMultiplier = throttleMultiplier;
            m_maxAllowableThrottle = max;
            m_minAllowableThrottle = min;
            m_currentThrottle = throttle;
            m_currentBitsSent = 0;

            CalcBits();
        }

        /// <summary>
        /// Calculate the actual throttle required.
        /// </summary>
        private void CalcBits()
        {
            m_throttleBits = (int)((float)m_currentThrottle * m_throttleMultiplier / (float)m_throttleTimeDivisor);
        }

        public void Reset()
        {
            m_currentBitsSent = 0;
        }

        public bool UnderLimit()
        {
            return m_currentBitsSent < m_throttleBits;
        }
        
        public int AddBytes(int bytes)
        {
            m_currentBitsSent += bytes * 8;
            return m_currentBitsSent;
        }

        public int Throttle
        {
            get { return m_currentThrottle; }
            set
            {
                if (value < m_minAllowableThrottle)
                {
                    m_currentThrottle = m_minAllowableThrottle;
                }
                else if (value > m_maxAllowableThrottle)
                {
                    m_currentThrottle = m_maxAllowableThrottle;
                }
                else
                {
                    m_currentThrottle = value;
                }

                CalcBits();
            }
        }
    }
}
