#ifndef TEST_H
#define TEST_H

#include <string>

namespace CppTestHarness
{
class TestResults;

class Test
{
public:
	virtual ~Test();
	void Run(TestResults& testResults);

	static Test* GetListHead();

protected:
	Test(std::string testName = std::string(), 
	     std::string filename = std::string(), 
	     int lineNumber = 0);

private:
	virtual void RunImpl(TestResults& testResults_) = 0;

	std::string const m_testName;
	std::string const m_filename;
	int const m_lineNumber;

	Test* m_listNext;

	// revoked
	Test(Test const&);
	Test& operator =(Test const&);
};

}

#endif

