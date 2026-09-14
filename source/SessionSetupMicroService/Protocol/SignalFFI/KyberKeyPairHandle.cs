namespace SessionSetupMicroService.Protocol.SignalFFI
{
    public class KyberKeyPairHandle : SignalHandle
    {
        protected override bool ReleaseHandle() { NativeMethods.signal_kyber_key_pair_destroy(handle); return true; }
    }
}
