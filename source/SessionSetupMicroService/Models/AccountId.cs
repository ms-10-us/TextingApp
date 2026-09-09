namespace SessionSetupMicroService.Models
{
    public readonly record struct AccountId(Guid Value)
    {
        public static AccountId New() => new(Guid.CreateVersion7());

        public override string ToString() => Value.ToString("N");

        public static bool TryParse(string? text, out AccountId account)
        {
            if (Guid.TryParseExact(text, "N", out var guid) || Guid.TryParse(text, out guid))
            {
                account = new AccountId(guid);
                return true;
            }

            account = default;
            return false;
        }
    }
}
