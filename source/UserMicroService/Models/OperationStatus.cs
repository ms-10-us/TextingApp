namespace UserMicroService.Models
{
    public enum OperationStatus
    {
        Success = 0,
        ValidationFailed = 1,
        Conflict = 2,
        NotFound = 3,
        Unexpected = 4,
        Unauthorized = 5,
        Forbidden = 6
    }
}
