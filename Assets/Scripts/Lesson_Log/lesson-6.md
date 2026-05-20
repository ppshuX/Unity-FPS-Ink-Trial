# Lesson 6 打卡日志：射击、后坐力与命中特效同步（课上代码版）

> 主题：整合开火冷却、连发控制、后坐力、音效与命中特效，并完成联机同步。

## 本节核心知识点

### 1) `PlayerShooting` 完整开火逻辑

- 维护 `shootCoolDownTime` 处理单发冷却。
- 用 `autoShootCount` 控制连发前几枪后坐力减弱。
- 单发与连发两套输入逻辑：
  - `shootRate <= 0`：单发 + 冷却判定；
  - `shootRate > 0`：`InvokeRepeating("Shoot", ...)` 连发。
- `Q` 切枪时会 `CancelInvoke("Shoot")`，避免连发残留。

### 2) 联机同步模式

- 开枪特效：`OnShootServerRpc -> OnShootClientRpc`。
- 命中特效：`OnHitServerRpc -> OnHitClientRpc`。
- 伤害判定：`ShootServerRpc(name, damage)` 在服务器端权威执行。

### 3) `WeaponManager` 统一资源入口

- `EquipWeapon()` 负责销毁旧枪、生成新枪模型、缓存 `WeaponGraphics` 与 `AudioSource`。
- 提供：
  - `GetCurrentWeapon()`
  - `GetCurrentGraphics()`
  - `GetCurrentAudioSource()`
- 本地玩家音频设为 2D（`spatialBlend = 0f`）。

### 4) `PlayerController` 的后坐力接入

- `AddRecoilForce()` 在每次开火时累积后坐力。
- 旋转阶段将后坐力注入角色水平抖动与视角上抬。
- 每次 `FixedUpdate` 后衰减 `recoilForce *= 0.5f`。

---

## 本节关键脚本（课上原始代码）

### `PlayerShooting.cs`

```csharp
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerShooting : NetworkBehaviour
{
    private const string PLAYER_TAG = "Player";

    private WeaponManager weaponManager;
    private PlayerWeapon currentWeapon;

    private float shootCoolDownTime = 0f;  // 距离上次开枪，过了多久。单位：秒
    private int autoShootCount = 0;  // 当前一共连开多少枪

    [SerializeField]
    private LayerMask mask;

    private Camera cam;
    private PlayerController playerController;

    enum HitEffectMaterial
    {
        Metal,
        Stone,
    }

    // Start is called before the first frame update
    void Start()
    {
        cam = GetComponentInChildren<Camera>();
        weaponManager = GetComponent<WeaponManager>();
        playerController = GetComponent<PlayerController>();
    }

    // Update is called once per frame
    void Update()
    {
        shootCoolDownTime += Time.deltaTime;

        if (!IsLocalPlayer) return;

        currentWeapon = weaponManager.GetCurrentWeapon();

        if (currentWeapon.shootRate <= 0)  // 单发
        {
            if (Input.GetButtonDown("Fire1") && shootCoolDownTime >= currentWeapon.shootCoolDownTime)
            {
                autoShootCount = 0;
                Shoot();
                shootCoolDownTime = 0f;  // 重置冷却时间
            }
        } else
        {
            if (Input.GetButtonDown("Fire1"))
            {
                autoShootCount = 0;
                InvokeRepeating("Shoot", 0f, 1f / currentWeapon.shootRate);
            } else if (Input.GetButtonUp("Fire1") || Input.GetKeyDown(KeyCode.Q))
            {
                CancelInvoke("Shoot");
            }
        }
    }

    private void OnHit(Vector3 pos, Vector3 normal, HitEffectMaterial material)  // 击中点的特效
    {
        GameObject hitEffectPrefab;
        if (material == HitEffectMaterial.Metal)
        {
            hitEffectPrefab = weaponManager.GetCurrentGraphics().metalHitEffectPrefab;
        } else
        {
            hitEffectPrefab = weaponManager.GetCurrentGraphics().stoneHitEffectPrefab;
        }

        GameObject hitEffectObject = Instantiate(hitEffectPrefab, pos, Quaternion.LookRotation(normal));
        ParticleSystem particleSystem = hitEffectObject.GetComponent<ParticleSystem>();
        particleSystem.Emit(1);
        particleSystem.Play();
        Destroy(hitEffectObject, 1f);
    }

    [ClientRpc]
    private void OnHitClientRpc(Vector3 pos, Vector3 normal, HitEffectMaterial material)
    {
        OnHit(pos, normal, material);
    }

    [ServerRpc]
    private void OnHitServerRpc(Vector3 pos, Vector3 normal, HitEffectMaterial material)
    {
        if (!IsHost)
        {
            OnHit(pos, normal, material);
        }
        OnHitClientRpc(pos, normal, material);
    }

    private void OnShoot(float recoilForce)  // 每次射击相关的逻辑，包括特效、声音等
    {
        weaponManager.GetCurrentGraphics().muzzleFlash.Play();
        weaponManager.GetCurrentAudioSource().Play();

        if (IsLocalPlayer)  // 施加后坐力
        {
            playerController.AddRecoilForce(recoilForce);
        }
    }

    [ClientRpc]
    private void OnShootClientRpc(float recoilForce)
    {
        OnShoot(recoilForce);
    }

    [ServerRpc]
    private void OnShootServerRpc(float recoilForce)
    {
        if (!IsHost)
        {
            OnShoot(recoilForce);
        }
        OnShootClientRpc(recoilForce);
    }

    private void Shoot()
    {
        autoShootCount++;
        float recoilForce = currentWeapon.recoilForce;

        if (autoShootCount <= 3)
        {
            recoilForce *= 0.2f;
        }

        OnShootServerRpc(recoilForce);

        RaycastHit hit;
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, currentWeapon.range, mask))
        {
            if (hit.collider.tag == PLAYER_TAG)
            {
                ShootServerRpc(hit.collider.name, currentWeapon.damage);
                OnHitServerRpc(hit.point, hit.normal, HitEffectMaterial.Metal);
            } else
            {
                OnHitServerRpc(hit.point, hit.normal, HitEffectMaterial.Stone);
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

### `WeaponManager.cs`

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class WeaponManager : NetworkBehaviour
{
    [SerializeField]
    private PlayerWeapon primaryWeapon;

    [SerializeField]
    private PlayerWeapon secondaryWeapon;

    [SerializeField]
    private GameObject weaponHolder;

    private PlayerWeapon currentWeapon;
    private WeaponGraphics currentGraphics;
    private AudioSource currentAudiSource;

    // Start is called before the first frame update
    void Start()
    {
        EquipWeapon(primaryWeapon);
    }

    public void EquipWeapon(PlayerWeapon weapon)
    {
        currentWeapon = weapon;

        if (weaponHolder.transform.childCount > 0)
        {
            Destroy(weaponHolder.transform.GetChild(0).gameObject);
        }

        GameObject weaponObject = Instantiate(currentWeapon.graphics, weaponHolder.transform.position, weaponHolder.transform.rotation);
        weaponObject.transform.SetParent(weaponHolder.transform);

        currentGraphics = weaponObject.GetComponent<WeaponGraphics>();
        currentAudiSource = weaponObject.GetComponent<AudioSource>();

        if (IsLocalPlayer)
        {
            currentAudiSource.spatialBlend = 0f;
        }
    }

    public PlayerWeapon GetCurrentWeapon()
    {
        return currentWeapon;
    }

    public WeaponGraphics GetCurrentGraphics()
    {
        return currentGraphics;
    }

    public AudioSource GetCurrentAudioSource()
    {
        return currentAudiSource;
    }

    private void ToggleWeapon()
    {
        if (currentWeapon == primaryWeapon)
        {
            EquipWeapon(secondaryWeapon);
        } else
        {
            EquipWeapon(primaryWeapon);
        }
    }

    [ClientRpc]
    private void ToggleWeaponClientRpc()
    {
        ToggleWeapon();
    }

    [ServerRpc]
    private void ToggleWeaponServerRpc()
    {
        if (!IsHost)
        {
            ToggleWeapon();
        }
        ToggleWeaponClientRpc();
    }

    // Update is called once per frame
    void Update()
    {
        if (IsLocalPlayer)
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                ToggleWeaponServerRpc();
            }
        }
    }
}
```

