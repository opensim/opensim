#ifndef TYPED_TEST_CREATOR_H
#define TYPED_TEST_CREATOR_H

#include "TestLauncher.h"

namespace CppTestHarness
{

template< typename TestClass >
class TypedTestLauncher : public TestLauncher
{
public:
	TypedTestLauncher(TestLauncher** listHead)
		: TestLauncher(listHead)
	{
	}

	virtual void Launch(TestResults& testResults_) const 
	{
		TestClass().Run(testResults_);
	}
};

}

#endif

