using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Header("Панели")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject lobbyPanel;

    [Header("Элементы Main Panel")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Элементы Lobby Panel")]
    [SerializeField] private TextMeshProUGUI lobbyCodeText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveLobbyButton;

    private string currentJoinCode = "";

    private void Start()
    {
        ShowMainPanel();
        statusText.text = "";

        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(OnJoinClicked);
        startGameButton.onClick.AddListener(OnStartGameClicked);
        leaveLobbyButton.onClick.AddListener(OnLeaveLobbyClicked);

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void ShowMainPanel()
    {
        mainPanel.SetActive(true);
        lobbyPanel.SetActive(false);
    }

    private void ShowLobbyPanel()
    {
        mainPanel.SetActive(false);
        lobbyPanel.SetActive(true);

        bool isHost = NetworkManager.Singleton.IsHost;
        startGameButton.gameObject.SetActive(isHost);
        lobbyCodeText.text = isHost ? $"КОД ЛОББИ: {currentJoinCode}" : "ПОДКЛЮЧЕНО К СЕССИИ";
        UpdatePlayerCount();
    }

    private async void OnHostClicked()
    {
        SetButtonsInteractable(false);
        statusText.text = "Создание комнаты...";

        currentJoinCode = await RelayManager.Instance.CreateRelay(4);

        if (!string.IsNullOrEmpty(currentJoinCode))
        {
            statusText.text = "";
            ShowLobbyPanel();
        }
        else
        {
            statusText.text = "Ошибка создания лобби.";
            SetButtonsInteractable(true);
        }
    }

    private async void OnJoinClicked()
    {
        string code = joinCodeInput.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(code))
        {
            statusText.text = "Введите код подключения!";
            return;
        }

        SetButtonsInteractable(false);
        statusText.text = "Связь с Relay сервером...";

        bool started = await RelayManager.Instance.JoinRelay(code);

        if (started)
        {
            // Ждем реального подтверждения подключения в OnClientConnected
            statusText.text = "Вход в комнату хоста...";
            currentJoinCode = code;
        }
        else
        {
            statusText.text = "Неверный код или комната не найдена.";
            SetButtonsInteractable(true);
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        // Если подключились мы сами (клиент)
        if (!NetworkManager.Singleton.IsServer && clientId == NetworkManager.Singleton.LocalClientId)
        {
            statusText.text = "";
            SetButtonsInteractable(true);
            ShowLobbyPanel();
        }

        // Обновляем счетчик для всех
        UpdatePlayerCount();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer || clientId == NetworkManager.Singleton.LocalClientId)
        {
            string reason = NetworkManager.Singleton.DisconnectReason;
            if (string.IsNullOrEmpty(reason))
            {
                reason = "Таймаут ответа хоста (проверьте код и соединение)";
            }

            statusText.text = $"Сброс: {reason}";
            Debug.LogWarning($"[Netcode] Отключение от хоста. Причина: {reason}");

            SetButtonsInteractable(true);
            ShowMainPanel();
        }

        UpdatePlayerCount();
    }

    private void OnStartGameClicked()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        NetworkManager.Singleton.SceneManager.LoadScene("MainGame", LoadSceneMode.Single);
    }

    private void OnLeaveLobbyClicked()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }
        currentJoinCode = "";
        SetButtonsInteractable(true);
        ShowMainPanel();
    }

    private void UpdatePlayerCount()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            playerCountText.text = $"Игроков: {NetworkManager.Singleton.ConnectedClientsList.Count} / 4";
        }
        else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
        {
            playerCountText.text = "В группе хоста";
        }
    }

    private void SetButtonsInteractable(bool state)
    {
        hostButton.interactable = state;
        joinButton.interactable = state;
        joinCodeInput.interactable = state;
    }
}