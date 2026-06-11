// ====================================================
// 펫라이프 지도 모듈 — Leaflet 기반 인터랙티브 지도
// ====================================================

let petlifeMap = null;
let petlifeMarkers = [];
let petlifeMyLocationMarker = null;
let petlifeMyLocationCircle = null;
let currentMapStyle = 'voyager'; // voyager, satellite, dark

// 펫라이프 가맹점 데이터 (좌표 기반)
const PETLIFE_LOCATIONS = [
    {
        id: 'healing-spa',
        name: '포레스트 힐 펫 스파',
        emoji: '🛁',
        category: 'spa',
        lat: 37.5665,
        lng: 126.9780,
        color: '#0d9488'
    },
    {
        id: 'healing-camping',
        name: '도그빌 오션 캠핑장',
        emoji: '🏕️',
        category: 'spa',
        lat: 37.5700,
        lng: 126.9820,
        color: '#15803d'
    },
    {
        id: 'healing-therapy',
        name: '아로마 펫 테라피 살롱',
        emoji: '🌸',
        category: 'medical',
        lat: 37.5630,
        lng: 126.9750,
        color: '#e11d48'
    },
    {
        id: 'healing-hospital',
        name: '24시 센트럴 메디컬 센터',
        emoji: '🏥',
        category: 'medical',
        lat: 37.5680,
        lng: 126.9850,
        color: '#dc2626'
    },
    {
        id: 'healing-hotel',
        name: '가든 테라스 펫 리조트',
        emoji: '🏨',
        category: 'hotel',
        lat: 37.5640,
        lng: 126.9720,
        color: '#4f46e5'
    },
    {
        id: 'healing-shopping',
        name: '펫라이프 프리미엄 멀티샵',
        emoji: '🛒',
        category: 'shop',
        lat: 37.5710,
        lng: 126.9790,
        color: '#d97706'
    }
];

// 지도 타일 레이어 스타일
const MAP_TILES = {
    voyager: {
        url: 'https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png',
        attribution: '&copy; CARTO'
    },
    satellite: {
        url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
        attribution: '&copy; Esri'
    },
    dark: {
        url: 'https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png',
        attribution: '&copy; CARTO'
    }
};

// 지도 초기화
function initPetlifeMap() {
    const mapContainer = document.getElementById('petlife-map');
    if (!mapContainer) return;

    // Leaflet 로드 확인
    if (typeof L === 'undefined') {
        setTimeout(initPetlifeMap, 500);
        return;
    }

    // 이미 초기화되었으면 크기 재조정만
    if (petlifeMap) {
        setTimeout(() => {
            petlifeMap.invalidateSize();
        }, 100);
        return;
    }

    // 기본 중심 좌표 (서울 중심)
    const DEFAULT_LAT = 37.5665;
    const DEFAULT_LNG = 126.9780;

    // 지도 생성
    petlifeMap = L.map('petlife-map', {
        center: [DEFAULT_LAT, DEFAULT_LNG],
        zoom: 14,
        zoomControl: true,
        attributionControl: false
    });

    // 타일 레이어 추가
    L.tileLayer(MAP_TILES[currentMapStyle].url, {
        maxZoom: 20,
        attribution: MAP_TILES[currentMapStyle].attribution
    }).addTo(petlifeMap);

    // 가맹점 마커 추가
    renderPetlifeMarkers();

    // 100ms 후 지도 크기 재조정 (렌더링 완료 대기)
    setTimeout(() => {
        petlifeMap.invalidateSize();
    }, 100);
}

// 가맹점 마커 렌더링
function renderPetlifeMarkers() {
    if (!petlifeMap) return;

    // 기존 마커 제거
    petlifeMarkers.forEach(marker => petlifeMap.removeLayer(marker));
    petlifeMarkers = [];

    // 새 마커 생성
    PETLIFE_LOCATIONS.forEach(location => {
        const icon = L.divIcon({
            html: `
                <div class="petlife-marker" style="
                    background: ${location.color};
                    width: 44px;
                    height: 44px;
                    border-radius: 50% 50% 50% 0;
                    transform: rotate(-45deg);
                    border: 3px solid white;
                    box-shadow: 0 4px 12px rgba(0,0,0,0.25);
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    cursor: pointer;
                    transition: all 0.3s ease;
                ">
                    <span style="
                        font-size: 20px;
                        transform: rotate(45deg);
                        line-height: 1;
                    ">${location.emoji}</span>
                </div>
                <style>
                .petlife-marker:hover {
                    transform: rotate(-45deg) scale(1.2);
                }
                </style>
            `,
            className: '',
            iconSize: [44, 44],
            iconAnchor: [22, 44],
            popupAnchor: [0, -44]
        });

        const marker = L.marker([location.lat, location.lng], { icon: icon })
            .addTo(petlifeMap)
            .bindPopup(`
                <div style="text-align:center;padding:8px;">
                    <div style="font-size:28px;margin-bottom:8px;">${location.emoji}</div>
                    <div style="font-weight:900;font-size:13px;color:#0f172a;margin-bottom:4px;">${location.name}</div>
                    <button onclick="selectIslandShop('${location.id}')" style="
                        background: linear-gradient(135deg, ${location.color}, ${adjustColorBrightness(location.color, -20)});
                        color: white;
                        border: none;
                        padding: 6px 14px;
                        border-radius: 8px;
                        font-size: 11px;
                        font-weight: 800;
                        cursor: pointer;
                        margin-top: 4px;
                    ">상세보기 →</button>
                </div>
            `);

        // 마커 클릭 시 자동으로 상세 정보 표시
        marker.on('click', () => {
            if (typeof selectIslandShop === 'function') {
                selectIslandShop(location.id);
            }
        });

        petlifeMarkers.push(marker);
    });
}

