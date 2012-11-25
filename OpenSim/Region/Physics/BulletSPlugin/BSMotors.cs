using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Region.Physics.BulletSPlugin
{
public abstract class BSMotor
{
    public BSMotor()
    {
        PhysicsScene = null;
    }
    public virtual void Reset() { }
    public virtual void Zero() { }

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
    public Vector3 CurrentValueReductionTimescale { get; set; }
    public float Efficiency { get; set; }

    public Vector3 TargetValue { get; private set; }
    public Vector3 CurrentValue { get; private set; }

    BSVMotor(float timeScale, float decayTimeScale, Vector3 frictionTimeScale, float efficiency) : base()
    {
        TimeScale = timeScale;
        TargetValueDecayTimeScale = decayTimeScale;
        CurrentValueReductionTimescale = frictionTimeScale;
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
        if (CurrentValue.LengthSquared() > 0.001f)
        {
            // Vector3 origDir = Target;       // DEBUG
            // Vector3 origVel = CurrentValue;   // DEBUG

            // Add (desiredVector - currentAppliedVector) / howLongItShouldTakeToComplete
            Vector3 addAmount = (TargetValue - CurrentValue)/TimeScale * timeStep;
            CurrentValue += addAmount;
            returnCurrent = CurrentValue;

            // The desired value reduces to zero when also reduces the difference with current.
            float decayFactor = (1.0f / TargetValueDecayTimeScale) * timeStep;
            TargetValue *= (1f - decayFactor);

            Vector3 frictionFactor = (Vector3.One / CurrentValueReductionTimescale) * timeStep;
            CurrentValue *= (Vector3.One - frictionFactor);

            MDetailLog("{0},BSVMotor.Step,nonZero,curr={1},target={2},add={3},decay={4},frict={5},ret={6}",
                                    BSScene.DetailLogZero, TargetValue, CurrentValue, 
                                    addAmount, decayFactor, frictionFactor, returnCurrent);
        }
        else
        {
            // Difference between what we have and target is small. Motor is done.
            CurrentValue = Vector3.Zero;
            TargetValue = Vector3.Zero;

            MDetailLog("{0},BSVMotor.Step,zero,curr={1},target={2},ret={3}",
                                    BSScene.DetailLogZero, TargetValue, CurrentValue, returnCurrent);

        }
        return returnCurrent;
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

    BSFMotor(float timeScale, float decayTimescale, float friction, float efficiency) : base()
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
    BSPIDMotor() : base()
    {
    }
}
}
