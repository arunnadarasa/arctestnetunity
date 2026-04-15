using System.Collections;
using System;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using UnityEngine.Networking;
using UnityEngine;
using System.Collections.Generic;

public class Web3AuthApi
{
    static Web3AuthApi instance;
    /// <summary>v2 store — must match what auth.web3auth.io reads (session-service v2).</summary>
    static string baseAddress = "https://session.web3auth.io/v2";

    public static Web3AuthApi getInstance()
    {
        if (instance == null)
            instance = new Web3AuthApi();
        return instance;
    }

    static string GetOriginFromRedirectUrl(string redirectUrl)
    {
        if (string.IsNullOrEmpty(redirectUrl))
            return "*";
        if (!Uri.TryCreate(redirectUrl, UriKind.Absolute, out var uri))
            return "*";
        return uri.GetLeftPart(UriPartial.Authority);
    }

    public IEnumerator authorizeSession(string key, string redirectUrlForOrigin, Action<StoreApiResponse> callback)
    {
        WWWForm data = new WWWForm();
        data.AddField("key", key);

        var request = UnityWebRequest.Post($"{baseAddress}/store/get", data);
        request.SetRequestHeader("origin", GetOriginFromRedirectUrl(redirectUrlForOrigin));

        yield return request.SendWebRequest();
        // Debug.Log("baseAddress =>" + baseAddress);
        // Debug.Log("key =>" + key);
        // Debug.Log("request URL =>"+ requestURL);
        // Debug.Log("request.isNetworkError =>" + request.isNetworkError);
        // Debug.Log("request.isHttpError =>" + request.isHttpError);
        // Debug.Log("request.isHttpError =>" + request.error);
        // Debug.Log("request.result =>" + request.result);
        // Debug.Log("request.downloadHandler.text =>" + request.downloadHandler.text);
        if (request.result == UnityWebRequest.Result.Success)
        {
            string result = request.downloadHandler.text;
            callback(Newtonsoft.Json.JsonConvert.DeserializeObject<StoreApiResponse>(result));
        }
        else
            callback(null);
    }

    public IEnumerator logout(LogoutApiRequest logoutApiRequest, Action<JObject> callback)
    {
        WWWForm data = new WWWForm();
        data.AddField("key", logoutApiRequest.key);
        data.AddField("data", logoutApiRequest.data);
        data.AddField("signature", logoutApiRequest.signature);
        data.AddField("timeout", logoutApiRequest.timeout.ToString());
        data.AddField("allowedOrigin", string.IsNullOrEmpty(logoutApiRequest.allowedOrigin) ? "*" : logoutApiRequest.allowedOrigin);
        // Debug.Log("key during logout session =>" + logoutApiRequest.key);

        var request = UnityWebRequest.Post($"{baseAddress}/store/set", data);
        yield return request.SendWebRequest();

        // Debug.Log("baseAddress =>" + baseAddress);
        // Debug.Log("key =>" + logoutApiRequest.key);
        // Debug.Log("request URL =>"+ requestURL);
        // Debug.Log("request.isNetworkError =>" + request.isNetworkError);
        // Debug.Log("request.isHttpError =>" + request.isHttpError);
        // Debug.Log("request.isHttpError =>" + request.error);
        // Debug.Log("request.result =>" + request.result);
        // Debug.Log("request.downloadHandler.text =>" + request.downloadHandler.text);

        if (request.result == UnityWebRequest.Result.Success)
        {
            string result = request.downloadHandler.text;
            callback(Newtonsoft.Json.JsonConvert.DeserializeObject<JObject>(result));
        }
        else
            callback(null);
    }

    public IEnumerator createSession(LogoutApiRequest logoutApiRequest, Action<JObject> callback)
    {
        WWWForm data = new WWWForm();
        data.AddField("key", logoutApiRequest.key);
        data.AddField("data", logoutApiRequest.data);
        data.AddField("signature", logoutApiRequest.signature);
        data.AddField("timeout", logoutApiRequest.timeout.ToString());
        data.AddField("allowedOrigin", string.IsNullOrEmpty(logoutApiRequest.allowedOrigin) ? "*" : logoutApiRequest.allowedOrigin);
        // Debug.Log("key during create session =>" + logoutApiRequest.key);
        var request = UnityWebRequest.Post($"{baseAddress}/store/set", data);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string result = request.downloadHandler.text;
            callback(Newtonsoft.Json.JsonConvert.DeserializeObject<JObject>(result));
        }
        else
            callback(null);
    }

    public IEnumerator fetchProjectConfig(string project_id, string network, Action<ProjectConfigResponse> callback)
    {
        //Debug.Log("network =>" + network);
        string baseUrl = SIGNER_MAP[network];
        var requestURL = $"{baseUrl}/api/configuration?project_id={project_id}&network={network}&whitelist=true";
        var request = UnityWebRequest.Get(requestURL);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string result = request.downloadHandler.text;
            callback(Newtonsoft.Json.JsonConvert.DeserializeObject<ProjectConfigResponse>(result));
        }
        else
            callback(null);
    }

    public static Dictionary<string, string> SIGNER_MAP = new Dictionary<string, string>()
    {
        { "mainnet", "https://signer.web3auth.io" },
        { "testnet", "https://signer.web3auth.io" },
        { "cyan", "https://signer-polygon.web3auth.io" },
        { "aqua", "https://signer-polygon.web3auth.io" },
        { "sapphire_mainnet", "https://signer.web3auth.io" },
        { "sapphire_devnet", "https://signer.web3auth.io" }
    };
}
