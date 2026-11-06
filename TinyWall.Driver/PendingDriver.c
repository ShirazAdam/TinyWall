#include <ntifs.h>
#include <wdmsec.h>
#include <initguid.h>
#include <fwpsk.h>
#include <fwpmk.h>
#include "PendingProtocol.h"

DEFINE_GUID(TW_CALLOUT_V4, 0x7b788b57, 0x6009, 0x4dfe, 0x9a, 0xf0, 0x21, 0x66, 0x90, 0xb9, 0x6b, 0x71);
DEFINE_GUID(TW_CALLOUT_V6, 0xd6ff3c55, 0x1924, 0x4361, 0xb5, 0x35, 0xc7, 0x6a, 0x44, 0x33, 0x77, 0x64);
DEFINE_GUID(TW_DEVICE_CLASS, 0x96c2d7f3, 0x39ad, 0x43e4, 0xb2, 0x6a, 0x86, 0x7a, 0x5a, 0x75, 0x6a, 0x39);

#define TW_TAG 'pWTT'
#define TW_SECONDS(s) ((UINT64)(s) * 10000000)

typedef struct TW_ENTRY {
    LIST_ENTRY Link;
    TW_PENDING_REQUEST Request;
    UCHAR LocalAddress[16];
    HANDLE Completion;
    NET_BUFFER_LIST* Clone;
    UINT64 Endpoint;
    COMPARTMENT_ID Compartment;
    FWPS_TRANSPORT_SEND_PARAMS0 Send;
    UINT64 Expires;
    BOOLEAN Ready;
    BOOLEAN Allow;
    BOOLEAN Completing;
    BOOLEAN Completed;
} TW_ENTRY;

typedef struct TW_SEND {
    FWPS_TRANSPORT_SEND_PARAMS0 Parameters;
    UCHAR RemoteAddress[16];
} TW_SEND;

static PDEVICE_OBJECT Device;
static HANDLE Engine;
static HANDLE InjectionV4;
static HANDLE InjectionV6;
static UINT32 CalloutV4;
static UINT32 CalloutV6;
static KSPIN_LOCK Guard;
static LIST_ENTRY Entries;
static ULONG EntryCount;
static UINT64 NextId;
static UINT64 Heartbeat;
static BOOLEAN Stopping;
static KEVENT Wake;
static HANDLE Worker;

DRIVER_INITIALIZE DriverEntry;
DRIVER_UNLOAD Unload;
DRIVER_DISPATCH Dispatch;

static void FreeEntry(TW_ENTRY* entry)
{
    if (entry->Clone != NULL)
        FwpsFreeCloneNetBufferList0(entry->Clone, 0);
    if (entry->Send.controlData != NULL)
        ExFreePoolWithTag(entry->Send.controlData, TW_TAG);
    ExFreePoolWithTag(entry, TW_TAG);
}

static void NTAPI InjectComplete(void* context, NET_BUFFER_LIST* list, BOOLEAN dispatchLevel)
{
    TW_SEND* send = context;
    UNREFERENCED_PARAMETER(dispatchLevel);
    if (!NT_SUCCESS(NET_BUFFER_LIST_STATUS(list)))
        DbgPrintEx(DPFLTR_IHVNETWORK_ID, DPFLTR_ERROR_LEVEL, "TinyWall UDP injection failed: %08x\n", NET_BUFFER_LIST_STATUS(list));
    FwpsFreeCloneNetBufferList0(list, 0);
    if (send->Parameters.controlData != NULL)
        ExFreePoolWithTag(send->Parameters.controlData, TW_TAG);
    ExFreePoolWithTag(send, TW_TAG);
}

static void Block(FWPS_CLASSIFY_OUT0* result)
{
    result->actionType = FWP_ACTION_BLOCK;
    result->rights &= ~FWPS_RIGHT_ACTION_WRITE;
}

