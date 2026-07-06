// 선택된 상점 퀘스트 데이터
const SHOP_QUEST_DATA = {
    'healing-spa': {
        badge: 'SPA & HOTSPRING',
        title: '포레스트 힐 펫 스파',
        emoji: '🛁',
        desc: '최고급 천연 허브 탄산천 스파와 아로마 케어를 통해 반려동물의 누적된 피로를 완벽히 씻어내고 모질을 개선합니다.',
        reward: '스파 서비스 이용 시 탄산스파 15% 즉시 할인',
        rewardBadge: '상생 혜택'
    },
    'healing-camping': {
        badge: 'CAMPING',
        title: '도그빌 오션 캠핑장',
        emoji: '🏕️',
        desc: '드넓은 바다가 보이는 잔디밭 위에서 반려동물과 함께 자유롭게 뛰어놀고 펫 전용 글램핑 텐트와 안전한 울타리를 즐길 수 있는 최고의 캠핑 사이트입니다.',
        reward: '캠핑장 예약 시 웰컴 펫 바비큐 키트 무료 증정',
        rewardBadge: '상생 혜택'
    },
    'healing-therapy': {
        badge: 'THERAPY',
        title: '아로마 펫 테라피 살롱',
        emoji: '🌸',
        desc: '반려동물 사주 및 스트레스 진단 분석에 따른 맞춤형 천연 아로마 오일 도포와 1:1 관절 테라피 마사지로 심신을 안정시킵니다.',
        reward: '첫 방문 시 사주 맞춤형 아로마 미스트 1병 증정',
        rewardBadge: '상생 혜택'
    },
    'healing-hospital': {
        badge: 'EMERGENCY',
        title: '24시 센트럴 메디컬 센터',
        emoji: '🏥',
        desc: '석박사 수의사 의료진들이 상주하는 24시간 응급 진료 및 안심 외과 전문 수술실을 갖춘 영남권 최대 규모의 동물 메디컬 종합 병원입니다.',
        reward: '첫 방문 정밀 혈액검사 및 엑스레이 20% 우대',
        rewardBadge: '의료 지원'
    },
    'healing-hotel': {
        badge: 'PET HOTEL',
        title: '가든 테라스 펫 리조트',
        emoji: '🏨',
        desc: '1인 1실 친환경 개별 바닥 난방 객실과 넓은 개별 테라스, 전담 훈련사가 상주하는 놀이 케어 및 24시간 안심 CCTV 밀착 돌봄 서비스를 제공합니다.',
        reward: '2박 이상 숙박 시 무료 펫 스파 케어 1회 연계',
        rewardBadge: '돌봄 혜택'
    },
    'healing-shopping': {
        badge: 'SHOPPING',
        title: '펫라이프 프리미엄 멀티샵',
        emoji: '🛒',
        desc: '엄선된 유기농 홀리스틱 사료, 천연 수제 간식, 해외 명품 프리미엄 반려동물 어패럴 및 지능 개발 토이까지 한자리에 모은 플래그십 스퀘어입니다.',
        reward: '3만원 이상 안심 결제 시 5,000원 즉시 캐시백',
        rewardBadge: '쇼핑 혜택'
    }
};

let activeShopId = null;
let activeLocation = null;

function getQuestIdByCategory(category) {
    switch(category) {
        case 'grooming': return 'healing-spa';
        case 'hospital': return 'healing-hospital';
        case 'hotel': return 'healing-hotel';
        case 'cafe':
        case 'shop': return 'healing-shopping';
        case 'training': return 'healing-therapy';
        default: return 'healing-spa';
    }
}

