const PET_MBTI_SPEECHES = {
    "ENFP": "집사님! 우리 오늘 새로운 산책길 가볼까요? 너무 신나요! 🌟🐾",
    "ENFJ": "우리 집사님 오늘도 고생 많았어요! 제가 늘 곁에 있을게요 💖🐶",
    "ENTP": "집사님, 울타리 밖 세상은 어떤가요? 저기 간식 서랍 비밀번호가 뭐예요? 😼💡",
    "ENTJ": "오늘의 놀이와 간식 스케줄은 완벽히 수행되고 있나 보군! 만족스럽다 👑🐾",
    "ESFP": "간식 소리인가요?! 어서 저와 함께 신나게 놀아주세요! 🥳🥎",
    "ESFJ": "집사님 기분이 안 좋아 보여요... 제 애교를 보며 힘내세요! 🍭💕",
    "ESTP": "몸이 근질근질해요! 진흙탕이든 잔디밭이든 어디든 달려가요! ⚡🏃",
    "ESTJ": "약속된 간식 시간이 5분 지났습니다, 집사님. 어서 루틴을 지켜주세요! ⏰🐾",
    "INFP": "집사님과 둘이 조용히 있는 이 공간이 세상에서 제일 행복해요... 🌸💤",
    "INFJ": "말하지 않아도 알아요. 집사님이 날 얼마나 사랑하는지, 그리고 오늘 좀 피곤하다는 것도요... 🔮✨",
    "INTP": "음... 저기 기어가는 개미는 무슨 생각을 하고 있을까요? 멍... 💭🐱",
    "INTJ": "집사님이 숨긴 간식을 찾는 건 생각보다 간단한 연역적 추리였어요 🕵️‍♂️🔍",
    "ISFP": "이불 속이 제일 따뜻하고 편해요. 집사님, 저랑 같이 누워서 낮잠 자요... 🛌🐾",
    "ISFJ": "집사님을 지켜보는 게 제 가장 큰 기쁨이에요. 언제나 든든히 지켜줄게요 🛡️🐕",
    "ISTP": "혼자 노는 것도 재미있지만, 집사님이 쓰다듬어 주면 꽤 기분 좋아요 ⚙️🐾",
    "ISTJ": "배변 패드 중앙 조준 완료! 오늘의 할 일을 정확히 마쳤습니다 📝🐾"
};

let activePetIndex = 0;
const ROOM_LAYOUTS = {
    living: {
        id: 'living',
        label: '거실형',
        iconClass: 'fa-solid fa-house',
        decorDensity: 'calm',
        connectionStyle: 'subtle',
        butlerClearance: 14,
        petClearance: 8,
        nodeSize: 4,
        toast: '거실형 방으로 바꿨어요. 차분하게 쉬는 느낌이에요 🏠'
    },
    circle: {
        id: 'circle',
        label: '교감형',
        iconClass: 'fa-solid fa-circle-nodes',
        decorDensity: 'balanced',
        connectionStyle: 'expressive',
        butlerClearance: 12,
        petClearance: 7,
        nodeSize: 5,
        toast: '교감형 방으로 바꿨어요. 모두가 둥글게 이어져요 ✨'
    }
};

function getActivePet() {
    if (!pets || pets.length === 0) return null;
    return pets[Math.min(activePetIndex, pets.length - 1)];
}

function setActivePet(idx) {
    activePetIndex = idx;
    renderPetStageList();
    renderMyPets();
}

function normalizeRoomLayout(layout) {
    return ROOM_LAYOUTS[layout] ? layout : 'living';
}

function getRoomLayoutPreset(layout = 'living') {
    return ROOM_LAYOUTS[normalizeRoomLayout(layout)];
}

function getActiveRoomLayout() {
    const current = getActivePet();
    return normalizeRoomLayout(current?.roomLayout || 'living');
}

function updateRoomLayoutControls(layout = getActiveRoomLayout()) {
    if (typeof document === 'undefined') return;
    Object.keys(ROOM_LAYOUTS).forEach((layoutId) => {
        const btn = document.getElementById(`room-layout-${layoutId}`);
        if (!btn) return;
        const isActive = layoutId === layout;
        btn.classList.toggle('is-active', isActive);
        btn.setAttribute('aria-pressed', isActive ? 'true' : 'false');
    });

    const badge = document.getElementById('room-layout-badge');
    const badgeText = document.getElementById('room-layout-badge-text');
    if (badge) {
        const icon = badge.querySelector ? badge.querySelector('i') : null;
        if (icon) icon.className = `${ROOM_LAYOUTS[layout].iconClass} text-[9px]`;
        badge.className = `room-layout-badge is-${layout}`;
    }
    if (badgeText) badgeText.textContent = ROOM_LAYOUTS[layout].label;
}

function applyRoomLayoutVisualState(layout = getActiveRoomLayout()) {
    if (typeof document === 'undefined') return;
    const stage = document.querySelector ? document.querySelector('.room-stage') : null;
    if (!stage) return;
    Object.keys(ROOM_LAYOUTS).forEach(layoutId => stage.classList.remove(`room-layout-${layoutId}`));
    stage.classList.add(`room-layout-${layout}`);
    updateRoomLayoutControls(layout);
}

function setRoomLayoutForActivePet(layout) {
    const current = getActivePet();
    if (!current) return;
    const nextLayout = normalizeRoomLayout(layout);
    current.roomLayout = nextLayout;
    if (typeof saveState === 'function') saveState();
    if (typeof updatePetInSupabase === 'function') updatePetInSupabase(current);
    applyRoomLayoutVisualState(nextLayout);
    renderPetStageList();
    if (typeof showToast === 'function') showToast(ROOM_LAYOUTS[nextLayout].toast);
}

function clampPetStagePoint(point, isMobile) {
    const marginX = isMobile ? 10 : 8;
    const marginTop = isMobile ? 20 : 17;
    const marginBottom = isMobile ? 88 : 90;
    return {
        x: Math.max(marginX, Math.min(100 - marginX, point.x)),
        y: Math.max(marginTop, Math.min(marginBottom, point.y))
    };
}

function getPetStageSlots(count, isMobile, layout = 'living') {
    if (normalizeRoomLayout(layout) === 'living') {
        // 집사가 y:42%에 위치하므로, 펫은 하단(60~85%)과 상단(15~28%), 양옆(10~20%)으로 분산
        const livingRoomySlots = [
            { x: 28, y: 68 }, { x: 72, y: 68 },  // 집사 양옆 아래
            { x: 50, y: 82 }, { x: 15, y: 38 },   // 정하단, 왼쪽
            { x: 85, y: 38 }, { x: 28, y: 18 },   // 오른쪽, 왼쪽 상단
            { x: 72, y: 18 }, { x: 10, y: 72 },   // 오른쪽 상단, 좌하단
            { x: 90, y: 72 }, { x: 50, y: 14 }    // 우하단, 정상단
        ];
        const livingCompactSlots = [
            { x: 26, y: 72 }, { x: 74, y: 72 },
            { x: 50, y: 85 }, { x: 13, y: 44 },
            { x: 87, y: 44 }, { x: 26, y: 20 },
            { x: 74, y: 20 }, { x: 10, y: 76 },
            { x: 90, y: 76 }, { x: 50, y: 16 },
            { x: 10, y: 30 }, { x: 90, y: 30 }
        ];
        const livingSlots = isMobile ? livingCompactSlots : livingRoomySlots;
        if (count === 1) return [{ x: isMobile ? 74 : 72, y: isMobile ? 74 : 70 }];
        if (count <= livingSlots.length) return livingSlots.slice(0, count);
        return livingSlots.concat(getPetStageSlots(count - livingSlots.length, isMobile, 'circle'));
    }

    const roomySlots = [
        { x: 22, y: 20 }, { x: 78, y: 20 },
        { x: 15, y: 48 }, { x: 85, y: 48 },
        { x: 28, y: 78 }, { x: 72, y: 78 },
        { x: 10, y: 72 }, { x: 90, y: 72 },
        { x: 38, y: 18 }, { x: 62, y: 18 }
    ];
    const compactSlots = [
        { x: 22, y: 20 }, { x: 78, y: 20 },
        { x: 12, y: 48 }, { x: 88, y: 48 },
        { x: 26, y: 80 }, { x: 74, y: 80 },
        { x: 46, y: 18 }, { x: 54, y: 85 },
        { x: 10, y: 74 }, { x: 90, y: 74 },
        { x: 10, y: 28 }, { x: 90, y: 28 }
    ];
    const slots = isMobile ? compactSlots : roomySlots;

    if (count === 1) return [{ x: isMobile ? 76 : 74, y: isMobile ? 74 : 70 }];
    if (count === 2) return [slots[0], slots[1]];
    if (count === 3) return [slots[0], slots[1], { x: 50, y: 82 }];
    if (count === 4) return [slots[0], slots[1], slots[2], slots[3]];
    if (count <= slots.length) return slots.slice(0, count);

    const generated = slots.slice();
    const extra = count - slots.length;
    const expansionSlots = isMobile
        ? [{ x: 12, y: 38 }, { x: 88, y: 38 }, { x: 50, y: 90 }]
        : [{ x: 8, y: 36 }, { x: 92, y: 36 }, { x: 50, y: 90 }];
    for (let i = 0; i < extra; i++) {
        if (expansionSlots[i]) {
            generated.push(expansionSlots[i]);
            continue;
        }
        const angle = (Math.PI * 2 * i) / extra - Math.PI / 2;
        generated.push({
            x: 50 + Math.cos(angle) * (isMobile ? 34 : 39),
            y: 54 + Math.sin(angle) * (isMobile ? 31 : 34)
        });
    }
    return generated;
}

function resolvePetStageCollisions(points, isMobile) {
    const count = points.length;
    const minDx = isMobile ? (count > 8 ? 13 : 16) : (count > 10 ? 13 : 15);
    const minDy = isMobile ? (count > 8 ? 15 : 18) : (count > 10 ? 15 : 17);
    const butler = { x: 50, y: 42 };
    const butlerSafeX = isMobile ? 24 : 20;
    const butlerSafeY = isMobile ? 25 : 22;
    const relaxed = points.map(point => clampPetStagePoint(point, isMobile));

    for (let pass = 0; pass < 7; pass++) {
        for (let i = 0; i < relaxed.length; i++) {
            for (let j = i + 1; j < relaxed.length; j++) {
                const a = relaxed[i];
                const b = relaxed[j];
                const dx = b.x - a.x;
                const dy = b.y - a.y;
                if (Math.abs(dx) < minDx && Math.abs(dy) < minDy) {
                    const pushX = ((minDx - Math.abs(dx)) / 2) * (dx >= 0 ? 1 : -1);
                    const pushY = ((minDy - Math.abs(dy)) / 2) * (dy >= 0 ? 1 : -1);
                    a.x -= pushX;
                    b.x += pushX;
                    a.y -= pushY * 0.35;
                    b.y += pushY * 0.35;
                }
            }

            const p = relaxed[i];
            const fromButlerX = p.x - butler.x;
            const fromButlerY = p.y - butler.y;
            if (Math.abs(fromButlerX) < butlerSafeX && Math.abs(fromButlerY) < butlerSafeY) {
                const angle = Math.atan2(fromButlerY || 1, fromButlerX || (i % 2 ? 1 : -1));
                p.x = butler.x + Math.cos(angle) * butlerSafeX;
                p.y = butler.y + Math.sin(angle) * butlerSafeY;
            }

            relaxed[i] = clampPetStagePoint(p, isMobile);
        }
    }

    return relaxed.map((point, idx) => {
        const fromButlerX = point.x - butler.x;
        const fromButlerY = point.y - butler.y;
        if (Math.abs(fromButlerX) >= butlerSafeX || Math.abs(fromButlerY) >= butlerSafeY) {
            return clampPetStagePoint(point, isMobile);
        }
        const signX = fromButlerX >= 0 ? 1 : -1;
        const signY = fromButlerY >= 0 ? 1 : -1;
        const xPressure = Math.abs(fromButlerX) / butlerSafeX;
        const yPressure = Math.abs(fromButlerY) / butlerSafeY;
        const corrected = { ...point };
        if (yPressure >= xPressure) {
            corrected.y = butler.y + signY * (butlerSafeY + 3);
        } else {
            corrected.x = butler.x + signX * (butlerSafeX + 3);
        }
        return clampPetStagePoint(corrected, isMobile);
    });
}

function createRoomConnection(svg, butlerPoint, petPoint, isActive, idx, layout = getActiveRoomLayout()) {
    const svgNs = 'http://www.w3.org/2000/svg';
    const preset = getRoomLayoutPreset(layout);
    const dx = petPoint.x - butlerPoint.x;
    const dy = petPoint.y - butlerPoint.y;
    const length = Math.hypot(dx, dy) || 1;
    const unitX = dx / length;
    const unitY = dy / length;
    const start = {
        x: butlerPoint.x + unitX * preset.butlerClearance,
        y: butlerPoint.y + unitY * preset.butlerClearance
    };
    const end = {
        x: petPoint.x - unitX * preset.petClearance,
        y: petPoint.y - unitY * preset.petClearance
    };
    const bendBase = preset.connectionStyle === 'subtle' ? 3 : 5;
    const bend = (idx % 2 === 0 ? 1 : -1) * (isActive ? bendBase + 1 : bendBase);
    const control = {
        x: (start.x + end.x) / 2 + (-unitY * bend),
        y: (start.y + end.y) / 2 + (unitX * bend) - 2
    };
    const d = `M ${start.x.toFixed(2)} ${start.y.toFixed(2)} Q ${control.x.toFixed(2)} ${control.y.toFixed(2)} ${end.x.toFixed(2)} ${end.y.toFixed(2)}`;

    const glow = document.createElementNS(svgNs, 'path');
    glow.setAttribute('d', d);
    glow.setAttribute('class', `room-connection-glow ${isActive ? 'is-active' : 'is-muted'}`);
    svg.appendChild(glow);

    const line = document.createElementNS(svgNs, 'path');
    line.setAttribute('d', d);
    line.setAttribute('class', `room-connection-line ${isActive ? 'is-active' : 'is-muted'}`);
    line.style.setProperty('--connection-order', idx);
    svg.appendChild(line);

    const nodeX = start.x * 0.48 + end.x * 0.52;
    const nodeY = start.y * 0.48 + end.y * 0.52;
    const node = document.createElementNS(svgNs, 'foreignObject');
    node.setAttribute('x', (nodeX - preset.nodeSize / 2).toFixed(2));
    node.setAttribute('y', (nodeY - preset.nodeSize / 2).toFixed(2));
    node.setAttribute('width', String(preset.nodeSize));
    node.setAttribute('height', String(preset.nodeSize));
    node.setAttribute('class', `room-connection-node ${isActive ? 'is-active' : 'is-muted'}`);
    node.innerHTML = '<div class="room-connection-node-inner"></div>';
    svg.appendChild(node);

    return {
        start,
        end,
        node: { x: nodeX, y: nodeY, size: preset.nodeSize },
        butlerClearance: preset.butlerClearance,
        petClearance: preset.petClearance,
        nodeSize: preset.nodeSize
    };
}

function _getBingoSVG() {
    return `<img src="images/bingo_corgi.png" style="width:100%;height:100%;object-fit:contain;display:block;" alt="Bingo">`;
}

// ── 펫 위치 저장/로드 ──────────────────────────────────────────
function _petPosKey() {
    try {
        const uid = auth?.currentUser?.uid || 'local';
        return `petnna_pet_pos_${uid}`;
    } catch { return 'petnna_pet_pos_local'; }
}
function _loadPetPos(petId) {
    try { return JSON.parse(localStorage.getItem(_petPosKey()) || '{}')[petId] || null; }
    catch { return null; }
}
function _savePetPos(petId, x, y) {
    try {
        const all = JSON.parse(localStorage.getItem(_petPosKey()) || '{}');
        all[petId] = { x, y };
        localStorage.setItem(_petPosKey(), JSON.stringify(all));
    } catch {}
}

function _makePetDraggable(el, petId) {
    let sx, sy, sl, st, moved = false;
    el.addEventListener('pointerdown', e => {
        if (e.target.closest('button, span')) return;
        moved = false;
        el.setPointerCapture(e.pointerId);
        sx = e.clientX; sy = e.clientY;
        sl = parseFloat(el.style.left);
        st = parseFloat(el.style.top);
        el.style.cursor = 'grabbing';
        el.style.transition = 'none';
        e.stopPropagation();
        e.preventDefault();
    });
    el.addEventListener('pointermove', e => {
        if (!e.buttons) return;
        if (Math.abs(e.clientX - sx) < 4 && Math.abs(e.clientY - sy) < 4) return;
        moved = true;
        const rect = el.closest('.room-stage').getBoundingClientRect();
        const nx = Math.max(6, Math.min(94, sl + (e.clientX - sx) / rect.width * 100));
        const ny = Math.max(10, Math.min(85, st + (e.clientY - sy) / rect.height * 100));
        const ds = _depthScale(ny);
        el.style.left = `${nx}%`;
        el.style.top = `${ny}%`;
        el.style.transform = `translate(-50%, -100%) scale(${ds})`;
        el.style.zIndex = Math.round(ny) + 50;
    });
    el.addEventListener('pointerup', e => {
        el.style.cursor = 'grab';
        el.style.transition = '';
        el._moved = moved;
        if (!moved) return;
        const nx = parseFloat(el.style.left);
        const ny = parseFloat(el.style.top);
        el.style.zIndex = Math.round(ny) + 5;
        _savePetPos(petId, nx, ny);
        _redrawLeashes();
        setTimeout(() => { el._moved = false; }, 200);
    });
}

function _initButlerDrag() {
    const el = document.getElementById('butler-stage-wrapper');
    if (!el || el._dragInit) return;
    el._dragInit = true;

    // 저장된 위치 복원
    try {
        const saved = JSON.parse(localStorage.getItem('petnna_butler_pos') || 'null');
        if (saved) {
            el.style.left = `${saved.x}%`;
            el.style.top  = `${saved.y}%`;
            el.style.transform = `translate(-50%, -100%) scale(${_depthScale(saved.y).toFixed(3)})`;
        }
    } catch {}

    let sx, sy, sl, st, moved = false;
    el.addEventListener('pointerdown', e => {
        if (e.target.closest('input,button')) return;
        moved = false;
        el.setPointerCapture(e.pointerId);
        sx = e.clientX; sy = e.clientY;
        sl = parseFloat(el.style.left);
        st = parseFloat(el.style.top);
        el.style.cursor = 'grabbing';
        el.style.transition = 'none';
        e.stopPropagation();
        e.preventDefault();
    });
    el.addEventListener('pointermove', e => {
        if (!e.buttons) return;
        if (Math.abs(e.clientX - sx) < 4 && Math.abs(e.clientY - sy) < 4) return;
        moved = true;
        const rect = el.closest('.room-stage').getBoundingClientRect();
        const nx = Math.max(8, Math.min(92, sl + (e.clientX - sx) / rect.width  * 100));
        const ny = Math.max(15, Math.min(90, st + (e.clientY - sy) / rect.height * 100));
        el.style.left      = `${nx}%`;
        el.style.top       = `${ny}%`;
        el.style.transform = `translate(-50%,-100%) scale(${_depthScale(ny).toFixed(3)})`;
        el.style.zIndex    = Math.round(ny) + 50;
    });
    el.addEventListener('pointerup', () => {
        el.style.cursor = 'grab';
        el.style.transition = '';
        if (!moved) return;
        const nx = parseFloat(el.style.left);
        const ny = parseFloat(el.style.top);
        el.style.zIndex = Math.round(ny) + 10;
        localStorage.setItem('petnna_butler_pos', JSON.stringify({ x: nx, y: ny }));
        _redrawLeashes();
    });
}

