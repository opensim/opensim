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

using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OpenSim.Region.ScriptEngine.Shared.CodeTools;
using OpenSim.Tests.Common;

namespace OpenSim.Region.ScriptEngine.Shared.CodeTools.Tests
{
    /// <summary>
    /// Tests the LSL compiler, both the code generation and transformation.
    /// Each test has some LSL code as input and C# code as expected output.
    /// The generated C# code is compared against the expected C# code.
    /// </summary>
    [TestFixture]
    public class CSCodeGeneratorTest : OpenSimTestCase
    {
        [Test]
        public void TestDefaultState()
        {
            TestHelpers.InMethod();

            string input = @"default
{
    state_entry()
    {
    }
}
";
            string expected =
                "\n        public void default_event_state_entry()" +
                "\n        {" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestCustomState()
        {
            TestHelpers.InMethod();

            string input = @"default
{
    state_entry()
    {
    }
}

state another_state
{
    no_sensor()
    {
    }
}
";
            string expected =
                "\n        public void default_event_state_entry()" +
                "\n        {" +
                "\n        }" +
                "\n        public void another_state_event_no_sensor()" +
                "\n        {" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestEventWithArguments()
        {
            TestHelpers.InMethod();

            string input = @"default
{
    at_rot_target(integer tnum, rotation targetrot, rotation ourrot)
    {
    }
}
";
            string expected =
                "\n        public void default_event_at_rot_target(LSL_Types.LSLInteger tnum, LSL_Types.Quaternion targetrot, LSL_Types.Quaternion ourrot)" +
                "\n        {" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestIntegerDeclaration()
        {
            TestHelpers.InMethod();

            string input = @"default
{
    touch_start(integer num_detected)
    {
        integer x;
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            LSL_Types.LSLInteger x = new LSL_Types.LSLInteger(0);" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestLoneIdent()
        {
            TestHelpers.InMethod();

            // A lone ident should be removed completely as it's an error in C#
            // (MONO at least).
            string input = @"default
{
    touch_start(integer num_detected)
    {
        integer x;
        x;
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            LSL_Types.LSLInteger x = new LSL_Types.LSLInteger(0);" +
                "\n            ;" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestAssignments()
        {
            TestHelpers.InMethod();

            string input = @"default
{
    touch_start(integer num_detected)
    {
        string y;
        integer x = 14;
        y = ""Hello"";
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            LSL_Types.LSLString y = new LSL_Types.LSLString(\"\");" +
                "\n            LSL_Types.LSLInteger x = new LSL_Types.LSLInteger(14);" +
                "\n            y = new LSL_Types.LSLString(\"Hello\");" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestAdditionSubtractionOperator()
        {
            TestHelpers.InMethod();

            string input = @"default
{
    touch_start(integer num_detected)
    {
        integer y = -3;
        integer x = 14 + 6;
        y = 12 +45+20+x + 23 + 1 + x + y;
        y = 12 + -45 + -   20 + x + 23 + -1 + x + y;
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)\n" +
                "        {\n" +
                "            LSL_Types.LSLInteger y = -new LSL_Types.LSLInteger(3);\n" +
                "            LSL_Types.LSLInteger x = new LSL_Types.LSLInteger(14) + new LSL_Types.LSLInteger(6);\n" +
                "            y = new LSL_Types.LSLInteger(12) + new LSL_Types.LSLInteger(45) + new LSL_Types.LSLInteger(20) + x + new LSL_Types.LSLInteger(23) + new LSL_Types.LSLInteger(1) + x + y;\n" +
                "            y = new LSL_Types.LSLInteger(12) + -new LSL_Types.LSLInteger(45) + -new LSL_Types.LSLInteger(20) + x + new LSL_Types.LSLInteger(23) + -new LSL_Types.LSLInteger(1) + x + y;\n" +
                "        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestStrings()
        {
            TestHelpers.InMethod();

            string input = @"default
{
    touch_start(integer num_detected)
    {
        llOwnerSay(""Testing, 1, 2, 3"");
        llSay(0, ""I can hear you!"");
        some_custom_function(1, 2, 3 +x, 4, ""five"", ""arguments"");
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            llOwnerSay(new LSL_Types.LSLString(\"Testing, 1, 2, 3\"));" +
                "\n            llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"I can hear you!\"));" +
                "\n            some_custom_function(new LSL_Types.LSLInteger(1), new LSL_Types.LSLInteger(2), new LSL_Types.LSLInteger(3) + x, new LSL_Types.LSLInteger(4), new LSL_Types.LSLString(\"five\"), new LSL_Types.LSLString(\"arguments\"));" +
                "\n        }" +
                "\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestBinaryExpression()
        {
            TestHelpers.InMethod();

            string input = @"default
{
    touch_start(integer num_detected)
    {
        integer y;
        integer x = 14 + 6;
        y = 12 - 3;
        y = 12 && 3;
        y = 12 || 3;
        y = 12 * 3;
        y = 12 / 3;
        y = 12 | 3;
        y = 12 & 3;
        y = 12 % 3;
        y = 12 + 45 - 20 * x / 23 | 1 & x + y;
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            LSL_Types.LSLInteger y = new LSL_Types.LSLInteger(0);" +
                "\n            LSL_Types.LSLInteger x = new LSL_Types.LSLInteger(14) + new LSL_Types.LSLInteger(6);" +
                "\n            y = new LSL_Types.LSLInteger(12) - new LSL_Types.LSLInteger(3);" +
                "\n            y = ((bool)(new LSL_Types.LSLInteger(12))) & ((bool)(new LSL_Types.LSLInteger(3)));" +
                "\n            y = ((bool)(new LSL_Types.LSLInteger(12))) | ((bool)(new LSL_Types.LSLInteger(3)));" +
                "\n            y = new LSL_Types.LSLInteger(12) * new LSL_Types.LSLInteger(3);" +
                "\n            y = new LSL_Types.LSLInteger(12) / new LSL_Types.LSLInteger(3);" +
                "\n            y = new LSL_Types.LSLInteger(12) | new LSL_Types.LSLInteger(3);" +
                "\n            y = new LSL_Types.LSLInteger(12) & new LSL_Types.LSLInteger(3);" +
                "\n            y = new LSL_Types.LSLInteger(12) % new LSL_Types.LSLInteger(3);" +
                "\n            y = new LSL_Types.LSLInteger(12) + new LSL_Types.LSLInteger(45) - new LSL_Types.LSLInteger(20) * x / new LSL_Types.LSLInteger(23) | new LSL_Types.LSLInteger(1) & x + y;" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestFloatConstants()
        {
            TestHelpers.InMethod();

            string input = @"default
{
    touch_start(integer num_detected)
    {
        float y = 1.1;
        y = 1.123E3;
        y = 1.123e3;
        y = 1.123E+3;
        y = 1.123e+3;
        y = 1.123E-3;
        y = 1.123e-3;
        y = .4;
        y = -1.123E3;
        y = -1.123e3;
        y = -1.123E+3;
        y = -1.123e+3;
        y = -1.123E-3;
        y = -1.123e-3;
        y = -.4;
        y = 12.3 + -1.45E3 - 1.20e-2;
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            LSL_Types.LSLFloat y = new LSL_Types.LSLFloat(1.1);" +
                "\n            y = new LSL_Types.LSLFloat(1.123E3);" +
                "\n            y = new LSL_Types.LSLFloat(1.123e3);" +
                "\n            y = new LSL_Types.LSLFloat(1.123E+3);" +
                "\n            y = new LSL_Types.LSLFloat(1.123e+3);" +
                "\n            y = new LSL_Types.LSLFloat(1.123E-3);" +
                "\n            y = new LSL_Types.LSLFloat(1.123e-3);" +
                "\n            y = new LSL_Types.LSLFloat(.4);" +
                "\n            y = -new LSL_Types.LSLFloat(1.123E3);" +
                "\n            y = -new LSL_Types.LSLFloat(1.123e3);" +
                "\n            y = -new LSL_Types.LSLFloat(1.123E+3);" +
                "\n            y = -new LSL_Types.LSLFloat(1.123e+3);" +
                "\n            y = -new LSL_Types.LSLFloat(1.123E-3);" +
                "\n            y = -new LSL_Types.LSLFloat(1.123e-3);" +
                "\n            y = -new LSL_Types.LSLFloat(.4);" +
                "\n            y = new LSL_Types.LSLFloat(12.3) + -new LSL_Types.LSLFloat(1.45E3) - new LSL_Types.LSLFloat(1.20e-2);" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestComments()
        {
            TestHelpers.InMethod();

            string input = @"// this test tests comments
default
{
    touch_start(integer num_detected) // this should be stripped
    {
        // fill in code here...
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestStringsWithEscapedQuotesAndComments()
        {
            TestHelpers.InMethod();

            string input = @"// this test tests strings, with escaped quotes and comments in strings
default
{
    touch_start(integer num_detected)
    {
        string s1 = ""this is a string."";
        string s2 = ""this is a string ""+""with an escaped \"" inside it."";
        s1 = s2+"" and this ""+""is a string with // comments."";

        string onemore = ""[\^@]"";

        string multiline = ""Good evening Sir,
        my name is Steve.
        I come from a rough area.
        I used to be addicted to crack
        but now I am off it and trying to stay clean.
        That is why I am selling magazine subscriptions.""; // http://www.imdb.com/title/tt0151804/quotes
    }
}
";

            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            LSL_Types.LSLString s1 = new LSL_Types.LSLString(\"this is a string.\");" +
                "\n            LSL_Types.LSLString s2 = new LSL_Types.LSLString(\"this is a string \") + new LSL_Types.LSLString(\"with an escaped \\\" inside it.\");" +
                "\n            s1 = s2 + new LSL_Types.LSLString(\" and this \") + new LSL_Types.LSLString(\"is a string with // comments.\");" +
                "\n            LSL_Types.LSLString onemore = new LSL_Types.LSLString(\"[\\^@]\");" +
                "\n            LSL_Types.LSLString multiline = new LSL_Types.LSLString(\"Good evening Sir,\\n        my name is Steve.\\n        I come from a rough area.\\n        I used to be addicted to crack\\n        but now I am off it and trying to stay clean.\\n        That is why I am selling magazine subscriptions.\");" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestCStyleComments()
        {
            TestHelpers.InMethod();

            string input = @"/* this test tests comments
   of the C variety
*/
default
{
    touch_start(integer /* you can't see me! */ num_detected) /* this should be stripped */
    {
        /*
         * fill
         * in
         * code
         * here...
         */
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestGlobalDefinedFunctions()
        {
            TestHelpers.InMethod();

            string input = @"// this test tests custom defined functions

string onefunc()
{
    return ""Hi from onefunc()!"";
}

twofunc(string s)
{
    llSay(1000, s);
}

default
{
    touch_start(integer num_detected)
    {
        llSay(2000, onefunc());
        twofunc();
    }
}
";
            string expected =
                "\n        LSL_Types.LSLString onefunc()" +
                "\n        {" +
                "\n            return new LSL_Types.LSLString(\"Hi from onefunc()!\");" +
                "\n        }" +
                "\n        void twofunc(LSL_Types.LSLString s)" +
                "\n        {" +
                "\n            llSay(new LSL_Types.LSLInteger(1000), s);" +
                "\n        }" +
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            llSay(new LSL_Types.LSLInteger(2000), onefunc());" +
                "\n            twofunc();" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestGlobalDeclaredVariables()
        {
            TestHelpers.InMethod();

            string input = @"// this test tests custom defined functions and global variables

string globalString;
integer globalInt = 14;
integer anotherGlobal = 20 * globalInt;

string onefunc()
{
    globalString = "" ...and the global!"";
    return ""Hi "" +
           ""from "" +
           ""onefunc()!"" + globalString;
}

twofunc(string s)
{
    llSay(1000, s);
}

default
{
    touch_start(integer num_detected)
    {
        llSay(2000, onefunc());
        twofunc();
    }
}
";
            string expected =
                "\n        LSL_Types.LSLString globalString = new LSL_Types.LSLString(\"\");" +
                "\n        LSL_Types.LSLInteger globalInt = new LSL_Types.LSLInteger(14);" +
                "\n        LSL_Types.LSLInteger anotherGlobal = new LSL_Types.LSLInteger(20) * globalInt;" +
                "\n        LSL_Types.LSLString onefunc()" +
                "\n        {" +
                "\n            globalString = new LSL_Types.LSLString(\" ...and the global!\");" +
                "\n            return new LSL_Types.LSLString(\"Hi \") + new LSL_Types.LSLString(\"from \") + new LSL_Types.LSLString(\"onefunc()!\") + globalString;" +
                "\n        }" +
                "\n        void twofunc(LSL_Types.LSLString s)" +
                "\n        {" +
                "\n            llSay(new LSL_Types.LSLInteger(1000), s);" +
                "\n        }" +
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            llSay(new LSL_Types.LSLInteger(2000), onefunc());" +
                "\n            twofunc();" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestMoreAssignments()
        {
            TestHelpers.InMethod();

            string input = @"// this test tests +=, -=, *=, /=, %=

string globalString;
integer globalInt = 14;

string onefunc(string addition)
{
    globalInt -= 2;

    globalString += addition;
    return ""Hi "" +
           ""from "" +
           ""onefunc()! "" + globalString;
}

default
{
    touch_start(integer num_detected)
    {
        llSay(2000, onefunc());

        integer x = 2;
        x *= 3;
        x /= 14 + -2;
        x %= 10;
    }
}
";
            string expected =
                "\n        LSL_Types.LSLString globalString = new LSL_Types.LSLString(\"\");" +
                "\n        LSL_Types.LSLInteger globalInt = new LSL_Types.LSLInteger(14);" +
                "\n        LSL_Types.LSLString onefunc(LSL_Types.LSLString addition)" +
                "\n        {" +
                "\n            globalInt -= new LSL_Types.LSLInteger(2);" +
                "\n            globalString += addition;" +
                "\n            return new LSL_Types.LSLString(\"Hi \") + new LSL_Types.LSLString(\"from \") + new LSL_Types.LSLString(\"onefunc()! \") + globalString;" +
                "\n        }" +
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            llSay(new LSL_Types.LSLInteger(2000), onefunc());" +
                "\n            LSL_Types.LSLInteger x = new LSL_Types.LSLInteger(2);" +
                "\n            x *= new LSL_Types.LSLInteger(3);" +
                "\n            x /= new LSL_Types.LSLInteger(14) + -new LSL_Types.LSLInteger(2);" +
                "\n            x %= new LSL_Types.LSLInteger(10);" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestVectorConstantNotation()
        {
            TestHelpers.InMethod();

            string input = @"default
{
    touch_start(integer num_detected)
    {
        vector y = <1.2, llGetMeAFloat(), 4.4>;
        rotation x = <0.1, 0.1, one + 2, 0.9>;

        y = <0.1, 0.1, 1.1 - three - two+eight*8>;
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            LSL_Types.Vector3 y = new LSL_Types.Vector3(new LSL_Types.LSLFloat(1.2), llGetMeAFloat(), new LSL_Types.LSLFloat(4.4));" +
                "\n            LSL_Types.Quaternion x = new LSL_Types.Quaternion(new LSL_Types.LSLFloat(0.1), new LSL_Types.LSLFloat(0.1), one + new LSL_Types.LSLInteger(2), new LSL_Types.LSLFloat(0.9));" +
                "\n            y = new LSL_Types.Vector3(new LSL_Types.LSLFloat(0.1), new LSL_Types.LSLFloat(0.1), new LSL_Types.LSLFloat(1.1) - three - two + eight * new LSL_Types.LSLInteger(8));" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestVectorMemberAccess()
        {
            TestHelpers.InMethod();

            string input = @"default
{
    touch_start(integer num_detected)
    {
        vector y = <1.2, llGetMeAFloat(), 4.4>;
        x = y.x + 1.1;
        y.x = 1.1;
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            LSL_Types.Vector3 y = new LSL_Types.Vector3(new LSL_Types.LSLFloat(1.2), llGetMeAFloat(), new LSL_Types.LSLFloat(4.4));" +
                "\n            x = y.x + new LSL_Types.LSLFloat(1.1);" +
                "\n            y.x = new LSL_Types.LSLFloat(1.1);" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestExpressionInParentheses()
        {
            TestHelpers.InMethod();

            string input = @"default
{
    touch_start(integer num_detected)
    {
        integer y = -3;
        integer x = 14 + 6;
        y = 12 +45+20+x + (23 + 1) + x + y;
        y = (12 + -45 + -20 + x + 23 )+ -1 + x + y;
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            LSL_Types.LSLInteger y = -new LSL_Types.LSLInteger(3);" +
                "\n            LSL_Types.LSLInteger x = new LSL_Types.LSLInteger(14) + new LSL_Types.LSLInteger(6);" +
                "\n            y = new LSL_Types.LSLInteger(12) + new LSL_Types.LSLInteger(45) + new LSL_Types.LSLInteger(20) + x + (new LSL_Types.LSLInteger(23) + new LSL_Types.LSLInteger(1)) + x + y;" +
                "\n            y = (new LSL_Types.LSLInteger(12) + -new LSL_Types.LSLInteger(45) + -new LSL_Types.LSLInteger(20) + x + new LSL_Types.LSLInteger(23)) + -new LSL_Types.LSLInteger(1) + x + y;" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestIncrementDecrementOperator()
        {
            TestHelpers.InMethod();

            string input = @"// here we'll test the ++ and -- operators

default
{
    touch_start(integer num_detected)
    {
        integer y = -3;
        integer x = 14 + 6;
        y = 12 +45+20+x++ + (23 + 1) + ++x + --    y;
        y = (12 + -45 + -20 + x-- + 23 )+ -1 + x -- + ++y;
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            LSL_Types.LSLInteger y = -new LSL_Types.LSLInteger(3);" +
                "\n            LSL_Types.LSLInteger x = new LSL_Types.LSLInteger(14) + new LSL_Types.LSLInteger(6);" +
                "\n            y = new LSL_Types.LSLInteger(12) + new LSL_Types.LSLInteger(45) + new LSL_Types.LSLInteger(20) + x++ + (new LSL_Types.LSLInteger(23) + new LSL_Types.LSLInteger(1)) + ++x + --y;" +
                "\n            y = (new LSL_Types.LSLInteger(12) + -new LSL_Types.LSLInteger(45) + -new LSL_Types.LSLInteger(20) + x-- + new LSL_Types.LSLInteger(23)) + -new LSL_Types.LSLInteger(1) + x-- + ++y;" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestLists()
        {
            TestHelpers.InMethod();

            string input = @"// testing lists

default
{
    touch_start(integer num_detected)
    {
        list l = [];
        list m = [1, two, ""three"", <4.0, 4.0, 4.0>, 5 + 5];
        llCallSomeFunc(1, llAnotherFunc(), [1, 2, 3]);
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            LSL_Types.list l = new LSL_Types.list();" +
                "\n            LSL_Types.list m = new LSL_Types.list(new LSL_Types.LSLInteger(1), two, new LSL_Types.LSLString(\"three\"), new LSL_Types.Vector3(new LSL_Types.LSLFloat(4.0), new LSL_Types.LSLFloat(4.0), new LSL_Types.LSLFloat(4.0)), new LSL_Types.LSLInteger(5) + new LSL_Types.LSLInteger(5));" +
                "\n            llCallSomeFunc(new LSL_Types.LSLInteger(1), llAnotherFunc(), new LSL_Types.list(new LSL_Types.LSLInteger(1), new LSL_Types.LSLInteger(2), new LSL_Types.LSLInteger(3)));" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestIfStatement()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string input = @"// let's test if statements

default
{
    touch_start(integer num_detected)
    {
        integer x = 1;

        if (x) llSay(0, ""Hello"");
        if (1)
        {
            llSay(0, ""Hi"");
            integer r = 3;
            return;
        }

        if (f(x)) llSay(0, ""f(x) is true"");
        else llSay(0, ""f(x) is false"");

        if (x + y) llSay(0, ""x + y is true"");
        else if (y - x) llSay(0, ""y - x is true"");
        else llSay(0, ""Who needs x and y anyway?"");

        if (x * y) llSay(0, ""x * y is true"");
        else if (y / x)
        {
            llSay(0, ""uh-oh, y / x is true, exiting"");
            return;
        }
        else llSay(0, ""Who needs x and y anyway?"");

        // and now for my last trick
        if (x % y) llSay(0, ""x is true"");
        else if (y & x) llSay(0, ""y is true"");
        else if (z | x) llSay(0, ""z is true"");
        else if (a * (b + x)) llSay(0, ""a is true"");
        else if (b) llSay(0, ""b is true"");
        else if (v) llSay(0, ""v is true"");
        else llSay(0, ""Everything is lies!"");
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            LSL_Types.LSLInteger x = new LSL_Types.LSLInteger(1);" +
                "\n            if (x)" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"Hello\"));" +
                "\n            if (new LSL_Types.LSLInteger(1))" +
                "\n            {" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"Hi\"));" +
                "\n                LSL_Types.LSLInteger r = new LSL_Types.LSLInteger(3);" +
                "\n                return ;" +
                "\n            }" +
                "\n            if (f(x))" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"f(x) is true\"));" +
                "\n            else" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"f(x) is false\"));" +
                "\n            if (x + y)" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"x + y is true\"));" +
                "\n            else" +
                "\n            if (y - x)" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"y - x is true\"));" +
                "\n            else" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"Who needs x and y anyway?\"));" +
                "\n            if (x * y)" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"x * y is true\"));" +
                "\n            else" +
                "\n            if (y / x)" +
                "\n            {" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"uh-oh, y / x is true, exiting\"));" +
                "\n                return ;" +
                "\n            }" +
                "\n            else" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"Who needs x and y anyway?\"));" +
                "\n            if (x % y)" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"x is true\"));" +
                "\n            else" +
                "\n            if (y & x)" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"y is true\"));" +
                "\n            else" +
                "\n            if (z | x)" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"z is true\"));" +
                "\n            else" +
                "\n            if (a * (b + x))" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"a is true\"));" +
                "\n            else" +
                "\n            if (b)" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"b is true\"));" +
                "\n            else" +
                "\n            if (v)" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"v is true\"));" +
                "\n            else" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"Everything is lies!\"));" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestIfElseStatement()
        {
            TestHelpers.InMethod();

            string input = @"// let's test complex logical expressions

default
{
    touch_start(integer num_detected)
    {
        integer x = 1;
        integer y = 0;

        if (x && y) llSay(0, ""Hello"");
        if (x || y)
        {
            llSay(0, ""Hi"");
            integer r = 3;
            return;
        }

        if (x && y || z) llSay(0, ""x is true"");
        else llSay(0, ""x is false"");

        if (x == y) llSay(0, ""x is true"");
        else if (y < x) llSay(0, ""y is true"");
        else llSay(0, ""Who needs x and y anyway?"");

        if (x > y) llSay(0, ""x is true"");
        else if (y <= x)
        {
            llSay(0, ""uh-oh, y is true, exiting"");
            return;
        }
        else llSay(0, ""Who needs x and y anyway?"");

        // and now for my last trick
        if (x >= y) llSay(0, ""x is true"");
        else if (y != x) llSay(0, ""y is true"");
        else if (!z) llSay(0, ""z is true"");
        else if (!(a && b)) llSay(0, ""a is true"");
        else if (b) llSay(0, ""b is true"");
        else if (v) llSay(0, ""v is true"");
        else llSay(0, ""Everything is lies!"");
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            LSL_Types.LSLInteger x = new LSL_Types.LSLInteger(1);" +
                "\n            LSL_Types.LSLInteger y = new LSL_Types.LSLInteger(0);" +
                "\n            if (((bool)(x)) & ((bool)(y)))" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"Hello\"));" +
                "\n            if (((bool)(x)) | ((bool)(y)))" +
                "\n            {" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"Hi\"));" +
                "\n                LSL_Types.LSLInteger r = new LSL_Types.LSLInteger(3);" +
                "\n                return ;" +
                "\n            }" +
                "\n            if (((bool)(((bool)(x)) & ((bool)(y)))) | ((bool)(z)))" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"x is true\"));" +
                "\n            else" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"x is false\"));" +
                "\n            if (x == y)" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"x is true\"));" +
                "\n            else" +
                "\n            if (y < x)" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"y is true\"));" +
                "\n            else" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"Who needs x and y anyway?\"));" +
                "\n            if (x > y)" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"x is true\"));" +
                "\n            else" +
                "\n            if (y <= x)" +
                "\n            {" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"uh-oh, y is true, exiting\"));" +
                "\n                return ;" +
                "\n            }" +
                "\n            else" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"Who needs x and y anyway?\"));" +
                "\n            if (x >= y)" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"x is true\"));" +
                "\n            else" +
                "\n            if (y != x)" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"y is true\"));" +
                "\n            else" +
                "\n            if (!z)" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"z is true\"));" +
                "\n            else" +
                "\n            if (!(((bool)(a)) & ((bool)(b))))" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"a is true\"));" +
                "\n            else" +
                "\n            if (b)" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"b is true\"));" +
                "\n            else" +
                "\n            if (v)" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"v is true\"));" +
                "\n            else" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"Everything is lies!\"));" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestWhileLoop()
        {
            TestHelpers.InMethod();

            string input = @"// let's test while loops

default
{
    touch_start(integer num_detected)
    {
        integer x = 1;
        integer y = 0;

        while (x) llSay(0, ""To infinity, and beyond!"");
        while (0 || (x && 0))
        {
            llSay(0, ""Never say never."");
            return;
        }
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            LSL_Types.LSLInteger x = new LSL_Types.LSLInteger(1);" +
                "\n            LSL_Types.LSLInteger y = new LSL_Types.LSLInteger(0);" +
                "\n            while (x)" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"To infinity, and beyond!\"));" +
                "\n            while (((bool)(new LSL_Types.LSLInteger(0))) | ((bool)((((bool)(x)) & ((bool)(new LSL_Types.LSLInteger(0)))))))" +
                "\n            {" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"Never say never.\"));" +
                "\n                return ;" +
                "\n            }" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestDoWhileLoop()
        {
            TestHelpers.InMethod();

            string input = @"// let's test do-while loops

default
{
    touch_start(integer num_detected)
    {
        integer x = 1;
        integer y = 0;

        do llSay(0, ""And we're doing..."");
        while (x);

        do
        {
            llSay(0, ""I like it here. I wish we could stay here forever."");
            y--;
        } while (y);
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            LSL_Types.LSLInteger x = new LSL_Types.LSLInteger(1);" +
                "\n            LSL_Types.LSLInteger y = new LSL_Types.LSLInteger(0);" +
                "\n            do" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"And we're doing...\"));" +
                "\n            while (x);" +
                "\n            do" +
                "\n            {" +
                "\n                llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"I like it here. I wish we could stay here forever.\"));" +
                "\n                y--;" +
                "\n            }" +
                "\n            while (y);" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestForLoop()
        {
            TestHelpers.InMethod();

            string input = @"// let's test for loops

default
{
    touch_start(integer num_detected)
    {
        integer x = 1;
        integer y = 0;

        for (x = 10; x >= 0; x--)
        {
            llOwnerSay(""Launch in T minus "" + x);
            IncreaseRocketPower();
        }

        for (x = 0, y = 6; y > 0 && x != y; x++, y--) llOwnerSay(""Hi "" + x + "", "" + y);
        for (x = 0, y = 6; ! y; x++,y--) llOwnerSay(""Hi "" + x + "", "" + y);
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            LSL_Types.LSLInteger x = new LSL_Types.LSLInteger(1);" +
                "\n            LSL_Types.LSLInteger y = new LSL_Types.LSLInteger(0);" +
                "\n            for (x = new LSL_Types.LSLInteger(10); x >= new LSL_Types.LSLInteger(0); x--)" +
                "\n            {" +
                "\n                llOwnerSay(new LSL_Types.LSLString(\"Launch in T minus \") + x);" +
                "\n                IncreaseRocketPower();" +
                "\n            }" +
                "\n            for (x = new LSL_Types.LSLInteger(0), y = new LSL_Types.LSLInteger(6); ((bool)(y > new LSL_Types.LSLInteger(0))) & ((bool)(x != y)); x++, y--)" +
                "\n                llOwnerSay(new LSL_Types.LSLString(\"Hi \") + x + new LSL_Types.LSLString(\", \") + y);" +
                "\n            for (x = new LSL_Types.LSLInteger(0), y = new LSL_Types.LSLInteger(6); !y; x++, y--)" +
                "\n                llOwnerSay(new LSL_Types.LSLString(\"Hi \") + x + new LSL_Types.LSLString(\", \") + y);" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestFloatsWithTrailingDecimal()
        {
            TestHelpers.InMethod();

            string input = @"// a curious feature of LSL that allows floats to be defined with a trailing dot

default
{
    touch_start(integer num_detected)
    {
        float y = 1.;
        y = 1.E3;
        y = 1.e3;
        y = 1.E+3;
        y = 1.e+3;
        y = 1.E-3;
        y = 1.e-3;
        y = -1.E3;
        y = -1.e3;
        y = -1.E+3;
        y = -1.e+3;
        y = -1.E-3;
        y = -1.e-3;
        y = 12. + -1.E3 - 1.e-2;
        vector v = <0.,0.,0.>;
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            LSL_Types.LSLFloat y = new LSL_Types.LSLFloat(1.0);" +
                "\n            y = new LSL_Types.LSLFloat(1.0E3);" +
                "\n            y = new LSL_Types.LSLFloat(1.0e3);" +
                "\n            y = new LSL_Types.LSLFloat(1.0E+3);" +
                "\n            y = new LSL_Types.LSLFloat(1.0e+3);" +
                "\n            y = new LSL_Types.LSLFloat(1.0E-3);" +
                "\n            y = new LSL_Types.LSLFloat(1.0e-3);" +
                "\n            y = -new LSL_Types.LSLFloat(1.0E3);" +
                "\n            y = -new LSL_Types.LSLFloat(1.0e3);" +
                "\n            y = -new LSL_Types.LSLFloat(1.0E+3);" +
                "\n            y = -new LSL_Types.LSLFloat(1.0e+3);" +
                "\n            y = -new LSL_Types.LSLFloat(1.0E-3);" +
                "\n            y = -new LSL_Types.LSLFloat(1.0e-3);" +
                "\n            y = new LSL_Types.LSLFloat(12.0) + -new LSL_Types.LSLFloat(1.0E3) - new LSL_Types.LSLFloat(1.0e-2);" +
                "\n            LSL_Types.Vector3 v = new LSL_Types.Vector3(new LSL_Types.LSLFloat(0.0), new LSL_Types.LSLFloat(0.0), new LSL_Types.LSLFloat(0.0));" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestUnaryAndBinaryOperators()
        {
            TestHelpers.InMethod();

            string input = @"// let's test a few more operators

default
{
    touch_start(integer num_detected)
    {
        integer x = 2;
        integer y = 1;
        integer z = x ^ y;
        x = ~ z;
        x = ~(y && z);
        y = x >> z;
        z = y << x;
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            LSL_Types.LSLInteger x = new LSL_Types.LSLInteger(2);" +
                "\n            LSL_Types.LSLInteger y = new LSL_Types.LSLInteger(1);" +
                "\n            LSL_Types.LSLInteger z = x ^ y;" +
                "\n            x = ~z;" +
                "\n            x = ~(((bool)(y)) & ((bool)(z)));" +
                "\n            y = x >> z;" +
                "\n            z = y << x;" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestTypecasts()
        {
            TestHelpers.InMethod();

            string input = @"// let's test typecasts

default
{
    touch_start(integer num_detected)
    {
        string s = """";
        integer x = 1;

        s = (string) x++;
        s = (string) x;
        s = (string) <0., 0., 0.>;
        s = (string) <1., 1., 1., 1.>;
        s = (integer) ""1"";
        s = (string) llSomethingThatReturnsInteger();
        s = (string) 134;
        s = (string) (x ^ y | (z && l)) + (string) (x + y - 13);
        llOwnerSay(""s is: "" + s);
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            LSL_Types.LSLString s = new LSL_Types.LSLString(\"\");" +
                "\n            LSL_Types.LSLInteger x = new LSL_Types.LSLInteger(1);" +
                "\n            s = (LSL_Types.LSLString) (x++);" +
                "\n            s = (LSL_Types.LSLString) (x);" +
                "\n            s = (LSL_Types.LSLString) (new LSL_Types.Vector3(new LSL_Types.LSLFloat(0.0), new LSL_Types.LSLFloat(0.0), new LSL_Types.LSLFloat(0.0)));" +
                "\n            s = (LSL_Types.LSLString) (new LSL_Types.Quaternion(new LSL_Types.LSLFloat(1.0), new LSL_Types.LSLFloat(1.0), new LSL_Types.LSLFloat(1.0), new LSL_Types.LSLFloat(1.0)));" +
                "\n            s = (LSL_Types.LSLInteger) (new LSL_Types.LSLString(\"1\"));" +
                "\n            s = (LSL_Types.LSLString) (llSomethingThatReturnsInteger());" +
                "\n            s = (LSL_Types.LSLString) (new LSL_Types.LSLInteger(134));" +
                "\n            s = (LSL_Types.LSLString) (x ^ y | (((bool)(z)) & ((bool)(l)))) + (LSL_Types.LSLString) (x + y - new LSL_Types.LSLInteger(13));" +
                "\n            llOwnerSay(new LSL_Types.LSLString(\"s is: \") + s);" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestStates()
        {
            TestHelpers.InMethod();

            string input = @"// let's test states

default
{
    touch_start(integer num_detected)
    {
        llSay(0, ""Going to state 'statetwo'"");
        state statetwo;
    }
}

state statetwo
{
    state_entry()
    {
        llSay(0, ""Going to the default state"");
        state default;
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"Going to state 'statetwo'\"));" +
                "\n            state(\"statetwo\");" +
                "\n        }" +
                "\n        public void statetwo_event_state_entry()" +
                "\n        {" +
                "\n            llSay(new LSL_Types.LSLInteger(0), new LSL_Types.LSLString(\"Going to the default state\"));" +
                "\n            state(\"default\");" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestHexIntegerConstants()
        {
            TestHelpers.InMethod();

            string input = @"// let's test hex integers

default
{
    touch_start(integer num_detected)
    {
        integer x = 0x23;
        integer x = 0x2f34B;
        integer x = 0x2F34b;
        integer x = 0x2F34B;
        integer x = 0x2f34b;
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            LSL_Types.LSLInteger x = new LSL_Types.LSLInteger(0x23);" +
                "\n            LSL_Types.LSLInteger x = new LSL_Types.LSLInteger(0x2f34B);" +
                "\n            LSL_Types.LSLInteger x = new LSL_Types.LSLInteger(0x2F34b);" +
                "\n            LSL_Types.LSLInteger x = new LSL_Types.LSLInteger(0x2F34B);" +
                "\n            LSL_Types.LSLInteger x = new LSL_Types.LSLInteger(0x2f34b);" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestJumps()
        {
            TestHelpers.InMethod();

            string input = @"// let's test jumps

default
{
    touch_start(integer num_detected)
    {
        jump here;
        llOwnerSay(""Uh oh, the jump didn't work"");
        @here;
        llOwnerSay(""After the jump"");
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            goto here;" +
                "\n            llOwnerSay(new LSL_Types.LSLString(\"Uh oh, the jump didn't work\"));" +
                "\n            here: NoOp();" +
                "\n            llOwnerSay(new LSL_Types.LSLString(\"After the jump\"));" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestImplicitVariableInitialization()
        {
            TestHelpers.InMethod();

            string input = @"// let's test implicitly initializing variables

default
{
    touch_start(integer num_detected)
    {
        integer i; integer j = 14;
        float f; float g = 14.0;
        string s; string t = ""Hi there"";
        list l; list m = [1, 2, 3];
        vector v; vector w = <1.0, 0.1, 0.5>;
        rotation r; rotation u = <0.8, 0.7, 0.6, llSomeFunc()>;
        key k; key n = ""ping"";
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            LSL_Types.LSLInteger i = new LSL_Types.LSLInteger(0);" +
                "\n            LSL_Types.LSLInteger j = new LSL_Types.LSLInteger(14);" +
                "\n            LSL_Types.LSLFloat f = new LSL_Types.LSLFloat(0.0);" +
                "\n            LSL_Types.LSLFloat g = new LSL_Types.LSLFloat(14.0);" +
                "\n            LSL_Types.LSLString s = new LSL_Types.LSLString(\"\");" +
                "\n            LSL_Types.LSLString t = new LSL_Types.LSLString(\"Hi there\");" +
                "\n            LSL_Types.list l = new LSL_Types.list();" +
                "\n            LSL_Types.list m = new LSL_Types.list(new LSL_Types.LSLInteger(1), new LSL_Types.LSLInteger(2), new LSL_Types.LSLInteger(3));" +
                "\n            LSL_Types.Vector3 v = new LSL_Types.Vector3(new LSL_Types.LSLFloat(0.0), new LSL_Types.LSLFloat(0.0), new LSL_Types.LSLFloat(0.0));" +
                "\n            LSL_Types.Vector3 w = new LSL_Types.Vector3(new LSL_Types.LSLFloat(1.0), new LSL_Types.LSLFloat(0.1), new LSL_Types.LSLFloat(0.5));" +
                "\n            LSL_Types.Quaternion r = new LSL_Types.Quaternion(new LSL_Types.LSLFloat(0.0), new LSL_Types.LSLFloat(0.0), new LSL_Types.LSLFloat(0.0), new LSL_Types.LSLFloat(1.0));" +
                "\n            LSL_Types.Quaternion u = new LSL_Types.Quaternion(new LSL_Types.LSLFloat(0.8), new LSL_Types.LSLFloat(0.7), new LSL_Types.LSLFloat(0.6), llSomeFunc());" +
                "\n            LSL_Types.LSLString k = new LSL_Types.LSLString(\"\");" +
                "\n            LSL_Types.LSLString n = new LSL_Types.LSLString(\"ping\");" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestMultipleEqualsExpression()
        {
            TestHelpers.InMethod();

            string input = @"// let's test x = y = 5 type expressions

default
{
    touch_start(integer num_detected)
    {
        integer x;
        integer y;
        x = y = 5;
        x += y -= 5;
        llOwnerSay(""x is: "" + (string) x + "", y is: "" + (string) y);
    }
}
";
            string expected =
                "\n        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)" +
                "\n        {" +
                "\n            LSL_Types.LSLInteger x = new LSL_Types.LSLInteger(0);" +
                "\n            LSL_Types.LSLInteger y = new LSL_Types.LSLInteger(0);" +
                "\n            x = y = new LSL_Types.LSLInteger(5);" +
                "\n            x += y -= new LSL_Types.LSLInteger(5);" +
                "\n            llOwnerSay(new LSL_Types.LSLString(\"x is: \") + (LSL_Types.LSLString) (x) + new LSL_Types.LSLString(\", y is: \") + (LSL_Types.LSLString) (y));" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestUnaryExpressionLastInVectorConstant()
        {
            TestHelpers.InMethod();

            string input = @"// let's test unary expressions some more

default
{
    state_entry()
    {
        vector v = <x,y,-0.5>;
    }
}
";
            string expected =
                "\n        public void default_event_state_entry()" +
                "\n        {" +
                "\n            LSL_Types.Vector3 v = new LSL_Types.Vector3(x, y, -new LSL_Types.LSLFloat(0.5));" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestVectorMemberPlusEquals()
        {
            TestHelpers.InMethod();

            string input = @"// let's test unary expressions some more

default
{
    state_entry()
    {
        vector v = llGetPos();
        v.z += 4;
        v.z -= 4;
        v.z *= 4;
        v.z /= 4;
        v.z %= 4;
    }
}
";
            string expected =
                "\n        public void default_event_state_entry()" +
                "\n        {" +
                "\n            LSL_Types.Vector3 v = llGetPos();" +
                "\n            v.z += new LSL_Types.LSLInteger(4);" +
                "\n            v.z -= new LSL_Types.LSLInteger(4);" +
                "\n            v.z *= new LSL_Types.LSLInteger(4);" +
                "\n            v.z /= new LSL_Types.LSLInteger(4);" +
                "\n            v.z %= new LSL_Types.LSLInteger(4);" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestWhileLoopWithNoBody()
        {
            TestHelpers.InMethod();

            string input = @"default
{
    state_entry()
    {
        while (1<0);
    }
}";

            string expected =
                "\n        public void default_event_state_entry()" +
                "\n        {" +
                "\n            while (new LSL_Types.LSLInteger(1) < new LSL_Types.LSLInteger(0))" +
                "\n                ;" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestDoWhileLoopWithNoBody()
        {
            TestHelpers.InMethod();

            string input = @"default
{
    state_entry()
    {
        do;
        while (1<0);
    }
}";

            string expected =
                "\n        public void default_event_state_entry()" +
                "\n        {" +
                "\n            do" +
                "\n                ;" +
                "\n            while (new LSL_Types.LSLInteger(1) < new LSL_Types.LSLInteger(0));" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestIfWithNoBody()
        {
            TestHelpers.InMethod();

            string input = @"default
{
    state_entry()
    {
        if (1<0);
    }
}";

            string expected =
                "\n        public void default_event_state_entry()" +
                "\n        {" +
                "\n            if (new LSL_Types.LSLInteger(1) < new LSL_Types.LSLInteger(0))" +
                "\n                ;" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestIfElseWithNoBody()
        {
            TestHelpers.InMethod();

            string input = @"default
{
    state_entry()
    {
        if (1<0);
        else;
    }
}";

            string expected =
                "\n        public void default_event_state_entry()" +
                "\n        {" +
                "\n            if (new LSL_Types.LSLInteger(1) < new LSL_Types.LSLInteger(0))" +
                "\n                ;" +
                "\n            else" +
                "\n                ;" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestForLoopWithNoBody()
        {
            TestHelpers.InMethod();

            string input = @"default
{
    state_entry()
    {
        for (x = 4; 1<0; x += 2);
    }
}";

            string expected =
                "\n        public void default_event_state_entry()" +
                "\n        {" +
                "\n            for (x = new LSL_Types.LSLInteger(4); new LSL_Types.LSLInteger(1) < new LSL_Types.LSLInteger(0); x += new LSL_Types.LSLInteger(2))" +
                "\n                ;" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestForLoopWithNoAssignment()
        {
            TestHelpers.InMethod();

            string input = @"default
{
    state_entry()
    {
        integer x = 4;
        for (; 1<0; x += 2);
    }
}";

            string expected =
                "\n        public void default_event_state_entry()" +
                "\n        {" +
                "\n            LSL_Types.LSLInteger x = new LSL_Types.LSLInteger(4);" +
                "\n            for (; new LSL_Types.LSLInteger(1) < new LSL_Types.LSLInteger(0); x += new LSL_Types.LSLInteger(2))" +
                "\n                ;" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestForLoopWithOnlyIdentInAssignment()
        {
            TestHelpers.InMethod();

            string input = @"default
{
    state_entry()
    {
        integer x = 4;
        for (x; 1<0; x += 2);
    }
}";

            string expected =
                "\n        public void default_event_state_entry()" +
                "\n        {" +
                "\n            LSL_Types.LSLInteger x = new LSL_Types.LSLInteger(4);" +
                "\n            for (; new LSL_Types.LSLInteger(1) < new LSL_Types.LSLInteger(0); x += new LSL_Types.LSLInteger(2))" +
                "\n                ;" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestAssignmentInIfWhileDoWhile()
        {
            TestHelpers.InMethod();

            string input = @"default
{
    state_entry()
    {
        integer x;

        while (x = 14) llOwnerSay(""x is: "" + (string) x);

        if (x = 24) llOwnerSay(""x is: "" + (string) x);

        do
            llOwnerSay(""x is: "" + (string) x);
        while (x = 44);
    }
}";

            string expected =
                "\n        public void default_event_state_entry()" +
                "\n        {" +
                "\n            LSL_Types.LSLInteger x = new LSL_Types.LSLInteger(0);" +
                "\n            while (x = new LSL_Types.LSLInteger(14))" +
                "\n                llOwnerSay(new LSL_Types.LSLString(\"x is: \") + (LSL_Types.LSLString) (x));" +
                "\n            if (x = new LSL_Types.LSLInteger(24))" +
                "\n                llOwnerSay(new LSL_Types.LSLString(\"x is: \") + (LSL_Types.LSLString) (x));" +
                "\n            do" +
                "\n                llOwnerSay(new LSL_Types.LSLString(\"x is: \") + (LSL_Types.LSLString) (x));" +
                "\n            while (x = new LSL_Types.LSLInteger(44));" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestLSLListHack()
        {
            TestHelpers.InMethod();

            string input = @"default
{
    state_entry()
    {
        list l = [""hello""];
        l = (l=[]) + l + ""world"";
    }
}";

            string expected =
                "\n        public void default_event_state_entry()" +
                "\n        {" +
                "\n            LSL_Types.list l = new LSL_Types.list(new LSL_Types.LSLString(\"hello\"));" +
                "\n            l = (l = new LSL_Types.list()) + l + new LSL_Types.LSLString(\"world\");" +
                "\n        }\n";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestSyntaxError()
        {
            TestHelpers.InMethod();

            bool gotException = false;

            string input = @"default
{
    state_entry()
    {
        integer y
    }
}
";
            try
            {
                CSCodeGenerator cg = new CSCodeGenerator();
                cg.Convert(input);
            }
            catch (System.Exception e)
            {
                // The syntax error is on line 5, char 4 (expected ';', found
                // '}').
                Assert.AreEqual("(5,4) syntax error", e.Message);
                gotException = true;
            }

            Assert.That(gotException, Is.True);
        }

        [Test]
        public void TestSyntaxErrorDeclaringVariableInForLoop()
        {
            TestHelpers.InMethod();

            bool gotException = false;

            string input = @"default
{
    state_entry()
    {
        for (integer x = 0; x < 10; x++) llOwnerSay(""x is: "" + (string) x);
    }
}
";
            try
            {
                CSCodeGenerator cg = new CSCodeGenerator();
                cg.Convert(input);
            }
            catch (System.Exception e)
            {
                // The syntax error is on line 4, char 13 (Syntax error)
                Assert.AreEqual("(4,13) syntax error", e.Message);

                gotException = true;
            }

            Assert.That(gotException, Is.True);
        }
    }
}
