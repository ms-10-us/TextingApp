using BitcoinWalletMicroService.Entities;
using BitcoinWalletMicroService.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace BitcoinWalletMicroServiceUnitTest
{
    internal static class TestData
    {
        public const string WalletId = "9f1c2e44-0a7b-4d3e-9c11-5b6a7d8e9f01";

        public const string ValidMnemonic =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

        public const string TestnetAddress = "tb1qcr8te4kr609gcawutmrza0j4xv80jy8z306fyu";

        public static DerivedKeyResult KeyResult(int index = 0, bool isChange = false) => new()
        {
            AddressIndex = index,
            IsChange = isChange,
            DerivationPath = $"m/84'/1'/0'/{(isChange ? 1 : 0)}/{index}",
            PublicKeyHex = "02a1b2c3d4e5f6",
            Address = $"{TestnetAddress}{index}",
            PrivateKeyWif = null
        };

        public static WalletSummary Summary(string? id = null) => new()
        {
            WalletId = id ?? WalletId,
            Label = "Test Wallet",
            Network = "TestNet",
            AccountExtendedPublicKey = "vpub5Y...",
            AccountDerivationPath = "m/84'/1'/0'",
            AddressCount = 1,
            CreatedUtc = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc)
        };

        public static WalletEntity Wallet(string? id = null) => new()
        {
            Id = id ?? WalletId,
            Label = "Test Wallet",
            Network = "TestNet",
            EncryptedMnemonic = [1, 2, 3, 4],
            MnemonicFingerprint = "fingerprint-abc",
            AccountExtendedPublicKey = "vpub5Y...",
            AccountDerivationPath = "m/84'/1'/0'",
            CreatedUtc = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc)
        };

        public static DerivedKeyEntity KeyEntity(
            int index = 0,
            bool isChange = false,
            string? walletId = null) => new()
            {
                WalletId = walletId ?? WalletId,
                IsChange = isChange,
                AddressIndex = index,
                DerivationPath = $"\"m/84'/1'/0'/{{(isChange ? 1 : 0)}}/{{index}}",
                PublicKeyHex = "02a1b2c3d4e5f6",
                Address = $"{TestnetAddress}{index}",
                CreatedUtc = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc)
            };
            
    }
}
