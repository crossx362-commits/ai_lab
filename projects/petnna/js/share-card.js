// share-card.js — AI 건강 / 사주 공유 이미지 카드

// ─── 공통 헬퍼 ────────────────────────────────────────────────────
function _wrapText(ctx, text, x, y, maxWidth, lineHeight) {
    if (!text) return y;
    const words = text.split('');
    let line = '';
    for (const ch of words) {
        const test = line + ch;
        if (ctx.measureText(test).width > maxWidth && line) {
            ctx.fillText(line, x, y);
            line = ch;
            y += lineHeight;
        } else {
            line = test;
        }
    }
    if (line) { ctx.fillText(line, x, y); y += lineHeight; }
    return y;
}

function _statusColor(val) {
    if (!val || val === '확인불가') return '#9ca3af';
    if (['정상','윤기있음','촉촉함','적정','활발'].includes(val)) return '#10b981';
    if (['주의','건조함','보통','저체중','과체중'].includes(val)) return '#f59e0b';
    return '#ef4444';
}

async function _downloadOrShare(canvas, filename, title, text) {
    const dataUrl = canvas.toDataURL('image/png');
    const blob = await (await fetch(dataUrl)).blob();
    const file = new File([blob], filename, { type: 'image/png' });
    if (navigator.share && navigator.canShare({ files: [file] })) {
        try { await navigator.share({ files: [file], title, text }); return; } catch {}
    }
    const a = document.createElement('a');
    a.href = dataUrl;
    a.download = filename;
    a.click();
    if (typeof showToast === 'function') showToast('이미지를 저장했습니다 📸');
}

// ─── AI 건강 카드 (정사각형 1080×1080) ───────────────────────────
function generateShareCard(type = 'health') {
    const pet = typeof getActivePet === 'function' ? getActivePet() : null;
    const petName = pet?.name || '댕이';
    const analyses = typeof getHealthAnalyses === 'function' ? getHealthAnalyses() : [];
    const h = analyses[0] || {};

    const S = 1080;
    const canvas = document.createElement('canvas');
    canvas.width = S; canvas.height = S;
    const ctx = canvas.getContext('2d');

    // 배경 그라데이션
    const grad = ctx.createLinearGradient(0, 0, S, S);
    if (type === 'health') {
        grad.addColorStop(0, '#faf3ef'); grad.addColorStop(1, '#f4e2d9');
    } else {
        grad.addColorStop(0, '#fffbeb'); grad.addColorStop(1, '#fde68a');
    }
    ctx.fillStyle = grad;
    ctx.fillRect(0, 0, S, S);

    // 상단 헤더 배경
    ctx.fillStyle = type === 'health' ? '#a9583e' : '#d97706';
    ctx.beginPath();
    ctx.roundRect(40, 40, S - 80, 140, 24);
    ctx.fill();

    // 헤더 텍스트
    ctx.fillStyle = '#fff';
    ctx.font = 'bold 52px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText(type === 'health' ? '🏥 AI 건강 분석 결과' : '🔯 사주 분석 결과', S / 2, 130);

    // 펫 이름
    ctx.fillStyle = '#374151';
    ctx.font = 'bold 40px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.fillText(`${petName} 의 분석 결과`, S / 2, 250);

    if (type === 'health') {
        const score = h.score ?? 0;
        const scoreColor = score >= 80 ? '#10b981' : score >= 60 ? '#f59e0b' : '#ef4444';

        // 점수 큰 원
        ctx.beginPath();
        ctx.arc(S / 2, 420, 110, 0, Math.PI * 2);
        ctx.fillStyle = '#fff';
        ctx.fill();
        ctx.strokeStyle = scoreColor;
        ctx.lineWidth = 8;
        ctx.stroke();

        ctx.fillStyle = scoreColor;
        ctx.font = 'bold 100px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
        ctx.fillText(String(score), S / 2, 460);
        ctx.fillStyle = '#6b7280';
        ctx.font = 'bold 28px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
        ctx.fillText('건강점수', S / 2, 510);

        // 10항목 배지 그리드
        const items = [
            { label:'눈', val: h.eyes }, { label:'귀', val: h.ears },
            { label:'피부', val: h.skin }, { label:'털', val: h.coat },
            { label:'치아', val: h.teeth }, { label:'코', val: h.nose },
            { label:'자세', val: h.posture }, { label:'체중', val: h.weight },
            { label:'활력', val: h.alertness }, { label:'발', val: h.paw },
        ].filter(i => i.val && i.val !== '확인불가');

        const cols = 5;
        const bW = 170, bH = 70, gapX = 16, gapY = 12;
        const totalW = cols * bW + (cols - 1) * gapX;
        const startX = (S - totalW) / 2;
        let startY = 580;

        items.forEach((item, idx) => {
            const col = idx % cols;
            const row = Math.floor(idx / cols);
            const x = startX + col * (bW + gapX);
            const y = startY + row * (bH + gapY);
            ctx.fillStyle = '#fff';
            ctx.beginPath();
            ctx.roundRect(x, y, bW, bH, 12);
            ctx.fill();
            ctx.fillStyle = _statusColor(item.val);
            ctx.font = 'bold 22px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
            ctx.textAlign = 'center';
            ctx.fillText(`${item.label}: ${item.val}`, x + bW / 2, y + 42);
        });

        // 요약
        if (h.summary) {
            ctx.fillStyle = '#4b5563';
            ctx.font = '28px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
            ctx.textAlign = 'center';
            _wrapText(ctx, h.summary, S / 2, 900, S - 120, 38);
        }
    }

    // 워터마크
    ctx.fillStyle = 'rgba(0,0,0,0.15)';
    ctx.font = '26px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText('🐾 펫과나 (Pet & Na) — petnna.app', S / 2, S - 40);

    return canvas;
}

