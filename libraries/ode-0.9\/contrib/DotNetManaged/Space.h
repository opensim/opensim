#pragma once

#include "CommonMgd.h"

namespace ODEManaged
{
	__gc public class Space
	{
	public:

		//Constructor
			
			Space(void);
			Space(int minlevel, int maxlevel);
			
		//Destructor
			
			~Space(void);


		//Methods

			dSpaceID Id(void);
			void Collide(void *data, dNearCallback *callback);


		private:
			
			dSpaceID _id;

	};

}
