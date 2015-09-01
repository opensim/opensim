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
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Tests.Common;

namespace OpenSim.Data.Tests
{
    public static class Constraints
    {
        //This is here because C# has a gap in the language, you can't infer type from a constructor
        public static PropertyCompareConstraint<T> PropertyCompareConstraint<T>(T expected)
        {
            return new PropertyCompareConstraint<T>(expected);
        }
    }

    public class PropertyCompareConstraint<T> : NUnit.Framework.Constraints.Constraint
    {
        private readonly object _expected;
        //the reason everywhere uses propertyNames.Reverse().ToArray() is because the stack is backwards of the order we want to display the properties in.
        private string failingPropertyName = string.Empty;
        private object failingExpected;
        private object failingActual;

        public PropertyCompareConstraint(T expected)
        {
            _expected = expected;
        }

        public override bool Matches(object actual)
        {
            return ObjectCompare(_expected, actual, new Stack<string>());
        }

        private bool ObjectCompare(object expected, object actual, Stack<string> propertyNames)
        {
            //If they are both null, they are equal
            if (actual == null && expected == null)
                return true;

            //If only one is null, then they aren't
            if (actual == null || expected == null)
            {
                failingPropertyName = string.Join(".", propertyNames.Reverse().ToArray());
                failingActual = actual;
                failingExpected = expected;
                return false;
            }

            //prevent loops...
            if (propertyNames.Count > 50)
            {
                failingPropertyName = string.Join(".", propertyNames.Reverse().ToArray());
                failingActual = actual;
                failingExpected = expected;
                return false;
            }

            if (actual.GetType() != expected.GetType())
            {
                propertyNames.Push("GetType()");
                failingPropertyName = string.Join(".", propertyNames.Reverse().ToArray());
                propertyNames.Pop();
                failingActual = actual.GetType();
                failingExpected = expected.GetType();
                return false;
            }

            if (actual.GetType() == typeof(Color))
            {
                Color actualColor = (Color) actual;
                Color expectedColor = (Color) expected;
                if (actualColor.R != expectedColor.R)
                {
                    propertyNames.Push("R");
                    failingPropertyName = string.Join(".", propertyNames.Reverse().ToArray());
                    propertyNames.Pop();
                    failingActual = actualColor.R;
                    failingExpected = expectedColor.R;
                    return false;
                }
                if (actualColor.G != expectedColor.G)
                {
                    propertyNames.Push("G");
                    failingPropertyName = string.Join(".", propertyNames.Reverse().ToArray());
                    propertyNames.Pop();
                    failingActual = actualColor.G;
                    failingExpected = expectedColor.G;
                    return false;
                }
                if (actualColor.B != expectedColor.B)
                {
                    propertyNames.Push("B");
                    failingPropertyName = string.Join(".", propertyNames.Reverse().ToArray());
                    propertyNames.Pop();
                    failingActual = actualColor.B;
                    failingExpected = expectedColor.B;
                    return false;
                }
                if (actualColor.A != expectedColor.A)
                {
                    propertyNames.Push("A");
                    failingPropertyName = string.Join(".", propertyNames.Reverse().ToArray());
                    propertyNames.Pop();
                    failingActual = actualColor.A;
                    failingExpected = expectedColor.A;
                    return false;
                }
                return true;
            }

            IComparable comp = actual as IComparable;
            if (comp != null)
            {
                if (comp.CompareTo(expected) != 0)
                {
                    failingPropertyName = string.Join(".", propertyNames.Reverse().ToArray());
                    failingActual = actual;
                    failingExpected = expected;
                    return false;
                }
                return true;
            }

            //Now try the much more annoying IComparable<T>
            Type icomparableInterface = actual.GetType().GetInterface("IComparable`1");
            if (icomparableInterface != null)
            {
                int result = (int)icomparableInterface.GetMethod("CompareTo").Invoke(actual, new[] { expected });
                if (result != 0)
                {
                    failingPropertyName = string.Join(".", propertyNames.Reverse().ToArray());
                    failingActual = actual;
                    failingExpected = expected;
                    return false;
                }
                return true;
            }

            IEnumerable arr = actual as IEnumerable;
            if (arr != null)
            {
                List<object> actualList = arr.Cast<object>().ToList();
                List<object> expectedList = ((IEnumerable)expected).Cast<object>().ToList();
                if (actualList.Count != expectedList.Count)
                {
                    propertyNames.Push("Count");
                    failingPropertyName = string.Join(".", propertyNames.Reverse().ToArray());
                    failingActual = actualList.Count;
                    failingExpected = expectedList.Count;
                    propertyNames.Pop();
                    return false;
                }
                //actualList and expectedList should be the same size.
                for (int i = 0; i < actualList.Count; i++)
                {
                    propertyNames.Push("[" + i + "]");
                    if (!ObjectCompare(expectedList[i], actualList[i], propertyNames))
                        return false;
                    propertyNames.Pop();
                }
                //Everything seems okay...
                return true;
            }

            //Skip static properties.  I had a nasty problem comparing colors because of all of the public static colors.
            PropertyInfo[] properties = expected.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                if (ignores.Contains(property.Name))
                    continue;

                object actualValue = property.GetValue(actual, null);
                object expectedValue = property.GetValue(expected, null);

                propertyNames.Push(property.Name);
                if (!ObjectCompare(expectedValue, actualValue, propertyNames))
                    return false;
                propertyNames.Pop();
            }

