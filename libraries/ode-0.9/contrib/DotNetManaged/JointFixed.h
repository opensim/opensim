#pragma once

#include "Joint.h"

namespace ODEManaged
{
	__gc public class JointFixed : public Joint
	{
	public:

		//Constructors

			JointFixed(void);
			JointFixed(World &world);
			JointFixed(World &world, JointGroup &jointGroup);
		

		//Destructor

			virtual ~JointFixed(void);


		//Methods
		
			//Overloaded Create
			void Create(World &world, JointGroup &jointGroup);
			void Create(World &world);

			//Overloaded Attach 
			void Attach(Body &body1, Body &body2);	
			void Attach(Body &body1);
		
			void SetFixed(void);

	};

}
