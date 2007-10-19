#include "TestRunner.h"
#include "TestLauncher.h"
#include "TestResults.h"
#include "Test.h"

#include "PrintfTestReporter.h"

namespace CppTestHarness
{

TestRunner::TestRunner()
	: m_testLauncherListHead(TestLauncher::GetHeadAddr())
	, m_testReporter(&m_defaultTestReporter)
{
}

TestRunner::~TestRunner()
{
}

void TestRunner::SetTestLauncherListHead(TestLauncher** listHead)
{
	m_testLauncherListHead = listHead;
}

void TestRunner::SetTestReporter(TestReporter* testReporter)
{
	m_testReporter = testReporter;
}

int TestRunner::RunAllTests()
{
	int failureCount = 0;

	int testCount = 0;
	TestLauncher const* curLauncher = *m_testLauncherListHead;
	while (curLauncher)
	{
		++testCount;

		TestResults result(*m_testReporter);
		curLauncher->Launch(result);

		if (result.Failed())
			++failureCount;

		curLauncher = curLauncher->GetNext();
	}

	m_testReporter->ReportSummary(testCount, failureCount);

	return failureCount;
}

}