async function shareHealthCard() {
    const canvas = generateShareCard('health');
    const pet = typeof getActivePet === 'function' ? getActivePet() : null;
    await _downloadOrShare(canvas, 'petna-health.png', '펫 AI 건강 분석',
        `🐾 AI가 분석한 ${pet?.name || '우리 펫'}의 건강점수 확인해요! #펫과나 #AI건강분석`);
}

// ─── 사주 공유 카드 (세로 9:16 = 1080×1920, 쇼츠/릴스 최적) ──────
function generateSajuShareCard() {
    const pet = typeof getActivePet === 'function' ? getActivePet() : null;
    const petName = pet?.name || '댕이';
    const sajuData = pet?.sajuData || {};
    const score = sajuData.compatScore || 0;
    const nickname = typeof settings_nickname !== 'undefined' ? settings_nickname : '집사';

    const petBirthYear = sajuData.petBirth ? sajuData.petBirth.split('-')[0] : null;
    const ownerBirthYear = sajuData.ownerBirth ? sajuData.ownerBirth.split('-')[0] : null;

    const W = 1080, H = 1920;
    const canvas = document.createElement('canvas');
    canvas.width = W; canvas.height = H;
    const ctx = canvas.getContext('2d');

    // 배경 — 황금 그라데이션
    const grad = ctx.createLinearGradient(0, 0, W, H);
    grad.addColorStop(0, '#1c1917');
    grad.addColorStop(0.4, '#292524');
    grad.addColorStop(1, '#1c1038');
    ctx.fillStyle = grad;
    ctx.fillRect(0, 0, W, H);

    // 별 파티클 효과 (랜덤 고정)
    ctx.fillStyle = 'rgba(255,255,255,0.6)';
    const stars = [[120,80],[340,160],[780,50],[950,220],[200,350],[870,400],[60,600],[1020,580]];
    stars.forEach(([x, y]) => {
        ctx.beginPath(); ctx.arc(x, y, 2, 0, Math.PI * 2); ctx.fill();
    });

    // 상단 브랜드
    ctx.fillStyle = '#f59e0b';
    ctx.font = 'bold 36px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText('🐾 펫과나 사주팔자', W / 2, 90);

    // 제목 박스
    ctx.fillStyle = 'rgba(245,158,11,0.15)';
    ctx.strokeStyle = '#f59e0b';
    ctx.lineWidth = 2;
    ctx.beginPath(); ctx.roundRect(60, 120, W - 120, 200, 24); ctx.fill(); ctx.stroke();

    ctx.fillStyle = '#fef3c7';
    ctx.font = 'bold 64px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText(`${petName} × ${nickname}`, W / 2, 200);

    // 년생 표시
    if (petBirthYear || ownerBirthYear) {
        ctx.font = '30px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
        ctx.fillStyle = '#fcd34d';
        const yearParts = [];
        if (petBirthYear) yearParts.push(`🐾 ${petBirthYear}년생`);
        if (ownerBirthYear) yearParts.push(`👑 ${ownerBirthYear}년생`);
        ctx.fillText(yearParts.join('  ·  '), W / 2, 248);
    }

    ctx.font = 'bold 38px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.fillStyle = '#fbbf24';
    ctx.fillText('반려동물 사주 조화도 분석', W / 2, 300);

    // 조화도 점수 원형
    const cx = W / 2, cy = 560, r = 160;
    ctx.beginPath(); ctx.arc(cx, cy, r, 0, Math.PI * 2);
    const circleGrad = ctx.createRadialGradient(cx, cy, 0, cx, cy, r);
    const scoreColor = score >= 90 ? '#10b981' : score >= 80 ? '#f59e0b' : score >= 70 ? '#fb923c' : '#ef4444';
    circleGrad.addColorStop(0, scoreColor + '33');
    circleGrad.addColorStop(1, scoreColor + '11');
    ctx.fillStyle = circleGrad;
    ctx.fill();
    ctx.strokeStyle = scoreColor;
    ctx.lineWidth = 6;
    ctx.stroke();

    ctx.fillStyle = scoreColor;
    ctx.font = 'bold 120px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText(`${score}%`, W / 2, cy + 40);
    ctx.fillStyle = '#d1d5db';
    ctx.font = 'bold 32px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.fillText('조화도 점수', W / 2, cy + 100);

    // 조화도 제목
    if (sajuData.compatTitle) {
        ctx.fillStyle = '#fef9c3';
        ctx.font = 'bold 48px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
        ctx.fillText(`✨ ${sajuData.compatTitle}`, W / 2, 780);
    }

    // 구분선
    ctx.strokeStyle = 'rgba(245,158,11,0.3)';
    ctx.lineWidth = 1;
    ctx.beginPath(); ctx.moveTo(80, 820); ctx.lineTo(W - 80, 820); ctx.stroke();

    // 오행 정보
    const elementY = 880;
    [
        { label: petBirthYear ? `🐾 ${petName} (${petBirthYear}년생)` : `🐾 ${petName}`, text: sajuData.petSummary || '─' },
        { label: ownerBirthYear ? `👑 ${nickname} (${ownerBirthYear}년생)` : `👑 ${nickname}`, text: sajuData.ownerSummary || '─' }
    ].forEach((item, i) => {
        const bx = 80 + i * (W / 2 - 60), by = elementY, bw = W / 2 - 120, bh = 160;
        ctx.fillStyle = 'rgba(255,255,255,0.06)';
        ctx.beginPath(); ctx.roundRect(bx, by, bw, bh, 16); ctx.fill();
        ctx.fillStyle = '#fbbf24';
        ctx.font = 'bold 30px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText(item.label, bx + bw / 2, elementY + 50);
        ctx.fillStyle = '#e5e7eb';
        ctx.font = '26px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
        _wrapText(ctx, item.text, bx + bw / 2, elementY + 90, bw - 20, 34);
    });

    // 전생 인연
    if (sajuData.pastDesc) {
        ctx.fillStyle = '#eccab8';
        ctx.font = '30px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
        ctx.textAlign = 'center';
        _wrapText(ctx, `💫 ${sajuData.pastDesc}`, W / 2, 1120, W - 160, 42);
    }

    // 시너지
    if (sajuData.synergyDesc) {
        ctx.fillStyle = '#86efac';
        ctx.font = '30px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
        ctx.textAlign = 'center';
        _wrapText(ctx, `✨ ${sajuData.synergyDesc}`, W / 2, 1250, W - 160, 42);
    }

    // 구분선
    ctx.strokeStyle = 'rgba(245,158,11,0.2)';
    ctx.beginPath(); ctx.moveTo(80, 1400); ctx.lineTo(W - 80, 1400); ctx.stroke();

    // 해시태그
    ctx.fillStyle = '#9ca3af';
    ctx.font = '28px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText('#펫과나 #반려동물사주 #강아지사주 #고양이사주 #펫팸족', W / 2, 1460);
    ctx.fillText('#럭키맥싱 #펫스타그램 #댕댕이스타그램', W / 2, 1510);

    // QR 힌트 박스
    ctx.fillStyle = 'rgba(245,158,11,0.12)';
    ctx.beginPath(); ctx.roundRect(80, 1560, W - 160, 200, 20); ctx.fill();
    ctx.fillStyle = '#f59e0b';
    ctx.font = 'bold 32px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.fillText('우리 반려동물의 사주를 확인하세요!', W / 2, 1630);
    ctx.fillStyle = '#9ca3af';
    ctx.font = '26px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.fillText('AI 건강분석 · 산책 GPS · 소셜 피드', W / 2, 1680);
    ctx.fillStyle = '#fbbf24';
    ctx.font = 'bold 30px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.fillText('🐾 펫과나 — 반려동물 케어 올인원', W / 2, 1730);

    // 하단 워터마크
    ctx.fillStyle = 'rgba(255,255,255,0.1)';
    ctx.font = '22px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.fillText('Made with 펫과나 · petnna.app', W / 2, 1880);

    return canvas;
}

