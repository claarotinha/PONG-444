using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PongUdpClient : MonoBehaviour
{
    public string serverIp = "127.0.0.1";
    public int serverPort = 7777;
    public int playerId = -1;
    public string matchStatus = "CONNECTING";
    public int connectedPlayers, leftScore, rightScore, roundNumber;
    public bool roundPaused;
    public Vector2 ballPosition;
    public float[] paddlePositions = new float[4];
    public bool useKeyboard = true;
    [Range(-1, 1)] public int simulatedDirection;

    private UdpClient udp;
    private IPEndPoint server;
    private string sessionToken;
    private float nextJoin, nextInput, lastPacket, nextDeniedLog;
    private int previousPlayers = -1;
    private string previousStatus = "";
    private readonly CultureInfo culture = CultureInfo.InvariantCulture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (FindFirstObjectByType<PongUdpClient>() == null)
            new GameObject("Pong UDP Client").AddComponent<PongUdpClient>();
    }

    private void Start()
    {
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
        sessionToken = Guid.NewGuid().ToString("N");
        try
        {
            server = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
            udp = new UdpClient(0);
            udp.Client.Blocking = false;
            Debug.Log("Conectando ao servidor UDP " + server);
        }
        catch (Exception e) { Debug.LogError("Conexão UDP: " + e.Message); enabled = false; }
    }

    private void Update()
    {
        if (udp == null) return;
        for (int n = 0; n < 128 && udp.Available > 0; n++)
        {
            try
            {
                IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                string packet = Encoding.UTF8.GetString(udp.Receive(ref from));
                if (from.Equals(server)) Receive(packet);
            }
            catch (Exception e) { Debug.LogWarning("Recepção UDP: " + e.Message); }
        }
        if (playerId >= 0 && Time.realtimeSinceStartup - lastPacket > 5f)
        {
            playerId = -1; matchStatus = "CONNECTING";
            Debug.LogWarning("Servidor sem resposta; reconectando.");
        }
        if (playerId < 0 && Time.realtimeSinceStartup >= nextJoin)
        {
            Send("JOIN|" + sessionToken);
            nextJoin = Time.realtimeSinceStartup + 1f;
        }
        if (playerId >= 0 && Time.realtimeSinceStartup >= nextInput)
        {
            Send("INPUT|" + Direction());
            nextInput = Time.realtimeSinceStartup + 1f / 30f;
        }
    }

    private int Direction()
    {
        if (!useKeyboard) return Mathf.Clamp(simulatedDirection, -1, 1);
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return 0;
        bool up = keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed;
        bool down = keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed;
        return (up ? 1 : 0) - (down ? 1 : 0);
#elif ENABLE_LEGACY_INPUT_MANAGER
        return (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) ? 1 : 0)
            - (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) ? 1 : 0);
#else
        return 0;
#endif
    }

    private void Receive(string packet)
    {
        if (packet.Length > 256) return;
        string[] p = packet.Split('|');
        lastPacket = Time.realtimeSinceStartup;
        if (p[0] == "WELCOME" && p.Length == 2 && int.TryParse(p[1], out int id) && id >= 0 && id < 4)
        {
            if (playerId != id) Debug.Log("Conectado como jogador " + (id + 1));
            playerId = id; return;
        }
        if (p[0] == "DENY")
        {
            if (Time.realtimeSinceStartup >= nextDeniedLog)
            {
                Debug.LogWarning("Entrada recusada: " + packet);
                nextDeniedLog = Time.realtimeSinceStartup + 5f;
            }
            return;
        }
        if (p[0] == "END" && p.Length == 2)
        {
            matchStatus = "FINISHED";
            Debug.Log("Equipe vencedora: " + (p[1] == "0" ? "esquerda" : "direita"));
            return;
        }
        if (p[0] != "STATE" || p.Length != 13) return;
        if (!int.TryParse(p[2], out int count) ||
            !float.TryParse(p[3], NumberStyles.Float, culture, out float bx) ||
            !float.TryParse(p[4], NumberStyles.Float, culture, out float by)) return;
        float[] positions = new float[4];
        for (int i = 0; i < 4; i++)
            if (!float.TryParse(p[5 + i], NumberStyles.Float, culture, out positions[i])) return;
        if (!int.TryParse(p[9], out int a) || !int.TryParse(p[10], out int b)) return;
        if (!int.TryParse(p[11], out int round)) return;
        roundNumber = round;
        roundPaused = p[12] == "PAUSE";
        matchStatus = p[1]; connectedPlayers = count;
        ballPosition = new Vector2(bx, by);
        Array.Copy(positions, paddlePositions, 4);
        leftScore = a; rightScore = b;
        if (count != previousPlayers || matchStatus != previousStatus)
        {
            Debug.Log("Estado: " + matchStatus + " | Jogadores: " + count + "/4 | Placar: " + a + " x " + b);
            previousPlayers = count; previousStatus = matchStatus;
        }
    }

    public void ConnectTo(string ip)
    {
        if (!IPAddress.TryParse(ip, out IPAddress address))
        {
            Debug.LogWarning("IP invalido: " + ip);
            return;
        }
        if (playerId >= 0) Send("LEAVE");
        serverIp = ip;
        server = new IPEndPoint(address, serverPort);
        sessionToken = Guid.NewGuid().ToString("N");
        playerId = -1;
        matchStatus = "CONNECTING";
        connectedPlayers = 0;
        leftScore = rightScore = roundNumber = 0;
        roundPaused = false;
        lastPacket = Time.realtimeSinceStartup;
        nextJoin = 0f;
        Debug.Log("Conectando a " + server);
    }

    public void RequestRestart() { if (playerId >= 0) Send("RESTART"); }

    private void Send(string packet)
    {
        if (udp == null || server == null) return;
        try { byte[] data = Encoding.UTF8.GetBytes(packet); udp.Send(data, data.Length, server); }
        catch (Exception e) { Debug.LogWarning("Envio UDP: " + e.Message); }
    }

    private void OnDestroy()
    {
        if (playerId >= 0) Send("LEAVE");
        if (udp != null) udp.Close();
    }
}
