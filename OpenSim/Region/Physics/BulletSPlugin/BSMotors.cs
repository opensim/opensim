using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Region.Physics.BulletSPlugin
{
public abstract class BSMotor
{
    public virtual void Reset() { }
    public virtual void Zero() { }
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



    BSVMotor(float timeScale, float decayTimeScale, Vector3 frictionTimeScale, float efficiency)
    {
        TimeScale = timeScale;
        TargetValueDecayTimeScale = decayTimeScale;
        CurrentValueReductionTimescale = frictionTimeScale;
        Efficiency = efficiency;
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
        if (CurrentValue.LengthSquared() > 0.001f)
        {
            // Vector3 origDir = Target;       // DEBUG
            // Vector3 origVel = CurrentValue;   // DEBUG

            // Add (desiredVelocity - currentAppliedVelocity) / howLongItShouldTakeToComplete
            Vector3 addAmount = (TargetValue - CurrentValue)/(TargetValue) * timeStep;
            CurrentValue += addAmount;

            float decayFactor = (1.0f / TargetValueDecayTimeScale) * timeStep;
            TargetValue *= (1f - decayFactor);

            Vector3 frictionFactor = (Vector3.One / CurrentValueReductionTimescale) * timeStep;
            CurrentValue *= (Vector3.One - frictionFactor);
        }
        else
        {
            // if what remains of direction is very small, zero it.
            TargetValue = Vector3.Zero;
            CurrentValue = Vector3.Zero;

            // VDetailLog("{0},MoveLinear,zeroed", Prim.LocalID);
        }
        return CurrentValue;
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

    BSFMotor(float timeScale, float decayTimescale, float friction, float efficiency)
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
    BSPIDMotor()
    {
    }
}
}