async function shareSajuCard() {
    const pet = typeof getActivePet === 'function' ? getActivePet() : null;
    if (!pet?.sajuData) {
        if (typeof showToast === 'function') showToast('먼저 사주 분석을 완료해주세요 🔯');
        return;
    }
    const canvas = generateSajuShareCard();
    const petName = pet.name || '댕이';
    const score = pet.sajuData.compatScore || 0;
    await _downloadOrShare(
        canvas, 'petna-saju.png',
        `${petName}의 사주 조화도 ${score}%`,
        `🔯 ${petName}의 사주팔자 조화도가 ${score}%! #펫과나 #반려동물사주 #럭키맥싱`
    );
}

// ─── 신규 펫 탄생 카드 ────────────────────────────────────────────────────────
function generateWelcomeCard(pet) {
    const petName = pet?.name || '새 친구';
    const petEmoji = pet?.type === 'cat' ? '🐱' : pet?.type === 'rabbit' ? '🐰' : pet?.type === 'hamster' ? '🐹' : '🐶';
    const breed = pet?.breed || '';

    const S = 1080;
    const canvas = document.createElement('canvas');
    canvas.width = S; canvas.height = S;
    const ctx = canvas.getContext('2d');

    // 배경 그라데이션 — 따뜻한 오렌지/골든
    const grad = ctx.createLinearGradient(0, 0, S, S);
    grad.addColorStop(0, '#fff7ed');
    grad.addColorStop(1, '#fde68a');
    ctx.fillStyle = grad;
    ctx.fillRect(0, 0, S, S);

    // 헤더 배너
    ctx.fillStyle = '#e37736';
    ctx.beginPath();
    ctx.roundRect(40, 40, S - 80, 150, 28);
    ctx.fill();

    ctx.fillStyle = '#fff';
    ctx.font = 'bold 52px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText('🐾 펫과나에 합류했어요!', S / 2, 140);

    // 펫 이모지 크게
    ctx.font = '220px serif';
    ctx.textAlign = 'center';
    ctx.fillText(petEmoji, S / 2, 430);

    // 펫 이름
    ctx.fillStyle = '#92400e';
    ctx.font = 'bold 80px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.fillText(petName, S / 2, 560);

    if (breed) {
        ctx.fillStyle = '#b45309';
        ctx.font = '38px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
        ctx.fillText(breed, S / 2, 620);
    }

    // 소개 문구
    ctx.fillStyle = '#78350f';
    ctx.font = '42px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.fillText(`${petName}이(가) 펫과나 가족이 됐어요! 🎉`, S / 2, 720);

    // 하단 브랜드
    ctx.fillStyle = '#e37736';
    ctx.beginPath();
    ctx.roundRect(40, S - 140, S - 80, 100, 24);
    ctx.fill();
    ctx.fillStyle = '#fff';
    ctx.font = 'bold 40px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.fillText('Pet & Na — AI 반려동물 케어 올인원', S / 2, S - 75);

    return canvas;
}

