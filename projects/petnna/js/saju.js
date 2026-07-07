let calculatedSajuData = null;

let savedPetIqScore = 0;
let savedOwnerIqScore = 0;
let savedPetMbti = '';
let savedOwnerMbti = '';
let savedSajuScore = 0;

function loadSajuVariables() {
    const current = getSajuPet();
    if (current) {
        savedPetIqScore = current.iqScore ? Math.round(current.iqScore / 1.5) : 0;
        savedPetMbti = current.mbtiCode || '';
        if (current.sajuData) {
            savedSajuScore = current.sajuData.compatScore || 0;
        } else {
            savedSajuScore = 0;
        }
    } else {
        savedPetIqScore = 0;
        savedPetMbti = '';
        savedSajuScore = 0;
    }
    
    const email = (typeof settings_email !== 'undefined') ? settings_email : 'butler@petna.co.kr';
    savedOwnerIqScore = parseInt(localStorage.getItem(`petna_owner_iq_${email}`) || "0");
    savedOwnerMbti = localStorage.getItem(`petna_owner_mbti_${email}`) || "";
}



let currentIqTarget = 'pet';
let currentMbtiTarget = 'pet';

function switchIqMode(mode) {
    currentIqTarget = mode;
    const btnPet = document.getElementById('iq-mode-pet');
    const btnOwner = document.getElementById('iq-mode-owner');
    
    if (mode === 'pet') {
        if(btnPet) btnPet.className = 'flex-1 bg-white text-sky-600 font-bold text-xs py-2 rounded-lg shadow-sm';
        if(btnOwner) btnOwner.className = 'flex-1 text-gray-500 font-bold text-xs py-2 rounded-lg';
        document.getElementById('iq-start-title').innerText = '반려동물 지능(IQ) 테스트 🧠';
    } else {
        if(btnPet) btnPet.className = 'flex-1 text-gray-500 font-bold text-xs py-2 rounded-lg';
        if(btnOwner) btnOwner.className = 'flex-1 bg-white text-sky-600 font-bold text-xs py-2 rounded-lg shadow-sm';
        document.getElementById('iq-start-title').innerText = '집사 눈치 테스트 👀';
    }
}

function switchMbtiMode(mode) {
    currentMbtiTarget = mode;
    const btnPet = document.getElementById('mbti-mode-pet');
    const btnOwner = document.getElementById('mbti-mode-owner');
    
    if (mode === 'pet') {
        if(btnPet) btnPet.className = 'flex-1 bg-white text-pink-600 font-bold text-xs py-2 rounded-lg shadow-sm transition-all';
        if(btnOwner) btnOwner.className = 'flex-1 text-gray-500 font-bold text-xs py-2 rounded-lg transition-all';
        const titleEl = document.getElementById('mbti-start-title');
        if(titleEl) titleEl.innerText = '댕냥이 성향 MBTI 진단 🐾';
    } else {
        if(btnPet) btnPet.className = 'flex-1 text-gray-500 font-bold text-xs py-2 rounded-lg transition-all';
        if(btnOwner) btnOwner.className = 'flex-1 bg-white text-pink-600 font-bold text-xs py-2 rounded-lg shadow-sm transition-all';
        const titleEl = document.getElementById('mbti-start-title');
        if(titleEl) titleEl.innerText = '집사 성향 MBTI 진단 🧔';
    }

    // Hide stepper screen when switching modes
    const stepper = document.getElementById('mbti-stepper-screen');
    if (stepper) stepper.classList.add('hidden');
    
    // Toggle start screen vs result container depending on if results are saved
    const startScreen = document.getElementById('mbti-start-screen');
    const resultScreen = document.getElementById('mbti-result-container');
    
    const savedCode = (mode === 'pet') ? savedPetMbti : savedOwnerMbti;
    
    if (savedCode) {
        if (startScreen) startScreen.classList.add('hidden');
        if (resultScreen) resultScreen.classList.remove('hidden');
        
        let title = "";
        let desc = "";
        const badgeEl = document.getElementById('mbti-res-badge');
        
        if (mode === 'pet') {
            if (badgeEl) badgeEl.innerText = "PET P-MBTI";
            if (savedCode === "ENFP") { title = "천진난만 힐링 요정"; desc = "세상 모든 것이 신기하고 즐거운 에너지 메이커입니다."; }
            else if (savedCode === "ISTJ") { title = "원칙주의 선비냥멍"; desc = "규칙적인 루틴을 사랑하며 얌전하고 점잖은 성격입니다."; }
            else if (savedCode === "ENTP") { title = "호기심 대마왕 말썽꾸러기"; desc = "새로운 사고를 치는 데 천부적인 재능이 있습니다."; }
            else if (savedCode === "ISFJ") { title = "집사바라기 수호천사"; desc = "주인 곁을 묵묵히 지키는 충성스럽고 따뜻한 아이입니다."; }
            else { title = "자유로운 마이웨이"; desc = "독특한 개성을 가진 4차원 매력의 소유자입니다."; }
        } else {
            if (badgeEl) badgeEl.innerText = "OWNER MBTI";
            if (savedCode === "ENFJ") { title = "다정다감 펫 인플루언서"; desc = "펫과의 일상을 공유하고 사랑을 듬뿍 주는 열정 집사입니다."; }
            else if (savedCode === "ISTP") { title = "츤데레 실용주의 집사"; desc = "겉으론 무뚝뚝해 보여도 펫에게 필요한 건 다 챙겨줍니다."; }
            else if (savedCode === "INFP") { title = "펫과 영혼의 교감을 나누는 몽상가"; desc = "동물의 마음에 깊이 공감하는 감수성 풍부한 집사입니다."; }
            else if (savedCode === "ESTJ") { title = "칼각 펫 매니저"; desc = "산책, 식사, 영양제 등 펫의 스케줄을 철저하게 관리합니다."; }
            else { title = "개성 넘치는 자유로운 영혼의 집사"; desc = "펫과 함께 매일매일 새로운 추억을 만들어가는 스타일입니다."; }
        }
        
        const scoreEl = document.getElementById('mbti-res-score');
        if (scoreEl) scoreEl.innerText = savedCode;
        
        const titleEl = document.getElementById('mbti-res-title');
        if (titleEl) titleEl.innerText = '"' + title + '"';
        
        const descEl = document.getElementById('mbti-res-desc');
        if (descEl) descEl.innerText = desc;
    } else {
        if (resultScreen) resultScreen.classList.add('hidden');
        if (startScreen) startScreen.classList.remove('hidden');
        
        const nameInput = document.getElementById('mbti-target-name');
        if (nameInput) {
            if (mode === 'pet') {
                nameInput.value = (typeof pets !== 'undefined' && pets.length > 0) ? getSajuPet()?.name : "";
                nameInput.placeholder = "반려동물 이름 입력 (예: 초코)";
            } else {
                nameInput.value = (typeof settings_nickname !== 'undefined') ? settings_nickname : "";
                nameInput.placeholder = "집사 이름 입력 (예: 집사)";
            }
        }
    }
}
 // To store last calculated saju for sharing

// ─── 펫 선택기 ─────────────────────────────────────────────────
let sajuSelectedPetIndex = 0;

function getSajuPet() {
    if (typeof pets === 'undefined' || !pets.length) return null;
    return pets[Math.min(sajuSelectedPetIndex, pets.length - 1)];
}

function getCurrentHarmonyResult() {
    const sajuPet = getSajuPet();
    const activePet = (typeof getActivePet === 'function') ? getActivePet() : null;
    const appResult = (typeof AppStore !== 'undefined') ? AppStore.getState('harmonyResult') : null;
    return sajuPet?.harmonyData || activePet?.harmonyData || appResult || null;
}

function renderSajuPetPicker() {
    const list = document.getElementById('saju-pet-list');
    const picker = document.getElementById('saju-pet-picker');
    if (!list) return;
    if (!pets || pets.length <= 1) {
        // 펫이 1마리 이하면 선택기 숨김
        if (picker) picker.classList.add('hidden');
        return;
    }
    if (picker) picker.classList.remove('hidden');
    list.innerHTML = pets.map((pet, idx) => {
        const active = idx === sajuSelectedPetIndex;
        const img = pet.imageUrl
            ? `<img loading="lazy" src="${pet.imageUrl}" class="w-full h-full object-cover">`
            : `<span class="text-lg">${pet.type === 'cat' ? '🐱' : pet.type === 'rabbit' ? '🐰' : '🐶'}</span>`;
        return `<button onclick="selectSajuPet(${idx})"
            class="flex items-center gap-2 px-3 py-1.5 rounded-xl border-2 transition-all text-xs font-bold
                   ${active ? 'border-brand-500 bg-brand-50 text-brand-700' : 'border-gray-200 bg-white text-gray-600 hover:border-brand-300'}">
            <div class="w-6 h-6 rounded-full overflow-hidden flex items-center justify-center bg-gray-100 flex-shrink-0">${img}</div>
            ${escapeHtml(pet.name)}
            ${active ? '<i class="fa-solid fa-check text-brand-500 text-[9px]"></i>' : ''}
        </button>`;
    }).join('');
}

