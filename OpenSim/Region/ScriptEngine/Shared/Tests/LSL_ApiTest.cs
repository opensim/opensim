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
using OpenSim.Tests.Common;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.Framework.Scenes;
using Nini.Config;
using OpenSim.Region.ScriptEngine.Shared.Api;
using OpenMetaverse;
using System;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Region.ScriptEngine.Shared.Tests
{
    /// <summary>
    /// Tests for LSL_Api
    /// </summary>
    [TestFixture, LongRunning]
    public class LSL_ApiTest
    {

        private const double ANGLE_ACCURACY_IN_RADIANS = 1E-6;
        private const double VECTOR_COMPONENT_ACCURACY = 0.0000005d;
        private LSL_Api m_lslApi;

        [SetUp]
        public void SetUp()
        {

            IConfigSource initConfigSource = new IniConfigSource();
            IConfig config = initConfigSource.AddConfig("XEngine");
            config.Set("Enabled", "true");

            Scene scene = SceneSetupHelpers.SetupScene();
            SceneObjectPart part = SceneSetupHelpers.AddSceneObject(scene);

            XEngine.XEngine engine = new XEngine.XEngine();
            engine.Initialise(initConfigSource);
            engine.AddRegion(scene);

            m_lslApi = new LSL_Api();
            m_lslApi.Initialize(engine, part, part.LocalId, part.UUID);

        }

        [Test]
        public void TestllAngleBetween()
        {
            CheckllAngleBetween(new Vector3(1, 0, 0), 0);
            CheckllAngleBetween(new Vector3(1, 0, 0), 90);
            CheckllAngleBetween(new Vector3(1, 0, 0), 180);

            CheckllAngleBetween(new Vector3(0, 1, 0), 0);
            CheckllAngleBetween(new Vector3(0, 1, 0), 90);
            CheckllAngleBetween(new Vector3(0, 1, 0), 180);

            CheckllAngleBetween(new Vector3(0, 0, 1), 0);
            CheckllAngleBetween(new Vector3(0, 0, 1), 90);
            CheckllAngleBetween(new Vector3(0, 0, 1), 180);

            CheckllAngleBetween(new Vector3(1, 1, 1), 0);
            CheckllAngleBetween(new Vector3(1, 1, 1), 90);
            CheckllAngleBetween(new Vector3(1, 1, 1), 180);
        }

        private void CheckllAngleBetween(Vector3 axis,float originalAngle)
        {
            Quaternion rotation1 = Quaternion.CreateFromAxisAngle(axis, 0);
            Quaternion rotation2 = Quaternion.CreateFromAxisAngle(axis, ToRadians(originalAngle));

            double deducedAngle = FromLslFloat(m_lslApi.llAngleBetween(ToLslQuaternion(rotation2), ToLslQuaternion(rotation1)));

            Assert.Greater(deducedAngle, ToRadians(originalAngle) - ANGLE_ACCURACY_IN_RADIANS);
            Assert.Less(deducedAngle, ToRadians(originalAngle) + ANGLE_ACCURACY_IN_RADIANS);
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
            // 180, 90 and zero degree rotations.
            CheckllRot2Euler(new LSL_Types.Quaternion(1.0f, 0.0f, 0.0f, 0.0f), new LSL_Types.Vector3(Math.PI, 0.0f, 0.0f));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.0f, 1.0f, 0.0f, 0.0f), new LSL_Types.Vector3(Math.PI, 0.0f, Math.PI));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.0f, 0.0f, 1.0f, 0.0f), new LSL_Types.Vector3(0.0f, 0.0f, Math.PI));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.0f, 0.0f, 0.0f, 1.0f), new LSL_Types.Vector3(0.0f, 0.0f, 0.0f));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.5f, -0.5f, 0.5f, 0.5f), new LSL_Types.Vector3(0, -Math.PI / 2.0f, Math.PI / 2.0f));
            CheckllRot2Euler(new LSL_Types.Quaternion(-0.707107f, 0.0f, 0.0f, -0.707107f), new LSL_Types.Vector3(Math.PI / 2.0f, 0.0f, 0.0f));
            // A couple of messy rotations.
            CheckllRot2Euler(new LSL_Types.Quaternion(1.0f, 5.651f, -3.1f, 67.023f), new LSL_Types.Vector3(0.037818f, 0.166447f, -0.095595f));
            CheckllRot2Euler(new LSL_Types.Quaternion(0.719188f, -0.408934f, -0.363998f, -0.427841f), new LSL_Types.Vector3(-1.954769f, -0.174533f, 1.151917f));
        }

        private void CheckllRot2Euler(LSL_Types.Quaternion rot, LSL_Types.Vector3 eulerCheck)
        {
            // Call LSL function to convert quaternion rotaion to euler radians.
            LSL_Types.Vector3 eulerCalc = m_lslApi.llRot2Euler(rot);
            // Check upper and lower bounds of x, y and z.
            // This type of check is performed as opposed to comparing for equal numbers, in order to allow slight
            // differences in accuracy.
            Assert.Greater(eulerCalc.x, eulerCheck.x - ANGLE_ACCURACY_IN_RADIANS, "TestllRot2Euler X lower bounds check fail");
            Assert.Less(eulerCalc.x, eulerCheck.x + ANGLE_ACCURACY_IN_RADIANS, "TestllRot2Euler X upper bounds check fail");
            Assert.Greater(eulerCalc.y, eulerCheck.y - ANGLE_ACCURACY_IN_RADIANS, "TestllRot2Euler Y lower bounds check fail");
            Assert.Less(eulerCalc.y, eulerCheck.y + ANGLE_ACCURACY_IN_RADIANS, "TestllRot2Euler Y upper bounds check fail");
            Assert.Greater(eulerCalc.z, eulerCheck.z - ANGLE_ACCURACY_IN_RADIANS, "TestllRot2Euler Z lower bounds check fail");
            Assert.Less(eulerCalc.z, eulerCheck.z + ANGLE_ACCURACY_IN_RADIANS, "TestllRot2Euler Z upper bounds check fail");
        }

        [Test]
        // llVecNorm test.
        public void TestllVecNorm()
        {
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
