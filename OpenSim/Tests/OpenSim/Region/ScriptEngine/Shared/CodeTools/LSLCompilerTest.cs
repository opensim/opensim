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

using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OpenSim.Region.ScriptEngine.Shared.CodeTools;

namespace OpenSim.Region.ScriptEngine.Shared.CodeTools.Tests
{
    /// <summary>
    /// Tests the LSL compiler, both the code generation and transformation.
    /// Each test has some LSL code as input and C# code as expected output.
    /// The generated C# code is compared against the expected C# code.
    /// </summary>
    [TestFixture]
    public class LSLCompilerTest
    {
        [Test]
        public void TestDefaultState()
        {
            string input = @"default
{
    state_entry()
    {
    }
}
";
            string expected = @"
        public void default_event_state_entry()
        {
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestCustomState()
        {
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
            string expected = @"
        public void default_event_state_entry()
        {
        }
        public void another_state_event_no_sensor()
        {
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestEventWithArguments()
        {
            string input = @"default
{
    at_rot_target(integer tnum, rotation targetrot, rotation ourrot)
    {
    }
}
";
            string expected = @"
        public void default_event_at_rot_target(LSL_Types.LSLInteger tnum, LSL_Types.Quaternion targetrot, LSL_Types.Quaternion ourrot)
        {
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestIntegerDeclaration()
        {
            string input = @"default
{
    touch_start(integer num_detected)
    {
        integer x;
    }
}
";
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            LSL_Types.LSLInteger x = 0;
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestAssignments()
        {
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
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            LSL_Types.LSLString y = """";
            LSL_Types.LSLInteger x = 14;
            y = ""Hello"";
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestAdditionSubtractionOperator()
        {
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
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            LSL_Types.LSLInteger y = -3;
            LSL_Types.LSLInteger x = 14 + 6;
            y = 12 + 45 + 20 + x + 23 + 1 + x + y;
            y = 12 + -45 + -20 + x + 23 + -1 + x + y;
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestStrings()
        {
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
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            llOwnerSay(""Testing, 1, 2, 3"");
            llSay(0, ""I can hear you!"");
            some_custom_function(1, 2, 3 + x, 4, ""five"", ""arguments"");
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestBinaryExpression()
        {
            string input = @"default
{
    touch_start(integer num_detected)
    {
        integer y;
        integer x = 14 + 6;
        y = 12 - 3;
        y = 12 * 3;
        y = 12 / 3;
        y = 12 | 3;
        y = 12 & 3;
        y = 12 % 3;
        y = 12 + 45 - 20 * x / 23 | 1 & x + y;
    }
}
";
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            LSL_Types.LSLInteger y = 0;
            LSL_Types.LSLInteger x = 14 + 6;
            y = 12 - 3;
            y = 12 * 3;
            y = 12 / 3;
            y = 12 | 3;
            y = 12 & 3;
            y = 12 % 3;
            y = 12 + 45 - 20 * x / 23 | 1 & x + y;
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestFloatConstants()
        {
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
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            LSL_Types.LSLFloat y = 1.1;
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
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestComments()
        {
            string input = @"// this test tests comments
default
{
    touch_start(integer num_detected) // this should be stripped
    {
        // fill in code here...
    }
}
";
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestStringsWithEscapedQuotesAndComments()
        {
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
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            LSL_Types.LSLString s1 = ""this is a string."";
            LSL_Types.LSLString s2 = ""this is a string "" + ""with an escaped \"" inside it."";
            s1 = s2 + "" and this "" + ""is a string with // comments."";
            LSL_Types.LSLString onemore = ""[\^@]"";
            LSL_Types.LSLString multiline = ""Good evening Sir,\n        my name is Steve.\n        I come from a rough area.\n        I used to be addicted to crack\n        but now I am off it and trying to stay clean.\n        That is why I am selling magazine subscriptions."";
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestGlobalDefinedFunctions()
        {
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
            string expected = @"
        LSL_Types.LSLString onefunc()
        {
            return ""Hi from onefunc()!"";
        }
        void twofunc(LSL_Types.LSLString s)
        {
            llSay(1000, s);
        }
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            llSay(2000, onefunc());
            twofunc();
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestGlobalDeclaredVariables()
        {
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
            string expected = @"
        LSL_Types.LSLString globalString = """";
        LSL_Types.LSLInteger globalInt = 14;
        LSL_Types.LSLInteger anotherGlobal = 20 * globalInt;
        LSL_Types.LSLString onefunc()
        {
            globalString = "" ...and the global!"";
            return ""Hi "" + ""from "" + ""onefunc()!"" + globalString;
        }
        void twofunc(LSL_Types.LSLString s)
        {
            llSay(1000, s);
        }
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            llSay(2000, onefunc());
            twofunc();
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestMoreAssignments()
        {
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
            string expected = @"
        LSL_Types.LSLString globalString = """";
        LSL_Types.LSLInteger globalInt = 14;
        LSL_Types.LSLString onefunc(LSL_Types.LSLString addition)
        {
            globalInt -= 2;
            globalString += addition;
            return ""Hi "" + ""from "" + ""onefunc()! "" + globalString;
        }
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            llSay(2000, onefunc());
            LSL_Types.LSLInteger x = 2;
            x *= 3;
            x /= 14 + -2;
            x %= 10;
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestVectorConstantNotation()
        {
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
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            LSL_Types.Vector3 y = new LSL_Types.Vector3(1.2, llGetMeAFloat(), 4.4);
            LSL_Types.Quaternion x = new LSL_Types.Quaternion(0.1, 0.1, one + 2, 0.9);
            y = new LSL_Types.Vector3(0.1, 0.1, 1.1 - three - two + eight * 8);
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestVectorMemberAccess()
        {
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
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            LSL_Types.Vector3 y = new LSL_Types.Vector3(1.2, llGetMeAFloat(), 4.4);
            x = y.x + 1.1;
            y.x = 1.1;
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestExpressionInParentheses()
        {
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
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            LSL_Types.LSLInteger y = -3;
            LSL_Types.LSLInteger x = 14 + 6;
            y = 12 + 45 + 20 + x + (23 + 1) + x + y;
            y = (12 + -45 + -20 + x + 23) + -1 + x + y;
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestIncrementDecrementOperator()
        {
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
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            LSL_Types.LSLInteger y = -3;
            LSL_Types.LSLInteger x = 14 + 6;
            y = 12 + 45 + 20 + x++ + (23 + 1) + ++x + --y;
            y = (12 + -45 + -20 + x-- + 23) + -1 + x-- + ++y;
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestLists()
        {
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
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            LSL_Types.list l = new LSL_Types.list();
            LSL_Types.list m = new LSL_Types.list(1, two, ""three"", new LSL_Types.Vector3(4.0, 4.0, 4.0), 5 + 5);
            llCallSomeFunc(1, llAnotherFunc(), new LSL_Types.list(1, 2, 3));
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestIfStatement()
        {
            string input = @"// let's test if statements

default
{
    touch_start(integer num_detected)
    {
        integer x = 1;

        if(x) llSay(0, ""Hello"");
        if(1) 
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
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            LSL_Types.LSLInteger x = 1;
            if (x)
                llSay(0, ""Hello"");
            if (1)
            {
                llSay(0, ""Hi"");
                LSL_Types.LSLInteger r = 3;
                return ;
            }
            if (f(x))
                llSay(0, ""f(x) is true"");
            else
                llSay(0, ""f(x) is false"");
            if (x + y)
                llSay(0, ""x + y is true"");
            else
            if (y - x)
                llSay(0, ""y - x is true"");
            else
                llSay(0, ""Who needs x and y anyway?"");
            if (x * y)
                llSay(0, ""x * y is true"");
            else
            if (y / x)
            {
                llSay(0, ""uh-oh, y / x is true, exiting"");
                return ;
            }
            else
                llSay(0, ""Who needs x and y anyway?"");
            if (x % y)
                llSay(0, ""x is true"");
            else
            if (y & x)
                llSay(0, ""y is true"");
            else
            if (z | x)
                llSay(0, ""z is true"");
            else
            if (a * (b + x))
                llSay(0, ""a is true"");
            else
            if (b)
                llSay(0, ""b is true"");
            else
            if (v)
                llSay(0, ""v is true"");
            else
                llSay(0, ""Everything is lies!"");
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestIfElseStatement()
        {
            string input = @"// let's test complex logical expressions

default
{
    touch_start(integer num_detected)
    {
        integer x = 1;
        integer y = 0;

        if(x && y) llSay(0, ""Hello"");
        if(x || y) 
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
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            LSL_Types.LSLInteger x = 1;
            LSL_Types.LSLInteger y = 0;
            if (x && y)
                llSay(0, ""Hello"");
            if (x || y)
            {
                llSay(0, ""Hi"");
                LSL_Types.LSLInteger r = 3;
                return ;
            }
            if (x && y || z)
                llSay(0, ""x is true"");
            else
                llSay(0, ""x is false"");
            if (x == y)
                llSay(0, ""x is true"");
            else
            if (y < x)
                llSay(0, ""y is true"");
            else
                llSay(0, ""Who needs x and y anyway?"");
            if (x > y)
                llSay(0, ""x is true"");
            else
            if (y <= x)
            {
                llSay(0, ""uh-oh, y is true, exiting"");
                return ;
            }
            else
                llSay(0, ""Who needs x and y anyway?"");
            if (x >= y)
                llSay(0, ""x is true"");
            else
            if (y != x)
                llSay(0, ""y is true"");
            else
            if (!z)
                llSay(0, ""z is true"");
            else
            if (!(a && b))
                llSay(0, ""a is true"");
            else
            if (b)
                llSay(0, ""b is true"");
            else
            if (v)
                llSay(0, ""v is true"");
            else
                llSay(0, ""Everything is lies!"");
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestWhileLoop()
        {
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
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            LSL_Types.LSLInteger x = 1;
            LSL_Types.LSLInteger y = 0;
            while (x)
                llSay(0, ""To infinity, and beyond!"");
            while (0 || (x && 0))
            {
                llSay(0, ""Never say never."");
                return ;
            }
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestDoWhileLoop()
        {
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
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            LSL_Types.LSLInteger x = 1;
            LSL_Types.LSLInteger y = 0;
            do
                llSay(0, ""And we're doing..."");
            while (x);
            do
            {
                llSay(0, ""I like it here. I wish we could stay here forever."");
                y--;
            }
            while (y);
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestForLoop()
        {
            string input = @"// let's test for loops

default
{
    touch_start(integer num_detected)
    {
        integer x = 1;
        integer y = 0;

        for(x = 10; x >= 0; x--)
        {
            llOwnerSay(""Launch in T minus "" + x);
            IncreaseRocketPower();
        }

        for(x = 0, y = 6; y > 0 && x != y; x++, y--) llOwnerSay(""Hi "" + x + "", "" + y);
        for(x = 0, y = 6; ! y; x++,y--) llOwnerSay(""Hi "" + x + "", "" + y);
    }
}
";
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            LSL_Types.LSLInteger x = 1;
            LSL_Types.LSLInteger y = 0;
            for (x = 10; x >= 0; x--)
            {
                llOwnerSay(""Launch in T minus "" + x);
                IncreaseRocketPower();
            }
            for (x = 0, y = 6; y > 0 && x != y; x++, y--)
                llOwnerSay(""Hi "" + x + "", "" + y);
            for (x = 0, y = 6; !y; x++, y--)
                llOwnerSay(""Hi "" + x + "", "" + y);
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestFloatsWithTrailingDecimal()
        {
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
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            LSL_Types.LSLFloat y = 1.0;
            y = 1.0E3;
            y = 1.0e3;
            y = 1.0E+3;
            y = 1.0e+3;
            y = 1.0E-3;
            y = 1.0e-3;
            y = -1.0E3;
            y = -1.0e3;
            y = -1.0E+3;
            y = -1.0e+3;
            y = -1.0E-3;
            y = -1.0e-3;
            y = 12.0 + -1.0E3 - 1.0e-2;
            LSL_Types.Vector3 v = new LSL_Types.Vector3(0.0, 0.0, 0.0);
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestUnaryAndBinaryOperators()
        {
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
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            LSL_Types.LSLInteger x = 2;
            LSL_Types.LSLInteger y = 1;
            LSL_Types.LSLInteger z = x ^ y;
            x = ~z;
            x = ~(y && z);
            y = x >> z;
            z = y << x;
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestTypecasts()
        {
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
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            LSL_Types.LSLString s = """";
            LSL_Types.LSLInteger x = 1;
            s = (LSL_Types.LSLString) (x++);
            s = (LSL_Types.LSLString) (x);
            s = (LSL_Types.LSLString) (new LSL_Types.Vector3(0.0, 0.0, 0.0));
            s = (LSL_Types.LSLString) (new LSL_Types.Quaternion(1.0, 1.0, 1.0, 1.0));
            s = (LSL_Types.LSLInteger) (""1"");
            s = (LSL_Types.LSLString) (llSomethingThatReturnsInteger());
            s = (LSL_Types.LSLString) (134);
            s = (LSL_Types.LSLString) (x ^ y | (z && l)) + (LSL_Types.LSLString) (x + y - 13);
            llOwnerSay(""s is: "" + s);
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestStates()
        {
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
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            llSay(0, ""Going to state 'statetwo'"");
            state(""statetwo"");
        }
        public void statetwo_event_state_entry()
        {
            llSay(0, ""Going to the default state"");
            state(""default"");
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestHexIntegerConstants()
        {
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
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            LSL_Types.LSLInteger x = 0x23;
            LSL_Types.LSLInteger x = 0x2f34B;
            LSL_Types.LSLInteger x = 0x2F34b;
            LSL_Types.LSLInteger x = 0x2F34B;
            LSL_Types.LSLInteger x = 0x2f34b;
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestJumps()
        {
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
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            goto here;
            llOwnerSay(""Uh oh, the jump didn't work"");
            here:
            llOwnerSay(""After the jump"");
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestImplicitVariableInitialization()
        {
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
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            LSL_Types.LSLInteger i = 0;
            LSL_Types.LSLInteger j = 14;
            LSL_Types.LSLFloat f = 0.0;
            LSL_Types.LSLFloat g = 14.0;
            LSL_Types.LSLString s = """";
            LSL_Types.LSLString t = ""Hi there"";
            LSL_Types.list l = new LSL_Types.list();
            LSL_Types.list m = new LSL_Types.list(1, 2, 3);
            LSL_Types.Vector3 v = new LSL_Types.Vector3(0.0, 0.0, 0.0);
            LSL_Types.Vector3 w = new LSL_Types.Vector3(1.0, 0.1, 0.5);
            LSL_Types.Quaternion r = new LSL_Types.Quaternion(0.0, 0.0, 0.0, 0.0);
            LSL_Types.Quaternion u = new LSL_Types.Quaternion(0.8, 0.7, 0.6, llSomeFunc());
            LSL_Types.LSLString k = """";
            LSL_Types.LSLString n = ""ping"";
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestMultipleEqualsExpression()
        {
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
            string expected = @"
        public void default_event_touch_start(LSL_Types.LSLInteger num_detected)
        {
            LSL_Types.LSLInteger x = 0;
            LSL_Types.LSLInteger y = 0;
            x = y = 5;
            x += y -= 5;
            llOwnerSay(""x is: "" + (LSL_Types.LSLString) (x) + "", y is: "" + (LSL_Types.LSLString) (y));
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        public void TestUnaryExpressionLastInVectorConstant()
        {
            string input = @"// let's test unary expressions some more

default
{
    state_entry()
    {
        vector v = <x,y,-0.5>;
    }
}
";
            string expected = @"
        public void default_event_state_entry()
        {
            LSL_Types.Vector3 v = new LSL_Types.Vector3(x, y, -0.5);
        }
";

            CSCodeGenerator cg = new CSCodeGenerator();
            string output = cg.Convert(input);
            Assert.AreEqual(expected, output);
        }

        [Test]
        [ExpectedException("Tools.CSToolsException")]
        public void TestSyntaxError()
        {
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
            catch (Tools.CSToolsException e)
            {
                // The syntax error is on line 6, char 5 (expected ';', found
                // '}').
                Regex r = new Regex("Line ([0-9]+), char ([0-9]+)");
                Match m = r.Match(e.Message);
                Assert.AreEqual("6", m.Groups[1].Value);
                Assert.AreEqual("5", m.Groups[2].Value);

                throw;
            }
        }
    }
}