function selectSajuPet(idx) {
    sajuSelectedPetIndex = idx;
    renderSajuPetPicker();
    _syncSajuPetNames();
    if (typeof showToast === 'function') showToast(`${pets[idx].name} 선택됨 🐾`);
}

function _syncSajuPetNames() {
    const pet = getSajuPet();
    if (!pet) return;
    const fields = ['saju-pet-name', 'iq-target-name', 'mbti-target-name'];
    fields.forEach(id => {
        const el = document.getElementById(id);
        if (el) el.value = pet.name;
    });
}

function saveIqToWidget() {
    if (typeof saveState === 'function') saveState();
    if (typeof showToast === 'function') showToast('마이룸 위젯에 IQ 점수가 저장되었습니다! 🧠');
}

// 조화도를 마이룸에 등록
function saveHarmonyToWidget() {
    const harmonyResult = getCurrentHarmonyResult();

    if (!harmonyResult || !harmonyResult.avgScore) {
        if (typeof showToast === 'function') {
            showToast('⚠️ 조화도를 먼저 측정해주세요!');
        }
        return;
    }

    const pet = getSajuPet();
    if (pet && !pet.harmonyData) {
        pet.harmonyData = harmonyResult;
    }

    // 상태 저장
    if (typeof saveState === 'function') saveState();

    // 방 조화도 업데이트
    if (typeof updateRoomThemeByHarmony === 'function') {
        updateRoomThemeByHarmony();
    }

    if (typeof showToast === 'function') {
        showToast(`💖 조화도 ${Math.round(harmonyResult.avgScore)}점이 마이룸에 등록되었습니다!`);
    }

    // 마이펫 탭으로 이동
    setTimeout(() => {
        if (typeof switchTab === 'function') {
            switchTab('mypet');
        }
    }, 1500);
}

window.saveHarmonyToWidget = saveHarmonyToWidget;

// 조화도를 소셜 피드에 공유
function shareHarmonyToSocial() {
    const harmonyResult = getCurrentHarmonyResult();
    const pet = getSajuPet() || ((typeof getActivePet === 'function') ? getActivePet() : null);
    const ownerName = (typeof settings_nickname !== 'undefined' && settings_nickname) ? settings_nickname : '집사';

    if (!harmonyResult || !harmonyResult.avgScore) {
        if (typeof showToast === 'function') {
            showToast('⚠️ 조화도를 먼저 측정해주세요!');
        }
        return;
    }

    const score = Math.round(harmonyResult.avgScore);
    const petName = pet?.name || '댕이';
    const petAvatar = pet?.imageUrl || 'https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=300';

    // 점수별 메시지
    let message = '';
    let emoji = '💖';
    if (score >= 90) {
        message = `${ownerName}와 ${petName}는 영혼의 단짝! 완벽한 듀오입니다 💖✨`;
        emoji = '💖✨';
    } else if (score >= 75) {
        message = `${ownerName}와 ${petName}는 서로를 잘 이해하는 최고의 파트너 💛🌟`;
        emoji = '💛🌟';
    } else if (score >= 60) {
        message = `${ownerName}와 ${petName}는 서로에게 긍정적인 영향을 주는 관계 💚🍀`;
        emoji = '💚🍀';
    } else if (score >= 40) {
        message = `${ownerName}와 ${petName}는 노력하면 더욱 발전할 수 있는 관계 💙⭐`;
        emoji = '💙⭐';
    } else {
        message = `${ownerName}와 ${petName}는 서로 다른 성향, 이해와 배려가 필요해요 🤍🌈`;
        emoji = '🤍🌈';
    }

    // 소셜 피드 포스트 생성
    const newPost = {
        id: Date.now(),
        petName: petName,
        petAvatar: petAvatar,
        content: `${emoji} 영혼의 조화도 측정 결과: ${score}점!\n\n${message}`,
        image: null,
        isVideo: false,
        videoUrl: null,
        likes: 0,
        liked: false,
        comments: [],
        attachedWalk: null,
        attachedAiHealth: null
    };

    // posts 배열에 추가
    if (typeof posts !== 'undefined') {
        posts.unshift(newPost);
    }

    if (typeof saveState === 'function') saveState();
    if (typeof showToast === 'function') {
        showToast(`✅ 조화도가 소셜 피드에 공유되었습니다!`);
    }

    // 소셜 탭으로 이동
    setTimeout(() => {
        if (typeof switchTab === 'function') {
            switchTab('social');
        }
    }, 1500);
}

window.shareHarmonyToSocial = shareHarmonyToSocial;

function saveIqResult() {
    if (typeof saveState === 'function') saveState();
    if (typeof showToast === 'function') showToast('IQ 결과가 저장되었습니다! 🧠');
}

function saveMbtiResult() {
    if (typeof saveState === 'function') saveState();
    if (typeof showToast === 'function') showToast('MBTI 결과가 저장되었습니다! 🐾');
}

function renderSajuTab() {
    renderSajuPetPicker();
    _syncSajuPetNames();
    switchSajuSubTab('harmony');

    const petNameInput = document.getElementById('saju-pet-name');
    const ownerNameInput = document.getElementById('saju-owner-name');

    if (petNameInput && typeof pets !== 'undefined' && pets.length > 0) {
        petNameInput.value = getSajuPet()?.name;
    }
    if (ownerNameInput && typeof settings_nickname !== 'undefined') {
        ownerNameInput.value = settings_nickname;
    }

    const current = getSajuPet();
    const inputContainer = document.getElementById('saju-input-container');
    const loadingContainer = document.getElementById('saju-loading-container');
    const resultContainer = document.getElementById('saju-result-container');

    if (current && current.sajuData) {
        if (inputContainer) inputContainer.classList.add('hidden');
        if (loadingContainer) loadingContainer.classList.add('hidden');
        if (resultContainer) resultContainer.classList.remove('hidden');

        // Fill results
        const resPetSum = document.getElementById('res-pet-summary');
        if (resPetSum) {
            resPetSum.innerHTML = colorizeSajuElement(current.sajuData.petSummary);
        }

        const resPetDesc = document.getElementById('res-desc-pet');
        if (resPetDesc) {
            resPetDesc.innerHTML = formatSajuDesc(current.sajuData.petDesc);
        }

        const resOwnerSum = document.getElementById('res-owner-summary');
        if (resOwnerSum) {
            resOwnerSum.innerHTML = colorizeSajuElement(current.sajuData.ownerSummary);
        }

        const resOwnerDesc = document.getElementById('res-desc-owner');
        if (resOwnerDesc) {
            resOwnerDesc.innerHTML = formatSajuDesc(current.sajuData.ownerDesc);
        }
    } else {
        if (inputContainer) inputContainer.classList.remove('hidden');
        if (loadingContainer) loadingContainer.classList.add('hidden');
        if (resultContainer) resultContainer.classList.add('hidden');
    }
}

function colorizeSajuElement(text) {
    if (!text) return "";
    return text.replace('木 (나무)', '<span class="text-emerald-600">木 (나무)</span>')
               .replace('火 (불)', '<span class="text-rose-600">火 (불)</span>')
               .replace('土 (흙)', '<span class="text-amber-700">土 (흙)</span>')
               .replace('金 (쇠)', '<span class="text-gray-600">金 (쇠)</span>')
               .replace('水 (물)', '<span class="text-sky-600">水 (물)</span>');
}

function formatSajuDesc(text) {
    if (!text) return "";
    let formatted = text.replace(/\n/g, '<br>');
    formatted = formatted.replace(/\[(.*?)\]/, '<strong>[$1]</strong>');
    return formatted;
}

function deleteSajuData() {
    if (confirm("정말 사주 분석 결과를 삭제하시겠습니까?")) {
        const current = getSajuPet();
        if (current) {
            delete current.sajuData;
            savedSajuScore = 0;
            if (typeof window !== 'undefined') window.savedSajuScore = 0;
            
            if (typeof saveState === 'function') saveState();
            if (typeof updatePetInSupabase === 'function') {
                try { updatePetInSupabase(current); } catch(e) {}
            }
        }
        
        renderSajuTab();
        
        if (typeof renderMyPets === 'function') {
            renderMyPets();
        }
        
        if (typeof showToast === 'function') {
            showToast("사주 결과가 삭제되었습니다.");
        }
    }
}