async function shareWelcomeCard(pet) {
    const canvas = generateWelcomeCard(pet);
    const petName = pet?.name || '새 친구';
    await _downloadOrShare(
        canvas, 'petna-welcome.png',
        `${petName}이(가) 펫과나에 합류했어요! 🐾`,
        `🐾 ${petName}이(가) 펫과나 가족이 됐어요! #펫과나 #반려동물 #새가족`
    );
}

function generateCompatChallengeCard(pet, compatScore) {
    const petName = pet?.name || '우리 펫';
    const score = Math.round(compatScore) || 0;

    const S = 1080;
    const canvas = document.createElement('canvas');
    canvas.width = S; canvas.height = S;
    const ctx = canvas.getContext('2d');

    // 배경 그라데이션 — 보라-핑크
    const grad = ctx.createLinearGradient(0, 0, S, S);
    grad.addColorStop(0, '#faf3ef');
    grad.addColorStop(1, '#fce7f3');
    ctx.fillStyle = grad;
    ctx.fillRect(0, 0, S, S);

    // 헤더 배너
    ctx.fillStyle = '#a9583e';
    ctx.beginPath();
    ctx.roundRect(40, 40, S - 80, 150, 28);
    ctx.fill();
    ctx.fillStyle = '#fff';
    ctx.font = 'bold 52px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText('☯️ 조화도 챌린지', S / 2, 140);

    // 큰 숫자 — 점수
    ctx.fillStyle = '#8f4832';
    ctx.font = 'bold 280px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText(`${score}%`, S / 2, 580);

    // 서브 문구
    ctx.fillStyle = '#4c1d95';
    ctx.font = 'bold 54px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.fillText(`우리 ${petName}와 나는 조화도 ${score}%!`, S / 2, 680);

    // 해시태그
    ctx.fillStyle = '#a9583e';
    ctx.font = '40px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.fillText('당신의 펫과 조화도는? #펫과나 #조화도챌린지', S / 2, 760);

    // 하단 브랜드
    ctx.fillStyle = '#a9583e';
    ctx.beginPath();
    ctx.roundRect(40, S - 140, S - 80, 100, 24);
    ctx.fill();
    ctx.fillStyle = '#fff';
    ctx.font = 'bold 40px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.fillText('Pet & Na — AI 반려동물 케어 올인원', S / 2, S - 75);

    return canvas;
}

