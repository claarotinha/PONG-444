#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// Ferramenta do Editor: cria objetos reais na Hierarchy, não via OnGUI.
public static class PongSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Pong/Montar Canvas")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject camera = new GameObject("Main Camera");
        camera.tag = "MainCamera";
        Camera cam = camera.AddComponent<Camera>();
        cam.orthographic = true;
        cam.backgroundColor = new Color(0.05f, 0.07f, 0.12f);
        camera.transform.position = new Vector3(0, 0, -10);

        GameObject network = new GameObject("Cliente UDP");
        network.AddComponent<PongUdpClient>();

        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1100, 700);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject events = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        events.AddComponent<StandaloneInputModule>();
#endif
        PongCanvasUI ui = new GameObject("Pong UI").AddComponent<PongCanvasUI>();
        ui.transform.SetParent(canvasObject.transform, false);
        ui.client = network.GetComponent<PongUdpClient>();

        Panel(canvasObject.transform, "Campo", new Vector2(820, 450), new Vector2(0, -25),
            new Color(0.08f, 0.12f, 0.18f));
        Panel(canvasObject.transform, "Equipe Azul", new Vector2(410, 450), new Vector2(-205, -25),
            new Color(0.08f, 0.21f, 0.32f));
        Panel(canvasObject.transform, "Equipe Dourada", new Vector2(410, 450), new Vector2(205, -25),
            new Color(0.29f, 0.20f, 0.10f));
        for (int n = -4; n <= 4; n++)
            Panel(canvasObject.transform, "Linha central " + n, new Vector2(4, 24),
                new Vector2(0, -25 + n * 50), Color.white);
        ui.ball = Panel(canvasObject.transform, "Bola", new Vector2(18, 18), new Vector2(0, 0), Color.white);
        ui.paddles = new RectTransform[4];
        for (int i = 0; i < 4; i++)
            ui.paddles[i] = Panel(canvasObject.transform, "Raquete " + (i + 1),
                new Vector2(14, 70), new Vector2(i < 2 ? -370 : 370, i % 2 == 0 ? 100 : -100),
                i < 2 ? new Color(0.1f, 0.8f, 1f) : new Color(1f, 0.75f, 0.2f));
        ui.scoreText = Label(canvasObject.transform, "Placar", "0 : 0", new Vector2(0, 310), 32);
        ui.roundText = Label(canvasObject.transform, "Rodada", "", new Vector2(0, 275), 20);
        ui.infoText = Label(canvasObject.transform, "Controles", "", new Vector2(0, -315), 19);

        ui.menuPanel = Overlay(canvasObject.transform, "Menu Inicial");
        Label(ui.menuPanel.transform, "Titulo", "PONG 2 CONTRA 2", new Vector2(0, 100), 30);
        Label(ui.menuPanel.transform, "IP Label", "IP do servidor", new Vector2(0, 45), 18);
        ui.ipInput = MakeInput(ui.menuPanel.transform, new Vector2(0, 0));
        ui.connectButton = MakeButton(ui.menuPanel.transform, "Conectar", new Vector2(0, -55));

        ui.waitingPanel = Overlay(canvasObject.transform, "Sala de Espera");
        Label(ui.waitingPanel.transform, "Titulo", "SALA DE ESPERA", new Vector2(0, 80), 30);
        ui.waitingText = Label(ui.waitingPanel.transform, "Jogadores", "Jogadores: 0/4", new Vector2(0, 10), 22);
        Label(ui.waitingPanel.transform, "Aviso", "A partida comeca com quatro jogadores", new Vector2(0, -55), 17);

        ui.victoryPanel = Overlay(canvasObject.transform, "Tela de Vitoria");
        ui.winnerText = Label(ui.victoryPanel.transform, "Vencedor", "Vitoria", new Vector2(0, 65), 27);
        ui.restartButton = MakeButton(ui.victoryPanel.transform, "Jogar novamente", new Vector2(0, -25));

        ui.waitingPanel.SetActive(false);
        ui.victoryPanel.SetActive(false);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Selection.activeGameObject = canvasObject;
        Debug.Log("Pong: Canvas montado na SampleScene.");
    }

    private static RectTransform Panel(Transform parent, string name, Vector2 size, Vector2 pos, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;
        obj.GetComponent<Image>().color = color;
        return rect;
    }

    private static GameObject Overlay(Transform parent, string name)
    {
        RectTransform rect = Panel(parent, name, new Vector2(490, 290), Vector2.zero,
            new Color(0.035f, 0.055f, 0.09f, 0.97f));
        return rect.gameObject;
    }

    private static Text Label(Transform parent, string name, string value, Vector2 pos, int size)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(480, 45);
        rect.anchoredPosition = pos;
        Text text = obj.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = value;
        return text;
    }

    private static Button MakeButton(Transform parent, string title, Vector2 pos)
    {
        RectTransform rect = Panel(parent, "Botao " + title, new Vector2(205, 42), pos,
            new Color(0.17f, 0.45f, 0.68f));
        Label(rect, "Texto", title, Vector2.zero, 18);
        return rect.GetComponent<Image>().gameObject.AddComponent<Button>();
    }

    private static InputField MakeInput(Transform parent, Vector2 pos)
    {
        RectTransform rect = Panel(parent, "Campo IP", new Vector2(260, 42), pos, Color.white);
        Text value = Label(rect, "Texto IP", "127.0.0.1", Vector2.zero, 19);
        value.color = Color.black;
        value.supportRichText = false;
        InputField field = rect.gameObject.AddComponent<InputField>();
        field.textComponent = value;
        field.text = "127.0.0.1";
        return field;
    }
}
#endif