function startSajuAnalysis() {
    const petNameEl = document.getElementById('saju-pet-name');
    const petName = petNameEl ? petNameEl.value.trim() : ((typeof pets !== 'undefined' && getSajuPet()?.name) || "댕이");
    
    const petBirthEl = document.getElementById('saju-pet-birth');
    const petBirth = petBirthEl ? petBirthEl.value : "";
    
    const petTimeEl = document.getElementById('saju-pet-time');
    const petTime = petTimeEl ? petTimeEl.value : "";
    
    const ownerBirthEl = document.getElementById('saju-owner-birth');
    const ownerBirth = ownerBirthEl ? ownerBirthEl.value : "";
    
    const ownerTimeEl = document.getElementById('saju-owner-time');
    const ownerTime = ownerTimeEl ? ownerTimeEl.value : "";

    if (!petBirth || !ownerBirth) {
        if(typeof showToast === 'function') showToast("⚠️ 반려동물과 집사의 생년월일을 모두 입력해 주세요!");
        return;
    }

    const inputContainer = document.getElementById('saju-input-container');
    if (inputContainer) inputContainer.classList.add('hidden');
    
    const loadingContainer = document.getElementById('saju-loading-container');
    if (loadingContainer) loadingContainer.classList.remove('hidden');

    setTimeout(() => {
        if (loadingContainer) loadingContainer.classList.add('hidden');
        
        const resultContainer = document.getElementById('saju-result-container');
        if (resultContainer) resultContainer.classList.remove('hidden');

        const hashString = (str) => {
            let hash = 0;
            for (let i = 0; i < str.length; i++) {
                hash = str.charCodeAt(i) + ((hash << 5) - hash);
            }
            return Math.abs(hash);
        };

        const elements = [
            { el: '木 (나무)', color: 'text-emerald-600', bg: 'bg-emerald-50', desc: '성장과 활력이 넘치는 따뜻한 봄의 기운' },
            { el: '火 (불)', color: 'text-rose-600', bg: 'bg-rose-50', desc: '열정과 밝은 에너지가 솟구치는 여름의 기운' },
            { el: '土 (흙)', color: 'text-amber-700', bg: 'bg-amber-50', desc: '모든 것을 포용하고 안정적인 중재자의 기운' },
            { el: '金 (쇠)', color: 'text-gray-600', bg: 'bg-gray-100', desc: '단단하고 결단력 있는 가을의 기운' },
            { el: '水 (물)', color: 'text-sky-600', bg: 'bg-sky-50', desc: '지혜롭고 유연하게 흘러가는 겨울의 기운' }
        ];

        const petHash = hashString(petBirth + petTime);
        const ownerHash = hashString(ownerBirth + ownerTime);
        
        const petElement = elements[petHash % 5];
        const ownerElement = elements[ownerHash % 5];

        const petReadings = [
            "🐶 [식신생재격] 타고난 식복과 애교로 어디서든 굶지 않고 사랑받을 팔자입니다. 보호자의 마음을 녹이는 치명적인 매력을 가졌으며, 먹을 것을 보면 고도의 집중력을 발휘합니다. 가끔 고집을 부릴 땐 백 마디 말보다 간식 하나로 회유하는 것이 직빵입니다. 건강하고 무탈하게 장수할 좋은 기운을 가졌네요.",
            "🐱 [역마살/호기심] 두뇌 회전이 매우 빠르고 호기심이 왕성합니다. 가만히 있는 것보다 집안 구석구석 새로운 냄새를 맡고 돌아다녀야 직성이 풀리는 약간의 역마살이 있습니다. 낯선 물건이 오면 반드시 먼저 검사를 거쳐야 하며, 똑똑한 만큼 보호자의 약점을 잘 알고 교묘하게 이용할 줄 아는 영특한 아이입니다.",
            "🐾 [관인상생격] 보호자에 대한 충성심과 애착이 남다릅니다. 주인이 세상의 전부인 것처럼 행동하며, 주위 사람들에게도 다정다감하고 젠틀한 성격을 가졌습니다. 가족이 우울해하면 가장 먼저 다가와 위로해주는 속 깊은 천사입니다. 칭찬을 받을수록 능력이 배가 되니 무한한 칭찬이 필요합니다.",
            "🐰 [예민/섬세] 매우 예민하고 섬세한 영혼의 소유자입니다. 큰 소리나 급격한 환경 변화를 싫어하며, 안정적이고 포근한 자기만의 보금자리를 가장 좋아합니다. 낯선 사람에게는 곁을 잘 내어주지 않지만, 한 번 마음을 연 가족에게는 무한한 애정을 쏟습니다. 조용하고 평화로운 환경에서 가장 행복해합니다.",
            "🦁 [비견겁재/독립] 독립심이 강하고 자기주장이 아주 확실한 장군감입니다. 자기가 원할 때만 애교를 부리며, 귀찮게 구는 것을 딱 질색하는 밀당의 고수라 할 수 있습니다. 억지로 시키는 것을 싫어하지만 납득하면 누구보다 잘 따릅니다. 프라이드를 존중해주고 대등한 파트너로서 대해주면 최고의 관계가 됩니다."
        ];
        
        const ownerReadings = [
            "🧔 [자애/희생] 동물을 사랑하는 마음이 태평양처럼 넓고 깊어 펫에게 조건 없이 헌신하는 따뜻한 부모 사주입니다. 내 입에 들어가는 것보다 펫 입에 들어가는 간식이 더 기쁘며, 펫의 행복이 곧 나의 행복입니다. 펫의 럭셔리한 삶을 위해 열심히 돈을 벌게 될 팔자이니 체력 관리에 유의하세요.",
            "🏃 [활동/친구] 펫을 일방적으로 돌보기보다는 친구처럼 티격태격하며 지내는 평등하고 수평적인 관계를 추구합니다. 활동적인 에너지가 넘쳐서 펫과 함께 산책, 캠핑, 나들이를 자주 나가면 본인의 운까지 덩달아 크게 트이는 일석이조의 사주입니다. 펫과 찰떡 호흡을 자랑합니다.",
            "🧐 [세심/관리] 꼼꼼하고 세심한 성격으로 펫의 아주 작은 변화나 컨디션 저하도 금방 알아채는 훌륭한 관찰력을 가졌습니다. 식단, 배변, 영양제 등을 철저하게 기록하고 관리하는 데 탁월한 재능이 있습니다. 펫에게는 그야말로 완벽에 가까운 주치의이자 매니저 같은 든든한 존재입니다.",
            "✨ [교감/감수성] 감수성이 매우 풍부하여 펫과 깊은 영혼의 교감을 나눌 수 있는 사주입니다. 굳이 소리 내어 말하지 않아도 펫의 눈빛과 작은 몸짓만으로 무엇을 원하는지 직감적으로 알아챕니다. 펫 역시 당신의 기분을 귀신같이 알아채어 서로 뗄 수 없는 끈끈한 유대감을 형성하게 됩니다.",
            "🎉 [낙천/긍정] 다소 덤벙거리고 계획성이 부족할 순 있지만, 특유의 밝고 초긍정적인 에너지로 펫에게 끊임없는 즐거움을 선사합니다. 가끔 밥 시간이나 산책 시간을 깜빡 잊어도, 당신의 해맑은 미소와 진심 어린 사과(?)를 보며 펫은 모든 것을 용서할 것입니다. 집안에 웃음이 끊이지 않겠네요."
        ];

        const petReading = petReadings[petHash % 5];
        const ownerReading = ownerReadings[ownerHash % 5];

        const resPetSum = document.getElementById('res-pet-summary');
        if (resPetSum) resPetSum.innerHTML = `<span class="${petElement.color}">${petElement.el}</span>의 기운을 가진 펫`;
        
        const resPetDesc = document.getElementById('res-desc-pet');
        if (resPetDesc) resPetDesc.innerHTML = `<strong>[${petElement.desc}]</strong><br><br>${petReading}`;
        
        const resOwnerSum = document.getElementById('res-owner-summary');
        if (resOwnerSum) resOwnerSum.innerHTML = `<span class="${ownerElement.color}">${ownerElement.el}</span>의 기운을 가진 집사`;
        
        const resOwnerDesc = document.getElementById('res-desc-owner');
        if (resOwnerDesc) resOwnerDesc.innerHTML = `<strong>[${ownerElement.desc}]</strong><br><br>${ownerReading}`;

        let compatibility = 50 + ((petHash + ownerHash) % 51);
        if(typeof savedSajuScore !== 'undefined') savedSajuScore = compatibility;
        else window.savedSajuScore = compatibility;

        const sajuData = {
            petBirth: petBirth,
            ownerBirth: ownerBirth,
            petSummary: `${petElement.el}의 기운을 가진 펫`,
            petDesc: `[${petElement.desc}]\n\n${petReading}`,
            ownerSummary: `${ownerElement.el}의 기운을 가진 집사`,
            ownerDesc: `[${ownerElement.desc}]\n\n${ownerReading}`,
            compatScore: compatibility,
            compatTitle: compatibility >= 90 ? "수어지교(水魚之交)" : (compatibility >= 80 ? "금상첨화(錦上添花)" : (compatibility >= 70 ? "유유상종(類類相從)" : "동상이몽(同床異夢)")),
            pastDesc: compatibility >= 90 ? "전생에 함께 강을 건넜던 깊은 동반자적 인연입니다." : "전생에 서로 소중히 여겼던 이웃이었습니다.",
            synergyDesc: compatibility >= 90 ? "서로의 기운을 보완하며 함께 있을 때 큰 행운이 따릅니다." : "서로를 이해하고 천천히 맞춰가면 아주 좋은 흐름이 생깁니다."
        };
        
        if (typeof pets !== 'undefined' && pets.length > 0) {
            getSajuPet().sajuData = sajuData;
            
            // Also store in AppStore immediately
            if (typeof AppStore !== 'undefined') {
                AppStore.setState('petSaju', {
                    year: petBirth.split('-')[0],
                    birthDate: petBirth
                });
                AppStore.setState('butlerSaju', {
                    year: ownerBirth.split('-')[0],
                    birthDate: ownerBirth
                });
            }
            
            if (typeof saveState === 'function') saveState();
            if (typeof updatePetInSupabase === 'function') {
                try { updatePetInSupabase(getSajuPet()); } catch(e) {}
            }
        }

    }, 1500);
}

