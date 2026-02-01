using System.IO;
using System.Security.Cryptography;
using System.Text;
using ImmenseWall.Services;

namespace ImmenseWall.Services
{
    public static class Hasher
    {
        public static string HashStream(Stream stream)
        {
            using (var hasher = System.Security.Cryptography.SHA256.Create())
            {
                return Utils.HexEncode(hasher.ComputeHash(stream));
            }
        }

        public static string HashFile(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                return HashStream(fs);
            }
        }

        public static string HashString(string text)
        {
            using (var hasher = System.Security.Cryptography.SHA256.Create())
            {
                return Utils.HexEncode(hasher.ComputeHash(Encoding.UTF8.GetBytes(text)));
            }
        }

        public static string HashFileSha1(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var hasher = System.Security.Cryptography.SHA1.Create())
            {
                return Utils.HexEncode(hasher.ComputeHash(fs));
            }
        }

        public static string ComputeSHA256(string filePath)
        {
            return HashFile(filePath);
        }

        public static string ComputeSHA1(string filePath)
        {
            return HashFileSha1(filePath);
        }
    }
}