function _redrawLeashes() {
    const svg = document.getElementById('leash-svg');
    if (!svg) return;
    svg.innerHTML = '';
    const butlerEl = document.getElementById('butler-stage-wrapper');
    const butlerX = butlerEl ? parseFloat(butlerEl.style.left) || 50 : 50;
    const butlerY = butlerEl ? parseFloat(butlerEl.style.top)  || 82 : 82;
    const layout = typeof getActiveRoomLayout === 'function' ? getActiveRoomLayout() : 'living';
    document.querySelectorAll('.pet-stage-wrapper').forEach((el, idx) => {
        const px = parseFloat(el.style.left);
        const py = parseFloat(el.style.top);
        const isActive = el.dataset.active === '1';
        createRoomConnection(svg, { x: butlerX, y: butlerY }, { x: px, y: py }, isActive, idx, layout);
    });
}

function renderPetStageList() {
    const list = document.getElementById('pet-stage-list');
    const svg = document.getElementById('leash-svg');
    if (!list) return;

    list.innerHTML = '';
    if (svg) svg.innerHTML = '';

    const count = pets ? pets.length : 0;
    if (count === 0) return;

    // 집사 위치 (%)
    const butlerX = 50;
    const butlerY = 82;

    // ── 펫 스프라이트 배치 ───────────────────────────────
    if (pets && pets.length > 0) {
        const isMobile = window.innerWidth < 768;
        const layout = getActiveRoomLayout();
        applyRoomLayoutVisualState(layout);
        const baseSlots = getPetStageSlots(count, isMobile, layout);
        const positions = resolvePetStageCollisions(baseSlots, isMobile);

        pets.forEach((pet, idx) => {
            const isActive = idx === activePetIndex;
            const slot = positions[idx];
            // 저장된 위치 우선, 없으면 기본 슬롯
            const saved = _loadPetPos(pet.id || pet.name || `pet_${idx}`);
            const finalX = saved ? saved.x : slot.x;
            const finalY = saved ? saved.y : slot.y;

            if (svg) createRoomConnection(svg, { x: butlerX, y: butlerY }, { x: finalX, y: finalY }, isActive, idx, layout);

            // 스프라이트 wrapper
            const wrapper = document.createElement('div');
            wrapper.className = 'pet-stage-wrapper absolute';
            wrapper.dataset.petIdx = idx;
            wrapper.dataset.active = isActive ? '1' : '0';
            wrapper.style.cssText = `left:${finalX}%;top:${finalY}%;transform:translate(-50%,-100%) scale(${_depthScale(finalY).toFixed(3)});z-index:${Math.round(finalY)+5};cursor:grab;`;

            // 스프라이트 이미지
            const spriteImg = document.createElement('img');
            if (pet.type === 'custom' && pet.imageUrl) {
                spriteImg.src = pet.imageUrl;
                spriteImg.style.cssText = 'width:72px;height:72px;object-fit:cover;border-radius:50%;filter:drop-shadow(0 4px 10px rgba(0,0,0,0.3));display:block;';
            } else {
                const dogBreeds = [
                    'images/bingo_corgi.png',
                    'images/pet_dog_golden.png',
                    'images/pet_dog_shiba.png',
                    'images/pet_dog_poodle.png',
                    'images/pet_dog_dalmatian.png',
                    'images/pet_dog_husky.png',
                    'images/pet_dog_pomeranian.png',
                ];
                const petImgMap = {
                    cat:     'images/pet_cat.png',
                    rabbit:  'images/pet_rabbit.png',
                    hamster: 'images/pet_hamster.png',
                };
                if (pet.type === 'dog' || !pet.type) {
                    // 펫 ID나 이름 해시로 품종 고정 배정 (같은 펫은 항상 같은 품종)
                    const seed = (pet.id || pet.name || '').split('').reduce((a, c) => a + c.charCodeAt(0), idx);
                    spriteImg.src = dogBreeds[seed % dogBreeds.length];
                } else {
                    spriteImg.src = petImgMap[pet.type] || 'images/bingo_corgi.png';
                }
                spriteImg.style.cssText = 'width:80px;height:80px;object-fit:contain;filter:drop-shadow(0 4px 10px rgba(0,0,0,0.25));display:block;';
            }
            spriteImg.id = isActive ? 'pet-graphic-container' : `pet-img-${idx}`;

            // 이름 태그
            const nameTag = document.createElement('span');
            nameTag.style.cssText = 'display:block;text-align:center;font-size:10px;font-weight:900;padding:1px 6px;border-radius:99px;border:1.5px solid;margin-top:2px;white-space:nowrap;';
            nameTag.style.color = isActive ? '#92400e' : '#6b7280';
            nameTag.style.background = isActive ? 'rgba(255,251,235,0.95)' : 'rgba(255,255,255,0.85)';
            nameTag.style.borderColor = isActive ? '#fcd34d' : '#e5e7eb';
            if (isActive) nameTag.id = 'pet-stage-name';
            nameTag.textContent = pet.name || '펫';

            const inner = document.createElement('div');
            inner.style.cssText = 'display:flex;flex-direction:column;align-items:center;gap:0;';
            if (isActive) {
                spriteImg.style.outline = '2.5px solid #f59e0b';
                spriteImg.style.outlineOffset = '2px';
                spriteImg.style.borderRadius = '12px';
                spriteImg.title = `${pet.name || '펫'} 사진 변경`;
                spriteImg.onclick = (e) => { e.stopPropagation(); triggerPetPhotoUploadDirect(); };
            }
            inner.appendChild(spriteImg);
            inner.appendChild(nameTag);
            wrapper.appendChild(inner);

            wrapper.addEventListener('click', (e) => { if (!wrapper._moved) setActivePet(idx); });
            _makePetDraggable(wrapper, pet.id || pet.name || `pet_${idx}`);
            list.appendChild(wrapper);
        });
    }
    renderRoomStickers();
    loadRoomTheme();
    setTimeout(_initButlerDrag, 0);
    const _pet = getActivePet();
    if (_pet) { runStatDecay(_pet); saveState(); }
    _applyUnlockedItems();
    _renderPetStatBars();
    _renderDailyMissions();
}


// ── 펫 케어 게임 루프 ──────────────────────────────────────────────────────────

function _ensurePetGameFields(pet) {
    if (pet == null) return;
    if (pet.pawCoins == null) pet.pawCoins = 0;
    if (pet.clean == null) pet.clean = 80;
    if (pet.lastStatDecay == null) pet.lastStatDecay = Date.now();
}

function _earnCoins(pet, amount, label) {
    if (!pet) return;
    _ensurePetGameFields(pet);
    pet.pawCoins = (pet.pawCoins || 0) + amount;
    showToast(`+${amount} 🐾코인 획득! (${label})`);
    _renderPetStatBars();
}

function runStatDecay(pet) {
    if (!pet) return;
    _ensurePetGameFields(pet);
    const now = Date.now();
    const elapsedHours = (now - pet.lastStatDecay) / 3600000;
    if (elapsedHours < 0.5) return;
    const decaySteps = Math.floor(elapsedHours / 0.5);
    pet.hunger = Math.max(0, pet.hunger - decaySteps * 3);
    pet.happy  = Math.max(0, pet.happy  - decaySteps * 2);
    pet.clean  = Math.max(0, pet.clean  - decaySteps * 2);
    pet.lastStatDecay = now;
}

function _renderPetStatBars() {
    const pet = getActivePet();
    const el = document.getElementById('pet-game-stat-bars');
    if (!el) return;
    if (!pet) { el.innerHTML = ''; return; }
    _ensurePetGameFields(pet);
    const bar = (val, color) =>
        `<div class="flex-1 bg-gray-100 rounded-full h-1.5 overflow-hidden"><div class="h-full rounded-full transition-all duration-500" style="width:${val}%;background:${color}"></div></div>`;
    el.innerHTML = `
        <div class="flex items-center gap-2 text-[9px] font-black text-gray-500">
            <span>🍖</span>${bar(pet.hunger,'#f59e0b')}
            <span>💛</span>${bar(pet.happy,'#ec4899')}
            <span>🛁</span>${bar(pet.clean,'#22d3ee')}
            <span class="ml-auto flex items-center gap-0.5 text-amber-600 font-black text-[10px]"><span>🐾</span>${pet.pawCoins || 0}</span>
        </div>`;
}

// ── 데일리 미션 ───────────────────────────────────────────────────────────────

function _dailyMissionKey() {
    const d = new Date().toISOString().slice(0, 10);
    return `petna_daily_missions_${settings_email}_${d}`;
}

function _getDailyMissions() {
    try { return JSON.parse(localStorage.getItem(_dailyMissionKey())) || { feed: false, play: false, shower: false, bonusClaimed: false }; }
    catch { return { feed: false, play: false, shower: false, bonusClaimed: false }; }
}

function _saveDailyMissions(m) {
    localStorage.setItem(_dailyMissionKey(), JSON.stringify(m));
}

function _completeMission(type) {
    const m = _getDailyMissions();
    if (m[type]) return;
    m[type] = true;
    _saveDailyMissions(m);
    const pet = getActivePet();
    if (pet) _earnCoins(pet, type === 'feed' ? 5 : type === 'play' ? 10 : 8, `미션:${type === 'feed' ? '밥주기' : type === 'play' ? '놀기' : '목욕'}`);
    if (!m.bonusClaimed && m.feed && m.play && m.shower) {
        m.bonusClaimed = true;
        _saveDailyMissions(m);
        if (pet) _earnCoins(pet, 50, '데일리 미션 완료 보너스!');
    }
    _renderDailyMissions();
}

function _renderDailyMissions() {
    const el = document.getElementById('pet-daily-missions');
    if (!el) return;
    const m = _getDailyMissions();
    const chip = (done, icon, label) =>
        `<span class="flex items-center gap-1 px-2 py-0.5 rounded-full text-[9px] font-black border ${done ? 'bg-green-50 border-green-200 text-green-600' : 'bg-gray-50 border-gray-200 text-gray-400'}">
            ${done ? '✅' : icon} ${label}
        </span>`;
    el.innerHTML = `
        <div class="flex items-center gap-1.5 flex-wrap">
            <span class="text-[9px] font-black text-gray-400 mr-0.5">오늘 미션</span>
            ${chip(m.feed,'🍖','밥주기')}${chip(m.play,'🎾','놀기')}${chip(m.shower,'🛁','목욕')}
            ${m.bonusClaimed ? '<span class="text-[9px] text-amber-500 font-black ml-auto">🎉 +50보너스 완료!</span>' : ''}
        </div>`;
}

// ── 방 아이템 상점 ────────────────────────────────────────────────────────────

const ROOM_SHOP_ITEMS = [
    { id: 'pack_ocean',   type: 'stickerPack', label: '🌊 바다 팩',   price: 30, items: ['🐠','🐙','🦀','🐚','🌊','⚓','🐋','🦈','🦞','🐡','🌴','🏝️'], size: 'md' },
    { id: 'pack_space',   type: 'stickerPack', label: '🚀 우주 팩',   price: 30, items: ['🚀','🌙','⭐','🪐','👾','🛸','🌠','☄️','🔭','🌌','👩‍🚀','🛰️'], size: 'md' },
    { id: 'pack_xmas',    type: 'stickerPack', label: '🎄 크리스마스', price: 25, items: ['🎄','🎅','⛄','🦌','🎁','🔔','❄️','🕯️','🧦','🍪','🎶','✨'], size: 'md' },
    { id: 'pack_food2',   type: 'stickerPack', label: '🍰 디저트 팩', price: 20, items: ['🍰','🧁','🍩','🎂','🍫','🍭','🍬','🧇','🥞','🍦','🍮','☕'], size: 'sm' },
    { id: 'theme_ocean',  type: 'theme',       label: '🏖️ 오션 테마',  price: 50, themeId: 'ocean'  },
    { id: 'theme_forest', type: 'theme',       label: '🌲 숲 테마',    price: 50, themeId: 'forest' },
];

function _unlockedKey() {
    const pet = getActivePet();
    return `petnna_unlocked_${pet?.id || 'default'}`;
}

function _getUnlocked() {
    try { return JSON.parse(localStorage.getItem(_unlockedKey())) || []; }
    catch { return []; }
}

function _saveUnlocked(list) {
    localStorage.setItem(_unlockedKey(), JSON.stringify(list));
}

function buyRoomItem(itemId) {
    const pet = getActivePet();
    if (!pet) return;
    _ensurePetGameFields(pet);
    const item = ROOM_SHOP_ITEMS.find(i => i.id === itemId);
    if (!item) return;
    const unlocked = _getUnlocked();
    if (unlocked.includes(itemId)) { showToast('이미 보유한 아이템입니다!'); return; }
    if ((pet.pawCoins || 0) < item.price) { showToast(`🐾코인이 부족해요! (${item.price - pet.pawCoins}개 더 필요)`); return; }
    pet.pawCoins -= item.price;
    unlocked.push(itemId);
    _saveUnlocked(unlocked);
    saveState();
    showToast(`✅ ${item.label} 구매 완료!`);
    if (item.type === 'stickerPack') {
        STICKER_CATEGORIES[item.label] = { items: item.items, size: item.size };
    } else if (item.type === 'theme' && item.themeId) {
        ROOM_THEMES[item.themeId] = ROOM_SHOP_THEMES[item.themeId];
    }
    _renderPetStatBars();
    renderRoomShop();
}

function _applyUnlockedItems() {
    const unlocked = _getUnlocked();
    unlocked.forEach(id => {
        const item = ROOM_SHOP_ITEMS.find(i => i.id === id);
        if (!item) return;
        if (item.type === 'stickerPack') STICKER_CATEGORIES[item.label] = { items: item.items, size: item.size };
        else if (item.type === 'theme' && item.themeId) ROOM_THEMES[item.themeId] = ROOM_SHOP_THEMES[item.themeId];
    });
}

const ROOM_SHOP_THEMES = {
    ocean:  { cls: 'room-theme-ocean',  swatch: 'linear-gradient(135deg,#e0f2fe 50%,#7dd3fc 50%)', label: '오션' },
    forest: { cls: 'room-theme-forest', swatch: 'linear-gradient(135deg,#dcfce7 50%,#86efac 50%)', label: '숲' },
};

function renderRoomShop() {
    const el = document.getElementById('room-shop-grid');
    if (!el) return;
    const pet = getActivePet();
    _ensurePetGameFields(pet);
    const coins = pet?.pawCoins || 0;
    const unlocked = _getUnlocked();
    el.innerHTML = ROOM_SHOP_ITEMS.map(item => {
        const owned = unlocked.includes(item.id);
        const canAfford = coins >= item.price;
        return `<div class="flex items-center justify-between bg-white border ${owned ? 'border-green-200 bg-green-50/40' : 'border-gray-100'} rounded-xl px-2.5 py-2 gap-2">
            <div class="flex flex-col">
                <span class="text-[11px] font-black text-gray-700">${item.label}</span>
                <span class="text-[9px] text-gray-400">${item.type === 'stickerPack' ? '스티커 팩' : '방 테마'}</span>
            </div>
            ${owned
                ? `<span class="text-[9px] text-green-600 font-black px-2 py-1 bg-green-100 rounded-lg">보유중</span>`
                : `<button onclick="buyRoomItem('${item.id}')" class="text-[9px] font-black px-2 py-1 rounded-lg border transition-all ${canAfford ? 'bg-amber-50 border-amber-200 text-amber-700 hover:bg-amber-100' : 'bg-gray-100 border-gray-200 text-gray-400 cursor-not-allowed'}">🐾${item.price}</button>`
            }
        </div>`;
    }).join('');
    const coinEl = document.getElementById('room-shop-coin-display');
    if (coinEl) coinEl.textContent = `🐾 ${coins}`;
}

// ── 방꾸미기 시스템 ───────────────────────────────────────────────────────────
const STICKER_CATEGORIES = {
    '가구': { items: ['🛋️','🪑','🛏️','📺','🖼️','🪞','🚪','🪟','💡','🧸','🗃️','🧺'], size: 'lg' },
    '식물': { items: ['🪴','🌿','🌸','🌺','🌻','🌵','🍀','🎋','🌴','🌾','🌱','🎍'], size: 'md' },
    '장난감': { items: ['🎾','🦴','🧸','🎀','🪀','🏀','⚽','🎊','🪁','🧩','🎯','🪃'], size: 'md' },
    '음식': { items: ['🍖','🐟','🥩','🫙','🍬','🍗','🥕','🍎','🍕','🧁','☕','🧃'], size: 'sm' },
    '기타': { items: ['⭐','💛','🌈','🌙','✨','🎵','🎨','📸','🎪','🏆','🎁','🪄'], size: 'sm' },
};

const ROOM_THEMES = {
    'cozy':   { cls: '',                   swatch: 'linear-gradient(135deg,#fef7ee 50%,#e8cfa0 50%)', label: '아늑' },
    'mint':   { cls: 'room-theme-mint',    swatch: 'linear-gradient(135deg,#edfaf4 50%,#b8e0c8 50%)', label: '민트' },
    'purple': { cls: 'room-theme-purple',  swatch: 'linear-gradient(135deg,#f5f0fe 50%,#d4c0f0 50%)', label: '보라' },
    'sky':    { cls: 'room-theme-sky',     swatch: 'linear-gradient(135deg,#eaf4fe 50%,#b8d8f0 50%)', label: '하늘' },
    'pink':   { cls: 'room-theme-pink',    swatch: 'linear-gradient(135deg,#fef0f4 50%,#f0c0cc 50%)', label: '핑크' },
    'dark':   { cls: 'room-theme-dark',    swatch: 'linear-gradient(135deg,#2a2a3e 50%,#1e1e2e 50%)', label: '밤' },
};

function applyRoomTheme(themeId) {
    const theme = ROOM_THEMES[themeId] || ROOM_THEMES['cozy'];
    const stage = document.querySelector('.room-stage');
    if (!stage) return;
    Object.values(ROOM_THEMES).forEach(t => { if (t.cls) stage.classList.remove(t.cls); });
    if (theme.cls) stage.classList.add(theme.cls);
    document.querySelectorAll('.room-theme-btn').forEach(b => b.classList.toggle('is-active', b.dataset.theme === themeId));
    const pet = pets && pets[activePetIndex];
    if (pet) { pet.roomTheme = themeId; if (typeof savePets === 'function') savePets(); }
    localStorage.setItem('petnna_room_theme_' + (pet?.id || 'default'), themeId);
}

function loadRoomTheme() {
    const pet = pets && pets[activePetIndex];
    const themeId = pet?.roomTheme || localStorage.getItem('petnna_room_theme_' + (pet?.id || 'default')) || 'cozy';
    applyRoomTheme(themeId);
}

function _stickerKey() {
    const pet = pets && pets[activePetIndex];
    return `petnna_room_stickers_${pet?.id || 'default'}`;
}

function loadRoomStickers() {
    try { return JSON.parse(localStorage.getItem(_stickerKey()) || '[]'); }
    catch { return []; }
}

function saveRoomStickers(stickers) {
    localStorage.setItem(_stickerKey(), JSON.stringify(stickers));
}

