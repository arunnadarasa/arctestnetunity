using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace KrumpKraft
{
    /// <summary>
    /// IWalletProvider that uses MetaMask Embedded Wallets (Web3Auth) Unity SDK.
    /// Requires: (1) Web3Auth Unity package installed and a Web3Auth component in the scene.
    /// (2) WalletConfig with web3AuthClientId set (e.g. from Embedded Wallets dashboard).
    /// (3) Nethereum for signing (optional for address-only; needed for Sign pay).
    /// See https://docs.metamask.io/embedded-wallets/sdk/unity
    /// </summary>
    public class Web3AuthWalletProvider : IWalletProvider
    {
        private readonly WalletConfig _config;
        private string _address;
        private string _privateKey;
        private bool _connected;
        private object _web3AuthInstance;
        private object _messageSigner;
        private object _key;
        private bool _optionsSet;

        public bool IsConnected => _connected;
        public string ConnectedAddress => _address;
        public event Action<string> OnConnected;
        public event Action OnDisconnected;

        public string GetPrivateKey()
        {
            if (!_connected || string.IsNullOrEmpty(_privateKey))
            {
                Debug.LogWarning("[Web3AuthWalletProvider] GetPrivateKey: Wallet not connected or private key not available");
                return null;
            }
            return _privateKey;
        }

        public Web3AuthWalletProvider(WalletConfig config)
        {
            _config = config ?? new WalletConfig();
        }

        public void Connect(Action<bool> onComplete)
        {
            var web3Auth = GetWeb3Auth();
            if (web3Auth == null)
            {
                Debug.LogWarning("[KrumpKraft] Web3Auth component not found. Add the Web3Auth script from the MetaMask Embedded Wallets Unity package to a GameObject in the scene.");
                onComplete?.Invoke(false);
                return;
            }
            if (string.IsNullOrEmpty(_config.Web3AuthClientIdTrimmed))
            {
                Debug.LogWarning("[KrumpKraft] Web3Auth Client ID not set. Set web3AuthClientId in Resources/WalletConfig.json or from your Embedded Wallets dashboard.");
                onComplete?.Invoke(false);
                return;
            }

            _web3AuthInstance = web3Auth;
            EnsureOptionsSet(web3Auth);

            _pendingProvider = this;
            _pendingConnectCallback = onComplete;

            var loginEvent = GetEvent(web3Auth, "onLogin");
            var logoutEvent = GetEvent(web3Auth, "onLogout");
            if (loginEvent != null)
            {
                try
                {
                    var handler = CreateLoginDelegate(loginEvent.EventHandlerType, web3Auth);
                    if (handler != null)
                        loginEvent.AddEventHandler(web3Auth, handler);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[KrumpKraft] Web3Auth onLogin subscription failed. Use Web3AuthWalletProvider.SetLoginResult(response.privKey) in your onLogin handler. {e.Message}");
                }
            }
            if (logoutEvent != null)
            {
                try
                {
                    var handler = CreateLogoutDelegate(logoutEvent.EventHandlerType, web3Auth);
                    if (handler != null)
                        logoutEvent.AddEventHandler(web3Auth, handler);
                }
                catch { }
            }

            // Wait for setOptions (async) to fully complete before invoking login.
            (web3Auth as MonoBehaviour).StartCoroutine(ConnectAfterInitCoroutine(web3Auth));
        }

        /// <summary>
        /// Polls web3Auth.isOptionsSet then invokes login once initialization is complete.
        /// Must be started on the web3Auth MonoBehaviour since Web3AuthWalletProvider is not one.
        /// </summary>
        private IEnumerator ConnectAfterInitCoroutine(Component web3Auth)
        {
            var isOptionsSetProp = web3Auth.GetType()
                .GetProperty("isOptionsSet", BindingFlags.Public | BindingFlags.Instance);

            const float TimeoutSeconds = 15f;
            float elapsed = 0f;
            while (isOptionsSetProp == null || !(bool)isOptionsSetProp.GetValue(web3Auth))
            {
                elapsed += Time.deltaTime;
                if (elapsed > TimeoutSeconds)
                {
                    Debug.LogError("[Web3AuthWalletProvider] Timeout: Web3Auth.isOptionsSet never became true.");
                    _pendingProvider = null;
                    _pendingConnectCallback?.Invoke(false);
                    _pendingConnectCallback = null;
                    yield break;
                }
                yield return null;
            }

            var loginMethod = web3Auth.GetType().GetMethod("login", BindingFlags.Public | BindingFlags.Instance);
            if (loginMethod == null)
            {
                Debug.LogError("[Web3AuthWalletProvider] Web3Auth.login() method not found.");
                _pendingProvider = null;
                _pendingConnectCallback?.Invoke(false);
                _pendingConnectCallback = null;
                yield break;
            }

            try
            {
                var loginParamsType = Type.GetType("LoginParams") ?? web3Auth.GetType().Assembly.GetType("LoginParams");
                if (loginParamsType != null)
                {
                    var loginParams = Activator.CreateInstance(loginParamsType);

                    // Web3Auth v10 requires an explicit loginProvider — null shows a blank page.
                    // Custom Auth Connection (dashboard verifier + Google client ID): use CUSTOM_VERIFIER
                    // and loginConfig keyed by verifier id — see Web3AuthSample loginConfig.
                    var providerType = Type.GetType("Provider") ?? web3Auth.GetType().Assembly.GetType("Provider");
                    if (providerType != null && providerType.IsEnum)
                    {
                        var providerName = UsesCustomGoogleVerifier() ? "CUSTOM_VERIFIER" : "GOOGLE";
                        var providerValue = Enum.Parse(providerType, providerName);
                        var providerProp = loginParamsType.GetProperty("loginProvider",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (providerProp != null)
                            providerProp.SetValue(loginParams, providerValue);
                    }

                    // Default Google: authConnectionId must match dashboard (default w3a-google; override defaultGoogleAuthConnectionId if needed).
                    // https://docs.metamask.io/embedded-wallets/authentication/social-logins/google/
                    // Do not set params.clientId for default Google — that field is for OAuth client id, not Web3Auth project id.
                    if (UsesCustomGoogleVerifier())
                    {
                        var authConnProp = loginParamsType.GetProperty("authConnectionId",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (authConnProp != null && authConnProp.CanWrite)
                            authConnProp.SetValue(loginParams, _config.Web3AuthVerifierTrimmed);
                    }
                    else
                    {
                        var authConnProp = loginParamsType.GetProperty("authConnectionId",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (authConnProp != null && authConnProp.CanWrite)
                            authConnProp.SetValue(loginParams, _config.ResolvedDefaultGoogleAuthConnectionId);
                    }

                    loginMethod.Invoke(web3Auth, new object[] { loginParams });
                }
                else
                    loginMethod.Invoke(web3Auth, null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Web3AuthWalletProvider] Login invocation failed: {ex.Message}");
                _pendingProvider = null;
                _pendingConnectCallback?.Invoke(false);
                _pendingConnectCallback = null;
            }
        }

        private Delegate CreateLoginDelegate(Type eventHandlerType, object target)
        {
            var invoke = eventHandlerType.GetMethod("Invoke");
            if (invoke == null || invoke.GetParameters().Length == 0) return null;
            var paramType = invoke.GetParameters()[0].ParameterType;
            var ourMethod = GetType().GetMethod(nameof(OnWeb3AuthLogin), BindingFlags.NonPublic | BindingFlags.Instance);
            if (paramType.IsAssignableFrom(typeof(object)))
                return Delegate.CreateDelegate(eventHandlerType, this, ourMethod);
            var pendingField = GetType().GetField("_pendingProvider", BindingFlags.NonPublic | BindingFlags.Static);
            if (pendingField == null) return null;
            var dm = new System.Reflection.Emit.DynamicMethod("Web3AuthLoginShim", null, new[] { paramType }, GetType().Module, true);
            var il = dm.GetILGenerator();
            il.Emit(System.Reflection.Emit.OpCodes.Ldsfld, pendingField);
            il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
            il.Emit(System.Reflection.Emit.OpCodes.Box, paramType);
            il.Emit(System.Reflection.Emit.OpCodes.Call, ourMethod);
            il.Emit(System.Reflection.Emit.OpCodes.Ret);
            return dm.CreateDelegate(eventHandlerType);
        }

        private void OnWeb3AuthLogoutNoArg()
        {
            OnWeb3AuthLogout(null);
        }

        private Delegate CreateLogoutDelegate(Type eventHandlerType, object target)
        {
            var invoke = eventHandlerType.GetMethod("Invoke");
            if (invoke == null) return null;
            var paramCount = invoke.GetParameters().Length;
            if (paramCount == 0)
            {
                var noArg = GetType().GetMethod(nameof(OnWeb3AuthLogoutNoArg), BindingFlags.NonPublic | BindingFlags.Instance);
                return noArg != null ? Delegate.CreateDelegate(eventHandlerType, this, noArg) : null;
            }
            if (paramCount == 1 && invoke.GetParameters()[0].ParameterType.IsAssignableFrom(typeof(object)))
            {
                var ourMethod = GetType().GetMethod(nameof(OnWeb3AuthLogout), BindingFlags.NonPublic | BindingFlags.Instance);
                return Delegate.CreateDelegate(eventHandlerType, this, ourMethod);
            }
            return null;
        }

        private Action<bool> _pendingConnectCallback;
        private static Web3AuthWalletProvider _pendingProvider;

        /// <summary>
        /// If reflection-based event subscription fails, call this from your Web3Auth onLogin handler:
        /// Web3AuthWalletProvider.SetLoginResult(response.privKey);
        /// </summary>
        public static void SetLoginResult(string privateKey)
        {
            _pendingProvider?.ApplyLoginResult(privateKey);
        }

        internal void ApplyLoginResult(string privKey)
        {
            if (string.IsNullOrEmpty(privKey)) return;
            _privateKey = privKey.Trim();
            if (_privateKey.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) _privateKey = _privateKey.Substring(2);
            _address = DeriveAddressFromKey(_privateKey);
            if (string.IsNullOrEmpty(_address))
                _address = "0x?" + (_privateKey.Length > 6 ? _privateKey.Substring(0, 6) : "");
            LoadSigner();
            _connected = true;
            OnConnected?.Invoke(_address);
            _pendingConnectCallback?.Invoke(true);
            _pendingConnectCallback = null;
            _pendingProvider = null;
        }

        private void OnWeb3AuthLogin(object response)
        {
            if (response == null) return;
            
            var privKeyProp = response.GetType().GetProperty("privKey", BindingFlags.Public | BindingFlags.Instance);
            var privKey = privKeyProp?.GetValue(response) as string;
            if (string.IsNullOrEmpty(privKey))
            {
                _pendingProvider = null;
                _pendingConnectCallback?.Invoke(false);
                _pendingConnectCallback = null;
                return;
            }
            _privateKey = privKey.Trim();
            if (_privateKey.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) _privateKey = _privateKey.Substring(2);
            
            _address = DeriveAddressFromKey(_privateKey);
            
            if (string.IsNullOrEmpty(_address))
            {
                Debug.LogError("[Web3AuthWalletProvider] Failed to derive Ethereum address from private key. Nethereum may not be properly installed.");
                _pendingProvider = null;
                _pendingConnectCallback?.Invoke(false);
                _pendingConnectCallback = null;
                return;
            }
            
            if (_address.Length != 42)
            {
                Debug.LogError($"[Web3AuthWalletProvider] Invalid Ethereum address length: {_address.Length}. Expected 42 characters (including 0x). Address: {_address}");
                _pendingProvider = null;
                _pendingConnectCallback?.Invoke(false);
                _pendingConnectCallback = null;
                return;
            }
            
            Debug.Log($"[Web3AuthWalletProvider] Connected: {_address}");
            LoadSigner();
            _connected = true;
            OnConnected?.Invoke(_address);
            _pendingProvider = null;
            _pendingConnectCallback?.Invoke(true);
            _pendingConnectCallback = null;
        }

        private void OnWeb3AuthLogout(object _)
        {
            // setOptions() calls authorizeSession("") which reloads a session from disk.
            // Expired/stale sessions yield empty privKey and Web3Auth invokes onLogout (see Web3Auth.cs).
            // That is not a user disconnect — ignore while Connect() is still waiting for the browser flow.
            if (_pendingConnectCallback != null)
            {
                Debug.Log("[Web3AuthWalletProvider] Ignoring onLogout during pending connect (stale session from authorizeSession).");
                return;
            }

            _connected = false;
            _address = null;
            _privateKey = null;
            _messageSigner = null;
            _key = null;
            OnDisconnected?.Invoke();
        }

        public void Disconnect()
        {
            var web3Auth = _web3AuthInstance ?? GetWeb3Auth();
            var logoutMethod = web3Auth?.GetType().GetMethod("logout", BindingFlags.Public | BindingFlags.Instance);
            logoutMethod?.Invoke(web3Auth, null);
            _connected = false;
            _address = null;
            _privateKey = null;
            _messageSigner = null;
            _key = null;
            _web3AuthInstance = null;
            _pendingConnectCallback = null;
            OnDisconnected?.Invoke();
        }

        public void SignMessage(string message, Action<bool, string> onComplete)
        {
            if (!_connected)
            {
                Debug.LogError("[Web3AuthWalletProvider] SignMessage: Wallet not connected");
                onComplete?.Invoke(false, null);
                return;
            }
            if (_messageSigner == null || _key == null)
            {
                Debug.LogError($"[Web3AuthWalletProvider] SignMessage: Nethereum signer not initialized. _messageSigner is {(_messageSigner == null ? "null" : "not null")}, _key is {(_key == null ? "null" : "not null")}. " +
                    "Signing requires Nethereum. Verify Nethereum.Unity package is installed and properly configured. " +
                    "Check earlier logs for LoadSigner errors.");
                onComplete?.Invoke(false, null);
                return;
            }
            Debug.Log($"[Web3AuthWalletProvider] SignMessage: Signing message with length {message?.Length ?? 0}");
            Debug.Log($"[Web3AuthWalletProvider] Using address: {_address}");
            Debug.Log($"[Web3AuthWalletProvider] Private key (first 10 chars): {(_privateKey?.Length >= 10 ? _privateKey.Substring(0, 10) : _privateKey)}...");
            
            var signature = SignWithNethereum(message);
            Debug.Log($"[Web3AuthWalletProvider] SignMessage: Signature generated: {(!string.IsNullOrEmpty(signature) ? signature.Substring(0, Math.Min(10, signature.Length)) + "..." : "null")}");
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
                if (signMethod == null)
                {
                    Debug.LogError("[Web3AuthWalletProvider] SignHash: EthECKey.Sign(byte[]) not found");
                    onComplete?.Invoke(false, null);
                    return;
                }
                var sig = signMethod.Invoke(_key, new object[] { hash });
                if (sig == null) { onComplete?.Invoke(false, null); return; }
                var sigType = sig.GetType();
                var r = GetSigComponent(sig, sigType, "R");
                var s = GetSigComponent(sig, sigType, "S");
                var v = GetSigV(sig, sigType);
                if (r == null || s == null || r.Length != 32 || s.Length != 32)
                {
                    onComplete?.Invoke(false, null);
                    return;
                }
                // EIP-712/ecrecover: use the v (27 or 28) that recovers to our address; Nethereum often returns 27 when 28 is correct
                var expected = (ConnectedAddress ?? "").Trim().ToLowerInvariant();
                var tried27 = TryRecoverAddress(hash, r, s, 27, out var rec27);
                var tried28 = TryRecoverAddress(hash, r, s, 28, out var rec28);
                var match27 = !string.IsNullOrEmpty(expected) && tried27 && rec27?.Trim().ToLowerInvariant() == expected;
                var match28 = !string.IsNullOrEmpty(expected) && tried28 && rec28?.Trim().ToLowerInvariant() == expected;
                if (match27) v = 27;
                else if (match28) v = 28;
                else if (v == 27 && !match27)
                    v = 28; // fallback: Nethereum often returns 27 when ecrecover needs 28
                var sb = new System.Text.StringBuilder("0x", 132);
                for (var i = 0; i < 32; i++) sb.Append(r[i].ToString("x2"));
                for (var i = 0; i < 32; i++) sb.Append(s[i].ToString("x2"));
                sb.Append((v & 0xff).ToString("x2"));
                onComplete?.Invoke(true, sb.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError($"[Web3AuthWalletProvider] SignHash failed: {e.Message}");
                onComplete?.Invoke(false, null);
            }
        }

        private static byte[] GetSigComponent(object sig, Type sigType, string name)
        {
            var prop = sigType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null) return null;
            var val = prop.GetValue(sig);
            if (val == null) return null;
            if (val is byte[] arr) return arr;
            var valType = val.GetType();
            if (valType.Name == "BigInteger")
            {
                var toByteArray = valType.GetMethod("ToByteArray", Type.EmptyTypes);
                if (toByteArray == null) return null;
                var bytes = (byte[])toByteArray.Invoke(val, null);
                if (bytes == null || bytes.Length == 0) return null;
                Array.Reverse(bytes);
                var result = new byte[32];
                var copyLen = Math.Min(32, bytes.Length);
                Buffer.BlockCopy(bytes, 0, result, 32 - copyLen, copyLen);
                return result;
            }
            return null;
        }

        private static int GetSigV(object sig, Type sigType)
        {
            var prop = sigType.GetProperty("V", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
            {
                var v = prop.GetValue(sig);
                if (v is int vi) return (vi == 0 || vi == 1) ? 27 + vi : vi;
                if (v is byte vb) return (vb == 0 || vb == 1) ? 27 + vb : vb;
            }
            return 27;
        }

        private static Component GetWeb3Auth()
        {
            var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var b in behaviours)
            {
                if (b != null && b.GetType().Name == "Web3Auth")
                    return b;
            }
            return null;
        }

        /// <summary>True when WalletConfig supplies a custom Google connection (verifier + OAuth client id).</summary>
        private bool UsesCustomGoogleVerifier()
        {
            return !string.IsNullOrEmpty(_config.Web3AuthVerifierTrimmed)
                && !string.IsNullOrEmpty(_config.GoogleClientIdTrimmed);
        }

        private void EnsureOptionsSet(Component web3Auth)
        {
            if (_optionsSet) return;
            var setOptions = web3Auth.GetType().GetMethod("setOptions", BindingFlags.Public | BindingFlags.Instance);
            if (setOptions == null) return;
            var optionsType = setOptions.GetParameters()[0].ParameterType;
            var options = Activator.CreateInstance(optionsType);
            SetProp(options, "clientId", _config.Web3AuthClientIdTrimmed);
            SetProp(options, "redirectUrl", new Uri(_config.Web3AuthRedirectUrlOrDefault));
            var web3AuthType = web3Auth.GetType();
            var networkEnum = web3AuthType.GetNestedType("Network", BindingFlags.Public | BindingFlags.NonPublic);
            if (networkEnum != null && networkEnum.IsEnum)
            {
                try
                {
                    var netName = _config.Web3AuthNetworkTrimmed;
                    object netValue;
                    try
                    {
                        netValue = Enum.Parse(networkEnum, netName);
                    }
                    catch
                    {
                        Debug.LogWarning($"[Web3AuthWalletProvider] Invalid web3AuthNetwork \"{netName}\", using SAPPHIRE_DEVNET.");
                        netValue = Enum.Parse(networkEnum, "SAPPHIRE_DEVNET");
                    }
                    SetProp(options, "network", netValue);
                }
                catch { }
            }

            // chainConfig is intentionally omitted from login options.
            // Web3Auth v10's auth page silently fails to render when an unrecognised
            // custom chain (Arc Testnet) is included in the session params.
            // Chain config is only needed post-login for wallet/RPC operations.

            // loginConfig: key must be the Auth Connection ID (verifier name), not "google".
            // See Web3AuthSample — custom verifiers use { "YOUR_VERIFIER_ID", LoginConfigItem }.
            var verifier = _config.Web3AuthVerifierTrimmed;
            var googleClientId = _config.GoogleClientIdTrimmed;
            if (!string.IsNullOrEmpty(verifier) && !string.IsNullOrEmpty(googleClientId))
            {
                try
                {
                    var loginConfigItemType = Type.GetType("LoginConfigItem") ?? web3Auth.GetType().Assembly.GetType("LoginConfigItem");
                    var typeOfLoginType = Type.GetType("TypeOfLogin") ?? web3Auth.GetType().Assembly.GetType("TypeOfLogin");
                    if (loginConfigItemType != null && typeOfLoginType != null)
                    {
                        var item = Activator.CreateInstance(loginConfigItemType);
                        SetProp(item, "verifier", verifier);
                        SetProp(item, "typeOfLogin", Enum.Parse(typeOfLoginType, "GOOGLE"));
                        SetProp(item, "clientId", googleClientId);

                        var dictType = typeof(System.Collections.Generic.Dictionary<,>)
                            .MakeGenericType(typeof(string), loginConfigItemType);
                        var loginConfig = Activator.CreateInstance(dictType);
                        dictType.GetMethod("Add").Invoke(loginConfig, new object[] { verifier, item });
                        SetProp(options, "loginConfig", loginConfig);
                        Debug.Log($"[Web3AuthWalletProvider] loginConfig set: key={verifier} (custom verifier, typeOfLogin=GOOGLE)");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Web3AuthWalletProvider] Failed to set loginConfig: {ex.Message}");
                }
            }
            else
            {
                // Hosted v10: verifier key = authConnectionId (default w3a-google unless WalletConfig overrides).
                var defaultConn = _config.ResolvedDefaultGoogleAuthConnectionId;
                try
                {
                    var loginConfigItemType = Type.GetType("LoginConfigItem") ?? web3Auth.GetType().Assembly.GetType("LoginConfigItem");
                    var typeOfLoginType = Type.GetType("TypeOfLogin") ?? web3Auth.GetType().Assembly.GetType("TypeOfLogin");
                    if (loginConfigItemType != null && typeOfLoginType != null)
                    {
                        var item = Activator.CreateInstance(loginConfigItemType);
                        SetProp(item, "verifier", defaultConn);
                        SetProp(item, "typeOfLogin", Enum.Parse(typeOfLoginType, "GOOGLE"));

                        var dictType = typeof(System.Collections.Generic.Dictionary<,>)
                            .MakeGenericType(typeof(string), loginConfigItemType);
                        var loginConfig = Activator.CreateInstance(dictType);
                        dictType.GetMethod("Add").Invoke(loginConfig, new object[] { defaultConn, item });
                        SetProp(options, "loginConfig", loginConfig);
                        Debug.Log($"[Web3AuthWalletProvider] Default Google: loginConfig verifier={defaultConn} (set defaultGoogleAuthConnectionId to Dashboard → Social Connections → Google if login fails).");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Web3AuthWalletProvider] Default loginConfig failed: {ex.Message}");
                }
            }

            if (UsesCustomGoogleVerifier())
                SetProp(options, "defaultGoogleAuthConnectionId", _config.Web3AuthVerifierTrimmed);
            else
                SetProp(options, "defaultGoogleAuthConnectionId", _config.ResolvedDefaultGoogleAuthConnectionId);

            setOptions.Invoke(web3Auth, new[] { options });
            _optionsSet = true;
        }

        private static void SetProp(object obj, string name, object value)
        {
            var p = obj?.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            p?.SetValue(obj, value);
        }

        private static EventInfo GetEvent(object obj, string name)
        {
            return obj?.GetType().GetEvent(name, BindingFlags.Public | BindingFlags.Instance);
        }

        private static string DeriveAddressFromKey(string keyHex)
        {
            try
            {
                Debug.Log($"[Web3AuthWalletProvider] Attempting to derive address from private key...");
                var keyType = Type.GetType("Nethereum.Signer.EthECKey, Nethereum.Signer");
                if (keyType == null)
                {
                    Debug.LogError("[Web3AuthWalletProvider] Nethereum.Signer.EthECKey type not found!");
                    return null;
                }
                
                Debug.Log($"[Web3AuthWalletProvider] Found EthECKey type: {keyType.FullName}");
                var key = Activator.CreateInstance(keyType, new object[] { keyHex });
                Debug.Log($"[Web3AuthWalletProvider] Created EthECKey instance");
                
                var getAddress = keyType.GetMethod("GetPublicAddress", Type.EmptyTypes);
                if (getAddress == null)
                {
                    Debug.LogError("[Web3AuthWalletProvider] GetPublicAddress() method not found!");
                    return null;
                }
                
                var address = getAddress.Invoke(key, null) as string;
                Debug.Log($"[Web3AuthWalletProvider] Derived address: {address} (length: {address?.Length ?? 0})");
                return address;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Web3AuthWalletProvider] Failed to derive address: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        private void LoadSigner()
        {
            if (string.IsNullOrEmpty(_privateKey))
            {
                Debug.LogWarning("[Web3AuthWalletProvider] LoadSigner: privateKey is empty");
                return;
            }
            try
            {
                Debug.Log("[Web3AuthWalletProvider] LoadSigner: Attempting to load Nethereum types...");
                var keyType = Type.GetType("Nethereum.Signer.EthECKey, Nethereum.Signer");
                var signerType = Type.GetType("Nethereum.Signer.EthereumMessageSigner, Nethereum.Signer");
                if (keyType == null)
                {
                    Debug.LogError("[Web3AuthWalletProvider] LoadSigner: Nethereum.Signer.EthECKey type not found. Nethereum may not be properly installed.");
                    return;
                }
                if (signerType == null)
                {
                    Debug.LogError("[Web3AuthWalletProvider] LoadSigner: Nethereum.Signer.EthereumMessageSigner type not found. Nethereum may not be properly installed.");
                    return;
                }
                Debug.Log("[Web3AuthWalletProvider] LoadSigner: Creating EthECKey instance...");
                Debug.Log($"[Web3AuthWalletProvider] LoadSigner: Using private key (first 10 chars): {(_privateKey?.Length >= 10 ? _privateKey.Substring(0, 10) : _privateKey)}...");
                _key = Activator.CreateInstance(keyType, new object[] { _privateKey });
                
                // Verify the key's address (optional - don't fail if this errors)
                try
                {
                    var getPublicAddressMethod = keyType.GetMethod("GetPublicAddress", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                    if (getPublicAddressMethod != null)
                    {
                        var keyAddress = getPublicAddressMethod.Invoke(_key, null) as string;
                        Debug.Log($"[Web3AuthWalletProvider] LoadSigner: Key derives to address: {keyAddress}");
                        Debug.Log($"[Web3AuthWalletProvider] LoadSigner: Expected address: {_address}");
                        
                        if (!string.Equals(keyAddress, _address, StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.LogError($"[Web3AuthWalletProvider] LoadSigner: ADDRESS MISMATCH! Key address: {keyAddress}, Expected: {_address}");
                        }
                    }
                }
                catch (Exception verifyEx)
                {
                    Debug.LogWarning($"[Web3AuthWalletProvider] LoadSigner: Could not verify key address: {verifyEx.Message}");
                }
                
                Debug.Log("[Web3AuthWalletProvider] LoadSigner: Creating EthereumMessageSigner instance...");
                _messageSigner = Activator.CreateInstance(signerType);
                Debug.Log("[Web3AuthWalletProvider] LoadSigner: Successfully loaded Nethereum signer");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Web3AuthWalletProvider] LoadSigner failed: {e.Message}\n{e.StackTrace}");
            }
        }

        private string SignWithNethereum(string message)
        {
            if (_key == null)
            {
                Debug.LogError("[Web3AuthWalletProvider] SignWithNethereum: _key is null");
                return null;
            }
            try
            {
                Debug.Log("[Web3AuthWalletProvider] SignWithNethereum: Signing with EIP-191 (personal_sign)");
                
                var messageBytes = System.Text.Encoding.UTF8.GetBytes(message);
                Debug.Log($"[Web3AuthWalletProvider] Message bytes length: {messageBytes.Length}");

                // Use EncodeUTF8AndSign for EIP-191 signing (same as personal_sign in MetaMask)
                // This adds the "\x19Ethereum Signed Message:\n{length}" prefix before hashing
                var encodeMethod = _messageSigner.GetType().GetMethod("EncodeUTF8AndSign",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(string), _key.GetType() },
                    null);
                
                if (encodeMethod == null)
                {
                    Debug.LogError("[Web3AuthWalletProvider] EncodeUTF8AndSign method not found");
                    return null;
                }
                
                Debug.Log("[Web3AuthWalletProvider] Calling EncodeUTF8AndSign with EIP-191 format");
                var signature = encodeMethod.Invoke(_messageSigner, new[] { message, _key }) as string;
                
                if (string.IsNullOrEmpty(signature))
                {
                    Debug.LogError("[Web3AuthWalletProvider] EncodeUTF8AndSign returned null or empty signature");
                    return null;
                }
                
                Debug.Log($"[Web3AuthWalletProvider] SignWithNethereum: EIP-191 signature generated: {signature.Substring(0, Math.Min(20, signature.Length))}...");
                
                // Verify the signature recovers to our address
                var recoverMethod = _messageSigner.GetType().GetMethod("EncodeUTF8AndEcRecover",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(string), typeof(string) },
                    null);
                
                if (recoverMethod != null)
                {
                    var recoveredAddress = recoverMethod.Invoke(_messageSigner, new object[] { message, signature }) as string;
                    Debug.Log($"[Web3AuthWalletProvider] Signature verification: recovers to {recoveredAddress}");
                    Debug.Log($"[Web3AuthWalletProvider] Expected address: {ConnectedAddress}");
                    
                    if (!string.Equals(recoveredAddress, ConnectedAddress, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.LogError($"[Web3AuthWalletProvider] Signature verification FAILED! Recovered {recoveredAddress} != Expected {ConnectedAddress}");
                    }
                    else
                    {
                        Debug.Log("[Web3AuthWalletProvider] Signature verification PASSED!");
                    }
                }
                
                return signature;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Web3AuthWalletProvider] SignWithNethereum failed: {e.Message}\n{e.StackTrace}");
                return null;
            }
        }
        
        private byte CalculateRecoveryId(object key, byte[] hash, byte[] r, byte[] s)
        {
            // Try both recovery IDs (27 and 28) and see which one recovers to our address
            var keyType = key.GetType();
            var getPublicAddressMethod = keyType.GetMethod("GetPublicAddress", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            
            if (getPublicAddressMethod == null)
            {
                Debug.LogWarning("[Web3AuthWalletProvider] CalculateRecoveryId: Could not find GetPublicAddress method, using default V=27");
                return 27;
            }
            
            var expectedAddress = (getPublicAddressMethod.Invoke(key, null) as string)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(expectedAddress))
            {
                Debug.LogWarning("[Web3AuthWalletProvider] CalculateRecoveryId: Could not get expected address, using default V=27");
                return 27;
            }
            
            Debug.Log($"[Web3AuthWalletProvider] CalculateRecoveryId: Expected address: {expectedAddress}");
            
            // Try V=27
            if (TryRecoverAddress(hash, r, s, 27, out string recoveredAddress27))
            {
                Debug.Log($"[Web3AuthWalletProvider] CalculateRecoveryId: V=27 recovers to: {recoveredAddress27}");
                if (recoveredAddress27?.ToLowerInvariant() == expectedAddress)
                {
                    return 27;
                }
            }
            
            // Try V=28
            if (TryRecoverAddress(hash, r, s, 28, out string recoveredAddress28))
            {
                Debug.Log($"[Web3AuthWalletProvider] CalculateRecoveryId: V=28 recovers to: {recoveredAddress28}");
                if (recoveredAddress28?.ToLowerInvariant() == expectedAddress)
                {
                    return 28;
                }
            }
            
            Debug.LogWarning($"[Web3AuthWalletProvider] CalculateRecoveryId: Neither V=27 nor V=28 recovered the expected address. Using V=27 by default.");
            return 27;
        }
        
        private bool TryRecoverAddress(byte[] hash, byte[] r, byte[] s, byte v, out string address)
        {
            address = null;
            try
            {
                // Use Nethereum's EthECKey.RecoverFromSignature (disambiguate overload to avoid "Ambiguous match found")
                var keyType = Type.GetType("Nethereum.Signer.EthECKey, Nethereum.Signer");
                if (keyType == null) return false;

                var sigType = Type.GetType("Nethereum.Signer.EthECDSASignature, Nethereum.Signer");
                if (sigType == null) return false;

                // Nethereum RecoverFromSignature(EthECDSASignature signature, int recId, byte[] hash) — order is signature, recId, hash
                var recoverMethod = keyType.GetMethod("RecoverFromSignature", BindingFlags.Public | BindingFlags.Static, null, new Type[] { sigType, typeof(int), typeof(byte[]) }, null);
                if (recoverMethod == null) return false;

                var sigConstructor = sigType.GetConstructor(new[] { typeof(byte[]), typeof(byte[]) });
                if (sigConstructor == null) return false;

                var signature = sigConstructor.Invoke(new object[] { r, s });
                var recoveryId = (v == 27 || v == 28) ? (v - 27) : 0;

                var recoveredKey = recoverMethod.Invoke(null, new object[] { signature, recoveryId, hash });
                if (recoveredKey == null) return false;
                
                // Get the address
                var getAddressMethod = keyType.GetMethod("GetPublicAddress", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (getAddressMethod == null) return false;
                
                address = getAddressMethod.Invoke(recoveredKey, null) as string;
                return !string.IsNullOrEmpty(address);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Web3AuthWalletProvider] TryRecoverAddress failed for V={v}: {e.Message}");
                return false;
            }
        }
        
        private static byte[] Keccak256(byte[] input)
        {
            var hasherType = Type.GetType("Nethereum.Util.Sha3Keccack, Nethereum.Util");
            if (hasherType == null)
            {
                Debug.LogError("[Web3AuthWalletProvider] Keccak256: Sha3Keccack type not found");
                return null;
            }
            
            var hasher = Activator.CreateInstance(hasherType);
            var calculateHashMethod = hasherType.GetMethod("CalculateHash", new[] { typeof(byte[]) });
            if (calculateHashMethod == null)
            {
                Debug.LogError("[Web3AuthWalletProvider] Keccak256: CalculateHash method not found");
                return null;
            }
            
            return calculateHashMethod.Invoke(hasher, new object[] { input }) as byte[];
        }
    }
}
