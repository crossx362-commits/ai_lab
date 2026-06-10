// WalkModule — 산책 관련 상태를 단일 네임스페이스로 관리 (전역 오염 방지)
const WalkModule = {
    mapInstance: null,
    mapMarkers: [],
    simulationWalkLine: null,
    simulationMarkerInstance: null,
    walkTimerInterval: null,
    isWalkingActive: false,

    isDrawingRouteMode: false,
    editingRouteId: null,
    drawingRoutePoints: [],
    drawingRouteLine: null,
    drawingRouteMarkers: [],
    isSimulationActive: false,
    lastMapInteractionTime: 0,

    myLocationMarker: null,
    myLocationCircle: null,

    walkDistanceRun: 0,
    walkCaloriesRun: 0,
    walkSecondsRun: 0,
    walkMarkerCoordsHistory: [],
    walkMarkingEvents: [],

    countPoop: 0,
    countPee: 0,
    countSniff: 0,

    simulationPathIndex: 0,

    SIMULATION_ROUTE_PATH: [
        [37.3912, 126.6392],
        [37.3918, 126.6405],
        [37.3925, 126.6418],
        [37.3934, 126.6425],
        [37.3945, 126.6410],
        [37.3952, 126.6395],
        [37.3942, 126.6380],
        [37.3928, 126.6372],
        [37.3915, 126.6381],
        [37.3912, 126.6392]
    ],
};

// 하위 호환성: 기존 코드에서 전역 변수 참조 시 WalkModule 프록시
const mapInstanceProxy = () => WalkModule.mapInstance;
let mapInstance = WalkModule.mapInstance;
let mapMarkers = WalkModule.mapMarkers;
let simulationWalkLine = WalkModule.simulationWalkLine;
let simulationMarkerInstance = WalkModule.simulationMarkerInstance;
let walkTimerInterval = WalkModule.walkTimerInterval;
let isWalkingActive = WalkModule.isWalkingActive;
let isDrawingRouteMode = WalkModule.isDrawingRouteMode;
let editingRouteId = WalkModule.editingRouteId;
let drawingRoutePoints = WalkModule.drawingRoutePoints;
let drawingRouteLine = WalkModule.drawingRouteLine;
let drawingRouteMarkers = WalkModule.drawingRouteMarkers;
let isSimulationActive = WalkModule.isSimulationActive;
let lastMapInteractionTime = WalkModule.lastMapInteractionTime;
let walkDistanceRun = WalkModule.walkDistanceRun;
let walkCaloriesRun = WalkModule.walkCaloriesRun;
let walkSecondsRun = WalkModule.walkSecondsRun;
let walkMarkerCoordsHistory = WalkModule.walkMarkerCoordsHistory;
let walkMarkingEvents = WalkModule.walkMarkingEvents;
let countPoop = WalkModule.countPoop;
let countPee = WalkModule.countPee;
let countSniff = WalkModule.countSniff;
const SIMULATION_ROUTE_PATH = WalkModule.SIMULATION_ROUTE_PATH;
let simulationPathIndex = WalkModule.simulationPathIndex;

function initWalkSimulator() {
    const mapContainer = document.getElementById('map');
    if (!mapContainer) return;

    if (typeof L === 'undefined') {
        setTimeout(initWalkSimulator, 500);
        return;
    }

    if (mapInstance) {
        setTimeout(() => {
            mapInstance.invalidateSize();
        }, 100);
        return;
    }

    // 기본 좌표 (서울 중심 - GPS 실패 시 폴백)
    const DEFAULT_LAT = 37.5665;
    const DEFAULT_LNG = 126.9780;

    mapInstance = L.map('map', {
        center: [DEFAULT_LAT, DEFAULT_LNG],
        zoom: 15,
        zoomControl: true,
        attributionControl: false
    });

    L.tileLayer('https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png', {
        maxZoom: 20
    }).addTo(mapInstance);

    // 📍 현재 위치 이동 함수
    function moveToMyLocation() {
        if (!navigator.geolocation) {
            showToast("이 브라우저는 위치 정보를 지원하지 않습니다.");
            return;
        }

        showToast("📍 현재 위치를 불러오는 중...");

        navigator.geolocation.getCurrentPosition(
            function (pos) {
                const lat = pos.coords.latitude;
                const lng = pos.coords.longitude;
                const accuracy = pos.coords.accuracy;

                // 지도 중심 이동
                mapInstance.setView([lat, lng], 17);

                // 기존 위치 마커 제거
                if (WalkModule.myLocationMarker) mapInstance.removeLayer(WalkModule.myLocationMarker);
                if (WalkModule.myLocationCircle) mapInstance.removeLayer(WalkModule.myLocationCircle);

                // 현재 위치 마커 (파란 점)
                const myIcon = L.divIcon({
                    html: `<div style="
                        width:18px; height:18px;
                        background:#2563eb;
                        border:3px solid white;
                        border-radius:50%;
                        box-shadow:0 0 0 4px rgba(37,99,235,0.25);
                        animation: pulse-loc 1.5s infinite;
                    "></div>
                    <style>
                        @keyframes pulse-loc {
                            0%   { box-shadow: 0 0 0 0px rgba(37,99,235,0.4); }
                            100% { box-shadow: 0 0 0 14px rgba(37,99,235,0); }
                        }
                    </style>`,
                    className: '',
                    iconSize: [18, 18],
                    iconAnchor: [9, 9]
                });

                WalkModule.myLocationMarker = L.marker([lat, lng], { icon: myIcon, zIndexOffset: 1000 })
                    .addTo(mapInstance)
                    .bindPopup("📍 현재 내 위치");

                // 위치 정확도 원
                WalkModule.myLocationCircle = L.circle([lat, lng], {
                    radius: accuracy,
                    color: '#2563eb',
                    fillColor: '#93c5fd',
                    fillOpacity: 0.15,
                    weight: 1
                }).addTo(mapInstance);

                showToast(`📍 현재 위치로 이동했습니다!`);
            },
            function (err) {
                const msgs = {
                    1: "위치 권한이 거부되었습니다. 브라우저 설정에서 위치 허용 후 다시 시도해 주세요.",
                    2: "위치 정보를 가져올 수 없습니다.",
                    3: "위치 요청 시간이 초과되었습니다."
                };
                showToast(msgs[err.code] || "위치를 불러오지 못했습니다.");
            },
            { enableHighAccuracy: true, timeout: 10000, maximumAge: 0 }
        );
    }

    // 🔘 내 위치 버튼 지도에 추가
    const LocateControl = L.Control.extend({
        options: { position: 'topright' },
        onAdd: function () {
            const btn = L.DomUtil.create('button');
            btn.title = '내 현재 위치로 이동';
            btn.innerHTML = `<span style="font-size:18px">📍</span>`;
            btn.style.cssText = `
                background: white;
                border: none;
                border-radius: 8px;
                padding: 7px 10px;
                cursor: pointer;
                box-shadow: 0 2px 8px rgba(0,0,0,0.18);
                font-size: 14px;
                font-weight: bold;
                color: #2563eb;
                display: flex;
                align-items: center;
                gap: 4px;
                margin-bottom: 4px;
            `;
            L.DomEvent.on(btn, 'click', function (e) {
                L.DomEvent.stopPropagation(e);
                moveToMyLocation();
            });
            return btn;
        }
    });
    new LocateControl().addTo(mapInstance);

    // Track map interactions to prevent clicks during/immediately after pan or zoom gestures
    mapInstance.on('movestart zoomstart', function () {
        lastMapInteractionTime = Date.now();
    });
    mapInstance.on('move zoom', function () {
        lastMapInteractionTime = Date.now();
    });
    mapInstance.on('moveend zoomend', function () {
        lastMapInteractionTime = Date.now();
    });

    // 🚀 지도 최초 로드 시 자동으로 내 위치 이동
    moveToMyLocation();

    renderMapPlacesPins();
    renderCustomRoutesList();

    mapInstance.on('click', function (e) {
        if (isWalkingActive) return;

        // Ignore clicks if the map was interacted with (panned/zoomed) in the last 350ms
        if (Date.now() - lastMapInteractionTime < 350) {
            return;
        }

        if (typeof isDrawingRouteMode !== 'undefined' && isDrawingRouteMode) {
            addPointToDrawnRoute(e.latlng.lat, e.latlng.lng);
            return;
        }

        const lat = e.latlng.lat;
        const lng = e.latlng.lng;

        showCustomDialog({
            title: "안심 핫플레이스 제안 📍",
            message: `해당 지정 장소(위경도: ${lat.toFixed(4)}, ${lng.toFixed(4)})에 우리 동네 댕댕이/냥이가 함께 갈 수 있는 새로운 공간을 직접 등록하시겠습니까?`,
            icon: "🗺️",
            type: "confirm",
            onConfirm: () => {
                const name = prompt("장소 이름을 써보세요 (예: 개구쟁이 소형견 잔디밭)", "우리끼리 비밀 산책 쉼터");
                if (!name) return;

                const newPlace = {
                    id: Date.now(),
                    name: name,
                    lat: lat,
                    lng: lng,
                    category: "park",
                    desc: "이웃 집사가 직접 지도 상에 발자국을 찍어 남긴 동네 개방형 반려 공간입니다.",
                    rating: 5.0,
                    reviews: [{ author: "최초 개척 집사", rating: 5, comment: "이곳 산책하기 정말 공기도 좋고 안전합니다!" }]
                };

                places.push(newPlace);
                saveState();
                renderMapPlacesPins();
                showToast(`'${name}' 장소가 동동안심 지도상에 새롭게 실시간 등록되었습니다!`);
            }
        });
    });
}


function renderMapPlacesPins() {
    if (!mapInstance) return;
    mapMarkers.forEach(m => mapInstance.removeLayer(m));
    mapMarkers = [];

    places.forEach(p => {
        let emoji = "🌳";
        let colorClass = "bg-emerald-500";
        if (p.category === "cafe") { emoji = "☕"; colorClass = "bg-amber-500"; }
        else if (p.category === "hospital") { emoji = "🏥"; colorClass = "bg-red-500"; }

        const customHtmlIcon = L.divIcon({
            html: `<div class="w-8 h-8 rounded-full ${colorClass} border-2 border-white shadow-md flex items-center justify-center text-sm transform transition-transform hover:scale-125">${emoji}</div>`,
            className: 'custom-leaflet-marker',
            iconSize: [32, 32],
            iconAnchor: [16, 32]
        });

        const pin = L.marker([p.lat, p.lng], { icon: customHtmlIcon }).addTo(mapInstance);
        pin.on('click', function () {
            openPlacePanel(p.id);
        });

        mapMarkers.push(pin);
    });
}

