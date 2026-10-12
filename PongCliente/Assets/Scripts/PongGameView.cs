using UnityEngine;

// Arena 2x2 gerada em tempo de execução: 2 raquetes azuis à esquerda,
// 2 douradas à direita. Não depende de objetos da SampleScene.
public class PongGameView : MonoBehaviour
{
    private PongUdpClient client;
    private Sprite square;
    private Transform ball;
    private readonly Transform[] paddles = new Transform[4];
    private readonly Transform[] highlights = new Transform[4];
    private Camera gameCamera;
    private GUIStyle heading, textStyle, scoreStyle;
    private string ip = "127.0.0.1";
    private bool ipLoaded;
    private static readonly Color Blue = new Color(0.12f, 0.82f, 1f);
    private static readonly Color Gold = new Color(1f, 0.72f, 0.22f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (FindFirstObjectByType<PongGameView>() == null)
            new GameObject("Pong Game View").AddComponent<PongGameView>();
    }

    private void Start()
    {
        client = FindFirstObjectByType<PongUdpClient>();
        // Texture2D.whiteTexture tem 1 pixel; 1 pixel por unidade produz quadrados 1x1.
        square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1),
            new Vector2(0.5f, 0.5f), 1f);
        gameCamera = Camera.main;
        if (gameCamera == null)
        {
            GameObject cameraObject = new GameObject("Pong Camera");
            cameraObject.tag = "MainCamera";
            gameCamera = cameraObject.AddComponent<Camera>();
        }
        gameCamera.orthographic = true;
        gameCamera.transform.position = new Vector3(0, 0, -10);
        gameCamera.backgroundColor = new Color(0.025f, 0.04f, 0.08f);
        gameCamera.clearFlags = CameraClearFlags.SolidColor;
        gameCamera.cullingMask = ~0;
        FitCamera();

        Create("Arena", new Vector2(16.2f, 9f), new Color(0.07f, 0.10f, 0.16f), 0, 0, -10);
        Create("Area azul", new Vector2(8f, 9f), new Color(0.07f, 0.17f, 0.25f), -4, 0, -9);
        Create("Area dourada", new Vector2(8f, 9f), new Color(0.20f, 0.14f, 0.07f), 4, 0, -9);
        Create("Borda superior", new Vector2(16.3f, 0.12f), Color.white, 0, 4.55f, 1);
        Create("Borda inferior", new Vector2(16.3f, 0.12f), Color.white, 0, -4.55f, 1);
        for (int n = -4; n <= 4; n++)
            Create("Divisoria " + n, new Vector2(0.07f, 0.48f),
                new Color(0.65f, 0.75f, 0.85f), 0, n, 1);
        ball = Create("Bola", new Vector2(0.34f, 0.34f), Color.white, 0, 0, 4);
        for (int i = 0; i < 4; i++)
        {
            float x = i < 2 ? -7.4f : 7.4f;
            float y = i % 2 == 0 ? 2f : -2f;
            Color team = i < 2 ? Blue : Gold;
            highlights[i] = Create("Contorno Jogador " + (i + 1),
                new Vector2(0.42f, 1.55f), Color.white, x, y, 2);
            paddles[i] = Create("Raquete Jogador " + (i + 1),
                new Vector2(0.27f, 1.4f), team, x, y, 3);
        }
    }

    private Transform Create(string name, Vector2 size, Color color, float x, float y, int order)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(transform);
        obj.transform.position = new Vector3(x, y, 0);
        obj.transform.localScale = new Vector3(size.x, size.y, 1);
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = square;
        renderer.color = color;
        renderer.sortingOrder = order;
        return obj.transform;
    }

    private void FitCamera()
    {
        if (gameCamera == null) return;
        // A largura do campo é 16 unidades. Evita cortar as raquetes em Free Aspect.
        float aspect = Mathf.Max(0.1f, (float)Screen.width / Mathf.Max(1, Screen.height));
        gameCamera.orthographicSize = Mathf.Max(5.15f, 9f / aspect);
    }

    private void Update()
    {
        FitCamera();
        if (client == null) client = FindFirstObjectByType<PongUdpClient>();
        if (client == null) return;
        if (!ipLoaded) { ip = client.serverIp; ipLoaded = true; }
        if (ball != null)
            ball.position = new Vector3(client.ballPosition.x, client.ballPosition.y, 0);
        for (int i = 0; i < 4; i++)
        {
            if (paddles[i] == null || highlights[i] == null) continue;
            float y = client.playerId >= 0 && client.paddlePositions != null && client.paddlePositions.Length == 4
                ? client.paddlePositions[i] : (i % 2 == 0 ? 2f : -2f);
            Vector3 position = new Vector3(i < 2 ? -7.4f : 7.4f, y, 0);
            paddles[i].position = position;
            highlights[i].position = position;
        }
    }

    private void Styles()
    {
        if (heading != null) return;
        heading = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24, alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        heading.normal.textColor = Color.white;
        textStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        textStyle.normal.textColor = Color.white;
        scoreStyle = new GUIStyle(heading) { fontSize = 28 };
    }

    private void OnGUI()
    {
        if (client == null) return;
        Styles();
        float w = Screen.width, h = Screen.height;
        GUI.Label(new Rect(w / 2 - 220, 4, 115, 35), "AZUL", textStyle);
        GUI.Label(new Rect(w / 2 + 105, 4, 115, 35), "DOURADO", textStyle);
        GUI.Label(new Rect(w / 2 - 105, 0, 210, 43),
            client.leftScore + "  :  " + client.rightScore, scoreStyle);

        if (client.matchStatus == "PLAYING")
        {
            GUI.Label(new Rect(w / 2 - 260, 45, 520, 28),
                "Jogador " + (client.playerId + 1) + " - Equipe " +
                (client.playerId < 2 ? "AZUL" : "DOURADA") + " | W/S ou setas", textStyle);
            return;
        }

        float pw = Mathf.Min(480, w - 20);
        Rect panel = new Rect((w - pw) / 2, Mathf.Max(55, (h - 270) / 2), pw, 270);
        GUI.Box(panel, "");
        if (client.matchStatus == "FINISHED")
        {
            GUI.Label(new Rect(panel.x + 10, panel.y + 20, pw - 20, 40),
                "VITORIA DA EQUIPE " + (client.leftScore > client.rightScore ? "AZUL" : "DOURADA"), heading);
            GUI.Label(new Rect(panel.x + 10, panel.y + 75, pw - 20, 32),
                "Resultado: " + client.leftScore + " x " + client.rightScore, textStyle);
            if (GUI.Button(new Rect(panel.x + pw / 2 - 100, panel.y + 140, 200, 45),
                "JOGAR NOVAMENTE")) client.RequestRestart();
            return;
        }
        bool waiting = client.matchStatus == "WAITING";
        GUI.Label(new Rect(panel.x + 10, panel.y + 16, pw - 20, 40),
            waiting ? "SALA DE ESPERA" : "PONG 2 CONTRA 2", heading);
        GUI.Label(new Rect(panel.x + 10, panel.y + 65, pw - 20, 30),
            waiting ? "Jogadores: " + client.connectedPlayers + " / 4" :
                "Conecte-se ao servidor para jogar", textStyle);
        if (waiting)
            GUI.Label(new Rect(panel.x + 10, panel.y + 100, pw - 20, 28),
                "Voce e o jogador " + (client.playerId + 1) +
                " - equipe " + (client.playerId < 2 ? "AZUL" : "DOURADA"), textStyle);
        GUI.Label(new Rect(panel.x + 12, panel.y + 170, 70, 30), "IP:", textStyle);
        ip = GUI.TextField(new Rect(panel.x + 82, panel.y + 170, pw - 215, 30), ip);
        if (GUI.Button(new Rect(panel.x + pw - 125, panel.y + 170, 115, 30), "CONECTAR"))
            client.ConnectTo(ip.Trim());
        GUI.Label(new Rect(panel.x + 10, panel.y + 215, pw - 20, 28),
            "A partida comeca com quatro jogadores | UDP 7777", textStyle);
    }
}