function addRoomSticker(emoji, size = 'md') {
    const stickers = loadRoomStickers();
    const x = 25 + Math.random() * 50;
    const y = 58 + Math.random() * 25;
    stickers.push({ id: `s${Date.now()}`, emoji, x, y, size });
    saveRoomStickers(stickers);
    renderRoomStickers();
}

function deleteRoomSticker(id) {
    saveRoomStickers(loadRoomStickers().filter(s => s.id !== id));
    renderRoomStickers();
}

function _depthScale(yPct) {
    // 야외 필드: 지평선 y=28%, 앞쪽 y=95%
    // 멀수록(위) 작게, 가까울수록(아래) 크게
    if (yPct < 30) return 0.28;
    return 0.28 + ((yPct - 30) / 65) * 1.1;
}

function renderRoomStickers() {
    const stage = document.querySelector('.room-stage');
    if (!stage) return;
    let layer = document.getElementById('room-sticker-layer');
    if (!layer) {
        layer = document.createElement('div');
        layer.id = 'room-sticker-layer';
        layer.className = 'absolute inset-0';
        layer.style.zIndex = '3';
        layer.style.pointerEvents = 'none';
        stage.appendChild(layer);
    }
    layer.innerHTML = '';
    loadRoomStickers().forEach(s => {
        const el = document.createElement('div');
        el.className = 'room-sticker absolute';
        const ds = _depthScale(s.y).toFixed(3);
        el.style.cssText = `left:${s.x}%;top:${s.y}%;transform:translate(-50%,-50%) scale(${ds});pointer-events:auto;z-index:${Math.round(s.y)+3};`;
        el.dataset.id = s.id;
        el.innerHTML = `<span class="room-sticker-emoji size-${s.size||'md'}">${s.emoji}</span><button class="room-sticker-delete" onclick="event.stopPropagation();deleteRoomSticker('${s.id}')" title="삭제">✕</button>`;
        _makeStickerDraggable(el, s.id);
        layer.appendChild(el);
    });
}

function _makeStickerDraggable(el, stickerId) {
    let startX, startY, startLeft, startTop, moved = false;
    el.addEventListener('pointerdown', e => {
        if (e.target.classList.contains('room-sticker-delete')) return;
        moved = false;
        el.setPointerCapture(e.pointerId);
        startX = e.clientX; startY = e.clientY;
        startLeft = parseFloat(el.style.left);
        startTop = parseFloat(el.style.top);
        e.preventDefault();
    });
    el.addEventListener('pointermove', e => {
        if (!e.buttons) return;
        if (Math.abs(e.clientX - startX) < 4 && Math.abs(e.clientY - startY) < 4) return;
        moved = true;
        el.classList.add('is-dragging');
        const rect = el.closest('.room-stage').getBoundingClientRect();
        const newTop = Math.max(5, Math.min(93, startTop + (e.clientY - startY) / rect.height * 100));
        const ds = _depthScale(newTop).toFixed(3);
        el.style.left = `${Math.max(5, Math.min(95, startLeft + (e.clientX - startX) / rect.width * 100))}%`;
        el.style.top = `${newTop}%`;
        el.style.transform = `translate(-50%,-50%) scale(${ds})`;
        el.style.zIndex = Math.round(newTop) + 50;
    });
    el.addEventListener('pointerup', () => {
        el.classList.remove('is-dragging');
        if (!moved) return;
        const newY = parseFloat(el.style.top);
        el.style.zIndex = Math.round(newY) + 3;
        const stickers = loadRoomStickers();
        const s = stickers.find(s => s.id === stickerId);
        if (s) { s.x = parseFloat(el.style.left); s.y = newY; saveRoomStickers(stickers); }
    });
}

function toggleStickerPicker() {
    const panel = document.getElementById('room-sticker-picker-panel');
    if (!panel) return;
    const opening = panel.classList.contains('hidden');
    ['butler-profile-editor-panel','pet-profile-editor-panel'].forEach(id => document.getElementById(id)?.classList.add('hidden'));
    panel.classList.toggle('hidden', !opening);
    if (opening) {
        switchDecorTab('decor');
    }
}

function switchDecorTab(tab) {
    const decorContent = document.getElementById('room-sticker-picker-panel')?.querySelector('.space-y-2:not(#room-shop-panel),.space-y-3>div:not([id])');
    const decorArea = document.querySelector('#room-sticker-picker-panel > div:not(.flex):not(#room-shop-panel)');
    const shopPanel = document.getElementById('room-shop-panel');
    document.querySelectorAll('.decor-tab-btn').forEach(b => b.classList.toggle('is-active', b.id === `${tab}-tab-btn`));
    if (tab === 'shop') {
        document.querySelectorAll('#room-sticker-picker-panel > div:not(.flex):not(#room-shop-panel)').forEach(el => el.classList.add('hidden'));
        shopPanel?.classList.remove('hidden');
        renderRoomShop();
    } else {
        document.querySelectorAll('#room-sticker-picker-panel > div:not(.flex):not(#room-shop-panel)').forEach(el => el.classList.remove('hidden'));
        shopPanel?.classList.add('hidden');
        renderStickerPickerCategory(Object.keys(STICKER_CATEGORIES)[0]);
    }
}

function renderStickerPickerCategory(cat) {
    document.querySelectorAll('.sticker-cat-btn').forEach(b => b.classList.toggle('is-active', b.dataset.cat === cat));
    const grid = document.getElementById('sticker-emoji-grid');
    if (!grid) return;
    const { items, size } = STICKER_CATEGORIES[cat];
    const fontSize = size === 'lg' ? 'text-3xl' : size === 'md' ? 'text-2xl' : 'text-xl';
    grid.innerHTML = items.map(e =>
        `<button onclick="addRoomSticker('${e}','${size}')" class="${fontSize} w-10 h-10 flex items-center justify-center rounded-xl hover:bg-amber-50 hover:scale-110 transition-all active:scale-95">${e}</button>`
    ).join('');
}
// ─────────────────────────────────────────────────────────────────────────────

let mypetWidgetInitialized = false;
function initMypetWeatherWidget() {
    async function fetchAndApplyWeather() {
        let lat = 37.5665, lng = 126.9780; // 서울 기본값
        try {
            const pos = await new Promise((res, rej) =>
                navigator.geolocation.getCurrentPosition(res, rej, { timeout: 5000 }));
            lat = pos.coords.latitude;
            lng = pos.coords.longitude;
        } catch (_) { /* 실패 시 서울 기본값 유지 */ }

        try {
            const [wxRes, aqRes] = await Promise.all([
                fetch(`https://api.open-meteo.com/v1/forecast?latitude=${lat}&longitude=${lng}&current=temperature_2m,relative_humidity_2m,weathercode&timezone=Asia/Seoul`),
                fetch(`https://air-quality-api.open-meteo.com/v1/air-quality?latitude=${lat}&longitude=${lng}&current=pm10&timezone=Asia/Seoul`)
            ]);
            const wx = await wxRes.json();
            const aq = await aqRes.json();

            const temp = Math.round(wx.current.temperature_2m);
            const humidity = Math.round(wx.current.relative_humidity_2m);
            const code = wx.current.weathercode;
            const pm10 = aq.current.pm10;

            const weatherTempEl = document.getElementById('mypet-weather-temp');
            const weatherIconEl = document.getElementById('mypet-weather-icon');
            const weatherDescEl = document.getElementById('mypet-weather-desc');
            const humidityEl = document.getElementById('mypet-weather-humidity');
            const dustEl = document.getElementById('mypet-weather-dust');
            const uvEl = document.getElementById('mypet-weather-uv');

            if (weatherTempEl) weatherTempEl.innerText = `${temp}°C`;
            if (humidityEl) humidityEl.innerText = `습도 ${humidity}%`;

            // weathercode → 아이콘/설명
            let iconClass = 'fa-solid fa-sun text-amber-400 animate-pulse';
            let descText = '맑음 ☀️';
            if (code <= 2) {
                iconClass = 'fa-solid fa-sun text-amber-400 animate-pulse';
                descText = '맑음 ☀️';
            } else if (code === 3) {
                iconClass = 'fa-solid fa-cloud text-gray-400';
                descText = '흐림 ☁️';
            } else if (code >= 45 && code <= 67) {
                iconClass = 'fa-solid fa-cloud-rain text-blue-400';
                descText = '비 🌧️';
            } else if (code >= 71 && code <= 77) {
                iconClass = 'fa-solid fa-snowflake text-blue-200';
                descText = '눈 ❄️';
            } else {
                iconClass = 'fa-solid fa-cloud-sun text-orange-300';
                descText = '흐림';
            }
            if (weatherIconEl) weatherIconEl.className = `${iconClass} text-2xl`;
            if (weatherDescEl) weatherDescEl.innerText = descText;

            // pm10 → 미세먼지 등급
            if (dustEl) {
                let dustText, dustColorClass;
                if (pm10 <= 30) {
                    dustText = `좋음 (${Math.round(pm10)}㎍/㎥)`;
                    dustColorClass = 'text-emerald-600 font-extrabold';
                } else if (pm10 <= 80) {
                    dustText = `보통 (${Math.round(pm10)}㎍/㎥)`;
                    dustColorClass = 'text-amber-500 font-extrabold';
                } else {
                    dustText = `나쁨 (${Math.round(pm10)}㎍/㎥) ⚠️`;
                    dustColorClass = 'text-rose-500 font-extrabold';
                }
                dustEl.innerText = `미세먼지 ${dustText}`;
                dustEl.className = dustColorClass;
            }

            // UV는 시간 기반 유지
            if (uvEl) {
                const h = new Date().getHours();
                let uvText = '낮음';
                if (h >= 10 && h <= 15) uvText = '높음 ⚠️';
                else if ((h >= 8 && h < 10) || (h > 15 && h <= 17)) uvText = '보통';
                uvEl.innerText = `자외선 ${uvText}`;
            }
        } catch (err) {
            console.warn('[PETNA] 날씨 API 오류, 폴백 표시', err);
            // API 실패 시 기존 Math.sin 폴백
            const now = new Date();
            const hour = now.getHours();
            const temp = Math.round(18 + 7 * Math.sin((hour - 8) / 24 * 2 * Math.PI));
            const weatherTempEl = document.getElementById('mypet-weather-temp');
            if (weatherTempEl) weatherTempEl.innerText = `${temp}°C`;
            const humidityEl = document.getElementById('mypet-weather-humidity');
            if (humidityEl) {
                const humidity = Math.round(60 - 15 * Math.sin((hour - 8) / 24 * 2 * Math.PI));
                humidityEl.innerText = `습도 ${humidity}%`;
            }
        }
    }

    function updateClockAndWeather() {
        const timeEl = document.getElementById('mypet-time-display');
        const dateEl = document.getElementById('mypet-date-display');

        const now = new Date();

        if (timeEl && dateEl) {
            const hours = String(now.getHours()).padStart(2, '0');
            const minutes = String(now.getMinutes()).padStart(2, '0');
            const seconds = String(now.getSeconds()).padStart(2, '0');
            timeEl.innerText = `${hours}:${minutes}:${seconds}`;

            const year = now.getFullYear();
            const month = String(now.getMonth() + 1).padStart(2, '0');
            const date = String(now.getDate()).padStart(2, '0');
            const days = ['일', '월', '화', '수', '목', '금', '토'];
            const day = days[now.getDay()];
            dateEl.innerText = `${year}. ${month}. ${date} (${day})`;
        }
    }

    function updateTodayFortune() {
        const today = new Date();
        const dateNum = today.getFullYear() * 10000 + (today.getMonth()+1) * 100 + today.getDate();

        const petFortunes = [
            "뜻밖의 특급 간식을 득템하는 날! 🥩",
            "에너지가 넘쳐 산책이 즐거운 날! 🦮",
            "포근한 이불 속에서 늦잠 자기 좋은 날! 💤",
            "주인과의 텔레파시가 통하는 날! 💖",
            "새로운 장난감이나 친구를 만나는 날! 🥎",
            "털 컨디션이 최고조! 오늘따라 더 빛나는 날 ✨",
            "낮잠 한 번에 꿀꿀한 기분 싹 날아가는 날 😴"
        ];
        const butlerFortunes = [
            "오늘은 반려동물과 특별한 추억을 만들기 좋은 날! 📸",
            "작은 선물 하나가 큰 행복이 되는 날! 🎁",
            "산책길에서 뜻밖의 좋은 인연을 만나는 날! 🤝",
            "집에서 쉬며 충전하는 것이 최선인 날! ☕",
            "펫과 함께하면 무엇이든 잘 풀리는 날! 🍀",
            "오래된 고민이 해결의 실마리를 찾는 날! 💡",
            "웃음이 끊이지 않는 에너지 넘치는 날! 😄"
        ];

        const r1 = ((dateNum * 9301 + 49297) % 233280) / 233280;
        const r2 = ((dateNum * 6311 + 13297) % 233280) / 233280;

        const petEl = document.getElementById('mypet-fortune-text');
        const butlerEl = document.getElementById('mypet-butler-fortune-text');
        const mobileEl = document.getElementById('mypet-fortune-text-mobile');

        const petFortuneText = petFortunes[Math.floor(r1 * petFortunes.length)];
        if (petEl) petEl.innerText = petFortuneText;
        if (butlerEl) butlerEl.innerText = butlerFortunes[Math.floor(r2 * butlerFortunes.length)];
        if (mobileEl) mobileEl.innerText = petFortuneText;
        // 사주 카드 운세 미리보기
        const previewEl = document.getElementById('mypet-fortune-preview');
        if (previewEl) previewEl.innerText = petFortuneText;
    }

    updateClockAndWeather();
    updateTodayFortune();
    if (typeof renderHealthDashboard === 'function') renderHealthDashboard();
    if (typeof updateDDayBubble === 'function') updateDDayBubble();
    if (mypetWidgetInitialized) return;
    mypetWidgetInitialized = true;
    setInterval(updateClockAndWeather, 1000);
    fetchAndApplyWeather();
    setInterval(fetchAndApplyWeather, 1800000); // 30분마다 날씨 갱신
}

function showWeeklyWeatherDetail(dayLabel, dateStr, weather) {
    let walkIndex = 100;
    let tip = "";
    let alertIcon = "🦮";
    let detailStatus = "";

    switch(weather.desc) {
        case "비":
            walkIndex = 20;
            detailStatus = "🌧️ 비바람 주의보 (실내 놀이 대체 적극 권장)";
            tip = "폭우 또는 소나기로 인해 야외 산책 시 반려동물의 발바닥 습진 우려 및 체온 저하가 유발될 수 있습니다.\n집안에서 스니프 매트(노즈워크)나 로프 터그 놀이로 호기심과 활동량을 채워주시고, 부득이 산책 시에는 기능성 레인코트를 장착 후 복귀 즉시 따뜻하게 드라이해 주세요! ☔";
            alertIcon = "☔";
            break;
        case "뇌우":
            walkIndex = 5;
            detailStatus = "⚡ 천둥번개 경보 (야외 활동 절대 금지)";
            tip = "천둥번개의 고주파 소음은 반려동물의 지능지수와 감각기관에 강한 트라우마성 공포를 주게 됩니다.\n절대 야외로 나가지 마시고 커튼을 쳐 시각 자극을 가린 뒤 클래식 음악을 틀어 안정을 도모해 주시고, 든든한 백허그나 애착 장난감을 안겨주세요. ⚡";
            alertIcon = "⚡";
            break;
        case "흐림":
            walkIndex = 75;
            detailStatus = "☁️ 흐림 (가벼운 동네 산책로 권장)";
            tip = "자외선 노출 위험은 낮아 쾌적하지만 대기 중 습도가 높아 호흡기가 예민한 아이는 주의가 필요합니다.\n인적이 드물고 평탄한 동네 골목 중심의 단거리 코스(15~20분 내외)를 돌며 가볍게 냄새를 맡고 마킹을 하는 리프레시 산책이 적당합니다. ☁️";
            alertIcon = "☁️";
            break;
        case "구름조금":
            walkIndex = 90;
            detailStatus = "⛅ 쾌적함 (신나는 잔디밭 모험하기 좋은 날)";
            tip = "선선한 바람과 적당한 구름으로 산책 강도를 높이기에 아주 훌륭한 골든 타임입니다.\n인근의 펫 파크나 잔디가 넓은 공원에 방문하여 평소 하지 못했던 다른 댕댕이/냥이 친구들과의 소셜 교감 및 공놀이를 신나게 즐겨보세요! ⛅";
            alertIcon = "⛅";
            break;
        case "맑음":
        case "화창함":
        default:
            walkIndex = 98;
            detailStatus = "☀️ 화창하고 높은 하늘 (아스팔트 온도 주의 필요)";
            tip = "구름 없이 맑아 시야가 확보되는 최고의 날씨입니다!\n다만 직사광선으로 인해 대낮의 아스팔트 바닥 온도가 50도 이상으로 치솟아 발바닥 패드에 화상을 입을 수 있으니,\n해가 뜨거운 낮(11시~15시)을 피해 흙길 위주로 걷거나, 오전 일찍 혹은 노을빛이 내리는 저녁 쿨다운 타이밍에 산책을 진행해 주세요. ☀️";
            alertIcon = "☀️";
            break;
    }

    if (typeof showCustomDialog === 'function') {
        showCustomDialog({
            title: `📅 ${dayLabel} (${dateStr}) 안심 산책 리포트`,
            message: `기상 예보: ${weather.desc} (${alertIcon})\n기온 정보: 최저 ${weather.tempMin}°C ~ 최고 ${weather.tempMax}°C\n\n📊 안심 산책 지수: ${walkIndex} / 100점\n🚨 현황: ${detailStatus}\n\n💡 집사 안심 가이드:\n${tip}`,
            icon: alertIcon,
            type: "alert"
        });
    }
}

function getButlerAndPetCondition() {
    const today = new Date();
    const seed = today.getFullYear() * 10000 + (today.getMonth() + 1) * 100 + today.getDate();
    
    function rand(s) {
        return ((s * 9301 + 49297) % 233280) / 233280;
    }
    
    const butlerSeed = seed + 101;
    const petSeed = seed + 202;
    
    const butlerRand = rand(butlerSeed);
    const petRand = rand(petSeed);
    
    const butlerPct = Math.round(70 + butlerRand * 30);
    const petPct = Math.round(70 + petRand * 30);
    
    let butlerStatus = "";
    let butlerEmoji = "🧔";
    if (butlerPct >= 95) {
        butlerStatus = "활력 충만! 최상의 컨디션으로 몸도 마음도 가볍습니다. 🚀";
        butlerEmoji = "🤩";
    } else if (butlerPct >= 85) {
        butlerStatus = "매우 쾌적하고 맑은 정신 상태입니다. 가벼운 운동이나 산책이 최적! 👟";
        butlerEmoji = "😃";
    } else if (butlerPct >= 75) {
        butlerStatus = "평이한 일상적 컨디션입니다. 따뜻한 차 한 잔으로 에너지를 충전하세요. ☕";
        butlerEmoji = "🙂";
    } else {
        butlerStatus = "조금 나른하고 피로가 누적된 상태입니다. 무리하지 말고 충분한 휴식을 권장해요. 🥱";
        butlerEmoji = "🥱";
    }
    
    let petStatus = "";
    let petEmoji = "🐕";
    const current = getActivePet();
    const petType = current ? current.type : 'dog';
    if (petType === 'cat') petEmoji = "🐈";
    else if (petType === 'rabbit') petEmoji = "🐰";
    else if (petType === 'hamster') petEmoji = "🐹";
    
    if (petPct >= 95) {
        if (petType === 'cat') petStatus = "기분이 아주 날아갈 것 같아요! 우다다 타임 및 사냥 놀이에 제격입니다! 🎯🐾";
        else petStatus = "에너지가 뿜뿜! 당장이라도 마당이나 넓은 공원을 뛰어놀 준비가 되어 있습니다! ⚡🏃";
    } else if (petPct >= 85) {
        petStatus = "눈망울이 초롱초롱하고 활력이 높습니다. 집사와의 교감 놀이를 매우 원하고 있어요! 🥎";
    } else if (petPct >= 75) {
        petStatus = "무난하고 평온한 상태입니다. 편안한 휴식과 가벼운 스킨십이 행복을 줍니다. 💤";
    } else {
        petStatus = "컨디션이 다소 다운되어 낮잠을 청하고 싶어 해요. 소화하기 쉬운 간식과 편안한 잠자리가 필요해요. 🛌";
    }
    
    return {
        butlerPct,
        butlerStatus,
        butlerEmoji,
        petPct,
        petStatus,
        petEmoji
    };
}