function filterMapPlaces() {
    const val = document.getElementById('map-search-input').value.toLowerCase().trim();
    if (!val) {
        renderMapPlacesPins();
        return;
    }

    const filtered = places.filter(p => p.name.toLowerCase().includes(val) || p.desc.toLowerCase().includes(val));
    mapMarkers.forEach(m => mapInstance.removeLayer(m));
    mapMarkers = [];

    filtered.forEach(p => {
        const pin = L.marker([p.lat, p.lng]).addTo(mapInstance);
        pin.on('click', () => openPlacePanel(p.id));
        mapMarkers.push(pin);
    });
}

function filterMapCategory(cat) {
    document.querySelectorAll('.map-cat-btn').forEach(b => {
        b.className = "map-cat-btn flex-1 text-[10px] font-bold py-2 px-3 rounded-xl bg-gray-50 text-gray-600 border border-gray-100 transition-all";
    });

    const activeBtn = document.querySelector(`.map-cat-btn[data-cat="${cat}"]`);
    if (activeBtn) {
        activeBtn.className = "map-cat-btn flex-1 text-[10px] font-bold py-2 px-3 rounded-xl bg-brand-500 text-white transition-all";
    }

    mapMarkers.forEach(m => mapInstance.removeLayer(m));
    mapMarkers = [];

    const filtered = cat === 'all' ? places : places.filter(p => p.category === cat);
    filtered.forEach(p => {
        let emoji = "🌳";
        let colorClass = "bg-emerald-500";
        if (p.category === "cafe") { emoji = "☕"; colorClass = "bg-amber-500"; }
        else if (p.category === "hospital") { emoji = "🏥"; colorClass = "bg-red-500"; }

        const customIcon = L.divIcon({
            html: `<div class="w-8 h-8 rounded-full ${colorClass} border-2 border-white shadow-md flex items-center justify-center text-sm">${emoji}</div>`,
            className: 'custom-leaflet-marker',
            iconSize: [32, 32],
            iconAnchor: [16, 32]
        });

        const pin = L.marker([p.lat, p.lng], { icon: customIcon }).addTo(mapInstance);
        pin.on('click', () => openPlacePanel(p.id));
        mapMarkers.push(pin);
    });
}

let activeSelectedPlaceId = null;

function openPlacePanel(id) {
    const p = places.find(item => item.id === id);
    if (!p) return;

    activeSelectedPlaceId = id;
    document.getElementById('p-detail-name').innerText = p.name;
    document.getElementById('p-detail-desc').innerText = p.desc;
    document.getElementById('p-detail-rating').innerText = p.rating.toFixed(1);
    document.getElementById('p-detail-rev-count').innerText = p.reviews ? p.reviews.length : 0;
    document.getElementById('p-detail-badge').innerText = p.category.toUpperCase();

    const reviewsContainer = document.getElementById('p-detail-reviews');
    reviewsContainer.innerHTML = '';

    if (!p.reviews || p.reviews.length === 0) {
        reviewsContainer.innerHTML = `<div class="text-center py-4 text-[10px] text-gray-400">등록된 이웃 후기가 없습니다. 첫 발자국 평을 남겨보세요!</div>`;
    } else {
        p.reviews.forEach(r => {
            const el = document.createElement('div');
            el.className = "bg-gray-50 p-2 rounded-xl text-[10px] border border-gray-100/60";
            el.innerHTML = `
                <div class="flex justify-between font-bold text-gray-700 mb-0.5">
                    <span>👤 ${r.author}</span>
                    <span class="text-amber-500">⭐ ${r.rating}</span>
                </div>
                <p class="text-gray-500 leading-snug">${escapeHtml(r.comment)}</p>
            `;
            reviewsContainer.appendChild(el);
        });
    }

    document.getElementById('place-detail-panel').classList.remove('translate-x-full');
}

function closePlacePanel() {
    document.getElementById('place-detail-panel').classList.add('translate-x-full');
    activeSelectedPlaceId = null;
}

function submitPlaceReview() {
    if (!activeSelectedPlaceId) return;

    const author = document.getElementById('p-review-author').value.trim() || "익명 집사";
    const rating = parseInt(document.getElementById('p-review-rating').value);
    const text = document.getElementById('p-review-text').value.trim();

    if (!text) {
        showToast("한평 소감 내용을 성의있게 적어주세요!");
        return;
    }

    const p = places.find(item => item.id === activeSelectedPlaceId);
    if (!p) return;

    p.reviews.push({
        author: author,
        rating: rating,
        comment: text
    });

    const totalRating = p.reviews.reduce((sum, r) => sum + r.rating, 0);
    p.rating = totalRating / p.reviews.length;

    saveState();
    openPlacePanel(activeSelectedPlaceId);

    document.getElementById('p-review-text').value = '';
    document.getElementById('p-review-author').value = '';

    showToast("소중한 이웃 안심 한줄평이 연동 등록되었습니다!");
}

let gpsWatchId = null; // GPS 실시간 추적 ID

function flyToPlace(lat, lng, name) {
    if (typeof switchTab === 'function') switchTab('walk');
    setTimeout(() => {
        if (typeof mapInstance !== 'undefined' && mapInstance) {
            mapInstance.setView([lat, lng], 17);
            L.popup()
                .setLatLng([lat, lng])
                .setContent(`<div class="text-xs font-bold text-center p-1">📍 ${name}</div>`)
                .openOn(mapInstance);
        }
        if (typeof showToast === 'function') showToast(`📍 ${name} 위치로 이동합니다`);
    }, 450);
}

// 산책 상태를 상단 배지로 표시 (전·중·후)
function _setWalkStatusBadge(state) {
    const badge = document.getElementById('walk-status-badge');
    if (!badge) return;
    const states = {
        idle:    { text: '산책 준비',  cls: 'bg-gray-100 text-gray-500' },
        active:  { text: '🐾 산책 중', cls: 'bg-green-100 text-green-700 animate-pulse' },
        paused:  { text: '⏸ 일시정지', cls: 'bg-amber-100 text-amber-700' },
        saving:  { text: '✅ 저장 완료', cls: 'bg-blue-100 text-blue-700' },
    };
    const s = states[state] || states.idle;
    badge.textContent = s.text;
    badge.className = `text-[10px] font-bold px-2.5 py-1 rounded-full transition-all ${s.cls}`;
}

function toggleWalk() {
    if (isWalkingActive) {
        // 일시 정지
        isWalkingActive = false;
        clearInterval(walkTimerInterval);
        if (gpsWatchId !== null) {
            navigator.geolocation.clearWatch(gpsWatchId);
            gpsWatchId = null;
        }
        _setWalkStatusBadge('paused');
        document.getElementById('walk-overlay').classList.remove('hidden');
        document.getElementById('walk-start-btn').innerHTML = `<i class="fa-solid fa-play"></i> <span>산책 재개</span>`;
        showToast("산책이 일시 중지되었습니다. ⏸️");
    } else {
        // 산책 시작 / 재개
        startGpsWalk();
    }
}

// 두 좌표 간 거리 계산 (Haversine 공식, km 단위)
function getDistanceKm(lat1, lng1, lat2, lng2) {
    const R = 6371;
    const dLat = (lat2 - lat1) * Math.PI / 180;
    const dLng = (lng2 - lng1) * Math.PI / 180;
    const a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
        Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180) *
        Math.sin(dLng / 2) * Math.sin(dLng / 2);
    return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
}

function updateWalkSimulationTick() {
    // GPS 실시간 추적으로 대체됨 — 이 함수는 더 이상 사용하지 않습니다
}

function placeMarking(type) {
    if (!isWalkingActive) return;
    if (walkMarkerCoordsHistory.length === 0) {
        showToast("📍 GPS 위치를 불러오는 중입니다. 잠시 후 다시 눌러주세요!");
        return;
    }

    const currentPos = walkMarkerCoordsHistory[walkMarkerCoordsHistory.length - 1];
    let emoji = "👃";
    if (type === "poop") {
        emoji = "💩";
        countPoop++;
        const el = document.getElementById('stat-poop-count');
        if (el) el.innerText = countPoop;
    } else if (type === "pee") {
        emoji = "💦";
        countPee++;
        const el = document.getElementById('stat-pee-count');
        if (el) el.innerText = countPee;
    } else if (type === "sniff") {
        emoji = "👃";
        countSniff++;
        const el = document.getElementById('stat-sniff-count');
        if (el) el.innerText = countSniff;
    }

    walkMarkingEvents.push({
        lat:            currentPos[0],
        lng:            currentPos[1],
        type:           type,
        timestamp:      Date.now(),          // 실제 시각 (ms)
        elapsedSeconds: walkSecondsRun,      // 산책 시작 후 경과 시간
    });

    const markingHtmlIcon = L.divIcon({
        html: `<div class="text-2xl hover:scale-125 transition-transform cursor-pointer drop-shadow">${emoji}</div>`,
        className: 'marking-event-icon',
        iconSize: [28, 28],
        iconAnchor: [14, 14]
    });

    const marker = L.marker(currentPos, { icon: markingHtmlIcon }).addTo(mapInstance);
    marker.bindPopup(`<div class="text-xs font-bold text-center p-1">우리 아이의 흔적!<br>${emoji} 지점 등록 완료 🐾</div>`).openPopup();

    showToast(`실시간 위도에 '${emoji}' 흔적 마킹을 배치 완료했습니다!`);
}

