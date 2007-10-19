#pragma once

#include "JointGroup.h"
#include "World.h"
#include "Body.h"

namespace ODEManaged
{	
	__gc public class Joint
	{
	protected:
		//Constructor and Destructor Defenition
		Joint(void);
		~Joint(void);

		//Public Methods
		dJointID Id(void);

		dJointID _id;	
 };
}