function startFortuneDraw() {
    const drawContainer = document.getElementById('fortune-draw-container');
    if (drawContainer) drawContainer.classList.add('hidden');
    
    const resContainer = document.getElementById('fortune-result-container');
    if (resContainer) resContainer.classList.remove('hidden');

    const today = new Date();
    const dateStr = `${today.getFullYear()}년 ${today.getMonth()+1}월 ${today.getDate()}일`;
    const todayDateEl = document.getElementById('fortune-today-date');
    if(todayDateEl) todayDateEl.innerText = dateStr;

    const seed = today.getFullYear() * 10000 + (today.getMonth()+1) * 100 + today.getDate();
    let randomValue = (seed * 9301 + 49297) % 233280;
    randomValue = randomValue / 233280;

    const keywords = [
        "뜻밖의 특급 간식을 득템하는 날!",
        "에너지가 넘쳐 산책이 즐거운 날!",
        "포근한 이불 속에서 늦잠 자기 딱 좋은 날!",
        "주인과의 텔레파시가 100% 통하는 날!",
        "새로운 장난감이나 친구를 만날 수 있는 날!"
    ];
    
    const petFortunes = [
        "오늘은 컨디션이 최고조에 달합니다. 꼬리가 하루 종일 멈추지 않을 예정이니 마음껏 뛰어놀게 해주세요!",
        "조금 나른하고 귀찮은 하루입니다. 억지로 무언가를 하기보다는 따뜻한 곳에서 푹 쉬는 것이 최고입니다.",
        "식욕이 폭발하는 날입니다. 자꾸만 주방 쪽을 서성이며 간식을 요구할 수 있으니 체중 관리에 유의하세요.",
        "보호자의 껌딱지가 되는 날입니다. 평소보다 더 많이 안아주고 쓰다듬어 주면 행복지수가 200% 상승합니다.",
        "장난기가 발동하여 집안을 우다다 뛰어다닐 수 있습니다. 위험한 물건은 미리 치워두는 센스가 필요합니다."
    ];

    const ownerFortunes = [
        "우연히 들른 펫샵이나 온라인 몰에서 원하던 용품을 역대급 할인가에 득템할 수 있는 행운이 따릅니다.",
        "펫과 산책을 하다가 평소 인사하고 싶었던 동네 보호자와 즐거운 대화를 나누게 될 지도 모릅니다.",
        "펫의 귀여운 돌발 행동 덕분에 배꼽 잡고 크게 웃을 일이 생깁니다. 카메라를 항상 대기시켜 두세요!",
        "오늘은 조금 피곤한 하루가 될 수 있습니다. 퇴근 후 펫을 껴안고 일찍 잠자리에 드는 것을 추천합니다.",
        "펫의 새로운 매력 포인트를 발견하게 되는 날입니다. 우리 애가 이런 면이 있었어? 하며 놀라게 될 것입니다."
    ];

    const index1 = Math.floor(randomValue * keywords.length);
    const index2 = Math.floor((randomValue * 10) % petFortunes.length);
    const index3 = Math.floor((randomValue * 100) % ownerFortunes.length);

    const mainKeyword = document.getElementById('fortune-main-keyword');
    if (mainKeyword) mainKeyword.innerText = '"' + keywords[index1] + '"';
    
    const petText = document.getElementById('fortune-pet-text');
    if (petText) petText.innerText = petFortunes[index2];
    
    const ownerText = document.getElementById('fortune-owner-text');
    if (ownerText) ownerText.innerText = ownerFortunes[index3];
}



// ==========================================
// 🧠 IQ TEST DATA (PET & OWNER)
// ==========================================
const IQ_QUESTIONS_PET = [
    { q: "간식을 손에 숨기고 섞었을 때, 간식이 있는 손을 찾는 속도는?", a: [{text:"1초 만에 바로 찍는다", score:15}, {text:"냄새를 한참 맡다가 찾는다", score:10}, {text:"못 찾고 내 얼굴만 본다", score:5}] },
    { q: "새로운 장난감을 주었을 때의 반응은?", a: [{text:"금방 노는 방법을 파악한다", score:15}, {text:"몇 번 툭툭 치다가 익숙해진다", score:10}, {text:"무서워하거나 전혀 관심이 없다", score:5}] },
    { q: "내가 옷을 챙겨 입고 나갈 준비를 할 때 펫은?", a: [{text:"벌써 현관문 앞에서 대기한다", score:15}, {text:"나가는 걸 알고 쳐다보며 우울해한다", score:10}, {text:"내가 나가는지도 모른 채 자고 있다", score:5}] },
    { q: "이름을 불렀을 때 어떻게 반응하나요?", a: [{text:"부르자마자 득달같이 달려온다", score:15}, {text:"귀만 쫑긋거리거나 쳐다만 본다", score:10}, {text:"못 들은 척 무시한다", score:5}] },
    { q: "배가 고플 때 밥그릇 앞에서 하는 행동은?", a: [{text:"밥그릇을 발로 치거나 나를 쳐다보며 요구한다", score:15}, {text:"밥그릇 옆에 얌전히 앉아 기다린다", score:10}, {text:"밥을 줄 때까지 그냥 돌아다닌다", score:5}] },
    { q: "산책 갈 때 목줄이나 하네스를 꺼내면?", a: [{text:"흥분해서 폴짝폴짝 뛰고 난리난다", score:15}, {text:"다가와서 채워주기를 기다린다", score:10}, {text:"이게 뭔지 아직도 잘 모른다", score:5}] },
    { q: "내가 슬퍼서 울고 있을 때 펫의 반응은?", a: [{text:"다가와서 얼굴을 핥거나 위로해준다", score:15}, {text:"옆에 가만히 앉아 있어준다", score:10}, {text:"평소와 다름없이 자기 할 일을 한다", score:5}] },
    { q: "소파 밑으로 장난감이 굴러들어갔을 때?", a: [{text:"앞발을 사용해 어떻게든 꺼내려 한다", score:15}, {text:"나를 쳐다보며 꺼내달라고 짖는다", score:10}, {text:"포기하고 다른 걸 가지고 논다", score:5}] },
    { q: "TV에서 다른 동물 소리가 나면?", a: [{text:"화면을 뚫어지게 보거나 찾아다닌다", score:15}, {text:"귀만 쫑긋거리다 만다", score:10}, {text:"TV 소리인 줄 알고 무시한다", score:5}] },
    { q: "낯선 사람이 집에 방문했을 때?", a: [{text:"경계하다가 내가 반기면 안심한다", score:15}, {text:"그냥 무조건 좋다고 꼬리친다", score:10}, {text:"구석에 숨어서 안 나온다", score:5}] }
];

const IQ_QUESTIONS_OWNER = [
    { q: "펫이 갑자기 코로 내 손을 툭툭 칠 때 의미는?", a: [{text:"나랑 놀아줘! (정답)", score:15}, {text:"간식 내놔라", score:10}, {text:"잘 모르겠다", score:5}] },
    { q: "산책 중 펫이 갑자기 바닥의 냄새를 오래 맡는 이유는?", a: [{text:"스트레스를 풀고 정보를 수집하는 중", score:15}, {text:"맛있는 게 떨어져 있나 찾는 중", score:10}, {text:"그냥 멈추고 싶어서", score:5}] },
    { q: "강아지가 하품을 하는 가장 주된 이유 중 하나는?", a: [{text:"긴장하거나 스트레스를 받을 때", score:15}, {text:"졸릴 때만", score:10}, {text:"나한테 화가 났을 때", score:5}] },
    { q: "고양이가 꼬리를 천천히 살랑살랑 흔든다면?", a: [{text:"심기 불편, 짜증남", score:15}, {text:"기분이 좋음", score:10}, {text:"간식을 원함", score:5}] },
    { q: "펫이 꼬리를 배 밑으로 말아 넣었다면?", a: [{text:"극도의 공포와 불안", score:15}, {text:"나랑 숨바꼭질 하는 중", score:10}, {text:"배가 아파서", score:5}] },
    { q: "고양이가 내 눈을 보며 천천히 눈을 깜빡이는 것은?", a: [{text:"사랑과 신뢰의 '눈키스'", score:15}, {text:"졸리다는 신호", score:10}, {text:"먼지가 들어갔음", score:5}] },
    { q: "강아지가 내 발을 깔고 앉는 이유는?", a: [{text:"내 거야! 소유욕과 보호 본능", score:15}, {text:"발이 따뜻해서", score:10}, {text:"우연의 일치", score:5}] },
    { q: "고양이가 갑자기 미친듯이 우다다를 하는 이유는?", a: [{text:"남아도는 에너지를 발산하기 위해", score:15}, {text:"벌레를 쫓고 있어서", score:10}, {text:"귀신을 봤다", score:5}] },
    { q: "강아지가 나를 보고 엉덩이를 치켜들고 앞발을 낮춘다면?", a: [{text:"신나게 놀자는 플레이 바우", score:15}, {text:"스트레칭 중이다", score:10}, {text:"공격 준비 자세", score:5}] },
    { q: "펫과 함께하는 삶에서 집사에게 가장 필요한 덕목은?", a: [{text:"무한한 인내와 세심한 관찰력", score:15}, {text:"돈벌어서 간식 사기", score:10}, {text:"일단 귀여워하기", score:5}] }
];

