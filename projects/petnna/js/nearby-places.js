// nearby-places.js — 실제 공원, 카페, 병원 데이터를 OpenStreetMap Overpass API로 가져오기

let nearbyPlacesCache = {
    parks: [],
    cafes: [],
    hospitals: [],
    lastFetch: null,
    lastPosition: null
};

// 현재 위치 주변의 실제 장소 데이터 가져오기
async function fetchNearbyPlaces(lat, lng, radiusMeters = 1500) {
    // 캐시 체크: 같은 위치에서 5분 이내 재요청 방지
    const now = Date.now();
    const cacheValid = nearbyPlacesCache.lastFetch &&
                       nearbyPlacesCache.lastPosition &&
                       (now - nearbyPlacesCache.lastFetch < 5 * 60 * 1000) &&
                       (Math.abs(nearbyPlacesCache.lastPosition.lat - lat) < 0.01) &&
                       (Math.abs(nearbyPlacesCache.lastPosition.lng - lng) < 0.01);

    if (cacheValid) {
        console.log('✅ Using cached nearby places data');
        return nearbyPlacesCache;
    }

    console.log('🔍 Fetching nearby places from Overpass API...');

    try {
        // Overpass API 쿼리 (공원, 카페, 동물병원)
        const query = `
            [out:json][timeout:15];
            (
                // 공원 (park, dog_park, garden)
                node["leisure"="park"](around:${radiusMeters},${lat},${lng});
                way["leisure"="park"](around:${radiusMeters},${lat},${lng});
                node["leisure"="dog_park"](around:${radiusMeters},${lat},${lng});
                way["leisure"="dog_park"](around:${radiusMeters},${lat},${lng});
                node["leisure"="garden"](around:${radiusMeters},${lat},${lng});
                way["leisure"="garden"](around:${radiusMeters},${lat},${lng});

                // 카페 (반려동물 동반 가능한 곳 우선)
                node["amenity"="cafe"](around:${radiusMeters},${lat},${lng});
                way["amenity"="cafe"](around:${radiusMeters},${lat},${lng});

                // 동물병원
                node["amenity"="veterinary"](around:${radiusMeters},${lat},${lng});
                way["amenity"="veterinary"](around:${radiusMeters},${lat},${lng});
            );
            out center;
        `;

        const response = await fetch('https://overpass-api.de/api/interpreter', {
            method: 'POST',
            body: query,
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' }
        });

        if (!response.ok) {
            throw new Error(`Overpass API error: ${response.status}`);
        }

        const data = await response.json();
        console.log(`📍 Fetched ${data.elements.length} nearby places`);

        // 데이터 파싱 및 분류
        const parks = [];
        const cafes = [];
        const hospitals = [];

        data.elements.forEach(el => {
            const name = el.tags?.name || el.tags?.['name:ko'] || '이름 없음';
            const lat = el.lat || el.center?.lat;
            const lng = el.lon || el.center?.lon;

            if (!lat || !lng) return;

            const place = {
                id: `osm_${el.type}_${el.id}`,
                name: name,
                lat: lat,
                lng: lng,
                source: 'osm',
                tags: el.tags
            };

            // 카테고리별 분류
            if (el.tags?.leisure === 'park' || el.tags?.leisure === 'dog_park' || el.tags?.leisure === 'garden') {
                parks.push({
                    ...place,
                    category: 'park',
                    desc: el.tags?.leisure === 'dog_park' ? '🐕 반려견 전용 공원' : '🌳 공원',
                    rating: 4.5
                });
            } else if (el.tags?.amenity === 'cafe') {
                cafes.push({
                    ...place,
                    category: 'cafe',
                    desc: '☕ 카페',
                    rating: 4.0
                });
            } else if (el.tags?.amenity === 'veterinary') {
                hospitals.push({
                    ...place,
                    category: 'hospital',
                    desc: '🏥 동물병원',
                    rating: 4.5
                });
            }
        });

        // 캐시 업데이트
        nearbyPlacesCache = {
            parks: parks.slice(0, 20), // 최대 20개씩
            cafes: cafes.slice(0, 15),
            hospitals: hospitals.slice(0, 10),
            lastFetch: now,
            lastPosition: { lat, lng }
        };

        console.log(`✅ Cached: ${parks.length} parks, ${cafes.length} cafes, ${hospitals.length} hospitals`);
        return nearbyPlacesCache;

    } catch (error) {
        console.error('❌ Overpass API fetch error:', error);
        if (typeof showToast === 'function') {
            showToast('주변 장소를 불러오는데 실패했습니다. 다시 시도해주세요.');
        }
        return nearbyPlacesCache; // 기존 캐시라도 반환
    }
}

