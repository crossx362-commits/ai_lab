// onboarding.js — 신규 사용자 온보딩 시퀀스 (아린)

const ONBOARDING_STEPS = [
    {
        title: "펫과나에 오신 걸 환영합니다! 🐾",
        content: "AI 건강 분석, GPS 산책 기록, 사주 조화도까지!\n반려동물을 위한 모든 케어를 한곳에서 관리하세요.",
        icon: "🎉",
        image: "https://images.unsplash.com/photo-1548199973-03cce0bbc87b?auto=format&fit=crop&q=80&w=400"
    },
    {
        title: "🏠 마이펫 — 우리 아이 프로필",
        content: "반려동물 프로필을 등록하고\n건강, 성격, 일정을 한눈에 관리하세요.\n돌봄 스케줄러로 놓치는 일정이 없어요!",
        icon: "🏠",
        tab: "mypet"
    },
    {
        title: "🏥 건강 — AI 분석 & 기록",
        content: "사진 한 장으로 10가지 건강 항목 분석!\n7일 트렌드 차트로 건강 변화를 추적하고,\nAI 수의사와 실시간 상담까지 가능해요.",
        icon: "🏥",
        tab: "health"
    },
    {
        title: "🗺️ 산책 — GPS 기록 & 지도",
        content: "실시간 GPS로 산책 경로를 기록하고,\n거리, 시간, 칼로리를 자동 계산해요.\n배변 위치도 지도에 표시됩니다!",
        icon: "🗺️",
        tab: "walk"
    },
    {
        title: "☯️ 조화도 — 사주팔자 분석",
        content: "반려동물의 사주팔자를 분석하고,\n집사님과의 조화도, 방 풍수까지 확인하세요.\n재미있는 운세 게임도 즐겨보세요!",
        icon: "☯️",
        tab: "saju"
    },
    {
        title: "이제 첫 반려동물을 등록해볼까요? 🐾",
        content: "프로필만 등록하면 건강 분석·산책 기록·사주까지\n바로 시작할 수 있어요. 1분이면 충분해요!",
        icon: "🐾",
        isRegister: true
    }
];

function hasCompletedOnboarding() {
    const email = localStorage.getItem('petna_user_email') || '';
    return localStorage.getItem(`petna_onboarding_completed_${email}`) === 'true';
}

function markOnboardingCompleted() {
    const email = localStorage.getItem('petna_user_email') || '';
    localStorage.setItem(`petna_onboarding_completed_${email}`, 'true');
}

function shouldShowOnboarding() {
    // 로그인 직후 & 온보딩 미완료 & 펫이 없을 때
    const isLoggedIn = localStorage.getItem('petna_is_logged_in') === 'true';
    const hasPets = pets && pets.length > 0;
    return isLoggedIn && !hasCompletedOnboarding() && !hasPets;
}

let currentOnboardingStep = 0;

function startOnboarding() {
    currentOnboardingStep = 0;
    showOnboardingModal();
}

function showOnboardingModal() {
    const step = ONBOARDING_STEPS[currentOnboardingStep];
    if (!step) {
        completeOnboarding();
        return;
    }

    const modal = document.createElement('div');
    modal.id = 'onboarding-modal';
    modal.className = 'fixed inset-0 bg-black/70 backdrop-blur-sm z-50 flex items-center justify-center p-4 animate-fade-in';

    const progress = ((currentOnboardingStep + 1) / ONBOARDING_STEPS.length) * 100;

    modal.innerHTML = `
        <div class="bg-white rounded-3xl max-w-md w-full shadow-2xl overflow-hidden animate-scale-in">
            <!-- Progress Bar -->
            <div class="h-1 bg-gray-200">
                <div class="h-full bg-gradient-to-r from-brand-400 to-brand-600 transition-all duration-500" style="width: ${progress}%"></div>
            </div>

            <!-- Content -->
            <div class="p-8 text-center space-y-4">
                <div class="text-6xl mb-4">${step.icon}</div>
                <h2 class="text-xl font-black text-gray-800">${step.title}</h2>
                <p class="text-sm text-gray-600 leading-relaxed whitespace-pre-line">${step.content}</p>

                ${step.image ? `
                    <div class="mt-4 rounded-2xl overflow-hidden border-2 border-brand-100">
                        <img src="${step.image}" alt="preview" class="w-full h-48 object-cover">
                    </div>
                ` : ''}

                <!-- Step Indicators -->
                <div class="flex justify-center gap-2 pt-4">
                    ${ONBOARDING_STEPS.map((_, idx) => `
                        <div class="w-2 h-2 rounded-full ${idx === currentOnboardingStep ? 'bg-brand-500' : 'bg-gray-300'}"></div>
                    `).join('')}
                </div>
            </div>

            <!-- Actions -->
            <div class="p-6 bg-gray-50 border-t flex gap-3">
                ${currentOnboardingStep > 0 ? `
                    <button onclick="prevOnboardingStep()" class="flex-1 py-3 rounded-xl border-2 border-gray-300 font-bold text-gray-700 hover:bg-gray-100 transition-all">
                        ← 이전
                    </button>
                ` : `
                    <button onclick="skipOnboarding()" class="flex-1 py-3 rounded-xl border-2 border-gray-300 font-bold text-gray-500 hover:bg-gray-100 transition-all">
                        건너뛰기
                    </button>
                `}
                <button onclick="nextOnboardingStep()" class="flex-1 py-3 rounded-xl bg-gradient-to-r from-brand-500 to-brand-600 text-white font-black hover:shadow-lg transition-all">
                    ${step.isRegister ? '🐾 반려동물 등록하기' : (currentOnboardingStep === ONBOARDING_STEPS.length - 1 ? '시작하기 🚀' : '다음 →')}
                </button>
            </div>
        </div>
    `;

    // Remove existing modal if any
    const existingModal = document.getElementById('onboarding-modal');
    if (existingModal) existingModal.remove();

    document.body.appendChild(modal);
}

function nextOnboardingStep() {
    currentOnboardingStep++;
    if (currentOnboardingStep >= ONBOARDING_STEPS.length) {
        completeOnboarding();
    } else {
        showOnboardingModal();
    }
}

function prevOnboardingStep() {
    if (currentOnboardingStep > 0) {
        currentOnboardingStep--;
        showOnboardingModal();
    }
}

function skipOnboarding() {
    showCustomDialog({
        title: "온보딩 건너뛰기",
        message: "온보딩을 건너뛰시겠습니까?\n언제든 설정에서 다시 볼 수 있습니다.",
        type: "confirm",
        onConfirm: () => {
            completeOnboarding();
        }
    });
}

function completeOnboarding() {
    const modal = document.getElementById('onboarding-modal');
    if (modal) modal.remove();

    markOnboardingCompleted();
    showToast("🎉 펫과나에 오신 것을 환영합니다! 반려동물을 등록해보세요!");

    // 펫 등록 모달 자동 오픈
    setTimeout(() => {
        if (typeof openPetRegistrationModal === 'function') {
            openPetRegistrationModal();
        }
    }, 500);
}

// 온보딩 재시작 (설정에서 호출)
function restartOnboarding() {
    const email = localStorage.getItem('petna_user_email') || '';
    localStorage.removeItem(`petna_onboarding_completed_${email}`);
    startOnboarding();
}
