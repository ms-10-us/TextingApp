using BitcoinWalletMicroService.Dtos;
using BitcoinWalletMicroService.Entities;
using BitcoinWalletMicroService.Enums;
using BitcoinWalletMicroService.Models;
using System.Reflection.Emit;
using System.Reflection.Metadata.Ecma335;

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

        public static ReceiveAddressDtoResponse ToDto(this ReceiveAddressResult model)
        {
            ArgumentNullException.ThrowIfNull(model);

            return new ReceiveAddressDtoResponse
            {
                Address = model.Address,
                DerivationPath = model.DerivationPath,
                AddressIndex = model.AddressIndex,
                IsChange = model.IsChange,
                PaymentUri = model.PaymentUri
            };
        }

        public static WalletBalanceDtoResponse ToDto(this WalletBalance model)
        {
            ArgumentNullException.ThrowIfNull(model);

            return new WalletBalanceDtoResponse
            {
                WalletId = model.WalletId,
                ConfirmedSats = model.ConfirmedSats,
                UnconfimredSats = model.UncofirmedSats,
                TotalSats = model.TotalSats,
                UtxoCount = model.UtxoCount
            };
        }

        public static SendModel ToModel(this SendDto dto, string walletId)
        {
            ArgumentNullException.ThrowIfNull(dto);
            ArgumentException.ThrowIfNullOrWhiteSpace(walletId);

            return new SendModel
            {
                WalletId = walletId,
                ToAddress = dto.ToAddress,
                AmountSats = dto.AmountSats,
                Passphrase = dto.Passphrase,
                FeeSats = dto.FeeRateSatsPerVByte,
                SweepAll = dto.SweepAll,
                DryRun = dto.DryRun,
                IdempotencyKey = dto.IdempotencyKey
            };

        }

        public static SendResult ToResult(this SendTransactionEntity entity)
        {
            ArgumentNullException.ThrowIfNull(entity);

            return new SendResult
            {
                TxId = entity.TxId,
                RawTransactionHex = entity.RawTransactionHex,
                AmountSats = entity.AmountSats,
                FeeSats = entity.FeeSats,
                FeeRateSatsPerVByte = entity.VirtualSizeBytes > 0 
                    ? Math.Round((decimal)entity.FeeSats / entity.VirtualSizeBytes, 2) 
                    : 0m,
                VirtualSizeBytes = entity.VirtualSizeBytes,
                ToAddress = entity.ToAddress,
                ChangeAddress = entity.ChangeAddress,
                ChangeSats = entity.ChangeSats,
                InputCount = entity.InputCount,
                Broadcast = true,
                WasIdempotentReplay = true,
            };
        }

        public static SendTransactionEntity ToEntity(this SendResult result, string walletId, 
            string idempotencyKey, DateTime? broadcastUtc = null)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentException.ThrowIfNullOrEmpty(result.TxId);

            return new SendTransactionEntity
            {
                TxId = result.TxId,
                WalletId = walletId,
                IdempotencyKey = idempotencyKey,
                ToAddress = result.ToAddress,
                AmountSats = result.AmountSats,
                FeeSats = result.FeeSats,
                VirtualSizeBytes = result.VirtualSizeBytes,
                InputCount = result.InputCount,
                ChangeAddress = result.ChangeAddress,
                ChangeSats = result.ChangeSats,
                RawTransactionHex = result.RawTransactionHex,
                BroadcastUtc = broadcastUtc ?? DateTime.UtcNow
            };
        }

        public static SendTransaction ToModel(this SendTransactionEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException("entity");
            }

            return new SendTransaction
            {
                TxId = entity.TxId,
                WalletId = entity.WalletId,
                ToAddress = entity.ToAddress,
                AmountSats = entity.AmountSats,
                FeeSats = entity.FeeSats,
                VirtualSizeBytes = entity.VirtualSizeBytes,
                InputCount = entity.InputCount,
                ChangeAddress = entity.ChangeAddress,
                ChangeSats = entity.ChangeSats,
                BroadcastUtc = entity.BroadcastUtc
            };
        }

        public static SendDtoResponse ToDto(this SendResult model)
        {
            ArgumentNullException.ThrowIfNull(model);

            return new SendDtoResponse
            {
                TxId = model.TxId,
                RawTransactionHex = model.RawTransactionHex,
                AmountSats = model.AmountSats,
                FeeSats = model.FeeSats,
                VirtualSizeBytes = model.VirtualSizeBytes,
                ToAddress = model.ToAddress,
                ChangeAddress = model.ChangeAddress,
                ChangeSats = model.ChangeSats,
                InputCount = model.InputCount,
                Broadcast = model.Broadcast,
                WasIdempotentReplay = model.WasIdempotentReplay
            };
        }

        public static CreateDepositModel ToModel(this CreateDepositDto dto, string waletId)
        {
            ArgumentNullException.ThrowIfNull(dto);

            return new CreateDepositModel
            {
                WalletId = waletId,
                ExpectedSats = dto.ExpectedSats,
                Label = dto.Label,
                ExpiryMinutes = dto.ExpiryMinutes
            };
        }

        public static DepositDtoResponse ToDto(this DepositResult model)
        {
            ArgumentNullException.ThrowIfNull("model");

            return new DepositDtoResponse
            {
                DepositId = model.DepositId,
                WalletId = model.WalletId,
                Address = model.Address,
                PaymentUri = model.PaymentUri,
                ExpectedSats = model.ExpectedSats,
                ReceivedSats = model.ReceivedSats,
                UnconfirmedSats = model.UnconfirmedSats,
                Status = model.Status.ToString(),
                Confirmations = model.Confirmations,
                TxId = model.TxId,
                CreatedUtc = model.CreatedUtc,
                ExpiresUtc = model.ExpiresUtc,
                ConfirmedUtc = model.ConfirmedUtc
            };
        }

        public static DepositResult ToResult (
            this DepositEntity entity,
            string paymentUri,
            long unconfirmedSats,
            int confirmations)
        {
            ArgumentNullException.ThrowIfNull("entity");

            return new DepositResult
            {
                DepositId = entity.DepositId,
                WalletId = entity.WalletId,
                Address = entity.Address,
                PaymentUri = paymentUri,
                ExpectedSats = entity.ExpectedSats,
                ReceivedSats = entity.ReceivedSats,
                UnconfirmedSats = unconfirmedSats,
                Status = Enum.TryParse(entity.Status, out DepositStatus status)
                    ? status
                    : DepositStatus.Pending,
                Confirmations = confirmations,
                TxId = entity.TxId,
                CreatedUtc = entity.CreatedUtc,
                ExpiresUtc = entity.ExpiresUtc,
                ConfirmedUtc = entity.ConfirmedUtc
            };
        }

    }
}