// 실제 장소 + 사용자 등록 장소 통합하여 지도에 표시
async function loadAndRenderNearbyPlaces() {
    if (!mapInstance) return;

    // 현재 위치 가져오기
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

    if (typeof showToast === 'function') {
        showToast('📍 주변 공원, 카페, 병원을 검색 중...');
    }

    // 실제 장소 데이터 가져오기
    const nearby = await fetchNearbyPlaces(lat, lng);

    // 사용자가 등록한 장소 (places 배열)
    const userPlaces = (typeof places !== 'undefined') ? places : [];

    // 통합 장소 배열 생성
    const allPlaces = [
        ...nearby.parks,
        ...nearby.cafes,
        ...nearby.hospitals,
        ...userPlaces
    ];

    // 중복 제거 (같은 이름 + 근처 좌표)
    const uniquePlaces = [];
    const seen = new Set();

    allPlaces.forEach(p => {
        const key = `${p.name}_${Math.round(p.lat * 1000)}_${Math.round(p.lng * 1000)}`;
        if (!seen.has(key)) {
            seen.add(key);
            uniquePlaces.push(p);
        }
    });

    // 지도에 마커 렌더링
    if (typeof renderPlacesOnMap === 'function') {
        renderPlacesOnMap(uniquePlaces);
    }

    if (typeof showToast === 'function') {
        const total = uniquePlaces.length;
        const parks = uniquePlaces.filter(p => p.category === 'park').length;
        const cafes = uniquePlaces.filter(p => p.category === 'cafe').length;
        const hospitals = uniquePlaces.filter(p => p.category === 'hospital').length;
        showToast(`✅ 주변 장소 ${total}개 로드 완료! (공원 ${parks}, 카페 ${cafes}, 병원 ${hospitals})`);
    }

    return uniquePlaces;
}

// 지도 이동 완료 시 주변 장소 자동 갱신
function setupAutoRefreshPlaces() {
    if (!mapInstance) return;

    let moveTimeout;
    mapInstance.on('moveend', () => {
        clearTimeout(moveTimeout);
        moveTimeout = setTimeout(() => {
            // 사용자가 지도를 많이 이동했을 때만 자동 갱신
            const center = mapInstance.getCenter();
            const lastPos = nearbyPlacesCache.lastPosition;

            if (!lastPos ||
                Math.abs(center.lat - lastPos.lat) > 0.02 ||
                Math.abs(center.lng - lastPos.lng) > 0.02) {
                console.log('📍 Map moved significantly, refreshing places...');
                loadAndRenderNearbyPlaces();
            }
        }, 1000); // 1초 디바운스
    });
}

// 장소 마커를 지도에 렌더링 (기존 renderMapPlacesPins 대체)
function renderPlacesOnMap(placesToRender) {
    if (!mapInstance) return;

    // 기존 마커 제거
    if (typeof mapMarkers !== 'undefined') {
        mapMarkers.forEach(m => mapInstance.removeLayer(m));
        mapMarkers.length = 0;
    } else {
        window.mapMarkers = [];
    }

    placesToRender.forEach(p => {
        let emoji = "🌳";
        let colorClass = "bg-emerald-500";

        if (p.category === "cafe") {
            emoji = "☕";
            colorClass = "bg-amber-500";
        } else if (p.category === "hospital") {
            emoji = "🏥";
            colorClass = "bg-red-500";
        }

        const customHtmlIcon = L.divIcon({
            html: `<div class="w-8 h-8 rounded-full ${colorClass} border-2 border-white shadow-md flex items-center justify-center text-sm transform transition-transform hover:scale-125">${emoji}</div>`,
            className: 'custom-leaflet-marker',
            iconSize: [32, 32],
            iconAnchor: [16, 32]
        });

        const pin = L.marker([p.lat, p.lng], { icon: customHtmlIcon }).addTo(mapInstance);

        // 클릭 시 장소 정보 표시
        pin.on('click', function () {
            // 기존 openPlacePanel이 있으면 사용, 없으면 팝업
            if (typeof openPlacePanel === 'function' && p.id && !p.id.toString().startsWith('osm_')) {
                openPlacePanel(p.id);
            } else {
                // OSM 장소는 간단한 팝업 표시
                const popupContent = `
                    <div class="text-xs p-2">
                        <div class="font-black text-sm mb-1">${emoji} ${p.name}</div>
                        <div class="text-gray-500 text-[10px]">${p.desc || '장소 정보'}</div>
                        ${p.tags?.['addr:full'] || p.tags?.['addr:street'] ?
                            `<div class="text-gray-400 text-[9px] mt-1">📍 ${p.tags['addr:full'] || p.tags['addr:street']}</div>` : ''}
                    </div>
                `;
                pin.bindPopup(popupContent).openPopup();
            }
        });

        mapMarkers.push(pin);
    });

    console.log(`✅ Rendered ${placesToRender.length} place markers on map`);
}
