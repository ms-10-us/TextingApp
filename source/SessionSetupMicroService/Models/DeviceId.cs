namespace SessionSetupMicroService.Models
{
    public readonly record struct DeviceId
    {
        public int Value { get; }

        public const int Primary = 1;

        public DeviceId(int value)
        {
            if (value < Primary)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Device ids start at 1.");
            }

            Value = value;
        }

        public override string ToString() => Value.ToString();
    }
}
