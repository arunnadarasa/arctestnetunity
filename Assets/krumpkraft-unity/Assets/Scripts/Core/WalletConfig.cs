using UnityEngine;

namespace KrumpKraft
{
    [System.Serializable]
    public class WalletConfig
    {
        public bool usePrivateKey;
        public string privateKey;
        public string address;

        [Header("MetaMask Embedded Wallets (Web3Auth)")]
        [Tooltip("Client ID from Embedded Wallets dashboard. Prefer env or gitignored config.")]
        public string web3AuthClientId = "";
        [Tooltip("Must match your project in the dashboard: SAPPHIRE_DEVNET or SAPPHIRE_MAINNET. Wrong network causes invalid auth connection errors.")]
        public string web3AuthNetwork = "SAPPHIRE_DEVNET";
        [Tooltip("Optional. Defaults to torusapp://com.torus.Web3AuthUnity/auth if empty.")]
        public string web3AuthRedirectUrl = "";
        [Tooltip("Auth Connection ID — only if you use a custom Google connection. Leave empty for Web3Auth default Google (dashboard Default Credentials).")]
        public string web3AuthVerifier = "";
        [Tooltip("Google OAuth Web client ID — only for a custom verifier. Leave empty when using Web3Auth default Google.")]
        public string googleClientId = "";
        [Tooltip("Exact Auth Connection ID from Dashboard → Authentication → Social Connections → Google. Leave empty to use w3a-google (typical Plug & Play). Set explicitly if login shows \"Invalid auth connection\" (e.g. w3a-google-demo only matches some doc examples).")]
        public string defaultGoogleAuthConnectionId = "";

        public bool HasPrivateKey => usePrivateKey && !string.IsNullOrWhiteSpace(privateKey);
        public string PrivateKeyTrimmed => (privateKey ?? "").Trim();
        public string AddressTrimmed => (address ?? "").Trim();
        public bool HasWeb3AuthClientId => !string.IsNullOrWhiteSpace(web3AuthClientId);
        public string Web3AuthClientIdTrimmed => (web3AuthClientId ?? "").Trim();
        public string Web3AuthRedirectUrlOrDefault => string.IsNullOrWhiteSpace(web3AuthRedirectUrl) ? "torusapp://com.torus.Web3AuthUnity/auth" : web3AuthRedirectUrl.Trim();
        public string Web3AuthVerifierTrimmed => (web3AuthVerifier ?? "").Trim();
        public string GoogleClientIdTrimmed => (googleClientId ?? "").Trim();
        public string Web3AuthNetworkTrimmed =>
            string.IsNullOrWhiteSpace(web3AuthNetwork) ? "SAPPHIRE_DEVNET" : web3AuthNetwork.Trim();
        /// <summary>Explicit override only; empty means use built-in default below.</summary>
        public string DefaultGoogleAuthConnectionIdTrimmed => (defaultGoogleAuthConnectionId ?? "").Trim();
        /// <summary>
        /// When unset, uses w3a-google (common Web3Auth Google connection on Sapphire). Override with the exact id from your dashboard if hosted still rejects.
        /// </summary>
        public string ResolvedDefaultGoogleAuthConnectionId =>
            string.IsNullOrWhiteSpace(defaultGoogleAuthConnectionId)
                ? "w3a-google"
                : defaultGoogleAuthConnectionId.Trim();
    }
}
