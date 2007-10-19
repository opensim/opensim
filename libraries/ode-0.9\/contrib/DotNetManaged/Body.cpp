#include "StdAfx.h"

#include <ode/ode.h>
#include "Body.h"

namespace ODEManaged
{

	//Constructors

		Body::Body(void)
		{
			_id = 0;
		}

		Body::Body(World &world)
		{
			_id = dBodyCreate(world.Id());
		}


	//Destructor

		Body::~Body(void)
		{
			dBodyDestroy(this->_id);
		}


	//Methods

		//Id
		dBodyID Body::Id()
		{
			return _id;
		}


		//SetData
		void Body::SetData(void *data)
		{
			dBodySetData(this->_id, data);
		}

		//GetData
		void *Body::GetData(void)
		{
			return dBodyGetData(this->_id);
		}


		//SetPosition
		void Body::SetPosition (double x, double y, double z)
		{
			dBodySetPosition(this->_id, x, y, z);
		}


	//Overloaded GetPosition
		Vector3 Body::GetPosition(void)
		{
			Vector3 retVal;
			const dReal *temp;
			temp = dBodyGetPosition(this->_id);
			retVal.x = temp[0];
			retVal.y = temp[1];
			retVal.z = temp[2];
			return retVal;
		};

		void Body::GetPosition(double position __gc[])
		{
			const dReal *temp;
			temp = dBodyGetPosition(this->_id);
			position[0] = temp[0];
			position[1] = temp[1];
			position[2] = temp[2];
		}


		//SetRotationIdentity
		void Body::SetRotationIdentity(void)
		{
			dMatrix3 temp;
			dRSetIdentity(temp);
			dBodySetRotation(this->_id, temp);	
		}
	

		//SetRotation (left handed system=>transpose)
		void Body::SetRotation(Matrix3 rotation)
		{
			dMatrix3 temp;
			temp[0] = rotation.m11;  
			temp[4] = rotation.m12;  
			temp[8] = rotation.m13; 
			temp[1] = rotation.m21;
			temp[5] = rotation.m22; 
			temp[9] = rotation.m23; 
			temp[2] = rotation.m31; 
			temp[6] = rotation.m32; 
			temp[10] = rotation.m33; 
			dBodySetRotation(this->_id, temp);
		}

		//GetRotation (left handed system=>transpose)
		Matrix3 Body::GetRotation(void)
		{
			Matrix3 retVal;
			//const dMatrix3 *m;
			const dReal *temp;
			temp = dBodyGetRotation(this->_id);
			retVal.m11 = temp[0];
			retVal.m12 = temp[4];
			retVal.m13 = temp[8];
			retVal.m21 = temp[1];
			retVal.m22 = temp[5];
			retVal.m23 = temp[9];
			retVal.m31 = temp[2];
			retVal.m32 = temp[6];
			retVal.m33 = temp[10];
			return retVal;	
		}


		//Overloaded SetMass
		void Body::SetMass(double mass, Vector3 centerOfGravity, Matrix3 inertia)
		{
			dMass *temp = new dMass();
			dMassSetParameters(temp, mass, 
							   centerOfGravity.x, 
							   centerOfGravity.y, 
							   centerOfGravity.z, 
							   inertia.m11, inertia.m22, 
							   inertia.m33, inertia.m12, 
							   inertia.m13, inertia.m23);

  			dBodySetMass(this->_id, temp);
		}


		//SetMassSphere
		void Body::SetMassSphere(double density, double radius)
		{
			dMass *temp = new dMass();
			dMassSetSphere(temp, density, radius);
			dBodySetMass(this->_id, temp);
		}	


		//SetMassBox
		void Body::SetMassBox(double density, double sideX, double sideY, double sideZ)
		{
			dMass *temp = new dMass();
			dMassSetBox(temp, density, sideX, sideY, sideZ);
			dBodySetMass(this->_id, temp);
		}	


		//SetMassCappedCylinder
		void Body::SetMassCappedCylinder(double density, int axis, double cylinderRadius, double cylinderLength)
		{
			dMass *temp = new dMass();
			dMassSetCappedCylinder(temp, density, axis,
								   cylinderRadius, 
								   cylinderLength);

			dBodySetMass(this->_id, temp);
		}