function showOnboardingCard() {
    const card = document.getElementById('pet-room-card');
    if (!card) return;
    card.innerHTML = `
        <div class="relative overflow-hidden">
            <div class="absolute inset-0 bg-gradient-to-br from-brand-50 via-amber-50/60 to-brand-50/40 pointer-events-none"></div>
            <div class="relative flex flex-col items-center text-center px-6 pt-10 pb-8 space-y-6">
                <div class="relative">
                    <span class="text-7xl drop-shadow-md" style="animation:petBounce 2s ease-in-out infinite">🐾</span>
                </div>
                <div class="space-y-2">
                    <p class="text-xl font-black text-gray-800 keep-all">반려동물을 등록하고<br>케어를 시작해보세요</p>
                    <p class="text-xs text-gray-400 font-medium leading-relaxed keep-all">AI 건강분석 · 산책 GPS · 사주팔자 · 소셜 피드<br>모든 케어를 한 곳에서</p>
                </div>
                <div class="grid grid-cols-3 gap-2.5 w-full max-w-xs">
                    <div class="flex flex-col items-center gap-1.5 bg-white/80 rounded-2xl p-3 border border-amber-100 shadow-xs">
                        <span class="text-xl">🏥</span>
                        <span class="text-[10px] font-black text-gray-600">AI 건강분석</span>
                    </div>
                    <div class="flex flex-col items-center gap-1.5 bg-white/80 rounded-2xl p-3 border border-amber-100 shadow-xs">
                        <span class="text-xl">🗺️</span>
                        <span class="text-[10px] font-black text-gray-600">산책 GPS</span>
                    </div>
                    <div class="flex flex-col items-center gap-1.5 bg-white/80 rounded-2xl p-3 border border-amber-100 shadow-xs">
                        <span class="text-xl">🔯</span>
                        <span class="text-[10px] font-black text-gray-600">사주팔자</span>
                    </div>
                </div>
                <button onclick="openPetRegistrationModal()"
                    class="w-full max-w-xs flex items-center justify-center gap-2 py-3.5 bg-brand-500 hover:bg-brand-600 active:scale-95 text-white font-black text-sm rounded-2xl transition-all shadow-lg shadow-brand-200">
                    <i class="fa-solid fa-paw text-sm"></i> 첫 반려동물 등록하기
                </button>
                <p class="text-[10px] text-gray-300 font-medium">30초면 완료됩니다</p>
            </div>
        </div>
        <style>
        @keyframes petBounce {
            0%,100%{transform:translateY(0)}
            50%{transform:translateY(-8px)}
        }
        </style>`;
}

function renderMyPets() {
    initMypetWeatherWidget();
    if (typeof PetGame !== 'undefined' && document.getElementById('petgame-root')) {
        // 레거시 스티커 1회 이관 준비 — ensureGame이 pet._legacyStickers를 읽는다
        const cur = getActivePet();
        if (cur && !cur.game) {
            try { cur._legacyStickers = JSON.parse(localStorage.getItem(`petnna_room_stickers_${cur.id}`)) || []; } catch (e) {}
        }
        PetGame.mount('petgame-root');
    }
    const current = getActivePet();
    if (!current) {
        showOnboardingCard();
        return;
    }

    if (typeof renderPetRecoCard === 'function') renderPetRecoCard('reco-card-home');
    if (typeof renderHealthDigestBanner === 'function') renderHealthDigestBanner();
    if (typeof renderMemoryFlashbackBanner === 'function') renderMemoryFlashbackBanner();

    const roomNameEl = document.getElementById('pet-room-name');
    if (roomNameEl) {
        if (current.roomName) {
            roomNameEl.innerText = current.roomName;
        } else {
            roomNameEl.innerText = current.name + "의 하루 방 🏠";
        }
    }

    const visitCountEl = document.getElementById('pet-room-visit-count');
    if (visitCountEl) {
        const visitCount = localStorage.getItem('petna_visit_count_' + settings_email) || "1";
        visitCountEl.innerText = visitCount;
    }

    const breedEl = document.getElementById('pet-info-breed');
    const ageEl = document.getElementById('pet-info-age');
    const genderEl = document.getElementById('pet-info-gender');
    const weightEl = document.getElementById('pet-info-weight');
    const personalityEl = document.getElementById('pet-info-personality');

    if (breedEl) breedEl.innerText = current.breed;
    if (ageEl) ageEl.innerText = current.age;
    if (genderEl) genderEl.innerText = current.gender;
    if (weightEl) weightEl.innerText = current.weight + " kg";
    if (personalityEl) personalityEl.innerText = current.personality;

    // Render Butler & Pet Conditions
    const butlerCondEmoji = document.getElementById('butler-condition-emoji');
    const butlerCondTitle = document.getElementById('butler-condition-title');
    const butlerCondPct = document.getElementById('butler-condition-pct');
    const butlerCondBar = document.getElementById('butler-condition-bar');
    const butlerCondDesc = document.getElementById('butler-condition-desc');
    
    const petCondEmoji = document.getElementById('pet-condition-emoji');
    const petCondTitle = document.getElementById('pet-condition-title');
    const petCondPct = document.getElementById('pet-condition-pct');
    const petCondBar = document.getElementById('pet-condition-bar');
    const petCondDesc = document.getElementById('pet-condition-desc');
    
    const cond = getButlerAndPetCondition();
    
    if (butlerCondEmoji) {
        butlerCondEmoji.innerText = settings_photo_url ? "👤" : (settings_avatar || cond.butlerEmoji);
    }
    if (butlerCondTitle) {
        butlerCondTitle.innerText = `${settings_nickname || '집사'}님의 오늘 컨디션`;
    }
    if (butlerCondPct) butlerCondPct.innerText = `${cond.butlerPct}%`;
    if (butlerCondBar) butlerCondBar.style.width = `${cond.butlerPct}%`;
    if (butlerCondDesc) butlerCondDesc.innerText = cond.butlerStatus;
    
    if (petCondEmoji) petCondEmoji.innerText = cond.petEmoji;
    if (petCondTitle) {
        petCondTitle.innerText = `${current.name || '반려동물'}의 오늘 컨디션`;
    }
    if (petCondPct) petCondPct.innerText = `${cond.petPct}%`;
    if (petCondBar) petCondBar.style.width = `${cond.petPct}%`;
    if (petCondDesc) petCondDesc.innerText = cond.petStatus;

    const graphicContainer = document.getElementById('pet-graphic-container');
    if (graphicContainer) {
        if (current.type === 'custom') {
            graphicContainer.innerHTML = `<img loading="lazy" src="${escapeHtml(current.imageUrl)}" class="w-full h-full object-cover rounded-xl animate-pet-body" onerror="this.src='https://placehold.co/200/fbeee0/732f18?text=${escapeHtml(current.name)}'">`;
        } else {
            let svgMarkup = '';
            if (current.type === 'cat') {
                svgMarkup = `<svg viewBox="0 0 100 100" class="w-full h-full">
                    <defs>
                        <linearGradient id="cat-body-grad" x1="0%" y1="0%" x2="100%" y2="100%">
                            <stop offset="0%" stop-color="#ffb07c" />
                            <stop offset="100%" stop-color="#e37736" />
                        </linearGradient>
                        <linearGradient id="cat-ear-inner" x1="0%" y1="0%" x2="0%" y2="100%">
                            <stop offset="0%" stop-color="#ffccd5" />
                            <stop offset="100%" stop-color="#ff85a1" />
                        </linearGradient>
                        <radialGradient id="cat-face-grad" cx="50%" cy="50%" r="50%">
                            <stop offset="0%" stop-color="#ffe5d9" />
                            <stop offset="75%" stop-color="#fcd5be" />
                            <stop offset="100%" stop-color="#eb9b63" />
                        </radialGradient>
                        <radialGradient id="cat-blush" cx="50%" cy="50%" r="50%">
                            <stop offset="0%" stop-color="#ff85a1" stop-opacity="0.6" />
                            <stop offset="100%" stop-color="#ff85a1" stop-opacity="0" />
                        </radialGradient>
                        <filter id="cat-shadow" x="-10%" y="-10%" width="120%" height="120%">
                            <feDropShadow dx="0" dy="2.5" stdDeviation="1.5" flood-color="#732f18" flood-opacity="0.15" />
                        </filter>
                    </defs>
                    <!-- Ears -->
                    <path d="M 16,18 L 36,36 L 22,42 Z" fill="url(#cat-body-grad)" filter="url(#cat-shadow)" />
                    <path d="M 20,22 L 32,34 L 23,38 Z" fill="url(#cat-ear-inner)" />
                    <path d="M 84,18 L 64,36 L 78,42 Z" fill="url(#cat-body-grad)" filter="url(#cat-shadow)" />
                    <path d="M 80,22 L 68,34 L 77,38 Z" fill="url(#cat-ear-inner)" />
                    <!-- Face -->
                    <ellipse cx="50" cy="56" rx="32" ry="29" fill="url(#cat-face-grad)" filter="url(#cat-shadow)" />
                    <!-- Blush -->
                    <circle cx="28" cy="62" r="6" fill="url(#cat-blush)" />
                    <circle cx="72" cy="62" r="6" fill="url(#cat-blush)" />
                    <!-- Eyes (Animated Group) -->
                    <g class="animate-eye">
                        <ellipse cx="38" cy="50" rx="4.5" ry="5" fill="#332" />
                        <circle cx="36.5" cy="48" r="1.3" fill="#ffffff" />
                        <circle cx="39.5" cy="51.5" r="0.6" fill="#ffffff" />
                    </g>
                    <g class="animate-eye">
                        <ellipse cx="62" cy="50" rx="4.5" ry="5" fill="#332" />
                        <circle cx="60.5" cy="48" r="1.3" fill="#ffffff" />
                        <circle cx="63.5" cy="51.5" r="0.6" fill="#ffffff" />
                    </g>
                    <!-- Whiskers -->
                    <line x1="22" y1="58" x2="10" y2="56" stroke="#d55d24" stroke-width="1" stroke-linecap="round" opacity="0.6" />
                    <line x1="22" y1="61" x2="8" y2="61" stroke="#d55d24" stroke-width="1" stroke-linecap="round" opacity="0.6" />
                    <line x1="22" y1="64" x2="10" y2="66" stroke="#d55d24" stroke-width="1" stroke-linecap="round" opacity="0.6" />
                    <line x1="78" y1="58" x2="90" y2="56" stroke="#d55d24" stroke-width="1" stroke-linecap="round" opacity="0.6" />
                    <line x1="78" y1="61" x2="92" y2="61" stroke="#d55d24" stroke-width="1" stroke-linecap="round" opacity="0.6" />
                    <line x1="78" y1="64" x2="90" y2="66" stroke="#d55d24" stroke-width="1" stroke-linecap="round" opacity="0.6" />
                    <!-- Nose -->
                    <polygon points="47,57 53,57 50,61" fill="#d55d24" />
                    <!-- Mouth -->
                    <path d="M 45,65 Q 50,68 50,65 Q 50,68 55,65" stroke="#d55d24" stroke-width="2.2" stroke-linecap="round" fill="none" />
                </svg>`;
            } else if (current.type === 'rabbit') {
                svgMarkup = `<svg viewBox="0 0 100 100" class="w-full h-full">
                    <defs>
                        <linearGradient id="rab-body-grad" x1="0%" y1="0%" x2="100%" y2="100%">
                            <stop offset="0%" stop-color="#ffffff" />
                            <stop offset="100%" stop-color="#fdf5ed" />
                        </linearGradient>
                        <linearGradient id="rab-ear-inner" x1="0%" y1="0%" x2="0%" y2="100%">
                            <stop offset="0%" stop-color="#ffe3e8" />
                            <stop offset="100%" stop-color="#ff9ebb" />
                        </linearGradient>
                        <radialGradient id="rab-face-grad" cx="50%" cy="50%" r="50%">
                            <stop offset="0%" stop-color="#ffffff" />
                            <stop offset="85%" stop-color="#fdfbf7" />
                            <stop offset="100%" stop-color="#f5ede4" />
                        </radialGradient>
                        <radialGradient id="rab-blush" cx="50%" cy="50%" r="50%">
                            <stop offset="0%" stop-color="#ff9ebb" stop-opacity="0.65" />
                            <stop offset="100%" stop-color="#ff9ebb" stop-opacity="0" />
                        </radialGradient>
                        <filter id="rab-shadow" x="-10%" y="-10%" width="120%" height="120%">
                            <feDropShadow dx="0" dy="2" stdDeviation="1.5" flood-color="#a69485" flood-opacity="0.12" />
                        </filter>
                    </defs>
                    <!-- Ears (Animated) -->
                    <g class="animate-ear-left" style="transform-origin: 32px 30px;">
                        <ellipse cx="38" cy="22" rx="8" ry="22" fill="url(#rab-body-grad)" filter="url(#rab-shadow)" />
                        <ellipse cx="38" cy="22" rx="4.5" ry="17" fill="url(#rab-ear-inner)" />
                    </g>
                    <g class="animate-ear-right" style="transform-origin: 68px 30px;">
                        <ellipse cx="62" cy="22" rx="8" ry="22" fill="url(#rab-body-grad)" filter="url(#rab-shadow)" />
                        <ellipse cx="62" cy="22" rx="4.5" ry="17" fill="url(#rab-ear-inner)" />
                    </g>
                    <!-- Face -->
                    <ellipse cx="50" cy="60" rx="30" ry="26" fill="url(#rab-face-grad)" filter="url(#rab-shadow)" />
                    <!-- Blush -->
                    <circle cx="27" cy="65" r="6" fill="url(#rab-blush)" />
                    <circle cx="73" cy="65" r="6" fill="url(#rab-blush)" />
                    <!-- Eyes (Animated Group) -->
                    <g class="animate-eye">
                        <circle cx="40" cy="54" r="4" fill="#ff758c" />
                        <circle cx="38.5" cy="52" r="1.3" fill="#ffffff" />
                        <circle cx="41.2" cy="55.5" r="0.6" fill="#ffffff" />
                    </g>
                    <g class="animate-eye">
                        <circle cx="60" cy="54" r="4" fill="#ff758c" />
                        <circle cx="58.5" cy="52" r="1.3" fill="#ffffff" />
                        <circle cx="61.2" cy="55.5" r="0.6" fill="#ffffff" />
                    </g>
                    <!-- Nose -->
                    <polygon points="48.5,59.5 51.5,59.5 50,62" fill="#ff758c" />
                    <!-- Mouth -->
                    <path d="M 46.5,64.5 Q 50,66.5 50,64.5 Q 50,66.5 53.5,64.5" stroke="#ff758c" stroke-width="1.8" stroke-linecap="round" fill="none" />
                </svg>`;
            } else if (current.type === 'hamster') {
                svgMarkup = `<svg viewBox="0 0 100 100" class="w-full h-full">
                    <defs>
                        <linearGradient id="ham-body-grad" x1="0%" y1="0%" x2="100%" y2="100%">
                            <stop offset="0%" stop-color="#fca366" />
                            <stop offset="100%" stop-color="#d47030" />
                        </linearGradient>
                        <radialGradient id="ham-white-grad" cx="50%" cy="50%" r="50%">
                            <stop offset="0%" stop-color="#ffffff" />
                            <stop offset="80%" stop-color="#fff9f2" />
                            <stop offset="100%" stop-color="#f3e5d3" />
                        </radialGradient>
                        <linearGradient id="ham-ear-inner" x1="0%" y1="0%" x2="0%" y2="100%">
                            <stop offset="0%" stop-color="#ffb3c1" />
                            <stop offset="100%" stop-color="#ff758c" />
                        </linearGradient>
                        <radialGradient id="ham-blush" cx="50%" cy="50%" r="50%">
                            <stop offset="0%" stop-color="#f58c73" stop-opacity="0.6" />
                            <stop offset="100%" stop-color="#f58c73" stop-opacity="0" />
                        </radialGradient>
                        <filter id="ham-shadow" x="-10%" y="-10%" width="120%" height="120%">
                            <feDropShadow dx="0" dy="2" stdDeviation="1.5" flood-color="#804a24" flood-opacity="0.15" />
                        </filter>
                    </defs>
                    <!-- Ears -->
                    <circle cx="30" cy="35" r="8" fill="url(#ham-body-grad)" filter="url(#ham-shadow)" />
                    <circle cx="30" cy="35" r="4.5" fill="url(#ham-ear-inner)" />
                    <circle cx="70" cy="35" r="8" fill="url(#ham-body-grad)" filter="url(#ham-shadow)" />
                    <circle cx="70" cy="35" r="4.5" fill="url(#ham-ear-inner)" />
                    <!-- Face (Body) -->
                    <ellipse cx="50" cy="62" rx="30" ry="26" fill="url(#ham-body-grad)" filter="url(#ham-shadow)" />
                    <!-- White Patch -->
                    <ellipse cx="50" cy="66" rx="20" ry="18" fill="url(#ham-white-grad)" />
                    <!-- Blush -->
                    <circle cx="27" cy="66" r="6" fill="url(#ham-blush)" />
                    <circle cx="73" cy="66" r="6" fill="url(#ham-blush)" />
                    <!-- Eyes (Animated Group) -->
                    <g class="animate-eye">
                        <ellipse cx="40" cy="56" rx="3.5" ry="4" fill="#111" />
                        <circle cx="38.5" cy="54" r="1.1" fill="#ffffff" />
                        <circle cx="41.2" cy="57.5" r="0.5" fill="#ffffff" />
                    </g>
                    <g class="animate-eye">
                        <ellipse cx="60" cy="56" rx="3.5" ry="4" fill="#111" />
                        <circle cx="58.5" cy="54" r="1.1" fill="#ffffff" />
                        <circle cx="61.2" cy="57.5" r="0.5" fill="#ffffff" />
                    </g>
                    <!-- Nose -->
                    <polygon points="48.5,59.5 51.5,59.5 50,61.5" fill="#ff758c" />
                    <!-- Mouth & Teeth -->
                    <path d="M 47,63.5 Q 50,65 50,63.5 Q 50,65 53,63.5" stroke="#ff758c" stroke-width="1.8" stroke-linecap="round" fill="none" />
                    <rect x="49" y="64.2" width="2" height="1.8" fill="#ffffff" stroke="#ff758c" stroke-width="0.5" rx="0.2" />
                </svg>`;
            } else {
                svgMarkup = `<svg viewBox="0 0 100 100" class="w-full h-full">
                    <defs>
                        <linearGradient id="dog-body-grad" x1="0%" y1="0%" x2="100%" y2="100%">
                            <stop offset="0%" stop-color="#f7be7e" />
                            <stop offset="100%" stop-color="#cf7f34" />
                        </linearGradient>
                        <linearGradient id="dog-ear-grad" x1="0%" y1="0%" x2="0%" y2="100%">
                            <stop offset="0%" stop-color="#b86921" />
                            <stop offset="100%" stop-color="#7a3f0d" />
                        </linearGradient>
                        <radialGradient id="dog-face-grad" cx="50%" cy="50%" r="50%">
                            <stop offset="0%" stop-color="#fce2c4" />
                            <stop offset="80%" stop-color="#f7be7e" />
                            <stop offset="100%" stop-color="#d98c41" />
                        </radialGradient>
                        <radialGradient id="dog-muzzle" cx="50%" cy="50%" r="50%">
                            <stop offset="0%" stop-color="#ffffff" />
                            <stop offset="100%" stop-color="#f2eae1" />
                        </radialGradient>
                        <radialGradient id="dog-blush" cx="50%" cy="50%" r="50%">
                            <stop offset="0%" stop-color="#ff7c8e" stop-opacity="0.55" />
                            <stop offset="100%" stop-color="#ff7c8e" stop-opacity="0" />
                        </radialGradient>
                        <filter id="dog-shadow" x="-10%" y="-10%" width="120%" height="120%">
                            <feDropShadow dx="0" dy="2.5" stdDeviation="1.5" flood-color="#693910" flood-opacity="0.15" />
                        </filter>
                    </defs>
                    <!-- Ears (Animated) -->
                    <g class="animate-ear-left" style="transform-origin: 32px 30px;">
                        <ellipse cx="23" cy="46" rx="9" ry="20" fill="url(#dog-ear-grad)" filter="url(#dog-shadow)" />
                    </g>
                    <g class="animate-ear-right" style="transform-origin: 68px 30px;">
                        <ellipse cx="77" cy="46" rx="9" ry="20" fill="url(#dog-ear-grad)" filter="url(#dog-shadow)" />
                    </g>
                    <!-- Face -->
                    <ellipse cx="50" cy="55" rx="31" ry="28" fill="url(#dog-face-grad)" filter="url(#dog-shadow)" />
                    <!-- Blush -->
                    <circle cx="28" cy="62" r="6" fill="url(#dog-blush)" />
                    <circle cx="72" cy="62" r="6" fill="url(#dog-blush)" />
                    <!-- Eyes (Animated Group) -->
                    <g class="animate-eye">
                        <circle cx="38" cy="48" r="4.5" fill="#332" />
                        <circle cx="36.5" cy="46" r="1.3" fill="#ffffff" />
                        <circle cx="39.5" cy="49.5" r="0.6" fill="#ffffff" />
                    </g>
                    <g class="animate-eye">
                        <circle cx="62" cy="48" r="4.5" fill="#332" />
                        <circle cx="60.5" cy="46" r="1.3" fill="#ffffff" />
                        <circle cx="63.5" cy="49.5" r="0.6" fill="#ffffff" />
                    </g>
                    <!-- Muzzle -->
                    <ellipse cx="50" cy="61" rx="12" ry="9" fill="url(#dog-muzzle)" />
                    <!-- Nose -->
                    <ellipse cx="50" cy="55" rx="5" ry="3.5" fill="#111" />
                    <circle cx="48.5" cy="53.8" r="1" fill="#ffffff" />
                    <!-- Tongue (Animated) -->
                    <ellipse cx="50" cy="62" rx="4" ry="5.5" class="animate-tongue" fill="#ff758c" />
                    <!-- Mouth line -->
                    <path d="M 46.5,61.5 Q 50,64.5 50,61.5 Q 50,64.5 53.5,61.5" stroke="#7a3f0d" stroke-width="1.8" stroke-linecap="round" fill="none" />
                </svg>`;
            }
            graphicContainer.innerHTML = svgMarkup;
        }
    }

    // Render Saju & Chemistry results in My Pet Room
    const sajuCard = document.getElementById('mypet-saju-card');
    if (sajuCard) {
        const sajuNoResult = document.getElementById('mypet-saju-no-result');
        const sajuHasResult = document.getElementById('mypet-saju-has-result');

        if (current.sajuData || current.harmonyData) {
            if (sajuNoResult) sajuNoResult.classList.add('hidden');
            if (sajuHasResult) sajuHasResult.classList.remove('hidden');

            const sajuGridSec = document.getElementById('mypet-saju-grid-section');
            const sajuCompatSec = document.getElementById('mypet-saju-compat-section');
            const sajuBtnsSec = document.getElementById('mypet-saju-buttons-section');

            if (current.sajuData) {
                if (sajuGridSec) sajuGridSec.classList.remove('hidden');
                if (sajuCompatSec) sajuCompatSec.classList.remove('hidden');
                if (sajuBtnsSec) sajuBtnsSec.classList.remove('hidden');

                const petNameLabel = document.getElementById('mypet-saju-pet-name-label');
                const petSummary = document.getElementById('mypet-saju-pet-summary');
                const petDesc = document.getElementById('mypet-saju-pet-desc');
                const ownerSummary = document.getElementById('mypet-saju-owner-summary');
                const ownerDesc = document.getElementById('mypet-saju-owner-desc');
                const compatScore = document.getElementById('mypet-saju-compat-score');
                const compatTitle = document.getElementById('mypet-saju-compat-title');
                const pastDesc = document.getElementById('mypet-saju-past-desc');
                const synergyDesc = document.getElementById('mypet-saju-synergy-desc');

                if (petNameLabel) petNameLabel.innerText = current.name;
                if (petSummary) petSummary.innerText = current.sajuData.petSummary;
                if (petDesc) petDesc.innerText = current.sajuData.petDesc;
                if (ownerSummary) ownerSummary.innerText = current.sajuData.ownerSummary;
                if (ownerDesc) ownerDesc.innerText = current.sajuData.ownerDesc;
                if (compatScore) compatScore.innerText = `${current.sajuData.compatScore}점`;
                if (compatTitle) compatTitle.innerText = `"${current.sajuData.compatTitle}"`;
                if (pastDesc) pastDesc.innerText = current.sajuData.pastDesc;
                if (synergyDesc) synergyDesc.innerText = current.sajuData.synergyDesc;
            } else {
                if (sajuGridSec) sajuGridSec.classList.add('hidden');
                if (sajuCompatSec) sajuCompatSec.classList.add('hidden');
                if (sajuBtnsSec) sajuBtnsSec.classList.add('hidden');
            }

            // Render Harmony Report (종합 조화도)
            const harmonyBox = document.getElementById('mypet-harmony-display-box');
            if (harmonyBox) {
                if (current.harmonyData) {
                    harmonyBox.classList.remove('hidden');
                    const harmonyAvgScore = document.getElementById('mypet-harmony-avg-score');
                    const harmonyTitle = document.getElementById('mypet-harmony-title');
                    const harmonySolution = document.getElementById('mypet-harmony-solution');

                    if (harmonyAvgScore) {
                        harmonyAvgScore.innerText = `${current.harmonyData.avgScore}점 (${current.harmonyData.level}단계)`;
                    }
                    if (harmonyTitle) {
                        harmonyTitle.innerText = `"${current.harmonyData.title}"`;
                    }
                    if (harmonySolution) {
                        harmonySolution.innerText = current.harmonyData.solution;
                    }
                } else {
                    harmonyBox.classList.add('hidden');
                }
            }

            // 상단 조화도 배지 업데이트
            const roomHarmonyBadge = document.getElementById('room-harmony-badge');
            const roomHarmonyScore = document.getElementById('room-harmony-score');
            const roomHarmonyIcon = document.getElementById('room-harmony-icon');

            if (current.harmonyData && roomHarmonyBadge && roomHarmonyScore) {
                roomHarmonyScore.textContent = `${current.harmonyData.avgScore}점`;
                roomHarmonyScore.classList.add('text-rose-600');
                roomHarmonyBadge.classList.add('cursor-pointer', 'hover:shadow-md', 'transition-shadow');
                roomHarmonyBadge.onclick = () => switchTab('saju');
            } else if (roomHarmonyScore) {
                roomHarmonyScore.textContent = '조화도 측정하기';
                roomHarmonyScore.classList.add('text-rose-600');
                if (roomHarmonyBadge) {
                    roomHarmonyBadge.classList.add('cursor-pointer', 'hover:shadow-md', 'transition-shadow');
                    roomHarmonyBadge.onclick = () => switchTab('saju');
                }
            }
        } else {
            if (sajuNoResult) sajuNoResult.classList.remove('hidden');
            if (sajuHasResult) sajuHasResult.classList.add('hidden');
        }
    }

    // Render Stage Badges
    const petStageMbtiBadge = document.getElementById('pet-stage-mbti-badge');
    const petStageIqBadge = document.getElementById('pet-stage-iq-badge');
    const petStageSajuBadge = document.getElementById('pet-stage-saju-badge');

    if (petStageMbtiBadge) {
        if (current.mbtiCode) {
            petStageMbtiBadge.innerText = current.mbtiCode;
            petStageMbtiBadge.classList.remove('hidden');
        } else {
            petStageMbtiBadge.classList.add('hidden');
        }
    }

    if (petStageIqBadge) {
        if (current.iqScore) {
            petStageIqBadge.innerText = `IQ ${current.iqScore}`;
            petStageIqBadge.classList.remove('hidden');
        } else {
            petStageIqBadge.classList.add('hidden');
        }
    }

    if (petStageSajuBadge) {
        if (current.sajuData) {
            petStageSajuBadge.innerText = `☯️ 조화도 ${current.sajuData.compatScore}점`;
            petStageSajuBadge.classList.remove('hidden');
        } else {
            petStageSajuBadge.classList.add('hidden');
        }
    }

    // Render Speech Bubble
    const bubbleText = document.getElementById('pet-bubble-text');
    if (bubbleText) {
        if (current.tempSpeechText) {
            bubbleText.innerText = current.tempSpeechText;
        } else if (current.mbtiCode && PET_MBTI_SPEECHES[current.mbtiCode]) {
            bubbleText.innerText = PET_MBTI_SPEECHES[current.mbtiCode];
        } else {
            bubbleText.innerText = "집사님, 오늘 날씨 좋은데 산책하러 가요! 🐕";
        }
    }

    // Sync Butler Profile Card settings & Double stage displays
    syncButlerAvatarDisplay();

    renderMealLogsList();
    renderStatsChart();
    if (typeof saveHealthHistoryToday === 'function') saveHealthHistoryToday();
    if (typeof renderHealthTrendChart === 'function') setTimeout(renderHealthTrendChart, 300);
    if (typeof updateAiHealthUsageBadge === 'function') updateAiHealthUsageBadge();
    if (typeof renderHealthStreak === 'function') renderHealthStreak();
    if (typeof renderHealthCalendar === 'function') renderHealthCalendar();
    if (typeof renderDailyChallenges === 'function') renderDailyChallenges();
    if (typeof renderAchievementBadges === 'function') renderAchievementBadges();
    if (typeof checkNewAchievements === 'function') setTimeout(checkNewAchievements, 800);
    if (typeof renderCareScheduler === 'function') renderCareScheduler();
    if (typeof updateCareCompletionBadge === 'function') updateCareCompletionBadge();
    if (typeof updateReportDashboard === 'function') updateReportDashboard();
    if (typeof updateHarmonyWidget === 'function') updateHarmonyWidget();
    if (typeof updateRoomThemeByHarmony === 'function') updateRoomThemeByHarmony();

    // ── 📊 펄스 패치: 산책 streak 배너 + 월간 리포트 카드 갱신 ──
    if (typeof renderWeeklyCareChallenge === 'function') renderWeeklyCareChallenge();
    if (typeof renderWeeklyWalkChallenge === 'function') renderWeeklyWalkChallenge();
    if (typeof renderHoodChallenge === 'function') renderHoodChallenge();
    if (typeof renderWalkStreakBanner === 'function') renderWalkStreakBanner();
    if (typeof renderBuddyStreakCard === 'function') renderBuddyStreakCard();
    if (typeof renderMonthlyReport === 'function') renderMonthlyReport('monthly-report-card');
    // 투약 대시보드·건강수첩 렌더는 건강 탭(renderHealthTab)으로 이동됨
}

