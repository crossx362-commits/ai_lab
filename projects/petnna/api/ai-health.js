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

// 모든 건강 프롬프트 앞에 붙는 공통 제약(2026-08-12 오너 지시 "출력도 기록형으로").
//
// 왜: 사진 기반 질병 탐지·중증도 판정은 동물용의료기기 품목허가 대상이다. 카피만
// 기록형으로 바꾸고 출력이 "정상|주의|이상"·urgent·needsVet 그대로면 위험이 사라진 게
// 아니라 안 보이게 된 것이다. 판정을 아예 만들지 않는 쪽으로 프롬프트를 바꾼다.
//
// 안전 측면에서도 이쪽이 낫다: 허가 없는 LLM 분류의 진짜 위험은 오탐이 아니라
// **놓침**이다("관찰" 판정을 받고 병원을 안 가는 경우). 판정을 안 하는 대신
// 수의사 상담 권유는 조건부가 아니라 항상 노출한다.
const RECORD_ONLY = `[역할 제한 — 반드시 지켜라]
너는 진단 도구가 아니라 **보호자의 기록을 돕는 도우미**다.
- 병명·질병 추정·감별진단을 절대 하지 마라.
- "정상/이상/주의" 같은 상태 판정, 건강 점수, 긴급도·중증도 등급을 매기지 마라.
- 치료·투약을 안내하지 마라.
- 보이는 것/들은 것을 그대로 기술하고, 수의사에게 물어볼 거리를 정리하는 데 그쳐라.
- 확신할 수 없으면 "확인불가"라고 적어라. 추측으로 채우지 마라.`;

function buildPrompt(body) {
  const petName = body.petName || "펫";

  if (body.type === "photo") {
    return {
      responseJson: true,
      parts: [
        {
          text: `${RECORD_ONLY}

이 반려동물 사진에서 **눈에 보이는 것만** 기록해줘. 사진에 안 보이는 부위는 "확인불가".
JSON으로만 반환 (다른 텍스트 없이):

{
  "observations": {
    "eyes": "보이는 그대로 짧게 (예: 맑고 눈곱 없음 / 오른쪽 눈물자국 있음) 또는 확인불가",
    "ears": "...", "skin": "...", "coat": "...", "teeth": "...",
    "nose": "...", "posture": "...", "weight": "...", "alertness": "...", "paw": "..."
  },
  "summary": "사진에서 보이는 것만 한국어 2문장으로 기술. 원인·병명 추측 금지",
  "vetTalkingPoints": ["다음 진료 때 수의사에게 이야기하거나 물어보면 좋을 것 1~3개"],
  "recordNote": "오늘 기록에 덧붙일 한 줄 (일상 케어 메모 수준)"
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
        text: `${RECORD_ONLY}

반려동물 보호자가 "${petName}"의 상태를 이렇게 설명했어:
"${body.transcript || ""}"

보호자가 **진료 때 잘 전달할 수 있도록 정리**만 해줘. JSON으로만:
{
  "recordedSymptoms": ["보호자 설명에서 그대로 뽑은 관찰 내용 1~5개"],
  "observationChecklist": ["집에서 추가로 기록해두면 좋을 것 1~3개 (횟수·시각·양 등)"],
  "vetTalkingPoints": ["진료 때 전달하거나 물어보면 좋을 것 1~3개"],
  "summary": "보호자 설명을 2문장으로 정리. 원인 추측·중증도 판단 금지"
}`
      }]
    };
  }

  if (body.type === "vet-chat") {
    return {
      responseJson: false,
      parts: [{
        text: `${RECORD_ONLY}

당신은 반려동물 **기록을 돕는** AI 도우미입니다. 수의사가 아니며 진단하지 않습니다.
현재 이야기 중인 반려동물: ${petName} (${body.breed || "품종 미상"}, ${body.age || "나이 미상"})
보호자가 무엇을 기록하고 수의사에게 무엇을 물어보면 좋을지 정리해 주세요.
건강이 걱정되는 내용이면 판단을 내리지 말고 수의사 상담을 권하세요.
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
