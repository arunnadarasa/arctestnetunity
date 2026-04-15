using UnityEngine;
using UnityEngine.UI;

namespace KrumpKraft
{
    /// <summary>
    /// Optional stub UI for dance-move display or interaction.
    /// Extend with real move list, verification, or token-gated content as needed.
    /// </summary>
    public class DanceMoveUI : MonoBehaviour
    {
        [SerializeField] private TMPro.TMP_Text titleLabel;
        [SerializeField] private TMPro.TMP_Text moveLabel;
        [SerializeField] private Button refreshButton;

        private void Start()
        {
            if (titleLabel != null)
                titleLabel.text = "Dance Move";
            if (moveLabel != null)
                moveLabel.text = "—";
            if (refreshButton != null)
                refreshButton.onClick.AddListener(OnRefresh);
        }

        private void OnRefresh()
        {
            if (moveLabel != null)
                moveLabel.text = "—";
        }

        /// <summary>
        /// Set the displayed move name or id (stub).
        /// </summary>
        public void SetMove(string moveNameOrId)
        {
            if (moveLabel != null)
                moveLabel.text = string.IsNullOrEmpty(moveNameOrId) ? "—" : moveNameOrId;
        }
    }
}