static BOOLEAN ReadTuple(
    const FWPS_INCOMING_VALUES0* values,
    const FWPS_INCOMING_METADATA_VALUES0* metadata,
    TW_ENTRY* entry,
    UINT32* flags)
{
    const BOOLEAN v6 = values->layerId == FWPS_LAYER_ALE_AUTH_CONNECT_V6;
    UINT32 address;
    if (!FWPS_IS_METADATA_FIELD_PRESENT(metadata, FWPS_METADATA_FIELD_PROCESS_ID))
        return FALSE;
    entry->Request.ProcessId = metadata->processId;
    entry->Request.AddressFamily = v6 ? AF_INET6 : AF_INET;
    if (v6) {
        entry->Request.Protocol = values->incomingValue[FWPS_FIELD_ALE_AUTH_CONNECT_V6_IP_PROTOCOL].value.uint8;
        entry->Request.LocalPort = values->incomingValue[FWPS_FIELD_ALE_AUTH_CONNECT_V6_IP_LOCAL_PORT].value.uint16;
        entry->Request.RemotePort = values->incomingValue[FWPS_FIELD_ALE_AUTH_CONNECT_V6_IP_REMOTE_PORT].value.uint16;
        RtlCopyMemory(entry->Request.RemoteAddress, values->incomingValue[FWPS_FIELD_ALE_AUTH_CONNECT_V6_IP_REMOTE_ADDRESS].value.byteArray16->byteArray16, 16);
        RtlCopyMemory(entry->LocalAddress, values->incomingValue[FWPS_FIELD_ALE_AUTH_CONNECT_V6_IP_LOCAL_ADDRESS].value.byteArray16->byteArray16, 16);
        *flags = values->incomingValue[FWPS_FIELD_ALE_AUTH_CONNECT_V6_FLAGS].value.uint32;
    } else {
        entry->Request.Protocol = values->incomingValue[FWPS_FIELD_ALE_AUTH_CONNECT_V4_IP_PROTOCOL].value.uint8;
        entry->Request.LocalPort = values->incomingValue[FWPS_FIELD_ALE_AUTH_CONNECT_V4_IP_LOCAL_PORT].value.uint16;
        entry->Request.RemotePort = values->incomingValue[FWPS_FIELD_ALE_AUTH_CONNECT_V4_IP_REMOTE_PORT].value.uint16;
        address = RtlUlongByteSwap(values->incomingValue[FWPS_FIELD_ALE_AUTH_CONNECT_V4_IP_REMOTE_ADDRESS].value.uint32);
        RtlCopyMemory(entry->Request.RemoteAddress, &address, sizeof(address));
        address = RtlUlongByteSwap(values->incomingValue[FWPS_FIELD_ALE_AUTH_CONNECT_V4_IP_LOCAL_ADDRESS].value.uint32);
        RtlCopyMemory(entry->LocalAddress, &address, sizeof(address));
        *flags = values->incomingValue[FWPS_FIELD_ALE_AUTH_CONNECT_V4_FLAGS].value.uint32;
    }
    entry->Compartment = FWPS_IS_METADATA_FIELD_PRESENT(metadata, FWPS_METADATA_FIELD_COMPARTMENT_ID)
        ? metadata->compartmentId : UNSPECIFIED_COMPARTMENT_ID;
    entry->Endpoint = FWPS_IS_METADATA_FIELD_PRESENT(metadata, FWPS_METADATA_FIELD_TRANSPORT_ENDPOINT_HANDLE)
        ? metadata->transportEndpointHandle : 0;
    return TRUE;
}

static BOOLEAN SameTuple(const TW_ENTRY* left, const TW_ENTRY* right)
{
    return left->Request.ProcessId == right->Request.ProcessId
        && left->Endpoint == right->Endpoint
        && left->Compartment == right->Compartment
        && left->Request.Protocol == right->Request.Protocol
        && left->Request.AddressFamily == right->Request.AddressFamily
        && left->Request.LocalPort == right->Request.LocalPort
        && left->Request.RemotePort == right->Request.RemotePort
        && RtlCompareMemory(left->Request.RemoteAddress, right->Request.RemoteAddress, 16) == 16
        && RtlCompareMemory(left->LocalAddress, right->LocalAddress, 16) == 16;
}

