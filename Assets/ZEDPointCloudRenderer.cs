using UnityEngine;
using sl;

public class ZEDPointCloudRenderer : MonoBehaviour
{
    public ZEDManager zedManager;
    [Range(0.001f, 0.05f)]
    public float pointSize = 0.003f;
    [Range(0.1f, 20f)]
    public float maxDepth = 3f;

    private bool isReady = false;
    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles;
    private ZEDCamera zed;
    private ZEDMat pointCloudMat;
    private int subW, subH;

    void Start()
    {
        ps = gameObject.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.maxParticles = 100000;
        main.startLifetime = float.PositiveInfinity;
        main.startSpeed = 0;
        main.startSize = pointSize;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = ps.emission;
        emission.enabled = false;
        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.material = new Material(Shader.Find("Particles/Standard Unlit"));
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        particles = new ParticleSystem.Particle[100000];
        zedManager.OnZEDReady += () => { OnZEDReady(); };
    }

    void OnZEDReady()
    {
        zed = zedManager.zedCamera;
        subW = zed.ImageWidth / 4;
        subH = zed.ImageHeight / 4;
        pointCloudMat = new ZEDMat();
        pointCloudMat.Create(
            new sl.Resolution(subW, subH),
            ZEDMat.MAT_TYPE.MAT_32F_C4,
            ZEDMat.MEM.MEM_CPU);
        isReady = true;
    }

    void Update()
    {
        if (!isReady || zed == null) return;

        zed.RetrieveMeasure(pointCloudMat, MEASURE.XYZRGBA,
            ZEDMat.MEM.MEM_CPU,
            new sl.Resolution(subW, subH));

        int count = 0;
        for (int y = 0; y < subH && count < particles.Length; y++)
        {
            for (int x = 0; x < subW && count < particles.Length; x++)
            {
                float4 pt;
                pointCloudMat.GetValue(x, y, out pt, ZEDMat.MEM.MEM_CPU);

                if (float.IsNaN(pt.r) || float.IsInfinity(pt.r)) continue;
                if (pt.b > maxDepth || pt.b < 0.15f) continue;

                uint packed = System.BitConverter.ToUInt32(
                    System.BitConverter.GetBytes(pt.a), 0);
                float red = (packed & 0xFF) / 255f;
                float green = ((packed >> 8) & 0xFF) / 255f;
                float blue = ((packed>>16) & 0xFF) / 255f;
                particles[count].position = new Vector3(pt.r, pt.g, pt.b);

                particles[count].startColor = new Color(red, green, blue, 1f);
                particles[count].startSize = pointSize;
                particles[count].remainingLifetime = float.PositiveInfinity;
                particles[count].startLifetime = float.PositiveInfinity;
                count++;
            }
        }
        ps.SetParticles(particles, count);
    }

    void OnDestroy()
    {
        pointCloudMat?.Free(ZEDMat.MEM.MEM_CPU);
    }
}