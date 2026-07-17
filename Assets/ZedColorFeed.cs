using UnityEngine;
using System;
using System.Net.Sockets;
using System.Threading;

/// <summary>
/// Receives the color video stream from the ZED server over a persistent
/// TCP socket and displays it on a Unity Renderer (attach this to a Quad
/// or any mesh with a material).
///
/// WIRE FORMAT (matches the video TCP port in zed_server.py):
///   4 bytes  uint32 (little-endian) - JPEG byte length
///   N bytes  - raw JPEG bytes
///
/// In the Inspector:
///   - Server IP: IP address of the Ubuntu server (e.g. 192.168.1.130)
///   - Server Port: 5003 (video TCP port, see zed_server.py)
///   - Target Renderer: drag the GameObject's Renderer here
/// </summary>
public class ZedColorFeed : MonoBehaviour
{
    [Header("Server")]
    public string serverIp = "192.168.1.130";
    public int serverPort = 5003;

    [Header("Display")]
    [Tooltip("Renderer to display the camera feed on. Can be a Quad, plane, etc.")]
    public Renderer targetRenderer;

    [Tooltip("Material property to update with the texture. Default is _MainTex.")]
    public string textureProperty = "_MainTex";

    [Header("Debug")]
    public bool showDebugInfo = true;

    // -------------------------------------------------------------------------

    private Texture2D texture;
    private Thread networkThread;
    private volatile bool running = false;

    // The network thread hands off completed JPEG frames here. Unity API
    // calls (like Texture2D.LoadImage) can only happen on the main thread,
    // so Update() picks this up and does the actual decode.
    private readonly object frameLock = new object();
    private byte[] pendingJpeg = null;
    private bool hasNewFrame = false;

    void Start()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        // Size doesn't matter here, LoadImage resizes the texture to match
        // whatever JPEG dimensions actually arrive.
        texture = new Texture2D(2, 2, TextureFormat.RGB24, false);

        if (targetRenderer != null)
            targetRenderer.material.SetTexture(textureProperty, texture);

        running = true;
        networkThread = new Thread(NetworkLoop);
        networkThread.IsBackground = true;
        networkThread.Start();
    }

    void OnDestroy()
    {
        running = false;
        networkThread?.Join(500);
    }

    void Update()
    {
        byte[] jpeg = null;
        lock (frameLock)
        {
            if (hasNewFrame)
            {
                jpeg = pendingJpeg;
                hasNewFrame = false;
            }
        }

        if (jpeg != null && texture.LoadImage(jpeg))
        {
            if (targetRenderer != null)
                targetRenderer.material.SetTexture(textureProperty, texture);
        }
    }

    // -------------------------------------------------------------------------
    // Background thread: persistent TCP connection, blocking reads.
    // Reconnects automatically if the connection drops.
    // -------------------------------------------------------------------------

    void NetworkLoop()
    {
        byte[] lengthBuf = new byte[4];

        while (running)
        {
            TcpClient client = null;
            try
            {
                client = new TcpClient();
                client.NoDelay = true;
                client.ReceiveTimeout = 5000;
                client.Connect(serverIp, serverPort);

                if (showDebugInfo)
                    Debug.Log($"ZedColorFeed: Connected to {serverIp}:{serverPort}");

                using NetworkStream stream = client.GetStream();

                while (running)
                {
                    if (!ReadExactly(stream, lengthBuf, 4)) break;
                    uint frameLen = BitConverter.ToUInt32(lengthBuf, 0);

                    // Sanity cap to avoid trying to allocate a huge buffer if
                    // the stream ever gets out of sync.
                    if (frameLen == 0 || frameLen > 50_000_000)
                    {
                        Debug.LogWarning($"ZedColorFeed: Rejected implausible frame length {frameLen}");
                        break;
                    }

                    byte[] jpegBytes = new byte[frameLen];
                    if (!ReadExactly(stream, jpegBytes, (int)frameLen)) break;

                    lock (frameLock)
                    {
                        pendingJpeg = jpegBytes;
                        hasNewFrame = true;
                    }
                }
            }
            catch (Exception e)
            {
                if (running)
                    Debug.LogWarning($"ZedColorFeed: Connection error: {e.Message}. Retrying in 2s...");
            }
            finally
            {
                client?.Close();
            }

            if (running)
                Thread.Sleep(2000);
        }
    }

    /// <summary>
    /// Blocks until exactly `count` bytes are read into `buffer`, since a
    /// single NetworkStream.Read call can return fewer bytes than requested
    /// even mid-frame. Returns false if the connection closed early.
    /// </summary>
    bool ReadExactly(NetworkStream stream, byte[] buffer, int count)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = stream.Read(buffer, offset, count - offset);
            if (read <= 0) return false; // connection closed
            offset += read;
        }
        return true;
    }
}