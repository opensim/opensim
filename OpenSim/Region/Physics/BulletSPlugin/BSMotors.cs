using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Region.Physics.BulletSPlugin
{
public abstract class BSMotor
{
    public BSMotor(string useName)
    {
        UseName = useName;
        PhysicsScene = null;
    }
    public virtual void Reset() { }
    public virtual void Zero() { }

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
public class BSVMotor : BSMotor
{
    public Vector3 FrameOfReference { get; set; }
    public Vector3 Offset { get; set; }

    public float TimeScale { get; set; }
    public float TargetValueDecayTimeScale { get; set; }
    public Vector3 FrictionTimescale { get; set; }
    public float Efficiency { get; set; }

    public Vector3 TargetValue { get; private set; }
    public Vector3 CurrentValue { get; private set; }

    public BSVMotor(string useName)
        : base(useName)
    {
        TimeScale = TargetValueDecayTimeScale = Efficiency = 1f;
        FrictionTimescale = Vector3.Zero;
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

            // The desired value reduces to zero when also reduces the difference with current.
            float decayFactor = (1.0f / TargetValueDecayTimeScale) * timeStep;
            TargetValue *= (1f - decayFactor);

            Vector3 frictionFactor = Vector3.Zero;
            frictionFactor = (Vector3.One / FrictionTimescale) * timeStep;
            CurrentValue *= (Vector3.One - frictionFactor);

            MDetailLog("{0},BSVMotor.Step,nonZero,{1},origTarget={2},origCurr={3},timeStep={4},timeScale={5},addAmnt={6},targetDecay={7},decayFact={8},fricTS={9},frictFact={10}",
                                BSScene.DetailLogZero, UseName, origTarget, origCurrVal,
                                timeStep, TimeScale, addAmount,
                                TargetValueDecayTimeScale, decayFactor,
                                FrictionTimescale, frictionFactor);
            MDetailLog("{0},BSVMotor.Step,nonZero,{1},curr={2},target={3},add={4},decay={5},frict={6},ret={7}",
                                    BSScene.DetailLogZero, UseName, TargetValue, CurrentValue, 
                                    addAmount, decayFactor, frictionFactor, returnCurrent);
        }
        else
        {
            // Difference between what we have and target is small. Motor is done.
            CurrentValue = Vector3.Zero;
            TargetValue = Vector3.Zero;

            MDetailLog("{0},BSVMotor.Step,zero,{1},curr={2},target={3},ret={4}",
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
