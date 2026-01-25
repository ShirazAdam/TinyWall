using System;
using System.IO;
using System.Text;
using Xunit;
using pylorak.TinyWall;

namespace TinyWall.Tests
{
    public class FileUtilsTests
    {
        [Fact]
        public void GetExactPath_ExistingFile_ReturnsCorrectPath()
        {
            // Arrange
            string tempDir = Path.GetTempPath();
            string fileName = "test_file_" + Guid.NewGuid().ToString() + ".txt";
            string fullPath = Path.Combine(tempDir, fileName);
            
            try
            {
                // Create a temporary file
                File.WriteAllText(fullPath, "test content");

                // Act
                string result = Utils.GetExactPath(fullPath);

                // Assert
                Assert.Equal(fullPath, result);
            }
            finally
            {
                // Clean up
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }
        }

        [Fact]
        public void GetExactPath_NonExistentPath_ReturnsOriginalPath()
        {
            // Arrange
            string nonExistentPath = Path.Combine(Path.GetTempPath(), "non_existent_file_" + Guid.NewGuid().ToString() + ".txt");

            // Act
            string result = Utils.GetExactPath(nonExistentPath);

            // Assert
            Assert.Equal(nonExistentPath, result);
        }

        [Fact]
        public void GetExactPath_NullInput_ReturnsNull()
        {
            // Act
            string result = Utils.GetExactPath(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void StringArrayContains_ArrayContainsValue_ReturnsTrue()
        {
            // Arrange
            string[] array = { "apple", "banana", "cherry" };
            string value = "banana";

            // Act
            bool result = Utils.StringArrayContains(array, value);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void StringArrayContains_ArrayDoesNotContainValue_ReturnsFalse()
        {
            // Arrange
            string[] array = { "apple", "banana", "cherry" };
            string value = "orange";

            // Act
            bool result = Utils.StringArrayContains(array, value);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void StringArrayContains_ArrayContainsValueIgnoreCase_ReturnsTrue()
        {
            // Arrange
            string[] array = { "Apple", "Banana", "Cherry" };
            string value = "APPLE";

            // Act
            bool result = Utils.StringArrayContains(array, value, StringComparison.OrdinalIgnoreCase);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void SplitFirstLine_MultiLineString_SplitsCorrectly()
        {
            // Arrange
            string input = "First line\r\nSecond line\r\nThird line";
            string expectedFirstLine = "First line";
            string expectedRest = "Second line\r\nThird line";

            // Act
            Utils.SplitFirstLine(input, out string actualFirstLine, out string actualRest);

            // Assert
            Assert.Equal(expectedFirstLine, actualFirstLine);
            Assert.Equal(expectedRest, actualRest);
        }

        [Fact]
        public void SplitFirstLine_SingleLineString_SplitsCorrectly()
        {
            // Arrange
            string input = "Single line";
            string expectedFirstLine = "Single line";
            string expectedRest = string.Empty;

            // Act
            Utils.SplitFirstLine(input, out string actualFirstLine, out string actualRest);

            // Assert
            Assert.Equal(expectedFirstLine, actualFirstLine);
            Assert.Equal(expectedRest, actualRest);
        }

        [Fact]
        public void CompressDecompressDeflate_RoundTrip_DataIntact()
        {
            // Arrange
            string tempDir = Path.GetTempPath();
            string inputFile = Path.Combine(tempDir, "compress_test_input_" + Guid.NewGuid().ToString() + ".txt");
            string outputFile = Path.Combine(tempDir, "compress_test_output_" + Guid.NewGuid().ToString() + ".txt");
            string testData = "This is test data that will be compressed and then decompressed to verify integrity.";
            
            try
            {
                // Write test data to input file
                File.WriteAllText(inputFile, testData);

                // Act - compress and then decompress
                string compressedFile = inputFile + ".compressed";
                Utils.CompressDeflate(inputFile, compressedFile);
                Utils.DecompressDeflate(compressedFile, outputFile);

                // Assert
                string result = File.ReadAllText(outputFile);
                Assert.Equal(testData, result);
                
                // Also verify that compressed file is smaller than original
                var originalInfo = new FileInfo(inputFile);
                var compressedInfo = new FileInfo(compressedFile);
                Assert.True(compressedInfo.Length < originalInfo.Length, "Compressed file should be smaller than original");
            }
            finally
            {
                // Clean up
                if (File.Exists(inputFile))
                    File.Delete(inputFile);
                if (File.Exists(inputFile + ".compressed"))
                    File.Delete(inputFile + ".compressed");
                if (File.Exists(outputFile))
                    File.Delete(outputFile);
            }
        }
    }
}