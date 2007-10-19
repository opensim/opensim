#pragma once

#include "Joint.h"
#include "CommonMgd.h"

namespace ODEManaged
{
	__gc public class JointHinge : public Joint
	{
	public:

		//Constructors

			JointHinge(void);
			JointHinge(World &world);
			JointHinge(World &world, JointGroup &jointGroup);
			

		//Destructor

			virtual~JointHinge(void);

		
		//Methods	
			
			//Overloaded Create
			void Create(World &world, JointGroup &jointGroup);
			void Create(World &world);

			//Overloaded Attach
			void Attach(Body &body1, Body &body2);	
			void Attach(Body &body1);

			void SetAnchor(double x, double y, double z);
			Vector3 GetAnchor(void);

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
					return dJointGetHingeParam(this->_id, dParamLoStop);
				}

			__property void	set_LoStop(double value)
			{
				if (value > -3.141592653 && value <= 0)
					dJointSetHingeParam(this->_id, dParamLoStop, value);
			}


			//HiStop
			__property double get_HiStop(void)
			{
				return dJointGetHingeParam(this->_id, dParamHiStop);
			}

			__property void set_HiStop(double value)
			{
				if (value < 3.141592653 && value >= 0)
					dJointSetHingeParam(this->_id, dParamHiStop, value);
			}
			

			//Velocity
			__property double get_Velocity(void)
			{
				return dJointGetHingeParam(this->_id, dParamVel);
			}

			__property void set_Velocity(double value)
			{
				dJointSetHingeParam(this->_id, dParamVel, value);
			}


			//MaxForce
			__property double get_MaxForce(void)
			{
				return dJointGetHingeParam(this->_id, dParamFMax);
			}

			__property void set_MaxForce(double value)
			{
				dJointSetHingeParam(this->_id, dParamFMax, value);
			}


			//FudgeFactor
			__property double get_FudgeFactor(void)
			{
				return dJointGetHingeParam(this->_id, dParamFudgeFactor);
			}

			__property void set_FudgeFactor(double value)
			{
				dJointSetHingeParam(this->_id, dParamFudgeFactor, value);
			}


			//Bounce
			__property double get_Bounce(void)
			{
				return dJointGetHingeParam(this->_id, dParamBounce);
			}

			__property void set_Bounce(double value)
			{
				dJointSetHingeParam(this->_id, dParamBounce, value);
			}


			//StopERP
			__property double get_StopERP(void)
			{
				return dJointGetHingeParam(this->_id, dParamStopERP);
			}

			__property void set_StopERP(double value)
			{
				dJointSetHingeParam(this->_id, dParamStopERP, value);
			}


			//StopCFM
			__property double get_StopCFM(void)
			{
				return dJointGetHingeParam(this->_id, dParamStopCFM);
			}

			__property void set_StopCFM(double value)
			{
				dJointSetHingeParam(this->_id, dParamStopCFM, value);
			}


			//GetAngle
			__property double get_Angle(void)
			{
				return dJointGetHingeAngle(this->_id);
			}


			//GetAngleRate
			__property double get_AngleRate(void)
			{
				return dJointGetHingeAngleRate(this->_id);
			}

	};

}

//				void	SetSuspensionERP(double value);
//				double	GetSuspensionERP(void);

//				void	SetSuspensionCFM(double value);
//				double	GetSuspensionCFM(void);

/*				
			//SetSuspensionERP
			void JointHinge::SetSuspensionERP(double value)
			{
				dJointSetHingeParam(this->_id, dParamSuspensionERP, value);
			}

			//GetSuspensionERP
			double JointHinge::GetSuspensionERP(void)
			{
				return dJointGetHingeParam(this->_id, dParamSuspensionERP);
			}
							
				
			//SetSuspensionCFM
			void JointHinge::SetSuspensionCFM(double value)
			{
				dJointSetHingeParam(this->_id, dParamSuspensionCFM, value);
			}

			//GetSuspensionCFM
			double JointHinge::GetSuspensionCFM(void)
			{
				return dJointGetHingeParam(this->_id, dParamSuspensionCFM);
			}

*/
