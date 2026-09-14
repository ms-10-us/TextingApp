using SessionSetupMicroService.Dtos;
using SessionSetupMicroService.Models;

namespace SessionSetupMicroService.Protocol
{
    public interface ISessionSetupProtocol
    {
        GeneratedSignedPreKey GenerateDeviceKeys(int oneTimePreKeyCount);

        bool VerifyBundle(PreKeyBundleResponse bundle);

        PqxdhResult InitiateSession(GeneratedDeviceKeys initiator, PreKeyBundleResponse bundle);
    }
}
