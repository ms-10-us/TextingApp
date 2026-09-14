using System.Runtime.InteropServices;

namespace SessionSetupMicroService.Protocol.SignalFFI
{
    public abstract class SignalHandle : SafeHandle
    {
        protected SignalHandle() : base (IntPtr.Zero, ownsHandle: true)
        {

        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        public IntPtr Borrow()
        {
            return IsInvalid ? throw new ObjectDisposedException(GetType().Name) : handle;
        }

        public void Adopt(IntPtr value)
        {
            SetHandle(value);
        }
    }
}
