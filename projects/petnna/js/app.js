// ==========================================
// 🏗️ 아키텍처 고도화: 라우팅 및 탭 라이프사이클 정의
// ==========================================

const TabControllers = {
    mypet: {
        initialized: false,
        init() {
            if (this.initialized) return;
            this.initialized = true;
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info('TabControllers: mypet initialized');
            }
        },
        render() {
            const isLoggedIn = localStorage.getItem('petna_is_logged_in') === 'true';
            if (isLoggedIn && typeof settings_email !== 'undefined' && settings_email) {
                const currentCount = parseInt(localStorage.getItem('petna_visit_count_' + settings_email) || "0");
                localStorage.setItem('petna_visit_count_' + settings_email, currentCount + 1);
            }
            const activePet = typeof getActivePet === 'function' ? getActivePet() : null;
            if (activePet) {
                activePet.tempSpeechText = null;
            }
            if (typeof renderMyPets === 'function') renderMyPets();
            if (typeof renderCalendar === 'function') renderCalendar();
            if (typeof renderMealLogsList === 'function') renderMealLogsList();
            if (typeof runDailyAgeReminderCheck === 'function') runDailyAgeReminderCheck();
        },
        destroy() {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info('TabControllers: mypet destroyed');
            }
        }
    },
    health: {
        initialized: false,
        init() {
            if (this.initialized) return;
            this.initialized = true;
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info('TabControllers: health initialized');
            }
        },
        render() {
            if (typeof renderHealthTab === 'function') renderHealthTab();
        },
        destroy() {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info('TabControllers: health destroyed');
            }
        }
    },
    walk: {
        initialized: false,
        init() {
            if (this.initialized) return;
            this.initialized = true;
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info('TabControllers: walk initialized');
            }
        },
        render() {
            if (typeof renderCalendar === 'function') renderCalendar();
            if (typeof renderWalkHistory === 'function') renderWalkHistory();
            if (typeof renderWalkWeatherCoach === 'function') renderWalkWeatherCoach();
            setTimeout(() => {
                if (typeof initWalkSimulator === 'function') initWalkSimulator();
            }, 150);
        },
        destroy() {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info('TabControllers: walk destroyed');
            }
        }
    },
    social: {
        initialized: false,
        init() {
            if (this.initialized) return;
            this.initialized = true;
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info('TabControllers: social initialized');
            }
        },
        render() {
            if (typeof renderSocialRoom === 'function') renderSocialRoom();
        },
        destroy() {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info('TabControllers: social destroyed');
            }
        }
    },
    album: {
        initialized: false,
        init() {
            if (this.initialized) return;
            this.initialized = true;
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info('TabControllers: album initialized');
            }
        },
        render() {
            if (typeof renderAlbumGallery === 'function') renderAlbumGallery();
            if (typeof renderGrowthChart === 'function') setTimeout(renderGrowthChart, 200);
        },
        destroy() {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info('TabControllers: album destroyed');
            }
        }
    },
    shop: {
        initialized: false,
        init() {
            if (this.initialized) return;
            this.initialized = true;
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info('TabControllers: shop initialized');
            }
        },
        render() {
            if (typeof renderShop === 'function') renderShop();
            if (typeof renderPetRecoCard === 'function') renderPetRecoCard('reco-card-shop');
            // 펫라이프 실시간 지도 초기화
            setTimeout(() => {
                if (typeof initPetlifeMap === 'function') initPetlifeMap();
            }, 150);
        },
        destroy() {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info('TabControllers: shop destroyed');
            }
        }
    },
    saju: {
        initialized: false,
        init() {
            if (this.initialized) return;
            this.initialized = true;
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info('TabControllers: saju initialized');
            }
        },
        render() {
            if (typeof renderSajuTab === 'function') renderSajuTab();
        },
        destroy() {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info('TabControllers: saju destroyed');
            }
            if (typeof endArcadeGame === 'function' && typeof gameActive !== 'undefined' && gameActive) {
                endArcadeGame();
            }
        }
    },
    settings: {
        initialized: false,
        init() {
            if (this.initialized) return;
            this.initialized = true;
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info('TabControllers: settings initialized');
            }
        },
        render() {
            if (typeof renderSettings === 'function') renderSettings();
        },
        destroy() {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info('TabControllers: settings destroyed');
            }
        }
    },
    mailbox: {
        initialized: false,
        init() {
            if (this.initialized) return;
            this.initialized = true;
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info('TabControllers: mailbox initialized');
            }
        },
        render() {
            if (typeof renderMailbox === 'function') renderMailbox();
        },
        destroy() {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info('TabControllers: mailbox destroyed');
            }
        }
    },
    cart: {
        initialized: false,
        init() {
            if (this.initialized) return;
            this.initialized = true;
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info('TabControllers: cart initialized');
            }
        },
        render() {
            if (typeof renderCartPage === 'function') renderCartPage();
        },
        destroy() {
            if (typeof AppLogger !== 'undefined') {
                AppLogger.info('TabControllers: cart destroyed');
            }
        }
    }
};

