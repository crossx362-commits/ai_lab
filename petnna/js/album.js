let activeSticker = null;
let activeDecoFilter = 'natural';
let albumTrimStart = 0;
let albumTrimEnd = 10;
let selectedAlbumIndices = [];
let currentDiaryMode = 'me';
// { name: string, email: string } 형태
let sharedFriends = JSON.parse(localStorage.getItem('petna_shared_friends') || '[]');

function selectSticker(el) {
    document.querySelectorAll('#stickers-container > div').forEach(div => {
        div.classList.remove('sticker-active');
        div.style.outline = "none";
    });
    el.classList.add('sticker-active');
    el.style.outline = "2px dashed #f59e0b";
    el.style.borderRadius = "8px";
    activeSticker = el;

    const hud = document.getElementById('sticker-control-hud');
    if (hud) {
        hud.classList.remove('hidden');
        
        const scaleSlider = document.getElementById('hud-sticker-scale');
        const scaleVal = document.getElementById('hud-scale-val');
        const rotateSlider = document.getElementById('hud-sticker-rotate');
        const rotateVal = document.getElementById('hud-rotate-val');
        
        const currentScale = el.dataset.scale || "1.0";
        const currentRotate = el.dataset.rotate || "0";
        
        if (scaleSlider) scaleSlider.value = currentScale;
        if (scaleVal) scaleVal.innerText = currentScale + "x";
        if (rotateSlider) rotateSlider.value = currentRotate;
        if (rotateVal) rotateVal.innerText = currentRotate + "°";
    }
}

function deselectActiveSticker() {
    document.querySelectorAll('#stickers-container > div').forEach(div => {
        div.classList.remove('sticker-active');
        div.style.outline = "none";
    });
    activeSticker = null;
    const hud = document.getElementById('sticker-control-hud');
    if (hud) hud.classList.add('hidden');
}

function updateActiveStickerScale(val) {
    if (!activeSticker) return;
    activeSticker.dataset.scale = val;
    updateStickerTransform(activeSticker);
    const scaleVal = document.getElementById('hud-scale-val');
    if (scaleVal) scaleVal.innerText = val + "x";
}

function updateActiveStickerRotate(val) {
    if (!activeSticker) return;
    activeSticker.dataset.rotate = val;
    updateStickerTransform(activeSticker);
    const rotateVal = document.getElementById('hud-rotate-val');
    if (rotateVal) rotateVal.innerText = val + "°";
}

function changeActiveStickerZIndex(dir) {
    if (!activeSticker) return;
    let z = parseInt(activeSticker.dataset.zIndex || "20");
    if (dir === 'up') z = Math.min(z + 5, 50);
    else z = Math.max(z - 5, 5);
    activeSticker.dataset.zIndex = z;
    activeSticker.style.zIndex = z;
    showToast("레이어 순서가 조정되었습니다! (Z-index: " + z + ")");
}

function updateStickerTransform(el) {
    const scale = el.dataset.scale || "1.0";
    const rotate = el.dataset.rotate || "0";
    el.style.transform = `scale(${scale}) rotate(${rotate}deg)`;
}

function deleteActiveSticker() {
    if (!activeSticker) return;
    activeSticker.remove();
    deselectActiveSticker();
    showToast("선택했던 스티커가 삭제되었습니다.");
}

function addEmojiSticker(emoji) {
    const container = document.getElementById('stickers-container');
    if (!container) return;

    const sticker = document.createElement('div');
    sticker.className = "absolute text-4xl select-none cursor-move sticker-active pointer-events-auto p-1.5 touch-none";
    sticker.style.left = "40%";
    sticker.style.top = "40%";
    
    // Set custom properties
    sticker.dataset.type = "emoji";
    sticker.dataset.content = emoji;
    sticker.dataset.scale = "1.0";
    sticker.dataset.rotate = "0";
    sticker.dataset.zIndex = "20";
    sticker.style.zIndex = "20";

    sticker.innerHTML = `
        <span>${emoji}</span>
        <button onclick="event.stopPropagation(); this.parentElement.remove(); deselectActiveSticker();" class="delete-btn absolute -top-2.5 -right-2.5 bg-red-500 text-white rounded-full w-4 h-4 text-[9px] flex items-center justify-center shadow-lg"><i class="fa-solid fa-x"></i></button>
    `;

    bindStickerDragEvents(sticker);
    container.appendChild(sticker);
    selectSticker(sticker);

    document.getElementById('decorator-placeholder').classList.add('hidden');
}

function addTextSticker() {
    const input = document.getElementById('sticker-text-input');
    if (!input || !input.value.trim()) return;

    const container = document.getElementById('stickers-container');
    if (!container) return;

    const text = input.value.trim();
    const bubbleTheme = document.getElementById('sticker-bubble-theme').value;
    const fontSize = document.getElementById('sticker-font-size').value;

    const sticker = document.createElement('div');
    sticker.className = `absolute select-none cursor-move sticker-active pointer-events-auto p-2 rounded-xl shadow-lg border touch-none flex items-center space-x-1 ${bubbleTheme} ${fontSize}`;
    sticker.style.left = "30%";
    sticker.style.top = "50%";

    // Set custom properties
    sticker.dataset.type = "text";
    sticker.dataset.content = text;
    sticker.dataset.scale = "1.0";
    sticker.dataset.rotate = "0";
    sticker.dataset.zIndex = "20";
    sticker.style.zIndex = "20";
    sticker.dataset.bubbleTheme = bubbleTheme;
    sticker.dataset.fontSize = fontSize;

    sticker.innerHTML = `
        <span>💬 ${text}</span>
        <button onclick="event.stopPropagation(); this.parentElement.remove(); deselectActiveSticker();" class="delete-btn absolute -top-2.5 -right-2.5 bg-red-500 text-white rounded-full w-4 h-4 text-[9px] flex items-center justify-center shadow-lg"><i class="fa-solid fa-x"></i></button>
    `;

    bindStickerDragEvents(sticker);
    container.appendChild(sticker);
    selectSticker(sticker);

    input.value = '';
    document.getElementById('decorator-placeholder').classList.add('hidden');
}

function bindStickerDragEvents(el) {
    let isDragging = false;
    let startX, startY;
    let currentLeft, currentTop;

    const onStart = (e) => {
        if (e.target.closest('.delete-btn')) return;

        isDragging = true;
        selectSticker(el);

        const clientX = e.touches ? e.touches[0].clientX : e.clientX;
        const clientY = e.touches ? e.touches[0].clientY : e.clientY;

        startX = clientX;
        startY = clientY;

        currentLeft = el.offsetLeft;
        currentTop = el.offsetTop;
        e.preventDefault();
    };

    const onMove = (e) => {
        if (!isDragging) return;

        const clientX = e.touches ? e.touches[0].clientX : e.clientX;
        const clientY = e.touches ? e.touches[0].clientY : e.clientY;

        const dx = clientX - startX;
        const dy = clientY - startY;

        const canvas = document.getElementById('decorator-canvas');
        const nextX = currentLeft + dx;
        const nextY = currentTop + dy;

        if (nextX >= 0 && nextX <= canvas.clientWidth - el.clientWidth) {
            el.style.left = nextX + "px";
        }
        if (nextY >= 0 && nextY <= canvas.clientHeight - el.clientHeight) {
            el.style.top = nextY + "px";
        }
    };

    const onEnd = () => {
        isDragging = false;
    };

    el.addEventListener('mousedown', onStart);
    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup', onEnd);

    el.addEventListener('touchstart', onStart, { passive: false });
    document.addEventListener('touchmove', onMove, { passive: false });
    document.addEventListener('touchend', onEnd);
}

