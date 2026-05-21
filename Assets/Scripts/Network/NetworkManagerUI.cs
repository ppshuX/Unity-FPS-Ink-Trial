using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UNET;
using UnityEngine;
using UnityEngine.UI;

public class NetworkManagerUI : MonoBehaviour
{
    /// <summary>与 nginx HTTPS 站点一致；改部署域名时只改此处。</summary>
    public const string DeployHttpsHost = "app7926.acapp.acwing.com.cn";

#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>WebGL 与页面同源，用根路径请求 <c>/fps/...</c>。</summary>
    private static readonly string ApiBase = "";
#else
    private static readonly string ApiBase = "https://" + DeployHttpsHost;
#endif

    [SerializeField]
    private Button refreshButton;
    [SerializeField]
    private Button buildButton;
    private Button localTrialButton;

    [SerializeField]
    private Canvas menuUI;
    [SerializeField]
    private GameObject roomButtonPrefab;

    /// <summary>
    /// UNet 专服监听进程口常见为 7777–7779；若 HTTP 接口只给出外壳口（如 1777x）且无外层 UDP 映射，勾选后在编辑器/Standalone 上会优先连 <see cref="Room.internal_port"/> / <see cref="BuildRoomResponse.internal_port"/>。
    /// </summary>
    [SerializeField]
    private bool preferInternalPortForDedicatedClient = true;

    [Tooltip(
        "UNet 不走浏览器 TLS。后端未返回专用 game host、或仍为 Web 站点域名时使用该值（通常为 VPS 公网 IPv4，与场景中 NetworkManager 默认 ConnectAddress 一致）。")]
    [SerializeField]
    private string fallbackUnetDedicatedHost = "49.232.65.186";

    private readonly List<Button> rooms = new List<Button>();

    private int buildRoomPortForRemoveApi = -1;
    private bool menuClosed = false;

    private void Start()
    {
        if (!ApplyCommandLineConfig())
            return;

        InitButtons();
        RefreshRoomList();
    }

    private void OnApplicationQuit()
    {
        if (buildRoomPortForRemoveApi != -1)
        {
            RemoveRoom();
        }
    }

    private void Update()
    {
        if (!menuClosed && menuUI != null && menuUI.gameObject.activeInHierarchy && Input.GetKeyDown(KeyCode.L))
        {
            StartLocalTrial();
        }
    }

    private static void LogWebFailure(string phase, UnityEngine.Networking.UnityWebRequest req)
    {
        string body = req.downloadHandler != null ? req.downloadHandler.text : "";
        Debug.LogWarning(
            $"{phase}: result={req.result}, code={req.responseCode}, error={req.error}\nurl={req.url}\nresp={Truncate(body)}");
    }

    private static string Truncate(string s, int max = 300)
    {
        if (string.IsNullOrEmpty(s))
            return "(empty)";
        return s.Length <= max ? s : s.Substring(0, max) + "…";
    }

    /// <returns>建房/进房写入 UNet 的监听与连接端口（同一值）。</summary>
    private int ResolveGamePort(Room room)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return room.port;
#else
        if (preferInternalPortForDedicatedClient && room.internal_port != 0)
            return room.internal_port;
        return room.port;
#endif
    }

    private int ResolveGamePort(BuildRoomResponse resp)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return resp.port;
#else
        if (preferInternalPortForDedicatedClient && resp.internal_port != 0)
            return resp.internal_port;
        return resp.port;
