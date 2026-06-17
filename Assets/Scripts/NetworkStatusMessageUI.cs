using System.Collections;
using TMPro;
using UnityEngine;

public class NetworkStatusMessageUI : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private float visibleSeconds = 3f;

    private Coroutine hideCoroutine;

    private void Awake()
    {
        if (messageText != null)
        {
            messageText.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        GameEvents.OnNetworkStatusMessage += ShowMessage;
    }

    private void OnDisable()
    {
        GameEvents.OnNetworkStatusMessage -= ShowMessage;
    }

    private void ShowMessage(string message)
    {
        if (messageText == null)
        {
            Debug.LogWarning("[NetworkStatusMessageUI] messageText no está asignado.");
            return;
        }

        messageText.text = message;
        messageText.gameObject.SetActive(true);

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
        }

        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(visibleSeconds);

        if (messageText != null)
        {
            messageText.gameObject.SetActive(false);
        }

        hideCoroutine = null;
    }
}