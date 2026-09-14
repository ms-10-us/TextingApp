namespace SessionSetupMicroService.Protocol.SignalFFI
{
    public class SignalFfiException : Exception
    {
        public int ErrorCode { get; }

        public SignalFfiException(int errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}
