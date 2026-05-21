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
        if (primaryWeapon != null)
        {
            EquipWeapon(primaryWeapon);
        }
    }

    public void EquipWeapon(PlayerWeapon weapon)
    {
        if (weapon == null || weapon.graphics == null || weaponHolder == null)
        {
            return;
        }

        currentWeapon = weapon;

        if (weaponHolder.transform.childCount > 0)
        {
            Destroy(weaponHolder.transform.GetChild(0).gameObject);
        }

        GameObject weaponObject = Instantiate(currentWeapon.graphics, weaponHolder.transform);

        currentGraphics = weaponObject.GetComponent<WeaponGraphics>();
        currentAudiSource = weaponObject.GetComponent<AudioSource>();

        if (IsLocalPlayer && currentAudiSource != null)
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
            else if (Input.GetKeyDown(KeyCode.Alpha1) && currentWeapon != primaryWeapon)
            {
                ToggleWeaponServerRpc();
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) && currentWeapon != secondaryWeapon)
            {
                ToggleWeaponServerRpc();
            }
        }
    }

    public void Reload(PlayerWeapon playerWeapon)
    {
        if (playerWeapon == null || playerWeapon.isReloading || playerWeapon.bullets >= playerWeapon.maxBullets) return;
        playerWeapon.isReloading = true;

        StartCoroutine(ReloadCoroutine(playerWeapon));
    }

    public void RefillAllAmmo()
    {
        RefillWeapon(primaryWeapon);
        RefillWeapon(secondaryWeapon);
    }

    private void RefillWeapon(PlayerWeapon weapon)
    {
        if (weapon == null)
        {
            return;
        }

        weapon.bullets = weapon.maxBullets;
        weapon.isReloading = false;
    }

    private IEnumerator ReloadCoroutine(PlayerWeapon playerWeapon)
    {
        yield return new WaitForSeconds(playerWeapon.reloadTime);

        playerWeapon.bullets = playerWeapon.maxBullets;
        playerWeapon.isReloading = false;
    }
}
