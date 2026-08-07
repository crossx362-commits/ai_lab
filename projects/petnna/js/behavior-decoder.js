// behavior-decoder.js — 🐾 행동 큐 감정 디코더 (백로그 나무 제안, P2 기획)
// ─────────────────────────────────────────────────────────────
// 사용자가 관찰한 행동 큐(꼬리·귀·자세·발성)를 선택하면 규칙 기반으로
// 감정 상태와 대응법을 리포트한다. 각 판독 결과는 localStorage에 누적돼
// 감정 로그가 된다(신규 DB 테이블 없음 — inventory-tracker·expense-tracker와
// 동일 원칙, 서버 동기화는 후속 과제). 데일리 컨디션(daily-condition.js)과
// 같은 '오늘의 기록' 카드 영역에 나란히 놓여 그날의 몸·마음 상태를 함께 남긴다.
// ─────────────────────────────────────────────────────────────
(function () {
    'use strict';

    var LS_KEY = 'petna_behavior_emotion_log'; // [{ id, date, ts, petId, cues:{tail,ears,posture,vocal}, emotion }]
    var MAX_LOG = 60;

    // 감정 축: 규칙 기반 점수 합산 → 최댓값 감정을 채택한다.
    var EMOTIONS = {
        joy:     { emoji: '😄', label: '기쁨·신남', tone: 'emerald',
                   report: '지금 신나고 행복한 상태예요! 함께 놀아주거나 산책으로 에너지를 풀어주면 좋아요.' },
        calm:    { emoji: '😌', label: '편안·안정', tone: 'brand',
                   report: '편안하고 안정된 상태예요. 조용히 곁을 지켜주면 신뢰가 더 깊어져요.' },
        anxious: { emoji: '😰', label: '불안·두려움', tone: 'amber',
                   report: '불안하거나 두려움을 느끼고 있어요. 자극을 줄이고 안전한 공간을 마련해 주세요. 억지로 안거나 다가가지 마세요.' },
        alert:   { emoji: '😠', label: '경계·흥분', tone: 'rose',
                   report: '경계하거나 흥분한 상태예요. 무리하게 만지지 말고 거리를 두고 진정될 때까지 기다려 주세요.' },
    };

    // 큐 그룹. 각 옵션은 감정 축별 가중치(scores)를 갖는다.
    var GROUPS = [
        { field: 'tail', label: '꼬리', icon: '🐕', options: [
            { value: 'high-wag', emoji: '🙌', text: '높이 흔듦',   scores: { joy: 2 } },
            { value: 'low-slow',  emoji: '↩️', text: '낮게 천천히', scores: { anxious: 1, alert: 1 } },
            { value: 'tucked',    emoji: '⬇️', text: '다리 사이',   scores: { anxious: 2 } },
            { value: 'stiff-up',  emoji: '⬆️', text: '뻣뻣이 세움', scores: { alert: 2 } },
        ] },
        { field: 'ears', label: '귀', icon: '👂', options: [
            { value: 'forward', emoji: '📡', text: '쫑긋 앞으로', scores: { joy: 1, alert: 1 } },
            { value: 'neutral', emoji: '😊', text: '편안하게',    scores: { calm: 2 } },
            { value: 'back',    emoji: '🔙', text: '뒤로 눕힘',   scores: { anxious: 2 } },
        ] },
        { field: 'posture', label: '자세', icon: '🧍', options: [
            { value: 'play-bow', emoji: '🤸', text: '놀이 인사', scores: { joy: 2 } },
            { value: 'relaxed',  emoji: '🛋️', text: '배 보이고 편안', scores: { calm: 2 } },
            { value: 'crouch',   emoji: '🫣', text: '웅크림',    scores: { anxious: 2 } },
            { value: 'stiff',    emoji: '🗿', text: '뻣뻣이 정지', scores: { alert: 2 } },
        ] },
        { field: 'vocal', label: '발성', icon: '🔊', options: [
            { value: 'none',     emoji: '🤫', text: '조용',   scores: { calm: 1 } },
            { value: 'excited',  emoji: '🗣️', text: '신난 짖음', scores: { joy: 1, alert: 1 } },
            { value: 'whine',    emoji: '😢', text: '낑낑',   scores: { anxious: 2 } },
            { value: 'growl',    emoji: '😤', text: '으르렁', scores: { alert: 2 } },
        ] },
    ];

    var _hostId = 'behavior-decoder-widget';
    var _sel = {}; // { tail, ears, posture, vocal } — 현재 선택

    function esc(s) {
        if (typeof window.escapeHtml === 'function') return window.escapeHtml(s);
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }
    function loadLog() { try { return JSON.parse(localStorage.getItem(LS_KEY)) || []; } catch (e) { return []; } }
    function saveLog(list) { try { localStorage.setItem(LS_KEY, JSON.stringify(list)); } catch (e) { } }

    // 선택된 큐로 감정 점수를 합산해 최댓값 감정 키를 돌려준다. 동점이면 선언 순서 우선.
    function decode(sel) {
        var totals = { joy: 0, calm: 0, anxious: 0, alert: 0 };
        var any = false;
        GROUPS.forEach(function (g) {
            var v = sel[g.field];
            if (!v) return;
            var opt = g.options.find(function (o) { return o.value === v; });
            if (!opt) return;
            any = true;
            Object.keys(opt.scores).forEach(function (k) { totals[k] += opt.scores[k]; });
        });
        if (!any) return null;
        var best = null, bestScore = -1;
        Object.keys(EMOTIONS).forEach(function (k) {
            if (totals[k] > bestScore) { bestScore = totals[k]; best = k; }
        });
        return best;
    }

    function _btn(field, opt, selected) {
        var on = selected
            ? 'border-brand-500 bg-brand-50 text-brand-700 ring-2 ring-brand-200 shadow-sm'
            : 'border-gray-200 bg-white text-gray-600 hover:border-brand-300';
        return '<button type="button" aria-pressed="' + (selected ? 'true' : 'false') + '"' +
            ' onclick="BehaviorDecoder.set(\'' + field + '\',\'' + opt.value + '\')"' +
            ' class="flex-1 flex flex-col items-center gap-0.5 py-2 rounded-xl border transition-all outline-none ' + on + '">' +
            '<span class="text-lg">' + opt.emoji + '</span>' +
            '<span class="text-[11px] font-bold">' + esc(opt.text) + '</span>' +
            '</button>';
    }

    function _rows() {
        return GROUPS.map(function (g) {
            return '<div>' +
                '<div class="flex items-center gap-1.5 mb-1.5">' +
                '<span class="text-sm">' + g.icon + '</span>' +
                '<span class="text-xs font-bold text-gray-700">' + esc(g.label) + '</span>' +
                '</div>' +
                '<div class="flex gap-2">' +
                g.options.map(function (o) { return _btn(g.field, o, _sel[g.field] === o.value); }).join('') +
                '</div></div>';
        }).join('');
    }

    // 판독 결과 배너 + 대응법. 선택이 하나도 없으면 안내만 표시.
    function _resultBlock() {
        var key = decode(_sel);
        if (!key) {
            return '<div class="mt-3 text-xs text-gray-400 text-center py-2">위에서 관찰한 행동을 골라 보세요.</div>';
        }
        var e = EMOTIONS[key];
        return '<div class="mt-3 rounded-xl border border-' + e.tone + '-200 bg-' + e.tone + '-50 p-3">' +
            '<div class="flex items-center gap-2 mb-1">' +
            '<span class="text-2xl">' + e.emoji + '</span>' +
            '<span class="text-sm font-bold text-' + e.tone + '-700">' + esc(e.label) + '</span>' +
            '</div>' +
            '<p class="text-xs text-gray-600 leading-relaxed">' + esc(e.report) + '</p>' +
            '<button type="button" onclick="BehaviorDecoder.save()"' +
            ' class="w-full mt-2 py-2 rounded-xl bg-brand-500 text-white text-xs font-bold hover:bg-brand-600 transition-colors outline-none">' +
            '📌 감정 로그로 저장</button>' +
            '</div>';
    }

    function renderWidget(hostId) {
        if (hostId) _hostId = hostId;
        var host = document.getElementById(_hostId);
        if (!host) return;
        var log = loadLog();
        host.innerHTML =
            '<div class="pt-4 border-t border-gray-100">' +
            '<div class="flex items-center gap-2 mb-3">' +
            '<span class="text-xl">🐾</span>' +
            '<span class="text-sm font-bold text-gray-900">행동 큐 감정 디코더</span>' +
            '<span class="text-[10px] font-bold text-brand-500 bg-brand-50 px-1.5 py-0.5 rounded">분석</span>' +
            (log.length ? '<span class="text-xs text-gray-400 truncate">기록 ' + log.length + '회</span>' : '') +
            '</div>' +
            '<div class="grid grid-cols-1 sm:grid-cols-2 gap-3">' + _rows() + '</div>' +
            _resultBlock() +
            '</div>';
    }

    // 큐 선택(같은 값 다시 누르면 해제).
    function set(field, value) {
        _sel[field] = (_sel[field] === value) ? null : value;
        renderWidget(_hostId);
    }

    // 현재 판독 결과를 감정 로그에 누적 저장.
    function save() {
        var key = decode(_sel);
        if (!key) return;
        var pet = (typeof getActivePet === 'function') ? getActivePet() : null;
        var log = loadLog();
        log.unshift({
            id: 'be_' + Date.now(),
            date: new Date().toISOString().slice(0, 10),
            ts: Date.now(),
            petId: pet ? (pet.id || null) : null,
            cues: { tail: _sel.tail || null, ears: _sel.ears || null, posture: _sel.posture || null, vocal: _sel.vocal || null },
            emotion: key,
        });
        if (log.length > MAX_LOG) log.splice(MAX_LOG);
        saveLog(log);
        _sel = {};
        renderWidget(_hostId);
        if (typeof showToast === 'function') showToast('감정 로그 저장 완료! ' + EMOTIONS[key].emoji);
    }

    window.BehaviorDecoder = { renderWidget: renderWidget, set: set, save: save, decode: decode };
})();
