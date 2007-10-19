#pragma once

#include "Body.h"
#include "Space.h"
#include "CommonMgd.h"

namespace ODEManaged
{
	__gc public class Geom
	{
	public:

		
		//Constructor
			
			Geom					(void);

			
		//Destructor
			
			~Geom					(void);


		//Methods

			//Basic Stuff

				dGeomID Id					(void);
				dBodyID GetBody				(void);
				
				//Overloaded SetBody
				void	SetBody				(Body &body);
				/*void	SetBody				(dBodyID b);*/
				
				Vector3	GetPosition			(void);
				void	SetPosition			(double x, double y, double z);
				
				Matrix3	GetRotation			(void);
				void	SetRotation			(Matrix3 rotation);
				
				void	SetData				(void *data);
				void	*GetData			(void);


			//Create Objects

				void	CreateSphere		(Space &space, double radius);
				void	CreateBox			(Space &space, double lx, double ly, double lz);
				void	CreatePlane			(Space &space, double a, double b, double c, double d);
				void	CreateCCylinder		(Space &space, double radius, double length);
				

			//Destroy Objects

				void	Destroy				(void);


			//Get Object's Parameters

				double	SphereGetRadius		(void);
				Vector3 BoxGetLengths		(void);
				Vector4 PlaneGetParams		(void);
				void	CCylinderGetParams	(double *radius, double *length);
				int		GetClass			(void);


		//Properties
		
			private:
			
				dGeomID _id;
	
	};

}
