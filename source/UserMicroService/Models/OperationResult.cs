namespace UserMicroService.Models
{
    public sealed class OperationResult<T>
    {
        public OperationStatus Status { get; }

        public T? Value { get; }

        public IReadOnlyCollection<OperationError> Errors { get; }

        public bool Succeeded => Status == OperationStatus.Success;

        private OperationResult(OperationStatus status, T? value, IReadOnlyCollection<OperationError> errors)
        {
            Status = status;
            Value = value;
            Errors = errors; 
        }

        public static OperationResult<T> Success(T value)
        {
            return new OperationResult<T>(OperationStatus.Success, value, []);
        }

        public static OperationResult<T> Failure(OperationStatus status, params OperationError[] errors)
        {
            return new OperationResult<T>(status, default, errors);
        }

        public static OperationResult<T> Failure(OperationStatus status, IReadOnlyCollection<OperationError> errors)
        {
            return new OperationResult<T>(status, default, errors);
        }

        public static OperationResult<T> Conflict(string code, string description)
        {
            return Failure(OperationStatus.Conflict, new OperationError(code, description));
        }

        public static OperationResult<T> ValidationFailed(string code, string description)
        {
            return Failure(OperationStatus.ValidationFailed, new OperationError(code, description));
        }

        public static OperationResult<T> NotFound(string code, string description)
        {
            return Failure(OperationStatus.NotFound, new OperationError(code, description));
        }

        public static OperationResult<T> Unauthorized(string code, string description)
        {
            return Failure(OperationStatus.Unauthorized, new OperationError(code, description));
        }

        public static OperationResult<T> Forbidden(string code, string description)
        {
            return Failure(OperationStatus.Forbidden, new OperationError(code, description));
        }

        public OperationResult<TOther> ToFailure<TOther>()
        {
            if (Succeeded)
            {
                throw new InvalidOperationException("A successful result cannot be converted into a failure.");
            }

            return OperationResult<TOther>.Failure(Status, Errors);
        }
    }
}
