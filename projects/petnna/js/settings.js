let selectedDonationAmount = 20000;
let selectedDonationMethod = 'kakao';

let inquiries = JSON.parse(localStorage.getItem('petna_inquiries')) || [
    {
        id: 1,
        category: "사주 성향",
        title: "저희 댕댕이 사주가 너무 잘 맞아서 깜짝 놀랐어요!",
        content: "원래 사주나 신점 같은 걸 잘 안 믿는 편인데, 성격 분석 부분이 완전 소름 돋게 똑같네요. 유용한 서비스 감사합니다!",
        date: "2026-05-15",
        status: "답변 완료",
        replies: ["안녕하세요 집사님! 펫과나 안심 동행 서비스팀입니다. 소중한 반려견과의 신비로운 묘연에 저희 사주 성향 시스템이 긍정적인 가이드가 되어 드린 것 같아 큰 보람을 느낍니다. 십이운성과 상생 조율 분석을 기초로 하여 성격 분석에 세심함을 기울였습니다. 앞으로도 영구 소장 앨범 꾸미기와 스마트 산책로 등 다양한 펫 라이프 여정에서 늘 행복하시기를 응원하겠습니다. 감사합니다!"]
    }
];

function renderSettings() {
    const nicknameInput = document.getElementById('settings-user-nickname');
    const emailInput = document.getElementById('settings-user-email');
    const avatarDisp = document.getElementById('settings-avatar-disp');

    if (nicknameInput) nicknameInput.value = settings_nickname;
    if (emailInput) emailInput.value = settings_email;
    if (avatarDisp) avatarDisp.innerText = settings_avatar;

    const connectedEmailEl = document.getElementById('settings-connected-email');
    if (connectedEmailEl) connectedEmailEl.innerText = settings_email;

    if (typeof updateSocialLinkButtons === 'function') {
        updateSocialLinkButtons();
    }

    const themes = ['light', 'sepia', 'dark'];
    themes.forEach(t => {
        const btn = document.getElementById(`btn-theme-${t}`);
        if (btn) {
            if (settings_theme === t) {
                btn.className = "py-2.5 rounded-xl border-2 border-brand-500 bg-brand-50 text-brand-700 font-bold text-center";
            } else {
                btn.className = "py-2.5 rounded-xl border border-gray-200 bg-white hover:bg-gray-50 text-gray-700 font-bold text-center";
            }
        }
    });

    const units = ['metric', 'imperial'];
    units.forEach(u => {
        const btn = document.getElementById(`btn-unit-${u}`);
        if (btn) {
            if (settings_unit === u) {
                btn.className = "py-2.5 rounded-xl border-2 border-brand-500 bg-brand-50 text-brand-700 font-bold text-center";
            } else {
                btn.className = "py-2.5 rounded-xl border border-gray-200 bg-white hover:bg-gray-50 text-gray-700 font-bold text-center";
            }
        }
    });

    renderInquiries();

    // 위치 권한 상태 감지 및 UI 업데이트
    checkLocationPermissionStatus();

    // 알림 권한 상태 감지 및 UI 업데이트
    checkNotificationPermissionStatus();
}

// 📍 위치 권한 현재 상태 확인 및 버튼 UI 업데이트
function checkLocationPermissionStatus() {
    const statusText = document.getElementById('location-perm-status-text');
    const btn = document.getElementById('location-perm-btn');
    const icon = document.getElementById('location-perm-icon');
    if (!statusText || !btn) return;

    if (!navigator.geolocation) {
        statusText.textContent = '이 브라우저는 GPS를 지원하지 않습니다';
        btn.textContent = '미지원';
        btn.className = 'px-4 py-2 rounded-xl text-[11px] font-black bg-gray-200 text-gray-400 cursor-not-allowed';
        btn.disabled = true;
        return;
    }

    if (!navigator.permissions) {
        // permissions API 미지원 브라우저 (Safari 구형 등)
        statusText.textContent = '권한 요청을 눌러 GPS를 활성화하세요';
        btn.textContent = '위치 허용하기';
        btn.className = 'px-4 py-2 rounded-xl text-[11px] font-black transition-all bg-blue-500 hover:bg-blue-600 text-white shadow-sm';
        btn.disabled = false;
        return;
    }

    navigator.permissions.query({ name: 'geolocation' }).then(function (result) {
        if (result.state === 'granted') {
            statusText.textContent = '✅ 위치 권한 허용됨 — GPS 산책 지도 사용 가능';
            statusText.classList.replace('text-gray-400', 'text-emerald-600');
            btn.textContent = '허용됨 ✓';
            btn.className = 'px-4 py-2 rounded-xl text-[11px] font-black bg-emerald-100 text-emerald-700 cursor-default';
            if (icon) { icon.className = 'w-9 h-9 rounded-xl bg-emerald-100 flex items-center justify-center text-lg'; icon.textContent = '✅'; }
        } else if (result.state === 'denied') {
            statusText.textContent = '❌ 위치 권한 차단됨 — 브라우저 설정에서 직접 허용 필요';
            statusText.classList.replace('text-gray-400', 'text-red-500');
            btn.textContent = '설정에서 허용하기';
            btn.className = 'px-4 py-2 rounded-xl text-[11px] font-black bg-red-100 text-red-600 hover:bg-red-200 transition-all';
            if (icon) { icon.className = 'w-9 h-9 rounded-xl bg-red-100 flex items-center justify-center text-lg'; icon.textContent = '🚫'; }
        } else {
            // prompt 상태 - 아직 묻지 않음
            statusText.textContent = '⚠️ 위치 권한 미설정 — 아래 버튼을 눌러 허용해 주세요';
            btn.textContent = '위치 허용하기';
            btn.className = 'px-4 py-2 rounded-xl text-[11px] font-black bg-blue-500 hover:bg-blue-600 text-white shadow-sm transition-all';
            if (icon) { icon.className = 'w-9 h-9 rounded-xl bg-blue-100 flex items-center justify-center text-lg'; icon.textContent = '📍'; }
        }

        // 권한 상태 변경 감지 (허용 → 차단 등 실시간 반영)
        result.onchange = () => checkLocationPermissionStatus();
    });
}

