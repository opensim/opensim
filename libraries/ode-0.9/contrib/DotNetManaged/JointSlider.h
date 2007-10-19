#pragma once

#include "Joint.h"
#include "CommonMgd.h"

namespace ODEManaged
{
	__gc public class JointSlider : public Joint
	{
	public:
			
		
		//Constructors

			JointSlider(void);
			JointSlider(World &world);
			JointSlider(World &world, JointGroup &jointGroup);
			

		//Destructors

			virtual ~JointSlider(void);

			
		//Methods	
			
			//Overloaded Create
			void Create(World &world, JointGroup &jointGroup);
			void Create(World &world);
			
			//Overloaded Attach
			void Attach(Body &body1, Body &body2);	
			void Attach(Body &body1);
			
			void SetAxis(double x, double y, double z);
			Vector3 GetAxis(void);

			void SetAllMovParams(double LoStop, double HiStop,
								 double Velocity, double MaxForce,
								 double FudgeFactor, double Bounce,
								 double StopERP, double StopCFM);


		//Properties

			//LoStop
			__property double get_LoStop(void)
				{
					return dJointGetSliderParam(this->_id, dParamLoStop);
				}

			__property void	set_LoStop(double value)
			{
				if (value <=0)
					dJointSetSliderParam(this->_id, dParamLoStop, value);
			}


			//HiStop
			__property double get_HiStop(void)
			{
				return dJointGetSliderParam(this->_id, dParamHiStop);
			}

			__property void set_HiStop(double value)
			{
				if (value >= 0)
					dJointSetSliderParam(this->_id, dParamHiStop, value);
			}
			

			//Velocity
			__property double get_Velocity(void)
			{
				return dJointGetSliderParam(this->_id, dParamVel);
			}

			__property void set_Velocity(double value)
			{
				dJointSetSliderParam(this->_id, dParamVel, value);
			}


			//MaxForce
			__property double get_MaxForce(void)
			{
				return dJointGetSliderParam(this->_id, dParamFMax);
			}

			__property void set_MaxForce(double value)
			{
				dJointSetSliderParam(this->_id, dParamFMax, value);
			}


			//FudgeFactor
			__property double get_FudgeFactor(void)
			{
				return dJointGetSliderParam(this->_id, dParamFudgeFactor);
			}

			__property void set_FudgeFactor(double value)
			{
				dJointSetSliderParam(this->_id, dParamFudgeFactor, value);
			}


			//Bounce
			__property double get_Bounce(void)
			{
				return dJointGetSliderParam(this->_id, dParamBounce);
			}

			__property void set_Bounce(double value)
			{
				dJointSetSliderParam(this->_id, dParamBounce, value);
			}


			//StopERP
			__property double get_StopERP(void)
			{
				return dJointGetSliderParam(this->_id, dParamStopERP);
			}

			__property void set_StopERP(double value)
			{
				dJointSetSliderParam(this->_id, dParamStopERP, value);
			}


			//StopCFM
			__property double get_StopCFM(void)
			{
				return dJointGetSliderParam(this->_id, dParamStopCFM);
			}

			__property void set_StopCFM(double value)
			{
				dJointSetSliderParam(this->_id, dParamStopCFM, value);
			}


			//GetAngle
			__property double get_Position(void)
			{
				return dJointGetSliderPosition(this->_id);
			}


			//GetAngleRate
			__property double get_PositionRate(void)
			{
				return dJointGetSliderPositionRate(this->_id);
			}

	};
}
