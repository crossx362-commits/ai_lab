// wellness-anomaly.js — 예측 웰니스 이상감지 (오너 승인 2026-07-10)
// healthLogs.history의 음수량(water)·식사량(food)을 z-score로 분석해
// '평소보다 급증/급감' 미세변화(micro-shift)를 부드럽게 감지한다.
// 신규 인프라 없이 순수 JS 통계. 서버 이력은 health_logs 테이블에서 동기화(있으면).
//
// 가드레일(회의 결정 2026-07-09):
//  - 기준표본 N<14면 알림 억제(표본 부족 오탐 방지)
//  - health-dashboard 카드 단일 채널 + 확정 이상만 토스트(하루 1회)
(function () {
    'use strict';

    const MIN_SAMPLES = 14;   // 기준 표본 하한(이하면 판정·알림 억제)
    const RECENT_DAYS = 3;    // 최근 관찰 창(일)
    const Z_THRESHOLD = 2.0;  // 이상 판정 z 임계

    const METRICS = [
        { key: 'water', label: '음수량', unit: 'ml', emoji: '💧' },
        { key: 'food',  label: '식사량', unit: 'g',  emoji: '🍚' },
    ];

    // 배변 건강 신호(핏펫 '어헤드' 벤치마크, 백로그 나무 제안):
    // 'normal' 외 굳기(soft/hard/liquid)를 이상변으로 보고, 최근 기록이
    // STOOL_STREAK일 연속 이상이면 wellness 신호로 전달한다. z-score와 달리
    // 표본 하한 없이 소수 기록으로도 조기 감지(설사·변비는 빠른 대응이 중요).
    const STOOL_STREAK = 3;
    const STOOL_ABNORMAL = {
        soft:   { label: '무른 변', emoji: '💩' },
        hard:   { label: '딱딱한 변(변비)', emoji: '🪨' },
        liquid: { label: '설사', emoji: '💦' },
    };

    // 소변 색 셀프체크(daily-condition.js urine 필드 연동, 백로그 나무 제안
    // '홈 셀프 건강검사 트래커'): 붉은색(혈뇨)·진한색(탈수·농축뇨)은 급성 신호라
    // 배변 연속(STOOL_STREAK)과 달리 가장 최근 기록 1건만 이상이어도 즉시 감지한다.
    const URINE_ABNORMAL = {
        red:  { label: '붉은 소변(혈뇨 의심)', emoji: '🔴' },
        dark: { label: '진한 소변(탈수·농축 의심)', emoji: '🟠' },
    };

    // 데일리 컨디션 원탭 로그(daily-condition.js) 연결(백로그 나무 제안):
    // 식욕·활력이 최근 CONDITION_STREAK일 연속 '저하(low)'면 조기 신호로 본다.
    // 배변과 동일하게 표본 하한 없이 소수 기록으로도 감지한다.
    const CONDITION_STREAK = 3;
    const CONDITION_ABNORMAL = {
        appetite: { low: { label: '식욕 저하', emoji: '😔' } },
        activity: { low: { label: '활력 저하', emoji: '😴' } },
    };

    // 체중 급변 감지(백로그 나무 제안 '체중·BCS 장수 추세선'):
    // getWeightHistory()의 최근 체중이 직전 기준(직전 N회 평균)보다 WEIGHT_ALERT_PCT%
    // 이상 급변하면 조기 신호로 본다. 체중은 자주 안 재므로 z-score(일간 표본) 대신
    // 배변·컨디션처럼 소수 기록으로도 감지하는 별도 로직을 쓴다. 주간 리포트
    // (weekly-report.js)와 달리 '확정 급변'만 토스트 알림까지 전달한다.
    const WEIGHT_MIN_RECORDS = 3;   // 최소 기록 수(최근 1 + 기준 2 이상)
    const WEIGHT_BASELINE_N = 3;    // 최근값 직전, 기준으로 삼을 최대 기록 수
    const WEIGHT_ALERT_PCT = 7;     // 기준 대비 이 % 이상 급변 시 경고

    function _mean(a) { return a.reduce((s, x) => s + x, 0) / a.length; }
    function _std(a, m) { return Math.sqrt(a.reduce((s, x) => s + (x - m) * (x - m), 0) / a.length); }

    // history에서 유효한 metric 값 시퀀스(날짜순). 0/null/비숫자는 미기록으로 제외.
    function _series(history, key) {
        return (history || [])
            .filter(d => d && typeof d[key] === 'number' && d[key] > 0)
            .map(d => d[key]);
    }

    // 순수 함수: history 배열 → 이상 소견 배열. 테스트/재사용 용이.
    function analyzeWellness(history) {
        const findings = [];
        for (const m of METRICS) {
            const s = _series(history, m.key);
            if (s.length < MIN_SAMPLES + RECENT_DAYS) continue;   // 표본 부족 → 억제
            const recent = s.slice(-RECENT_DAYS);
            const baseline = s.slice(0, -RECENT_DAYS);
            if (baseline.length < MIN_SAMPLES) continue;
            const bMean = _mean(baseline);
            const bStd = _std(baseline, bMean);
            if (bStd < 1e-6) continue;                            // 변동 없음 → 판정 불가
            const rMean = _mean(recent);
            const z = (rMean - bMean) / bStd;
            if (Math.abs(z) >= Z_THRESHOLD) {
                findings.push({
                    metric: m.key, label: m.label, unit: m.unit, emoji: m.emoji,
                    direction: z > 0 ? 'up' : 'down',
                    z: Math.round(z * 10) / 10,
                    recentAvg: Math.round(rMean),
                    baselineAvg: Math.round(bMean),
                    pct: Math.round(((rMean - bMean) / bMean) * 100),
                    samples: baseline.length,
                });
            }
        }
        return findings;
    }

    // 순수 함수: history → 배변 이상 소견(없으면 null).
    // 배변 기록이 있는 항목만 날짜 내림차순으로 보고, 가장 최근부터 이어지는
    // 이상변 연속 횟수가 STOOL_STREAK 이상이면 소견을 반환한다.
    function analyzeStool(history) {
        const dated = (history || [])
            .filter(d => d && d.poop && (d.poop === 'normal' || STOOL_ABNORMAL[d.poop]))
            .slice()
            .sort((a, b) => (b.date || '').localeCompare(a.date || ''));
        let streak = 0;
        let type = null;
        for (const d of dated) {
            if (STOOL_ABNORMAL[d.poop]) { streak++; if (!type) type = d.poop; }
            else break;
        }
        if (streak < STOOL_STREAK) return null;
        const info = STOOL_ABNORMAL[type];
        return { metric: 'poop', poop: type, label: info.label, emoji: info.emoji, days: streak };
    }

    // 순수 함수: history → 소변 색 이상 소견(없으면 null).
    // 가장 최근 소변 기록이 이상색이면 즉시 보고(혈뇨·농축뇨는 조기 대응이 중요).
    function analyzeUrine(history) {
        const dated = (history || [])
            .filter(d => d && d.urine && (d.urine === 'normal' || URINE_ABNORMAL[d.urine]))
            .slice()
            .sort((a, b) => (b.date || '').localeCompare(a.date || ''));
        if (!dated.length) return null;
        const latest = dated[0].urine;
        if (!URINE_ABNORMAL[latest]) return null;
        const info = URINE_ABNORMAL[latest];
        return { metric: 'urine', urine: latest, label: info.label, emoji: info.emoji };
    }

    // 순수 함수: history → 식욕·활력 저하 소견 배열(없으면 빈 배열).
    // 각 필드에서 가장 최근부터 이어지는 'low' 연속이 CONDITION_STREAK 이상이면 보고.
    function analyzeCondition(history) {
        const findings = [];
        for (const field of ['appetite', 'activity']) {
            const map = CONDITION_ABNORMAL[field];
            const dated = (history || [])
                .filter(d => d && d[field])
                .slice()
                .sort((a, b) => (b.date || '').localeCompare(a.date || ''));
            let streak = 0;
            let val = null;
            for (const d of dated) {
                if (map[d[field]]) { streak++; if (!val) val = d[field]; }
                else break;
            }
            if (streak >= CONDITION_STREAK && val) {
                const info = map[val];
                findings.push({ metric: field, field, value: val, label: info.label, emoji: info.emoji, days: streak });
            }
        }
        return findings;
    }

    // 순수 함수: weightHistory → 체중 급변 소견(없으면 null).
    // 날짜 오름차순 정렬 후 마지막(최근) 체중을 직전 최대 WEIGHT_BASELINE_N회 평균과
    // 비교해 변화율이 WEIGHT_ALERT_PCT 이상이면 소견 반환.
    function analyzeWeight(weightHistory) {
        const rows = (weightHistory || [])
            .filter(d => d && typeof d.weight === 'number' && d.weight > 0)
            .slice()
            .sort((a, b) => (a.date || '').localeCompare(b.date || ''));
        if (rows.length < WEIGHT_MIN_RECORDS) return null;
        const latest = rows[rows.length - 1].weight;
        const prior = rows.slice(-1 - WEIGHT_BASELINE_N, -1).map(r => r.weight);
        if (prior.length < WEIGHT_MIN_RECORDS - 1) return null;
        const base = _mean(prior);
        if (base <= 0) return null;
        const pct = ((latest - base) / base) * 100;
        if (Math.abs(pct) < WEIGHT_ALERT_PCT) return null;
        return {
            metric: 'weight',
            label: '체중',
            emoji: '⚖️',
            direction: pct > 0 ? 'up' : 'down',
            pct: Math.round(pct),
            latest: Math.round(latest * 10) / 10,
            baseline: Math.round(base * 10) / 10,
        };
    }

    function _weightText(f) {
        const dir = f.direction === 'up' ? '늘었어요' : '줄었어요';
        const arrow = f.direction === 'up' ? '급증' : '급감';
        return `최근 체중이 평소보다 ${Math.abs(f.pct)}% ${dir} ` +
               `(평균 ${f.baseline}kg → ${f.latest}kg, ${arrow}). 급격한 체중 변화는 건강 신호일 수 있어요`;
    }

    function _stoolText(f) {
        return `최근 ${f.days}일 연속 ${f.label} 기록이 있어요. 지속되면 수의사 상담을 권해요`;
    }

    function _urineText(f) {
        return `가장 최근 ${f.label} 기록이 있어요. 지속되면 수의사 상담을 권해요`;
    }

    function _conditionText(f) {
        return `최근 ${f.days}일 연속 ${f.label} 기록이 있어요. 컨디션을 살펴봐 주세요`;
    }

    function _findingText(f) {
        const dir = f.direction === 'up' ? '높아요' : '낮아요';
        const arrow = f.direction === 'up' ? '급증' : '급감';
        return `최근 ${RECENT_DAYS}일 ${f.label}이 평소보다 ${Math.abs(f.pct)}% ${dir} ` +
               `(평균 ${f.baselineAvg}${f.unit} → ${f.recentAvg}${f.unit}, ${arrow})`;
    }

    function _sampleCount() {
        const h = (typeof healthLogs !== 'undefined' && healthLogs) ? healthLogs.history : [];
        return METRICS.reduce((mx, m) => Math.max(mx, _series(h, m.key).length), 0);
    }

    // 카드 렌더 (health-dashboard 단일 채널)
    function renderWellnessCard() {
        const host = document.getElementById('wellness-anomaly-card');
        if (!host) return;
        const history = (typeof healthLogs !== 'undefined' && healthLogs) ? healthLogs.history : [];
        const weightHistory = (typeof getWeightHistory === 'function') ? getWeightHistory() : [];
        const findings = analyzeWellness(history);
        const stool = analyzeStool(history);
        const urine = analyzeUrine(history);
        const conditions = analyzeCondition(history);
        const weight = analyzeWeight(weightHistory);
        const samples = _sampleCount();

        // 배변·소변·컨디션·체중 급변은 표본 하한과 무관하게 조기 감지 — z-score 표본이
        // 부족해도 이상변/저하 연속·체중 급변이면 경고 카드로 바로 노출한다.
        if (samples < MIN_SAMPLES + RECENT_DAYS && !stool && !urine && !conditions.length && !weight) {
            // 표본 부족 — 조용히 안내(알림 없음)
            host.innerHTML = `
            <div class="card-modern p-5 border border-brand-100/60">
                <div class="flex items-center gap-3">
                    <div class="text-2xl">🔮</div>
                    <div class="min-w-0">
                        <h3 class="text-sm font-bold text-gray-900">예측 웰니스</h3>
                        <p class="text-xs text-gray-500 mt-0.5">
                            건강 기록이 ${MIN_SAMPLES + RECENT_DAYS}일 이상 쌓이면 평소와 다른 미세 변화를 자동으로 감지해요.
                            <span class="text-brand-600 font-semibold">(현재 ${samples}일)</span>
                        </p>
                    </div>
                </div>
            </div>`;
            return;
        }

        if (findings.length === 0 && !stool && !urine && !conditions.length && !weight) {
            host.innerHTML = `
            <div class="card-modern p-5 border border-emerald-100">
                <div class="flex items-center gap-3">
                    <div class="text-2xl">🔮</div>
                    <div class="min-w-0 flex-1">
                        <h3 class="text-sm font-bold text-gray-900">예측 웰니스 · <span class="text-emerald-600">안정적</span></h3>
                        <p class="text-xs text-gray-500 mt-0.5">최근 ${RECENT_DAYS}일 음수·식사량이 평소 범위 안에 있어요. 계속 잘 돌보고 계세요! 🐾</p>
                    </div>
                    <span class="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold bg-emerald-50 text-emerald-700">이상 없음</span>
                </div>
            </div>`;
            return;
        }

        const stoolItem = stool ? `
            <li class="flex items-start gap-2 text-sm text-amber-900">
                <span class="mt-0.5">${stool.emoji}</span>
                <span>${_stoolText(stool)}</span>
            </li>` : '';
        const urineItem = urine ? `
            <li class="flex items-start gap-2 text-sm text-amber-900">
                <span class="mt-0.5">${urine.emoji}</span>
                <span>${_urineText(urine)}</span>
            </li>` : '';
        const weightItem = weight ? `
            <li class="flex items-start gap-2 text-sm text-amber-900">
                <span class="mt-0.5">${weight.emoji}</span>
                <span>${_weightText(weight)}</span>
            </li>` : '';
        const conditionItems = conditions.map(c => `
            <li class="flex items-start gap-2 text-sm text-amber-900">
                <span class="mt-0.5">${c.emoji}</span>
                <span>${_conditionText(c)}</span>
            </li>`).join('');
        const items = stoolItem + urineItem + weightItem + conditionItems + findings.map(f => `
            <li class="flex items-start gap-2 text-sm text-amber-900">
                <span class="mt-0.5">${f.emoji}</span>
                <span>${_findingText(f)}</span>
            </li>`).join('');
        // 벳챗 유도: 이상 소견이 있을 때 AI 수의사 상담 CTA(백로그 나무 '홈 셀프
        // 건강검사 트래커 — 이상 징후 시 벳챗 유도'). openVetChatModal은 vet-chat.js.
        const vetCta = (typeof openVetChatModal === 'function') ? `
            <button type="button" onclick="openVetChatModal()"
                class="mt-3 w-full rounded-xl bg-amber-500 hover:bg-amber-600 text-white py-2 text-xs font-bold transition-colors">
                🩺 AI 수의사에게 상담하기
            </button>` : '';
        host.innerHTML = `
        <div class="card-modern p-5 border border-amber-200 bg-amber-50/40">
            <div class="flex items-center gap-3 mb-2">
                <div class="text-2xl">⚠️</div>
                <div class="min-w-0 flex-1">
                    <h3 class="text-sm font-bold text-amber-900">평소와 다른 변화가 감지됐어요</h3>
                    <p class="text-xs text-amber-700/80 mt-0.5">이상 징후가 아닐 수 있지만, 최근 컨디션을 한 번 살펴봐 주세요.</p>
                </div>
            </div>
            <ul class="space-y-1.5 pl-1">${items}</ul>
            ${vetCta}
        </div>`;

        _maybeNotify(findings, stool, urine, conditions, weight);
    }

    // 확정 이상 → 토스트 1회(하루 1회 억제). 배변 > 소변 > 체중 > 컨디션 > z 순으로 우선 노출.
    function _maybeNotify(findings, stool, urine, conditions, weight) {
        conditions = conditions || [];
        if (!stool && !urine && !weight && !conditions.length && !findings.length) return;
        const today = (typeof healthLogs !== 'undefined' && healthLogs && healthLogs.today && healthLogs.today.date)
            || new Date().toISOString().slice(0, 10);
        const flagKey = 'petna_wellness_alerted_' + today;
        try { if (localStorage.getItem(flagKey)) return; localStorage.setItem(flagKey, '1'); } catch (e) { return; }
        const msg = stool ? stool.emoji + ' ' + _stoolText(stool)
            : urine ? urine.emoji + ' ' + _urineText(urine)
            : weight ? weight.emoji + ' ' + _weightText(weight)
            : conditions.length ? conditions[0].emoji + ' ' + _conditionText(conditions[0])
            : '🔮 ' + _findingText(findings[0]);
        if (typeof showToast === 'function') showToast(msg);
    }

    window.analyzeWellness = analyzeWellness;
    window.analyzeStool = analyzeStool;
    window.analyzeUrine = analyzeUrine;
    window.analyzeCondition = analyzeCondition;
    window.analyzeWeight = analyzeWeight;
    window.renderWellnessCard = renderWellnessCard;
})();
