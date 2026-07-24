// care-scheduler-ui.js — 돌봄 스케줄러 UI 제어

let selectedCareType = 'feed';
let selectedRepeatDays = [0, 1, 2, 3, 4, 5, 6]; // 기본값: 매일

function triggerAgeBasedReminders() {
    const pet = typeof getActivePet === 'function' ? getActivePet() : null;
    if (!pet) {
        if (typeof showToast === 'function') showToast('활성 반려동물을 찾을 수 없습니다 🐾');
        return;
    }
    const count = typeof checkAndAddAgeBasedReminders === 'function' ? checkAndAddAgeBasedReminders(pet) : 0;
    if (typeof showToast === 'function') {
        if (count > 0) {
            showToast(`${count}개의 월령별 알림을 추가했습니다 🔔`);
        } else {
            showToast('추가할 새 알림이 없습니다 ✅');
        }
    }
}

function runDailyAgeReminderCheck() {
    const today = new Date().toISOString().split('T')[0];
    const lastCheckKey = 'petna_age_reminder_last_check';
    const lastCheck = localStorage.getItem(lastCheckKey);
    if (lastCheck === today) return;
    localStorage.setItem(lastCheckKey, today);
    const pet = typeof getActivePet === 'function' ? getActivePet() : null;
    if (pet && typeof checkAndAddAgeBasedReminders === 'function') {
        checkAndAddAgeBasedReminders(pet);
    }
}

// 돌봄 일정 추가 모달 열기
function openCareScheduleModal() {
    const modal = document.getElementById('care-schedule-modal');
    if (!modal) return;
    modal.classList.remove('hidden');
    modal.classList.add('flex');

    // 기본값 설정
    const now = new Date();
    const timeInput = document.getElementById('care-schedule-time');
    if (timeInput) timeInput.value = `${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`;

    const dateInput = document.getElementById('care-schedule-date');
    if (dateInput) dateInput.value = now.toISOString().split('T')[0];

    // 첫 번째 타입 선택
    const firstTypeBtn = document.querySelector('.care-type-btn');
    if (firstTypeBtn) selectCareType('feed', firstTypeBtn);
}

// 돌봄 일정 추가 모달 닫기
function closeCareScheduleModal() {
    const modal = document.getElementById('care-schedule-modal');
    if (!modal) return;
    modal.classList.add('hidden');
    modal.classList.remove('flex');

    // 폼 초기화
    const titleInput = document.getElementById('care-schedule-title');
    if (titleInput) titleInput.value = '';
    const notesInput = document.getElementById('care-schedule-notes');
    if (notesInput) notesInput.value = '';
    const pillTotalInput = document.getElementById('care-schedule-pill-total');
    if (pillTotalInput) pillTotalInput.value = '';
    const dosePerDayInput = document.getElementById('care-schedule-dose-per-day');
    if (dosePerDayInput) dosePerDayInput.value = '';

    selectedCareType = 'feed';
    selectedRepeatDays = [0, 1, 2, 3, 4, 5, 6];
}

// 돌봄 타입 선택
function selectCareType(type, btnElement) {
    selectedCareType = type;

    // 모든 버튼 비활성화
    document.querySelectorAll('.care-type-btn').forEach(btn => {
        btn.classList.remove('bg-sky-500', 'text-white', 'border-sky-500');
        btn.classList.add('bg-white', 'border-gray-200');
    });

    // 선택된 버튼 활성화
    if (btnElement) {
        btnElement.classList.add('bg-sky-500', 'text-white', 'border-sky-500');
        btnElement.classList.remove('bg-white', 'border-gray-200');
    }

    // 처방약 리필 필드는 투약(medicine) 타입일 때만 노출
    const refillField = document.getElementById('care-schedule-refill-field');
    if (refillField) refillField.classList.toggle('hidden', type !== 'medicine');
}

// 반복 설정 변경 시 요일/날짜 필드 토글
function toggleRepeatDays() {
    const repeatSelect = document.getElementById('care-schedule-repeat');
    const repeatDaysDiv = document.getElementById('care-schedule-repeat-days');
    const dateField = document.getElementById('care-schedule-date-field');

    if (!repeatSelect || !repeatDaysDiv || !dateField) return;

    const value = repeatSelect.value;

    if (value === 'weekly') {
        repeatDaysDiv.classList.remove('hidden');
        dateField.classList.add('hidden');
    } else if (value === 'once') {
        repeatDaysDiv.classList.add('hidden');
        dateField.classList.remove('hidden');
    } else {
        repeatDaysDiv.classList.add('hidden');
        dateField.classList.add('hidden');
    }
}

// 반복 요일 토글
function toggleRepeatDay(day, btnElement) {
    const idx = selectedRepeatDays.indexOf(day);

    if (idx >= 0) {
        // 이미 선택됨 -> 제거
        selectedRepeatDays.splice(idx, 1);
        btnElement.classList.remove('bg-sky-500', 'text-white', 'border-sky-500');
        btnElement.classList.add('bg-white', 'border-gray-200');
    } else {
        // 선택 안됨 -> 추가
        selectedRepeatDays.push(day);
        btnElement.classList.add('bg-sky-500', 'text-white', 'border-sky-500');
        btnElement.classList.remove('bg-white', 'border-gray-200');
    }
}

// 일정 추가 제출
function submitCareSchedule() {
    const titleInput = document.getElementById('care-schedule-title');
    const timeInput = document.getElementById('care-schedule-time');
    const repeatSelect = document.getElementById('care-schedule-repeat');
    const dateInput = document.getElementById('care-schedule-date');
    const notesInput = document.getElementById('care-schedule-notes');

    if (!titleInput || !timeInput || !repeatSelect) return;

    const title = titleInput.value.trim();
    const time = timeInput.value;
    const repeat = repeatSelect.value;
    const notes = notesInput ? notesInput.value.trim() : '';

    if (!title) {
        if (typeof showToast === 'function') showToast('일정 제목을 입력해주세요 ✏️');
        return;
    }

    if (!time) {
        if (typeof showToast === 'function') showToast('시간을 선택해주세요 ⏰');
        return;
    }

    const schedule = {
        type: selectedCareType,
        title: title,
        time: time,
        repeat: repeat,
        repeatDays: repeat === 'weekly' ? selectedRepeatDays : [0, 1, 2, 3, 4, 5, 6],
        date: repeat === 'once' && dateInput ? dateInput.value : null,
        notes: notes
    };

    // 처방약 리필: 투약 타입 + 총 수량·1일 투여 횟수 입력 시에만 저장
    if (selectedCareType === 'medicine') {
        const pillTotalInput = document.getElementById('care-schedule-pill-total');
        const dosePerDayInput = document.getElementById('care-schedule-dose-per-day');
        const pillTotal = pillTotalInput ? parseFloat(pillTotalInput.value) : NaN;
        const dosePerDay = dosePerDayInput ? parseFloat(dosePerDayInput.value) : NaN;
        if (pillTotal > 0 && dosePerDay > 0) {
            schedule.pillTotal = pillTotal;
            schedule.dosePerDay = dosePerDay;
            schedule.refillStart = new Date().toISOString();
        }
    }

    if (typeof addCareSchedule === 'function') {
        addCareSchedule(schedule);
        closeCareScheduleModal();
    }
}
