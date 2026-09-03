using BitcoinWalletMicroService.Dtos;
using BitcoinWalletMicroService.Entities;
using BitcoinWalletMicroService.Models;
using System.Reflection.Emit;

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
                IsChange = key.IsChange == true,
                AddressIndex = key.AddressIndex,
                DerivationPath = key.DerivationPath,
                PublicKeyHex = key.PublicKeyHex,
                Address = key.Address,
                CreatedUtc = createdUtc ?? DateTime.UtcNow
            };
        }

        public static CreateWalletDtoResponse ToDto(this CreateWalletResult model)
        {
            ArgumentNullException.ThrowIfNull(model);

            return new CreateWalletDtoResponse
            {
                WalletId = model.WalletId,
                Label = model.Label,
                Mnemonic = model.Mnemonic,
                AccountExtendedPublicKey = model.AccountExtendedPublicKey,
                AccountDerivationPath = model.AccountDerivationPath,
                Addresses = model.Addresses,
                CreatedUtc = model.CreatedUtc
            };
        }

        public static ImportWalletModel ToModel(this ImportWalletDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            return new ImportWalletModel
            {
                Label = dto.Label,
                Mnemonic = dto.Mnemonic,
                Passphrase = dto.Passphrase,
                Network = dto.Network,
                AddressType = dto.AddressType,
                InitalAddressCount = dto.InitalAddressCount
            };

        }

        public static WalletSummaryDtoResponse ToDto(this WalletSummary model)
        {
            ArgumentNullException.ThrowIfNull(model);

            return new WalletSummaryDtoResponse
            {
                WalletId = model.WalletId,
                Label = model.Label,
                Network = model.Network,
                AccountExtendedPublicKey = model.AccountExtendedPublicKey,
                AccountDerivationPath = model.AccountDerivationPath,
                AddressCount = model.AddressCount,
                CreatedUtc = model.CreatedUtc
            };

        }

        public static WalletSummary ToModel(this WalletSummaryEntity entity)
        {
            ArgumentNullException.ThrowIfNull(entity);

            return new WalletSummary
            {
                WalletId = entity.WalletId,
                Label = entity.Label,
                Network = entity.Network,
                AccountExtendedPublicKey = entity.AccountExtendedPublicKey,
                AccountDerivationPath= entity.AccountDerivationPath,
                AddressCount = entity.AddressCount,
                CreatedUtc= entity.CreatedUtc
            };
        }

        public static DerivedKeyDtoResponse ToDto(this DerivedKeyResult model)
        {
            ArgumentNullException.ThrowIfNull(model);

            return new DerivedKeyDtoResponse
            {
                IsChange = model.IsChange,
                Address = model.Address
            };
        }

        public static DerivedKeyResult ToModel(this DerivedKeyEntity entity)
        {
            ArgumentNullException.ThrowIfNull(entity);

            return new DerivedKeyResult
            {
                DerivationPath = entity.DerivationPath,
                AddressIndex = entity.AddressIndex,
                IsChange = entity.IsChange,
                Address = entity.Address,
                PublicKeyHex = entity.PublicKeyHex,
                PrivateKeyWif = entity.PrivateKeyWif
            };
        }

    }
}
