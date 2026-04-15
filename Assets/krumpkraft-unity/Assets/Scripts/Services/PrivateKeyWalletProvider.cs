using System;
using UnityEngine;

namespace KrumpKraft
{
    /// <summary>
    /// Wallet provider that uses a private key from WalletConfig (Resources/WalletConfig.json).
    /// For dev/test only — never ship with a private key in production.
    /// Signing requires Nethereum (add package com.nethereum.unity or Nethereum.Unity) or the key is ignored and SignMessage fails.
    /// If Nethereum is not present, Connect() still works using the address from config (for balance display).
    /// </summary>
    public class PrivateKeyWalletProvider : IWalletProvider
    {
        private readonly WalletConfig _config;
        private string _address;
        private bool _connected;
        private object _messageSigner;
        private object _key;

        public bool IsConnected => _connected;
        public string ConnectedAddress => _address;
        public event Action<string> OnConnected;
        public event Action OnDisconnected;

        public string GetPrivateKey()
        {
            if (!_connected || !_config.HasPrivateKey)
            {
                Debug.LogWarning("[PrivateKeyWalletProvider] GetPrivateKey: Wallet not connected or private key not available");
                return null;
            }
            return _config.PrivateKeyTrimmed;
        }

        public PrivateKeyWalletProvider(WalletConfig config)
        {
            _config = config ?? new WalletConfig();
        }

        public void Connect(Action<bool> onComplete)
        {
            if (_connected)
            {
                onComplete?.Invoke(true);
                return;
            }
            _address = _config.AddressTrimmed;
            if (string.IsNullOrEmpty(_address) && _config.HasPrivateKey)
                _address = TryDeriveAddress(_config.PrivateKeyTrimmed);
            if (string.IsNullOrEmpty(_address))
            {
                Debug.LogWarning("[KrumpKraft] PrivateKeyWalletProvider: no address in WalletConfig and could not derive from key.");
                onComplete?.Invoke(false);
                return;
            }
            if (!string.IsNullOrEmpty(_config.PrivateKeyTrimmed))
            {
                TryLoadSigner();
            }
            _connected = true;
            OnConnected?.Invoke(_address);
            onComplete?.Invoke(true);
        }

        public void Disconnect()
        {
            _connected = false;
            _address = null;
            _messageSigner = null;
            _key = null;
            OnDisconnected?.Invoke();
        }

        public void SignMessage(string message, Action<bool, string> onComplete)
        {
            if (!_connected)
            {
                onComplete?.Invoke(false, null);
                return;
            }
            if (_messageSigner == null || _key == null)
            {
                Debug.LogWarning("[KrumpKraft] Signing requires Nethereum. Add Nethereum.Unity or use an external wallet (e.g. MetaMask).");
                onComplete?.Invoke(false, null);
                return;
            }
            var signature = TrySign(message);
            onComplete?.Invoke(!string.IsNullOrEmpty(signature), signature);
        }

        public void SignHash(byte[] hash, Action<bool, string> onComplete)
        {
            if (!_connected || _key == null || hash == null || hash.Length != 32)
            {
                onComplete?.Invoke(false, null);
                return;
            }
            try
            {
                var keyType = _key.GetType();
                var signMethod = keyType.GetMethod("Sign", new[] { typeof(byte[]) });
                if (signMethod == null) { onComplete?.Invoke(false, null); return; }
                var sig = signMethod.Invoke(_key, new object[] { hash });
                if (sig == null) { onComplete?.Invoke(false, null); return; }
                var sigType = sig.GetType();
                var r = GetSigBytes(sigType.GetProperty("R")?.GetValue(sig));
                var s = GetSigBytes(sigType.GetProperty("S")?.GetValue(sig));
                var vProp = sigType.GetProperty("V")?.GetValue(sig);
                int v = 27;
                if (vProp is int vi) v = (vi == 0 || vi == 1) ? 27 + vi : vi;
                else if (vProp is byte vb) v = (vb == 0 || vb == 1) ? 27 + vb : vb;
                if (r == null || s == null || r.Length != 32 || s.Length != 32) { onComplete?.Invoke(false, null); return; }
                var sb = new System.Text.StringBuilder("0x", 132);
                for (var i = 0; i < 32; i++) sb.Append(r[i].ToString("x2"));
                for (var i = 0; i < 32; i++) sb.Append(s[i].ToString("x2"));
                sb.Append((v & 0xff).ToString("x2"));
                onComplete?.Invoke(true, sb.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PrivateKeyWalletProvider] SignHash failed: {e.Message}");
                onComplete?.Invoke(false, null);
            }
        }

        private static byte[] GetSigBytes(object val)
        {
            if (val == null) return null;
            if (val is byte[] arr) return arr.Length == 32 ? arr : null;
            var t = val.GetType();
            if (t.Name == "BigInteger")
            {
                var bytes = (byte[])t.GetMethod("ToByteArray")?.Invoke(val, null);
                if (bytes == null || bytes.Length == 0) return null;
                Array.Reverse(bytes);
                var result = new byte[32];
                Buffer.BlockCopy(bytes, 0, result, 32 - Math.Min(32, bytes.Length), Math.Min(32, bytes.Length));
                return result;
            }
            return null;
        }

        private void TryLoadSigner()
        {
            try
            {
                var keyType = Type.GetType("Nethereum.Signer.EthECKey, Nethereum.Signer");
                var signerType = Type.GetType("Nethereum.Signer.EthereumMessageSigner, Nethereum.Signer");
                if (keyType == null || signerType == null) return;
                var keyHex = _config.PrivateKeyTrimmed;
                if (keyHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) keyHex = keyHex.Substring(2);
                _key = Activator.CreateInstance(keyType, new object[] { keyHex });
                _messageSigner = Activator.CreateInstance(signerType);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[KrumpKraft] Could not load Nethereum signer: {e.Message}");
            }
        }

        private string TryDeriveAddress(string keyHex)
        {
            try
            {
                var keyType = Type.GetType("Nethereum.Signer.EthECKey, Nethereum.Signer");
                if (keyType == null) return null;
                if (keyHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) keyHex = keyHex.Substring(2);
                var key = Activator.CreateInstance(keyType, new object[] { keyHex });
                var getAddress = keyType.GetMethod("GetPublicAddress");
                if (getAddress == null) return null;
                return getAddress.Invoke(key, null) as string;
            }
            catch { return null; }
        }

        private string TrySign(string message)
        {
            if (_messageSigner == null || _key == null) return null;
            try
            {
                var signerType = _messageSigner.GetType();
                var sign = signerType.GetMethod("Sign", new[] { typeof(string), typeof(object) });
                if (sign == null) sign = signerType.GetMethod("Sign", new[] { typeof(byte[]), typeof(object) });
                if (sign != null)
                {
                    var msgBytes = System.Text.Encoding.UTF8.GetBytes(message);
                    object result = sign.GetParameters()[0].ParameterType == typeof(string)
                        ? sign.Invoke(_messageSigner, new object[] { message, _key })
                        : sign.Invoke(_messageSigner, new object[] { msgBytes, _key });
                    if (result is string s) return s;
                    if (result is byte[] bytes) return "0x" + BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[KrumpKraft] Sign failed: {e.Message}");
            }
            return null;
        }
    }
}
