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

namespace OpenSim.Framework
{
    /// <summary>
    /// Utility class that is used to find small prime numbers and test is number prime number.
    /// </summary>
    public static class PrimeNumberHelper
    {
        /// <summary>
        /// Precalculated prime numbers.
        /// </summary>
        private static readonly int[] Primes = new int[]
                                                   {
                                                       3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239,
                                                       293, 353, 431, 521, 631, 761, 919, 1103, 1327, 1597, 1931, 2333,
                                                       2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
                                                       17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431,
                                                       90523, 108631, 130363, 156437, 187751, 225307, 270371, 324449,
                                                       389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
                                                       1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559,
                                                       5999471, 7199369
                                                   };

        /// <summary>
        /// Get prime number that is equal or larger than <see cref="min"/>.
        /// </summary>
        /// <param name="min">
        /// Minimal returned prime number.
        /// </param>
        /// <returns>
        /// Primer number that is equal or larger than <see cref="min"/>. If <see cref="min"/> is too large, return -1.
        /// </returns>
        public static int GetPrime(int min)
        {
            if (min <= 2)
                return 2;

            if (Primes[ Primes.Length - 1 ] < min)
            {
                for (int i = min | 1 ; i < 0x7FFFFFFF ; i += 2)
                {
                    if (IsPrime(i))
                        return i;
                }

                return -1;
            }

            for (int i = Primes.Length - 2 ; i >= 0 ; i--)
            {
                if (min == Primes[ i ])
                    return min;

                if (min > Primes[ i ])
                    return Primes[ i + 1 ];
            }

            return 2;
        }

        /// <summary>
        /// Just basic Sieve of Eratosthenes prime number test.
        /// </summary>
        /// <param name="candinate">
        /// Number that is tested.
        /// </param>
        /// <returns>
        /// true, if <see cref="candinate"/> is prime number; otherwise false.
        /// </returns>
        public static bool IsPrime(int candinate)
        {
            if ((candinate & 1) == 0)

                // Even number - only prime if 2
                return candinate == 2;

            int upperBound = (int) Math.Sqrt(candinate);
            for (int i = 3 ; i < upperBound ; i += 2)
            {
                if (candinate % i == 0)
                    return false;
            }

            return true;
        }
    }
}