            return true;
        }

        public override void WriteDescriptionTo(MessageWriter writer)
        {
            writer.WriteExpectedValue(failingExpected);
        }

        public override void WriteActualValueTo(MessageWriter writer)
        {
            writer.WriteActualValue(failingActual);
            writer.WriteLine();
            writer.Write("  On Property: " + failingPropertyName);
        }

        //These notes assume the lambda: (x=>x.Parent.Value)
        //ignores should really contain like a fully dotted version of the property name, but I'm starting with small steps
        readonly List<string> ignores = new List<string>();
        public PropertyCompareConstraint<T> IgnoreProperty(Expression<Func<T, object>> func)
        {
            Expression express = func.Body;
            PullApartExpression(express);

            return this;
        }

        private void PullApartExpression(Expression express)
        {
            //This deals with any casts... like implicit casts to object.  Not all UnaryExpression are casts, but this is a first attempt.
            if (express is UnaryExpression)
                PullApartExpression(((UnaryExpression)express).Operand);
            if (express is MemberExpression)
            {
                //If the inside of the lambda is the access to x, we've hit the end of the chain.
                //   We should track by the fully scoped parameter name, but this is the first rev of doing this.
                ignores.Add(((MemberExpression)express).Member.Name);
            }
        }
    }

    [TestFixture]
    public class PropertyCompareConstraintTest : OpenSimTestCase
    {
        public class HasInt
        {
            public int TheValue { get; set; }
        }

        [Test]
        public void IntShouldMatch()
        {
            HasInt actual = new HasInt { TheValue = 5 };
            HasInt expected = new HasInt { TheValue = 5 };
            var constraint = Constraints.PropertyCompareConstraint(expected);

            Assert.That(constraint.Matches(actual), Is.True);
        }

        [Test]
        public void IntShouldNotMatch()
        {
            HasInt actual = new HasInt { TheValue = 5 };
            HasInt expected = new HasInt { TheValue = 4 };
            var constraint = Constraints.PropertyCompareConstraint(expected);

            Assert.That(constraint.Matches(actual), Is.False);
        }


        [Test]
        public void IntShouldIgnore()
        {
            HasInt actual = new HasInt { TheValue = 5 };
            HasInt expected = new HasInt { TheValue = 4 };
            var constraint = Constraints.PropertyCompareConstraint(expected).IgnoreProperty(x => x.TheValue);

            Assert.That(constraint.Matches(actual), Is.True);
        }

        [Test]
        public void AssetShouldMatch()
        {
            UUID uuid1 = UUID.Random();
            AssetBase actual = new AssetBase(uuid1, "asset one", (sbyte)AssetType.Texture, UUID.Zero.ToString());
            AssetBase expected = new AssetBase(uuid1, "asset one", (sbyte)AssetType.Texture, UUID.Zero.ToString());

            var constraint = Constraints.PropertyCompareConstraint(expected);

            Assert.That(constraint.Matches(actual), Is.True);
        }

        [Test]
        public void AssetShouldNotMatch()
        {
            UUID uuid1 = UUID.Random();
            AssetBase actual = new AssetBase(uuid1, "asset one", (sbyte)AssetType.Texture, UUID.Zero.ToString());
            AssetBase expected = new AssetBase(UUID.Random(), "asset one", (sbyte)AssetType.Texture, UUID.Zero.ToString());

            var constraint = Constraints.PropertyCompareConstraint(expected);

            Assert.That(constraint.Matches(actual), Is.False);
        }

        [Test]
        public void AssetShouldNotMatch2()
        {
            UUID uuid1 = UUID.Random();
            AssetBase actual = new AssetBase(uuid1, "asset one", (sbyte)AssetType.Texture, UUID.Zero.ToString());
            AssetBase expected = new AssetBase(uuid1, "asset two", (sbyte)AssetType.Texture, UUID.Zero.ToString());

            var constraint = Constraints.PropertyCompareConstraint(expected);

            Assert.That(constraint.Matches(actual), Is.False);
        }

        [Test]
        public void UUIDShouldMatch()
        {
            UUID uuid1 = UUID.Random();
            UUID uuid2 = UUID.Parse(uuid1.ToString());

            var constraint = Constraints.PropertyCompareConstraint(uuid1);

            Assert.That(constraint.Matches(uuid2), Is.True);
        }

        [Test]
        public void UUIDShouldNotMatch()
        {
            UUID uuid1 = UUID.Random();
            UUID uuid2 = UUID.Random();

            var constraint = Constraints.PropertyCompareConstraint(uuid1);

            Assert.That(constraint.Matches(uuid2), Is.False);
        }

        [Test]
        public void TestColors()
        {
            Color actual = Color.Red;
            Color expected = Color.FromArgb(actual.A, actual.R, actual.G, actual.B);

            var constraint = Constraints.PropertyCompareConstraint(expected);

            Assert.That(constraint.Matches(actual), Is.True);
        }

        [Test]
        public void ShouldCompareLists()
        {
            List<int> expected = new List<int> { 1, 2, 3 };
            List<int> actual = new List<int> { 1, 2, 3 };

            var constraint = Constraints.PropertyCompareConstraint(expected);
            Assert.That(constraint.Matches(actual), Is.True);
        }


        [Test]
        public void ShouldFailToCompareListsThatAreDifferent()
        {
            List<int> expected = new List<int> { 1, 2, 3 };
            List<int> actual = new List<int> { 1, 2, 4 };

            var constraint = Constraints.PropertyCompareConstraint(expected);
            Assert.That(constraint.Matches(actual), Is.False);
        }

        [Test]
        public void ShouldFailToCompareListsThatAreDifferentLengths()
        {
            List<int> expected = new List<int> { 1, 2, 3 };
            List<int> actual = new List<int> { 1, 2 };

            var constraint = Constraints.PropertyCompareConstraint(expected);
            Assert.That(constraint.Matches(actual), Is.False);
        }

        public class Recursive
        {
            public Recursive Other { get; set; }
        }

        [Test]
        public void ErrorsOutOnRecursive()
        {
            Recursive parent = new Recursive();
            Recursive child = new Recursive();
            parent.Other = child;
            child.Other = parent;

            var constraint = Constraints.PropertyCompareConstraint(child);
            Assert.That(constraint.Matches(child), Is.False);
        }
    }
}