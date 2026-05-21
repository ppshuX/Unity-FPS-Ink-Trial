using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PlayerUI : MonoBehaviour
{
    public static PlayerUI Singleton;

    private Player player = null;

    [SerializeField]
    private TextMeshProUGUI bulletsText;
    [SerializeField]
    private GameObject bulletsObject;

    private WeaponManager weaponManager;

    [SerializeField]
    private Transform healthBarFill;
    [SerializeField]
    private GameObject healthBarObject;

    private float displayedHealthRatio = 1f;


    private void Awake()
    {
        Singleton = this;
    }

    public void setPlayer(Player localPlayer)
    {
        player = localPlayer;
        weaponManager = player.GetComponent<WeaponManager>();
        if (bulletsObject != null)
        {
            bulletsObject.SetActive(true);
        }
        if (healthBarObject != null)
        {
            healthBarObject.SetActive(true);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (player == null) return;

        if (weaponManager == null)
        {
            return;
        }

        var currentWeapon = weaponManager.GetCurrentWeapon();
        if (currentWeapon == null)
        {
            return;
        }

        if (bulletsText != null && currentWeapon.isReloading)
        {
            bulletsText.text = currentWeapon.name + "  RELOADING";
            bulletsText.color = new Color(1f, 0.82f, 0.38f);
        }
        else if (bulletsText != null)
        {
            bulletsText.text = currentWeapon.name + "  " + currentWeapon.bullets + "/" + currentWeapon.maxBullets;
            bulletsText.color = currentWeapon.bullets <= Mathf.CeilToInt(currentWeapon.maxBullets * 0.25f)
                ? new Color(1f, 0.45f, 0.38f)
                : Color.white;
        }

        if (healthBarFill != null)
        {
            float targetHealthRatio = Mathf.Clamp01((float)player.GetHealth() / Mathf.Max(1, player.GetMaxHealth()));
            displayedHealthRatio = Mathf.MoveTowards(displayedHealthRatio, targetHealthRatio, Time.deltaTime * 3.5f);
            healthBarFill.localScale = new Vector3(displayedHealthRatio, 1f, 1f);
        }
    }
}
