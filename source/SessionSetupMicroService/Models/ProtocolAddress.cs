namespace SessionSetupMicroService.Models
{
    public readonly record struct ProtocolAddress(AccountId Account, DeviceId Device)
    {
        public override string ToString() => $"{Account}.{Device}";

        public static bool TryParse(string? text, out ProtocolAddress address)
        {
            address = default;
            
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var seperator = text.LastIndexOf('.');
            if (seperator <= 0 || seperator == text.Length - 1)
            {
                return false;
            }

            if (!AccountId.TryParse(text[..seperator], out var account))
            {
                return false;
            }

            if (!int.TryParse(text[(seperator + 1)..],out var device) || device < DeviceId.Primary)
            {
                return false;
            }

            address = new ProtocolAddress(account, new DeviceId(device));
            return true;
        }

        public static ProtocolAddress Parse(string text) =>
       TryParse(text, out var address) ? address : throw new FormatException($"Not a protocol address: '{text}'.");
    }
}
