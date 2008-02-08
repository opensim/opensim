using System;
using libsecondlife;
using NUnit.Framework;

namespace OpenSim.Tests.Common
{
    public class VectorToleranceConstraint : ANumericalToleranceConstraint
    {
        private LLVector3 _baseValue;
        private LLVector3 _valueToBeTested;

        public VectorToleranceConstraint(LLVector3 baseValue, double tolerance) : base(tolerance)
        {
            _baseValue = baseValue;
        }

        ///<summary>
        ///Test whether the constraint is satisfied by a given value            
        ///</summary>
        ///<param name="valueToBeTested">The value to be tested</param>
        ///<returns>
        ///True for success, false for failure
        ///</returns>
        public override bool Matches(object valueToBeTested)
        {
            if (valueToBeTested == null)
            {
                throw new ArgumentException("Constraint cannot be used upon null values.");
            }
            if (valueToBeTested.GetType() != typeof (LLVector3))
            {
                throw new ArgumentException("Constraint cannot be used upon non vector values.");
            }

            _valueToBeTested = (LLVector3) valueToBeTested;

            if (    IsWithinDoubleConstraint(_valueToBeTested.X,_baseValue.X) &&
                    IsWithinDoubleConstraint(_valueToBeTested.Y,_baseValue.Y) &&
                    IsWithinDoubleConstraint(_valueToBeTested.Z,_baseValue.Z) )
            {
                return true;
            }

            return false;
        }

        public override void WriteDescriptionTo(MessageWriter writer)
        {
            writer.WriteExpectedValue(
                string.Format("A value {0} within tolerance of plus or minus {1}", _baseValue, _tolerance));
        }

        public override void WriteActualValueTo(MessageWriter writer)
        {
            writer.WriteActualValue(_valueToBeTested);
        }
    }
}
