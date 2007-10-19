#pragma once

#include "Joint.h"

namespace ODEManaged
{
	__gc public class JointAMotor : public Joint
	{
	public:


		//Constructors

			JointAMotor				(void);
			JointAMotor				(World &world);
			JointAMotor				(World &world, JointGroup &jointGroup);
			

		//Destructor
			
			virtual ~JointAMotor	(void);


		//Methods	
		
			//Basic Stuff
			
				//Overloaded Create
				void	Create			(World &world, JointGroup &jointGroup);
				void	Create			(World &world);

				void	SetNumAxes		(int num);
				int		GetNumAxes		(void);

				void	SetAxis			(int anum, int rel, double x, double y, double z);
				Vector3 GetAxis			(int anum);
				
				void	SetAngle		(int anum, double angle);
				double	GetAngle		(int anum);

				void	SetMode			(int mode);
				int		GetMode			(void);

				int		GetAxisRel		(int anum);
				double	GetAngleRate	(int anum);

				//Overloaded Attach
				void	Attach			(Body &body1, Body &body2);	
				void	Attach			(Body &body1);


			//Movement Parameters

			void	SetParam		(int parameter, double value);
			double	GetParam		(int parameter);
			

			


	};
}