#endif
    }

    /// <summary>
    /// Unity HLAPI 的 <see cref="NetworkTransport.Connect"/> 需要可用 IPv4 字符串；直接把 Web 前端域名传给 UNet 会报 Wrong ip address。
    /// </summary>
    private static string TryResolveIpv4String(string hostOrIp)
    {
        if (string.IsNullOrWhiteSpace(hostOrIp))
            return null;

        hostOrIp = hostOrIp.Trim();

        if (IPAddress.TryParse(hostOrIp, out var parsed))
            return parsed.AddressFamily == AddressFamily.InterNetwork ? parsed.ToString() : null;

        try
        {
            foreach (var a in Dns.GetHostAddresses(hostOrIp))
            {
                if (a.AddressFamily == AddressFamily.InterNetwork)
                    return a.ToString();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[UNet] DNS 解析失败: " + ex.Message);
        }

        return null;
    }

    /// <summary>API 里是 Web 绑定域名或未填时，改用专服监听机器（一般是裸 IP）；否则 DNS → IPv4。</summary>
    private string ResolveLogicalHostForUnet(string logicalHost)
    {
        string h = string.IsNullOrWhiteSpace(logicalHost) ? null : logicalHost.Trim();
        bool useFallback = string.IsNullOrEmpty(h) ||
                           string.Equals(h, DeployHttpsHost, StringComparison.OrdinalIgnoreCase);
        string candidate = useFallback
            ? (string.IsNullOrWhiteSpace(fallbackUnetDedicatedHost)
                ? "127.0.0.1"
                : fallbackUnetDedicatedHost.Trim())
            : h;

        string ipv4 = TryResolveIpv4String(candidate);
        if (!string.IsNullOrEmpty(ipv4))
            return ipv4;

        Debug.LogWarning(
            $"[UNet] 未能把专服主机解析为 IPv4: '{candidate}'。请在 Inspector 设置 Fallback，或确认后端返回可解析的专用 game IP。");
        return candidate;
    }

    private void ConfigureUnetClientEndpoints(UNetTransport transport, string logicalHost, int port)
    {
        if (transport == null)
            return;

        transport.ConnectAddress = ResolveLogicalHostForUnet(logicalHost);
        transport.ConnectPort = transport.ServerListenPort = port;
    }

    /// <summary>返回 false 时表示已作为专服启动，不再初始化菜单。</summary>
    private bool ApplyCommandLineConfig()
    {
        var args = System.Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-port" && i + 1 < args.Length)
            {
                int port = int.Parse(args[i + 1]);
                var transport = GetComponent<UNetTransport>();
                if (transport != null)
                    ConfigureUnetClientEndpoints(transport, transport.ConnectAddress, port);
            }
        }

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-lauch-as-server")
            {
                if (NetworkManager.Singleton != null)
                    NetworkManager.Singleton.StartServer();
                else
                    Debug.LogError("NetworkManager.Singleton 为 null，无法以专服启动。");

                DestroyAllButtons();
                return false;
            }
        }

        return true;
    }

    private void InitButtons()
    {
        if (refreshButton != null)
            refreshButton.onClick.AddListener(RefreshRoomList);

        if (buildButton != null)
            buildButton.onClick.AddListener(BuildRoom);

        CreateLocalTrialButton();
    }

    private void CreateLocalTrialButton()
    {
        if (localTrialButton != null || buildButton == null)
        {
            return;
        }

        GameObject buttonObj = Instantiate(buildButton.gameObject, buildButton.transform.parent);
        buttonObj.name = "Local Trial";

        RectTransform rect = buttonObj.GetComponent<RectTransform>();
        RectTransform sourceRect = buildButton.GetComponent<RectTransform>();
        if (rect != null && sourceRect != null)
        {
            rect.anchorMin = sourceRect.anchorMin;
            rect.anchorMax = sourceRect.anchorMax;
            rect.pivot = sourceRect.pivot;
            rect.sizeDelta = sourceRect.sizeDelta;
            rect.anchoredPosition = sourceRect.anchoredPosition + new Vector2(0f, -62f);
        }
        else
        {
            buttonObj.transform.localPosition = buildButton.transform.localPosition + new Vector3(0f, -62f, 0f);
        }

        localTrialButton = buttonObj.GetComponent<Button>();
        if (localTrialButton == null)
        {
            localTrialButton = buttonObj.AddComponent<Button>();
        }

        localTrialButton.onClick.RemoveAllListeners();
        localTrialButton.onClick.AddListener(StartLocalTrial);

        TextMeshProUGUI label = localTrialButton.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            label.text = "LOCAL TRIAL";
        }
    }

    private void StartLocalTrial()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager.Singleton 为 null，无法启动单人试炼。");
            return;
        }

        if (!NetworkManager.Singleton.IsListening)
        {
            var transport = GetComponent<UNetTransport>();
            ConfigureUnetClientEndpoints(transport, "127.0.0.1", 7777);

            if (!NetworkManager.Singleton.StartHost())
            {
                Debug.LogError("单人试炼 StartHost 失败。");
                return;
            }
        }

        DestroyAllButtons();
        if (menuUI != null)
        {
            menuUI.gameObject.SetActive(false);
        }

        if (TrialHud.Singleton != null)
        {
            TrialHud.Singleton.ShowFeedback("LOCAL TRIAL");
        }
    }

    private void RefreshRoomList()
    {
        StartCoroutine(RefreshRoomListRequest(ApiBase + "/fps/get_room_list/"));
    }

    private IEnumerator RefreshRoomListRequest(string uri)
    {
        using (UnityEngine.Networking.UnityWebRequest uwr = UnityEngine.Networking.UnityWebRequest.Get(uri))
        {
            yield return uwr.SendWebRequest();

            if (uwr.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                LogWebFailure("[RoomList]", uwr);
                yield break;
            }

            if (menuClosed)
            {
                yield break;
            }

            var resp = JsonUtility.FromJson<GetRoomListResponse>(uwr.downloadHandler.text);
            if (resp == null || resp.rooms == null)
            {
                Debug.LogWarning("[RoomList] JSON 无效或 rooms 为空: " +
                                 Truncate(uwr.downloadHandler.text));
                yield break;
            }

            foreach (var room in rooms)
            {
                if (room != null)
                {
                    room.onClick.RemoveAllListeners();
                    Destroy(room.gameObject);
                }
            }

            rooms.Clear();

            int k = 0;
            foreach (var room in resp.rooms)
            {
                if (roomButtonPrefab == null || menuUI == null)
                    break;

                int roomGamePort = ResolveGamePort(room);
                string roomName = room.name;
                string connectLogicalHost = string.IsNullOrEmpty(room.host) ? DeployHttpsHost : room.host;

                GameObject buttonObj = Instantiate(roomButtonPrefab, menuUI.transform);
                buttonObj.transform.localPosition = new Vector3(-21, 92 - k * 60, 0);
                Button button = buttonObj.GetComponent<Button>();
                var label = button != null ? button.GetComponentInChildren<TextMeshProUGUI>() : null;
                if (label != null)
                    label.text = roomName;

                if (button != null)
                {
                    button.onClick.AddListener(() =>
                    {
                        var transport = GetComponent<UNetTransport>();
                        ConfigureUnetClientEndpoints(transport, connectLogicalHost, roomGamePort);

                        if (NetworkManager.Singleton == null)
                        {
                            Debug.LogError("NetworkManager.Singleton 为 null。");
                            return;
                        }

                        NetworkManager.Singleton.StartClient();
                        DestroyAllButtons();
                    });
                    rooms.Add(button);
                }

                k++;
            }
        }
    }

    private void BuildRoom()
    {
        StartCoroutine(BuildRoomRequest(ApiBase + "/fps/build_room/"));
    }

    private IEnumerator BuildRoomRequest(string uri)
    {
        using (UnityEngine.Networking.UnityWebRequest uwr = UnityEngine.Networking.UnityWebRequest.Get(uri))
        {
            yield return uwr.SendWebRequest();

            if (uwr.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                LogWebFailure("[BuildRoom]", uwr);
                yield break;
            }

            string json = uwr.downloadHandler.text;
            var resp = JsonUtility.FromJson<BuildRoomResponse>(json);

            if (resp == null)
            {
                Debug.LogWarning("[BuildRoom] JSON 反序列化失败: " + Truncate(json));
                yield break;
            }

            if (string.IsNullOrEmpty(resp.error_message) ||
                !resp.error_message.Equals("success", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning(
                    $"[BuildRoom] error_message 非 success: '{resp.error_message}'  body={Truncate(json)}");
                yield break;
            }

            string connectLogicalHost = string.IsNullOrEmpty(resp.host) ? DeployHttpsHost : resp.host;
            int gamePort = ResolveGamePort(resp);
            if (gamePort <= 0)
            {
                Debug.LogWarning("[BuildRoom] 返回端口无效 port=" + resp.port + " internal_port=" +
                                 resp.internal_port);
                yield break;
            }

            var transport = GetComponent<UNetTransport>();
            ConfigureUnetClientEndpoints(transport, connectLogicalHost, gamePort);

            buildRoomPortForRemoveApi = resp.internal_port != 0 ? resp.internal_port : resp.port;

            Debug.Log(
                $"[BuildRoom] logicalHost={connectLogicalHost} ipv4Connect={transport?.ConnectAddress} gamePort={gamePort}, remove_room port={buildRoomPortForRemoveApi}");

            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("NetworkManager.Singleton 为 null。");
                yield break;
            }

            NetworkManager.Singleton.StartClient();
            DestroyAllButtons();
        }
    }

    private void RemoveRoom()
    {
        StartCoroutine(RemoveRoomRequest(ApiBase + "/fps/remove_room/?port=" + buildRoomPortForRemoveApi));
    }

    private IEnumerator RemoveRoomRequest(string uri)
    {
        using (UnityEngine.Networking.UnityWebRequest uwr = UnityEngine.Networking.UnityWebRequest.Get(uri))
        {
            yield return uwr.SendWebRequest();

            if (uwr.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                LogWebFailure("[RemoveRoom]", uwr);
                yield break;
            }

            var resp = JsonUtility.FromJson<RemoveRoomResponse>(uwr.downloadHandler.text);
            if (resp != null && resp.error_message == "success")
            {
            }
        }
    }

    private void DestroyAllButtons()
    {
        menuClosed = true;

        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveAllListeners();
            Destroy(refreshButton.gameObject);
        }

        if (buildButton != null)
        {
            buildButton.onClick.RemoveAllListeners();
            Destroy(buildButton.gameObject);
        }

        if (localTrialButton != null)
        {
            localTrialButton.onClick.RemoveAllListeners();
            Destroy(localTrialButton.gameObject);
        }

        foreach (var room in rooms)
        {
            if (room != null)
            {
                room.onClick.RemoveAllListeners();
                Destroy(room.gameObject);
            }
        }

        rooms.Clear();
    }
}