function getPast7DaysLabels() {
    const labels = [];
    for (let i = 6; i >= 0; i--) {
        const d = new Date();
        d.setDate(d.getDate() - i);
        const month = d.getMonth() + 1;
        const day = d.getDate();
        labels.push(`${month}/${day}`);
    }
    return labels;
}

// 종/품종별 표준 체중 밴드(kg) — 오버레이 참고용 근사치
const PET_WEIGHT_BANDS = {
    cat: { min: 3.5, max: 5.5 },
    dog: { min: 5, max: 30 },
    _breeds: {
        '토이푸들': { min: 2, max: 4 },
        '푸들': { min: 4, max: 8 },
        '말티즈': { min: 2, max: 4 },
        '포메라니안': { min: 1.5, max: 3.5 },
        '치와와': { min: 1.5, max: 3 },
        '시바': { min: 8, max: 11 },
        '골든리트리버': { min: 25, max: 34 },
        '리트리버': { min: 25, max: 34 },
        '허스키': { min: 16, max: 27 },
        '달마시안': { min: 20, max: 32 },
        '웰시코기': { min: 10, max: 14 },
        '코기': { min: 10, max: 14 }
    }
};

// 펫의 품종·종에 맞는 표준 체중 밴드를 반환(품종 우선, 없으면 종 단위)
function getPetWeightBand(pet) {
    if (!pet) return null;
    const breed = (pet.breed || '').replace(/\s/g, '');
    const breeds = PET_WEIGHT_BANDS._breeds;
    for (const key in breeds) {
        if (breed.includes(key)) return breeds[key];
    }
    if (pet.type === 'cat') return PET_WEIGHT_BANDS.cat;
    return PET_WEIGHT_BANDS.dog;
}

// 오늘 날짜로 실제 체중을 기록(같은 날은 덮어씀)
function recordPetWeight(pet, weightKg) {
    if (!pet) return;
    const w = parseFloat(weightKg);
    if (!(w > 0)) return;
    if (!Array.isArray(pet.weightLog)) pet.weightLog = [];
    const today = new Date().toISOString().slice(0, 10);
    const existing = pet.weightLog.find(e => e.date === today);
    if (existing) existing.weight = w;
    else pet.weightLog.push({ date: today, weight: w });
    pet.weightLog.sort((a, b) => a.date.localeCompare(b.date));
}

