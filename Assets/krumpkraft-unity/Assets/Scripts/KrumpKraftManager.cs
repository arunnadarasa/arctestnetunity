using UnityEngine;

namespace KrumpKraft
{
    public class KrumpKraftManager : MonoBehaviour
    {
        public static KrumpKraftManager Instance { get; private set; }

        public enum WalletProviderMode { SimpleStub, PrivateKeyConfig, Web3AuthEmbeddedWallet, Custom }

        [SerializeField] private WalletProviderMode walletProviderMode = WalletProviderMode.SimpleStub;

        public ChainSettings ChainSettings { get; private set; }
        public MoralisConfig MoralisConfig { get; private set; }
        public WalletService WalletService { get; private set; }
        public TokenService TokenService { get; private set; }
        public MoralisApiClient MoralisApiClient { get; private set; }
        public RpcBalanceService RpcBalanceService { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            ChainSettings = ConfigLoader.Load<ChainSettings>("ChainSettings");
            MoralisConfig = ConfigLoader.Load<MoralisConfig>("MoralisConfig");
            if (ChainSettings == null)
            {
                Debug.LogError("[KrumpKraft] ChainSettings not found. Create Resources/ChainSettings.json");
                return;
            }
            if (MoralisConfig == null)
                MoralisConfig = new MoralisConfig();

            WalletService = new WalletService();
            Debug.Log($"[KrumpKraftManager] Wallet provider mode: {walletProviderMode}");
            if (walletProviderMode == WalletProviderMode.SimpleStub)
            {
                Debug.Log("[KrumpKraftManager] Creating SimpleWalletProvider");
                var simple = new SimpleWalletProvider();
                WalletService.SetProvider(simple);
            }
            else if (walletProviderMode == WalletProviderMode.PrivateKeyConfig)
            {
                Debug.Log("[KrumpKraftManager] Creating PrivateKeyWalletProvider");
                var walletConfig = ConfigLoader.Load<WalletConfig>("WalletConfig");
                if (walletConfig == null) walletConfig = new WalletConfig();
                WalletService.SetProvider(new PrivateKeyWalletProvider(walletConfig));
            }
            else if (walletProviderMode == WalletProviderMode.Web3AuthEmbeddedWallet)
            {
                Debug.Log("[KrumpKraftManager] Creating Web3AuthWalletProvider");
                var walletConfig = ConfigLoader.Load<WalletConfig>("WalletConfig");
                if (walletConfig == null) walletConfig = new WalletConfig();
                WalletService.SetProvider(new Web3AuthWalletProvider(walletConfig));
            }

            MoralisApiClient = new MoralisApiClient(MoralisConfig, this);
            TokenService = new TokenService(MoralisApiClient, ChainSettings.ChainId);
            RpcBalanceService = new RpcBalanceService(ChainSettings.hostRpcUrl, this, ChainSettings.KrumpKraftApiBaseUrl);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
