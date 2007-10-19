#ifndef TEST_LAUNCHER_H
#define TEST_LAUNCHER_H

namespace CppTestHarness
{
class TestResults;
class TestRegistry;

class TestLauncher
{
public:
	virtual void Launch(TestResults& results_) const = 0;

	static TestLauncher** GetHeadAddr();
	TestLauncher const* GetNext() const;

protected:
	TestLauncher(TestLauncher** listHead);
	virtual ~TestLauncher();

private:
	TestLauncher const* m_next;

	// revoked
	TestLauncher();
	TestLauncher(TestLauncher const&);
	TestLauncher& operator =(TestLauncher const&);
};
}

#endif

