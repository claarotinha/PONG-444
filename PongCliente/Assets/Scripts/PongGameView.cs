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
        float w = Screen.width, h = Screen.height;
        bool playing = client.matchStatus == "PLAYING";
        bool finished = client.matchStatus == "FINISHED";
        bool waiting = client.matchStatus == "WAITING";
        bool connecting = client.matchStatus == "CONNECTING";

        GUI.Label(new Rect(w / 2 - 180, 5, 360, 40),
            client.leftScore + "     x     " + client.rightScore, title);

        if (playing)
        {
            GUI.Label(new Rect(w / 2 - 280, 45, 560, 30),
                "Equipe " + (client.playerId < 2 ? "AZUL" : "AMARELA") +
                "  |  Jogador " + (client.playerId + 1) + "  |  W/S ou setas", label);
            return;
        }

        Rect panel = new Rect(w / 2 - 240, h / 2 - 145, 480, 290);
        GUI.Box(panel, "");
        if (finished)
        {
            string winner = client.leftScore > client.rightScore ? "AZUL" : "AMARELA";
            GUI.Label(new Rect(panel.x + 20, panel.y + 25, 440, 40),
                "VITORIA DA EQUIPE " + winner, title);
            GUI.Label(new Rect(panel.x + 20, panel.y + 85, 440, 30),
                "Placar final: " + client.leftScore + " x " + client.rightScore, label);
            if (GUI.Button(new Rect(panel.x + 120, panel.y + 155, 240, 45), "JOGAR NOVAMENTE"))
                client.RequestRestart();
            GUI.Label(new Rect(panel.x + 20, panel.y + 225, 440, 25),
                "A nova partida comeca quando os 4 estiverem conectados.", label);
            return;
        }

        GUI.Label(new Rect(panel.x + 20, panel.y + 18, 440, 40),
            waiting ? "SALA DE ESPERA" : "PONG 2 CONTRA 2", title);
        GUI.Label(new Rect(panel.x + 20, panel.y + 68, 440, 30),
            waiting ? "Jogadores conectados: " + client.connectedPlayers + " / 4"
                    : "Informe o IP do servidor para entrar na partida.", label);
        if (waiting)
        {
            GUI.Label(new Rect(panel.x + 20, panel.y + 110, 440, 30),
                "Voce e o jogador " + (client.playerId + 1) +
                " - equipe " + (client.playerId < 2 ? "AZUL" : "AMARELA"), label);
            GUI.Label(new Rect(panel.x + 20, panel.y + 145, 440, 30),
                "A partida comeca automaticamente com 4 jogadores.", label);
        }
        else if (connecting)
        {
            GUI.Label(new Rect(panel.x + 20, panel.y + 125, 440, 30),
                "Conectando... mantenha o servidor aberto.", label);
        }
        GUI.Label(new Rect(panel.x + 50, panel.y + 210, 90, 28), "IP:");
        ip = GUI.TextField(new Rect(panel.x + 135, panel.y + 210, 170, 28), ip);
        if (GUI.Button(new Rect(panel.x + 315, panel.y + 210, 115, 28), "CONECTAR"))
            client.ConnectTo(ip);
        GUI.Label(new Rect(panel.x + 20, panel.y + 250, 440, 25),
            "Servidor primeiro  |  Porta UDP 7777", label);
    }
}
