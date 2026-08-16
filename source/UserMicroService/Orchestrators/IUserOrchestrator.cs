using Microsoft.AspNetCore.Mvc;
using System.ClientModel.Primitives;
using UserMicroService.Models;

namespace UserMicroService.Orchestrators
{
    public interface IUserOrchestrator
    {
        Task<OperationResult<UserModel>> AddUserAsync(AddUserModel model, CancellationToken cancellationToken);

        Task<OperationResult<UserModel>> GetUserAsync(Guid userId, CancellationToken cancellationToken);
    }
}