function stopAndSaveWalk() {
    isWalkingActive = false;
    clearInterval(walkTimerInterval);

    // GPS 추적 중단
    if (gpsWatchId !== null) {
        navigator.geolocation.clearWatch(gpsWatchId);
        gpsWatchId = null;
    }

    showCustomDialog({
        title: "산책 기록 저장 💾",
        message: "현재까지의 산책 기록을 역사관에 저장하시겠습니까?",
        icon: "💾",
        type: "confirm",
        onConfirm: () => {
            const mins = String(Math.floor(walkSecondsRun / 60)).padStart(2, '0');
            const secs = String(walkSecondsRun % 60).padStart(2, '0');

            const completedSession = {
                id: Date.now(),
                date: "오늘 방금",
                duration: `${mins}:${secs}`,
                distance: walkDistanceRun.toFixed(2),
                calories: String(walkCaloriesRun),
                poop: countPoop,
                pee: countPee,
                sniff: countSniff,
                coords: walkMarkerCoordsHistory,
                marks: walkMarkingEvents
            };

            walks.unshift(completedSession);
            saveState();

            const currentPet = getActivePet();
            if (currentPet) {
                currentPet.happy = Math.min(100, currentPet.happy + 25);
                currentPet.hunger = Math.max(0, currentPet.hunger - 15);
                saveState();
                if (typeof updatePetInSupabase === 'function') {
                    updatePetInSupabase(currentPet);
                }
            }

            walkDistanceRun = 0;
            walkCaloriesRun = 0;
            walkSecondsRun = 0;
            countPoop = 0;
            countPee = 0;
            countSniff = 0;
            walkMarkerCoordsHistory = [];
            walkMarkingEvents = [];
            simulationPathIndex = 0;
            isSimulationActive = false;

            document.getElementById('walk-time-display').innerText = "00:00";
            document.getElementById('walk-distance-display').innerText = "0.00 km";
            document.getElementById('walk-calories-display').innerText = "0 kcal";
            document.getElementById('stat-poop-count').innerText = "0회";
            document.getElementById('stat-pee-count').innerText = "0회";
            document.getElementById('stat-sniff-count').innerText = "0회";

            document.getElementById('walk-stop-btn').disabled = true;
            document.getElementById('walk-stop-btn').classList.add('opacity-40');
            const saveBtn = document.getElementById('walk-save-btn');
            if (saveBtn) {
                saveBtn.disabled = true;
                saveBtn.classList.add('opacity-40');
            }
            document.getElementById('walk-start-btn').innerHTML = `<i class="fa-solid fa-play"></i> <span>산책 시작</span>`;

            toggleMarkingButtons(false);

            if (simulationWalkLine) mapInstance.removeLayer(simulationWalkLine);
            if (simulationMarkerInstance) mapInstance.removeLayer(simulationMarkerInstance);
            if (sharedOverlayLine) {
                mapInstance.removeLayer(sharedOverlayLine);
                sharedOverlayLine = null;
            }
            if (sharedOverlayMarkers && sharedOverlayMarkers.length > 0) {
                sharedOverlayMarkers.forEach(m => mapInstance.removeLayer(m));
                sharedOverlayMarkers = [];
            }
            loadedRouteId = null;
            window.SIMULATION_ROUTE_PATH = [];
            renderCustomRoutesList();


            _setWalkStatusBadge('idle');
            renderWalkHistory();
            renderMyPets();
            showToast("🏆 안심 산책 성료! 역사관에 기록이 등록되었습니다. 펫 행복지수가 올라갔어요!");
            _promptRegisterWalkPlace();

            // 🔔 알림 설정이 활성화되어 있고 권한이 허용된 경우 브라우저 알림 전송
            if (settings_notifications_enabled && settings_notification_permission_granted) {
                if ("Notification" in window && Notification.permission === "granted") {
                    new Notification("🐾 산책 완료!", {
                        body: `우리 아이와 ${completedSession.distance} km 산책을 완료했습니다! 행복지수가 상승했어요!`,
                        icon: "https://raw.githubusercontent.com/leejsh/petnna/main/img/petnna_logo_square.png"
                    });
                }
            }
        },
        onCancel: () => {
            // 취소를 선택하면 산책을 다시 재개하여 기록을 이어갈 수 있도록 합니다.
            isWalkingActive = false;
            toggleWalk();
        }
    });
}

function discardWalk() {
    isWalkingActive = false;
    clearInterval(walkTimerInterval);

    // GPS 추적 중단
    if (gpsWatchId !== null) {
        navigator.geolocation.clearWatch(gpsWatchId);
        gpsWatchId = null;
    }

    showCustomDialog({
        title: "산책 기록 저장 💾",
        message: "현재까지의 산책 기록을 역사관에 저장하시겠습니까?",
        icon: "💾",
        type: "confirm",
        onConfirm: () => {
            const mins = String(Math.floor(walkSecondsRun / 60)).padStart(2, '0');
            const secs = String(walkSecondsRun % 60).padStart(2, '0');

            const completedSession = {
                id: Date.now(),
                date: "오늘 방금",
                duration: `${mins}:${secs}`,
                distance: walkDistanceRun.toFixed(2),
                calories: String(walkCaloriesRun),
                poop: countPoop,
                pee: countPee,
                sniff: countSniff,
                coords: walkMarkerCoordsHistory,
                marks: walkMarkingEvents
            };

            walks.unshift(completedSession);
            saveState();

            const currentPet = getActivePet();
            if (currentPet) {
                currentPet.happy = Math.min(100, currentPet.happy + 25);
                currentPet.hunger = Math.max(0, currentPet.hunger - 15);
                saveState();
                if (typeof updatePetInSupabase === 'function') {
                    updatePetInSupabase(currentPet);
                }
            }

            walkDistanceRun = 0;
            walkCaloriesRun = 0;
            walkSecondsRun = 0;
            countPoop = 0;
            countPee = 0;
            countSniff = 0;
            walkMarkerCoordsHistory = [];
            walkMarkingEvents = [];
            simulationPathIndex = 0;
            isSimulationActive = false;

            document.getElementById('walk-time-display').innerText = "00:00";
            document.getElementById('walk-distance-display').innerText = "0.00 km";
            document.getElementById('walk-calories-display').innerText = "0 kcal";
            document.getElementById('stat-poop-count').innerText = "0회";
            document.getElementById('stat-pee-count').innerText = "0회";
            document.getElementById('stat-sniff-count').innerText = "0회";

            document.getElementById('walk-stop-btn').disabled = true;
            document.getElementById('walk-stop-btn').classList.add('opacity-40');
            const saveBtn = document.getElementById('walk-save-btn');
            if (saveBtn) {
                saveBtn.disabled = true;
                saveBtn.classList.add('opacity-40');
            }
            document.getElementById('walk-start-btn').innerHTML = `<i class="fa-solid fa-play"></i> <span>산책 시작</span>`;

            toggleMarkingButtons(false);

            if (simulationWalkLine) mapInstance.removeLayer(simulationWalkLine);
            if (simulationMarkerInstance) mapInstance.removeLayer(simulationMarkerInstance);
            if (sharedOverlayLine) {
                mapInstance.removeLayer(sharedOverlayLine);
                sharedOverlayLine = null;
            }
            if (sharedOverlayMarkers && sharedOverlayMarkers.length > 0) {
                sharedOverlayMarkers.forEach(m => mapInstance.removeLayer(m));
                sharedOverlayMarkers = [];
            }
            loadedRouteId = null;
            window.SIMULATION_ROUTE_PATH = [];
            renderCustomRoutesList();


            _setWalkStatusBadge('idle');
            renderWalkHistory();
            renderMyPets();
            showToast("🏆 안심 산책 성료! 역사관에 기록이 등록되었습니다. 펫 행복지수가 올라갔어요!");
            _promptRegisterWalkPlace();

            // 🔔 알림 설정이 활성화되어 있고 권한이 허용된 경우 브라우저 알림 전송
            if (settings_notifications_enabled && settings_notification_permission_granted) {
                if ("Notification" in window && Notification.permission === "granted") {
                    new Notification("🐾 산책 완료!", {
                        body: `우리 아이와 ${completedSession.distance} km 산책을 완료했습니다! 행복지수가 상승했어요!`,
                        icon: "https://raw.githubusercontent.com/leejsh/petnna/main/img/petnna_logo_square.png"
                    });
                }
            }
        },
        onCancel: () => {
            showCustomDialog({
                title: "산책 기록 초기화 🗑️",
                message: "현재까지의 산책 기록을 저장하지 않고 모두 초기화(종료)하시겠습니까?",
                icon: "🗑️",
                type: "confirm",
                onConfirm: () => {
                    walkDistanceRun = 0;
                    walkCaloriesRun = 0;
                    walkSecondsRun = 0;
                    countPoop = 0;
                    countPee = 0;
                    countSniff = 0;
                    walkMarkerCoordsHistory = [];
                    walkMarkingEvents = [];
                    simulationPathIndex = 0;
                    isSimulationActive = false;

                    document.getElementById('walk-time-display').innerText = "00:00";
                    document.getElementById('walk-distance-display').innerText = "0.00 km";
                    document.getElementById('walk-calories-display').innerText = "0 kcal";
                    document.getElementById('stat-poop-count').innerText = "0회";
                    document.getElementById('stat-pee-count').innerText = "0회";
                    document.getElementById('stat-sniff-count').innerText = "0회";

                    document.getElementById('walk-stop-btn').disabled = true;
                    document.getElementById('walk-stop-btn').classList.add('opacity-40');

                    const saveBtn = document.getElementById('walk-save-btn');
                    if (saveBtn) {
                        saveBtn.disabled = true;
                        saveBtn.classList.add('opacity-40');
                    }

                    document.getElementById('walk-start-btn').innerHTML = `<i class="fa-solid fa-play"></i> <span>산책 시작</span>`;

                    toggleMarkingButtons(false);

                    if (simulationWalkLine) mapInstance.removeLayer(simulationWalkLine);
                    if (simulationMarkerInstance) mapInstance.removeLayer(simulationMarkerInstance);
                    if (sharedOverlayLine) {
                        mapInstance.removeLayer(sharedOverlayLine);
                        sharedOverlayLine = null;
                    }
                    if (sharedOverlayMarkers && sharedOverlayMarkers.length > 0) {
                        sharedOverlayMarkers.forEach(m => mapInstance.removeLayer(m));
                        sharedOverlayMarkers = [];
                    }
                    loadedRouteId = null;
                    window.SIMULATION_ROUTE_PATH = [];
                    renderCustomRoutesList();


                    document.getElementById('walk-overlay').classList.remove('hidden');

                    showToast("산책 기록이 초기화되었습니다.");
                },
                onCancel: () => {
                    isWalkingActive = false;
                    toggleWalk();
                }
            });
        }
    });
}


function toggleMarkingButtons(enable) {
    ['btn-mark-poop', 'btn-mark-pee', 'btn-mark-sniff'].forEach(id => {
        const btn = document.getElementById(id);
        if (!btn) return;
        btn.disabled = !enable;
        if (enable) {
            // 활성: 고대비 + 클릭 가능 시각 표시
            btn.classList.remove('opacity-40', 'cursor-not-allowed', 'grayscale');
            btn.classList.add('hover:scale-110', 'active:scale-95', 'cursor-pointer');
        } else {
            // 비활성: 명확한 비활성 표시 (opacity 낮추고 grayscale)
            btn.classList.add('opacity-40', 'cursor-not-allowed', 'grayscale');
            btn.classList.remove('hover:scale-110', 'active:scale-95', 'cursor-pointer');
        }
    });

    // 산책 상태를 지도 컨테이너 테두리 색상으로도 표시
    const mapContainer = document.querySelector('#tab-walk .relative.w-full.bg-gray-100');
    if (mapContainer) {
        if (enable) {
            mapContainer.classList.add('border-brand-400', 'border-2');
            mapContainer.classList.remove('border-amber-100\\/50');
        } else {
            mapContainer.classList.remove('border-brand-400', 'border-2');
            mapContainer.classList.add('border-amber-100\\/50');
        }
    }
}