// 내 위치로 이동
function moveToMyLocation() {
    if (!navigator.geolocation) {
        if (typeof showToast === 'function') {
            showToast("이 브라우저는 위치 정보를 지원하지 않습니다.");
        }
        return;
    }

    if (typeof showToast === 'function') {
        showToast("📍 현재 위치를 불러오는 중...");
    }

    navigator.geolocation.getCurrentPosition(
        function (pos) {
            const lat = pos.coords.latitude;
            const lng = pos.coords.longitude;

            if (!petlifeMap) return;

            // 지도 중심 이동
            petlifeMap.setView([lat, lng], 16);

            // 기존 위치 마커 제거
            if (petlifeMyLocationMarker) petlifeMap.removeLayer(petlifeMyLocationMarker);
            if (petlifeMyLocationCircle) petlifeMap.removeLayer(petlifeMyLocationCircle);

            // 현재 위치 마커
            const myIcon = L.divIcon({
                html: `
                    <div style="
                        width:18px;
                        height:18px;
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
                    </style>
                `,
                className: '',
                iconSize: [18, 18],
                iconAnchor: [9, 9]
            });

            petlifeMyLocationMarker = L.marker([lat, lng], { icon: myIcon, zIndexOffset: 1000 })
                .addTo(petlifeMap)
                .bindPopup("📍 현재 내 위치");

            // 정확도 원
            petlifeMyLocationCircle = L.circle([lat, lng], {
                radius: pos.coords.accuracy,
                color: '#2563eb',
                fillColor: '#93c5fd',
                fillOpacity: 0.15,
                weight: 2
            }).addTo(petlifeMap);

            if (typeof showToast === 'function') {
                showToast("✅ 현재 위치로 이동했습니다!");
            }
        },
        function (error) {
            if (typeof showToast === 'function') {
                showToast("⚠️ 위치 정보를 가져올 수 없습니다.");
            }
            console.warn('Geolocation error:', error);
        },
        {
            enableHighAccuracy: true,
            timeout: 10000,
            maximumAge: 30000
        }
    );
}

// 지도 스타일 변경
function toggleMapStyle() {
    if (!petlifeMap) return;

    // 스타일 순환: voyager → satellite → dark → voyager
    const styles = ['voyager', 'satellite', 'dark'];
    const currentIndex = styles.indexOf(currentMapStyle);
    const nextIndex = (currentIndex + 1) % styles.length;
    currentMapStyle = styles[nextIndex];

    // 기존 타일 레이어 제거
    petlifeMap.eachLayer(layer => {
        if (layer instanceof L.TileLayer) {
            petlifeMap.removeLayer(layer);
        }
    });

    // 새 타일 레이어 추가
    L.tileLayer(MAP_TILES[currentMapStyle].url, {
        maxZoom: 20,
        attribution: MAP_TILES[currentMapStyle].attribution
    }).addTo(petlifeMap);

    // 마커 다시 추가 (레이어 순서 유지)
    renderPetlifeMarkers();

    const styleNames = {
        voyager: '기본 지도',
        satellite: '위성 지도',
        dark: '다크 모드'
    };

    if (typeof showToast === 'function') {
        showToast(`🗺️ ${styleNames[currentMapStyle]}로 변경되었습니다`);
    }
}

// 색상 밝기 조정 유틸리티
function adjustColorBrightness(hex, percent) {
    const num = parseInt(hex.replace('#', ''), 16);
    const amt = Math.round(2.55 * percent);
    const R = (num >> 16) + amt;
    const G = (num >> 8 & 0x00FF) + amt;
    const B = (num & 0x0000FF) + amt;
    return '#' + (
        0x1000000 +
        (R < 255 ? (R < 1 ? 0 : R) : 255) * 0x10000 +
        (G < 255 ? (G < 1 ? 0 : G) : 255) * 0x100 +
        (B < 255 ? (B < 1 ? 0 : B) : 255)
    ).toString(16).slice(1);
}

// 펫라이프 지도 필터 적용
function applyPetlifeMapFilters() {
    const fSpa = document.getElementById('f-spa')?.checked;
    const fMedical = document.getElementById('f-medical')?.checked;
    const fHotel = document.getElementById('f-hotel')?.checked;
    const fShop = document.getElementById('f-shop')?.checked;

    let activeCount = 0;

    petlifeMarkers.forEach((marker, index) => {
        const location = PETLIFE_LOCATIONS[index];
        if (!location) return;

        let show = true;

        if (location.category === 'spa' && !fSpa) show = false;
        if (location.category === 'medical' && !fMedical) show = false;
        if (location.category === 'hotel' && !fHotel) show = false;
        if (location.category === 'shop' && !fShop) show = false;

        if (show) {
            marker.setOpacity(1);
            marker.getElement()?.style.setProperty('pointer-events', 'auto');
            activeCount++;
        } else {
            marker.setOpacity(0.15);
            marker.getElement()?.style.setProperty('pointer-events', 'none');
        }
    });

    // 통계 바 업데이트
    const statBar = document.getElementById('stat-bar');
    const statLabel = document.getElementById('stat-label');
    if (statBar) statBar.style.width = `${(activeCount / PETLIFE_LOCATIONS.length) * 100}%`;
    if (statLabel) statLabel.innerText = `${activeCount} / ${PETLIFE_LOCATIONS.length} 활성`;
}

// 전역 함수 등록
window.initPetlifeMap = initPetlifeMap;
window.moveToMyLocation = moveToMyLocation;
window.toggleMapStyle = toggleMapStyle;
window.applyPetlifeMapFilters = applyPetlifeMapFilters;
