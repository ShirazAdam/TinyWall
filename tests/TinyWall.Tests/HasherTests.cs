using System;
using System.IO;
using System.Text;
using Xunit;

namespace TinyWall.Tests
{
    public class HasherTests
    {
        [Fact]
        public void HashString_ValidInput_ReturnsCorrectHash()
        {
            // Arrange
            string input = "test";
            string expected = "9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B2B0B822CD15D6C15B0F00A08";

            // Act
            string result = Hasher.HashString(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void HashStream_ValidStream_ReturnsCorrectHash()
        {
            // Arrange
            byte[] data = Encoding.UTF8.GetBytes("test");
            using var stream = new MemoryStream(data);
            string expected = "9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B2B0B822CD15D6C15B0F00A08";

            // Act
            string result = Hasher.HashStream(stream);

            // Assert
            Assert.Equal(expected, result);
        }
        
        [Fact]
        public void HashStream_StreamPositionReset_AfterHashing()
        {
            // Arrange
            byte[] data = Encoding.UTF8.GetBytes("test data for position reset");
            using var stream = new MemoryStream(data);

            // Act - hash twice to ensure position is reset
            string result1 = Hasher.HashStream(stream);
            string result2 = Hasher.HashStream(stream);

            // Assert
            Assert.Equal(result1, result2);
        }
    }
}