using SessionSetupMicroService.Dtos;
using System.Runtime.CompilerServices;

namespace SessionSetupMicroService.Models
{
    public class KeyPair
    {
        public required string Algorithm { get; set; }

        public required byte[] PublicKey { get; set; }

        public required byte[] PrivateKey { get; set; }
    }
}
