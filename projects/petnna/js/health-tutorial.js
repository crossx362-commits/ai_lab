// health-tutorial.js — 건강 트렌드 사용법 안내 표시

// 건강 데이터가 비어있을 때 튜토리얼 표시
function updateHealthTutorialVisibility() {
    const tutorialEl = document.getElementById('health-tutorial');
    if (!tutorialEl) return;

    const history = (typeof healthLogs !== 'undefined' && healthLogs.history) ? healthLogs.history : [];
    const last7Days = typeof getLast7DaysHealthData === 'function' ? getLast7DaysHealthData() : [];
    const hasData = last7Days.some(d => d.food > 0 || d.water > 0 || d.poop > 0);

    // 데이터가 없으면 튜토리얼 표시, 있으면 숨김
    if (hasData || history.length > 0) {
        tutorialEl.classList.add('hidden');
    } else {
        tutorialEl.classList.remove('hidden');
    }
}

// 페이지 로드 시 및 데이터 변경 시 튜토리얼 업데이트
if (typeof window !== 'undefined') {
    // DOM 로드 후 실행
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', updateHealthTutorialVisibility);
    } else {
        updateHealthTutorialVisibility();
    }

    // 탭 전환 시에도 체크 (MyPets 탭으로 돌아올 때)
    const observer = new MutationObserver(() => {
        updateHealthTutorialVisibility();
    });

    // health-tutorial 요소가 있으면 관찰 시작
    const checkTutorialElement = setInterval(() => {
        const tutorialEl = document.getElementById('health-tutorial');
        if (tutorialEl) {
            clearInterval(checkTutorialElement);
            updateHealthTutorialVisibility();
        }
    }, 500);

    // 5초 후 종료
    setTimeout(() => clearInterval(checkTutorialElement), 5000);
}
