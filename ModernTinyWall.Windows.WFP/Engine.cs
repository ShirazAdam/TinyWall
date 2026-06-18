using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Security;


namespace ModernTinyWall.Windows.WFP
{
    public sealed partial class Engine : IDisposable
    {
        [SuppressUnmanagedCodeSecurity]
        internal static partial class NativeMethods
        {
            [LibraryImport("FWPUClnt.dll", EntryPoint = "FwpmEngineOpen0", StringMarshalling = StringMarshalling.Utf16)]
            internal static partial uint FwpmEngineOpen0(
                string? serverName,
                uint authnService,
                IntPtr authIdentity,
                IntPtr session,
                out FwpmEngineSafeHandle engineHandle);

            [LibraryImport("FWPUClnt.dll", EntryPoint = "FwpmEngineSetOption0")]
            internal static partial uint FwpmEngineSetOption0(FwpmEngineSafeHandle engineHandle, Interop.FWPM_ENGINE_OPTION option, ref Interop.FWP_VALUE0 newValue);

            [LibraryImport("FWPUClnt.dll", EntryPoint = "FwpmEngineGetOption0")]
            internal static partial uint FwpmEngineGetOption0(FwpmEngineSafeHandle engineHandle, Interop.FWPM_ENGINE_OPTION option, out FwpmMemorySafeHandle value);

            [LibraryImport("FWPUClnt.dll", EntryPoint = "FwpmProviderAdd0")]
            internal static partial uint FwpmProviderAdd0(FwpmEngineSafeHandle engineHandle, IntPtr provider, IntPtr sd);

            [LibraryImport("FWPUClnt.dll", EntryPoint = "FwpmProviderDeleteByKey0")]
            internal static partial uint FwpmProviderDeleteByKey0(FwpmEngineSafeHandle engineHandle, ref Guid key);

            [LibraryImport("FWPUClnt.dll", EntryPoint = "FwpmSubLayerAdd0")]
            internal static partial uint FwpmSubLayerAdd0(FwpmEngineSafeHandle engineHandle, IntPtr subLayer, IntPtr sd);

            [LibraryImport("FWPUClnt.dll", EntryPoint = "FwpmSubLayerDeleteByKey0")]
            internal static partial uint FwpmSubLayerDeleteByKey0(FwpmEngineSafeHandle engineHandle, ref Guid key);

            [LibraryImport("FWPUClnt.dll", EntryPoint = "FwpmFilterAdd0")]
            internal static partial uint FwpmFilterAdd0(FwpmEngineSafeHandle engineHandle, ref Interop.FWPM_FILTER0_NoStrings filter, IntPtr sd, out ulong id);

            [LibraryImport("FWPUClnt.dll", EntryPoint = "FwpmFilterDeleteByKey0")]
            internal static partial uint FwpmFilterDeleteByKey0(FwpmEngineSafeHandle engineHandle, ref Guid key);

            [LibraryImport("FWPUClnt.dll", EntryPoint = "FwpmFilterGetByKey0")]
            internal static partial uint FwpmFilterGetByKey0(FwpmEngineSafeHandle engineHandle, ref Guid key, out FwpmMemorySafeHandle filter);

            [LibraryImport("FWPUClnt.dll", EntryPoint = "FwpmFilterGetById0")]
            internal static partial uint FwpmFilterGetById0(FwpmEngineSafeHandle engineHandle, ulong id, out FwpmMemorySafeHandle filter);
        }

        private readonly FwpmEngineSafeHandle _nativeEngineHandle;

        public FwpmEngineSafeHandle NativePtr
        {

            get
            {
                return _nativeEngineHandle;
            }
        }

        public Engine(string displName, string displDescr, Interop.FWPM_SESSION_FLAGS flags, uint txnTimeoutMsec)
        {
            var session = new Interop.FWPM_SESSION0();
            SessionKey = Guid.NewGuid();
            session.sessionKey = SessionKey;
            session.displayData.name = displName;
            session.displayData.description = displDescr;
            session.flags = flags;
            session.txnWaitTimeoutInMSec = txnTimeoutMsec;

            using var sessionHandle = SafeHGlobalHandle.FromManagedStruct(session);
            uint error = NativeMethods.FwpmEngineOpen0(null, (uint)Interop.RPC_C_AUTHN.RPC_C_AUTHN_WINNT, IntPtr.Zero, sessionHandle.DangerousGetHandle(), out _nativeEngineHandle);
            if (0 != error)
                throw new WfpException(error, "FwpmEngineOpen0");
        }

