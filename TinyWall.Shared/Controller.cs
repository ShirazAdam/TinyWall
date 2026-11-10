using System;

namespace ModernTinyWall.TinyWall
{
    public sealed class Controller
    {
        private readonly PipeClientEndpoint Endpoint;

        public Controller(string serverEndpoint)
        {
            Endpoint = new PipeClientEndpoint(serverEndpoint);
        }

        public MessageType GetServerConfig(out ServerConfiguration? serverConfig, out ServerState? serverState, ref Guid clientChangeset)
        {
            // Detect if server settings have changed in comparison to ours and download
            // settings only if we need them. Settings are "version numbered" using the "changeset"
            // property. We send our changeset number to the service and if it differs from his,
            // the service will send back the settings.

            serverConfig = null;
            serverState = null;

            var resp = Endpoint.QueueMessage(TwMessageGetSettings.CreateRequest(clientChangeset)).Response;

            if (resp.Type == MessageType.GET_SETTINGS)
            {
                var respArgs = (TwMessageGetSettings)resp;
                if (respArgs.Changeset != clientChangeset)
                {
                    clientChangeset = respArgs.Changeset;
                    serverConfig = respArgs.Config;
                    serverState = respArgs.State;
                }
            }

            return resp.Type;
        }

        public TwMessage SetServerConfig(ServerConfiguration serverConfig, Guid clientChangeset)
        {
            return Endpoint.QueueMessage(TwMessagePutSettings.CreateRequest(clientChangeset, serverConfig)).Response;
        }

        public BlockedTrafficSnapshot ReadBlockedTraffic()
        {
            var response = Endpoint.QueueMessage(new TwMessageBlockedTraffic(null)).Response;
            return response is TwMessageBlockedTraffic { Snapshot: not null } message
                ? message.Snapshot
                : new(DateOnly.FromDateTime(DateTime.Now), 0, null,
                    $"Blocked traffic is unavailable (service response: {response.Type}). Install and run the matching service.", null);
        }

        public TwRequest BeginReadFwLog()
        {
            return Endpoint.QueueMessage(TwMessageReadFwLog.CreateRequest());
        }

        public TwMessage ReadPendingConnections()
        {
            return Endpoint.QueueMessage(new TwMessagePendingConnections(MessageType.READ_PENDING_CONNECTIONS, 0, false, false, [])).Response;
        }

        public TwMessage ResolvePendingConnection(ulong connectionId, bool allow)
        {
            return Endpoint.QueueMessage(new TwMessagePendingConnections(MessageType.RESOLVE_PENDING_CONNECTION, connectionId, allow, false, [])).Response;
        }

        public DataUsageSnapshot ReadDataUsage(DataUsagePeriod period)
        {
            var response = Endpoint.QueueMessage(new TwMessageDataUsage(period, null)).Response;
            if (response is TwMessageDataUsage { Snapshot: not null } usage)
                return usage.Snapshot;

            throw new InvalidOperationException(
                $"Data usage is unavailable (service response: {response.Type}). Ensure the updated TinyWall service is installed and running.");
        }

        public static FirewallLogEntry[] EndReadFwLog(TwMessage twResp)
        {
            if (twResp is TwMessageReadFwLog fwLog)
                return fwLog.Entries;
            else
                // TODO: Do we want to show an error to the user?
                return [];
        }

        public MessageType SwitchFirewallMode(FirewallMode mode)
        {
            return Endpoint.QueueMessage(TwMessageModeSwitch.CreateRequest(mode)).Response.Type;
        }

        public MessageType RequestServerStop()
        {
            return Endpoint.QueueMessage(TwMessageSimple.CreateRequest(MessageType.STOP_SERVICE)).Response.Type;
        }

        public bool IsServerLocked
        {
            get
            {
                var resp = Endpoint.QueueMessage(TwMessageIsLocked.CreateRequest()).Response;
                if (resp is TwMessageIsLocked isLockedResp)
                    return isLockedResp.LockedStatus;
                else
                    return false;
            }
        }

        public MessageType SetPassphrase(string pwd)
        {
            return Endpoint.QueueMessage(TwMessageSetPassword.CreateRequest(pwd)).Response.Type;
        }

        public MessageType TryUnlockServer(string pwd)
        {
            return Endpoint.QueueMessage(TwMessageUnlock.CreateRequest(pwd)).Response.Type;
        }

        public MessageType LockServer()
        {
            return Endpoint.QueueMessage(TwMessageSimple.CreateRequest(MessageType.LOCK)).Response.Type;
        }

        public string TryGetProcessPath(uint pid)
        {
            var resp = Endpoint.QueueMessage(TwMessageGetProcessPath.CreateRequest(pid)).Response;
            if (resp.Type == MessageType.GET_PROCESS_PATH)
            {
                var respArgs = (TwMessageGetProcessPath)resp;
                return respArgs.Path;
            }
            else
                return string.Empty;
        }
    }
}
