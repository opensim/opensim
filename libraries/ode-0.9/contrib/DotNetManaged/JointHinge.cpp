#include "stdafx.h"

#include <ode/ode.h>
#include "jointhinge.h"

namespace ODEManaged
{

	//Constructors

		JointHinge::JointHinge(void) : Joint(){}


		JointHinge::JointHinge(World &world)
		{
			if(this->_id) dJointDestroy(this->_id);
			_id = dJointCreateHinge(world.Id(), 0);
		}

		
		JointHinge::JointHinge(World &world, JointGroup &jointGroup)
		{
			if(this->_id) dJointDestroy(this->_id);
			_id = dJointCreateHinge(world.Id(), jointGroup.Id());
		}


	//Destructor

		JointHinge::~JointHinge(void){}


	//Methods

		//Overloaded Create 
		void JointHinge::Create(World &world, JointGroup &jointGroup)
		{
			if(this->_id) dJointDestroy(this->_id);
			_id = dJointCreateHinge(world.Id(), jointGroup.Id());
		}

		void JointHinge::Create(World &world)
		{
			if(this->_id) dJointDestroy(this->_id);
			_id = dJointCreateHinge(world.Id(), 0);
		}


		//Overloaded Attach 
		void JointHinge::Attach(Body &body1, Body &body2)
		{
			dJointAttach(this->_id, body1.Id(), body2.Id());
		}

		void JointHinge::Attach(Body &body1)
		{
			dJointAttach(this->_id, body1.Id(), 0);
		}


		//SetAxis
		void JointHinge::SetAxis(double x, double y, double z)
		{
			dJointSetHingeAxis(this->_id, x, y, z);
		}

		//GetAxis
		Vector3 JointHinge::GetAxis(void)
		{
			Vector3 retVal;
			dVector3 temp;
			dJointGetHingeAxis(this->_id, temp);
			retVal.x = temp[0];
			retVal.y = temp[1];
			retVal.z = temp[2];
			return retVal;
		}


		//SetAnchor
		void JointHinge::SetAnchor(double x, double y, double z)
		{
			dJointSetHingeAnchor(this->_id, x, y, z);
		}

		//GetAnchor
		Vector3 JointHinge::GetAnchor(void)
		{
			Vector3 retVal;
			dVector3 temp;
			dJointGetHingeAnchor(this->_id, temp);
			retVal.x = temp[0];
			retVal.y = temp[1];
			retVal.z = temp[2];
			return retVal;
		}


	//Movement Parameters

		//SetAllMovParams
		void JointHinge::SetAllMovParams(double LoStop, double HiStop,
										 double Velocity, double MaxForce,
										 double FudgeFactor, double Bounce,
										 double StopERP, double StopCFM)
		{
			if (LoStop > -3.141592653 && LoStop <= 0) 
				dJointSetHingeParam(this->_id, dParamLoStop, LoStop);

			if (HiStop < 3.141592653 && HiStop >= 0)
				dJointSetHingeParam(this->_id, dParamHiStop, HiStop);

			dJointSetHingeParam(this->_id, dParamVel, Velocity);
			dJointSetHingeParam(this->_id, dParamFMax, MaxForce);
			dJointSetHingeParam(this->_id, dParamFudgeFactor, FudgeFactor);
			dJointSetHingeParam(this->_id, dParamBounce, Bounce);
			dJointSetHingeParam(this->_id, dParamStopERP, StopERP);
			dJointSetHingeParam(this->_id, dParamStopCFM, StopCFM);
		}
				
}
