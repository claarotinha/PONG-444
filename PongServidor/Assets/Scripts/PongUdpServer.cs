using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class PongUdpServer : MonoBehaviour
{
    public int port = 7777;
    public float fieldHalfWidth = 8f, fieldHalfHeight = 4.5f;
    public float paddleSpeed = 6f, paddleHalfHeight = 0.7f, ballSpeed = 5f;
    public int winningScore = 5;
    public float disconnectSeconds = 8f;
    public bool allowLocalTest = true;

    private UdpClient udp;
    private readonly IPEndPoint[] peers = new IPEndPoint[4];
    private readonly float[] y = new float[4], seen = new float[4];
    private readonly int[] input = new int[4], scores = new int[2];
    private readonly string[] tokens = new string[4];
    private Vector2 ball, velocity;
    private bool playing, finished;
    private float nextRound, nextBroadcast, accumulator;
    private readonly CultureInfo culture = CultureInfo.InvariantCulture;
    private const float StepSeconds = 1f / 60f;
    private int roundNumber;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (FindFirstObjectByType<PongUdpServer>() == null)
            new GameObject("Pong UDP Server").AddComponent<PongUdpServer>();
    }

    private void Start()
    {
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
        try
        {
            udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            udp.Client.Blocking = false;
            Debug.Log("Servidor UDP ativo em 0.0.0.0:" + port);
        }
        catch (Exception e) { Debug.LogError(e.Message); enabled = false; return; }
        ResetMatch();
    }

    private void Update()
    {
        if (udp == null) return;
        for (int n = 0; n < 128 && udp.Available > 0; n++)
        {
            try
            {
                IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                Handle(Encoding.UTF8.GetString(udp.Receive(ref from)), from);
            }
            catch (Exception e) { Debug.LogWarning("Pacote ignorado: " + e.Message); }
        }
        for (int i = 0; i < 4; i++)
            if (peers[i] != null && Time.realtimeSinceStartup - seen[i] > disconnectSeconds)
                Remove(i);
        accumulator = Mathf.Min(accumulator + Time.deltaTime, 0.25f);
        while (accumulator >= StepSeconds) { Simulate(StepSeconds); accumulator -= StepSeconds; }
        if (Time.realtimeSinceStartup >= nextBroadcast)
        {
            Broadcast(State());
            nextBroadcast = Time.realtimeSinceStartup + 1f / 30f;
        }
    }

    private int Find(IPEndPoint endpoint)
    {
        for (int i = 0; i < 4; i++) if (peers[i] != null && peers[i].Equals(endpoint)) return i;
        return -1;
    }

    private int Count()
    {
        int count = 0;
        foreach (IPEndPoint p in peers) if (p != null) count++;
        return count;
    }

    private void Handle(string packet, IPEndPoint from)
    {
        if (packet.Length > 256) return;
        string[] parts = packet.Trim().Split('|');
        int id = Find(from);
        if (parts[0] == "JOIN")
        {
            if (parts.Length != 2 || parts[1].Length < 8 || parts[1].Length > 64) return;
            string token = parts[1];
            // Um cliente pode reconectar em outra porta sem ocupar um quinto slot.
            if (id < 0)
                for (int i = 0; i < 4; i++)
                    if (peers[i] != null && tokens[i] == token) { id = i; peers[i] = from; break; }
            if (id < 0)
            {
                if (playing || finished) { Send(from, "DENY|IN_PROGRESS"); return; }
                for (int i = 0; i < 4; i++)
                    if (peers[i] == null) { id = i; peers[i] = from; tokens[i] = token; break; }
            }
            if (id < 0) { Send(from, "DENY|FULL"); return; }
            seen[id] = Time.realtimeSinceStartup;
            Send(from, "WELCOME|" + id);
            if (!playing && !finished && Count() == 4) StartMatch();
            return;
        }
        if (id < 0) return;
        seen[id] = Time.realtimeSinceStartup;
        if (parts[0] == "INPUT" && parts.Length == 2 && int.TryParse(parts[1], out int dir))
            input[id] = Mathf.Clamp(dir, -1, 1);
        if (parts[0] == "RESTART" && finished) { ResetMatch(); if (Count() == 4) StartMatch(); }
        if (parts[0] == "LEAVE") Remove(id);
    }

    private void Remove(int id)
    {
        peers[id] = null; tokens[id] = null; input[id] = 0;
        Debug.Log("Jogador " + (id + 1) + " saiu. Sala reiniciada.");
        ResetMatch();
        if (Count() == 4) StartMatch();
    }

    private void ResetMatch()
    {
        playing = false; finished = false;
        roundNumber = 0;
        scores[0] = scores[1] = 0;
        for (int i = 0; i < 4; i++) { input[i] = 0; y[i] = i % 2 == 0 ? 2f : -2f; }
        ResetBall(1);
    }

    private void StartMatch()
    {
        playing = true; finished = false;
        roundNumber = 1;
        nextRound = Time.realtimeSinceStartup + 1.5f;
        Debug.Log("Partida iniciada: 4 jogadores.");
    }

    private void ResetBall(int direction)
    {
        ball = Vector2.zero;
        velocity = new Vector2(direction, UnityEngine.Random.Range(-0.5f, 0.5f)).normalized * ballSpeed;
        nextRound = Time.realtimeSinceStartup + 1f;
    }

    private void Simulate(float dt)
    {
        if (!playing || finished) return;
        for (int i = 0; i < 4; i++)
        {
            bool upper = i % 2 == 0;
            float min = upper ? paddleHalfHeight : -fieldHalfHeight + paddleHalfHeight;
            float max = upper ? fieldHalfHeight - paddleHalfHeight : -paddleHalfHeight;
            y[i] = Mathf.Clamp(y[i] + input[i] * paddleSpeed * dt, min, max);
        }
        if (Time.realtimeSinceStartup < nextRound) return;
        ball += velocity * dt;
        float ceiling = fieldHalfHeight - 0.2f;
        if (ball.y >= ceiling) { ball.y = ceiling; velocity.y = -Mathf.Abs(velocity.y); }
        if (ball.y <= -ceiling) { ball.y = -ceiling; velocity.y = Mathf.Abs(velocity.y); }
        bool left = velocity.x < 0 && ball.x <= -fieldHalfWidth + 0.6f;
        bool right = velocity.x > 0 && ball.x >= fieldHalfWidth - 0.6f;
        if (left || right)
        {
            int start = left ? 0 : 2;
            for (int i = start; i < start + 2; i++)
            {
                if (Mathf.Abs(ball.y - y[i]) > paddleHalfHeight + 0.2f) continue;
                ball.x = left ? -fieldHalfWidth + 0.6f : fieldHalfWidth - 0.6f;
                velocity = new Vector2(left ? 1f : -1f, (ball.y - y[i]) / paddleHalfHeight * 0.7f).normalized * ballSpeed;
                return;
            }
        }
        if (ball.x < -fieldHalfWidth || ball.x > fieldHalfWidth)
        {
            int winner = ball.x < 0 ? 1 : 0;
            scores[winner]++;
            Debug.Log("Placar: " + scores[0] + " x " + scores[1]);
            if (scores[winner] >= winningScore)
            {
                playing = false; finished = true;
                Broadcast("END|" + winner);
            }
            else { roundNumber++; ResetBall(winner == 0 ? -1 : 1); }
        }
    }

    private string F(float n) { return n.ToString("0.000", culture); }
    private string State()
    {
        return "STATE|" + (finished ? "FINISHED" : playing ? "PLAYING" : "WAITING") + "|" + Count()
            + "|" + F(ball.x) + "|" + F(ball.y)
            + "|" + F(y[0]) + "|" + F(y[1]) + "|" + F(y[2]) + "|" + F(y[3])
            + "|" + scores[0] + "|" + scores[1] + "|" + roundNumber + "|" + (playing && Time.realtimeSinceStartup < nextRound ? "PAUSE" : "LIVE");
    }
    private void Broadcast(string packet)
    {
        foreach (IPEndPoint p in peers) if (p != null) Send(p, packet);
    }
    private void Send(IPEndPoint peer, string packet)
    {
        try { byte[] data = Encoding.UTF8.GetBytes(packet); udp.Send(data, data.Length, peer); }
        catch (Exception e) { Debug.LogWarning("Falha UDP: " + e.Message); }
    }
    private void OnDestroy() { if (udp != null) udp.Close(); }
}
