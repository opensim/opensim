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
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Tests.Common;

namespace OpenSim.Data.Tests
{
    //This is generic so that the lambda expressions will work right in IDEs.
    public class PropertyScrambler<T>
    {
        readonly System.Collections.Generic.List<string> membersToNotScramble = new List<string>();

        private void AddExpressionToNotScrableList(Expression expression)
        {
            UnaryExpression unaryExpression = expression as UnaryExpression;
            if (unaryExpression != null)
            {
                AddExpressionToNotScrableList(unaryExpression.Operand);
                return;
            }

            MemberExpression memberExpression = expression as MemberExpression;
            if (memberExpression != null)
            {
                if (!(memberExpression.Member is PropertyInfo))
                {
                    throw new NotImplementedException("I don't know how deal with a MemberExpression that is a " + expression.Type);
                }
                membersToNotScramble.Add(memberExpression.Member.Name);
                return;
            }

            throw new NotImplementedException("I don't know how to parse a " + expression.Type);
        }

        public PropertyScrambler<T> DontScramble(Expression<Func<T, object>> expression)
        {
            AddExpressionToNotScrableList(expression.Body);
            return this;
        }

        public void Scramble(T obj)
        {
            internalScramble(obj);
        }

        private void internalScramble(object obj)
        {
            PropertyInfo[] properties = obj.GetType().GetProperties();
            foreach (var property in properties)
            {
                //Skip indexers of classes.  We will assume that everything that has an indexer
                //  is also IEnumberable.  May not always be true, but should be true normally.
                if (property.GetIndexParameters().Length > 0)
                    continue;

                RandomizeProperty(obj, property, null);
            }
            //Now if it implments IEnumberable, it's probably some kind of list, so we should randomize
            //  everything inside of it.
            IEnumerable enumerable = obj as IEnumerable;
            if (enumerable != null)
            {
                foreach (object value in enumerable)
                {
                    internalScramble(value);
                }
            }
        }

        private readonly Random random = new Random();
        private void RandomizeProperty(object obj, PropertyInfo property, object[] index)
        {//I'd like a better way to compare, but I had lots of problems with InventoryFolderBase because the ID is inherited.
            if (membersToNotScramble.Contains(property.Name))
                return;
            Type t = property.PropertyType;
            if (!property.CanWrite)
                return;
            object value = property.GetValue(obj, index);
            if (value == null)
                return;

            if (t == typeof(string))
                property.SetValue(obj, RandomName(), index);
            else if (t == typeof(UUID))
                property.SetValue(obj, UUID.Random(), index);
            else if (t == typeof(sbyte))
                property.SetValue(obj, (sbyte)random.Next(sbyte.MinValue, sbyte.MaxValue), index);
            else if (t == typeof(short))
                property.SetValue(obj, (short)random.Next(short.MinValue, short.MaxValue), index);
            else if (t == typeof(int))
                property.SetValue(obj, random.Next(), index);
            else if (t == typeof(long))
                property.SetValue(obj, random.Next() * int.MaxValue, index);
            else if (t == typeof(byte))
                property.SetValue(obj, (byte)random.Next(byte.MinValue, byte.MaxValue), index);
            else if (t == typeof(ushort))
                property.SetValue(obj, (ushort)random.Next(ushort.MinValue, ushort.MaxValue), index);
            else if (t == typeof(uint))
                property.SetValue(obj, Convert.ToUInt32(random.Next()), index);
            else if (t == typeof(ulong))
                property.SetValue(obj, Convert.ToUInt64(random.Next()) * Convert.ToUInt64(UInt32.MaxValue), index);
            else if (t == typeof(bool))
                property.SetValue(obj, true, index);
            else if (t == typeof(byte[]))
            {
                byte[] bytes = new byte[30];
                random.NextBytes(bytes);
                property.SetValue(obj, bytes, index);
            }
            else
                internalScramble(value);
        }

        private string RandomName()
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
    public class PropertyScramblerTests : OpenSimTestCase
    {
        [Test]
        public void TestScramble()
        {
            AssetBase actual = new AssetBase(UUID.Random(), "asset one", (sbyte)AssetType.Texture, UUID.Zero.ToString());
            new PropertyScrambler<AssetBase>().Scramble(actual);
        }

        [Test]
        public void DontScramble()
        {
            UUID uuid = UUID.Random();
            AssetBase asset = new AssetBase(uuid, "asset", (sbyte)AssetType.Texture, UUID.Zero.ToString());
            new PropertyScrambler<AssetBase>()
                .DontScramble(x => x.Metadata)
                .DontScramble(x => x.FullID)
                .DontScramble(x => x.ID)
                .Scramble(asset);
            Assert.That(asset.FullID, Is.EqualTo(uuid));
        }
    }
}