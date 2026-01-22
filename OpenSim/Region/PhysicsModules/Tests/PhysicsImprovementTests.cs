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
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Region.PhysicsModules.SharedBase;

namespace OpenSim.Region.PhysicsModules.Tests
{
    /// <summary>
    /// Tests for physics improvements and bug fixes
    /// </summary>
    [TestFixture]
    public class PhysicsImprovementTests
    {
        [Test]
        public void TestPhysicsProfilerBasicFunctionality()
        {
            // Test that the profiler can be enabled and records timing
            PhysicsProfiler.Enabled = true;
            PhysicsProfiler.Reset();
            
            using (PhysicsProfiler.StartTiming("TestOperation"))
            {
                // Simulate some work
                System.Threading.Thread.Sleep(10);
            }
            
            var metrics = PhysicsProfiler.GetMetrics();
            Assert.That(metrics.ContainsKey("TestOperation"), "Profiler should record test operation");
            Assert.That(metrics["TestOperation"].CallCount, Is.EqualTo(1), "Should record one call");
            Assert.That(metrics["TestOperation"].TotalTime, Is.GreaterThan(5), "Should record meaningful time");
        }
        
        [Test]
        public void TestBuoyancyClampingValidation()
        {
            // Test that buoyancy values are properly clamped
            // This would normally require a full physics scene setup, 
            // so we test the utility functions instead
            
            float testBuoyancy1 = 2.0f; // Above maximum
            float testBuoyancy2 = -2.0f; // Below minimum
            float testBuoyancy3 = 0.5f; // Valid range
            
            float clamped1 = Utils.Clamp(testBuoyancy1, -1.0f, 1.0f);
            float clamped2 = Utils.Clamp(testBuoyancy2, -1.0f, 1.0f);
            float clamped3 = Utils.Clamp(testBuoyancy3, -1.0f, 1.0f);
            
            Assert.That(clamped1, Is.EqualTo(1.0f), "Buoyancy above 1.0 should be clamped to 1.0");
            Assert.That(clamped2, Is.EqualTo(-1.0f), "Buoyancy below -1.0 should be clamped to -1.0");
            Assert.That(clamped3, Is.EqualTo(0.5f), "Valid buoyancy should remain unchanged");
        }
        
        [Test]
        public void TestQuaternionNormalization()
        {
            // Test that quaternion normalization works correctly
            Quaternion testQuat = new Quaternion(1.0f, 1.0f, 1.0f, 1.0f); // Unnormalized
            
            float originalLength = testQuat.Length();
            testQuat.Normalize();
            float normalizedLength = testQuat.Length();
            
            Assert.That(originalLength, Is.GreaterThan(1.0f), "Original quaternion should be unnormalized");
            Assert.That(normalizedLength, Is.EqualTo(1.0f).Within(0.0001f), "Normalized quaternion should have unit length");
        }
        
        [Test]
        public void TestVector3FiniteValidation()
        {
            // Test Vector3.IsFinite() functionality for invalid gravity detection
            Vector3 validVector = new Vector3(1.0f, 2.0f, 3.0f);
            Vector3 nanVector = new Vector3(float.NaN, 2.0f, 3.0f);
            Vector3 infinityVector = new Vector3(float.PositiveInfinity, 2.0f, 3.0f);
            
            Assert.That(validVector.IsFinite(), Is.True, "Valid vector should be finite");
            Assert.That(nanVector.IsFinite(), Is.False, "Vector with NaN should not be finite");
            Assert.That(infinityVector.IsFinite(), Is.False, "Vector with infinity should not be finite");
        }
        
        [Test]
        public void TestPerformanceMetricsAccumulation()
        {
            // Test that performance metrics accumulate correctly
            PhysicsProfiler.Enabled = true;
            PhysicsProfiler.Reset();
            
            // Record multiple operations
            PhysicsProfiler.RecordTiming("TestOp", 10.0);
            PhysicsProfiler.RecordTiming("TestOp", 20.0);
            PhysicsProfiler.RecordTiming("TestOp", 5.0);
            
            var metrics = PhysicsProfiler.GetMetrics();
            var testOpMetrics = metrics["TestOp"];
            
            Assert.That(testOpMetrics.CallCount, Is.EqualTo(3), "Should record three calls");
            Assert.That(testOpMetrics.TotalTime, Is.EqualTo(35.0), "Should sum total time correctly");
            Assert.That(testOpMetrics.MaxTime, Is.EqualTo(20.0), "Should track maximum time");
            Assert.That(testOpMetrics.MinTime, Is.EqualTo(5.0), "Should track minimum time");
        }
        
        [TearDown]
        public void TearDown()
        {
            // Clean up after tests
            PhysicsProfiler.Enabled = false;
            PhysicsProfiler.Reset();
        }
    }
}