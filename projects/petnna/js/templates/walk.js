const WALK_TEMPLATE = `
<div class="space-y-4 animate-fade-in">
    <div class="flex flex-col sm:flex-row gap-4 items-start">
    <div class="flex-1 min-w-0 bg-white rounded-2xl p-4 border border-amber-50 shadow-sm relative overflow-hidden">

        <!-- ── 헤더 ────────────────────────────────────────────────── -->
        <div class="flex items-center justify-between mb-3">
            <div>
                <h2 class="text-base font-bold text-gray-800 flex items-center gap-2">
                    <i class="fa-solid fa-map-location-dot text-brand-500 text-lg"></i>
                    동동안심 반려 지도
                </h2>
                <p class="text-[11px] text-gray-500 mt-0.5">💩응가 · 💦쉬야 · 👃킁킁 지점을 지도에 기록해요</p>
            </div>
            <div class="flex gap-1.5">
                <button onclick="startRouteDrawingMode()"
                    class="bg-brand-50 hover:bg-brand-100 text-brand-700 font-bold text-[11px] py-2 px-3 rounded-xl transition-all flex items-center gap-1.5">
                    <i class="fa-solid fa-route text-sm"></i>
                    <span class="hidden sm:inline">경로 만들기</span>
                </button>
                <button onclick="promptImportTrailCode()"
                    class="bg-brand-50 hover:bg-brand-100 text-brand-700 font-bold text-[11px] py-2 px-3 rounded-xl transition-all flex items-center gap-1.5">
                    <i class="fa-solid fa-file-import text-sm"></i>
                    <span class="hidden sm:inline">가져오기</span>
                </button>
            </div>
        </div>

        <!-- ── 검색바 ──────────────────────────────────────────────── -->
        <div class="relative mb-3">
            <i class="fa-solid fa-magnifying-glass text-gray-400 absolute left-3 top-1/2 -translate-y-1/2 text-xs"></i>
            <input type="text" id="map-search-input" onkeyup="filterMapPlaces()"
                placeholder="동네 병원, 공원, 카페 검색..."
                class="w-full text-xs border border-gray-200 rounded-xl pl-9 pr-3 py-2.5 outline-none focus:border-brand-400 transition-colors">
        </div>

        <!-- ── 카테고리 탭 ──────────────────────────────────────────── -->
        <div class="flex gap-1.5 mb-3 overflow-x-auto no-scrollbar">
            <button onclick="filterMapCategory('all')"
                class="map-cat-btn flex-shrink-0 text-[11px] font-bold py-2 px-4 rounded-xl bg-brand-500 text-white transition-all"
                data-cat="all">전체</button>
            <button onclick="filterMapCategory('park')"
                class="map-cat-btn flex-shrink-0 text-[11px] font-bold py-2 px-4 rounded-xl bg-gray-50 text-gray-600 border border-gray-200 transition-all"
                data-cat="park"><span class="mr-1">🌳</span>공원</button>
            <button onclick="filterMapCategory('cafe')"
                class="map-cat-btn flex-shrink-0 text-[11px] font-bold py-2 px-4 rounded-xl bg-gray-50 text-gray-600 border border-gray-200 transition-all"
                data-cat="cafe"><span class="mr-1">☕</span>카페</button>
            <button onclick="filterMapCategory('hospital')"
                class="map-cat-btn flex-shrink-0 text-[11px] font-bold py-2 px-4 rounded-xl bg-gray-50 text-gray-600 border border-gray-200 transition-all"
                data-cat="hospital"><span class="mr-1">🏥</span>병원</button>
        </div>

        <!-- ── 지도 (Critical: 최소 280px 확보) ──────────────────────── -->
        <div class="relative w-full bg-gray-100 rounded-2xl overflow-hidden border border-amber-100/50 shadow-inner z-10"
            style="height: clamp(360px, 70vh, 760px);">
            <div id="map" class="w-full h-full"></div>

            <!-- 일시정지 오버레이 (기본 hidden) -->
            <div id="walk-overlay"
                class="hidden absolute inset-0 bg-black/40 backdrop-blur-sm flex flex-col items-center justify-center text-white p-6 text-center pointer-events-none transition-opacity duration-300"
                style="z-index: 9990;">
                <span class="text-5xl mb-3">⏸️</span>
                <h4 class="font-bold text-base mb-1">산책 일시정지</h4>
                <p class="text-xs text-white/80 leading-snug">'산책 시작' 버튼을 눌러 재개하세요.</p>
            </div>

            <!-- 장소 상세 패널 -->
            <div id="place-detail-panel"
                class="absolute top-0 right-0 bottom-0 w-72 bg-white/95 backdrop-blur shadow-2xl border-l border-amber-100 p-4 transition-transform duration-300 translate-x-full overflow-y-auto no-scrollbar flex flex-col justify-between"
                style="z-index: 10000;">
                <div class="space-y-3.5">
                    <div class="flex justify-between items-start pb-2 border-b">
                        <div>
                            <span id="p-detail-badge" class="bg-emerald-100 text-emerald-800 text-[8px] font-bold px-1.5 py-0.5 rounded uppercase">PARK</span>
                            <h4 id="p-detail-name" class="font-bold text-xs text-gray-800 mt-1">센트럴 파크</h4>
                        </div>
                        <button onclick="closePlacePanel()" class="text-gray-400 hover:text-gray-600 p-1" aria-label="닫기">
                            <i class="fa-solid fa-xmark text-base"></i>
                        </button>
                    </div>
                    <p id="p-detail-desc" class="text-[10px] text-gray-500 leading-relaxed">상세 설명입니다.</p>
                    <div class="flex items-center gap-1.5 text-xs">
                        <i class="fa-solid fa-star text-amber-400 text-xs"></i>
                        <span class="font-bold text-gray-700" id="p-detail-rating">4.9</span>
                        <span class="text-gray-300">|</span>
                        <span class="text-gray-400 text-[10px]">후기 <span id="p-detail-rev-count" class="font-mono">2</span>개</span>
                    </div>
                    <div class="space-y-2 pt-2 border-t">
                        <span class="block text-[10px] text-gray-400 font-bold uppercase tracking-wider">이웃 한줄평</span>
                        <div id="p-detail-reviews" class="space-y-2 max-h-40 overflow-y-auto no-scrollbar"></div>
                    </div>
                </div>
                <div class="mt-4 pt-3 border-t border-gray-100 space-y-2.5">
                    <span class="block text-[10px] text-brand-700 font-bold flex items-center gap-1">
                        <i class="fa-solid fa-pen-nib text-xs"></i> 후기 남기기
                    </span>
                    <div class="grid grid-cols-2 gap-1.5">
                        <input type="text" id="p-review-author" placeholder="닉네임" class="border rounded-lg px-2 py-2 outline-none text-[10px]">
                        <select id="p-review-rating" class="border rounded-lg px-1 py-2 outline-none text-[10px]">
                            <option value="5">⭐⭐⭐⭐⭐</option>
                            <option value="4">⭐⭐⭐⭐</option>
                            <option value="3">⭐⭐⭐</option>
                        </select>
                    </div>
                    <textarea id="p-review-text" rows="2" placeholder="꿀팁을 나눠주세요!"
                        class="w-full text-[10px] border rounded-lg p-2 outline-none focus:border-brand-400 resize-none"></textarea>
                    <button onclick="submitPlaceReview()"
                        class="w-full bg-brand-500 hover:bg-brand-600 text-white font-bold text-xs py-2.5 rounded-xl shadow-sm transition-all">
                        작성 완료 🚀
                    </button>
                </div>
            </div>
        </div>

        <!-- 경로 그리기 패널 -->
        <div id="route-draw-banner" class="hidden mt-3 p-4 bg-brand-50 border border-brand-200 rounded-2xl animate-fade-in flex flex-col gap-2.5"
            onclick="event.stopPropagation();">
            <div class="flex items-center gap-2.5">
                <span class="text-2xl">🎨</span>
                <div>
                    <span class="block text-[10px] font-bold text-brand-700 uppercase tracking-wider">Route Builder</span>
                    <p id="route-draw-banner-desc" class="text-xs font-bold text-gray-700">지도를 클릭해 경로를 그려주세요.</p>
                </div>
            </div>
            <div class="grid grid-cols-3 gap-2">
                <button onclick="undoLastRoutePoint()"
                    class="bg-white hover:bg-gray-50 text-gray-700 font-bold text-xs py-3 rounded-xl border border-gray-200 flex items-center justify-center gap-1.5 transition-all min-h-[44px]">
                    <i class="fa-solid fa-rotate-left text-base"></i> 취소
                </button>
                <button onclick="saveDrawnRoute()"
                    class="bg-brand-500 hover:bg-brand-600 text-white font-bold text-xs py-3 rounded-xl shadow-md flex items-center justify-center gap-1.5 transition-all min-h-[44px]">
                    <i class="fa-solid fa-floppy-disk text-base"></i> 저장
                </button>
                <button onclick="cancelDrawnRoute()"
                    class="bg-gray-700 hover:bg-gray-800 text-white font-bold text-xs py-3 rounded-xl flex items-center justify-center gap-1.5 transition-all min-h-[44px]" aria-label="닫기">
                    <i class="fa-solid fa-xmark text-base"></i> 종료
                </button>
            </div>
        </div>

    </div>

    <!-- ── 오른쪽 컬럼: 산책기록 + 나만의 산책로 ── -->
    <div class="w-full sm:w-72 flex-shrink-0 flex flex-col gap-4">

        <!-- 나만의 맞춤 산책로 (고도화) -->
        <div class="bg-white rounded-2xl p-4 border border-amber-50 shadow-sm space-y-3">

            <!-- 헤더 + 새 경로 버튼 -->
            <div class="flex items-center justify-between">
                <h4 class="font-bold text-gray-800 text-sm flex items-center gap-2">
                    <i class="fa-solid fa-map-location text-brand-500 text-base"></i>
                    나만의 산책로
                </h4>
                <div class="flex gap-1.5">
                    <button onclick="generateRandomRoute()"
                        class="bg-brand-500 hover:bg-brand-600 text-white font-bold text-[10px] px-2.5 py-1.5 rounded-xl flex items-center gap-1 transition-all shadow-sm">
                        <i class="fa-solid fa-shuffle text-xs"></i> 랜덤
                    </button>
                    <button onclick="startRouteDrawingMode()"
                        class="bg-brand-500 hover:bg-brand-600 text-white font-bold text-[10px] px-2.5 py-1.5 rounded-xl flex items-center gap-1 transition-all shadow-sm">
                        <i class="fa-solid fa-plus text-xs"></i> 직접
                    </button>
                </div>
            </div>

            <!-- 필터 탭 -->
            <div class="flex gap-1.5 overflow-x-auto no-scrollbar">
                <button onclick="filterCustomRoutes('all')"
                    class="route-filter-btn flex-shrink-0 text-[10px] font-bold py-1.5 px-3 rounded-lg bg-brand-500 text-white transition-all"
                    data-filter="all">전체</button>
                <button onclick="filterCustomRoutes('favorite')"
                    class="route-filter-btn flex-shrink-0 text-[10px] font-bold py-1.5 px-3 rounded-lg bg-gray-50 text-gray-600 border border-gray-200 transition-all"
                    data-filter="favorite">⭐ 즐겨찾기</button>
                <button onclick="filterCustomRoutes('short')"
                    class="route-filter-btn flex-shrink-0 text-[10px] font-bold py-1.5 px-3 rounded-lg bg-gray-50 text-gray-600 border border-gray-200 transition-all"
                    data-filter="short">🏃 단거리</button>
                <button onclick="filterCustomRoutes('long')"
                    class="route-filter-btn flex-shrink-0 text-[10px] font-bold py-1.5 px-3 rounded-lg bg-gray-50 text-gray-600 border border-gray-200 transition-all"
                    data-filter="long">🌿 장거리</button>
            </div>

            <!-- 정렬 + 공유코드 가져오기 -->
            <div class="flex gap-2">
                <select onchange="sortCustomRoutes(this.value)"
                    class="flex-1 text-[10px] font-bold border border-gray-200 rounded-xl px-2.5 py-2 outline-none focus:border-brand-400 text-gray-600 bg-gray-50">
                    <option value="recent">최근 생성순</option>
                    <option value="distance">거리순</option>
                    <option value="name">이름순</option>
                </select>
                <button onclick="promptImportTrailCode()"
                    class="flex-shrink-0 bg-brand-50 hover:bg-brand-100 text-brand-700 font-bold text-[10px] px-3 py-2 rounded-xl flex items-center gap-1 transition-all">
                    <i class="fa-solid fa-file-import text-xs"></i> 가져오기
                </button>
            </div>

            <!-- 경로 리스트 -->
            <div id="custom-routes-list" class="space-y-2 max-h-[320px] overflow-y-auto no-scrollbar"></div>

            <!-- 빈 상태 안내 (경로 없을 때 JS로 show/hide) -->
            <div id="custom-routes-empty" class="hidden text-center py-6 space-y-2">
                <span class="text-4xl">🗺️</span>
                <p class="text-[11px] text-gray-400 font-semibold">아직 저장된 산책로가 없어요</p>
                <p class="text-[10px] text-gray-300">지도에서 경로를 그려 나만의 코스를 만들어보세요!</p>
            </div>
        </div>

        <!-- 산책기록: 실시간 수치 + 제어 버튼 + 마킹 버튼 -->
        <div class="bg-white rounded-2xl p-4 border border-amber-50 shadow-sm space-y-3">

            <!-- 실시간 수치 + 제어 버튼 -->
            <div class="p-3.5 bg-brand-50/60 rounded-2xl border border-brand-100/40 space-y-3">

                <!-- 상태 배지 + 라벨 -->
                <div class="flex items-center justify-between">
                    <span class="text-[11px] font-bold text-gray-500 flex items-center gap-1.5">
                        <i class="fa-solid fa-chart-line text-brand-400 text-xs"></i> 실시간 기록
                    </span>
                    <span id="walk-status-badge"
                        class="text-[10px] font-bold px-2.5 py-1 rounded-full bg-gray-100 text-gray-500 transition-all">
                        산책 준비
                    </span>
                </div>

                <!-- 통계 3칸 -->
                <div class="grid grid-cols-3 gap-2 text-center">
                    <div class="bg-white py-3 px-1 rounded-xl border border-amber-100/60 shadow-sm overflow-hidden">
                        <i class="fa-regular fa-clock text-gray-400 text-sm mb-1 block"></i>
                        <span id="walk-time-display" class="text-sm font-bold text-gray-800 font-mono block leading-none truncate">00:00</span>
                        <span class="text-[9px] text-gray-500 font-semibold mt-0.5 block">시간</span>
                    </div>
                    <div class="bg-white py-3 px-1 rounded-xl border border-amber-100/60 shadow-sm overflow-hidden">
                        <i class="fa-solid fa-location-dot text-brand-400 text-sm mb-1 block"></i>
                        <span id="walk-distance-display" class="text-sm font-bold text-brand-600 font-mono block leading-none truncate">0.00</span>
                        <span class="text-[9px] text-gray-500 font-semibold mt-0.5 block">km</span>
                    </div>
                    <div class="bg-white py-3 px-1 rounded-xl border border-amber-100/60 shadow-sm overflow-hidden">
                        <i class="fa-solid fa-fire text-rose-400 text-sm mb-1 block"></i>
                        <span id="walk-calories-display" class="text-sm font-bold text-rose-500 font-mono block leading-none truncate">0</span>
                        <span class="text-[9px] text-gray-500 font-semibold mt-0.5 block">kcal</span>
                    </div>
                </div>

                <!-- 제어 버튼 3개 (Critical: min-h-[48px]) -->
                <div class="grid grid-cols-3 gap-2">
                    <button id="walk-start-btn" onclick="toggleWalk()"
                        class="bg-brand-500 hover:bg-brand-600 active:scale-95 text-white font-bold text-xs rounded-xl shadow-md transition-all flex flex-col items-center justify-center gap-1.5 min-h-[72px]">
                        <i class="fa-solid fa-play text-xl"></i>
                        <span>시작</span>
                    </button>
                    <button id="walk-stop-btn" onclick="discardWalk()" disabled
                        class="bg-gray-700 hover:bg-gray-800 text-white font-bold text-xs rounded-xl opacity-40 transition-all flex flex-col items-center justify-center gap-1.5 min-h-[72px]">
                        <i class="fa-solid fa-flag-checkered text-xl"></i>
                        <span>정지</span>
                    </button>
                    <button id="walk-save-btn" onclick="stopAndSaveWalk()" disabled
                        class="bg-emerald-600 hover:bg-emerald-700 text-white font-bold text-xs rounded-xl opacity-40 shadow-md transition-all flex flex-col items-center justify-center gap-1.5 min-h-[72px]">
                        <i class="fa-solid fa-floppy-disk text-xl"></i>
                        <span>저장 💾</span>
                    </button>
                </div>
            </div>

            <!-- ── 마킹 버튼 (Critical: min-h-[56px], disabled 명확 표현) ── -->
            <div class="grid grid-cols-3 gap-2">
                <button id="btn-mark-poop" onclick="placeMarking('poop')" disabled
                    class="opacity-40 grayscale cursor-not-allowed bg-amber-100 text-amber-800 font-bold rounded-xl transition-all shadow-sm flex flex-col items-center justify-center gap-1.5 min-h-[80px] border-2 border-dashed border-amber-200">
                    <span class="text-3xl leading-none">💩</span>
                    <span class="text-[10px] font-bold">응가</span>
                    <span class="text-[9px] font-bold bg-amber-200/60 px-1.5 py-0.5 rounded-full"><span id="stat-poop-count" class="font-mono">0</span>회</span>
                </button>
                <button id="btn-mark-pee" onclick="placeMarking('pee')" disabled
                    class="opacity-40 grayscale cursor-not-allowed bg-sky-100 text-sky-800 font-bold rounded-xl transition-all shadow-sm flex flex-col items-center justify-center gap-1.5 min-h-[80px] border-2 border-dashed border-sky-200">
                    <span class="text-3xl leading-none">💦</span>
                    <span class="text-[10px] font-bold">쉬야</span>
                    <span class="text-[9px] font-bold bg-sky-200/60 px-1.5 py-0.5 rounded-full"><span id="stat-pee-count" class="font-mono">0</span>회</span>
                </button>
                <button id="btn-mark-sniff" onclick="placeMarking('sniff')" disabled
                    class="opacity-40 grayscale cursor-not-allowed bg-emerald-100 text-emerald-800 font-bold rounded-xl transition-all shadow-sm flex flex-col items-center justify-center gap-1.5 min-h-[80px] border-2 border-dashed border-emerald-200">
                    <span class="text-3xl leading-none">👃</span>
                    <span class="text-[10px] font-bold">킁킁</span>
                    <span class="text-[9px] font-bold bg-emerald-200/60 px-1.5 py-0.5 rounded-full"><span id="stat-sniff-count" class="font-mono">0</span>회</span>
                </button>
            </div>

        </div>

    </div>
    </div>

    <!-- 산책 완료 역사관 -->
    <div class="bg-white rounded-2xl p-4 border border-amber-50 shadow-sm space-y-3">
        <h4 class="font-bold text-gray-800 text-sm flex items-center gap-2">
            <i class="fa-solid fa-route text-brand-500 text-lg"></i>
            산책 완료 역사관 🏆
        </h4>
        <div id="walk-history-list" class="space-y-3 max-h-[300px] overflow-y-auto no-scrollbar"></div>
    </div>

</div>
`;