static void NTAPI Classify(
    const FWPS_INCOMING_VALUES0* values,
    const FWPS_INCOMING_METADATA_VALUES0* metadata,
    void* layerData,
    const FWPS_FILTER0* filter,
    UINT64 flowContext,
    FWPS_CLASSIFY_OUT0* result)
{
    TW_ENTRY tuple = { 0 };
    TW_ENTRY* entry;
    LIST_ENTRY* link;
    KIRQL irql;
    UINT32 flags = 0;
    NTSTATUS status;
    const FWP_BYTE_BLOB* app;
    ULONG appIndex;
    HANDLE injection;
    BOOLEAN reauthorize;
    UNREFERENCED_PARAMETER(filter);
    UNREFERENCED_PARAMETER(flowContext);
    if (!(result->rights & FWPS_RIGHT_ACTION_WRITE))
        return;
    Block(result);
    if (!ReadTuple(values, metadata, &tuple, &flags))
        return;
    injection = tuple.Request.AddressFamily == AF_INET6 ? InjectionV6 : InjectionV4;
    if (layerData != NULL) {
        FWPS_PACKET_INJECTION_STATE state = FwpsQueryPacketInjectionState0(injection, layerData, NULL);
        if (state == FWPS_PACKET_INJECTED_BY_SELF || state == FWPS_PACKET_PREVIOUSLY_INJECTED_BY_SELF) {
            result->actionType = FWP_ACTION_PERMIT;
            return;
        }
    }
    reauthorize = (flags & FWP_CONDITION_FLAG_IS_REAUTHORIZE) != 0;
    KeAcquireSpinLock(&Guard, &irql);
    for (link = Entries.Flink; link != &Entries; link = link->Flink) {
        entry = CONTAINING_RECORD(link, TW_ENTRY, Link);
        if (SameTuple(entry, &tuple)) {
            if (reauthorize && entry->Ready && entry->Allow)
                result->actionType = FWP_ACTION_PERMIT;
            KeReleaseSpinLock(&Guard, irql);
            return;
        }
    }
    if (reauthorize || Stopping || KeQueryInterruptTime() - Heartbeat > TW_SECONDS(10) || EntryCount >= TW_MAX_PENDING) {
        KeReleaseSpinLock(&Guard, irql);
        return;
    }
    ++EntryCount;
    KeReleaseSpinLock(&Guard, irql);
    entry = ExAllocatePool2(POOL_FLAG_NON_PAGED, sizeof(*entry), TW_TAG);
    if (entry == NULL)
        goto failed;
    *entry = tuple;
    appIndex = tuple.Request.AddressFamily == AF_INET6 ? FWPS_FIELD_ALE_AUTH_CONNECT_V6_ALE_APP_ID : FWPS_FIELD_ALE_AUTH_CONNECT_V4_ALE_APP_ID;
    app = values->incomingValue[appIndex].value.byteBlob;
    if (app == NULL || app->size < sizeof(WCHAR) || app->size > sizeof(entry->Request.AppPath) - sizeof(WCHAR)
        || (app->size % sizeof(WCHAR)) != 0 || app->data == NULL)
        goto failed;
    RtlCopyMemory(entry->Request.AppPath, app->data, app->size);
    if (!FWPS_IS_METADATA_FIELD_PRESENT(metadata, FWPS_METADATA_FIELD_COMPLETION_HANDLE))
        goto failed;
    if (entry->Request.Protocol == IPPROTO_UDP) {
        ULONG controlLength = FWPS_IS_METADATA_FIELD_PRESENT(metadata, FWPS_METADATA_FIELD_TRANSPORT_CONTROL_DATA)
            ? metadata->controlDataLength : 0;
        ULONG64 packetBytes = 0;
        NET_BUFFER* buffer;
        if (layerData == NULL || entry->Endpoint == 0
            || controlLength > 4096
            || (FWPS_IS_METADATA_FIELD_PRESENT(metadata, FWPS_METADATA_FIELD_IP_HEADER_SIZE) && metadata->ipHeaderSize > 0))
            goto failed;
        for (buffer = NET_BUFFER_LIST_FIRST_NB((NET_BUFFER_LIST*)layerData); buffer != NULL; buffer = NET_BUFFER_NEXT_NB(buffer)) {
            packetBytes += NET_BUFFER_DATA_LENGTH(buffer);
            if (packetBytes > 65535)
                goto failed;
        }
        entry->Send.remoteAddress = entry->Request.RemoteAddress;
        if (FWPS_IS_METADATA_FIELD_PRESENT(metadata, FWPS_METADATA_FIELD_REMOTE_SCOPE_ID))
            entry->Send.remoteScopeId = metadata->remoteScopeId;
        if (controlLength > 0) {
            if (metadata->controlData == NULL)
                goto failed;
            entry->Send.controlData = ExAllocatePool2(POOL_FLAG_NON_PAGED, controlLength, TW_TAG);
            if (entry->Send.controlData == NULL)
                goto failed;
            entry->Send.controlDataLength = controlLength;
            RtlCopyMemory(entry->Send.controlData, metadata->controlData, controlLength);
        }
        status = FwpsAllocateCloneNetBufferList0(layerData, NULL, NULL, 0, &entry->Clone);
        if (!NT_SUCCESS(status))
            goto failed;
    } else if (entry->Request.Protocol != IPPROTO_TCP || layerData != NULL) {
        goto failed;
    }
    // Publish under the lock only after pend succeeds; a decision cannot race initial classification.
    KeAcquireSpinLock(&Guard, &irql);
    if (Stopping || KeQueryInterruptTime() - Heartbeat > TW_SECONDS(10)) {
        KeReleaseSpinLock(&Guard, irql);
        goto failed;
    }
    for (link = Entries.Flink; link != &Entries; link = link->Flink) {
        TW_ENTRY* other = CONTAINING_RECORD(link, TW_ENTRY, Link);
        if (SameTuple(other, entry)) {
            KeReleaseSpinLock(&Guard, irql);
            goto failed;
        }
    }
    status = FwpsPendOperation0(metadata->completionHandle, &entry->Completion);
    if (!NT_SUCCESS(status)) {
        KeReleaseSpinLock(&Guard, irql);
        goto failed;
    }
    entry->Request.Id = ++NextId;
    entry->Expires = KeQueryInterruptTime() + TW_SECONDS(120);
    InsertTailList(&Entries, &entry->Link);
    result->flags |= FWPS_CLASSIFY_OUT_FLAG_ABSORB;
    KeReleaseSpinLock(&Guard, irql);
    return;
failed:
    if (entry != NULL)
        FreeEntry(entry);
    KeAcquireSpinLock(&Guard, &irql);
    --EntryCount;
    KeReleaseSpinLock(&Guard, irql);
}

