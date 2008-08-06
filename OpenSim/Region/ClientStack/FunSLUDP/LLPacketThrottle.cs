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
 *     * Neither the name of the OpenSim Project nor the
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
        private int m_currentBytesSent;

        public LLPacketThrottle(int Min, int Max, int Throttle)
        {
            m_maxAllowableThrottle = Max;
            m_minAllowableThrottle = Min;
            m_currentThrottle = Throttle;
            m_currentBytesSent = 0;
        }

        public void Reset()
        {
            m_currentBytesSent = 0;
        }

        public bool UnderLimit()
        {
            return (m_currentBytesSent < (m_currentThrottle/m_throttleTimeDivisor));
        }

        public int Add(int bytes)
        {
            m_currentBytesSent += bytes;
            return m_currentBytesSent;
        }

        // Properties
        public int Max
        {
            get { return m_maxAllowableThrottle; }
        }

        public int Min
        {
            get { return m_minAllowableThrottle; }
        }

        public int Throttle
        {
            get { return m_currentThrottle; }
            set
            {
                if (value > m_maxAllowableThrottle)
                {
                    m_currentThrottle = m_maxAllowableThrottle;
                }
                else if (value < m_minAllowableThrottle)
                {
                    m_currentThrottle = m_minAllowableThrottle;
                }
                else
                {
                    m_currentThrottle = value;
                }
            }
        }
    }
}