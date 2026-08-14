using UnityEngine;

/// <summary>
/// 노이즈맵 바닥 타일을 깐다 (블렌더에서 seamless로 베이크한 텍스처).
/// 타일을 여러 장 깔지 않고 **쿼드 1장 + UV 타일링**으로 처리한다 —
/// 드로우콜을 1로 유지해야 W1 측정에서 바닥이 변수가 되지 않는다.
/// </summary>
public static class GroundBuilder
{
    /// <summary>
    /// 바닥을 깐다. `groundTex`로 바이옴 텍스처를 지정할 수 있다 —
    /// 던전 노드에서 필드 잔디가 깔리면 "던전에 들어왔다"가 화면에서 부정된다.
    /// </summary>
    public static void Build(SpriteBank bank, float radius, string groundTex = "ground/field_plain_albedo")
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Ground";
        Object.Destroy(go.GetComponent<Collider>());

        // 쿼터뷰: 월드 세로를 ISO_Y로 눌러 그리므로 바닥도 같은 비율로 눌러야
        // 스프라이트와 원근이 맞는다 (§17 2D 쿼터뷰)
        float size = radius * 3f;
        go.transform.position = new Vector3(0, 0, 1f);   // 스프라이트보다 뒤
        go.transform.localScale = new Vector3(size, size * StressTest.ISO_Y, 1f);

        var tex = Resources.Load<Texture2D>(groundTex) ?? Resources.Load<Texture2D>("ground/field_plain_albedo");
        var sh = Shader.Find("Unlit/Texture") ?? Shader.Find("Sprites/Default");
        var mat = new Material(sh);

        if (tex != null)
        {
            tex.wrapMode = TextureWrapMode.Repeat;
            mat.mainTexture = tex;
            mat.mainTextureScale = new Vector2(size / 5f, size / 5f);  // 5m마다 반복 — 눌린 세로에 맞춰 무늬가 늘어지지 않게
        }
        else
        {
            mat.color = new Color(0.17f, 0.22f, 0.16f);
            Debug.LogWarning("[Ground] 노이즈맵 텍스처 없음 — 단색 대체");
        }

        go.GetComponent<MeshRenderer>().sharedMaterial = mat;

        BuildIsoGrid(size, radius);
    }

    /// <summary>
    /// 아이소메트릭 격자를 바닥 위에 얹는다.
    ///
    /// 왜 필요한가 (2026-08-13 오너 지적 "바닥 너무 평면이니까 탑뷰처럼 보이자너"):
    ///   노이즈 텍스처만 깔면 **방향 단서가 없어** 위에서 내려다본 평면으로 읽힌다.
    ///   쿼터뷰라는 인상은 원근보다 **바닥의 마름모 격자**에서 온다.
    ///   그래서 45° 격자를 그린 텍스처를 얹고, 쿼드가 ISO_Y로 눌리면서
    ///   화면에서는 가로로 넓은 마름모(= 아이소메트릭 타일)가 된다.
    /// </summary>
    static void BuildIsoGrid(float size, float radius)
    {
        const int N = 256;          // 격자 텍스처 한 변
        const int CELL = 64;        // 타일 간격(px)
        const int W = 2;            // 선 두께(px)

        var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
        var px = new Color32[N * N];
        var clear = new Color32(0, 0, 0, 0);
        var line = new Color32(255, 255, 255, 26);      // 아주 옅게 — 바닥 무늬를 덮으면 안 된다
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                // 45° 두 방향. (x+y)와 (x-y)가 각각 한 축이 된다.
                bool a = ((x + y) % CELL) < W;
                bool b = (((x - y) % CELL + CELL) % CELL) < W;
                px[y * N + x] = (a || b) ? line : clear;
            }
        t.SetPixels32(px);
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Bilinear;
        t.Apply();

        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        g.name = "GroundIsoGrid";
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.position = new Vector3(0, 0, 0.98f);      // 바닥(z=1)보다 아주 살짝 앞
        g.transform.localScale = new Vector3(size, size * StressTest.ISO_Y, 1f);

        var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
        var m = new Material(sh) { mainTexture = t };
        m.mainTextureScale = new Vector2(size / 4f, size / 4f);
        g.GetComponent<MeshRenderer>().sharedMaterial = m;
    }
}