function uploadDecoDeviceMedia(event) {
    const file = event.target.files[0];
    if (!file) return;

    // 업로드 중 배지 표시 및 완료 배지 숨김
    const uploadingBadge = document.getElementById('deco-uploading-badge');
    const uploadBadge = document.getElementById('deco-upload-badge');
    if (uploadingBadge) uploadingBadge.classList.remove('hidden');
    if (uploadBadge) uploadBadge.classList.add('hidden');

    const reader = new FileReader();
    reader.onload = function(e) {
        const url = e.target.result;
        
        // 박진감 넘치는 업로드 체감을 위해 750ms의 시뮬레이션 지연을 줍니다.
        setTimeout(() => {
            const albumBg = document.getElementById('decorator-bg');
            const albumVideo = document.getElementById('decorator-bg-video');
            const albumPlaceholder = document.getElementById('decorator-placeholder');
            const trimmerBox = document.getElementById('video-trimmer-box');

            if (file.type.startsWith('video/')) {
                if (albumBg && albumVideo && albumPlaceholder) {
                    albumBg.classList.add('hidden');
                    albumVideo.src = url;
                    albumVideo.classList.remove('hidden');
                    albumPlaceholder.classList.add('hidden');
                    
                    if (trimmerBox) trimmerBox.classList.remove('hidden');

                    albumVideo.load();

                    albumVideo.onloadedmetadata = function() {
                        const duration = albumVideo.duration || 10;
                        albumTrimStart = 0;
                        albumTrimEnd = Math.min(10, duration);

                        const startSlider = document.getElementById('trim-start-slider');
                        const endSlider = document.getElementById('trim-end-slider');

                        if (startSlider) {
                            startSlider.max = duration;
                            startSlider.value = 0;
                        }
                        if (endSlider) {
                            endSlider.max = duration;
                            endSlider.value = albumTrimEnd;
                        }

                        const startValLabel = document.getElementById('trim-start-val');
                        const endValLabel = document.getElementById('trim-end-val');
                        const durationBadge = document.getElementById('trim-duration-badge');

                        if (startValLabel) startValLabel.innerText = "0.0s";
                        if (endValLabel) endValLabel.innerText = albumTrimEnd.toFixed(1) + "s";
                        if (durationBadge) durationBadge.innerText = "구간 길이: " + albumTrimEnd.toFixed(1) + "초";
                    };

                    albumVideo.ontimeupdate = function() {
                        if (this.currentTime < albumTrimStart) {
                            this.currentTime = albumTrimStart;
                        }
                        if (this.currentTime > albumTrimEnd) {
                            this.currentTime = albumTrimStart;
                        }
                    };

                    albumVideo.play();
                }
            } else {
                if (albumBg && albumVideo && albumPlaceholder) {
                    albumBg.src = url;
                    albumBg.classList.remove('hidden');
                    albumVideo.classList.add('hidden');
                    albumPlaceholder.classList.add('hidden');
                    
                    if (trimmerBox) trimmerBox.classList.add('hidden');
                }
            }
            
            // 로딩 상태 완료로 전환
            if (uploadingBadge) uploadingBadge.classList.add('hidden');
            if (uploadBadge) uploadBadge.classList.remove('hidden');
            
            showToast("내 기기의 파일이 액티브 데코룸에 직접 업로드되었습니다! 📂✨");
        }, 750);
    };
    reader.readAsDataURL(file);
}

function updateVideoTrimStart(val) {
    const video = document.getElementById('decorator-bg-video');
    if (!video) return;

    albumTrimStart = parseFloat(val);
    const startValLabel = document.getElementById('trim-start-val');
    if (startValLabel) startValLabel.innerText = albumTrimStart.toFixed(1) + "s";

    if (albumTrimStart >= albumTrimEnd) {
        albumTrimEnd = Math.min(video.duration || 30, albumTrimStart + 1.0);
        const endSlider = document.getElementById('trim-end-slider');
        if (endSlider) endSlider.value = albumTrimEnd;
        const endValLabel = document.getElementById('trim-end-val');
        if (endValLabel) endValLabel.innerText = albumTrimEnd.toFixed(1) + "s";
    }

    const durationBadge = document.getElementById('trim-duration-badge');
    if (durationBadge) {
        durationBadge.innerText = "구간 길이: " + (albumTrimEnd - albumTrimStart).toFixed(1) + "초";
    }

    video.currentTime = albumTrimStart;
}

function updateVideoTrimEnd(val) {
    const video = document.getElementById('decorator-bg-video');
    if (!video) return;

    albumTrimEnd = parseFloat(val);
    const endValLabel = document.getElementById('trim-end-val');
    if (endValLabel) endValLabel.innerText = albumTrimEnd.toFixed(1) + "s";

    if (albumTrimEnd <= albumTrimStart) {
        albumTrimStart = Math.max(0, albumTrimEnd - 1.0);
        const startSlider = document.getElementById('trim-start-slider');
        if (startSlider) startSlider.value = albumTrimStart;
        const startValLabel = document.getElementById('trim-start-val');
        if (startValLabel) startValLabel.innerText = albumTrimStart.toFixed(1) + "s";
    }

    const durationBadge = document.getElementById('trim-duration-badge');
    if (durationBadge) {
        durationBadge.innerText = "구간 길이: " + (albumTrimEnd - albumTrimStart).toFixed(1) + "초";
    }

    video.currentTime = albumTrimStart;
}

function addEmojiStickerAtPosition(emoji, left, top, scale = "1.0", rotate = "0") {
    const container = document.getElementById('stickers-container');
    if (!container) return;

    const sticker = document.createElement('div');
    sticker.className = "absolute text-4xl select-none cursor-move sticker-active pointer-events-auto p-1.5 touch-none";
    sticker.style.left = left;
    sticker.style.top = top;
    
    sticker.dataset.type = "emoji";
    sticker.dataset.content = emoji;
    sticker.dataset.scale = scale;
    sticker.dataset.rotate = rotate;
    sticker.dataset.zIndex = "20";
    sticker.style.zIndex = "20";

    sticker.innerHTML = `
        <span>${emoji}</span>
        <button onclick="event.stopPropagation(); this.parentElement.remove(); deselectActiveSticker();" class="delete-btn absolute -top-2.5 -right-2.5 bg-red-500 text-white rounded-full w-4 h-4 text-[9px] flex items-center justify-center shadow-lg"><i class="fa-solid fa-x"></i></button>
    `;

    updateStickerTransform(sticker);
    bindStickerDragEvents(sticker);
    container.appendChild(sticker);
    selectSticker(sticker);

    document.getElementById('decorator-placeholder').classList.add('hidden');
}

