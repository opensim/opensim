#pragma once

namespace ODEManaged
{
	__gc public class JointGroup
	{
	public:

		//Constructors

			JointGroup(void);
			JointGroup(int maxSize);
		

		//Destructor

			~JointGroup(void);


		//Methods
		
			dJointGroupID Id(void);
			void Create(int maxSize);
			void Empty(void);

		
		private:

			dJointGroupID _id;

	};

}
