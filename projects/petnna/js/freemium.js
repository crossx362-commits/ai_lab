// freemium.js — 월별 AI 분석 횟수 추적 + 프리미엄 게이트

const FREE_LIMIT = 5; // 월 무료 AI 분석 횟수
const PREMIUM_PRICE = "5,900원";
const PREMIUM_PRICE_NUM = 5900;

function getMonthlyAiUsage() {
    const key = "petna_ai_health_count_" + new Date().toISOString().slice(0, 7);
    return parseInt(localStorage.getItem(key) || "0");
}

function isPremium() {
    return !!localStorage.getItem("petna_premium");
}

function getPremiumTier() {
    return localStorage.getItem("petna_premium") || null; // "stripe_verified" | "demo" | null
}

function getRemainingFreeAnalyses() {
    if (isPremium()) return Infinity;
    return Math.max(0, FREE_LIMIT - getMonthlyAiUsage());
}

function updateAiHealthUsageBadge() {
    const badge = document.getElementById("ai-health-usage-badge");
    if (!badge) return;
    if (isPremium()) {
        badge.textContent = "프리미엄 ∞";
        badge.className = "text-[10px] font-black text-amber-500 bg-amber-50 px-2 py-0.5 rounded-full";
    } else {
        const rem = getRemainingFreeAnalyses();
        badge.textContent = `이번 달 ${rem}회 남음`;
        badge.className = `text-[10px] font-black px-2 py-0.5 rounded-full ${rem > 0 ? 'text-violet-500 bg-violet-50' : 'text-red-500 bg-red-50'}`;
    }
}

function showPremiumModal() {
    const modal = document.getElementById("premium-modal");
    if (modal) modal.classList.remove("hidden");
}

function closePremiumModal() {
    const modal = document.getElementById("premium-modal");
    if (modal) modal.classList.add("hidden");
}

function canUseAI() {
    return isPremium() || getMonthlyAiUsage() < FREE_LIMIT;
}

function incrementAIUsage() {
    const key = "petna_ai_health_count_" + new Date().toISOString().slice(0, 7);
    localStorage.setItem(key, String(getMonthlyAiUsage() + 1));
}

// 연간/월간 플랜 선택 상태
let _premiumPlanSelected = 'monthly'; // 'monthly' | 'yearly'

// 구독 플랜 탭 전환 UI
function selectPremiumPlan(plan) {
    _premiumPlanSelected = plan;
    const monthly = document.getElementById('premium-plan-monthly');
    const yearly = document.getElementById('premium-plan-yearly');
    const priceMain = document.getElementById('premium-price-main');
    const priceSub = document.getElementById('premium-price-sub');
    const yearlyBonus = document.getElementById('premium-yearly-bonus');
    if (!monthly || !yearly || !priceMain || !priceSub) return;

    if (plan === 'yearly') {
        monthly.className = 'flex-1 py-2 text-xs font-bold rounded-xl text-gray-400 transition-all';
        yearly.className = 'flex-1 py-2 text-xs font-black rounded-xl bg-white text-violet-700 shadow-sm transition-all relative';
        priceMain.textContent = '연 49,000원';
        priceSub.textContent = '월 4,083원 · 월간 대비 30% 절약 🎉';
        if (yearlyBonus) yearlyBonus.classList.remove('hidden');
    } else {
        monthly.className = 'flex-1 py-2 text-xs font-black rounded-xl bg-white text-violet-700 shadow-sm transition-all';
        yearly.className = 'flex-1 py-2 text-xs font-bold rounded-xl text-gray-400 transition-all relative';
        priceMain.textContent = '월 5,900원';
        priceSub.textContent = 'TTcare 대비 올인원 · 해지 언제든 가능';
        if (yearlyBonus) yearlyBonus.classList.add('hidden');
    }
}

