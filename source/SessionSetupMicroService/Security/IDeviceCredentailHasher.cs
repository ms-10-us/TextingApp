namespace SessionSetupMicroService.Security
{
    public interface IDeviceCredentialHasher
    {
        public string Generate();

        public byte[] Hash(string credential);

        public bool Verify(string credential, byte[] storedHash);
    }
}
