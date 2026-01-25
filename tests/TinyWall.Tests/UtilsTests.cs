using System;
using System.IO;
using System.Text;
using Xunit;
using pylorak.TinyWall;

namespace TinyWall.Tests
{
    public class UtilsTests
    {
        [Fact]
        public void HexEncode_ValidByteArray_ReturnsCorrectHexString()
        {
            // Arrange
            byte[] input = { 0xFF, 0x00, 0xAB };
            string expected = "FF00AB";

            // Act
            string result = Utils.HexEncode(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void IsNullOrEmpty_NullInput_ReturnsTrue()
        {
            // Arrange
            string input = null;

            // Act
            bool result = Utils.IsNullOrEmpty(input);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsNullOrEmpty_EmptyInput_ReturnsTrue()
        {
            // Arrange
            string input = "";

            // Act
            bool result = Utils.IsNullOrEmpty(input);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsNullOrEmpty_WhitespaceInput_ReturnsTrue()
        {
            // Arrange
            string input = "   \t\n  ";

            // Act
            bool result = Utils.IsNullOrEmpty(input);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsNullOrEmpty_NonEmptyInput_ReturnsFalse()
        {
            // Arrange
            string input = "not empty";

            // Act
            bool result = Utils.IsNullOrEmpty(input);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void GetLongPathName_NullOrEmptyPath_ReturnsEmpty()
        {
            // Arrange
            string input = null;

            // Act
            string result = Utils.GetLongPathName(input);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void GetLongPathName_EmptyPath_ReturnsEmpty()
        {
            // Arrange
            string input = "";

            // Act
            string result = Utils.GetLongPathName(input);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void RandomString_GeneratesCorrectLength()
        {
            // Arrange
            int length = 10;

            // Act
            string result = Utils.RandomString(length);

            // Assert
            Assert.Equal(length, result.Length);
        }

        [Fact]
        public void RandomString_GeneratesDifferentStrings()
        {
            // Act
            string result1 = Utils.RandomString(10);
            string result2 = Utils.RandomString(10);

            // Assert - highly unlikely to generate same random string twice
            // Though not impossible, this test should pass in most cases
            Assert.NotEqual(result1, result2);
        }

        [Fact]
        public void EqualsCaseInsensitive_SameStrings_ReturnsTrue()
        {
            // Arrange
            string a = "Test";
            string b = "Test";

            // Act
            bool result = Utils.EqualsCaseInsensitive(a, b);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EqualsCaseInsensitive_DifferentCaseStrings_ReturnsTrue()
        {
            // Arrange
            string a = "Test";
            string b = "TEST";

            // Act
            bool result = Utils.EqualsCaseInsensitive(a, b);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EqualsCaseInsensitive_DifferentStrings_ReturnsFalse()
        {
            // Arrange
            string a = "Test";
            string b = "Different";

            // Act
            bool result = Utils.EqualsCaseInsensitive(a, b);

            // Assert
            Assert.False(result);
        }
    }
}