const AppRouter = {
    currentTab: 'mypet',

    switchTab(tabName) {

        if (tabName === 'feed') {
            this.switchTab('social');
            if (typeof switchSocialSubTab === 'function') {
                switchSocialSubTab('feed');
            }
            return;
        }

        if (tabName === 'mailbox') {
            this.switchTab('social');
            if (typeof switchSocialSubTab === 'function') {
                switchSocialSubTab('mailbox');
            }
            return;
        }

        if (typeof AppLogger !== 'undefined') {
            AppLogger.info(`AppRouter: Switching to tab '${tabName}'`);
        }

        // 1. 이전 활성화 탭 destroy 실행
        if (this.currentTab && this.currentTab !== tabName) {
            const prevController = TabControllers[this.currentTab];
            if (prevController && typeof prevController.destroy === 'function') {
                try {
                    prevController.destroy();
                } catch (err) {
                    if (typeof AppLogger !== 'undefined') {
                        AppLogger.error(`AppRouter: Failed to destroy tab controller '${this.currentTab}'`, err);
                    }
                }
            }
        }

        // 2. 모든 탭 숨김 처리
        document.querySelectorAll('.tab-content').forEach(element => {
            element.classList.add('hidden');
            element.classList.remove('block');
        });

        // 3. 대상 탭 보이기
        const targetTab = document.getElementById(`tab-${tabName}`);
        if (targetTab) {
            targetTab.classList.remove('hidden');
            targetTab.classList.add('block');
        }

        // 4. 헤더 메뉴 버튼 스타일 업데이트
        document.querySelectorAll('.tab-btn').forEach(btn => {
            const btnTab = btn.getAttribute('data-tab');
            if (btnTab === tabName) {
                btn.classList.remove('text-gray-500');
                btn.classList.add('text-brand-600', 'bg-brand-50');
            } else {
                btn.classList.add('text-gray-500');
                btn.classList.remove('text-brand-600', 'bg-brand-50');
            }
        });

        // 5. 모바일 바텀 메뉴 버튼 스타일 업데이트
        document.querySelectorAll('.mobile-tab-btn').forEach(btn => {
            const btnTab = btn.getAttribute('data-tab');
            if (btnTab === tabName) {
                btn.classList.remove('text-gray-400');
                btn.classList.add('text-brand-500');
            } else {
                btn.classList.add('text-gray-400');
                btn.classList.remove('text-brand-500');
            }
        });

        // 5.1. 모바일 상단 헤더 페이지 타이틀 업데이트
        const mobileTitles = {
            mypet: '마이펫',
            health: '건강',
            walk: '산책',
            saju: '조화도',
            social: '소셜 피드',
            album: '일기장',
            shop: '펫라이프',
            settings: '설정',
            mailbox: '우체통',
            cart: '장바구니'
        };
        const mobileTitleEl = document.getElementById('mobile-page-title');
        if (mobileTitleEl && mobileTitles[tabName]) {
            mobileTitleEl.textContent = mobileTitles[tabName];
        }

        this.currentTab = tabName;
        if (localStorage.getItem('petna_is_logged_in') === 'true') {
            localStorage.setItem('petna_active_tab', tabName);
        }

        // 6. 대상 탭 init & render 실행
        const controller = TabControllers[tabName];
        if (controller) {
            try {
                if (typeof controller.init === 'function') {
                    controller.init();
                }
                if (typeof controller.render === 'function') {
                    controller.render();
                }
            } catch (err) {
                if (typeof AppLogger !== 'undefined') {
                    AppLogger.error(`AppRouter: Failed to init/render tab controller '${tabName}'`, err);
                } else {
                    console.error(`Failed to init/render tab: ${tabName}`, err);
                }
            }
        }
    }
};

