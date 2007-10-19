#include "StdAfx.h"

#include <ode/ode.h>
#include "jointhinge2.h"

namespace ODEManaged
{
	//Constructors
	JointHinge2::JointHinge2(void) : Joint(){}

	JointHinge2::JointHinge2(World &world)
	{
		if(this->_id) dJointDestroy(this->_id);
		_id = dJointCreateHinge2(world.Id(),0);
	}
	
	JointHinge2::JointHinge2(World &world, JointGroup &jointGroup)
	{
		if(this->_id) dJointDestroy(this->_id);
		_id = dJointCreateHinge2(world.Id(), jointGroup.Id());
	}

	//Destructor
	JointHinge2::~JointHinge2(void){}

	//CreateHinge2 (overload 1)
	void JointHinge2::Create(World &world, JointGroup &jointGroup)
	{
		if(this->_id) dJointDestroy(this->_id);
		_id = dJointCreateHinge2(world.Id(), jointGroup.Id());
	}

	//CreateHinge2 (overload 2)
	void JointHinge2::Create(World &world)
	{
		if(this->_id) dJointDestroy(this->_id);
		_id = dJointCreateHinge2(world.Id(),0);
	}

	//SetAnchor1
	void JointHinge2::SetAnchor (double x, double y ,double z)
	{
		dJointSetHinge2Anchor(_id, x,y,z);
	}

	//GetAnchor1
	Vector3 JointHinge2::GetAnchor()
	{
		Vector3 retVal;
		dVector3 temp;
		dJointGetHinge2Anchor(_id,temp);
		retVal.x = temp[0];
		retVal.y = temp[1];
		retVal.z = temp[2];
		return retVal;
	}

	//SetAxis1
	void JointHinge2::SetAxis1 (double x, double y ,double z)
	{
		dJointSetHinge2Axis1(_id, x,y,z);
	}

	//GetAxis1
	Vector3 JointHinge2::GetAxis1()
	{
		Vector3 retVal;
		dVector3 temp;
		dJointGetHinge2Axis1(_id,temp);
		retVal.x = temp[0];
		retVal.y = temp[1];
		retVal.z = temp[2];
		return retVal;
	}

	//SetAxis2
	void JointHinge2::SetAxis2 (double x, double y ,double z)
	{
		dJointSetHinge2Axis2(_id, x,y,z);
	}

	//GetAxis2
	Vector3 JointHinge2::GetAxis2()
	{
		Vector3 retVal;
		dVector3 temp;
		dJointGetHinge2Axis2(_id,temp);
		retVal.x = temp[0];
		retVal.y = temp[1];
		retVal.z = temp[2];
		return retVal;
	}

	//GetAngle1
	double JointHinge2::GetAngle1 ()
	{
		return dJointGetHinge2Angle1(this->_id);
	}

	//GetAngle1Rate
	double JointHinge2::GetAngle1Rate ()
	{
		return dJointGetHinge2Angle1Rate(this->_id);
	}

	////GetAngle hmm, this doesn't exist
	//double JointHinge2::GetAngle2 ()
	//{
	//	return dJointGetHinge2Angle2(this->_id);
	//}

	//GetAngle2Rate
	double JointHinge2::GetAngle2Rate ()
	{
		return dJointGetHinge2Angle2Rate(this->_id);
	}


	//Attach (overload 1)
	void JointHinge2::Attach (Body &body1, Body &body2)
	{
		dJointAttach(_id, body1.Id(),body2.Id());
	}

	//Attach (overload 2) 
	//TODO: possibly add an overload that takes anchor as a param also.
	void JointHinge2::Attach (Body &body1)
	{
		dJointAttach(_id, body1.Id(),0);
	}


}