// ==========================================
// 🐾 MBTI TEST DATA (PET & OWNER)
// ==========================================
const MBTI_QUESTIONS_PET = [
    { q: "산책을 나갔을 때 펫의 주된 모습은?", a: [{text:"동네 멍냥이들과 다 인사해야 직성이 풀린다", type:"E"}, {text:"주인 옆에서 조용히 내 갈 길만 간다", type:"I"}] },
    { q: "집에 새로운 택배 상자가 왔을 때 반응은?", a: [{text:"이게 뭐야? 당장 냄새 맡고 탐색한다", type:"E"}, {text:"멀리서 지켜보다가 흥미를 잃는다", type:"I"}] },
    { q: "간식을 줄 때 펫의 행동은?", a: [{text:"주자마자 1초 컷으로 삼켜버린다", type:"S"}, {text:"이게 뭐지? 냄새를 맡으며 음미한다", type:"N"}] },
    { q: "처음 보는 신기한 장난감을 주었을 때?", a: [{text:"일단 물고 뜯고 던지며 논다", type:"S"}, {text:"살금살금 다가가서 관찰부터 한다", type:"N"}] },
    { q: "집사가 집에 돌아왔을 때의 환영 방식은?", a: [{text:"꼬리가 프로펠러처럼 돌고 얼굴을 핥는다", type:"F"}, {text:"왔어? 하고 한 번 쳐다보고 다시 잔다", type:"T"}] },
    { q: "내가 혼내거나 안돼! 라고 단호하게 말하면?", a: [{text:"눈치를 보며 낑낑거리거나 애교를 부린다", type:"F"}, {text:"알았어 알았어~ 하고 쿨하게 돌아선다", type:"T"}] },
    { q: "밥 먹고 자고 노는 일상 루틴이 있다면?", a: [{text:"매일 일정한 시간에 밥과 산책을 정확히 요구한다", type:"J"}, {text:"배고플 때 먹고, 자고 싶을 때 잔다", type:"P"}] },
    { q: "잠자리를 선택할 때 펫의 취향은?", a: [{text:"항상 자는 지정석 푹신한 방석이 있다", type:"J"}, {text:"그날그날 끌리는 바닥이나 소파 아무데서나 잔다", type:"P"}] },
    { q: "다른 펫이 내 장난감을 뺏으려 한다면?", a: [{text:"절대 안 뺏겨! 으르렁거리며 사수한다", type:"T"}, {text:"어쩔 수 없지.. 하고 양보하거나 슬퍼한다", type:"F"}] },
    { q: "주말에 집사가 하루종일 집에 있으면?", a: [{text:"나랑 놀자며 장난감을 계속 물어온다", type:"E"}, {text:"각자의 시간을 즐기며 조용히 옆에 누워있는다", type:"I"}] }
];

const MBTI_QUESTIONS_OWNER = [
    { q: "쉬는 날 나는 주로 어떻게 시간을 보내나요?", a: [{text:"친구들을 만나거나 펫과 함께 야외 핫플 카페에 간다", type:"E"}, {text:"집에서 펫을 끌어안고 넷플릭스를 본다", type:"I"}] },
    { q: "펫과 산책 중에 모르는 보호자가 말을 걸면?", a: [{text:"자연스럽게 스몰토크를 하며 펫 정보를 교환한다", type:"E"}, {text:"짧게 대답하고 어색하게 웃으며 지나간다", type:"I"}] },
    { q: "펫 용품을 고를 때 나의 기준은?", a: [{text:"실용성! 가격 대비 성분이 좋고 튼튼한 것", type:"S"}, {text:"디자인! 예쁘고 유니크해서 사진 찍기 좋은 것", type:"N"}] },
    { q: "펫이 아플 때 나의 첫 번째 행동은?", a: [{text:"일단 병원부터 예약하고 증상을 메모한다", type:"S"}, {text:"인터넷 폭풍 검색하며 불안함에 휩싸인다", type:"N"}] },
    { q: "펫이 밥을 안 먹고 시무룩해 보일 때 나는?", a: [{text:"어디 아픈가? 병원에 가봐야 하나 이성적으로 판단한다", type:"T"}, {text:"무슨 일 있어 우리 아가? 하면서 같이 시무룩해진다", type:"F"}] },
    { q: "친구가 '나 오늘 우울해서 강아지 데리고 카페 왔어' 라고 하면?", a: [{text:"어느 카페 갔어? 강아지는 재밌게 놀아?", type:"T"}, {text:"무슨 일 있어? ㅠㅠ 강아지 보고 힐링해!", type:"F"}] },
    { q: "펫과 함께 1박 2일 여행을 갈 때 나의 스타일은?", a: [{text:"숙소부터 애견동반 식당까지 시간 단위로 계획을 짠다", type:"J"}, {text:"일단 출발! 가다가 좋아보이는 곳에 멈춘다", type:"P"}] },
    { q: "펫 용품 정리 및 청소 상태는?", a: [{text:"장난감 상자, 간식 서랍 등 항상 각 잡혀 정리되어 있다", type:"J"}, {text:"집안 곳곳에 장난감과 간식이 널브러져 있다", type:"P"}] },
    { q: "SNS에서 유행하는 펫 챌린지를 본다면?", a: [{text:"재밌어 보이네~ 하고 그냥 넘긴다", type:"S"}, {text:"우리 애도 해보자! 당장 카메라를 켠다", type:"N"}] },
    { q: "펫의 생일날 나는 어떻게 파티를 준비하나요?", a: [{text:"케이크도 미리 주문하고 예쁜 옷 입혀서 스튜디오 파티!", type:"J"}, {text:"당일에 맛있는 특식 사주고 사진 몇 장 찍어준다", type:"P"}] }
];

let iqCurrentStep = 0;
let iqTotalScore = 0;
let currentIqQuestions = [];

let mbtiCurrentStep = 0;
let mbtiScores = { E:0, I:0, S:0, N:0, T:0, F:0, J:0, P:0 };
let currentMbtiQuestions = [];



function resetIqTest() {
    document.getElementById('iq-result-container').classList.add('hidden');
    document.getElementById('iq-start-screen').classList.remove('hidden');
    document.getElementById('iq-stepper-screen').classList.add('hidden');
    const grid = document.getElementById('iq-test-grid');
    if (grid) grid.classList.remove('hidden');
    iqCurrentStep = 0;
    iqTotalScore = 0;
}

function startIqTestStepper() {
    let name = document.getElementById('iq-target-name').value || (currentIqTarget === 'pet' ? '마이펫' : '집사');
    
    currentIqQuestions = currentIqTarget === 'pet' ? IQ_QUESTIONS_PET : IQ_QUESTIONS_OWNER;
    iqCurrentStep = 0;
    iqTotalScore = 0;
    
    document.getElementById('iq-start-screen').classList.add('hidden');
    document.getElementById('iq-stepper-screen').classList.remove('hidden');
    
    renderIqQuestion();
}

function renderIqQuestion() {
    let total = currentIqQuestions.length;
    let percent = Math.round(((iqCurrentStep + 1) / total) * 100);
    
    document.getElementById('iq-progress-text').innerText = `질문 ${iqCurrentStep + 1} / ${total}`;
    // 프로그레스 바 요소가 존재하면 너비 업데이트
    const progressBar = document.getElementById('iq-progress-bar');
    if(progressBar) progressBar.style.width = `${percent}%`;
    
    let qData = currentIqQuestions[iqCurrentStep];
    document.getElementById('iq-question-title').innerText = qData.q;
    
    let optionsHtml = '';
    qData.a.forEach((opt, idx) => {
        optionsHtml += `<button onclick="handleIqAnswer(${opt.score})" class="w-full bg-white border border-sky-200 hover:bg-sky-50 text-gray-700 font-bold py-3 px-4 rounded-xl text-xs text-left transition-all active:scale-95 shadow-sm">${opt.text}</button>`;
    });
    
    document.getElementById('iq-options-container').innerHTML = optionsHtml;
}

function handleIqAnswer(score) {
    iqTotalScore += score;
    iqCurrentStep++;
    
    if (iqCurrentStep < currentIqQuestions.length) {
        renderIqQuestion();
    } else {
        finishIqTest();
    }
}

