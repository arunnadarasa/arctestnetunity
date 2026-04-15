using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace KrumpKraft
{
    public class MoralisApiClient
    {
        private readonly MoralisConfig _config;
        private readonly MonoBehaviour _coroutineRunner;

        public MoralisApiClient(MoralisConfig config, MonoBehaviour coroutineRunner)
        {
            _config = config ?? new MoralisConfig();
            _coroutineRunner = coroutineRunner;
        }

        public void GetTokenBalances(string walletAddress, int chainId, Action<MoralisTokenBalanceEntry[]> onSuccess, Action<string> onError)
        {
            if (string.IsNullOrEmpty(walletAddress))
            {
                onError?.Invoke("Wallet address is empty");
                return;
            }
            var path = $"{walletAddress}/erc20";
            var url = _config.GetFullUrl(path) + "?chain=0x" + chainId.ToString("x");
            var req = UnityWebRequest.Get(url);
            if (!string.IsNullOrEmpty(_config.apiKey))
                req.SetRequestHeader("X-API-Key", _config.apiKey);
            _coroutineRunner.StartCoroutine(SendRequest(req, onSuccess, onError));
        }

        private IEnumerator SendRequest(UnityWebRequest req, Action<MoralisTokenBalanceEntry[]> onSuccess, Action<string> onError)
        {
            using (req)
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(req.error ?? req.downloadHandler?.text ?? "Request failed");
                    yield break;
                }
                var json = req.downloadHandler?.text;
                if (string.IsNullOrEmpty(json))
                {
                    onSuccess?.Invoke(Array.Empty<MoralisTokenBalanceEntry>());
                    yield break;
                }
                try
                {
                    var list = JsonHelper.ArrayFromJson<MoralisTokenBalanceEntry>(json);
                    onSuccess?.Invoke(list ?? Array.Empty<MoralisTokenBalanceEntry>());
                }
                catch (Exception e)
                {
                    onError?.Invoke(e.Message);
                }
            }
        }
    }

    [Serializable]
    public class MoralisTokenBalanceEntry
    {
        public string token_address;
        public string name;
        public string symbol;
        public string balance;
        public int decimals;
    }

    public static class JsonHelper
    {
        public static T[] ArrayFromJson<T>(string json)
        {
            var wrapper = JsonUtility.FromJson<Wrapper<T>>("{\"items\":" + json + "}");
            return wrapper?.items ?? new T[0];
        }

        [Serializable]
        private class Wrapper<T>
        {
            public T[] items;
        }
    }
}
