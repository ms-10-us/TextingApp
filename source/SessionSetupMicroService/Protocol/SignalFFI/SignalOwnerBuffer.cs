using System.Runtime.InteropServices;

namespace SessionSetupMicroService.Protocol.SignalFFI
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SignalOwnerBuffer
    {
        public IntPtr Base;

        public nuint Length;
    }
}
