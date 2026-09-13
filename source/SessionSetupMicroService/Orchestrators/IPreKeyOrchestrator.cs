using SessionSetupMicroService.Models;
using SessionSetupMicroService.OperationResult;

namespace SessionSetupMicroService.Orchestrators
{
    public interface IPreKeyOrchestrator
    {
        Task<Result<PreKeyBundle>> TakeBundleAsync(ProtocolAddress protocolAddress, CancellationToken ct = default);

        Task<Result<PreKeyInventory>> GetInventoryAsync(ProtocolAddress address, CancellationToken ct = default);
    }
}
