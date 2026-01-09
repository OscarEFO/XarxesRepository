using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections;

public class Player : MonoBehaviour
{
    [Header("Dependencies")]
    public ClientManagerUDP clientManager;

    [Header("Player Data")]
    public string userName = "User";
    public float moveSpeed = 5f;
    public TMP_Text tmp;

    [Header("Health System")]
    public int maxHealth = 4;
    public int currentHealth;

    [Header("State")]
    public bool isLocalPlayer = false;
    public int networkId;

    [Tooltip("If your sprite faces UP (default), set 0. If it faces RIGHT, set -90.")]
    public float rotationOffset = 0f;

    [Header("Shooting")]
    public GameObject bulletPrefab;   // assigned by client manager on spawn

    [Header("Dash")]
    public float dashForce = 8f;
    public float dashDuration = 0.12f;
    public float dashCooldown = 0.8f;

    private bool isDashing = false;
    private float lastDashTime = -999f;

    private Rigidbody2D rb;
    private Vector2 moveInput;

    private float desiredAngle = 0f;
    private Vector2 targetPosition;
    private float targetRotation;
    private Vector2 targetVelocity;
    private bool hasIncomingNetworkUpdate = false;

    private HealthUIController healthUI;


    void Start()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody2D>();

        if (tmp != null)
            tmp.SetText(userName);

        if (clientManager == null)
            clientManager = FindObjectOfType<ClientManagerUDP>();

        healthUI = FindObjectOfType<HealthUIController>();