function renderWalkHistory() {
    const list = document.getElementById('walk-history-list');
    if (!list) return;

    list.innerHTML = '';

    if (walks.length === 0) {
        list.innerHTML = `<div class="text-center py-6 text-gray-400 text-xs">산책 완료 내역이 비어있습니다. 첫 산책을 다녀와 보세요!</div>`;
        return;
    }

    walks.forEach(w => {
        const el = document.createElement('div');
        const isActive = activeSelectedWalkId === w.id;

        // 클릭하면 지도로 경로 로드 (버튼부 제외 영역에 cursor-pointer)
        el.className = `p-4 border rounded-2xl flex flex-col sm:flex-row justify-between items-start sm:items-center gap-3 transition-all ${isActive
                ? "bg-brand-50/50 border-brand-500 ring-2 ring-brand-500/20 shadow-sm"
                : "bg-gray-50/80 border-gray-100 hover:border-brand-200 cursor-pointer"
            }`;

        // 카드 전체 클릭 시 경로 지도에 표현 (버튼 클릭 시는 전파 방지)
        el.onclick = () => {
            if (typeof loadHistoryTrailOnMap === 'function') {
                loadHistoryTrailOnMap(w.id);
            }
        };

        const codeString = btoa(encodeURIComponent(JSON.stringify({ distance: w.distance, coords: w.coords, marks: w.marks })));

        el.innerHTML = `
            <div class="space-y-1 flex-grow">
                <div class="flex items-center space-x-2">
                    <span class="bg-brand-100 text-brand-700 text-[9px] font-black px-2 py-0.5 rounded-full">${w.date}</span>
                    <span class="text-xs text-gray-400 font-mono">ID: #${w.id.toString().slice(-4)}</span>
                    ${isActive ? '<span class="bg-emerald-100 text-emerald-800 text-[9px] font-black px-1.5 py-0.5 rounded-full animate-pulse">지도 로드됨 🗺️</span>' : ''}
                </div>
                <h5 class="font-black text-gray-800 text-sm">🦮 안심 트래킹 ${w.distance} km 완료</h5>
                <div class="flex items-center space-x-3 text-[10px] text-gray-500 font-bold">
                    <span>⏱️ ${w.duration}</span>
                    <span>🔥 ${w.calories} kcal</span>
                    <span>💩 ${w.poop}회</span>
                    <span>💦 ${w.pee}회</span>
                    <span>👃 ${w.sniff}회</span>
                </div>
            </div>
            <div class="flex space-x-2 w-full sm:w-auto shrink-0 justify-end" onclick="event.stopPropagation()">
                <button onclick="addWalkToAlbum(${w.id})" class="flex-grow sm:flex-none text-[10px] bg-white hover:bg-amber-50 border border-amber-200 text-amber-700 font-bold py-1.5 px-3 rounded-xl shadow-sm transition-all"><i class="fa-solid fa-images mr-1"></i>앨범에 추가</button>
                <button onclick="shareWalkToFeed(${w.id})" class="flex-grow sm:flex-none text-[10px] bg-white hover:bg-indigo-50 border border-indigo-200 text-indigo-700 font-bold py-1.5 px-3 rounded-xl shadow-sm transition-all"><i class="fa-solid fa-share mr-1"></i> 피드 발행</button>
                <button onclick="copyWalkShareCode('${codeString}')" class="flex-grow sm:flex-none text-[10px] bg-white hover:bg-amber-50 border border-amber-200 text-brand-700 font-bold py-1.5 px-3 rounded-xl shadow-sm transition-all"><i class="fa-solid fa-copy mr-1"></i> 코드 복사</button>
                <button onclick="deleteWalkLog(${w.id})" class="text-gray-300 hover:text-red-500 py-1.5 px-2 text-xs transition-colors"><i class="fa-solid fa-trash-can"></i></button>
            </div>
        `;
        list.appendChild(el);
    });
}

function deleteWalkLog(id) {
    walks = walks.filter(w => w.id !== id);
    saveState();
    renderWalkHistory();
    showToast("선택한 산책 역사 로그가 제거되었습니다.");
}

// 📮 산책 기록을 앨범 소장 갤러리에 추가
function addWalkToAlbum(walkId) {
    const w = walks.find(item => item.id === walkId);
    if (!w) return;

    // 산책 기록 카드를 앨범 아이템으로 변환 (배경 없는 텍스트 카드 형태)
    const walkAlbumItem = {
        url: '',
        isVideo: false,
        start: 0,
        end: 0,
        filter: 'natural',
        isWalkCard: true,
        walkData: {
            id: w.id,
            date: w.date,
            distance: w.distance,
            duration: w.duration,
            calories: w.calories,
            poop: w.poop,
            pee: w.pee,
            sniff: w.sniff
        },
        stickers: [
            { type: 'text', content: `🦮 ${w.distance}km 산책 완료!`, left: 50, top: 25, scale: 1.2, rotate: 0, zIndex: '20', bubbleTheme: 'bg-brand-500 text-white border-brand-600', fontSize: 'text-xs' },
            { type: 'text', content: `⏱️ ${w.duration}  🔥 ${w.calories}kcal`, left: 50, top: 55, scale: 1.0, rotate: 0, zIndex: '20', bubbleTheme: 'bg-white/95 text-brand-700 border-amber-200/60', fontSize: 'text-[10px]' },
            { type: 'text', content: `💩${w.poop}회  💦${w.pee}회  👃${w.sniff}회`, left: 50, top: 80, scale: 0.9, rotate: 0, zIndex: '20', bubbleTheme: 'bg-amber-100/95 text-amber-900 border-amber-300', fontSize: 'text-[10px]' }
        ]
    };

    // 일기장에 추가 (앨범 아님)
    albums.unshift(walkAlbumItem);
    saveState();

    // 즉시 일기장 업데이트
    if (typeof renderAlbumGallery === 'function') {
        renderAlbumGallery();
    }

    showCustomDialog({
        title: "일기장 추가 완료! 📖",
        message: `"${w.date}" 산책 기록이 일기장에 성공적으로 추가되었습니다!\n일기장 탭에서 바로 확인해보세요.`,
        icon: "✅",
        type: "alert"
    });
}

function copyWalkShareCode(code) {
    const tempInput = document.createElement("input");
    tempInput.value = code;
    document.body.appendChild(tempInput);
    tempInput.select();
    document.execCommand('copy');
    document.body.removeChild(tempInput);

    showCustomDialog({
        title: "공유 코드 복사 성공 📋",
        message: "카카오톡이나 문자메시지로 친구에게 전달해 이 경로를 똑같이 달려보게 권해보세요!\n\n코드: " + code.slice(0, 25) + "...",
        icon: "✅"
    });
}

function promptImportTrailCode() {
    showCustomDialog({
        title: "산책로 코드 불러오기 🗺️",
        message: "친구에게 전달받은 펫과나 고유 산책 공유 코드를 입력하세요:",
        icon: "🔗",
        type: "prompt",
        onConfirm: (rawCode) => {
            if (!rawCode) return;

            try {
                const decodedObj = JSON.parse(decodeURIComponent(atob(rawCode.trim())));
                if (!decodedObj.coords || decodedObj.coords.length === 0) {
                    throw new Error("유효하지 않은 좌표입니다.");
                }

                loadSharedTrailOnMyMap(decodedObj);
            } catch (e) {
                showCustomDialog({
                    title: "코드 인식 오류 ❌",
                    message: "올바른 펫과나 안심 산책 공유 코드가 아닙니다. 형식을 확인하세요.",
                    icon: "⚠️"
                });
            }
        }
    });
}

let sharedOverlayLine = null;
let sharedOverlayMarkers = [];

function loadSharedTrailOnMyMap(trailData) {
    if (!mapInstance) return;
    if (sharedOverlayLine) mapInstance.removeLayer(sharedOverlayLine);
    sharedOverlayMarkers.forEach(m => mapInstance.removeLayer(m));
    sharedOverlayMarkers = [];

    sharedOverlayLine = L.polyline(trailData.coords, {
        color: '#ec4899',
        weight: 4,
        opacity: 0.75,
        dashArray: '8, 8'
    }).addTo(mapInstance);

    if (trailData.marks) {
        trailData.marks.forEach(m => {
            let emoji = "👃";
            if (m.type === "poop") emoji = "💩";
            else if (m.type === "pee") emoji = "💦";

            const mIcon = L.divIcon({
                html: `<div class="text-xl opacity-80 filter saturate-50 hover:scale-125 transition-transform">${emoji}</div>`,
                iconSize: [24, 24],
                iconAnchor: [12, 12]
            });

            const marker = L.marker([m.lat, m.lng], { icon: mIcon }).addTo(mapInstance);
            marker.bindPopup(`<div class="text-[10px] font-bold">친구의 흔적!<br>${emoji} 지점 🐾</div>`);
            sharedOverlayMarkers.push(marker);
        });
    }

    mapInstance.fitBounds(sharedOverlayLine.getBounds());
    showToast("친구의 안심 산책 코스 지도가 보라 핑크색 라인으로 정밀 로딩되었습니다!");
}

function shareWalkToFeed(walkId) {
    const w = walks.find(item => item.id === walkId);
    if (!w) return;

    const currentPet = getActivePet();
    const text = `우리 ${currentPet ? currentPet.name : '댕이'}와 함께 정말 멋진 안심 코스 정복하고 왔어요! 지도를 복제해 같이 걸어봐요. 🦮✨`;

    const sharedPost = {
        id: Date.now(),
        petName: currentPet ? currentPet.name : "댕이",
        petAvatar: currentPet ? (currentPet.type === 'custom' ? currentPet.imageUrl : "https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=150") : "https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=150",
        content: text,
        image: "https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=400",
        isVideo: false,
        attachedWalk: w,
        likes: 0,
        liked: false,
        comments: []
    };

    posts.unshift(sharedPost);
    saveState();
    if (typeof uploadPostToSupabase === 'function') {
        uploadPostToSupabase(sharedPost);
    }
    switchTab('social');
    switchSocialSubTab('feed');
    showToast("안심 산책 이력이 동봉된 자랑 피드가 새롭게 발행되었습니다! 🚀");
}

function loadFeedWalkOnMap(postId) {
    const post = posts.find(p => p.id === postId);
    if (!post || !post.attachedWalk) return;

    switchTab('walk');
    setTimeout(() => {
        loadSharedTrailOnMyMap(post.attachedWalk);
    }, 300);
}

let calendarYear = 2026;
let calendarMonth = 4;

function renderCalendar() {
    const monthYearEl = document.getElementById('calendar-month-year');
    const daysContainer = document.getElementById('calendar-days');
    if (!monthYearEl || !daysContainer) return;

    monthYearEl.innerText = `${calendarYear}년 ${calendarMonth + 1}월`;
    daysContainer.innerHTML = '';

    const firstDayIndex = new Date(calendarYear, calendarMonth, 1).getDay();
    const totalDays = new Date(calendarYear, calendarMonth + 1, 0).getDate();

    for (let i = 0; i < firstDayIndex; i++) {
        const emptyCell = document.createElement('div');
        emptyCell.className = "py-2.5 text-gray-300";
        emptyCell.innerText = "";
        daysContainer.appendChild(emptyCell);
    }

    for (let day = 1; day <= totalDays; day++) {
        const cell = document.createElement('div');
        cell.className = "py-2.5 hover:bg-brand-50 rounded-xl cursor-pointer relative font-bold text-gray-700 flex flex-col items-center justify-center transition-colors";
        cell.innerText = day;

        const fullDateStr = `${calendarYear}-${String(calendarMonth + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`;

        if (day === 17 && calendarMonth === 4 && calendarYear === 2026) {
            cell.className = "py-2.5 bg-brand-500 text-white rounded-xl cursor-pointer relative font-bold flex flex-col items-center justify-center shadow-sm";
        }

        const daySchedules = schedules.filter(s => s.date === fullDateStr);
        if (daySchedules.length > 0) {
            const dot = document.createElement('span');
            dot.className = "w-1.5 h-1.5 bg-rose-500 rounded-full absolute bottom-1.5";
            cell.appendChild(dot);
        }

        cell.onclick = () => showSchedulesForDate(fullDateStr, daySchedules);
        daysContainer.appendChild(cell);
    }

    renderUpcomingSchedules();
}

