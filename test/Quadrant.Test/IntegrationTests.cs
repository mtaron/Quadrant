using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace Quadrant.Test
{
    [TestClass]
    public class IntegrationTests : UITest
    {
        [UITestMethod]
        public void RemoveFunction()
        {
            int id = AddFunction("atan(x)");
            id.Should().Be(1);
            ToggleAngleType();
            IReadOnlyList<int> removedFunctions = RemoveFunction(id);
            removedFunctions.Should().ContainSingle(x => x == id);
        }
    }
}
