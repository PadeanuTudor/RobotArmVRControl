using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Net.Sockets;
using System.Threading;

/// <summary>
/// Receives binary point cloud data from the ZED server over a persistent
/// TCP socket and renders it using a GPU compute buffer - handles 250k+
/// points at VR framerates.
///
/// SETUP IN UNITY:
///   1. Create an empty GameObject, attach this script
///   2. Assign the PointCloudMaterial (create a new Material using the
///      PointCloudShader.shader included with this package, with Cull Off
///      set in its Pass block)
///   3. Set Server IP / Server Port (5004, point cloud TCP port) to match
///      your Ubuntu server
///
/// WIRE FORMAT (matches the point cloud TCP port in zed_server.py):
///   4 bytes  uint32 (little-endian) - payload byte length
///   payload:
///     4 bytes  uint32 - N, number of valid points
///     N * 16 bytes - N points, each:
///       float32 x (mm)
///       float32 y (mm)
///       float32 z (mm)
///       uint8   r
///       uint8   g
///       uint8   b
///       uint8   pad
///
/// PERFORMANCE NOTE:
///   The live point count fluctuates every frame (real-world depth data
///   never returns exactly the same N twice). The GPU ComputeBuffer and
///   the CPU-side parse array are therefore allocated ONCE at a fixed
///   maxPointCapacity and reused every frame via partial SetData calls,
///   rather than being released and recreated whenever N changes. Doing
///   that recreation every frame is expensive (GPU resource allocation
///   plus driver synchronization) and was the main source of VR frame
///   stutter before this fix.
/// </summary>
public class ZedPointCloud : MonoBehaviour
{
    [Header("Server")]
    public string serverIp = "192.168.1.130";
    public int serverPort = 5004;

    [Header("Rendering")]
    [Tooltip("Material using PointCloudShader.shader (with Cull Off set)")]
    public Material pointCloudMaterial;

    [Tooltip("Size of each point in world units. 0.005 = 5mm - good starting point.")]
    public float pointSize = 0.005f;

    [Tooltip("Scale factor to convert mm to Unity units. 0.001 = 1mm -> 0.001 Unity units (metres).")]
    public float mmToUnits = 0.001f;

    [Tooltip("Maximum number of points the GPU buffer can hold. Allocated once at Start " +
             "and reused every frame, so set this comfortably above your expected peak point " +
             "count (check the 'points uploaded to GPU' log to see your typical N). Frames " +
             "with more points than this are truncated to this cap.")]
    public int maxPointCapacity = 300000;

    [Header("Camera")]
    [Tooltip("ZED depth confidence threshold, 1 to 100. 100 = no filtering (every depth " +
             "pixel kept, noisier). Lower values reject low-confidence depth, giving a " +
             "cleaner but sparser cloud. Sent live to the server whenever changed, no " +
             "restart needed, takes effect within one frame.")]
    [Range(1, 100)]
    public int confidenceThreshold = 50;

    [Tooltip("Port for the server's HTTP config/debug endpoints (see HTTP_PORT in zed_server.py). " +
             "Separate from Server Port above, which is the point cloud TCP stream.")]
    public int httpConfigPort = 5002;

    private int lastSentConfidence = int.MinValue;
    private float lastConfidenceSendTime = -999f;

    [Header("Debug")]
    public bool showDebugInfo = true;

    // -------------------------------------------------------------------------

    private ComputeBuffer pointBuffer;
    private int pointCount = 0;
    private Thread networkThread;
    private volatile bool running = false;

    // Double buffering: the network thread parses incoming frames directly
    // into one of these two arrays (off the main thread, since parsing is
    // pure byte math with no Unity API calls), then flips a pointer under a
    // short lock. Update() only ever takes that pointer and does a fast
    // SetData, it never parses. This removes the point-parsing cost from
    // the main thread entirely, which both smooths out frame time and
    // reduces the delay between a frame arriving and it being visible.
    private PointData[] bufferA;
    private PointData[] bufferB;
    private int writeIndex = 0; // which buffer the network thread is currently filling

