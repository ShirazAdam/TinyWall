using pylorak.Utilities;
using System;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading;

namespace pylorak.TinyWall
{
    internal delegate TwMessage PipeDataReceived(TwMessage req);

    internal class PipeServerEndpoint : Disposable
    {
        private readonly Thread _mPipeWorkerThread;
        private readonly PipeDataReceived _mRcvCallback;
        private readonly string _mPipeName;

        private bool _mRun = true;

        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            _mRun = false;

            // Create a dummy connection so that worker thread gets out of the infinite WaitForConnection()
            using (var npcs = new NamedPipeClientStream(_mPipeName))
            {
                npcs.Connect(500);
            }

            if (disposing)
            {
                // Release managed resources
                _mPipeWorkerThread.Join(TimeSpan.FromMilliseconds(1000));
            }

            // Release unmanaged resources.
            // Set large fields to null.
            // Call Dispose on your base class.
            base.Dispose(disposing);
        }

        internal PipeServerEndpoint(PipeDataReceived recvCallback, string serverPipeName)
        {
            _mRcvCallback = recvCallback;
            _mPipeName = serverPipeName;

            _mPipeWorkerThread = new Thread(PipeServerWorker)
            {
                Name = "ServerPipeWorker",
                IsBackground = true
            };
            _mPipeWorkerThread.Start();
        }

        private void PipeServerWorker()
        {
            // Allow authenticated users access to the pipe
            SecurityIdentifier authenticatedSid = new(WellKnownSidType.AuthenticatedUserSid, null);
            PipeAccessRule par = new(authenticatedSid, PipeAccessRights.ReadWrite, System.Security.AccessControl.AccessControlType.Allow);
            PipeSecurity ps = new();
            ps.AddAccessRule(par);

            while (_mRun)
            {
                try
                {
                    // Create pipe server
                    using var pipeServer = new NamedPipeServerStream(_mPipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.WriteThrough, 2048 * 10, 2048 * 10, ps);
                    if (!pipeServer.IsConnected)
                    {
                        pipeServer.WaitForConnection();
                        pipeServer.ReadMode = PipeTransmissionMode.Message;

                        if (!AuthAsServer(pipeServer))
                            throw new InvalidOperationException("Client authentication failed.");
                    }

                    var req = SerialisationHelper.DeserialiseFromPipe<TwMessage>(pipeServer, 3000, TwMessageComError.Instance);
                    var resp = _mRcvCallback(req);
                    SerialisationHelper.SerialiseToPipe(pipeServer, resp);
                }
                catch
                {
                    Thread.Sleep(200);
                }
            } //while
        }

        private static bool AuthAsServer(PipeStream stream)
        {
#if !DEBUG
            if (!Utils.SafeNativeMethods.GetNamedPipeClientProcessId(stream.SafePipeHandle.DangerousGetHandle(), out ulong clientPid))
                return false;

            string clientFilePath = Utils.GetPathOfProcess((uint)clientPid);

            return clientFilePath.Equals(pylorak.Windows.ProcessManager.ExecutablePath, StringComparison.OrdinalIgnoreCase);
#else
            return true;
#endif
        }
    }
}
