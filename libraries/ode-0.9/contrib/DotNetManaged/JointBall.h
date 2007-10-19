#pragma once

#include "Joint.h"

namespace ODEManaged
{
	__gc public class JointBall : public Joint
	{
	public:

		//Constructors

			JointBall(void);
			JointBall(World &world);
			JointBall(World &world, JointGroup &jointGroup);
		

		//Destructors

			virtual ~JointBall(void);


		//Methods
		
			//Overloaded Create
			void Create(World &world, JointGroup &jointGroup);
			void Create(World &world);
			
			//Overloaded Attach
			void Attach(Body &body1, Body &body2);	
			void Attach(Body &body1);

			void SetAnchor(double x, double y, double z);
			Vector3 GetAnchor(void);

	};

}