// 📍 위치 권한 요청 버튼 핸들러
function handleLocationPermission() {
    if (!navigator.geolocation) {
        showToast('이 브라우저는 위치 권한을 지원하지 않습니다.');
        return;
    }

    // 차단 상태면 브라우저 설정 안내
    if (navigator.permissions) {
        navigator.permissions.query({ name: 'geolocation' }).then(function (result) {
            if (result.state === 'denied') {
                showToast('위치가 차단되어 있습니다. 주소창 왼쪽 🔒 아이콘 → 사이트 설정 → 위치 → 허용으로 변경해 주세요.');
                return;
            }
            // prompt 또는 granted 상태면 실제 위치 요청
            requestLocationNow();
        });
    } else {
        requestLocationNow();
    }
}

function requestLocationNow() {
    showToast('📍 위치 권한을 요청하고 있습니다...');
    navigator.geolocation.getCurrentPosition(
        function () {
            showToast('✅ 위치 권한이 허용되었습니다! 산책 탭에서 현재 위치 지도를 사용할 수 있습니다.');
            checkLocationPermissionStatus();
        },
        function (err) {
            if (err.code === 1) {
                showToast('❌ 위치 권한이 거부되었습니다. 브라우저 설정에서 직접 허용해 주세요.');
            } else {
                showToast('위치를 불러오지 못했습니다. 다시 시도해 주세요.');
            }
            checkLocationPermissionStatus();
        },
        { enableHighAccuracy: true, timeout: 8000 }
    );
}

// 🔔 알림 권한 현재 상태 확인 및 버튼 UI 업데이트
function checkNotificationPermissionStatus() {
    const statusText = document.getElementById('notification-perm-status-text');
    const btn = document.getElementById('notification-perm-btn');
    const toggle = document.getElementById('notification-toggle');
    const icon = document.getElementById('notification-perm-icon');

    if (!statusText || !btn || !toggle || !icon) return;

    // 브라우저 알림 미지원
    if (!("Notification" in window)) {
        statusText.textContent = '이 브라우저는 알림을 지원하지 않습니다';
        btn.textContent = '미지원';
        btn.className = 'px-4 py-2 rounded-xl text-[11px] font-black bg-gray-200 text-gray-400 cursor-not-allowed';
        btn.disabled = true;
        toggle.disabled = true;
        toggle.checked = false;
        icon.className = 'w-9 h-9 rounded-xl bg-gray-100 flex items-center justify-center text-lg'; icon.textContent = '🚫';
        return;
    }

    // 알림 권한 상태에 따른 UI 업데이트
    if (Notification.permission === 'granted') {
        settings_notification_permission_granted = true;
        statusText.textContent = '✅ 알림 허용됨 — 스트릭 리마인더(오후 8시) · AI 분석 알림(오후 3시) 활성화';
        statusText.classList.replace('text-gray-400', 'text-emerald-600');
        btn.textContent = '허용됨 ✓';
        btn.className = 'px-4 py-2 rounded-xl text-[11px] font-black bg-emerald-100 text-emerald-700 cursor-default';
        btn.disabled = true;
        toggle.disabled = false;
        toggle.checked = settings_notifications_enabled;
        icon.className = 'w-9 h-9 rounded-xl bg-emerald-100 flex items-center justify-center text-lg'; icon.textContent = '🔔';
    } else if (Notification.permission === 'denied') {
        settings_notification_permission_granted = false;
        statusText.textContent = '❌ 알림 권한 차단됨 — 브라우저 설정에서 직접 허용 필요';
        statusText.classList.replace('text-gray-400', 'text-red-500');
        btn.textContent = '설정에서 허용하기';
        btn.className = 'px-4 py-2 rounded-xl text-[11px] font-black bg-red-100 text-red-600 hover:bg-red-200 transition-all';
        btn.disabled = false;
        toggle.disabled = true;
        toggle.checked = false;
        icon.className = 'w-9 h-9 rounded-xl bg-red-100 flex items-center justify-center text-lg'; icon.textContent = '🔕';
    } else { // default 또는 prompt 상태
        settings_notification_permission_granted = false;
        statusText.textContent = '⚠️ 알림 권한 미설정 — 아래 버튼을 눌러 허용해 주세요';
        btn.textContent = '알림 허용하기';
        btn.className = 'px-4 py-2 rounded-xl text-[11px] font-black bg-blue-500 hover:bg-blue-600 text-white shadow-sm transition-all';
        btn.disabled = false;
        toggle.disabled = true;
        toggle.checked = false;
        icon.className = 'w-9 h-9 rounded-xl bg-blue-100 flex items-center justify-center text-lg'; icon.textContent = '🔕';
    }
    saveState(); // 권한 상태 변경 저장
}

// 🔔 알림 권한 요청 핸들러
function requestNotificationPermission() {
    if (!("Notification" in window)) {
        showToast("이 브라우저는 알림을 지원하지 않습니다.");
        return;
    }
    Notification.requestPermission().then(function (permission) {
        checkNotificationPermissionStatus(); // 권한 상태에 따라 UI 업데이트
        if (permission === "granted") showToast("✅ 알림 권한이 허용되었습니다!");
        else showToast("❌ 알림 권한이 거부되었습니다.");
    });
}

// 🔔 알림 활성화/비활성화 토글 핸들러
function toggleNotificationsEnabled() {
    settings_notifications_enabled = !settings_notifications_enabled;
    saveState();
    checkNotificationPermissionStatus(); // UI 업데이트
    
    if (typeof updateProfileInSupabase === 'function') {
        updateProfileInSupabase({ notifications_enabled: settings_notifications_enabled });
    }
    
    if (settings_notifications_enabled) {
        showToast("✅ 산책 완료 알림이 활성화되었습니다.");
    } else {
        showToast("❌ 산책 완료 알림이 비활성화되었습니다.");
    }
}

function changeUserAvatar(emoji) {
    settings_avatar = emoji;
    settings_photo_url = "";
    localStorage.removeItem('petna_user_photo_url_' + settings_email);
    
    if (typeof syncButlerAvatarDisplay === 'function') {
        syncButlerAvatarDisplay();
    }
    
    if (typeof updateProfileInSupabase === 'function') {
        updateProfileInSupabase({ avatar: emoji, photo_url: "" });
    }
    
    showToast(`대표 프로필 이모지가 '${emoji}'로 선택되었습니다.`);
}

