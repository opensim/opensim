#ifndef CHECKS_H
#define CHECKS_H

#include <sstream>
#include <cmath>

namespace CppTestHarness
{
	template< typename Value >
	bool Check(Value const value)
	{
#ifdef _MSC_VER
#	pragma warning(push)
#	pragma warning(disable:4127) // conditional expression is constant
#	pragma warning(disable:4800) // forcing value to bool true/false, performance warning
#endif
		return value;
#ifdef _MSC_VER
#	pragma warning(pop)
#endif
	}

	template< typename Actual, typename Expected >
	bool CheckEqual(Actual const actual, Expected const expected)
	{
#ifdef _MSC_VER
#	pragma warning(push)
#	pragma warning(disable:4127) // conditional expression is constant
#endif
		return (actual == expected);
#ifdef _MSC_VER
#	pragma warning(pop)
#endif
	}

	template< typename Actual, typename Expected >
	bool CheckArrayEqual(Actual const actual, Expected const expected, int const count)
	{
		for (int i = 0; i < count; ++i)
		{
			if (!(actual[i] == expected[i]))
				return false;
		}

		return true;
	}

	template< typename Actual, typename Expected, typename Tolerance >
	bool CheckClose(Actual const actual, Expected const expected, Tolerance const tolerance)
	{
		return (std::abs(double(actual) - double(expected)) <= double(tolerance));
	}

	template< typename Actual, typename Expected, typename Tolerance >
	bool CheckArrayClose(Actual const actual, Expected const expected, int const count, Tolerance const tolerance)
	{
		for (int i = 0; i < count; ++i)
		{
			if (!CheckClose(actual[i], expected[i], tolerance))
				return false;
		}

		return true;
	}

	template< typename Actual, typename Expected >
	std::string BuildFailureString(Actual const actual, Expected const expected)
	{
		std::stringstream failureStr;
		failureStr << "Expected " << actual << " but got " << expected << std::endl;
		return failureStr.str();
	}

	template< typename Actual, typename Expected >
	std::string BuildFailureString(Actual const* actual, Expected const* expected, int const count)
	{
		std::stringstream failureStr;
		int i;
		
		failureStr << "Expected [ ";

		for (i = 0; i < count; ++i)
			failureStr << expected[i] << ' ';

		failureStr << "] but got [ ";

		for (i = 0; i < count; ++i)
			failureStr << expected[i] << ' ';

		failureStr << ']' << std::endl;

        return failureStr.str();		
	}
}

#endif 

