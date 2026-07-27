// section-collapse.js — 긴 탭의 카드를 접었다 펼 수 있게 한다 (2026-07-27 오너 지시).
//
// 건강 탭이 데스크톱에서 3800px를 넘어 원하는 카드를 찾으려면 한참 스크롤해야 했다.
// 카드마다 헤더 바를 씌워 클릭으로 접고, 선택은 localStorage에 남긴다.
//
// 설계 원칙:
//  · 기존 카드의 DOM·id·리스너를 건드리지 않는다 — 카드를 '감싸기'만 한다
//    (노드를 옮겨도 이벤트 리스너는 따라간다). 내부를 고치면 각 위젯 렌더러가
//    innerHTML을 덮어쓸 때 접기 UI가 통째로 날아간다.
//  · 기본은 '펼침' — 처음 보는 사용자가 내용이 사라진 것으로 오해하지 않게.
//  · 탭 템플릿은 렌더될 때마다 새로 그려지므로 apply()는 여러 번 불려도
//    안전해야 한다(이미 감싼 카드는 건너뛴다).
(function () {
    'use strict';

    var LS_KEY = 'petna_collapsed_sections';   // { [sectionKey]: true }  — true면 접힘

    function loadState() {
        try { return JSON.parse(localStorage.getItem(LS_KEY)) || {}; } catch (e) { return {}; }
    }
    function saveState(map) {
        try { localStorage.setItem(LS_KEY, JSON.stringify(map)); } catch (e) { /* quota — 무시 */ }
    }

    function esc(s) {
        if (typeof window.escapeHtml === 'function') return window.escapeHtml(s);
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    // 카드에서 제목을 뽑는다. 못 뽑으면 감싸지 않는다(제목 없는 접기 바는 무의미).
    function titleOf(card) {
        var h = card.querySelector('h2,h3,h4');
        if (!h) return '';
        return (h.textContent || '').replace(/\s+/g, ' ').trim().slice(0, 30);
    }

    function toggle(key) {
        var section = document.querySelector('[data-collapse-key="' + key + '"]');
        if (!section) return;
        var state = loadState();
        var collapsed = !state[key];
        if (collapsed) state[key] = true; else delete state[key];
        saveState(state);
        paint(section, collapsed);
    }

    function paint(section, collapsed) {
        var body = section.querySelector('[data-collapse-body]');
        var icon = section.querySelector('[data-collapse-icon]');
        if (body) body.hidden = collapsed;
        if (icon) icon.style.transform = collapsed ? 'rotate(-90deg)' : 'rotate(0deg)';
        section.setAttribute('aria-expanded', String(!collapsed));
        var btn = section.querySelector('[data-collapse-btn]');
        if (btn) btn.setAttribute('aria-expanded', String(!collapsed));
    }

    // 카드 하나를 접기 섹션으로 감싼다. 이미 감싼 카드면 그대로 둔다.
    function wrap(card, key) {
        if (!card || !card.parentNode) return false;
        if (card.closest('[data-collapse-key]')) return false;   // 이미 감쌈
        var title = titleOf(card);
        if (!title) return false;

        var section = document.createElement('div');
        section.className = 'pn-collapse-section';
        section.setAttribute('data-collapse-key', key);

        var head = document.createElement('button');
        head.type = 'button';
        head.setAttribute('data-collapse-btn', '');
        head.className = 'w-full flex items-center justify-between gap-2 px-4 py-2 mb-1 '
            + 'text-left rounded-xl hover:bg-white/70 transition-colors outline-none';
        head.innerHTML =
            '<span class="text-xs font-black text-gray-500 truncate">' + esc(title) + '</span>'
            + '<i data-collapse-icon class="fa-solid fa-chevron-down text-gray-300 text-[10px] '
            + 'transition-transform shrink-0"></i>';
        head.onclick = function () { toggle(key); };

        var body = document.createElement('div');
        body.setAttribute('data-collapse-body', '');

        card.parentNode.insertBefore(section, card);
        section.appendChild(head);
        section.appendChild(body);
        body.appendChild(card);                                  // 노드 이동(리스너 보존)
        return true;
    }

    // 컨테이너의 최상위 카드들을 전부 접기 가능하게 만든다.
    // key는 카드 id가 있으면 그걸, 없으면 제목으로 만든다 — 재렌더 후에도 같은 카드가
    // 같은 key를 받아야 접힘 상태가 유지된다.
    function apply(containerId) {
        var host = document.getElementById(containerId);
        if (!host) return 0;
        var n = 0;
        [].slice.call(host.children).forEach(function (col) {
            [].slice.call(col.children).forEach(function (card) {
                if (card.classList.contains('pn-collapse-section')) return;
                var key = card.id || (containerId + ':' + titleOf(card));
                if (wrap(card, key)) n++;
            });
        });
        // 저장된 접힘 상태 반영
        var state = loadState();
        [].slice.call(host.querySelectorAll('[data-collapse-key]')).forEach(function (s) {
            paint(s, !!state[s.getAttribute('data-collapse-key')]);
        });
        return n;
    }

    function expandAll(containerId) {
        var state = loadState();
        var host = document.getElementById(containerId);
        if (!host) return;
        [].slice.call(host.querySelectorAll('[data-collapse-key]')).forEach(function (s) {
            delete state[s.getAttribute('data-collapse-key')];
            paint(s, false);
        });
        saveState(state);
    }

    window.SectionCollapse = { apply: apply, toggle: toggle, expandAll: expandAll, _loadState: loadState };
})();
