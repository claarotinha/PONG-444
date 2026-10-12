using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

// Servidor autoritativo. Execute SOMENTE no projeto PongServidor.
public class PongUdpServer : MonoBehaviour
{
    public int port = 7777;
    public float paddleSpeed = 6f, ballSpeed = 5f;
    public float fieldHalfWidth = 8f, fieldHalfHeight = 4.5f;
    public float paddleHalfHeight = 0.8f;
    public int winningScore = 5;

    private UdpClient udp;
    private readonly IPEndPoint[] peers = new IPEndPoint[4];
    private readonly float[] paddleY = new float[4];
    private readonly int[] directions = new int[4];
    private readonly float[] lastSeen = new float[4];
    private readonly int[] score = new int[2];
    private Vector2 ball, velocity;
    private bool running, finished;
    private float nextSnapshot, nextRound;
    private readonly CultureInfo ci = CultureInfo.InvariantCulture;
    private const float Tick = 1f / 30f;
    private float accumulator;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (FindFirstObjectByType<PongUdpServer>() == null)
            new GameObject("Pong UDP Server").AddComponent<PongUdpServer>();
    }

    private void Start()
    {
        try { udp = new UdpClient(port); udp.Client.Blocking = false; Debug.Log("SERVIDOR UDP iniciado na porta " + port); }
        catch (Exception ex) { Debug.LogError("Falha ao abrir porta UDP: " + ex.Message); enabled = false; }
        ResetBall(1);
    }

    private void Update()
    {
        if (udp == null) return;
        for (int n = 0; n < 128 && udp.Available > 0; n++)
        {
            try
            {
                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                string message = Encoding.UTF8.GetString(udp.Receive(ref remote));
                Receive(message, remote);
            }
            catch (SocketException) { break; }
            catch (Exception ex) { Debug.LogWarning("Pacote descartado: " + ex.Message); }
        }
        for (int i = 0; i < 4; i++)
            if (peers[i] != null && Time.realtimeSinceStartup - lastSeen[i] > 8f) Disconnect(i);
        accumulator = Mathf.Min(accumulator + Time.deltaTime, 0.25f);
        while (accumulator >= Tick) { Step(Tick); accumulator -= Tick; }
        if (Time.realtimeSinceStartup >= nextSnapshot)
        {
            BroadcastSnapshot();
            nextSnapshot = Time.realtimeSinceStartup + 0.05f;
        }
    }

    private int Find(IPEndPoint remote)
    {
        for (int i = 0; i < 4; i++) if (peers[i] != null && peers[i].Equals(remote)) return i;
        return -1;
    }

    private int Count()
    {
        int total = 0; foreach (var p in peers) if (p != null) total++;
        return total;
    }

    private void Receive(string message, IPEndPoint remote)
    {
        if (message.Length > 128) return;
        string[] parts = message.Trim().Split('|');
        int id = Find(remote);
        if (parts[0] == "JOIN")
        {
            if (id < 0)
            {
                if (running || finished) { Send(remote, "DENY|MATCH_STARTED"); return; }
                for (int i = 0; i < 4; i++) if (peers[i] == null) { id = i; peers[i] = remote; break; }
                if (id < 0) { Send(remote, "DENY|FULL"); return; }
                Debug.Log("Jogador " + (id + 1) + " entrou: " + remote);
            }
            lastSeen[id] = Time.realtimeSinceStartup;
            Send(remote, "WELCOME|" + id);
            if (Count() == 4 && !running) { running = true; nextRound = Time.realtimeSinceStartup + 1.5f; Debug.Log("Partida iniciada!"); }
            return;
        }
        if (id < 0) return;
        lastSeen[id] = Time.realtimeSinceStartup;
        if (parts[0] == "INPUT" && parts.Length == 2 && int.TryParse(parts[1], out int dir))
            directions[id] = Mathf.Clamp(dir, -1, 1);
        if (parts[0] == "LEAVE") Disconnect(id);
    }

    private void Disconnect(int id)
    {
        Debug.Log("Jogador " + (id + 1) + " desconectado; aguardando nova sala.");
        peers[id] = null; directions[id] = 0; paddleY[id] = 0f;
        running = false; finished = false; score[0] = score[1] = 0;
        ResetBall(1);
    }

    private void ResetBall(int direction)
    {
        ball = Vector2.zero;
        velocity = new Vector2(direction, UnityEngine.Random.Range(-0.5f, 0.5f)).normalized * ballSpeed;
        nextRound = Time.realtimeSinceStartup + 1f;
    }

    private void Step(float dt)
    {
        if (!running || finished) return;
        for (int i = 0; i < 4; i++)
            paddleY[i] = Mathf.Clamp(paddleY[i] + directions[i] * paddleSpeed * dt,
                -fieldHalfHeight + paddleHalfHeight, fieldHalfHeight - paddleHalfHeight);
        if (Time.realtimeSinceStartup < nextRound) return;
        Vector2 previous = ball;
        ball += velocity * dt;
        if (ball.y >= fieldHalfHeight - 0.2f) { ball.y = fieldHalfHeight - 0.2f; velocity.y = -Mathf.Abs(velocity.y); }
        if (ball.y <= -fieldHalfHeight + 0.2f) { ball.y = -fieldHalfHeight + 0.2f; velocity.y = Mathf.Abs(velocity.y); }
        // Duas raquetes por lado: jogadores 0/1 à esquerda, 2/3 à direita.
        bool left = velocity.x < 0 && previous.x > -fieldHalfWidth + 0.6f && ball.x <= -fieldHalfWidth + 0.6f;
        bool right = velocity.x > 0 && previous.x < fieldHalfWidth - 0.6f && ball.x >= fieldHalfWidth - 0.6f;
        if (left || right)
        {
            int start = left ? 0 : 2;
            for (int i = start; i < start + 2; i++)
                if (Mathf.Abs(ball.y - paddleY[i]) <= paddleHalfHeight + 0.2f)
                {
                    ball.x = left ? -fieldHalfWidth + 0.6f : fieldHalfWidth - 0.6f;
                    float offset = (ball.y - paddleY[i]) / paddleHalfHeight;
                    velocity = new Vector2(left ? 1f : -1f, offset * 0.7f).normalized * ballSpeed;
                    return;
                }
        }
        if (ball.x < -fieldHalfWidth || ball.x > fieldHalfWidth)
        {
            int winner = ball.x < 0 ? 1 : 0;
            score[winner]++;
            Debug.Log("Ponto! " + score[0] + " x " + score[1]);
            if (score[winner] >= winningScore) { finished = true; Broadcast("END|" + winner); }
            else ResetBall(winner == 0 ? -1 : 1);
        }
    }

    private string F(float value) { return value.ToString("0.000", ci); }

    private void BroadcastSnapshot()
    {
        string status = finished ? "FINISHED" : running ? "PLAYING" : "WAITING";
        string packet = "STATE|" + status + "|" + Count() + "|" + F(ball.x) + "|" + F(ball.y)
            + "|" + F(paddleY[0]) + "|" + F(paddleY[1]) + "|" + F(paddleY[2]) + "|" + F(paddleY[3])
            + "|" + score[0] + "|" + score[1];
        Broadcast(packet);
    }

    private void Broadcast(string message)
    {
        foreach (var peer in peers) if (peer != null) Send(peer, message);
    }

    private void Send(IPEndPoint remote, string message)
    {
        try { byte[] data = Encoding.UTF8.GetBytes(message); udp.Send(data, data.Length, remote); }
        catch (Exception ex) { Debug.LogWarning("Erro de envio UDP: " + ex.Message); }
    }

    private void OnDestroy() { udp?.Close(); }
}
