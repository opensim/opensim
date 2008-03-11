using System;

namespace OpenSim.TestSuite
{

    public class Utils
    {
        enum Result {
            Fail = 0,
            Pass = 1,
            Skip = 3
        }
        
        private static String ResultToString(Result r)
        {
            if (r == Result.Pass)
            {
                return "PASS";
            } 
            else if (r == Result.Fail)
            {
                return "FAIL";
            }
            else if (r == Result.Skip)
            {
                return "SKIP";
            }
            else 
            {
                return "UNKNOWN";
            }
        }
        
        
        private static void TestResult(Result r, String msg)
        {
            System.Console.WriteLine("[{0}]: {1}", ResultToString(r), msg);
        }
        
        public static void TestFail(String msg)
        {
            TestResult(Result.Fail, msg);
        }
        
        public static void TestPass(String msg)
        {
            TestResult(Result.Pass, msg);
        }
        
        public static void TestSkip(String msg)
        {
            TestResult(Result.Skip, msg);       
        }
    }
}