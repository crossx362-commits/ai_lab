// service-status.js - central feature gates for external services.
const PETNNA_LOCKED_MESSAGE = "현재 준비 중인 기능입니다. 설정이 열리면 바로 사용할 수 있게 연결 대기 중입니다.";

function petnnaEnv(name, fallback = "") {
    return (window._env_ && window._env_[name] !== undefined) ? String(window._env_[name]) : fallback;
}

function petnnaFlag(name) {
    return petnnaEnv(name, "false").toLowerCase() === "true";
}

function isAiHealthEnabled() {
    return petnnaFlag("AI_HEALTH_ENABLED") && !!petnnaEnv("AI_HEALTH_PROXY_PATH", "/api/ai-health");
}

function isPaymentEnabled() {
    const hasMonthly = !!petnnaEnv("STRIPE_PAYMENT_LINK");
    const hasYearly = !!petnnaEnv("STRIPE_PAYMENT_LINK_YEARLY") || hasMonthly;
    return petnnaFlag("PAYMENTS_ENABLED") && hasMonthly && hasYearly;
}

function isPremiumActivationEnabled() {
    return petnnaFlag("PREMIUM_ACTIVATION_ENABLED");
}

function isSupabaseReady() {
    return !!petnnaEnv("SUPABASE_URL") && !!petnnaEnv("SUPABASE_ANON_KEY");
}

function getAiHealthProxyPath() {
    return petnnaEnv("AI_HEALTH_PROXY_PATH", "/api/ai-health");
}

function getPetnnaServiceStatus() {
    return [
        {
            id: "supabase",
            label: "클라우드 동기화",
            enabled: isSupabaseReady(),
            readyText: "연결 준비",
            lockedText: "환경값 필요"
        },
        {
            id: "ai",
            label: "AI 건강/상담",
            enabled: isAiHealthEnabled(),
            readyText: "호출 허용",
            lockedText: "호출 차단"
        },
        {
            id: "payment",
            label: "결제/구독",
            enabled: isPaymentEnabled(),
            readyText: "결제 허용",
            lockedText: "결제 차단"
        },
        {
            id: "premium",
            label: "프리미엄 활성화",
            enabled: isPremiumActivationEnabled(),
            readyText: "활성화 허용",
            lockedText: "활성화 차단"
        }
    ];
}

function renderServiceStatusPanel() {
    const list = document.getElementById("service-status-list");
    if (!list) return;

    list.innerHTML = getPetnnaServiceStatus().map(item => {
        const dotClass = item.enabled ? "bg-emerald-500" : "bg-gray-300";
        const textClass = item.enabled ? "text-emerald-700 bg-emerald-50" : "text-gray-500 bg-gray-100";
        const statusText = item.enabled ? item.readyText : item.lockedText;
        return `
            <div class="flex items-center justify-between bg-gray-50/70 border border-gray-100 rounded-xl p-3">
                <div class="flex items-center gap-2 min-w-0">
                    <span class="w-2 h-2 rounded-full ${dotClass} flex-shrink-0"></span>
                    <span class="text-xs font-black text-gray-700 truncate">${item.label}</span>
                </div>
                <span class="text-[10px] font-black px-2 py-1 rounded-full ${textClass}">${statusText}</span>
            </div>
        `;
    }).join("");
}

function notifyPetnnaServiceLocked(featureName = "해당 기능") {
    const message = `${featureName}은 아직 열어두지 않았습니다. 운영 설정을 켜면 바로 연결됩니다.`;
    if (typeof showToast === "function") {
        showToast(message);
    }
    return message;
}