function finishIqTest() {
    const stepper = document.getElementById('iq-stepper-screen');
    if (stepper) stepper.classList.add('hidden');
    const grid = document.getElementById('iq-test-grid');
    if (grid) grid.classList.add('hidden');
    
    // 로딩 화면이 있다면 노출
    const loading = document.getElementById('iq-loading-container');
    if (loading) loading.classList.remove('hidden');
    
    setTimeout(() => {
        if (loading) loading.classList.add('hidden');
        
        const resultContainer = document.getElementById('iq-result-container');
        if (resultContainer) resultContainer.classList.remove('hidden');
        
        // 결과 판정
        let score = iqTotalScore; // Max 150
        // 백분율로 환산 (조화도 연동을 위해 저장)
        let percentScore = Math.round((score / 150) * 100);
        
        let title = "";
        let desc = "";
        
        const badgeEl = document.getElementById('iq-res-badge');
        const scoreEl = document.getElementById('iq-res-score');
        
        if (currentIqTarget === 'pet') {
            savedPetIqScore = percentScore;
            if (typeof pets !== 'undefined' && pets.length > 0) {
                getSajuPet().iqScore = Math.round(percentScore * 1.5);
                if (typeof saveState === 'function') saveState();
            }
            if (badgeEl) badgeEl.innerText = "PET IQ";
            
            if (percentScore >= 90) { title = "우주 대천재급 지능"; desc = "사람 말을 다 알아듣는 천재견/묘입니다. 말을 못할 뿐 당신의 속마음까지 다 꿰뚫어보고 있습니다."; }
            else if (percentScore >= 70) { title = "영리한 수재"; desc = "눈치가 빠르고 상황 파악 능력이 뛰어납니다. 훈련 효율이 아주 좋은 영특한 두뇌를 가졌습니다."; }
            else if (percentScore >= 50) { title = "평범하지만 귀여워"; desc = "때로는 엉뚱하고 바보 같지만, 그것이 바로 가장 큰 매력 포인트입니다. 사랑스러운 백치미를 가졌네요."; }
            else { title = "아무 생각이 없다"; desc = "뇌맑은 영혼입니다. 본능에 충실하며 해맑게 살아가는 행복한 아이입니다."; }
            
            if (scoreEl) scoreEl.innerText = `IQ ${Math.round(percentScore * 1.5)}`; // 150점 만점으로 뻥튀기
        } else {
            savedOwnerIqScore = percentScore;
            const email = (typeof settings_email !== 'undefined') ? settings_email : 'butler@petna.co.kr';
            localStorage.setItem(`petna_owner_iq_${email}`, percentScore);
            if (badgeEl) badgeEl.innerText = "OWNER SENSE";
            
            if (percentScore >= 90) { title = "개통령/묘통령 강림"; desc = "동물의 바디랭귀지를 완벽하게 해독하는 경지에 이르렀습니다. 펫의 사소한 움직임만 봐도 마음을 읽어냅니다."; }
            else if (percentScore >= 70) { title = "훌륭한 프로 집사"; desc = "펫에 대한 지식과 이해도가 훌륭합니다. 어디 가서 자랑해도 손색없는 멋진 집사입니다."; }
            else if (percentScore >= 50) { title = "노력파 초보 집사"; desc = "아직 펫의 마음을 100% 이해하진 못하지만, 열심히 노력하고 있는 사랑 넘치는 집사입니다."; }
            else { title = "펫 알못.. 공부 필요!"; desc = "펫의 행동을 오해하고 있을 확률이 높습니다. 펫의 바디랭귀지 공부를 조금 더 해보시길 권장합니다."; }
            
            if (scoreEl) scoreEl.innerText = `눈치 ${percentScore}점`;
        }
        
        const titleEl = document.getElementById('iq-res-title');
        if (titleEl) titleEl.innerText = '"' + title + '"';
        
        const descEl = document.getElementById('iq-res-desc');
        if (descEl) descEl.innerText = desc;
        
    }, 1500);
}



function resetMbtiTest() {
    if (currentMbtiTarget === 'pet') {
        savedPetMbti = '';
    } else {
        savedOwnerMbti = '';
    }
    
    document.getElementById('mbti-result-container').classList.add('hidden');
    document.getElementById('mbti-start-screen').classList.remove('hidden');
    document.getElementById('mbti-stepper-screen').classList.add('hidden');
    const mbtiGrid = document.getElementById('mbti-test-grid');
    if (mbtiGrid) mbtiGrid.classList.remove('hidden');
    mbtiCurrentStep = 0;
    mbtiScores = { E:0, I:0, S:0, N:0, T:0, F:0, J:0, P:0 };
    
    // update target name input
    const nameInput = document.getElementById('mbti-target-name');
    if (nameInput) {
        if (currentMbtiTarget === 'pet') {
            nameInput.value = (typeof pets !== 'undefined' && pets.length > 0) ? getSajuPet()?.name : "";
        } else {
            nameInput.value = (typeof settings_nickname !== 'undefined') ? settings_nickname : "";
        }
    }
}

function startMbtiTestStepper() {
    let name = document.getElementById('mbti-target-name').value || (currentMbtiTarget === 'pet' ? '마이펫' : '집사');
    
    currentMbtiQuestions = currentMbtiTarget === 'pet' ? MBTI_QUESTIONS_PET : MBTI_QUESTIONS_OWNER;
    mbtiCurrentStep = 0;
    mbtiScores = { E:0, I:0, S:0, N:0, T:0, F:0, J:0, P:0 };
    
    document.getElementById('mbti-start-screen').classList.add('hidden');
    document.getElementById('mbti-stepper-screen').classList.remove('hidden');
    
    renderMbtiQuestion();
}

function renderMbtiQuestion() {
    let total = currentMbtiQuestions.length;
    let percent = Math.round(((mbtiCurrentStep + 1) / total) * 100);
    
    document.getElementById('mbti-progress-text').innerText = `질문 ${mbtiCurrentStep + 1} / ${total}`;
    const progressBar = document.getElementById('mbti-progress-bar');
    if(progressBar) progressBar.style.width = `${percent}%`;
    
    let qData = currentMbtiQuestions[mbtiCurrentStep];
    document.getElementById('mbti-question-title').innerText = qData.q;
    
    let optionsHtml = '';
    qData.a.forEach((opt, idx) => {
        optionsHtml += `<button onclick="handleMbtiAnswer('${opt.type}')" class="w-full bg-white border border-pink-200 hover:bg-pink-50 text-gray-700 font-bold py-3 px-4 rounded-xl text-xs text-left transition-all active:scale-95 shadow-sm">${opt.text}</button>`;
    });
    
    document.getElementById('mbti-options-container').innerHTML = optionsHtml;
}

function handleMbtiAnswer(type) {
    mbtiScores[type]++;
    mbtiCurrentStep++;
    
    if (mbtiCurrentStep < currentMbtiQuestions.length) {
        renderMbtiQuestion();
    } else {
        finishMbtiTest();
    }
}

function finishMbtiTest() {
    const grid = document.getElementById('mbti-test-grid');
    if (grid) grid.classList.add('hidden');
    const stepper = document.getElementById('mbti-stepper-screen');
    if (stepper) stepper.classList.add('hidden');
    
    const loading = document.getElementById('mbti-loading-container');
    if (loading) loading.classList.remove('hidden');
    
    setTimeout(() => {
        if (loading) loading.classList.add('hidden');
        
        const resultContainer = document.getElementById('mbti-result-container');
        if (resultContainer) resultContainer.classList.remove('hidden');
        
        // MBTI 산출
        let resType = "";
        resType += (mbtiScores.E >= mbtiScores.I) ? "E" : "I";
        resType += (mbtiScores.N >= mbtiScores.S) ? "N" : "S";
        resType += (mbtiScores.F >= mbtiScores.T) ? "F" : "T";
        resType += (mbtiScores.P >= mbtiScores.J) ? "P" : "J";
        
        let title = "";
        let desc = "";
        
        const badgeEl = document.getElementById('mbti-res-badge');
        
        if (currentMbtiTarget === 'pet') {
            savedPetMbti = resType;
            if (typeof pets !== 'undefined' && pets.length > 0) {
                getSajuPet().mbtiCode = resType;
                if (typeof saveState === 'function') saveState();
            }
            if (badgeEl) badgeEl.innerText = "PET P-MBTI";
            
            if (resType === "ENFP") { title = "천진난만 힐링 요정"; desc = "세상 모든 것이 신기하고 즐거운 에너지 메이커입니다."; }
            else if (resType === "ISTJ") { title = "원칙주의 선비냥멍"; desc = "규칙적인 루틴을 사랑하며 얌전하고 점잖은 성격입니다."; }
            else if (resType === "ENTP") { title = "호기심 대마왕 말썽꾸러기"; desc = "새로운 사고를 치는 데 천부적인 재능이 있습니다."; }
            else if (resType === "ISFJ") { title = "집사바라기 수호천사"; desc = "주인 곁을 묵묵히 지키는 충성스럽고 따뜻한 아이입니다."; }
            else { title = "자유로운 마이웨이"; desc = "독특한 개성을 가진 4차원 매력의 소유자입니다."; }
        } else {
            savedOwnerMbti = resType;
            const email = (typeof settings_email !== 'undefined') ? settings_email : 'butler@petna.co.kr';
            localStorage.setItem(`petna_owner_mbti_${email}`, resType);
            if (badgeEl) badgeEl.innerText = "OWNER MBTI";
            
            if (resType === "ENFJ") { title = "다정다감 펫 인플루언서"; desc = "펫과의 일상을 공유하고 사랑을 듬뿍 주는 열정 집사입니다."; }
            else if (resType === "ISTP") { title = "츤데레 실용주의 집사"; desc = "겉으론 무뚝뚝해 보여도 펫에게 필요한 건 다 챙겨줍니다."; }
            else if (resType === "INFP") { title = "펫과 영혼의 교감을 나누는 몽상가"; desc = "동물의 마음에 깊이 공감하는 감수성 풍부한 집사입니다."; }
            else if (resType === "ESTJ") { title = "칼각 펫 매니저"; desc = "산책, 식사, 영양제 등 펫의 스케줄을 철저하게 관리합니다."; }
            else { title = "개성 넘치는 자유로운 영혼의 집사"; desc = "펫과 함께 매일매일 새로운 추억을 만들어가는 스타일입니다."; }
        }
        
        const scoreEl = document.getElementById('mbti-res-score');
        if (scoreEl) scoreEl.innerText = resType;
        
        const titleEl = document.getElementById('mbti-res-title');
        if (titleEl) titleEl.innerText = '"' + title + '"';
        
        const descEl = document.getElementById('mbti-res-desc');
        if (descEl) descEl.innerText = desc;
        
    }, 1500);
}