function changeMonth(dir) {
    calendarMonth += dir;
    if (calendarMonth < 0) {
        calendarMonth = 11;
        calendarYear--;
    } else if (calendarMonth > 11) {
        calendarMonth = 0;
        calendarYear++;
    }
    renderCalendar();
}

function showSchedulesForDate(dateStr, daySchedules) {
    let msg = `일정 날짜: ${dateStr}\n\n`;
    if (daySchedules.length === 0) {
        msg += "예정된 안심 돌봄 일정이 없습니다.";
    } else {
        msg += daySchedules.map((s, idx) => `${idx + 1}. [${s.type === 'vet' ? '병원' : '미용'}] ${s.title}`).join('\n');
    }

    showCustomDialog({
        title: "하루 계획표 안내 📅",
        message: msg,
        icon: "🗓️"
    });
}

function openScheduleModal() {
    const modal = document.getElementById('schedule-modal');
    if (modal) {
        modal.classList.remove('hidden');
        modal.classList.add('flex');
        document.getElementById('schedule-date').value = "2026-05-17";
    }
}

function closeScheduleModal() {
    const modal = document.getElementById('schedule-modal');
    if (modal) {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }
}

function submitSchedule() {
    const title = document.getElementById('schedule-title').value.trim();
    const date = document.getElementById('schedule-date').value;
    const type = document.getElementById('schedule-type').value;

    if (!title || !date) {
        showToast("일정 이름 and 일시를 올바르게 작성해주세요.");
        return;
    }

    const newSched = {
        id: Date.now(),
        date: date,
        title: title,
        type: type
    };

    schedules.push(newSched);
    saveState();
    closeScheduleModal();
    renderCalendar();
    showToast("소중한 펫 일정이 스케줄러에 추가 수립되었습니다!");
}

function renderUpcomingSchedules() {
    const container = document.getElementById('upcoming-schedules');
    if (!container) return;

    container.innerHTML = '';
    const sorted = [...schedules].sort((a, b) => new Date(a.date) - new Date(b.date));

    if (sorted.length === 0) {
        container.innerHTML = `<div class="text-center py-4 text-[11px] text-gray-400">다가오는 일정이 비어있습니다.</div>`;
        return;
    }

    sorted.forEach(s => {
        let icon = "🏥";
        if (s.type === "groom") { icon = "✂️"; }
        else if (s.type === "walk") { icon = "🦮"; }
        else if (s.type === "etc") { icon = "🎁"; }

        const el = document.createElement('div');
        el.className = "p-2.5 bg-gray-50 hover:bg-brand-50/50 rounded-xl flex justify-between items-center text-xs transition-colors";
        el.innerHTML = `
            <div class="space-y-0.5">
                <span class="block text-[9px] text-gray-400 font-mono font-bold">${s.date}</span>
                <span class="font-bold text-gray-700">${icon} ${s.title}</span>
            </div>
            <button onclick="deleteSchedule(${s.id})" class="text-gray-300 hover:text-red-500 transition-colors text-xs"><i class="fa-solid fa-trash-can"></i></button>
        `;
        container.appendChild(el);
    });
}

function deleteSchedule(id) {
    schedules = schedules.filter(s => s.id !== id);
    saveState();
    renderCalendar();
    showToast("선택하신 주요 돌봄 일정이 철회되었습니다.");
}

// 역사관 선택 상태 전역 관리
let activeSelectedWalkId = null;

function loadHistoryTrailOnMap(walkId) {
    const w = walks.find(item => item.id === walkId);
    if (!w || !mapInstance) return;

    activeSelectedWalkId = walkId;

    // 1. 기존 공유 경로 오버레이 및 마커 일체 정리
    if (sharedOverlayLine) mapInstance.removeLayer(sharedOverlayLine);
    sharedOverlayMarkers.forEach(m => mapInstance.removeLayer(m));
    sharedOverlayMarkers = [];

    // 2. 혹시 떠있을 수 있는 산책 시뮬레이션 흔적 일체 정리
    if (simulationWalkLine) mapInstance.removeLayer(simulationWalkLine);
    if (simulationMarkerInstance) mapInstance.removeLayer(simulationMarkerInstance);

    // 3. 역사관 전용 두꺼운 오렌지색 선으로 지도 드로잉
    sharedOverlayLine = L.polyline(w.coords, {
        color: '#e37736',
        weight: 6,
        opacity: 0.9,
        lineJoin: 'round'
    }).addTo(mapInstance);

    // 4. 저장되었던 응가, 쉬야, 킁킁 흔적 지점들을 지도에 핀 마크로 복원
    if (w.marks && w.marks.length > 0) {
        w.marks.forEach(m => {
            let emoji = "👃";
            if (m.type === "poop") emoji = "💩";
            else if (m.type === "pee") emoji = "💦";

            const mIcon = L.divIcon({
                html: `<div class="text-2xl hover:scale-125 transition-transform cursor-pointer drop-shadow bg-white/90 p-1 rounded-full border border-orange-200 flex items-center justify-center" style="width:32px; height:32px;">${emoji}</div>`,
                iconSize: [32, 32],
                iconAnchor: [16, 16]
            });

            const marker = L.marker([m.lat, m.lng], { icon: mIcon }).addTo(mapInstance);
            marker.bindPopup(`<div class="text-[11px] font-black text-center">아이의 흔적 복원!<br>${emoji} 지점 🐾</div>`);
            sharedOverlayMarkers.push(marker);
        });
    }

    // 5. 맵 포커스를 경로 전체 영역으로 맞춤 이동
    if (w.coords && w.coords.length > 0) {
        mapInstance.fitBounds(sharedOverlayLine.getBounds(), { padding: [30, 30] });
    }

    // 6. 리스트 하이라이트 갱신을 위해 목록 다시 그리기
    renderWalkHistory();

    showToast(`"${w.date}"의 산책 동선 정보와 마킹 위치를 지도에 안전하게 로드했습니다! 🗺️✨`);
}

// 자랑피드 내 안심 트래킹 코스 미리보기 미니 지도 모달 제어
let modalMapInstance = null;
let modalMapLayers = [];

function openWalkTrailModal(postId) {
    const post = posts.find(p => p.id === postId);
    if (!post || !post.attachedWalk) {
        showToast("첨부된 산책 정보가 유효하지 않습니다.");
        return;
    }

    const w = post.attachedWalk;
    const modal = document.getElementById('walk-trail-modal');
    if (!modal) return;

    // 1. 모달 팝업 표시
    modal.classList.remove('hidden');
    modal.classList.add('flex');

    // 2. 안심 지표 대시보드 텍스트 바인딩
    document.getElementById('modal-trail-distance').innerText = `${w.distance} km`;
    document.getElementById('modal-trail-duration').innerText = w.duration;
    document.getElementById('modal-trail-calories').innerText = `${w.calories} kcal`;
    document.getElementById('modal-trail-poop').innerText = w.poop || 0;
    document.getElementById('modal-trail-pee').innerText = w.pee || 0;
    document.getElementById('modal-trail-sniff').innerText = w.sniff || 0;

    // 2.5. 흔적 타임라인 리스트 빌드
    const eventsContainer = document.getElementById('modal-trail-events');
    if (eventsContainer) {
        eventsContainer.innerHTML = '';
        if (!w.marks || w.marks.length === 0) {
            eventsContainer.innerHTML = `<div class="text-center py-4 text-gray-400 text-[10px]">흔적(응가, 쉬야, 킁킁) 없이 쾌적하게 완주한 산책로입니다. 🐾</div>`;
        } else {
            w.marks.forEach((m, idx) => {
                let emoji = "👃";
                let typeName = "킁킁 관심 영역 탐색";
                let colorClass = "text-emerald-600 bg-emerald-50 border border-emerald-100";

                if (m.type === "poop") {
                    emoji = "💩";
                    typeName = "응가 흔적 배치";
                    colorClass = "text-amber-700 bg-amber-50 border border-amber-100";
                } else if (m.type === "pee") {
                    emoji = "💦";
                    typeName = "쉬야 마킹 완료";
                    colorClass = "text-sky-700 bg-sky-50 border border-sky-100";
                }

                const item = document.createElement('div');
                item.className = `p-2 rounded-xl flex items-center justify-between gap-2 ${colorClass}`;
                item.innerHTML = `
                    <div class="flex items-center gap-1.5 font-bold">
                        <span class="text-sm">${emoji}</span>
                        <span>${idx + 1}. ${typeName}</span>
                    </div>
                    <span class="text-[9px] text-gray-400 font-mono">📍 위경도: ${m.lat.toFixed(4)}, ${m.lng.toFixed(4)}</span>
                `;
                eventsContainer.appendChild(item);
            });
        }
    }

    // 3. Leaflet 미니 지도 렌더링 (모달 레이아웃 갱신 대기 후 150ms 딜레이 구동)
    setTimeout(() => {
        if (modalMapInstance) {
            modalMapInstance.remove();
            modalMapInstance = null;
        }

        modalMapInstance = L.map('modal-trail-map', {
            zoomControl: false,
            attributionControl: false
        }).setView([37.3912, 126.6392], 15);

        // 지도 타일 소스 추가
        L.tileLayer('https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png', {
            maxZoom: 19
        }).addTo(modalMapInstance);

        // 컨테이너 크기 변경 감지 렌더러 리셋 (깨짐 예방)
        modalMapInstance.invalidateSize();

        // 경로 그리기
        const polyline = L.polyline(w.coords, {
            color: '#e37736',
            weight: 5,
            opacity: 0.9,
            lineJoin: 'round'
        }).addTo(modalMapInstance);
        modalMapLayers.push(polyline);

        // 응가/쉬야/킁킁 마커 복원
        if (w.marks && w.marks.length > 0) {
            w.marks.forEach(m => {
                let emoji = "👃";
                if (m.type === "poop") emoji = "💩";
                else if (m.type === "pee") emoji = "💦";

                const mIcon = L.divIcon({
                    html: `<div class="text-xl bg-white/90 p-0.5 rounded-full border border-orange-200 shadow-sm flex items-center justify-center" style="width:24px; height:24px; line-height: 24px;">${emoji}</div>`,
                    iconSize: [24, 24],
                    iconAnchor: [12, 12]
                });

                const marker = L.marker([m.lat, m.lng], { icon: mIcon }).addTo(modalMapInstance);
                marker.bindPopup(`<div class="text-[9px] font-black">${emoji} 지점 🐾</div>`);
                modalMapLayers.push(marker);
            });
        }

        // 경로 경계 기준 지도 시야각 맞춤 조절
        if (w.coords && w.coords.length > 0) {
            modalMapInstance.fitBounds(polyline.getBounds(), { padding: [30, 30] });
        }
    }, 150);
}

