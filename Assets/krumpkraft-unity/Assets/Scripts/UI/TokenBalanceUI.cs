using UnityEngine;
using UnityEngine.UI;

namespace KrumpKraft
{
    public class TokenBalanceUI : MonoBehaviour
    {
        public enum TokenType { Dhp, USDCHealth, NativeUsdc }

        [SerializeField] private TokenType tokenType = TokenType.Dhp;
        [SerializeField] private TMPro.TMP_Text balanceLabel;
        [SerializeField] private Button refreshButton;

        private RpcBalanceService _rpcBalanceService;
        private WalletService _wallet;
        private bool _useTmp = true;

        private void Awake()
        {
            if (balanceLabel == null && GetComponentInChildren<TMPro.TMP_Text>() != null)
                balanceLabel = GetComponentInChildren<TMPro.TMP_Text>();
            if (balanceLabel == null) _useTmp = false;
        }

        private void Start()
        {
            var manager = KrumpKraftManager.Instance;
            if (manager == null) return;
            _rpcBalanceService = manager.RpcBalanceService;
            _wallet = manager.WalletService;
            if (_rpcBalanceService == null || _wallet == null) return;

            if (refreshButton != null)
                refreshButton.onClick.AddListener(Refresh);

            _wallet.OnConnected += OnWalletChanged;
            _wallet.OnDisconnected += OnWalletDisconnected;
            Refresh();
        }

        private void OnWalletChanged(string _) => Refresh();
        private void OnWalletDisconnected() => Refresh();

        private void OnDestroy()
        {
            if (_wallet != null)
            {
                _wallet.OnConnected -= OnWalletChanged;
                _wallet.OnDisconnected -= OnWalletDisconnected;
            }
        }

        public void Refresh()
        {
            if (_rpcBalanceService == null || _wallet == null || !_wallet.IsConnected)
            {
                SetBalance(GetTokenLabel() + ": --");
                return;
            }
            var addr = _wallet.ConnectedAddress;
            if (tokenType == TokenType.Dhp)
                _rpcBalanceService.GetDHPBalance(addr, (ok, balance) => SetBalance(GetTokenLabel() + ": " + (ok ? balance : "?")));
            else if (tokenType == TokenType.USDCHealth)
                _rpcBalanceService.GetUSDCHealthBalance(addr, (ok, balance) => SetBalance(GetTokenLabel() + ": " + (ok ? balance : "?")));
            else
                _rpcBalanceService.GetNativeBalance(addr, (ok, balance) => SetBalance(GetTokenLabel() + ": " + (ok ? balance : "?")));
        }

        private string GetTokenLabel()
        {
            switch (tokenType)
            {
                case TokenType.Dhp:
                    return "DHP";
                case TokenType.USDCHealth:
                    return "USDC Health";
                case TokenType.NativeUsdc:
                    return "USDC (native)";
                default:
                    return "";
            }
        }

        private void SetBalance(string text)
        {
            if (balanceLabel != null && _useTmp)
                balanceLabel.text = text;
            else if (balanceLabel != null)
                balanceLabel.text = text;
        }
    }
}
