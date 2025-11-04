using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManagerUI : MonoBehaviour
{
    public void StartAsServer()
    {
        // Cargar escena del servidor
        SceneManager.LoadScene("Server");
    }

    public void StartClient()
    {
        // Cargar escena del cliente
        SceneManager.LoadScene("Client");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
