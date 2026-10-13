using UnityEngine;
using UnityEngine.UI;

// Liga os elementos do Canvas aos dados recebidos por UDP.
public class PongCanvasUI : MonoBehaviour
{
    public PongUdpClient client;
    public RectTransform ball;
    public RectTransform[] paddles = new RectTransform[4];
    public Text scoreText, roundText, infoText, winnerText, waitingText;
    public GameObject menuPanel, waitingPanel, victoryPanel;
    public InputField ipInput;
    public Button connectButton, restartButton;
    private readonly float[] displayedY = { 2f, -2f, 2f, -2f };
    private Vector2 displayedBall;
    private bool initialized;

    private void Start()
    {
        if (client == null) client = FindFirstObjectByType<PongUdpClient>();
        if (client != null && ipInput != null) ipInput.text = client.serverIp;
        if (connectButton != null) connectButton.onClick.AddListener(Connect);
        if (restartButton != null) restartButton.onClick.AddListener(Restart);
    }

    private void Connect()
    {
        if (client != null && ipInput != null) client.ConnectTo(ipInput.text.Trim());
    }

    private void Restart()
    {
        if (client != null) client.RequestRestart();
    }

    private void Update()
    {
        if (client == null) { client = FindFirstObjectByType<PongUdpClient>(); return; }
        bool playing = client.matchStatus == "PLAYING";
        bool waiting = client.matchStatus == "WAITING";
        bool finished = client.matchStatus == "FINISHED";
        if (menuPanel != null) menuPanel.SetActive(!playing && !waiting && !finished);
        if (waitingPanel != null) waitingPanel.SetActive(waiting);
        if (victoryPanel != null) victoryPanel.SetActive(finished);
        if (scoreText != null) scoreText.text = client.leftScore + "  :  " + client.rightScore;
        if (roundText != null) roundText.text = playing
            ? "Rodada " + client.roundNumber + (client.roundPaused ? " - Prepare-se" : "") : "";
        if (infoText != null) infoText.text = playing
            ? "Jogador " + (client.playerId + 1) + "  |  W/S ou setas" : "";
        if (waitingText != null) waitingText.text = "Jogadores: " + client.connectedPlayers + "/4";
        if (winnerText != null) winnerText.text = "Vitoria da equipe " +
            (client.leftScore > client.rightScore ? "Azul" : "Dourada");
        if (client.playerId < 0) initialized = false;
        if (!initialized && client.playerId >= 0)
        {
            displayedBall = client.ballPosition;
            for (int i = 0; i < 4; i++) displayedY[i] = client.paddlePositions[i];
            initialized = true;
        }
        float t = 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime);
        displayedBall = Vector2.Lerp(displayedBall, client.ballPosition, t);
        if (ball != null) ball.anchoredPosition = displayedBall * 50f;
        for (int i = 0; i < 4 && i < paddles.Length; i++)
        {
            if (paddles[i] == null) continue;
            float target = initialized ? client.paddlePositions[i] : (i % 2 == 0 ? 2f : -2f);
            displayedY[i] = Mathf.Lerp(displayedY[i], target, t);
            paddles[i].anchoredPosition = new Vector2(i < 2 ? -370 : 370, displayedY[i] * 50f);
        }
    }
}
