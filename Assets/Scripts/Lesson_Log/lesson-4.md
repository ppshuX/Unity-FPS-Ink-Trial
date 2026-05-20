# Lesson 4 打卡日志：射击伤害与重生

> 主题：在联机 FPS 中补齐战斗闭环（命中 -> 扣血 -> 死亡 -> 重生）。

## 本节核心知识点

### 1) `Awake()`、`Start()`、`OnNetworkSpawn()` 的时机

- `Awake()` 一定早于 `Start()` 和 `OnNetworkSpawn()`。
- `Start()` 和 `OnNetworkSpawn()` 的先后不保证固定顺序。
- `Awake()` 不论对象是否启用都会执行；`Start()` 仅对象启用时执行。
- `OnNetworkSpawn()` 在对象成功加入网络后触发，适合做联机初始化逻辑。

### 2) `NetworkVariable`

- 由 Server 端写入，值会自动同步到所有 Client。
- 本节用于同步：
  - `currentHealth`
  - `isDead`

### 3) `ClientRpc`

- 由 Server 调用，在每个 Client 上执行对应函数。
- 本节通过 `DieClientRpc()` 让所有客户端统一执行死亡表现。

### 4) 准星 / 命中检测

- 使用本地玩家相机发射 Raycast。
- 通过 `LayerMask` 过滤可命中层。
- 命中 `Player` 标签后，调用 `ServerRpc` 由服务器进行权威扣血。

---

## 本节实现结果（当前项目同步版）

### 关键流程

1. 本地点击开火。
2. `PlayerShooting` 进行射线检测，命中玩家后发起 `ShootServerRpc(name, damage)`。
3. Server 在 `GameManager` 中找到目标 `Player`，执行 `TakeDamage(damage)`。
4. 若血量归零：
   - 服务端设置 `isDead = true`；
   - 本地与所有客户端统一执行 `Die()`；
   - 等待 `respawnTime` 后 `Respawn()`，恢复状态并重生位置。

---

## 课上代码（我的当前版本）

### `Network/NetworkManagerUI.cs`

```csharp
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkManagerUI : MonoBehaviour
{
    [SerializeField]
    private Button hostBtn;
    [SerializeField]
    private Button serverBtn;
    [SerializeField]
    private Button clientBtn;

    // Start is called before the first frame update
    void Start()
    {
        hostBtn.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartHost();
            DestroyAllButtons();
        });
        serverBtn.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartServer();
            DestroyAllButtons();
        });
        clientBtn.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartClient();
            DestroyAllButtons();
        });
    }

    private void DestroyAllButtons()
    {
        Destroy(hostBtn.gameObject);
        Destroy(serverBtn.gameObject);
        Destroy(clientBtn.gameObject);
    }
}
```

### `Player/PlayerSetup.cs`

```csharp
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerSetup : NetworkBehaviour
{
    [SerializeField]
    private Behaviour[] componentsToDisable;

    private Camera sceneCamera;

    // Start is called before the first frame update
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsLocalPlayer)
        {
            DisableComponents();
        } else
        {
            sceneCamera = Camera.main;
            if (sceneCamera != null)
            {
                sceneCamera.gameObject.SetActive(false);
            }
        }

        string name = "Player " + GetComponent<NetworkObject>().NetworkObjectId.ToString();
        Player player = GetComponent<Player>();
        player.Setup();

        GameManager.Singleton.RegisterPlayer(name, player);
    }

    private void DisableComponents()
    {
        for (int i = 0; i < componentsToDisable.Length; i++)
        {
            componentsToDisable[i].enabled = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (sceneCamera != null)
        {
            sceneCamera.gameObject.SetActive(true);
        }

        GameManager.Singleton.UnRegisterPlayer(transform.name);
    }
}
```

### `Player/Player.cs`

