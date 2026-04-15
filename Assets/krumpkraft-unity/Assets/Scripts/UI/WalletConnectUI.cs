using UnityEngine;
using UnityEngine.UI;

namespace KrumpKraft
{
    public class WalletConnectUI : MonoBehaviour
    {
        [SerializeField] private Button connectButton;
        [SerializeField] private Button disconnectButton;
        [SerializeField] private GameObject connectedPanel;
        [SerializeField] private TMPro.TMP_Text addressLabel;

        private WalletService _wallet;
        private bool _useTmp = true;

        private void Awake()
        {
            if (addressLabel == null && GetComponentInChildren<TMPro.TMP_Text>() != null)
                addressLabel = GetComponentInChildren<TMPro.TMP_Text>();
            if (addressLabel == null) _useTmp = false;
        }

        private void Start()
        {
            var manager = KrumpKraftManager.Instance;
            if (manager == null)
            {
                Debug.LogError("[WalletConnectUI] KrumpKraftManager.Instance is null!");
                return;
            }
            _wallet = manager.WalletService;
            if (_wallet == null)
            {
                Debug.LogError("[WalletConnectUI] WalletService is null!");
                return;
            }

            Debug.Log("[WalletConnectUI] Setting up wallet UI...");
            if (connectButton != null)
                connectButton.onClick.AddListener(OnConnectClicked);
            else
                Debug.LogWarning("[WalletConnectUI] connectButton is null!");
            
            if (disconnectButton != null)
                disconnectButton.onClick.AddListener(OnDisconnectClicked);

            _wallet.OnConnected += OnWalletChanged;
            _wallet.OnDisconnected += RefreshState;
            RefreshState();
            Debug.Log("[WalletConnectUI] Wallet UI setup complete");
        }

        private void OnWalletChanged(string _)
        {
            RefreshState();
        }

        private void OnDestroy()
        {
            if (_wallet != null)
            {
                _wallet.OnConnected -= OnWalletChanged;
                _wallet.OnDisconnected -= RefreshState;
            }
        }

        private void OnConnectClicked()
        {
            Debug.Log("[WalletConnectUI] Connect button clicked!");
            _wallet?.Connect(success =>
            {
                Debug.Log($"[WalletConnectUI] Connect callback: success={success}");
                if (success) RefreshState();
            });
        }

        private void OnDisconnectClicked()
        {
            _wallet?.Disconnect();
        }

        private void RefreshState()
        {
            var connected = _wallet != null && _wallet.IsConnected;
            if (connectButton != null) connectButton.gameObject.SetActive(!connected);
            if (disconnectButton != null) disconnectButton.gameObject.SetActive(connected);
            if (connectedPanel != null) connectedPanel.SetActive(connected);
            if (addressLabel != null && _useTmp)
            {
                var addr = _wallet?.ConnectedAddress ?? "";
                addressLabel.text = string.IsNullOrEmpty(addr) ? "" : (addr.Length > 10 ? addr.Substring(0, 6) + "..." + addr.Substring(addr.Length - 4) : addr);
            }
        }
    }
}
