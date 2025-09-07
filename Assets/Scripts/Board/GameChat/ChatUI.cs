using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Mirror;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ChatUI : MonoBehaviour
{
    [Header("Wiring")]
    public TMP_InputField inputField;
    public ScrollRect scrollRect;
    public RectTransform content;
    public GameObject messageItemPrefab;

    NetworkChat localChat;

    void Awake()
    {
        if (inputField != null)
            inputField.onSubmit.AddListener(_ => Send());
    }

    void OnEnable()
    {
        NetworkChat.OnMessage += HandleMessage;
        TryBindLocal();
    }

    void OnDisable()
    {
        NetworkChat.OnMessage -= HandleMessage;
        if (inputField != null) inputField.onSubmit.RemoveAllListeners();
    }

    void TryBindLocal()
    {
        if (localChat != null) return;
        var conn = NetworkClient.connection;
        if (conn != null && conn.identity != null)
            localChat = conn.identity.GetComponent<NetworkChat>();
    }

    public void OnSendClicked() => Send();

    void Send()
    {
        if (localChat == null) TryBindLocal();
        if (localChat == null || inputField == null) return;

        string text = inputField.text;
        if (string.IsNullOrWhiteSpace(text)) return;

        localChat.Send(text);
        inputField.text = string.Empty;
        inputField.ActivateInputField();
        inputField.caretPosition = 0;
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
        Canvas.ForceUpdateCanvases();

    }

    void HandleMessage(string from, string text, double sentAt)
    {
        var go = Instantiate(messageItemPrefab, content);
        var label = go.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = $"<b>{from}</b>: {text}";
        }
        Canvas.ForceUpdateCanvases();
        scrollRect.normalizedPosition = new Vector2(0, 0);
        Canvas.ForceUpdateCanvases();
    }

#if ENABLE_INPUT_SYSTEM
    void Update()
    {
        if (inputField != null && inputField.isFocused)
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                Send();
        }
    }
#else
    void Update()
    {
        if (inputField != null && inputField.isFocused &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            Send();
        }
    }
#endif
}
