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
        if (componentsToDisable == null)
        {
            componentsToDisable = new Behaviour[0];
        }

        componentsEnabled = new bool[componentsToDisable.Length];
        for (int i = 0; i < componentsToDisable.Length; i ++ )
        {
            componentsEnabled[i] = componentsToDisable[i].enabled;
        }
        Collider col = GetComponent<Collider>();
        colliderEnabled = col != null && col.enabled;

        SetDefaults();
    }

    private void SetDefaults()
    {
        for (int i = 0; i < componentsToDisable.Length; i ++ )
        {
            componentsToDisable[i].enabled = componentsEnabled[i];
        }
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = colliderEnabled;
        }

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            isDead.Value = false;
        }
    }

    public bool IsDead()
    {
        return isDead.Value;
    }

    public void TakeDamage(int damage)  // 收到了伤害，只在服务器端被调用
    {
        damage = Mathf.Max(0, damage);
        if (isDead.Value) return;

        currentHealth.Value -= damage;

        if (currentHealth.Value <= 0)
        {
            currentHealth.Value = 0;
            isDead.Value = true;

            if (!IsHost)
            {
                DieOnServer();
            }
            DieClientRpc();
        }
    }

    private IEnumerator Respawn()  // 重生
    {
        float respawnTime = GameManager.Singleton != null && GameManager.Singleton.MatchingSettings != null
            ? GameManager.Singleton.MatchingSettings.respawnTime
            : 3f;
        yield return new WaitForSeconds(respawnTime);

        SetDefaults();
        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.SetInteger("direction", 0);
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = true;
        }

        if (IsLocalPlayer)
        {
            transform.position = TrialChallengeDirector.GetRecommendedRespawnPosition(new Vector3(0f, 10f, 0f));
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
        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
        {
            shooting.StopShooting();
        }

        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.SetInteger("direction", -1);
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
        }

        for (int i = 0; i < componentsToDisable.Length; i++)
        {
            componentsToDisable[i].enabled = false;
        }
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        StartCoroutine(Respawn());
    }

    public int GetHealth()
    {
        return currentHealth.Value;
    }

    public void RestoreHealth(int amount)
    {
        if (!IsServer || isDead.Value)
        {
            return;
        }

        currentHealth.Value = Mathf.Min(maxHealth, currentHealth.Value + Mathf.Max(0, amount));
    }

    public int GetMaxHealth()
    {
        return maxHealth;
    }
}
