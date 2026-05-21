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

        if (TrialChallengeDirector.Singleton != null && TrialChallengeDirector.Singleton.IsPaused())
        {
            CancelInvoke("Shoot");
            return;
        }

        if (weaponManager == null)
        {
            return;
        }

        currentWeapon = weaponManager.GetCurrentWeapon();

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.K))
        {
            ShootServerRpc(transform.name, 10);
        }
#endif

        if (currentWeapon == null)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            weaponManager.Reload(currentWeapon);
            return;
        }

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
            } else if (Input.GetButtonUp("Fire1") || Input.GetKeyDown(KeyCode.Q) || currentWeapon.isReloading)
            {
                CancelInvoke("Shoot");
            }
        }
    }

    public void StopShooting()
    {
        CancelInvoke("Shoot");
    }

    private void OnHit(Vector3 pos, Vector3 normal, HitEffectMaterial material)  // 击中点的特效
    {
        if (weaponManager == null)
        {
            return;
        }

        WeaponGraphics graphics = weaponManager.GetCurrentGraphics();
        if (graphics == null)
        {
            return;
        }

        GameObject hitEffectPrefab;
        if (material == HitEffectMaterial.Metal)
        {
            hitEffectPrefab = graphics.metalHitEffectPrefab;
        } else
        {
            hitEffectPrefab = graphics.stoneHitEffectPrefab;
        }

        if (hitEffectPrefab == null)
        {
            return;
        }

        GameObject hitEffectObject = Instantiate(hitEffectPrefab, pos, Quaternion.LookRotation(normal));
        ParticleSystem particleSystem = hitEffectObject.GetComponent<ParticleSystem>();
        if (particleSystem != null)
        {
            particleSystem.Emit(1);
            particleSystem.Play();
        }
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
        if (weaponManager == null)
        {
            return;
        }

        WeaponGraphics graphics = weaponManager.GetCurrentGraphics();
        if (graphics != null && graphics.muzzleFlash != null)
        {
            graphics.muzzleFlash.Play();
        }

        AudioSource audioSource = weaponManager.GetCurrentAudioSource();
        if (audioSource != null)
        {
            audioSource.Play();
        }

        if (IsLocalPlayer)  // 施加后坐力
        {
            if (playerController != null)
            {
                playerController.AddRecoilForce(recoilForce);
            }
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
        if (currentWeapon == null || currentWeapon.isReloading || cam == null)
        {
            return;
        }

        if (currentWeapon.bullets <= 0)
        {
            CancelInvoke("Shoot");
            weaponManager.Reload(currentWeapon);
            return;
        }

        currentWeapon.bullets--;
        if (TrialChallengeDirector.Singleton != null)
        {
            TrialChallengeDirector.Singleton.RegisterShotFired();
        }

        if (currentWeapon.bullets <= 0)
        {
            CancelInvoke("Shoot");
            weaponManager.Reload(currentWeapon);
        }

        autoShootCount++;
        float recoilForce = currentWeapon.recoilForce;

        if (autoShootCount <= 3)
        {
            recoilForce *= 0.2f;
        }

        OnShootServerRpc(recoilForce);

        Vector3 trailEnd = cam.transform.position + cam.transform.forward * currentWeapon.range;
        RaycastHit hit;
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, currentWeapon.range, mask))
        {
            trailEnd = hit.point;
            TrialTarget target = hit.collider.GetComponentInParent<TrialTarget>();
            if (target != null)
            {
                if (TrialChallengeDirector.Singleton != null)
                {
                    TrialChallengeDirector.Singleton.RegisterTargetHit(target, currentWeapon.damage, hit.point);
                }
                OnHitServerRpc(hit.point, hit.normal, HitEffectMaterial.Metal);
            }
            else
            {
                TrialSpecter specter = hit.collider.GetComponentInParent<TrialSpecter>();
                if (specter != null)
                {
                    if (TrialChallengeDirector.Singleton != null)
                    {
                        TrialChallengeDirector.Singleton.RegisterSpecterHit(specter, currentWeapon.damage);
                    }
                    OnHitServerRpc(hit.point, hit.normal, HitEffectMaterial.Metal);
                }
                else if (hit.collider.CompareTag(PLAYER_TAG))
                {
                    ShootServerRpc(hit.collider.name, currentWeapon.damage);
                    OnHitServerRpc(hit.point, hit.normal, HitEffectMaterial.Metal);
                } else
                {
                    OnHitServerRpc(hit.point, hit.normal, HitEffectMaterial.Stone);
                }
            }
        }

        TrialEffects.SpawnBulletTrail(cam.transform.position + cam.transform.forward * 0.7f, trailEnd, new Color(1f, 0.86f, 0.38f));
    }

    [ServerRpc]
    private void ShootServerRpc(string name, int damage)
    {
        if (GameManager.Singleton == null)
        {
            return;
        }

        Player player = GameManager.Singleton.GetPlayer(name);
        if (player != null)
        {
            player.TakeDamage(damage);
        }
    }
}
