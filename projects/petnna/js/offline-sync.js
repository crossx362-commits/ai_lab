// 📴 오프라인 기록 안전저장 + 재연결 자동 동기화 (P2, 회의 제안)
//
// 통신이 끊긴 상태에서 산책·건강기록을 올리려 하면 upload 함수가 조용히 실패해
// 기록이 유실될 수 있다(과거 Supabase 동기화 유실 사고 이력). 이 모듈은 오프라인일 때
// 업로드 페이로드를 localStorage 큐에 안전 저장하고, 재연결(online 이벤트) 시 자동으로
// 다시 올린 뒤 진행/완료 상태를 화면 우하단 배지에 표시한다.
//
// DB 스키마 무접촉 — 기존 upload 함수(window.uploadHealthLogToSupabase 등)를 그대로
// 재호출할 뿐이다. supabase.js의 uploadHealthLog/uploadRoute가 오프라인일 때
// OfflineSync.enqueue()로 우회한다(단일 진입점).

(function () {
    const QUEUE_KEY = 'petna_offline_queue';

    // kind → 재전송 핸들러. 페이로드는 원래 upload 함수에 그대로 넘긴다.
    const HANDLERS = {
        healthLog: (p) => window.uploadHealthLogToSupabase && window.uploadHealthLogToSupabase(p),
        route: (p) => window.uploadRouteToSupabase && window.uploadRouteToSupabase(p),
    };

    function _read() {
        try { return JSON.parse(localStorage.getItem(QUEUE_KEY) || '[]'); }
        catch (e) { return []; }
    }
    function _write(list) {
        try { localStorage.setItem(QUEUE_KEY, JSON.stringify(list)); } catch (e) { /* 용량 초과 등 무시 */ }
    }

    const OfflineSync = {
        _flushing: false,

        count() { return _read().length; },

        // 오프라인 저장: 같은 kind의 동일 페이로드가 아니어도 기록마다 개별 큐잉한다.
        enqueue(kind, payload) {
            if (!HANDLERS[kind]) return false;
            const list = _read();
            list.push({ id: Date.now() + '_' + Math.random().toString(36).slice(2, 8), kind, payload, ts: Date.now() });
            _write(list);
            this._render();
            return true;
        },

        async flush() {
            if (this._flushing) return;
            if (typeof navigator !== 'undefined' && navigator.onLine === false) return;
            let list = _read();
            if (list.length === 0) return;
            this._flushing = true;
            this._render(true);
            const remaining = [];
            for (const item of list) {
                try {
                    const h = HANDLERS[item.kind];
                    if (h) await Promise.resolve(h(item.payload));
                    // upload 함수는 실패 시에도 예외를 삼키므로(재시도 내장), 온라인에서 호출을
                    // 마치면 전송 시도 완료로 간주하고 큐에서 제거한다.
                } catch (e) {
                    remaining.push(item); // 예기치 못한 예외만 다음 재연결에 재시도
                }
            }
            _write(remaining);
            this._flushing = false;
            if (remaining.length === 0) this._renderDone();
            else this._render();
        },

        // ── 상태 배지 UI (우하단 고정) ─────────────────────────────
        _badge() {
            let el = document.getElementById('offline-sync-status');
            if (!el && document.body) {
                el = document.createElement('div');
                el.id = 'offline-sync-status';
                el.style.cssText = 'position:fixed;right:16px;bottom:80px;z-index:9999;'
                    + 'padding:8px 14px;border-radius:9999px;font-size:13px;font-weight:600;'
                    + 'box-shadow:0 4px 12px rgba(0,0,0,0.15);display:none;transition:opacity .3s;';
                document.body.appendChild(el);
            }
            return el;
        },

        _render(syncing) {
            const el = this._badge();
            if (!el) return;
            const n = _read().length;
            if (n === 0 && !syncing) { el.style.display = 'none'; return; }
            const offline = (typeof navigator !== 'undefined' && navigator.onLine === false);
            el.style.display = 'block';
            el.style.opacity = '1';
            if (syncing) {
                el.style.background = '#dbeafe'; el.style.color = '#1e40af';
                el.textContent = `🔄 동기화 중… (${n}건)`;
            } else if (offline) {
                el.style.background = '#f3f4f6'; el.style.color = '#4b5563';
                el.textContent = `📴 오프라인 · 기록 ${n}건 저장됨`;
            } else {
                el.style.background = '#dbeafe'; el.style.color = '#1e40af';
                el.textContent = `🔄 동기화 대기 ${n}건`;
            }
        },

        _renderDone() {
            const el = this._badge();
            if (!el) return;
            el.style.display = 'block';
            el.style.opacity = '1';
            el.style.background = '#dcfce7'; el.style.color = '#166534';
            el.textContent = '✅ 동기화 완료';
            setTimeout(() => { el.style.opacity = '0'; setTimeout(() => { el.style.display = 'none'; }, 300); }, 2000);
        },

        init() {
            window.addEventListener('online', () => this.flush());
            window.addEventListener('offline', () => this._render());
            // 페이지 로드 시 온라인이면 남은 큐를 즉시 비운다(직전 세션에서 못 보낸 기록).
            if (typeof navigator === 'undefined' || navigator.onLine !== false) {
                setTimeout(() => this.flush(), 1500);
            } else {
                this._render();
            }
        }
    };

    window.OfflineSync = OfflineSync;

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => OfflineSync.init());
    } else {
        OfflineSync.init();
    }
})();
