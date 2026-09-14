using System.Runtime.InteropServices;

namespace SessionSetupMicroService.Protocol.SignalFFI
{
    public static class SignalFfi
    {
        public static void Check(IntPtr error)
        {
            if (error == IntPtr.Zero)
            {
                return;
            }

            int code;
            string message;
            try
            {
                code = NativeMethods.signal_error_get_type(error);
                var messagePointer = IntPtr.Zero;
                var inner = NativeMethods.signal_error_get_message(error, ref messagePointer);
                message = inner == IntPtr.Zero && messagePointer != IntPtr.Zero
                ? Marshal.PtrToStringUTF8(messagePointer) ?? "libsignal error"
                : "libsignal error (message unavailable)";

                if (messagePointer != IntPtr.Zero)
                {
                    NativeMethods.signal_free_string(messagePointer);
                }
            }
            finally
            {
                NativeMethods.signal_error_free(error);
            }

            throw new SignalFfiException(code, message);
        }

        public static byte[] Consume(SignalOwnerBuffer buffer)
        {
            if (buffer.Base == IntPtr.Zero)
            {
                return [];
            }

            try
            {
                var managed = new byte[checked((int)buffer.Length)];
                Marshal.Copy(buffer.Base, managed, 0, managed.Length);
                return managed;
            }
            finally
            {
                NativeMethods.signal_free_buffer(buffer.Base, buffer.Length);
            }
        }

        public static THandle Own<THandle>(IntPtr raw) where THandle : SignalHandle, new()
        {
            var handle = new THandle();
            handle.Adopt(raw);
            return handle;
        }
    }
}
