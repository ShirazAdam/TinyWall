using NeoSmart.AsyncLock;
using System;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace pylorak.TinyWall
{
    public class PipeClientEndpoint
    {
        private readonly AsyncLock _asyncLock = new();
        private readonly string _mPipeName;

        public PipeClientEndpoint(string clientPipeName)
        {
            _mPipeName = clientPipeName;
        }

        private void SendRequest(TwRequest req)
        {
            TwMessage ret = TwMessageComError.Instance;

            Task.Run(async () =>
            {
                using (await _asyncLock.LockAsync())
                {
                    for (var i = 0; i < 2; ++i)
                    {
                        var resp = SendRequest(req.Request);
                        if (resp.Type != MessageType.COM_ERROR)
                        {
                            ret = resp;
                            break;
                        }
                        await Task.Delay(TimeSpan.FromMilliseconds(200));
                    }
                }
                req.Response = ret;
            });
        }

        private TwMessage SendRequest(TwMessage msg)
        {
            try
            {
                using var pipeClient = new NamedPipeClientStream(".", _mPipeName, PipeDirection.InOut, PipeOptions.WriteThrough);
                pipeClient.Connect(1000);
                pipeClient.ReadMode = PipeTransmissionMode.Message;

                // Send command
                SerialisationHelper.SerialiseToPipe(pipeClient, msg);

                // Get response
                return SerialisationHelper.DeserialiseFromPipe<TwMessage>(pipeClient, 20000, TwMessageComError.Instance);
            }
            catch
            {
                return TwMessageComError.Instance;
            }
        }

        public TwRequest QueueMessage(TwMessage msg)
        {
            var req = new TwRequest(msg);
            SendRequest(req);
            return req;
        }
    }
}
