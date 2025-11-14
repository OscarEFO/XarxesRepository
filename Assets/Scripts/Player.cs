using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Tooltip("Si true el script aplica movimiento localmente (use en Server player). Si false solo lee input para enviarlo.")]
    public bool applyMovementLocally = false;

    public float speed = 3f;

    // cached input string (W/A/S/D or NONE)
    private string lastInput = "NONE";

    void Update()
    {
        // Always update cached input so NetworkPlayer can read it
        lastInput = ComputeInputString();

        if (!applyMovementLocally) return;

        // If this instance should move locally (server side)
        Vector3 move = Vector3.zero;
        if (Keyboard.current == null) return;

        if (Keyboard.current.wKey.isPressed) move += Vector3.up;
        if (Keyboard.current.sKey.isPressed) move += Vector3.down;
        if (Keyboard.current.aKey.isPressed) move += Vector3.left;
        if (Keyboard.current.dKey.isPressed) move += Vector3.right;

        transform.Translate(move.normalized * speed * Time.deltaTime, Space.World);
    }

    // Devuelve "W","A","S","D" o "NONE"
    public string GetInputString()
    {
        return lastInput;
    }

    // Devuelve vector normalizado de la entrada actual (no mueve el transform)
    public Vector2 GetInputVector()
    {
        if (Keyboard.current == null) return Vector2.zero;

        Vector2 v = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) v += Vector2.up;
        if (Keyboard.current.sKey.isPressed) v += Vector2.down;
        if (Keyboard.current.aKey.isPressed) v += Vector2.left;
        if (Keyboard.current.dKey.isPressed) v += Vector2.right;
        return v.normalized;
    }

    private string ComputeInputString()
    {
        if (Keyboard.current == null) return "NONE";
        if (Keyboard.current.wKey.isPressed) return "W";
        if (Keyboard.current.sKey.isPressed) return "S";
        if (Keyboard.current.aKey.isPressed) return "A";
        if (Keyboard.current.dKey.isPressed) return "D";
        return "NONE";
    }

    // Aplicar posición autoritativa enviada por el servidor (reconciliación)
    public void ApplyPosition(Vector2 pos)
    {
        transform.position = new Vector3(pos.x, pos.y, 0f);
    }
}
