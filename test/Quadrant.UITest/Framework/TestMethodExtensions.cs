using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Quadrant.UITest.Framework
{
    public static class TestMethodExtensions
    {
        public static TestResult[] CreateExceptionResult(this ITestMethod testMethod, Exception exception)
        {
            var errorResult = new TestResult()
            {
                DisplayName = testMethod.TestMethodName,
                Outcome = UnitTestOutcome.Error,
                TestFailureException = exception
            };
            return new TestResult[] { errorResult };
        }
    }
}
