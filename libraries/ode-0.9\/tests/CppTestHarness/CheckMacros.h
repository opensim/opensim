#ifndef CHECK_MACROS_H
#define CHECK_MACROS_H

#include "Checks.h"

#define CHECK(value) \
	if (!CppTestHarness::Check(value)) \
		testResults_.ReportFailure(__FILE__, __LINE__, #value);

#define CHECK_EQUAL(actual, expected) \
	if (!CppTestHarness::CheckEqual(actual, expected)) \
		testResults_.ReportFailure(__FILE__, __LINE__, CppTestHarness::BuildFailureString(expected, actual));

#define CHECK_CLOSE(actual, expected, tolerance) \
	if (!CppTestHarness::CheckClose(actual, expected, tolerance)) \
		testResults_.ReportFailure(__FILE__, __LINE__, CppTestHarness::BuildFailureString(expected, actual));

#define CHECK_ARRAY_EQUAL(actual, expected, count) \
	if (!CppTestHarness::CheckArrayEqual(actual, expected, count)) \
		testResults_.ReportFailure(__FILE__, __LINE__, CppTestHarness::BuildFailureString(expected, actual, count));

#define CHECK_ARRAY_CLOSE(actual, expected, count, tolerance) \
	if (!CppTestHarness::CheckArrayClose(actual, expected, count, tolerance)) \
		testResults_.ReportFailure(__FILE__, __LINE__, CppTestHarness::BuildFailureString(expected, actual, count));

#endif

