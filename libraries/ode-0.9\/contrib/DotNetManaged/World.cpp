#include "StdAfx.h"

#include <ode/ode.h>
#include "World.h"

namespace ODEManaged
{
	
	//Constructor
	
		World::World(void)
		{
			/*dWorldID _temp = dWorldCreate();
			_id = _temp;*/ 
			_id = dWorldCreate();
		}


	//Destructor

		World::~World(void)
		{
			dWorldDestroy(this->_id);
		}


	//Methods

		//Id
		dWorldID World::Id()
		{
			return _id;
		}


		//SetGravity
		void World::SetGravity(double x, double y, double z)
		{ 
			dWorldSetGravity(this->_id, x, y, z); 
		}


		//Overloaded GetGravity
		Vector3 World::GetGravity(void)
		{
			Vector3 retVal;
			dVector3 temp;
			dWorldGetGravity(this->_id, temp);
			retVal.x = temp[0];
			retVal.y = temp[1];
			retVal.z = temp[2];
			return retVal;
		}

		void World::GetGravity(double gravity __gc[])
		{
			dVector3 temp;
			dWorldGetGravity(this->_id, temp);
			gravity[0] = temp[0];
			gravity[1] = temp[1];
			gravity[2] = temp[2];
		}


		//Step
		void World::Step(double stepSize)
		{
			dWorldStep(this->_id, stepSize);
		}

}