function renderStatsChart() {
    const ctx = document.getElementById('pet-stats-chart');
    if (!ctx) return;

    if (typeof Chart === 'undefined') {
        setTimeout(renderStatsChart, 500);
        return;
    }

    const labels = getPast7DaysLabels();
    const currentPet = getActivePet();
    const baseWeight = currentPet ? parseFloat(currentPet.weight) || 8.4 : 8.4;

    // 실제 walks/meals 데이터가 있을 때만 채움. 빈 배열로 시작하려면 0으로 초기화.
    const hasWalkData = walks && walks.length > 0;
    const hasMealData = meals && meals.length > 0;

    // 실제 데이터가 없으면 모두 0, 있으면 실제 데이터로 채움
    const dailyDistance = hasWalkData ? [0, 0, 0, 0, 0, 0, 0] : [0, 0, 0, 0, 0, 0, 0];
    const dailyWalkCount = hasWalkData ? [0, 0, 0, 0, 0, 0, 0] : [0, 0, 0, 0, 0, 0, 0];
    const dailyMealCount = hasMealData ? [0, 0, 0, 0, 0, 0, 0] : [0, 0, 0, 0, 0, 0, 0];

    // 실제 체중 로그(pet.weightLog: [{date:'YYYY-MM-DD', weight}])를 7일 라벨에 매핑.
    // 해당 날짜 기록이 없으면 직전 기록을 이어받고(carry-forward), 로그 자체가 없으면 현재 체중으로 표시.
    const past7Dates = [];
    for (let i = 6; i >= 0; i--) {
        const d = new Date();
        d.setDate(d.getDate() - i);
        past7Dates.push(d.toISOString().slice(0, 10));
    }
    const wLog = (currentPet && Array.isArray(currentPet.weightLog)) ? currentPet.weightLog : [];
    let lastKnownWeight = null;
    wLog.forEach(e => { if (e.date <= past7Dates[0]) lastKnownWeight = e.weight; });
    const dailyWeight = past7Dates.map(date => {
        const entry = wLog.find(e => e.date === date);
        if (entry) { lastKnownWeight = entry.weight; return entry.weight; }
        return lastKnownWeight != null ? lastKnownWeight : (baseWeight || null);
    });

    // 종/품종별 표준 체중 밴드 오버레이
    const weightBand = (typeof getPetWeightBand === 'function') ? getPetWeightBand(currentPet) : null;
    const bandDatasets = weightBand ? [
        {
            label: '표준 상한 (kg)',
            data: labels.map(() => weightBand.max),
            type: 'line',
            borderColor: 'rgba(148, 163, 184, 0.45)',
            backgroundColor: 'rgba(148, 163, 184, 0.12)',
            borderWidth: 1,
            borderDash: [4, 4],
            pointRadius: 0,
            fill: '+1',
            yAxisID: 'y-weight',
            order: 5
        },
        {
            label: '표준 하한 (kg)',
            data: labels.map(() => weightBand.min),
            type: 'line',
            borderColor: 'rgba(148, 163, 184, 0.45)',
            backgroundColor: 'transparent',
            borderWidth: 1,
            borderDash: [4, 4],
            pointRadius: 0,
            fill: false,
            yAxisID: 'y-weight',
            order: 6
        }
    ] : [];

    // 목표(이상) 체중선 — 사용자가 지정한 목표(pet.goalWeight)가 있으면 우선,
    // 없으면 표준 밴드 중앙값을 이상체중으로 사용
    let goalWeight = currentPet ? parseFloat(currentPet.goalWeight) : NaN;
    if (!(goalWeight > 0) && weightBand) goalWeight = (weightBand.min + weightBand.max) / 2;
    const goalDataset = (goalWeight > 0) ? [{
        label: '목표 체중 (kg)',
        data: labels.map(() => goalWeight),
        type: 'line',
        borderColor: 'rgba(16, 185, 129, 0.9)',
        backgroundColor: 'transparent',
        borderWidth: 2,
        borderDash: [6, 3],
        pointRadius: 0,
        fill: false,
        yAxisID: 'y-weight',
        order: 4
    }] : [];

    // 증감 화살표 + 목표까지 진행 상황(부제로 표시)
    const knownWeights = dailyWeight.filter(v => v != null);
    const latestWeight = knownWeights.length ? knownWeights[knownWeights.length - 1] : null;
    const prevWeight = knownWeights.length > 1 ? knownWeights[knownWeights.length - 2] : null;
    let weightSubtitle = '';
    if (latestWeight != null) {
        const diff = (prevWeight != null) ? (latestWeight - prevWeight) : 0;
        const arrow = diff > 0.05 ? '▲' : (diff < -0.05 ? '▼' : '＝');
        const diffTxt = (prevWeight != null) ? `${arrow} ${diff > 0 ? '+' : ''}${diff.toFixed(1)}kg` : `${arrow} 변화 없음`;
        weightSubtitle = `현재 ${latestWeight.toFixed(1)}kg · ${diffTxt}`;
        if (goalWeight > 0) {
            const toGoal = latestWeight - goalWeight;
            const goalTxt = Math.abs(toGoal) < 0.05 ? '목표 달성 🎯'
                : (toGoal > 0 ? `목표까지 ${toGoal.toFixed(1)}kg 감량` : `목표까지 ${Math.abs(toGoal).toFixed(1)}kg 증량`);
            weightSubtitle += ` · ${goalTxt}`;
        }
    }

    if (hasWalkData) {
        walks.forEach(w => {
            let idx = -1;
            if (w.date === "오늘 방금" || w.date === "오늘") idx = 6;
            else if (w.date === "1일 전" || w.date === "어제") idx = 5;
            else if (w.date === "2일 전") idx = 4;
            else if (w.date === "3일 전") idx = 3;
            else if (w.date === "4일 전") idx = 2;
            else if (w.date === "5일 전") idx = 1;
            else if (w.date === "6일 전") idx = 0;

            if (idx !== -1) {
                dailyDistance[idx] += parseFloat(w.distance) || 0;
                dailyWalkCount[idx] += 1;
            }
        });
    }

    if (hasMealData) {
        const todayStart = new Date(2026, 4, 17, 0, 0, 0).getTime();
        const todayMeals = meals.filter(m => m.id >= todayStart).length;
        dailyMealCount[6] = Math.max(todayMeals, meals.filter(m => m.type !== "간식").length);
    }

    if (statsChart) {
        statsChart.destroy();
    }

    // Determine chart theme colors dynamically
    const isDarkTheme = document.body.classList.contains('theme-dark') || (typeof settings_theme !== 'undefined' && settings_theme === 'dark');
    const isSepiaTheme = document.body.classList.contains('theme-sepia') || (typeof settings_theme !== 'undefined' && settings_theme === 'sepia');
    let chartTextColor = '#4b5563'; // Default: gray-600
    let gridColor = 'rgba(243, 244, 246, 0.6)';

    if (isDarkTheme) {
        chartTextColor = '#d1d1e0';
        gridColor = 'rgba(255, 255, 255, 0.08)';
    } else if (isSepiaTheme) {
        chartTextColor = '#5d4631';
        gridColor = 'rgba(93, 70, 49, 0.1)';
    }

    statsChart = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [
                ...bandDatasets,
                ...goalDataset,
                {
                    label: '산책 거리 (km)',
                    data: dailyDistance,
                    backgroundColor: 'rgba(227, 119, 54, 0.75)',
                    borderColor: 'rgba(227, 119, 54, 1)',
                    borderWidth: 1.5,
                    borderRadius: 4,
                    yAxisID: 'y-distance',
                    order: 3
                },
                {
                    label: '먹이 횟수 (회)',
                    data: dailyMealCount,
                    backgroundColor: 'rgba(52, 211, 153, 0.75)',
                    borderColor: 'rgba(52, 211, 153, 1)',
                    borderWidth: 1.5,
                    borderRadius: 4,
                    yAxisID: 'y-count',
                    order: 4
                },
                {
                    label: '산책 횟수 (회)',
                    data: dailyWalkCount,
                    type: 'line',
                    borderColor: 'rgba(79, 70, 229, 1)',
                    backgroundColor: 'rgba(79, 70, 229, 0.1)',
                    borderWidth: 2,
                    pointStyle: 'rect',
                    pointRadius: 5,
                    yAxisID: 'y-count',
                    order: 2
                },
                {
                    label: '몸무게 (kg)',
                    data: dailyWeight,
                    type: 'line',
                    borderColor: 'rgba(244, 63, 94, 1)',
                    backgroundColor: 'transparent',
                    borderWidth: 3,
                    pointStyle: 'circle',
                    pointRadius: 6,
                    tension: 0.3,
                    yAxisID: 'y-weight',
                    order: 1
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                subtitle: {
                    display: !!weightSubtitle,
                    text: weightSubtitle,
                    color: chartTextColor,
                    font: { family: 'Noto Sans KR', size: 11, weight: 'bold' },
                    padding: { bottom: 6 }
                },
                legend: {
                    position: 'top',
                    labels: {
                        boxWidth: 10,
                        color: chartTextColor,
                        font: { family: 'Noto Sans KR', size: 10, weight: 'bold' }
                    }
                },
                tooltip: {
                    mode: 'index',
                    intersect: false,
                    backgroundColor: 'rgba(15, 23, 42, 0.9)',
                    titleFont: { family: 'Noto Sans KR', size: 11, weight: 'bold' },
                    bodyFont: { family: 'Noto Sans KR', size: 11 }
                }
            },
            scales: {
                x: {
                    grid: { display: false },
                    ticks: { color: chartTextColor, font: { family: 'Noto Sans KR', size: 10 } }
                },
                'y-distance': {
                    type: 'linear',
                    position: 'left',
                    grid: { color: gridColor },
                    ticks: { color: chartTextColor, font: { family: 'Noto Sans KR', size: 9 } }
                },
                'y-count': {
                    type: 'linear',
                    position: 'right',
                    grid: { display: false },
                    ticks: { color: chartTextColor, font: { family: 'Noto Sans KR', size: 9 }, stepSize: 1 }
                },
                'y-weight': {
                    type: 'linear',
                    position: 'right',
                    grid: { display: false },
                    ticks: { color: chartTextColor, font: { family: 'Noto Sans KR', size: 9 } }
                }
            }
        }
    });
}

function feedPet() {
    const current = getActivePet();
    if (!current) return;

    if (current.hunger >= 100) {
        showToast("포만감이 이미 가득 차있습니다. 그만 먹여도 괜찮아요!");
        return;
    }

    _ensurePetGameFields(current);
    current.hunger = Math.min(100, current.hunger + 15);
    current.happy = Math.min(100, current.happy + 8);
    current.tempSpeechText = `냠냠! 맛있는 간식을 먹고 배가 든든해졌어요! 🥩 (${current.hunger}%)`;
    _completeMission('feed');

    const now = new Date();
    const timeStr = `${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`;
    const autoMeal = {
        id: Date.now(),
        type: "간식",
        time: timeStr,
        notes: "집사 수동 케어로 간식 배식 (포만감 업) 🍖"
    };
    meals.unshift(autoMeal);
    saveState();

    renderMyPets();
    showToast("간식을 맛있게 먹고식사 일지에 자동 저장되었습니다!");
}

function playPet() {
    const current = getActivePet();
    if (!current) return;

    _ensurePetGameFields(current);
    current.happy = Math.min(100, current.happy + 18);
    current.hunger = Math.max(0, current.hunger - 10);
    current.tempSpeechText = `얍! 집사님이 터그놀이 공을 던져줘서 엄청 행복해요! 🥎 (${current.happy}%)`;
    _completeMission('play');
    saveState();

    renderMyPets();
    showToast("신나게 터그 놀이를 즐겼습니다. 행복지수 폭발!");
}

function showerPet() {
    const current = getActivePet();
    if (!current) return;

    _ensurePetGameFields(current);
    current.happy = Math.min(100, current.happy + 10);
    current.clean = Math.min(100, (current.clean || 0) + 30);
    current.tempSpeechText = `뽀송뽀송 샴푸 스파 완료! 털이 부드러워요 🧼`;
    _completeMission('shower');
    saveState();

    renderMyPets();
    showToast("스파 목욕으로 펫 피로를 말끔히 풀어주었습니다.");
}

function toggleNotebookEdit(isEdit) {
    const viewMode = document.getElementById('notebook-view-mode');
    const editMode = document.getElementById('notebook-edit-mode');
    const current = getActivePet();

    if (isEdit && current) {
        document.getElementById('edit-nb-breed').value = current.breed;
        document.getElementById('edit-nb-age').value = current.age;
        document.getElementById('edit-nb-gender').value = current.gender;
        document.getElementById('edit-nb-weight').value = current.weight;
        document.getElementById('edit-nb-personality').value = current.personality;
    }

    if (viewMode && editMode) {
        if (isEdit) {
            viewMode.classList.add('hidden');
            editMode.classList.remove('hidden');
        } else {
            viewMode.classList.remove('hidden');
            editMode.classList.add('hidden');
        }
    }
}

function saveNotebookEdit() {
    const current = getActivePet();
    if (!current) return;

    current.breed = document.getElementById('edit-nb-breed').value.trim() || current.breed;
    current.age = document.getElementById('edit-nb-age').value.trim() || current.age;
    current.gender = document.getElementById('edit-nb-gender').value.trim() || current.gender;
    current.weight = document.getElementById('edit-nb-weight').value.trim() || current.weight;
    current.personality = document.getElementById('edit-nb-personality').value.trim() || current.personality;

    if (typeof recordPetWeight === 'function') recordPetWeight(current, current.weight);

    saveState();
    if (typeof updatePetInSupabase === 'function') {
        updatePetInSupabase(current);
    }
    toggleNotebookEdit(false);
    renderMyPets();
    showToast("생활수첩의 펫 정보가 성공적으로 변경되었습니다! 📝");
}

function openNotebookModal() {
    const modal = document.getElementById('notebook-modal');
    if (modal) {
        modal.classList.remove('hidden');
        modal.classList.add('flex');
    }
    switchNotebookTab('info');
    if (typeof renderCustomRoutesList === 'function') {
        renderCustomRoutesList();
    }
    renderMyPets();
}

function closeNotebookModal() {
    const modal = document.getElementById('notebook-modal');
    if (modal) {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }
}

function switchNotebookTab(tabName) {
    const tabInfo = document.getElementById('notebook-tab-info');
    const tabRoutes = document.getElementById('notebook-tab-routes');
    const contentInfo = document.getElementById('notebook-tab-content-info');
    const contentRoutes = document.getElementById('notebook-tab-content-routes');
    
    if (tabName === 'info') {
        if (contentInfo) contentInfo.classList.remove('hidden');
        if (contentRoutes) contentRoutes.classList.add('hidden');
        
        if (tabInfo) {
            tabInfo.className = "flex-1 py-2 text-center text-[11px] font-black rounded-xl transition-all text-brand-600 bg-white shadow-xs outline-none";
        }
        if (tabRoutes) {
            tabRoutes.className = "flex-1 py-2 text-center text-[11px] font-black rounded-xl transition-all text-gray-400 hover:text-gray-600 outline-none";
        }
    } else if (tabName === 'routes') {
        if (contentInfo) contentInfo.classList.add('hidden');
        if (contentRoutes) contentRoutes.classList.remove('hidden');
        
        if (tabInfo) {
            tabInfo.className = "flex-1 py-2 text-center text-[11px] font-black rounded-xl transition-all text-gray-400 hover:text-gray-600 outline-none";
        }
        if (tabRoutes) {
            tabRoutes.className = "flex-1 py-2 text-center text-[11px] font-black rounded-xl transition-all text-brand-600 bg-white shadow-xs outline-none";
        }
        
        if (typeof renderCustomRoutesList === 'function') {
            renderCustomRoutesList();
        }
    }
}


function toggleMealForm(isOpen) {
    const form = document.getElementById('meal-form');
    if (form) {
        if (isOpen) {
            form.classList.remove('hidden');
            const now = new Date();
            document.getElementById('meal-time').value = `${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`;
        } else {
            form.classList.add('hidden');
        }
    }
}

function saveMealRecord() {
    const type = document.getElementById('meal-type').value;
    const time = document.getElementById('meal-time').value;
    const notes = document.getElementById('meal-notes').value.trim() || "유기농 맞춤 배식";

    if (!time) {
        showToast("식사 시각을 올바르게 선택해주세요.");
        return;
    }

    const newMeal = {
        id: Date.now(),
        type: type,
        time: time,
        notes: notes
    };

    meals.unshift(newMeal);
    saveState();
    toggleMealForm(false);

    const current = getActivePet();
    if (current) {
        current.hunger = Math.min(100, current.hunger + 20);
        saveState();
    }

    renderMyPets();
    showToast("배식 시간 기록과 함께 펫 포만감이 상향되었습니다!");
}

function renderMealLogsList() {
    const listContainer = document.getElementById('meal-list');
    if (!listContainer) return;

    listContainer.innerHTML = '';

    if (!meals || meals.length === 0) {
        listContainer.innerHTML = `
            <div class="text-center py-6 text-gray-400">
                <div class="text-3xl mb-1.5">🍽️</div>
                <p class="text-xs font-semibold">아직 식사 기록이 없어요</p>
                <p class="text-[10px] mt-0.5 text-gray-300">기록 버튼으로 추가해보세요</p>
            </div>`;
        return;
    }

    const sorted = [...meals].sort((a, b) => (a.time || '').localeCompare(b.time || ''));

    sorted.forEach(m => {
        let icon = '🌙'; let bg = 'bg-amber-50 border-amber-100'; let badge = 'bg-amber-100 text-amber-700';
        if (m.type === '아침') { icon = '🌅'; bg = 'bg-sky-50 border-sky-100'; badge = 'bg-sky-100 text-sky-700'; }
        else if (m.type === '점심') { icon = '☀️'; bg = 'bg-emerald-50 border-emerald-100'; badge = 'bg-emerald-100 text-emerald-700'; }
        else if (m.type === '간식') { icon = '🍖'; bg = 'bg-rose-50 border-rose-100'; badge = 'bg-rose-100 text-rose-700'; }

        const row = document.createElement('div');
        row.className = `flex items-center gap-2.5 p-2.5 ${bg} border rounded-xl`;
        row.innerHTML = `
            <span class="text-xl leading-none">${icon}</span>
            <div class="flex-1 min-w-0">
                <div class="flex items-center gap-1.5 mb-0.5">
                    <span class="text-[10px] font-bold px-1.5 py-0.5 rounded-md ${badge}">${m.type}</span>
                    <span class="text-[10px] text-gray-400">${m.time || ''}</span>
                </div>
                <p class="text-[11px] text-gray-600 font-medium truncate">${escapeHtml(m.notes || '내용 없음')}</p>
            </div>
            <button onclick="deleteMealLog(${m.id})" class="text-gray-300 hover:text-red-400 transition-colors flex-shrink-0">
                <i class="fa-solid fa-xmark text-sm"></i>
            </button>
        `;
        listContainer.appendChild(row);
    });

    renderMealTimeline();
}

function renderMealTimeline() {
    const el = document.getElementById('meal-timeline');
    if (!el) return;

    el.innerHTML = '';

    if (!meals || meals.length === 0) {
        el.innerHTML = `
            <div class="text-center py-6 text-gray-400">
                <div class="text-3xl mb-1.5">⏰</div>
                <p class="text-xs font-semibold">식사 기록이 없어요</p>
            </div>`;
        return;
    }

    const sorted = [...meals].sort((a, b) => (a.time || '').localeCompare(b.time || ''));
    sorted.forEach((m, i) => {
        let dot = 'bg-amber-400'; let icon = '🌙';
        if (m.type === '아침') { dot = 'bg-sky-400'; icon = '🌅'; }
        else if (m.type === '점심') { dot = 'bg-emerald-400'; icon = '☀️'; }
        else if (m.type === '간식') { dot = 'bg-rose-400'; icon = '🍖'; }

        const isLast = i === sorted.length - 1;
        const row = document.createElement('div');
        row.className = 'flex items-start gap-2.5';
        row.innerHTML = `
            <div class="flex flex-col items-center flex-shrink-0">
                <div class="w-2.5 h-2.5 rounded-full ${dot} mt-1 ring-2 ring-white shadow-sm"></div>
                ${!isLast ? '<div class="w-px flex-1 bg-gray-200 mt-0.5" style="min-height:20px"></div>' : ''}
            </div>
            <div class="pb-2 flex-1 min-w-0">
                <div class="flex items-center gap-1.5">
                    <span class="text-[10px] font-bold text-gray-700">${m.time || '--:--'}</span>
                    <span class="text-[10px] text-gray-400">${icon} ${m.type}</span>
                </div>
                <p class="text-[11px] text-gray-500 truncate mt-0.5">${escapeHtml(m.notes || '내용 없음')}</p>
            </div>
        `;
        el.appendChild(row);
    });
}

function deleteMealLog(id) {
    meals = meals.filter(m => m.id !== id);
    saveState();
    renderMealLogsList();
    showToast("선택하신 배식 기록 일지가 삭제되었습니다.");
}

function openPetRegistrationModal() {
    const modal = document.getElementById('pet-reg-modal');
    if (modal) {
        modal.classList.remove('hidden');
        modal.classList.add('flex');
    }
}

function closePetRegistrationModal() {
    const modal = document.getElementById('pet-reg-modal');
    if (modal) {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }
}

function adjustPetTypeSelection() {
    const type = document.getElementById('reg-pet-type').value;
    const customGroup = document.getElementById('custom-image-upload-group');
    if (customGroup) {
        if (type === 'custom') {
            customGroup.classList.remove('hidden');
        } else {
            customGroup.classList.add('hidden');
        }
    }
}

let tempRegisteredPhotoUrl = "";

function uploadPetProfileImage(event) {
    const file = event.target.files[0];
    if (!file) return;

    if (!file.type.startsWith('image/')) {
        showToast("이미지 파일만 업로드할 수 있습니다.");
        return;
    }

    const reader = new FileReader();
    reader.onload = function(e) {
        tempRegisteredPhotoUrl = e.target.result;
        showToast("기기 내 이미지가 성공적으로 불러와졌습니다! 📸");
    };
    reader.readAsDataURL(file);
}

function selectRegPresetPhoto(url) {
    tempRegisteredPhotoUrl = url;
    document.getElementById('reg-pet-photo-url').value = url;
    showToast("엄선된 프리미엄 펫 사진이 선택되었습니다! ✨");
}

