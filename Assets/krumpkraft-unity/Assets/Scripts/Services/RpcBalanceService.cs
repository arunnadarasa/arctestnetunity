using System;
using System.Collections;
using System.Numerics;
using UnityEngine;
using UnityEngine.Networking;

namespace KrumpKraft
{
    public class RpcBalanceService
    {
        private readonly string _rpcUrl;
        private readonly string _krumpKraftApiBaseUrl;
        private readonly MonoBehaviour _coroutineRunner;

        public RpcBalanceService(string rpcUrl, MonoBehaviour coroutineRunner, string krumpKraftApiBaseUrl = null)
        {
            _rpcUrl = rpcUrl;
            _coroutineRunner = coroutineRunner;
            _krumpKraftApiBaseUrl = string.IsNullOrEmpty(krumpKraftApiBaseUrl) ? "http://localhost:3099" : krumpKraftApiBaseUrl.TrimEnd('/');
        }

        public void GetUSDCHealthBalance(string walletAddress, Action<bool, string> onComplete)
        {
            var data = BuildERC20BalanceOfCallData(walletAddress);
            if (data == null)
            {
                onComplete?.Invoke(false, "0");
                return;
            }
            _coroutineRunner.StartCoroutine(CallContract(ContractAddresses.USDCHealth, data, result =>
            {
                if (result != null)
                {
                    var balance = TokenService.FormatBalance(result, TokenService.USDCHealthDecimals);
                    onComplete?.Invoke(true, balance);
                }
                else
                {
                    onComplete?.Invoke(false, "0");
                }
            }));
        }

        /// <summary>
        /// Get DHP balance via RPC: eth_call to EVVM Core getBalance(user, token) with DhpPrincipal.
        /// No dependency on Bezi/KrumpKraft API.
        /// </summary>
        public void GetDHPBalance(string walletAddress, Action<bool, string> onComplete)
        {
            var data = BuildEVVMGetBalanceCallData(walletAddress, ContractAddresses.DhpPrincipal);
            _coroutineRunner.StartCoroutine(CallContract(ContractAddresses.Core, data, result =>
            {
                if (result != null)
                {
                    var balance = TokenService.FormatBalance(result, TokenService.DhpDecimals);
                    onComplete?.Invoke(true, balance);
                }
                else
                {
                    onComplete?.Invoke(false, "0");
                }
            }));
        }

        public void GetNativeBalance(string walletAddress, Action<bool, string> onComplete)
        {
            // Arc native gas token is Native USDC (18 decimals via eth_getBalance)
            _coroutineRunner.StartCoroutine(GetBalance(walletAddress, result =>
            {
                if (result != null)
                {
                    var balance = TokenService.FormatBalance(result, 18);
                    onComplete?.Invoke(true, balance);
                }
                else
                {
                    onComplete?.Invoke(false, "0");
                }
            }));
        }

        private string BuildERC20BalanceOfCallData(string address)
        {
            var cleanAddress = address.Replace("0x", "").Replace("0X", "").ToLower();
            if (cleanAddress.Length != 40)
            {
                Debug.LogError($"[RpcBalanceService] Invalid address length: {cleanAddress.Length}, address: {address}");
                return null;
            }
            cleanAddress = cleanAddress.PadLeft(64, '0');
            return "0x70a08231" + cleanAddress;
        }

        private string BuildEVVMGetBalanceCallData(string userAddress, string tokenAddress)
        {
            var cleanUser = userAddress.Replace("0x", "").Replace("0X", "").ToLower().PadLeft(64, '0');
            var cleanToken = tokenAddress.Replace("0x", "").Replace("0X", "").ToLower().PadLeft(64, '0');
            return "0xd4fac45d" + cleanUser + cleanToken;
        }

        private IEnumerator CallContract(string contractAddress, string data, Action<string> onComplete)
        {
            var jsonPayload = $"{{\"jsonrpc\":\"2.0\",\"method\":\"eth_call\",\"params\":[{{\"to\":\"{contractAddress}\",\"data\":\"{data}\"}},\"latest\"],\"id\":1}}";
            
            using (var request = new UnityWebRequest(_rpcUrl, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var responseText = request.downloadHandler.text;
                    
                    try
                    {
                        var response = JsonUtility.FromJson<RpcResponse>(responseText);
                        if (!string.IsNullOrEmpty(response?.result))
                        {
                            var hexValue = response.result.Replace("0x", "");
                            if (hexValue.Length > 0)
                            {
                                var balance = BigInteger.Parse(hexValue, System.Globalization.NumberStyles.HexNumber).ToString();
                                onComplete?.Invoke(balance);
                            }
                            else
                            {
                                onComplete?.Invoke("0");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[RpcBalanceService] RPC call failed. Response: {responseText}");
                            onComplete?.Invoke(null);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[RpcBalanceService] Parse error: {ex.Message}");
                        onComplete?.Invoke(null);
                    }
                }
                else
                {
                    Debug.LogError($"[RpcBalanceService] RPC call failed: {request.error}");
                    onComplete?.Invoke(null);
                }
            }
        }

        private IEnumerator GetBalance(string address, Action<string> onComplete)
        {
            var jsonPayload = $"{{\"jsonrpc\":\"2.0\",\"method\":\"eth_getBalance\",\"params\":[\"{address}\",\"latest\"],\"id\":1}}";
            
            using (var request = new UnityWebRequest(_rpcUrl, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<RpcResponse>(request.downloadHandler.text);
                    if (!string.IsNullOrEmpty(response?.result))
                    {
                        var hexValue = response.result.Replace("0x", "");
                        var balance = BigInteger.Parse(hexValue, System.Globalization.NumberStyles.HexNumber).ToString();
                        onComplete?.Invoke(balance);
                    }
                    else
                    {
                        Debug.LogWarning("[RpcBalanceService] No result in response");
                        onComplete?.Invoke(null);
                    }
                }
                else
                {
                    Debug.LogError($"[RpcBalanceService] eth_getBalance failed: {request.error}");
                    onComplete?.Invoke(null);
                }
            }
        }

        [Serializable]
        private class RpcResponse
        {
            public string result;
        }
    }
}