### `PlayerWeapon.cs`

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlayerWeapon
{
    public string name = "M16A1";

    public int damage = 10;
    public float range = 100f;

    public float shootRate = 10f;  // 一秒可以打多少发子弹。如果小于等于0，则为单发。
    public float shootCoolDownTime = 0.75f;  // 单发模式的冷却时间
    public float recoilForce = 2f;  // 后坐力

    public GameObject graphics;
}
```

### `PlayerController.cs`

```csharp
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField]
    private Rigidbody rb;
    [SerializeField]
    private Camera cam;

    private Vector3 velocity = Vector3.zero;  // 速度：每秒移动的距离
    private Vector3 yRotation = Vector3.zero;  // 旋转角色
    private Vector3 xRotation = Vector3.zero;  // 旋转视角
    private float recoilForce = 0f;  // 后坐力

    private float cameraRotationTotal = 0f;  // 累计转了多少度
    [SerializeField]
    private float cameraRotationLimit = 85f;

    private Vector3 thrusterForce = Vector3.zero;  // 向上的推力


    public void Move(Vector3 _velocity)
    {
        velocity = _velocity;
    }


    public void Rotate(Vector3 _yRotation, Vector3 _xRotation)
    {
        yRotation = _yRotation;
        xRotation = _xRotation;
    }

    public void Thrust(Vector3 _thrusterForce)
    {
        thrusterForce = _thrusterForce;
    }

    public void AddRecoilForce(float newRecoilForce)
    {
        recoilForce += newRecoilForce;
    }

    private void PerformMovement()
    {
        if (velocity != Vector3.zero)
        {
            rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
        }

        if (thrusterForce != Vector3.zero)
        {
            rb.AddForce(thrusterForce);  // 作用Time.fixedDeltaTime秒：0.02s
        }
    }

    private void PerformRotation()
    {
        if (recoilForce < 0.1)
        {
            recoilForce = 0f;
        }

        if (yRotation != Vector3.zero || recoilForce > 0)
        {
            rb.transform.Rotate(yRotation + rb.transform.up * Random.Range(-2f * recoilForce, 2f * recoilForce));
        }

        if (xRotation != Vector3.zero || recoilForce > 0)
        {
            cameraRotationTotal += xRotation.x - recoilForce;
            cameraRotationTotal = Mathf.Clamp(cameraRotationTotal, -cameraRotationLimit, cameraRotationLimit);
            cam.transform.localEulerAngles = new Vector3(cameraRotationTotal, 0f, 0f);
        }

        recoilForce *= 0.5f;
    }

    private void FixedUpdate()
    {
        PerformMovement();
        PerformRotation();
    }
}
```