// Stripe Payment Link 결제 시작
function startStripeCheckout() {
    const paymentsReady = (typeof isPaymentEnabled === 'function') ? isPaymentEnabled() : false;
    if (!paymentsReady) {
        showPremiumWaitlist();
        if (typeof notifyPetnnaServiceLocked === 'function') notifyPetnnaServiceLocked('프리미엄 결제');
        return;
    }

    const isYearly = _premiumPlanSelected === 'yearly';
    const paymentLink = isYearly
        ? (window._env_?.STRIPE_PAYMENT_LINK_YEARLY || window._env_?.STRIPE_PAYMENT_LINK || "")
        : (window._env_?.STRIPE_PAYMENT_LINK || "");
    if (!paymentLink) {
        // 결제 링크 미설정 시 이메일 웨이팅 리스트로 fallback
        showPremiumWaitlist();
        return;
    }
    const returnUrl = encodeURIComponent(window.location.href.split('?')[0] + '?premium=activated');
    window.open(`${paymentLink}?client_reference_id=${encodeURIComponent(settings_email || 'guest')}&success_url=${returnUrl}`, '_blank');
    showToast(`Stripe 결제 페이지로 이동합니다. ${isYearly ? '연간(49,000원)' : '월간(5,900원)'} 결제 완료 후 돌아와 주세요! 💳`);
}

// 결제 완료 후 URL 파라미터로 프리미엄 활성화
function checkPremiumFromUrl() {
    const activationReady = (typeof isPremiumActivationEnabled === 'function') ? isPremiumActivationEnabled() : false;
    if (!activationReady) return;
    const params = new URLSearchParams(window.location.search);
    if (params.get('premium') === 'activated') {
        localStorage.setItem("petna_premium", "stripe_verified");
        updateAiHealthUsageBadge();
        showToast("🎉 프리미엄 활성화 완료! AI 분석 무제한 사용 가능합니다 👑");
        // URL 파라미터 제거
        window.history.replaceState({}, '', window.location.pathname);
    }
}

// 결제 링크 미설정 시 이메일 웨이팅 리스트
function showPremiumWaitlist() {
    const email = typeof settings_email !== 'undefined' ? settings_email : '';
    showCustomDialog({
        title: "프리미엄 사전 신청 👑",
        message: `월 ${PREMIUM_PRICE}로 AI 건강분석 무제한 + 건강 리포트 PDF + 우선 지원을 받으세요. 결제 오픈 시 이메일로 먼저 안내드립니다.`,
        type: "prompt",
        placeholder: email || "이메일 주소 입력",
        val: email,
        onConfirm: (inputEmail) => {
            if (!inputEmail || !inputEmail.includes('@')) {
                showToast("올바른 이메일 주소를 입력해주세요.");
                return;
            }
            const list = JSON.parse(localStorage.getItem("petna_waitlist") || "[]");
            if (!list.includes(inputEmail)) {
                list.push(inputEmail);
                localStorage.setItem("petna_waitlist", JSON.stringify(list));
            }
            closePremiumModal();
            showToast("사전 신청 완료! 결제 오픈 시 가장 먼저 알려드릴게요 💌");
        }
    });
}

// 테스트 전용 — 프로덕션 배포 전 반드시 이 버튼 숨기거나 제거
function activatePremiumDemo() {
    const isLocal = ['localhost', '127.0.0.1'].some(h => location.hostname.includes(h));
    if (!isLocal) return;
    if (typeof isPremiumActivationEnabled === 'function' && !isPremiumActivationEnabled()) {
        notifyPetnnaServiceLocked('프리미엄 테스트 활성화');
        return;
    }
    console.warn("[PETNA] activatePremiumDemo: 테스트 전용. 프로덕션에서 제거 필요.");
    localStorage.setItem("petna_premium", "demo");
    closePremiumModal();
    updateAiHealthUsageBadge();
    showToast("[테스트] 프리미엄 활성화! 실제 배포 시 Stripe 연동 필요 🚧");
}
