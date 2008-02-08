using System;
using NUnit.Framework.Constraints;

namespace OpenSim.Tests.Common
{
    public abstract class ANumericalToleranceConstraint : Constraint
    {
        protected double _tolerance;

        public ANumericalToleranceConstraint(double tolerance)
        {
            if (tolerance < 0)
            {
                throw new ArgumentException("Tolerance cannot be negative.");
            }
            _tolerance = tolerance;
        }


        protected bool IsWithinDoubleConstraint(double doubleValue, double baseValue)
        {
            if (doubleValue >= baseValue - _tolerance && doubleValue <= baseValue + _tolerance)
            {
                return true;
            }

            return false;
        }
    }
}
