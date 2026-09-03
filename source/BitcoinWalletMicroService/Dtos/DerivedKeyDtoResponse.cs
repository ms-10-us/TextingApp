namespace BitcoinWalletMicroService.Dtos
{
    public class DerivedKeyDtoResponse
    {
        public bool IsChange { get; set; }

        public string Address { get; set; } = string.Empty;
    }
}
