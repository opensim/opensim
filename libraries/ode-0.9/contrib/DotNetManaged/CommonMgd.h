#pragma once

namespace ODEManaged
{

	__value public struct Vector3
	{
		double x;
		double y;
		double z;
	};


	__value public struct Vector4
	{
		double W;
		double x;
		double y;
		double z;
	};


	__value public struct Matrix3
	{
		double m11;
		double m12;
		double m13;
		double m21;
		double m22;
		double m23;
		double m31;
		double m32;
		double m33;
	};

	//__value public struct NearCallback
	//{
	//	void *data;
	//	dGeomID o1;
	//	dGeomID o2;
	//};

}