using System;
using UnityEngine;

namespace KrumpKraft
{
    public class SimpleWalletProvider : IWalletProvider
    {
        private string _address;
        private bool _connected;

        public bool IsConnected => _connected;
        public string ConnectedAddress => _address;
        public event Action<string> OnConnected;
        public event Action OnDisconnected;

        public void Connect(Action<bool> onComplete)
        {
            if (_connected)
            {
                onComplete?.Invoke(true);
                return;
            }
            _address = "0x0000000000000000000000000000000000000001";
            _connected = true;
            OnConnected?.Invoke(_address);
            onComplete?.Invoke(true);
        }

        public void Disconnect()
        {
            _connected = false;
            _address = null;
            OnDisconnected?.Invoke();
        }

        public void SignMessage(string message, Action<bool, string> onComplete)
        {
            if (!_connected)
            {
                onComplete?.Invoke(false, null);
                return;
            }
            var fakeSignature = "0x" + new string('0', 130);
            onComplete?.Invoke(true, fakeSignature);
        }

        public void SignHash(byte[] hash, Action<bool, string> onComplete)
        {
            if (!_connected || hash == null)
            {
                onComplete?.Invoke(false, null);
                return;
            }
            onComplete?.Invoke(true, "0x" + new string('0', 130));
        }

        public string GetPrivateKey()
        {
            Debug.LogWarning("[SimpleWalletProvider] GetPrivateKey: This is a test wallet with no private key");
            return null;
        }

        public void SetTestAddress(string address)
        {
            _address = address;
        }
    }
}