function addTextStickerWithParams(text, bubbleTheme, fontSize, left, top, scale = "1.0", rotate = "0") {
    const container = document.getElementById('stickers-container');
    if (!container) return;

    const sticker = document.createElement('div');
    sticker.className = `absolute select-none cursor-move sticker-active pointer-events-auto p-2 rounded-xl shadow-lg border touch-none flex items-center space-x-1 ${bubbleTheme} ${fontSize}`;
    sticker.style.left = left;
    sticker.style.top = top;

    sticker.dataset.type = "text";
    sticker.dataset.content = text;
    sticker.dataset.scale = scale;
    sticker.dataset.rotate = rotate;
    sticker.dataset.zIndex = "20";
    sticker.style.zIndex = "20";
    sticker.dataset.bubbleTheme = bubbleTheme;
    sticker.dataset.fontSize = fontSize;

    sticker.innerHTML = `
        <span>💬 ${text}</span>
        <button onclick="event.stopPropagation(); this.parentElement.remove(); deselectActiveSticker();" class="delete-btn absolute -top-2.5 -right-2.5 bg-red-500 text-white rounded-full w-4 h-4 text-[9px] flex items-center justify-center shadow-lg"><i class="fa-solid fa-x"></i></button>
    `;

    updateStickerTransform(sticker);
    bindStickerDragEvents(sticker);
    container.appendChild(sticker);
    selectSticker(sticker);

    document.getElementById('decorator-placeholder').classList.add('hidden');
}


function applyDecoFilter(filterName, btn) {
    activeDecoFilter = filterName;
    
    const albumBg = document.getElementById('decorator-bg');
    const albumVideo = document.getElementById('decorator-bg-video');
    const filterVal = getFilterCSSValue(filterName);

    if (albumBg) albumBg.style.filter = filterVal;
    if (albumVideo) albumVideo.style.filter = filterVal;

    // Highlight button
    const bar = document.getElementById('deco-filter-bar');
    if (bar) {
        bar.querySelectorAll('button').forEach(b => {
            b.className = "filter-btn bg-white hover:bg-amber-50 text-gray-600 font-bold text-[10px] py-1.5 px-3 rounded-lg border border-gray-200 shadow-sm transition-all";
        });
    }
    if (btn) {
        btn.className = "filter-btn bg-brand-500 text-white font-bold text-[10px] py-1.5 px-3 rounded-lg border border-transparent shadow-sm transition-all";
    }
    showToast("선택하신 감성 백그라운드 필터가 조화롭게 적용되었습니다! 🪄");
}

function getFilterCSSValue(filterName) {
    switch (filterName) {
        case 'sepia': return 'sepia(0.65) contrast(1.1) brightness(0.95)';
        case 'vintage': return 'contrast(0.9) brightness(1.05) saturate(0.85)';
        case 'cyberpunk': return 'hue-rotate(45deg) saturate(1.8) contrast(1.2)';
        case 'noir': return 'grayscale(1) contrast(1.1)';
        default: return 'none';
    }
}

function resetStickerCanvas() {
    const container = document.getElementById('stickers-container');
    if (container) container.innerHTML = '';

    const albumBg = document.getElementById('decorator-bg');
    const albumVideo = document.getElementById('decorator-bg-video');
    const albumPlaceholder = document.getElementById('decorator-placeholder');
    const trimmerBox = document.getElementById('video-trimmer-box');
    const uploadBadge = document.getElementById('deco-upload-badge');

    if (albumBg) { albumBg.src = ''; albumBg.classList.add('hidden'); }
    if (albumVideo) { albumVideo.src = ''; albumVideo.classList.add('hidden'); }
    if (albumPlaceholder) albumPlaceholder.classList.remove('hidden');
    if (trimmerBox) trimmerBox.classList.add('hidden');
    if (uploadBadge) uploadBadge.classList.add('hidden');

    deselectActiveSticker();
    showToast("꾸미기 보드판의 모든 요소와 업로드 파일이 비워졌습니다.");
}



// ─── 성장 기록 ─────────────────────────────────────────────────
let _growthChart = null;

function getWeightHistory() {
    const pet = typeof getActivePet === 'function' ? getActivePet() : null;
    if (!pet) return [];
    return pet.weightHistory || [];
}

function openWeightLogModal() {
    const pet = typeof getActivePet === 'function' ? getActivePet() : null;
    if (!pet) { if (typeof showToast === 'function') showToast('먼저 반려동물을 등록해주세요'); return; }
    const modal = document.getElementById('weight-log-modal');
    const input = document.getElementById('weight-log-input');
    if (input && pet.weight) input.value = pet.weight;
    if (modal) modal.classList.remove('hidden');
}

function closeWeightLogModal() {
    const modal = document.getElementById('weight-log-modal');
    if (modal) modal.classList.add('hidden');
}

function submitWeightLog() {
    const input = document.getElementById('weight-log-input');
    const noteInput = document.getElementById('weight-log-note');
    const val = parseFloat(input?.value);
    if (!val || val <= 0) { if (typeof showToast === 'function') showToast('체중을 입력해주세요'); return; }

    const pet = typeof getActivePet === 'function' ? getActivePet() : null;
    if (!pet) return;
    if (!pet.weightHistory) pet.weightHistory = [];
    pet.weightHistory.unshift({
        date: new Date().toISOString().split('T')[0],
        weight: val,
        note: noteInput?.value?.trim() || ''
    });
    if (pet.weightHistory.length > 24) pet.weightHistory.splice(24);
    pet.weight = String(val);
    if (typeof saveState === 'function') saveState();
    closeWeightLogModal();
    renderGrowthChart();
    if (typeof showToast === 'function') showToast(`체중 ${val}kg 기록 완료! ⚖️`);
}

function renderGrowthChart() {
    const history = getWeightHistory();
    const chartContainer = document.getElementById('growth-chart-container');
    const emptyEl = document.getElementById('growth-empty');
    const latestEl = document.getElementById('growth-latest');

    if (!chartContainer) return;
    if (history.length === 0) {
        chartContainer.style.display = 'none';
        if (emptyEl) emptyEl.classList.remove('hidden');
        return;
    }
    chartContainer.style.display = 'block';
    if (emptyEl) emptyEl.classList.add('hidden');

    const sorted = [...history].sort((a, b) => a.date.localeCompare(b.date)).slice(-12);
    const latest = sorted[sorted.length - 1];
    const oldest = sorted[0];
    const diff = latest.weight - oldest.weight;
    if (latestEl) {
        latestEl.textContent = `최근: ${latest.weight}kg (${diff >= 0 ? '+' : ''}${diff.toFixed(1)}kg / ${sorted.length}회 기록)`;
    }

    const ctx = document.getElementById('growth-chart');
    if (!ctx || typeof Chart === 'undefined') return;
    if (_growthChart) { _growthChart.destroy(); _growthChart = null; }

    const isDark = document.body.classList.contains('theme-dark');
    const textColor = isDark ? '#d1d1e0' : '#6b7280';

    _growthChart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: sorted.map(h => h.date.slice(5)),
            datasets: [{
                data: sorted.map(h => h.weight),
                borderColor: '#10b981',
                backgroundColor: 'rgba(16,185,129,0.08)',
                borderWidth: 2,
                pointRadius: 3,
                pointBackgroundColor: '#10b981',
                tension: 0.3,
                fill: true
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: {
                x: { ticks: { color: textColor, font: { size: 9 } }, grid: { display: false } },
                y: { ticks: { color: textColor, font: { size: 9 }, callback: v => v + 'kg' }, grid: { color: 'rgba(0,0,0,0.04)' } }
            }
        }
    });
}

// ─── 일기장 고도화 함수들 ────────────────────────────────────────

const DIARY_FREE_LIMIT = 30;

