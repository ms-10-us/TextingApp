namespace UserMicroService.Models
{
    public enum CredentialCheckResult
    {
        Succeeded = 0,
        InvalidPassword = 1,
        LockedOut = 2,
        NotAllowed = 3
    }
}