function saveUserProfile() {
    const nickInput = document.getElementById('settings-user-nickname');
    const mailInput = document.getElementById('settings-user-email');

    if (!nickInput || !nickInput.value.trim()) {
        showToast("집사 닉네임은 필수로 채워주셔야 합니다.");
        return;
    }

    settings_nickname = nickInput.value.trim();
    settings_email = mailInput ? mailInput.value.trim() : "";

    localStorage.setItem('petna_user_email', settings_email);
    localStorage.setItem('petna_user_nickname_' + settings_email, settings_nickname);
    localStorage.setItem('petna_user_avatar_' + settings_email, settings_avatar);

    if (typeof renderMyPets === 'function') renderMyPets();
    
    if (typeof updateProfileInSupabase === 'function') {
        updateProfileInSupabase({ nickname: settings_nickname, email: settings_email, avatar: settings_avatar });
    }

    showToast("집사 프로필 정보가 영구 저장되었습니다! 👤");
}

function setAppTheme(theme) {
    settings_theme = theme;
    applyThemeStyles(theme);
    renderSettings();
    
    if (typeof updateProfileInSupabase === 'function') {
        updateProfileInSupabase({ theme: theme });
    }
    
    showToast(`테마가 '${theme}' 스타일로 전환되었습니다.`);
}

function applyThemeStyles(theme) {
    const body = document.body;
    const cards = document.querySelectorAll('.bg-white');

    body.classList.remove('theme-light', 'theme-sepia', 'theme-dark');
    body.classList.add(`theme-${theme}`);

    if (theme === 'dark') {
        body.style.backgroundColor = '#15151e';
        body.style.color = '#e2e2e9';
        cards.forEach(c => {
            c.style.backgroundColor = '#1f1f2e';
            c.style.color = '#e2e2e9';
            c.style.borderColor = '#2d2d3f';
        });
    } else if (theme === 'sepia') {
        body.style.backgroundColor = '#f4ecd8';
        body.style.color = '#4a2f13';
        cards.forEach(c => {
            c.style.backgroundColor = '#faf5e8';
            c.style.color = '#4a2f13';
            c.style.borderColor = '#e4d2a3';
        });
    } else {
        body.style.backgroundColor = '#fbf8f5';
        body.style.color = '#1f2937';
        cards.forEach(c => {
            c.style.backgroundColor = '#ffffff';
            c.style.color = '#1f2937';
            c.style.borderColor = '#fef3c7';
        });
    }
}

function setAppUnits(unit) {
    settings_unit = unit;
    renderSettings();
    
    if (typeof updateProfileInSupabase === 'function') {
        updateProfileInSupabase({ unit: unit });
    }
    
    showToast(`측정 표기법이 '${unit === 'metric' ? 'km/kg' : 'mile/lbs'}' 단위로 동기화되었습니다.`);
}

function saveAppSettings() {
    localStorage.setItem('petna_app_theme', settings_theme);
    localStorage.setItem('petna_app_unit', settings_unit);

    showToast("앱 환경설정이 로컬에 완벽히 적용되었습니다! ⚙️");
    setTimeout(() => {
        window.location.reload();
    }, 500);
}

function selectDonationPreset(amount, btn) {
    selectedDonationAmount = amount;
    document.getElementById('donation-custom-amount').value = '';
    document.querySelectorAll('.donation-preset-btn').forEach(el => {
        el.className = "donation-preset-btn py-2.5 rounded-xl border border-amber-200/60 bg-white hover:bg-amber-50/60 text-gray-700 font-bold text-center text-xs transition-all";
    });
    if (btn) {
        btn.className = "donation-preset-btn py-2.5 rounded-xl border-2 border-brand-500 bg-brand-50 text-brand-700 font-black text-center text-xs transition-all shadow-sm";
    }
}

function selectDonationMethod(method, btn) {
    selectedDonationMethod = method;
    document.querySelectorAll('.donation-method-btn').forEach(el => {
        el.className = "donation-method-btn flex items-center justify-center gap-1.5 py-3 rounded-xl border border-amber-100 bg-white text-gray-700 font-bold hover:bg-amber-50/30 transition-all outline-none";
    });
    if (btn) {
        btn.className = "donation-method-btn flex items-center justify-center gap-1.5 py-3 rounded-xl border-2 border-brand-500 bg-brand-50 text-brand-700 font-black hover:bg-brand-50 transition-all outline-none shadow-sm";
    }
}

function triggerDonation() {
    const customVal = document.getElementById('donation-custom-amount').value.trim();
    if (customVal !== '') {
        const amount = parseInt(customVal);
        if (isNaN(amount) || amount <= 0) {
            showToast("올바른 후원 금액을 입력해주세요.");
            return;
        }
        selectedDonationAmount = amount;
    }

    document.getElementById('donation-modal-amount').innerText = selectedDonationAmount.toLocaleString() + '원';
    const modal = document.getElementById('donation-payment-modal');
    modal.classList.remove('hidden');
    modal.classList.add('flex');
}

function closeDonationModal() {
    const modal = document.getElementById('donation-payment-modal');
    modal.classList.add('hidden');
    modal.classList.remove('flex');
}

function confirmDonation() {
    closeDonationModal();
    triggerConfetti();
    showCustomDialog({
        title: "후원 대성공! 💖",
        message: `집사님께서 따뜻한 사랑으로 보내주신 ${selectedDonationAmount.toLocaleString()}원이 펫과나 동행단에 무사히 전달되었습니다. 소중한 정성은 정밀 알고리즘 고도화에 감사히 사용하겠습니다!`,
        icon: "💝",
        type: "alert"
    });
}

