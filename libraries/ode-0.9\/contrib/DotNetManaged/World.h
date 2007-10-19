#pragma once

#include "CommonMgd.h"

namespace ODEManaged
{	
	__gc public class World
	{	
	public:

		//Constructor
		
			World(void);


		//Destructor
			
			~World(void);

			
		// Methods

			dWorldID Id(void);
			
			void SetGravity(double x, double y, double z);

			//Overloaded GetGravity
			Vector3 GetGravity(void);		
			void GetGravity(double gravity __gc[]);

			void Step(double stepSize);


		//Properties

			//Constraint Force Mixing
			__property void set_CFM(double cfm)
			{
				dWorldSetCFM(this->_id,cfm);
			}

			__property double get_CFM(void)
			{
				return dWorldGetCFM(this->_id); 
			}


			//Error Reduction Parameter
			__property void set_ERP(double erp)
			{
				dWorldSetERP(this->_id,erp);
			}

			__property double get_ERP(void)
			{
				return dWorldGetERP(this->_id); 
			}


		private:

			dWorldID _id;

	};

}

