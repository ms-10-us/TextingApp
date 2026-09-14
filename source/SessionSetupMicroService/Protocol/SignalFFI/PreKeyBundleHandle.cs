namespace SessionSetupMicroService.Protocol.SignalFFI
{
    public class PreKeyBundleHandle : SignalHandle
    {
        protected override bool ReleaseHandle() { NativeMethods.signal_pre_key_bundle_destroy(handle); return true; }
    }
}
