using System.Runtime.InteropServices;

namespace ModbusExt.ProfileEditor;

[ComImport, Guid("00000016-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IOleMessageFilter
{
    [PreserveSig] int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);
    [PreserveSig] int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);
    [PreserveSig] int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
}

sealed class MessageFilter : IOleMessageFilter
{
    [DllImport("ole32.dll")]
    static extern int CoRegisterMessageFilter(IOleMessageFilter? newFilter, out IOleMessageFilter? oldFilter);

    public static void Register() => CoRegisterMessageFilter(new MessageFilter(), out _);
    public static void Revoke()   => CoRegisterMessageFilter(null, out _);

    int IOleMessageFilter.HandleInComingCall(int t, IntPtr c, int tick, IntPtr i) => 0;              // SERVERCALL_ISHANDLED
    int IOleMessageFilter.RetryRejectedCall(IntPtr c, int tick, int reject) => reject == 2 ? 200 : -1; // retry after 200 ms
    int IOleMessageFilter.MessagePending(IntPtr c, int tick, int pending) => 2;                       // PENDINGMSG_WAITDEFPROCESS
}