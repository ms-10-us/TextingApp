namespace SessionSetupMicroService.Protocol.SignalFFI
{
    public class PublicKeyHandle : SignalHandle
    {
        protected override bool ReleaseHandle() { NativeMethods.signal_publickey_destroy(handle); return true; }
    }
}