    private readonly object swapLock = new object();
    private int readyBufferIndex = -1; // -1 = nothing ready yet
    private int readyCount = 0;
    private bool newDataAvailable = false;

    // Struct matching the binary layout: 3 floats + 4 bytes = 16 bytes
    struct PointData
    {
        public float x, y, z;
        public float color; // packed R,G,B,pad as float (matches shader)
    }

    void Start()
    {
        if (pointCloudMaterial == null)
        {
            Debug.LogError("ZedPointCloud: pointCloudMaterial is not assigned. " +
                           "Create a material using PointCloudShader.shader.");
            return;
        }

        // Allocate once, up front, at the fixed capacity. These are reused
        // for the lifetime of the component, no per-frame allocation.
        pointBuffer = new ComputeBuffer(maxPointCapacity, 16);
        bufferA = new PointData[maxPointCapacity];
        bufferB = new PointData[maxPointCapacity];

        // Unity queues a couple of GPU frames ahead by default to smooth out
        // frame pacing, which adds direct latency for a real-time display
        // like this one. Reducing it trades a small stutter risk (if frame
        // time varies a lot) for lower input-to-photon delay. Try 0 first;
        // bump to 1 if you see new stutter that wasn't there before.
        QualitySettings.maxQueuedFrames = 1;

        running = true;
        networkThread = new Thread(NetworkLoop);
        networkThread.IsBackground = true;
        networkThread.Start();
        Debug.Log($"ZedPointCloud: Connecting to {serverIp}:{serverPort}");

        // Push the Inspector's starting confidence value to the server so
        // both sides agree on it from the first frame.
        StartCoroutine(SendConfidenceThreshold(confidenceThreshold));
        lastSentConfidence = confidenceThreshold;
        lastConfidenceSendTime = Time.time;
    }

