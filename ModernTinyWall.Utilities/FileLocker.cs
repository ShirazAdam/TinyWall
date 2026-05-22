using System.Collections.Generic;
using System.IO;

namespace ModernTinyWall.Utilities
{
    public sealed class FileLocker : Disposable
    {
        private readonly Dictionary<string, FileStream> _lockedFiles = new();

        public bool Lock(string filePath, FileAccess localAccess, FileShare shareMode)
        {
            if (IsLocked(filePath))
                return false;

            try
            {
                _lockedFiles.Add(filePath, new FileStream(filePath, FileMode.OpenOrCreate, localAccess, shareMode));
                return true;
            }
            catch
            {
                return false;
            }
        }

        public FileStream GetStream(string filePath)
        {
            return _lockedFiles[filePath];
        }

        public bool IsLocked(string filePath)
        {
            return _lockedFiles.ContainsKey(filePath);
        }

        public bool Unlock(string filePath)
        {
            if (!IsLocked(filePath))
                return false;

            try
            {
                _lockedFiles[filePath].Close();
                _lockedFiles.Remove(filePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void UnlockAll()
        {
            foreach (var stream in _lockedFiles.Values)
            {
                try { stream.Close(); }
                catch
                {
                    // ignored
                }
            }

            _lockedFiles.Clear();
        }

        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            if (disposing)
                UnlockAll();

            base.Dispose(disposing);
        }
    }
}
