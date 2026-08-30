using BitcoinWalletMicroService.Enums;
using BitcoinWalletMicroService.Models;

namespace BitcoinWalletMicroService.Utilities
{
    public interface IKeyDerivationService
    {
        AccountKeys DeriveAccount(byte[] seed, BitcoinNetwork network, AddressType addressType, int account);

        IReadOnlyList<DerivedKeyResult> DerivePublicKeys(
            string accountExtendedPublicKey,
            BitcoinNetwork network,
            AddressType addressType,
            bool isChange,
            int startIndex,
            int count);

        DerivedKeyResult DerivePrivateKeys(
            byte[] seed,
            BitcoinNetwork network,
            AddressType addressType,
            int account,
            bool isChange,
            int index);
    }
}
