using UnityEngine;
using UnityEngine.UI;

public class DestroyOnClick : MonoBehaviour
{
    public Button destroyButton;  // Assign this in the prefab inspector

    void Start()
    {
        destroyButton.onClick.AddListener(() => Destroy(gameObject));
    }
}
