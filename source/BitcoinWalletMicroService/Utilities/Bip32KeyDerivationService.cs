using BitcoinWalletMicroService.Enums;
using BitcoinWalletMicroService.Models;
using NBitcoin;
using System.Globalization;

namespace BitcoinWalletMicroService.Utilities
{
    public class Bip32KeyDerivationService : IKeyDerivationService
    {
        public AccountKeys DeriveAccount(byte[] seed, BitcoinNetwork network, AddressType addressType, int account)
        {
            if (seed == null)
            {
                throw new ArgumentNullException("Seed can't be null.");
            }

            if (seed.Length != 64)
            {
                throw new ArgumentException("BIP39 seed must be 64 bytes.", "seed");
            }

            if (account < 0)
            {
                throw new ArgumentOutOfRangeException("Account is out of range.");
            }

            Network net = ToNetwork(network);
            string accountPath = BuildAccountPath(network, addressType, account);

            ExtKey masterKey = ExtKey.CreateFromSeed(seed);
            ExtKey accountKey = masterKey.Derive(KeyPath.Parse(accountPath));
            ExtPubKey accountPubKey = accountKey.Neuter();

            return new AccountKeys
            {
                AccountDerivationPath = accountPath,
                AccountExtendedPublicKey = accountPubKey.ToString(net)
            };

        }

        public DerivedKeyResult DerivePrivateKeys(
            byte[] seed, 
            BitcoinNetwork network, 
            AddressType addressType, 
            int account, 
            bool isChange, 
            int index)
        {
            if (seed == null)
            {
                throw new ArgumentNullException("Seed cna't be null");
            }

            if (seed.Length != 64)
            {
                throw new ArgumentException("BIP39 seed must be 64 bytes.", "seed");
            }

            if (index < 0)
            {
                throw new ArgumentOutOfRangeException("Index is out of range");
            }

            Network net = ToNetwork(network);
            string fullPath = string.Format(
                CultureInfo.InvariantCulture, "{0}/{1}/{2}",
                BuildAccountPath(network, addressType, account), isChange ? 1 : 0, index);

            ExtKey masterKey = ExtKey.CreateFromSeed(seed);
            ExtKey childKey = masterKey.Derive(KeyPath.Parse(fullPath));
            PubKey pubKey = childKey.PrivateKey.PubKey;

            return new DerivedKeyResult
            {
                AddressIndex = index,
                IsChange = isChange,
                DerivationPath = fullPath,
                PublicKeyHex = pubKey.ToHex(),
                Address = ToAddress(pubKey, addressType, net),
                PrivateKeyWif = childKey.PrivateKey.GetWif(net).ToString()
            };
        }

        public IReadOnlyList<DerivedKeyResult> DerivePublicKeys(
            string accountExtendedPublicKey, 
            BitcoinNetwork network, 
            AddressType addressType, 
            bool isChange, 
            int startIndex, 
            int count)
        {

            if (string.IsNullOrWhiteSpace(accountExtendedPublicKey))
            {
                throw new ArgumentException("Account xpub is required.", "accountExtendedPublicKey");
            }

            if (startIndex < 0)
            {
                throw new ArgumentOutOfRangeException("startIndex");
            }

            if (count < 1)
            {
                throw new ArgumentOutOfRangeException("count");
            }

            Network net = ToNetwork(network);
            ExtPubKey accountPubKey = ExtPubKey.Parse(accountExtendedPublicKey, net);
            ExtPubKey chainKey = accountPubKey.Derive((uint)(isChange ? 1 : 0));

            var result = new List<DerivedKeyResult>(count);

            for (int i = 0; i < count; i++)
            {
                int index = startIndex + i;
                ExtPubKey child = chainKey.Derive((uint)index);
                PubKey pubKey = child.PubKey;

                result.Add(new DerivedKeyResult
                {
                    AddressIndex = index,
                    IsChange = isChange,
                    DerivationPath = string.Format(
                        CultureInfo.InvariantCulture, "{0}/{1}/{2}",
                        BuildAccountPath(network, addressType, 0), isChange ? 1 : 0, index),
                    PublicKeyHex = pubKey.ToHex(),
                    Address = ToAddress(pubKey, addressType, net),
                    PrivateKeyWif = null
                });
            }

            return result;
        }

        private static Network ToNetwork(BitcoinNetwork network)
        {
            return network == BitcoinNetwork.Main ? Network.Main : Network.TestNet;
        }

        private static string BuildAccountPath(BitcoinNetwork network, AddressType addressType, int account)
        {
            int purpose = (int)addressType;
            int coinType = network == BitcoinNetwork.Main ? 0 : 1;

            return string.Format(CultureInfo.InvariantCulture,
                "m/{0}'/{1}'/{2}'", purpose, coinType, account);
        }

        private static string ToAddress(PubKey pubKey, AddressType addressType, Network net)
        {
            switch (addressType)
            {
                case AddressType.Legacy:
                    return pubKey.GetAddress(ScriptPubKeyType.Legacy, net).ToString();

                case AddressType.NestedSegwit:
                    return pubKey.GetAddress(ScriptPubKeyType.SegwitP2SH, net).ToString();

                case AddressType.NativeSegwit:
                    return pubKey.GetAddress(ScriptPubKeyType.Segwit, net).ToString();

                default:
                    throw new ArgumentOutOfRangeException("addressType", addressType, "Unsupported address type.");
            }
        }
    }
}
