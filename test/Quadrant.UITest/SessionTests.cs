using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Quadrant.UITest
{
    [TestClass]
    public class SessionTests : QuadrantTest
    {
        [TestMethod]
        public void ResumeSession()
        {
            Resume("3688db8c-af9e-4585-9b4f-172080f9baa5");
        }
    }
}
