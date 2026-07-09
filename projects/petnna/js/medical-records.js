// medical-records.js — 반려 건강수첩 (병원 방문·진료비·서류 아카이브)
// care-scheduler(예정 일정)와 분리된 '지나간 의료 이력' 저장소.
// 저장: localStorage 우선 + Supabase(medical_records) 동기화 — albums 패턴 준수.

window.medicalRecords = window.medicalRecords || [];

// dot/pill은 정적 Tailwind 클래스로 명시 — 런타임 `bg-${color}-400` 조합은 Play CDN이
// 생성하지 못해 색이 사라진다(빌드타임 JIT 대상 밖). 명시 클래스만 안전.
const MEDICAL_CATEGORIES = {
    visit:     { label: '진료/방문', icon: '🏥', dot: 'bg-rose-400',    pill: 'bg-rose-50 text-rose-700' },
    vaccine:   { label: '예방접종', icon: '💉', dot: 'bg-emerald-400', pill: 'bg-emerald-50 text-emerald-700' },
    checkup:   { label: '정기검진', icon: '🩺', dot: 'bg-sky-400',     pill: 'bg-sky-50 text-sky-700' },
    surgery:   { label: '수술',     icon: '🔪', dot: 'bg-amber-400',   pill: 'bg-amber-50 text-amber-700' },
    medication:{ label: '투약/처방', icon: '💊', dot: 'bg-violet-400',  pill: 'bg-violet-50 text-violet-700' },
    other:     { label: '기타',     icon: '📋', dot: 'bg-stone-400',   pill: 'bg-stone-100 text-stone-600' }
};

function _medicalEmail() {
    return (typeof settings_email !== 'undefined' && settings_email)
        || localStorage.getItem('petna_user_email')
        || 'butler@petna.co.kr';
}

function loadMedicalRecords() {
    try {
        const raw = localStorage.getItem('petna_medical_records_' + _medicalEmail());
        window.medicalRecords = raw ? JSON.parse(raw) : [];
    } catch (e) {
        window.medicalRecords = [];
    }
    return window.medicalRecords;
}

function saveMedicalRecordsLocal() {
    try {
        localStorage.setItem('petna_medical_records_' + _medicalEmail(), JSON.stringify(window.medicalRecords));
    } catch (e) { /* quota — 무시 */ }
}

function getMedicalRecordsForActivePet() {
    const pet = (typeof getActivePet === 'function') ? getActivePet() : null;
    const list = Array.isArray(window.medicalRecords) ? window.medicalRecords : [];
    const filtered = pet ? list.filter(r => String(r.petId) === String(pet.id)) : list;
    return filtered.slice().sort((a, b) => (b.visitDate || '').localeCompare(a.visitDate || ''));
}

// 월간 병원비 요약 (monthly-report-modal.js에서 사용) — yyyymm = 'YYYY-MM'
function getMonthlyHospitalSummary(yyyymm) {
    const pet = (typeof getActivePet === 'function') ? getActivePet() : null;
    const list = Array.isArray(window.medicalRecords) ? window.medicalRecords : [];
    const rows = list.filter(r =>
        (!pet || String(r.petId) === String(pet.id)) &&
        typeof r.visitDate === 'string' && r.visitDate.startsWith(yyyymm)
    );
    const totalCost = rows.reduce((sum, r) => sum + (parseFloat(r.cost) || 0), 0);
    return { count: rows.length, totalCost };
}