function selectIslandShop(shopId, location = null) {
    const data = SHOP_QUEST_DATA[shopId];
    if (!data) return;

    activeShopId = shopId;

    const defaultState = document.getElementById('quest-default-state');
    const activeState = document.getElementById('quest-active-state');
    
    if (defaultState && activeState) {
        defaultState.style.display = 'none';
        activeState.style.display = 'flex';
        activeState.classList.remove('hidden');

        document.getElementById('quest-badge').innerText = data.badge;
        document.getElementById('quest-title').innerText = data.title;
        document.getElementById('quest-emoji').innerText = data.emoji;
        document.getElementById('quest-desc').innerText = data.desc;
        document.getElementById('quest-reward-name').innerText = data.reward;
        const rewardBadgeEl = document.getElementById('quest-reward-badge');
        if (rewardBadgeEl) rewardBadgeEl.innerText = data.rewardBadge;
    }

    // 모든 핀 스타일 복원
    document.querySelectorAll('.petlife-pin').forEach(p => {
        p.classList.remove('active');
        p.style.transform = '';
    });

    // 만약 location이 전달되지 않았다면 해당 카테고리에 속하는 첫 번째 가맹점 찾기
    if (!location && typeof PETLIFE_REAL_LOCATIONS !== 'undefined') {
        location = PETLIFE_REAL_LOCATIONS.find(loc => getQuestIdByCategory(loc.category) === shopId);
    }

    activeLocation = location;

    if (location) {
        const pinEl = document.getElementById(`petlife-pin-${location.id}`);
        if (pinEl) {
            pinEl.classList.add('active');
            pinEl.style.transform = 'translate(-50%, -50%) scale(1.3) translateY(-4px)';
        }

        // 지시선(Connector Line) 및 정보 말풍선(Callout Bubble) 동적 렌더링
        const connectorLine = document.getElementById('map-connector-line');
        const calloutBubble = document.getElementById('map-html-callout');
        const calloutText = document.getElementById('map-callout-text');

        const leftPct = parseFloat(location.position.left);
        const topPct = parseFloat(location.position.top);

        if (calloutBubble && calloutText) {
            calloutBubble.style.left = location.position.left;
            calloutBubble.style.top = location.position.top;
            calloutText.textContent = location.name;
            calloutBubble.classList.remove('hidden');
            calloutBubble.style.display = 'block';
        }

        if (connectorLine) {
            // viewBox가 0 0 100 100이므로 퍼센트 수치 그대로 사용 가능
            connectorLine.setAttribute('d', `M ${leftPct} ${topPct} L 100 ${topPct}`);
            connectorLine.setAttribute('opacity', '0.8');
        }
        
        if (typeof showToast === 'function') {
            showToast(`🗺️ ${location.name} 영토가 활성화되었습니다!`);
        }
    } else {
        if (typeof showToast === 'function') {
            showToast(`🗺️ ${data.title} 영토가 활성화되었습니다!`);
        }
    }
}

function closeQuestPanel() {
    activeShopId = null;
    activeLocation = null;
    const defaultState = document.getElementById('quest-default-state');
    const activeState = document.getElementById('quest-active-state');
    if (defaultState && activeState) {
        defaultState.style.display = '';
        activeState.style.display = 'none';
        activeState.classList.add('hidden');
    }

    // 말풍선 및 지시선 제거
    const calloutBubble = document.getElementById('map-callout');
    const connectorLine = document.getElementById('map-connector-line');
    if (calloutBubble) {
        calloutBubble.classList.add('hidden');
        calloutBubble.style.display = 'none';
    }
    if (connectorLine) connectorLine.setAttribute('opacity', '0');

    // 모든 핀 스타일 복원
    document.querySelectorAll('.petlife-pin').forEach(pin => {
        pin.style.transform = '';
        pin.classList.remove('active');
    });
}

