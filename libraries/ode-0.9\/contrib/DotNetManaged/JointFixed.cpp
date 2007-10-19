#include "StdAfx.h"

#include <ode/ode.h>
#include "jointfixed.h"

namespace ODEManaged
{

	//Constructors

		JointFixed::JointFixed(void) : Joint(){}


		JointFixed::JointFixed(World &world)
		{
			if(this->_id) dJointDestroy(this->_id);
			_id = dJointCreateFixed(world.Id(),0);
		}
		

		JointFixed::JointFixed(World &world, JointGroup &jointGroup)
		{
			if(this->_id) dJointDestroy(this->_id);
			_id = dJointCreateFixed(world.Id(), jointGroup.Id());
		}


	//Destructor

		JointFixed::~JointFixed(void){}


	//Methods

		//Overloaded Create 
		void JointFixed::Create(World &world, JointGroup &jointGroup)
		{
			if(this->_id) dJointDestroy(this->_id);
			_id = dJointCreateFixed(world.Id(), jointGroup.Id());
		}

		void JointFixed::Create(World &world)
		{
			if(this->_id) dJointDestroy(this->_id);
			_id = dJointCreateFixed(world.Id(), 0);
		}


		//Overloaded Attach
		void JointFixed::Attach(Body &body1, Body &body2)
		{
			dJointAttach(this->_id, body1.Id(), body2.Id());
		}

		void JointFixed::Attach(Body &body1)
		{
			dJointAttach(this->_id, body1.Id(), 0);
		}


		//Fixed
		void JointFixed::SetFixed(void)
		{
			dJointSetFixed(this->_id);
		}

}
