// symptom-journal.js — 증상 사진 일지 (부위별 증상 사진을 날짜순 누적)
// 피부·눈·귀 등 부위별로 증상 사진을 시간순 기록해 병원 방문·수의사 상담 시 경과 확인용.
// 저장: localStorage(펫·이메일 스코프) — medical-records.js 패턴 준수, 별도 스키마 없음.

window.symptomJournal = window.symptomJournal || [];

// dot/pill은 정적 Tailwind 클래스로 명시 — 런타임 `bg-${color}-400` 조합은 Play CDN JIT 밖.
const SYMPTOM_BODY_PARTS = {
    skin:  { label: '피부/털', icon: '🐾', dot: 'bg-amber-400',   pill: 'bg-amber-50 text-amber-700' },
    eyes:  { label: '눈',     icon: '👁️', dot: 'bg-sky-400',     pill: 'bg-sky-50 text-sky-700' },
    ears:  { label: '귀',     icon: '👂', dot: 'bg-violet-400',  pill: 'bg-violet-50 text-violet-700' },
    mouth: { label: '입/치아', icon: '🦷', dot: 'bg-rose-400',    pill: 'bg-rose-50 text-rose-700' },
    paws:  { label: '발/발톱', icon: '🐾', dot: 'bg-emerald-400', pill: 'bg-emerald-50 text-emerald-700' },
    other: { label: '기타',   icon: '📍', dot: 'bg-stone-400',   pill: 'bg-stone-100 text-stone-600' }
};

function _symptomEmail() {
    return (typeof settings_email !== 'undefined' && settings_email)
        || localStorage.getItem('petna_user_email')
        || 'butler@petna.co.kr';
}

