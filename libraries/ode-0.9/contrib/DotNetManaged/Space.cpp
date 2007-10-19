#include "StdAfx.h"

#include <ode/ode.h>
#include "Space.h"
#include "TEST.h"

namespace ODEManaged
{

	//Constructor

		Space::Space(void)
		{ 
			_id = dSimpleSpaceCreate();
		}

		Space::Space(int minlevel, int maxlevel)
		{ 
			_id = dHashSpaceCreate();
			dHashSpaceSetLevels(this->_id, minlevel, maxlevel);
		}

	
	//Destructor

		Space::~Space(void)
		{
			dSpaceDestroy(this->_id);
		}


	//Methods

		//Id
		dSpaceID Space::Id()
		{
			return _id;
		}


		//Collide
		void Space::Collide(void *data, dNearCallback *callback)
		{		
			dSpaceCollide(this->_id, data, callback);
		}







}