        private uint EngineOptionGetValue(Interop.FWPM_ENGINE_OPTION opt)
        {
            uint err = NativeMethods.FwpmEngineGetOption0(_nativeEngineHandle, opt, out FwpmMemorySafeHandle nativeMem);
            if (0 != err)
                throw new WfpException(err, "FwpmEngineGetOption0");

            try
            {
                Interop.FWP_VALUE0 val = PInvokeHelper.PtrToStructure<Interop.FWP_VALUE0>(nativeMem.DangerousGetHandle());
                System.Diagnostics.Debug.Assert(val.type == Interop.FWP_DATA_TYPE.FWP_UINT32);
                return val.value.uint32;
            }
            finally
            {
                nativeMem.Dispose();
            }
        }

        private void EngineOptionSetValue(Interop.FWPM_ENGINE_OPTION opt, uint val)
        {
            var vs = new Interop.FWP_VALUE0();
            vs.type = Interop.FWP_DATA_TYPE.FWP_UINT32;
            vs.value.uint32 = val;

            uint err = NativeMethods.FwpmEngineSetOption0(_nativeEngineHandle, opt, ref vs);
            if (0 != err)
                throw new WfpException(err, "FwpmEngineSetOption0");
        }

        public bool CollectNetEvents
        {
            get
            {
                uint val = EngineOptionGetValue(Interop.FWPM_ENGINE_OPTION.FWPM_ENGINE_COLLECT_NET_EVENTS);
                return (val != 0);
            }
            set
            {
                EngineOptionSetValue(Interop.FWPM_ENGINE_OPTION.FWPM_ENGINE_COLLECT_NET_EVENTS, value ? 1u : 0u);
            }
        }

        public Interop.InboundEventMatchKeyword EventMatchAnyKeywords
        {
            get
            {
                return (Interop.InboundEventMatchKeyword)EngineOptionGetValue(Interop.FWPM_ENGINE_OPTION.FWPM_ENGINE_NET_EVENT_MATCH_ANY_KEYWORDS);
            }
            set
            {
                EngineOptionSetValue(Interop.FWPM_ENGINE_OPTION.FWPM_ENGINE_NET_EVENT_MATCH_ANY_KEYWORDS, (uint)value);
            }
        }

        public int TxnWatchdogTimeoutMsec
        {
            get
            {
                return (int)EngineOptionGetValue(Interop.FWPM_ENGINE_OPTION.FWPM_ENGINE_TXN_WATCHDOG_TIMEOUT_IN_MSEC);
            }
            set
            {
                EngineOptionSetValue(Interop.FWPM_ENGINE_OPTION.FWPM_ENGINE_TXN_WATCHDOG_TIMEOUT_IN_MSEC, (uint)value);
            }
        }

        public Guid SessionKey
        {
            get; private set;
        }

        public SessionCollection GetSessions()
        {
            return new SessionCollection(this);
        }

        public ProviderCollection GetProviders()
        {
            return new ProviderCollection(this);
        }

        public SublayerCollection GetSublayers()
        {
            return new SublayerCollection(this);
        }

        public FilterEnumerator EnumerateFilters(bool getFilterConditions)
        {
            return new FilterEnumerator(this, null, getFilterConditions);
        }

        public FilterEnumerator EnumerateFilters(bool getFilterConditions, Guid provider, Guid layer)
        {
            using var providerGuidHandle = SafeHGlobalHandle.FromStruct(provider);
            var template = new Interop.FWPM_FILTER_ENUM_TEMPLATE0
            {
                providerKey = providerGuidHandle.DangerousGetHandle(),
                layerKey = layer,
                flags = Interop.FilterEnumTemplateFlags.FWP_FILTER_ENUM_FLAG_INCLUDE_BOOTTIME | Interop.FilterEnumTemplateFlags.FWP_FILTER_ENUM_FLAG_INCLUDE_DISABLED,
                numFilterConditions = 0,
                actionMask = 0xFFFFFFFFu,
            };

            return new FilterEnumerator(this, template, getFilterConditions);
        }

        public FilterKeyEnumerator EnumerateFilterKeys()
        {
            return new FilterKeyEnumerator(this, null);
        }

        public FilterKeyEnumerator EnumerateFilterKeys(Guid provider, Guid layer)
        {
            using var providerGuidHandle = SafeHGlobalHandle.FromStruct(provider);
            var template = new Interop.FWPM_FILTER_ENUM_TEMPLATE0
            {
                providerKey = providerGuidHandle.DangerousGetHandle(),
                layerKey = layer,
                flags = Interop.FilterEnumTemplateFlags.FWP_FILTER_ENUM_FLAG_INCLUDE_BOOTTIME | Interop.FilterEnumTemplateFlags.FWP_FILTER_ENUM_FLAG_INCLUDE_DISABLED,
                numFilterConditions = 0,
                actionMask = 0xFFFFFFFFu,
            };

            return new FilterKeyEnumerator(this, template);
        }

