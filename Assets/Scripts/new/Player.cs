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
    public float rotationSpeed = 720f;
    public TMP_Text tmp;

    [Header("State")]
    public bool isLocalPlayer = false;     // Solo true en TU propio jugador
    public int networkId = -1;             // ID asignado por el servidor

    private Rigidbody2D rb;

    private Vector2 moveInput;
    private bool shooting;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        tmp.SetText(this.userName);

        if (isLocalPlayer && clientManager != null)
            clientManager.SetLocalPlayer(this);
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        HandleInput();
        SendUpdateToServer();
    }

    void FixedUpdate()
    {
        if (isLocalPlayer)
        {
            rb.linearVelocity = moveInput * moveSpeed;
            RotateTowardsMouse();
        }
    }

    private void HandleInput()
    {
        // Movimiento con teclado
        moveInput = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) moveInput.y += 1;
        if (Keyboard.current.sKey.isPressed) moveInput.y -= 1;
        if (Keyboard.current.aKey.isPressed) moveInput.x -= 1;
        if (Keyboard.current.dKey.isPressed) moveInput.x += 1;

        // Normalizar diagonal
        if (moveInput.magnitude > 1f)
            moveInput.Normalize();

        // Disparo con bot�n izquierdo del mouse
        shooting = Mouse.current.leftButton.wasPressedThisFrame;
        if (shooting)
            SendShootPacket();
    }

    private void RotateTowardsMouse()
    {
        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mousePosition);
        Vector2 direction = (mouseWorld - transform.position);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        rb.rotation = angle;
    }

    private void SendUpdateToServer()
    {
        if (clientManager == null) return;

        clientManager.SendUpdate(
            networkId,
            transform.position,
            rb.rotation,
            rb.linearVelocity,
            shooting
        );
    }

    private void SendShootPacket()
    {
        if (clientManager == null) return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mousePosition);
        Vector2 shootDir = (mouseWorld - transform.position).normalized;

        clientManager.SendShoot(
            networkId,
            transform.position,
            shootDir
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
