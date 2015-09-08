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
 *
 */
using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Region.PhysicsModule.BulletS
{
public abstract class BSMotor
{
    // Timescales and other things can be turned off by setting them to 'infinite'.
    public const float Infinite = 12345.6f;
    public readonly static Vector3 InfiniteVector = new Vector3(BSMotor.Infinite, BSMotor.Infinite, BSMotor.Infinite);

    public BSMotor(string useName)
    {
        UseName = useName;
        PhysicsScene = null;
        Enabled = true;
    }
    public virtual bool Enabled { get; set; }
    public virtual void Reset() { }
    public virtual void Zero() { }
    public virtual void GenerateTestOutput(float timeStep) { }

    // A name passed at motor creation for easily identifyable debugging messages.
    public string UseName { get; private set; }

    // Used only for outputting debug information. Might not be set so check for null.
    public BSScene PhysicsScene { get; set; }
    protected void MDetailLog(string msg, params Object[] parms)
    {
        if (PhysicsScene != null)
        {
            PhysicsScene.DetailLog(msg, parms);
        }
    }
}

// Motor which moves CurrentValue to TargetValue over TimeScale seconds.
// The TargetValue decays in TargetValueDecayTimeScale.
// This motor will "zero itself" over time in that the targetValue will
//    decay to zero and the currentValue will follow it to that zero.
//    The overall effect is for the returned correction value to go from large
//    values to small and eventually zero values.
// TimeScale and TargetDelayTimeScale may be 'infinite' which means no decay.

// For instance, if something is moving at speed X and the desired speed is Y,
//    CurrentValue is X and TargetValue is Y. As the motor is stepped, new
//    values of CurrentValue are returned that approach the TargetValue.
// The feature of decaying TargetValue is so vehicles will eventually
//    come to a stop rather than run forever. This can be disabled by
//    setting TargetValueDecayTimescale to 'infinite'.
// The change from CurrentValue to TargetValue is linear over TimeScale seconds.
public class BSVMotor : BSMotor
{
    // public Vector3 FrameOfReference { get; set; }
    // public Vector3 Offset { get; set; }

    public virtual float TimeScale { get; set; }
    public virtual float TargetValueDecayTimeScale { get; set; }
    public virtual float Efficiency { get; set; }

    public virtual float ErrorZeroThreshold { get; set; }

    public virtual Vector3 TargetValue { get; protected set; }
    public virtual Vector3 CurrentValue { get; protected set; }
    public virtual Vector3 LastError { get; protected set; }

    public virtual bool ErrorIsZero()
    {
        return ErrorIsZero(LastError);
    }
    public virtual bool ErrorIsZero(Vector3 err)
    {
        return (err == Vector3.Zero || err.ApproxEquals(Vector3.Zero, ErrorZeroThreshold));
    }

    public BSVMotor(string useName)
        : base(useName)
    {
        TimeScale = TargetValueDecayTimeScale = BSMotor.Infinite;
        Efficiency = 1f;
        CurrentValue = TargetValue = Vector3.Zero;
        ErrorZeroThreshold = 0.001f;
    }
    public BSVMotor(string useName, float timeScale, float decayTimeScale, float efficiency)
        : this(useName)
    {
        TimeScale = timeScale;
        TargetValueDecayTimeScale = decayTimeScale;
        Efficiency = efficiency;
        CurrentValue = TargetValue = Vector3.Zero;
    }
    public void SetCurrent(Vector3 current)
    {
        CurrentValue = current;
    }
    public void SetTarget(Vector3 target)
    {
        TargetValue = target;
    }
    public override void Zero()
    {
        base.Zero();
        CurrentValue = TargetValue = Vector3.Zero;
    }

    // Compute the next step and return the new current value.
    // Returns the correction needed to move 'current' to 'target'.
    public virtual Vector3 Step(float timeStep)
    {
        if (!Enabled) return TargetValue;

        Vector3 origTarget = TargetValue;       // DEBUG
        Vector3 origCurrVal = CurrentValue;     // DEBUG

        Vector3 correction = Vector3.Zero;
        Vector3 error = TargetValue - CurrentValue;
        if (!ErrorIsZero(error))
        {
            correction = StepError(timeStep, error);

            CurrentValue += correction;

            // The desired value reduces to zero which also reduces the difference with current.
            // If the decay time is infinite, don't decay at all.
            float decayFactor = 0f;
            if (TargetValueDecayTimeScale != BSMotor.Infinite)
            {
                decayFactor = (1.0f / TargetValueDecayTimeScale) * timeStep;
                TargetValue *= (1f - decayFactor);
            }

            MDetailLog("{0},  BSVMotor.Step,nonZero,{1},origCurr={2},origTarget={3},timeStep={4},err={5},corr={6}",
                                BSScene.DetailLogZero, UseName, origCurrVal, origTarget,
                                timeStep, error, correction);
            MDetailLog("{0},  BSVMotor.Step,nonZero,{1},tgtDecayTS={2},decayFact={3},tgt={4},curr={5}",
                                BSScene.DetailLogZero, UseName, TargetValueDecayTimeScale, decayFactor, TargetValue, CurrentValue);
        }
        else
        {
            // Difference between what we have and target is small. Motor is done.
            if (TargetValue.ApproxEquals(Vector3.Zero, ErrorZeroThreshold))
            {
                // The target can step down to nearly zero but not get there.  If close to zero
                //     it is really zero.
                TargetValue = Vector3.Zero;
            }
            CurrentValue = TargetValue;
            MDetailLog("{0},  BSVMotor.Step,zero,{1},origTgt={2},origCurr={3},currTgt={4},currCurr={5}",
                        BSScene.DetailLogZero, UseName, origCurrVal, origTarget, TargetValue, CurrentValue);
        }
        LastError = error;

        return correction;
    }
    // version of step that sets the current value before doing the step
    public virtual Vector3 Step(float timeStep, Vector3 current)
    {
        CurrentValue = current;
        return Step(timeStep);
    }
    // Given and error, computer a correction for this step.
    // Simple scaling of the error by the timestep.
    public virtual Vector3 StepError(float timeStep, Vector3 error)
    {
        if (!Enabled) return Vector3.Zero;

        Vector3 returnCorrection = Vector3.Zero;
        if (!ErrorIsZero(error))
        {
            // correction =  error / secondsItShouldTakeToCorrect
            Vector3 correctionAmount;
            if (TimeScale == 0f || TimeScale == BSMotor.Infinite)
                correctionAmount = error * timeStep;
            else
                correctionAmount = error / TimeScale * timeStep;

            returnCorrection = correctionAmount;
            MDetailLog("{0},  BSVMotor.Step,nonZero,{1},timeStep={2},timeScale={3},err={4},corr={5}",
                                    BSScene.DetailLogZero, UseName, timeStep, TimeScale, error, correctionAmount);
        }
        return returnCorrection;
    }

    // The user sets all the parameters and calls this which outputs values until error is zero.
    public override void GenerateTestOutput(float timeStep)
    {
        // maximum number of outputs to generate.
        int maxOutput = 50;
        MDetailLog("{0},BSVMotor.Test,{1},===================================== BEGIN Test Output", BSScene.DetailLogZero, UseName);
        MDetailLog("{0},BSVMotor.Test,{1},timeScale={2},targDlyTS={3},eff={4},curr={5},tgt={6}",
                                BSScene.DetailLogZero, UseName,
                                TimeScale, TargetValueDecayTimeScale, Efficiency,
                                CurrentValue, TargetValue);

        LastError = BSMotor.InfiniteVector;
        while (maxOutput-- > 0 && !ErrorIsZero())
        {
            Vector3 lastStep = Step(timeStep);
            MDetailLog("{0},BSVMotor.Test,{1},cur={2},tgt={3},lastError={4},lastStep={5}",
                            BSScene.DetailLogZero, UseName, CurrentValue, TargetValue, LastError, lastStep);
        }
        MDetailLog("{0},BSVMotor.Test,{1},===================================== END Test Output", BSScene.DetailLogZero, UseName);


    }

    public override string ToString()
    {
        return String.Format("<{0},curr={1},targ={2},lastErr={3},decayTS={4}>",
            UseName, CurrentValue, TargetValue, LastError, TargetValueDecayTimeScale);
    }
}

// ============================================================================
// ============================================================================
public class BSFMotor : BSMotor
{
    public virtual float TimeScale { get; set; }
    public virtual float TargetValueDecayTimeScale { get; set; }
    public virtual float Efficiency { get; set; }

    public virtual float ErrorZeroThreshold { get; set; }

    public virtual float TargetValue { get; protected set; }
    public virtual float CurrentValue { get; protected set; }
    public virtual float LastError { get; protected set; }

    public virtual bool ErrorIsZero()
    {
        return ErrorIsZero(LastError);
    }
    public virtual bool ErrorIsZero(float err)
    {
        return (err >= -ErrorZeroThreshold && err <= ErrorZeroThreshold);
    }

    public BSFMotor(string useName, float timeScale, float decayTimescale, float efficiency)
        : base(useName)
    {
        TimeScale = TargetValueDecayTimeScale = BSMotor.Infinite;
        Efficiency = 1f;
        CurrentValue = TargetValue = 0f;
        ErrorZeroThreshold = 0.01f;
    }
    public void SetCurrent(float current)
    {
        CurrentValue = current;
    }
    public void SetTarget(float target)
    {
        TargetValue = target;
    }
    public override void Zero()
    {
        base.Zero();
        CurrentValue = TargetValue = 0f;
    }

    public virtual float Step(float timeStep)
    {
        if (!Enabled) return TargetValue;

        float origTarget = TargetValue;       // DEBUG
        float origCurrVal = CurrentValue;     // DEBUG

        float correction = 0f;
        float error = TargetValue - CurrentValue;
        if (!ErrorIsZero(error))
        {
            correction = StepError(timeStep, error);

            CurrentValue += correction;

            // The desired value reduces to zero which also reduces the difference with current.
            // If the decay time is infinite, don't decay at all.
            float decayFactor = 0f;
            if (TargetValueDecayTimeScale != BSMotor.Infinite)
            {
                decayFactor = (1.0f / TargetValueDecayTimeScale) * timeStep;
                TargetValue *= (1f - decayFactor);
            }

            MDetailLog("{0},  BSFMotor.Step,nonZero,{1},origCurr={2},origTarget={3},timeStep={4},err={5},corr={6}",
                                BSScene.DetailLogZero, UseName, origCurrVal, origTarget,
                                timeStep, error, correction);
            MDetailLog("{0},  BSFMotor.Step,nonZero,{1},tgtDecayTS={2},decayFact={3},tgt={4},curr={5}",
                                BSScene.DetailLogZero, UseName, TargetValueDecayTimeScale, decayFactor, TargetValue, CurrentValue);
        }
        else
        {
            // Difference between what we have and target is small. Motor is done.
            if (Util.InRange<float>(TargetValue, -ErrorZeroThreshold, ErrorZeroThreshold))
            {
                // The target can step down to nearly zero but not get there.  If close to zero
                //     it is really zero.
                TargetValue = 0f;
            }
            CurrentValue = TargetValue;
            MDetailLog("{0},  BSFMotor.Step,zero,{1},origTgt={2},origCurr={3},ret={4}",
                        BSScene.DetailLogZero, UseName, origCurrVal, origTarget, CurrentValue);
        }
        LastError = error;

        return CurrentValue;
    }

    public virtual float StepError(float timeStep, float error)
    {
        if (!Enabled) return 0f;

        float returnCorrection = 0f;
        if (!ErrorIsZero(error))
        {
            // correction =  error / secondsItShouldTakeToCorrect
            float correctionAmount;
            if (TimeScale == 0f || TimeScale == BSMotor.Infinite)
                correctionAmount = error * timeStep;
            else
                correctionAmount = error / TimeScale * timeStep;

            returnCorrection = correctionAmount;
            MDetailLog("{0},  BSFMotor.Step,nonZero,{1},timeStep={2},timeScale={3},err={4},corr={5}",
                                    BSScene.DetailLogZero, UseName, timeStep, TimeScale, error, correctionAmount);
        }
        return returnCorrection;
    }

    public override string ToString()
    {
        return String.Format("<{0},curr={1},targ={2},lastErr={3},decayTS={4}>",
            UseName, CurrentValue, TargetValue, LastError, TargetValueDecayTimeScale);
    }

}

// ============================================================================
// ============================================================================
// Proportional, Integral, Derivitive ("PID") Motor
// Good description at http://www.answers.com/topic/pid-controller . Includes processes for choosing p, i and d factors.
public class BSPIDVMotor : BSVMotor
{
    // Larger makes more overshoot, smaller means converge quicker. Range of 0.1 to 10.
    public Vector3 proportionFactor { get; set; }
    public Vector3 integralFactor { get; set; }
    public Vector3 derivFactor { get; set; }

    // The factors are vectors for the three dimensions. This is the proportional of each
    //    that is applied. This could be multiplied through the actual factors but it
    //    is sometimes easier to manipulate the factors and their mix separately.
    public Vector3 FactorMix;

    // Arbritrary factor range.
    // EfficiencyHigh means move quickly to the correct number. EfficiencyLow means might over correct.
    public float EfficiencyHigh = 0.4f;
    public float EfficiencyLow = 4.0f;

    // Running integration of the error
    Vector3 RunningIntegration { get; set; }

    public BSPIDVMotor(string useName)
        : base(useName)
    {
        proportionFactor = new Vector3(1.00f, 1.00f, 1.00f);
        integralFactor = new Vector3(1.00f, 1.00f, 1.00f);
        derivFactor = new Vector3(1.00f, 1.00f, 1.00f);
        FactorMix = new Vector3(0.5f, 0.25f, 0.25f);
        RunningIntegration = Vector3.Zero;
        LastError = Vector3.Zero;
    }

    public override void Zero()
    {
        base.Zero();
    }

    public override float Efficiency
    {
        get { return base.Efficiency; }
        set
        {
            base.Efficiency = Util.Clamp(value, 0f, 1f);

            // Compute factors based on efficiency.
            // If efficiency is high (1f), use a factor value that moves the error value to zero with little overshoot.
            // If efficiency is low (0f), use a factor value that overcorrects.
            // TODO: might want to vary contribution of different factor depending on efficiency.
            // float factor = ((1f - this.Efficiency) * EfficiencyHigh + EfficiencyLow) / 3f;
            float factor = (1f - this.Efficiency) * EfficiencyHigh + EfficiencyLow;

            proportionFactor = new Vector3(factor, factor, factor);
            integralFactor = new Vector3(factor, factor, factor);
            derivFactor = new Vector3(factor, factor, factor);

            MDetailLog("{0},  BSPIDVMotor.setEfficiency,eff={1},factor={2}", BSScene.DetailLogZero, Efficiency, factor);
        }
    }

    // Advance the PID computation on this error.
    public override Vector3 StepError(float timeStep, Vector3 error)
    {
        if (!Enabled) return Vector3.Zero;

        // Add up the error so we can integrate over the accumulated errors
        RunningIntegration += error * timeStep;

        // A simple derivitive is the rate of change from the last error.
        Vector3 derivitive = (error - LastError) * timeStep;

        // Correction = (proportionOfPresentError + accumulationOfPastError + rateOfChangeOfError)
        Vector3 ret   =   error / TimeScale * timeStep   * proportionFactor * FactorMix.X
                        + RunningIntegration / TimeScale * integralFactor   * FactorMix.Y
                        + derivitive / TimeScale         * derivFactor      * FactorMix.Z
                        ;

        MDetailLog("{0},  BSPIDVMotor.step,ts={1},err={2},lerr={3},runnInt={4},deriv={5},ret={6}",
                        BSScene.DetailLogZero, timeStep, error, LastError, RunningIntegration, derivitive, ret);

        return ret;
    }
}
}