// XSS 방어 — 사용자 입력을 innerHTML에 넣기 전 항상 통과시킨다
function escapeHtml(str) {
    return String(str ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

function switchTab(tabName) {
    AppRouter.switchTab(tabName);
}

// 페이지 벗어날 때 확인창 표시 (비활성화됨)
// window.addEventListener('beforeunload', function (e) {
//     // 로그아웃, 회원탈퇴 등 특정 작업 제외
//     if (window._allowPageExit) return;
//
//     // 페이지 이탈 확인 메시지
//     e.preventDefault();
//     e.returnValue = '변경사항이 저장되지 않을 수 있습니다. 페이지를 떠나시겠습니까?';
//     return e.returnValue;
// });

document.addEventListener('DOMContentLoaded', function () {
    if (typeof checkPremiumFromUrl === 'function') checkPremiumFromUrl();
    // Inject templates dynamically before state setup and rendering
    try {
        document.getElementById('tab-mypet').innerHTML = MYPET_TEMPLATE;
        if(typeof initMypetClock === 'function') initMypetClock();

        if (typeof HEALTH_TEMPLATE !== 'undefined') {
            document.getElementById('tab-health').innerHTML = HEALTH_TEMPLATE;
        }

        document.getElementById('tab-walk').innerHTML = WALK_TEMPLATE;
        document.getElementById('tab-social').innerHTML = SOCIAL_TEMPLATE;
        document.getElementById('tab-album').innerHTML = ALBUM_TEMPLATE;
        document.getElementById('tab-shop').innerHTML = SHOP_ISLAND_TEMPLATE;
        document.getElementById('tab-saju').innerHTML = SAJU_TEMPLATE;
        document.getElementById('tab-settings').innerHTML = SETTINGS_TEMPLATE;
        document.getElementById('tab-mailbox').innerHTML = MAILBOX_TEMPLATE;
        document.getElementById('tab-cart').innerHTML = CART_TEMPLATE;
        document.getElementById('modal-container').innerHTML = MODALS_TEMPLATE;
    } catch(e) {
        console.error('템플릿 초기화 오류:', e);
    }

    applyThemeStyles(settings_theme);

    // 📸 디자인 캡처용 데모 진입 (미오 디자인 리뷰 파이프라인) — ?demo=1 이면 데모 세션을 심어 로그인 게이트를 통과.
    // 기존 데모 계정(butler@petna.co.kr)만 사용하며 새 자격증명을 추가하지 않는다. 캡처(비파괴 읽기 전용) 용도.
    try {
        const _params = new URLSearchParams(window.location.search);
        if ((_params.get('demo') === '1' || _params.get('capture') === '1') &&
            localStorage.getItem('petna_is_logged_in') !== 'true') {
            localStorage.setItem('petna_is_logged_in', 'true');
            localStorage.setItem('petna_user_email', 'butler@petna.co.kr');
            const _tab = _params.get('tab');
            if (_tab) localStorage.setItem('petna_active_tab', _tab);
        }
    } catch (e) { console.warn('데모 진입 파라미터 처리 오류:', e); }

    // 🔓 로그인 세션 확인 및 레이아웃 상태 설정
    const isLoggedIn = localStorage.getItem('petna_is_logged_in') === 'true';
    const loginOverlay = document.getElementById('login-landing-overlay');
    const headerEl = document.querySelector('header');
    const mainEl = document.querySelector('main');
    const mobileNavbarEl = document.getElementById('mobile-navbar');
    const mobileHeaderEl = document.getElementById('mobile-header');

    if (isLoggedIn) {
        // 이전 세션 복원
        try {
            const savedEmail = localStorage.getItem('petna_user_email') || '';
            if (savedEmail) {
                settings_email = savedEmail;
                settings_nickname = localStorage.getItem('petna_user_nickname_' + savedEmail) || localStorage.getItem('petna_user_nickname') || '집사';
                settings_avatar = localStorage.getItem('petna_user_avatar_' + savedEmail) || localStorage.getItem('petna_user_avatar') || '🧔';
                settings_photo_url = localStorage.getItem('petna_user_photo_url_' + savedEmail) || '';
                try { if (typeof loadState === 'function') loadState(savedEmail); } catch(e) { console.warn('loadState 오류:', e); }
            }
        } catch(e) { console.warn('세션 복원 오류:', e); }

        if (loginOverlay) loginOverlay.style.display = 'none';
        if (headerEl) headerEl.style.display = 'block';
        if (mainEl) mainEl.style.display = 'block';
        if (mobileNavbarEl) mobileNavbarEl.classList.remove('hidden');
        if (mobileHeaderEl) mobileHeaderEl.style.display = 'flex';
        document.body.classList.add('logged-in');
        try {
            const activeTab = localStorage.getItem('petna_active_tab') || 'mypet';
            switchTab(activeTab);
        } catch(e) {
            console.warn('switchTab 오류:', e);
        }

        setTimeout(() => { if (typeof PetGame !== 'undefined') PetGame.earnCare('attend'); }, 1500);
    } else {
        if (loginOverlay) {
            loginOverlay.style.display = 'flex';
            loginOverlay.classList.remove('opacity-0', 'scale-95');
        }
        if (headerEl) headerEl.style.display = 'none';
        if (mainEl) mainEl.style.display = 'none';
        if (mobileNavbarEl) mobileNavbarEl.classList.add('hidden');
        if (mobileHeaderEl) mobileHeaderEl.style.display = 'none';
        document.body.classList.remove('logged-in');
    }

    renderWalkHistory();
    updateCartBadge();
    if (typeof updateMailboxBadge === 'function') updateMailboxBadge();

    document.addEventListener('mousedown', function (e) {
        if (e.target && 
            !e.target.closest('#stickers-container') && 
            !e.target.closest('#sticker-control-hud') && 
            !e.target.closest('#sticker-text-input') && 
            !e.target.closest('#sticker-bubble-theme') && 
            !e.target.closest('#sticker-font-size') && 
            !e.target.closest('#deco-file-upload') && 
            !e.target.closest('select') &&
            !e.target.closest('.delete-btn')) {
            deselectActiveSticker();
        }
    });
});

// 🔒 로그인 및 보안 세션 기능 함수 (Auth functions)
async function hashPassword(pw) {
    const buf = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(pw));
    return Array.from(new Uint8Array(buf)).map(b => b.toString(16).padStart(2, '0')).join('');
}

function toggleAuthForm(mode) {
    const loginSection = document.getElementById('login-form-section');
    const signupSection = document.getElementById('signup-form-section');
    if (mode === 'signup') {
        if (loginSection) loginSection.classList.add('hidden');
        if (signupSection) signupSection.classList.remove('hidden');
    } else {
        if (loginSection) loginSection.classList.remove('hidden');
        if (signupSection) signupSection.classList.add('hidden');
    }
}

async function executeSignUp() {
    const nicknameInput = document.getElementById('signup-nickname-input');
    const emailInput = document.getElementById('signup-email-input');
    const passwordInput = document.getElementById('signup-password-input');
    
    if (!nicknameInput || !nicknameInput.value.trim() || !emailInput || !emailInput.value.trim() || !passwordInput || !passwordInput.value.trim()) {
        showToast("모든 회원가입 입력 항목을 입력해 주세요.");
        return;
    }
    
    const nickname = nicknameInput.value.trim();
    const email = emailInput.value.trim();
    const password = passwordInput.value.trim();
    
    if (password.length < 6) {
        showToast("비밀번호는 최소 6자리 이상이어야 합니다.");
        return;
    }

    // A. Supabase Auth 회원가입 연동 (클라우드가 활성화되어 연결된 경우)
    if (typeof isSupabaseConnected !== 'undefined' && isSupabaseConnected && supabaseClient) {
        try {
            const { data, error } = await supabaseClient.auth.signUp({
                email: email,
                password: password,
                options: {
                    data: {
                        nickname: nickname
                    }
                }
            });
            if (error) throw error;
            showToast("Supabase 클라우드 회원등록 완료! 🟢");
        } catch (e) {
            console.error("Supabase Auth 회원가입 실패:", e.message);
            showToast(`클라우드 회원등록 실패: ${e.message}`);
            return;
        }
    } else {
        // B. 로컬 무설정 fallback 회원가입 (LocalStorage 유저 풀 가입 처리)
        let registeredUsers = JSON.parse(localStorage.getItem('petna_registered_users') || '[]');
        if (registeredUsers.some(u => u.email === email)) {
            showToast("이미 가입된 이메일 주소입니다.");
            return;
        }
        const passwordHash = await hashPassword(password);
        registeredUsers.push({ nickname, email, passwordHash });
        localStorage.setItem('petna_registered_users', JSON.stringify(registeredUsers));
        showToast("로컬 회원가입이 성공적으로 완료되었습니다! 🎉");
    }

    // 회원가입 성공 시 자동 로그인 연동을 위해 닉네임 설정 바인딩
    localStorage.setItem('petna_user_nickname', nickname);
    settings_nickname = nickname;
    
    const settingsNicknameEl = document.getElementById('settings-display-nickname');
    if (settingsNicknameEl) settingsNicknameEl.innerText = nickname;

    executeLogin(email, password, true);
}

async function executeLogin(email = "", password = "", bypassVerification = false) {
    const emailInput = document.getElementById('login-email-input');
    const passwordInput = document.getElementById('login-password-input');
    const loginBtn = document.getElementById('login-submit-btn');
    
    const finalEmail = email || (emailInput ? emailInput.value.trim() : "") || "butler@petna.co.kr";
    const finalPassword = password || (passwordInput ? passwordInput.value.trim() : "") || "123456";
    
    // 이메일 입력 검증
    if (!email && emailInput && !emailInput.value.trim()) {
        showToast("📧 이메일 주소를 입력해주세요.");
        if (emailInput) emailInput.focus();
        return;
    }
    
    // 비밀번호 입력 검증
    if (!password && passwordInput && !passwordInput.value.trim()) {
        showToast("🔒 비밀번호를 입력해주세요.");
        if (passwordInput) passwordInput.focus();
        return;
    }

    // 로딩 상태 표시
    let originalBtnHTML = '';
    if (loginBtn) {
        originalBtnHTML = loginBtn.innerHTML;
        loginBtn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> 로그인 중...';
        loginBtn.disabled = true;
        loginBtn.classList.add('opacity-70', 'cursor-not-allowed');
    }

    let supabaseAuthSuccess = false;

    try {
        // ──────────────────────────────────────────────
        // A. Supabase 클라우드 인증 (우선 시도)
        // ──────────────────────────────────────────────
        if (!bypassVerification && typeof isSupabaseConnected !== 'undefined' && isSupabaseConnected && supabaseClient) {
            try {
                const { data, error } = await supabaseClient.auth.signInWithPassword({
                    email: finalEmail,
                    password: finalPassword
                });
                
                if (error) {
                    const errorMsg = error.message || '';
                    // 자격증명 오류 → 로컬 계정 폴백 시도 (기존 로컬 가입자 대응)
                    if (errorMsg.includes('Invalid login credentials') || errorMsg.includes('invalid_credentials')) {
                        // 로컬 저장소에 계정이 있으면 로컬 인증으로 진행
                        const localUsers = JSON.parse(localStorage.getItem('petna_registered_users') || '[]');
                        const isDemoAccount = finalEmail === "butler@petna.co.kr";
                        if (!isDemoAccount && !localUsers.some(u => u.email === finalEmail)) {
                            showToast("🔐 이메일 또는 비밀번호가 올바르지 않습니다.");
                            return;
                        }
                        // 로컬 폴백으로 계속 진행 (supabaseAuthSuccess = false)
                    } else if (errorMsg.includes('Email not confirmed')) {
                        showToast("📩 이메일 인증이 완료되지 않았습니다. 메일함을 확인해주세요.");
                        return;
                    } else if (errorMsg.includes('Too many requests') || errorMsg.includes('rate_limit')) {
                        showToast("⏳ 로그인 시도가 너무 많습니다. 잠시 후 다시 시도해주세요.");
                        return;
                    } else {
                        showToast(`로그인 실패: ${errorMsg}`);
                        return;
                    }
                }
                
                // Supabase 인증 성공 → 세션에서 닉네임 추출
                supabaseAuthSuccess = true;
                if (data && data.user) {
                    const supabaseNickname = data.user.user_metadata?.nickname || data.user.email?.split('@')[0] || '집사';
                    localStorage.setItem('petna_user_nickname', supabaseNickname);
                    settings_nickname = supabaseNickname;
                    const settingsNicknameEl = document.getElementById('settings-display-nickname');
                    if (settingsNicknameEl) settingsNicknameEl.innerText = supabaseNickname;
                }
                
            } catch (supabaseErr) {
                // Supabase 네트워크 오류 등 → 로컬 Fallback으로 자연스럽게 전환
                console.warn("Supabase 인증 연결 실패, 로컬 인증으로 전환합니다:", supabaseErr.message);
            }
        }
        
        // ──────────────────────────────────────────────
        // B. 로컬 인증 Fallback (Supabase 미연결 또는 네트워크 실패 시)
        // ──────────────────────────────────────────────
        if (!supabaseAuthSuccess && !bypassVerification) {
            // 개발 전용 데모 계정 — 배포 전 반드시 삭제
            const isDemoAccount = finalEmail === "butler@petna.co.kr";
            if (!isDemoAccount) {
                let registeredUsers = JSON.parse(localStorage.getItem('petna_registered_users') || '[]');
                const matchedUser = registeredUsers.find(u => u.email === finalEmail);

                if (matchedUser) {
                    const inputHash = await hashPassword(finalPassword);
                    // 구버전 평문 저장 자동 마이그레이션
                    if (matchedUser.password && !matchedUser.passwordHash) {
                        if (matchedUser.password !== finalPassword) {
                            showToast("🔒 비밀번호가 일치하지 않습니다. 다시 입력해주세요.");
                            return;
                        }
                        matchedUser.passwordHash = inputHash;
                        delete matchedUser.password;
                        localStorage.setItem('petna_registered_users', JSON.stringify(registeredUsers));
                    } else if (matchedUser.passwordHash !== inputHash) {
                        showToast("🔒 비밀번호가 일치하지 않습니다. 다시 입력해주세요.");
                        return;
                    }
                    localStorage.setItem('petna_user_nickname', matchedUser.nickname);
                    settings_nickname = matchedUser.nickname;
                    const settingsNicknameEl = document.getElementById('settings-display-nickname');
                    if (settingsNicknameEl) settingsNicknameEl.innerText = matchedUser.nickname;
                } else {
                    showToast("📋 등록되지 않은 회원 정보입니다. 회원가입을 먼저 진행해 주세요.");
                    return;
                }
            } else if (isDemoAccount && finalPassword !== "123456") {
                showToast("🔑 기본 데모 계정 비밀번호가 틀렸습니다. (기본: 123456)");
                return;
            }
        }
    } finally {
        // 로딩 상태 복원
        if (loginBtn && originalBtnHTML) {
            loginBtn.innerHTML = originalBtnHTML;
            loginBtn.disabled = false;
            loginBtn.classList.remove('opacity-70', 'cursor-not-allowed');
        }
    }
    
    localStorage.setItem('petna_is_logged_in', 'true');
    localStorage.setItem('petna_user_email', finalEmail);
    
    settings_email = finalEmail;
    settings_nickname = localStorage.getItem('petna_user_nickname_' + finalEmail) || localStorage.getItem('petna_user_nickname') || "초코 집사";
    settings_avatar = localStorage.getItem('petna_user_avatar_' + finalEmail) || localStorage.getItem('petna_user_avatar') || "🧔";
    settings_photo_url = localStorage.getItem('petna_user_photo_url_' + finalEmail) || localStorage.getItem('petna_user_photo_url') || "";
    
    // 1. 계정별 상태 로드 진행
    if (typeof loadState === 'function') {
        loadState(finalEmail);
    }

    const settingsEmailEl = document.getElementById('settings-connected-email');
    if (settingsEmailEl) settingsEmailEl.innerText = finalEmail;

    // 2. 각 컴포넌트 뷰 렌더링 초기화
    const _safeRender = (fn) => { try { if (typeof fn === 'function') fn(); } catch(e) { console.warn('render error:', e); } };
    _safeRender(renderMyPets);
    _safeRender(renderFeed);
    _safeRender(renderAlbumGallery);
    _safeRender(renderCareScheduler);
    _safeRender(renderWalkHistory);
    
    // 설정 프로필 사진 갱신
    const settingsPhoto = document.getElementById('settings-profile-photo');
    const settingsPhotoBtn = document.getElementById('settings-photo-btn');
    if (settingsPhoto) {
        settingsPhoto.src = settings_photo_url || "https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?auto=format&fit=crop&q=80&w=150";
    }
    if (settingsPhotoBtn) {
        settingsPhotoBtn.src = settings_photo_url || "https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?auto=format&fit=crop&q=80&w=150";
    }

    // 3. Supabase 연동 시 새 유저 기준 동기화 트리거
    if (typeof isSupabaseConnected !== 'undefined' && isSupabaseConnected) {
        if (typeof syncPetsFromSupabase === 'function') syncPetsFromSupabase();
        if (typeof syncFeedFromSupabase === 'function') syncFeedFromSupabase();
    }

    const loginOverlay = document.getElementById('login-landing-overlay');
    if (loginOverlay) {
        loginOverlay.classList.add('opacity-0', 'scale-95');
        setTimeout(() => {
            loginOverlay.style.display = 'none';
            
            // Show main layout elements
            const headerEl = document.querySelector('header');
            const mainEl = document.querySelector('main');
            const mobileNavbarEl = document.getElementById('mobile-navbar');
            const mobileHeaderEl = document.getElementById('mobile-header');
            
            if (headerEl) headerEl.style.display = 'block';
            if (mainEl) mainEl.style.display = 'block';
            if (mobileNavbarEl) mobileNavbarEl.classList.remove('hidden');
            if (mobileHeaderEl) mobileHeaderEl.style.display = 'flex';
            document.body.classList.add('logged-in');
            
            showToast("성공적으로 로그인되었습니다! 환영합니다! 🐾✨");
            setTimeout(() => {
                scheduleStreakReminder();
                scheduleAiReminder();

                // 온보딩 체크 (아린)
                if (typeof shouldShowOnboarding === 'function' && shouldShowOnboarding()) {
                    setTimeout(() => {
                        if (typeof startOnboarding === 'function') startOnboarding();
                    }, 1000);
                }
            }, 2000);
            switchTab('mypet');
        }, 300);
    }
}

async function executeSocialLogin(provider) {
    // Supabase OAuth 실제 연동 (Google, Kakao)
    if (typeof isSupabaseConnected !== 'undefined' && isSupabaseConnected && supabaseClient) {
        // Supabase가 지원하는 프로바이더 매핑
        const providerMap = {
            'google': 'google',
            'kakao': 'kakao'
        };

        if (providerMap[provider]) {
            try {
                showToast(`${provider === 'google' ? '구글' : '카카오'} 로그인 페이지로 이동합니다...`);

                // 현재 접속 도메인 자동 감지 (Vercel Preview 환경 대응)
                const currentOrigin = window.location.origin;
                const oauthOptions = {
                    redirectTo: currentOrigin.endsWith('/') ? currentOrigin : `${currentOrigin}/`
                };

                // 카카오: 이메일 요청 없이 프로필 정보만 요청 (에러 방지)
                if (provider === 'kakao') {
                    oauthOptions.scopes = 'profile_nickname profile_image';
                }

                const { data, error } = await supabaseClient.auth.signInWithOAuth({
                    provider: providerMap[provider],
                    options: oauthOptions
                });
                if (error) throw error;
                // OAuth 리디렉션이 발생하므로 이 아래 코드는 실행 안됨
                return;
            } catch (e) {
                console.error(`${provider} OAuth 실패:`, e.message);
                showToast(`${provider} 로그인 중 오류가 발생했습니다: ${e.message}`);
                return;
            }
        }

        // 네이버는 Supabase 기본 지원 안됨 → 안내 메시지
        if (provider === 'naver') {
            showToast("네이버 로그인은 Supabase 대시보드에서 Custom OAuth 설정 후 이용 가능합니다.");
            return;
        }
    }

    // Supabase 미연동 시 로컬 폴백 (데모용)
    _localSocialLoginFallback(provider);
}

function _localSocialLoginFallback(provider) {
    const providerInfo = {
        'kakao': { email: 'kakao_butler@kakao.com', name: '카카오 집사' },
        'naver': { email: 'naver_butler@naver.com', name: '네이버 집사' },
        'google': { email: 'google_butler@gmail.com', name: '구글 집사' }
    };
    const info = providerInfo[provider] || { email: 'social@petna.co.kr', name: '소셜 집사' };

    localStorage.setItem('petna_user_nickname', info.name);
    settings_nickname = info.name;
    localStorage.setItem(`petna_social_linked_${provider}`, 'true');
    if (typeof updateSocialLinkButtons === 'function') updateSocialLinkButtons();

    executeLogin(info.email, 'demo1234', true);
}

function triggerLogout() {
    showCustomDialog({
        title: "로그아웃 확인 🐾",
        message: "정말 로그아웃하고 첫 화면으로 이동하시겠습니까?",
        type: "confirm",
        onConfirm: () => {
            localStorage.removeItem('petna_is_logged_in');
            
            // Supabase 세션도 함께 해제 (비동기 처리)
            if (typeof isSupabaseConnected !== 'undefined' && isSupabaseConnected && supabaseClient) {
                supabaseClient.auth.signOut().catch(err => {
                    console.warn('Supabase 로그아웃 중 에러 (무시됨):', err.message);
                });
            }
            
            const loginOverlay = document.getElementById('login-landing-overlay');
            if (loginOverlay) {
                loginOverlay.style.display = 'flex';
                loginOverlay.classList.remove('opacity-0', 'scale-95');
            }
            
            const headerEl = document.querySelector('header');
            const mainEl = document.querySelector('main');
            const mobileNavbarEl = document.getElementById('mobile-navbar');
            const mobileHeaderEl = document.getElementById('mobile-header');
            
            if (headerEl) headerEl.style.display = 'none';
            if (mainEl) mainEl.style.display = 'none';
            if (mobileNavbarEl) mobileNavbarEl.classList.add('hidden');
            if (mobileHeaderEl) mobileHeaderEl.style.display = 'none';
            document.body.classList.remove('logged-in');
            
            showToast("성공적으로 로그아웃되었습니다.");
        }
    });
}

/**
 * 📮 우체통 HTML 템플릿 (UI 구조)
 */
const MAILBOX_TEMPLATE = ``;

function triggerWithdrawal() {
    showCustomDialog({
        title: "회원 탈퇴 경고 ⚠️",
        message: "탈퇴 시 모든 사주, 앨범, 산책 기록 등이 완전히 삭제되며 복구할 수 없습니다. 탈퇴하시겠습니까?",
        type: "confirm",
        onConfirm: async () => {
            try {
                // Supabase 연동 시 실제 계정 삭제
                if (typeof isSupabaseConnected !== 'undefined' && isSupabaseConnected && supabaseClient) {
                    const { error } = await supabaseClient.auth.admin.deleteUser(
                        supabaseClient.auth.getSession()?.data?.session?.user?.id
                    );
                    if (error) {
                        console.error('Supabase 계정 삭제 실패:', error);
                    }
                }

                // 모든 로컬 데이터 완전 삭제
                localStorage.clear();
                sessionStorage.clear();

                // 메모리 변수 즉시 초기화
                pets = JSON.parse(JSON.stringify(INITIAL_PETS));
                walks = [];
                meals = [];
                schedules = JSON.parse(JSON.stringify(INITIAL_SCHEDULES));
                posts = JSON.parse(JSON.stringify(INITIAL_POSTS));
                albums = JSON.parse(JSON.stringify(INITIAL_ALBUM));
                cart = [];

                showToast("회원 탈퇴가 완료되었습니다. 로그인 화면으로 이동합니다.");

                // 로그아웃 처리: 로그인 화면으로 강제 이동
                setTimeout(() => {
                    localStorage.setItem('petna_is_logged_in', 'false');
                    window.location.reload();
                }, 1500);
                // reload() 후 코드는 실행되지 않으므로 제거됨
            } catch (e) {
                console.error('회원 탈퇴 중 오류:', e);
                showToast("⚠️ 회원 탈퇴 중 오류가 발생했습니다.");
            }
        }
    });
}

function toggleSocialLoginLink(provider) {
    const key = `petna_social_linked_${provider}`;
    const current = localStorage.getItem(key) === 'true';
    if (current) {
        localStorage.setItem(key, 'false');
        showToast(`${provider === 'kakao' ? '카카오' : provider === 'naver' ? '네이버' : '구글'} 계정 연동이 해제되었습니다.`);
    } else {
        localStorage.setItem(key, 'true');
        showToast(`${provider === 'kakao' ? '카카오' : provider === 'naver' ? '네이버' : '구글'} 계정이 성공적으로 연동되었습니다! 🔒`);
    }
    if (typeof updateSocialLinkButtons === 'function') {
        updateSocialLinkButtons();
    }
}

function updateSocialLinkButtons() {
    const providers = ['kakao', 'naver', 'google'];
    providers.forEach(p => {
        const btn = document.getElementById(`link-btn-${p}`);
        if (btn) {
            const isLinked = localStorage.getItem(`petna_social_linked_${p}`) === 'true';
            if (isLinked) {
                btn.className = "py-2 rounded-xl border-2 border-brand-500 bg-brand-50 font-bold text-center flex items-center justify-center gap-1 text-brand-700 text-[11px] transition-all";
                btn.innerHTML = `<i class="fa-solid fa-circle-check text-brand-600"></i>연동 완료`;
            } else {
                btn.className = "py-2 rounded-xl border border-gray-200 bg-white font-bold text-center flex items-center justify-center gap-1 hover:bg-gray-50/20 text-gray-700 text-[11px] transition-all";
                if (p === 'kakao') btn.innerHTML = `<i class="fa-solid fa-comment text-yellow-600"></i>카카오 연동`;
                if (p === 'naver') btn.innerHTML = `<i class="fa-solid fa-n text-emerald-600"></i>네이버 연동`;
                if (p === 'google') btn.innerHTML = `<i class="fa-brands fa-google text-rose-600"></i>구글 연동`;
            }
        }
    });
}


function initMypetClock() {
    if (typeof initMypetWeatherWidget === 'function') {
        initMypetWeatherWidget();
    }
}
window.initMypetClock = initMypetClock;

// 스트릭 리마인더 알림 스케줄 (오늘 오후 8시)
function scheduleStreakReminder() {
    if (Notification.permission !== 'granted') return;
    if (!('serviceWorker' in navigator)) return;

    const pet = typeof getActivePet === 'function' ? getActivePet() : null;
    const petName = pet?.name || '반려동물';
    const streak = typeof calcHealthStreak === 'function' ? calcHealthStreak() : 0;
    if (streak === 0) return; // 스트릭 없으면 알림 불필요

    const now = new Date();
    const reminder = new Date(now);
    reminder.setHours(20, 0, 0, 0); // 오늘 오후 8시
    if (reminder <= now) return; // 이미 지났으면 스킵

    const delayMs = reminder.getTime() - now.getTime();
    navigator.serviceWorker.ready.then(reg => {
        reg.active?.postMessage({
            type: 'SCHEDULE_STREAK_REMINDER',
            petName,
            streak,
            delayMs
        });
    });
}

// AI 분석 리마인더 (이번 달 미사용 시 오후 3시)
function scheduleAiReminder() {
    if (Notification.permission !== 'granted') return;
    if (!('serviceWorker' in navigator)) return;
    if (typeof canUseAI === 'function' && !canUseAI()) return; // 한도 소진 시 불필요

    const pet = typeof getActivePet === 'function' ? getActivePet() : null;
    const petName = pet?.name || '반려동물';
    const analyses = typeof getHealthAnalyses === 'function' ? getHealthAnalyses() : [];
    const today = new Date().toISOString().split('T')[0];
    const hasToday = analyses.some(a => a.analyzedAt?.startsWith(today));
    if (hasToday) return;

    const now = new Date();
    const reminder = new Date(now);
    reminder.setHours(15, 0, 0, 0);
    if (reminder <= now) return;

    navigator.serviceWorker.ready.then(reg => {
        reg.active?.postMessage({
            type: 'SCHEDULE_AI_REMINDER',
            petName,
            delayMs: reminder.getTime() - now.getTime()
        });
    });
}

function toggleMoreDrawer() {
    const drawer = document.getElementById('more-drawer');
    const backdrop = document.getElementById('more-drawer-backdrop');
    if (!drawer || !backdrop) return;
    const isOpen = drawer.classList.contains('open');
    drawer.classList.toggle('open', !isOpen);
    backdrop.classList.toggle('open', !isOpen);
}

function updateMoreDrawerDot() {
    const dot = document.getElementById('more-drawer-dot');
    if (!dot) return;
    const mailboxBadge = document.getElementById('mailbox-mobile-badge');
    const cartBadge = document.getElementById('cart-mobile-badge');
    const hasBadge = (mailboxBadge && !mailboxBadge.classList.contains('hidden')) ||
                     (cartBadge && !cartBadge.classList.contains('hidden'));
    dot.classList.toggle('hidden', !hasBadge);
}
