using System;
using System.Linq;
using UnityEngine;

namespace KrumpKraft
{
    public class TokenService
    {
        public const int DhpDecimals = 18;
        public const int USDCHealthDecimals = 18;

        private readonly MoralisApiClient _moralis;
        private readonly int _chainId;

        public TokenService(MoralisApiClient moralis, int chainId)
        {
            _moralis = moralis;
            _chainId = chainId;
        }

        public void GetDhpBalance(string walletAddress, Action<bool, string> onComplete)
        {
            GetBalance(walletAddress, ContractAddresses.DhpPrincipal, DhpDecimals, onComplete);
        }

        public void GetUSDCHealthBalance(string walletAddress, Action<bool, string> onComplete)
        {
            GetBalance(walletAddress, ContractAddresses.USDCHealth, USDCHealthDecimals, onComplete);
        }

        public void GetBalance(string walletAddress, string tokenAddress, int decimals, Action<bool, string> onComplete)
        {
            if (string.IsNullOrEmpty(walletAddress))
            {
                onComplete?.Invoke(false, "0");
                return;
            }
            _moralis.GetTokenBalances(walletAddress, _chainId, entries =>
            {
                var tokenLower = (tokenAddress ?? "").ToLowerInvariant();
                var entry = entries?.FirstOrDefault(e => (e?.token_address ?? "").ToLowerInvariant() == tokenLower);
                if (entry == null)
                {
                    onComplete?.Invoke(true, "0");
                    return;
                }
                var balance = FormatBalance(entry.balance, entry.decimals);
                onComplete?.Invoke(true, balance);
            }, err =>
            {
                Debug.LogWarning($"[KrumpKraft] Balance fetch failed: {err}");
                onComplete?.Invoke(false, "0");
            });
        }

        public static string FormatBalance(string weiAmount, int decimals)
        {
            if (string.IsNullOrEmpty(weiAmount)) return "0";
            if (!System.Numerics.BigInteger.TryParse(weiAmount, out var bi))
                return "0";
            var divisor = System.Numerics.BigInteger.Pow(10, decimals);
            var whole = bi / divisor;
            var frac = bi % divisor;
            if (frac < 0) frac = -frac;
            var fracStr = frac.ToString().PadLeft(decimals, '0').TrimEnd('0');
            return string.IsNullOrEmpty(fracStr) ? whole.ToString() : $"{whole}.{fracStr}";
        }
    }
}
