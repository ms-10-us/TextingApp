using SessionSetupMicroService.Enums;

namespace SessionSetupMicroService.OperationResult
{
    public sealed record SessionSetupError(SessionSetupErrorCode Code, string Message)
    {
        public static SessionSetupError DeviceNotFound(object address) =>
        new(SessionSetupErrorCode.DeviceNotFound, $"No device is registered at {address}.");

        public static SessionSetupError InvalidKeyMaterial(string why) =>
            new(SessionSetupErrorCode.InvalidKeyMaterial, why);

        public static SessionSetupError PoolLimitExceeded(int limit) =>
            new(SessionSetupErrorCode.PreKeyPoolLimitExceeded, $"Upload would exceed the maximum pool size of {limit}.");

        public static SessionSetupError NothingToPublish() =>
            new(SessionSetupErrorCode.NothingToPublish, "The request contained no keys to publish.");

        public static SessionSetupError Unauthorized() =>
            new(SessionSetupErrorCode.Unauthorized, "The device credential is missing or does not match.");
    }
}