function closeWalkTrailModal() {
    const modal = document.getElementById('walk-trail-modal');
    if (modal) {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }

    if (modalMapInstance) {
        modalMapInstance.remove();
        modalMapInstance = null;
    }
    modalMapLayers = [];
}

// ==========================================
// 🎨 나만의 맞춤 산책 경로 설계 및 시뮬레이션 엔진
// ==========================================

function startRouteDrawingMode() {
    if (isWalkingActive) {
        showToast("⚠️ 산책 중에는 경로를 설계할 수 없습니다. 산책을 먼저 종료해주세요.");
        return;
    }

    closePlacePanel();

    if (sharedOverlayLine) mapInstance.removeLayer(sharedOverlayLine);
    sharedOverlayMarkers.forEach(m => mapInstance.removeLayer(m));
    sharedOverlayMarkers = [];
    if (simulationWalkLine) mapInstance.removeLayer(simulationWalkLine);
    if (simulationMarkerInstance) mapInstance.removeLayer(simulationMarkerInstance);

    editingRouteId = null;
    isDrawingRouteMode = true;
    drawingRoutePoints = [];
    drawingRouteMarkers = [];

    // 현재 위치를 첫 번째 포인트로 자동 추가
    if (WalkModule.myLocationMarker) {
        const pos = WalkModule.myLocationMarker.getLatLng();
        drawingRoutePoints.push([pos.lat, pos.lng]);
        reRenderDrawingRoute();
    }

    if (mapInstance) {
        mapInstance.doubleClickZoom.disable();
    }

    const banner = document.getElementById('route-draw-banner');
    if (banner) banner.classList.remove('hidden');

    const desc = document.getElementById('route-draw-banner-desc');
    if (desc) {
        if (drawingRoutePoints.length > 0) {
            desc.innerText = "현재 위치에서 시작합니다. 지도를 클릭해 다음 지점을 추가하세요.";
        } else {
            desc.innerText = "지도 위를 차례대로 클릭해 나만의 산책 경로를 그려보세요.";
        }
    }

    document.getElementById('walk-overlay').classList.add('hidden');

    showToast(drawingRoutePoints.length > 0 ? "🎨 현재 위치에서 경로 그리기 시작!" : "🎨 지도 위를 클릭해 산책 경로를 그리기 시작합니다.");
}

// ── OSRM 도로 경로 계산 ──────────────────────────────────────────────────────
let _routingBusy = false;

async function routeViaOSRM(waypoints) {
    // waypoints: [[lat, lng], ...]  →  도로 따라 [[lat, lng], ...] 반환
    try {
        const coordStr = waypoints.map(p => `${p[1]},${p[0]}`).join(';');
        const url = `https://router.project-osrm.org/route/v1/foot/${coordStr}?geometries=geojson&overview=full`;
        const res = await fetch(url);
        if (!res.ok) return null;
        const data = await res.json();
        if (!data.routes?.[0]) return null;
        // GeoJSON은 [lng, lat] 순 → [lat, lng]로 변환
        return data.routes[0].geometry.coordinates.map(c => [c[1], c[0]]);
    } catch {
        return null;
    }
}

async function addPointToDrawnRoute(lat, lng) {
    if (_routingBusy) { showToast('⏳ 도로 경로 계산 중입니다...'); return; }

    const newPoint = [lat, lng];

    if (drawingRoutePoints.length === 0) {
        drawingRoutePoints.push(newPoint);
        reRenderDrawingRoute();
        showToast('📍 시작점 설정! 다음 지점을 클릭하세요.');
        return;
    }

    _routingBusy = true;
    const lastPoint = drawingRoutePoints[drawingRoutePoints.length - 1];

    const segment = await routeViaOSRM([lastPoint, newPoint]);

    if (segment && segment.length > 1) {
        // 첫 점은 이미 있으므로 건너뜀
        for (let i = 1; i < segment.length; i++) {
            drawingRoutePoints.push(segment[i]);
        }
        showToast(`🛣️ 지점 추가 (도로 경로 ${segment.length - 1}개 연결)`);
    } else {
        // OSRM 실패 시 직선 연결
        drawingRoutePoints.push(newPoint);
        showToast('📍 지점 추가 (오프라인: 직선 연결)');
    }

    _routingBusy = false;
    reRenderDrawingRoute();
}

function reRenderDrawingRoute() {
    // Clear old markers
    drawingRouteMarkers.forEach(m => mapInstance.removeLayer(m));
    drawingRouteMarkers = [];
    
    // Clear line
    if (drawingRouteLine) {
        mapInstance.removeLayer(drawingRouteLine);
        drawingRouteLine = null;
    }
    
    // Redraw markers
    drawingRoutePoints.forEach((pt, i) => {
        const index = i + 1;
        const lat = pt[0];
        const lng = pt[1];
        
        const drawIcon = L.divIcon({
            html: `<div class="w-6 h-6 rounded-full bg-brand-600 border-2 border-white shadow text-white font-extrabold text-[10px] flex items-center justify-center cursor-move" title="드래그하여 이동, 더블클릭하여 삭제">${index}</div>`,
            iconSize: [24, 24],
            iconAnchor: [12, 12]
        });
        
        const marker = L.marker([lat, lng], { icon: drawIcon, draggable: true }).addTo(mapInstance);
        
        marker.on('dragstart', function (event) {
            L.DomEvent.stopPropagation(event);
            lastMapInteractionTime = Date.now();
        });
        marker.on('drag', function (event) {
            lastMapInteractionTime = Date.now();
        });
        marker.on('dragend', function (event) {
            L.DomEvent.stopPropagation(event);
            lastMapInteractionTime = Date.now();
            const newLatLng = event.target.getLatLng();
            drawingRoutePoints[i] = [newLatLng.lat, newLatLng.lng];
            if (drawingRouteLine) {
                drawingRouteLine.setLatLngs(drawingRoutePoints);
            }
            showToast(`📍 지점 ${index}의 위치가 수정되었습니다.`);
        });
        
        marker.on('click', function (event) {
            L.DomEvent.stopPropagation(event);
        });
        
        marker.on('dblclick', function (event) {
            L.DomEvent.stopPropagation(event);
            removePointFromRoute(i);
        });
        
        drawingRouteMarkers.push(marker);
    });
    
    // Redraw line
    if (drawingRoutePoints.length >= 2) {
        drawingRouteLine = L.polyline(drawingRoutePoints, {
            color: '#6366f1',
            weight: 5,
            opacity: 0.85,
            dashArray: '5, 5'
        }).addTo(mapInstance);
    }
}

function removePointFromRoute(idx) {
    drawingRoutePoints.splice(idx, 1);
    reRenderDrawingRoute();
    showToast(`🗑️ 지점 ${idx + 1}이 삭제되었습니다.`);
}

function undoLastRoutePoint() {
    if (drawingRoutePoints.length === 0) {
        showToast("⚠️ 취소할 지점이 없습니다.");
        return;
    }
    drawingRoutePoints.pop();
    reRenderDrawingRoute();
    showToast("↩️ 마지막 지점이 취소되었습니다.");
}

function saveDrawnRoute() {
    if (drawingRoutePoints.length < 2) {
        showToast("⚠️ 최소 2개 이상의 지점을 지도에 클릭해 경로를 완성해 주세요!");
        return;
    }
    
    let totalDist = 0;
    for (let i = 1; i < drawingRoutePoints.length; i++) {
        const prev = drawingRoutePoints[i-1];
        const curr = drawingRoutePoints[i];
        totalDist += getDistanceKm(prev[0], prev[1], curr[0], curr[1]);
    }
    const distStr = totalDist.toFixed(2);
    
    if (editingRouteId !== null) {
        // Edit Mode
        const rIndex = customRoutes.findIndex(item => item.id === editingRouteId);
        if (rIndex !== -1) {
            const oldName = customRoutes[rIndex].name;
            showCustomDialog({
                title: "산책 경로 수정 ✍️",
                message: "산책 경로의 이름을 입력해주세요:",
                icon: "🗺️",
                type: "prompt",
                val: oldName,
                onConfirm: (name) => {
                    if (!name) return;
                    
                    customRoutes[rIndex].name = name;
                    customRoutes[rIndex].coords = drawingRoutePoints;
                    customRoutes[rIndex].distance = distStr;
                    
                    saveState();
                    if (typeof uploadRouteToSupabase === 'function') {
                        uploadRouteToSupabase(customRoutes[rIndex]);
                    }
                    
                    // Clean up drawing layers and exit mode
                    drawingRouteMarkers.forEach(m => mapInstance.removeLayer(m));
                    drawingRouteMarkers = [];
                    if (drawingRouteLine) {
                        mapInstance.removeLayer(drawingRouteLine);
                        drawingRouteLine = null;
                    }
                    drawingRoutePoints = [];
                    isDrawingRouteMode = false;
                    editingRouteId = null;
                    
                    if (mapInstance) {
                        mapInstance.doubleClickZoom.enable();
                    }
                    
                    const desc = document.getElementById('route-draw-banner-desc');
                    if (desc) desc.innerText = "지도 위를 차례대로 클릭해 나만의 산책 경로를 그려보세요.";
                    
                    document.getElementById('route-draw-banner').classList.add('hidden');
                    if (!isWalkingActive) {
                        document.getElementById('walk-overlay').classList.remove('hidden');
                    }
                    
                    renderCustomRoutesList();
                    showToast(`🎉 맞춤 산책로 '${name}' 수정이 완료되었습니다!`);
                }
            });
        }
    } else {
        // Create Mode
        showCustomDialog({
            title: "새 맞춤 산책 경로 저장 💾",
            message: "이 맞춤 산책 경로의 이름을 입력해주세요:",
            icon: "🗺️",
            type: "prompt",
            val: "우리 동네 비밀 산책로 🦮",
            onConfirm: (name) => {
                if (!name) return;
                
                const newRoute = {
                    id: Date.now(),
                    name: name,
                    coords: drawingRoutePoints,
                    distance: distStr,
                    favorite: false,
                    createdAt: Date.now()
                };
                
                customRoutes.push(newRoute);
                saveState();
                if (typeof uploadRouteToSupabase === 'function') {
                    uploadRouteToSupabase(newRoute);
                }
                
                cancelDrawnRoute();
                renderCustomRoutesList();
                showToast(`🎉 맞춤 산책로 '${name}'가 리스트에 등록 저장되었습니다!`);
            }
        });
    }
}

