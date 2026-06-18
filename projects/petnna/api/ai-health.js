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

  try {
    const body = typeof req.body === "string" ? JSON.parse(req.body || "{}") : (req.body || {});
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