```csharp
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Player : NetworkBehaviour
{
    [SerializeField]
    private int maxHealth = 100;
    [SerializeField]
    private Behaviour[] componentsToDisable;
    private bool[] componentsEnabled;
    private bool colliderEnabled;

    private NetworkVariable<int> currentHealth = new NetworkVariable<int>();
    private NetworkVariable<bool> isDead = new NetworkVariable<bool>();

    public void Setup()
    {
        componentsEnabled = new bool[componentsToDisable.Length];
        for (int i = 0; i < componentsToDisable.Length; i ++ )
        {
            componentsEnabled[i] = componentsToDisable[i].enabled;
        }
        Collider col = GetComponent<Collider>();
        colliderEnabled = col.enabled;

        SetDefaults();
    }

    private void SetDefaults()
    {
        for (int i = 0; i < componentsToDisable.Length; i ++ )
        {
            componentsToDisable[i].enabled = componentsEnabled[i];
        }
        Collider col = GetComponent<Collider>();
        col.enabled = colliderEnabled;

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            isDead.Value = false;
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead.Value) return;

        currentHealth.Value -= damage;

        if (currentHealth.Value <= 0)
        {
            currentHealth.Value = 0;
            isDead.Value = true;

            DieOnServer();
            DieClientRpc();
        }
    }

    private IEnumerator Respawn()
    {
        yield return new WaitForSeconds(GameManager.Singleton.MatchingSettings.respawnTime);

        SetDefaults();

        if (IsLocalPlayer)
        {
            transform.position = new Vector3(0f, 10f, 0f);
        }
    }

    private void DieOnServer()
    {
        Die();
    }

    [ClientRpc]
    private void DieClientRpc()
    {
        Die();
    }

    private void Die()
    {
        for (int i = 0; i < componentsToDisable.Length; i++)
        {
            componentsToDisable[i].enabled = false;
        }
        Collider col = GetComponent<Collider>();
        col.enabled = false;

        StartCoroutine(Respawn());
    }

    public int GetHealth()
    {
        return currentHealth.Value;
    }
}
```

### `Player/PlayerShooting.cs`

```csharp
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerShooting : NetworkBehaviour
{
    private const string PLAYER_TAG = "Player";

    [SerializeField]
    private PlayerWeapon weapon;
    [SerializeField]
    private LayerMask mask;

    private Camera cam;

    // Start is called before the first frame update
    void Start()
    {
        cam = GetComponentInChildren<Camera>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            Shoot();
        }
    }

    private void Shoot()
    {
        RaycastHit hit;
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, weapon.range, mask))
        {
            if (hit.collider.tag == PLAYER_TAG)
            {
                ShootServerRpc(hit.collider.name, weapon.damage);
            }
        }
    }

    [ServerRpc]
    private void ShootServerRpc(string name, int damage)
    {
        Player player = GameManager.Singleton.GetPlayer(name);
        player.TakeDamage(damage);
    }
}
```

### `GameManager/GameManager.cs`

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Singleton;

    [SerializeField]
    public MatchingSettings MatchingSettings;

    private Dictionary<string, Player> players = new Dictionary<string, Player>();

    private void Awake()
    {
        Singleton = this;
    }

    public void RegisterPlayer(string name, Player player)
    {
        player.transform.name = name;
        players.Add(name, player);
    }

    public void UnRegisterPlayer(string name)
    {
        players.Remove(name);
    }

    public Player GetPlayer(string name)
    {
        return players[name];
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(200f, 200f, 200f, 400f));
        GUILayout.BeginVertical();

        GUI.color = Color.red;
        foreach (string name in players.Keys)
        {
            Player player = GetPlayer(name);
            GUILayout.Label(name + " - " + player.GetHealth());
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}
```

### `GameManager/MatchingSettings.cs`

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MatchingSettings
{
    public float respawnTime = 3f;
}
```

---

## Inspector 配置检查（防止运行时报空）

- `Player` 预制体必须挂载 `Player (Script)`。
- `Player Setup (Script)` 的 `Components To Disable` 需要配置（通常包含 `PlayerInput`、`PlayerController`、`PlayerShooting`、本地相机相关控制组件）。
- `Player (Script)` 的 `Components To Disable` 也需要配置（用于死亡时禁用输入/控制/射击）。
- `PlayerShooting` 的 `Weapon` 不能为空，`Mask` 需要包含玩家所在层。
- `GameManager` 场景对象的 `Matching Settings` 不能为空。

---

## 本节复盘

- 完成了从“命中提示”到“联机战斗闭环”的升级。
- 明确了联机初始化逻辑应优先放在 `OnNetworkSpawn()`。
- 学会了 Server 权威写 `NetworkVariable` + `ClientRpc` 广播表现的组合用法。
- 为后续第 5 课（如击杀统计、UI 进阶、武器系统扩展）打下了基础。
