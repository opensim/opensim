#ifndef TEST_REPORTER_H
#define TEST_REPORTER_H

#include <string>

namespace CppTestHarness
{

class TestReporter
{
public:
	virtual ~TestReporter();

	virtual void ReportFailure(char const* file, int line, std::string failure) = 0;
	virtual void ReportSingleResult(const std::string& testName, bool failed) = 0;
	virtual void ReportSummary(int testCount, int failureCount) = 0;

protected:
	TestReporter();
};

}
#endif

