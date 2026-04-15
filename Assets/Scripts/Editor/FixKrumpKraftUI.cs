using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace KrumpKraft
{
    public class FixKrumpKraftUI : EditorWindow
    {
        [MenuItem("KrumpKraft/Fix UI Setup")]
        public static void FixUI()
        {
            var canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                Debug.LogError("Canvas not found in scene!");
                return;
            }

            FixWalletPanel();
            FixDropdown();
            EnableWalletPanel();
            
            Debug.Log("[KrumpKraft] UI fixes applied successfully!");
        }

        private static void FixWalletPanel()
        {
            var walletPanel = GameObject.Find("WalletPanel");
            if (walletPanel == null) return;

            var connectedPanel = walletPanel.transform.Find("ConnectedPanel");
            if (connectedPanel == null)
            {
                var connectedPanelGO = new GameObject("ConnectedPanel");
                connectedPanelGO.transform.SetParent(walletPanel.transform, false);
                
                var rect = connectedPanelGO.AddComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                var addressLabel = walletPanel.transform.Find("AddressLabel");
                if (addressLabel != null)
                    addressLabel.SetParent(connectedPanelGO.transform, false);

                var disconnectBtn = walletPanel.transform.Find("DisconnectButton");
                if (disconnectBtn != null)
                    disconnectBtn.SetParent(connectedPanelGO.transform, false);

                var walletUI = walletPanel.GetComponent<WalletConnectUI>();
                if (walletUI != null)
                {
                    var so = new SerializedObject(walletUI);
                    so.FindProperty("connectedPanel").objectReferenceValue = connectedPanelGO;
                    so.ApplyModifiedProperties();
                }

                Debug.Log("[KrumpKraft] Created ConnectedPanel and moved children");
            }
        }

        private static void FixDropdown()
        {
            var dropdown = GameObject.Find("TokenDropdown");
            if (dropdown == null) return;

            var tmpDropdown = dropdown.GetComponent<TMP_Dropdown>();
            if (tmpDropdown == null) return;

            var template = dropdown.transform.Find("Template");
            if (template == null)
            {
                var templateGO = new GameObject("Template");
                templateGO.transform.SetParent(dropdown.transform, false);
                
                var templateRect = templateGO.AddComponent<RectTransform>();
                templateRect.anchorMin = new Vector2(0, 0);
                templateRect.anchorMax = new Vector2(1, 0);
                templateRect.pivot = new Vector2(0.5f, 1f);
                templateRect.anchoredPosition = new Vector2(0, 2);
                templateRect.sizeDelta = new Vector2(0, 150);
                
                var templateImg = templateGO.AddComponent<Image>();
                templateImg.color = Color.white;

                var viewportGO = new GameObject("Viewport");
                viewportGO.transform.SetParent(templateGO.transform, false);
                var viewportRect = viewportGO.AddComponent<RectTransform>();
                viewportRect.anchorMin = Vector2.zero;
                viewportRect.anchorMax = Vector2.one;
                viewportRect.sizeDelta = new Vector2(-18, 0);
                viewportRect.pivot = new Vector2(0, 1);

                var contentGO = new GameObject("Content");
                contentGO.transform.SetParent(viewportGO.transform, false);
                var contentRect = contentGO.AddComponent<RectTransform>();
                contentRect.anchorMin = new Vector2(0, 1);
                contentRect.anchorMax = new Vector2(1, 1);
                contentRect.pivot = new Vector2(0.5f, 1);
                contentRect.anchoredPosition = Vector2.zero;
                contentRect.sizeDelta = new Vector2(0, 28);

                var itemGO = new GameObject("Item");
                itemGO.transform.SetParent(contentGO.transform, false);
                var itemRect = itemGO.AddComponent<RectTransform>();
                itemRect.anchorMin = new Vector2(0, 0.5f);
                itemRect.anchorMax = new Vector2(1, 0.5f);
                itemRect.pivot = new Vector2(0.5f, 0.5f);
                itemRect.sizeDelta = new Vector2(0, 20);

                var itemBg = itemGO.AddComponent<Image>();
                itemBg.color = new Color(0.95f, 0.95f, 0.95f, 1f);
                
                var toggle = itemGO.AddComponent<Toggle>();
                toggle.targetGraphic = itemBg;
                toggle.isOn = true;

                var itemLabelGO = new GameObject("Item Label");
                itemLabelGO.transform.SetParent(itemGO.transform, false);
                var itemLabelRect = itemLabelGO.AddComponent<RectTransform>();
                itemLabelRect.anchorMin = Vector2.zero;
                itemLabelRect.anchorMax = Vector2.one;
                itemLabelRect.sizeDelta = new Vector2(-20, 0);
                
                var itemLabel = itemLabelGO.AddComponent<TextMeshProUGUI>();
                itemLabel.text = "Option A";
                itemLabel.fontSize = 14;
                itemLabel.color = Color.black;
                itemLabel.alignment = TextAlignmentOptions.Left;

                tmpDropdown.template = templateRect;
                tmpDropdown.itemText = itemLabel;

                templateGO.SetActive(false);

                Debug.Log("[KrumpKraft] Created dropdown template structure");
            }
        }

        private static void EnableWalletPanel()
        {
            var walletPanel = GameObject.Find("WalletPanel");
            if (walletPanel != null && !walletPanel.activeSelf)
            {
                walletPanel.SetActive(true);
                Debug.Log("[KrumpKraft] Enabled WalletPanel");
            }
        }
    }
}
