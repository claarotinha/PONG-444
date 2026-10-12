using UnityEngine;

// Interface provisória funcional, criada em execução sem editar SampleScene.
public class PongGameView : MonoBehaviour
{
    public PongUdpClient client;
    private Sprite square;
    private Transform ball;
    private readonly Transform[] paddles = new Transform[4];
    private GUIStyle title, label;
    private Transform ballHalo;
    private readonly Transform[] paddleHalos = new Transform[4];
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
        cam.backgroundColor = new Color(0.035f, 0.055f, 0.10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        Create("Arena", new Vector3(16.2f, 9f, 1), new Color(0.07f, 0.10f, 0.17f)).position = new Vector3(0, 0, 2);
        Create("Lado azul", new Vector3(8f, 9f, 1), new Color(0.08f, 0.16f, 0.23f)).position = new Vector3(-4, 0, 1);
        Create("Lado dourado", new Vector3(8f, 9f, 1), new Color(0.20f, 0.14f, 0.08f)).position = new Vector3(4, 0, 1);
        ballHalo = Create("Brilho bola", new Vector3(0.52f, 0.52f, 1), new Color(0.2f, 0.65f, 1f, 0.5f));
        ball = Create("Bola", new Vector3(0.3f, 0.3f, 1), Color.white);
        for (int i = 0; i < 4; i++)
        {
            Color team = i < 2 ? new Color(0.1f, 0.8f, 1f) : new Color(1f, 0.7f, 0.15f);
            paddleHalos[i] = Create("Brilho raquete " + (i + 1), new Vector3(0.39f, 1.54f, 1), team * 0.55f);
            paddles[i] = Create("Raquete " + (i + 1), new Vector3(0.25f, 1.4f, 1), team);
        }
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
        renderer.sortingOrder = name == "Arena" || name.StartsWith("Lado") ? -10
            : name.StartsWith("Brilho") ? 0 : 1;
        obj.transform.localScale = scale;
        return obj.transform;
    }

    private void Update()
    {
        if (client == null) { client = FindFirstObjectByType<PongUdpClient>(); return; }
        ball.position = new Vector3(client.ballPosition.x, client.ballPosition.y, 0);
        ballHalo.position = ball.position;
        for (int i = 0; i < 4; i++)
        {
            paddles[i].position = new Vector3(i < 2 ? -7.4f : 7.4f, client.paddlePositions[i], 0);
            paddleHalos[i].position = paddles[i].position;
        }
    }

    private void OnGUI()
    {
        if (client == null) return;
        float w = Screen.width, h = Screen.height;
        bool playing = client.matchStatus == "PLAYING";
        bool finished = client.matchStatus == "FINISHED";
        bool waiting = client.matchStatus == "WAITING";
        bool connecting = client.matchStatus == "CONNECTING";

        GUI.Label(new Rect(w / 2 - 230, 5, 120, 35), "AZUL", label);
        GUI.Label(new Rect(w / 2 + 110, 5, 120, 35), "DOURADO", label);
        GUI.Label(new Rect(w / 2 - 110, 5, 220, 40),
            client.leftScore + "    :    " + client.rightScore, title);

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
