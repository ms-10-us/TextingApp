using System.Reflection;

namespace BitcoinWalletMicroService.Utilities
{
    public class EnglishWordList
    {
        private const string ResourceName = "BitcoinWalletMicroService.RequiredFiles.english.txt";

        private const int ExpectedWordCount = 2048;

        private static readonly Lazy<string[]> WordsLazy = new Lazy<string[]>(Load, isThreadSafe: true);

        private static readonly Lazy<Dictionary<string, int>> IndexLazy = new Lazy<Dictionary<string, int>>(BuildIndex, isThreadSafe: true);

        public static IReadOnlyList<string> Words => WordsLazy.Value;

        public static bool TryGetIndex(string word, out int index) => IndexLazy.Value.TryGetValue(word, out index);

        private static string[] Load()
        {

            Assembly assembly = typeof(EnglishWordList).Assembly;

            using Stream? stream = assembly.GetManifestResourceStream(ResourceName);

            if (stream == null)
            {
                string available = string.Join(", ", assembly.GetManifestResourceNames());

                throw new InvalidOperationException(
                    $"Embedded resource '{ResourceName}' was not found. Confirm " +
                    "Utilities\\Wordlists\\english.txt has Build Action = Embedded Resource. " +
                    $"Resources present: [{available}]");
            }

            using var reader = new StreamReader(stream);

            var words = new List<string>(ExpectedWordCount);
            string? line;
            while((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length > 0)
                {
                    words.Add(line);
                }
            }

            if (words.Count != ExpectedWordCount)
            {
                throw new InvalidOperationException($"BIP-39 wordlist must contain exactly {ExpectedWordCount} words but contained {words.Count}. " +
                    "The file is truncated or is not the official list.");
            }

            return [.. words];
        }

        private static Dictionary<string, int> BuildIndex()
        {
            string[] words = WordsLazy.Value;

            var map = new Dictionary<string, int>(words.Length, StringComparer.Ordinal);

            for (int i = 0; i < words.Length; i++)
            {
                map[words[i]] = i;
            }

            return map;
        }

    }
}
