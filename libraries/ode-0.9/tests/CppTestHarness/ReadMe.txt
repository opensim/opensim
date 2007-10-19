------------------------------------------------------------------------
CppTestHarness

written by Charles Nicholson (cn@cnicholson.net).
linux/gcc port by Dan Lind (podcat@gmail.com).

This work is based on CppUnitLite by Michael Feathers with changes inspired by Noel Llopis.

You the user have free license to do whatever you want with this source code.
No persons mentioned above accept any responsibility if files in this archive 
set your computer on fire or subject you to any number of woes. Use at your own risk!



HISTORY:
------------------------------------------------------------------------
28 dec 2005, charles nicholson (cn@cnicholson.net)
- upgraded win32 build to VS.NET 2005
- silenced all 'conditional expression is constant' warning (CHECK(true), CHECK_EQUAL(1,1), etc...)

20 dec 2005, dan lind (podcat@gmail.com)
- added signal-to-exception translator for posix systems
- more methods in TestReporter. We can now optionaly have output on each finished test
    HTMLTestReporter illustrates a fairly complex reporter doing this.

13 dec 2005, dan lind (podcat@gmail.com)
- added newlines at the end of all files (this is a warning on gcc)
- reordered initialization list of TestRunner (init order not same as order in class)
- added _MSC_VER to TestCppTestHarness.cpp to block pragmas from gcc

11 dec 2005, charles nicholson (cn@cnicholson.net)
- get rid of TestRegistry and static std::vector.
- TestRunner holds a PrintfTestReporter by value to avoid dynamic allocation at static-init
- TestCreator -> TestLauncher are now nodes in a linked list of tests.
