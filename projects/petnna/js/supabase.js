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

    _completeOAuthLogin(session) {
        const user = session.user;
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

    async startSync() {
        await this.handleOAuthCallback();
        if (this.isConnected) {
            this.syncPets();
            this.syncFeed();
            this.syncProfile();
            this.syncAlbums();
            this.syncRoutes();
            this.startRealtimeFeed();
        }
    },

    _realtimeChannel: null,

    startRealtimeFeed() {
        if (!this.isConnected || !this.client) return;
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
        if (!this.isConnected || !this.client) return { success: false, reason: 'not_connected' };
        
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
            const { data, error } = await this.client
                .from('posts')
                .insert([
                    {
                        pet_name: newPost.petName,
                        pet_avatar: newPost.petAvatar,
                        content: newPost.content,
                        image: newPost.image || null,
                        is_video: newPost.isVideo || false,
                        video_url: newPost.videoUrl || null,
                        likes: newPost.likes || 0,
                        comments: JSON.stringify(newPost.comments || []),
                        attached_walk: newPost.attachedWalk ? JSON.stringify(newPost.attachedWalk) : null,
                        attached_ai_health: newPost.attachedAiHealth ? JSON.stringify(newPost.attachedAiHealth) : null
                    }
                ])
                .select();
                
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
        if (!this.isConnected || !this.client) return;
        
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
                        attachedAiHealth: _parse(item.attached_ai_health)
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
            const { error } = await this.client
                .from('pets')
                .upsert([
                    {
                        id: pet.id,
                        name: pet.name,
                        breed: pet.breed || null,
                        type: pet.type || null,
                        imageUrl: pet.imageUrl || null,
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
            const { error } = await this.client
                .from('profiles')
                .upsert([
                    {
                        email: email,
                        nickname: profile.nickname !== undefined ? profile.nickname : (typeof settings_nickname !== 'undefined' ? settings_nickname : null),
                        avatar: profile.avatar !== undefined ? profile.avatar : (typeof settings_avatar !== 'undefined' ? settings_avatar : null),
                        photo_url: profile.photo_url !== undefined ? profile.photo_url : (typeof settings_photo_url !== 'undefined' ? settings_photo_url : null),
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
        if (!this.isConnected || !this.client) return;
        const email = (typeof settings_email !== 'undefined' && settings_email) || localStorage.getItem('petna_user_email') || "butler@petna.co.kr";
        try {
            const { data, error } = await this.client
                .from('profiles')
                .select('*')
                .eq('email', email)
                .single();
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
        if (!this.isConnected || !this.client) return;
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
        if (!this.isConnected || !this.client) return;
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
    }
});
