#ifndef SIGNAL_TRANSLATOR_H
#define SIGNAL_TRANSLATOR_H

#include <signal.h>
#include <setjmp.h>

namespace CppTestHarness
{

template <int SIGNAL>
class SignalTranslator {
public:
	SignalTranslator()
	{
		//setup new signal handler
		struct sigaction act;
		act.sa_handler = signalHandler;
		sigemptyset(&act.sa_mask);
		act.sa_flags = 0;

		sigaction(SIGNAL, &act, &m_oldAction);

		if (sigsetjmp(getJumpPoint(), 1) != 0)
		{
			//if signal thrown we will return here from handler
			throw "Unhandled system exception";
		}
	}

	~SignalTranslator()
	{
		sigaction(SIGNAL, &m_oldAction, 0);
	}

private:
	static void signalHandler(int signum)
	{
		siglongjmp(getJumpPoint(), signum);
	}

		static sigjmp_buf& getJumpPoint()
		{
			static sigjmp_buf jmpPnt;
			return jmpPnt;
		}

	struct sigaction m_oldAction;
};

} //CppTestHarness

#endif //SIGNAL_TRANSLATOR_H

