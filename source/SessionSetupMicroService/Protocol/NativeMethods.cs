using SessionSetupMicroService.Protocol.SignalFFI;
using System.Runtime.InteropServices;

namespace SessionSetupMicroService.Protocol
{
    public static partial class NativeMethods
    {
        internal const string Library = "signal_ffi";

        /// <summary>
        /// Registers the library resolver before any P/Invoke here can run. A static constructor is the
        /// right hook for a library: a module initializer would run at assembly load, which is both too
        /// early and a code-analysis violation outside application code.
        /// </summary>
        static NativeMethods() => NativeLibraryResolver.Register();

        // --- errors --------------------------------------------------------------

        [LibraryImport(Library)]
        internal static partial int signal_error_get_type(IntPtr error);

        [LibraryImport(Library)]
        internal static partial IntPtr signal_error_get_message(IntPtr error, ref IntPtr outMessage);

        [LibraryImport(Library)]
        internal static partial void signal_error_free(IntPtr error);

        // --- memory --------------------------------------------------------------

        [LibraryImport(Library)]
        internal static partial void signal_free_buffer(IntPtr buffer, nuint length);

        [LibraryImport(Library)]
        internal static partial void signal_free_string(IntPtr text);

        // --- destructors ---------------------------------------------------------

        [LibraryImport(Library)]
        internal static partial void signal_privatekey_destroy(IntPtr handle);

        [LibraryImport(Library)]
        internal static partial void signal_publickey_destroy(IntPtr handle);

        [LibraryImport(Library)]
        internal static partial void signal_kyber_key_pair_destroy(IntPtr handle);

        [LibraryImport(Library)]
        internal static partial void signal_pre_key_bundle_destroy(IntPtr handle);

        // --- curve keys (the classical half of PQXDH) ----------------------------

        [LibraryImport(Library)]
        internal static partial IntPtr signal_privatekey_generate(out IntPtr outKey);

        [LibraryImport(Library)]
        internal static partial IntPtr signal_privatekey_get_public_key(out IntPtr outPublicKey, IntPtr privateKey);

        [LibraryImport(Library)]
        internal static partial IntPtr signal_privatekey_serialize(out SignalOwnerBuffer outBuffer, IntPtr privateKey);

        [LibraryImport(Library)]
        internal static partial IntPtr signal_publickey_serialize(out SignalOwnerBuffer outBuffer, IntPtr publicKey);

        [LibraryImport(Library)]
        internal static partial IntPtr signal_publickey_deserialize(out IntPtr outKey, IntPtr data, nuint length);
    }
}
