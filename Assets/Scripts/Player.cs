using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 3f;
    public Transform firePoint;
    // �ltimo input le�do (cache para evitar enviar constantemente lo mismo)
    public GameObject projectilePrefab;
    public float projectileSpeed = 10f;
    private string lastInput = "NONE";

    void Update()
    {
        // Movimiento local (responsivo)
        if (Keyboard.current == null) return;

        Vector3 move = Vector3.zero;

        if (Keyboard.current.wKey.isPressed) move += Vector3.up;
        if (Keyboard.current.sKey.isPressed) move += Vector3.down;
        if (Keyboard.current.aKey.isPressed) move += Vector3.left;
        if (Keyboard.current.dKey.isPressed) move += Vector3.right;

        transform.Translate(move.normalized * speed * Time.deltaTime, Space.World);
        
        if (ClientUDP.Instance != null)
            ClientUDP.Instance.SendPlayerState(transform.position, transform.eulerAngles.z);

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            ShootProjectile();
        }        // Actualizamos lastInput para que GetInputString devuelva el input actual

        lastInput = ComputeInputString();
    }

    // Devuelve "W","A","S","D" o "NONE" basado en la entrada actual
    public string GetInputString()
    {
        return lastInput;
    }
    private void ShootProjectile()
    {
        if (firePoint == null || projectilePrefab == null) return;

        GameObject proj = Instantiate(projectilePrefab, firePoint.position, transform.rotation);
        Rigidbody2D rb = proj.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.linearVelocity = transform.up * projectileSpeed;

        if (ClientUDP.Instance != null)
            ClientUDP.Instance.SendProjectile(firePoint.position, transform.eulerAngles.z, transform.up);
    }
    // Calcula el input a partir del teclado (sin cambiar estado)
    private string ComputeInputString()
    {
        if (Keyboard.current == null) return "NONE";

        if (Keyboard.current.wKey.isPressed) return "W";
        if (Keyboard.current.sKey.isPressed) return "S";
        if (Keyboard.current.aKey.isPressed) return "A";
        if (Keyboard.current.dKey.isPressed) return "D";
        return "NONE";
    }

    // Aplicar la posici�n autoritativa (usado por NetworkPlayer cuando llega estado del server)
    public void ApplyPosition(Vector2 pos)
    {
        // Mantener z = 0 (2D)
        transform.position = new Vector3(pos.x, pos.y, 0f);
    }
}