		//AddForce
		void Body::AddForce(double fX, double fY, double fZ)
		{
			dBodyAddForce(this->_id, fX, fY, fZ);
		}


		//AddRelForce
		void Body::AddRelForce(double fX, double fY, double fZ)
		{
			dBodyAddRelForce(this->_id, fX,fY,fZ);
		}


		//AddForceAtPos
		void Body::AddForceAtPos(double fX, double fY, double fZ, double pX, double pY, double pZ)
		{
			dBodyAddForceAtPos(this->_id, fX, fY, fZ, pX, pY, pZ);
		}


		//AddRelForceAtPos
		void Body::AddRelForceAtPos(double fX, double fY, double fZ, double pX, double pY, double pZ)
		{
			dBodyAddRelForceAtPos(this->_id, fX, fY, fZ, pX, pY, pZ);
		}


		//AddRelForceAtRelPos
		void Body::AddRelForceAtRelPos(double fX, double fY, double fZ, double pX, double pY, double pZ)
		{
			dBodyAddRelForceAtRelPos(this->_id, fX, fY, fZ, pX, pY, pZ);
		}	


		//ApplyLinearVelocityDrag
		void Body::ApplyLinearVelocityDrag(double dragCoef)
		{
			const dReal *temp;
			double fX;
			double fY;
			double fZ;		
			temp = dBodyGetLinearVel(this->_id);			
			fX = temp[0]*dragCoef*-1;
			fY = temp[1]*dragCoef*-1;
			fZ = temp[2]*dragCoef*-1;
			dBodyAddForce(this->_id, fX, fY, fZ);
		}


		//ApplyAngularVelocityDrag
		void Body::ApplyAngularVelocityDrag(double dragCoef)
		{
			const dReal *temp;
			double fX;
			double fY;
			double fZ;
			temp = dBodyGetAngularVel(this->_id);
			fX = temp[0]*dragCoef*-1;
			fY = temp[1]*dragCoef*-1;
			fZ = temp[2]*dragCoef*-1;
			dBodyAddTorque(this->_id, fX, fY, fZ);
		}


		//AddTorque
		void Body::AddTorque(double fX, double fY, double fZ)
		{
			dBodyAddTorque(this->_id, fX, fY, fZ);
		}


		//AddRelTorque
		void Body::AddRelTorque(double fX, double fY, double fZ)
		{
			dBodyAddRelTorque(this->_id, fX,fY,fZ);
		}


		//SetLinearVelocity
		void Body::SetLinearVelocity(double x, double y, double z)
		{
			dBodySetLinearVel(this->_id, x, y, z);
		}


		//GetLinearVelocity
		Vector3 Body::GetLinearVelocity(void)
		{
			Vector3 retVal;
			const dReal *temp;
			temp = dBodyGetLinearVel(this->_id);
			retVal.x = temp[0];
			retVal.y = temp[1];
			retVal.z = temp[2];
			return retVal;
		}


		//SetAngularVelocity
		void Body::SetAngularVelocity(double x, double y, double z)
		{
			dBodySetAngularVel(this->_id, x, y, z);
		}

		//GetAngularVelocity
		Vector3 Body::GetAngularVelocity(void)
		{
			Vector3 retVal;
			const dReal *temp;
			temp = dBodyGetAngularVel(this->_id);
			retVal.x = temp[0];
			retVal.y = temp[1];
			retVal.z = temp[2];
			return retVal;
		}

		
		//GetRelPointPos
		Vector3 Body::GetRelPointPos(double pX, double pY, double pZ)
		{
			Vector3 retVal;
			dVector3 temp;
			dBodyGetRelPointPos(this->_id, pX, pY, pZ, temp);
			retVal.x = temp[0];
			retVal.y = temp[1];
			retVal.z = temp[2];
			return retVal;
		}


		//GetRelPointVel
		Vector3 Body::GetRelPointVel(double pX, double pY, double pZ)
		{
			Vector3 retVal;
			dVector3 temp;
			dBodyGetRelPointVel(this->_id, pX, pY, pZ, temp);
			retVal.x = temp[0];
			retVal.y = temp[1];
			retVal.z = temp[2];
			return retVal;
		}


		//ConnectedTo
		int Body::ConnectedTo(const Body &b)
		{ 
			return dAreConnected(this->_id, b._id);
		}

}
