#ifndef PRINTF_TEST_REPORTER
#define PRINTF_TEST_REPORTER

#include "TestReporter.h"

namespace CppTestHarness
{

class PrintfTestReporter : public TestReporter
{
private:
	virtual void ReportFailure(char const* file, int line, std::string failure);
	virtual void ReportSingleResult(const std::string& testName, bool failed);
	virtual void ReportSummary(int testCount, int failureCount);
};

}

#endif 

