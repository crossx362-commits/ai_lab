using UnityEngine;

/// <summary>
/// 노이즈맵 바닥 타일을 깐다 (블렌더에서 seamless로 베이크한 텍스처).
/// 타일을 여러 장 깔지 않고 **쿼드 1장 + UV 타일링**으로 처리한다 —
/// 드로우콜을 1로 유지해야 W1 측정에서 바닥이 변수가 되지 않는다.
/// </summary>
public static class GroundBuilder
{
    public static void Build(SpriteBank bank, float radius)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Ground";
        Object.Destroy(go.GetComponent<Collider>());

        // 쿼터뷰: 월드 세로를 ISO_Y로 눌러 그리므로 바닥도 같은 비율로 눌러야
        // 스프라이트와 원근이 맞는다 (§17 2D 쿼터뷰)
        float size = radius * 3f;
        go.transform.position = new Vector3(0, 0, 1f);   // 스프라이트보다 뒤
        go.transform.localScale = new Vector3(size, size * StressTest.ISO_Y, 1f);

        var tex = Resources.Load<Texture2D>("ground/field_plain_albedo");
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
    }
}