function triggerConfetti() {
    const colors = ['#f59e0b', '#ec4899', '#3b82f6', '#10b981', '#8b5cf6'];
    const container = document.createElement('div');
    container.className = "fixed inset-0 pointer-events-none z-[200] overflow-hidden";
    document.body.appendChild(container);

    for (let i = 0; i < 60; i++) {
        const p = document.createElement('div');
        const color = colors[Math.floor(Math.random() * colors.length)];
        p.style.position = 'absolute';
        p.style.width = Math.random() * 8 + 6 + 'px';
        p.style.height = Math.random() * 12 + 6 + 'px';
        p.style.backgroundColor = color;
        p.style.left = Math.random() * 100 + 'vw';
        p.style.top = '-20px';
        p.style.borderRadius = '2px';
        p.style.opacity = Math.random() * 0.5 + 0.5;
        p.style.transform = `rotate(${Math.random() * 360}deg)`;
        
        container.appendChild(p);

        const duration = Math.random() * 2 + 1.5;
        const drift = Math.random() * 120 - 60;
        p.animate([
            { top: '-20px', left: p.style.left, transform: p.style.transform },
            { top: '105vh', left: `calc(${p.style.left} + ${drift}px)`, transform: `rotate(${Math.random() * 1000 + 360}deg)` }
        ], {
            duration: duration * 1000,
            easing: 'cubic-bezier(0.1, 0.8, 0.3, 1)',
            fill: 'forwards'
        });
    }

    setTimeout(() => {
        container.remove();
    }, 3500);
}

function toggleFAQ(id) {
    for (let i = 1; i <= 4; i++) {
        const content = document.getElementById(`faq-content-${i}`);
        const icon = document.getElementById(`faq-icon-${i}`);
        if (!content || !icon) continue;
        if (i === id) {
            if (content.classList.contains('hidden')) {
                content.classList.remove('hidden');
                icon.classList.add('rotate-180');
            } else {
                content.classList.add('hidden');
                icon.classList.remove('rotate-180');
            }
        } else {
            content.classList.add('hidden');
            icon.classList.remove('rotate-180');
        }
    }
}

function renderInquiries() {
    const listBody = document.getElementById('inquiry-list-body');
    if (!listBody) return;
    listBody.innerHTML = '';

    if (inquiries.length === 0) {
        listBody.innerHTML = `
            <tr>
                <td colspan="4" class="p-8 text-center text-gray-400">접수된 문의 내역이 없습니다.</td>
            </tr>
        `;
        return;
    }

    inquiries.forEach(inq => {
        const row = document.createElement('tr');
        row.className = "border-b border-gray-50 hover:bg-gray-50/50 transition-all cursor-pointer";
        row.onclick = () => openInquiryDetail(inq.id);
        
        const statusClass = inq.status === '답변 완료' 
            ? 'bg-emerald-50 text-emerald-600 border-emerald-100' 
            : 'bg-amber-50 text-amber-600 border-amber-100';

        row.innerHTML = `
            <td class="p-3 text-gray-400 font-mono text-[10px]">${inq.date}</td>
            <td class="p-3 font-bold text-gray-600 text-[10px]">${inq.category}</td>
            <td class="p-3 text-gray-700 truncate font-semibold max-w-[140px]">${inq.title}</td>
            <td class="p-3 text-center">
                <span class="inline-block px-2 py-0.5 rounded-full border text-[9px] font-bold ${statusClass}">${inq.status}</span>
            </td>
        `;
        listBody.appendChild(row);
    });

    // Initialize default active selections on load/render settings
    const defaultPresetBtn = document.getElementById('donation-preset-20000');
    if (defaultPresetBtn && !defaultPresetBtn.classList.contains('border-2')) {
        selectDonationPreset(20000, defaultPresetBtn);
    }
    const defaultMethodBtn = document.getElementById('donation-method-kakao');
    if (defaultMethodBtn && !defaultMethodBtn.classList.contains('border-2')) {
        selectDonationMethod('kakao', defaultMethodBtn);
    }
}

function openInquiryWriteModal() {
    const modal = document.getElementById('inquiry-write-modal');
    if (modal) {
        modal.classList.remove('hidden');
        modal.classList.add('flex');
    }
}

function closeInquiryWriteModal() {
    const modal = document.getElementById('inquiry-write-modal');
    if (modal) {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }
}

function submitInquiry() {
    const category = document.getElementById('inquiry-write-category').value;
    const titleInput = document.getElementById('inquiry-write-title');
    const contentInput = document.getElementById('inquiry-write-content');

    if (!titleInput || !titleInput.value.trim() || !contentInput || !contentInput.value.trim()) {
        showToast("제목과 내용을 성실하게 작성해 주셔야 접수됩니다.");
        return;
    }

    const newInq = {
        id: Date.now(),
        category: category,
        title: titleInput.value.trim(),
        content: contentInput.value.trim(),
        date: new Date().toISOString().split('T')[0],
        status: "답변 대기",
        replies: []
    };

    inquiries.unshift(newInq);
    localStorage.setItem('petna_inquiries', JSON.stringify(inquiries));
    
    // 폼 리셋
    titleInput.value = '';
    contentInput.value = '';
    
    closeInquiryWriteModal();
    renderInquiries();
    showToast("💌 1:1 신문고 문의가 접수되었습니다. 약 3초 뒤에 AI 케어팀의 전문 진단 답변이 도착합니다!");

    // 3초 뒤 답변 자동 도착 시뮬레이터 실행
    setTimeout(() => {
        simulateAIReply(newInq.id);
    }, 3000);
}

async function fetchAIResponse(promptData) {
    const endpoints = [
        "http://localhost:1234/v1/chat/completions",
        "http://127.0.0.1:1234/v1/chat/completions"
    ];
    if (/android/i.test(navigator.userAgent)) {
        endpoints.unshift("http://10.0.2.2:1234/v1/chat/completions");
    }
    
    for (const url of endpoints) {
        try {
            const controller = new AbortController();
            const timeoutId = setTimeout(() => controller.abort(), 7000); // 7 seconds timeout
            
            const res = await fetch(url, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify({
                    model: "google/gemma-4-e2b",
                    messages: [
                        {
                            role: "system",
                            content: "당신은 반려동물 동행 플랫폼 '펫과나'의 'AI 동행 케어팀'의 전문 수의사이자 행동 분석가입니다. 사용자의 문의 카테고리, 제목, 내용을 분석하여 매우 구체적이고 따뜻하며 신뢰감 있는 조언을 한국어로 답변해 주세요."
                        },
                        {
                            role: "user",
                            content: `문의 분야: ${promptData.category}\n제목: ${promptData.title}\n내용: ${promptData.content}\n\n위 문의에 대해 전문적이고 친근한 조언을 3~5문장 내외로 작성해 주세요.`
                        }
                    ],
                    temperature: 0.7,
                    max_tokens: 500
                }),
                signal: controller.signal
            });
            clearTimeout(timeoutId);
            if (res.ok) {
                const json = await res.json();
                if (json.choices && json.choices[0] && json.choices[0].message) {
                    return json.choices[0].message.content.trim();
                }
            }
        } catch (e) {
            console.warn(`Failed to connect to LM Studio at ${url}:`, e);
        }
    }
    throw new Error("No LM Studio server available");
}

