using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace KrumpKraft
{
    /// <summary>
    /// Optional: (1) Create Default Hierarchy builds Canvas + panel structure and adds WalletConnectUI,
    /// TokenBalanceUI x2, PaymentTestUI, plus a scene-level "Web3Auth" placeholder GameObject for the MetaMask
    /// Embedded Wallets (Web3Auth) component. (2) Run wires references to buttons/labels/inputs under those panels.
    /// You can create the hierarchy then add Button, TMP_Text, TMP_InputField, TMP_Dropdown under each panel
    /// with names containing: Connect, Disconnect, address, balance, recipient, amount, Sign pay, Copy payload, status —
    /// then Run to wire. For Web3Auth: attach the Web3Auth script from the Embedded Wallets package to the
    /// Web3Auth placeholder (or to KrumpKraftManager's GameObject). Add this component to KrumpKraftManager or a dedicated setup object.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class AutoUISetup : MonoBehaviour
    {
        [SerializeField] private bool runOnAwake = true;
        [SerializeField] private Canvas targetCanvas;
        [Tooltip("If true, create Canvas + panels when no Canvas exists (and a 'Web3Auth' placeholder for the Embedded Wallets component). Does not create buttons/inputs — add those in Editor then Run.")]
        [SerializeField] private bool createHierarchyIfMissing = false;

        private void Awake()
        {
            if (createHierarchyIfMissing)
            {
                var canvas = targetCanvas != null ? targetCanvas : UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
                if (canvas == null)
                    CreateDefaultHierarchy();
            }
            if (runOnAwake)
                Run();
        }

        /// <summary>
        /// Creates Canvas, EventSystem (if missing), and panel hierarchy with WalletConnectUI,
        /// two TokenBalanceUI (JAB, USDC Krump), and PaymentTestUI. Does not create buttons/inputs —
        /// add those under each panel in the Editor (names: Connect, Disconnect, AddressLabel, BalanceLabel, etc.) then Run.
        /// </summary>
        [ContextMenu("Create Default Hierarchy")]
        public void CreateDefaultHierarchy()
        {
            if (UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include) != null)
            {
                Debug.Log("[KrumpKraft] Canvas already exists. Create Default Hierarchy only runs when no Canvas is in the scene.");
                return;
            }

            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                AddInputModuleForEventSystem(esGo);
            }

            new GameObject("Web3Auth");

            var root = new GameObject("KrumpKraftUI");
            root.transform.SetParent(canvasGo.transform, false);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            var layout = root.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            var walletPanel = AddPanel(root.transform, "WalletPanel");
            walletPanel.gameObject.AddComponent<WalletConnectUI>();
            var dhpPanel = AddPanel(root.transform, "DHPBalancePanel");
            dhpPanel.gameObject.AddComponent<TokenBalanceUI>();
            var usdcPanel = AddPanel(root.transform, "USDCBalancePanel");
            var usdcToken = usdcPanel.gameObject.AddComponent<TokenBalanceUI>();
            SetSerializedValue(usdcToken, "tokenType", TokenBalanceUI.TokenType.USDCHealth);
            var paymentPanel = AddPanel(root.transform, "PaymentPanel");
            paymentPanel.gameObject.AddComponent<PaymentTestUI>();

            targetCanvas = canvas;
            Debug.Log("[KrumpKraft] Default hierarchy created. Add Buttons/Inputs under WalletPanel, DHPBalancePanel, USDCBalancePanel, PaymentPanel with names containing Connect, Disconnect, address, balance, recipient, amount, Sign pay, Copy payload, status — then use Run Auto Setup. For Web3Auth (MetaMask Embedded Wallets), attach the Web3Auth script from the package to the 'Web3Auth' GameObject.");
        }

        /// <summary>
        /// Adds the appropriate UI input module for the EventSystem: Input System UI Input Module when the Input System package is active, otherwise StandaloneInputModule (old Input).
        /// </summary>
        private static void AddInputModuleForEventSystem(GameObject eventSystemGo)
        {
            var inputSystemUiType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemUiType != null)
            {
                eventSystemGo.AddComponent(inputSystemUiType);
                return;
            }
            eventSystemGo.AddComponent<StandaloneInputModule>();
        }

        private static RectTransform AddPanel(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(0, 60);
            return rect;
        }

        [ContextMenu("Run Auto Setup")]
        public void Run()
        {
            var root = targetCanvas != null ? targetCanvas.transform : UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include)?.transform;
            if (root == null)
            {
                Debug.LogWarning("[KrumpKraft] AutoUISetup: no Canvas found. Use Create Default Hierarchy or add a Canvas.");
                return;
            }

            var walletUi = root.GetComponentInChildren<WalletConnectUI>(true);
            if (walletUi != null)
                TryAssignWalletConnectUI(walletUi, root);

            var balanceUis = root.GetComponentsInChildren<TokenBalanceUI>(true);
            foreach (var ui in balanceUis)
                TryAssignTokenBalanceUI(ui, root);

            var paymentUi = root.GetComponentInChildren<PaymentTestUI>(true);
            if (paymentUi != null)
                TryAssignPaymentTestUI(paymentUi, root);
        }

        private static void TryAssignWalletConnectUI(WalletConnectUI ui, Transform root)
        {
            if (ui == null) return;
            var uiTransform = ui.transform;
            var buttons = uiTransform.GetComponentsInChildren<Button>(true);
            Button connect = null, disconnect = null;
            foreach (var b in buttons)
            {
                var t = GetButtonLabel(b);
                if (t.Contains("CONNECT") && !t.Contains("DISCONNECT")) connect = b;
                if (t.Contains("DISCONNECT")) disconnect = b;
            }
            if (connect != null) SetSerialized(ui, "connectButton", connect);
            if (disconnect != null) SetSerialized(ui, "disconnectButton", disconnect);
            TMPro.TMP_Text addressLabel = null;
            var labels = uiTransform.GetComponentsInChildren<TMPro.TMP_Text>(true);
            foreach (var l in labels)
                if (l != null && l.gameObject.name.ToLowerInvariant().Contains("address"))
                {
                    addressLabel = l;
                    SetSerialized(ui, "addressLabel", l);
                    break;
                }
            if (addressLabel != null)
            {
                var go = addressLabel.transform.parent != null ? addressLabel.transform.parent.gameObject : addressLabel.gameObject;
                if (go != null) SetSerialized(ui, "connectedPanel", go);
            }
            if (GetSerialized<GameObject>(ui, "connectedPanel") == null)
            {
                var underWallet = ui.transform.GetComponentsInChildren<Transform>(true);
                foreach (var t in underWallet)
                    if (t != null && t != ui.transform && (t.gameObject.name.ToLowerInvariant().Contains("connected") || t.gameObject.name.ToLowerInvariant().Contains("panel")))
                    {
                        SetSerialized(ui, "connectedPanel", t.gameObject);
                        break;
                    }
            }
        }

        private static void TryAssignTokenBalanceUI(TokenBalanceUI ui, Transform root)
        {
            if (ui == null) return;
            var labels = ui.GetComponentsInChildren<TMPro.TMP_Text>(true);
            foreach (var l in labels)
                if (l != null && (l.gameObject.name.ToLowerInvariant().Contains("balance") || l.transform == ui.transform))
                {
                    SetSerialized(ui, "balanceLabel", l);
                    break;
                }
            var btn = ui.GetComponentInChildren<Button>(true);
            if (btn != null) SetSerialized(ui, "refreshButton", btn);
        }

        private static void TryAssignPaymentTestUI(PaymentTestUI ui, Transform root)
        {
            if (ui == null) return;
            var uiTransform = ui.transform;
            var inputs = uiTransform.GetComponentsInChildren<TMPro.TMP_InputField>(true);
            TMPro.TMP_InputField recipient = null, amount = null;
            int idx = 0;
            foreach (var i in inputs)
            {
                if (i == null) continue;
                var name = (i.gameObject.name ?? "").ToLowerInvariant();
                if (name.Contains("recipient")) recipient = i;
                else if (name.Contains("amount")) amount = i;
                else
                {
                    if (idx == 0) recipient = i;
                    else if (idx == 1) amount = i;
                    idx++;
                }
            }
            if (recipient != null) SetSerialized(ui, "recipientInput", recipient);
            if (amount != null) SetSerialized(ui, "amountInput", amount);
            var dropdown = uiTransform.GetComponentInChildren<TMPro.TMP_Dropdown>(true);
            if (dropdown != null) SetSerialized(ui, "tokenDropdown", dropdown);
            var buttons = uiTransform.GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                var t = GetButtonLabel(b);
                if (t.Contains("SIGN") && t.Contains("PAY")) { SetSerialized(ui, "signPayButton", b); break; }
            }
            foreach (var b in buttons)
            {
                var t = GetButtonLabel(b);
                if (t.Contains("COPY") && t.Contains("PAYLOAD")) { SetSerialized(ui, "copyPayloadButton", b); break; }
            }
            foreach (var b in buttons)
            {
                var t = GetButtonLabel(b);
                if (t.Contains("SEND") && t.Contains("RELAY")) { SetSerialized(ui, "sendToRelayButton", b); break; }
            }
            var texts = uiTransform.GetComponentsInChildren<TMPro.TMP_Text>(true);
            foreach (var t in texts)
                if (t != null && (t.gameObject.name.ToLowerInvariant().Contains("status") || t.gameObject.name.ToLowerInvariant().Contains("message")))
                {
                    SetSerialized(ui, "statusText", t);
                    break;
                }
        }

        private static string GetButtonLabel(Button b)
        {
            var tmp = b.GetComponentInChildren<TMPro.TMP_Text>();
            if (tmp != null && !string.IsNullOrEmpty(tmp.text)) return tmp.text.ToUpperInvariant();
            var leg = b.GetComponentInChildren<Text>();
            return (leg != null ? leg.text : null)?.ToUpperInvariant() ?? "";
        }

        private static void SetSerialized(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
        {
            if (target == null || value == null) return;
            SetSerializedValue(target, fieldName, value);
        }

        private static void SetSerializedValue(UnityEngine.Object target, string fieldName, object value)
        {
            if (target == null) return;
            var type = target.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
                field.SetValue(target, value);
        }

        private static T GetSerialized<T>(UnityEngine.Object target, string fieldName) where T : UnityEngine.Object
        {
            if (target == null) return null;
            var type = target.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field == null) return null;
            return field.GetValue(target) as T;
        }
    }
}
