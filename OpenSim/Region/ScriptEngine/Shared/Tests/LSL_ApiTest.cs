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
using NUnit.Framework;
using OpenSim.Framework;
using OpenSim.Tests.Common;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.Framework.Scenes;
using Nini.Config;
using OpenSim.Region.ScriptEngine.Shared.Api;
using OpenSim.Region.ScriptEngine.Shared.Instance;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenMetaverse;
using System;

namespace OpenSim.Region.ScriptEngine.Shared.Tests
{
    /// <summary>
    /// Tests for LSL_Api
    /// </summary>
    [TestFixture, LongRunning]
    public class LSL_ApiTest
    {
        private const double VECTOR_COMPONENT_ACCURACY = 0.0000005d;
        private const double ANGLE_ACCURACY_IN_RADIANS = 1E-6;
        private LSL_Api m_lslApi;

        [SetUp]
        public void SetUp()
        {
            IConfigSource initConfigSource = new IniConfigSource();
            IConfig config = initConfigSource.AddConfig("XEngine");
            config.Set("Enabled", "true");

            Scene scene = new SceneHelpers().SetupScene();
            SceneObjectPart part = SceneHelpers.AddSceneObject(scene).RootPart;

            XEngine.XEngine engine = new XEngine.XEngine();
            engine.Initialise(initConfigSource);
            engine.AddRegion(scene);

            m_lslApi = new LSL_Api();
            m_lslApi.Initialize(engine, part, null);
        }

        [Test]
        public void TestllAngleBetween()
        {
            TestHelpers.InMethod();

            CheckllAngleBetween(new Vector3(1, 0, 0), 0,   1, 1);
            CheckllAngleBetween(new Vector3(1, 0, 0), 90,  1, 1);
            CheckllAngleBetween(new Vector3(1, 0, 0), 180, 1, 1);

            CheckllAngleBetween(new Vector3(0, 1, 0), 0,   1, 1);
            CheckllAngleBetween(new Vector3(0, 1, 0), 90,  1, 1);
            CheckllAngleBetween(new Vector3(0, 1, 0), 180, 1, 1);

            CheckllAngleBetween(new Vector3(0, 0, 1), 0,   1, 1);
            CheckllAngleBetween(new Vector3(0, 0, 1), 90,  1, 1);
            CheckllAngleBetween(new Vector3(0, 0, 1), 180, 1, 1);

            CheckllAngleBetween(new Vector3(1, 1, 1), 0,   1, 1);
            CheckllAngleBetween(new Vector3(1, 1, 1), 90,  1, 1);
            CheckllAngleBetween(new Vector3(1, 1, 1), 180, 1, 1);

            CheckllAngleBetween(new Vector3(1, 0, 0), 0,   1.6f, 1.8f);
            CheckllAngleBetween(new Vector3(1, 0, 0), 90,  0.3f, 3.9f);
            CheckllAngleBetween(new Vector3(1, 0, 0), 180, 8.8f, 7.4f);

            CheckllAngleBetween(new Vector3(0, 1, 0), 0,   9.8f, -9.4f);
            CheckllAngleBetween(new Vector3(0, 1, 0), 90,  8.4f, -8.2f);
            CheckllAngleBetween(new Vector3(0, 1, 0), 180, 0.4f, -5.8f);

            CheckllAngleBetween(new Vector3(0, 0, 1), 0,   -6.8f, 3.4f);
            CheckllAngleBetween(new Vector3(0, 0, 1), 90,  -3.6f, 5.6f);
            CheckllAngleBetween(new Vector3(0, 0, 1), 180, -3.8f, 1.1f);

            CheckllAngleBetween(new Vector3(1, 1, 1), 0,   -7.7f, -2.0f);
            CheckllAngleBetween(new Vector3(1, 1, 1), 90,  -3.0f, -9.1f);
            CheckllAngleBetween(new Vector3(1, 1, 1), 180, -7.9f, -8.0f);
        }

        private void CheckllAngleBetween(Vector3 axis,float originalAngle, float denorm1, float denorm2)
        {
            Quaternion rotation1 = Quaternion.CreateFromAxisAngle(axis, 0);
            Quaternion rotation2 = Quaternion.CreateFromAxisAngle(axis, ToRadians(originalAngle));
            rotation1 *= denorm1;
            rotation2 *= denorm2;

            double deducedAngle = FromLslFloat(m_lslApi.llAngleBetween(ToLslQuaternion(rotation2), ToLslQuaternion(rotation1)));

            Assert.That(deducedAngle, Is.EqualTo(ToRadians(originalAngle)).Within(ANGLE_ACCURACY_IN_RADIANS), "TestllAngleBetween check fail");
        }

