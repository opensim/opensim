#include "StdAfx.h"

#include <ode/ode.h>
#include "jointgroup.h"

namespace ODEManaged
{

	//Constructors

		JointGroup::JointGroup(void)
		{	
			_id=0;
		}

		JointGroup::JointGroup (int maxSize)
		{
			_id = dJointGroupCreate(maxSize);
		}


	//Destructor

		JointGroup::~JointGroup(void)
		{
			dJointGroupDestroy(this->_id);
		}

	
	//Methods

		//ID
		dJointGroupID JointGroup::Id()
		{
			return _id;
		}


		//Create
		void JointGroup::Create (int maxSize)
		{
			if(_id) dJointGroupDestroy(_id);
			_id = dJointGroupCreate(maxSize);
		}


		//Empty
		void JointGroup::Empty (void)
		{
			dJointGroupEmpty(this->_id);
		}

}
