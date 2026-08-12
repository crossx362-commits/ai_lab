const LOCKED_RESPONSE = {
  error: true,
  locked: true,
  message: "AI 기능은 현재 운영 설정에서 차단되어 있습니다."
};

function sendJson(res, status, payload) {
  res.statusCode = status;
  res.setHeader("Content-Type", "application/json; charset=utf-8");
  res.end(JSON.stringify(payload));
}

const MAX_IMAGE_BASE64_CHARS = Number(process.env.AI_HEALTH_MAX_IMAGE_BASE64_CHARS || 900000);

// ── 남용 방지(2026-08-12) ────────────────────────────────────────────────
// 이 핸들러는 인증이 없다. AI_HEALTH_ENABLED를 켜는 순간 /api/ai-health 는
// **누구나 쓸 수 있는 공개 Gemini 프록시**가 된다(type:"vet-chat"은 자유 텍스트를
// 그대로 전달) — 오너의 Gemini 크레딧이 그대로 남의 것이 된다.
// 아래 둘은 완전한 인증이 아니라 '문턱'이다: 브라우저가 자동으로 붙이는 Origin을
// 위조하는 건 curl 한 줄이면 된다. 진짜 방어는 Supabase JWT 검증인데, 그러면
// 게스트(둘러보기)가 AI를 못 쓰게 되므로 그건 오너 결정 사항으로 남긴다.
const ALLOWED_HOSTS = (process.env.AI_HEALTH_ALLOWED_HOSTS ||
  "petnna.vercel.app,localhost,127.0.0.1").split(",").map(s => s.trim()).filter(Boolean);

// 브라우저는 GET/HEAD가 아닌 요청에 Origin을 항상 붙인다 — 아예 없으면 스크립트다.
function originAllowed(req) {
  const raw = req.headers.origin || req.headers.referer || "";
  if (!raw) return false;
  try {
    const host = new URL(raw).hostname;
    return ALLOWED_HOSTS.some(h => host === h || host.endsWith("." + h));
  } catch (e) {
    return false;
  }
}

const RATE_MAX = Number(process.env.AI_HEALTH_RATE_MAX || 20);
const RATE_WINDOW_MS = Number(process.env.AI_HEALTH_RATE_WINDOW_MS || 3600000);
const hits = new Map();

// 서버리스라 인스턴스마다 카운터가 따로 놀고 콜드스타트로 리셋된다 — 즉 상한이
// 정확히 지켜지진 않는다. 그래도 한 인스턴스에 쏟아지는 버스트는 실제로 끊긴다.
function rateLimited(req) {
  const ip = String(req.headers["x-forwarded-for"] || "").split(",")[0].trim() || "unknown";
  const now = Date.now();
  if (hits.size > 5000) {
    for (const [k, v] of hits) if (now > v.resetAt) hits.delete(k);
  }
  const rec = hits.get(ip);
  if (!rec || now > rec.resetAt) {
    hits.set(ip, { n: 1, resetAt: now + RATE_WINDOW_MS });
    return false;
  }
  rec.n += 1;
  return rec.n > RATE_MAX;
}