function _symptomEsc(s) {
    return String(s == null ? '' : s)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

function loadSymptomJournal() {
    try {
        const raw = localStorage.getItem('petna_symptom_journal_' + _symptomEmail());
        window.symptomJournal = raw ? JSON.parse(raw) : [];
    } catch (e) {
        window.symptomJournal = [];
    }
    return window.symptomJournal;
}

function saveSymptomJournalLocal() {
    try {
        localStorage.setItem('petna_symptom_journal_' + _symptomEmail(), JSON.stringify(window.symptomJournal));
    } catch (e) { /* quota — 무시 */ }
}

function getSymptomEntriesForActivePet() {
    const pet = (typeof getActivePet === 'function') ? getActivePet() : null;
    const list = Array.isArray(window.symptomJournal) ? window.symptomJournal : [];
    const filtered = pet ? list.filter(r => String(r.petId) === String(pet.id)) : list;
    return filtered.slice().sort((a, b) => (b.date || '').localeCompare(a.date || ''));
}

function renderSymptomJournal() {
    const container = document.getElementById('symptom-journal-timeline');
    if (!container) return;

    const entries = getSymptomEntriesForActivePet();

    if (entries.length === 0) {
        container.innerHTML = `
            <div class="text-center py-8 px-4">
                <div class="w-14 h-14 mx-auto rounded-2xl bg-brand-50 flex items-center justify-center text-2xl mb-3">📸</div>
                <p class="text-sm font-bold text-gray-700">아직 증상 사진이 없어요</p>
                <p class="text-xs text-gray-400 mt-1 keep-all leading-relaxed">피부·눈·귀 증상 사진을 날짜순으로 기록해<br>병원 방문 시 경과를 보여주세요</p>
            </div>`;
        return;
    }

    // 부위별로 묶어 각 부위의 사진을 날짜순(오래된→최근)으로 나열 — 경과가 한눈에 보이게.
    const byPart = {};
    entries.forEach(e => {
        const key = SYMPTOM_BODY_PARTS[e.bodyPart] ? e.bodyPart : 'other';
        (byPart[key] = byPart[key] || []).push(e);
    });

    const groups = Object.keys(SYMPTOM_BODY_PARTS)
        .filter(k => byPart[k] && byPart[k].length > 0)
        .map(k => {
            const part = SYMPTOM_BODY_PARTS[k];
            const chron = byPart[k].slice().sort((a, b) => (a.date || '').localeCompare(b.date || ''));
            const thumbs = chron.map(e => {
                const photo = e.photo
                    ? `<img loading="lazy" src="${_symptomEsc(e.photo)}" onclick="window.open('${_symptomEsc(e.photo)}','_blank')" class="w-16 h-16 rounded-xl object-cover border border-gray-200 cursor-pointer hover:opacity-90 transition-opacity" alt="증상 사진">`
                    : `<div class="w-16 h-16 rounded-xl bg-gray-50 border border-gray-200 flex items-center justify-center text-gray-300 text-xl">📷</div>`;
                return `
                    <div class="shrink-0 text-center">
                        ${photo}
                        <div class="text-[9px] font-bold text-gray-400 tabular-nums mt-1">${_symptomEsc(e.date || '')}</div>
                        ${e.note ? `<div class="text-[9px] text-gray-500 max-w-[64px] truncate mt-0.5">${_symptomEsc(e.note)}</div>` : ''}
                        <button onclick="deleteSymptomEntry(${e.id})" class="text-[9px] font-bold text-gray-300 hover:text-rose-500 transition-colors mt-0.5">삭제</button>
                    </div>`;
            }).join('');
            return `
                <div class="rounded-2xl bg-white border border-gray-100 p-3.5 shadow-soft">
                    <div class="flex items-center gap-1.5 mb-2">
                        <span class="inline-flex items-center gap-1 ${part.pill} text-[10px] font-black px-2 py-0.5 rounded-lg">${part.icon} ${part.label}</span>
                        <span class="text-[10px] font-bold text-gray-400 tabular-nums">${chron.length}장</span>
                    </div>
                    <div class="flex items-start gap-2.5 overflow-x-auto pb-1">${thumbs}</div>
                </div>`;
        }).join('');

    container.innerHTML = `<div class="space-y-2.5">${groups}</div>`;
}
window.renderSymptomJournal = renderSymptomJournal;

// ===== 추가 모달 =====
let _symptomPhotoData = null;

function _ensureSymptomModal() {
    if (document.getElementById('symptom-journal-modal')) return;
    const options = Object.entries(SYMPTOM_BODY_PARTS)
        .map(([k, v]) => `<option value="${k}">${v.icon} ${v.label}</option>`).join('');
    const wrap = document.createElement('div');
    wrap.innerHTML = `
    <div id="symptom-journal-modal" class="fixed inset-0 bg-black/60 backdrop-blur-sm items-center justify-center z-[100] p-4 hidden">
        <div class="bg-white rounded-3xl max-w-md w-full max-h-[90vh] overflow-y-auto shadow-2xl">
            <div class="sticky top-0 bg-gradient-to-r from-brand-500 to-brand-600 px-5 py-4 rounded-t-3xl flex items-center justify-between">
                <h2 class="text-base font-bold text-white"><i class="fa-solid fa-camera mr-1.5"></i>증상 사진 기록</h2>
                <button onclick="closeSymptomModal()" class="text-white/80 hover:text-white"><i class="fa-solid fa-xmark text-lg"></i></button>
            </div>
            <div class="p-5 space-y-3">
                <div class="grid grid-cols-2 gap-3">
                    <div>
                        <label class="block text-[11px] font-bold text-gray-500 mb-1">날짜</label>
                        <input type="date" id="symptom-date" class="w-full border rounded-lg px-2.5 py-1.5 outline-none focus:border-brand-400 text-xs">
                    </div>
                    <div>
                        <label class="block text-[11px] font-bold text-gray-500 mb-1">부위</label>
                        <select id="symptom-body-part" class="w-full border rounded-lg px-2.5 py-1.5 outline-none focus:border-brand-400 text-xs bg-white">${options}</select>
                    </div>
                </div>
                <div>
                    <label class="block text-[11px] font-bold text-gray-500 mb-1">메모</label>
                    <input type="text" id="symptom-note" placeholder="예: 붉은 반점, 긁는 정도" class="w-full border rounded-lg px-2.5 py-1.5 outline-none focus:border-brand-400 text-xs">
                </div>
                <div>
                    <label class="block text-[11px] font-bold text-gray-500 mb-1">증상 사진</label>
                    <div class="flex items-center gap-2">
                        <button type="button" onclick="document.getElementById('symptom-photo-input').click()" class="px-3 py-1.5 bg-gray-50 border border-gray-200 rounded-lg text-xs font-bold text-gray-600 hover:bg-brand-50 hover:border-brand-200 transition-colors">
                            <i class="fa-solid fa-camera mr-1"></i>사진 첨부
                        </button>
                        <img id="symptom-photo-preview" class="hidden w-12 h-12 rounded-lg object-cover border border-gray-200">
                        <button type="button" id="symptom-photo-clear" onclick="clearSymptomPhoto()" class="hidden text-[10px] font-bold text-gray-400 hover:text-rose-500">제거</button>
                    </div>
                    <input type="file" id="symptom-photo-input" accept="image/*" class="hidden" onchange="handleSymptomPhotoUpload(event)">
                </div>
                <button onclick="saveSymptomEntry()" class="w-full bg-brand-500 hover:bg-brand-600 text-white font-bold text-sm py-3 rounded-2xl transition-all shadow-soft">저장</button>
            </div>
        </div>
    </div>`;
    document.body.appendChild(wrap.firstElementChild);
}

function openSymptomModal() {
    if (typeof getActivePet === 'function' && !getActivePet()) {
        if (typeof showToast === 'function') showToast('⚠️ 먼저 반려동물을 등록해주세요.');
        return;
    }
    _ensureSymptomModal();
    _symptomPhotoData = null;
    document.getElementById('symptom-date').value = new Date().toISOString().slice(0, 10);
    document.getElementById('symptom-body-part').value = 'skin';
    document.getElementById('symptom-note').value = '';
    document.getElementById('symptom-photo-preview').classList.add('hidden');
    document.getElementById('symptom-photo-clear').classList.add('hidden');
    const modal = document.getElementById('symptom-journal-modal');
    modal.classList.remove('hidden');
    modal.classList.add('flex');
}
window.openSymptomModal = openSymptomModal;

function closeSymptomModal() {
    const modal = document.getElementById('symptom-journal-modal');
    if (modal) { modal.classList.add('hidden'); modal.classList.remove('flex'); }
}
window.closeSymptomModal = closeSymptomModal;

function handleSymptomPhotoUpload(event) {
    const file = event.target.files && event.target.files[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = (e) => {
        _symptomPhotoData = e.target.result;
        const preview = document.getElementById('symptom-photo-preview');
        const clearBtn = document.getElementById('symptom-photo-clear');
        preview.src = _symptomPhotoData;
        preview.classList.remove('hidden');
        clearBtn.classList.remove('hidden');
    };
    reader.readAsDataURL(file);
}
window.handleSymptomPhotoUpload = handleSymptomPhotoUpload;

function clearSymptomPhoto() {
    _symptomPhotoData = null;
    const preview = document.getElementById('symptom-photo-preview');
    const clearBtn = document.getElementById('symptom-photo-clear');
    const input = document.getElementById('symptom-photo-input');
    if (preview) preview.classList.add('hidden');
    if (clearBtn) clearBtn.classList.add('hidden');
    if (input) input.value = '';
}
window.clearSymptomPhoto = clearSymptomPhoto;

function saveSymptomEntry() {
    const pet = (typeof getActivePet === 'function') ? getActivePet() : null;
    const date = document.getElementById('symptom-date').value;
    if (!date) {
        if (typeof showToast === 'function') showToast('⚠️ 날짜를 입력해주세요.');
        return;
    }
    if (!_symptomPhotoData) {
        if (typeof showToast === 'function') showToast('⚠️ 증상 사진을 첨부해주세요.');
        return;
    }
    const entry = {
        id: Date.now(),
        petId: pet ? pet.id : null,
        email: _symptomEmail(),
        date,
        bodyPart: document.getElementById('symptom-body-part').value || 'other',
        note: document.getElementById('symptom-note').value.trim(),
        photo: _symptomPhotoData,
        createdAt: new Date().toISOString()
    };
    if (!Array.isArray(window.symptomJournal)) window.symptomJournal = [];
    window.symptomJournal.push(entry);
    saveSymptomJournalLocal();
    renderSymptomJournal();
    closeSymptomModal();
    if (typeof showToast === 'function') showToast('📸 증상 사진을 기록했어요.');
}
window.saveSymptomEntry = saveSymptomEntry;

function deleteSymptomEntry(entryId) {
    const doDelete = () => {
        window.symptomJournal = (window.symptomJournal || []).filter(r => String(r.id) !== String(entryId));
        saveSymptomJournalLocal();
        renderSymptomJournal();
    };
    if (typeof showCustomDialog === 'function') {
        showCustomDialog({ title: '사진 삭제', message: '이 증상 사진을 삭제할까요?', icon: '🗑️', type: 'confirm', onConfirm: doDelete });
    } else {
        doDelete();
    }
}
window.deleteSymptomEntry = deleteSymptomEntry;

window.addEventListener('DOMContentLoaded', () => {
    loadSymptomJournal();
});
