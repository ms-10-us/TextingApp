using System.Security.Cryptography;
using System.Text;

namespace SessionSetupMicroService.Security
{
    public class Sha256DeviceCredentialHasher : IDeviceCredentialHasher
    {
        public string Generate()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        }

        public byte[] Hash(string credential)
        {
            return SHA256.HashData(Encoding.UTF8.GetBytes(credential));
        }

        public bool Verify(string credential, byte[] storedHash)
        {
            return CryptographicOperations.FixedTimeEquals(Hash(credential), storedHash);
        }
    }
}
