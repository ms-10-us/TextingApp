using UserMicroService.Models;

namespace UserMicroService.Orchestrators
{
    public interface IAuthOrchestrator
    {
        Task<OperationResult<LoginResultModel>> LoginAsync(LoginModel model, CancellationToken cancellationToken);
    }
}
