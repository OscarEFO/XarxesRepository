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
    public int maxHealth = 3;
    public int currentHealth;

    [Header("State")]
    public bool isLocalPlayer = false;
    public int networkId;

    [Tooltip("If your sprite faces UP (default), set 0. If it faces RIGHT, set -90.")]
    public float rotationOffset = 0f;

    [Header("Shooting")]
    public GameObject bulletPrefab;   // assigned by client manager on spawn

    private Rigidbody2D rb;
    private Vector2 moveInput;

    private float desiredAngle = 0f;
    private Vector2 targetPosition;
    private float targetRotation;
    private Vector2 targetVelocity;
    private bool hasIncomingNetworkUpdate = false;

    void Start()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody2D>();

        if (tmp != null)
            tmp.SetText(userName);

        if (clientManager == null)
            clientManager = FindObjectOfType<ClientManagerUDP>();
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
            rb.linearVelocity = moveInput * moveSpeed;
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
        // Decrement locally and notify server
        currentHealth -= amount;
        Debug.Log($"{userName} took {amount} dmg → {currentHealth} HP");

        // Send an update so other clients learn this health (we reuse vel.y slot)
        if (clientManager != null)
        {
            // pack current health into vel.y (this keeps server protocol unchanged)
            clientManager.SendUpdate(networkId, transform.position, rb.rotation, new Vector2(rb.linearVelocity.x, currentHealth));
        }

        if (currentHealth <= 0)
        {
            Die();
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

        if (moveInput.magnitude > 1f)
            moveInput.Normalize();
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

        // vel.y contains the health value (packed by SendUpdate)
        currentHealth = Mathf.Max(0, Mathf.RoundToInt(vel.y));

        hasIncomingNetworkUpdate = true;
    }
}
