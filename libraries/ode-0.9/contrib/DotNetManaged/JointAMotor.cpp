#include "StdAfx.h"

#include <ode/ode.h>
#include "JointAMotor.h"

namespace ODEManaged
{

	//Constructors

		JointAMotor::JointAMotor(void) : Joint(){}


		JointAMotor::JointAMotor(World &world)
		{
			if(this->_id) dJointDestroy(this->_id);
			_id = dJointCreateAMotor(world.Id(), 0);
		}

		
		JointAMotor::JointAMotor(World &world, JointGroup &jointGroup)
		{
			if(this->_id) dJointDestroy(this->_id);
			_id = dJointCreateAMotor(world.Id(), jointGroup.Id());
		}


	//Destructor

		JointAMotor::~JointAMotor(void){}


	//Methods

		//Overloaded Create 
			void JointAMotor::Create(World &world, JointGroup &jointGroup)
			{
				if(this->_id) dJointDestroy(this->_id);
				_id = dJointCreateAMotor(world.Id(), jointGroup.Id());
			}

			void JointAMotor::Create(World &world)
			{
				if(this->_id) dJointDestroy(this->_id);
				_id = dJointCreateAMotor(world.Id(), 0);
			}


		//Overloaded Attach
			void JointAMotor::Attach(Body &body1, Body &body2)
			{
				dJointAttach(this->_id, body1.Id(), body2.Id());
			}

			void JointAMotor::Attach(Body &body1)
			{
				dJointAttach(this->_id, body1.Id(), 0);
			}


		//SetNumAxes

			void JointAMotor::SetNumAxes(int num)
			{
				dJointSetAMotorNumAxes(this->_id, num);
			}


		//GetNumAxes

			int JointAMotor::GetNumAxes(void)
			{
				return dJointGetAMotorNumAxes(this->_id);
			}


		//SetAxis

			void JointAMotor::SetAxis(int anum, int rel, double x, double y ,double z)
			{
				dJointSetAMotorAxis(this->_id, anum, rel, x, y, z);
			}


		//GetAxis

			Vector3 JointAMotor::GetAxis(int anum)
			{
				Vector3 retVal;
				dVector3 temp;
				dJointGetAMotorAxis(this->_id, anum, temp);
				retVal.x = temp[0];
				retVal.y = temp[1];
				retVal.z = temp[2];
				return retVal;
			}


		//SetAngle

			void JointAMotor::SetAngle(int anum, double angle)
			{
				dJointSetAMotorAngle(this->_id, anum, angle);
			}


		//GetAngle

			double JointAMotor::GetAngle(int anum)
			{
				return dJointGetAMotorAngle(this->_id, anum);
			}


		//SetParam

			void JointAMotor::SetParam(int parameter, double value)
			{
				dJointSetAMotorParam(this->_id, parameter, value);
			}


		//GetParam

			double JointAMotor::GetParam(int parameter)
			{
				return dJointGetAMotorParam(this->_id, parameter);
			}


		//SetMode

			void JointAMotor::SetMode(int mode)
			{
				dJointSetAMotorMode(this->_id, mode);
			}


		//GetMode

			int JointAMotor::GetMode(void)
			{
				return dJointGetAMotorMode(this->_id);
			}


		//GetAxisRel

			int JointAMotor::GetAxisRel(int anum)
			{
				return dJointGetAMotorAxisRel(this->_id, anum);
			}


		//GetAngleRate

			double JointAMotor::GetAngleRate(int anum)
			{
				return dJointGetAMotorAngleRate(this->_id, anum);
			}

}
