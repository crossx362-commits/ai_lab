// 🔌 Supabase 클라이언트 연결 및 데이터베이스 실시간 동기화 모듈

// ⚠️ 보안: 키는 window._env_ (Vercel 환경변수) 또는 .env 파일에서만 주입해야 합니다.
// 로컬 개발: 프로젝트 루트에 .env 파일 생성 후 아래 변수 설정
//   SUPABASE_URL=https://your-project.supabase.co
//   SUPABASE_ANON_KEY=your-anon-key
// 프로덕션(Vercel): 대시보드 Settings > Environment Variables에 동일 키 등록
const SUPABASE_URL = window._env_?.SUPABASE_URL || "";
const SUPABASE_ANON_KEY = window._env_?.SUPABASE_ANON_KEY || "";

let supabaseClient = null;
let isSupabaseConnected = false;
window.hasShownSchemaError = false;

function handleSupabaseSchemaError(e) {
    if (e && (e.code === 'PGRST205' || (e.message && e.message.includes('schema cache')))) {
        if (!window.hasShownSchemaError) {
            window.hasShownSchemaError = true;
            if (typeof showCustomDialog === 'function') {
                showCustomDialog({
                    title: "Supabase 테이블 미생성 오류 ⚠️",
                    message: "Supabase 데이터베이스에 'pets' 또는 'posts' 테이블이 존재하지 않습니다.\n\n프로젝트 루트의 'supabase_schema.sql' 파일 내용을 복사하여 Supabase 콘솔(SQL Editor)에서 실행(Run)해 주세요!",
                    icon: "💾",
                    type: "alert"
                });
            }
        }
    }
}

