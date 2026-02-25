using System;
using System.Threading;
using System.Threading.Tasks;

namespace pylorak.TinyWall
{
    public sealed class Controller
    {
        private readonly PipeClientEndpoint _endpoint;

        public Controller(string serverEndpoint)
        {
            _endpoint = new PipeClientEndpoint(serverEndpoint);
        }

        #region Synchronous Methods (Legacy - prefer async versions)

        public MessageType GetServerConfig(out ServerConfiguration? serverConfig, out ServerState? serverState, ref Guid clientChangeset)
        {
            // Detect if server settings have changed in comparison to ours and download
            // settings only if we need them. Settings are "version numbered" using the "changeset"
            // property. We send our changeset number to the service and if it differs from his,
            // the service will send back the settings.

            serverConfig = null;
            serverState = null;

            var resp = _endpoint.QueueMessage(TwMessageGetSettings.CreateRequest(clientChangeset)).Response;

            if (resp.Type != MessageType.GET_SETTINGS) return resp.Type;

            var respArgs = (TwMessageGetSettings)resp;
            if (respArgs.Changeset == clientChangeset) return resp.Type;

            clientChangeset = respArgs.Changeset;
            serverConfig = respArgs.Config;
            serverState = respArgs.State;

            return resp.Type;
        }

        public TwMessage SetServerConfig(ServerConfiguration serverConfig, Guid clientChangeset)
        {
            return _endpoint.QueueMessage(TwMessagePutSettings.CreateRequest(clientChangeset, serverConfig)).Response;
        }

        public TwRequest BeginReadFwLog() => _endpoint.QueueMessage(TwMessageReadFwLog.CreateRequest());

        public static FirewallLogEntry[] EndReadFwLog(TwMessage twResp)
        {
            if (twResp is TwMessageReadFwLog fwLog)
                return fwLog.Entries;

            // TODO: Do we want to show an error to the user?
            return Array.Empty<FirewallLogEntry>();
        }

        public MessageType SwitchFirewallMode(FirewallMode mode) => _endpoint.QueueMessage(TwMessageModeSwitch.CreateRequest(mode)).Response.Type;

        public MessageType RequestServerStop() => _endpoint.QueueMessage(TwMessageSimple.CreateRequest(MessageType.STOP_SERVICE)).Response.Type;

        public bool IsServerLocked
        {
            get
            {
                var resp = _endpoint.QueueMessage(TwMessageIsLocked.CreateRequest()).Response;
                if (resp is TwMessageIsLocked isLockedResp)
                    return isLockedResp.LockedStatus;
                else
                    return false;
            }
        }

        public MessageType SetPassphrase(string pwd) => _endpoint.QueueMessage(TwMessageSetPassword.CreateRequest(pwd)).Response.Type;

        public MessageType TryUnlockServer(string pwd) => _endpoint.QueueMessage(TwMessageUnlock.CreateRequest(pwd)).Response.Type;

        public MessageType LockServer() => _endpoint.QueueMessage(TwMessageSimple.CreateRequest(MessageType.LOCK)).Response.Type;

        public string TryGetProcessPath(uint pid)
        {
            var resp = _endpoint.QueueMessage(TwMessageGetProcessPath.CreateRequest(pid)).Response;
            if (resp.Type == MessageType.GET_PROCESS_PATH)
            {
                var respArgs = (TwMessageGetProcessPath)resp;
                return respArgs.Path;
            }
            else
                return string.Empty;
        }

        #endregion

        #region Async Methods (Preferred - do not block UI thread)

        public async Task<(MessageType Type, ServerConfiguration? Config, ServerState? State, Guid Changeset)> GetServerConfigAsync(Guid clientChangeset, CancellationToken ct = default)
        {
            var resp = await _endpoint.QueueMessage(TwMessageGetSettings.CreateRequest(clientChangeset)).ResponseAsync.WaitAsync(ct);

            if (resp.Type == MessageType.GET_SETTINGS)
            {
                var respArgs = (TwMessageGetSettings)resp;
                if (respArgs.Changeset != clientChangeset)
                {
                    return (resp.Type, respArgs.Config, respArgs.State, respArgs.Changeset);
                }
            }

            return (resp.Type, null, null, clientChangeset);
        }

        public async Task<TwMessage> SetServerConfigAsync(ServerConfiguration serverConfig, Guid clientChangeset, CancellationToken ct = default)
        {
            return await _endpoint.QueueMessage(TwMessagePutSettings.CreateRequest(clientChangeset, serverConfig)).ResponseAsync.WaitAsync(ct);
        }

        public async Task<FirewallLogEntry[]> ReadFwLogAsync(CancellationToken ct = default)
        {
            var resp = await _endpoint.QueueMessage(TwMessageReadFwLog.CreateRequest()).ResponseAsync.WaitAsync(ct);
            return EndReadFwLog(resp);
        }
        
        public async Task<MessageType> SwitchFirewallModeAsync(FirewallMode mode, CancellationToken ct = default)
        {
            var resp = await _endpoint.QueueMessage(TwMessageModeSwitch.CreateRequest(mode)).ResponseAsync.WaitAsync(ct);
            return resp.Type;
        }

        public async Task<MessageType> RequestServerStopAsync(CancellationToken ct = default)
        {
            var resp = await _endpoint.QueueMessage(TwMessageSimple.CreateRequest(MessageType.STOP_SERVICE)).ResponseAsync.WaitAsync(ct);
            return resp.Type;
        }

        public async Task<bool> IsServerLockedAsync(CancellationToken ct = default)
        {
            var resp = await _endpoint.QueueMessage(TwMessageIsLocked.CreateRequest()).ResponseAsync.WaitAsync(ct);
            if (resp is TwMessageIsLocked isLockedResp)
                return isLockedResp.LockedStatus;
            else
                return false;
        }

        public async Task<MessageType> SetPassphraseAsync(string pwd, CancellationToken ct = default)
        {
            var resp = await _endpoint.QueueMessage(TwMessageSetPassword.CreateRequest(pwd)).ResponseAsync.WaitAsync(ct);
            return resp.Type;
        }

        public async Task<MessageType> TryUnlockServerAsync(string pwd, CancellationToken ct = default)
        {
            var resp = await _endpoint.QueueMessage(TwMessageUnlock.CreateRequest(pwd)).ResponseAsync.WaitAsync(ct);
            return resp.Type;
        }

        public async Task<MessageType> LockServerAsync(CancellationToken ct = default)
        {
            var resp = await _endpoint.QueueMessage(TwMessageSimple.CreateRequest(MessageType.LOCK)).ResponseAsync.WaitAsync(ct);
            return resp.Type;
        }

        public async Task<string> TryGetProcessPathAsync(uint pid, CancellationToken ct = default)
        {
            var resp = await _endpoint.QueueMessage(TwMessageGetProcessPath.CreateRequest(pid)).ResponseAsync.WaitAsync(ct);
            if (resp.Type == MessageType.GET_PROCESS_PATH)
            {
                var respArgs = (TwMessageGetProcessPath)resp;
                return respArgs.Path;
            }
            else
                return string.Empty;
        }

        #endregion
    }
}
