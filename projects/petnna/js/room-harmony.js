// room-harmony.js — 방에 조화도 통합 및 테마 변경

// 조화도에 따른 방 테마 정의
const ROOM_THEMES = {
    perfect: {
        score: 90,
        name: '완벽한 조화',
        bgGradient: 'from-rose-50 via-pink-50 to-purple-50',
        borderColor: 'border-rose-200',
        icon: '💖✨',
        message: '영혼의 단짝! 완벽한 듀오입니다'
    },
    great: {
        score: 75,
        name: '훌륭한 조화',
        bgGradient: 'from-amber-50 via-orange-50 to-yellow-50',
        borderColor: 'border-amber-200',
        icon: '💛🌟',
        message: '서로를 잘 이해하는 최고의 파트너'
    },
    good: {
        score: 60,
        name: '좋은 조화',
        bgGradient: 'from-emerald-50 via-teal-50 to-cyan-50',
        borderColor: 'border-emerald-200',
        icon: '💚🍀',
        message: '서로에게 긍정적인 영향을 주는 관계'
    },
    normal: {
        score: 40,
        name: '평범한 조화',
        bgGradient: 'from-blue-50 via-sky-50 to-indigo-50',
        borderColor: 'border-blue-200',
        icon: '💙⭐',
        message: '노력하면 더욱 발전할 수 있는 관계'
    },
    challenging: {
        score: 0,
        name: '도전적인 조화',
        bgGradient: 'from-gray-50 via-slate-50 to-zinc-50',
        borderColor: 'border-gray-200',
        icon: '🤍🌈',
        message: '서로 다른 성향, 이해와 배려가 필요해요'
    }
};

// 조화도 점수로 테마 가져오기
function getThemeByHarmonyScore(score) {
    if (score >= 90) return ROOM_THEMES.perfect;
    if (score >= 75) return ROOM_THEMES.great;
    if (score >= 60) return ROOM_THEMES.good;
    if (score >= 40) return ROOM_THEMES.normal;
    return ROOM_THEMES.challenging;
}

// 방 테마 업데이트 (조화도 기반)
function updateRoomThemeByHarmony() {
    const roomCard = document.getElementById('pet-room-card');
    const harmonyBadge = document.getElementById('room-harmony-badge');
    const harmonyScore = document.getElementById('room-harmony-score');
    const harmonyIcon = document.getElementById('room-harmony-icon');
    const sajuBtn = document.getElementById('room-saju-btn');

    // 조화도 설명 요소
    const harmonyMessage = document.getElementById('room-harmony-message');
    const harmonyMessageIcon = document.getElementById('room-harmony-message-icon');
    const harmonyMessageTitle = document.getElementById('room-harmony-message-title');
    const harmonyMessageText = document.getElementById('room-harmony-message-text');

    if (!roomCard) return;

    // 사주 결과에서 조화도 가져오기
    const petSaju = (typeof AppStore !== 'undefined') ? AppStore.getState('petSaju') : null;
    const butlerSaju = (typeof AppStore !== 'undefined') ? AppStore.getState('butlerSaju') : null;
    const harmonyResult = (typeof AppStore !== 'undefined') ? AppStore.getState('harmonyResult') : null;

    // 조화도 계산 완료 여부 확인
    if (!harmonyResult || !harmonyResult.avgScore) {
        // 조화도 미분석 상태: 기본 테마
        roomCard.className = 'bg-white rounded-3xl border border-amber-100 shadow-sm overflow-hidden transition-all duration-500';

        if (harmonyBadge) harmonyBadge.classList.add('hidden');
        if (harmonyMessage) harmonyMessage.classList.add('hidden');
        if (sajuBtn) sajuBtn.classList.remove('hidden'); // 사주 분석 버튼 표시

        return;
    }

    // 조화도 점수
    const score = harmonyResult.avgScore || 0;
    const theme = getThemeByHarmonyScore(score);

    // 방 배경 테마 변경
    roomCard.className = `bg-gradient-to-br ${theme.bgGradient} rounded-3xl border ${theme.borderColor} shadow-lg overflow-hidden transition-all duration-500`;

    // 조화도 배지 표시
    if (harmonyBadge) {
        harmonyBadge.classList.remove('hidden');
        harmonyBadge.classList.add('flex');
        harmonyBadge.className = `flex items-center gap-1 px-2 py-0.5 bg-white/80 backdrop-blur-sm rounded-full border ${theme.borderColor} shadow-sm`;
    }

    if (harmonyIcon) {
        harmonyIcon.textContent = theme.icon.split('')[0]; // 첫 번째 이모지만
    }

    if (harmonyScore) {
        harmonyScore.textContent = `${score}점`;
        harmonyScore.className = 'text-[10px] font-black bg-gradient-to-r from-rose-600 to-pink-600 bg-clip-text text-transparent';
    }

    // 조화도 설명 박스 표시
    if (harmonyMessage) {
        harmonyMessage.classList.remove('hidden');
        harmonyMessage.className = `bg-white/60 backdrop-blur-sm border ${theme.borderColor} rounded-xl p-2.5 mt-2 transition-all duration-500`;
    }

    if (harmonyMessageIcon) {
        harmonyMessageIcon.textContent = theme.icon;
    }

    if (harmonyMessageTitle) {
        harmonyMessageTitle.textContent = theme.name;
    }

    if (harmonyMessageText) {
        harmonyMessageText.textContent = theme.message;
    }

    // 오른쪽 조화도 설명 카드 표시
    const harmonyDescCard = document.getElementById('harmony-description-card');
    if (harmonyDescCard) {
        harmonyDescCard.classList.remove('hidden');
        harmonyDescCard.innerHTML = `
            <p class="text-[11px] font-medium leading-relaxed">
                <span class="font-black text-rose-700">${theme.icon} ${theme.name}</span>
                <span class="text-gray-600 ml-1">${theme.message}</span>
            </p>
        `;
    }

    // 사주 분석 버튼 유지 (재분석 가능)
    if (sajuBtn) {
        sajuBtn.classList.remove('hidden');
        sajuBtn.title = `조화도 ${score}점 - 클릭하여 상세 보기`;
    }

    console.log(`✨ Room theme updated: ${theme.name} (${score}점)`);
}

// 조화도 계산 후 방 테마 자동 업데이트
function onHarmonyCalculated() {
    updateRoomThemeByHarmony();

    if (typeof showToast === 'function') {
        const harmonyResult = (typeof AppStore !== 'undefined') ? AppStore.getState('harmonyResult') : null;
        const score = harmonyResult?.avgScore || 0;
        const theme = getThemeByHarmonyScore(score);
        showToast(`${theme.icon} ${theme.message} (${score}점)`);
    }
}

// 페이지 로드 시 조화도 확인 및 방 테마 업데이트
if (typeof window !== 'undefined') {
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', updateRoomThemeByHarmony);
    } else {
        updateRoomThemeByHarmony();
    }

    // 주기적 업데이트 (사주 탭에서 분석 완료 시)
    setInterval(updateRoomThemeByHarmony, 3000);
}
