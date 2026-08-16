namespace UserMicroService.Models
{
    public sealed class OperationError
    {
        public string Code { get; set; }

        public string Description { get; }

        public OperationError(string code, string description)
        {
            Code = code;
            Description = description;
        }
    }
}
