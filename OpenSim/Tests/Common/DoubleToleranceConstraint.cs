using System;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using OpenSim.Tests.Common;

namespace OpenSim.Tests.Common
{
    public class DoubleToleranceConstraint : ANumericalToleranceConstraint
    {

        private double _baseValue;
        private double _valueToBeTested;

        public DoubleToleranceConstraint(double baseValue, double tolerance) : base(tolerance)
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
            if( valueToBeTested.GetType() != typeof(double))
            {
                throw new ArgumentException("Constraint cannot be used upon non double-values.");
            }

            _valueToBeTested = (double)valueToBeTested;

            return IsWithinDoubleConstraint(_valueToBeTested, _baseValue );
        }

        public override void WriteDescriptionTo(MessageWriter writer)
        {
            writer.WriteExpectedValue(string.Format("A value {0} within tolerance of plus or minus {1}",_baseValue,_tolerance));
        }

        public override void WriteActualValueTo(MessageWriter writer)
        {
            writer.WriteActualValue(_valueToBeTested);        
        }
    }
}
