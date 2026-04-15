using System;

namespace KrumpKraft
{
    public interface IWalletProvider
    {
        bool IsConnected { get; }
        string ConnectedAddress { get; }
        event Action<string> OnConnected;
        event Action OnDisconnected;
        void Connect(Action<bool> onComplete);
        void Disconnect();
        void SignMessage(string message, Action<bool, string> onComplete);
        /// <summary>Sign raw 32-byte hash (e.g. EIP-712 digest). Returns 0x + r (64 hex) + s (64 hex) + v (2 hex).</summary>
        void SignHash(byte[] hash, Action<bool, string> onComplete);
        string GetPrivateKey();
    }
}