function calcDiaryStreak() {
    if (!albums || albums.length === 0) return 0;
    const toDay = (ts) => new Date(Number(ts)).toISOString().split('T')[0];
    const today = toDay(Date.now());
    const days = [...new Set(albums.map(a => toDay(a.id)))].sort().reverse();
    if (!days.length) return 0;
    let streak = 0;
    let check = new Date();
    // 오늘 일기 없으면 어제부터
    if (days[0] !== today) check.setDate(check.getDate() - 1);
    for (const d of days) {
        const expected = check.toISOString().split('T')[0];
        if (d === expected) { streak++; check.setDate(check.getDate() - 1); }
        else if (d < expected) break;
    }
    return streak;
}

function renderDiaryStats() {
    const el = document.getElementById('diary-stats-row');
    if (!el) return;
    const streak = calcDiaryStreak();
    const total = albums ? albums.length : 0;
    const limit = (typeof isPremium === 'function' && isPremium()) ? '∞' : `${DIARY_FREE_LIMIT}`;
    const streakEmoji = streak === 0 ? '' : streak < 7 ? '🌱' : streak < 30 ? '🔥' : '🏆';
    el.innerHTML = `
        <span class="text-[10px] font-black text-brand-600 bg-brand-50 px-2 py-0.5 rounded-full border border-brand-100">
            📖 ${total}/${limit}개
        </span>
        ${streak > 0 ? `<span class="text-[10px] font-black text-amber-600 bg-amber-50 px-2 py-0.5 rounded-full border border-amber-100">
            ${streakEmoji} ${streak}일 연속
        </span>` : '<span class="text-[10px] text-gray-400 font-medium">오늘 일기를 써보세요!</span>'}`;
    checkDiaryOnThisDay();
}

