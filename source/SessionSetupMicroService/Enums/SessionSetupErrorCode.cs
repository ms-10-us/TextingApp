namespace SessionSetupMicroService.Enums
{
    public enum SessionSetupErrorCode
    {
        None = 0,
        DeviceNotFound,
        DeviceAlreadyRegistered,
        InvalidKeyMaterial,
        PreKeyPoolLimitExceeded,
        NothingToPublish,
        Unauthorized
    }
}
