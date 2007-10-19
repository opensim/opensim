#include "StdAfx.h"

#include <ode/ode.h>
#include "joint.h"
#include "CommonMgd.h"
#include "world.h"

namespace ODEManaged
{
	
	//Constructor

		Joint::Joint(void)
		{
			_id=0;
		}


	//Destructor

		Joint::~Joint(void)
		{
			dJointDestroy(this->_id);
		}


	//Methods

		//Id
		dJointID Joint::Id(void)
		{
			return _id;
		}

}