function checkDiaryOnThisDay() {
    const el = document.getElementById('diary-on-this-day');
    if (!el || !albums || albums.length === 0) return;
    const now = new Date();
    const thisMonthDay = `${String(now.getMonth() + 1).padStart(2,'0')}-${String(now.getDate()).padStart(2,'0')}`;
    const match = albums.find(a => {
        const d = new Date(a.id);
        const md = `${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;
        const yearDiff = now.getFullYear() - d.getFullYear();
        return md === thisMonthDay && yearDiff >= 1;
    });
    if (!match) { el.classList.add('hidden'); return; }
    const yearsAgo = now.getFullYear() - new Date(match.id).getFullYear();
    el.classList.remove('hidden');
    el.innerHTML = `
        <div class="flex items-center gap-3 bg-gradient-to-r from-amber-50 to-orange-50 border border-amber-200 rounded-2xl p-3 cursor-pointer hover:shadow-sm transition-all"
            onclick="scrollToEntry(${match.id})">
            <span class="text-2xl">📅</span>
            <div class="flex-1 min-w-0">
                <p class="text-[11px] font-black text-amber-800">${yearsAgo}년 전 오늘의 추억</p>
                <p class="text-[10px] text-amber-600 font-medium truncate">${match.text || match.dateStr || '소중한 기억'}</p>
            </div>
            <i class="fa-solid fa-chevron-right text-amber-400 text-xs flex-shrink-0"></i>
        </div>`;
}

function scrollToEntry(id) {
    const el = document.getElementById(`diary-entry-${id}`);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
}

function exportDiaryPDF() {
    if (typeof isPremium === 'function' && !isPremium()) {
        if (typeof showPremiumModal === 'function') showPremiumModal();
        return;
    }
    if (!albums || albums.length === 0) {
        if (typeof showToast === 'function') showToast('일기가 없습니다. 먼저 일기를 써보세요!');
        return;
    }
    const pet = typeof getActivePet === 'function' ? getActivePet() : null;
    const petName = pet?.name || '반려동물';
    const now = new Date();

    const entries = albums.slice(0, 50).map(a => {
        const d = new Date(a.id);
        const dateLabel = `${d.getFullYear()}년 ${d.getMonth()+1}월 ${d.getDate()}일`;
        const moodEmoji = { happy: '😊', sad: '😢', excited: '🎉', tired: '😴', sick: '🤒' }[a.mood] || '📝';
        return `
        <div style="page-break-inside:avoid;margin-bottom:24px;padding:16px;border:1px solid #fef3c7;border-radius:12px;background:#fffbeb">
            <div style="display:flex;align-items:center;gap:8px;margin-bottom:8px">
                <span style="font-size:18px">${moodEmoji}</span>
                <span style="font-size:12px;font-weight:900;color:#92400e">${dateLabel}</span>
                <span style="font-size:10px;color:#d97706;margin-left:auto">by ${a.author || '집사'}</span>
            </div>
            ${a.url ? `<div style="margin-bottom:8px"><img loading="lazy" src="${a.url}" style="max-width:100%;max-height:200px;object-fit:cover;border-radius:8px"></div>` : ''}
            <p style="font-size:12px;color:#374151;line-height:1.6;white-space:pre-wrap">${a.text || ''}</p>
        </div>`;
    }).join('');

    const html = `<!DOCTYPE html><html lang="ko"><head>
<meta charset="UTF-8"><title>${petName}의 일기장</title>
<style>
body{font-family:'Apple SD Gothic Neo','Noto Sans KR',sans-serif;margin:0;padding:32px;color:#1f2937}
h1{font-size:28px;font-weight:900;color:#e37736;margin-bottom:4px}
.sub{font-size:13px;color:#9ca3af;margin-bottom:32px}
.footer{margin-top:40px;text-align:center;font-size:10px;color:#d1d5db}
@media print{body{padding:20px}button{display:none}}
</style></head><body>
<h1>📖 ${petName}의 소중한 일기장</h1>
<p class="sub">총 ${albums.length}편 · 생성일: ${now.toLocaleDateString('ko-KR')} · 펫과나</p>
${entries}
<div class="footer">🐾 펫과나 (Pet & Na) — 펫과 함께 사는 삶</div>
<div style="text-align:center;margin-top:20px">
<button onclick="window.print()" style="background:#e37736;color:#fff;border:none;padding:10px 24px;border-radius:8px;font-size:13px;font-weight:700;cursor:pointer">🖨️ PDF로 저장</button>
</div></body></html>`;
    const blob = new Blob([html], { type: 'text/html; charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const win = window.open(url, '_blank');
    if (!win) { if (typeof showToast === 'function') showToast('팝업 차단을 해제해주세요'); URL.revokeObjectURL(url); return; }
    setTimeout(() => URL.revokeObjectURL(url), 60000);
}

function openDiaryComposerModal() {
    const modal = document.getElementById('diary-composer-modal');
    if (modal) {
        modal.classList.remove('hidden');
        setTimeout(() => {
            modal.classList.remove('opacity-0');
            modal.firstElementChild.classList.remove('scale-95');
        }, 10);
    }
}

function closeDiaryComposerModal() {
    const modal = document.getElementById('diary-composer-modal');
    if (modal) {
        modal.classList.add('opacity-0');
        modal.firstElementChild.classList.add('scale-95');
        setTimeout(() => modal.classList.add('hidden'), 300);
    }
}

function openFriendInviteModal() {
    const modal = document.getElementById('friend-invite-modal');
    if (modal) {
        modal.classList.remove('hidden');
        setTimeout(() => {
            modal.classList.remove('opacity-0');
            modal.firstElementChild.classList.remove('scale-95');
        }, 10);
    }
}

function closeFriendInviteModal() {
    const modal = document.getElementById('friend-invite-modal');
    if (modal) {
        modal.classList.add('opacity-0');
        modal.firstElementChild.classList.add('scale-95');
        setTimeout(() => modal.classList.add('hidden'), 300);
    }
}

function sendFriendInvite(friendName, friendEmail, btnEl) {
    if (sharedFriends.some(f => f.email === friendEmail)) {
        showToast("이미 공유 중인 친구입니다.");
        return;
    }

    btnEl.innerText = "초대 중...";
    btnEl.disabled = true;
    btnEl.classList.add('opacity-50', 'cursor-not-allowed');

    setTimeout(() => {
        btnEl.innerText = "수락 완료!";
        btnEl.classList.remove('bg-brand-500', 'hover:bg-brand-600', 'opacity-50', 'cursor-not-allowed');
        btnEl.classList.add('bg-emerald-500', 'hover:bg-emerald-600');

        sharedFriends.push({ name: friendName, email: friendEmail });
        localStorage.setItem('petna_shared_friends', JSON.stringify(sharedFriends));
        showToast(`🎉 ${friendName}님이 교환 일기 초대를 수락했습니다!`);

        setTimeout(() => {
            closeFriendInviteModal();
            renderAlbumGallery();
        }, 1000);
    }, 1500);
}

function sendFriendInviteByEmail(btnEl) {
    const nameEl = document.getElementById('invite-friend-name');
    const emailEl = document.getElementById('invite-friend-email');
    const name = (nameEl?.value || '').trim();
    const email = (emailEl?.value || '').trim();
    if (!email) { showToast("이메일을 입력해주세요."); return; }
    sendFriendInvite(name || email.split('@')[0], email, btnEl);
    if (nameEl) nameEl.value = '';
    if (emailEl) emailEl.value = '';
}

function submitDiaryAuth() {
    // 무료 30개 제한
    if (typeof isPremium === 'function' && !isPremium() && albums && albums.length >= DIARY_FREE_LIMIT) {
        if (typeof showPremiumModal === 'function') showPremiumModal();
        if (typeof showToast === 'function') showToast(`무료 일기는 ${DIARY_FREE_LIMIT}개까지 저장됩니다. 프리미엄으로 무제한 저장하세요 👑`);
        return;
    }
    const bgVideoEl = document.getElementById('decorator-bg-video');
    const bgImgEl = document.getElementById('decorator-bg');
    if (!bgVideoEl || !bgImgEl) { if (typeof showToast === 'function') showToast('오류: 에디터를 다시 열어주세요'); return; }
    const isVideoActive = !bgVideoEl.classList.contains('hidden');
    let finalUrl = isVideoActive ? bgVideoEl.src : bgImgEl.src;

    const mood = document.getElementById('diary-auth-mood').value;
    const text = document.getElementById('diary-auth-text').value;

    const stickersData = [];
    const canvas = document.getElementById('decorator-canvas');
    if (!canvas) return;

    const canvasWidth = canvas.clientWidth || 400;
    const canvasHeight = canvas.clientHeight || 300;

    document.querySelectorAll('#stickers-container > div').forEach(el => {
        const leftPct = (parseFloat(el.style.left) / canvasWidth) * 100;
        const topPct = (parseFloat(el.style.top) / canvasHeight) * 100;
        
        stickersData.push({
            type: el.dataset.type || "emoji",
            content: el.dataset.content || "",
            left: leftPct,
            top: topPct,
            scale: parseFloat(el.dataset.scale || "1.0"),
            rotate: parseFloat(el.dataset.rotate || "0"),
            zIndex: el.dataset.zIndex || "20",
            bubbleTheme: el.dataset.bubbleTheme || "",
            fontSize: el.dataset.fontSize || ""
        });
    });

    const now = new Date();
    const dateStr = `${now.getFullYear()}년 ${now.getMonth()+1}월 ${now.getDate()}일 ${now.getHours()}:${now.getMinutes().toString().padStart(2,'0')}`;

    const work = {
        id: Date.now(),
        url: finalUrl,
        isVideo: isVideoActive,
        start: albumTrimStart,
        end: albumTrimEnd,
        filter: activeDecoFilter || 'natural',
        stickers: stickersData,
        mood: mood,
        text: text,
        dateStr: dateStr,
        author: typeof settings_nickname !== 'undefined' ? settings_nickname : '집사'
    };

    albums.unshift(work);
    saveState();

    if (typeof uploadAlbumToSupabase === 'function') {
        uploadAlbumToSupabase(work);
    }

    renderDiaryStats();
    closeDiaryComposerModal();
    document.getElementById('diary-auth-text').value = ''; // 초기화
    
    // 강제로 내 일기장 탭으로 전환 후 렌더링
    switchDiaryMode('me');
    
    deselectActiveSticker();
    resetStickerCanvas();
    
    showCustomDialog({
        title: "일기 저장 성공! 📕✨",
        message: "오늘의 예쁜 추억이 일기장에 차곡차곡 쌓였습니다.\n지금 아래 타임라인에서 확인해보시겠습니까?",
        icon: "🖼️",
        type: "confirm",
        onConfirm: () => {
            const gallerySection = document.getElementById('diary-timeline-container');
            if (gallerySection) {
                gallerySection.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        }
    });
}

function goToDayRoom() {
    if (typeof switchTab === 'function') {
        switchTab('mypet');
        showToast("🐾 댕이의 하루방으로 이동했습니다!");
        setTimeout(() => {
            const petRoomTitle = document.getElementById('pet-room-name-wrapper');
            if (petRoomTitle) {
                petRoomTitle.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }
        }, 300);
    }
}

// -----------------------------------------
// 공유 일기장 탭 전환 로직
// -----------------------------------------
function switchDiaryMode(mode) {
    currentDiaryMode = mode;
    const btnMe = document.getElementById('diary-tab-me');
    const btnShared = document.getElementById('diary-tab-shared');
    
    if (mode === 'me') {
        if(btnMe) btnMe.className = "flex-1 bg-white text-brand-600 font-bold text-xs py-2 rounded-lg shadow-sm transition-all";
        if(btnShared) btnShared.className = "flex-1 text-gray-500 hover:text-gray-700 font-bold text-xs py-2 rounded-lg transition-all";
    } else {
        if(btnShared) btnShared.className = "flex-1 bg-white text-brand-600 font-bold text-xs py-2 rounded-lg shadow-sm transition-all";
        if(btnMe) btnMe.className = "flex-1 text-gray-500 hover:text-gray-700 font-bold text-xs py-2 rounded-lg transition-all";
    }
    renderAlbumGallery();
}

function getSharedDiaryMocks() {
    return [
        {
            id: 999999,
            url: "https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=400",
            isVideo: false,
            filter: "natural",
            stickers: [{ type: "text", content: "너무 귀여운 내새끼", left: 50, top: 20, scale: 1.0, rotate: -5, bubbleTheme: "bg-white", fontSize: "text-xs" }],
            mood: "💖 사랑해",
            text: "오랜만에 댕댕이 친구네 놀러갔다. 서로 너무 잘 놀아서 다행! 앞으로도 자주 보자~",
            dateStr: "2026년 5월 22일 14:30",
            author: "이웃집 집사",
            isMock: true
        },
        {
            id: 999998,
            url: "https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba?auto=format&fit=crop&q=80&w=400",
            isVideo: false,
            filter: "sepia",
            stickers: [{ type: "emoji", content: "💤", left: 80, top: 80, scale: 1.5, rotate: 10 }],
            mood: "😴 피곤",
            text: "산책 다녀왔더니 고양이가 완전 뻗어버림 ㅋㅋ 귀엽다 증말",
            dateStr: "2026년 5월 21일 18:00",
            author: "동네 냥이맘",
            isMock: true
        }
    ];
}



function renderAlbumGallery() {
    const container = document.getElementById('diary-timeline-container');
    const emptyState = document.getElementById('diary-empty-state');
    const friendListContainer = document.getElementById('diary-shared-friends-list');
    const friendCountBadge = document.getElementById('diary-friend-count');
    
    if (friendListContainer) {
        if (sharedFriends.length === 0) {
            friendListContainer.innerHTML = '<div class="text-center py-4 text-[10px] text-gray-400">아직 공유 중인 친구가 없습니다.</div>';
            if (friendCountBadge) friendCountBadge.innerText = '0명';
        } else {
            if (friendCountBadge) friendCountBadge.innerText = `${sharedFriends.length}명`;
            friendListContainer.innerHTML = sharedFriends.map(f => `
                <div class="flex items-center justify-between">
                    <div class="flex items-center gap-2">
                        <div class="w-8 h-8 rounded-full bg-brand-100 text-brand-500 flex items-center justify-center font-black overflow-hidden shadow-sm">
                            ${f.slice(0, 1)}
                        </div>
                        <div>
                            <span class="block text-[11px] font-bold text-gray-800">${f}</span>
                        </div>
                    </div>
                    <span class="text-[9px] font-bold text-brand-500 bg-brand-50 px-1.5 py-0.5 rounded border border-brand-100">구독중</span>
                </div>
            `).join('');
        }
    }

    if (!container) return;

    container.innerHTML = '';

    // 친구 일기 fetch 후 렌더링
    const friendEmails = sharedFriends.map(f => f.email).filter(Boolean);
    const fetchPromise = (friendEmails.length > 0 && typeof fetchFriendDiaries === 'function')
        ? fetchFriendDiaries(friendEmails)
        : Promise.resolve([]);

    renderDiaryStats();
    fetchPromise.then(friendDiaries => {
        const displayList = [...albums, ...friendDiaries]
            .sort((a, b) => (b.id || 0) - (a.id || 0));
        _renderDiaryList(container, emptyState, displayList);
    });

}

function _renderDiaryList(container, emptyState, displayList) {
    container.innerHTML = '';

    const pubBtnContainer = document.getElementById('album-publish-btn-container');

    if (displayList.length === 0) {
        if (emptyState) emptyState.classList.remove('hidden');
        container.classList.add('hidden');
        if (pubBtnContainer) pubBtnContainer.classList.add('hidden');
        return;
    }
    if (emptyState) emptyState.classList.add('hidden');
    container.classList.remove('hidden');

    if (pubBtnContainer) {
        pubBtnContainer.classList.remove('hidden');
        pubBtnContainer.innerHTML = `
            <div class="bg-gradient-to-r from-brand-100 to-amber-100 rounded-2xl p-4 border border-brand-200/50 flex flex-col sm:flex-row justify-between items-center gap-3">
                <div class="text-left">
                    <span class="block text-xs font-black text-brand-800">📖 일기장 실물 책으로 출판하기</span>
                    <span class="block text-[10px] text-gray-500">지금까지 쌓인 소중한 일기장 기록들을 평생 소장할 수 있는 실물 하드커버 책으로 만드세요!</span>
                </div>
                <button onclick="openPhotobookPublishModal()" class="w-full sm:w-auto bg-brand-500 hover:bg-brand-600 text-white font-black text-xs py-2.5 px-5 rounded-xl transition-all shadow-md flex items-center justify-center gap-1.5 whitespace-nowrap">
                    <i class="fa-solid fa-print"></i> 포토북 인쇄 출판 신청 📕
                </button>
            </div>
        `;
    }

    const myEmail = (typeof settings_email !== 'undefined' && settings_email) || localStorage.getItem('petna_user_email') || '';

    displayList.forEach((item, idx) => {
        const filterVal = getFilterCSSValue(item.filter || 'natural');
        const isFriend = item.email && item.email !== myEmail;

        let mediaMarkup = '';
        if (item.isWalkCard && item.walkData) {
            const wd = item.walkData;
            mediaMarkup = `
                <div class="w-full h-full bg-gradient-to-br from-brand-500 to-indigo-600 flex flex-col items-center justify-center p-3 text-center">
                    <span class="text-3xl mb-1">🦮</span>
                    <span class="block text-white/90 font-black text-xl mt-0.5">${wd.distance}km</span>
                    <div class="flex gap-2 mt-2 justify-center">
                        <span class="bg-white/20 text-white text-[10px] font-bold px-2 py-1 rounded-full">⏱️ ${wd.duration}</span>
                        <span class="bg-white/20 text-white text-[10px] font-bold px-2 py-1 rounded-full">🔥 ${wd.calories}kcal</span>
                    </div>
                </div>`;
        } else if (item.isVideo) {
            mediaMarkup = `<video src="${item.url}" style="filter: ${filterVal};" class="w-full h-full object-cover bg-gray-900" muted loop autoplay playsinline></video>
                           <div class="absolute top-2 left-2 bg-amber-500 text-white font-bold text-[9px] py-0.5 px-2 rounded-full z-20"><i class="fa-solid fa-video"></i> VIDEO</div>`;
        } else {
            mediaMarkup = `<img src="${item.url}" style="filter: ${filterVal};" class="w-full h-full object-cover bg-gray-100">`;
        }

        let stickersHtml = '';
        if (!item.isWalkCard && item.stickers && item.stickers.length > 0) {
            item.stickers.forEach(st => {
                const s = `position:absolute;left:${st.left}%;top:${st.top}%;z-index:${st.zIndex};transform:translate(-50%,-50%) scale(${st.scale * 0.45}) rotate(${st.rotate}deg);pointer-events:none;transform-origin:center center;`;
                stickersHtml += st.type === 'emoji'
                    ? `<div style="${s} font-size:14px;">${st.content}</div>`
                    : `<div class="p-0.5 rounded px-1 text-[5px] font-black leading-tight border ${st.bubbleTheme} shadow-sm" style="${s}">💬 ${st.content}</div>`;
            });
        }

        const moodBadge = item.mood ? `<span class="bg-white border border-gray-200 text-gray-600 text-[10px] font-black px-2 py-0.5 rounded-full shadow-sm">${item.mood}</span>` : '';
        const authorBadge = isFriend
            ? `<span class="bg-indigo-100 text-indigo-700 text-[9px] font-black px-2 py-0.5 rounded-full">${item.author || '친구'} 👤</span>`
            : `<span class="bg-brand-100 text-brand-700 text-[9px] font-black px-2 py-0.5 rounded-full">${item.author || '나'} 🧔</span>`;

        container.insertAdjacentHTML('beforeend', `
            <div id="diary-entry-${item.id}" class="relative z-10 flex flex-col items-start mb-8 last:mb-0">
                <div class="absolute -left-7 top-4 w-4 h-4 ${isFriend ? 'bg-indigo-400' : 'bg-brand-400'} border-4 border-white rounded-full shadow-sm"></div>
                <div class="bg-white p-3 rounded-2xl shadow-sm border ${isFriend ? 'border-indigo-100' : 'border-gray-100'} w-full hover:-translate-y-1 transition-transform">
                    <div class="flex justify-between items-center mb-3 px-1">
                        <div class="flex items-center gap-2">
                            ${authorBadge}
                            <span class="text-[10px] text-gray-400 font-bold">${item.dateStr || ''}</span>
                        </div>
                        <div class="flex gap-2 items-center">${moodBadge}</div>
                    </div>
                    <div class="relative w-full aspect-[4/3] rounded-xl overflow-hidden border border-gray-100 bg-gray-50 shadow-inner mb-3">
                        ${mediaMarkup}
                        <div class="absolute inset-0 pointer-events-none z-10 overflow-hidden">${stickersHtml}</div>
                    </div>
                    <div class="px-1 text-xs text-gray-700 leading-relaxed font-medium keep-all break-words mb-3">
                        ${item.text || '사진만 있는 기록입니다.'}
                    </div>
                    <div class="border-t border-gray-50 pt-2 flex justify-between items-center px-1">
                        ${!isFriend ? `<button onclick="quickPublishToFeedFromDiary(${idx}, false)" class="text-[10px] text-brand-600 hover:text-brand-800 font-black flex items-center gap-1"><i class="fa-solid fa-paper-plane"></i> 소셜 피드로 공유</button>` : '<span></span>'}
                        ${!isFriend ? `<button onclick="deleteAlbumItem(${idx})" class="text-[10px] text-red-400 hover:text-red-600 font-bold"><i class="fa-solid fa-trash-can"></i> 삭제</button>` : ''}
                    </div>
                </div>
            </div>
        `);
    });
}

function deleteAlbumItem(index) {
    showCustomDialog({
        title: "소장품 파기 ⚠️",
        message: "정말 보관함 내 소중한 펫 액티브 완성 앨범을 영구 소멸시키겠습니까?",
        icon: "🗑️",
        type: "confirm",
        onConfirm: () => {
            const deletedItem = albums[index];
            albums.splice(index, 1);
            selectedAlbumIndices = selectedAlbumIndices
                .filter(idx => idx !== index)
                .map(idx => idx > index ? idx - 1 : idx);
            saveState();
            renderAlbumGallery();
            
            if (deletedItem && typeof deleteAlbumFromSupabase === 'function') {
                deleteAlbumFromSupabase(deletedItem.id);
            }
            
            showToast("선택된 소장 앨범이 완벽히 파괴되었습니다.");
        }
    });
}

function publishToCommunityFeed() {
    if (albums.length === 0) {
        showToast("먼저 우측에서 앨범 작품을 디자인하여 소장 보관함에 추가해주셔야 합니다.");
        return;
    }

    const latest = albums[0];
    const currentPet = getActivePet();

    const post = {
        id: Date.now(),
        petName: currentPet ? currentPet.name : "댕이",
        petAvatar: currentPet ? (currentPet.type === 'custom' ? currentPet.imageUrl : "https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=150") : "https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=150",
        content: `이웃님들! 제가 액티브 스티커 데코방에서 심혈을 기울여 완성한 멋진 작품 어때요? 귀엽게 봐주세요! 🎨👑`,
        image: latest.isVideo ? "" : latest.url,
        isVideo: latest.isVideo,
        videoUrl: latest.isVideo ? latest.url : "",
        videoStart: latest.start || 0,
        videoEnd: latest.end || 5,
        likes: 0,
        liked: false,
        comments: []
    };

    posts.unshift(post);
    saveState();
    if (typeof uploadPostToSupabase === 'function') {
        try {
            uploadPostToSupabase(post);
        } catch (e) {
            console.error("Supabase post upload failed:", e);
        }
    }
    
    // 배포 완료 후 체크 해제 및 보관함 리스트 갱신
    selectedAlbumIndices = [];
    renderAlbumGallery();
    
    // 명시적인 업로드 완료 토스트 메시지 출력
    showToast("✓ 꾸민 앨범이 자랑 피드에 성공적으로 올라갔습니다! 📢");
    
    showCustomDialog({
        title: "자랑 피드 전송 완료! 🚀",
        message: "꾸민 앨범 완성작이 자랑 피드에 정상 배포되었습니다.\n지금 자랑 피드로 이동하여 확인해보시겠습니까?",
        icon: "🎉",
        type: "confirm",
        onConfirm: () => {
            switchTab('social');
            switchSocialSubTab('feed');
        }
    });
}

// 소장 갤러리에서 즉시 피드로 보내기 (toast만 표시, 다이얼로그 없음)
function quickPublishToFeedFromDiary(index, isMock) {
    if (isMock) {
        showToast("친구의 일기는 내 피드에 공유할 수 없습니다!");
        return;
    }
    const item = currentDiaryMode === 'me' ? albums[index] : [...albums, ...getSharedDiaryMocks()][index];
    if(!item) return;
    
    const realIndex = albums.findIndex(a => a.id === item.id);
    if(realIndex !== -1) {
        quickPublishToFeed(realIndex);
    }
}
function quickPublishToFeed(index) {
    const item = albums[index];
    if (!item) return;

    const currentPet = getActivePet();
    let content;
    if (item.isWalkCard && item.walkData) {
        const wd = item.walkData;
        content = `우리 ${currentPet ? currentPet.name : '댕이'}와 함께 완주한 ${wd.date} 산책 기록 공유해요! 🦮 ${wd.distance}km · ⏱️ ${wd.duration} · 🔥 ${wd.calories}kcal 이었어요! 같이 걸어요~ 🐾`;
    } else {
        content = `이웃님들! 제가 액티브 데코 룸에서 이쁘게 꾸민 우리 아이 앨범 카드입니다! 이쁘게 봐주세요 🎨💖`;
    }

    const post = {
        id: Date.now(),
        petName: currentPet ? currentPet.name : "댕이",
        petAvatar: currentPet ? (currentPet.type === 'custom' ? currentPet.imageUrl : "https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=150") : "https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=150",
        content: content,
        image: (item.isVideo || item.isWalkCard) ? "" : item.url,
        isVideo: item.isVideo || false,
        videoUrl: item.isVideo ? item.url : "",
        videoStart: item.start || 0,
        videoEnd: item.end || 10,
        likes: 0,
        liked: false,
        comments: [],
        filter: item.filter || 'natural',
        stickers: item.stickers || [],
        attachedWalk: item.isWalkCard ? item.walkData : null
    };

    posts.unshift(post);
    saveState();
    if (typeof uploadPostToSupabase === 'function') {
        try { uploadPostToSupabase(post); } catch(e) {}
    }
    if (typeof renderSocialRoom === 'function') renderSocialRoom();

    showCustomDialog({
        title: "피드 게시 성공! 📢",
        message: "피드에 성공적으로 게시되었습니다!\n지금 소셜 탭으로 이동하여 확인하시겠습니까?",
        icon: "🚀",
        type: "confirm",
        onConfirm: () => {
            switchTab('social');
            switchSocialSubTab('feed');
        }
    });
}

function publishAlbumItemToSocial(index) {
    quickPublishToFeed(index);
}

function selectAlbumItem(index) {
    const foundIdx = selectedAlbumIndices.indexOf(index);
    if (foundIdx > -1) {
        selectedAlbumIndices.splice(foundIdx, 1);
        showToast("앨범 카드 선택이 해제되었습니다.");
    } else {
        selectedAlbumIndices.push(index);
        showToast(`${index + 1}번째 앨범 카드가 추가 선택되었습니다. (총 ${selectedAlbumIndices.length}개 선택)`);
    }
    renderAlbumGallery();
}

function publishSelectedAlbumsToSocial() {
    if (selectedAlbumIndices.length === 0) {
        showToast("피드에 올릴 앨범 카드를 먼저 선택해 주세요.");
        return;
    }

    const sortedIndices = [...selectedAlbumIndices].sort((a, b) => a - b);
    const selectedItems = sortedIndices.map(idx => albums[idx]);
    
    if (selectedItems.length === 0) return;

    const currentPet = getActivePet();
    const primaryItem = selectedItems[0];

    const post = {
        id: Date.now(),
        petName: currentPet ? currentPet.name : "댕이",
        petAvatar: currentPet ? (currentPet.type === 'custom' ? currentPet.imageUrl : "https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=150") : "https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=150",
        content: `이웃님들! 제가 액티브 데코 룸에서 이쁘게 정성껏 꾸민 우리 아이 앨범 카드 ${selectedItems.length}장을 묶어서 자랑합니다! 이쁘게 봐주세요 🎨💖🐾`,
        image: primaryItem.isVideo ? "" : primaryItem.url,
        isVideo: primaryItem.isVideo,
        videoUrl: primaryItem.isVideo ? primaryItem.url : "",
        videoStart: primaryItem.start || 0,
        videoEnd: primaryItem.end || 10,
        likes: 0,
        liked: false,
        comments: [],
        filter: primaryItem.filter || 'natural',
        stickers: primaryItem.stickers || [],
        items: selectedItems.map(item => ({
            url: item.url,
            isVideo: item.isVideo,
            videoUrl: item.isVideo ? item.url : "",
            videoStart: item.start || 0,
            videoEnd: item.end || 10,
            filter: item.filter || 'natural',
            stickers: item.stickers || []
        }))
    };

    posts.unshift(post);
    saveState();
    if (typeof uploadPostToSupabase === 'function') {
        uploadPostToSupabase(post);
    }

    selectedAlbumIndices = [];
    renderAlbumGallery();

    // 명시적인 업로드 완료 토스트 메시지 출력
    showToast("✓ 선택한 앨범 카드들이 자랑 피드에 성공적으로 올라갔습니다! 📢");

    showCustomDialog({
        title: "자랑 피드 전송 완료! 🚀",
        message: "꾸민 앨범 카드들이 이웃 자랑 피드에 정상 업로드되었습니다.\n지금 자랑 피드로 이동하여 확인해보시겠습니까?",
        icon: "🎉",
        type: "confirm",
        onConfirm: () => {
            switchTab('social');
            switchSocialSubTab('feed');
        }
    });
}

// -----------------------------------------
// 📕 실물 포토북 출판 제어 로직
// -----------------------------------------
let photobookBasePrice = 18000;
let photobookCoverType = 'hard';

function openPhotobookPublishModal() {
    if (albums.length === 0) {
        showToast("출판할 일기가 없습니다. 일기를 작성해 주세요!");
        return;
    }

    const modal = document.getElementById('photobook-publish-modal');
    if (modal) {
        modal.classList.remove('hidden');
        modal.classList.add('flex');
        
        // 커버 이미지 및 정보 채우기
        const latest = albums[0];
        const previewCover = document.getElementById('photobook-preview-cover');
        if (previewCover && latest) {
            previewCover.src = latest.url;
        }

        const authorPreview = document.getElementById('photobook-preview-author');
        if (authorPreview) {
            authorPreview.innerText = (typeof settings_nickname !== 'undefined' ? settings_nickname : '집사') + "의 일기장";
        }

        const pageCountEl = document.getElementById('photobook-page-count');
        if (pageCountEl) {
            pageCountEl.innerText = `${albums.length}장 (${albums.length * 2}페이지)`;
        }

        // 기본 수령자 정보 자동완성
        const recipientInput = document.getElementById('photobook-recipient');
        if (recipientInput) {
            recipientInput.value = typeof settings_nickname !== 'undefined' ? settings_nickname : '';
        }

        updatePhotobookPreview();
        calculatePhotobookPrice();
    }
}

function closePhotobookPublishModal() {
    const modal = document.getElementById('photobook-publish-modal');
    if (modal) {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }
}

function updatePhotobookPreview() {
    const titleInput = document.getElementById('photobook-title-input');
    const previewTitle = document.getElementById('photobook-preview-title');
    if (titleInput && previewTitle) {
        previewTitle.innerText = titleInput.value.trim() || '우리의 소중한 추억';
    }
}

function selectPhotobookOption(type, value, basePrice, btn) {
    if (type === 'cover') {
        photobookCoverType = value;
        photobookBasePrice = basePrice;
        
        // 버튼 하이라이트
        document.querySelectorAll('.photobook-opt-cover').forEach(b => {
            b.className = "photobook-opt-cover py-3 rounded-2xl border-2 border-transparent bg-gray-50 font-bold hover:bg-amber-50/20 text-center flex flex-col items-center justify-center gap-1 transition-all outline-none";
            const textSpan = b.querySelector('span:nth-child(2)');
            if (textSpan) textSpan.className = '';
        });
        
        if (btn) {
            btn.className = "photobook-opt-cover py-3 rounded-2xl border-2 border-brand-500 bg-brand-50/50 font-bold text-center flex flex-col items-center justify-center gap-1 transition-all outline-none";
            const textSpan = btn.querySelector('span:nth-child(2)');
            if (textSpan) textSpan.className = 'text-brand-700';
        }

        const coverText = document.getElementById('photobook-selected-cover');
        if (coverText) {
            coverText.innerText = value === 'soft' ? '소프트커버' : value === 'hard' ? '하드커버' : '레더 명품커버';
        }
    }
    calculatePhotobookPrice();
}

function calculatePhotobookPrice() {
    let finalPrice = photobookBasePrice;

    // 내지 타입에 따른 추가 금액
    const paper = document.getElementById('photobook-paper-select')?.value;
    if (paper === 'glossy') finalPrice += 3000;
    if (paper === 'eco') finalPrice += 1500;

    // 인쇄 사이즈에 따른 추가 금액
    const size = document.getElementById('photobook-size-select')?.value;
    if (size === 'a5') finalPrice += 2000;
    if (size === 'a4') finalPrice += 7000;

    // 일기장 장수가 10장을 초과하는 경우 장당 500원씩 추가
    if (albums.length > 10) {
        finalPrice += (albums.length - 10) * 500;
    }

    const priceEl = document.getElementById('photobook-final-price');
    if (priceEl) {
        priceEl.innerText = finalPrice.toLocaleString() + "원";
    }
}

function confirmPhotobookPublish() {
    const recipient = document.getElementById('photobook-recipient')?.value.trim();
    const phone = document.getElementById('photobook-phone')?.value.trim();
    const address = document.getElementById('photobook-address')?.value.trim();
    const title = document.getElementById('photobook-title-input')?.value.trim() || '우리의 소중한 추억';

    if (!recipient || !phone || !address) {
        showToast("🚚 배송 정보(이름, 연락처, 주소)를 모두 입력해 주세요.");
        return;
    }

    closePhotobookPublishModal();

    showCustomDialog({
        title: "출판 주문 접수 성공! 🎉📕",
        message: `소중한 [${title}] 포토북 출판 주문이 성공적으로 접수되었습니다!\n인쇄 제작 및 배송에 영업일 기준 약 3~5일이 소요됩니다.`,
        icon: "✨",
        type: "alert",
        onConfirm: () => {
            showToast("🎁 세상에 하나뿐인 책이 집사님 품으로 곧 찾아갑니다!");
        }
    });
}