        public Guid RegisterProvider(ref Interop.FWPM_PROVIDER0 provider)
        {
            if (Guid.Empty == provider.providerKey)
                provider.providerKey = Guid.NewGuid();

            using var providerHandle = SafeHGlobalHandle.FromManagedStruct(provider);
            uint error = NativeMethods.FwpmProviderAdd0(_nativeEngineHandle, providerHandle.DangerousGetHandle(), IntPtr.Zero);
            if (0 != error)
                throw new WfpException(error, "FwpmProviderAdd0");

            return provider.providerKey;
        }

        public Guid RegisterSublayer(Sublayer sublayer)
        {
            if (Guid.Empty == sublayer.SublayerKey)
                sublayer.SublayerKey = Guid.NewGuid();

            var nativeStruct = sublayer.Marshal();

            using var sublayerHandle = SafeHGlobalHandle.FromManagedStruct(nativeStruct);
            uint error = NativeMethods.FwpmSubLayerAdd0(_nativeEngineHandle, sublayerHandle.DangerousGetHandle(), IntPtr.Zero);
            if (0 != error)
                throw new WfpException(error, "FwpmProviderAdd0");

            return sublayer.SublayerKey;
        }

        public void RegisterFilter(Filter filter)
        {
            if (Guid.Empty == filter.FilterKey)
                filter.FilterKey = Guid.NewGuid();

            var nf = filter.Prepare();
            uint err = NativeMethods.FwpmFilterAdd0(_nativeEngineHandle, ref nf, IntPtr.Zero, out ulong id);
            if (0 != err)
                throw new WfpException(err, "FwpmFilterAdd0");

            filter.FilterId = id;
        }

        public void UnregisterProvider(Guid providerKey)
        {
            uint error = NativeMethods.FwpmProviderDeleteByKey0(_nativeEngineHandle, ref providerKey);
            if (0 != error)
                throw new WfpException(error, "FwpmProviderDeleteByKey0");
        }

        public void UnregisterSublayer(Guid subLayerKey)
        {
            uint error = NativeMethods.FwpmSubLayerDeleteByKey0(_nativeEngineHandle, ref subLayerKey);
            if (0 != error)
                throw new WfpException(error, "FwpmProviderDeleteByKey0");
        }

        public void UnregisterFilter(Guid filterKey)
        {
            uint error = NativeMethods.FwpmFilterDeleteByKey0(_nativeEngineHandle, ref filterKey);
            if (0 != error)
                throw new WfpException(error, "FwpmFilterDeleteByKey0");
        }

        public Transaction BeginTransaction(bool readOnly = false)
        {
            return new Transaction(this, readOnly);
        }

        public FilterSubscription SubscribeFilterChange(FilterChangeCallback callback, object context)
        {
            return new FilterSubscription(this, callback, context);
        }

        public FilterSubscription SubscribeFilterChange(FilterChangeCallback callback, object context, Guid providerKey, Guid layerKey)
        {
            return new FilterSubscription(this, callback, context, providerKey, layerKey);
        }

        public NetEventSubscription SubscribeNetEvent(NetEventCallback callback)
        {
            if (VersionInfo.Win8OrNewer)
                return new NetEventSubscription1(this, callback);
            else
                return new NetEventSubscription0(this, callback);
        }

        public Filter GetFilter(Guid guid, bool getConditions)
        {
            uint err = NativeMethods.FwpmFilterGetByKey0(this._nativeEngineHandle, ref guid, out FwpmMemorySafeHandle nativeMem);
            if (err != 0)
                throw new WfpException(err, "FwpmFilterGetByKey0");

            try
            {
                var nativeFilter = PInvokeHelper.PtrToStructure<Interop.FWPM_FILTER0_NoStrings>(nativeMem.DangerousGetHandle());
                return new Filter(in nativeFilter, getConditions);
            }
            finally
            {
                nativeMem.Dispose();
            }
        }

        public Filter GetFilter(ulong id, bool getConditions)
        {
            uint err = NativeMethods.FwpmFilterGetById0(this._nativeEngineHandle, id, out FwpmMemorySafeHandle nativeMem);
            if (err != 0)
                throw new WfpException(err, "FwpmFilterGetById0");

            try
            {
                var nativeFilter = PInvokeHelper.PtrToStructure<Interop.FWPM_FILTER0_NoStrings>(nativeMem.DangerousGetHandle());
                return new Filter(in nativeFilter, getConditions);
            }
            finally
            {
                nativeMem.Dispose();
            }
        }

        public void Dispose()
        {
            _nativeEngineHandle.Dispose();
        }
    }
}
