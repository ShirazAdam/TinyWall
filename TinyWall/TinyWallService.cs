using pylorak.Utilities;
using pylorak.Windows;
using pylorak.Windows.Services;
using pylorak.Windows.WFP;
using pylorak.Windows.WFP.Interop;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace pylorak.TinyWall
{
    public sealed class TinyWallServer : IDisposable
    {
        private enum FilterWeights : ulong
        {
            Blocklist = 9000000,
            RawSocketPermit = 8000000,
            RawSocketBlock = 7000000,
            UserBlock = 6000000,
            UserPermit = 5000000,
            DefaultPermit = 4000000,
            DefaultBlock = 3000000,
        }

        private static readonly Guid TinywallProviderKey = new("{66CA412C-4453-4F1E-A973-C16E433E34D0}");

        private readonly BlockingCollection<TwRequest> _q = new(32);
        private readonly PipeServerEndpoint _serverPipe;
        private readonly Timer _minuteTimer;

        private readonly CircularBuffer<FirewallLogEntry> _firewallLogEntries = new(500);
        private readonly FileLocker _fileLocker = new();
        private readonly HostsFileManager _hostsFileManager = new();
        private DateTime _lastControllerCommandTime = DateTime.Now;
        private DateTime _lastRuleReloadTime = DateTime.Now;

        // Context needed for learning mode
        private readonly FirewallLogWatcher _logWatcher = new();
        private readonly List<FirewallExceptionV3> _learningNewExceptions = new();

        // Context for auto rule inheritance
        private readonly object _inheritanceGuard = new();
        private readonly HashSet<string> _userSubjectExes = new(StringComparer.OrdinalIgnoreCase);        // All executables with pre-configured rules.
        private readonly Dictionary<string, List<FirewallExceptionV3>> _childInheritance = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _childInheritedSubjectExes = new(StringComparer.OrdinalIgnoreCase);   // Executables that have been already auto-whitelisted due to inheritance
        private readonly ThreadThrottler _firewallThreadThrottler = new(Thread.CurrentThread, ThreadPriority.Highest);
        private StringBuilder? _processStartWatcherSbuilder;

        private bool _runService;
        private bool _displayCurrentlyOn = true;
        private readonly ServerState _visibleState = new();

        private readonly Engine _wfpEngine = new("TinyWall Session", "", FWPM_SESSION_FLAGS.None, 5000);
        private readonly ManagementEventWatcher _processStartWatcher = new(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
        private readonly EventMerger _ruleReloadEventMerger = new(1000);

        private HashSet<IpAddrMask> _localSubnetAddreses = new();
        private HashSet<IpAddrMask> _gatewayAddresses = new();
        private HashSet<IpAddrMask> _dnsAddresses = new();
        private readonly FilterConditionList _localSubnetFilterConditions = new();
        private readonly FilterConditionList _gatewayFilterConditions = new();
        private readonly FilterConditionList _dnsFilterConditions = new();

        private List<RuleDef> AssembleActiveRules(List<RuleDef>? rawSocketExceptions)
        {
            using var timer = new HierarchicalStopwatch("AssembleActiveRules()");
            var rules = new List<RuleDef>();
            var modeId = Guid.NewGuid();

            // Do we want to let local traffic through?
            if (ActiveConfig.Service.ActiveProfile.AllowLocalSubnet)
            {
                var def = new RuleDef(modeId, "Allow local subnet", GlobalSubject.Instance, RuleAction.Allow, RuleDirection.InOut, Protocol.Any, (ulong)FilterWeights.DefaultPermit)
                {
                    RemoteAddresses = RuleDef.LOCALSUBNET_ID
                };
                rules.Add(def);
            }

            // Do we want to block known malware ports?
            if (ActiveConfig.Service.Blocklists.EnableBlocklists && ActiveConfig.Service.Blocklists.EnablePortBlocklist)
            {
                var exceptions = new List<FirewallExceptionV3>();
                exceptions.AddRange(CollectExceptionsForAppByName("Malware Ports"));
                foreach (var ex in exceptions)
                {
                    ex.RegenerateId();
                    GetRulesForException(ex, rules, rawSocketExceptions, (ulong)FilterWeights.DefaultPermit, (ulong)FilterWeights.Blocklist);
                }
            }

            // Rules specific to the selected firewall mode
            var needUserRules = true;
            switch (_visibleState.Mode)
            {
                case FirewallMode.AllowOutgoing:
                    {
                        // Block everything
                        var def = new RuleDef(modeId, "Block everything", GlobalSubject.Instance, RuleAction.Block, RuleDirection.InOut, Protocol.Any, (ulong)FilterWeights.DefaultBlock);
                        rules.Add(def);

                        // Allow outgoing
                        def = new RuleDef(modeId, "Allow outbound", GlobalSubject.Instance, RuleAction.Allow, RuleDirection.Out, Protocol.Any, (ulong)FilterWeights.DefaultPermit);
                        rules.Add(def);
                        break;
                    }
                case FirewallMode.BlockAll:
                    {
                        // We won't need application exceptions
                        needUserRules = false;

                        // Block all
                        var def = new RuleDef(modeId, "Block everything", GlobalSubject.Instance, RuleAction.Block, RuleDirection.InOut, Protocol.Any, (ulong)FilterWeights.DefaultBlock);
                        rules.Add(def);
                        break;
                    }
                case FirewallMode.Learning:
                    {
                        // Add rule to explicitly allow everything
                        var def = new RuleDef(modeId, "Allow everything", GlobalSubject.Instance, RuleAction.Allow, RuleDirection.InOut, Protocol.Any, (ulong)FilterWeights.DefaultPermit);
                        rules.Add(def);
                        break;
                    }
                case FirewallMode.Disabled:
                    {
                        // We won't need application exceptions
                        needUserRules = false;

                        // Add rule to explicitly allow everything
                        var def = new RuleDef(modeId, "Allow everything", GlobalSubject.Instance, RuleAction.Allow, RuleDirection.InOut, Protocol.Any, (ulong)FilterWeights.DefaultPermit);
                        rules.Add(def);
                        break;
                    }
                case FirewallMode.Normal:
                    {
                        // Block all by default
                        var def = new RuleDef(modeId, "Block everything", GlobalSubject.Instance, RuleAction.Block, RuleDirection.InOut, Protocol.Any, (ulong)FilterWeights.DefaultBlock);
                        rules.Add(def);
                        break;
                    }
                case FirewallMode.Unknown:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (needUserRules)
            {
                // Initialize the collection with our own binary
                var userExceptions = new List<FirewallExceptionV3>
                {
                    new(
                        new ExecutableSubject(ProcessManager.ExecutablePath),
                        new TcpUdpPolicy()
                        {
                            AllowedRemoteTcpConnectPorts = "443"
                        }
                    )
                };

                // Collect all applications exceptions
                userExceptions.AddRange(ActiveConfig.Service.ActiveProfile.AppExceptions);

                // Collect all special exceptions
                ActiveConfig.Service.ActiveProfile.SpecialExceptions.Remove("TinyWall");    // TODO: Deprecated: Needed due to old configs. Remove in future version.
                foreach (string appName in ActiveConfig.Service.ActiveProfile.SpecialExceptions)
                    userExceptions.AddRange(CollectExceptionsForAppByName(appName));

                // Convert exceptions to rules
                foreach (var ex in userExceptions)
                {
                    if (ex.Subject is ExecutableSubject exe)
                    {
                        var exePath = exe.ExecutablePath;
                        _userSubjectExes.Add(exePath);
                        if (ex.ChildProcessesInherit)
                        {
                            // We might have multiple rules with the same exePath, so we maintain a list of exceptions
                            if (!_childInheritance.ContainsKey(exePath))
                                _childInheritance.Add(exePath, new List<FirewallExceptionV3>());
                            _childInheritance[exePath].Add(ex);
                        }
                    }

                    GetRulesForException(ex, rules, rawSocketExceptions, (ulong)FilterWeights.UserPermit, (ulong)FilterWeights.UserBlock);
                }

                if (_childInheritance.Count != 0)
                {
                    timer.NewSubTask("Rule inheritance processing");

                    var procTree = ProcessManager.CreateToolhelp32SnapshotExtended().ToDictionary(p => p.ProcessId);

                    // This list will hold parents that we already checked for a process.
                    // Used to avoid inf. loop when parent-PID info is unreliable.
                    var pidsChecked = new HashSet<uint>();

                    foreach (var pair in procTree)
                    {
                        pidsChecked.Clear();

                        var procPath = pair.Value.ImagePath;

                        // Skip if we have no path
                        if (string.IsNullOrEmpty(procPath))
                            continue;

                        // Skip if we have a user-defined rule for this path
                        if (_userSubjectExes.Contains(procPath))
                            continue;

                        // Start walking up the process tree
                        for (var parentEntry = procTree[pair.Key]; ;)
                        {
                            var childCreationTime = parentEntry.CreationTime;
                            if (procTree.TryGetValue(parentEntry.ParentProcessId, out var parentProcessId))
                                parentEntry = parentProcessId;
                            else
                                // We reached top of process tree (with non-existing parent)
                                break;

                            // Check if what we have is really the parent, or just a reused PID
                            if (parentEntry.CreationTime > childCreationTime)
                                // We reached the top of the process tree (with non-existing parent)
                                break;

                            if (parentEntry.ProcessId == 0)
                                // We reached top of process tree (with idle process)
                                break;

                            if (!pidsChecked.Add(parentEntry.ProcessId))
                                // We've been here before, damn it. Avoid looping eternally...
                                break;

                            pidsChecked.Add(parentEntry.ProcessId);

                            if (string.IsNullOrEmpty(parentEntry.ImagePath))
                                // We cannot get the path, so let's skip this parent
                                continue;

                            if (_childInheritedSubjectExes.ContainsKey(procPath) && _childInheritedSubjectExes[procPath].Contains(parentEntry.ImagePath))
                                // We have already processed this parent-child combination
                                break;

                            if (_childInheritance.TryGetValue(parentEntry.ImagePath, out List<FirewallExceptionV3> exList))
                            {
                                var subj = new ExecutableSubject(procPath);
                                foreach (var userEx in exList)
                                    GetRulesForException(new FirewallExceptionV3(subj, userEx.Policy), rules, rawSocketExceptions, (ulong)FilterWeights.UserPermit, (ulong)FilterWeights.UserBlock);

                                if (!_childInheritedSubjectExes.ContainsKey(procPath))
                                    _childInheritedSubjectExes.Add(procPath, new HashSet<string>());
                                _childInheritedSubjectExes[procPath].Add(parentEntry.ImagePath);
                                break;
                            }
                        }
                    }
                }   // if (ChildInheritance ...
            }

            // Convert all paths to kernel-format
            foreach (var r in rules)
            {
                if (r.Application is not null)
                    r.Application = PathMapper.Instance.ConvertPathIgnoreErrors(r.Application, PathFormat.NativeNt);
            }

            bool displayBlockActive = ActiveConfig.Service.ActiveProfile.DisplayOffBlock && !_displayCurrentlyOn;
            if (displayBlockActive)
            {
                // Modify all allow-rules to only allow local subnet
                foreach (var r in rules.Where(r => r.Action == RuleAction.Allow))
                {
                    r.RemoteAddresses = RuleDef.LOCALSUBNET_ID;
                }
            }

            return rules;
        }

        private void InstallRules(List<RuleDef> rules, List<RuleDef>? rawSocketExceptions, bool useTransaction)
        {
            var trx = useTransaction ? _wfpEngine.BeginTransaction() : null;
            try
            {
                // Add new rules
                foreach (RuleDef r in rules)
                {
                    try
                    {
                        ConstructFilter(r);
                    }
                    catch
                    {
                        // ignored
                    }
                }

                // Built-in protections
                if (_visibleState.Mode != FirewallMode.Disabled)
                {
                    InstallRawSocketPermits(rawSocketExceptions);
                    InstallWsl2Filters(ActiveConfig.Service.ActiveProfile.HasSpecialException("WSL_2"));
                }

                trx?.Commit();
            }
            finally
            {
                trx?.Dispose();
            }

        }

        private void InstallFirewallRules()
        {
            using var timer = new HierarchicalStopwatch("InstallFirewallRules()");
            _lastRuleReloadTime = DateTime.Now;
            PathMapper.Instance.RebuildCache();

            var rules = new List<RuleDef>();
            var rawSocketExceptions = new List<RuleDef>();
            lock (_inheritanceGuard)
            {
                _userSubjectExes.Clear();
                _childInheritance.Clear();
                _childInheritedSubjectExes.Clear();
                rules.AddRange(AssembleActiveRules(rawSocketExceptions));

                try
                {
                    if (_childInheritance.Count > 0)
                        _processStartWatcher.Start();
                    else
                        _processStartWatcher.Stop();
                }
                catch
                {
                    // TODO: Add nonce-flag and log only if it has not been logged already
                    // Utils.Log("WMI error. Subprocess monitoring will be disabled.", Utils.LOG_ID_SERVICE);
                }
            }

            timer.NewSubTask("WFP transaction acquire");
            using Transaction trx = _wfpEngine.BeginTransaction();
            timer.NewSubTask("WFP preparation");
            // Remove all existing WFP objects
            DeleteWfpObjects(_wfpEngine, true);

            // Install provider
            var provider = new FWPM_PROVIDER0();
            provider.displayData.name = "Karoly Pados";
            provider.displayData.description = "TinyWall Provider";
            provider.serviceName = TinyWallService.NameOfService;
            provider.flags = FWPM_PROVIDER_FLAGS.FWPM_PROVIDER_FLAG_PERSISTENT;
            provider.providerKey = TinywallProviderKey;
            var providerKey = _wfpEngine.RegisterProvider(ref provider);
            Debug.Assert(TinywallProviderKey == providerKey);

            // Install sublayers
            var layerKeys = (LayerKeyEnum[])Enum.GetValues(typeof(LayerKeyEnum));
            foreach (var layer in layerKeys)
            {
                var slKey = GetSublayerKey(layer);
                var wfpSublayer = new Sublayer($"TinyWall Sublayer for {layer}")
                {
                    Description = string.Empty,
                    SublayerKey = Guid.NewGuid(),
                    Flags = FWPM_SUBLAYER_FLAGS.FWPM_SUBLAYER_FLAG_PERSISTENT,
                    ProviderKey = null,
                    Weight = 0
                };
                wfpSublayer.Weight = ushort.MaxValue >> 4;
                wfpSublayer.SublayerKey = slKey;
                wfpSublayer.ProviderKey = TinywallProviderKey;
                wfpSublayer.Flags = FWPM_SUBLAYER_FLAGS.FWPM_SUBLAYER_FLAG_PERSISTENT;
                _wfpEngine.RegisterSublayer(wfpSublayer);
            }

            // Add standard protections
            if (_visibleState.Mode != FirewallMode.Disabled)
            {
                InstallPortScanProtection();
                InstallRawSocketBlocks();
            }

            timer.NewSubTask("Installing rules");
            InstallRules(rules, rawSocketExceptions, false);

            timer.NewSubTask("WFP transaction commit");
            trx.Commit();
        }

        private enum LayerKeyEnum
        {
            FWPM_LAYER_OUTBOUND_ICMP_ERROR_V6,
            FWPM_LAYER_OUTBOUND_ICMP_ERROR_V4,
            FWPM_LAYER_INBOUND_ICMP_ERROR_V6,
            FWPM_LAYER_INBOUND_ICMP_ERROR_V4,
            FWPM_LAYER_ALE_AUTH_CONNECT_V6,
            FWPM_LAYER_ALE_AUTH_CONNECT_V4,
            FWPM_LAYER_ALE_AUTH_LISTEN_V6,
            FWPM_LAYER_ALE_AUTH_LISTEN_V4,
            FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V6,
            FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V4,
            FWPM_LAYER_INBOUND_TRANSPORT_V6_DISCARD,
            FWPM_LAYER_INBOUND_TRANSPORT_V4_DISCARD,
            FWPM_LAYER_ALE_RESOURCE_ASSIGNMENT_V6,
            FWPM_LAYER_ALE_RESOURCE_ASSIGNMENT_V4,
        }

        private static Guid GetSublayerKey(LayerKeyEnum layer)
        {
            return layer switch
            {
                LayerKeyEnum.FWPM_LAYER_OUTBOUND_ICMP_ERROR_V6 => WfpSublayerKeys.FWPM_LAYER_OUTBOUND_ICMP_ERROR_V6,
                LayerKeyEnum.FWPM_LAYER_OUTBOUND_ICMP_ERROR_V4 => WfpSublayerKeys.FWPM_LAYER_OUTBOUND_ICMP_ERROR_V4,
                LayerKeyEnum.FWPM_LAYER_INBOUND_ICMP_ERROR_V6 => WfpSublayerKeys.FWPM_LAYER_INBOUND_ICMP_ERROR_V6,
                LayerKeyEnum.FWPM_LAYER_INBOUND_ICMP_ERROR_V4 => WfpSublayerKeys.FWPM_LAYER_INBOUND_ICMP_ERROR_V4,
                LayerKeyEnum.FWPM_LAYER_ALE_AUTH_CONNECT_V6 => WfpSublayerKeys.FWPM_LAYER_ALE_AUTH_CONNECT_V6,
                LayerKeyEnum.FWPM_LAYER_ALE_AUTH_CONNECT_V4 => WfpSublayerKeys.FWPM_LAYER_ALE_AUTH_CONNECT_V4,
                LayerKeyEnum.FWPM_LAYER_ALE_AUTH_LISTEN_V6 => WfpSublayerKeys.FWPM_LAYER_ALE_AUTH_LISTEN_V6,
                LayerKeyEnum.FWPM_LAYER_ALE_AUTH_LISTEN_V4 => WfpSublayerKeys.FWPM_LAYER_ALE_AUTH_LISTEN_V4,
                LayerKeyEnum.FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V6 => WfpSublayerKeys.FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V6,
                LayerKeyEnum.FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V4 => WfpSublayerKeys.FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V4,
                LayerKeyEnum.FWPM_LAYER_INBOUND_TRANSPORT_V6_DISCARD => WfpSublayerKeys.FWPM_LAYER_INBOUND_TRANSPORT_V6_DISCARD,
                LayerKeyEnum.FWPM_LAYER_INBOUND_TRANSPORT_V4_DISCARD => WfpSublayerKeys.FWPM_LAYER_INBOUND_TRANSPORT_V4_DISCARD,
                LayerKeyEnum.FWPM_LAYER_ALE_RESOURCE_ASSIGNMENT_V6 => WfpSublayerKeys.FWPM_LAYER_ALE_RESOURCE_ASSIGNMENT_V6,
                LayerKeyEnum.FWPM_LAYER_ALE_RESOURCE_ASSIGNMENT_V4 => WfpSublayerKeys.FWPM_LAYER_ALE_RESOURCE_ASSIGNMENT_V4,
                _ => throw new ArgumentException("Invalid or not support layerEnum."),
            };
        }

        private static Guid GetLayerKey(LayerKeyEnum layer)
        {
            return layer switch
            {
                LayerKeyEnum.FWPM_LAYER_OUTBOUND_ICMP_ERROR_V6 => LayerKeys.FWPM_LAYER_OUTBOUND_ICMP_ERROR_V6,
                LayerKeyEnum.FWPM_LAYER_OUTBOUND_ICMP_ERROR_V4 => LayerKeys.FWPM_LAYER_OUTBOUND_ICMP_ERROR_V4,
                LayerKeyEnum.FWPM_LAYER_INBOUND_ICMP_ERROR_V6 => LayerKeys.FWPM_LAYER_INBOUND_ICMP_ERROR_V6,
                LayerKeyEnum.FWPM_LAYER_INBOUND_ICMP_ERROR_V4 => LayerKeys.FWPM_LAYER_INBOUND_ICMP_ERROR_V4,
                LayerKeyEnum.FWPM_LAYER_ALE_AUTH_CONNECT_V6 => LayerKeys.FWPM_LAYER_ALE_AUTH_CONNECT_V6,
                LayerKeyEnum.FWPM_LAYER_ALE_AUTH_CONNECT_V4 => LayerKeys.FWPM_LAYER_ALE_AUTH_CONNECT_V4,
                LayerKeyEnum.FWPM_LAYER_ALE_AUTH_LISTEN_V6 => LayerKeys.FWPM_LAYER_ALE_AUTH_LISTEN_V6,
                LayerKeyEnum.FWPM_LAYER_ALE_AUTH_LISTEN_V4 => LayerKeys.FWPM_LAYER_ALE_AUTH_LISTEN_V4,
                LayerKeyEnum.FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V6 => LayerKeys.FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V6,
                LayerKeyEnum.FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V4 => LayerKeys.FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V4,
                LayerKeyEnum.FWPM_LAYER_INBOUND_TRANSPORT_V6_DISCARD => LayerKeys.FWPM_LAYER_INBOUND_TRANSPORT_V6_DISCARD,
                LayerKeyEnum.FWPM_LAYER_INBOUND_TRANSPORT_V4_DISCARD => LayerKeys.FWPM_LAYER_INBOUND_TRANSPORT_V4_DISCARD,
                LayerKeyEnum.FWPM_LAYER_ALE_RESOURCE_ASSIGNMENT_V6 => LayerKeys.FWPM_LAYER_ALE_RESOURCE_ASSIGNMENT_V6,
                LayerKeyEnum.FWPM_LAYER_ALE_RESOURCE_ASSIGNMENT_V4 => LayerKeys.FWPM_LAYER_ALE_RESOURCE_ASSIGNMENT_V4,
                _ => throw new ArgumentException("Invalid or not support layerEnum."),
            };
        }

        private void InstallWfpFilter(Filter f)
        {
            try
            {
                f.FilterKey = Guid.NewGuid();
                f.Flags = FilterFlags.FWPM_FILTER_FLAG_PERSISTENT;
                _wfpEngine.RegisterFilter(f);

                f.FilterKey = Guid.NewGuid();
                f.Flags = FilterFlags.FWPM_FILTER_FLAG_BOOTTIME;
                _wfpEngine.RegisterFilter(f);
            }
            catch
            {
                // ignored
            }
        }

        private void ConstructFilter(RuleDef r, LayerKeyEnum layer)
        {
            // Local helper methods

            bool AddCommonIpFilterCondition(IpFilterCondition cond, FilterConditionList coll)
            {
                if (cond.IsIPv6 == LayerIsV6Stack(layer))
                {
                    coll.Add(cond);
                    return true;
                }
                return false;
            }
            bool AddIpFilterCondition(IpAddrMask peerAddr, RemoteOrLocal peerType, FilterConditionList coll)
            {
                if (peerAddr.IsIPv6 == LayerIsV6Stack(layer))
                {
                    coll.Add(new IpFilterCondition(peerAddr.Address, (byte)peerAddr.PrefixLen, peerType));
                    return true;
                }
                return false;
            }
            (ushort, ushort) ParseUInt16Range(ReadOnlySpan<char> str)
            {
                if (-1 != str.IndexOf('-'))
                {
                    ReadOnlySpan<char> min, max;
                    using (var enumerator = str.Split('-'))
                    {
                        enumerator.MoveNext(); min = enumerator.Current;
                        enumerator.MoveNext(); max = enumerator.Current;
                    }
                    return (min.DecimalToUInt16(), max.DecimalToUInt16());
                }
                else
                {
                    var port = str.DecimalToUInt16();
                    return (port, port);
                }
            }

            // ---------------------------------------

            using var conditions = new FilterConditionList();

            // Application identity
            if (!Utils.IsNullOrEmpty(r.AppContainerSid))
            {
                Debug.Assert(!r.AppContainerSid.Equals("*"));

                // Skip filter if OS is not supported
                if (!Windows.VersionInfo.Win81OrNewer)
                    return;

                if (!LayerIsIcmpError(layer))
                    conditions.Add(new PackageIdFilterCondition(r.AppContainerSid));
                else
                    return;
            }
            else
            {
                if (!Utils.IsNullOrEmpty(r.ServiceName))
                {
                    Debug.Assert(!r.ServiceName.Equals("*"));
                    if (!LayerIsIcmpError(layer))
                        conditions.Add(new ServiceNameFilterCondition(r.ServiceName));
                    else
                        return;
                }

                if (!Utils.IsNullOrEmpty(r.Application))
                {
                    Debug.Assert(!r.Application.Equals("*"));

                    if (!LayerIsIcmpError(layer))
                        conditions.Add(new AppIdFilterCondition(r.Application, false, true));
                    else
                        return;
                }
            }

            // IP address
            if (!Utils.IsNullOrEmpty(r.RemoteAddresses))
            {
                Debug.Assert(!r.RemoteAddresses.Equals("*"));

                var validAddressFound = false;
                foreach (var ipStr in r.RemoteAddresses.AsSpan().Split(',', SpanSplitOptions.RemoveEmptyEntries))
                {
                    if (ipStr.Equals(RuleDef.LOCALSUBNET_ID, StringComparison.Ordinal))
                    {
                        validAddressFound = _localSubnetFilterConditions.Aggregate(validAddressFound, (current, filter) => current | AddCommonIpFilterCondition((IpFilterCondition)filter, conditions));
                    }
                    else if (ipStr.Equals("DefaultGateway", StringComparison.Ordinal))
                    {
                        validAddressFound = _gatewayFilterConditions.Aggregate(validAddressFound, (current, filter) => current | AddCommonIpFilterCondition((IpFilterCondition)filter, conditions));
                    }
                    else if (ipStr.Equals("DNS", StringComparison.Ordinal))
                    {
                        validAddressFound = _dnsFilterConditions.Aggregate(validAddressFound, (current, filter) => current | AddCommonIpFilterCondition((IpFilterCondition)filter, conditions));
                    }
                    else
                    {
                        validAddressFound |= AddIpFilterCondition(IpAddrMask.Parse(ipStr), RemoteOrLocal.Remote, conditions);
                    }
                }

                if (!validAddressFound)
                {
                    // Break. We don't want to add this filter to this layer.
                    return;
                }
            }

            // We never want to affect loopback traffic
            conditions.Add(new FlagsFilterCondition(ConditionFlags.FWP_CONDITION_FLAG_IS_LOOPBACK, FieldMatchType.FWP_MATCH_FLAGS_NONE_SET));

            // Protocol
            if (r.Protocol != Protocol.Any)
            {
                if (LayerIsAleAuthConnect(layer) || LayerIsAleAuthRecvAccept(layer))
                {
                    if (r.Protocol == Protocol.TcpUdp)
                    {
                        conditions.Add(new ProtocolFilterCondition((byte)Protocol.TCP));
                        conditions.Add(new ProtocolFilterCondition((byte)Protocol.UDP));
                    }
                    else
                        conditions.Add(new ProtocolFilterCondition((byte)r.Protocol));
                }
            }

            // Ports
            if (!Utils.IsNullOrEmpty(r.LocalPorts))
            {
                Debug.Assert(!r.LocalPorts.Equals("*"));
                foreach (var p in r.LocalPorts.AsSpan().Split(',', SpanSplitOptions.RemoveEmptyEntries))
                {
                    var (minPort, maxPort) = ParseUInt16Range(p);
                    conditions.Add(new PortFilterCondition(minPort, maxPort, RemoteOrLocal.Local));
                }
            }
            if (!Utils.IsNullOrEmpty(r.RemotePorts))
            {
                Debug.Assert(!r.RemotePorts.Equals("*"));
                foreach (var p in r.RemotePorts.AsSpan().Split(',', SpanSplitOptions.RemoveEmptyEntries))
                {
                    var (minPort, maxPort) = ParseUInt16Range(p);
                    conditions.Add(new PortFilterCondition(minPort, maxPort, RemoteOrLocal.Remote));
                }
            }

            // ICMP
            if (!Utils.IsNullOrEmpty(r.IcmpTypesAndCodes))
            {
                Debug.Assert(!r.IcmpTypesAndCodes.Equals("*"));
                foreach (var e in r.IcmpTypesAndCodes.AsSpan().Split(',', SpanSplitOptions.RemoveEmptyEntries))
                {
                    using var tc = e.Split(':');
                    tc.MoveNext(); var icmpType = tc.Current;

                    if (LayerIsIcmpError(layer))
                    {
                        // ICMP Type
                        if ((icmpType.Length != 0) && icmpType.TryDecimalToUInt16(out ushort icmpTypeVal))
                            conditions.Add(new IcmpErrorTypeFilterCondition(icmpTypeVal));

                        // ICMP Code
                        if (!tc.MoveNext()) continue;

                        var icmpCode = tc.Current;
                        if ((icmpCode.Length != 0) && !icmpCode.Equals("*", StringComparison.Ordinal) && icmpCode.TryDecimalToUInt16(out var icmpCodeVal))
                            conditions.Add(new IcmpErrorCodeFilterCondition(icmpCodeVal));
                    }
                    else
                    {
                        // ICMP Type - note different condition key
                        if ((icmpType.Length != 0) && icmpType.TryDecimalToUInt16(out ushort icmpTypeVal))
                            conditions.Add(new IcmpTypeFilterCondition(icmpTypeVal));

                        // Matching on ICMP Code not possible
                    }
                }
            }

            // Create and install filter
            using var f = new Filter(
                r.ExceptionId.ToString(),
                r.Name,
                TinywallProviderKey,
                (r.Action == RuleAction.Allow) ? FilterActions.FWP_ACTION_PERMIT : FilterActions.FWP_ACTION_BLOCK,
                r.Weight,
                conditions
            );
            f.LayerKey = GetLayerKey(layer);
            f.SublayerKey = GetSublayerKey(layer);

            InstallWfpFilter(f);
        }

        private void InstallRawSocketBlocks()
        {
            InstallRawSocketBlocks(LayerKeyEnum.FWPM_LAYER_ALE_RESOURCE_ASSIGNMENT_V4);
            InstallRawSocketBlocks(LayerKeyEnum.FWPM_LAYER_ALE_RESOURCE_ASSIGNMENT_V6);
        }

        private void InstallRawSocketBlocks(LayerKeyEnum layer)
        {
            using var f = new Filter(
                "Raw socket block",
                string.Empty,
                TinywallProviderKey,
                FilterActions.FWP_ACTION_BLOCK,
                (ulong)FilterWeights.RawSocketBlock
            );
            f.LayerKey = GetLayerKey(layer);
            f.SublayerKey = GetSublayerKey(layer);
            f.Conditions.Add(new FlagsFilterCondition(ConditionFlags.FWP_CONDITION_FLAG_IS_RAW_ENDPOINT, FieldMatchType.FWP_MATCH_FLAGS_ANY_SET));

            InstallWfpFilter(f);
        }

        private void InstallWsl2Filters(bool permit)
        {
            const string IF_ALIAS = "vEthernet (WSL)";
            try
            {
                if (!LocalInterfaceCondition.InterfaceAliasExists(IF_ALIAS)) return;

                InstallWsl2Filters(permit, IF_ALIAS, LayerKeyEnum.FWPM_LAYER_ALE_AUTH_CONNECT_V4);
                InstallWsl2Filters(permit, IF_ALIAS, LayerKeyEnum.FWPM_LAYER_ALE_AUTH_CONNECT_V6);
                InstallWsl2Filters(permit, IF_ALIAS, LayerKeyEnum.FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V4);
                InstallWsl2Filters(permit, IF_ALIAS, LayerKeyEnum.FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V6);
                InstallWsl2Filters(permit, IF_ALIAS, LayerKeyEnum.FWPM_LAYER_OUTBOUND_ICMP_ERROR_V4);
                InstallWsl2Filters(permit, IF_ALIAS, LayerKeyEnum.FWPM_LAYER_OUTBOUND_ICMP_ERROR_V6);
                InstallWsl2Filters(permit, IF_ALIAS, LayerKeyEnum.FWPM_LAYER_INBOUND_ICMP_ERROR_V4);
                InstallWsl2Filters(permit, IF_ALIAS, LayerKeyEnum.FWPM_LAYER_INBOUND_ICMP_ERROR_V6);
            }
            catch
            {
                // ignored
            }
        }

        private void InstallWsl2Filters(bool permit, string ifAlias, LayerKeyEnum layer)
        {
            var action = permit ? FilterActions.FWP_ACTION_PERMIT : FilterActions.FWP_ACTION_BLOCK;
            var weight = (ulong)(permit ? FilterWeights.UserPermit : FilterWeights.UserBlock);

            using var f = new Filter(
                "Allow WSL2",
                string.Empty,
                TinywallProviderKey,
                action,
                weight
            );
            f.LayerKey = GetLayerKey(layer);
            f.SublayerKey = GetSublayerKey(layer);
            f.Conditions.Add(new LocalInterfaceCondition(ifAlias));

            InstallWfpFilter(f);
        }

        private void InstallRawSocketPermits(List<RuleDef>? rawSocketExceptions)
        {
            InstallRawSocketPermits(rawSocketExceptions, LayerKeyEnum.FWPM_LAYER_ALE_RESOURCE_ASSIGNMENT_V4);
            InstallRawSocketPermits(rawSocketExceptions, LayerKeyEnum.FWPM_LAYER_ALE_RESOURCE_ASSIGNMENT_V6);
        }

        private void InstallRawSocketPermits(List<RuleDef>? rawSocketExceptions, LayerKeyEnum layer)
        {
            if (rawSocketExceptions == null) return;

            foreach (var subj in rawSocketExceptions)
            {
                try
                {
                    using var conditions = new FilterConditionList();
                    if (!Utils.IsNullOrEmpty(subj.Application))
                        conditions.Add(new AppIdFilterCondition(subj.Application, false, true));
                    if (!Utils.IsNullOrEmpty(subj.ServiceName))
                        conditions.Add(new ServiceNameFilterCondition(subj.ServiceName));
                    if (conditions.Count == 0)
                        return;

                    using var f = new Filter(
                        "Raw socket permit",
                        string.Empty,
                        TinywallProviderKey,
                        FilterActions.FWP_ACTION_PERMIT,
                        (ulong)FilterWeights.RawSocketPermit,
                        conditions
                    );
                    f.LayerKey = GetLayerKey(layer);
                    f.SublayerKey = GetSublayerKey(layer);

                    InstallWfpFilter(f);
                }
                catch
                {
                    // ignored
                }
            }
        }

        private void InstallPortScanProtection()
        {
            InstallPortScanProtection(LayerKeyEnum.FWPM_LAYER_INBOUND_TRANSPORT_V4_DISCARD, BuiltinCallouts.FWPM_CALLOUT_WFP_TRANSPORT_LAYER_V4_SILENT_DROP);
            InstallPortScanProtection(LayerKeyEnum.FWPM_LAYER_INBOUND_TRANSPORT_V6_DISCARD, BuiltinCallouts.FWPM_CALLOUT_WFP_TRANSPORT_LAYER_V6_SILENT_DROP);
        }

        private void InstallPortScanProtection(LayerKeyEnum layer, Guid callout)
        {
            using var f = new Filter(
                "Port Scanning Protection",
                string.Empty,
                TinywallProviderKey,
                FilterActions.FWP_ACTION_CALLOUT_TERMINATING,
                (ulong)FilterWeights.Blocklist
            );
            f.LayerKey = GetLayerKey(layer);
            f.SublayerKey = GetSublayerKey(layer);
            f.CalloutKey = callout;

            // Don't affect loopback traffic
            f.Conditions.Add(new FlagsFilterCondition(ConditionFlags.FWP_CONDITION_FLAG_IS_LOOPBACK | ConditionFlags.FWP_CONDITION_FLAG_IS_IPSEC_SECURED, FieldMatchType.FWP_MATCH_FLAGS_NONE_SET));

            InstallWfpFilter(f);
        }

        private static bool LayerIsAleAuthConnect(LayerKeyEnum layer)
        {
            return
                (layer == LayerKeyEnum.FWPM_LAYER_ALE_AUTH_CONNECT_V4) ||
                (layer == LayerKeyEnum.FWPM_LAYER_ALE_AUTH_CONNECT_V6);
        }

        private static bool LayerIsAleAuthRecvAccept(LayerKeyEnum layer)
        {
            return
                (layer == LayerKeyEnum.FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V6) ||
                (layer == LayerKeyEnum.FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V4);
        }

        private static bool LayerIsIcmpError(LayerKeyEnum layer)
        {
            return
                (layer == LayerKeyEnum.FWPM_LAYER_OUTBOUND_ICMP_ERROR_V6) ||
                (layer == LayerKeyEnum.FWPM_LAYER_OUTBOUND_ICMP_ERROR_V4) ||
                (layer == LayerKeyEnum.FWPM_LAYER_INBOUND_ICMP_ERROR_V6) ||
                (layer == LayerKeyEnum.FWPM_LAYER_INBOUND_ICMP_ERROR_V4);
        }

        private static bool LayerIsV6Stack(LayerKeyEnum layer)
        {
            return
                (layer == LayerKeyEnum.FWPM_LAYER_ALE_AUTH_CONNECT_V6) ||
                (layer == LayerKeyEnum.FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V6) ||
                (layer == LayerKeyEnum.FWPM_LAYER_ALE_AUTH_LISTEN_V6) ||
                (layer == LayerKeyEnum.FWPM_LAYER_OUTBOUND_ICMP_ERROR_V6) ||
                (layer == LayerKeyEnum.FWPM_LAYER_INBOUND_ICMP_ERROR_V6);
        }

        private void ConstructFilter(RuleDef r)
        {
            // Also, relevant info:
            // https://networkengineering.stackexchange.com/questions/58903/how-to-handle-icmp-in-ipv6-or-icmpv6-in-ipv4

            if ((r.Direction & RuleDirection.Out) != 0)
            {
                ConstructFilter(r, LayerKeyEnum.FWPM_LAYER_ALE_AUTH_CONNECT_V6);
                ConstructFilter(r, LayerKeyEnum.FWPM_LAYER_ALE_AUTH_CONNECT_V4);

                if ((r.Protocol == Protocol.Any) || (r.Protocol == Protocol.ICMPv6))
                    ConstructFilter(r, LayerKeyEnum.FWPM_LAYER_OUTBOUND_ICMP_ERROR_V6);
                if ((r.Protocol == Protocol.Any) || (r.Protocol == Protocol.ICMPv4))
                    ConstructFilter(r, LayerKeyEnum.FWPM_LAYER_OUTBOUND_ICMP_ERROR_V4);
            }
            if ((r.Direction & RuleDirection.In) != 0)
            {
                ConstructFilter(r, LayerKeyEnum.FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V6);
                ConstructFilter(r, LayerKeyEnum.FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V4);

                if ((r.Protocol == Protocol.Any) || (r.Protocol == Protocol.ICMPv6))
                    ConstructFilter(r, LayerKeyEnum.FWPM_LAYER_INBOUND_ICMP_ERROR_V6);
                if ((r.Protocol == Protocol.Any) || (r.Protocol == Protocol.ICMPv4))
                    ConstructFilter(r, LayerKeyEnum.FWPM_LAYER_INBOUND_ICMP_ERROR_V4);
            }
        }

        private static List<FirewallExceptionV3> CollectExceptionsForAppByName(string name)
        {
            var exceptions = new List<FirewallExceptionV3>();

            try
            {
                // Retrieve database entry for appName
                var app = GlobalInstances.AppDatabase.GetApplicationByName(name);
                if (app is null)
                    return exceptions;

                // Create rules
                foreach (DatabaseClasses.SubjectIdentity id in app.Components)
                {
                    try
                    {
                        var foundSubjects = id.SearchForFile();
                        exceptions.AddRange(foundSubjects.Select(subject => id.InstantiateException(subject)));
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
            catch
            {
                // ignored
            }

            return exceptions;
        }

        private static void GetRulesForException(FirewallExceptionV3 ex, List<RuleDef> results, List<RuleDef>? rawSocketExceptions, ulong permitWeight, ulong blockWeight)
        {
            if (ex.Id == Guid.Empty)
            {
                // Do not let the service crash if a rule cannot be constructed
                //#if DEBUG
                //throw new InvalidOperationException("Firewall exception specification must have an ID.");
                //#else
                ex.RegenerateId();
                GlobalInstances.ServerChangeset = Guid.NewGuid();
                //#endif
            }

            switch (ex.Policy.PolicyType)
            {
                case PolicyType.HardBlock:
                    {
                        var def = new RuleDef(ex.Id, "Block", ex.Subject, RuleAction.Block, RuleDirection.InOut, Protocol.Any, blockWeight);
                        results.Add(def);
                        break;
                    }
                case PolicyType.Unrestricted:
                    {
                        var pol = (UnrestrictedPolicy)ex.Policy;

                        var def = new RuleDef(ex.Id, "Full access", ex.Subject, RuleAction.Allow, RuleDirection.InOut, Protocol.Any, permitWeight);
                        if (pol.LocalNetworkOnly)
                            def.RemoteAddresses = RuleDef.LOCALSUBNET_ID;
                        results.Add(def);

                        // Make exception for promiscuous mode
                        rawSocketExceptions?.Add(def);

                        break;
                    }
                case PolicyType.TcpUdpOnly:
                    {
                        var pol = (TcpUdpPolicy)ex.Policy;

                        // Incoming
                        if (!string.IsNullOrEmpty(pol.AllowedLocalTcpListenerPorts) && (pol.AllowedLocalTcpListenerPorts == pol.AllowedLocalUdpListenerPorts))
                        {
                            var def = new RuleDef(ex.Id, "TCP/UDP Listen Ports", ex.Subject, RuleAction.Allow, RuleDirection.In, Protocol.TcpUdp, permitWeight);
                            if (!string.Equals(pol.AllowedLocalTcpListenerPorts, "*"))
                                def.LocalPorts = pol.AllowedLocalTcpListenerPorts;
                            if (pol.LocalNetworkOnly)
                                def.RemoteAddresses = RuleDef.LOCALSUBNET_ID;
                            results.Add(def);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(pol.AllowedLocalTcpListenerPorts))
                            {
                                var def = new RuleDef(ex.Id, "TCP Listen Ports", ex.Subject, RuleAction.Allow, RuleDirection.In, Protocol.TCP, permitWeight);
                                if (!string.Equals(pol.AllowedLocalTcpListenerPorts, "*"))
                                    def.LocalPorts = pol.AllowedLocalTcpListenerPorts;
                                if (pol.LocalNetworkOnly)
                                    def.RemoteAddresses = RuleDef.LOCALSUBNET_ID;
                                results.Add(def);
                            }
                            if (!string.IsNullOrEmpty(pol.AllowedLocalUdpListenerPorts))
                            {
                                var def = new RuleDef(ex.Id, "UDP Listen Ports", ex.Subject, RuleAction.Allow, RuleDirection.In, Protocol.UDP, permitWeight);
                                if (!string.Equals(pol.AllowedLocalUdpListenerPorts, "*"))
                                    def.LocalPorts = pol.AllowedLocalUdpListenerPorts;
                                if (pol.LocalNetworkOnly)
                                    def.RemoteAddresses = RuleDef.LOCALSUBNET_ID;
                                results.Add(def);
                            }
                        }

                        // Outgoing
                        if (!string.IsNullOrEmpty(pol.AllowedRemoteTcpConnectPorts) && (pol.AllowedRemoteTcpConnectPorts == pol.AllowedRemoteUdpConnectPorts))
                        {
                            var def = new RuleDef(ex.Id, "TCP/UDP Outbound Ports", ex.Subject, RuleAction.Allow, RuleDirection.Out, Protocol.TcpUdp, permitWeight);
                            if (!string.Equals(pol.AllowedRemoteTcpConnectPorts, "*"))
                                def.RemotePorts = pol.AllowedRemoteTcpConnectPorts;
                            if (pol.LocalNetworkOnly)
                                def.RemoteAddresses = RuleDef.LOCALSUBNET_ID;
                            results.Add(def);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(pol.AllowedRemoteTcpConnectPorts))
                            {
                                var def = new RuleDef(ex.Id, "TCP Outbound Ports", ex.Subject, RuleAction.Allow, RuleDirection.Out, Protocol.TCP, permitWeight);
                                if (!string.Equals(pol.AllowedRemoteTcpConnectPorts, "*"))
                                    def.RemotePorts = pol.AllowedRemoteTcpConnectPorts;
                                if (pol.LocalNetworkOnly)
                                    def.RemoteAddresses = RuleDef.LOCALSUBNET_ID;
                                results.Add(def);
                            }
                            if (!string.IsNullOrEmpty(pol.AllowedRemoteUdpConnectPorts))
                            {
                                var def = new RuleDef(ex.Id, "UDP Outbound Ports", ex.Subject, RuleAction.Allow, RuleDirection.Out, Protocol.UDP, permitWeight);
                                if (!string.Equals(pol.AllowedRemoteUdpConnectPorts, "*"))
                                    def.RemotePorts = pol.AllowedRemoteUdpConnectPorts;
                                if (pol.LocalNetworkOnly)
                                    def.RemoteAddresses = RuleDef.LOCALSUBNET_ID;
                                results.Add(def);
                            }
                        }
                        break;
                    }
                case PolicyType.RuleList:
                    {
                        // The RuleDefs returned can get modified by the caller.
                        // To avoid changing the original templates we return copies of rules.

                        var pol = (RuleListPolicy)ex.Policy;
                        foreach (var rule in pol.Rules)
                        {
                            var ruleCopy = rule.ShallowCopy();
                            ruleCopy.SetSubject(ex.Subject);
                            ruleCopy.ExceptionId = ex.Id;
                            ruleCopy.Weight = (rule.Action == RuleAction.Allow) ? permitWeight : blockWeight;
                            results.Add(ruleCopy);
                        }
                        break;
                    }
                case PolicyType.Invalid:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static string ConfigSavePath => Path.Combine(Utils.AppDataPath, "config");

        private static ServerConfiguration LoadServerConfig()
        {
            try
            {
                return ServerConfiguration.Load(ConfigSavePath);
            }
            catch
            {
                // ignored
            }

            // Load from file failed, prepare default config instead

            var ret = new ServerConfiguration
            {
                ActiveProfileName = Resources.Messages.Default
            };

            // Allow recommended exceptions
            var db = GlobalInstances.AppDatabase;
            foreach (var app in db.KnownApplications.Where(app => app.HasFlag("TWUI:Special") && app.HasFlag("TWUI:Recommended")))
            {
                ret.ActiveProfile.SpecialExceptions.Add(app.Name);
            }

            return ret;
        }

        // This method completely reinitializes the firewall.
        private void InitFirewall()
        {
            using var timer = new HierarchicalStopwatch(nameof(InitFirewall));
            LoadDatabase();
            ActiveConfig.Service = LoadServerConfig();
            _visibleState.Mode = ActiveConfig.Service.StartupMode;
            GlobalInstances.ServerChangeset = Guid.NewGuid();

            if (CommitLearnedRules() || PruneExpiredRules())
                ActiveConfig.Service.Save(ConfigSavePath);

            ReapplySettings();
            InstallFirewallRules();
        }


        // This method reapplies all firewall settings.
        private void ReapplySettings()
        {
            using var timer = new HierarchicalStopwatch(nameof(ReapplySettings));
            _hostsFileManager.EnableProtection = ActiveConfig.Service.LockHostsFile;
            if (ActiveConfig.Service.Blocklists.EnableBlocklists
                && ActiveConfig.Service.Blocklists.EnableHostsBlocklist)
                _hostsFileManager.EnableHostsFile();
            else
                _hostsFileManager.DisableHostsFile();
        }

        private static void LoadDatabase()
        {
            using var timer = new HierarchicalStopwatch(nameof(LoadDatabase));

            try
            {
                GlobalInstances.AppDatabase = DatabaseClasses.AppDatabase.Load();
            }
            catch
            {
                GlobalInstances.AppDatabase = new DatabaseClasses.AppDatabase();
            }
        }

        //#if !DEBUG
        private DateTime? _lastUpdateCheck;
        private const string LAST_UPDATE_CHECK_FILENAME = "updatecheck";
        private DateTime LastUpdateCheck
        {
            get
            {
                if (!_lastUpdateCheck.HasValue)
                {
                    try
                    {
                        string filePath = Path.Combine(Utils.AppDataPath, LAST_UPDATE_CHECK_FILENAME);
                        if (File.Exists(filePath))
                        {
                            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                            using var sr = new StreamReader(fs, Encoding.UTF8);
                            _lastUpdateCheck = DateTime.Parse(sr.ReadLine());
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                }

                _lastUpdateCheck ??= DateTime.MinValue;
                if (_lastUpdateCheck.Value > DateTime.Now)
                    _lastUpdateCheck = DateTime.MinValue;

                return _lastUpdateCheck.Value;
            }

            set
            {
                _lastUpdateCheck = value;

                try
                {
                    string filePath = Path.Combine(Utils.AppDataPath, LAST_UPDATE_CHECK_FILENAME);
                    using var afu = new AtomicFileUpdater(filePath);
                    using (var fs = new FileStream(afu.TemporaryFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        using var sw = new StreamWriter(fs, Encoding.UTF8);
                        sw.WriteLine(value.ToString("O"));
                    }
                    afu.Commit();
                }
                catch
                {
                    // ignored
                }
            }
        }

        private void UpdaterMethod()
        {
            UpdateDescriptor? update = null;
            try
            {
                if (DateTime.Now - LastUpdateCheck >= TimeSpan.FromDays(2))
                {
                    LastUpdateCheck = DateTime.Now;
                    update = UpdateChecker.GetDescriptor();
                }
            }
            catch
            {
                // This is an automatic update check in the background.
                // If we fail (for whatever reason, no internet, server down etc.),
                // we fail silently.
                return;
            }

            if (update is null)
                return;

            _visibleState.Update = update;
            GlobalInstances.ServerChangeset = Guid.NewGuid();

            try
            {
                UpdateModule? module = UpdateChecker.GetDatabaseFileModule(_visibleState.Update);
                if (module is not null)
                {
                    if (!string.Equals(module.DownloadHash, Hasher.HashFile(DatabaseClasses.AppDatabase.DBPath), StringComparison.OrdinalIgnoreCase))
                    {
                        GetCompressedUpdate(module, DatabaseUpdateInstall);
                    }
                }

                module = UpdateChecker.GetHostsFileModule(_visibleState.Update);
                if (module is not null)
                {
                    if (!string.Equals(module.DownloadHash, HostsFileManager.GetHostsHash(), StringComparison.OrdinalIgnoreCase))
                    {
                        GetCompressedUpdate(module, HostsUpdateInstall);
                    }
                }
            }
            catch (Exception e)
            {
                Utils.LogException(e, Utils.LOG_ID_SERVICE);
            }
        }

        private static void GetCompressedUpdate(UpdateModule module, WaitCallback installMethod)
        {
            var tmpCompressedPath = Path.GetTempFileName();
            var tmpFile = Path.GetTempFileName();
            try
            {
                using (var downloader = new WebClient())
                {
                    if (module.UpdateURL != null) downloader.DownloadFile(module.UpdateURL, tmpCompressedPath);
                }
                Utils.DecompressDeflate(tmpCompressedPath, tmpFile);

                if (Hasher.HashFile(tmpFile).Equals(module.DownloadHash, StringComparison.OrdinalIgnoreCase))
                    installMethod(tmpFile);
            }
            catch
            {
                // ignored
            }
            finally
            {
                try
                {
                    File.Delete(tmpCompressedPath);
                }
                catch
                {
                    // ignored
                }

                try
                {
                    File.Delete(tmpFile);
                }
                catch
                {
                    // ignored
                }
            }
        }

        private void HostsUpdateInstall(object file)
        {
            var tmpHostsPath = (string)file;
            _hostsFileManager.UpdateHostsFile(tmpHostsPath);

            if (ActiveConfig.Service.Blocklists.EnableBlocklists
                && ActiveConfig.Service.Blocklists.EnableHostsBlocklist)
            {
                _hostsFileManager.EnableHostsFile();
            }
        }
        private void DatabaseUpdateInstall(object file)
        {
            var tmpFilePath = (string)file;

            _fileLocker.Unlock(DatabaseClasses.AppDatabase.DBPath);
            using (var afu = new AtomicFileUpdater(DatabaseClasses.AppDatabase.DBPath))
            {
                File.Copy(tmpFilePath, afu.TemporaryFilePath, true);
                afu.Commit();
            }
            _fileLocker.Lock(DatabaseClasses.AppDatabase.DBPath, FileAccess.Read, FileShare.Read);
            NotifyController(MessageType.DATABASE_UPDATED);
            _q.Add(new TwRequest(TwMessageSimple.CreateRequest(MessageType.REINIT)));
        }

        private void NotifyController(MessageType msg)
        {
            _visibleState.ClientNotifs.Add(msg);
            GlobalInstances.ServerChangeset = Guid.NewGuid();
        }
        //#endif

        internal void TimerCallback(object state)
        {
            _q.Add(new TwRequest(TwMessageSimple.CreateRequest(MessageType.MINUTE_TIMER)));
        }

        private Task<List<FirewallLogEntry>> GetFwLog()
        {
            return Task.Run(() =>
            {
                var entries = new List<FirewallLogEntry>();
                lock (_firewallLogEntries)
                {
                    entries.AddRange(_firewallLogEntries);
                }
                return entries;
            });
        }

        private bool CommitLearnedRules()
        {
            var configChanged = false;
            lock (_learningNewExceptions)
            {
                if (_learningNewExceptions.Count <= 0) return configChanged;

                GlobalInstances.ServerChangeset = Guid.NewGuid();
                ActiveConfig.Service.ActiveProfile.AddExceptions(_learningNewExceptions);
                _learningNewExceptions.Clear();
                configChanged = true;
            }

            return configChanged;
        }

        private static bool HasSystemRebooted()
        {
            try
            {
                const string ATOM_NAME = "TinyWall-NoMachineReboot";
                bool rebooted = !GlobalAtomTable.Exists(ATOM_NAME);
                if (rebooted)
                    GlobalAtomTable.Add(ATOM_NAME);
                return rebooted;
            }
            catch
            {
                return true;
            }
        }

        private static bool PruneExpiredRules()
        {
            var systemRebooted = HasSystemRebooted();
            var configChanged = false;

            List<FirewallExceptionV3> exs = ActiveConfig.Service.ActiveProfile.AppExceptions;
            for (int i = exs.Count - 1; i >= 0; --i)
            {
                // Timer values above zero are the number of minutes to stay active

                if (systemRebooted && (exs[i].Timer == AppExceptionTimer.UNTIL_REBOOT))
                {
                    exs.RemoveAt(i);
                    configChanged = true;
                }
                else if (((int)exs[i].Timer > 0) && (exs[i].CreationDate.AddMinutes((double)exs[i].Timer) <= DateTime.Now))
                {
                    exs.RemoveAt(i);
                    configChanged = true;
                }
            }

            if (configChanged)
            {
                GlobalInstances.ServerChangeset = Guid.NewGuid();
                ActiveConfig.Service.ActiveProfile.AppExceptions = exs;
            }

            return configChanged;
        }

        private async Task<TwMessage> ProcessCmd(TwMessage req)
        {
            switch (req.Type)
            {
                case MessageType.READ_FW_LOG:
                    {
                        var args = (TwMessageReadFwLog)req;
                        var fwlist = await GetFwLog();
                        return args.CreateResponse(fwlist.ToArray());
                    }
                case MessageType.IS_LOCKED:
                    {
                        var args = (TwMessageIsLocked)req;
                        return args.CreateResponse(PasswordLock.Locked);
                    }
                case MessageType.MODE_SWITCH:
                    {
                        var args = (TwMessageModeSwitch)req;
                        FirewallMode newMode = args.Mode;

                        try
                        {
                            _logWatcher.Enabled = (FirewallMode.Learning == newMode);
                        }
                        catch (Exception e)
                        {
                            Utils.Log("Cannot enter auto-learn mode. Is the 'eventlog' service running? For details see next log entry.", Utils.LOG_ID_SERVICE);
                            Utils.LogException(e, Utils.LOG_ID_SERVICE);
                            return TwMessageError.Instance;
                        }

                        bool saveNeeded = CommitLearnedRules();
                        _visibleState.Mode = newMode;
                        if ((ActiveConfig.Service.StartupMode != _visibleState.Mode) &&
                            (_visibleState.Mode != FirewallMode.Disabled) &&
                            (_visibleState.Mode != FirewallMode.Learning))
                        {
                            ActiveConfig.Service.StartupMode = _visibleState.Mode;
                            saveNeeded = true;
                        }
                        if (saveNeeded)
                            ActiveConfig.Service.Save(ConfigSavePath);

                        InstallFirewallRules();
                        return args.CreateResponse(_visibleState.Mode);
                    }
                case MessageType.PUT_SETTINGS:
                    {
                        var args = (TwMessagePutSettings)req;

                        var warning = (args.Changeset != GlobalInstances.ServerChangeset);
                        if (!warning)
                        {
                            try
                            {
                                GlobalInstances.ServerChangeset = Guid.NewGuid();
                                ActiveConfig.Service = args.Config;
                                ActiveConfig.Service.Save(ConfigSavePath);
                                ReapplySettings();
                                InstallFirewallRules();
                            }
                            catch (Exception e)
                            {
                                Utils.LogException(e, Utils.LOG_ID_SERVICE);
                            }
                        }
                        _visibleState.HasPassword = PasswordLock.HasPassword;
                        _visibleState.Locked = PasswordLock.Locked;
                        return args.CreateResponse(GlobalInstances.ServerChangeset, ActiveConfig.Service, _visibleState, warning);
                    }
                case MessageType.ADD_TEMPORARY_EXCEPTION:
                    {
                        var rules = new List<RuleDef>();
                        var rawSocketExceptions = new List<RuleDef>();
                        var args = (TwMessageAddTempException)req;

                        foreach (var ex in args.Exceptions)
                        {
                            GetRulesForException(ex, rules, rawSocketExceptions, (ulong)FilterWeights.UserPermit, (ulong)FilterWeights.UserBlock);
                        }

                        InstallRules(rules, rawSocketExceptions, true);
                        lock (_firewallThreadThrottler.SynchRoot) { _firewallThreadThrottler.Release(); }

                        return args.CreateResponse();
                    }
                case MessageType.GET_SETTINGS:
                    {
                        var args = (TwMessageGetSettings)req;

                        // If our changeset is different from the client's, send new settings
                        if (args.Changeset != GlobalInstances.ServerChangeset)
                        {
                            _visibleState.HasPassword = PasswordLock.HasPassword;
                            _visibleState.Locked = PasswordLock.Locked;

                            var ret = args.CreateResponse(GlobalInstances.ServerChangeset, ActiveConfig.Service, _visibleState);
                            _visibleState.ClientNotifs.Clear();  // TODO: VisibleState is a reference so it cleants notifs before client could receive them
                            return ret;
                        }
                        else
                        {
                            // Our changeset is the same, so do not send settings again
                            return args.CreateResponse(GlobalInstances.ServerChangeset);
                        }
                    }
                case MessageType.REINIT:
                    {
                        var args = (TwMessageSimple)req;
                        InitFirewall();
                        return args.CreateResponse();
                    }
                case MessageType.RELOAD_WFP_FILTERS:
                    {
                        var args = (TwMessageSimple)req;
                        InstallFirewallRules();
                        return args.CreateResponse();
                    }
                case MessageType.UNLOCK:
                    {
                        var args = (TwMessageUnlock)req;
                        bool success = PasswordLock.Unlock(args.Password);
                        if (success)
                            return args.CreateResponse();
                        else
                            return TwMessageError.Instance;
                    }
                case MessageType.LOCK:
                    {
                        var args = (TwMessageSimple)req;
                        PasswordLock.Locked = true;
                        return args.CreateResponse();
                    }
                case MessageType.GET_PROCESS_PATH:
                    {
                        var args = (TwMessageGetProcessPath)req;
                        string path = Utils.GetPathOfProcess(args.Pid);
                        if (string.IsNullOrEmpty(path))
                            return TwMessageError.Instance;
                        else
                            return args.CreateResponse(path);
                    }
                case MessageType.SET_PASSPHRASE:
                    {
                        var args = (TwMessageSetPassword)req;
                        _fileLocker.Unlock(PasswordLock.PasswordFilePath);
                        try
                        {
                            PasswordLock.SetPass(args.Password);
                            GlobalInstances.ServerChangeset = Guid.NewGuid();
                            return args.CreateResponse();
                        }
                        catch
                        {
                            return TwMessageError.Instance;
                        }
                        finally
                        {
                            _fileLocker.Lock(PasswordLock.PasswordFilePath, FileAccess.Read, FileShare.Read);
                        }
                    }
                case MessageType.STOP_SERVICE:
                    {
                        var args = (TwMessageSimple)req;
                        _runService = false;
                        return args.CreateResponse();
                    }
                case MessageType.MINUTE_TIMER:
                    {
                        var args = (TwMessageSimple)req;
                        var saveNeeded = false;
                        var ruleReloadNeeded = false;

                        // Check for inactivity and lock if necessary
                        if (DateTime.Now - _lastControllerCommandTime > TimeSpan.FromMinutes(10))
                        {
                            _q.Add(new TwRequest(TwMessageSimple.CreateRequest(MessageType.LOCK)));
                        }

                        if (PruneExpiredRules())
                        {
                            saveNeeded = true;
                            ruleReloadNeeded = true;
                        }

                        // Periodically reload all rules.
                        // This is needed to clear out temprary rules added due to child-process rule inheritance.
                        if (DateTime.Now - _lastRuleReloadTime > TimeSpan.FromMinutes(30))
                        {
                            ruleReloadNeeded = true;
                        }

                        if (saveNeeded)
                        {
                            ActiveConfig.Service.Save(ConfigSavePath);
                        }
                        if (ruleReloadNeeded)
                        {
                            InstallFirewallRules();
                        }

                        //#if !DEBUG
                        // Check for updates once every 2 days
                        if (ActiveConfig.Service.AutoUpdateCheck)
                        {
                            UpdaterMethod();
                        }
                        //#endif

                        return args.CreateResponse();
                    }
                case MessageType.REENUMERATE_ADDRESSES:
                    {
                        var args = (TwMessageSimple)req;
                        if (ReenumerateAdresses())  // returns true if anything changed
                            InstallFirewallRules();
                        return args.CreateResponse();
                    }
                case MessageType.DISPLAY_POWER_EVENT:
                    {
                        var args = (TwMessageDisplayPowerEvent)req;
                        if (args.PowerOn == _displayCurrentlyOn) return args.CreateResponse(args.PowerOn);

                        _displayCurrentlyOn = args.PowerOn;
                        InstallFirewallRules();
                        return args.CreateResponse(args.PowerOn);
                    }
                case MessageType.INVALID_COMMAND:
                case MessageType.RESPONSE_ERROR:
                case MessageType.RESPONSE_LOCKED:
                case MessageType.COM_ERROR:
                case MessageType.DATABASE_UPDATED:
                default:
                    {
                        return TwMessageError.Instance;
                    }
            }
        }

        private bool ReenumerateAdresses()
        {
            using var timer = new HierarchicalStopwatch("NIC enumeration");
            var newLocalSubnetAddreses = new HashSet<IpAddrMask>();
            var newGatewayAddresses = new HashSet<IpAddrMask>();
            var newDnsAddresses = new HashSet<IpAddrMask>();
            var coll = NetworkInterface.GetAllNetworkInterfaces();

            foreach (var iface in coll)
            {
                if (iface.OperationalStatus != OperationalStatus.Up)
                    continue;

                var props = iface.GetIPProperties();

                foreach (var uni in props.UnicastAddresses)
                {
                    var am = new IpAddrMask(uni);
                    if (am.IsLoopback || am.IsLinkLocal)
                        continue;

                    newLocalSubnetAddreses.Add(am.Subnet);
                }

                foreach (var uni in props.GatewayAddresses)
                {
                    var am = new IpAddrMask(uni);
                    newGatewayAddresses.Add(am);
                }

                foreach (var uni in props.DnsAddresses)
                {
                    var am = new IpAddrMask(uni);
                    newDnsAddresses.Add(am);
                }
            }

            newLocalSubnetAddreses.Add(new IpAddrMask(IPAddress.Parse("255.255.255.255")));
            newLocalSubnetAddreses.Add(IpAddrMask.LinkLocal);
            newLocalSubnetAddreses.Add(IpAddrMask.IPv6LinkLocal);
            newLocalSubnetAddreses.Add(IpAddrMask.LinkLocalMulticast);
            newLocalSubnetAddreses.Add(IpAddrMask.AdminScopedMulticast);
            newLocalSubnetAddreses.Add(IpAddrMask.IPv6LinkLocalMulticast);

            bool ipConfigurationChanged =
                !_localSubnetAddreses.SetEquals(newLocalSubnetAddreses) ||
                !_gatewayAddresses.SetEquals(newGatewayAddresses) ||
                !_dnsAddresses.SetEquals(newDnsAddresses);

            if (!ipConfigurationChanged) return ipConfigurationChanged;

            _localSubnetAddreses = newLocalSubnetAddreses;
            _gatewayAddresses = newGatewayAddresses;
            _dnsAddresses = newDnsAddresses;

            _localSubnetFilterConditions.Clear();
            _gatewayFilterConditions.Clear();
            _dnsFilterConditions.Clear();

            foreach (var addr in _localSubnetAddreses)
                _localSubnetFilterConditions.Add(new IpFilterCondition(addr.Address, (byte)addr.PrefixLen, RemoteOrLocal.Remote));
            foreach (var addr in _gatewayAddresses)
                _gatewayFilterConditions.Add(new IpFilterCondition(addr.Address, (byte)addr.PrefixLen, RemoteOrLocal.Remote));
            foreach (var addr in _dnsAddresses)
                _dnsFilterConditions.Add(new IpFilterCondition(addr.Address, (byte)addr.PrefixLen, RemoteOrLocal.Remote));

            return ipConfigurationChanged;
        }

        internal static void DeleteWfpObjects(Engine wfp, bool removeLayersAndProvider)
        {
            // WARNING! This method is super-slow if not executed inside a WFP transaction!
            using var timer = new HierarchicalStopwatch("DeleteWfpObjects()");
            var layerKeys = (LayerKeyEnum[])Enum.GetValues(typeof(LayerKeyEnum));
            foreach (var layer in layerKeys)
            {
                var layerKey = GetLayerKey(layer);
                var subLayerKey = GetSublayerKey(layer);

                // Remove filters in the sublayer
                foreach (var filterKey in wfp.EnumerateFilterKeys(TinywallProviderKey, layerKey))
                    wfp.UnregisterFilter(filterKey);

                // Remove sublayer
                if (!removeLayersAndProvider) continue;

                try { wfp.UnregisterSublayer(subLayerKey); }
                catch
                {
                    // ignored
                }
            }

            // Remove provider
            if (!removeLayersAndProvider) return;

            try { wfp.UnregisterProvider(TinywallProviderKey); }
            catch
            {
                // ignored
            }
        }

        public TinyWallServer()
        {
            // Make sure the very-first command is a REINIT
            _q.Add(new TwRequest(TwMessageSimple.CreateRequest(MessageType.REINIT)));

            // Fire up file protections as soon as possible
            _fileLocker.Lock(DatabaseClasses.AppDatabase.DBPath, FileAccess.Read, FileShare.Read);
            _fileLocker.Lock(PasswordLock.PasswordFilePath, FileAccess.Read, FileShare.Read);

            // Lock configuration if we have a password
            if (PasswordLock.HasPassword)
                PasswordLock.Locked = true;

            _logWatcher.NewLogEntry += (_, entry) => { AutoLearnLogEntry(entry); };
            _minuteTimer = new Timer(TimerCallback, null, Timeout.Infinite, Timeout.Infinite);

            // Discover network configuration
            ReenumerateAdresses();

            // Fire up pipe
            _serverPipe = new PipeServerEndpoint(PipeServerDataReceived, "TinyWallController");
        }

        // Entry point for thread that actually issues commands to Windows Firewall.
        // Only one thread (this one) is allowed to issue them.
        public async Task Run(ServiceBase service)
        {
            using var timer = new HierarchicalStopwatch("Service Run()");
            using var winDefFirewall = new WindowsFirewall();
            using var networkInterfaceWatcher = new IpInterfaceWatcher();
            using var wfpEvent = _wfpEngine.SubscribeNetEvent(WfpNetEventCallback);
            using var displayOffSubscription = SafeHandlePowerSettingNotification.Create(service.ServiceHandle, PowerSetting.GUID_CONSOLE_DISPLAY_STATE, DeviceNotifFlags.DEVICE_NOTIFY_SERVICE_HANDLE);
            using var deviceNotification = SafeHandleDeviceNotification.Create(service.ServiceHandle, DeviceInterfaceClass.GUID_DEVINTERFACE_VOLUME, DeviceNotifFlags.DEVICE_NOTIFY_SERVICE_HANDLE);
            using var mountPointsWatcher = new RegistryWatcher(@"HKEY_LOCAL_MACHINE\SYSTEM\MountedDevices", true);

            _wfpEngine.CollectNetEvents = true;
            _wfpEngine.EventMatchAnyKeywords = InboundEventMatchKeyword.FWPM_NET_EVENT_KEYWORD_INBOUND_BCAST | InboundEventMatchKeyword.FWPM_NET_EVENT_KEYWORD_INBOUND_MCAST;

            _processStartWatcher.EventArrived += ProcessStartWatcher_EventArrived;
            networkInterfaceWatcher.InterfaceChanged += (_, _) =>
            {
                _q.Add(new TwRequest(TwMessageSimple.CreateRequest(MessageType.REENUMERATE_ADDRESSES)));
            };
            _ruleReloadEventMerger.Event += (_, _) =>
            {
                _q.Add(new TwRequest(TwMessageSimple.CreateRequest(MessageType.RELOAD_WFP_FILTERS)));
            };
            mountPointsWatcher.RegistryChanged += (_, _) =>
            {
                _ruleReloadEventMerger.Pulse();
            };
            mountPointsWatcher.Enabled = true;
            service.FinishStateChange();
            //#if !DEBUG
            // Basic software health checks
            TinyWallDoctor.EnsureHealth(Utils.LOG_ID_SERVICE);
            //#endif

            _minuteTimer.Change(60000, 60000);
            _runService = true;
            while (_runService)
            {
                timer.NewSubTask("Message wait");
                var req = _q.Take();

                timer.NewSubTask($"Message {req.Request.Type}");
                try
                {
                    req.Response = await ProcessCmd(req.Request);
                }
                catch (Exception e)
                {
                    Utils.LogException(e, Utils.LOG_ID_SERVICE);
                    req.Response = TwMessageError.Instance;
                }
            }
        }

        private void ProcessStartWatcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            try
            {
                using var throttler = new ThreadThrottler(Thread.CurrentThread, ThreadPriority.Highest, true);
                uint pid = (uint)(e.NewEvent["ProcessID"]);
                string path = ProcessManager.GetProcessPath(pid, ref _processStartWatcherSbuilder);

                // Skip if we have no path
                if (string.IsNullOrEmpty(path))
                    return;

                List<FirewallExceptionV3>? newExceptions = null;

                lock (_inheritanceGuard)
                {
                    // Skip if we have a user-defined rule for this path
                    if (_userSubjectExes.Contains(path))
                        return;

                    // This list will hold parents that we already checked for a process.
                    // Used to avoid infinite loop when parent-PID info is unreliable.
                    var pidsChecked = new HashSet<uint>();

                    // Start walking up the process tree
                    for (var parentPid = pid; ;)
                    {
                        if (!ProcessManager.GetParentProcess(parentPid, ref parentPid))
                            // We reached the top of the process tree (with non-existent parent)
                            break;

                        if (parentPid == 0)
                            // We reached top of process tree (with idle process)
                            break;

                        if (pidsChecked.Contains(parentPid))
                            // We've been here before, damn it. Avoid looping eternally...
                            break;

                        pidsChecked.Add(parentPid);

                        string parentPath = ProcessManager.GetProcessPath(parentPid, ref _processStartWatcherSbuilder);
                        if (string.IsNullOrEmpty(parentPath))
                            continue;

                        // Skip if we have already processed this parent-child combination
                        if (_childInheritedSubjectExes.ContainsKey(path) && _childInheritedSubjectExes[path].Contains(parentPath))
                            break;

                        if (_childInheritance.TryGetValue(parentPath, out List<FirewallExceptionV3> exList))
                        {
                            newExceptions ??= new List<FirewallExceptionV3>();

                            foreach (var userEx in exList)
                                newExceptions.Add(new FirewallExceptionV3(new ExecutableSubject(path), userEx.Policy));

                            if (!_childInheritedSubjectExes.ContainsKey(path))
                                _childInheritedSubjectExes.Add(path, new HashSet<string>());
                            _childInheritedSubjectExes[path].Add(parentPath);
                            break;
                        }
                    }
                }

                if (newExceptions == null) return;

                lock (_firewallThreadThrottler.SynchRoot) { _firewallThreadThrottler.Request(); }
                _q.Add(new TwRequest(TwMessageAddTempException.CreateRequest(newExceptions.ToArray())));
            }
            finally
            {
                e.NewEvent.Dispose();
            }
        }

        private void WfpNetEventCallback(NetEventData data)
        {
            EventLogEvent eventType;
            switch (data.EventType)
            {
                case FWPM_NET_EVENT_TYPE.FWPM_NET_EVENT_TYPE_CLASSIFY_DROP:
                    eventType = EventLogEvent.BLOCKED;
                    break;
                case FWPM_NET_EVENT_TYPE.FWPM_NET_EVENT_TYPE_CLASSIFY_ALLOW:
                    eventType = EventLogEvent.ALLOWED;
                    break;
                case FWPM_NET_EVENT_TYPE.FWPM_NET_EVENT_TYPE_IKEEXT_MM_FAILURE:
                case FWPM_NET_EVENT_TYPE.FWPM_NET_EVENT_TYPE_IKEEXT_QM_FAILURE:
                case FWPM_NET_EVENT_TYPE.FWPM_NET_EVENT_TYPE_IKEEXT_EM_FAILURE:
                case FWPM_NET_EVENT_TYPE.FWPM_NET_EVENT_TYPE_IPSEC_KERNEL_DROP:
                case FWPM_NET_EVENT_TYPE.FWPM_NET_EVENT_TYPE_IPSEC_DOSP_DROP:
                case FWPM_NET_EVENT_TYPE.FWPM_NET_EVENT_TYPE_CAPABILITY_DROP:
                case FWPM_NET_EVENT_TYPE.FWPM_NET_EVENT_TYPE_CAPABILITY_ALLOW:
                case FWPM_NET_EVENT_TYPE.FWPM_NET_EVENT_TYPE_CLASSIFY_DROP_MAC:
                case FWPM_NET_EVENT_TYPE.FWPM_NET_EVENT_TYPE_MAX:
                default:
                    return;
            }

            var entry = new FirewallLogEntry
            {
                Timestamp = data.timeStamp,
                Event = eventType,
                AppPath = !Utils.IsNullOrEmpty(data.appId) ? PathMapper.Instance.ConvertPathIgnoreErrors(data.appId, PathFormat.Win32) : "System",
                PackageId = data.packageId,
                RemoteIp = data.remoteAddr,
                LocalIp = data.localAddr
            };

            if (data.remotePort.HasValue)
                entry.RemotePort = data.remotePort.Value;
            if (data.direction.HasValue)
                entry.Direction = data.direction == FwpmDirection.FWP_DIRECTION_OUT ? RuleDirection.Out : RuleDirection.In;
            if (data.ipProtocol.HasValue)
                entry.Protocol = (Protocol)data.ipProtocol;
            if (data.localPort.HasValue)
                entry.LocalPort = data.localPort.Value;

            // Replace invalid IP strings with the "unspecified address" IPv6 specifier
            if (string.IsNullOrEmpty(entry.RemoteIp))
                entry.RemoteIp = "::";
            if (string.IsNullOrEmpty(entry.LocalIp))
                entry.LocalIp = "::";

            lock (_firewallLogEntries)
            {
                _firewallLogEntries.Enqueue(entry);
            }
        }

        private void AutoLearnLogEntry(FirewallLogEntry entry)
        {
            if (  // IPv4
                ((string.Equals(entry.RemoteIp, "127.0.0.1", StringComparison.Ordinal)
                && string.Equals(entry.LocalIp, "127.0.0.1", StringComparison.Ordinal)))
               || // IPv6
                ((string.Equals(entry.RemoteIp, "::1", StringComparison.Ordinal)
                && string.Equals(entry.LocalIp, "::1", StringComparison.Ordinal)))
               )
            {
                // Ignore communication within local machine
                return;
            }

            // Certain things we don't want to whitelist
            if (Utils.IsNullOrEmpty(entry.AppPath)
                || string.Equals(entry.AppPath, "System", StringComparison.InvariantCultureIgnoreCase)
                || string.Equals(entry.AppPath, "svchost.exe", StringComparison.InvariantCultureIgnoreCase)
                )
                return;

            var newSubject = new ExecutableSubject(entry.AppPath);

            lock (_learningNewExceptions)
            {
                if (_learningNewExceptions.Any(t => t.Subject.Equals(newSubject)))
                {
                    return;
                }

                var exceptions = GlobalInstances.AppDatabase.GetExceptionsForApp(newSubject, false, out _);
                _learningNewExceptions.AddRange(exceptions);
            }
        }

        // Entry point for thread that listens to commands from the controller application.
        private TwMessage PipeServerDataReceived(TwMessage reqMsg)
        {
            if ((int)reqMsg.Type > 2047 && PasswordLock.Locked)
            {
                // Notify that we need to be unlocked first
                return TwMessageLocked.Instance;
            }

            if ((int)reqMsg.Type > 4095)
            {
                // We cannot receive this from the client
                return TwMessageError.Instance;
            }

            _lastControllerCommandTime = DateTime.Now;

            // Process and wait for response
            var req = new TwRequest(reqMsg);
            _q.Add(req);

            // Send response back to pipe
            return req.Response;
        }

        public void RequestStop()
        {
            var req = new TwRequest(TwMessageSimple.CreateRequest(MessageType.STOP_SERVICE));
            _q.Add(req);
            req.WaitResponse();
        }

        public void DisplayPowerEvent(bool turnOn)
        {
            _q.Add(new TwRequest(TwMessageDisplayPowerEvent.CreateRequest(turnOn)));
        }

        public void MountedVolumesChangedEvent()
        {
            _ruleReloadEventMerger.Pulse();
        }

        public void Dispose()
        {
            using var timer = new HierarchicalStopwatch("TinyWallService.Dispose()");
            _serverPipe.Dispose();
            _processStartWatcher.Dispose();

            using WaitHandle wh = new AutoResetEvent(false);
            _minuteTimer.Dispose(wh);
            wh.WaitOne();

            _ruleReloadEventMerger.Dispose();
            _localSubnetFilterConditions.Dispose();
            _gatewayFilterConditions.Dispose();
            _dnsFilterConditions.Dispose();
            _logWatcher.Dispose();
            CommitLearnedRules();
            _hostsFileManager.Dispose();
            _fileLocker.UnlockAll();

            _firewallThreadThrottler.Dispose();
            _q.Dispose();

            //#if !DEBUG
            // Basic software health checks
            TinyWallDoctor.EnsureHealth(Utils.LOG_ID_SERVICE);
            //#else
            using (var wfp = new Engine("TinyWall Cleanup Session", "", FWPM_SESSION_FLAGS.None, 5000))
            using (var trx = wfp.BeginTransaction())
            {
                DeleteWfpObjects(wfp, true);
                trx.Commit();
            }
            //#endif
            PathMapper.Instance.Dispose();
        }
    }


    internal sealed class TinyWallService : ServiceBase
    {
        internal static readonly string[] SERVICE_DEPENDENCIES = {
            "Schedule",
            "Winmgmt",
            "BFE"
        };

        internal const string NameOfService = "TinyWall";

        internal const string ServiceDisplayName = "TinyWall Service";

        private TinyWallServer? _server;

        private Thread? _firewallWorkerThread;
        //#if !DEBUG
        //private readonly bool _isComputerShuttingDown = false;
        //#endif
        internal TinyWallService()
        {
            AcceptedControls = ServiceAcceptedControl.SERVICE_ACCEPT_SHUTDOWN;
            AcceptedControls |= ServiceAcceptedControl.SERVICE_ACCEPT_POWEREVENT;
            //#if DEBUG
            AcceptedControls |= ServiceAcceptedControl.SERVICE_ACCEPT_STOP;
            //#endif
        }

        public override string ServiceName => NameOfService;

        private void FirewallWorkerMethod()
        {
            try
            {
                using (_server = new TinyWallServer())
                    _ = _server.Run(this);

            }
            finally
            {
                //#if !DEBUG
                Thread.MemoryBarrier();
                //if (!_isComputerShuttingDown)    // cannot set service state if a shutdown is already in progress
                //{
                SetServiceStateReached(ServiceState.Stopped);
                //}
                Process.GetCurrentProcess().Kill();
                //#endif
            }
        }

        // Entry point for Windows service.
        protected override void OnStart(string[] args)
        {
            // Initialisation on a new thread prevents stalling the SCM
            _firewallWorkerThread = new Thread(FirewallWorkerMethod)
            {
                Name = "ServiceMain"
            };
            _firewallWorkerThread.Start();
        }

        private void StopServer()
        {
            Thread.MemoryBarrier();
            _server?.RequestStop();
            _firewallWorkerThread?.Join(10000);
            FinishStateChange();
        }

        // Executed when service is stopped manually.
        protected override void OnStop()
        {
            StopServer();
        }

        // Executed on computer shutdown.
        protected override void OnShutdown()
        {
#if !DEBUG
            IsComputerShuttingDown = true;
#endif
            StartStateChange(ServiceState.StopPending);
        }

        protected override void OnDeviceEvent(DeviceEventData data)
        {
            if ((data.Event != DeviceEventType.DeviceArrival) &&
                (data.Event != DeviceEventType.DeviceRemoveComplete)) return;

            var pathMapperRebuildNeeded = false;

            switch (data.DeviceType)
            {
                case DeviceBroadcastHdrDevType.DBT_DEVTYP_DEVICEINTERFACE:
                    {
                        if (data.Class == DeviceInterfaceClass.GUID_DEVINTERFACE_VOLUME)
                        {
                            pathMapperRebuildNeeded = true;
                        }

                        break;
                    }
                case DeviceBroadcastHdrDevType.DBT_DEVTYP_VOLUME:
                    pathMapperRebuildNeeded = true;
                    break;
                case DeviceBroadcastHdrDevType.DBT_DEVTYP_HANDLE:
                case DeviceBroadcastHdrDevType.DBT_DEVTYP_OEM:
                case DeviceBroadcastHdrDevType.DBT_DEVTYP_PORT:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (pathMapperRebuildNeeded)
            {
                _server?.MountedVolumesChangedEvent();
            }
        }

        protected override void OnPowerEvent(PowerEventData data)
        {
            if (data.Event != PowerEventType.PowerSettingChange) return;

            if (data.Setting != PowerSetting.GUID_CONSOLE_DISPLAY_STATE) return;

            switch (data.PayloadInt)
            {
                case 0:
                    _server?.DisplayPowerEvent(false);
                    break;
                case 1:
                    _server?.DisplayPowerEvent(true);
                    break;
            }
            // Dimming event... ignore
        }
    }
}
