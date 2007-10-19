#include "TestLauncher.h"

namespace CppTestHarness
{

namespace
{
	TestLauncher* s_listHead;
}

TestLauncher** TestLauncher::GetHeadAddr()
{
	static bool initialized = false;
	if (!initialized)
	{
		s_listHead = 0;
		initialized = true;
	}

	return &s_listHead;
}

TestLauncher::TestLauncher(TestLauncher** listHead)
	: m_next(*listHead)
{
	*listHead = this;
}

TestLauncher::~TestLauncher()
{
}

TestLauncher const* TestLauncher::GetNext() const
{
	return m_next;
}

}

