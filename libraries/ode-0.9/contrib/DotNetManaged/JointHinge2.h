#pragma once

#include "Joint.h"
#include "CommonMgd.h"

namespace ODEManaged
{
	__gc public class JointHinge2 : public Joint
	{
		public:


		//Constructors

			JointHinge2				(void);
			JointHinge2				(World &world);
			JointHinge2				(World &world, JointGroup &jointGroup);
			
		//Destructors

			virtual ~JointHinge2	(void);
			

		//Methods

			//Overloaded Hinge.Create
			void	Create			(World &world, JointGroup &jointGroup);
			void	Create			(World &world);
			
			void	SetAnchor		(double x, double y, double z);
			Vector3 GetAnchor		(void);

			void	SetAxis1		(double x, double y, double z);
			Vector3 GetAxis1		(void);

			void	SetAxis2		(double x, double y, double z);
			Vector3 GetAxis2		(void);

			double	GetAngle1		(void);
			double	GetAngle1Rate	(void);

			//double GetAngle2 (void);
			double	GetAngle2Rate	(void);

			void	Attach			(Body &body1, Body &body2);	
			void	Attach(			Body &body1);
	};
}