function cancelDrawnRoute() {
    drawingRouteMarkers.forEach(m => mapInstance.removeLayer(m));
    drawingRouteMarkers = [];
    
    if (drawingRouteLine) {
        mapInstance.removeLayer(drawingRouteLine);
        drawingRouteLine = null;
    }
    
    drawingRoutePoints = [];
    isDrawingRouteMode = false;
    editingRouteId = null;
    
    if (mapInstance) {
        mapInstance.doubleClickZoom.enable();
    }
    
    const desc = document.getElementById('route-draw-banner-desc');
    if (desc) desc.innerText = "지도 위를 차례대로 클릭해 나만의 산책 경로를 그려보세요.";
    
    document.getElementById('route-draw-banner').classList.add('hidden');
    if (!isWalkingActive) {
        document.getElementById('walk-overlay').classList.remove('hidden');
    }
    showToast("경로 그리기 모드를 종료했습니다.");
}

function deleteDrawnRoute(id) {
    customRoutes = customRoutes.filter(r => r.id !== id);
    saveState();
    if (typeof deleteRouteFromSupabase === 'function') {
        deleteRouteFromSupabase(id);
    }
    renderCustomRoutesList();
    showToast("맞춤 산책로가 삭제되었습니다.");
}

function startRouteEditingMode(routeId) {
    if (isWalkingActive) {
        showToast("⚠️ 산책 중에는 경로를 수정할 수 없습니다.");
        return;
    }
    
    closePlacePanel();
    
    if (sharedOverlayLine) mapInstance.removeLayer(sharedOverlayLine);
    sharedOverlayMarkers.forEach(m => mapInstance.removeLayer(m));
    sharedOverlayMarkers = [];
    if (simulationWalkLine) mapInstance.removeLayer(simulationWalkLine);
    if (simulationMarkerInstance) mapInstance.removeLayer(simulationMarkerInstance);
    
    const r = customRoutes.find(item => item.id === routeId);
    if (!r) return;
    
    editingRouteId = routeId;
    isDrawingRouteMode = true;
    drawingRoutePoints = JSON.parse(JSON.stringify(r.coords));
    
    if (mapInstance) {
        mapInstance.doubleClickZoom.disable();
    }
    
    const banner = document.getElementById('route-draw-banner');
    if (banner) banner.classList.remove('hidden');
    
    const desc = document.getElementById('route-draw-banner-desc');
    if (desc) desc.innerHTML = `<strong>[경로 수정 중]</strong> 마커를 드래그해 경로를 편집하고 클릭해 새 지점을 추가하세요. (더블클릭 시 지점 삭제)`;
    
    document.getElementById('walk-overlay').classList.add('hidden');
    
    reRenderDrawingRoute();
    
    if (drawingRouteLine) {
        mapInstance.fitBounds(drawingRouteLine.getBounds(), { padding: [30, 30] });
    }
    
    showToast(`✏️ '${r.name}' 경로 편집 모드를 시작합니다.`);
}

let _routeFilter = 'all';
let _routeSort = 'recent';

function filterCustomRoutes(filter) {
    _routeFilter = filter;
    document.querySelectorAll('.route-filter-btn').forEach(btn => {
        const active = btn.dataset.filter === filter;
        btn.className = btn.className
            .replace(/bg-brand-500 text-white|bg-gray-50 text-gray-600 border border-gray-200/g, '')
            .trim();
        btn.className += active ? ' bg-brand-500 text-white' : ' bg-gray-50 text-gray-600 border border-gray-200';
    });
    renderCustomRoutesList();
}

function sortCustomRoutes(value) {
    _routeSort = value;
    renderCustomRoutesList();
}

function toggleRouteFavorite(id) {
    const r = customRoutes.find(item => item.id === id);
    if (!r) return;
    r.favorite = !r.favorite;
    saveState();
    renderCustomRoutesList();
    showToast(r.favorite ? `⭐ '${r.name}' 즐겨찾기 추가!` : `'${r.name}' 즐겨찾기 해제`);
}

function renderCustomRoutesList() {
    const listContainer = document.getElementById('custom-routes-list');
    const notebookContainer = document.getElementById('notebook-custom-routes-list');
    const emptyEl = document.getElementById('custom-routes-empty');
    if (!listContainer && !notebookContainer) return;

    let routes = [...customRoutes];

    if (_routeFilter === 'favorite') routes = routes.filter(r => r.favorite);
    else if (_routeFilter === 'short') routes = routes.filter(r => parseFloat(r.distance) < 1.5);
    else if (_routeFilter === 'long') routes = routes.filter(r => parseFloat(r.distance) >= 1.5);

    if (_routeSort === 'distance') routes.sort((a, b) => parseFloat(a.distance) - parseFloat(b.distance));
    else if (_routeSort === 'name') routes.sort((a, b) => a.name.localeCompare(b.name));
    else routes.sort((a, b) => (b.createdAt || b.id) - (a.createdAt || a.id));

    if (listContainer) listContainer.innerHTML = '';
    if (notebookContainer) notebookContainer.innerHTML = '';

    if (routes.length === 0) {
        if (emptyEl) emptyEl.classList.remove('hidden');
        return;
    }
    if (emptyEl) emptyEl.classList.add('hidden');

    routes.forEach(r => {
        const item = document.createElement('div');
        const isLoaded = (typeof SIMULATION_ROUTE_PATH !== 'undefined' && SIMULATION_ROUTE_PATH === r.coords);
        item.className = `p-2.5 rounded-xl flex justify-between items-start text-xs transition-colors border ${isLoaded ? 'bg-brand-50/40 border-brand-300' : 'bg-gray-50 hover:bg-brand-50/30 border-gray-100/60'}`;

        item.innerHTML = `
            <div class="space-y-1 min-w-0 flex-1 mr-2">
                <div class="flex items-center gap-1">
                    <button onclick="toggleRouteFavorite(${r.id})" class="flex-shrink-0 text-sm leading-none transition-transform hover:scale-125 active:scale-95" title="즐겨찾기">
                        ${r.favorite ? '⭐' : '☆'}
                    </button>
                    <span class="font-extrabold text-gray-800 truncate block text-[10px] leading-tight max-w-full">${r.name}</span>
                </div>
                <span class="text-[9px] text-brand-600 font-bold font-mono pl-0.5">📏 ${r.distance} km</span>
            </div>
            <div class="flex flex-col gap-1 shrink-0 w-[68px]">
                <button onclick="loadCustomRouteOnMap(${r.id})"
                    class="w-full ${isLoaded ? 'bg-brand-500 text-white border-brand-500' : 'bg-white hover:bg-brand-50 text-brand-700 border-brand-200'} font-bold text-[10px] px-2 py-1 rounded-lg border transition-colors">
                    ${isLoaded ? '숨기기' : '지도 표시'}
                </button>
                <div class="flex gap-1">
                    <button onclick="startRouteEditingMode(${r.id})"
                        class="flex-1 h-6 bg-white hover:bg-amber-50 text-gray-400 hover:text-amber-600 rounded-lg border border-gray-200 transition-colors flex items-center justify-center"
                        title="수정">
                        <i class="fa-solid fa-pen-to-square text-[10px]"></i>
                    </button>
                    <button onclick="deleteDrawnRoute(${r.id})"
                        class="flex-1 h-6 bg-white hover:bg-red-50 text-gray-400 hover:text-red-500 rounded-lg border border-gray-200 transition-colors flex items-center justify-center"
                        title="삭제">
                        <i class="fa-solid fa-trash-can text-[10px]"></i>
                    </button>
                </div>
            </div>
        `;
        if (listContainer) listContainer.appendChild(item.cloneNode(true));
        if (notebookContainer) notebookContainer.appendChild(item);
    });
}

let loadedRouteId = null;

function loadCustomRouteOnMap(routeId) {
    const r = customRoutes.find(item => item.id === routeId);
    if (!r || !mapInstance) return;
    
    // 이미 로드된 경로를 다시 클릭하면 지도에서 숨기기(제거)
    if (loadedRouteId === routeId) {
        if (sharedOverlayLine) {
            mapInstance.removeLayer(sharedOverlayLine);
            sharedOverlayLine = null;
        }
        window.SIMULATION_ROUTE_PATH = [];
        loadedRouteId = null;
        renderCustomRoutesList();
        showToast(`지우개: 맞춤 산책로 '${r.name}' 표시를 지도에서 숨겼습니다.`);
        return;
    }
    
    if (sharedOverlayLine) mapInstance.removeLayer(sharedOverlayLine);
    sharedOverlayMarkers.forEach(m => mapInstance.removeLayer(m));
    sharedOverlayMarkers = [];
    if (simulationWalkLine) mapInstance.removeLayer(simulationWalkLine);
    if (simulationMarkerInstance) mapInstance.removeLayer(simulationMarkerInstance);
    
    window.SIMULATION_ROUTE_PATH = r.coords;
    loadedRouteId = routeId;
    
    sharedOverlayLine = L.polyline(r.coords, {
        color: '#6366f1',
        weight: 6,
        opacity: 0.9,
        lineJoin: 'round'
    }).addTo(mapInstance);
    
    mapInstance.fitBounds(sharedOverlayLine.getBounds(), { padding: [30, 30] });
    
    renderCustomRoutesList();
    showToast(`🗺️ 맞춤 산책로 '${r.name}'를 성공적으로 로드했습니다! 산책을 시작하세요.`);
}

// ── GPS 고도화 상수 ──────────────────────────────────────────────────────────
const GPS_MIN_MOVE_METERS = 3;      // 3m 미만 이동은 노이즈로 무시
const GPS_MAX_SPEED_MPS   = 15;     // 초당 15m(54km/h) 초과는 이상 좌표로 무시
const GPS_RETRY_MAX       = 3;      // GPS 끊김 자동 재시도 최대 횟수
let   _gpsLastTime        = 0;      // 마지막 GPS 수신 시각 (ms)
let   _gpsRetryCount      = 0;      // 재시도 횟수