function buildPrompt(body) {
  const petName = body.petName || "펫";

  if (body.type === "photo") {
    return {
      responseJson: true,
      parts: [
        {
          text: `이 반려동물 사진을 보고 건강 상태를 전문 수의사 관점으로 분석해줘.
사진에서 보이지 않는 부위는 "확인불가"로 반환.
다음 10개 항목을 JSON으로만 반환 (다른 텍스트 없이):

{
  "eyes": "정상|주의|이상|확인불가",
  "ears": "정상|주의|이상|확인불가",
  "skin": "정상|주의|이상|확인불가",
  "coat": "윤기있음|보통|칙칙함|확인불가",
  "teeth": "정상|주의|이상|확인불가",
  "nose": "촉촉함|건조함|이상|확인불가",
  "posture": "정상|주의|이상|확인불가",
  "weight": "저체중|적정|과체중|확인불가",
  "alertness": "활발|보통|무기력|확인불가",
  "paw": "정상|주의|이상|확인불가",
  "score": 0~100,
  "urgent": true|false,
  "urgentReason": "긴급 사유 (urgent=false면 빈 문자열)",
  "summary": "한국어 2문장 요약",
  "advice": "권고 사항 1줄"
}`
        },
        { inline_data: { mime_type: body.mimeType || "image/jpeg", data: body.imageBase64 || "" } }
      ]
    };
  }

  if (body.type === "symptom") {
    return {
      responseJson: true,
      parts: [{
        text: `반려동물 보호자가 "${petName}"의 증상을 이렇게 설명했어:
"${body.transcript || ""}"

수의사 관점에서 아래 JSON 형식으로만 분석해줘:
{
  "possibleCauses": ["원인1", "원인2", "원인3"],
  "immediateAction": "지금 당장 할 수 있는 조치 1줄",
  "needsVet": true|false,
  "urgency": "즉시|24시간내|일주일내|관찰",
  "summary": "2문장 요약"
}`
      }]
    };
  }

  if (body.type === "vet-chat") {
    return {
      responseJson: false,
      parts: [{
        text: `당신은 10년 경력의 친절한 수의사 AI 어시스턴트입니다.
현재 상담 중인 반려동물: ${petName} (${body.breed || "품종 미상"}, ${body.age || "나이 미상"})
의학적으로 긴급한 경우 즉시 동물병원 방문을 권고하세요.
질문: ${body.message || ""}
답변은 한국어로, 친근하고 이해하기 쉽게 해주세요.`
      }]
    };
  }

  if (body.type === "social-caption") {
    const parts = [{
      text: body.imageBase64
        ? "이 반려동물 사진을 보고 진짜 집사가 인스타에 올릴 것처럼 짧고 자연스러운 한국어 자랑글 캡션을 써줘. 이모지 1개와 해시태그 5개 포함. 캡션만 출력."
        : `${petName}의 일상을 공유하는 진짜 집사 말투의 인스타 자랑글 캡션을 써줘. 이모지 1개와 해시태그 5개 포함. 캡션만.`
    }];
    if (body.imageBase64) {
      parts.push({ inline_data: { mime_type: body.mimeType || "image/jpeg", data: body.imageBase64 } });
    }
    return { responseJson: false, parts };
  }

  return null;
}

module.exports = async function handler(req, res) {
  if (req.method !== "POST") {
    return sendJson(res, 405, { error: true, message: "POST only" });
  }

  if (process.env.AI_HEALTH_ENABLED !== "true" || !process.env.GEMINI_API_KEY) {
    return sendJson(res, 503, LOCKED_RESPONSE);
  }

  if (!originAllowed(req)) {
    return sendJson(res, 403, { error: true, message: "허용되지 않은 요청 출처입니다." });
  }
  if (rateLimited(req)) {
    return sendJson(res, 429, {
      error: true,
      message: "AI 요청이 너무 잦습니다. 잠시 후 다시 시도해주세요."
    });
  }

  try {
    const body = typeof req.body === "string" ? JSON.parse(req.body || "{}") : (req.body || {});
    if (body.imageBase64 && body.imageBase64.length > MAX_IMAGE_BASE64_CHARS) {
      return sendJson(res, 413, {
        error: true,
        message: "이미지가 너무 큽니다. AI 사용량 절약을 위해 더 작은 이미지로 다시 시도해주세요."
      });
    }
    const prompt = buildPrompt(body);
    if (!prompt) {
      return sendJson(res, 400, { error: true, message: "지원하지 않는 AI 요청입니다." });
    }

    const geminiRes = await fetch(
      `https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=${process.env.GEMINI_API_KEY}`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          contents: [{ parts: prompt.parts }],
          generationConfig: prompt.responseJson ? { responseMimeType: "application/json" } : undefined
        })
      }
    );

    if (!geminiRes.ok) {
      return sendJson(res, geminiRes.status, { error: true, message: `Gemini API ${geminiRes.status}` });
    }

    const data = await geminiRes.json();
    const text = data?.candidates?.[0]?.content?.parts?.[0]?.text || "";
    if (prompt.responseJson) {
      return sendJson(res, 200, JSON.parse(text || "{}"));
    }
    return sendJson(res, 200, { text });
  } catch (error) {
    return sendJson(res, 500, { error: true, message: error.message || "AI 처리 실패" });
  }
};