static NTSTATUS NTAPI Notify(FWPS_CALLOUT_NOTIFY_TYPE type, const GUID* key, FWPS_FILTER0* filter)
{
    UNREFERENCED_PARAMETER(type);
    UNREFERENCED_PARAMETER(key);
    UNREFERENCED_PARAMETER(filter);
    return STATUS_SUCCESS;
}

static void CompleteEntry(TW_ENTRY* entry)
{
    NTSTATUS status;
    FwpsCompleteOperation0(entry->Completion, entry->Clone);
    entry->Completion = NULL;
    if (entry->Clone != NULL && entry->Allow) {
        TW_SEND* send = ExAllocatePool2(POOL_FLAG_NON_PAGED, sizeof(*send), TW_TAG);
        if (send != NULL) {
            send->Parameters = entry->Send;
            RtlCopyMemory(send->RemoteAddress, entry->Request.RemoteAddress, 16);
            send->Parameters.remoteAddress = send->RemoteAddress;
            // Injection may outlive the pending request; the callback owns its parameters.
            entry->Send.controlData = NULL;
            status = FwpsInjectTransportSendAsync0(
                entry->Request.AddressFamily == AF_INET6 ? InjectionV6 : InjectionV4,
                NULL, entry->Endpoint, 0, &send->Parameters, (ADDRESS_FAMILY)entry->Request.AddressFamily,
                entry->Compartment, entry->Clone, InjectComplete, send);
            if (NT_SUCCESS(status))
                entry->Clone = NULL;
            else {
                if (send->Parameters.controlData != NULL)
                    ExFreePoolWithTag(send->Parameters.controlData, TW_TAG);
                ExFreePoolWithTag(send, TW_TAG);
                DbgPrintEx(DPFLTR_IHVNETWORK_ID, DPFLTR_ERROR_LEVEL, "TinyWall UDP send failed: %08x\n", status);
            }
        }
    }
    if (entry->Clone != NULL) {
        FwpsFreeCloneNetBufferList0(entry->Clone, 0);
        entry->Clone = NULL;
    }
}

