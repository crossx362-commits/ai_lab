// 펫게임 스테이지 — DOM 렌더/연출. 로직은 PetGameCore에 위임.
(function (root) {
    const Core = root.PetGameCore, Items = root.PetGameItems;
    const S = { space: 'yard', mode: 'play', rootId: null, timers: [] };

    function pet() { return (typeof getActivePet === 'function') ? getActivePet() : null; }
    function toast(m) { if (typeof showToast === 'function') showToast(m); else console.log('[petgame]', m); }
    function esc(x) { return String(x ?? '').replace(/</g, '&lt;'); }

    // 이미지 로드 실패 → 이모지 폴백 (전역 constraint)
    function imgOrEmoji(src, emoji, cls, px) {
        return `<img src="${src}" class="${cls}" style="width:${px}px" draggable="false"
                 onerror="this.outerHTML='<span class=&quot;${cls}&quot; style=&quot;font-size:${Math.round(px * 0.8)}px;line-height:1&quot;>${emoji}</span>'">`;
    }

    function petSprite(p, px) {
        const g = Core.ensureGame(p);
        const st = Items.stageForLevel(g.level);
        const type = ['dog', 'cat', 'rabbit', 'hamster'].includes(p.type) ? p.type : 'dog';
        const fallback = { dog: '🐶', cat: '🐱', rabbit: '🐰', hamster: '🐹' }[type];
        const size = Math.round(px * st.scale);
        return imgOrEmoji(`images/petgame/pet/${type}_${st.stage}.png`, fallback, 'pg-pet-img', size);
    }

    function hudHTML(p, g) {
        const st = Items.stageForLevel(g.level);
        const need = Core.xpForLevel(g.level);
        const xpPct = g.level >= 10 ? 100 : Math.min(100, Math.round(g.xp / need * 100));
        return `
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2">
            <span class="font-black text-[15px]">${esc(p.name)}</span>
            <span class="text-[10px] font-bold text-white bg-brand-500 px-2 py-0.5 rounded-full">Lv.${g.level} ${st.name}</span>
          </div>
          <span class="text-[12px] font-black text-amber-700 bg-amber-50 px-2.5 py-1 rounded-full">🐾 ${p.pawCoins || 0}</span>
        </div>
        <div class="mt-1.5 h-[6px] bg-brand-100 rounded-full overflow-hidden"><div class="h-full bg-brand-500 rounded-full" style="width:${xpPct}%"></div></div>
        <div class="flex gap-3 mt-1.5 text-[10px] font-bold text-gray-500">
          <span class="flex-1 flex items-center gap-1">🍖<span class="flex-1 h-[4px] bg-gray-100 rounded-full overflow-hidden"><span class="block h-full bg-amber-400 rounded-full" style="width:${g.hunger}%"></span></span></span>
          <span class="flex-1 flex items-center gap-1">💗<span class="flex-1 h-[4px] bg-gray-100 rounded-full overflow-hidden"><span class="block h-full bg-pink-400 rounded-full" style="width:${g.happy}%"></span></span></span>
        </div>`;
    }

    function itemsOf(g) { return S.space === 'room' ? g.roomItems : g.yardItems; }

    function stageHTML(p, g) {
        const themeId = S.space === 'room' ? g.roomTheme : g.yardTheme;
        const theme = Items.THEMES.find(t => t.id === themeId) || Items.THEMES[0];
        const placed = itemsOf(g).map((e, idx) => {
            const it = e.emoji ? null : Items.getItem(e.id);
            const inner = e.emoji
                ? `<span style="font-size:${e.size}px;line-height:1">${e.emoji}</span>`
                : imgOrEmoji(it.img, it.emoji, '', e.size);
            return `<div class="pg-item absolute select-none ${S.mode === 'decorate' ? 'pg-editable' : ''}"
                      data-idx="${idx}" style="left:${e.x}%;top:${e.y}%;transform:translate(-50%,-100%)">${inner}</div>`;
        }).join('');
        const mood = g.hunger < 30 ? `<div class="pg-bubble">배고파요…🥺</div>` : '';
        return `
        <div id="pg-stage" class="relative w-full rounded-2xl overflow-hidden" style="aspect-ratio:4/3;background:#cde9f2 url('${theme.img}') center/cover">
          ${placed}
          <div id="pg-pet" class="absolute" style="left:50%;top:78%;transform:translate(-50%,-100%);transition:left 2.5s ease-in-out">
            ${mood}${petSprite(p, 150)}
          </div>
          <div id="pg-fx" class="absolute inset-0 pointer-events-none"></div>
        </div>`;
    }

    function actionsHTML() {
        const on = (m) => S.mode === m ? 'bg-brand-500 text-white' : 'bg-white border border-brand-100 text-gray-700';
        return `
        <div class="flex gap-2 mt-2">
          <button onclick="PetGame.openFeed()" class="flex-[1.3] py-2.5 rounded-xl font-black text-[13px] bg-brand-500 text-white shadow-sm">🦴 먹이주기</button>
          <button onclick="PetGame.openShop()" class="flex-1 py-2.5 rounded-xl font-black text-[13px] bg-white border border-brand-100 text-gray-700">🛒 상점</button>
          <button onclick="PetGame.toggleDecorate()" class="flex-1 py-2.5 rounded-xl font-black text-[13px] ${on('decorate')}">🎨 꾸미기</button>
        </div>
        <div id="pg-sheet"></div>`;
    }

    function refresh() {
        const p = pet(); if (!p || !S.rootId) return;
        const rootEl = document.getElementById(S.rootId); if (!rootEl) return;
        const g = Core.ensureGame(p);
        const tab = (sp, label) => `<button onclick="PetGame.setSpace('${sp}')"
            class="flex-1 py-1.5 rounded-xl text-[12px] font-bold ${S.space === sp ? 'bg-brand-500 text-white' : 'bg-brand-50 text-gray-500'}">${label}</button>`;
        rootEl.innerHTML = `
          <div class="bg-white rounded-2xl border border-brand-100 p-3.5 shadow-soft">
            <div id="pg-hud">${hudHTML(p, g)}</div>
            <div class="flex gap-1.5 mt-2.5">${tab('room', '🏠 방')}${tab('yard', '🌳 마당')}</div>
            <div class="mt-2">${stageHTML(p, g)}</div>
            ${actionsHTML()}
          </div>`;
        if (S.mode === 'decorate') bindDecorate();
    }

    function setSpace(sp) { S.space = sp; S.mode = 'play'; refresh(); }

    // 펫 산책 루프 — 스테이지 안에서 좌우로 이동
    function wanderTick() {
        const el = document.getElementById('pg-pet');
        if (el && S.mode === 'play') el.style.left = (25 + Math.random() * 50).toFixed(0) + '%';
    }

    function mount(rootId) {
        S.rootId = rootId;
        Core.init({ saveFn: root.PetGameSave || (() => {
            try { localStorage.setItem('petna_pets', JSON.stringify(root.pets || [])); } catch (e) {}
        }) });
        const p = pet(); if (p) Core.ensureGame(p);
        S.timers.forEach(clearInterval); S.timers = [];
        S.timers.push(setInterval(wanderTick, 4000));
        S.timers.push(setInterval(() => { const q = pet(); if (q) { Core.decay(q, 1 / 60); updateHud(); } }, 60000));
        refresh();
    }

    function updateHud() {
        const p = pet(); if (!p) return;
        const el = document.getElementById('pg-hud');
        if (el) el.innerHTML = hudHTML(p, Core.ensureGame(p));
    }

    function earnCare(type, amount) {
        const p = pet(); if (!p) return null;
        const r = Core.earnCare(p, type, amount);
        if (r) { toast(r.msg); updateHud(); }
        return r;
    }

    root.PetGame = Object.assign(root.PetGame || {}, {
        mount, refresh, setSpace, earnCare, updateHud, state: S,
        // Task 5·6에서 구현 — 미구현 호출 시 안내
        openFeed() { toast('준비 중'); }, openShop() { toast('준비 중'); }, toggleDecorate() { toast('준비 중'); },
    });
    function bindDecorate() {} // Task 6에서 교체
    root.PetGameStageInternals = { bindDecorate: (fn) => { bindDecorate = fn; } };
})(typeof window !== 'undefined' ? window : globalThis);
