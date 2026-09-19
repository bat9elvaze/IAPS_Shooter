using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class RelayManager : MonoBehaviour
{
    private static RelayManager _instance;

    public static RelayManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<RelayManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("RelayManager");
                    _instance = go.AddComponent<RelayManager>();
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        await InitializeServicesAsync();
    }

    public async Task<bool> InitializeServicesAsync()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                // Уникальный профиль исключает конфликт одинаковых Player ID при тестах
                var options = new InitializationOptions();
                string randomProfile = "Player_" + UnityEngine.Random.Range(1000, 99999);
                options.SetProfile(randomProfile);

                await UnityServices.InitializeAsync(options);
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[RelayManager] Ошибка инициализации сервисов: {e.Message}");
            return false;
        }
    }

    public async Task<string> CreateRelay(int maxConnections = 4)
    {
        try
        {
            bool initialized = await InitializeServicesAsync();
            if (!initialized) return null;

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            // Включаем одобрение подключений для хоста
            NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
            NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
            transport.SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartHost();
            return joinCode;
        }
        catch (Exception e)
        {
            Debug.LogError($"[RelayManager] Ошибка создания Relay: {e.Message}");
            return null;
        }
    }

    public async Task<bool> JoinRelay(string joinCode)
    {
        try
        {
            bool initialized = await InitializeServicesAsync();
            if (!initialized) return false;

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            RelayServerData relayServerData = new RelayServerData(joinAllocation, "dtls");
            transport.SetRelayServerData(relayServerData);

            return NetworkManager.Singleton.StartClient();
        }
        catch (Exception e)
        {
            Debug.LogError($"[RelayManager] Ошибка подключения по коду: {e.Message}");
            return false;
        }
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        response.Approved = true;
        response.CreatePlayerObject = false; // Персонаж спавнится только на MainGame
        response.Pending = false;
    }
}