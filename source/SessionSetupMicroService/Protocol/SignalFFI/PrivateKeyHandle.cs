namespace SessionSetupMicroService.Protocol.SignalFFI
{
    public class PrivateKeyHandle : SignalHandle
    {
        protected override bool ReleaseHandle() { NativeMethods.signal_privatekey_destroy(handle); return true; }
    }
}
