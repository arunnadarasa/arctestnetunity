using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace KrumpKraft
{
    public class PaymentTestUI : MonoBehaviour
    {
        [SerializeField] private TMPro.TMP_InputField recipientInput;
        [SerializeField] private TMPro.TMP_Dropdown tokenDropdown;
        [SerializeField] private TMPro.TMP_InputField amountInput;
        [SerializeField] private Button payButton;
        [SerializeField] private Button signPayButton;
        [SerializeField] private Button copyPayloadButton;
        [SerializeField] private Button sendToRelayButton;
        [SerializeField] private Button approveEvvmButton;
        [SerializeField] private Button depositEvvmButton;
        [SerializeField] private TMPro.TMP_Text statusText;
        [SerializeField] private TMPro.TMP_Text transactionLinkText;
        [Tooltip("When checked, POST always goes to http://localhost:7350 (ignores Relay Url). Uncheck to use Relay Url below.")]
        [SerializeField] private bool useLocalRelayer = true;
        [Tooltip("Relayer base URL when Use Local Relayer is off (e.g. https://krump-x402-relayer.fly.dev). Path /x402/pay is appended if URL has no path.")]
        [SerializeField] private string relayUrl = "https://krump-x402-relayer.fly.dev";
        [Tooltip("Path to append when relay URL has no path. Relayer expects /x402/pay.")]
        [SerializeField] private string relayPath = "/x402/pay";

        private string EffectiveRelayUrl => useLocalRelayer ? "http://localhost:7350" : (relayUrl ?? "").Trim();

        private WalletService _wallet;
        private TransactionService _txService;
        private ChainSettings _chainSettings;
        private string _lastSignedPayload;
        private string _lastTransactionHash;
        private bool _useTmp = true;

        private enum TokenType
        {
            DHP = 0,
            USDCHealth = 1,
            NativeUsdc = 2
        }

        private void Start()
        {
            var manager = KrumpKraftManager.Instance;
            if (manager == null) return;
            _wallet = manager.WalletService;
            if (_wallet == null) return;

            _chainSettings = manager.ChainSettings;

            _txService = gameObject.GetComponent<TransactionService>();
            if (_txService == null)
                _txService = gameObject.AddComponent<TransactionService>();

            if (tokenDropdown != null)
            {
                tokenDropdown.ClearOptions();
                tokenDropdown.AddOptions(new List<string> { "DHP", "USDC Health", "USDC (native)" });
                tokenDropdown.RefreshShownValue();
                tokenDropdown.onValueChanged.AddListener(_ => UpdateX402Visibility());
            }
            if (payButton != null)
                payButton.onClick.AddListener(OnPayClicked);
            if (signPayButton != null)
                signPayButton.onClick.AddListener(OnSignPayClicked);
            if (copyPayloadButton != null)
                copyPayloadButton.onClick.AddListener(OnCopyPayloadClicked);
            if (sendToRelayButton != null)
                sendToRelayButton.onClick.AddListener(OnPayViaX402Clicked);
            if (approveEvvmButton != null)
                approveEvvmButton.onClick.AddListener(OnApproveEvvmClicked);
            if (depositEvvmButton != null)
                depositEvvmButton.onClick.AddListener(OnDepositEvvmClicked);
            
            EnsureFundEvvmButtons();
            UpdateX402Visibility();
            
            if (transactionLinkText != null)
            {
                var button = transactionLinkText.GetComponent<Button>();
                if (button == null)
                {
                    button = transactionLinkText.gameObject.AddComponent<Button>();
                }
                button.onClick.AddListener(OnTransactionLinkClicked);
            }
            
            SetStatus("");
            ClearTransactionLink();
        }

        /// <summary>
        /// If Approve/Deposit EVVM buttons were not assigned in the Inspector, create them at runtime from the Pay via x402 button template.
        /// </summary>
        private void EnsureFundEvvmButtons()
        {
            var template = sendToRelayButton ?? copyPayloadButton;
            if (template == null) return;
            var parent = template.transform.parent;
            if (parent == null) return;

            var templateRect = template.GetComponent<RectTransform>();
            var fallbackSize = (templateRect != null && templateRect.sizeDelta.x > 0 && templateRect.sizeDelta.y > 0)
                ? templateRect.sizeDelta : new UnityEngine.Vector2(160f, 40f);
            if (approveEvvmButton == null)
            {
                var go = Instantiate(template.gameObject, parent);
                go.name = "ApproveEvvmButton";
                go.SetActive(true);
                var rect = go.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.sizeDelta = fallbackSize;
                    if (templateRect != null) rect.anchoredPosition = templateRect.anchoredPosition;
                }
                approveEvvmButton = go.GetComponent<Button>();
                if (approveEvvmButton != null)
                {
                    approveEvvmButton.onClick.RemoveAllListeners();
                    approveEvvmButton.onClick.AddListener(OnApproveEvvmClicked);
                    var label = go.GetComponentInChildren<TMPro.TMP_Text>(true);
                    if (label != null) label.text = "1. Approve USDCh";
                }
            }
            if (depositEvvmButton == null)
            {
                var go = Instantiate(template.gameObject, parent);
                go.name = "DepositEvvmButton";
                go.SetActive(true);
                var rect = go.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.sizeDelta = fallbackSize;
                    if (templateRect != null) rect.anchoredPosition = templateRect.anchoredPosition;
                }
                depositEvvmButton = go.GetComponent<Button>();
                if (depositEvvmButton != null)
                {
                    depositEvvmButton.onClick.RemoveAllListeners();
                    depositEvvmButton.onClick.AddListener(OnDepositEvvmClicked);
                    var label = go.GetComponentInChildren<TMPro.TMP_Text>(true);
                    if (label != null) label.text = "2. Deposit to EVVM";
                }
            }
            OrderFundEvvmButtons();
        }

        /// <summary>
        /// If the button's RectTransform has zero size (cloned before layout ran), set a visible size from template or default.
        /// </summary>
        private static void EnsureButtonSize(GameObject button, GameObject template)
        {
            var rect = button != null ? button.GetComponent<RectTransform>() : null;
            if (rect == null) return;
            if (rect.sizeDelta.x > 0 && rect.sizeDelta.y > 0) return;
            var refRect = template != null ? template.GetComponent<RectTransform>() : null;
            rect.sizeDelta = (refRect != null && refRect.sizeDelta.x > 0 && refRect.sizeDelta.y > 0)
                ? refRect.sizeDelta : new UnityEngine.Vector2(160f, 40f);
        }

        /// <summary>
        /// Order the 3 step buttons at the start of the parent (0,1,2) so layout shows them first and they are not clipped.
        /// </summary>
        private void OrderFundEvvmButtons()
        {
            var parent = sendToRelayButton != null ? sendToRelayButton.transform.parent : null;
            if (parent == null) return;
            int at = 0;
            if (approveEvvmButton != null) { approveEvvmButton.transform.SetParent(parent, false); approveEvvmButton.transform.SetSiblingIndex(at++); }
            if (depositEvvmButton != null) { depositEvvmButton.transform.SetParent(parent, false); depositEvvmButton.transform.SetSiblingIndex(at++); }
            if (sendToRelayButton != null) sendToRelayButton.transform.SetSiblingIndex(at++);
            // Position the three buttons below the USDC Health row (token dropdown), stacked vertically.
            const float buttonSpacing = 44f;
            const float gapBelowDropdown = 8f;
            var sendRect = sendToRelayButton != null ? sendToRelayButton.GetComponent<RectTransform>() : null;
            var dropdownRect = tokenDropdown != null ? tokenDropdown.GetComponent<RectTransform>() : null;
            float posX = sendRect != null ? sendRect.anchoredPosition.x : 0f;
            float topButtonY;
            if (dropdownRect != null)
            {
                // Place stack so top button is just below the dropdown (same parent space).
                float dropdownBottomY = dropdownRect.anchoredPosition.y + dropdownRect.rect.yMin;
                float buttonHeight = sendRect != null ? sendRect.rect.height : 40f;
                topButtonY = dropdownBottomY - gapBelowDropdown - buttonHeight * 0.5f;
                posX = dropdownRect.anchoredPosition.x;
            }
            else
            {
                topButtonY = sendRect != null ? sendRect.anchoredPosition.y + buttonSpacing * 2f : 0f;
            }
            if (approveEvvmButton != null)
            {
                var r = approveEvvmButton.GetComponent<RectTransform>();
                if (r != null) r.anchoredPosition = new UnityEngine.Vector2(posX, topButtonY);
            }
            if (depositEvvmButton != null)
            {
                var r = depositEvvmButton.GetComponent<RectTransform>();
                if (r != null) r.anchoredPosition = new UnityEngine.Vector2(posX, topButtonY - buttonSpacing);
            }
            if (sendRect != null)
                sendRect.anchoredPosition = new UnityEngine.Vector2(posX, topButtonY - buttonSpacing * 2f);
        }

        /// <summary>
        /// Show x402 section (Pay via x402, Copy payload) only when USDC Health is selected.
        /// DHP and USDC (native) use Pay only (no x402).
        /// </summary>
        private void UpdateX402Visibility()
        {
            var tokenIndex = tokenDropdown != null ? tokenDropdown.value : 0;
            var isUSDCHealth = (TokenType)tokenIndex == TokenType.USDCHealth;
            if (sendToRelayButton != null)
            {
                sendToRelayButton.gameObject.SetActive(isUSDCHealth);
                var label = sendToRelayButton.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (label != null)
                    label.text = "3. Pay via x402";
            }
            if (signPayButton != null)
                signPayButton.gameObject.SetActive(false);
            if (copyPayloadButton != null)
                copyPayloadButton.gameObject.SetActive(false);
            if (approveEvvmButton != null)
            {
                approveEvvmButton.gameObject.SetActive(isUSDCHealth);
                EnsureButtonSize(approveEvvmButton.gameObject, sendToRelayButton != null ? sendToRelayButton.gameObject : null);
                var approveLabel = approveEvvmButton.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (approveLabel != null) approveLabel.text = "1. Approve USDCh";
            }
            if (depositEvvmButton != null)
            {
                depositEvvmButton.gameObject.SetActive(isUSDCHealth);
                EnsureButtonSize(depositEvvmButton.gameObject, sendToRelayButton != null ? sendToRelayButton.gameObject : null);
                var depositLabel = depositEvvmButton.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (depositLabel != null) depositLabel.text = "2. Deposit to EVVM";
            }
            OrderFundEvvmButtons();
        }

        private void OnApproveEvvmClicked()
        {
            if (_wallet == null || !_wallet.IsConnected)
            {
                SetStatus("Wallet not connected.");
                return;
            }
            if (!ParseAmount(amountInput != null ? amountInput.text : "", out var amountWei, out _))
            {
                SetStatus("Enter a valid amount (e.g. 1 or 0.01).");
                return;
            }
            SetStatus("Approving USDCh…");
            _txService.ApproveErc20(ContractAddresses.USDCHealth, ContractAddresses.Treasury, amountWei, (ok, msg) =>
            {
                if (ok)
                    SetStatus("Approved. Now use \"2. Deposit to EVVM\".");
                else
                    SetStatus("Approve failed: " + msg);
            });
        }

        private void OnDepositEvvmClicked()
        {
            if (_wallet == null || !_wallet.IsConnected)
            {
                SetStatus("Wallet not connected.");
                return;
            }
            if (!ParseAmount(amountInput != null ? amountInput.text : "", out var amountWei, out _))
            {
                SetStatus("Enter a valid amount (e.g. 1 or 0.01).");
                return;
            }
            SetStatus("Depositing to EVVM…");
            _txService.DepositToEvvmTreasury(amountWei, (ok, msg) =>
            {
                if (ok)
                    SetStatus("Deposited. You can now use Pay via x402.");
                else
                    SetStatus("Deposit failed: " + msg);
            });
        }

        /// <summary>
        /// One-click x402 for USDC Krump: sign pay payload then send to relay.
        /// </summary>
        private void OnPayViaX402Clicked()
        {
            if (_wallet == null || !_wallet.IsConnected)
            {
                SetStatus("Wallet not connected.");
                return;
            }
            var recipient = recipientInput != null ? recipientInput.text?.Trim() : "";
            if (string.IsNullOrEmpty(recipient))
            {
                SetStatus("Enter recipient address.");
                return;
            }
            if (!ParseAmount(amountInput != null ? amountInput.text : "", out var amountWei, out _))
            {
                SetStatus("Invalid amount.");
                return;
            }
            if (string.IsNullOrWhiteSpace(EffectiveRelayUrl))
            {
                SetStatus("Relay URL not set.");
                return;
            }
            var token = ContractAddresses.USDCHealth;
            var priorityFeeWei = "0";
            var senderExecutor = ContractAddresses.ZeroAddress;
            const bool isAsyncExec = false; // x402/EVVM adapter expects sync execution (see BUILDING_WITH_EVVM_X402_STORY_AENEID.md)
            SetStatus("Fetching sync nonce…");
            _txService.GetNextSyncNonce(_wallet.ConnectedAddress, (nonceSuccess, evvmNonce) =>
            {
                if (!nonceSuccess)
                {
                    SetStatus("Sync nonce failed: " + evvmNonce);
                    return;
                }
                var payload = PayPayloadBuilder.BuildSignaturePayload(
                    recipient, "", token, amountWei, priorityFeeWei,
                    senderExecutor, evvmNonce, isAsyncExec);
                SetStatus("Signing EVVM…");
                _wallet.SignMessage(payload, (evvmOk, evvmSignature) =>
                {
                    if (!evvmOk || string.IsNullOrEmpty(evvmSignature))
                    {
                        SetStatus("EVVM sign failed.");
                        return;
                    }
                    var from = _wallet.ConnectedAddress;
                    // EIP-712 TransferWithAuthorization must have to = adapter (authorize adapter to pull USDC). Payment recipient is only for relayer/contract call, not for the signed message.
                    var x402ToAddress = ContractAddresses.EvvmX402Adapter;
                    var x402NonceBytes32 = GenerateReceiptId();
                    const ulong validAfter = 0;
                    var validBefore = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600;
                    var digest = X402Eip712.DigestTransferWithAuthorization(from, x402ToAddress, amountWei, validAfter, validBefore, x402NonceBytes32);
                    SetStatus("Signing x402…");
                    _wallet.SignHash(digest, (x402Ok, x402Sig) =>
                    {
                        if (!x402Ok || string.IsNullOrEmpty(x402Sig) || x402Sig.Length < 132)
                        {
                            SetStatus("x402 sign failed.");
                            return;
                        }
                        SplitSignature(x402Sig, out var v, out var r, out var s);
                        _lastSignedPayload = BuildRelayerPayload(from, recipient, amountWei, validAfter, validBefore, x402NonceBytes32, v, r, s, evvmNonce, evvmSignature, isAsyncExec, token, priorityFeeWei, senderExecutor);
                        StartCoroutine(PostToRelay(EffectiveRelayUrl, _lastSignedPayload));
                    });
                });
            });
        }

        private void OnPayClicked()
        {
            if (_wallet == null || !_wallet.IsConnected)
            {
                SetStatus("Wallet not connected.");
                return;
            }
            var recipient = recipientInput != null ? recipientInput.text?.Trim() : "";
            if (string.IsNullOrEmpty(recipient))
            {
                SetStatus("Enter recipient address.");
                return;
            }
            if (!ParseAmount(amountInput != null ? amountInput.text : "", out var amountWei, out var decimals))
            {
                SetStatus("Invalid amount.");
                return;
            }

            var tokenIndex = tokenDropdown != null ? tokenDropdown.value : 0;
            var tokenType = (TokenType)tokenIndex;

            switch (tokenType)
            {
                case TokenType.DHP:
                    PayWithDHP(recipient, amountWei);
                    break;
                case TokenType.USDCHealth:
                    PayWithUSDCHealth(recipient, amountWei);
                    break;
                case TokenType.NativeUsdc:
                    PayWithNativeUsdc(recipient, amountWei);
                    break;
            }
        }

        private void PayWithDHP(string recipient, string amountWei)
        {
            SetStatus("Preparing DHP payment...");
            
            var token = ContractAddresses.DhpPrincipal;
            var priorityFeeWei = "0";
            // Use the connected wallet address as executor (matching backend behavior)
            var senderExecutor = _wallet.ConnectedAddress;
            var isAsyncExec = false;

            if (isAsyncExec)
            {
                var nonce = DateTime.UtcNow.Ticks.ToString();
                SignAndSendDHP(recipient, token, amountWei, priorityFeeWei, senderExecutor, nonce, isAsyncExec);
            }
            else
            {
                SetStatus("Fetching sync nonce...");
                _txService.GetNextSyncNonce(_wallet.ConnectedAddress, (success, nonce) =>
                {
                    if (!success)
                    {
                        SetStatus($"Failed to fetch sync nonce: {nonce}");
                        return;
                    }
                    
                    Debug.Log($"[PaymentTestUI] Using sync nonce: {nonce}");
                    SignAndSendDHP(recipient, token, amountWei, priorityFeeWei, senderExecutor, nonce, isAsyncExec);
                });
            }
        }
        
        private void SignAndSendDHP(string recipient, string token, string amountWei, string priorityFeeWei, 
            string senderExecutor, string nonce, bool isAsyncExec)
        {
            var payload = PayPayloadBuilder.BuildSignaturePayload(
                recipient, "", token, amountWei, priorityFeeWei,
                senderExecutor, nonce, isAsyncExec);

            Debug.Log($"[PaymentTestUI] Message to sign: {payload}");
            Debug.Log($"[PaymentTestUI] Params - recipient:{recipient}, token:{token}, amount:{amountWei}, priorityFee:{priorityFeeWei}, executor:{senderExecutor}, nonce:{nonce}, isAsync:{isAsyncExec}");

            _wallet.SignMessage(payload, (success, signature) =>
            {
                if (!success || string.IsNullOrEmpty(signature))
                {
                    SetStatus("DHP payment failed: Signature failed.");
                    return;
                }
                
                SendDHPToCore(recipient, token, amountWei, priorityFeeWei, senderExecutor, nonce, isAsyncExec, signature);
            });
        }

        private void SendDHPToCore(string toAddress, string token, string amountWei, string priorityFeeWei, 
            string senderExecutor, string nonce, bool isAsyncExec, string signature)
        {
            if (_txService == null)
            {
                SetStatus("Transaction service not initialized.");
                return;
            }

            SetStatus("Sending DHP payment to EVVM Core...");
            ClearTransactionLink();
            
            _txService.SendJABToCore(toAddress, token, amountWei, priorityFeeWei, senderExecutor, nonce, isAsyncExec, signature, 
                (success, result) =>
                {
                    if (success)
                    {
                        _lastTransactionHash = result;
                        SetStatus($"DHP payment sent!");
                        ShowTransactionLink(result);
                        Debug.Log($"[PaymentTestUI] DHP payment successful: {result}");
                    }
                    else
                    {
                        SetStatus($"DHP payment failed: {result}");
                        ClearTransactionLink();
                        Debug.LogError($"[PaymentTestUI] DHP payment failed: {result}");
                    }
                });
        }

        private void PayWithUSDCHealth(string recipient, string amountWei)
        {
            if (_txService == null)
            {
                SetStatus("Transaction service not initialized.");
                return;
            }

            SetStatus("Sending USDC Health payment...");
            ClearTransactionLink();
            
            _txService.SendERC20Transfer(ContractAddresses.USDCHealth, recipient, amountWei, 
                (success, result) =>
                {
                    if (success)
                    {
                        _lastTransactionHash = result;
                        SetStatus($"USDC Health sent!");
                        ShowTransactionLink(result);
                        Debug.Log($"[PaymentTestUI] USDC Health payment successful: {result}");
                    }
                    else
                    {
                        SetStatus($"USDC Health failed: {result}");
                        ClearTransactionLink();
                        Debug.LogError($"[PaymentTestUI] USDC Health payment failed: {result}");
                    }
                });
        }

        private void PayWithNativeUsdc(string recipient, string amountWei)
        {
            if (_txService == null)
            {
                SetStatus("Transaction service not initialized.");
                return;
            }

            SetStatus("Sending USDC (native) payment...");
            ClearTransactionLink();
            
            var tokenAddress = ContractAddresses.ZeroAddress;
            var isNativeToken = tokenAddress == ContractAddresses.ZeroAddress;
            
            if (isNativeToken)
            {
                _txService.SendNativeTransaction(recipient, amountWei, 
                    (success, result) =>
                    {
                        if (success)
                        {
                            _lastTransactionHash = result;
                            SetStatus($"USDC (native) sent!");
                            ShowTransactionLink(result);
                            Debug.Log($"[PaymentTestUI] USDC (native) payment successful: {result}");
                        }
                        else
                        {
                            SetStatus($"USDC (native) failed: {result}");
                            ClearTransactionLink();
                            Debug.LogError($"[PaymentTestUI] USDC (native) payment failed: {result}");
                        }
                    });
            }
            else
            {
                _txService.SendERC20Transfer(tokenAddress, recipient, amountWei, 
                    (success, result) =>
                    {
                        if (success)
                        {
                            _lastTransactionHash = result;
                            SetStatus($"USDC (native) sent!");
                            ShowTransactionLink(result);
                            Debug.Log($"[PaymentTestUI] USDC (native) payment successful: {result}");
                        }
                        else
                        {
                            SetStatus($"USDC (native) failed: {result}");
                            ClearTransactionLink();
                            Debug.LogError($"[PaymentTestUI] USDC (native) payment failed: {result}");
                        }
                    });
            }
        }

        private void OnSignPayClicked()
        {
            if (_wallet == null || !_wallet.IsConnected)
            {
                SetStatus("Wallet not connected.");
                return;
            }
            var recipient = recipientInput != null ? recipientInput.text?.Trim() : "";
            if (string.IsNullOrEmpty(recipient))
            {
                SetStatus("Enter recipient address.");
                return;
            }
            if (!ParseAmount(amountInput != null ? amountInput.text : "", out var amountWei, out var decimals))
            {
                SetStatus("Invalid amount.");
                return;
            }
            var tokenIndex = tokenDropdown != null ? tokenDropdown.value : 0;
            string token;
            if (tokenIndex == 0)
                token = ContractAddresses.DhpPrincipal;
            else if (tokenIndex == 1)
                token = ContractAddresses.USDCHealth;
            else
                token = ContractAddresses.ZeroAddress;
            var priorityFeeWei = "0";
            var senderExecutor = ContractAddresses.ZeroAddress;
            var nonce = DateTime.UtcNow.Ticks.ToString();
            var isAsyncExec = true;
            var payload = PayPayloadBuilder.BuildSignaturePayload(
                recipient, "", token, amountWei, priorityFeeWei,
                senderExecutor, nonce, isAsyncExec);
            _wallet.SignMessage(payload, (success, signature) =>
            {
                if (!success || string.IsNullOrEmpty(signature))
                {
                    SetStatus("Sign failed.");
                    return;
                }
                _lastSignedPayload = BuildJsonPayload(_wallet.ConnectedAddress, recipient, token, amountWei, priorityFeeWei, senderExecutor, nonce, isAsyncExec, signature);
                SetStatus("Signed. Use Copy payload or Send to relay.");
            });
        }

        private bool ParseAmount(string text, out string amountWei, out int decimals)
        {
            amountWei = "0";
            decimals = 18;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim().Replace(",", ".");
            if (!decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) || d < 0)
                return false;
            var tokenIndex = tokenDropdown != null ? tokenDropdown.value : 0;
            if (tokenIndex == 0)
                decimals = TokenService.DhpDecimals;
            else if (tokenIndex == 1)
                decimals = TokenService.USDCHealthDecimals;
            else
                decimals = 18;
            var mult = (decimal)Math.Pow(10, decimals);
            var wei = (BigInteger)(d * mult);
            amountWei = wei.ToString();
            return true;
        }

        private static string BuildJsonPayload(string fromAddress, string toAddress, string token, string amountWei, string priorityFeeWei,
            string senderExecutor, string nonce, bool isAsyncExec, string signature)
        {
            var sig = signature ?? "";
            return "{" +
                   "\"from\":\"" + (fromAddress ?? "") + "\"," +
                   "\"to_address\":\"" + toAddress + "\"," +
                   "\"to\":\"" + toAddress + "\"," +
                   "\"to_identity\":\"\"," +
                   "\"token\":\"" + token + "\"," +
                   "\"amount\":\"" + amountWei + "\"," +
                   "\"priorityFee\":\"" + priorityFeeWei + "\"," +
                   "\"senderExecutor\":\"" + senderExecutor + "\"," +
                   "\"nonce\":\"" + nonce + "\"," +
                   "\"isAsyncExec\":" + (isAsyncExec ? "true" : "false") + "," +
                   "\"signature\":\"" + sig + "\"," +
                   "\"evvmSignature\":\"" + sig + "\"" +
                   "}";
        }

        // Relayer at POST /x402/pay expects: receiptId, from, amount, to/paymentTo, validAfter, validBefore, nonce (x402 bytes32), v, r, s (x402 sig), evvmNonce, evvmSignature, evvmIsAsyncExec, receiptIdString; plus token, to_identity, priorityFee, senderExecutor for EVVM pay()
        private static string BuildRelayerPayload(string from, string to, string amountWei, ulong validAfter, ulong validBefore, string x402NonceBytes32, int v, string r, string s, string evvmNonce, string evvmSignature, bool evvmIsAsyncExec, string token, string priorityFeeWei, string senderExecutor)
        {
            var receiptId = GenerateReceiptId();
            var sb = new StringBuilder();
            Func<string, string> esc = JsonEscape;
            sb.Append("{");
            sb.Append("\"receiptId\":\"").Append(esc(receiptId)).Append("\",");
            sb.Append("\"receiptIdString\":\"").Append(esc(receiptId)).Append("\",");
            sb.Append("\"from\":\"").Append(esc(from)).Append("\",");
            sb.Append("\"to\":\"").Append(esc(to)).Append("\",");
            sb.Append("\"paymentTo\":\"").Append(esc(to)).Append("\",");
            sb.Append("\"amount\":\"").Append(esc(amountWei)).Append("\",");
            sb.Append("\"validAfter\":").Append(validAfter).Append(",");
            sb.Append("\"validBefore\":").Append(validBefore).Append(",");
            sb.Append("\"nonce\":\"").Append(esc(x402NonceBytes32)).Append("\",");
            sb.Append("\"v\":").Append(v).Append(",");
            sb.Append("\"r\":\"").Append(esc(r)).Append("\",");
            sb.Append("\"s\":\"").Append(esc(s)).Append("\",");
            sb.Append("\"evvmNonce\":\"").Append(esc(evvmNonce)).Append("\",");
            sb.Append("\"evvmSignature\":\"").Append(esc(evvmSignature)).Append("\",");
            sb.Append("\"evvmIsAsyncExec\":").Append(evvmIsAsyncExec ? "true" : "false").Append(",");
            sb.Append("\"token\":\"").Append(esc(token)).Append("\",");
            sb.Append("\"to_identity\":\"\",");
            sb.Append("\"priorityFee\":\"").Append(esc(priorityFeeWei)).Append("\",");
            sb.Append("\"senderExecutor\":\"").Append(esc(senderExecutor ?? ContractAddresses.ZeroAddress)).Append("\",");
            sb.Append("\"chainId\":").Append(X402Eip712.ChainId).Append("}");
            return sb.ToString();
        }

        private static string JsonEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder();
            foreach (var c in s)
            {
                if (c == '\\') sb.Append("\\\\");
                else if (c == '"') sb.Append("\\\"");
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static string GenerateReceiptId()
        {
            var bytes = new byte[32];
            try
            {
                using (var rng = RandomNumberGenerator.Create())
                    rng.GetBytes(bytes);
            }
            catch
            {
                for (var i = 0; i < 32; i++) bytes[i] = (byte)UnityEngine.Random.Range(0, 256);
            }
            var hex = new StringBuilder("0x", 66);
            foreach (var b in bytes) hex.Append(b.ToString("x2"));
            return hex.ToString();
        }

        private static void SplitSignature(string sig, out int v, out string r, out string s)
        {
            r = "0x"; s = "0x"; v = 28;
            if (string.IsNullOrEmpty(sig) || !sig.StartsWith("0x") || sig.Length < 132)
                return;
            r = "0x" + sig.Substring(2, 64);
            s = "0x" + sig.Substring(66, 64);
            var vByte = Convert.ToInt32(sig.Substring(130, 2), 16);
            v = (vByte == 0 || vByte == 1) ? 27 + vByte : vByte;
        }

        private static string NonceToBytes32(string nonceDecimal)
        {
            if (string.IsNullOrEmpty(nonceDecimal)) return "0x" + new string('0', 64);
            if (!BigInteger.TryParse(nonceDecimal, out var n)) return "0x" + new string('0', 64);
            var bytes = n.ToByteArray();
            if (bytes.Length == 0) return "0x" + new string('0', 64);
            Array.Reverse(bytes);
            var padded = new byte[32];
            var copyLen = Math.Min(32, bytes.Length);
            Buffer.BlockCopy(bytes, 0, padded, 32 - copyLen, copyLen);
            var hex = new StringBuilder("0x", 66);
            for (var i = 0; i < 32; i++) hex.Append(padded[i].ToString("x2"));
            return hex.ToString();
        }

        private void OnCopyPayloadClicked()
        {
            if (string.IsNullOrEmpty(_lastSignedPayload))
            {
                SetStatus("Sign a payment first.");
                return;
            }
            GUIUtility.systemCopyBuffer = _lastSignedPayload;
            SetStatus("Payload copied to clipboard.");
        }

        private void OnSendToRelayClicked()
        {
            if (string.IsNullOrEmpty(_lastSignedPayload))
            {
                SetStatus("Sign a payment first.");
                return;
            }
            if (string.IsNullOrWhiteSpace(EffectiveRelayUrl))
            {
                SetStatus("Relay URL not set.");
                return;
            }
            StartCoroutine(PostToRelay(EffectiveRelayUrl, _lastSignedPayload));
        }

        private string GetRelayPostUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) return baseUrl;
            baseUrl = baseUrl.Trim();
            try
            {
                var uri = new Uri(baseUrl);
                var path = uri.AbsolutePath?.TrimEnd('/') ?? "";
                if (string.IsNullOrEmpty(path))
                {
                    var suffix = string.IsNullOrWhiteSpace(relayPath) ? "/x402/pay" : relayPath.Trim();
                    if (!suffix.StartsWith("/")) suffix = "/" + suffix;
                    // Relayer serves POST /x402/pay only; avoid 404 when scene has old "/" or "/pay"
                    if (suffix == "/" || suffix == "/pay") suffix = "/x402/pay";
                    return baseUrl.TrimEnd('/') + suffix;
                }
            }
            catch { }
            return baseUrl;
        }

        private IEnumerator PostToRelay(string url, string json)
        {
            var postUrl = GetRelayPostUrl(url);
            SetStatus("Sending to relay…");
            using (var req = new UnityWebRequest(postUrl, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 60;
                yield return req.SendWebRequest();
                var responseBody = req.downloadHandler?.text ?? "";
                var isHttpError = req.responseCode >= 400;
                if (req.result != UnityWebRequest.Result.Success || isHttpError)
                {
                    var err = req.error;
                    if (isHttpError && !string.IsNullOrEmpty(responseBody))
                    {
                        err = "HTTP " + req.responseCode + ": " + responseBody;
                        Debug.LogWarning("[PaymentTestUI] Relay error response: " + responseBody);
                    }
                    else if (string.IsNullOrEmpty(err))
                        err = responseBody;
                    SetStatus("Relay failed: " + (err ?? "Unknown"));
                    yield break;
                }
                SetStatus("USDC Health sent via x402.");
            }
        }

        private void SetStatus(string msg)
        {
            if (statusText != null && _useTmp)
                statusText.text = msg;
        }

        private void ShowTransactionLink(string txHash)
        {
            if (transactionLinkText == null || _chainSettings == null) return;
            
            _lastTransactionHash = txHash;
            var explorerUrl = GetExplorerUrl(txHash);
            
            transactionLinkText.text = $"<u>View on Explorer: {TruncateHash(txHash)}</u>";
            
            Debug.Log($"[PaymentTestUI] Transaction link: {explorerUrl}");
        }

        private void ClearTransactionLink()
        {
            if (transactionLinkText != null)
            {
                transactionLinkText.text = "";
                _lastTransactionHash = null;
            }
        }

        private void OnTransactionLinkClicked()
        {
            if (string.IsNullOrEmpty(_lastTransactionHash)) return;
            
            var explorerUrl = GetExplorerUrl(_lastTransactionHash);
            Debug.Log($"[PaymentTestUI] Opening transaction in browser: {explorerUrl}");
            Application.OpenURL(explorerUrl);
        }

        private string GetExplorerUrl(string txHash)
        {
            if (_chainSettings == null) return "";
            
            var baseUrl = _chainSettings.HostBlockExplorer;
            if (string.IsNullOrEmpty(baseUrl))
                baseUrl = "https://aeneid.storyscan.io";
            
            return $"{baseUrl}/tx/{txHash}";
        }

        private string TruncateHash(string hash)
        {
            if (string.IsNullOrEmpty(hash) || hash.Length < 14) return hash;
            return $"{hash.Substring(0, 6)}...{hash.Substring(hash.Length - 4)}";
        }
    }
}
