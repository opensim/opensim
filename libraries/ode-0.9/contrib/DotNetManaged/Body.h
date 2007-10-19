#pragma once

#include "World.h"
#include "CommonMgd.h"

namespace ODEManaged
{	
	__gc public class Body
	{
		public:

			//Constructors and Destructors
		
			Body(void);
			Body(World &world);
		
			~Body(void);


			//Public Methods

			dBodyID Id();
			void	SetData				(void *data);
			void	*GetData			(void);

			//POSITION
			void SetPosition(double x, double y, double z);
			Vector3 GetPosition(void);
			void GetPosition(double  position __gc[]);
			
			//ROTATION
			void SetRotationIdentity(void);
			void SetRotation(Matrix3 rotation);
			Matrix3 GetRotation(void);

			//MASS
			void SetMass(double mass, Vector3 centerOfGravity, Matrix3 inertia);
			void SetMassSphere(double density, double radius);
			void SetMassBox(double density, double sideX, double sideY, double sideZ);
			void SetMassCappedCylinder(double density, int axis, double cylinderRadius, double cylinderLength);

			//FORCE AND TORQUE
			void AddForce(double fX, double fY, double fZ);
			void AddRelForce(double fX, double fY, double fZ);
			void AddForceAtPos(double fX, double fY, double fZ,double pX, double pY, double pZ);
			void AddRelForceAtPos(double fX, double fY, double fZ,double pX, double pY, double pZ);
			void AddRelForceAtRelPos(double fX, double fY, double fZ,double pX, double pY, double pZ);
			void ApplyLinearVelocityDrag(double dragCoef);
			void ApplyAngularVelocityDrag(double dragCoef);

			
			void AddTorque(double fX, double fY, double fZ);
			void AddRelTorque(double fX, double fY, double fZ);

			//LINEAR VELOCITY
			void SetLinearVelocity (double x, double y, double z);
			Vector3 GetLinearVelocity(void);

			//ANGULAR VELOCITY
			void SetAngularVelocity (double x, double y, double z);
			Vector3 GetAngularVelocity(void);

			//POINT
			Vector3 GetRelPointPos(double pX, double pY, double pZ);
			Vector3 GetRelPointVel(double pX, double pY, double pZ);

			//CONNECTED TO
			int ConnectedTo (const Body &b);

		private:
			
			dBodyID _id;

	};
}

