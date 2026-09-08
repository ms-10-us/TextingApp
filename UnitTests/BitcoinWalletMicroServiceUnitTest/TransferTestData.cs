using BitcoinWalletMicroService.Entities;
using BitcoinWalletMicroService.Enums;
using BitcoinWalletMicroService.Models;

namespace BitcoinWalletMicroServiceUnitTest.TestSupport
{
    /// <summary>
    /// Builders for the transfer tests. Centralised so a change to a required property
    /// breaks in one place rather than across every file.
    /// </summary>
    internal static class TransferTestData
    {
        public const string WalletId = "9f1c2e44-0a7b-4d3e-9c11-5b6a7d8e9f01";
        public const string DepositId = "3a7d1b90-64c2-4f18-8e05-1d9b2c7a4e63";
        public const string Address = "tb1qcr8te4kr609gcawutmrza0j4xv80jy8z306fyu";
        public const string ToAddress = "tb1qh6lu4fn809hf6wpqtdeu5hm0l88d6zkul8f2wc";
        public const string AccountXpub = "vpub5YCTfBJVDNVLGqZmXKhcHu5vAsRUUyVJTZHqCLKQjxLoCLE6ZrEuFrDLnQrDy";
        public const string IdempotencyKey = "idem-key-0001";

        public static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

        public static WalletEntity Wallet(string network = "TestNet") => new()
        {
            Id = WalletId,
            Label = "Test Wallet",
            Network = network,
            EncryptedMnemonic = [1, 2, 3, 4],
            MnemonicFingerprint = "fingerprint-abc",
            AccountExtendedPublicKey = AccountXpub,
            AccountDerivationPath = "m/84'/1'/0'",
            CreatedUtc = Now
        };

        public static DerivedKeyEntity KeyEntity(int index = 0, bool isChange = false) => new()
        {
            WalletId = WalletId,
            IsChange = isChange,
            AddressIndex = index,
            DerivationPath = $"m/84'/1'/0'/{(isChange ? 1 : 0)}/{index}",
            PublicKeyHex = "02a1b2c3d4e5f6",
            Address = $"{Address}{index}",
            CreatedUtc = Now
        };

        public static DerivedKeyResult KeyResult(int index = 0, bool isChange = false) => new()
        {
            AddressIndex = index,
            IsChange = isChange,
            DerivationPath = $"m/84'/1'/0'/{(isChange ? 1 : 0)}/{index}",
            PublicKeyHex = "02a1b2c3d4e5f6",
            Address = $"{Address}{index}",
            PrivateKeyWif = "cQ7xLmT9vN2pR4sK8jH6wY1zA3bC5dE7fG9hJ0kL2mN4oP6qR8sT"
        };

        public static Utxo Utxo(
            long valueSats = 100_000,
            bool isConfirmed = true,
            int addressIndex = 0,
            int? blockHeight = 2_500_000) => new()
            {
                TxId = "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90",
                Vout = 0,
                ValueSats = valueSats,
                Address = $"{Address}{addressIndex}",
                IsChange = false,
                AddressIndex = addressIndex,
                IsConfirmed = isConfirmed,
                BlockHeight = isConfirmed ? blockHeight : null
            };

        public static AddressStats Stats(
            long confirmed = 0,
            long unconfirmed = 0,
            string? address = null) => new()
            {
                Address = address ?? Address,
                ConfirmedReceivedSats = confirmed,
                UnconfirmedReceivedSats = unconfirmed,
                ConfirmedTxCount = confirmed > 0 ? 1 : 0,
                UnconfirmedTxCount = unconfirmed > 0 ? 1 : 0
            };

        public static BuiltTransaction Built() => new()
        {
            RawHex = "0200000000010112ab...",
            TxId = "f0e1d2c3b4a5968778695a4b3c2d1e0ff0e1d2c3b4a5968778695a4b3c2d1e0f",
            FeeSats = 250,
            AmountSats = 50_000,
            ChangeSats = 49_750,
            VirtualSizeBytes = 141,
            InputCount = 1
        };

        public static SendTransactionEntity SendEntity() => new()
        {
            Id = 1,
            TxId = "f0e1d2c3b4a5968778695a4b3c2d1e0ff0e1d2c3b4a5968778695a4b3c2d1e0f",
            WalletId = WalletId,
            IdempotencyKey = IdempotencyKey,
            ToAddress = ToAddress,
            AmountSats = 50_000,
            FeeSats = 250,
            VirtualSizeBytes = 141,
            InputCount = 1,
            ChangeAddress = $"{Address}1",
            ChangeSats = 49_750,
            RawTransactionHex = "0200000000010112ab...",
            BroadcastUtc = Now
        };

        public static DepositEntity DepositEntity(
            string status = "Pending",
            long? expectedSats = 50_000,
            long receivedSats = 0,
            DateTime? expiresUtc = null) => new()
            {
                Id = 1,
                DepositId = DepositId,
                WalletId = WalletId,
                Address = Address,
                AddressIndex = 0,
                IsChange = false,
                ExpectedSats = expectedSats,
                Label = "Invoice 1234",
                ReceivedSats = receivedSats,
                Status = status,
                TxId = null,
                CreatedUtc = Now,
                ExpiresUtc = expiresUtc ?? Now.AddDays(1),
                ConfirmedUtc = null
            };

        public static SendModel SendModel(
            bool dryRun = true,
            long amountSats = 50_000,
            string? idempotencyKey = null,
            bool sweepAll = false) => new()
            {
                WalletId = WalletId,
                ToAddress = ToAddress,
                AmountSats = amountSats,
                Passphrase = string.Empty,
                FeeSats = 5m,
                SweepAll = sweepAll,
                DryRun = dryRun,
                IdempotencyKey = idempotencyKey
            };

        public static CreateDepositModel DepositModel(long? expectedSats = 50_000) => new()
        {
            WalletId = WalletId,
            ExpectedSats = expectedSats,
            Label = "Invoice 1234",
            ExpiryMinutes = 1440
        };

        public static SendResult SendResult() => new()
        {
            TxId = null,
            RawTransactionHex = "0200000000010112ab...",
            AmountSats = 50_000,
            FeeSats = 250,
            FeeRateSatsPerVByte = 5m,
            VirtualSizeBytes = 141,
            ToAddress = ToAddress,
            ChangeAddress = $"{Address}1",
            ChangeSats = 49_750,
            InputCount = 1,
            Broadcast = false,
            WasIdempotentReplay = false
        };

        public static DepositResult DepositResult(DepositStatus status = DepositStatus.Pending) => new()
        {
            DepositId = DepositId,
            WalletId = WalletId,
            Address = Address,
            PaymentUri = $"bitcoin:{Address}?amount=0.0005",
            ExpectedSats = 50_000,
            ReceivedSats = 0,
            UnconfirmedSats = 0,
            Status = status,
            Confirmations = 0,
            TxId = null,
            CreatedUtc = Now,
            ExpiresUtc = Now.AddDays(1),
            ConfirmedUtc = null
        };

        public static ReceiveAddressResult ReceiveAddress() => new()
        {
            Address = Address,
            DerivationPath = "m/84'/1'/0'/0/0",
            AddressIndex = 0,
            IsChange = false,
            PaymentUri = $"BITCOIN:{Address.ToUpperInvariant()}"
        };

        public static WalletBalance Balance(long confirmed = 100_000, long unconfirmed = 0) => new()
        {
            WalletId = WalletId,
            ConfirmedSats = confirmed,
            UncofirmedSats = unconfirmed,
            TotalSats = confirmed + unconfirmed,
            UtxoCount = 1
        };
    }
}