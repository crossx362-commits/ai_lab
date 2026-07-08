// room-harmony.js — 방에 조화도 통합 및 테마 변경

// 조화도에 따른 방 테마 정의
const HARMONY_THEMES = {
    perfect: {
        score: 90,
        name: '완벽한 조화',
        bgGradient: 'from-rose-50 via-pink-50 to-brand-50',
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
        bgGradient: 'from-blue-50 via-sky-50 to-brand-50',
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
    if (score >= 90) return HARMONY_THEMES.perfect;
    if (score >= 75) return HARMONY_THEMES.great;
    if (score >= 60) return HARMONY_THEMES.good;
    if (score >= 40) return HARMONY_THEMES.normal;
    return HARMONY_THEMES.challenging;
}

// 방 테마 업데이트 (조화도 기반)
function updateRoomThemeByHarmony() {
    const roomCard = document.getElementById('pet-room-card');
    const sajuBtn = document.getElementById('room-saju-btn');

    // 펫 객체 가져오기
    const currentPet = (typeof getActivePet === 'function') ? getActivePet() : null;
    // 현재 펫에 sajuData 없으면 다른 펫에서 탐색 (펫 인덱스 불일치 대응)
    const allPets = (typeof AppStore !== 'undefined') ? (AppStore.getState('pets') || []) : [];
    const sajuData = currentPet?.sajuData?.petBirth ? currentPet.sajuData
        : allPets.find(p => p?.sajuData?.petBirth)?.sajuData || currentPet?.sajuData;
    const harmonyResult = currentPet?.harmonyData || (typeof AppStore !== 'undefined' ? AppStore.getState('harmonyResult') : null);
    const petSaju = sajuData?.petBirth ? { year: sajuData.petBirth.split('-')[0] } : ((typeof AppStore !== 'undefined') ? AppStore.getState('petSaju') : null);
    const butlerSaju = sajuData?.ownerBirth ? { year: sajuData.ownerBirth.split('-')[0] } : ((typeof AppStore !== 'undefined') ? AppStore.getState('butlerSaju') : null);

    // 헤더 오른쪽 사주 카드 업데이트 (항상 실행)
    const roomSajuCard = document.getElementById('room-saju-card');
    if (roomSajuCard) {
        const butlerYear = butlerSaju?.year || '--';
        const petYear = petSaju?.year || '--';
        const butlerEl = document.getElementById('room-saju-butler');
        const petEl = document.getElementById('room-saju-pet');
        const scoreEl = document.getElementById('room-saju-score');
        const messageEl = document.getElementById('room-saju-message');
        const ownerSummaryEl = document.getElementById('room-saju-owner-summary');
        const petSummaryEl = document.getElementById('room-saju-pet-summary');

        if (butlerEl) butlerEl.textContent = `${butlerYear}년생`;
        if (petEl) petEl.textContent = `${petYear}년생`;

        // 요약 텍스트 업데이트
        if (sajuData) {
            const colorize = (text) => {
                if (!text) return "";
                return text.replace('木 (나무)', '<span class="text-emerald-600 font-bold">木 (나무)</span>')
                           .replace('火 (불)', '<span class="text-rose-600 font-bold">火 (불)</span>')
                           .replace('土 (흙)', '<span class="text-amber-700 font-bold">土 (흙)</span>')
                           .replace('金 (쇠)', '<span class="text-gray-600 font-bold">金 (쇠)</span>')
                           .replace('水 (물)', '<span class="text-sky-600 font-bold">水 (물)</span>');
            };
            if (ownerSummaryEl) ownerSummaryEl.innerHTML = colorize(sajuData.ownerSummary);
            if (petSummaryEl) petSummaryEl.innerHTML = colorize(sajuData.petSummary);
        } else {
            if (ownerSummaryEl) ownerSummaryEl.textContent = '';
            if (petSummaryEl) petSummaryEl.textContent = '';
        }

        // 조화도 결과가 있으면 점수와 메시지 표시
        let score = 0;
        let message = '조화도 탭에서 사주 조화도를 분석해보세요';
        let isMeasured = false;

        if (harmonyResult && harmonyResult.avgScore) {
            score = Math.round(harmonyResult.avgScore);
            isMeasured = true;
            if (score >= 90) message = '영혼의 단짝! 완벽한 듀오입니다';
            else if (score >= 75) message = '서로를 잘 이해하는 최고의 파트너';
            else if (score >= 60) message = '서로에게 긍정적인 영향을 주는 관계';
            else if (score >= 40) message = '노력하면 더욱 발전할 수 있는 관계';
            else message = '서로 다른 성향, 이해와 배려가 필요해요';
        } else if (sajuData && sajuData.compatScore) {
            score = Math.round(sajuData.compatScore);
            isMeasured = true;
            message = sajuData.compatTitle ? `${sajuData.compatTitle}: ${sajuData.pastDesc || ''}` : '사주 조화도 분석 완료!';
        }

        if (isMeasured) {
            if (scoreEl) {
                scoreEl.textContent = `${score}점`;
                scoreEl.className = 'text-[9px] font-bold text-rose-600 bg-rose-50 px-2 py-0.5 rounded-full';
            }
            if (messageEl) {
                messageEl.textContent = message;
            }
        } else {
            if (scoreEl) {
                scoreEl.textContent = '미측정';
                scoreEl.className = 'text-[9px] font-bold text-gray-400 bg-gray-50 px-2 py-0.5 rounded-full';
            }
            if (messageEl) {
                messageEl.textContent = '조화도 탭에서 사주 조화도를 분석해보세요';
            }
        }
    }

    // 조화도 계산 완료 여부 확인
    if (!harmonyResult || !harmonyResult.avgScore || !roomCard) {
        return;
    }

    // 조화도 점수
    const score = harmonyResult.avgScore || 0;
    const theme = getThemeByHarmonyScore(score);

    // 방 배경 테마 변경
    roomCard.className = `bg-gradient-to-br ${theme.bgGradient} rounded-3xl border ${theme.borderColor} shadow-lg overflow-hidden transition-all duration-500`;

    // 조화도 위젯 카드 업데이트 (오른쪽 사이드바)
    const harmonyWidgetCard = document.getElementById('harmony-widget-card');
    if (harmonyWidgetCard) {
        const widgetIcon = document.getElementById('harmony-widget-icon');
        const widgetScore = document.getElementById('harmony-widget-score');
        const widgetTitle = document.getElementById('harmony-widget-title');
        const widgetButler = document.getElementById('harmony-widget-butler');
        const widgetPet = document.getElementById('harmony-widget-pet');

        if (widgetIcon) widgetIcon.textContent = theme.icon.split('')[0];
        if (widgetScore) widgetScore.textContent = `${score}점`;
        if (widgetTitle) widgetTitle.textContent = theme.name;

        if (widgetButler && butlerSaju) {
            widgetButler.textContent = `${butlerSaju.year || butlerYear}년생`;
        }
        if (widgetPet && petSaju) {
            widgetPet.textContent = `${petSaju.year || petYear}년생`;
        }
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
