#include "StdAfx.h"

#include <ode/ode.h>
#include "jointball.h"

namespace ODEManaged
{
	
	//Constructors

		JointBall::JointBall(void) : Joint(){}


		JointBall::JointBall(World &world)
		{
			if(this->_id) dJointDestroy(this->_id);
			_id = dJointCreateBall(world.Id(), 0);
		}

		
		JointBall::JointBall(World &world, JointGroup &jointGroup)
		{
			if(this->_id) dJointDestroy(this->_id);
			_id = dJointCreateBall(world.Id(), jointGroup.Id());
		}


	//Destructor

		JointBall::~JointBall(void){}


	//Methods

		//Overloaded Create 
		void JointBall::Create(World &world, JointGroup &jointGroup)
		{
			if(this->_id) dJointDestroy(this->_id);
			_id = dJointCreateBall(world.Id(), jointGroup.Id());
		}

		void JointBall::Create(World &world)
		{
			if(this->_id) dJointDestroy(this->_id);
			_id = dJointCreateBall(world.Id(), 0);
		}


		//Overloaded Attach
		void JointBall::Attach(Body &body1, Body &body2)
		{
			dJointAttach(this->_id, body1.Id(), body2.Id());
		}

		void JointBall::Attach(Body &body1)
		{
			dJointAttach(this->_id, body1.Id(), 0);
		}


		//SetAnchor
		void JointBall::SetAnchor(double x, double y ,double z)
		{
			dJointSetBallAnchor(this->_id, x, y, z);
		}

		//GetAnchor
		Vector3 JointBall::GetAnchor(void)
		{
			Vector3 retVal;
			dVector3 temp;
			dJointGetBallAnchor(this->_id,temp);
			retVal.x = temp[0];
			retVal.y = temp[1];
			retVal.z = temp[2];
			return retVal;
		}

}