function submitPetRegistration() {
    // ── 🔍 가희 검수관: 입력값 검증 강화 ────────────────────────────────────
    function _setFieldError(id, msg) {
        const el = document.getElementById(id);
        if (!el) return false;
        el.classList.add('field-error');
        // 기존 오류 메시지 제거 후 재삽입
        const existing = el.parentNode.querySelector('.field-error-msg');
        if (existing) existing.remove();
        const errEl = document.createElement('p');
        errEl.className = 'field-error-msg';
        errEl.innerHTML = `⚠️ ${msg}`;
        el.parentNode.appendChild(errEl);
        setTimeout(() => { el.classList.remove('field-error'); }, 2000);
        el.focus();
        return true;
    }
    function _clearFieldError(id) {
        const el = document.getElementById(id);
        if (!el) return;
        el.classList.remove('field-error');
        const msg = el.parentNode.querySelector('.field-error-msg');
        if (msg) msg.remove();
    }

    const nameEl    = document.getElementById('reg-pet-name');
    const weightEl  = document.getElementById('reg-pet-weight');
    const ageEl     = document.getElementById('reg-pet-age');

    const name    = nameEl    ? nameEl.value.trim()   : '';
    const weight  = weightEl  ? weightEl.value.trim() : '';
    const age     = ageEl     ? ageEl.value.trim()    : '';

    // 1) 이름 검증: 필수, 1~20자, 숫자만으로 구성 불가
    _clearFieldError('reg-pet-name');
    if (!name) {
        _setFieldError('reg-pet-name', '펫 이름을 입력해주세요.');
        return;
    }
    if (name.length > 20) {
        _setFieldError('reg-pet-name', '이름은 20자 이내로 입력해주세요.');
        return;
    }
    if (/^[0-9]+$/.test(name)) {
        _setFieldError('reg-pet-name', '이름은 숫자만으로 구성할 수 없어요.');
        return;
    }

    // 2) 체중 검증: 입력된 경우 0.1 ~ 100 사이 숫자
    _clearFieldError('reg-pet-weight');
    if (weight) {
        const w = parseFloat(weight);
        if (isNaN(w) || w < 0.1 || w > 100) {
            _setFieldError('reg-pet-weight', '체중은 0.1 ~ 100 kg 사이로 입력해주세요.');
            return;
        }
    }

    // 3) 나이 검증: 너무 큰 숫자 방지 (선택 필드)
    _clearFieldError('reg-pet-age');
    if (age) {
        const ageNum = parseInt(age.replace(/[^0-9]/g, ''));
        if (!isNaN(ageNum) && ageNum > 50) {
            _setFieldError('reg-pet-age', '나이가 너무 커요. 올바른 나이를 입력해주세요.');
            return;
        }
    }
    // ── 가희 검증 완료 ───────────────────────────────────────────────────────

    const type        = document.getElementById('reg-pet-type').value;
    const breed       = document.getElementById('reg-pet-breed').value.trim() || '말티즈';
    const finalAge    = age || '1살';
    const finalWeight = weight || '3.5';
    const gender      = document.getElementById('reg-pet-gender').value;
    const personality = document.getElementById('reg-pet-personality').value.trim() || '활기차고 영특함';

    let finalUrl = tempRegisteredPhotoUrl || document.getElementById('reg-pet-photo-url').value.trim() || 'https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=300';

    const newPet = {
        id: Date.now(),
        name: name,
        type: type,
        breed: breed,
        imageUrl: finalUrl,
        age: finalAge,
        weight: finalWeight,
        gender: gender,
        personality: personality,
        hunger: 70,
        happy: 80
    };

    if (typeof recordPetWeight === 'function') recordPetWeight(newPet, finalWeight);

    pets = [...pets, newPet];
    activePetIndex = pets.length - 1; // 새로 추가한 펫을 활성 펫으로

    saveState();
    if (typeof updatePetInSupabase === 'function') {
        updatePetInSupabase(newPet);
    }
    closePetRegistrationModal();
    renderMyPets();

    // 업적 체크 (첫 펫 등록)
    if (typeof checkNewAchievements === 'function') checkNewAchievements();

    showToast(`'${name}'이(가) 새 펫으로 추가되었습니다! 🐾🎉`);

    // 탄생 카드 자동 생성 — 등록 직후 공유 유도
    setTimeout(() => {
        if (typeof shareWelcomeCard === 'function') {
            showCustomDialog({
                title: `🐾 ${name} 탄생 카드`,
                message: `${name}이(가) 펫과나 가족이 됐어요!\n탄생 카드를 만들어 친구들과 공유해볼까요?`,
                type: 'confirm',
                onConfirm: () => shareWelcomeCard(newPet)
            });
        }
    }, 1200);

}


// 🔒 집사 프로필 토글 및 업로드 관련 헬퍼 함수들 (Butler Settings Helpers)
function toggleRoomSettings() {
    const menu = document.getElementById('room-settings-menu');
    const btn  = document.getElementById('room-settings-btn');
    const icon = document.getElementById('room-settings-icon');
    if (!menu) return;
    const isOpen = !menu.classList.contains('hidden');
    menu.classList.toggle('hidden');
    if (icon) {
        icon.classList.toggle('text-amber-500', !isOpen);
        icon.classList.toggle('text-gray-400', isOpen);
        icon.style.transform = isOpen ? '' : 'rotate(90deg)';
    }

    // 메뉴 밖 클릭 시 자동 닫기
    if (!isOpen) {
        const closeHandler = (e) => {
            if (btn && btn.contains(e.target)) return;
            if (menu && !menu.contains(e.target)) {
                menu.classList.add('hidden');
                if (icon) { icon.classList.remove('text-amber-500'); icon.classList.add('text-gray-400'); icon.style.transform = ''; }
                document.removeEventListener('click', closeHandler);
            }
        };
        setTimeout(() => document.addEventListener('click', closeHandler), 0);
    }
}

function toggleButlerProfileEdit() {
    const panel = document.getElementById('butler-profile-editor-panel');
    if (panel) {
        panel.classList.toggle('hidden');
        // Pre-fill inputs when opened
        if (!panel.classList.contains('hidden')) {
            const nicknameInput = document.getElementById('settings-user-nickname');
            const emailInput = document.getElementById('settings-user-email');
            if (nicknameInput) nicknameInput.value = settings_nickname;
            if (emailInput) emailInput.value = settings_email;
            syncButlerAvatarDisplay();
        }
    }
}

function uploadButlerProfilePhoto(event) {
    const file = event.target.files[0];
    if (!file) return;

    if (!file.type.startsWith('image/')) {
        showToast("이미지 파일만 업로드할 수 있습니다.");
        return;
    }

    const reader = new FileReader();
    reader.onload = function(e) {
        const base64Url = e.target.result;
        
        // Save to state & LocalStorage (Account isolated)
        localStorage.setItem('petna_user_photo_url_' + settings_email, base64Url);
        settings_photo_url = base64Url;
        
        // Synchronize displays immediately
        syncButlerAvatarDisplay();
        
        if (typeof updateProfileInSupabase === 'function') {
            updateProfileInSupabase({ photo_url: base64Url });
        }
        
        showToast("집사님의 커스텀 프로필 사진이 성공적으로 등록되었습니다! 📸✨");
    };
    reader.readAsDataURL(file);
}

function syncButlerAvatarDisplay() {
    const avatarDisp = document.getElementById('settings-avatar-disp');
    const avatarImage = document.getElementById('settings-avatar-image');
    
    const stageAvatar = document.getElementById('butler-stage-avatar');
    const stageImage = document.getElementById('butler-stage-image');
    const stageName = document.getElementById('butler-stage-name');
    
    // Populate form inputs if they are currently loaded
    const nicknameInput = document.getElementById('settings-user-nickname');
    const emailInput = document.getElementById('settings-user-email');
    if (nicknameInput) nicknameInput.value = settings_nickname;
    if (emailInput) emailInput.value = settings_email;

    // 1. Collapsible Form Avatar Sync
    if (settings_photo_url) {
        if (avatarDisp) avatarDisp.classList.add('hidden');
        if (avatarImage) {
            avatarImage.src = settings_photo_url;
            avatarImage.classList.remove('hidden');
        }
    } else {
        if (avatarDisp) {
            avatarDisp.innerText = settings_avatar;
            avatarDisp.classList.remove('hidden');
        }
        if (avatarImage) avatarImage.classList.add('hidden');
    }

    // 2. Interactive Double Stage Avatar Sync
    if (settings_photo_url) {
        if (stageAvatar) stageAvatar.classList.add('hidden');
        if (stageImage) {
            stageImage.src = settings_photo_url;
            stageImage.classList.remove('hidden');
        }
    } else {
        if (stageAvatar) {
            stageAvatar.innerText = settings_avatar;
            stageAvatar.classList.remove('hidden');
        }
        if (stageImage) stageImage.classList.add('hidden');
    }
    
    if (stageName) {
        stageName.innerText = `${settings_nickname} 집사`;
    }
    
    // Sync Pet's Name in the Double Stage
    const petStageName = document.getElementById('pet-stage-name');
    if (petStageName && pets.length > 0) {
        const currentPet = getActivePet();
        if (currentPet) {
            petStageName.innerText = currentPet.name;
        }
    }
}

// 🐾 펫 프로필 & 방이름 설정 제어 함수군 (Pet Settings Functions)
function togglePetProfileEdit() {
    const panel = document.getElementById('pet-profile-editor-panel');
    if (panel) {
        panel.classList.toggle('hidden');
        if (!panel.classList.contains('hidden')) {
            const current = getActivePet();
            if (current) {
                document.getElementById('settings-pet-name').value = current.name;
                document.getElementById('settings-room-name-input').value = current.roomName || `${current.name}의 하루 방 🏠`;
                const imgEl = document.getElementById('settings-pet-image');
                if (imgEl) {
                    imgEl.src = current.imageUrl || "https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=300";
                }
                updateRoomLayoutControls(normalizeRoomLayout(current.roomLayout || 'living'));
                tempPetRoomPhotoUrl = "";
            }
        }
    }
}

let tempPetRoomPhotoUrl = "";

function uploadPetRoomPhoto(event) {
    const file = event.target.files[0];
    if (!file) return;

    if (!file.type.startsWith('image/')) {
        showToast("이미지 파일만 업로드할 수 있습니다.");
        return;
    }

    const reader = new FileReader();
    reader.onload = function(e) {
        const base64Url = e.target.result;
        tempPetRoomPhotoUrl = base64Url;
        
        const imgEl = document.getElementById('settings-pet-image');
        if (imgEl) {
            imgEl.src = base64Url;
        }
        showToast("기기 내 펫 사진이 임시 장착되었습니다! 📸");
    };
    reader.readAsDataURL(file);
}

function changePetPresetPhoto(url) {
    tempPetRoomPhotoUrl = url;
    const imgEl = document.getElementById('settings-pet-image');
    if (imgEl) {
        imgEl.src = url;
    }
    showToast("대표 펫 프리셋 사진이 선택되었습니다! 🐾");
}

function savePetProfileAndRoom() {
    const current = getActivePet();
    if (!current) return;

    const nameInput = document.getElementById('settings-pet-name').value.trim();
    const roomNameInput = document.getElementById('settings-room-name-input').value.trim();

    if (!nameInput) {
        showToast("반려동물 이름을 입력해 주세요.");
        return;
    }

    current.name = nameInput;
    current.roomName = roomNameInput || `${nameInput}의 하루 방 🏠`;
    current.roomLayout = normalizeRoomLayout(current.roomLayout || 'living');
    
    if (tempPetRoomPhotoUrl) {
        current.imageUrl = tempPetRoomPhotoUrl;
        current.type = 'custom'; // type is custom so that image is rendered
        tempPetRoomPhotoUrl = "";
    }

    saveState();
    if (typeof updatePetInSupabase === 'function') {
        updatePetInSupabase(current);
    }
    togglePetProfileEdit();
    renderMyPets();
    
    showToast("반려동물 프로필과 방 이름이 성공적으로 변경되었습니다! 🎉");
}

function triggerButlerPhotoUploadDirect() {
    const directInput = document.getElementById('butler-direct-upload');
    if (directInput) {
        directInput.click();
    }
}

function uploadButlerPhotoDirect(event) {
    const file = event.target.files[0];
    if (!file) return;

    if (!file.type.startsWith('image/')) {
        showToast("이미지 파일만 업로드할 수 있습니다.");
        return;
    }

    const reader = new FileReader();
    reader.onload = function(e) {
        const base64Url = e.target.result;
        
        // Save to state & LocalStorage (Account isolated)
        localStorage.setItem('petna_user_photo_url_' + settings_email, base64Url);
        settings_photo_url = base64Url;
        
        // Synchronize displays immediately
        syncButlerAvatarDisplay();
        showToast("집사님의 프로필 사진이 즉시 변경 및 자동 저장되었습니다! 📸🧔");
    };
    reader.readAsDataURL(file);
}

function triggerPetPhotoUploadDirect() {
    const directInput = document.getElementById('pet-direct-upload');
    if (directInput) {
        directInput.click();
    }
}

function uploadPetPhotoDirect(event) {
    const file = event.target.files[0];
    if (!file) return;

    if (!file.type.startsWith('image/')) {
        showToast("이미지 파일만 업로드할 수 있습니다.");
        return;
    }

    const reader = new FileReader();
    reader.onload = function(e) {
        const base64Url = e.target.result;
        const current = getActivePet();
        if (current) {
            current.imageUrl = base64Url;
            current.type = 'custom';
            
            saveState();
            if (typeof updatePetInSupabase === 'function') {
                updatePetInSupabase(current);
            }
            renderMyPets();
            showToast("반려동물 프로필 사진이 즉시 변경 및 자동 저장되었습니다! 📸🐾");
        } else {
            showToast("활성화된 반려동물 정보를 찾을 수 없습니다.");
        }
    };
    reader.readAsDataURL(file);
}

// ==========================================
// 🩺 헬스케어 모달 및 위젯 제어 로직
// ==========================================
let currentHealthLog = { poop: null, food: 0, water: 0 };

// 건강 퀵 요약 업데이트
function updateHealthQuickSummary() {
    const scoreEl = document.getElementById('health-quick-score');
    const streakEl = document.getElementById('health-quick-streak');
    const todayEl = document.getElementById('health-quick-today');

    if (!scoreEl || !streakEl || !todayEl) return;

    // 건강 점수
    const score = (typeof calcHealthScore === 'function') ? calcHealthScore() : 0;
    scoreEl.textContent = score || '--';

    // 연속 기록
    const streak = (typeof calcHealthStreak === 'function') ? calcHealthStreak() : 0;
    streakEl.textContent = streak ? `${streak}일` : '--일';

    // 오늘 기록 여부
    const logs = (typeof healthLogs !== 'undefined' && healthLogs?.today) ? healthLogs.today : null;
    const hasToday = logs && (logs.food > 0 || logs.water > 0 || logs.poop);
    todayEl.textContent = hasToday ? '완료' : '미기록';
    todayEl.className = hasToday ? 'text-lg font-black text-emerald-600' : 'text-lg font-black text-gray-400';
}

function renderHealthDashboard() {
    if (typeof healthLogs === 'undefined') return;
    const logs = healthLogs?.today;

    // 건강 퀵 요약 업데이트
    updateHealthQuickSummary();

    // UI 업데이트 (건강 기록 데이터)
    if (logs) {
        const poopDisp = document.getElementById('health-log-poop');
        const foodDisp = document.getElementById('health-log-food');
        const waterDisp = document.getElementById('health-log-water');

        if (poopDisp) {
            const poopIcons = { 'null': '-', 'normal': '💩', 'hard': '🪨', 'liquid': '💦' };
            poopDisp.innerText = poopIcons[logs.poop] || '-';
        }
        if (foodDisp) foodDisp.innerText = (logs.food || 0) + ' g';
        if (waterDisp) waterDisp.innerText = (logs.water || 0) + ' ml';
    }

    // 💡 맞춤 건강/생활 팁 업데이트
    const tipDisp = document.getElementById('personalized-health-tip');
    if (tipDisp) {
        const pet = getActivePet();
        const tips = [
            "🔥 불(火)의 기운이 강한 사주예요! 심장과 혈관 건강을 위해 오늘 음수량을 50ml 더 늘려주시면 좋습니다.",
            "🌳 나무(木)의 기운을 타고났군요! 간과 관절이 약해질 수 있으니 무리한 점프보다는 평지 산책을 권장합니다.",
            "💧 물(水)의 기운을 가진 아이입니다! 신장과 비뇨기 케어가 중요해요. 신선한 물을 자주 교체해 주세요.",
            "⛰️ 흙(土)의 기운을 지닌 펫이네요! 소화기관이 예민할 수 있으니 간식을 줄이고 정량의 사료를 급여해 주세요.",
            "⚔️ 금(金)의 기운을 품었습니다! 호흡기나 피부 쪽에 알러지가 생기기 쉬워요. 실내 환기와 보습에 신경 써 주세요!",
            "✨ 호기심이 많은 요정(ENFP) 타입이에요! 활동량이 많으니 양질의 단백질 식단을 추가해주시면 활력 유지에 좋습니다.",
            "🛡️ 든든한 리더형(ENTJ) 타입입니다! 체력 소모가 큰 편이므로 규칙적인 식사 시간과 충분한 수면이 필수입니다."
        ];
        
        let hash = 0;
        const name = pet ? pet.name : "댕이";
        for (let i = 0; i < name.length; i++) {
            hash = name.charCodeAt(i) + ((hash << 5) - hash);
        }
        // 날짜에 따라서도 조금씩 바뀌게 (옵션)
        const dateOffset = new Date().getDate();
        const idx = Math.abs(hash + dateOffset) % tips.length;
        tipDisp.innerText = tips[idx];
    }
}

function updateDDayBubble() {
    const bubble = document.getElementById('pet-dday-bubble');
    const bubbleText = document.getElementById('pet-dday-text');
    if (!bubble || !bubbleText || typeof schedules === 'undefined' || !schedules || schedules.length === 0) return;
    
    const today = new Date();
    today.setHours(0,0,0,0);
    
    const upcoming = schedules.map(s => {
        const sDate = new Date(s.date);
        const diffTime = sDate - today;
        const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
        return { ...s, diffDays };
    }).filter(s => s.diffDays >= 0).sort((a, b) => a.diffDays - b.diffDays);
    
    if (upcoming.length > 0) {
        const closest = upcoming[0];
        const ddayStr = closest.diffDays === 0 ? 'D-Day' : `D-${closest.diffDays}`;
        bubbleText.innerText = `${ddayStr} ${closest.title}`;
        bubble.classList.remove('hidden');
    } else {
        bubble.classList.add('hidden');
    }
}

function openHealthLogModal() {
    const modal = document.getElementById('health-log-modal');
    if (!modal) return;
    
    const logs = (typeof healthLogs !== 'undefined' && healthLogs?.today) ? healthLogs.today : { poop: null, food: 0, water: 0 };
    currentHealthLog = { ...logs };
    
    selectPoopType(currentHealthLog.poop);
    
    const foodSlider = document.getElementById('food-amount-slider');
    const foodDisp = document.getElementById('food-amount-disp');
    if (foodSlider && foodDisp) {
        foodSlider.value = currentHealthLog.food || 0;
        foodDisp.innerText = (currentHealthLog.food || 0) + 'g';
    }
    
    const waterSlider = document.getElementById('water-amount-slider');
    const waterDisp = document.getElementById('water-amount-disp');
    if (waterSlider && waterDisp) {
        waterSlider.value = currentHealthLog.water || 0;
        waterDisp.innerText = (currentHealthLog.water || 0) + 'ml';
    }
    
    modal.classList.remove('hidden');
    modal.classList.add('flex');
}

function closeHealthLogModal() {
    const modal = document.getElementById('health-log-modal');
    if (modal) {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }
}

// 💡 월간 맞춤 헬스 리포트 모달 제어 (Phase 3 Click-Flow)
function openHealthReportModal() {
    const modal = document.getElementById('health-report-modal');
    if (modal) {
        modal.classList.remove('hidden');
        modal.classList.add('flex');
    }
}

function closeHealthReportModal() {
    const modal = document.getElementById('health-report-modal');
    if (modal) {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }
}

function selectPoopType(type) {
    const normalizedType = (type === null || type === undefined || type === 'null') ? null : type;
    const selectedId = normalizedType === null ? 'poop-type-null' : `poop-type-${normalizedType}`;
    currentHealthLog.poop = normalizedType;
    const btns = document.querySelectorAll('.health-poop-btn');
    btns.forEach(btn => {
        const selected = btn.id === selectedId;
        btn.setAttribute('aria-pressed', selected ? 'true' : 'false');
        if (selected) {
            btn.classList.add('border-brand-500', 'bg-brand-50', 'text-brand-700', 'shadow-sm', 'ring-2', 'ring-brand-200');
            btn.classList.remove('border-gray-200', 'bg-white', 'text-gray-400', 'text-gray-700');
        } else {
            btn.classList.remove('border-brand-500', 'bg-brand-50', 'text-brand-700', 'shadow-sm', 'ring-2', 'ring-brand-200');
            btn.classList.add('border-gray-200', 'bg-white');
            if (btn.id === 'poop-type-null') {
                btn.classList.add('text-gray-400');
                btn.classList.remove('text-gray-700');
            } else {
                btn.classList.add('text-gray-700');
                btn.classList.remove('text-gray-400');
            }
        }
    });
}

