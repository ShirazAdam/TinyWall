using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;

// Standalone implementations for testing
namespace TinyWall.Tests
{
    public static class Hasher
    {
        public static string HashStream(Stream stream)
        {
            using var hasher = SHA256.Create();
            return Utils.HexEncode(hasher.ComputeHash(stream));
        }

        public static string HashFile(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            return HashStream(fs);
        }

        public static string HashString(string text)
        {
            using var hasher = SHA256.Create();
            return Utils.HexEncode(hasher.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        public static string HashFileSha1(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var hasher = SHA1.Create();
            return Utils.HexEncode(hasher.ComputeHash(fs));
        }
    }

    public static class Utils
    {
        public static string HexEncode(byte[] binstr)
        {
            var sb = new StringBuilder();

            foreach (byte oct in binstr)
                sb.Append(oct.ToString("X2"));

            return sb.ToString();
        }

        public static bool IsNullOrEmpty(string str)
        {
            return string.IsNullOrWhiteSpace(str);
        }

        public static string GetLongPathName(string shortPath)
        {
            return shortPath ?? string.Empty;
        }

        public static string RandomString(int length)
        {
            const string CHARS = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var buffer = new char[length];
            var rng = new Random();

            for (var i = 0; i < length; i++)
            {
                buffer[i] = CHARS[rng.Next(CHARS.Length)];
            }
            return new string(buffer);
        }

        public static bool StringArrayContains(string[] arr, string val, StringComparison opts = StringComparison.Ordinal)
        {
            foreach (var t in arr)
            {
                if (string.Equals(t, val, opts))
                    return true;
            }
            return false;
        }

        public static void SplitFirstLine(string str, out string firstLine, out string restLines)
        {
            var lines = str.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            firstLine = lines[0];
            restLines = string.Empty;

            if (lines.Length <= 1) 
            {
                return;
            }

            restLines = lines[1];
            for (var i = 2; i < lines.Length; ++i)
                restLines += Environment.NewLine + lines[i];
        }

        public static void CompressDeflate(string inputFile, string outputFile)
        {
            using var inFile = new FileStream(inputFile, FileMode.Open, FileAccess.Read);
            using var outFile = new FileStream(outputFile, FileMode.Create, FileAccess.Write);
            using var compressedOutFile = new System.IO.Compression.DeflateStream(outFile, System.IO.Compression.CompressionMode.Compress, true);

            var buffer = new byte[4096];
            int numRead;

            while ((numRead = inFile.Read(buffer, 0, buffer.Length)) != 0)
            {
                compressedOutFile.Write(buffer, 0, numRead);
            }
        }

        public static void DecompressDeflate(string inputFile, string outputFile)
        {
            using var outFile = new FileStream(outputFile, FileMode.Create, FileAccess.Write);
            using var inFile = new FileStream(inputFile, FileMode.Open, FileAccess.Read);
            using var decompressedInFile = new System.IO.Compression.DeflateStream(inFile, System.IO.Compression.CompressionMode.Decompress, true);

            var buffer = new byte[4096];
            int numRead;

            while ((numRead = decompressedInFile.Read(buffer, 0, buffer.Length)) != 0)
            {
                outFile.Write(buffer, 0, numRead);
            }
        }

        public static string GetExactPath(string path)
        {
            if (!(Directory.Exists(path) || File.Exists(path)))
                return path;

            if (path != null)
            {
                var dir = new DirectoryInfo(path);
                var parent = dir.Parent;    // will be null if there is no parent
                var result = string.Empty;

                while (parent != null)
                {
                    result = Path.Combine(GetOnlyFirst(parent.EnumerateFileSystemInfos(dir.Name)), result);

                    dir = parent;
                    parent = parent.Parent;
                }

                // Handle the root part (i.e., drive letter)
                var root = dir.FullName;

                if (!root.Contains(":")) return path;

                // Drive letter
                root = root.ToUpperInvariant();
                result = Path.Combine(root, result);
                return result;
            }

            return path;
        }

        private static string GetOnlyFirst<T>(System.Collections.Generic.IEnumerable<T> items)
        {
            var iter = items.GetEnumerator();

            iter.MoveNext();

            return iter.Current.ToString();
        }

        public static bool EqualsCaseInsensitive(string a, string b)
        {
            if (a == b)
                return true;

            return (a != null) && a.Equals(b, StringComparison.InvariantCultureIgnoreCase);
        }
    }

    public class RuleDef
    {
        public string Name { get; set; }
        public Guid ExceptionId { get; set; }
        public RuleAction Action { get; set; }
        public string AppContainerSid { get; set; }
        public string Application { get; set; }
        public string ServiceName { get; set; }
        public string LocalPorts { get; set; }
        public string RemotePorts { get; set; }
        public string LocalAddresses { get; set; }
        public string RemoteAddresses { get; set; }
        public string IcmpTypesAndCodes { get; set; }
        public Protocol Protocol { get; set; }
        public RuleDirection Direction { get; set; }
        public ulong Weight { get; set; }

        public RuleDef()
        { }

        public RuleDef ShallowCopy()
        {
            var copy = new RuleDef();
            copy.Name = this.Name;
            copy.ExceptionId = this.ExceptionId;
            copy.Action = this.Action;
            copy.Application = this.Application;
            copy.ServiceName = this.ServiceName;
            copy.AppContainerSid = this.AppContainerSid;
            copy.LocalPorts = this.LocalPorts;
            copy.RemotePorts = this.RemotePorts;
            copy.LocalAddresses = this.LocalAddresses;
            copy.RemoteAddresses = this.RemoteAddresses;
            copy.IcmpTypesAndCodes = this.IcmpTypesAndCodes;
            copy.Protocol = this.Protocol;
            copy.Direction = this.Direction;
            copy.Weight = this.Weight;
            return copy;
        }

        public void SetSubject(ExceptionSubject subject)
        {
            if (subject == null) return;

            switch (subject)
            {
                case ServiceSubject service:
                    Application = service.ExecutablePath;
                    ServiceName = service.ServiceName;
                    AppContainerSid = null;
                    break;
                case ExecutableSubject exe:
                    Application = exe.ExecutablePath;
                    ServiceName = null;
                    AppContainerSid = null;
                    break;
                case AppContainerSubject uwp:
                    Application = null;
                    ServiceName = null;
                    AppContainerSid = uwp.Sid;
                    break;
                case GlobalSubject _:
                    Application = null;
                    ServiceName = null;
                    AppContainerSid = null;
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public RuleDef(Guid exceptionId, string name, ExceptionSubject subject, RuleAction action, RuleDirection direction, Protocol protocol, ulong weight)
        {
            SetSubject(subject);
            Name = name;
            ExceptionId = exceptionId;
            Action = action;
            Direction = direction;
            Protocol = protocol;
            Weight = weight;
        }
    }


}
    // Supporting classes for testing
    internal class ExecutableSubject : ExceptionSubject
    {
        public string ExecutablePath { get; }
        
        public ExecutableSubject(string executablePath)
        {
            ExecutablePath = executablePath;
        }
        
        public override string DisplayName => ExecutablePath;
    }
    
    internal class ServiceSubject : ExceptionSubject
    {
        public string ExecutablePath { get; }
        public string ServiceName { get; }
        
        public ServiceSubject(string executablePath, string serviceName)
        {
            ExecutablePath = executablePath;
            ServiceName = serviceName;
        }
        
        public override string DisplayName => ServiceName;
    }
    
    internal class AppContainerSubject : ExceptionSubject
    {
        public string Sid { get; }
        
        public AppContainerSubject(string sid)
        {
            Sid = sid;
        }
        
        public override string DisplayName => Sid;
    }
    
    internal class GlobalSubject : ExceptionSubject
    {
        public override string DisplayName => "All Applications";
    }
    
    internal abstract class ExceptionSubject
    {
        public abstract string DisplayName { get; }
    }
    
    // Enums needed for testing
    internal enum RuleAction { Allow, Block }
    internal enum RuleDirection { Inbound, Outbound }
    internal enum Protocol { Tcp, Udp, Any }
}