function renderMedicalRecordsTimeline() {
    const container = document.getElementById('medical-records-timeline');
    if (!container) return;

    const records = getMedicalRecordsForActivePet();

    if (records.length === 0) {
        container.innerHTML = `
            <div class="text-center py-8 px-4">
                <div class="w-14 h-14 mx-auto rounded-2xl bg-brand-50 flex items-center justify-center text-2xl mb-3">🏥</div>
                <p class="text-sm font-bold text-gray-700">아직 건강 기록이 없어요</p>
                <p class="text-xs text-gray-400 mt-1 keep-all leading-relaxed">병원 방문·예방접종·진료비를 기록해<br>우리 아이 건강 히스토리를 만들어보세요</p>
            </div>`;
        return;
    }

    // 누적 요약 — 코랄 스탯 카드(브랜드 앵커)
    const totalCost = records.reduce((s, r) => s + (parseFloat(r.cost) || 0), 0);
    const summaryHtml = `
        <div class="flex items-stretch gap-2 mb-3">
            <div class="flex-1 rounded-2xl bg-brand-50/70 border border-brand-100 px-3.5 py-2.5">
                <div class="text-[10px] font-bold text-brand-600 uppercase tracking-wide">누적 진료비</div>
                <div class="text-lg font-black text-brand-800 tabular-nums leading-tight">${totalCost.toLocaleString('ko-KR')}<span class="text-xs font-bold text-brand-600 ml-0.5">원</span></div>
            </div>
            <div class="rounded-2xl bg-brand-50/70 border border-brand-100 px-3.5 py-2.5 text-right">
                <div class="text-[10px] font-bold text-brand-600 uppercase tracking-wide">기록</div>
                <div class="text-lg font-black text-brand-800 tabular-nums leading-tight">${records.length}<span class="text-xs font-bold text-brand-600 ml-0.5">건</span></div>
            </div>
        </div>`;

    // 백신 접종 이력 자동 정리
    const vaccines = records.filter(r => r.category === 'vaccine');
    let vaccineHtml = '';
    if (vaccines.length > 0) {
        const items = vaccines.slice(0, 4).map(v =>
            `<span class="inline-flex items-center gap-1 bg-white text-emerald-700 border border-emerald-200/70 text-[10px] font-bold px-2 py-0.5 rounded-full">💉 ${_esc(v.diagnosis || v.hospital || '접종')} · ${v.visitDate || ''}</span>`
        ).join('');
        vaccineHtml = `
            <div class="mb-3 p-3 bg-emerald-50/50 border border-emerald-100 rounded-2xl">
                <div class="text-[10px] font-black text-emerald-700 mb-1.5 uppercase tracking-wide">💉 예방접종 이력 · ${vaccines.length}건</div>
                <div class="flex flex-wrap gap-1.5">${items}</div>
            </div>`;
    }

    const cards = records.map(r => {
        const cat = MEDICAL_CATEGORIES[r.category] || MEDICAL_CATEGORIES.other;
        const cost = (parseFloat(r.cost) || 0);
        const costHtml = cost > 0
            ? `<span class="inline-flex items-center bg-brand-50 text-brand-700 text-xs font-black px-2 py-0.5 rounded-lg tabular-nums">${cost.toLocaleString('ko-KR')}원</span>`
            : '';
        const photoHtml = r.photo
            ? `<img loading="lazy" src="${_esc(r.photo)}" onclick="window.open('${_esc(r.photo)}','_blank')" class="w-14 h-14 rounded-xl object-cover border border-gray-200 cursor-pointer shrink-0 hover:opacity-90 transition-opacity" alt="검사지/영수증">`
            : '';
        const notesHtml = r.notes
            ? `<p class="text-[11px] text-gray-500 mt-1 keep-all leading-snug">${_esc(r.notes)}</p>`
            : '';
        return `
            <div class="relative pl-5 pb-3.5 border-l-2 border-brand-100 last:border-transparent last:pb-0">
                <span class="absolute -left-[7px] top-1.5 w-3 h-3 rounded-full ${cat.dot} ring-2 ring-white"></span>
                <div class="rounded-2xl bg-white border border-gray-100 p-3.5 shadow-soft">
                    <div class="flex items-start justify-between gap-2.5">
                        <div class="flex-1 min-w-0">
                            <div class="flex items-center gap-1.5 flex-wrap">
                                <span class="inline-flex items-center gap-1 ${cat.pill} text-[10px] font-black px-2 py-0.5 rounded-lg">${cat.icon} ${cat.label}</span>
                                <span class="text-[11px] font-bold text-gray-400 tabular-nums">${_esc(r.visitDate || '')}</span>
                            </div>
                            <div class="mt-2 text-sm font-black text-gray-800 keep-all leading-tight">${_esc(r.hospital || '병원 미기재')}</div>
                            ${r.diagnosis ? `<div class="text-xs text-gray-600 mt-0.5 keep-all">${_esc(r.diagnosis)}</div>` : ''}
                            ${notesHtml}
                            <div class="mt-2 flex items-center gap-2.5">
                                ${costHtml}
                                <button onclick="openMedicalRecordModal(${r.id})" class="text-[11px] font-bold text-gray-400 hover:text-brand-600 transition-colors">수정</button>
                                <button onclick="deleteMedicalRecord(${r.id})" class="text-[11px] font-bold text-gray-400 hover:text-rose-500 transition-colors">삭제</button>
                            </div>
                        </div>
                        ${photoHtml}
                    </div>
                </div>
            </div>`;
    }).join('');

    container.innerHTML = summaryHtml + vaccineHtml + `<div class="mt-1">${cards}</div>`;
}