function resetHarmonyTest() {
    const check = document.getElementById('harmony-check-container');
    if (check) check.classList.remove('hidden');
    const result = document.getElementById('harmony-result-container');
    if (result) result.classList.add('hidden');
    if (typeof showToast === 'function') showToast('다시 측정할 수 있습니다 💞');
}

function generateHarmonyReport() {
    const checkContainer = document.getElementById('harmony-check-container');
    if (checkContainer) checkContainer.classList.add('hidden');
    
    const resContainer = document.getElementById('harmony-result-container');
    if (resContainer) resContainer.classList.remove('hidden');
    if (typeof PetHarmonyView !== 'undefined') PetHarmonyView.enrichOnMeasure();

    // 1. 사주 궁합 점수 (Real score check)
    let sajuScore = 0;
    if (typeof savedSajuScore !== 'undefined' && savedSajuScore > 0) {
        sajuScore = savedSajuScore;
    } else if (typeof pets !== 'undefined' && getSajuPet() && getSajuPet().sajuData) {
        sajuScore = getSajuPet()?.sajuData.compatScore || 0;
    }
    
    // 2. 인지/지능 조화도 점수 (Real check)
    let iqScore = 0;
    if (savedPetIqScore > 0 && savedOwnerIqScore > 0) {
        iqScore = Math.round((savedPetIqScore + savedOwnerIqScore) / 2);
    } else if (savedPetIqScore > 0 || savedOwnerIqScore > 0) {
        iqScore = Math.round((savedPetIqScore || savedOwnerIqScore) * 0.8); // partial penalty
    }
    
    // 3. MBTI 성향 조화도 점수 (Real check & matching algorithm)
    let mbtiScore = 0;
    if (savedPetMbti && savedOwnerMbti) {
        let matchScore = 20; // base score
        const pet = savedPetMbti.toUpperCase();
        const owner = savedOwnerMbti.toUpperCase();
        
        // E-I (Complementary or same)
        if (pet[0] === owner[0]) matchScore += 20; // same
        else matchScore += 15; // complementary
        
        // N-S
        if (pet[1] === owner[1]) matchScore += 20;
        else matchScore += 10;
        
        // T-F (F-F gets extra empathy bonus)
        if (pet[2] === owner[2]) {
            matchScore += (pet[2] === 'F') ? 25 : 20;
        } else {
            matchScore += 10;
        }
        
        // J-P
        if (pet[3] === owner[3]) matchScore += 15;
        else matchScore += 15;
        
        mbtiScore = Math.min(100, matchScore);
    }
    
    const scoreSajuEl = document.getElementById('harmony-score-saju');
    const scoreIqEl = document.getElementById('harmony-score-iq');
    const scoreMbtiEl = document.getElementById('harmony-score-mbti');
    
    // Populate score cards with status badges
    if (scoreSajuEl) {
        scoreSajuEl.innerHTML = sajuScore > 0 ? `<span class="text-brand-600 font-extrabold font-mono">${sajuScore}%</span>` : `<span class="text-rose-500 font-bold">미진단 ⚠️</span>`;
    }
    if (scoreIqEl) {
        scoreIqEl.innerHTML = iqScore > 0 ? `<span class="text-sky-600 font-extrabold font-mono">${iqScore}%</span>` : `<span class="text-rose-500 font-bold">미진단 ⚠️</span>`;
    }
    if (scoreMbtiEl) {
        scoreMbtiEl.innerHTML = mbtiScore > 0 ? `<span class="text-pink-600 font-extrabold font-mono">${mbtiScore}%</span>` : `<span class="text-rose-500 font-bold">미진단 ⚠️</span>`;
    }
    
    // Calculate overall average based on completed tests only
    const completedScores = [sajuScore, iqScore, mbtiScore].filter(s => s > 0);
    let avgScore = 0;
    
    if (completedScores.length > 0) {
        avgScore = Math.floor(completedScores.reduce((a, b) => a + b, 0) / completedScores.length);
    }
    
    let level = 1;
    let title = "";
    let solution = "";
    
    if (completedScores.length === 0) {
        level = 0;
        title = "진단 대기 중 💤";
        solution = "사주 분석, 지능 테스트, MBTI 진단 중 단 하나도 진행되지 않았습니다. 상단 탭을 눌러 진단을 먼저 수행해주시면 신비로운 영혼 조화 분석 리포트가 완성됩니다!";
    } else {
        if (avgScore >= 90) {
            level = 5;
            title = "영혼의 단짝, 완벽한 듀오!";
            solution = "당신과 펫은 전생에서부터 이어진 완벽한 인연입니다. 말하지 않아도 통하는 교감 수준에 도달했습니다. 지금처럼 서로를 아끼며 최고의 라이프스타일을 유지하세요.";
        } else if (avgScore >= 80) {
            level = 4;
            title = "찰떡궁합 베스트 프렌드";
            solution = "서로의 다름을 이해하고 맞춰나가는 훌륭한 관계입니다. 가끔은 펫의 엉뚱한 행동도 귀엽게 넘어갈 수 있는 여유가 생겼군요! 조금만 더 교감 시간을 늘리면 완벽합니다.";
        } else if (avgScore >= 70) {
            level = 3;
            title = "평범하지만 따뜻한 가족";
            solution = "보통의 펫과 주인이 겪는 일상적인 티격태격이 있는 평범한 관계입니다. MBTI 성향이 조금 다를 수 있으니, 펫이 좋아하는 산책이나 놀이 스타일을 다시 한번 점검해 보세요.";
        } else if (avgScore >= 60) {
            level = 2;
            title = "조금은 엇갈리는 동거인";
            solution = "펫과 당신의 생활 패턴이나 성향이 약간 어긋나고 있습니다. 내가 좋다고 생각하는 것이 펫에게는 스트레스가 될 수도 있으니, 펫의 바디랭귀지를 세심하게 관찰하는 노력이 필요합니다.";
        } else {
            level = 1;
            title = "견원지간... 노력이 필요해요!";
            solution = "현재 펫 and 당신은 서로를 이해하는 데 어려움을 겪고 있습니다. 성향 차이가 크므로 기초적인 교감 훈련부터 다시 차근차근 시작해보는 것을 권장합니다.";
        }
        
        // Append tip if some tests are still missing
        if (completedScores.length < 3) {
            solution += "\n\n⚠️ (안내) 아직 완료되지 않은 진단이 있습니다. 사주, 지능, MBTI를 모두 진단해 주시면 더 신뢰성 높은 종합 영혼 조화도를 얻으실 수 있습니다!";
        }
    }
    
    const resLevelEl = document.getElementById('harmony-res-level');
    if (resLevelEl) resLevelEl.innerText = level > 0 ? `${level}단계` : "대기";
    
    const resTitleEl = document.getElementById('harmony-res-title');
    if (resTitleEl) resTitleEl.innerText = "\"" + title + "\"";
    
    const resSolutionEl = document.getElementById('harmony-res-solution');
    if (resSolutionEl) resSolutionEl.innerText = solution;

    const harmonyData = {
        level: level,
        title: title,
        solution: solution,
        sajuScore: sajuScore,
        iqScore: iqScore,
        mbtiScore: mbtiScore,
        avgScore: avgScore
    };

    if (typeof AppStore !== 'undefined') {
        AppStore.setState('harmonyResult', harmonyData);
    }

    if (typeof pets !== 'undefined' && pets.length > 0) {
        const sp = getSajuPet();
        sp.harmonyData = Object.assign({}, sp.harmonyData, harmonyData);
        if (typeof saveState === 'function') saveState();
        if (typeof updatePetInSupabase === 'function') {
            try { updatePetInSupabase(getSajuPet()); } catch(e) {}
        }
        if (typeof PetHarmonyView !== 'undefined') PetHarmonyView.renderPlus('harmony-plus', sp.harmonyData);
    }
}


