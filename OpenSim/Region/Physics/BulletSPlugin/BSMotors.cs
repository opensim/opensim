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

namespace OpenSim.Region.Physics.BulletSPlugin
{
public abstract class BSMotor
{
    // Timescales and other things can be turned off by setting them to 'infinite'.
    public const float Infinite = 12345f;
    public readonly static Vector3 InfiniteVector = new Vector3(BSMotor.Infinite, BSMotor.Infinite, BSMotor.Infinite);

    public BSMotor(string useName)
    {
        UseName = useName;
        PhysicsScene = null;
    }
    public virtual void Reset() { }
    public virtual void Zero() { }

    // A name passed at motor creation for easily identifyable debugging messages.
    public string UseName { get; private set; }

    // Used only for outputting debug information. Might not be set so check for null.
    public BSScene PhysicsScene { get; set; }
    protected void MDetailLog(string msg, params Object[] parms)
    {
        if (PhysicsScene != null)
        {
            if (PhysicsScene.VehicleLoggingEnabled)
            {
                PhysicsScene.DetailLog(msg, parms);
            }
        }
    }
}
// Can all the incremental stepping be replaced with motor classes?

// Motor which moves CurrentValue to TargetValue over TimeScale seconds.
// The TargetValue decays in TargetValueDecayTimeScale and
//     the CurrentValue will be held back by FrictionTimeScale.
// TimeScale and TargetDelayTimeScale may be 'infinite' which means go decay.

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

    public float TimeScale { get; set; }
    public float TargetValueDecayTimeScale { get; set; }
    public Vector3 FrictionTimescale { get; set; }
    public float Efficiency { get; set; }

    public Vector3 TargetValue { get; private set; }
    public Vector3 CurrentValue { get; private set; }

    public BSVMotor(string useName)
        : base(useName)
    {
        TimeScale = TargetValueDecayTimeScale = BSMotor.Infinite;
        Efficiency = 1f;
        FrictionTimescale = BSMotor.InfiniteVector;
        CurrentValue = TargetValue = Vector3.Zero;
    }
    public BSVMotor(string useName, float timeScale, float decayTimeScale, Vector3 frictionTimeScale, float efficiency) 
        : this(useName)
    {
        TimeScale = timeScale;
        TargetValueDecayTimeScale = decayTimeScale;
        FrictionTimescale = frictionTimeScale;
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

    // A form of stepping that does not take the time quantum into account.
    // The caller must do the right thing later.
    public Vector3 Step()
    {
        return Step(1f);
    }

    public Vector3 Step(float timeStep)
    {
        Vector3 returnCurrent = Vector3.Zero;
        if (!CurrentValue.ApproxEquals(TargetValue, 0.01f))
        {
            Vector3 origTarget = TargetValue;       // DEBUG
            Vector3 origCurrVal = CurrentValue;   // DEBUG

            // Addition =  (desiredVector - currentAppliedVector) / secondsItShouldTakeToComplete
            Vector3 addAmount = (TargetValue - CurrentValue)/TimeScale * timeStep;
            CurrentValue += addAmount;

            returnCurrent = CurrentValue;

            // The desired value reduces to zero which also reduces the difference with current.
            // If the decay time is infinite, don't decay at all.
            float decayFactor = 0f;
            if (TargetValueDecayTimeScale != BSMotor.Infinite)
            {
                decayFactor = (1.0f / TargetValueDecayTimeScale) * timeStep;
                TargetValue *= (1f - decayFactor);
            }

            Vector3 frictionFactor = Vector3.Zero;
            if (FrictionTimescale != BSMotor.InfiniteVector)
            {
                // frictionFactor = (Vector3.One / FrictionTimescale) * timeStep;
                // Individual friction components can be 'infinite' so compute each separately.
                frictionFactor.X = FrictionTimescale.X == BSMotor.Infinite ? 0f : (1f / FrictionTimescale.X) * timeStep;
                frictionFactor.Y = FrictionTimescale.Y == BSMotor.Infinite ? 0f : (1f / FrictionTimescale.Y) * timeStep;
                frictionFactor.Z = FrictionTimescale.Z == BSMotor.Infinite ? 0f : (1f / FrictionTimescale.Z) * timeStep;
                CurrentValue *= (Vector3.One - frictionFactor);
            }

            MDetailLog("{0},  BSVMotor.Step,nonZero,{1},origCurr={2},origTarget={3},timeStep={4},timeScale={5},addAmnt={6},targetDecay={7},decayFact={8},fricTS={9},frictFact={10}",
                                BSScene.DetailLogZero, UseName, origCurrVal, origTarget,
                                timeStep, TimeScale, addAmount,
                                TargetValueDecayTimeScale, decayFactor,
                                FrictionTimescale, frictionFactor);
            MDetailLog("{0},  BSVMotor.Step,nonZero,{1},curr={2},target={3},add={4},decay={5},frict={6},ret={7}",
                                    BSScene.DetailLogZero, UseName, CurrentValue, TargetValue,
                                    addAmount, decayFactor, frictionFactor, returnCurrent);
        }
        else
        {
            // Difference between what we have and target is small. Motor is done.
            CurrentValue = Vector3.Zero;
            TargetValue = Vector3.Zero;

            MDetailLog("{0},  BSVMotor.Step,zero,{1},curr={2},target={3},ret={4}",
                                    BSScene.DetailLogZero, UseName, TargetValue, CurrentValue, returnCurrent);

        }
        return returnCurrent;
    }
    public override string ToString()
    {
        return String.Format("<{0},curr={1},targ={2},decayTS={3},frictTS={4}>",
            UseName, CurrentValue, TargetValue, TargetValueDecayTimeScale, FrictionTimescale);
    }
}

public class BSFMotor : BSMotor
{
    public float TimeScale { get; set; }
    public float DecayTimeScale { get; set; }
    public float Friction { get; set; }
    public float Efficiency { get; set; }

    public float Target { get; private set; }
    public float CurrentValue { get; private set; }

    public BSFMotor(string useName, float timeScale, float decayTimescale, float friction, float efficiency)
        : base(useName)
    {
    }
    public void SetCurrent(float target)
    {
    }
    public void SetTarget(float target)
    {
    }
    public float Step(float timeStep)
    {
        return 0f;
    }
}
public class BSPIDMotor : BSMotor
{
    // TODO: write and use this one
    public BSPIDMotor(string useName)
        : base(useName)
    {
    }
}
}