async function simulateAIReply(inquiryId) {
    const inq = inquiries.find(x => x.id === inquiryId);
    if (!inq) return;

    let replyText = "";
    try {
        // Try getting response from local LM Studio server
        replyText = await fetchAIResponse(inq);
        showToast("🤖 로컬 LM Studio(Gemma)로부터 실시간 전문 진단을 받았습니다!");
    } catch (err) {
        console.log("LM Studio 연결 실패 또는 오류, 시뮬레이션 답변으로 대체합니다.", err);
        switch (inq.category) {
            case "사주 성향":
                replyText = `안녕하세요, ${settings_nickname || '집사'}님! 펫과나 안심 동양 명리 자문단입니다.

의뢰해주신 동행 성향 진단 결과를 면밀히 파악하고 주신 문의 사항에 대해 검토 완료하였습니다. 
반려동물과 집사님의 태어난 생년월일시 간의 삼합(三合)과 오행 분배 상태를 분석해본 결과, 성격 분석 및 생활 팁 연동이 집사님의 일상 교감 패턴과 정확히 부합하는 흐름으로 해석됩니다. 
향후 펫 사주 성향의 디테일한 길흉화복 시즌 리포트도 추가로 기획 중이오니 지속적인 관심 부탁드립니다.`;
                break;
            case "지능 테스트":
                replyText = `안녕하세요, ${settings_nickname || '집사'}님! 펫과나 동물 인지 행동 진단팀입니다.

소중한 반려동물의 지능 진단 홈 테스트(Cognitive Home Practice) 관련 문의에 답변 드립니다. 
본 진단지가 반려견/반려묘의 공간 인지 및 단기 메모리 반응 속도를 가볍게 집에서 평가해보실 수 있도록 임상 가이드를 토대로 구성되었습니다. 
진단 결과는 마이펫 프로필에 클래스 배지로 안전하게 자동 저장 및 갱신되며, 언제든 '자랑 피드'에 발행하여 이웃 집사들과 나눌 수 있습니다.`;
                break;
            case "산책 루트":
                replyText = `안녕하세요, ${settings_nickname || '집사'}님! 펫과나 스마트 안심 산책 지원단입니다.

작성하신 스마트 산책로 지도의 내비 마킹 정보 및 마이 맵 장착 관련 문의 사항을 확인하였습니다.
산책 중 실시간 마킹(배변, 마킹, 냄새맡기) 흔적 위치는 집사님의 폰 GPS 정보와 완전 무상 연동되며, 저장된 루트는 100% 암호화되어 로컬에만 영구 소장됩니다. '루트 정보 공유'를 누르면 언제든지 대화방의 집사들과 공유가 가능하니 적극 활용해 보세요!`;
                break;
            case "데코 앨범":
                replyText = `안녕하세요, ${settings_nickname || '집사'}님! 펫과나 크리에이티브 데코 엔진 기술지원팀입니다.

액티브 데코룸의 미디어 직접 파일 올리기 기능(이미지 및 영상) 및 실시간 스티커 HUD 트랜스폼 조작과 관련된 문의에 대해 답변 드립니다.
데코룸은 WebGL 가속 및 CSS Transform Matrix를 활용하여 모바일와 PC 환경 구분 없이 픽셀 단위 조작이 원활하게 지원됩니다. 특히 말풍선 색상 테마 및 Z-Index 조절을 활용해 꾸민 마스터피스는 갤러리 백업 시 오버레이 메타데이터로 고스란히 영구 압축 저장됩니다.`;
                break;
            default:
                replyText = `안녕하세요, 소중한 ${settings_nickname || '집사'}님! 펫과나 고객 안심 동행 서비스팀입니다.

보내주신 따뜻한 건의 사항과 문의를 신속하게 읽고 검토를 마쳤습니다. 
펫과나는 집사님들과 반려동물 간의 완벽한 상생 여정을 위해 최고의 기술과 정성으로 플랫폼을 다듬어가고 있습니다. 
앞으로도 문의사항이나 개선 사항이 있으시면 언제든지 1:1 안심 신문고를 두드려 주세요. 동행단이 24시간 언제든 집사님의 목소리에 성실히 귀기울이겠습니다. 감사합니다!`;
        }
    }

    inq.status = "답변 완료";
    inq.replies = [replyText];
    localStorage.setItem('petna_inquiries', JSON.stringify(inquiries));
    
    renderInquiries();

    // 만약 현재 상세 보기 모달이 이 문의에 대해 열려있다면 즉각 답변 뷰 갱신!
    const detailModal = document.getElementById('inquiry-detail-modal');
    const currentDetailId = detailModal ? detailModal.dataset.inquiryId : null;
    if (detailModal && !detailModal.classList.contains('hidden') && Number(currentDetailId) === inquiryId) {
        renderInquiryDetailReply(inq);
        showToast("✨ AI 케어팀의 실시간 전문 답변이 상세 뷰어에 즉시 주입되었습니다!");
    }
}

function openInquiryDetail(inquiryId) {
    const inq = inquiries.find(x => x.id === inquiryId);
    if (!inq) return;

    const modal = document.getElementById('inquiry-detail-modal');
    if (!modal) return;

    modal.dataset.inquiryId = inquiryId;
    document.getElementById('inquiry-detail-category').innerText = inq.category;
    document.getElementById('inquiry-detail-date').innerText = inq.date;
    document.getElementById('inquiry-detail-title').innerText = inq.title;
    document.getElementById('inquiry-detail-content').innerText = inq.content;

    renderInquiryDetailReply(inq);

    modal.classList.remove('hidden');
    modal.classList.add('flex');
}

