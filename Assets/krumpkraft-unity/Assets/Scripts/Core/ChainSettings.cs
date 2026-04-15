using UnityEngine;

namespace KrumpKraft
{
    [System.Serializable]
    public class ChainSettings
    {
        public string chainName;
        public int chainId;
        public string rpcUrl;
        public string blockExplorer;
        public string nativeCurrencySymbol;
        public int chainType;

        [Header("Host chain (Story) – EVVM 1140 is deployed on Story Aeneid Testnet")]
        public int hostChainId = 1315;
        public string hostRpcUrl = "https://aeneid.storyrpc.io";
        public string hostBlockExplorer = "https://aeneid.storyscan.io";

        [Header("KrumpKraft API (JAB balance, etc.) – use Bezi API (e.g. http://localhost:3099) or main API")]
        public string krumpKraftApiBaseUrl = "http://localhost:3099";

        public string ChainName => chainName;
        public int ChainId => chainId;
        public string RpcUrl => rpcUrl ?? hostRpcUrl;
        public string BlockExplorer => blockExplorer ?? hostBlockExplorer;
        public string NativeCurrencySymbol => nativeCurrencySymbol;
        public int ChainType => chainType;
        public int HostChainId => hostChainId;
        public string HostRpcUrl => hostRpcUrl ?? "https://aeneid.storyrpc.io";
        public string HostBlockExplorer => hostBlockExplorer ?? "https://aeneid.storyscan.io";
        public string KrumpKraftApiBaseUrl => string.IsNullOrEmpty(krumpKraftApiBaseUrl) ? "http://localhost:3099" : krumpKraftApiBaseUrl.TrimEnd('/');
    }
}
