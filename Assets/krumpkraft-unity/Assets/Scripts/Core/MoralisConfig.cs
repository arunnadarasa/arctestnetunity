using UnityEngine;
using UnityEngine.Networking;

namespace KrumpKraft
{
    [System.Serializable]
    public class MoralisConfig
    {
        public string apiKey;
        public string baseUrl = "https://deep-index.moralis.io/api";
        public string version = "v2";

        public string GetFullUrl(string path)
        {
            var baseUrlTrimmed = (baseUrl ?? "").TrimEnd('/');
            var versionTrimmed = (version ?? "v2").TrimStart('/').TrimEnd('/');
            var pathTrimmed = (path ?? "").TrimStart('/');
            return $"{baseUrlTrimmed}/{versionTrimmed}/{pathTrimmed}";
        }

        public UnityWebRequest GetRequest(string path)
        {
            var url = GetFullUrl(path);
            var req = UnityWebRequest.Get(url);
            if (!string.IsNullOrEmpty(apiKey))
                req.SetRequestHeader("X-API-Key", apiKey);
            return req;
        }

        public UnityWebRequest PostRequest(string path, string bodyJson)
        {
            var url = GetFullUrl(path);
            var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(bodyJson ?? "{}"));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(apiKey))
                req.SetRequestHeader("X-API-Key", apiKey);
            return req;
        }
    }
}