function renderInquiryDetailReply(inq) {
    const replyBox = document.getElementById('inquiry-detail-reply-box');
    if (!replyBox) return;

    if (inq.replies.length === 0) {
        // 답변 대기 중 로딩바 애니메이션 노출
        replyBox.innerHTML = `
            <div class="flex flex-col items-center justify-center py-4 space-y-2 text-gray-400">
                <div class="flex items-center space-x-1.5">
                    <div class="w-2 h-2 bg-indigo-500 rounded-full animate-bounce" style="animation-delay: 0.1s"></div>
                    <div class="w-2 h-2 bg-indigo-500 rounded-full animate-bounce" style="animation-delay: 0.2s"></div>
                    <div class="w-2 h-2 bg-indigo-500 rounded-full animate-bounce" style="animation-delay: 0.3s"></div>
                </div>
                <span class="text-[10px] font-bold animate-pulse">펫과나 AI 동행 케어팀이 전문 답변을 작성하고 있습니다... ✍️</span>
            </div>
        `;
    } else {
        // 작성 완료된 답변 노출
        replyBox.innerHTML = `
            <div class="space-y-2 text-gray-700 leading-relaxed text-[11px] whitespace-pre-line animate-fade-in">
                ${inq.replies[0]}
                <div class="mt-4 pt-3 border-t border-indigo-100/40 text-[9px] text-indigo-400 font-bold flex items-center justify-between">
                    <span>담당부서: 펫과나 안심 동행 고객지원부</span>
                    <span>처리 완료 🟢</span>
                </div>
            </div>
        `;
    }
}

function closeInquiryDetailModal() {
    const modal = document.getElementById('inquiry-detail-modal');
    if (modal) {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }
}

function resetToRichDemoData() {
    showCustomDialog({
        title: "데모 데이터 셋업 확인 ⚡",
        message: "정말 풍요로운 테스트용 일주일치 펫생활 데모 데이터로 덮어쓰시겠습니까? 기존 기록은 소실됩니다.",
        icon: "🪄",
        type: "confirm",
        onConfirm: () => {
            const sampleWalks = [
                { id: 401, date: "오늘 방금", duration: "25:40", distance: "2.10", calories: "158", poop: 1, pee: 2, sniff: 8, coords: [[37.3912, 126.6392], [37.3920, 126.6370], [37.3932, 126.6360], [37.3948, 126.6368]], marks: [{ lat: 37.3920, lng: 126.6370, type: "poop" }, { lat: 37.3932, lng: 126.6360, type: "pee" }] },
                { id: 402, date: "어제", duration: "18:15", distance: "1.25", calories: "94", poop: 0, pee: 1, sniff: 5, coords: [[37.3912, 126.6392], [37.3918, 126.6412], [37.3932, 126.6428]], marks: [{ lat: 37.3918, lng: 126.6412, type: "sniff" }] },
                { id: 403, date: "3일 전", duration: "32:04", distance: "2.80", calories: "210", poop: 2, pee: 3, sniff: 12, coords: [[37.3912, 126.6392], [37.3949, 126.6415]], marks: [] }
            ];

            const sampleMeals = [
                { id: Date.now() - 3600000, type: "간식", time: "14:15", notes: "바스락 황태 슬라이스 15g 간식 🍖" },
                { id: Date.now() - 28800000, type: "아침", time: "08:10", notes: "프리미엄 홀리스틱 사료 80g 배식 완료 🌅" },
                { id: Date.now() - 86400000, type: "저녁", time: "19:20", notes: "칠면조 습식 캔 85g 섭취 완수 🌙" }
            ];

            localStorage.setItem('petna_pets', JSON.stringify(INITIAL_PETS));
            localStorage.setItem('petna_posts', JSON.stringify(INITIAL_POSTS));
            localStorage.setItem('petna_schedules', JSON.stringify(INITIAL_SCHEDULES));
            localStorage.setItem('petna_albums', JSON.stringify(INITIAL_ALBUM));
            localStorage.setItem('petna_walks', JSON.stringify(sampleWalks));
            localStorage.setItem('petna_meals', JSON.stringify(sampleMeals));
            localStorage.setItem('petna_cart', JSON.stringify([]));

            showToast("데모 데이터 주입이 끝났습니다. 새로고침 중...");
            setTimeout(() => {
                window.location.reload();
            }, 1000);
        }
    });
}

function exportAllDataAsJSON() {
    const backupObj = {
        pets: JSON.parse(localStorage.getItem('petna_pets')) || INITIAL_PETS,
        posts: JSON.parse(localStorage.getItem('petna_posts')) || INITIAL_POSTS,
        schedules: JSON.parse(localStorage.getItem('petna_schedules')) || INITIAL_SCHEDULES,
        walks: JSON.parse(localStorage.getItem('petna_walks')) || INITIAL_WALKS,
        albums: JSON.parse(localStorage.getItem('petna_albums')) || INITIAL_ALBUM,
        meals: JSON.parse(localStorage.getItem('petna_meals')) || [],
        places: JSON.parse(localStorage.getItem('petna_places')) || INITIAL_PLACES,
        user: {
            nickname: settings_nickname,
            email: settings_email,
            avatar: settings_avatar,
            theme: settings_theme,
            unit: settings_unit
        }
    };

    try {
        const dataStr = "data:text/json;charset=utf-8," + encodeURIComponent(JSON.stringify(backupObj, null, 2));
        const downloadAnchor = document.createElement('a');
        downloadAnchor.setAttribute("href", dataStr);
        downloadAnchor.setAttribute("download", `petna_backup_${new Date().toISOString().slice(0, 10)}.json`);
        document.body.appendChild(downloadAnchor);
        downloadAnchor.click();
        document.body.removeChild(downloadAnchor);
        showToast("통합 백업 파일이 기기에 다운로드 되었습니다! 💾");
    } catch (err) {
        showToast("백업 파일을 변환하는 과정에서 에러가 터졌습니다.");
    }
}