static void WorkerMain(void* context)
{
    LARGE_INTEGER interval;
    UNREFERENCED_PARAMETER(context);
    interval.QuadPart = -1000000;
    for (;;) {
        TW_ENTRY* work = NULL;
        TW_ENTRY* garbage = NULL;
        KIRQL irql;
        LIST_ENTRY* link;
        UINT64 now = KeQueryInterruptTime();
        BOOLEAN done;
        KeAcquireSpinLock(&Guard, &irql);
        for (link = Entries.Flink; link != &Entries; link = link->Flink) {
            TW_ENTRY* entry = CONTAINING_RECORD(link, TW_ENTRY, Link);
            if (entry->Completed && (Stopping || now >= entry->Expires)) {
                RemoveEntryList(link);
                --EntryCount;
                garbage = entry;
                break;
            }
            if (!entry->Completed && !entry->Completing) {
                if (Stopping || now >= entry->Expires || now - Heartbeat > TW_SECONDS(10)) {
                    entry->Ready = TRUE;
                    entry->Allow = FALSE;
                }
                if (entry->Ready) {
                    entry->Completing = TRUE;
                    work = entry;
                    break;
                }
            }
        }
        done = Stopping && IsListEmpty(&Entries);
        KeReleaseSpinLock(&Guard, irql);
        if (garbage != NULL) {
            FreeEntry(garbage);
            continue;
        }
        if (work != NULL) {
            CompleteEntry(work);
            KeAcquireSpinLock(&Guard, &irql);
            work->Completed = TRUE;
            work->Expires = KeQueryInterruptTime() + TW_SECONDS(30);
            KeReleaseSpinLock(&Guard, irql);
            continue;
        }
        if (done)
            break;
        KeWaitForSingleObject(&Wake, Executive, KernelMode, FALSE, &interval);
    }
    PsTerminateSystemThread(STATUS_SUCCESS);
}

