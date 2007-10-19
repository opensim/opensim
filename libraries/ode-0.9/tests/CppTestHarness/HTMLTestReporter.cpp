#include "HTMLTestReporter.h"

#ifdef _MSC_VER
#    pragma warning(disable:4996) // shut the secure crt stuff up, ctime is used correctly here.
#endif

#include <iostream>
#include <ctime>
#include <sstream>

namespace CppTestHarness
{

void HTMLTestReporter::ReportFailure(char const* file, int const line, std::string const failure)
{
	std::stringstream msg;
	msg << file << "(" << line << ") : " << failure;

	m_failureMessages.push_back(msg.str());
}

void HTMLTestReporter::ReportSingleResult(const std::string& testName, bool failed)
{
	ResultRecord r;
	r.testName = testName;
	r.failed = failed;

	//get reported failures and clear temp list
	r.failureMessages = m_failureMessages;	
	m_failureMessages.clear();

	m_results.push_back(r);
}

void HTMLTestReporter::ReportSummary(int const testCount, int const failureCount)
{
	std::ostream& os = std::cout;

	//TODO: make all strings html safe
	os << "<!DOCTYPE HTML PUBLIC \"-//W3C//DTD HTML 4.01 Transitional//EN\" \"http://www.w3.org/TR/html4/loose.dtd\">";
	os << "<html>\n";
	os << "<head>\n";
	os << "    <title>Test Report</title>\n";
	os << "</head>\n";
	os << "<body text=\"black\" bgcolor=\"white\">\n";

	std::time_t currtime;
	std::time(&currtime);

	os << "<h1>Test Report: " << std::ctime(&currtime) << "</h1>\n";
	os << "<p><em>";
	os << testCount << " tests run.<br />\n";
	os << failureCount << " failed.<br />\n";
	os << "</em></p>";

	os << "<table border=1 bgcolor=\"#dddddd\">";

	//for each single test
	for (ResultList::iterator i = m_results.begin(); i != m_results.end(); ++i)
	{
		os << "<tr>";
		os << "<td><strong>" << i->testName << "</strong></td>";
		
		if (i->failed)
			os << "<td bgcolor=\"#ff0000\"><strong>FAILED</strong></td>";
		else
			os << "<td bgcolor=\"#00ee00\"><strong>PASSED</strong></td>";

		os << "</tr><tr><td>";
		if (i->failed) os << "<ul>";

		for (MessageList::iterator j = i->failureMessages.begin(); j != i->failureMessages.end(); ++j)
		{
			os << "<li><code>"<< *j << "</code></li>\n";
		}

		if (i->failed) os << "</ul>";
		os << "</td></tr>";
	}

	os << "</table>";
	os << "</body>";
	os << "</html>";
}

}