const SupabaseService = {
    client: null,
    isConnected: false,

    async _withRetry(fn, maxRetries = 3, baseDelayMs = 800) {
        for (let attempt = 0; attempt < maxRetries; attempt++) {
            try {
                return await fn();
            } catch (e) {
                const isRetryable =
                    e?.code === '57014' ||
                    e?.message?.includes('statement timeout') ||
                    e?.message?.includes('Failed to fetch') ||
                    e?.message?.includes('NetworkError') ||
                    e?.message?.includes('network');
                if (attempt < maxRetries - 1 && isRetryable) {
                    await new Promise(r => setTimeout(r, baseDelayMs * Math.pow(2, attempt)));
                    continue;
                }
                throw e;
            }
        }
    },

    async init() {
        try {
            if (SUPABASE_URL && !SUPABASE_URL.includes("your-project-ref")) {
                const lib = window.supabase || (typeof supabaseJs !== 'undefined' ? supabaseJs : null);
                if (lib && lib.createClient) {
                    this.client = lib.createClient(SUPABASE_URL, SUPABASE_ANON_KEY);
                    this.isConnected = true;

                    // 전역 브릿지 변수 동기화
                    supabaseClient = this.client;
                    isSupabaseConnected = true;

                    // OAuth 콜백 확실히 처리: SIGNED_IN 이벤트로 세션 수립 시점에 로그인 완료
                    this.client.auth.onAuthStateChange((event, session) => {
                        if (event === 'SIGNED_IN' && session && session.user) {
                            this._completeOAuthLogin(session);
                        }
                    });

                    if (typeof AppLogger !== 'undefined') {
                        AppLogger.info("Supabase Service: 클라이언트가 성공적으로 연결되었습니다.");
                    } else {
                        console.log("🟢 Supabase 클라이언트가 성공적으로 연결되었습니다.");
                    }
                    this.startSync();
                } else {
                    // CDN이 아직 로드 안 됐을 경우 DOMContentLoaded 시 재시도
                    document.addEventListener('DOMContentLoaded', () => {
                        const retryLib = window.supabase || null;
                        if (retryLib && retryLib.createClient) {
                            this.client = retryLib.createClient(SUPABASE_URL, SUPABASE_ANON_KEY);
                            this.isConnected = true;
                            supabaseClient = this.client;
                            isSupabaseConnected = true;
                            if (typeof AppLogger !== 'undefined') {
                                AppLogger.info("Supabase Service: 재시도 연결 성공.");
                            } else {
                                console.log("🟢 Supabase 클라이언트 재시도 연결 성공.");
                            }
                            this.startSync();
                        } else {
                            if (typeof AppLogger !== 'undefined') {
                                AppLogger.warn("Supabase Service: CDN 라이브러리 미탑재 - 로컬 모드로 동작합니다.");
                            } else {
                                console.warn("⚠️ Supabase CDN 라이브러리 미탑재 - 로컬 모드로 동작합니다.");
                            }
                        }
                    });
                }
            } else {
                if (typeof AppLogger !== 'undefined') {
                    AppLogger.warn("Supabase Service: 자격 증명이 기본 플레이스홀더 상태입니다. 로컬 데이터베이스(LocalStorage) 모드로 안전하게 작동합니다.");
                } else {
                    console.warn("⚠️ Supabase 자격 증명이 기본 플레이스홀더 상태입니다. 로컬 데이터베이스(LocalStorage) 모드로 안전하게 작동합니다.");
                }
            }
        } catch (error) {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.error("Supabase Service: 초기화 중 오류 발생", error);
            } else {
                console.error("🔴 Supabase Service: 초기화 중 오류 발생:", error);
            }
        }
    },

    async uploadMedia(fileOrBase64, path) {
        if (!this.isConnected || !this.client) return null;

        try {
            let fileBody;
            let contentType = 'image/png';

            if (typeof fileOrBase64 === 'string') {
                if (fileOrBase64.startsWith('data:')) {
                    const parts = fileOrBase64.split(',');
                    const match = parts[0].match(/data:(.*?);base64/);
                    if (match) contentType = match[1];
                    const bstr = atob(parts[1]);
                    let n = bstr.length;
                    const u8arr = new Uint8Array(n);
                    while (n--) {
                        u8arr[n] = bstr.charCodeAt(n);
                    }
                    fileBody = new Blob([u8arr], { type: contentType });
                } else if (fileOrBase64.startsWith('http://') || fileOrBase64.startsWith('https://')) {
                    return fileOrBase64;
                } else {
                    return null;
                }
            } else {
                fileBody = fileOrBase64;
                contentType = fileOrBase64.type || 'image/png';
            }

            const { data, error } = await this.client.storage
                .from('petnna-media')
                .upload(path, fileBody, {
                    contentType: contentType,
                    upsert: true
                });

            if (error) throw error;

            const { data: { publicUrl } } = this.client.storage
                .from('petnna-media')
                .getPublicUrl(path);

            return publicUrl;
        } catch (e) {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.error(`Supabase 미디어 업로드 실패 (${path})`, e);
            } else {
                console.error(`🔴 Supabase 미디어 업로드 실패 (${path}):`, e.message);
            }
            return null;
        }
    },

    _completeOAuthLogin(session) {
        const user = session.user;
        // 허용 이메일 게이트(오너 지시 2026-07-21) — 소셜(카카오/구글) 로그인은 어떤
        // 계정으로 올지 미리 알 수 없어 콜백에서 차단하고 세션을 즉시 파기한다.
        // app.js의 _isAllowedLoginEmail과 동일 규칙(_env_.ALLOWED_LOGIN_EMAILS, 빈 값=제한 없음).
        const _raw = (window._env_ && window._env_.ALLOWED_LOGIN_EMAILS) || '';
        const _allow = _raw.split(',').map(s => s.trim().toLowerCase()).filter(Boolean);
        if (_allow.length > 0 && !_allow.includes(String(user.email || '').trim().toLowerCase())) {
            this.client.auth.signOut();
            try {
                localStorage.removeItem('petna_is_logged_in');
            } catch (e) { /* 무시 */ }
            if (typeof showToast === 'function') showToast("🔒 초대된 계정만 이용할 수 있어요.");
            return;
        }
        const nickname =
            user.user_metadata?.full_name ||
            user.user_metadata?.name ||
            user.user_metadata?.preferred_username ||
            user.email?.split('@')[0] || '집사';
        const email = user.email;

        localStorage.setItem('petna_is_logged_in', 'true');
        localStorage.setItem('petna_user_email', email);
        if (typeof settings_email !== 'undefined') settings_email = email;

        const savedNickname = localStorage.getItem('petna_user_nickname_' + email) || nickname;
        localStorage.setItem('petna_user_nickname_' + email, savedNickname);
        localStorage.setItem('petna_user_nickname', savedNickname);
        if (typeof settings_nickname !== 'undefined') settings_nickname = savedNickname;

        if (!localStorage.getItem('petna_user_avatar_' + email)) {
            localStorage.setItem('petna_user_avatar_' + email, '🧔');
        }

        const loginOverlay = document.getElementById('login-landing-overlay');
        if (loginOverlay && loginOverlay.style.display !== 'none') {
            loginOverlay.classList.add('opacity-0', 'scale-95');
            setTimeout(() => {
                loginOverlay.style.display = 'none';
                const headerEl = document.querySelector('header');
                const mainEl = document.querySelector('main');
                const mobileNavbarEl = document.getElementById('mobile-navbar');
                if (headerEl) headerEl.style.display = 'block';
                if (mainEl) mainEl.style.display = 'block';
                if (mobileNavbarEl) mobileNavbarEl.classList.remove('hidden');
                document.body.classList.add('logged-in');
                if (typeof showToast === 'function') showToast(`환영합니다, ${savedNickname}님! 🐾✨`);
                if (typeof switchTab === 'function') switchTab('mypet');
                if (typeof loadState === 'function') loadState(email);
            }, 300);
        }
    },

    async handleOAuthCallback() {
        if (!this.isConnected || !this.client) return;

        try {
            const { data: { session }, error } = await this.client.auth.getSession();
            if (error) throw error;

            if (session && session.user) {
                this._completeOAuthLogin(session);
                const _email = session.user.email;
                if (typeof AppLogger !== 'undefined') {
                    AppLogger.info(`OAuth 세션 감지 완료: ${_email}`);
                } else {
                    console.log(`🟢 OAuth 세션 감지 완료: ${_email}`);
                }
            }
        } catch (e) {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.error('OAuth 세션 감지 실패', e);
            } else {
                console.error('🔴 OAuth 세션 감지 실패:', e.message);
            }
        }
    },

    // 🪄 데모 모드 — 설정의 '데모 데이터 주입'이 켜는 로컬 전용 플래그.
    // sync* 함수들은 DB에 행이 하나라도 있으면 로컬 상태를 통째로 덮어쓰고 saveState()까지
    // 부르기 때문에, 데모 데이터가 주입 직후 새로고침에서 1초도 못 버티고 지워졌다
    // (2026-07-25 발견 — "임시데이터 추가해도 반영 안 됨"의 진짜 원인).
    // 데모 모드에서는 원격 → 로컬 방향 동기화를 통째로 멈춘다.
    isDemoMode() {
        try { return localStorage.getItem('petna_demo_mode') === '1'; } catch (e) { return false; }
    },

    async startSync() {
        await this.handleOAuthCallback();
        if (this.isDemoMode()) {
            console.warn('🪄 데모 모드 — 클라우드 동기화를 건너뜁니다 (설정 > 데모 모드 종료로 해제)');
            return;
        }
        if (this.isConnected) {
            this.syncPets();
            this.syncFeed();
            this.syncProfile();
            this.syncAlbums();
            this.syncRoutes();
            this.syncMedicalRecords();
            this.syncHealthLogs();
            this.syncMedicationLogs();
            this.startRealtimeFeed();
        }
    },

    _realtimeChannel: null,

    startRealtimeFeed() {
        if (!this.isConnected || !this.client || this.isDemoMode()) return;
        if (this._realtimeChannel) {
            this.client.removeChannel(this._realtimeChannel);
        }
        this._realtimeChannel = this.client
            .channel('petna-posts-feed', {
                config: { broadcast: { self: false } }
            })
            .on('postgres_changes',
                { event: 'INSERT', schema: 'public', table: 'posts' },
                (payload) => {
                    // 새 포스트 감지 → 로컬 배열 앞에 추가 후 피드 갱신
                    if (!payload.new) return;
                    const item = payload.new;
                    const newPost = {
                        id: item.id,
                        petName: item.pet_name,
                        petAvatar: item.pet_avatar,
                        content: item.content,
                        image: item.image,
                        isVideo: item.is_video,
                        videoUrl: item.video_url,
                        likes: item.likes || 0,
                        liked: false,
                        comments: typeof item.comments === 'string' ? JSON.parse(item.comments || '[]') : (item.comments || [])
                    };
                    const already = (typeof posts !== 'undefined') && posts.some(p => p.id === newPost.id);
                    if (!already && typeof posts !== 'undefined') {
                        posts.unshift(newPost);
                        if (typeof renderFeed === 'function') renderFeed();
                        if (typeof showToast === 'function') showToast('📢 새 이웃 소식이 올라왔어요!');
                    }
                }
            )
            .on('postgres_changes',
                { event: 'UPDATE', schema: 'public', table: 'posts' },
                () => {
                    if (typeof SupabaseService !== 'undefined') SupabaseService.syncFeed();
                }
            )
            .subscribe((status) => {
                if (typeof AppLogger !== 'undefined') {
                    AppLogger.info(`Realtime 피드 채널: ${status}`);
                }
            });
    },

    async syncPets() {
        if (!this.isConnected || !this.client || this.isDemoMode()) return { success: false, reason: 'not_connected' };
        
        try {
            const { data, error } = await this._withRetry(() =>
                this.client.from('pets').select('*')
            );

            if (error) throw error;
            
            if (data && data.length > 0) {
                const targetEmail = (typeof settings_email !== 'undefined' && settings_email) || "butler@petna.co.kr";
                const localPets = JSON.parse(localStorage.getItem('petna_pets_' + targetEmail)) || JSON.parse(localStorage.getItem('petna_pets')) || [];
                
                pets = data.map(dbPet => {
                    const localPet = localPets.find(lp => lp.id === dbPet.id);
                    if (localPet) {
                        if (localPet.imageUrl && (localPet.type === 'custom' || localPet.imageUrl.startsWith('data:image/'))) {
                            dbPet.imageUrl = localPet.imageUrl;
                            dbPet.type = 'custom';
                        }
                        if (!dbPet.sajuData && localPet.sajuData) dbPet.sajuData = localPet.sajuData;
                        if (!dbPet.harmonyData && localPet.harmonyData) dbPet.harmonyData = localPet.harmonyData;
                        if (!dbPet.mbtiCode && localPet.mbtiCode) dbPet.mbtiCode = localPet.mbtiCode;
                        // 펫게임(마이펫 육성) 상태는 DB 스키마에 없음 — 로컬 보존 필수(없으면 리셋됨)
                        if (localPet.game) dbPet.game = localPet.game;
                        if (typeof localPet.pawCoins === 'number') dbPet.pawCoins = localPet.pawCoins;
                    }
                    return dbPet;
                });
                
                if (typeof saveState === 'function') saveState();
                if (typeof renderMyPets === 'function') renderMyPets();
                console.log("🐾 펫 동기화 완료");
                return { success: true, count: pets.length };
            }
            return { success: true, count: 0 };
        } catch (e) {
            const isTimeout = e && (e.code === '57014' || (e.message && e.message.includes('statement timeout')));
            const logFn = isTimeout ? 'warn' : 'error';
            const msg = isTimeout
                ? "Supabase 펫 동기화 타임아웃 — 로컬 캐시 사용"
                : "Supabase 펫 동기화 실패";
            (typeof AppLogger !== 'undefined' ? AppLogger[logFn] : console[logFn])(msg, e?.message);
            if (typeof handleSupabaseSchemaError === 'function') handleSupabaseSchemaError(e);
            // Fallback: 로컬 스토리지 복구 렌더링
            const email = (typeof settings_email !== 'undefined' && settings_email) || "butler@petna.co.kr";
            const cached = JSON.parse(localStorage.getItem('petna_pets_' + email) || localStorage.getItem('petna_pets') || '[]');
            if (cached.length > 0 && pets.length === 0) {
                pets = cached;
                if (typeof renderMyPets === 'function') renderMyPets();
            }
            return { success: false, error: e?.message };
        }
    },

    async uploadPost(newPost) {
        if (!this.isConnected || !this.client) return;
        
        try {
            let imageUrl = newPost.image;
            if (newPost.image && newPost.image.startsWith('data:')) {
                const ext = newPost.image.split(';')[0].split('/')[1] || 'png';
                const path = `posts/${Date.now()}_${Math.random().toString(36).substr(2, 9)}.${ext}`;
                const uploadedUrl = await this.uploadMedia(newPost.image, path);
                if (uploadedUrl) {
                    imageUrl = uploadedUrl;
                    newPost.image = uploadedUrl;
                } else {
                    // 스토리지 업로드 실패(예: petnna-media 버킷 부재) 시 data: URL을 그대로
                    // insert에 실으면 포스트 전체 동기화가 실패한다(2026-07-25 발견) —
                    // 이미지는 로컬 피드에만 남기고 DB에는 텍스트 포스트로 강등해 동기화를 살린다.
                    imageUrl = null;
                }
            }

            const row = {
                pet_name: newPost.petName,
                pet_avatar: newPost.petAvatar,
                content: newPost.content,
                image: imageUrl || null,
                is_video: newPost.isVideo || false,
                video_url: newPost.videoUrl || null,
                likes: newPost.likes || 0,
                comments: JSON.stringify(newPost.comments || []),
                attached_walk: newPost.attachedWalk ? JSON.stringify(newPost.attachedWalk) : null
                // attached_ai_health는 스키마에 컬럼이 없으므로 주석 처리
                // TODO: Supabase에서 attached_ai_health 컬럼 추가 필요
            };
            let { data, error } = await this.client.from('posts').insert([row]).select();

            // 라이브 DB에 없는 컬럼(PGRST204, 예: attached_walk 미적용)이면 그 컬럼만 빼고
            // 1회 재시도 — 컬럼 하나 때문에 포스트 전체 동기화가 죽는 것을 막는다(2026-07-25 발견:
            // supabase_schema.sql의 attached_walk ALTER가 라이브에 미적용이라 모든 포스트
            // 업로드가 조용히 실패 중이었다). pets 동기화의 PGRST204 처리와 같은 원칙.
            if (error && error.code === 'PGRST204') {
                const m = String(error.message || '').match(/'([^']+)' column/);
                if (m && m[1] in row) {
                    delete row[m[1]];
                    ({ data, error } = await this.client.from('posts').insert([row]).select());
                }
            }

            if (error) throw error;

            if (data && data.length > 0) {
                const insertedPost = data[0];
                const oldId = newPost.id;
                newPost.id = insertedPost.id;

                // 전역 posts 배열에서도 ID를 데이터베이스 ID로 업데이트하여 이후 실시간 인터랙션 동기화 보장
                if (typeof posts !== 'undefined' && Array.isArray(posts)) {
                    const idx = posts.findIndex(p => p.id === oldId);
                    if (idx !== -1) {
                        posts[idx].id = insertedPost.id;
                    }
                }
                
                if (typeof saveState === 'function') saveState();
                if (typeof renderFeed === 'function') renderFeed();
            }
            
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info("새 피드가 Supabase 실시간 데이터베이스에 업로드되었습니다!");
            } else {
                console.log("🟢 새 피드가 Supabase 실시간 데이터베이스에 업로드되었습니다!");
            }
        } catch (e) {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.error("Supabase 피드 업로드 실패", e);
            } else {
                console.error("🔴 Supabase 피드 업로드 실패:", e.message);
            }
        }
    },

    async syncFeed() {
        if (!this.isConnected || !this.client || this.isDemoMode()) return;
        
        try {
            const { data, error } = await this._withRetry(() =>
                this.client.from('posts').select('*').order('id', { ascending: false })
            );

            if (error) throw error;
            
            if (data && data.length > 0) {
                const targetEmail = (typeof settings_email !== 'undefined' && settings_email) || "butler@petna.co.kr";
                const localPosts = JSON.parse(localStorage.getItem('petna_posts_' + targetEmail)) || JSON.parse(localStorage.getItem('petna_posts')) || [];
                
                posts = data.map(item => {
                    const localPost = localPosts.find(lp => lp.id === item.id);
                    let finalImage = item.image;
                    
                    if (localPost && localPost.image && (localPost.image.startsWith('data:image/') || localPost.image.startsWith('data:video/'))) {
                        finalImage = localPost.image;
                    }
                    
                    const _parse = (v) => { try { return typeof v === 'string' ? JSON.parse(v) : v; } catch { return null; } };
                    return {
                        id: item.id,
                        petName: item.pet_name,
                        petAvatar: item.pet_avatar,
                        content: item.content,
                        image: finalImage,
                        isVideo: item.is_video,
                        videoUrl: item.video_url,
                        likes: item.likes,
                        liked: localPost ? localPost.liked : false,
                        comments: _parse(item.comments) || [],
                        attachedWalk: _parse(item.attached_walk),
                        attachedAiHealth: localPost ? localPost.attachedAiHealth : null // DB에 컬럼 없음, 로컬 데이터 사용
                    };
                });
                
                if (typeof saveState === 'function') saveState();
                if (typeof renderFeed === 'function') renderFeed();
                
                if (typeof AppLogger !== 'undefined') {
                    AppLogger.info("Supabase로부터 피드 타임라인 동기화를 완료했으며, 로컬 커스텀 업로드 파일이 안전하게 보존 병합되었습니다.");
                } else {
                    console.log("📢 Supabase로부터 피드 타임라인 동기화를 완료했으며, 로컬 커스텀 업로드 파일이 안전하게 보존 병합되었습니다.");
                }
            }
        } catch (e) {
            const isTimeoutError = e && (e.code === '57014' || (e.message && e.message.includes('statement timeout')));
            if (isTimeoutError) {
                if (typeof AppLogger !== 'undefined') {
                    AppLogger.warn("Supabase 피드 타임라인 동기화 타임아웃 (로컬 캐시 피드를 사용합니다.)", e);
                } else {
                    console.warn("⚠️ Supabase 피드 타임라인 동기화 타임아웃:", e.message);
                }
            } else {
                if (typeof AppLogger !== 'undefined') {
                    AppLogger.error("Supabase 피드 타임라인 동기화 실패", e);
                } else {
                    console.error("🔴 Supabase 피드 타임라인 동기화 실패:", e.message);
                }
            }
            if (typeof handleSupabaseSchemaError === 'function') {
                handleSupabaseSchemaError(e);
            }
            // 🔄 Supabase 통신 오류 시 로컬 스토리지의 피드 데이터를 Fallback으로 복구 렌더링
            const targetEmail = (typeof settings_email !== 'undefined' && settings_email) || "butler@petna.co.kr";
            const localPosts = JSON.parse(localStorage.getItem('petna_posts_' + targetEmail)) || JSON.parse(localStorage.getItem('petna_posts')) || [];
            if (localPosts.length > 0 && posts.length === 0) {
                posts = localPosts;
                if (typeof renderFeed === 'function') renderFeed();
            }
        }
    },

    async deletePost(postId) {
        if (!this.isConnected || !this.client) return;
        
        try {
            const { error } = await this.client
                .from('posts')
                .delete()
                .eq('id', postId);
                
            if (error) throw error;
            
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info(`피드 ID ${postId}가 Supabase에서 성공적으로 삭제되었습니다.`);
            } else {
                console.log(`🗑️ 피드 ID ${postId}가 Supabase에서 성공적으로 삭제되었습니다.`);
            }
        } catch (e) {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.error("Supabase 피드 삭제 실패", e);
            } else {
                console.error("🔴 Supabase 피드 삭제 실패:", e.message);
            }
        }
    },

    async updatePostLikes(postId, likes) {
        if (!this.isConnected || !this.client) return;
        
        try {
            const { error } = await this.client
                .from('posts')
                .update({ likes: likes })
                .eq('id', postId);
                
            if (error) throw error;
            
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info(`피드 ID ${postId}의 좋아요 수가 Supabase에 업데이트되었습니다.`);
            } else {
                console.log(`❤️ 피드 ID ${postId}의 좋아요 수가 Supabase에 업데이트되었습니다.`);
            }
        } catch (e) {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.error("Supabase 좋아요 수 업데이트 실패", e);
            } else {
                console.error("🔴 Supabase 좋아요 수 업데이트 실패:", e.message);
            }
        }
    },

    async updatePostComments(postId, comments) {
        if (!this.isConnected || !this.client) return;
        
        try {
            const { error } = await this.client
                .from('posts')
                .update({ comments: JSON.stringify(comments) })
                .eq('id', postId);
                
            if (error) throw error;
            
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info(`피드 ID ${postId}의 댓글이 Supabase에 업데이트되었습니다.`);
            } else {
                console.log(`💬 피드 ID ${postId}의 댓글이 Supabase에 업데이트되었습니다.`);
            }
        } catch (e) {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.error("Supabase 댓글 업데이트 실패", e);
            } else {
                console.error("🔴 Supabase 댓글 업데이트 실패:", e.message);
            }
        }
    },

    async updatePostContent(postId, content, image = undefined, videoUrl = undefined, isVideo = undefined) {
        if (!this.isConnected || !this.client) return;
        
        try {
            const updateData = { content: content };
            if (image !== undefined) updateData.image = image;
            if (videoUrl !== undefined) updateData.video_url = videoUrl;
            if (isVideo !== undefined) updateData.is_video = isVideo;

            const { error } = await this.client
                .from('posts')
                .update(updateData)
                .eq('id', postId);
                
            if (error) throw error;
            
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info(`피드 ID ${postId}의 본문 내용 및 미디어가 Supabase에 업데이트되었습니다.`);
            } else {
                console.log(`✏️ 피드 ID ${postId}의 본문 내용 및 미디어가 Supabase에 업데이트되었습니다.`);
            }
        } catch (e) {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.error("Supabase 피드 본문/미디어 업데이트 실패", e);
            } else {
                console.error("🔴 Supabase 피드 본문/미디어 업데이트 실패:", e.message);
            }
        }
    },


    async updatePet(pet) {
        if (!this.isConnected || !this.client) return;
        
        try {
            let imageUrl = pet.imageUrl;
            if (pet.imageUrl && pet.imageUrl.startsWith('data:')) {
                const ext = pet.imageUrl.split(';')[0].split('/')[1] || 'png';
                const path = `pets/${pet.id}_${Date.now()}.${ext}`;
                const uploadedUrl = await this.uploadMedia(pet.imageUrl, path);
                if (uploadedUrl) {
                    imageUrl = uploadedUrl;
                    pet.imageUrl = uploadedUrl;
                }
            }

            const { error } = await this.client
                .from('pets')
                .upsert([
                    {
                        id: pet.id,
                        name: pet.name,
                        breed: pet.breed || null,
                        type: pet.type || null,
                        imageUrl: imageUrl || null,
                        age: pet.age || null,
                        weight: pet.weight || null,
                        gender: pet.gender || null,
                        personality: pet.personality || null,
                        hunger: pet.hunger !== undefined ? pet.hunger : 70,
                        happy: pet.happy !== undefined ? pet.happy : 80,
                        roomName: pet.roomName || null,
                        iqScore: pet.iqScore || null,
                        iqTitle: pet.iqTitle || null,
                        iqDesc: pet.iqDesc || null,
                        sajuData: pet.sajuData || null,
                        harmonyData: pet.harmonyData || null,
                        mbtiCode: pet.mbtiCode || null
                    }
                ]);
                
            if (error) throw error;
            
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info(`펫 '${pet.name}' 정보가 Supabase에 성공적으로 동기화되었습니다.`);
            } else {
                console.log(`🐾 펫 '${pet.name}' 정보가 Supabase에 성공적으로 동기화되었습니다.`);
            }
        } catch (e) {
            const isColumnError = e && (e.code === 'PGRST204' || (e.message && (e.message.includes('harmonyData') || e.message.includes('sajuData') || e.message.includes('mbtiCode'))));
            const isTimeoutError = e && (e.code === '57014' || (e.message && e.message.includes('statement timeout')));
            
            if (isColumnError) {
                if (typeof showCustomDialog === 'function' && !window.hasShownColumnError) {
                    window.hasShownColumnError = true;
                    showCustomDialog({
                        title: "Supabase 컬럼 미생성 오류 ⚠️",
                        message: "Supabase 'pets' 테이블에 사주, MBTI, 조화도 데이터를 저장할 컬럼(sajuData, harmonyData, mbtiCode)이 존재하지 않습니다.\n\n프로젝트 루트의 'supabase_schema.sql' 파일 맨 아래에 있는 ALTER TABLE 마이그레이션 구문을 복사하여 Supabase 콘솔(SQL Editor)에서 실행(Run)해 주세요!",
                        icon: "💾",
                        type: "alert"
                    });
                }
                if (typeof AppLogger !== 'undefined') {
                    AppLogger.warn("Supabase 펫 동기화 업데이트 실패 (컬럼 누락 - 로컬 스토리지에 유지됨)", e);
                } else {
                    console.warn("⚠️ Supabase 펫 동기화 업데이트 실패 (컬럼 누락):", e.message);
                }
            } else if (isTimeoutError) {
                if (typeof AppLogger !== 'undefined') {
                    AppLogger.warn("Supabase 펫 동기화 업데이트 타임아웃 (서버 지연 - 로컬 스토리지에 유지됨)", e);
                } else {
                    console.warn("⚠️ Supabase 펫 동기화 업데이트 타임아웃:", e.message);
                }
            } else {
                if (typeof AppLogger !== 'undefined') {
                    AppLogger.error("Supabase 펫 동기화 업데이트 실패", e);
                } else {
                    console.error("🔴 Supabase 펫 동기화 업데이트 실패:", e.message);
                }
            }
        }
    },

    async updateProfile(profile) {
        if (!this.isConnected || !this.client) return;
        const email = profile.email || (typeof settings_email !== 'undefined' && settings_email) || localStorage.getItem('petna_user_email') || "butler@petna.co.kr";
        try {
            let photoUrl = profile.photo_url;
            if (profile.photo_url && profile.photo_url.startsWith('data:')) {
                const ext = profile.photo_url.split(';')[0].split('/')[1] || 'png';
                const safeEmail = email.replace(/[^a-zA-Z0-9]/g, '_');
                const path = `profiles/${safeEmail}_profile.${ext}`;
                const uploadedUrl = await this.uploadMedia(profile.photo_url, path);
                if (uploadedUrl) {
                    photoUrl = uploadedUrl;
                    profile.photo_url = uploadedUrl;
                    if (typeof settings_photo_url !== 'undefined' && email === localStorage.getItem('petna_user_email')) {
                        settings_photo_url = uploadedUrl;
                    }
                }
            }

            const { error } = await this.client
                .from('profiles')
                .upsert([
                    {
                        email: email,
                        nickname: profile.nickname !== undefined ? profile.nickname : (typeof settings_nickname !== 'undefined' ? settings_nickname : null),
                        avatar: profile.avatar !== undefined ? profile.avatar : (typeof settings_avatar !== 'undefined' ? settings_avatar : null),
                        photo_url: photoUrl !== undefined ? photoUrl : (typeof settings_photo_url !== 'undefined' ? settings_photo_url : null),
                        theme: profile.theme !== undefined ? profile.theme : (typeof settings_theme !== 'undefined' ? settings_theme : null),
                        unit: profile.unit !== undefined ? profile.unit : (typeof settings_unit !== 'undefined' ? settings_unit : null),
                        notifications_enabled: profile.notifications_enabled !== undefined ? profile.notifications_enabled : (typeof settings_notifications_enabled !== 'undefined' ? settings_notifications_enabled : true)
                    }
                ]);
            if (error) throw error;
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info(`프로필 정보가 Supabase에 성공적으로 동기화되었습니다.`);
            } else {
                console.log(`👤 프로필 정보가 Supabase에 성공적으로 동기화되었습니다.`);
            }
        } catch (e) {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.error("Supabase 프로필 업데이트 실패", e);
            } else {
                console.error("🔴 Supabase 프로필 업데이트 실패:", e.message);
            }
        }
    },

    async syncProfile() {
        if (!this.isConnected || !this.client || this.isDemoMode()) return;
        const email = (typeof settings_email !== 'undefined' && settings_email) || localStorage.getItem('petna_user_email') || "butler@petna.co.kr";
        try {
            const { data, error } = await this.client
                .from('profiles')
                .select('*')
                .eq('email', email)
                .maybeSingle();
            if (error && error.code !== 'PGRST116') throw error; // PGRST116: 0 rows returned
            if (data) {
                if (data.nickname) {
                    localStorage.setItem('petna_user_nickname_' + email, data.nickname);
                    if (typeof settings_nickname !== 'undefined') settings_nickname = data.nickname;
                }
                if (data.avatar) {
                    localStorage.setItem('petna_user_avatar_' + email, data.avatar);
                    if (typeof settings_avatar !== 'undefined') settings_avatar = data.avatar;
                }
                if (data.photo_url) {
                    localStorage.setItem('petna_user_photo_url_' + email, data.photo_url);
                    if (typeof settings_photo_url !== 'undefined') settings_photo_url = data.photo_url;
                }
                if (data.theme) {
                    localStorage.setItem('petna_theme_' + email, data.theme);
                    localStorage.setItem('petna_theme', data.theme);
                    if (typeof settings_theme !== 'undefined') settings_theme = data.theme;
                }
                if (data.unit) {
                    localStorage.setItem('petna_unit_' + email, data.unit);
                    localStorage.setItem('petna_unit', data.unit);
                    if (typeof settings_unit !== 'undefined') settings_unit = data.unit;
                }
                if (data.notifications_enabled !== null) {
                    localStorage.setItem('petna_notifications_' + email, data.notifications_enabled);
                    localStorage.setItem('petna_notifications', data.notifications_enabled);
                    if (typeof settings_notifications_enabled !== 'undefined') settings_notifications_enabled = data.notifications_enabled;
                }

                if (typeof applyThemeStyles === 'function' && data.theme) applyThemeStyles(data.theme);
                if (typeof renderUserProfile === 'function') renderUserProfile();
                if (typeof initSettingsUI === 'function') initSettingsUI();
                if (typeof renderSettings === 'function') renderSettings();

                if (typeof AppLogger !== 'undefined') {
                    AppLogger.info("Supabase로부터 프로필 설정을 동기화했습니다.");
                } else {
                    console.log("👤 Supabase로부터 프로필 설정을 동기화했습니다.");
                }
            }
        } catch (e) {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.error("Supabase 프로필 동기화 실패", e);
            } else {
                console.error("🔴 Supabase 프로필 동기화 실패:", e.message);
            }
        }
    },

    async uploadAlbum(albumItem) {
        if (!this.isConnected || !this.client) return;
        const email = albumItem.email || (typeof settings_email !== 'undefined' && settings_email) || localStorage.getItem('petna_user_email') || "butler@petna.co.kr";
        try {
            if (albumItem.url && albumItem.url.startsWith('data:')) {
                const ext = albumItem.url.split(';')[0].split('/')[1] || 'png';
                const path = `albums/${albumItem.id}_${Date.now()}.${ext}`;
                const uploadedUrl = await this.uploadMedia(albumItem.url, path);
                if (uploadedUrl) {
                    albumItem.url = uploadedUrl;
                }
            }

            const { error } = await this.client
                .from('albums')
                .upsert([
                    {
                        id: albumItem.id,
                        email: email,
                        data: albumItem
                    }
                ]);
            if (error) throw error;
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info(`앨범 카드(ID: ${albumItem.id})가 Supabase에 성공적으로 업로드되었습니다.`);
            } else {
                console.log(`🖼️ 앨범 카드(ID: ${albumItem.id})가 Supabase에 성공적으로 업로드되었습니다.`);
            }
        } catch (e) {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.error("Supabase 앨범 업로드 실패", e);
            } else {
                console.error("🔴 Supabase 앨범 업로드 실패:", e.message);
            }
        }
    },

    async deleteAlbum(albumId) {
        if (!this.isConnected || !this.client) return;
        try {
            const { error } = await this.client
                .from('albums')
                .delete()
                .eq('id', albumId);
            if (error) throw error;
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info(`앨범 카드(ID: ${albumId})가 Supabase에서 삭제되었습니다.`);
            } else {
                console.log(`🗑️ 앨범 카드(ID: ${albumId})가 Supabase에서 삭제되었습니다.`);
            }
        } catch (e) {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.error("Supabase 앨범 삭제 실패", e);
            } else {
                console.error("🔴 Supabase 앨범 삭제 실패:", e.message);
            }
        }
    },

    async syncAlbums() {
        if (!this.isConnected || !this.client || this.isDemoMode()) return;
        const email = (typeof settings_email !== 'undefined' && settings_email) || localStorage.getItem('petna_user_email') || "butler@petna.co.kr";
        try {
            const { data, error } = await this._withRetry(() =>
                this.client.from('albums').select('*').eq('email', email)
            );
            if (error) throw error;
            if (data && data.length > 0) {
                const dbAlbums = data.map(row => row.data);
                if (typeof albums !== 'undefined') {
                    albums = dbAlbums;
                    localStorage.setItem('petna_albums_' + email, JSON.stringify(albums));
                    localStorage.setItem('petna_albums', JSON.stringify(albums));
                    if (typeof renderAlbumGallery === 'function') renderAlbumGallery();
                }
                if (typeof AppLogger !== 'undefined') {
                    AppLogger.info("Supabase로부터 앨범 목록을 동기화했습니다.");
                } else {
                    console.log("🖼️ Supabase로부터 앨범 목록을 동기화했습니다.");
                }
            }
        } catch (e) {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.error("Supabase 앨범 동기화 실패", e);
            } else {
                console.error("🔴 Supabase 앨범 동기화 실패:", e.message);
            }
            const localAlbums = JSON.parse(localStorage.getItem('petna_albums_' + email)) || JSON.parse(localStorage.getItem('petna_albums')) || [];
            if (localAlbums.length > 0 && typeof albums !== 'undefined' && albums.length === 0) {
                albums = localAlbums;
                if (typeof renderAlbumGallery === 'function') renderAlbumGallery();
            }
        }
    },

    async fetchFriendDiaries(friendEmails) {
        if (!this.isConnected || !this.client || !friendEmails.length) return [];
        try {
            const { data, error } = await this.client
                .from('albums')
                .select('*')
                .in('email', friendEmails);
            if (error) throw error;
            return (data || []).map(row => ({ ...row.data, email: row.email }));
        } catch (e) {
            console.error('🔴 친구 일기 fetch 실패:', e.message);
            return [];
        }
    },

    async uploadRoute(routeItem) {
        // 오프라인이면 산책로를 큐에 안전 저장하고 재연결 시 자동 재전송(offline-sync.js).
        if (typeof navigator !== 'undefined' && navigator.onLine === false
            && typeof window.OfflineSync !== 'undefined') {
            window.OfflineSync.enqueue('route', routeItem);
            return;
        }
        if (!this.isConnected || !this.client) return;
        const email = (typeof settings_email !== 'undefined' && settings_email) || localStorage.getItem('petna_user_email') || "butler@petna.co.kr";
        try {
            const { error } = await this.client
                .from('routes')
                .upsert([
                    {
                        id: routeItem.id,
                        email: email,
                        name: routeItem.name,
                        coords: routeItem.coords,
                        distance: routeItem.distance
                    }
                ]);
            if (error) throw error;
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info(`맞춤 산책로(ID: ${routeItem.id})가 Supabase에 성공적으로 업로드되었습니다.`);
            } else {
                console.log(`🗺️ 맞춤 산책로(ID: ${routeItem.id})가 Supabase에 성공적으로 업로드되었습니다.`);
            }
        } catch (e) {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.error("Supabase 맞춤 산책로 업로드 실패", e);
            } else {
                console.error("🔴 Supabase 맞춤 산책로 업로드 실패:", e.message);
            }
        }
    },

    async deleteRoute(routeId) {
        if (!this.isConnected || !this.client) return;
        try {
            const { error } = await this.client
                .from('routes')
                .delete()
                .eq('id', routeId);
            if (error) throw error;
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info(`맞춤 산책로(ID: ${routeId})가 Supabase에서 삭제되었습니다.`);
            } else {
                console.log(`🗑️ 맞춤 산책로(ID: ${routeId})가 Supabase에서 삭제되었습니다.`);
            }
        } catch (e) {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.error("Supabase 맞춤 산책로 삭제 실패", e);
            } else {
                console.error("🔴 Supabase 맞춤 산책로 삭제 실패:", e.message);
            }
        }
    },

    async syncRoutes() {
        if (!this.isConnected || !this.client || this.isDemoMode()) return;
        const email = (typeof settings_email !== 'undefined' && settings_email) || localStorage.getItem('petna_user_email') || "butler@petna.co.kr";
        try {
            const { data, error } = await this._withRetry(() =>
                this.client.from('routes').select('*').eq('email', email)
            );
            if (error) throw error;
            if (data && data.length > 0) {
                const dbRoutes = data.map(row => ({
                    id: row.id,
                    name: row.name,
                    coords: row.coords,
                    distance: row.distance
                }));
                if (typeof customRoutes !== 'undefined') {
                    customRoutes = dbRoutes;
                    localStorage.setItem('petna_custom_routes_' + email, JSON.stringify(customRoutes));
                    localStorage.setItem('petna_custom_routes', JSON.stringify(customRoutes));
                    if (typeof renderCustomRoutesList === 'function') renderCustomRoutesList();
                }
                if (typeof AppLogger !== 'undefined') {
                    AppLogger.info("Supabase로부터 맞춤 산책로 목록을 동기화했습니다.");
                } else {
                    console.log("🗺️ Supabase로부터 맞춤 산책로 목록을 동기화했습니다.");
                }
            }
        } catch (e) {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.error("Supabase 맞춤 산책로 동기화 실패", e);
            } else {
                console.error("🔴 Supabase 맞춤 산책로 동기화 실패:", e.message);
            }
            const localRoutes = JSON.parse(localStorage.getItem('petna_custom_routes_' + email)) || JSON.parse(localStorage.getItem('petna_custom_routes')) || [];
            if (localRoutes.length > 0 && typeof customRoutes !== 'undefined' && customRoutes.length === 0) {
                customRoutes = localRoutes;
                if (typeof renderCustomRoutesList === 'function') renderCustomRoutesList();
            }
        }
    },

    // ── 건강수첩(medical_records) / 건강기록(health_logs) 실 배선 ──────────
    // 2026-07-16 개선회의(회의_202607162027_2) 결정 반영. 두 테이블 모두
    // RLS가 auth.uid() = user_id로 스코프돼 있어(마이그레이션 add_health_logs.sql·
    // add_medical_records.sql, 오너 승인 2026-07-10), 실제 Supabase Auth 세션이
    // 없으면(카카오/구글 미연동, 이메일 로컬 로그인만 한 경우) auth.uid()가 null이라
    // 어떤 쓰기도 RLS에서 거부된다 — 그래서 매 호출마다 auth.getUser()로 실사용자를
    // 확인하고, 없으면 조용히 로컬 전용으로 남긴다(에러 아님, 정상 동작).
    // 사진(photos)은 별도 storage 버킷 정책(Dashboard 수동 생성)이 아직 없어 동기화
    // 대상에서 제외 — base64를 jsonb에 그대로 넣으면 스키마 의도(스토리지 경로)에도
    // 어긋나고 행 용량도 커진다.
    async _authUser() {
        if (!this.isConnected || !this.client) return null;
        try {
            const { data: { user } } = await this.client.auth.getUser();
            return user || null;
        } catch (e) {
            return null;
        }
    },

    async uploadMedicalRecord(record) {
        const user = await this._authUser();
        if (!user) return; // 실 Supabase 세션 없음 — 로컬 전용 유지
        try {
            const { error } = await this.client
                .from('medical_records')
                .upsert([{
                    id: record.id,
                    user_id: user.id,
                    email: record.email || user.email || '',
                    pet_id: record.petId != null ? String(record.petId) : null,
                    category: record.category || 'other',
                    visit_date: record.visitDate,
                    title: record.diagnosis || record.hospital || '건강수첩 기록',
                    detail: record.notes || null,
                    hospital: record.hospital || null,
                    cost: record.cost || 0,
                }]);
            if (error) throw error;
            if (typeof AppLogger !== 'undefined') AppLogger.info(`건강수첩(ID: ${record.id})이 Supabase에 동기화되었습니다.`);
        } catch (e) {
            if (typeof AppLogger !== 'undefined') AppLogger.error("Supabase 건강수첩 업로드 실패", e);
            else console.error("🔴 Supabase 건강수첩 업로드 실패:", e.message);
        }
    },

    async deleteMedicalRecordRemote(recordId) {
        const user = await this._authUser();
        if (!user) return;
        try {
            const { error } = await this.client.from('medical_records').delete().eq('id', recordId);
            if (error) throw error;
        } catch (e) {
            if (typeof AppLogger !== 'undefined') AppLogger.error("Supabase 건강수첩 삭제 실패", e);
            else console.error("🔴 Supabase 건강수첩 삭제 실패:", e.message);
        }
    },

    async syncMedicalRecords() {
        if (this.isDemoMode()) return;
        const user = await this._authUser();
        if (!user) return;
        try {
            const { data, error } = await this._withRetry(() =>
                this.client.from('medical_records').select('*').eq('user_id', user.id)
            );
            if (error) throw error;
            if (data && data.length > 0 && typeof window.medicalRecords !== 'undefined') {
                const remote = data.map(row => ({
                    id: row.id,
                    petId: row.pet_id,
                    email: row.email,
                    visitDate: row.visit_date,
                    category: row.category,
                    hospital: row.hospital,
                    diagnosis: row.title,
                    cost: row.cost,
                    notes: row.detail,
                    photo: '',
                    createdAt: row.created_at,
                }));
                const localOnly = (window.medicalRecords || [])
                    .filter(r => !remote.some(rr => String(rr.id) === String(r.id)));
                window.medicalRecords = [...remote, ...localOnly];
                if (typeof saveMedicalRecordsLocal === 'function') saveMedicalRecordsLocal();
                if (typeof renderMedicalRecordsTimeline === 'function') renderMedicalRecordsTimeline();
                if (typeof AppLogger !== 'undefined') AppLogger.info("Supabase로부터 건강수첩을 동기화했습니다.");
            }
        } catch (e) {
            if (typeof AppLogger !== 'undefined') AppLogger.error("Supabase 건강수첩 동기화 실패", e);
            else console.error("🔴 Supabase 건강수첩 동기화 실패:", e.message);
        }
    },

    // PostgREST가 '스키마에 없는 컬럼'을 거절할 때의 신호. 코드는 PGRST204,
    // 메시지는 "Could not find the 'x' column of 'y' in the schema cache" 형태다.
    // 다른 오류(권한·네트워크·제약 위반)를 여기로 흡수하면 진짜 실패가 조용히 묻히므로
    // 이 두 신호만 본다.
    _isUnknownColumn(error) {
        if (!error) return false;
        if (error.code === 'PGRST204') return true;
        const m = String(error.message || '').toLowerCase();
        return m.includes('column') && (m.includes('schema cache') || m.includes('does not exist'));
    },

    async uploadHealthLog(entry) {
        // 오프라인이면 기록을 큐에 안전 저장하고 재연결 시 자동 재전송(offline-sync.js).
        if (typeof navigator !== 'undefined' && navigator.onLine === false
            && typeof window.OfflineSync !== 'undefined') {
            window.OfflineSync.enqueue('healthLog', entry);
            return;
        }
        const user = await this._authUser();
        if (!user) return;
        try {
            if (!entry._remoteId) entry._remoteId = Date.now() * 1000 + Math.floor(Math.random() * 1000);
            const email = (typeof settings_email !== 'undefined' && settings_email) || localStorage.getItem('petna_user_email') || user.email || '';
            const activePet = (typeof getActivePet === 'function') ? getActivePet() : null;
            const base = {
                id: entry._remoteId,
                user_id: user.id,
                email,
                pet_id: activePet ? String(activePet.id) : null,
                log_date: entry.date,
                water: entry.water || null,
                food: entry.food || null,
                poop: entry.poop || null,
                condition: entry.condition || null,
            };
            // 원탭 컨디션 4종(add_health_logs_condition_columns.sql, 오너 승인 2026-07-28).
            // 마이그레이션이 아직 안 돌아간 프로젝트에서도 업로드가 통째로 깨지면 안 되므로,
            // '컬럼 없음' 오류일 때만 이 네 개를 빼고 한 번 더 시도한다. 마이그레이션 실행과
            // 배포 순서를 신경 쓰지 않아도 되고, 실행되는 순간부터 자동으로 올라간다.
            const extra = {
                poop_color: entry.poopColor || null,
                urine: entry.urine || null,
                appetite: entry.appetite || null,
                activity: entry.activity || null,
            };
            const put = (row) => this.client.from('health_logs')
                .upsert([row], { onConflict: 'user_id,pet_id,log_date' });

            let { error } = await put({ ...base, ...extra });
            if (error && this._isUnknownColumn(error)) {
                if (!this._warnedHealthLogColumns) {
                    this._warnedHealthLogColumns = true;
                    const msg = "health_logs에 원탭 컨디션 컬럼이 없어 배변색·소변색·식욕·활력은 "
                        + "이 기기에만 남습니다 — migrations/add_health_logs_condition_columns.sql 실행 필요";
                    if (typeof AppLogger !== 'undefined') AppLogger.warn(msg);
                    else console.warn("⚠️ " + msg);
                }
                ({ error } = await put(base));
            }
            if (error) throw error;
            if (typeof saveState === 'function') saveState(); // entry._remoteId 캐시 영속화
            if (typeof AppLogger !== 'undefined') AppLogger.info(`건강기록(${entry.date})이 Supabase에 동기화되었습니다.`);
        } catch (e) {
            if (typeof AppLogger !== 'undefined') AppLogger.error("Supabase 건강기록 업로드 실패", e);
            else console.error("🔴 Supabase 건강기록 업로드 실패:", e.message);
        }
    },

    async syncHealthLogs() {
        if (this.isDemoMode()) return;
        const user = await this._authUser();
        if (!user) return;
        try {
            const { data, error } = await this._withRetry(() =>
                this.client.from('health_logs').select('*').eq('user_id', user.id)
            );
            if (error) throw error;
            if (data && data.length > 0 && typeof healthLogs !== 'undefined') {
                if (!healthLogs.history) healthLogs.history = [];
                const localByDate = {};
                healthLogs.history.forEach(h => { localByDate[h.date] = h; });
                data.forEach(row => {
                    // 로컬 항목 위에 '덮어쓰기'가 아니라 '겹쳐쓰기'다 — 통째로 교체하면
                    // health_logs 테이블에 컬럼이 없는 필드가 조용히 지워진다.
                    // 원탭 컨디션(daily-condition.js)이 쓰는 poopColor·urine·appetite·activity가
                    // 정확히 그 경우였다: 테이블이 2026-07-10에 만들어져 이 네 컬럼이 없었고,
                    // 교체하던 시절엔 동기화가 한 번 돌 때마다 네 필드가 사라져
                    // wellness-anomaly의 혈변·혈뇨·식욕저하 감지가 입력을 잃었다
                    // (2026-07-28 오너 신고 "기록이 반영이 안되 있는데"의 원인).
                    // null/undefined인 원격 값은 덮지 않는다 — 다른 기기가 아직 안 올린
                    // 항목의 null이 이 기기의 실제 기록을 지우면 그것도 같은 유실이다.
                    // 네 컬럼은 add_health_logs_condition_columns.sql로 추가됐다(오너 승인
                    // 2026-07-28). 아직 실행 전인 프로젝트에선 row에 키가 없어 undefined라
                    // 자동으로 건너뛴다 — 실행 순서를 신경 쓸 필요가 없다.
                    const local = localByDate[row.log_date] || {};
                    const merged = { ...local, date: row.log_date, _remoteId: row.id };
                    const remote = {
                        water: row.water, food: row.food, poop: row.poop, condition: row.condition,
                        poopColor: row.poop_color, urine: row.urine,
                        appetite: row.appetite, activity: row.activity,
                    };
                    Object.keys(remote).forEach(k => {
                        if (remote[k] !== null && remote[k] !== undefined) merged[k] = remote[k];
                    });
                    localByDate[row.log_date] = merged;
                });
                healthLogs.history = Object.values(localByDate)
                    .sort((a, b) => new Date(b.date) - new Date(a.date))
                    .slice(0, 90);
                if (typeof saveState === 'function') saveState();
                if (typeof renderHealthCalendarMain === 'function') renderHealthCalendarMain();
                if (typeof AppLogger !== 'undefined') AppLogger.info("Supabase로부터 건강기록을 동기화했습니다.");
            }
        } catch (e) {
            if (typeof AppLogger !== 'undefined') AppLogger.error("Supabase 건강기록 동기화 실패", e);
            else console.error("🔴 Supabase 건강기록 동기화 실패:", e.message);
        }
    },

    // 투약 이행 로그(medication_logs) — care-check.js '먹였어요/건너뜀'이 남긴 기록을 올린다.
    // UNIQUE(user_id,pet_id,schedule_id,log_date)로 같은 날 재기록 시 겹쳐 갱신된다.
    async uploadMedicationLog(entry) {
        const user = await this._authUser();
        if (!user) return; // 실 세션 없음 — 로컬 전용 유지
        try {
            const email = (typeof settings_email !== 'undefined' && settings_email) || localStorage.getItem('petna_user_email') || user.email || '';
            const { error } = await this.client
                .from('medication_logs')
                .upsert([{
                    id: entry._remoteId,
                    user_id: user.id,
                    email,
                    pet_id: entry.petId != null ? String(entry.petId) : null,
                    schedule_id: entry.scheduleId != null ? String(entry.scheduleId) : null,
                    kind: entry.kind || 'medicine',
                    title: entry.title || '투약',
                    log_date: entry.date,
                    taken: !!entry.taken,
                    taken_at: entry.takenAt || null,
                }], { onConflict: 'user_id,pet_id,schedule_id,log_date' });
            if (error) throw error;
            if (typeof AppLogger !== 'undefined') AppLogger.info(`투약 이행 로그(${entry.date})가 Supabase에 동기화되었습니다.`);
        } catch (e) {
            if (typeof AppLogger !== 'undefined') AppLogger.error("Supabase 투약 로그 업로드 실패", e);
            else console.error("🔴 Supabase 투약 로그 업로드 실패:", e.message);
        }
    },

    async syncMedicationLogs() {
        if (this.isDemoMode()) return;
        const user = await this._authUser();
        if (!user) return;
        try {
            const { data, error } = await this._withRetry(() =>
                this.client.from('medication_logs').select('*').eq('user_id', user.id)
            );
            if (error) throw error;
            if (data && data.length > 0 && typeof MedicationLog !== 'undefined') {
                MedicationLog.mergeRemote(data);
                if (typeof AppLogger !== 'undefined') AppLogger.info("Supabase로부터 투약 이행 로그를 동기화했습니다.");
            }
        } catch (e) {
            if (typeof AppLogger !== 'undefined') AppLogger.error("Supabase 투약 로그 동기화 실패", e);
            else console.error("🔴 Supabase 투약 로그 동기화 실패:", e.message);
        }
    }
};

