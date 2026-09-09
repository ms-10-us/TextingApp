namespace SessionSetupMicroService.Models
{
    public readonly record struct PreKeyId
    {
        public long Value { get; }

        public PreKeyId(long value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Prekey ids must not be negative.");
            }

            Value = value;
        }

        public override string ToString() => Value.ToString();
    }
}