function importDataFromJSON(event) {
    const file = event.target.files[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = function (e) {
        try {
            const parsed = JSON.parse(e.target.result);
            if (parsed.pets) localStorage.setItem('petna_pets', JSON.stringify(parsed.pets));
            if (parsed.posts) localStorage.setItem('petna_posts', JSON.stringify(parsed.posts));
            if (parsed.schedules) localStorage.setItem('petna_schedules', JSON.stringify(parsed.schedules));
            if (parsed.walks) localStorage.setItem('petna_walks', JSON.stringify(parsed.walks));
            if (parsed.albums) localStorage.setItem('petna_albums', JSON.stringify(parsed.albums));
            if (parsed.meals) localStorage.setItem('petna_meals', JSON.stringify(parsed.meals));
            if (parsed.places) localStorage.setItem('petna_places', JSON.stringify(parsed.places));

            if (parsed.user) {
                if (parsed.user.nickname) localStorage.setItem('petna_user_nickname', parsed.user.nickname);
                if (parsed.user.email) localStorage.setItem('petna_user_email', parsed.user.email);
                if (parsed.user.avatar) localStorage.setItem('petna_user_avatar', parsed.user.avatar);
                if (parsed.user.theme) localStorage.setItem('petna_app_theme', parsed.user.theme);
                if (parsed.user.unit) localStorage.setItem('petna_app_unit', parsed.user.unit);
            }

            showCustomDialog({
                title: "복구 복원 완수 🎉",
                message: "모든 반려 생활 데이터가 원상 복구되었습니다. 화면을 새로고침합니다.",
                icon: "✅",
                type: "alert"
            });
            setTimeout(() => {
                window.location.reload();
            }, 2000);
        } catch (err) {
            showCustomDialog({
                title: "가져오기 오류 ❌",
                message: "업로드한 파일의 구조가 올바르지 않습니다.",
                icon: "⚠️",
                type: "alert"
            });
        }
    };
    reader.readAsText(file);
}

function wipeAllAppData() {
    showCustomDialog({
        title: "보관소 파괴 확인 ⚠️",
        message: "정말 기기 보관소의 데이터 전체를 제거할까요? 복구할 수 없습니다.",
        icon: "🗑️",
        type: "confirm",
        onConfirm: () => {
            // 세션 및 회원정보 백업
            const currentEmail = settings_email;
            const currentNickname = settings_nickname;
            const currentAvatar = settings_avatar;
            const currentPhotoUrl = settings_photo_url;
            const currentTheme = settings_theme;
            const currentUnit = settings_unit;
            const registeredUsers = localStorage.getItem('petna_registered_users');

            localStorage.clear();

            // 세션 및 유저 정보 복원
            localStorage.setItem('petna_is_logged_in', 'true');
            localStorage.setItem('petna_user_email', currentEmail);
            localStorage.setItem('petna_user_nickname', currentNickname);
            if (currentNickname) localStorage.setItem('petna_user_nickname_' + currentEmail, currentNickname);
            if (currentAvatar) localStorage.setItem('petna_user_avatar_' + currentEmail, currentAvatar);
            if (currentPhotoUrl) localStorage.setItem('petna_user_photo_url_' + currentEmail, currentPhotoUrl);
            if (currentTheme) localStorage.setItem('petna_app_theme', currentTheme);
            if (currentUnit) localStorage.setItem('petna_app_unit', currentUnit);
            if (registeredUsers) localStorage.setItem('petna_registered_users', registeredUsers);

            // 메모리 변수 즉시 초기화
            pets = JSON.parse(JSON.stringify(INITIAL_PETS));
            walks = [];
            meals = [];
            schedules = JSON.parse(JSON.stringify(INITIAL_SCHEDULES));
            posts = JSON.parse(JSON.stringify(INITIAL_POSTS));
            albums = JSON.parse(JSON.stringify(INITIAL_ALBUM));
            cart = [];
            friends = JSON.parse(JSON.stringify(INITIAL_FRIENDS));
            letters = JSON.parse(JSON.stringify(INITIAL_LETTERS));

            // 상태를 로컬 스토리지에 동기화 저장
            if (typeof saveState === 'function') saveState();

            // 모든 컴포넌트 즉시 재렌더링 (페이지 리로드 없이)
            if (typeof renderMyPets === 'function') renderMyPets();
            if (typeof renderWalkHistory === 'function') renderWalkHistory();
            if (typeof renderStatsChart === 'function') renderStatsChart();
            if (typeof renderMealLogsList === 'function') renderMealLogsList();
            if (typeof renderSettings === 'function') renderSettings();

            showToast("🗑️ 모든 데이터가 초기화되었습니다.");
        }
    });
}

function destroyAllLocalStorage() {
    showCustomDialog({
        title: "🚨 보관소 완전 파괴 경고!",
        message: "이 작업은 기기 보관소(localStorage)의 모든 데이터(계정 정보, 세션, 환경설정, 산책 및 펫 기록 전체)를 복구 불가능하게 파괴합니다. 계속하시겠습니까?",
        icon: "💥",
        type: "confirm",
        onConfirm: () => {
            localStorage.clear();
            showToast("💥 보관소가 완전히 폭파되었습니다. 앱을 재부팅합니다...");
            setTimeout(() => {
                window.location.reload();
            }, 1500);
        }
    });
}

// GDPR/PIPA 준수 - 개인정보 삭제 요청
async function requestDataDeletion() {
    showCustomDialog({
        title: "🗑️ 개인정보 삭제 요청",
        message: "모든 개인정보 및 반려동물 데이터가 즉시 삭제됩니다. 이 작업은 되돌릴 수 없습니다.\n\n삭제되는 데이터:\n• 계정 정보 (이메일, 닉네임)\n• 반려동물 프로필 및 사진\n• 건강·산책·식사 기록\n• 소셜 게시물 및 댓글\n• 일기장 및 앨범\n\n정말로 삭제하시겠습니까?",
        icon: "⚠️",
        type: "confirm",
        onConfirm: async () => {
            try {
                // 1. Supabase 데이터 삭제 (클라우드 연동 시)
                if (typeof isSupabaseConnected !== 'undefined' && isSupabaseConnected && supabaseClient) {
                    const { data: { user } } = await supabaseClient.auth.getUser();

                    if (user) {
                        // 모든 테이블에서 사용자 데이터 삭제
                        const tables = ['pets', 'posts', 'walks', 'meals', 'albums', 'health_logs', 'schedules'];

                        for (const table of tables) {
                            await supabaseClient
                                .from(table)
                                .delete()
                                .eq('user_id', user.id);
                        }

                        // 계정 삭제는 admin API 필요하므로 로그아웃만 처리
                        await supabaseClient.auth.signOut();

                        showToast("✅ Supabase 클라우드 데이터가 삭제되었습니다.");
                    }
                }

                // 2. LocalStorage 완전 삭제
                localStorage.clear();
                sessionStorage.clear();

                // 3. IndexedDB 삭제 (있을 경우)
                if (window.indexedDB) {
                    const databases = await window.indexedDB.databases();
                    for (const db of databases) {
                        if (db.name) {
                            window.indexedDB.deleteDatabase(db.name);
                        }
                    }
                }

                // 4. 쿠키 삭제
                document.cookie.split(";").forEach((c) => {
                    document.cookie = c.replace(/^ +/, "").replace(/=.*/, "=;expires=" + new Date().toUTCString() + ";path=/");
                });

                showToast("✅ 모든 개인정보가 완전히 삭제되었습니다. 로그인 화면으로 이동합니다.");

                setTimeout(() => {
                    window.location.reload();
                }, 2000);

            } catch (error) {
                console.error('데이터 삭제 중 오류:', error);
                showToast("❌ 데이터 삭제 중 오류가 발생했습니다. 관리자에게 문의하세요: crossx362@gmail.com");
            }
        }
    });
}

// 🚨 시스템 오류 로그 모달 및 관리 기능
function openErrorLogModal() {
    const modal = document.getElementById('error-log-modal');
    const logList = document.getElementById('error-log-list');
    if (!modal || !logList) return;

    logList.innerHTML = '';
    
    const logs = (typeof AppLogger !== 'undefined' && AppLogger.getErrorLogs) 
        ? AppLogger.getErrorLogs() 
        : [];

    if (logs.length === 0) {
        logList.innerHTML = `
            <div class="text-center py-8 text-gray-400 text-xs">
                <i class="fa-solid fa-circle-check text-emerald-500 text-2xl mb-2 block animate-bounce"></i>
                발생한 시스템 오류 로그가 없습니다. 앱이 매우 안정적입니다! 🐾
            </div>
        `;
    } else {
        logs.forEach(log => {
            const card = document.createElement('div');
            const badgeClass = log.type === 'error' || log.type === 'global_error'
                ? 'bg-rose-100 text-rose-700'
                : 'bg-amber-100 text-amber-700';
            const badgeText = log.type.toUpperCase();

            card.className = "bg-gray-50/50 p-3 rounded-xl border border-gray-100/80 text-left space-y-1.5 transition-all hover:bg-gray-50";
            card.innerHTML = `
                <div class="flex items-center justify-between">
                    <span class="px-2 py-0.5 rounded-full font-bold text-[9px] ${badgeClass}">${badgeText}</span>
                    <span class="text-[9px] text-gray-400 font-mono">${new Date(log.timestamp).toLocaleString()}</span>
                </div>
                <h5 class="text-xs font-black text-gray-700 leading-snug break-all">${log.message}</h5>
                ${log.stack ? `
                <details class="text-[9px] text-gray-400 bg-white/70 p-2 rounded-lg border border-gray-100 font-mono cursor-pointer select-none">
                    <summary class="font-bold hover:text-gray-600 outline-none">StackTrace 더보기</summary>
                    <p class="mt-1 whitespace-pre-wrap break-all leading-normal select-text">${log.stack}</p>
                </details>
                ` : ''}
            `;
            logList.appendChild(card);
        });
    }

    modal.classList.remove('hidden');
    modal.classList.add('flex');
}

function closeErrorLogModal() {
    const modal = document.getElementById('error-log-modal');
    if (modal) {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }
}

function clearSystemErrorLogs() {
    showCustomDialog({
        title: "오류 로그 비우기 확인 ⚠️",
        message: "로컬 스토리지에 보관된 시스템 에러 및 경고 기록을 전체 삭제하시겠습니까?",
        icon: "🗑️",
        type: "confirm",
        onConfirm: () => {
            if (typeof AppLogger !== 'undefined' && AppLogger.clearErrorLogs) {
                AppLogger.clearErrorLogs();
            }
            showToast("🗑️ 시스템 오류 로그가 모두 비워졌습니다.");
            
            const logList = document.getElementById('error-log-list');
            if (logList) {
                logList.innerHTML = `
                    <div class="text-center py-8 text-gray-400 text-xs">
                        <i class="fa-solid fa-circle-check text-emerald-500 text-2xl mb-2 block"></i>
                        발생한 시스템 오류 로그가 없습니다. 앱이 매우 안정적입니다! 🐾
                    </div>
                `;
            }
        }
    });
}

function copyErrorLogsToClipboard() {
    const logs = (typeof AppLogger !== 'undefined' && AppLogger.getErrorLogs) 
        ? AppLogger.getErrorLogs() 
        : [];
    
    if (logs.length === 0) {
        showToast("📋 복사할 오류 로그가 없습니다.");
        return;
    }

    let dumpText = "=== PET&NA SYSTEM ERROR LOG DUMP ===\n";
    logs.forEach((log, index) => {
        dumpText += `[${index + 1}] Time: ${log.timestamp} | Type: ${log.type}\n`;
        dumpText += `Message: ${log.message}\n`;
        if (log.stack) dumpText += `Stack: ${log.stack}\n`;
        dumpText += "------------------------------------\n";
    });

    navigator.clipboard.writeText(dumpText).then(() => {
        showToast("📋 전체 오류 로그가 클립보드에 복사되었습니다!");
    }).catch(err => {
        console.error("Clipboard copy failed", err);
        showToast("❌ 클립보드 복사에 실패했습니다. 권한을 확인해주세요.");
    });
}
