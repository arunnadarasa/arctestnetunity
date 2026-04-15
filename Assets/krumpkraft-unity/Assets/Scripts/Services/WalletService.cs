using System;
using UnityEngine;

namespace KrumpKraft
{
    public class WalletService
    {
        private IWalletProvider _provider;

        public bool IsConnected => _provider != null && _provider.IsConnected;
        public string ConnectedAddress => _provider?.ConnectedAddress ?? "";
        public IWalletProvider Provider => _provider;

        public event Action<string> OnConnected;
        public event Action OnDisconnected;

        public void SetProvider(IWalletProvider provider)
        {
            if (_provider != null)
            {
                _provider.OnConnected -= HandleConnected;
                _provider.OnDisconnected -= HandleDisconnected;
            }
            _provider = provider;
            if (_provider != null)
            {
                _provider.OnConnected += HandleConnected;
                _provider.OnDisconnected += HandleDisconnected;
            }
        }

        private void HandleConnected(string address)
        {
            OnConnected?.Invoke(address);
        }

        private void HandleDisconnected()
        {
            OnDisconnected?.Invoke();
        }

        public void Connect(Action<bool> onComplete)
        {
            if (_provider == null)
            {
                Debug.LogWarning("[KrumpKraft] No wallet provider set.");
                onComplete?.Invoke(false);
                return;
            }
            _provider.Connect(success => onComplete?.Invoke(success));
        }

        public void Disconnect()
        {
            _provider?.Disconnect();
        }

        public void SignMessage(string message, Action<bool, string> onComplete)
        {
            if (_provider == null || !_provider.IsConnected)
            {
                Debug.LogWarning("[KrumpKraft] Wallet not connected.");
                onComplete?.Invoke(false, null);
                return;
            }
            _provider.SignMessage(message, onComplete);
        }

        public void SignHash(byte[] hash, Action<bool, string> onComplete)
        {
            if (_provider == null || !_provider.IsConnected)
            {
                Debug.LogWarning("[KrumpKraft] Wallet not connected.");
                onComplete?.Invoke(false, null);
                return;
            }
            _provider.SignHash(hash, onComplete);
        }
    }
}
