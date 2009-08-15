using System;
using System.Collections;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data.Tests
{
    public static class ScrambleForTesting
    {
        private static readonly Random random = new Random();
        public static void Scramble(object obj)
        {
            PropertyInfo[] properties = obj.GetType().GetProperties();
            foreach (var property in properties)
            {
                //Skip indexers of classes.  We will assume that everything that has an indexer
                //  is also IEnumberable.  May not always be true, but should be true normally.
                if(property.GetIndexParameters().Length > 0)
                    continue;

                RandomizeProperty(obj, property, null);
            }
            //Now if it implments IEnumberable, it's probably some kind of list, so we should randomize
            //  everything inside of it.
            IEnumerable enumerable = obj as IEnumerable;
            if(enumerable != null)
            {
                foreach (object value in enumerable)
                {
                    Scramble(value);
                }
            }
        }

        private static void RandomizeProperty(object obj, PropertyInfo property, object[] index)
        {
            Type t = property.PropertyType;
            if (!property.CanWrite)
                return;
            object value = property.GetValue(obj, index);
            if (value == null)
                return;

            if (t == typeof (string))
                property.SetValue(obj, RandomName(), index);
            else if (t == typeof (UUID))
                property.SetValue(obj, UUID.Random(), index);
            else if (t == typeof (sbyte))
                property.SetValue(obj, (sbyte)random.Next(sbyte.MinValue, sbyte.MaxValue), index);
            else if (t == typeof (short))
                property.SetValue(obj, (short)random.Next(short.MinValue, short.MaxValue), index);
            else if (t == typeof (int))
                property.SetValue(obj, random.Next(), index);
            else if (t == typeof (long))
                property.SetValue(obj, random.Next() * int.MaxValue, index);
            else if (t == typeof (byte))
                property.SetValue(obj, (byte)random.Next(byte.MinValue, byte.MaxValue), index);
            else if (t == typeof (ushort))
                property.SetValue(obj, (ushort)random.Next(ushort.MinValue, ushort.MaxValue), index);
            else if (t == typeof (uint))
                property.SetValue(obj, Convert.ToUInt32(random.Next()), index);
            else if (t == typeof (ulong))
                property.SetValue(obj, Convert.ToUInt64(random.Next()) * Convert.ToUInt64(UInt32.MaxValue), index);
            else if (t == typeof (bool))
                property.SetValue(obj, true, index);
            else if (t == typeof (byte[]))
            {
                byte[] bytes = new byte[30];
                random.NextBytes(bytes);
                property.SetValue(obj, bytes, index);
            }
            else
                Scramble(value);
        }

        private static string RandomName()
        {
            StringBuilder name = new StringBuilder();
            int size = random.Next(5, 12);
            for (int i = 0; i < size; i++)
            {
                char ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * random.NextDouble() + 65)));
                name.Append(ch);
            }
            return name.ToString();
        }
    }

    [TestFixture]
    public class ScrableForTestingTest
    {
        [Test]
        public void TestScramble()
        {
            AssetBase actual = new AssetBase(UUID.Random(), "asset one");
            ScrambleForTesting.Scramble(actual);
        }
    }
}