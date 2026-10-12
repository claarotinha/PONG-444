using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

// Cliente UDP. Sem Canvas; acompanha estado no Console do Unity.
public class PongUdpClient : MonoBehaviour
{
    public string serverIp = "127.0.0.1";
    public int serverPort = 7777;
    public int playerId = -1;
    public string matchStatus = "CONNECTING";
    public int connectedPlayers, leftScore, rightScore;
    public Vector2 ballPosition;
    public readonly float[] paddlePositions = new float[4];

    private UdpClient udp;
    private IPEndPoint server;
    private float nextJoin, nextInput, lastPacket;
    private int previousPlayers = -1;
    private string previousStatus = "";
    private readonly CultureInfo ci = CultureInfo.InvariantCulture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (FindFirstObjectByType<PongUdpClient>() == null)
            new GameObject("Pong UDP Client").AddComponent<PongUdpClient>();
    }

    private void Start()
    {
        try
        {
            server = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
            udp = new UdpClient(0);
            udp.Client.Blocking = false;
            Debug.Log("CLIENTE UDP: tentando conectar a " + server);
        }
        catch (Exception ex) { Debug.LogError("Erro ao iniciar cliente: " + ex.Message); enabled = false; }
    }

    private void Update()
    {
        if (udp == null) return;
        for (int n = 0; n < 128 && udp.Available > 0; n++)
        {
            try
            {
                IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                string message = Encoding.UTF8.GetString(udp.Receive(ref from));
                if (from.Equals(server)) Receive(message);
            }
            catch (SocketException) { break; }
            catch (Exception ex) { Debug.LogWarning("Pacote UDP ignorado: " + ex.Message); }
        }
        if (Time.realtimeSinceStartup - lastPacket > 5f && playerId >= 0)
        {
            Debug.LogWarning("Conexão perdida; tentando reconectar.");
            playerId = -1; matchStatus = "CONNECTING";
        }
        if (playerId < 0 && Time.realtimeSinceStartup >= nextJoin)
        {
            Send("JOIN"); nextJoin = Time.realtimeSinceStartup + 1f;
        }
        if (playerId >= 0 && Time.realtimeSinceStartup >= nextInput)
        {
            // Setas ou W/S, conforme o sistema de entrada clássico.
            int direction = (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) ? 1 : 0)
                          - (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) ? 1 : 0);
            Send("INPUT|" + direction);
            nextInput = Time.realtimeSinceStartup + 0.05f;
        }
    }

    private void Receive(string message)
    {
        if (message.Length > 256) return;
        string[] p = message.Split('|');
        lastPacket = Time.realtimeSinceStartup;
        if (p[0] == "WELCOME" && p.Length == 2 && int.TryParse(p[1], out int id))
        {
            playerId = id; Debug.Log("Conectado! Você é o jogador " + (id + 1)); return;
        }
        if (p[0] == "DENY") { Debug.LogWarning("Servidor recusou conexão: " + message); return; }
        if (p[0] == "END" && p.Length == 2) { matchStatus = "FINISHED"; Debug.Log("Fim de partida! Equipe vencedora: " + (p[1] == "0" ? "esquerda" : "direita")); return; }
        if (p[0] != "STATE" || p.Length != 11) return;
        if (!int.TryParse(p[2], out int count) ||
            !float.TryParse(p[3], NumberStyles.Float, ci, out float bx) ||
            !float.TryParse(p[4], NumberStyles.Float, ci, out float by)) return;
        float[] y = new float[4];
        for (int i = 0; i < 4; i++)
            if (!float.TryParse(p[5 + i], NumberStyles.Float, ci, out y[i])) return;
        if (!int.TryParse(p[9], out int a) || !int.TryParse(p[10], out int b)) return;
        matchStatus = p[1]; connectedPlayers = count;
        ballPosition = new Vector2(bx, by);
        Array.Copy(y, paddlePositions, 4);
        leftScore = a; rightScore = b;
        if (count != previousPlayers || matchStatus != previousStatus)
        {
            Debug.Log("Estado: " + matchStatus + " | Jogadores: " + count + "/4 | Placar: " + a + " x " + b);
            previousPlayers = count; previousStatus = matchStatus;
        }
    }

    private void Send(string message)
    {
        if (udp == null || server == null) return;
        try { byte[] bytes = Encoding.UTF8.GetBytes(message); udp.Send(bytes, bytes.Length, server); }
        catch (Exception ex) { Debug.LogWarning("Falha ao enviar UDP: " + ex.Message); }
    }

    private void OnDestroy()
    {
        if (playerId >= 0) Send("LEAVE");
        udp?.Close();
    }
}
