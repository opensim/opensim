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
using System.Text.RegularExpressions;
using NUnit.Framework;
using OpenSim.Region.ScriptEngine.Shared.CodeTools;
using OpenSim.Tests.Common;

namespace OpenSim.Region.ScriptEngine.Shared.Tests
{
    public class LSL_EventTests : OpenSimTestCase
    {
        CSCodeGenerator m_cg = new CSCodeGenerator();

        [Test]
        public void TestBadEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestCompile("default { bad() {} }", true);
        }

        [Test]
        public void TestAttachEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestKeyArgEvent("attach");
        }

        [Test]
        public void TestObjectRezEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestKeyArgEvent("object_rez");
        }

        [Test]
        public void TestMovingEndEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestVoidArgEvent("moving_end");
        }

        [Test]
        public void TestMovingStartEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestVoidArgEvent("moving_start");
        }

        [Test]
        public void TestNoSensorEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestVoidArgEvent("no_sensor");
        }

        [Test]
        public void TestNotAtRotTargetEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestVoidArgEvent("not_at_rot_target");
        }

        [Test]
        public void TestNotAtTargetEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestVoidArgEvent("not_at_target");
        }

        [Test]
        public void TestStateEntryEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestVoidArgEvent("state_entry");
        }

        [Test]
        public void TestStateExitEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestVoidArgEvent("state_exit");
        }

        [Test]
        public void TestTimerEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestVoidArgEvent("timer");
        }

        private void TestVoidArgEvent(string eventName)
        {
            TestCompile("default { " + eventName + "() {} }", false);
            TestCompile("default { " + eventName + "(integer n) {} }", true);
        }

        [Test]
        public void TestChangedEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestIntArgEvent("changed");
        }

        [Test]
        public void TestCollisionEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestIntArgEvent("collision");
        }

        [Test]
        public void TestCollisionStartEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestIntArgEvent("collision_start");
        }

        [Test]
        public void TestCollisionEndEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestIntArgEvent("collision_end");
        }

        [Test]
        public void TestOnRezEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestIntArgEvent("on_rez");
        }

        [Test]
        public void TestRunTimePermissionsEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestIntArgEvent("run_time_permissions");
        }

        [Test]
        public void TestSensorEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestIntArgEvent("sensor");
        }

        [Test]
        public void TestTouchEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestIntArgEvent("touch");
        }

        [Test]
        public void TestTouchStartEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestIntArgEvent("touch_start");
        }

        [Test]
        public void TestTouchEndEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestIntArgEvent("touch_end");
        }

        [Test]
        public void TestLandCollisionEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestVectorArgEvent("land_collision");
        }

        [Test]
        public void TestLandCollisionStartEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestVectorArgEvent("land_collision_start");
        }

        [Test]
        public void TestLandCollisionEndEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestVectorArgEvent("land_collision_end");
        }

        [Test]
        public void TestAtRotTargetEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestIntRotRotArgEvent("at_rot_target");
        }

        [Test]
        public void TestAtTargetEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestIntVecVecArgEvent("at_target");
        }

        [Test]
        public void TestControlEvent()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            TestKeyIntIntArgEvent("control");
        }

        private void TestIntArgEvent(string eventName)
        {
            TestCompile("default { " + eventName + "(integer n) {} }", false);
            TestCompile("default { " + eventName + "{{}} }", true);
            TestCompile("default { " + eventName + "(string s) {{}} }", true);
            TestCompile("default { " + eventName + "(integer n, integer o) {{}} }", true);
        }

        private void TestKeyArgEvent(string eventName)
        {
            TestCompile("default { " + eventName + "(key k) {} }", false);
            TestCompile("default { " + eventName + "{{}} }", true);
            TestCompile("default { " + eventName + "(string s) {{}} }", true);
            TestCompile("default { " + eventName + "(key k, key l) {{}} }", true);
        }

        private void TestVectorArgEvent(string eventName)
        {
            TestCompile("default { " + eventName + "(vector v) {} }", false);
            TestCompile("default { " + eventName + "{{}} }", true);
            TestCompile("default { " + eventName + "(string s) {{}} }", true);
            TestCompile("default { " + eventName + "(vector v, vector w) {{}} }", true);
        }

        private void TestIntRotRotArgEvent(string eventName)
        {
            TestCompile("default { " + eventName + "(integer n, rotation r, rotation s) {} }", false);
            TestCompile("default { " + eventName + "{{}} }", true);
            TestCompile("default { " + eventName + "(string s) {{}} }", true);
            TestCompile("default { " + eventName + "(integer n, rotation r, rotation s, rotation t) {{}} }", true);
        }

        private void TestIntVecVecArgEvent(string eventName)
        {
            TestCompile("default { " + eventName + "(integer n, vector v, vector w) {} }", false);
            TestCompile("default { " + eventName + "{{}} }", true);
            TestCompile("default { " + eventName + "(string s) {{}} }", true);
            TestCompile("default { " + eventName + "(integer n, vector v, vector w, vector x) {{}} }", true);
        }

        private void TestKeyIntIntArgEvent(string eventName)
        {
            TestCompile("default { " + eventName + "(key k, integer n, integer o) {} }", false);
            TestCompile("default { " + eventName + "{{}} }", true);
            TestCompile("default { " + eventName + "(string s) {{}} }", true);
            TestCompile("default { " + eventName + "(key k, integer n, integer o, integer p) {{}} }", true);
        }

        private void TestCompile(string script, bool expectException)
        {
            bool gotException = false;
            Exception ge = null;

            try
            {
                m_cg.Convert(script);
            }
            catch (Exception e)
            {
                gotException = true;
                ge = e;
            }

            Assert.That(
                gotException,
                Is.EqualTo(expectException),
                "Failed on {0}, exception {1}", script, ge != null ? ge.ToString() : "n/a");
        }
    }
}