function _esc(s) {
    return String(s == null ? '' : s)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

// ===== 기록 추가/수정 모달 =====
let _medicalPhotoData = null;   // 현재 편집 중 사진 dataURL/URL
let _editingMedicalId = null;

function _ensureMedicalModal() {
    if (document.getElementById('medical-record-modal')) return;
    const options = Object.entries(MEDICAL_CATEGORIES)
        .map(([k, v]) => `<option value="${k}">${v.icon} ${v.label}</option>`).join('');
    const wrap = document.createElement('div');
    wrap.innerHTML = `
    <div id="medical-record-modal" class="fixed inset-0 bg-black/60 backdrop-blur-sm items-center justify-center z-[100] p-4 hidden">
        <div class="bg-white rounded-3xl max-w-md w-full max-h-[90vh] overflow-y-auto shadow-2xl">
            <div class="sticky top-0 bg-gradient-to-r from-brand-500 to-brand-600 px-5 py-4 rounded-t-3xl flex items-center justify-between">
                <h2 class="text-base font-bold text-white"><i class="fa-solid fa-notes-medical mr-1.5"></i>건강수첩 기록</h2>
                <button onclick="closeMedicalRecordModal()" class="text-white/80 hover:text-white"><i class="fa-solid fa-xmark text-lg"></i></button>
            </div>
            <div class="p-5 space-y-3">
                <div class="grid grid-cols-2 gap-3">
                    <div>
                        <label class="block text-[11px] font-bold text-gray-500 mb-1">방문일</label>
                        <input type="date" id="medical-visit-date" class="w-full border rounded-lg px-2.5 py-1.5 outline-none focus:border-brand-400 text-xs">
                    </div>
                    <div>
                        <label class="block text-[11px] font-bold text-gray-500 mb-1">구분</label>
                        <select id="medical-category" class="w-full border rounded-lg px-2.5 py-1.5 outline-none focus:border-brand-400 text-xs bg-white">${options}</select>
                    </div>
                </div>
                <div>
                    <label class="block text-[11px] font-bold text-gray-500 mb-1">병원명</label>
                    <input type="text" id="medical-hospital" placeholder="○○동물병원" class="w-full border rounded-lg px-2.5 py-1.5 outline-none focus:border-brand-400 text-xs">
                </div>
                <div>
                    <label class="block text-[11px] font-bold text-gray-500 mb-1">진단/처방</label>
                    <input type="text" id="medical-diagnosis" placeholder="예: 종합백신 3차 / 외이염" class="w-full border rounded-lg px-2.5 py-1.5 outline-none focus:border-brand-400 text-xs">
                </div>
                <div>
                    <label class="block text-[11px] font-bold text-gray-500 mb-1">진료비 (원)</label>
                    <input type="number" id="medical-cost" placeholder="0" min="0" class="w-full border rounded-lg px-2.5 py-1.5 outline-none focus:border-brand-400 text-xs">
                </div>
                <div>
                    <label class="block text-[11px] font-bold text-gray-500 mb-1">메모</label>
                    <textarea id="medical-notes" rows="2" placeholder="특이사항, 다음 방문 안내 등" class="w-full border rounded-lg px-2.5 py-1.5 outline-none focus:border-brand-400 text-xs resize-none"></textarea>
                </div>
                <div>
                    <label class="block text-[11px] font-bold text-gray-500 mb-1">영수증/검사지 사진</label>
                    <div class="flex items-center gap-2">
                        <button type="button" onclick="document.getElementById('medical-photo-input').click()" class="px-3 py-1.5 bg-gray-50 border border-gray-200 rounded-lg text-xs font-bold text-gray-600 hover:bg-brand-50 hover:border-brand-200 transition-colors">
                            <i class="fa-solid fa-camera mr-1"></i>사진 첨부
                        </button>
                        <img id="medical-photo-preview" class="hidden w-12 h-12 rounded-lg object-cover border border-gray-200">
                        <button type="button" id="medical-photo-clear" onclick="clearMedicalPhoto()" class="hidden text-[10px] font-bold text-gray-400 hover:text-rose-500">제거</button>
                    </div>
                    <input type="file" id="medical-photo-input" accept="image/*" class="hidden" onchange="handleMedicalPhotoUpload(event)">
                </div>
                <button onclick="saveMedicalRecord()" class="w-full bg-brand-500 hover:bg-brand-600 text-white font-bold text-sm py-3 rounded-2xl transition-all shadow-soft">저장</button>
            </div>
        </div>
    </div>`;
    document.body.appendChild(wrap.firstElementChild);
}

function openMedicalRecordModal(recordId = null) {
    if (typeof getActivePet === 'function' && !getActivePet()) {
        if (typeof showToast === 'function') showToast('⚠️ 먼저 반려동물을 등록해주세요.');
        return;
    }
    _ensureMedicalModal();
    _medicalPhotoData = null;
    _editingMedicalId = recordId;

    const rec = recordId ? (window.medicalRecords || []).find(r => String(r.id) === String(recordId)) : null;
    document.getElementById('medical-visit-date').value = rec?.visitDate || new Date().toISOString().slice(0, 10);
    document.getElementById('medical-category').value = rec?.category || 'visit';
    document.getElementById('medical-hospital').value = rec?.hospital || '';
    document.getElementById('medical-diagnosis').value = rec?.diagnosis || '';
    document.getElementById('medical-cost').value = rec?.cost || '';
    document.getElementById('medical-notes').value = rec?.notes || '';

    const preview = document.getElementById('medical-photo-preview');
    const clearBtn = document.getElementById('medical-photo-clear');
    if (rec?.photo) {
        _medicalPhotoData = rec.photo;
        preview.src = rec.photo;
        preview.classList.remove('hidden');
        clearBtn.classList.remove('hidden');
    } else {
        preview.classList.add('hidden');
        clearBtn.classList.add('hidden');
    }

    const modal = document.getElementById('medical-record-modal');
    modal.classList.remove('hidden');
    modal.classList.add('flex');
}

function closeMedicalRecordModal() {
    const modal = document.getElementById('medical-record-modal');
    if (modal) { modal.classList.add('hidden'); modal.classList.remove('flex'); }
}

function handleMedicalPhotoUpload(event) {
    const file = event.target.files && event.target.files[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = (e) => {
        _medicalPhotoData = e.target.result;
        const preview = document.getElementById('medical-photo-preview');
        const clearBtn = document.getElementById('medical-photo-clear');
        preview.src = _medicalPhotoData;
        preview.classList.remove('hidden');
        clearBtn.classList.remove('hidden');
    };
    reader.readAsDataURL(file);
}

function clearMedicalPhoto() {
    _medicalPhotoData = null;
    const preview = document.getElementById('medical-photo-preview');
    const clearBtn = document.getElementById('medical-photo-clear');
    const input = document.getElementById('medical-photo-input');
    if (preview) preview.classList.add('hidden');
    if (clearBtn) clearBtn.classList.add('hidden');
    if (input) input.value = '';
}

function saveMedicalRecord() {
    const pet = (typeof getActivePet === 'function') ? getActivePet() : null;
    const visitDate = document.getElementById('medical-visit-date').value;
    if (!visitDate) {
        if (typeof showToast === 'function') showToast('⚠️ 방문일을 입력해주세요.');
        return;
    }
    const record = {
        id: _editingMedicalId || Date.now(),
        petId: pet ? pet.id : null,
        email: _medicalEmail(),
        visitDate,
        category: document.getElementById('medical-category').value || 'visit',
        hospital: document.getElementById('medical-hospital').value.trim(),
        diagnosis: document.getElementById('medical-diagnosis').value.trim(),
        cost: parseFloat(document.getElementById('medical-cost').value) || 0,
        notes: document.getElementById('medical-notes').value.trim(),
        photo: _medicalPhotoData || '',
        createdAt: new Date().toISOString()
    };

    if (!Array.isArray(window.medicalRecords)) window.medicalRecords = [];
    const idx = window.medicalRecords.findIndex(r => String(r.id) === String(record.id));
    if (idx >= 0) window.medicalRecords[idx] = record;
    else window.medicalRecords.push(record);

    saveMedicalRecordsLocal();
    renderMedicalRecordsTimeline();
    closeMedicalRecordModal();

    if (typeof window.uploadMedicalRecordToSupabase === 'function') {
        // 업로드 시 record.photo가 storage URL로 치환되면 로컬도 갱신
        Promise.resolve(window.uploadMedicalRecordToSupabase(record)).then(() => {
            saveMedicalRecordsLocal();
        });
    }
    if (typeof showToast === 'function') showToast('🏥 건강수첩에 기록했어요.');
}

function deleteMedicalRecord(recordId) {
    const doDelete = () => {
        window.medicalRecords = (window.medicalRecords || []).filter(r => String(r.id) !== String(recordId));
        saveMedicalRecordsLocal();
        renderMedicalRecordsTimeline();
        if (typeof window.deleteMedicalRecordFromSupabase === 'function') {
            window.deleteMedicalRecordFromSupabase(recordId);
        }
    };
    if (typeof showCustomDialog === 'function') {
        showCustomDialog({
            title: '기록 삭제',
            message: '이 의료 기록을 삭제할까요?',
            icon: '🗑️',
            type: 'confirm',
            onConfirm: doDelete
        });
    } else {
        doDelete();
    }
}

// 초기 로드 (Supabase 동기화 전 localStorage 즉시 반영)
window.addEventListener('DOMContentLoaded', () => {
    loadMedicalRecords();
});
