#ifndef TEST_RESULTS_H
#define TEST_RESULTS_H

#include <string>

namespace CppTestHarness
{

class TestReporter;

class TestResults
{
public:
	explicit TestResults(TestReporter& reporter);

	void ReportFailure(char const* file, int line, std::string failure);
	void ReportDone(const std::string& testName);

	bool Failed() const;

private:
	bool m_failure;
	TestReporter& m_testReporter;

	// revoked
	TestResults(TestResults const&);
	TestResults& operator =(TestResults const&);
};

}

#endif

