using UnityEngine;

// Interface provisória funcional, criada em execução sem editar SampleScene.
public class PongGameView : MonoBehaviour
{
    public PongUdpClient client;
    private Sprite square;
    private Transform ball;
    private readonly Transform[] paddles = new Transform[4];
    private GUIStyle title, label;
    private string ip = "127.0.0.1";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (FindFirstObjectByType<PongGameView>() == null)
            new GameObject("Pong Game View").AddComponent<PongGameView>();
    }

    private void Start()
    {
        client = FindFirstObjectByType<PongUdpClient>();
        ip = client != null ? client.serverIp : ip;
        Texture2D texture = Texture2D.whiteTexture;
        square = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject obj = new GameObject("Pong Camera");
            cam = obj.AddComponent<Camera>();
            obj.tag = "MainCamera";
        }
        cam.orthographic = true;
        cam.orthographicSize = 5.5f;
        cam.transform.position = new Vector3(0, 0, -10);
        cam.backgroundColor = Color.black;
        cam.clearFlags = CameraClearFlags.SolidColor;
        ball = Create("Bola", new Vector3(0.35f, 0.35f, 1), Color.white);
        for (int i = 0; i < 4; i++)
            paddles[i] = Create("Raquete " + (i + 1), new Vector3(0.25f, 1.4f, 1), i < 2 ? Color.cyan : Color.yellow);
        Create("Parede superior", new Vector3(16.4f, 0.12f, 1), Color.gray).position = new Vector3(0, 4.6f, 0);
        Create("Parede inferior", new Vector3(16.4f, 0.12f, 1), Color.gray).position = new Vector3(0, -4.6f, 0);
        for (int i = -4; i <= 4; i++)
            Create("Linha central", new Vector3(0.07f, 0.5f, 1), Color.gray).position = new Vector3(0, i, 0);
        title = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        title.normal.textColor = Color.white;
        label = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter };
        label.normal.textColor = Color.white;
    }

    private Transform Create(string name, Vector3 scale, Color color)
    {
        GameObject obj = new GameObject(name);
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = square;
        renderer.color = color;
        obj.transform.localScale = scale;
        return obj.transform;
    }

    private void Update()
    {
        if (client == null) { client = FindFirstObjectByType<PongUdpClient>(); return; }
        ball.position = new Vector3(client.ballPosition.x, client.ballPosition.y, 0);
        for (int i = 0; i < 4; i++)
            paddles[i].position = new Vector3(i < 2 ? -7.4f : 7.4f, client.paddlePositions[i], 0);
    }

    private void OnGUI()
    {
        if (client == null) return;
        float width = Screen.width;
        GUI.Label(new Rect(width / 2 - 180, 5, 360, 40),
            client.leftScore + "     x     " + client.rightScore, title);
        string status = client.matchStatus == "WAITING"
            ? "Aguardando jogadores: " + client.connectedPlayers + "/4"
            : client.matchStatus == "PLAYING" ? "Jogando - equipe " + (client.playerId < 2 ? "AZUL" : "AMARELA")
            : client.matchStatus == "FINISHED" ? "Fim de partida!" : "Conectando...";
        GUI.Label(new Rect(width / 2 - 260, 42, 520, 28), status, label);
        if (client.matchStatus == "FINISHED")
        {
            if (GUI.Button(new Rect(width / 2 - 90, 80, 180, 38), "Jogar novamente"))
                client.RequestRestart();
        }
        GUI.Label(new Rect(10, Screen.height - 65, 380, 25),
            "Jogador: " + (client.playerId < 0 ? "?" : (client.playerId + 1).ToString()) + " | W/S ou setas");
        GUI.Label(new Rect(10, Screen.height - 38, 110, 25), "IP servidor:");
        ip = GUI.TextField(new Rect(120, Screen.height - 38, 150, 25), ip);
        if (GUI.Button(new Rect(280, Screen.height - 38, 110, 25), "Conectar"))
            client.ConnectTo(ip);
    }
}
