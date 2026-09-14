using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing.Matching;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SessionSetupMicroService.Protocol
{
    public class NativeLibraryResolver
    {
        private const string PathVariable = "SIGNAL_FFI_PATH";
        private static int _registered;

        public static void Register()
        {
            if (Interlocked.Exchange(ref _registered, 1) != 0)
            {
                return;
            }

            NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, Resolve);
        }

        private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (name != NativeMethods.Library)
            {
                return IntPtr.Zero;
            }

            foreach(var candidate in Candidates())
            {
                if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out var handle))
                {
                    return handle;
                }
            }

            return NativeLibrary.TryLoad(name, assembly, searchPath, out var fallback)
                    ? fallback
                    : throw new DllNotFoundException(
                        $"Could not load '{name}'. Build it with source/SessionSetupMicroService/Protocol/build/build-native.sh, " +
                        $"or set {PathVariable} to the directory holding the compiled libsignal FFI library.");
        }

        private static IEnumerable<string> Candidates()
        {
            var fileName = OperatingSystem.IsWindows() ? "signal_ffi.dll"
                : OperatingSystem.IsMacOS() ? "libsignal_ffi.dylib"
                : "libsignal_ffi.so";

            if (Environment.GetEnvironmentVariable(PathVariable) is { Length: > 0} configured)
            {
                yield return Path.Combine(configured, fileName);
            }

            yield return Path.Combine(AppContext.BaseDirectory, fileName);
            yield return Path.Combine(AppContext.BaseDirectory, "runtimes", RuntimeIdentifier(), "native", fileName);
        }

        private static string RuntimeIdentifier()
        {
            var os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
            return $"{os}-{RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}";
        }
    }
}
