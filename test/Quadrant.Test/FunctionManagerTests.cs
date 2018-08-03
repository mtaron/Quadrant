using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quadrant.Functions;

namespace Quadrant.Test
{
    [TestClass]
    public class FunctionManagerTests
    {
        [TestMethod]
        public void FunctionManager_AngleType_RadianDefault()
        {
            // Arrange
            var functionManager = new FunctionManager();

            // Act
            // Testing defaults, do nothing.

            // Assert
            functionManager.UseRadians.Should().BeTrue();
        }

        [TestMethod]
        public void FunctionManager_AngleType_SetRadiansFalse()
        {
            // Arrange
            bool isInvalidated = false;
            var functionManager = new FunctionManager();
            functionManager.Invalidated += (s, e) => isInvalidated = true;

            // Act
            functionManager.UseRadians = false;

            // Assert
            functionManager.UseRadians.Should().BeFalse();
            isInvalidated.Should().BeTrue();
        }
    }
}
