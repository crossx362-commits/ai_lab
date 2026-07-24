// memory-flashback.js — 홈 "추억 다시보기" 자동 회고 배너 (백로그 나무 제안, P3)
// 앨범(일기)·산책 데이터를 활용해 '1년 전 오늘' 또는 '이번 달 하이라이트'를
// 홈(마이펫) 대시보드 상단에 자동 노출하는 리텐션 강화 배너.
// 신규 인프라·라이브러리 없이 순수 JS. 데이터가 없으면 배너 자체를 숨긴다.
// health-digest.js와 동일한 홈 배너 패턴을 따른다.
(function () {
    'use strict';

    function _albums() {
        return (typeof albums !== 'undefined' && Array.isArray(albums)) ? albums : [];
    }
    function _walks() {
        return (typeof walks !== 'undefined' && Array.isArray(walks)) ? walks : [];
    }

    function _md(d) {
        return `${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    }

    // 순수 함수: 앨범·산책 → 회고 { kind, ... } 또는 null(데이터 없음).
    function buildMemoryFlashback(albumList, walkList, now) {
        albumList = albumList || [];
        walkList = walkList || [];
        now = now || new Date();
        const todayMd = _md(now);

        // ① 1년 전 오늘의 앨범(일기) — 가장 오래된 매치를 우선(더 긴 세월일수록 감동)
        let bestAlbum = null;
        for (const a of albumList) {
            if (!a || a.id == null) continue;
            const d = new Date(a.id);
            if (isNaN(d)) continue;
            const yearDiff = now.getFullYear() - d.getFullYear();
            if (_md(d) === todayMd && yearDiff >= 1) {
                if (!bestAlbum || yearDiff > bestAlbum.years) {
                    bestAlbum = { years: yearDiff, id: a.id, text: (a.text || a.dateStr || '소중한 기억') };
                }
            }
        }
        if (bestAlbum) {
            return { kind: 'onThisDay', years: bestAlbum.years, id: bestAlbum.id, text: bestAlbum.text };
        }

        // ② 이번 달 하이라이트 — 이번 달 산책·일기 집계
        const ym = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
        let walkCount = 0, km = 0;
        for (const w of walkList) {
            if (!w || !w.savedAt) continue;
            if (String(w.savedAt).slice(0, 7) === ym) {
                walkCount++;
                km += parseFloat(w.distance) || 0;
            }
        }
        let photoCount = 0;
        for (const a of albumList) {
            if (!a || a.id == null) continue;
            const d = new Date(a.id);
            if (!isNaN(d) && `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}` === ym) photoCount++;
        }
        if (walkCount > 0 || photoCount > 0) {
            return { kind: 'monthly', walkCount, km: Math.round(km * 10) / 10, photoCount };
        }

        return null;
    }

    function _openAlbum(id) {
        if (typeof switchTab === 'function') switchTab('album');
        if (id != null) setTimeout(function () {
            if (typeof scrollToEntry === 'function') scrollToEntry(id);
        }, 300);
    }
    function _openWalk() {
        if (typeof switchTab === 'function') switchTab('walk');
    }

    function renderMemoryFlashbackBanner() {
        const host = document.getElementById('memory-flashback-banner');
        if (!host) return;
        const m = buildMemoryFlashback(_albums(), _walks(), new Date());
        if (!m) { host.innerHTML = ''; host.hidden = true; return; }
        host.hidden = false;

        if (m.kind === 'onThisDay') {
            host.innerHTML = `
            <button type="button" onclick="renderMemoryFlashbackOpen(${m.id})" class="w-full text-left p-3.5 bg-gradient-to-r from-amber-50 to-orange-50 flex items-center gap-3 hover:from-amber-100 transition-colors">
                <span class="text-xl shrink-0">📅</span>
                <span class="min-w-0 flex-1">
                    <span class="block text-xs font-black text-amber-900">${m.years}년 전 오늘의 추억</span>
                    <span class="block text-[11px] text-amber-600 font-medium truncate mt-0.5">${m.text}</span>
                </span>
                <i class="fa-solid fa-chevron-right text-amber-400 text-xs shrink-0"></i>
            </button>`;
            return;
        }

        // monthly
        const parts = [];
        if (m.walkCount > 0) parts.push(`산책 ${m.walkCount}회${m.km > 0 ? ` · ${m.km}km` : ''}`);
        if (m.photoCount > 0) parts.push(`일기 ${m.photoCount}개`);
        const target = m.walkCount > 0 ? 'renderMemoryFlashbackWalk()' : 'renderMemoryFlashbackOpen()';
        host.innerHTML = `
        <button type="button" onclick="${target}" class="w-full text-left p-3.5 bg-brand-50/40 flex items-center gap-3 hover:bg-brand-50 transition-colors">
            <span class="text-xl shrink-0">✨</span>
            <span class="min-w-0 flex-1">
                <span class="block text-xs font-black text-brand-700">이번 달 하이라이트</span>
                <span class="block text-[11px] text-gray-500 font-medium truncate mt-0.5">${parts.join(' · ')} 함께했어요</span>
            </span>
            <i class="fa-solid fa-chevron-right text-brand-300 text-xs shrink-0"></i>
        </button>`;
    }

    window.buildMemoryFlashback = buildMemoryFlashback;
    window.renderMemoryFlashbackBanner = renderMemoryFlashbackBanner;
    window.renderMemoryFlashbackOpen = _openAlbum;
    window.renderMemoryFlashbackWalk = _openWalk;
})();