// 🔗 브릿지 설정: 기존 전역 변수 및 전역 함수들에 대한 하위 호환성 유지
// 전역 변수 브릿지
Object.defineProperty(window, 'supabaseClient', {
    get() { return SupabaseService.client; },
    set(val) { SupabaseService.client = val; },
    configurable: true
});

Object.defineProperty(window, 'isSupabaseConnected', {
    get() { return SupabaseService.isConnected; },
    set(val) { SupabaseService.isConnected = val; },
    configurable: true
});

// 전역 함수 브릿지
window.syncPetsFromSupabase = () => SupabaseService.syncPets();
window.uploadPostToSupabase = (newPost) => SupabaseService.uploadPost(newPost);
window.syncFeedFromSupabase = () => SupabaseService.syncFeed();
window.deletePostFromSupabase = (postId) => SupabaseService.deletePost(postId);
window.updatePostLikesInSupabase = (postId, likes) => SupabaseService.updatePostLikes(postId, likes);
window.updatePostCommentsInSupabase = (postId, comments) => SupabaseService.updatePostComments(postId, comments);
window.updatePostContentInSupabase = (postId, content, image, videoUrl, isVideo) => SupabaseService.updatePostContent(postId, content, image, videoUrl, isVideo);
window.updatePetInSupabase = (pet) => SupabaseService.updatePet(pet);
window.updateProfileInSupabase = (profile) => SupabaseService.updateProfile(profile);
window.syncProfileFromSupabase = () => SupabaseService.syncProfile();
window.uploadAlbumToSupabase = (albumItem) => SupabaseService.uploadAlbum(albumItem);
window.fetchFriendDiaries = (emails) => SupabaseService.fetchFriendDiaries(emails);
window.deleteAlbumFromSupabase = (albumId) => SupabaseService.deleteAlbum(albumId);
window.syncAlbumsFromSupabase = () => SupabaseService.syncAlbums();
window.uploadRouteToSupabase = (routeItem) => SupabaseService.uploadRoute(routeItem);
window.deleteRouteFromSupabase = (routeId) => SupabaseService.deleteRoute(routeId);
window.syncRoutesFromSupabase = () => SupabaseService.syncRoutes();
window.uploadMedicalRecordToSupabase = (record) => SupabaseService.uploadMedicalRecord(record);
window.deleteMedicalRecordFromSupabase = (recordId) => SupabaseService.deleteMedicalRecordRemote(recordId);
window.syncMedicalRecordsFromSupabase = () => SupabaseService.syncMedicalRecords();
window.uploadHealthLogToSupabase = (entry) => SupabaseService.uploadHealthLog(entry);
window.syncHealthLogsFromSupabase = () => SupabaseService.syncHealthLogs();
window.uploadMedicationLogToSupabase = (entry) => SupabaseService.uploadMedicationLog(entry);
window.syncMedicationLogsFromSupabase = () => SupabaseService.syncMedicationLogs();

// 초기 구동
SupabaseService.init();

// 자동 동기화 트리거
window.addEventListener('DOMContentLoaded', () => {
    if (SupabaseService.isConnected) {
        SupabaseService.syncPets();
        SupabaseService.syncFeed();
        SupabaseService.syncProfile();
        SupabaseService.syncAlbums();
        SupabaseService.syncRoutes();
        SupabaseService.syncMedicalRecords();
        SupabaseService.syncHealthLogs();
        SupabaseService.syncMedicationLogs();
    }
});
