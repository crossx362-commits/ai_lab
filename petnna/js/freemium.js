// freemium.js — 월별 AI 분석 횟수 추적 + 프리미엄 게이트

const FREE_LIMIT = 3;
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

// Stripe Payment Link 결제 시작
function startStripeCheckout() {
    const paymentLink = window._env_?.STRIPE_PAYMENT_LINK || "";
    if (!paymentLink) {
        // 결제 링크 미설정 시 이메일 웨이팅 리스트로 fallback
        showPremiumWaitlist();
        return;
    }
    const returnUrl = encodeURIComponent(window.location.href.split('?')[0] + '?premium=activated');
    window.open(`${paymentLink}?client_reference_id=${encodeURIComponent(settings_email || 'guest')}&success_url=${returnUrl}`, '_blank');
    showToast("Stripe 결제 페이지로 이동합니다. 결제 완료 후 돌아와 주세요! 💳");
}

// 결제 완료 후 URL 파라미터로 프리미엄 활성화
function checkPremiumFromUrl() {
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
    console.warn("[PETNA] activatePremiumDemo: 테스트 전용. 프로덕션에서 제거 필요.");
    localStorage.setItem("petna_premium", "demo");
    closePremiumModal();
    updateAiHealthUsageBadge();
    showToast("[테스트] 프리미엄 활성화! 실제 배포 시 Stripe 연동 필요 🚧");
}
