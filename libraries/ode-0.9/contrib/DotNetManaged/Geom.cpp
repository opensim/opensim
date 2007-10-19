
#include "StdAfx.h"

#include <ode/ode.h>
#include "Geom.h"


namespace ODEManaged
{

	//Constructors

		Geom::Geom(void)
		{ 
			_id = 0;
		}


	//Destructor

		Geom::~Geom(void)
		{
			dGeomDestroy(this->_id);
		}


	//Methods
			
		//Id
		dGeomID Geom::Id(void)
		{
			return _id;
		}


		//GetBody
		dBodyID Geom::GetBody(void)
		{
			return dGeomGetBody(this->_id);
		}


		//Overloaded SetBody
		void Geom::SetBody(Body &body)
		{
			dGeomSetBody(this->_id, body.Id());
		}

		//void Geom::SetBody(dBodyID b)
		//{
		//	dGeomSetBody(this->_id, b);
		//}


		//SetPosition
		void Geom::SetPosition(double x, double y, double z)
		{
			dGeomSetPosition(this->_id, x, y, z);
		}


		//SetRotation
		void Geom::SetRotation(Matrix3 rotation)
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
			dGeomSetRotation(_id, temp);
		}
		
		
		//Destroy
		void Geom::Destroy() 
		{
			if(this->_id) dGeomDestroy(this->_id);
			_id = 0;
		}


		//SetData
		void Geom::SetData(void *data)
		{
			dGeomSetData(this->_id, data);
		}


		//GetData
		void *Geom::GetData(void)
		{
			return dGeomGetData(this->_id);
		}


		//GetPosition
		Vector3 Geom::GetPosition(void)
		{
			Vector3 retVal;
			const dReal *temp;
			temp = dGeomGetPosition(this->_id);
			retVal.x = temp[0];
			retVal.y = temp[1];
			retVal.z = temp[2];
			return retVal;
		}


		//GetRotation (left handed system=>transpose)
		Matrix3 Geom::GetRotation(void)
		{
			Matrix3 retVal;
			const dReal *temp;
			temp = dGeomGetRotation(this->_id);
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


		//CreateSphere
		void Geom::CreateSphere(Space &space, double radius)
		{
			if(this->_id) dGeomDestroy(this->_id);
			_id = dCreateSphere(space.Id(), radius);
		}


		//CreateBox
		void Geom::CreateBox(Space &space, double lx, double ly, double lz)
		{
			if(this->_id) dGeomDestroy(this->_id);
			_id = dCreateBox(space.Id(), lx, ly, lz);
		}

		
		//CreatePlane
		void Geom::CreatePlane(Space &space, double a, double b, double c, double d) 
		{
			if(this->_id) dGeomDestroy(this->_id);
			_id = dCreatePlane(space.Id(), a, b, c, d);
		}


		//CreateCCylinder
		void Geom::CreateCCylinder(Space &space, double radius, double length)
		{
			if(this->_id) dGeomDestroy(this->_id);
			_id = dCreateCCylinder(space.Id(), radius, length);
		}
	

		//SphereGetRadius
		double Geom::SphereGetRadius(void)
		{
			return dGeomSphereGetRadius(this->_id);
		}
		
		
		//BoxGetLengths
		Vector3 Geom::BoxGetLengths(void)
		{
			Vector3 retVal;
			dVector3 temp;
			dGeomBoxGetLengths(this->_id, temp);
			retVal.x = temp[0];
			retVal.y = temp[1];
			retVal.z = temp[2];
			return retVal;
		}


		//PlaneGetParams
		Vector4 Geom::PlaneGetParams(void)
		{
			Vector4 retVal;
			dVector4 temp;
			dGeomPlaneGetParams(this->_id, temp);
			retVal.W = temp[0];
			retVal.x = temp[1];
			retVal.y = temp[2];
			retVal.z = temp[3];
			return retVal;
		}
		
		
		//CCylinderGetParams
		void Geom::CCylinderGetParams(double *radius, double *length)
		{
			dGeomCCylinderGetParams(this->_id, radius, length);
		}
		

		//GetClass
		int Geom::GetClass(void)
		{
			return dGeomGetClass(this->_id);
		}
		
}