async function shareCompatChallenge(pet, compatScore) {
    const canvas = generateCompatChallengeCard(pet, compatScore);
    const petName = pet?.name || '우리 펫';
    await _downloadOrShare(
        canvas, 'petna-compat-challenge.png',
        `${petName}와 나의 조화도 ${Math.round(compatScore)}%! ☯️`,
        `☯️ 우리 ${petName}와 나는 조화도 ${Math.round(compatScore)}%! 당신의 펫과 조화도는? #펫과나 #조화도챌린지`
    );
}

// ─── 산책 리포트 카드 (경로 썸네일 + 거리·시간·칼로리·배변마커, 1080×1080) ──
function _drawWalkRoute(ctx, coords, marks, box) {
    // box = {x, y, w, h} — 경로 좌표를 박스에 맞춰 그린다
    ctx.fillStyle = '#eef7f0';
    ctx.beginPath();
    ctx.roundRect(box.x, box.y, box.w, box.h, 28);
    ctx.fill();

    const pts = Array.isArray(coords) ? coords.filter(c => Array.isArray(c) && c.length >= 2) : [];
    if (pts.length < 2) {
        ctx.fillStyle = '#9ca3af';
        ctx.font = '38px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText('🐾 경로 기록 없음', box.x + box.w / 2, box.y + box.h / 2 + 12);
        return;
    }

    const lats = pts.map(p => p[0]), lngs = pts.map(p => p[1]);
    let minLat = Math.min(...lats), maxLat = Math.max(...lats);
    let minLng = Math.min(...lngs), maxLng = Math.max(...lngs);
    const spanLat = Math.max(maxLat - minLat, 1e-6);
    const spanLng = Math.max(maxLng - minLng, 1e-6);
    const pad = 60;
    const iw = box.w - pad * 2, ih = box.h - pad * 2;
    const scale = Math.min(iw / spanLng, ih / spanLat);
    const offX = box.x + pad + (iw - spanLng * scale) / 2;
    const offY = box.y + pad + (ih - spanLat * scale) / 2;
    // 위도는 위로 갈수록 커지므로 y축 반전
    const toXY = (lat, lng) => [offX + (lng - minLng) * scale, offY + (maxLat - lat) * scale];

    ctx.strokeStyle = '#e37736';
    ctx.lineWidth = 10;
    ctx.lineJoin = 'round';
    ctx.lineCap = 'round';
    ctx.beginPath();
    pts.forEach((p, i) => {
        const [x, y] = toXY(p[0], p[1]);
        if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
    });
    ctx.stroke();

    // 시작/끝 지점
    const [sx, sy] = toXY(pts[0][0], pts[0][1]);
    const [ex, ey] = toXY(pts[pts.length - 1][0], pts[pts.length - 1][1]);
    ctx.fillStyle = '#10b981';
    ctx.beginPath(); ctx.arc(sx, sy, 16, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = '#ef4444';
    ctx.beginPath(); ctx.arc(ex, ey, 16, 0, Math.PI * 2); ctx.fill();

    // 배변/마킹 마커
    const markEmoji = { poop: '💩', pee: '💦', sniff: '👃' };
    (Array.isArray(marks) ? marks : []).forEach(m => {
        if (typeof m?.lat !== 'number' || typeof m?.lng !== 'number') return;
        const [mx, my] = toXY(m.lat, m.lng);
        ctx.font = '36px serif';
        ctx.textAlign = 'center';
        ctx.fillText(markEmoji[m.type] || '📍', mx, my + 12);
    });
}

// 산책 데이터 기반 AI 한 줄 코멘트 (규칙 기반, 외부 API 불필요 · 결정론적)
function _walkAiComment(w, petName) {
    const name = petName || '댕이';
    const dist = parseFloat(w?.distance) || 0;
    const kcal = parseInt(w?.calories) || 0;
    const sniff = parseInt(w?.sniff) || 0;
    const pee = parseInt(w?.pee) || 0;
    const poop = parseInt(w?.poop) || 0;
    const parts = [];
    if (dist >= 3) parts.push(`${dist.toFixed(1)}km 완주, ${name} 오늘 체력왕이네요! 🏅`);
    else if (dist >= 1.5) parts.push(`${dist.toFixed(1)}km 알찬 산책으로 ${name}의 스트레스가 쑥 내려갔어요.`);
    else if (dist > 0) parts.push(`짧아도 ${name}에겐 소중한 바깥 나들이였어요. 🌿`);
    else parts.push(`${name}와 함께한 오늘의 안심 산책 기록이에요.`);
    if (sniff >= 3) parts.push('킁킁 냄새 탐험을 많이 해서 두뇌 자극도 충분했어요.');
    else if (poop >= 1 && pee >= 1) parts.push('배변도 규칙적이라 컨디션이 좋아 보여요.');
    else if (kcal >= 150) parts.push(`${kcal}kcal이나 소모했으니 오늘 밤은 푹 잘 거예요. 💤`);
    return parts.join(' ');
}

function generateWalkReportCard(w) {
    const pet = typeof getActivePet === 'function' ? getActivePet() : null;
    const petName = pet?.name || '댕이';

    const S = 1080;
    const canvas = document.createElement('canvas');
    canvas.width = S; canvas.height = S;
    const ctx = canvas.getContext('2d');

    const grad = ctx.createLinearGradient(0, 0, S, S);
    grad.addColorStop(0, '#eef7f0');
    grad.addColorStop(1, '#d1e9d8');
    ctx.fillStyle = grad;
    ctx.fillRect(0, 0, S, S);

    // 헤더 배너
    ctx.fillStyle = '#0f9d58';
    ctx.beginPath();
    ctx.roundRect(40, 40, S - 80, 140, 28);
    ctx.fill();
    ctx.fillStyle = '#fff';
    ctx.font = 'bold 50px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText(`🦮 ${petName}의 산책 리포트`, S / 2, 122);

    // 경로 썸네일
    _drawWalkRoute(ctx, w.coords, w.marks, { x: 40, y: 210, w: S - 80, h: 360 });

    // AI 한 줄 코멘트 밴드
    const aiComment = _walkAiComment(w, petName);
    ctx.fillStyle = 'rgba(255,255,255,0.9)';
    ctx.beginPath();
    ctx.roundRect(40, 585, S - 80, 110, 24);
    ctx.fill();
    ctx.fillStyle = '#0f9d58';
    ctx.font = 'bold 28px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.textAlign = 'left';
    ctx.fillText('🤖 AI 산책 코멘트', 70, 623);
    ctx.fillStyle = '#334155';
    ctx.font = '27px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.textAlign = 'center';
    _wrapText(ctx, aiComment, S / 2, 660, S - 160, 34);

    // 핵심 지표 3개
    const stats = [
        ['📏 거리', `${w.distance} km`],
        ['⏱️ 시간', `${w.duration || '-'}`],
        ['🔥 칼로리', `${w.calories} kcal`],
    ];
    const cardW = (S - 80 - 40) / 3;
    stats.forEach((s, i) => {
        const cx = 40 + i * (cardW + 20);
        ctx.fillStyle = '#ffffff';
        ctx.beginPath();
        ctx.roundRect(cx, 730, cardW, 150, 24);
        ctx.fill();
        ctx.fillStyle = '#166534';
        ctx.font = '32px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText(s[0], cx + cardW / 2, 790);
        ctx.fillStyle = '#0f172a';
        ctx.font = 'bold 46px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
        ctx.fillText(s[1], cx + cardW / 2, 848);
    });

    // 배변/급수/냄새 요약 + 기분(선택 시)
    const moodMeta = WALK_MOODS.find(m => m.key === w.mood);
    const moodStr = moodMeta ? `   ${moodMeta.emoji} ${moodMeta.label}` : '';
    ctx.fillStyle = '#166534';
    ctx.font = `bold ${moodStr ? 40 : 44}px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif`;
    ctx.textAlign = 'center';
    ctx.fillText(`💩 ${w.poop || 0}회   💦 ${w.pee || 0}회   👃 ${w.sniff || 0}회${moodStr}`, S / 2, 950);

    // 하단 브랜드
    ctx.fillStyle = '#0f9d58';
    ctx.beginPath();
    ctx.roundRect(40, S - 130, S - 80, 90, 24);
    ctx.fill();
    ctx.fillStyle = '#fff';
    ctx.font = 'bold 38px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.fillText('Pet & Na — AI 반려동물 케어 올인원', S / 2, S - 72);

    return canvas;
}

// 산책 기분 옵션 (원탭 이모지 토글)
const WALK_MOODS = [
    { key: 'happy', emoji: '😄', label: '신남' },
    { key: 'calm', emoji: '😌', label: '편안' },
    { key: 'tired', emoji: '😴', label: '지침' },
    { key: 'anxious', emoji: '😰', label: '불안' },
];

let _pendingWalkMood = null;
function _selectWalkMood(key, el) {
    _pendingWalkMood = (_pendingWalkMood === key) ? null : key;
    document.querySelectorAll('.walk-mood-opt').forEach(b =>
        b.classList.remove('ring-2', 'ring-brand-500', 'bg-brand-50'));
    if (_pendingWalkMood && el) el.classList.add('ring-2', 'ring-brand-500', 'bg-brand-50');
}

// 리포트 카드 생성 전 기분 이모지를 원탭으로 고르게 하는 다이얼로그
function _promptWalkMood(current, onDone) {
    _pendingWalkMood = current || null;
    if (typeof showCustomDialog !== 'function') { onDone(current || null); return; }
    const opts = WALK_MOODS.map(m => `
        <button type="button" class="walk-mood-opt flex flex-col items-center gap-1 py-2 rounded-xl border border-gray-100 transition-all ${current === m.key ? 'ring-2 ring-brand-500 bg-brand-50' : ''}" onclick="_selectWalkMood('${m.key}', this)">
            <span class="text-2xl">${m.emoji}</span>
            <span class="text-[10px] font-bold text-gray-600">${m.label}</span>
        </button>`).join('');
    showCustomDialog({
        title: '오늘 산책 기분은? 🐾',
        message: `<div class="text-[11px] text-gray-400 mb-2">기분을 골라 카드에 담아보세요 (선택 안 함도 가능)</div><div class="grid grid-cols-4 gap-2">${opts}</div>`,
        icon: '🎨',
        type: 'confirm',
        allowHtml: true,
        onConfirm: () => onDone(_pendingWalkMood),
    });
}

async function _renderAndShareWalkCard(w) {
    const pet = typeof getActivePet === 'function' ? getActivePet() : null;
    const petName = pet?.name || '댕이';
    const canvas = generateWalkReportCard(w);
    const aiComment = _walkAiComment(w, petName);
    const moodMeta = WALK_MOODS.find(m => m.key === w.mood);
    const moodTxt = moodMeta ? ` ${moodMeta.emoji}${moodMeta.label}` : '';
    await _downloadOrShare(
        canvas, 'petna-walk-report.png',
        `${petName}의 산책 리포트 🦮`,
        `🦮 ${petName}와 ${w.distance}km 산책 완료!${moodTxt} ⏱️${w.duration || ''} 🔥${w.calories}kcal\n🤖 ${aiComment} #펫과나 #산책 #반려견산책`
    );
}

function shareWalkReportCard(walkId) {
    const list = (typeof walks !== 'undefined' && Array.isArray(walks)) ? walks : [];
    const w = list.find(item => item.id === walkId);
    if (!w) {
        if (typeof showToast === 'function') showToast('산책 기록을 찾을 수 없어요');
        return;
    }
    _promptWalkMood(w.mood, (mood) => {
        w.mood = mood || undefined;
        if (typeof saveState === 'function') saveState();
        _renderAndShareWalkCard(w);
    });
}
