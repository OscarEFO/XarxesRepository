using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    [Header("Dependencies")]
    public ClientManagerUDP clientManager;

    [Header("Player Data")]
    public string userName = "User";
    public float moveSpeed = 5f;
    public TMP_Text tmp;

    [Header("State")]
    public bool isLocalPlayer = false;
    public int networkId;

    [Tooltip("If your sprite faces UP (default), set 0. If it faces RIGHT, set -90.")]
    public float rotationOffset = 0f;

    private Rigidbody2D rb;
    private Vector2 moveInput;

    private float desiredAngle = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        if (tmp != null)
            tmp.SetText(userName);

        if (clientManager == null)
            clientManager = FindObjectOfType<ClientManagerUDP>();
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        HandleInput();
        UpdateRotationFromMouse();
        SendUpdateToServer();
    }

    void FixedUpdate()
    {
        if (!isLocalPlayer) return;

        rb.linearVelocity = moveInput * moveSpeed;

        rb.rotation = desiredAngle;
    }

    private void HandleInput()
    {
        moveInput = Vector2.zero;

        var kb = Keyboard.current;
        if (kb.wKey.isPressed) moveInput.y += 1;
        if (kb.sKey.isPressed) moveInput.y -= 1;
        if (kb.aKey.isPressed) moveInput.x -= 1;
        if (kb.dKey.isPressed) moveInput.x += 1;

        if (moveInput.magnitude > 1f)
            moveInput.Normalize();
    }

    private void UpdateRotationFromMouse()
    {
        if (Mouse.current == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mousePos);
        Vector2 dir = mouseWorld - transform.position;

        if (dir.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            desiredAngle = angle + rotationOffset;
        }
    }

    private void SendUpdateToServer()
    {
        if (clientManager == null) return;

        clientManager.SendUpdate(
            networkId,
            transform.position,
            rb.rotation,
            rb.linearVelocity
        );
    }

    public void ApplyNetworkState(Vector2 pos, float rot, Vector2 vel)
    {
        if (isLocalPlayer) return;

        transform.position = pos;
        transform.rotation = Quaternion.Euler(0, 0, rot);
        rb.linearVelocity = vel;
    }
}