NTSTATUS Dispatch(PDEVICE_OBJECT device, PIRP irp)
{
    PIO_STACK_LOCATION stack = IoGetCurrentIrpStackLocation(irp);
    NTSTATUS status = STATUS_SUCCESS;
    ULONG_PTR length = 0;
    KIRQL irql;
    LIST_ENTRY* link;
    UNREFERENCED_PARAMETER(device);
    if (stack->MajorFunction == IRP_MJ_DEVICE_CONTROL) {
        ULONG code = stack->Parameters.DeviceIoControl.IoControlCode;
        if (code == TW_IOCTL_READ && stack->Parameters.DeviceIoControl.OutputBufferLength >= sizeof(TW_READ_RESULT)) {
            TW_READ_RESULT* output = irp->AssociatedIrp.SystemBuffer;
            RtlZeroMemory(output, sizeof(*output));
            output->Version = TW_PROTOCOL_VERSION;
            KeAcquireSpinLock(&Guard, &irql);
            Heartbeat = KeQueryInterruptTime();
            for (link = Entries.Flink; link != &Entries; link = link->Flink) {
                TW_ENTRY* entry = CONTAINING_RECORD(link, TW_ENTRY, Link);
                if (!entry->Ready && !entry->Completed)
                    output->Requests[output->Count++] = entry->Request;
            }
            KeReleaseSpinLock(&Guard, irql);
            length = sizeof(*output);
        } else if (code == TW_IOCTL_DECIDE && stack->Parameters.DeviceIoControl.InputBufferLength == sizeof(TW_DECISION)) {
            TW_DECISION* decision = irp->AssociatedIrp.SystemBuffer;
            status = STATUS_NOT_FOUND;
            if (decision->Version != TW_PROTOCOL_VERSION || decision->Allow > 1) {
                status = STATUS_INVALID_PARAMETER;
            } else {
                KeAcquireSpinLock(&Guard, &irql);
                for (link = Entries.Flink; link != &Entries; link = link->Flink) {
                    TW_ENTRY* entry = CONTAINING_RECORD(link, TW_ENTRY, Link);
                    if (entry->Request.Id == decision->Id && !entry->Ready && !entry->Completed && KeQueryInterruptTime() < entry->Expires) {
                        entry->Allow = decision->Allow != 0;
                        entry->Ready = TRUE;
                        status = STATUS_SUCCESS;
                        break;
                    }
                }
                KeReleaseSpinLock(&Guard, irql);
                KeSetEvent(&Wake, IO_NO_INCREMENT, FALSE);
            }
        } else {
            status = STATUS_INVALID_DEVICE_REQUEST;
        }
    } else if (stack->MajorFunction == IRP_MJ_CLEANUP) {
        KeAcquireSpinLock(&Guard, &irql);
        Heartbeat = 0;
        KeReleaseSpinLock(&Guard, irql);
        KeSetEvent(&Wake, IO_NO_INCREMENT, FALSE);
    }
    irp->IoStatus.Status = status;
    irp->IoStatus.Information = length;
    IoCompleteRequest(irp, IO_NO_INCREMENT);
    return status;
}

static NTSTATUS RegisterCallout(const GUID* key, const GUID* layer, UINT32* id)
{
    FWPS_CALLOUT0 kernel = { 0 };
    FWPM_CALLOUT0 management = { 0 };
    NTSTATUS status;
    kernel.calloutKey = *key;
    kernel.classifyFn = Classify;
    kernel.notifyFn = Notify;
    status = FwpsCalloutRegister0(Device, &kernel, id);
    if (!NT_SUCCESS(status))
        return status;
    management.calloutKey = *key;
    management.applicableLayer = *layer;
    management.displayData.name = L"TinyWall pending outbound authorization";
    status = FwpmCalloutAdd0(Engine, &management, NULL, NULL);
    if (status == STATUS_FWP_ALREADY_EXISTS) {
        FWPM_CALLOUT0* existing = NULL;
        status = FwpmCalloutGetByKey0(Engine, key, &existing);
        if (NT_SUCCESS(status)) {
            if (RtlCompareMemory(&existing->applicableLayer, layer, sizeof(GUID)) != sizeof(GUID))
                status = STATUS_INVALID_PARAMETER;
            FwpmFreeMemory0((void**)&existing);
        }
    }
    if (!NT_SUCCESS(status)) {
        FwpsCalloutUnregisterById0(*id);
        *id = 0;
    }
    return status;
}

void Unload(PDRIVER_OBJECT driver)
{
    UNICODE_STRING link = RTL_CONSTANT_STRING(L"\\DosDevices\\TinyWallPending");
    KIRQL irql;
    UNREFERENCED_PARAMETER(driver);
    KeAcquireSpinLock(&Guard, &irql);
    Stopping = TRUE;
    KeReleaseSpinLock(&Guard, irql);
    KeSetEvent(&Wake, IO_NO_INCREMENT, FALSE);
    if (Worker != NULL) {
        ZwWaitForSingleObject(Worker, FALSE, NULL);
        ZwClose(Worker);
    }
    if (CalloutV4 != 0)
        FwpsCalloutUnregisterById0(CalloutV4);
    if (CalloutV6 != 0)
        FwpsCalloutUnregisterById0(CalloutV6);
    if (Engine != NULL) {
        // In-use management objects may remain, but no executable callout survives unregistration.
        FwpmCalloutDeleteByKey0(Engine, &TW_CALLOUT_V4);
        FwpmCalloutDeleteByKey0(Engine, &TW_CALLOUT_V6);
        FwpmEngineClose0(Engine);
    }
    if (InjectionV4 != NULL)
        FwpsInjectionHandleDestroy0(InjectionV4);
    if (InjectionV6 != NULL)
        FwpsInjectionHandleDestroy0(InjectionV6);
    IoDeleteSymbolicLink(&link);
    if (Device != NULL)
        IoDeleteDevice(Device);
}

