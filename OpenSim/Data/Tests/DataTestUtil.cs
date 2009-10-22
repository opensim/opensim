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
using System.Text;
using OpenMetaverse;
using NUnit.Framework;

namespace OpenSim.Data.Tests
{
    /// <summary>
    /// Shared constants and methods for database unit tests.
    /// </summary>
    public class DataTestUtil
    {
        public const uint UNSIGNED_INTEGER_MIN = uint.MinValue;
        public const uint UNSIGNED_INTEGER_MAX = uint.MaxValue;

        public const int INTEGER_MIN = int.MinValue + 1; // Postgresql requires +1 to .NET int.MinValue
        public const int INTEGER_MAX = int.MaxValue;

        public const float FLOAT_MIN = float.MinValue * (1 - FLOAT_PRECISSION);
        public const float FLOAT_MAX = float.MaxValue * (1 - FLOAT_PRECISSION);
        public const float FLOAT_ACCURATE = 1.234567890123456789012f;
        public const float FLOAT_PRECISSION = 1E-5f; // Native MySQL is severly limited with floating accuracy

        public const double DOUBLE_MIN = -1E52 * (1 - DOUBLE_PRECISSION);
        public const double DOUBLE_MAX = 1E52 * (1 - DOUBLE_PRECISSION);
        public const double DOUBLE_ACCURATE = 1.2345678901234567890123456789012345678901234567890123f;
        public const double DOUBLE_PRECISSION = 1E-14; // Native MySQL is severly limited with double accuracy

        public const string STRING_MIN = "";
        public static string STRING_MAX(int length)
        {
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < length; i++)
            {
                stringBuilder.Append(i % 10);
            }
            return stringBuilder.ToString();
        }

        public static UUID UUID_MIN = new UUID("00000000-0000-0000-0000-000000000000");
        public static UUID UUID_MAX = new UUID("ffffffff-ffff-ffff-ffff-ffffffffffff");

        public const bool BOOLEAN_MIN = false;
        public const bool BOOLEAN_MAX = true;

        public static void AssertFloatEqualsWithTolerance(float expectedValue, float actualValue)
        {
            Assert.GreaterOrEqual(actualValue, expectedValue - Math.Abs(expectedValue) * FLOAT_PRECISSION);
            Assert.LessOrEqual(actualValue, expectedValue + Math.Abs(expectedValue) * FLOAT_PRECISSION);
        }

        public static void AssertDoubleEqualsWithTolerance(double expectedValue, double actualValue)
        {
            Assert.GreaterOrEqual(actualValue, expectedValue - Math.Abs(expectedValue) * DOUBLE_PRECISSION);
            Assert.LessOrEqual(actualValue, expectedValue + Math.Abs(expectedValue) * DOUBLE_PRECISSION);
        }
    }
}

