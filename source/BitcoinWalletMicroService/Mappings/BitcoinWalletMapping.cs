using BitcoinWalletMicroService.Dtos;
using BitcoinWalletMicroService.Entities;
using BitcoinWalletMicroService.Models;

namespace BitcoinWalletMicroService.Mappings
{
    public static class BitcoinWalletMapping
    {
        public static CreateWalletModel ToModel(this CreateWalletDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            return new CreateWalletModel
            {
                Label = dto.Label,
                Strength = dto.Strength,
                Network = dto.Network,
                AddressType = dto.AddressType,
                Passphrase = dto.Passphrase,
                InitialAddressCount = dto.InitialAddressCount,
            };
        }

        public static DerivedKeyEntity ToEntity(this DerivedKeyResult key, string walletId, DateTime? createdUtc = null)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentException.ThrowIfNullOrWhiteSpace(walletId);

            return new DerivedKeyEntity
            {
                WalletId = walletId,
                IsChange = key.IsChange ? 1: 0,
                AddressIndex = key.AddressIndex,
                DerivationPath = key.DerivationPath,
                PublicKeyHex = key.PublicKeyHex,
                Address = key.Address,
                CreatedUtc = createdUtc ?? DateTime.UtcNow
            };
        }


    }
}
