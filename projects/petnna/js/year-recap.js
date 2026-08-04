// year-recap.js — "올해의 반려일기 리캡" (백로그 나무 제안, P2 기획)
// ─────────────────────────────────────────────────────────────
// 이미 쌓인 산책·앨범(일기)·지출 로컬 데이터를 연 단위로 집계해
// '총 산책 거리·사진 수·총 지출·최고의 순간' 요약 카드를 모달로 보여주고
// Web Share로 공유한다. 신규 인프라·라이브러리 없이 순수 JS·localStorage.
// 홈 배너 노출/모달 append 패턴은 memory-flashback.js·expense-tracker.js를 따른다.
// ─────────────────────────────────────────────────────────────
(function () {
    'use strict';

    var EXPENSE_LS_KEY = 'petna_expense_records'; // expense-tracker.js와 동일 키

    function _albums() {
        return (typeof albums !== 'undefined' && Array.isArray(albums)) ? albums
            : (Array.isArray(window.albums) ? window.albums : []);
    }
    function _walks() {
        return (typeof walks !== 'undefined' && Array.isArray(walks)) ? walks
            : (Array.isArray(window.walks) ? window.walks : []);
    }
    function _expenses() {
        try { return JSON.parse(localStorage.getItem(EXPENSE_LS_KEY)) || []; } catch (e) { return []; }
    }
    function esc(s) {
        if (typeof window.escapeHtml === 'function') return window.escapeHtml(s);
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }
    function toast(msg) { if (typeof window.showToast === 'function') window.showToast(msg); }
    function fmt(n) { return Math.round(n).toLocaleString('ko-KR'); }

    // 순수 함수: 산책·앨범·지출 → 해당 연도 리캡 집계.
    function buildYearRecap(albumList, walkList, expenseList, year) {
        albumList = albumList || [];
        walkList = walkList || [];
        expenseList = expenseList || [];
        year = year || new Date().getFullYear();

        var walkCount = 0, km = 0, best = null;
        for (var i = 0; i < walkList.length; i++) {
            var w = walkList[i];
            if (!w || !w.savedAt) continue;
            var wy = parseInt(String(w.savedAt).slice(0, 4), 10);
            if (wy !== year) continue;
            walkCount++;
            var dist = parseFloat(w.distance) || 0;
            km += dist;
            if (dist > 0 && (!best || dist > best.km)) {
                best = { km: dist, date: String(w.savedAt).slice(0, 10) };
            }
        }

        var photoCount = 0;
        for (var j = 0; j < albumList.length; j++) {
            var a = albumList[j];
            if (!a || a.id == null) continue;
            var d = new Date(a.id);
            if (!isNaN(d) && d.getFullYear() === year) photoCount++;
        }

        var totalSpent = 0;
        for (var k = 0; k < expenseList.length; k++) {
            var e = expenseList[k];
            if (!e || !e.date) continue;
            if (parseInt(String(e.date).slice(0, 4), 10) === year) totalSpent += (e.amount || 0);
        }

        return {
            year: year,
            walkCount: walkCount,
            km: Math.round(km * 10) / 10,
            photoCount: photoCount,
            totalSpent: totalSpent,
            best: best,
            hasData: (walkCount > 0 || photoCount > 0 || totalSpent > 0)
        };
    }

    function _petName() {
        var pet = (typeof getActivePet === 'function') ? getActivePet() : null;
        return (pet && pet.name) || '우리 아이';
    }

    // ── 홈 배너 (memory-flashback와 동일 규약: 데이터 없으면 host.hidden) ──
    function renderYearRecapBanner() {
        var host = document.getElementById('year-recap-banner');
        if (!host) return;
        var r = buildYearRecap(_albums(), _walks(), _expenses(), new Date().getFullYear());
        if (!r.hasData) { host.innerHTML = ''; host.hidden = true; return; }
        host.hidden = false;

        var teaser = [];
        if (r.km > 0) teaser.push('산책 ' + r.km + 'km');
        if (r.photoCount > 0) teaser.push('사진 ' + r.photoCount + '장');
        if (r.totalSpent > 0) teaser.push('지출 ' + fmt(r.totalSpent) + '원');

        host.innerHTML =
            '<button type="button" onclick="YearRecap.open()" class="w-full text-left p-3.5 bg-gradient-to-r from-rose-50 to-brand-50 flex items-center gap-3 hover:from-rose-100 transition-colors">' +
            '<span class="text-xl shrink-0">🎁</span>' +
            '<span class="min-w-0 flex-1">' +
            '<span class="block text-xs font-black text-rose-900">' + esc(r.year) + '년 반려일기 리캡</span>' +
            '<span class="block text-[11px] text-rose-600 font-medium truncate mt-0.5">' + esc(teaser.join(' · ')) + ' 함께했어요</span>' +
            '</span>' +
            '<i class="fa-solid fa-chevron-right text-rose-300 text-xs shrink-0"></i>' +
            '</button>';
    }

    // ── 리캡 모달 ──────────────────────────────────────────────
    function open() {
        close();
        var r = buildYearRecap(_albums(), _walks(), _expenses(), new Date().getFullYear());
        var name = _petName();

        var cards =
            _statCard('🚶', r.walkCount + '회', '총 산책 횟수', 'orange') +
            _statCard('📍', r.km + 'km', '총 산책 거리', 'amber') +
            _statCard('📸', r.photoCount + '장', '남긴 사진·일기', 'sky') +
            _statCard('💰', fmt(r.totalSpent) + '원', '올해 총 지출', 'brand');

        var bestBlock = r.best
            ? '<div class="card-modern bg-gradient-to-r from-rose-50 to-orange-50 p-4">' +
              '<div class="flex items-center justify-between">' +
              '<div><h3 class="text-sm font-bold text-gray-900 mb-1">🏆 최고의 순간</h3>' +
              '<p class="text-xs text-gray-600">가장 멀리 걸은 날 ' + esc(r.best.date) + '</p></div>' +
              '<div class="text-2xl font-bold text-rose-600">' + esc(r.best.km) + 'km</div></div></div>'
            : '';

        var overlay = document.createElement('div');
        overlay.id = 'year-recap-overlay';
        overlay.className = 'fixed inset-0 z-[9999] flex items-center justify-center bg-black/50 p-4';
        overlay.innerHTML =
            '<div class="bg-white rounded-3xl max-w-lg w-full max-h-[90vh] overflow-y-auto shadow-2xl">' +
            '<div class="sticky top-0 bg-gradient-to-r from-rose-500 to-brand-600 px-6 py-5 rounded-t-3xl">' +
            '<button onclick="YearRecap.close()" class="absolute top-4 right-4 text-white/80 hover:text-white transition-colors">' +
            '<i class="fa-solid fa-xmark text-xl"></i></button>' +
            '<div class="flex items-center gap-3">' +
            '<div class="w-12 h-12 bg-white/20 rounded-xl flex items-center justify-center"><span class="text-2xl">🎁</span></div>' +
            '<div><h2 class="text-xl font-bold text-white">' + esc(name) + '의 ' + esc(r.year) + '년 리캡</h2>' +
            '<p class="text-sm text-white/90 mt-0.5">올 한 해 함께한 발자취</p></div></div></div>' +
            '<div class="p-6 space-y-4">' +
            (r.hasData
                ? '<div class="grid grid-cols-2 gap-3">' + cards + '</div>' + bestBlock
                : '<p class="text-sm text-gray-400 py-8 text-center">아직 올해 기록이 없어요.<br>산책·일기·지출을 남기면 리캡이 채워져요.</p>') +
            '<button onclick="YearRecap.share()" class="w-full btn-modern bg-rose-500 hover:bg-rose-600 text-white py-3.5 text-sm font-bold">' +
            '<i class="fa-solid fa-share-nodes mr-2"></i>리캡 공유하기</button>' +
            '<button onclick="YearRecap.close()" class="w-full btn-modern bg-gray-100 hover:bg-gray-200 text-gray-700 py-3 text-sm font-bold">닫기</button>' +
            '</div></div>';
        overlay.addEventListener('click', function (e) { if (e.target === overlay) close(); });
        document.body.appendChild(overlay);
    }

    function _statCard(emoji, value, label, color) {
        return '<div class="card-modern bg-' + color + '-50/50 p-4">' +
            '<div class="flex items-center justify-between mb-2">' +
            '<span class="text-3xl">' + emoji + '</span>' +
            '<div class="text-2xl font-bold text-' + color + '-600">' + esc(value) + '</div></div>' +
            '<div class="text-sm font-semibold text-gray-700">' + esc(label) + '</div></div>';
    }

    function close() { var el = document.getElementById('year-recap-overlay'); if (el) el.remove(); }

    // 공유: Web Share → 클립보드 폴백 (care-share.js와 동일 패턴)
    function share() {
        var r = buildYearRecap(_albums(), _walks(), _expenses(), new Date().getFullYear());
        if (!r.hasData) { toast('아직 공유할 올해 기록이 없어요 🐾'); return; }
        var name = _petName();
        var lines = [name + '의 ' + r.year + '년 반려일기 리캡 🎁'];
        if (r.walkCount > 0) lines.push('🚶 산책 ' + r.walkCount + '회 · ' + r.km + 'km');
        if (r.photoCount > 0) lines.push('📸 사진·일기 ' + r.photoCount + '장');
        if (r.totalSpent > 0) lines.push('💰 총 지출 ' + fmt(r.totalSpent) + '원');
        if (r.best) lines.push('🏆 최고의 순간 ' + r.best.km + 'km (' + r.best.date + ')');
        var text = lines.join('\n');

        if (navigator.share) {
            navigator.share({ title: '펫과나 반려일기 리캡', text: text }).catch(function () {});
            return;
        }
        var done = function () { toast('리캡을 복사했어요! 📋 붙여넣어 공유하세요'); };
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(text).then(done, function () { window.prompt('리캡 공유', text); });
        } else {
            window.prompt('리캡 공유', text);
        }
    }

    window.buildYearRecap = buildYearRecap;
    window.renderYearRecapBanner = renderYearRecapBanner;
    window.YearRecap = { open: open, close: close, share: share, renderBanner: renderYearRecapBanner };
})();
