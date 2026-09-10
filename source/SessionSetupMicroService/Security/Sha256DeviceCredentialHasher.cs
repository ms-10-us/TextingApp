namespace SessionSetupMicroService.Security
{
    public class Sha256DeviceCredentialHasher : IDeviceCredentialHasher
    {
        public string Generate()
        {
            throw new NotImplementedException();
        }

        public byte[] Hash(string credential)
        {
            throw new NotImplementedException();
        }

        public bool Verify(string credential, byte[] storedHash)
        {
            throw new NotImplementedException();
        }
    }
}