function startGpsWalk() {
    if (!navigator.geolocation) {
        showToast("이 기기는 GPS를 지원하지 않습니다. 위치 권한을 확인해 주세요.");
        return;
    }

    isWalkingActive = true;
    _setWalkStatusBadge('active');
    document.getElementById('walk-overlay').classList.add('hidden');
    document.getElementById('walk-start-btn').innerHTML = `<i class="fa-solid fa-pause"></i> <span>일시 정지</span>`;
    document.getElementById('walk-stop-btn').disabled = false;
    document.getElementById('walk-stop-btn').classList.remove('opacity-40');
    const saveBtn = document.getElementById('walk-save-btn');
    if (saveBtn) {
        saveBtn.disabled = false;
        saveBtn.classList.remove('opacity-40');
    }
    toggleMarkingButtons(true);

    // ⏱️ 시간 타이머 (1초마다)
    walkTimerInterval = setInterval(() => {
        walkSecondsRun++;
        const mins = String(Math.floor(walkSecondsRun / 60)).padStart(2, '0');
        const secs = String(walkSecondsRun % 60).padStart(2, '0');
        document.getElementById('walk-time-display').innerText = `${mins}:${secs}`;
    }, 1000);

    showToast("📍 실제 GPS로 산책을 시작합니다! 🦮");

    // 📡 현재 위치 즉시 1회 수집 → 마킹 바로 사용 가능하게
    navigator.geolocation.getCurrentPosition(
        function (pos) {
            const lat = pos.coords.latitude;
            const lng = pos.coords.longitude;
            const currentCoord = [lat, lng];
            if (walkMarkerCoordsHistory.length === 0) {
                walkMarkerCoordsHistory.push(currentCoord);
            }
            mapInstance.setView(currentCoord, 17);
            updatePetMarker(currentCoord);
        },
        function () { },
        { enableHighAccuracy: true, timeout: 8000 }
    );

    _gpsLastTime    = Date.now();
    _gpsRetryCount  = 0;

    // 📡 GPS 실시간 위치 추적 시작
    gpsWatchId = navigator.geolocation.watchPosition(
        function (pos) {
            if (!isWalkingActive) return;
            _gpsRetryCount = 0; // 수신 성공 → 재시도 카운터 초기화

            const lat  = pos.coords.latitude;
            const lng  = pos.coords.longitude;
            const now  = Date.now();
            const currentCoord = [lat, lng];

            if (walkMarkerCoordsHistory.length > 0) {
                const prev    = walkMarkerCoordsHistory[walkMarkerCoordsHistory.length - 1];
                const distKm  = getDistanceKm(prev[0], prev[1], lat, lng);
                const distM   = distKm * 1000;
                const elapsedSec = Math.max((now - _gpsLastTime) / 1000, 0.1);
                const speedMps   = distM / elapsedSec;

                // 🔇 개선된 노이즈 필터: GPS 정확도 기반 적응형 필터링
                const accuracy = pos.coords.accuracy || 50; // 정확도 (미터)
                const minMoveThreshold = Math.max(GPS_MIN_MOVE_METERS, accuracy * 0.5);

                if (distM < minMoveThreshold) return;

                // 🚨 개선된 이상 점프 감지: 정확도 고려 및 다단계 검증
                const maxAllowedSpeed = accuracy > 20 ? GPS_MAX_SPEED_MPS * 0.7 : GPS_MAX_SPEED_MPS;

                if (speedMps > maxAllowedSpeed) {
                    console.warn(`[GPS] 이상 좌표 무시 — 속도 ${speedMps.toFixed(1)}m/s, 정확도 ${accuracy.toFixed(1)}m`);
                    return;
                }

                // ✅ 정상 이동 — 거리·칼로리 누적 (개선된 알고리즘)
                walkDistanceRun += distKm;

                // 칼로리 계산 최적화: 체중, 속도, 지형 고려
                const pet = typeof getActivePet === 'function' ? getActivePet() : null;
                const petWeight = pet?.weight || 8; // kg
                const walkSpeedKmh = (distKm / elapsedSec) * 3600;

                // MET (Metabolic Equivalent) 기반 칼로리 계산
                // 느린 산책: 2.5 MET, 보통: 3.5 MET, 빠른: 4.5 MET
                let met = 3.5;
                if (walkSpeedKmh < 3) met = 2.5;
                else if (walkSpeedKmh > 5) met = 4.5;

                const caloriesBurned = (met * petWeight * (elapsedSec / 3600));
                walkCaloriesRun += caloriesBurned;

                document.getElementById('walk-distance-display').innerText = `${walkDistanceRun.toFixed(2)} km`;
                document.getElementById('walk-calories-display').innerText = `${Math.round(walkCaloriesRun)} kcal`;
            }

            _gpsLastTime = now;
            walkMarkerCoordsHistory.push(currentCoord);

            // 🐾 이동 경로 선 업데이트
            if (simulationWalkLine) mapInstance.removeLayer(simulationWalkLine);
            simulationWalkLine = L.polyline(walkMarkerCoordsHistory, {
                color: '#e37736', weight: 5, opacity: 0.85, dashArray: '5, 10'
            }).addTo(mapInstance);

            updatePetMarker(currentCoord);
            mapInstance.panTo(currentCoord);
        },
        function (err) {
            const msgs = {
                1: "위치 권한이 거부되었습니다. 설정에서 위치를 허용해 주세요.",
                2: "현재 위치를 가져올 수 없습니다.",
                3: "GPS 신호 시간 초과."
            };
            showToast(msgs[err.code] || "GPS 오류가 발생했습니다.");

            // 🔄 자동 재시도: 에러 코드 2·3(신호 손실)이면 3초 후 재시작 (최대 3회)
            if (err.code !== 1 && _gpsRetryCount < GPS_RETRY_MAX && isWalkingActive) {
                _gpsRetryCount++;
                console.warn(`[GPS] 재시도 ${_gpsRetryCount}/${GPS_RETRY_MAX}회 (3초 후)`);
                if (gpsWatchId !== null) {
                    navigator.geolocation.clearWatch(gpsWatchId);
                    gpsWatchId = null;
                }
                setTimeout(() => {
                    if (isWalkingActive) {
                        showToast(`📡 GPS 신호 재연결 중... (${_gpsRetryCount}/${GPS_RETRY_MAX})`);
                        startGpsWalk(); // 재귀 재시작
                    }
                }, 3000);
            }
        },
        // 배터리 절약: maximumAge 3초 허용 (동일 위치 반복 수신 감소)
        { enableHighAccuracy: true, timeout: 15000, maximumAge: 3000 }
    );
}

function updatePetMarker(coord) {
    if (simulationMarkerInstance) mapInstance.removeLayer(simulationMarkerInstance);
    
    const currentPet = getActivePet();
    let petAvatarStr = "🐕";
    if (currentPet) {
        if (currentPet.type === "cat") petAvatarStr = "🐈";
        else if (currentPet.type === "rabbit") petAvatarStr = "🐰";
        else if (currentPet.type === "hamster") petAvatarStr = "🐹";
    }
    
    const dogHtmlIcon = L.divIcon({
        html: `<div class="w-9 h-9 bg-white border-2 border-brand-500 rounded-full shadow-lg flex items-center justify-center text-xl animate-bounce">${petAvatarStr}</div>`,
        className: 'dog-tracking-marker',
        iconSize: [36, 36],
        iconAnchor: [18, 18]
    });
    
    simulationMarkerInstance = L.marker(coord, { icon: dogHtmlIcon }).addTo(mapInstance);
}

async function generateRandomRoute() {
    if (!mapInstance) { showToast("⚠️ 지도를 먼저 로드해주세요."); return; }
    if (_routingBusy) { showToast("⏳ 경로 계산 중입니다..."); return; }

    _routingBusy = true;
    showToast('🗺️ 도로를 따라 산책 경로 생성 중...');

    // 현재 위치 사용 (GPS 위치 우선, 없으면 지도 중심)
    let lat, lng;
    if (WalkModule.myLocationMarker) {
        const pos = WalkModule.myLocationMarker.getLatLng();
        lat = pos.lat;
        lng = pos.lng;
    } else {
        const center = mapInstance.getCenter();
        lat = center.lat;
        lng = center.lng;
    }

    // 반경 250~700m 랜덤, 경유지 4~6개 (OSRM 좌표 수 제한 고려)
    const radiusM = 250 + Math.random() * 450;
    const pointCount = 4 + Math.floor(Math.random() * 3);
    const latPerM = 1 / 111000;
    const lngPerM = 1 / (111000 * Math.cos(lat * Math.PI / 180));

    const waypoints = [];
    for (let i = 0; i < pointCount; i++) {
        const angle = (i / pointCount) * 2 * Math.PI;
        const r = radiusM * (0.65 + Math.random() * 0.7);
        waypoints.push([
            lat + Math.sin(angle) * r * latPerM,
            lng + Math.cos(angle) * r * lngPerM
        ]);
    }
    waypoints.push(waypoints[0]); // 시작점 복귀

    // OSRM으로 도로 경로 계산
    const roadCoords = await routeViaOSRM(waypoints);
    const finalCoords = roadCoords || waypoints; // 실패 시 직선 폴백

    // 총 거리 계산
    let totalDist = 0;
    for (let i = 1; i < finalCoords.length; i++) {
        totalDist += getDistanceKm(finalCoords[i-1][0], finalCoords[i-1][1], finalCoords[i][0], finalCoords[i][1]);
    }

    const names = ['아침 산책 코스', '저녁 산책 코스', '공원 한 바퀴', '동네 한 바퀴', '힐링 코스', '활기찬 코스'];
    const today = new Date().toLocaleDateString('ko-KR', { month: 'numeric', day: 'numeric' });
    const name = `${names[Math.floor(Math.random() * names.length)]} (${today})`;

    const newRoute = {
        id: Date.now(),
        name,
        coords: finalCoords,
        distance: totalDist.toFixed(2),
        favorite: false,
        createdAt: Date.now()
    };

    customRoutes.push(newRoute);
    saveState();
    if (typeof uploadRouteToSupabase === 'function') uploadRouteToSupabase(newRoute);

    renderCustomRoutesList();
    loadCustomRouteOnMap(newRoute.id);
    mapInstance.fitBounds(L.polyline(finalCoords).getBounds(), { padding: [40, 40] });

    _routingBusy = false;
    const via = roadCoords ? '도로 경로' : '직선 경로';
    showToast(`🎲 '${name}' 생성 완료! ${totalDist.toFixed(2)}km (${via})`);
}

// 산책 완료 후 장소 등록 유도
function _promptRegisterWalkPlace() {
    setTimeout(() => {
        showCustomDialog({
            title: "장소 등록하기 📍",
            message: "방금 다녀온 산책 장소를 동네 지도에 등록할까요? 이웃 집사들과 핫플레이스를 공유해요!",
            icon: "🗺️",
            type: "confirm",
            onConfirm: () => {
                showCustomDialog({
                    title: "장소 이름 입력 ✏️",
                    message: "등록할 장소 이름을 입력해 주세요.",
                    icon: "📍",
                    type: "prompt",
                    placeholder: "예: 우리 동네 댕댕이 공원",
                    onConfirm: (name) => {
                        if (!name || !name.trim()) return;
                        const center = mapInstance ? mapInstance.getCenter() : { lat: 37.5665, lng: 126.9780 };
                        const newPlace = {
                            id: Date.now(),
                            name: name.trim(),
                            lat: center.lat,
                            lng: center.lng,
                            category: "park",
                            desc: "이웃 집사가 산책 완료 후 직접 등록한 반려동물 친화 공간입니다.",
                            rating: 5.0,
                            reviews: [{ author: settings_nickname || "집사", rating: 5, comment: "우리 동네 좋은 산책 장소예요!" }]
                        };
                        places.push(newPlace);
                        saveState();
                        if (typeof renderMapPlacesPins === 'function') renderMapPlacesPins();
                        showToast(`'${newPlace.name}' 장소가 동네 지도에 등록되었습니다! 🎉`);
                    }
                });
            }
        });
    }, 800);
}