        #region Conversions to and from LSL_Types

        private float ToRadians(double degrees)
        {
            return (float)(Math.PI * degrees / 180);
        }

        // private double FromRadians(float radians)
        // {
        //     return radians * 180 / Math.PI;
        // }

        private double FromLslFloat(LSL_Types.LSLFloat lslFloat)
        {
            return lslFloat.value;
        }

        // private LSL_Types.LSLFloat ToLslFloat(double value)
        // {
        //     return new LSL_Types.LSLFloat(value);
        // }

        // private Quaternion FromLslQuaternion(LSL_Types.Quaternion lslQuaternion)
        // {
        //     return new Quaternion((float)lslQuaternion.x, (float)lslQuaternion.y, (float)lslQuaternion.z, (float)lslQuaternion.s);
        // }

        private LSL_Types.Quaternion ToLslQuaternion(Quaternion quaternion)
        {
            return new LSL_Types.Quaternion(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W);
        }

        #endregion

        [Test]
        // llRot2Euler test.
        public void TestllRot2Euler()
        {
            TestHelpers.InMethod();

            // 180, 90 and zero degree rotations.
            CheckllRot2Euler(new LSL_Types.Quaternion(0.0f, 0.0f, 0.0f, 1.0f));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.0f, 0.0f, 0.707107f, 0.707107f));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.0f, 0.0f, 1.0f, 0.0f));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.0f, 0.0f, 0.707107f, -0.707107f));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.707107f, 0.0f, 0.0f, 0.707107f));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.5f, -0.5f, 0.5f, 0.5f));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.0f, -0.707107f, 0.707107f, 0.0f));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.5f, -0.5f, 0.5f, -0.5f));
            CheckllRot2Euler(new LSL_Types.Quaternion(1.0f, 0.0f, 0.0f, 0.0f));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.707107f, -0.707107f, 0.0f, 0.0f));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.0f, -1.0f, 0.0f, 0.0f));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.707107f, -0.707107f, 0.0f, 0.0f));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.707107f, 0.0f, 0.0f, -0.707107f));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.5f, -0.5f, -0.5f, -0.5f));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.0f, -0.707107f, -0.707107f, 0.0f));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.5f, -0.5f, -0.5f, 0.5f));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.0f, -0.707107f, 0.0f, 0.707107f));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.5f, -0.5f, 0.5f, 0.5f));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.707107f, 0.0f, 0.707107f, 0.0f));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.5f, 0.5f, 0.5f, -0.5f));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.0f, -0.707107f, 0.0f, -0.707107f));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.5f, -0.5f, -0.5f, -0.5f));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.707107f, 0.0f, -0.707107f, 0.0f));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.5f, 0.5f, -0.5f, 0.5f));

            // A couple of messy rotations.
            CheckllRot2Euler(new LSL_Types.Quaternion(1.0f, 5.651f, -3.1f, 67.023f));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.719188f, -0.408934f, -0.363998f, -0.427841f));

            // Some deliberately malicious rotations (intended on provoking singularity errors)
            // The "f" suffexes are deliberately omitted.
            CheckllRot2Euler(new LSL_Types.Quaternion(0.50001f, 0.50001f, 0.50001f, 0.50001f));
            // More malice. The "f" suffixes are deliberately omitted.
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.701055, 0.092296, 0.701055, -0.092296));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.183005, -0.683010, 0.183005, 0.683010));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.430460, -0.560982, 0.430460, 0.560982));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.701066, 0.092301, -0.701066, 0.092301));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.183013, -0.683010, 0.183013, 0.683010));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.183005, -0.683014, -0.183005, -0.683014));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.353556, 0.612375, 0.353556, -0.612375));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.353554, -0.612385, -0.353554, 0.612385));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.560989, 0.430450, 0.560989, -0.430450));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.183013, 0.683009, -0.183013, 0.683009));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.430457, -0.560985, -0.430457, 0.560985));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.353552, 0.612360, -0.353552, -0.612360));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.499991, 0.500003, 0.499991, -0.500003));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.353555, -0.612385, -0.353555, -0.612385));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.701066, -0.092301, -0.701066, 0.092301));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.499991, 0.500007, 0.499991, -0.500007));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.683002, 0.183016, -0.683002, 0.183016));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.430458, 0.560982, 0.430458, 0.560982));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.499991, -0.500003, -0.499991, 0.500003));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.183009, 0.683011, -0.183009, 0.683011));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.560975, -0.430457, 0.560975, -0.430457));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.701055, 0.092300, 0.701055, 0.092300));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.560990, 0.430459, -0.560990, 0.430459));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.092302, -0.701059, -0.092302, -0.701059));
        }

        /// <summary>
        /// Check an llRot2Euler conversion.
        /// </summary>
        /// <remarks>
        /// Testing Rot2Euler this way instead of comparing against expected angles because
        /// 1. There are several ways to get to the original Quaternion. For example a rotation
        ///    of PI and -PI will give the same result. But PI and -PI aren't equal.
        /// 2. This method checks to see if the calculated angles from a quaternion can be used
        ///    to create a new quaternion to produce the same rotation.
        /// However, can't compare the newly calculated quaternion against the original because
        /// once again, there are multiple quaternions that give the same result. For instance
        ///  <X, Y, Z, S> == <-X, -Y, -Z, -S>.  Additionally, the magnitude of S can be changed
        /// and will still result in the same rotation if the values for X, Y, Z are also changed
        /// to compensate.
        /// However, if two quaternions represent the same rotation, then multiplying the first
        /// quaternion by the conjugate of the second, will give a third quaternion representing
        /// a zero rotation. This can be tested for by looking at the X, Y, Z values which should
        /// be zero.
        /// </remarks>
        /// <param name="rot"></param>
        private void CheckllRot2Euler(LSL_Types.Quaternion rot)
        {
            // Call LSL function to convert quaternion rotaion to euler radians.
            LSL_Types.Vector3 eulerCalc = m_lslApi.llRot2Euler(rot);
            // Now use the euler radians to recalculate a new quaternion rotation
            LSL_Types.Quaternion newRot = m_lslApi.llEuler2Rot(eulerCalc);
            // Multiple original quaternion by conjugate of quaternion calculated with angles.
            LSL_Types.Quaternion check = rot * new LSL_Types.Quaternion(-newRot.x, -newRot.y, -newRot.z, newRot.s);

            Assert.AreEqual(0.0, check.x, VECTOR_COMPONENT_ACCURACY, "TestllRot2Euler X bounds check fail");
            Assert.AreEqual(0.0, check.y, VECTOR_COMPONENT_ACCURACY, "TestllRot2Euler Y bounds check fail");
            Assert.AreEqual(0.0, check.z, VECTOR_COMPONENT_ACCURACY, "TestllRot2Euler Z bounds check fail");
        }

        [Test]
        public void TestllVecNorm()
        {
            TestHelpers.InMethod();

            // Check special case for normalizing zero vector.
            CheckllVecNorm(new LSL_Types.Vector3(0.0d, 0.0d, 0.0d), new LSL_Types.Vector3(0.0d, 0.0d, 0.0d));
            // Check various vectors.
            CheckllVecNorm(new LSL_Types.Vector3(10.0d, 25.0d, 0.0d), new LSL_Types.Vector3(0.371391d, 0.928477d, 0.0d));
            CheckllVecNorm(new LSL_Types.Vector3(1.0d, 0.0d, 0.0d), new LSL_Types.Vector3(1.0d, 0.0d, 0.0d));
            CheckllVecNorm(new LSL_Types.Vector3(-90.0d, 55.0d, 2.0d), new LSL_Types.Vector3(-0.853128d, 0.521356d, 0.018958d));
            CheckllVecNorm(new LSL_Types.Vector3(255.0d, 255.0d, 255.0d), new LSL_Types.Vector3(0.577350d, 0.577350d, 0.577350d));
        }

        public void CheckllVecNorm(LSL_Types.Vector3 vec, LSL_Types.Vector3 vecNormCheck)
        {
            // Call LSL function to normalize the vector.
            LSL_Types.Vector3 vecNorm = m_lslApi.llVecNorm(vec);
            // Check each vector component against expected result.
            Assert.AreEqual(vecNorm.x, vecNormCheck.x, VECTOR_COMPONENT_ACCURACY, "TestllVecNorm vector check fail on x component");
            Assert.AreEqual(vecNorm.y, vecNormCheck.y, VECTOR_COMPONENT_ACCURACY, "TestllVecNorm vector check fail on y component");
            Assert.AreEqual(vecNorm.z, vecNormCheck.z, VECTOR_COMPONENT_ACCURACY, "TestllVecNorm vector check fail on z component");
        }
    }
}
