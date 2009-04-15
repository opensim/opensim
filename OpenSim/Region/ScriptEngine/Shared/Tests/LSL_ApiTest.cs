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
using NUnit.Framework;
using OpenSim.Tests.Common;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Tests.Common.Setup;
using OpenSim.Region.Framework.Scenes;
using Nini.Config;
using OpenSim.Region.ScriptEngine.Shared.Api;
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

        private const double ANGLE_ACCURACY_IN_RADIANS = 1E-6;
        private LSL_Api m_lslApi;

        [SetUp]
        public void SetUp()
        {

            IniConfigSource initConfigSource = new IniConfigSource();
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
            TestllAngleBetween(new Vector3(1, 0, 0), 0);
            TestllAngleBetween(new Vector3(1, 0, 0), 90);
            TestllAngleBetween(new Vector3(1, 0, 0), 180);

            TestllAngleBetween(new Vector3(0, 1, 0), 0);
            TestllAngleBetween(new Vector3(0, 1, 0), 90);
            TestllAngleBetween(new Vector3(0, 1, 0), 180);

            TestllAngleBetween(new Vector3(0, 0, 1), 0);
            TestllAngleBetween(new Vector3(0, 0, 1), 90);
            TestllAngleBetween(new Vector3(0, 0, 1), 180);

            TestllAngleBetween(new Vector3(1, 1, 1), 0);
            TestllAngleBetween(new Vector3(1, 1, 1), 90);
            TestllAngleBetween(new Vector3(1, 1, 1), 180);
        }

        private void TestllAngleBetween(Vector3 axis,float originalAngle)
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

    }
}