function openActiveQuestLink() {
    if (activeLocation) {
        openPetlifePopup(activeLocation);
    } else if (activeShopId) {
        const targetSection = document.getElementById('section-' + activeShopId);
        if (targetSection) {
            if (targetSection.classList.contains('hidden')) {
                toggleShopSection(activeShopId);
            }
            targetSection.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
    }
}

function applyMapFilters() {
    // 펫라이프 핀 필터 적용
    filterPetlifePins();
}

// 펫라이프 핀 렌더링
function renderPetlifePins() {
    const container = document.getElementById('petlife-pins-container');
    if (!container || typeof PETLIFE_REAL_LOCATIONS === 'undefined') return;

    container.innerHTML = '';

    PETLIFE_REAL_LOCATIONS.forEach((location, index) => {
        const pin = document.createElement('div');
        pin.className = 'petlife-pin';
        pin.id = `petlife-pin-${location.id}`;
        pin.style.left = location.position.left;
        pin.style.top = location.position.top;
        pin.style.background = location.color;
        pin.style.animationDelay = `${index * 0.05}s`;
        pin.innerHTML = `<span>${location.emoji}</span>`;
        
        const questId = getQuestIdByCategory(location.category);
        pin.onclick = () => {
            selectIslandShop(questId, location);
        };
        container.appendChild(pin);
    });

    // 초기 필터링 적용
    filterPetlifePins();
}

// 가맹점 상세 팝업 열기
function openPetlifePopup(location) {
    const popup = document.getElementById('location-popup');
    if (!popup) return;

    // 데이터 채우기
    document.getElementById('popup-emoji').textContent = location.emoji;
    document.getElementById('popup-name').textContent = location.name;
    document.getElementById('popup-category').textContent = CATEGORY_NAMES[location.category] || location.category;
    document.getElementById('popup-description').textContent = location.description;
    document.getElementById('popup-address').textContent = location.address;

    const phoneLink = document.getElementById('popup-phone');
    phoneLink.textContent = location.phone;
    phoneLink.href = `tel:${location.phone.replace(/[^0-9]/g, '')}`;

    document.getElementById('popup-hours').textContent = location.hours;

    // 서비스 태그
    const servicesContainer = document.getElementById('popup-services');
    servicesContainer.innerHTML = location.services.map(service =>
        `<span class="service-tag">${service}</span>`
    ).join('');

    // 버튼 링크
    const websiteBtn = document.getElementById('popup-website-btn');
    websiteBtn.href = location.website;
    if (location.website === '#') {
        websiteBtn.style.display = 'none';
    } else {
        websiteBtn.style.display = 'flex';
    }

    const phoneBtn = document.getElementById('popup-phone-btn');
    phoneBtn.href = `tel:${location.phone.replace(/[^0-9]/g, '')}`;

    // 팝업 표시
    popup.classList.remove('hidden');
    document.body.style.overflow = 'hidden';

    if (typeof showToast === 'function') {
        showToast(`${location.emoji} ${location.name} 정보를 확인하세요!`);
    }
}

// 가맹점 팝업 닫기
function closePetlifePopup() {
    const popup = document.getElementById('location-popup');
    if (popup) {
        popup.classList.add('hidden');
        document.body.style.overflow = '';
    }
}

// 핀 필터링
function filterPetlifePins() {
    if (typeof PETLIFE_REAL_LOCATIONS === 'undefined') return;

    const fSpa = document.getElementById('f-spa')?.checked;
    const fMedical = document.getElementById('f-medical')?.checked;
    const fHotel = document.getElementById('f-hotel')?.checked;
    const fShop = document.getElementById('f-shop')?.checked;

    const pins = document.querySelectorAll('.petlife-pin');
    let activeCount = 0;

    pins.forEach((pin, index) => {
        const location = PETLIFE_REAL_LOCATIONS[index];
        if (!location) return;

        let show = true;

        // 카테고리별 필터링
        if (location.category === 'grooming' && !fSpa) show = false;
        if (location.category === 'hospital' && !fMedical) show = false;
        if (location.category === 'hotel' && !fHotel) show = false;
        if (location.category === 'shop' && !fShop) show = false;
        if (location.category === 'cafe' && !fShop) show = false;
        if (location.category === 'training' && !fMedical) show = false;

        if (show) {
            pin.style.opacity = '1';
            pin.style.pointerEvents = 'auto';
            activeCount++;
        } else {
            pin.style.opacity = '0.15';
            pin.style.pointerEvents = 'none';
        }
    });

    // 통계 바 업데이트
    const statBar = document.getElementById('stat-bar');
    const statLabel = document.getElementById('stat-label');
    if (statBar) statBar.style.width = `${(activeCount / PETLIFE_REAL_LOCATIONS.length) * 100}%`;
    if (statLabel) statLabel.innerText = `${activeCount} / ${PETLIFE_REAL_LOCATIONS.length} 활성`;
}

// 팝업 외부 클릭 시 닫기
document.addEventListener('click', function(e) {
    const popup = document.getElementById('location-popup');
    if (popup && e.target === popup) {
        closePetlifePopup();
    }
});

// ESC 키로 팝업 닫기
document.addEventListener('keydown', function(e) {
    if (e.key === 'Escape') {
        closePetlifePopup();
    }
});

// 전역 함수 등록
window.renderPetlifePins = renderPetlifePins;
window.openPetlifePopup = openPetlifePopup;
window.closePetlifePopup = closePetlifePopup;
window.filterPetlifePins = filterPetlifePins;

function toggleShopSection(id) {
    const section = document.getElementById('section-' + id);
    const icon    = document.getElementById('icon-' + id);
    if (!section) return;
    const isOpen = !section.classList.contains('hidden');
    section.classList.toggle('hidden', isOpen);
    if (icon) icon.style.transform = isOpen ? '' : 'rotate(180deg)';
}

function renderShop() {
    const container = document.getElementById('shop-product-list');
    if (!container) return;

    container.innerHTML = '';

    INITIAL_PRODUCTS.forEach(p => {
        const col = document.createElement('div');
        col.className = "bg-white border border-amber-50 rounded-3xl overflow-hidden p-4 shadow-sm flex flex-col justify-between relative hover:shadow-md transition-shadow duration-300";
        col.innerHTML = `
            <div>
                <div class="relative w-full aspect-square bg-brand-50 rounded-2xl overflow-hidden mb-3.5">
                    <img loading="lazy" src="${p.image}" class="w-full h-full object-cover">
                    <span class="absolute top-2.5 left-2.5 bg-brand-500 text-white font-mono text-[9px] font-black px-2 py-0.5 rounded-full uppercase tracking-wider">${p.category}</span>
                </div>
                <h4 class="font-bold text-gray-800 text-xs mb-1">${p.name}</h4>
                <p class="text-[10px] text-gray-400 mb-3 leading-snug">${p.desc}</p>
            </div>
            <div>
                <div class="flex items-center justify-between mb-3 text-xs">
                    <span class="text-gray-400 font-bold">모의 권장가:</span>
                    <span class="font-black text-brand-700 font-mono">${p.price.toLocaleString()} 원</span>
                </div>
                <button onclick="addProductToCart(${p.id})" class="w-full bg-brand-50 hover:bg-brand-500 hover:text-white text-brand-700 font-bold text-[10px] py-2.5 rounded-xl transition-all shadow-inner flex items-center justify-center space-x-1">
                    <i class="fa-solid fa-cart-arrow-down"></i> <span>안심 바구니 담기</span>
                </button>
            </div>
        `;
        container.appendChild(col);
    });
}

function addProductToCart(productId) {
    const item = INITIAL_PRODUCTS.find(p => p.id === productId);
    if (!item) return;

    const exist = cart.find(c => c.id === productId);
    if (exist) {
        exist.qty++;
    } else {
        cart.push({ ...item, qty: 1 });
    }

    saveState();
    updateCartBadge();
    showToast(`'${item.name}' 장바구니에 쏙 담겼습니다! 🛍️`);
}

function updateCartBadge() {
    const countBadge = document.getElementById('cart-count-badge');
    if (!countBadge) return;

    const totalQty = cart.reduce((sum, item) => sum + item.qty, 0);
    if (totalQty > 0) {
        countBadge.innerText = totalQty;
        countBadge.classList.remove('hidden');
    } else {
        countBadge.classList.add('hidden');
    }

    if (typeof AppRouter !== 'undefined' && AppRouter.currentTab === 'cart') {
        renderCartPage();
    }
}

function renderCartPage() {
    const container = document.getElementById('cart-page-content');
    if (!container) return;

    if (!cart || cart.length === 0) {
        container.innerHTML = `
            <div class="flex flex-col items-center justify-center py-20 px-4 text-center space-y-4 bg-white border border-amber-50 rounded-3xl shadow-sm">
                <div class="w-20 h-20 bg-brand-50 rounded-full flex items-center justify-center text-4xl shadow-inner animate-bounce">
                    🛒
                </div>
                <div class="space-y-1">
                    <h3 class="text-base font-black text-gray-700">장바구니가 텅 비어 있습니다.</h3>
                    <p class="text-xs text-gray-400">우리 댕댕이, 냥이가 기다리는 맛있는 영양식과 재밌는 장난감을 채워볼까요?</p>
                </div>
                <button onclick="switchTab('shop')" class="bg-brand-500 hover:bg-brand-600 text-white font-bold text-xs py-3 px-6 rounded-xl transition-all shadow-md flex items-center gap-1">
                    <i class="fa-solid fa-cart-shopping"></i> 맛있는 상품 보러가기
                </button>
            </div>
        `;
        return;
    }

    // Calculate totals
    let goodsPrice = 0;
    cart.forEach(item => {
        goodsPrice += item.price * item.qty;
    });

    const shippingFee = goodsPrice >= 50000 ? 0 : 3000;
    const totalPrice = goodsPrice + shippingFee;
    
    // Shipping progress
    const freeShippingTarget = 50000;
    const neededForFreeShipping = Math.max(0, freeShippingTarget - goodsPrice);
    const progressPercent = Math.min(100, (goodsPrice / freeShippingTarget) * 100);

    // Build the 2-Column layout
    container.innerHTML = `
        <div class="grid grid-cols-1 lg:grid-cols-3 gap-6 items-start">
            <!-- Left Column: Item List (Col-Span 2) -->
            <div class="lg:col-span-2 space-y-4">
                <div class="flex justify-between items-center bg-white p-4 rounded-2xl border border-amber-50 shadow-sm">
                    <span class="text-xs font-bold text-gray-500">담은 상품 총 <span class="text-brand-600 font-bold font-mono">${cart.length}</span>개</span>
                    <button onclick="clearCart()" class="text-xs font-bold text-gray-400 hover:text-rose-500 flex items-center gap-1 transition-colors">
                        <i class="fa-solid fa-trash-can"></i> 전체 비우기
                    </button>
                </div>

                <div id="cart-page-items" class="space-y-3.5">
                    <!-- Loaded dynamically below -->
                </div>
            </div>

            <!-- Right Column: Payment summary card -->
            <div class="bg-white rounded-3xl p-5 border border-amber-50 shadow-sm space-y-5 relative">
                <h3 class="font-black text-gray-800 text-sm flex items-center border-b pb-2">
                    <i class="fa-solid fa-receipt text-brand-500 mr-2"></i>결제 상세 내역 💳
                </h3>

                <!-- Free Shipping Tracker -->
                <div class="bg-amber-50/50 p-3.5 rounded-2xl border border-amber-100/50 space-y-2">
                    <div class="flex justify-between text-xs font-bold text-gray-600">
                        <span id="shipping-alert-title">${goodsPrice >= 50000 ? '✅ 무료 배송 적용 완료!' : '무료 배송까지 남은 금액'}</span>
                        <span id="shipping-alert-amount" class="text-brand-600 font-mono">${goodsPrice >= 50000 ? '' : neededForFreeShipping.toLocaleString() + '원'}</span>
                    </div>
                    <!-- Progress Bar -->
                    <div class="w-full bg-gray-200 h-2 rounded-full overflow-hidden">
                        <div id="shipping-progress" class="bg-brand-500 h-2 rounded-full transition-all duration-500" style="width: ${progressPercent}%"></div>
                    </div>
                    <p class="text-[10px] text-gray-400 font-medium leading-relaxed">
                        * 펫과나 안심 배송은 <strong>50,000원</strong> 이상 구매 시 무료 배송됩니다. (기본 배송비 3,000원)
                    </p>
                </div>

                <!-- Cost breakdown -->
                <div class="space-y-3 text-xs border-b pb-4">
                    <div class="flex justify-between text-gray-500 font-medium">
                        <span>총 상품금액</span>
                        <span class="font-mono font-bold text-gray-700">${goodsPrice.toLocaleString()} 원</span>
                    </div>
                    <div class="flex justify-between text-gray-500 font-medium">
                        <span>배송비</span>
                        <span class="font-mono font-bold text-gray-700">${shippingFee === 0 ? '무료' : shippingFee.toLocaleString() + ' 원'}</span>
                    </div>
                </div>

                <!-- Total Price -->
                <div class="flex justify-between items-center text-sm font-black">
                    <span class="text-gray-800">최종 결제 금액</span>
                    <span class="text-brand-600 font-mono text-lg">${totalPrice.toLocaleString()} 원</span>
                </div>

                <!-- Payment Checkout Button -->
                <button id="cart-checkout-btn" onclick="processCartCheckout()" class="w-full bg-brand-500 hover:bg-brand-600 text-white font-black text-xs py-4 rounded-xl transition-all shadow-md flex items-center justify-center gap-2">
                    <i class="fa-solid fa-lock"></i> 안전 결제하기
                </button>

                <!-- Loading overlay inside summary for spinner -->
                <div id="cart-checkout-loading" class="hidden absolute inset-0 z-10 bg-white/95 rounded-3xl flex-col items-center justify-center space-y-3">
                    <div class="w-10 h-10 border-4 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
                    <span class="text-xs font-black text-brand-600 animate-pulse">가상 결제 처리 중... 💳</span>
                </div>
            </div>
        </div>
    `;

    // Render items list inside #cart-page-items
    const itemsContainer = document.getElementById('cart-page-items');
    if (!itemsContainer) return;

    cart.forEach(item => {
        const itemRow = document.createElement('div');
        itemRow.className = "flex items-center gap-4 p-4 bg-white border border-amber-50 rounded-2xl shadow-sm relative group hover:border-brand-200 transition-all duration-300";
        itemRow.innerHTML = `
            <!-- Product Image -->
            <div class="w-16 h-16 bg-brand-50 rounded-xl overflow-hidden shrink-0 border border-gray-100">
                <img loading="lazy" src="${item.image}" class="w-full h-full object-cover">
            </div>

            <!-- Details -->
            <div class="flex-grow min-w-0">
                <div class="flex justify-between items-start gap-2">
                    <h4 class="font-bold text-gray-800 text-xs truncate pr-4">${item.name}</h4>
                    <!-- Delete Button -->
                    <button onclick="removeCartItem(${item.id})" class="text-gray-300 hover:text-rose-500 transition-colors shrink-0">
                        <i class="fa-solid fa-trash-can text-sm"></i>
                    </button>
                </div>
                <p class="text-[10px] text-gray-400 mb-2 truncate">${item.desc}</p>

                <div class="flex items-center justify-between">
                    <!-- Price and Subtotal -->
                    <div class="space-y-0.5">
                        <span class="block text-[10px] text-gray-400 font-medium">개당 ${item.price.toLocaleString()}원</span>
                        <span class="block text-xs font-black text-brand-700 font-mono">${(item.price * item.qty).toLocaleString()} 원</span>
                    </div>

                    <!-- Qty adjuster -->
                    <div class="flex items-center gap-2 font-bold bg-gray-50 border border-gray-100 rounded-xl p-1 shrink-0">
                        <button onclick="adjustCartQty(${item.id}, -1)" class="w-6 h-6 bg-white border border-gray-200/80 rounded-lg flex items-center justify-center text-[10px] hover:bg-gray-100 transition-colors shadow-sm">-</button>
                        <span class="font-mono text-xs w-6 text-center text-gray-700">${item.qty}</span>
                        <button onclick="adjustCartQty(${item.id}, 1)" class="w-6 h-6 bg-white border border-gray-200/80 rounded-lg flex items-center justify-center text-[10px] hover:bg-gray-100 transition-colors shadow-sm">+</button>
                    </div>
                </div>
            </div>
        `;
        itemsContainer.appendChild(itemRow);
    });
}

function adjustCartQty(id, dir) {
    const target = cart.find(item => item.id === id);
    if (!target) return;

    target.qty += dir;
    if (target.qty <= 0) {
        cart = cart.filter(item => item.id !== id);
    } else {
        cart = [...cart];
    }
    saveState();
    updateCartBadge();
}

function removeCartItem(id) {
    const item = cart.find(x => x.id === id);
    if (!item) return;
    
    cart = cart.filter(x => x.id !== id);
    saveState();
    updateCartBadge();
    showToast(`'${item.name}' 장바구니에서 삭제되었습니다.`);
}

function clearCart() {
    showCustomDialog({
        title: "장바구니 비우기 ⚠️",
        message: "정말 장바구니에 담긴 모든 상품을 삭제하시겠습니까?",
        icon: "🗑️",
        type: "confirm",
        onConfirm: () => {
            cart = [];
            saveState();
            updateCartBadge();
            showToast("장바구니를 모두 비웠습니다.");
        }
    });
}

function processCartCheckout() {
    if (cart.length === 0) {
        showToast("결제할 상품이 없습니다.");
        return;
    }

    const shopPaymentLink = window._env_?.STRIPE_SHOP_PAYMENT_LINK || "";
    const total = cart.reduce((s, item) => s + (item.price || 0) * (item.qty || 1), 0);
    const itemNames = cart.map(i => i.name).join(', ');

    if (shopPaymentLink) {
        const params = new URLSearchParams({
            client_reference_id: typeof settings_email !== 'undefined' ? settings_email : 'guest',
            prefilled_promo_code: '',
        });
        showCustomDialog({
            title: "결제 진행 💳",
            message: `총 ${total.toLocaleString()}원 결제 페이지로 이동합니다.\n\n상품: ${itemNames}`,
            icon: "🛍️",
            type: "confirm",
            onConfirm: () => {
                window.open(`${shopPaymentLink}?${params.toString()}`, '_blank');
                showToast("Stripe 결제 페이지로 이동합니다 💳");
            }
        });
    } else {
        // Payment Link 미설정 시 — 이메일 주문 접수
        showCustomDialog({
            title: "주문 접수 📦",
            message: `총 ${total.toLocaleString()}원\n상품: ${itemNames}\n\n결제 완료 후 배송 안내를 이메일로 받으시겠습니까?`,
            icon: "🛍️",
            type: "confirm",
            onConfirm: () => {
                const email = typeof settings_email !== 'undefined' ? settings_email : '';
                const list = JSON.parse(localStorage.getItem('petna_shop_orders') || '[]');
                list.push({ items: cart.map(i => ({ name: i.name, qty: i.qty || 1, price: i.price })), total, email, date: new Date().toISOString() });
                localStorage.setItem('petna_shop_orders', JSON.stringify(list));
                cart = [];
                saveState();
                updateCartBadge();
                showToast("주문이 접수되었습니다! 결제 연동 오픈 시 가장 먼저 알려드릴게요 📦");
            }
        });
    }
}

// ── 굿즈 제작 ──────────────────────────────────────────────
let _goodsType = '아크릴 키링';
let _goodsSize = 'S';

const _GOODS_PRICES = {
    'S': { '아크릴 키링': 12900, '아크릴 스탠드': 14900, '머그컵': 18900, '쿠션': 24900, '에코백': 16900, '폰케이스': 19900 },
    'M': { '아크릴 키링': 15900, '아크릴 스탠드': 17900, '머그컵': 18900, '쿠션': 27900, '에코백': 16900, '폰케이스': 22900 },
    'L': { '아크릴 키링': 19900, '아크릴 스탠드': 22900, '머그컵': 18900, '쿠션': 32900, '에코백': 16900, '폰케이스': 25900 },
    'XL':{ '아크릴 키링': 24900, '아크릴 스탠드': 28900, '머그컵': 18900, '쿠션': 39900, '에코백': 16900, '폰케이스': 29900 },
};

function previewGoodsPhoto(event) {
    const file = event.target.files[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = e => {
        const img = document.getElementById('goods-preview-img');
        const placeholder = document.getElementById('goods-upload-placeholder');
        if (img) { img.src = e.target.result; img.classList.remove('hidden'); }
        if (placeholder) placeholder.classList.add('hidden');
    };
    reader.readAsDataURL(file);
}

function selectGoodsType(btn, type) {
    _goodsType = type;
    document.querySelectorAll('.goods-type-btn').forEach(b => {
        b.className = b.className
            .replace('border-2 border-brand-400 bg-brand-50 text-brand-700', '')
            .replace('border border-gray-200 bg-gray-50 text-gray-600', '')
            .trim();
        b.className += ' border border-gray-200 bg-gray-50 text-gray-600';
    });
    btn.className = btn.className
        .replace('border border-gray-200 bg-gray-50 text-gray-600', '')
        .trim();
    btn.className += ' border-2 border-brand-400 bg-brand-50 text-brand-700';
    _updateGoodsPrice();
}

function selectGoodsSize(btn, size) {
    _goodsSize = size;
    document.querySelectorAll('.goods-size-btn').forEach(b => {
        b.className = b.className
            .replace('border-2 border-brand-400 bg-brand-50 text-brand-700', '')
            .replace('border border-gray-200 bg-gray-50 text-gray-600', '')
            .trim();
        b.className += ' border border-gray-200 bg-gray-50 text-gray-600';
    });
    btn.className = btn.className
        .replace('border border-gray-200 bg-gray-50 text-gray-600', '')
        .trim();
    btn.className += ' border-2 border-brand-400 bg-brand-50 text-brand-700';
    _updateGoodsPrice();
}

function _updateGoodsPrice() {
    const el = document.getElementById('goods-price-display');
    if (!el) return;
    const price = (_GOODS_PRICES[_goodsSize] || {})[_goodsType];
    el.textContent = price ? price.toLocaleString() + '원' : '-';
}

function submitGoodsOrder() {
    const img = document.getElementById('goods-preview-img');
    if (!img || img.classList.contains('hidden')) {
        showToast('📸 먼저 반려동물 사진을 업로드해주세요!');
        return;
    }
    const price = (_GOODS_PRICES[_goodsSize] || {})[_goodsType] || 0;
    showCustomDialog({
        title: '굿즈 제작 신청 완료! 🎉',
        message: `${_goodsType} · ${_goodsSize} 사이즈\n${price.toLocaleString()}원\n\n사진이 접수되었습니다. 제작 기간은 5~7일이며, 등록된 주소로 무료 배송됩니다.`,
        icon: '✨',
        type: 'alert'
    });
}
