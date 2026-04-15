using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace KrumpKraft
{
    /// <summary>
    /// Optional stub for HTTP 402 Payment Required / payment relay.
    /// Can POST signed pay payloads to a configurable relay URL (e.g. fisher endpoint).
    /// </summary>
    public class X402PaymentService : MonoBehaviour
    {
        [SerializeField] private string relayUrl = "";
        [SerializeField] private float timeoutSeconds = 10f;

        private WalletService _wallet;

        private void Start()
        {
            var manager = KrumpKraftManager.Instance;
            if (manager != null)
                _wallet = manager.WalletService;
        }

        /// <summary>
        /// POST a signed pay payload (JSON) to the configured relay URL.
        /// </summary>
        public void SendToRelay(string signedPayJson, Action<bool, string> onComplete)
        {
            if (string.IsNullOrEmpty(relayUrl))
            {
                onComplete?.Invoke(false, "Relay URL not set.");
                return;
            }
            if (string.IsNullOrEmpty(signedPayJson))
            {
                onComplete?.Invoke(false, "No payload.");
                return;
            }
            StartCoroutine(PostPayload(signedPayJson, onComplete));
        }

        private IEnumerator PostPayload(string json, Action<bool, string> onComplete)
        {
            using (var req = new UnityWebRequest(relayUrl, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = (int)Mathf.Max(1f, timeoutSeconds);
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onComplete?.Invoke(false, req.error ?? req.downloadHandler?.text ?? "Request failed");
                    yield break;
                }
                onComplete?.Invoke(true, req.downloadHandler?.text ?? "");
            }
        }

        /// <summary>
        /// Set relay URL at runtime (e.g. from config).
        /// </summary>
        public void SetRelayUrl(string url)
        {
            relayUrl = url ?? "";
        }
    }
}