function saveHealthLog() {
    const foodSlider = document.getElementById('food-amount-slider');
    const waterSlider = document.getElementById('water-amount-slider');
    
    if (foodSlider) currentHealthLog.food = parseInt(foodSlider.value);
    if (waterSlider) currentHealthLog.water = parseInt(waterSlider.value);
    
    if (typeof healthLogs !== 'undefined') {
        if (!healthLogs.today) healthLogs.today = {};
        healthLogs.today = {
            date: new Date().toISOString().split('T')[0],
            ...currentHealthLog
        };
        if (typeof saveHealthHistoryToday === 'function') saveHealthHistoryToday();
        else if (typeof saveState === 'function') saveState();
    }

    // 화면 갱신 — 건강 탭 표시 ID(health-today-*)와 대시보드 모두 업데이트
    if (typeof renderHealthTab === 'function') renderHealthTab();
    else renderHealthDashboard();

    closeHealthLogModal();
    if (typeof showToast === 'function') showToast("오늘의 스마트 건강 기록 일지가 저장되었습니다! 📝");
}

// 기존 renderMyPets 후크 (이미 있는 함수 덮어쓰기)
if (typeof renderMyPets === 'function') {
    const originalRenderMyPets = renderMyPets;
    renderMyPets = function() {
        originalRenderMyPets();
        setTimeout(() => {
            renderHealthDashboard();
            updateDDayBubble();
        }, 50);
    };
}

function switchMypetSubTab(subTab, sajuSubTab = null) {
    const homeBtn = document.getElementById('mypet-subtab-home-btn');
    const playBtn = document.getElementById('mypet-subtab-play-btn');
    const homeContent = document.getElementById('mypet-subtab-home-content');
    const playContent = document.getElementById('mypet-subtab-play-content');

    if (!homeBtn || !playBtn || !homeContent || !playContent) return;

    if (subTab === 'home') {
        homeContent.classList.remove('hidden');
        homeContent.classList.add('block');
        playContent.classList.add('hidden');
        playContent.classList.remove('block');

        homeBtn.className = "flex-1 py-2 px-4 rounded-full text-xs font-black transition-all bg-brand-500 text-white shadow-sm flex items-center justify-center gap-1.5 outline-none";
        playBtn.className = "flex-1 py-2 px-4 rounded-full text-xs font-bold transition-all text-gray-500 hover:text-gray-700 flex items-center justify-center gap-1.5 outline-none";

        renderMyPets();
    } else if (subTab === 'play') {
        playContent.classList.remove('hidden');
        playContent.classList.add('block');
        homeContent.classList.add('hidden');
        homeContent.classList.remove('block');

        playBtn.className = "flex-1 py-2 px-4 rounded-full text-xs font-black transition-all bg-brand-500 text-white shadow-sm flex items-center justify-center gap-1.5 outline-none";
        homeBtn.className = "flex-1 py-2 px-4 rounded-full text-xs font-bold transition-all text-gray-500 hover:text-gray-700 flex items-center justify-center gap-1.5 outline-none";

        if (sajuSubTab) {
            if (typeof switchSajuSubTab === 'function') {
                switchSajuSubTab(sajuSubTab);
            }
        } else {
            if (typeof renderSajuTab === 'function') {
                renderSajuTab();
            }
        }
    }
}

function goToMbtiTest() {
    switchTab('saju');
    if (typeof switchSajuSubTab === 'function') {
        switchSajuSubTab('mbti');
    }
}

function goToIqTest() {
    switchTab('saju');
    if (typeof switchSajuSubTab === 'function') {
        switchSajuSubTab('petIq');
    }
}

function shareSajuToFeed() {
    const current = getActivePet();
    if (!current || !current.sajuData) {
        showToast("⚠️ 분석된 사주 결과가 없습니다.");
        return;
    }

    const pName = current.name || "댕이";
    const compatTitle = current.sajuData.compatTitle || "완벽한 조화";
    const compatScore = current.sajuData.compatScore || "95";
    const petSummary = current.sajuData.petSummary || "";
    const ownerSummary = current.sajuData.ownerSummary || "";
    const pastDesc = current.sajuData.pastDesc || "";
    const tagsStr = "#펫나사주 #영혼조화도 #반려견조화도 #PetNa";

    let postContent = `🔮 [펫과나 평생 사주 & 조화도 매칭 완료] 🔮\n\n` +
        `🐾 ${pName}의 사주 기질: [ ${petSummary} ]\n` +
        `🧔 집사의 사주 기질: [ ${ownerSummary} ]\n\n` +
        `💖 조화도 지표: "${compatTitle}" (점수: ${compatScore}점)\n` +
        `📜 전생 인연설: ${pastDesc}\n\n` +
        `우리 아이와 저의 평생 인연 분석 결과입니다. 정말 신기하게 딱 들어맞네요! 🥰 이웃님들도 아이와의 조화도를 한 번 분석해보세요! ${tagsStr}`;

    const newPost = {
        id: Date.now(),
        petName: pName,
        petAvatar: current.type === 'custom' ? current.imageUrl : "https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=150",
        content: postContent,
        image: "https://images.unsplash.com/photo-1608889175123-8ec330b86f84?auto=format&fit=crop&q=80&w=600",
        isVideo: false,
        likes: 0,
        liked: false,
        comments: []
    };

    posts.unshift(newPost);
    saveState();
    if (typeof uploadPostToSupabase === 'function') {
        uploadPostToSupabase(newPost);
    }
    showToast("📢 사주 분석 결과가 자랑 피드에 멋지게 공유되었습니다!");
    switchTab('social');
    switchSocialSubTab('feed');
}

function triggerAiHealthAnalysis() {
    if (!canUseAI()) {
        showToast("이번 달 무료 분석 3회를 모두 사용했습니다. 프리미엄에서 무제한 사용 가능합니다 🐾");
        if (typeof showPremiumModal === 'function') showPremiumModal();
        return;
    }
    document.getElementById("ai-health-photo-input")?.click();
}

async function runAiHealthAnalysis(event) {
    const file = event.target.files[0];
    if (!file) return;
    const btn = document.getElementById("ai-health-analyze-btn");
    const resultEl = document.getElementById("ai-health-result");
    if (!resultEl) return;

    btn.disabled = true;
    btn.innerHTML = '<i class="fa-solid fa-spinner fa-spin text-xs"></i> 분석 중...';
    resultEl.classList.remove("hidden");
    resultEl.innerHTML = '<p class="text-xs text-brand-500 text-center py-4 font-black">AI가 사진을 분석하고 있습니다... 🔍</p>';

    const pet = getActivePet();
    try {
        const base64 = await imageFileToBase64(file);
        const result = await analyzeHealthFromPhoto(base64, pet?.name || "펫");

        if (result.error) {
            resultEl.innerHTML = `<p class="text-xs text-red-500 font-bold p-3 text-center">${escapeHtml(result.message)}</p>`;
        } else {
            saveHealthAnalysis(result);
            incrementAIUsage();
            if (typeof updateAiHealthUsageBadge === 'function') updateAiHealthUsageBadge();
            resultEl.innerHTML = _buildHealthResultCard(result);
            document.getElementById('ai-health-share-btn-wrap')?.classList.remove('hidden');
        }
    } catch (e) {
        resultEl.innerHTML = `<p class="text-xs text-red-500 font-bold p-3 text-center">오류 발생: ${e.message}</p>`;
    }
    btn.disabled = false;
    btn.innerHTML = '<i class="fa-solid fa-camera text-xs"></i> 사진 분석';
    event.target.value = "";
}

function _buildHealthResultCard(result) {
    const score = result.score ?? 0;
    const scoreColor = score >= 80 ? "#10b981" : score >= 60 ? "#f59e0b" : "#ef4444";
    const scoreBg = score >= 80 ? "from-emerald-50 to-teal-50/60 border-emerald-100" : score >= 60 ? "from-amber-50 to-yellow-50/60 border-amber-100" : "from-red-50 to-rose-50/60 border-red-100";

    const statusClass = (val) => {
        if (!val || val === "확인불가") return "bg-gray-100 text-gray-400";
        if (["정상","윤기있음","촉촉함","적정","활발"].includes(val)) return "bg-emerald-100 text-emerald-700";
        if (["주의","건조함","보통","저체중","과체중"].includes(val)) return "bg-amber-100 text-amber-700";
        return "bg-red-100 text-red-700";
    };

    const items = [
        { label:"눈", val: result.eyes },
        { label:"귀", val: result.ears },
        { label:"피부", val: result.skin },
        { label:"털", val: result.coat },
        { label:"치아", val: result.teeth },
        { label:"코", val: result.nose },
        { label:"자세", val: result.posture },
        { label:"체중", val: result.weight },
        { label:"활력", val: result.alertness },
        { label:"발", val: result.paw },
    ].filter(i => i.val && i.val !== "확인불가");

    const itemsHtml = items.map(i => `
        <div class="flex flex-col items-center gap-0.5 bg-white/70 rounded-xl p-1.5 border border-white">
            <span class="text-[8px] text-gray-400 font-bold">${i.label}</span>
            <span class="text-[9px] font-black px-1.5 py-0.5 rounded-full ${statusClass(i.val)}">${escapeHtml(i.val)}</span>
        </div>`).join('');

    const urgentBanner = result.urgent ? `
        <div class="flex items-center gap-2 bg-red-50 border border-red-200 rounded-xl px-3 py-2">
            <i class="fa-solid fa-triangle-exclamation text-red-500 text-sm"></i>
            <p class="text-[11px] text-red-600 font-black">${escapeHtml(result.urgentReason || "빠른 수의사 상담이 필요합니다")}</p>
        </div>` : '';

    return `
        <div class="bg-gradient-to-br ${scoreBg} border rounded-2xl p-3.5 space-y-3 animate-fade-in">
            ${urgentBanner}
            <div class="flex items-center gap-3">
                <div class="flex flex-col items-center min-w-[56px] bg-white/80 rounded-2xl p-2.5 border border-white shadow-xs">
                    <span class="text-2xl font-black" style="color:${scoreColor}">${score}</span>
                    <span class="text-[8px] text-gray-400 font-bold mt-0.5">건강점수</span>
                </div>
                <div class="flex-1 space-y-1.5">
                    <p class="text-[11px] text-gray-700 font-medium leading-snug">${escapeHtml(result.summary || "")}</p>
                    <p class="text-[10px] text-brand-600 font-bold">${escapeHtml(result.advice || "")}</p>
                </div>
            </div>
            ${items.length > 0 ? `<div class="grid grid-cols-5 gap-1">${itemsHtml}</div>` : ""}
            <div class="flex gap-1.5">
                <button onclick="shareHealthCard()" class="flex items-center gap-1 px-2.5 py-1.5 bg-white/80 hover:bg-white border border-brand-200 text-brand-700 font-black text-[10px] rounded-xl transition-all">
                    <i class="fa-solid fa-image text-[9px]"></i> 카드 저장
                </button>
                <button onclick="shareAiHealthToFeed()" class="flex items-center gap-1 px-2.5 py-1.5 bg-brand-500 hover:bg-brand-600 text-white font-black text-[10px] rounded-xl transition-all">
                    <i class="fa-solid fa-share-nodes text-[9px]"></i> 피드 공유
                </button>
            </div>
        </div>`;
}

// 음성 문진
let _speechRecognition = null;

const VOICE_FREE_LIMIT = 5; // 무료 월 5회

function canUseVoice() {
    if (typeof isPremium === 'function' && isPremium()) return true;
    const key = "petna_voice_count_" + new Date().toISOString().slice(0, 7);
    return parseInt(localStorage.getItem(key) || "0") < VOICE_FREE_LIMIT;
}

function incrementVoiceUsage() {
    const key = "petna_voice_count_" + new Date().toISOString().slice(0, 7);
    localStorage.setItem(key, String(parseInt(localStorage.getItem(key) || "0") + 1));
    localStorage.setItem("petna_voice_used", "1");
}

async function startVoiceConsultation() {
    if (!canUseVoice()) {
        showToast("이번 달 무료 음성 문진 5회를 모두 사용했습니다. 프리미엄에서 무제한 사용 가능합니다 🎙️");
        if (typeof showPremiumModal === 'function') showPremiumModal();
        return;
    }
    const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SpeechRecognition) {
        showToast("음성 인식은 Chrome/Safari에서 지원됩니다.");
        return;
    }
    const voiceBtn = document.getElementById("ai-voice-btn");
    const voiceResult = document.getElementById("ai-voice-result");
    if (!voiceBtn || !voiceResult) return;

    if (_speechRecognition) {
        _speechRecognition.stop();
        _speechRecognition = null;
        voiceBtn.innerHTML = '<i class="fa-solid fa-microphone text-xs"></i> 증상 말하기';
        voiceBtn.classList.remove("bg-red-500","hover:bg-red-600");
        voiceBtn.classList.add("bg-brand-500","hover:bg-brand-600");
        return;
    }

    const recognition = new SpeechRecognition();
    recognition.lang = "ko-KR";
    recognition.continuous = false;
    recognition.interimResults = false;
    _speechRecognition = recognition;

    voiceBtn.innerHTML = '<i class="fa-solid fa-circle-stop text-xs"></i> 듣는 중... (탭하면 완료)';
    voiceBtn.classList.remove("bg-brand-500","hover:bg-brand-600");
    voiceBtn.classList.add("bg-red-500","hover:bg-red-600");
    voiceResult.classList.remove("hidden");
    voiceResult.innerHTML = '<p class="text-[11px] text-brand-500 text-center py-2 font-black">🎙️ 증상을 말해주세요...</p>';

    recognition.onresult = async (e) => {
        const transcript = e.results[0][0].transcript;
        // 결과 받으면 즉시 마이크 중단
        recognition.stop();
        _speechRecognition = null;
        voiceBtn.innerHTML = '<i class="fa-solid fa-microphone text-xs"></i> 증상 말하기';
        voiceBtn.classList.remove("bg-red-500","hover:bg-red-600");
        voiceBtn.classList.add("bg-brand-500","hover:bg-brand-600");

        voiceResult.innerHTML = `<p class="text-[11px] text-gray-500 font-medium p-2">"${escapeHtml(transcript)}"<br><span class="text-brand-500 font-black">AI 분석 중...</span></p>`;
        const pet = getActivePet();
        const analysis = await analyzeSymptomByVoice(transcript, pet?.name || "펫");
        incrementVoiceUsage();
        voiceResult.innerHTML = _buildVoiceResultCard(analysis);
    };

    recognition.onerror = () => {
        voiceResult.innerHTML = '<p class="text-xs text-red-500 p-2">인식 실패. 다시 시도해주세요.</p>';
    };

    recognition.onend = () => {
        _speechRecognition = null;
        voiceBtn.innerHTML = '<i class="fa-solid fa-microphone text-xs"></i> 증상 말하기';
        voiceBtn.classList.remove("bg-red-500","hover:bg-red-600");
        voiceBtn.classList.add("bg-brand-500","hover:bg-brand-600");
    };

    recognition.start();
}

function _buildVoiceResultCard(analysis) {
    if (analysis.error) return `<p class="text-xs text-red-500 p-2">${escapeHtml(analysis.message)}</p>`;
    const urgencyColor = {
        "즉시": "text-red-600 bg-red-50 border-red-200",
        "24시간내": "text-amber-600 bg-amber-50 border-amber-200",
        "일주일내": "text-blue-600 bg-blue-50 border-blue-200",
        "관찰": "text-emerald-600 bg-emerald-50 border-emerald-200",
    }[analysis.urgency] || "text-gray-600 bg-gray-50 border-gray-200";

    const causesHtml = (analysis.possibleCauses || []).map(c =>
        `<li class="text-[10px] text-gray-600 font-medium">• ${escapeHtml(c)}</li>`
    ).join('');

    return `
        <div class="space-y-2 p-1">
            <div class="flex items-center gap-1.5">
                <span class="text-[9px] font-black px-2 py-0.5 rounded-full border ${urgencyColor}">${escapeHtml(analysis.urgency || "관찰")}</span>
                ${analysis.needsVet ? '<span class="text-[9px] font-black px-2 py-0.5 rounded-full bg-red-100 text-red-700 border border-red-200">수의사 상담 권고</span>' : ''}
            </div>
            <p class="text-[11px] text-gray-700 font-medium leading-snug">${escapeHtml(analysis.summary || "")}</p>
            ${causesHtml ? `<ul class="space-y-0.5">${causesHtml}</ul>` : ""}
            <div class="bg-brand-50 border border-brand-100 rounded-xl px-2.5 py-1.5">
                <p class="text-[10px] text-brand-700 font-black">지금 할 수 있는 것</p>
                <p class="text-[10px] text-brand-600 font-medium">${escapeHtml(analysis.immediateAction || "")}</p>
            </div>
        </div>`;
}

// 건강 스트릭 UI 업데이트
function renderHealthStreak() {
    const el = document.getElementById("health-streak-badge");
    if (!el) return;
    const streak = typeof calcHealthStreak === 'function' ? calcHealthStreak() : 0;
    if (typeof checkStreakMilestone === 'function') checkStreakMilestone(streak);

    let html = '';
    if (streak === 0) {
        html = '<span class="text-[10px] text-gray-400 font-bold">오늘 첫 기록을 남겨보세요!</span>';
    } else if (streak < 3) {
        html = `<span class="text-[10px] font-black text-amber-500">🌱 ${streak}일 연속 중!</span>`;
    } else if (streak < 7) {
        html = `<span class="text-[10px] font-black text-orange-500">🔥 ${streak}일 연속! 습관이 되고 있어요</span>`;
    } else if (streak < 30) {
        html = `<span class="text-[10px] font-black text-brand-600">🔥🔥 ${streak}일 연속 🥉 훌륭한 집사예요!</span>`;
    } else if (streak < 100) {
        html = `<span class="text-[10px] font-black text-brand-600">🔥🔥🔥 ${streak}일 연속 🥈 전설의 집사!</span>`;
    } else {
        html = `<span class="text-[10px] font-black text-amber-600">👑 ${streak}일 연속 🥇 펫과나 명예의 전당!</span>`;
    }
    el.innerHTML = html;
}

function shareAiHealthToFeed() {
    const analyses = typeof getHealthAnalyses === 'function' ? getHealthAnalyses() : [];
    const latest = analyses[0];
    if (!latest) return;
    const pet = getActivePet();
    const petName = pet?.name || "댕이";
    const petAvatar = pet ? (pet.type === 'custom' ? pet.imageUrl : "https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=150") : "https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=150";
    const newPost = {
        id: Date.now(),
        petName,
        petAvatar,
        content: `${petName}의 AI 건강점수: ${latest.score}점 🏥`,
        image: null,
        isVideo: false,
        videoUrl: null,
        attachedWalk: null,
        attachedHealth: null,
        attachedAiHealth: {
            score: latest.score,
            eyes: latest.eyes,
            skin: latest.skin,
            body: latest.body || "정상",
            summary: latest.summary,
            advice: latest.advice,
            analyzedAt: latest.analyzedAt
        },
        likes: 0,
        liked: false,
        comments: []
    };
    if (typeof posts !== 'undefined') {
        posts.unshift(newPost);
        saveState();
        showToast("AI 건강 분석 결과가 피드에 공유되었습니다! 🏥");
        switchTab('social');
        switchSocialSubTab('feed');
    }
}


