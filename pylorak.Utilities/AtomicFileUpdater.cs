using System.IO;

namespace pylorak.Utilities
{
    public sealed class AtomicFileUpdater(string targetFile) : Disposable
    {
        // File.Replace needs the target and temporary files to be on the same volume.
        // To ensure this, we create our temporary file in the same folder as our target.

        private static string RandomFileInSameDir(string file)
        {
            var targetDir = Path.GetDirectoryName(file)!;
            return Path.Combine(targetDir, Path.GetRandomFileName());
        }

        public string TemporaryFilePath { get; } = RandomFileInSameDir(targetFile);

        public string TargetFilePath { get; } = targetFile;

        public void Commit()
        {
            string backup = RandomFileInSameDir(TargetFilePath);
            try
            {
                if (File.Exists(TargetFilePath))
                    File.Replace(TemporaryFilePath, TargetFilePath, backup, true);
                else
                    File.Move(TemporaryFilePath, TargetFilePath);
            }
            finally
            {
                try
                {
                    File.Delete(backup);
                }
                catch
                {
                    // ignored
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            if (disposing)
            {
                try
                {
                    File.Delete(TemporaryFilePath);
                }
                catch
                {
                    // ignored
                }
            }

            base.Dispose(disposing);
        }
    }
}
