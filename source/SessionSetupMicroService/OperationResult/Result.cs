namespace SessionSetupMicroService.OperationResult
{
    public readonly record struct Result<T>
    {
        public T? Value { get; }
        public SessionSetupError? Error { get; }

        private Result(T? value, SessionSetupError? error)
        {
            Value = value;
            Error = error;
        }

        public bool IsSuccess => Error is null;

        public static Result<T> Success(T value) => new(value, null);
        public static Result<T> Failure(SessionSetupError error) => new(default, error);

        public static implicit operator Result<T>(SessionSetupError error) => Failure(error);

        public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<SessionSetupError, TOut> onFailure) =>
            IsSuccess ? onSuccess(Value!) : onFailure(Error!);
    }
}