    void OnDestroy()
    {
        running = false;
        networkThread?.Join(500);
        pointBuffer?.Release();
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
                    Debug.Log($"ZedPointCloud: Connected to {serverIp}:{serverPort}");

                using NetworkStream stream = client.GetStream();

                while (running)
                {
                    if (!ReadExactly(stream, lengthBuf, 4)) break;
                    uint frameLen = BitConverter.ToUInt32(lengthBuf, 0);

                    // Sanity cap to avoid trying to allocate a huge buffer if
                    // the stream ever gets out of sync.
                    if (frameLen == 0 || frameLen > 100_000_000)
                    {
                        Debug.LogWarning($"ZedPointCloud: Rejected implausible frame length {frameLen}");
                        break;
                    }

                    byte[] frameBytes = new byte[frameLen];
                    if (!ReadExactly(stream, frameBytes, (int)frameLen)) break;

                    ParseIntoBuffer(frameBytes);
                }
            }
            catch (Exception e)
            {
                if (running)
                    Debug.LogWarning($"ZedPointCloud: Connection error: {e.Message}. Retrying in 2s...");
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
            if (read <= 0) return false;
            offset += read;
        }
        return true;
    }

    // -------------------------------------------------------------------------
    // Background thread: parse raw frame bytes directly into whichever
    // buffer isn't currently claimed by the main thread, then flip the
    // ready pointer. This is where the per-point byte unpacking actually
    // happens now, off the main thread, since it's pure math with no
    // Unity API calls.
    // -------------------------------------------------------------------------

    void ParseIntoBuffer(byte[] bytes)
    {
        if (bytes.Length < 4) return;

        uint rawN = BitConverter.ToUInt32(bytes, 0);
        if (rawN == 0) return;

        int expectedBytes = 4 + (int)rawN * 16;
        if (bytes.Length < expectedBytes) return;

        int count = Mathf.Min((int)rawN, maxPointCapacity);
        if (showDebugInfo && rawN > maxPointCapacity && Time.frameCount % 60 == 0)
            Debug.LogWarning($"ZedPointCloud: Incoming frame has {rawN:N0} points, " +
                              $"truncating to maxPointCapacity ({maxPointCapacity:N0}). " +
                              "Raise Max Point Capacity in the Inspector if this happens often.");

        PointData[] target = (writeIndex == 0) ? bufferA : bufferB;

        int offset = 4; // skip the uint32 count header
        for (int i = 0; i < count; i++)
        {
            target[i].x = BitConverter.ToSingle(bytes, offset); offset += 4;
            target[i].y = BitConverter.ToSingle(bytes, offset); offset += 4;
            target[i].z = BitConverter.ToSingle(bytes, offset); offset += 4;
            byte r = bytes[offset];
            byte g = bytes[offset + 1];
            byte b = bytes[offset + 2];
            byte[] colorBytes = { r, g, b, 255 }; // BGRA for Unity
            target[i].color = BitConverter.ToSingle(colorBytes, 0);
            offset += 4;
        }

        lock (swapLock)
        {
            readyBufferIndex = writeIndex;
            readyCount = count;
            newDataAvailable = true;
        }

        // Next frame parses into the other buffer, so we never write into
        // the one currently flagged ready for the main thread to read.
        writeIndex = 1 - writeIndex;
    }

    // -------------------------------------------------------------------------
    // Main thread: just grab whichever buffer is ready and upload it.
    // No parsing happens here anymore, this should be a very cheap call.
    // -------------------------------------------------------------------------

    void Update()
    {
        int bufIndex;
        int count;
        bool hasNew;

        lock (swapLock)
        {
            hasNew = newDataAvailable;
            bufIndex = readyBufferIndex;
            count = readyCount;
            newDataAvailable = false;
        }

        if (!hasNew || bufIndex < 0) return;

        PointData[] ready = (bufIndex == 0) ? bufferA : bufferB;

        // Partial update: only the first `count` entries of the fixed-size
        // buffer are touched. No Release/recreate here, this is the fix for
        // the per-frame GPU buffer churn that was causing the VR stutter.
        pointBuffer.SetData(ready, 0, 0, count);
        pointCount = count;

        if (showDebugInfo && Time.frameCount % 60 == 0)
            Debug.Log($"ZedPointCloud: {pointCount:N0} points uploaded to GPU");

        CheckConfidenceThresholdChange();
    }

    // -------------------------------------------------------------------------
    // Camera control: push the confidence threshold to the server whenever
    // the Inspector slider changes. Throttled so dragging the slider doesn't
    // fire a request every frame.
    // -------------------------------------------------------------------------

    void CheckConfidenceThresholdChange()
    {
        if (confidenceThreshold == lastSentConfidence) return;
        if (Time.time - lastConfidenceSendTime < 0.2f) return; // max 5 requests/sec while dragging

        lastSentConfidence = confidenceThreshold;
        lastConfidenceSendTime = Time.time;
        StartCoroutine(SendConfidenceThreshold(confidenceThreshold));
    }

    IEnumerator SendConfidenceThreshold(int value)
    {
        string url = $"http://{serverIp}:{httpConfigPort}/set_confidence?value={value}";

        using UnityWebRequest req = UnityWebRequest.Get(url);
        req.timeout = 3; // single-shot HTTP request, safe to wait for completion
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"ZedPointCloud: Failed to set confidence threshold to {value}: {req.error}");
        }
        else if (showDebugInfo)
        {
            Debug.Log($"ZedPointCloud: Confidence threshold set to {value}");
        }
    }

    // -------------------------------------------------------------------------
    // Render: draw point cloud every frame using the GPU compute buffer
    // -------------------------------------------------------------------------

    void OnRenderObject()
    {
        if (pointBuffer == null || pointCount == 0 || pointCloudMaterial == null)
            return;

        pointCloudMaterial.SetBuffer("_PointBuffer", pointBuffer);
        pointCloudMaterial.SetFloat("_PointSize", pointSize);
        pointCloudMaterial.SetFloat("_MmToUnits", mmToUnits);
        pointCloudMaterial.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);
        pointCloudMaterial.SetPass(0);

        Graphics.DrawProceduralNow(MeshTopology.Points, pointCount);
    }
}