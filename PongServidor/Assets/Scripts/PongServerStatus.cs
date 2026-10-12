using UnityEngine;

// HUD de diagnóstico do servidor: não requer cena nem Canvas.
public class PongServerStatus : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (FindFirstObjectByType<PongServerStatus>() == null)
            new GameObject("Pong Server Status").AddComponent<PongServerStatus>();
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(20, 20, 680, 35), "PONG - SERVIDOR UDP");
        GUI.Label(new Rect(20, 55, 680, 35), "Porta: 7777 | Inicie este projeto antes dos clientes.");
        GUI.Label(new Rect(20, 90, 680, 35), "Os clientes devem usar o IP local deste computador.");
        GUI.Label(new Rect(20, 125, 680, 35), "Consulte o Console para acompanhar entradas, pontos e saídas.");
    }
}
