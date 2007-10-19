#ifndef HTML_TEST_REPORTER
#define HTML_TEST_REPORTER

#include "TestReporter.h"
#include <vector>

namespace CppTestHarness
{

class HTMLTestReporter : public TestReporter
{
public:
	virtual void ReportFailure(char const* file, int line, std::string failure);
	virtual void ReportSingleResult(const std::string& testName, bool failed);
	virtual void ReportSummary(int testCount, int failureCount);

private:
	typedef std::vector<std::string> MessageList;

	struct ResultRecord 
	{
		std::string testName;
		bool failed;
		MessageList failureMessages;
	};

	MessageList m_failureMessages;

	typedef std::vector<ResultRecord> ResultList;
	ResultList m_results;
};

}

#endif //HTML_TEST_REPORTER

