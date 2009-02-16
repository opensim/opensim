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
        public const uint UNSIGNED_INTEGER_MAX = uint.MaxValue / 2; // NHibernate does not support unsigned integer range.

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
