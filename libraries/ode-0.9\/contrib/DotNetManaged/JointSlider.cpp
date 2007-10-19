#include "StdAfx.h"

#include <ode/ode.h>
#include "jointslider.h"

namespace ODEManaged
{
	
	//Constructors
	
		JointSlider::JointSlider(void) : Joint(){}


		JointSlider::JointSlider(World &world)
		{
			if(this->_id) dJointDestroy(this->_id);
			_id = dJointCreateSlider(world.Id(), 0);
		}


		JointSlider::JointSlider(World &world, JointGroup &jointGroup)
		{
			if(this->_id) dJointDestroy(this->_id);
			_id = dJointCreateSlider(world.Id(), jointGroup.Id());
		}


	//Destructor

		JointSlider::~JointSlider(void){}


	//Methods

		//Overloaded Create
		void JointSlider::Create(World &world, JointGroup &jointGroup)
		{
			if(this->_id) dJointDestroy(this->_id);
			_id = dJointCreateSlider(world.Id(), jointGroup.Id());
		}

		void JointSlider::Create(World &world)
		{
			if(this->_id) dJointDestroy(this->_id);
			_id = dJointCreateSlider(world.Id(), 0);
		}

		
		//Overloaded Attach 
		void JointSlider::Attach(Body &body1, Body &body2)
		{
			dJointAttach(this->_id, body1.Id(), body2.Id());
		}

		void JointSlider::Attach(Body &body1)
		{
			dJointAttach(this->_id, body1.Id(), 0);
		}


		//SetAxis
		void JointSlider::SetAxis(double x, double y, double z)
		{
			dJointSetSliderAxis(this->_id, x, y, z);
		}

		//GetAxis
		Vector3 JointSlider::GetAxis(void)
		{
			Vector3 retVal;
			dVector3 temp;
			dJointGetSliderAxis(this->_id, temp);
			retVal.x = temp[0];
			retVal.y = temp[1];
			retVal.z = temp[2];
			return retVal;
		}


	//Movement Parameters

		//SetAllMovParams
		void JointSlider::SetAllMovParams(double LoStop, double HiStop,
										  double Velocity, double MaxForce,
										  double FudgeFactor, double Bounce,
										  double StopERP, double StopCFM)
		{
			if (LoStop <= 0) 
				dJointSetHingeParam(this->_id, dParamLoStop, LoStop);

			if (HiStop >= 0)
				dJointSetHingeParam(this->_id, dParamHiStop, HiStop);

			dJointSetSliderParam(this->_id, dParamVel, Velocity);
			dJointSetSliderParam(this->_id, dParamFMax, MaxForce);
			dJointSetSliderParam(this->_id, dParamFudgeFactor, FudgeFactor);
			dJointSetSliderParam(this->_id, dParamBounce, Bounce);
			dJointSetSliderParam(this->_id, dParamStopERP, StopERP);
			dJointSetSliderParam(this->_id, dParamStopCFM, StopCFM);
		}

}