let arcadeSpawnTimer = null;
let arcadeScore = 0;
let arcadeLives = 3;
let arcadeBestScore = parseInt(localStorage.getItem('petna_arcade_highscore') || '0');
let gameActive = false;
let currentSpawnRate = 800; // 초기 스폰 속도 (ms)

function updateLivesDisplay() {
    const livesDisplay = document.getElementById('arcade-lives-display');
    if(livesDisplay) {
        const hearts = Math.max(0, arcadeLives);
        const broken = Math.max(0, 3 - hearts);
        livesDisplay.innerText = '❤️'.repeat(hearts) + '💔'.repeat(broken);
    }
}

function startArcadeGame() {
    const startOverlay = document.getElementById('arcade-start-overlay');
    const gameoverOverlay = document.getElementById('arcade-gameover-overlay');
    if(startOverlay) startOverlay.classList.add('hidden');
    if(gameoverOverlay) gameoverOverlay.classList.add('hidden');
    
    // Load best score
    arcadeBestScore = parseInt(localStorage.getItem('petna_arcade_highscore') || '0');
    const startBest = document.getElementById('arcade-start-best');
    if (startBest) startBest.innerText = arcadeBestScore;

    arcadeScore = 0;
    arcadeLives = 3;
    gameActive = true;
    currentSpawnRate = 1400; // 난이도 초기화 (완화)
    
    document.getElementById('arcade-score-display').innerText = arcadeScore;
    updateLivesDisplay();

    // innerHTML 대신 game item만 선택 제거 — overlay DOM 노드 보존
    const playArea = document.getElementById('arcade-play-area');
    Array.from(playArea.children).forEach(child => {
        if (child.id !== 'arcade-start-overlay' && child.id !== 'arcade-gameover-overlay') {
            playArea.removeChild(child);
        }
    });

    if(arcadeSpawnTimer) clearTimeout(arcadeSpawnTimer);
    
    // 첫 아이템 스폰 시작
    scheduleNextSpawn();
}

function scheduleNextSpawn() {
    if(!gameActive) return;
    
    spawnArcadeItem();
    
    // 점수에 비례하여 스폰 속도 증가 (최소 600ms — 완화)
    currentSpawnRate = Math.max(600, 1400 - (arcadeScore * 3));
    
    arcadeSpawnTimer = setTimeout(scheduleNextSpawn, currentSpawnRate);
}

function spawnArcadeItem() {
    if(!gameActive) return;
    
    const playArea = document.getElementById('arcade-play-area');
    if(!playArea) return;

    const item = document.createElement('div');
    
    const isSpecial = Math.random() > 0.8;
    const emoji = isSpecial ? '🍖' : '🦴';
    const points = isSpecial ? 5 : 1;
    const size = isSpecial ? 'text-5xl' : 'text-4xl'; // 더 크게 — 터치 쉽게

    item.className = `absolute cursor-pointer select-none hover:scale-125 ${size} p-2 z-10`; // p-2로 터치 영역 확보, z-10으로 오버레이 위에 표시
    item.innerText = emoji;
    
    // Random position
    const left = Math.random() * 80; // 0 to 80%
    item.style.left = left + '%';
    item.style.top = '-12%';

    let speedMultiplier = Math.max(0.65, 1.0 - (arcadeScore / 400));
    const duration = (Math.random() * 2000 + 3000) * speedMultiplier;

    item.clicked = false;
    playArea.appendChild(item);

    // 한 프레임 뒤에 transition 설정 후 낙하 시작 (단위 % 통일)
    requestAnimationFrame(() => {
        requestAnimationFrame(() => {
            item.style.transition = `top ${duration}ms linear`;
            item.style.top = '112%';
        });
    });
    
    // Touch event - 개선된 이벤트 핸들러
    const handleClick = (e) => {
        e.preventDefault();
        e.stopPropagation();
        if(!gameActive || item.clicked) return;

        item.clicked = true;
        arcadeScore += points;
        const scoreDisplay = document.getElementById('arcade-score-display');
        if(scoreDisplay) scoreDisplay.innerText = arcadeScore;

        // Pop effect
        item.innerText = '✨';
        item.style.transition = 'transform 0.2s, opacity 0.2s';
        item.classList.add('scale-150', 'opacity-0');

        setTimeout(() => {
            if(item.parentNode) item.parentNode.removeChild(item);
        }, 200);
    };

    // 마우스와 터치 이벤트 모두 지원
    item.addEventListener('mousedown', handleClick);
    item.addEventListener('touchstart', handleClick, { passive: false });
    item.addEventListener('click', handleClick);
    
    // Cleanup after fall (Missed item logic)
    setTimeout(() => {
        if(!item.clicked && gameActive) {
            // 놓쳤을 경우 라이프 감소
            arcadeLives = Math.max(0, arcadeLives - 1);
            updateLivesDisplay();
            
            if(arcadeLives <= 0) {
                endArcadeGame();
                return;
            }
        }
        if(item.parentNode) {
            try { item.parentNode.removeChild(item); } catch(e) {}
        }
    }, duration + 100);
}

function endArcadeGame() {
    gameActive = false;
    if(arcadeSpawnTimer) clearTimeout(arcadeSpawnTimer);
    
    // 최고 점수 갱신
    if(arcadeScore > arcadeBestScore) {
        arcadeBestScore = arcadeScore;
        localStorage.setItem('petna_arcade_highscore', arcadeBestScore.toString());
    }
    
    const finalScore = document.getElementById('arcade-final-score');
    if(finalScore) finalScore.innerText = arcadeScore;
    
    const bestScoreDisplay = document.getElementById('arcade-best-score');
    if(bestScoreDisplay) bestScoreDisplay.innerText = arcadeBestScore;
    
    const overlay = document.getElementById('arcade-gameover-overlay');
    if(overlay) overlay.classList.remove('hidden');
}



function switchSajuSubTab(tabName) {
    if (typeof loadSajuVariables === 'function') {
        loadSajuVariables();
    }
    // Hide all sections
    const sections = ['saju-main-section', 'fortune-test-section', 'iq-test-section', 'mbti-test-section', 'harmony-test-section', 'arcade-test-section'];
    sections.forEach(id => {
        const el = document.getElementById(id);
        if (el) el.classList.add('hidden');
    });

    // Reset all buttons
    const btnIds = ['saju-tab-saju', 'saju-tab-fortune', 'saju-tab-petIq', 'saju-tab-ownerIq', 'saju-tab-mbti', 'saju-tab-harmony', 'saju-tab-arcade'];
    btnIds.forEach(id => {
        const btn = document.getElementById(id);
        if (btn) {
            btn.className = 'saju-subtab-btn whitespace-nowrap bg-white text-gray-500 font-bold text-xs py-2.5 px-4 rounded-xl border border-gray-200 transition-all hover:bg-gray-50';
        }
    });

    // Show target section & set test mode
    let targetSectionId = '';
    
    if (tabName === 'saju') targetSectionId = 'saju-main-section';
    if (tabName === 'fortune') targetSectionId = 'fortune-test-section';
    if (tabName === 'harmony') targetSectionId = 'harmony-test-section';
    if (tabName === 'arcade') targetSectionId = 'arcade-test-section';
    
    if (tabName === 'petIq') {
        targetSectionId = 'iq-test-section';
        switchIqMode('pet');
        resetIqTest();
    }
    if (tabName === 'ownerIq') {
        targetSectionId = 'iq-test-section';
        switchIqMode('owner');
        resetIqTest();
    }
    if (tabName === 'mbti') {
        targetSectionId = 'mbti-test-section';
        switchMbtiMode(currentMbtiTarget || 'pet');
    }
    
    const targetSection = document.getElementById(targetSectionId);
    if (targetSection) targetSection.classList.remove('hidden');

    if (tabName === 'harmony' && typeof PetHarmonyView !== 'undefined') {
        const p = (typeof getSajuPet === 'function') ? getSajuPet() : null;
        PetHarmonyView.renderPlus('harmony-plus', p && p.harmonyData);
    }

    // Highlight target button
    const activeBtn = document.getElementById('saju-tab-' + tabName);
    if (activeBtn) {
        activeBtn.classList.remove('bg-white', 'text-gray-500', 'border', 'border-gray-200');
        activeBtn.classList.add('text-white', 'shadow-sm', 'border-transparent');
        
        if (tabName === 'saju') activeBtn.classList.add('bg-brand-500');
        if (tabName === 'fortune') activeBtn.classList.add('bg-emerald-500');
        if (tabName === 'petIq' || tabName === 'ownerIq') activeBtn.classList.add('bg-sky-500');
        if (tabName === 'mbti') activeBtn.classList.add('bg-pink-500');
        if (tabName === 'harmony') activeBtn.classList.add('bg-rose-500');
        if (tabName === 'arcade') activeBtn.classList.add('bg-brand-500');
    }
}
