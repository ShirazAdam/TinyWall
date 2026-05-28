using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;

namespace ModernTinyWall.Windows
{
    public static partial class GlobalAtomTable
    {
        [SuppressUnmanagedCodeSecurity]
        private static partial class NativeMethods
        {
            public const int ERROR_SUCCESS = 0;
            public const int ERROR_FILE_NOT_FOUND = 2;
            public const int ERROR_INVALID_HANDLE = 6;
            public const int ERROR_INVALID_PARAMETER = 87;
            public const int MAX_ATOM_NAME_LENGTH = 256;

            [LibraryImport("kernel32", SetLastError = true)]
            public static partial void SetLastError(uint dwErrorCode);

            [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
            public static partial ushort GlobalAddAtom(string lpString);

            [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
            public static partial ushort GlobalFindAtom(string lpString);

            [LibraryImport("kernel32", SetLastError = true)]
            public static partial ushort GlobalDeleteAtom(ushort nAtom);

            [LibraryImport("kernel32", EntryPoint = "GlobalGetAtomNameW", SetLastError = true)]
            public static unsafe partial uint GlobalGetAtomName(ushort nAtom, char* lpBuffer, int nSize);
        }

        public class AtomNotFoundException()
            : Win32Exception(NativeMethods.ERROR_FILE_NOT_FOUND, "The atom name was not found.");
        public class InvalidAtomNameException()
            : Win32Exception(NativeMethods.ERROR_INVALID_PARAMETER, "Invalid atom name.");
        public class InvalidAtomHandleException()
            : Win32Exception(NativeMethods.ERROR_INVALID_HANDLE, "Invalid atom handle.");

        private static void TranslateWin32LastError()
        {
            int win32err = Marshal.GetLastWin32Error();
            if (NativeMethods.ERROR_SUCCESS != win32err)
            {
                throw win32err switch
                {
                    NativeMethods.ERROR_FILE_NOT_FOUND => new AtomNotFoundException(),
                    NativeMethods.ERROR_INVALID_HANDLE => new InvalidAtomHandleException(),
                    NativeMethods.ERROR_INVALID_PARAMETER => new InvalidAtomNameException(),
                    _ => new Win32Exception(),
                };
            }
        }

        public static ushort Add(string name)
        {
            var ret = NativeMethods.GlobalAddAtom(name);
            if (0 == ret)
                TranslateWin32LastError();
            return ret;
        }

        public static ushort Find(string name)
        {
            var ret = NativeMethods.GlobalFindAtom(name);
            if (0 == ret)
                TranslateWin32LastError();
            return ret;
        }

        public static bool Exists(string name)
        {
            try
            {
                return 0 != Find(name);
            }
            catch (AtomNotFoundException)
            {
                return false;
            }
        }

        public static void Delete(ushort atom)
        {
            NativeMethods.SetLastError(NativeMethods.ERROR_SUCCESS);
            NativeMethods.GlobalDeleteAtom(atom);
            TranslateWin32LastError();
        }

        public static void Delete(string name)
        {
            Delete(Find(name));
        }

        public static string GetName(ushort atom)
        {
            unsafe
            {
                var buffer = stackalloc char[NativeMethods.MAX_ATOM_NAME_LENGTH];
                var ret = NativeMethods.GlobalGetAtomName(atom, buffer, NativeMethods.MAX_ATOM_NAME_LENGTH);
                if (0 == ret)
                    TranslateWin32LastError();

                return new string(buffer, 0, (int)ret);
            }
        }

    }

}
