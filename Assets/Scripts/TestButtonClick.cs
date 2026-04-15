using UnityEngine;
using UnityEngine.UI;

public class TestButtonClick : MonoBehaviour
{
    private void Start()
    {
        var button = GetComponent<Button>();
        if (button != null)
        {
            Debug.Log($"[TestButtonClick] Button found on {gameObject.name}, adding test listener");
            button.onClick.AddListener(() => {
                Debug.Log($"[TestButtonClick] BUTTON CLICKED on {gameObject.name}!");
            });
        }
        else
        {
            Debug.LogError($"[TestButtonClick] No Button component on {gameObject.name}");
        }
    }
}