        if (healthUI != null)
        healthUI.UpdateHealth(isLocalPlayer, currentHealth);

    }

    void Update()
    {
        if (!isLocalPlayer)
            return;

        HandleInput();
        UpdateRotationFromMouse();
        SendUpdateToServer();
    }

    void FixedUpdate()
    {
        if (isLocalPlayer)
        {
            if (!isDashing)
            {
                rb.linearVelocity = moveInput * moveSpeed;
            }

            rb.rotation = desiredAngle;
            return;
        }

        // REMOTE PLAYER SMOOTHING
        if (hasIncomingNetworkUpdate)
        {
            transform.position = Vector2.Lerp(transform.position, targetPosition, 0.25f);

            float smoothedRot = Mathf.LerpAngle(transform.rotation.eulerAngles.z, targetRotation, 0.25f);
            transform.rotation = Quaternion.Euler(0, 0, smoothedRot);

            rb.linearVelocity = targetVelocity;
        }
    }

    // -------------------------------
    // HEALTH + DAMAGE
    // -------------------------------
    public void TakeDamage(int amount)
    {
        if (currentHealth <= 0)
            return;

        Debug.Log($"{userName} took {amount} dmg = {currentHealth - amount} HP");

        SetHealth(currentHealth - amount);

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        // Send update to server
        if (clientManager != null)
        {
            clientManager.SendUpdate(
                networkId,
                transform.position,
                rb.rotation,
                new Vector2(rb.linearVelocity.x, currentHealth)
            );
        }
    }


    public void AddHealth(int amount)
    {
        if (currentHealth <= 0)
            return;

        int oldHealth = currentHealth;
        SetHealth(currentHealth + amount);

        if (currentHealth == oldHealth)
            return;

        Debug.Log($"{userName} healed +{amount} → {currentHealth} HP");

        if (clientManager != null)
        {
            clientManager.SendUpdate(
                networkId,
                transform.position,
                rb.rotation,
                new Vector2(rb.linearVelocity.x, currentHealth)
            );
        }
    }



    private void Die()
    {
        Debug.Log($"{userName} (id={networkId}) died!");

        // Inform server (server should broadcast delete)
        if (clientManager != null)
        {
            clientManager.SendDelete(networkId);
        }

        // Destroy locally
        Destroy(gameObject);
    }

    // -------------------------------
    // COLLISION HANDLING (trigger & collision)
    // -------------------------------
    // This handler intentionally does not bail out for !isLocalPlayer.
    // We want ALL clients to detect impacts and update visuals/HP.
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;

        // Bullet damage
        if (other.CompareTag("Bullet"))
        {
            var proj = other.GetComponent<Projectile>();
            if (proj != null)
            {
                // Only get damaged by projectiles that belong to another player
                if (proj.ownerId != networkId)
                {
                    TakeDamage(proj.damage);
                }
            }

            // Destroy bullet (hit confirmed)
            Destroy(other.gameObject);
            return;
        }

        // Asteroid damage
        if (other.CompareTag("Asteroid"))
        {
            TakeDamage(1);

            // Optionally destroy asteroid on hit
            Destroy(other.gameObject);
            return;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // just in case colliders aren't triggers - treat similar to trigger version
        var other = collision.collider;
        if (other == null) return;

        if (other.CompareTag("Bullet"))
        {
            var proj = other.GetComponent<Projectile>();
            if (proj != null)
            {
                if (proj.ownerId != networkId)
                    TakeDamage(proj.damage);
            }
            Destroy(other.gameObject);
            return;
        }

        if (other.CompareTag("Asteroid"))
        {
            TakeDamage(1);
            Destroy(other.gameObject);
            return;
        }
    }

    // -------------------------------
    // INPUT
    // -------------------------------
    private void HandleInput()
    {
        moveInput = Vector2.zero;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.wKey.isPressed) moveInput.y += 1;
        if (kb.sKey.isPressed) moveInput.y -= 1;
        if (kb.aKey.isPressed) moveInput.x -= 1;
        if (kb.dKey.isPressed) moveInput.x += 1;
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            AddHealth(1);
        }

        if (moveInput.magnitude > 1f)
            moveInput.Normalize();
        if (kb.spaceKey.wasPressedThisFrame)
        {
            TryDash();
        }

    }

    private void TryDash()
    {
    if (!isLocalPlayer) return;
    if (isDashing) return;
    if (Time.time < lastDashTime + dashCooldown) return;

    // Dash direction = current movement direction
    Vector2 dashDir = moveInput;

    // If player is standing still, dash forward (rotation)
    if (dashDir.sqrMagnitude < 0.01f)
    {
        float angleRad = (rb.rotation - rotationOffset) * Mathf.Deg2Rad;
        dashDir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
    }

    dashDir.Normalize();

    StartCoroutine(DashCoroutine(dashDir));
    }
    
    private IEnumerator DashCoroutine(Vector2 dir)
    {
        isDashing = true;
        lastDashTime = Time.time;

        Vector2 originalVelocity = rb.linearVelocity;

        rb.linearVelocity = dir * dashForce;

        float t = 0f;
        while (t < dashDuration)
        {
            t += Time.deltaTime;
            yield return null;
        }

        rb.linearVelocity = originalVelocity;
        isDashing = false;
    }
    private void UpdateRotationFromMouse()
    {
        if (Mouse.current == null || Camera.main == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mousePos);
        Vector2 dir = mouseWorld - transform.position;

        if (dir.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            desiredAngle = angle + rotationOffset;
        }
    }

    private void SetHealth(int newHealth)
    {
        currentHealth = Mathf.Clamp(newHealth, 0, maxHealth);

        if (healthUI != null && currentHealth > 0)
            healthUI.UpdateHealth(isLocalPlayer, currentHealth);
    }

    // -------------------------------
    // NETWORK UPDATES
    // -------------------------------
    private void SendUpdateToServer()
    {
        if (clientManager == null) return;

        // encode current health in vel.y to reuse existing server packet shape
        clientManager.SendUpdate(
            networkId,
            transform.position,
            rb.rotation,
            new Vector2(rb.linearVelocity.x, currentHealth)
        );
    }

    // Called by client manager when receiving remote snapshots
    public void ApplyNetworkState(Vector2 pos, float rot, Vector2 vel)
    {
        if (isLocalPlayer) return;

        targetPosition = pos;
        targetRotation = rot;
        targetVelocity = new Vector2(vel.x, 0);

        int newHealth = Mathf.Max(0, Mathf.RoundToInt(vel.y));
        if (newHealth != currentHealth)
            SetHealth(newHealth);

        hasIncomingNetworkUpdate = true;
    }
}