NTSTATUS DriverEntry(PDRIVER_OBJECT driver, PUNICODE_STRING registry)
{
    UNICODE_STRING name = RTL_CONSTANT_STRING(L"\\Device\\TinyWallPending");
    UNICODE_STRING link = RTL_CONSTANT_STRING(L"\\DosDevices\\TinyWallPending");
    UNICODE_STRING security = RTL_CONSTANT_STRING(L"D:P(A;;GA;;;SY)(A;;GA;;;BA)");
    FWPM_SESSION0 session = { 0 };
    OBJECT_ATTRIBUTES attributes;
    NTSTATUS status;
    UNREFERENCED_PARAMETER(registry);
    InitializeListHead(&Entries);
    KeInitializeSpinLock(&Guard);
    NextId = KeQueryInterruptTime();
    KeInitializeEvent(&Wake, SynchronizationEvent, FALSE);
    driver->DriverUnload = Unload;
    driver->MajorFunction[IRP_MJ_CREATE] = Dispatch;
    driver->MajorFunction[IRP_MJ_CLOSE] = Dispatch;
    driver->MajorFunction[IRP_MJ_CLEANUP] = Dispatch;
    driver->MajorFunction[IRP_MJ_DEVICE_CONTROL] = Dispatch;
    status = IoCreateDeviceSecure(driver, 0, &name, TW_DEVICE_TYPE, FILE_DEVICE_SECURE_OPEN, TRUE, &security, &TW_DEVICE_CLASS, &Device);
    if (!NT_SUCCESS(status))
        goto failed;
    Device->Flags |= DO_BUFFERED_IO;
    status = IoCreateSymbolicLink(&link, &name);
    if (!NT_SUCCESS(status))
        goto failed;
    status = FwpsInjectionHandleCreate0(AF_INET, FWPS_INJECTION_TYPE_TRANSPORT, &InjectionV4);
    if (!NT_SUCCESS(status))
        goto failed;
    status = FwpsInjectionHandleCreate0(AF_INET6, FWPS_INJECTION_TYPE_TRANSPORT, &InjectionV6);
    if (!NT_SUCCESS(status))
        goto failed;
    // The service's filters live in a separate session and cannot reference dynamic callouts.
    status = FwpmEngineOpen0(NULL, RPC_C_AUTHN_WINNT, NULL, &session, &Engine);
    if (!NT_SUCCESS(status))
        goto failed;
    status = RegisterCallout(&TW_CALLOUT_V4, &FWPM_LAYER_ALE_AUTH_CONNECT_V4, &CalloutV4);
    if (!NT_SUCCESS(status))
        goto failed;
    status = RegisterCallout(&TW_CALLOUT_V6, &FWPM_LAYER_ALE_AUTH_CONNECT_V6, &CalloutV6);
    if (!NT_SUCCESS(status))
        goto failed;
    InitializeObjectAttributes(&attributes, NULL, OBJ_KERNEL_HANDLE, NULL, NULL);
    status = PsCreateSystemThread(&Worker, THREAD_ALL_ACCESS, &attributes, NULL, NULL, WorkerMain, NULL);
    if (!NT_SUCCESS(status))
        goto failed;
    Device->Flags &= ~DO_DEVICE_INITIALIZING;
    return STATUS_SUCCESS;
failed:
    Unload(driver);
    return status;
}
