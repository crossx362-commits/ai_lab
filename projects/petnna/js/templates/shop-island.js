// 펫 라이프 — 캐주얼 다도해 SVG 월드맵 템플릿

const SHOP_ISLAND_TEMPLATE = `
<div class="island-world-wrap animate-fade-in">

  <!-- 헤더 -->
  <div class="iw-header">
    <div class="iw-header-icon">🌏</div>
    <div>
      <h1 class="iw-title">펫 라이프 아일랜드</h1>
      <p class="iw-subtitle">다도해 섬의 핀을 눌러 상상 속 펫 가맹점 퀘스트를 탐험하세요</p>
    </div>
    <span class="iw-badge">PET WORLD MAP</span>
  </div>

  <!-- 3-Col 레이아웃 -->
  <div class="iw-layout">

    <!-- 좌측 카테고리 필터 -->
    <div class="iw-panel iw-left-panel">
      <p class="panel-section-label">카테고리 필터</p>
      <div class="filter-list">
        <label class="filter-item">
          <input type="checkbox" checked id="f-spa" onchange="applyMapFilters()">
          <span class="filter-dot" style="background:#0d9488"></span>
          <span>힐링 & 스파</span>
          <span class="filter-count">2</span>
        </label>
        <label class="filter-item">
          <input type="checkbox" checked id="f-medical" onchange="applyMapFilters()">
          <span class="filter-dot" style="background:#e11d48"></span>
          <span>메디컬 케어</span>
          <span class="filter-count">2</span>
        </label>
        <label class="filter-item">
          <input type="checkbox" checked id="f-hotel" onchange="applyMapFilters()">
          <span class="filter-dot" style="background:#4f46e5"></span>
          <span>스테이 & 돌봄</span>
          <span class="filter-count">1</span>
        </label>
        <label class="filter-item">
          <input type="checkbox" checked id="f-shop" onchange="applyMapFilters()">
          <span class="filter-dot" style="background:#d97706"></span>
          <span>쇼핑 광장</span>
          <span class="filter-count">1</span>
        </label>
      </div>
      <div class="panel-divider"></div>
      <p class="panel-section-label">활성 영토</p>
      <div class="active-stat">
        <div class="stat-bar-bg"><div class="stat-bar-fill" id="stat-bar"></div></div>
        <span class="stat-text" id="stat-label">6 / 6 활성</span>
      </div>
    </div>

    <!-- 중앙: 인터랙티브 펫라이프 지도 (Leaflet) -->
    <div class="iw-map-container">
      <!-- 지도 컨트롤 -->
      <div class="map-controls">
        <button onclick="moveToMyLocation()" class="map-control-btn" title="내 위치">
          <i class="fa-solid fa-location-crosshairs"></i>
        </button>
        <button onclick="toggleMapStyle()" class="map-control-btn" title="지도 스타일">
          <i class="fa-solid fa-map"></i>
        </button>
      </div>

      <!-- Leaflet 지도 -->
      <div id="petlife-map" style="width:100%;height:100%;min-height:500px;border-radius:24px;"></div>

      <!-- 구 SVG 지도는 주석 처리 -->
      <!--
      <svg id="petlife-map-svg" viewBox="0 0 1000 650" xmlns="http://www.w3.org/2000/svg" style="width:100%;height:100%;display:block;">
        <defs>
          <!-- 바다 그라디언트 -->
          <linearGradient id="sea-grad" x1="0%" y1="0%" x2="0%" y2="100%">
            <stop offset="0%" stop-color="#bae6fd"/>
            <stop offset="50%" stop-color="#7dd3fc"/>
            <stop offset="100%" stop-color="#38bdf8"/>
          </linearGradient>
          
          <!-- 섬 3D 그라디언트들 -->
          <radialGradient id="island-main" cx="40%" cy="30%" r="70%">
            <stop offset="0%" stop-color="#d1fae5"/>
            <stop offset="45%" stop-color="#86efac"/>
            <stop offset="85%" stop-color="#4ade80"/>
            <stop offset="100%" stop-color="#22c55e"/>
          </radialGradient>
          
          <radialGradient id="island-sub" cx="45%" cy="35%" r="65%">
            <stop offset="0%" stop-color="#e2fdf0"/>
            <stop offset="60%" stop-color="#a7f3d0"/>
            <stop offset="100%" stop-color="#4ade80"/>
          </radialGradient>

          <!-- 입체 모래 해변 그라디언트 -->
          <linearGradient id="sand-grad" x1="0%" y1="0%" x2="0%" y2="100%">
            <stop offset="0%" stop-color="#fef08a"/>
            <stop offset="100%" stop-color="#fde047"/>
          </linearGradient>

          <!-- 그림자 필터 -->
          <filter id="island-shadow" x="-10%" y="-10%" width="125%" height="130%">
            <feDropShadow dx="0" dy="8" stdDeviation="8" flood-color="#0284c7" flood-opacity="0.3"/>
          </filter>
          <filter id="pin-shadow" x="-30%" y="-30%" width="160%" height="200%">
            <feDropShadow dx="0" dy="5" stdDeviation="4" flood-color="#000" flood-opacity="0.25"/>
          </filter>
        </defs>

        <!-- 바다 배경 -->
        <rect width="1000" height="650" fill="url(#sea-grad)"/>

        <!-- 배경 파도 애니메이션 데코레이션 -->
        <g opacity="0.3" stroke="white" stroke-width="1.8" fill="none" stroke-linecap="round">
          <path d="M 50 120 Q 75 110 100 120 T 150 120" class="wave-ani-1" />
          <path d="M 800 100 Q 825 90 850 100 T 900 100" class="wave-ani-2" />
          <path d="M 450 550 Q 475 540 500 550 T 550 550" class="wave-ani-1" />
          <path d="M 850 500 Q 875 490 900 500 T 950 500" class="wave-ani-3" />
          <path d="M 120 480 Q 145 470 170 480 T 220 480" class="wave-ani-2" />
        </g>

        <!-- 잔잔한 물결 링 이펙트 -->
        <circle cx="280" cy="300" r="140" fill="none" stroke="white" stroke-width="1.2" opacity="0.12" stroke-dasharray="5 5" />
        <circle cx="680" cy="220" r="120" fill="none" stroke="white" stroke-width="1" opacity="0.1" stroke-dasharray="4 4" />

        <!-- === 3D 입체 섬들 개별 배치 및 데코레이션 === -->

        <!-- ① 메인 본도 (비티레부) - 중앙 큰 섬 -->
        <g filter="url(#island-shadow)">
          <!-- 해변(Sand beach) 바닥 레이어 -->
          <ellipse cx="300" cy="330" rx="205" ry="125" fill="url(#sand-grad)" />
          <!-- 섬 본체 잔디 레이어 -->
          <ellipse cx="300" cy="326" rx="195" ry="115" fill="url(#island-main)" />
          <!-- 고지대/산맥 레이어 -->
          <path d="M 220 300 Q 250 260 300 270 T 380 310 Q 350 350 290 350 Z" fill="#22c55e" opacity="0.25" />
          <path d="M 250 310 Q 280 280 320 290 T 360 320 Q 320 340 270 330 Z" fill="#15803d" opacity="0.15" />
          <!-- 아기자기한 나무 데코레이션 -->
          <circle cx="180" cy="300" r="8" fill="#166534" />
          <circle cx="190" cy="305" r="6" fill="#14532d" />
          <circle cx="390" cy="360" r="7" fill="#166534" />
          <circle cx="400" cy="355" r="5" fill="#14532d" />
        </g>

        <!-- ② 북동 섬 (바누아레부) - 가로로 긴 화산섬 모양 -->
        <g filter="url(#island-shadow)">
          <path d="M500 160 Q570 120 650 130 T750 170 Q780 210 730 240 T580 230 Q510 230 480 200 Z" fill="url(#sand-grad)" />
          <path d="M510 160 Q575 125 645 135 T740 170 Q765 205 720 232 T580 222 Q515 222 490 197 Z" fill="url(#island-sub)" />
          <ellipse cx="610" cy="170" rx="50" ry="20" fill="#22c55e" opacity="0.2" />
        </g>

        <!-- ③ 서부 제도 (야사와 군도) - 작은 초승달형 섬 체인 -->
        <g filter="url(#island-shadow)">
          <!-- Yasawa 1 -->
          <ellipse cx="120" cy="180" rx="35" ry="20" fill="url(#sand-grad)" transform="rotate(-20 120 180)" />
          <ellipse cx="120" cy="178" rx="31" ry="17" fill="url(#island-sub)" transform="rotate(-20 120 178)" />
          <!-- Yasawa 2 -->
          <ellipse cx="150" cy="130" rx="25" ry="15" fill="url(#sand-grad)" transform="rotate(-15 150 130)" />
          <ellipse cx="150" cy="128" rx="22" ry="13" fill="url(#island-sub)" transform="rotate(-15 150 128)" />
          <!-- Yasawa 3 -->
          <ellipse cx="175" cy="90" rx="20" ry="12" fill="url(#sand-grad)" transform="rotate(-10 175 90)" />
          <ellipse cx="175" cy="89" rx="17" ry="10" fill="url(#island-sub)" transform="rotate(-10 175 89)" />
        </g>

        <!-- ④ 남부 섬 (카다부) -->
        <g filter="url(#island-shadow)">
          <ellipse cx="270" cy="530" rx="65" ry="40" fill="url(#sand-grad)" transform="rotate(10 270 530)" />
          <ellipse cx="270" cy="527" rx="58" ry="35" fill="url(#island-sub)" transform="rotate(10 270 527)" />
        </g>

        <!-- ⑤ 타베우니 섬 (극동 끝) -->
        <g filter="url(#island-shadow)">
          <ellipse cx="850" cy="180" rx="40" ry="25" fill="url(#sand-grad)" transform="rotate(35 850 180)" />
          <ellipse cx="850" cy="178" rx="35" ry="21" fill="url(#island-sub)" transform="rotate(35 850 178)" />
        </g>

        <!-- ⑥ 라우 군도 (동쪽 흩어진 산호섬들) -->
        <g filter="url(#island-shadow)">
          <circle cx="890" cy="330" r="22" fill="url(#sand-grad)" />
          <circle cx="890" cy="328" r="18" fill="url(#island-sub)" />
          
          <circle cx="830" cy="420" r="18" fill="url(#sand-grad)" />
          <circle cx="830" cy="418" r="15" fill="url(#island-sub)" />

          <circle cx="910" cy="450" r="14" fill="url(#sand-grad)" />
          <circle cx="910" cy="448" r="11" fill="url(#island-sub)" />
        </g>

        <!-- 아기자기한 미니 돛단배 데코레이션 -->
        <g class="boat-ani" transform="translate(680, 310)">
          <path d="M 0 10 L 20 10 L 25 15 L -5 15 Z" fill="#b45309" />
          <polygon points="10,10 10,0 18,5" fill="#f8fafc" />
        </g>

        <!-- 구름 데코레이션 (좌에서 우로 흘러가는 효과) -->
        <g class="cloud-ani-1" opacity="0.8">
          <path d="M 0 0 A 15 15 0 0 1 30 0 A 20 20 0 0 1 70 0 A 15 15 0 0 1 90 0 L 90 10 L 0 10 Z" fill="white" transform="translate(200, 60)" />
        </g>
        <g class="cloud-ani-2" opacity="0.75">
          <path d="M 0 0 A 12 12 0 0 1 24 0 A 18 18 0 0 1 60 0 A 12 12 0 0 1 78 0 L 78 8 L 0 8 Z" fill="white" transform="translate(600, 480)" />
        </g>


        <!-- ============================================================ -->
        <!-- 🔗 Connector Line (선택된 핀과 정보창을 잇는 지시선) -->
        <!-- ============================================================ -->
        <path id="map-connector-line" d="" stroke="#4f46e5" stroke-width="2.5" stroke-dasharray="6,4" fill="none" opacity="0" style="transition: opacity 0.3s ease, d 0.3s ease;" />


        <!-- ============================================================ -->
        <!-- 📍 가맹점 핀 노드들 -->
        <!-- ============================================================ -->

        <!-- ① 🏨 호텔 — 야사와 군도 (NW) -->
        <g class="map-pin-group filter-node" id="pin-healing-hotel" data-cat="hotel" onclick="selectIslandShop('healing-hotel')" style="cursor:pointer;" filter="url(#pf)">
          <circle cx="130" cy="150" r="22" fill="#4f46e5" class="pin-circle-outer"/>
          <circle cx="130" cy="150" r="22" fill="none" stroke="white" stroke-width="2.5"/>
          <rect x="122" y="142" width="16" height="13" rx="1.5" fill="none" stroke="white" stroke-width="1.8"/>
          <line x1="126" y1="142" x2="126" y2="155" stroke="white" stroke-width="1.1"/>
          <line x1="130" y1="142" x2="130" y2="155" stroke="white" stroke-width="1.1"/>
          <line x1="134" y1="142" x2="134" y2="155" stroke="white" stroke-width="1.1"/>
          <line x1="122"  y1="148" x2="138" y2="148" stroke="white" stroke-width="1.1"/>
          <polygon points="130,168 124,157 136,157" fill="#4f46e5"/>
          <!-- 미세 펄스 링 -->
          <circle cx="130" cy="150" r="22" fill="none" stroke="white" stroke-width="2.5" class="pin-pulse-ring"/>
        </g>

        <!-- ② 🛁 스파 — 비티레부 서부 Nadi (W) -->
        <g class="map-pin-group filter-node" id="pin-healing-spa" data-cat="spa" onclick="selectIslandShop('healing-spa')" style="cursor:pointer;" filter="url(#pf)">
          <circle cx="200" cy="310" r="22" fill="#0d9488" class="pin-circle-outer"/>
          <circle cx="200" cy="310" r="22" fill="none" stroke="white" stroke-width="2.5"/>
          <path d="M191 306 Q196 302 200 306 Q204 310 209 306" fill="none" stroke="white" stroke-width="2.2" stroke-linecap="round"/>
          <path d="M191 312 Q196 308 200 312 Q204 316 209 312" fill="none" stroke="white" stroke-width="2.2" stroke-linecap="round"/>
          <path d="M191 318 Q196 314 200 318 Q204 322 209 318" fill="none" stroke="white" stroke-width="2.2" stroke-linecap="round"/>
          <polygon points="200,328 194,317 206,317" fill="#0d9488"/>
          <circle cx="200" cy="310" r="22" fill="none" stroke="white" stroke-width="2.5" class="pin-pulse-ring"/>
        </g>

        <!-- ③ 🏕️ 캠핑 — 비티레부 동부 Suva (SE) -->
        <g class="map-pin-group filter-node" id="pin-healing-camping" data-cat="spa" onclick="selectIslandShop('healing-camping')" style="cursor:pointer;" filter="url(#pf)">
          <circle cx="390" cy="360" r="22" fill="#15803d" class="pin-circle-outer"/>
          <circle cx="390" cy="360" r="22" fill="none" stroke="white" stroke-width="2.5"/>
          <polygon points="390,348 378,368 402,368" fill="none" stroke="white" stroke-width="2" stroke-linejoin="round"/>
          <line x1="390" y1="348" x2="390" y2="368" stroke="white" stroke-width="1.5"/>
          <polygon points="390,360 396,368 402,368" fill="white" opacity="0.35"/>
          <polygon points="390,378 384,367 396,367" fill="#15803d"/>
          <circle cx="390" cy="360" r="22" fill="none" stroke="white" stroke-width="2.5" class="pin-pulse-ring"/>
        </g>

        <!-- ④ 🌸 테라피 — 바누아레부 서쪽 (N-C) -->
        <g class="map-pin-group filter-node" id="pin-healing-therapy" data-cat="medical" onclick="selectIslandShop('healing-therapy')" style="cursor:pointer;" filter="url(#pf)">
          <circle cx="550" cy="180" r="22" fill="#e11d48" class="pin-circle-outer"/>
          <circle cx="550" cy="180" r="22" fill="none" stroke="white" stroke-width="2.5"/>
          <line x1="550" y1="171" x2="550" y2="189" stroke="white" stroke-width="2.5" stroke-linecap="round"/>
          <line x1="541" y1="180" x2="559" y2="180" stroke="white" stroke-width="2.5" stroke-linecap="round"/>
          <polygon points="550,198 544,187 556,187" fill="#e11d48"/>
          <circle cx="550" cy="180" r="22" fill="none" stroke="white" stroke-width="2.5" class="pin-pulse-ring"/>
        </g>

        <!-- ⑤ 🛒 쇼핑 — 바누아레부 동쪽 (N-E) -->
        <g class="map-pin-group filter-node" id="pin-healing-shopping" data-cat="shop" onclick="selectIslandShop('healing-shopping')" style="cursor:pointer;" filter="url(#pf)">
          <circle cx="680" cy="170" r="22" fill="#d97706" class="pin-circle-outer"/>
          <circle cx="680" cy="170" r="22" fill="none" stroke="white" stroke-width="2.5"/>
          <path d="M670 163 L673 175 L687 175 L689 163" fill="none" stroke="white" stroke-width="2" stroke-linejoin="round"/>
          <circle cx="675" cy="179" r="2" fill="white"/>
          <circle cx="685" cy="179" r="2" fill="white"/>
          <line x1="667" y1="163" x2="670" y2="163" stroke="white" stroke-width="2" stroke-linecap="round"/>
          <polygon points="680,188 674,177 686,177" fill="#d97706"/>
          <circle cx="680" cy="170" r="22" fill="none" stroke="white" stroke-width="2.5" class="pin-pulse-ring"/>
        </g>

        <!-- ⑥ 🏥 병원 — 라우 군도 중앙 (E) -->
        <g class="map-pin-group filter-node" id="pin-healing-hospital" data-cat="medical" onclick="selectIslandShop('healing-hospital')" style="cursor:pointer;" filter="url(#pf)">
          <circle cx="890" cy="330" r="22" fill="#dc2626" class="pin-circle-outer"/>
          <circle cx="890" cy="330" r="22" fill="none" stroke="white" stroke-width="2.5"/>
          <line x1="882" y1="322" x2="882" y2="338" stroke="white" stroke-width="3" stroke-linecap="round"/>
          <line x1="898" y1="322" x2="898" y2="338" stroke="white" stroke-width="3" stroke-linecap="round"/>
          <line x1="882" y1="330" x2="898" y2="330" stroke="white" stroke-width="3" stroke-linecap="round"/>
          <polygon points="890,347 884,336 896,336" fill="#dc2626"/>
          <circle cx="890" cy="330" r="22" fill="none" stroke="white" stroke-width="2.5" class="pin-pulse-ring"/>
        </g>

        <!-- ============================================================ -->
        <!-- 💬 Callout Bubble (말풍선 팝업) -->
        <!-- ============================================================ -->
        <g id="map-callout" transform="translate(0,0)" style="display:none; pointer-events: none; transition: transform 0.3s ease;">
          <rect x="-70" y="-60" width="140" height="34" rx="10" fill="white" stroke="#4f46e5" stroke-width="2.2" filter="url(#pin-shadow)"/>
          <polygon points="0,-26 -8,-17 8,-17" fill="white" stroke="#4f46e5" stroke-width="2.2"/>
          <polygon points="-7,-18 7,-18 0,-25" fill="white"/>
          <text x="0" y="-38" text-anchor="middle" font-size="11" font-weight="900" fill="#1e293b" id="map-callout-text">가맹점명</text>
        </g>

        <!-- 나침반 (우하단) -->
        <g transform="translate(940, 590)" filter="url(#pin-shadow)">
          <circle r="25" fill="white" opacity="0.9"/>
          <circle r="22" fill="none" stroke="#e2e8f0" stroke-width="2"/>
          <text y="7" text-anchor="middle" font-size="24">🧭</text>
        </g>
      </svg>
      -->
    </div>

    <!-- 우측: 가맹점 상세 퀘스트 정보창 패널 -->
    <div class="iw-panel iw-right-panel" id="shop-quest-panel">
      <!-- 기본 상태 -->
      <div id="quest-default-state">
        <p class="panel-section-label">가맹점 상세 정보</p>
        <div class="quest-empty">
          <span style="font-size:2.8rem">🗺️</span>
          <p>지도상의 <strong>가맹점 핀</strong>을<br>선택하여 새로운 상생 혜택<br>퀘스트 정보를 탐험하세요!</p>
        </div>
      </div>

      <!-- 선택된 가맹점 상태 -->
      <div id="quest-active-state" class="hidden">
        <div class="quest-header">
          <span id="quest-emoji" style="font-size:2.2rem">🛁</span>
          <div>
            <span class="quest-badge-label" id="quest-badge">SPA</span>
            <h3 class="quest-title" id="quest-title">힐링 스파</h3>
          </div>
        </div>
        <div class="quest-desc-box">
          <p id="quest-desc" style="margin:0;">가맹점 퀘스트 내용이 여기에 표시됩니다.</p>
        </div>
        <div class="quest-reward-box">
          <span class="reward-icon">🎁</span>
          <div>
            <p class="reward-label" id="quest-reward-badge">상생 혜택</p>
            <p class="reward-text" id="quest-reward-name">-</p>
          </div>
        </div>
        <div class="quest-actions">
          <button onclick="openActiveQuestLink()" class="btn-primary">예약하러 가기 →</button>
          <button onclick="closeQuestPanel()" class="btn-secondary">← 전체 지도</button>
        </div>
      </div>
    </div>

  </div>

  <!-- 가맹점 접이식 상세 섹션 (shop.js의 toggleShopSection과 매핑됨) -->
  <div id="sections-container" class="space-y-4">
      <!-- 펫 스파 & 미용 -->
      <div id="section-healing-spa" class="hidden card-modern p-5 space-y-3">
          <div class="flex items-center justify-between border-b pb-3">
              <div class="flex items-center gap-2">
                  <span class="text-3xl">🛁</span>
                  <h3 class="font-black text-gray-800 text-base">포레스트 힐 펫 스파</h3>
              </div>
              <button onclick="toggleShopSection('healing-spa'); closeQuestPanel();" class="text-gray-400 hover:text-gray-600">
                  <i class="fa-solid fa-xmark text-xl"></i>
              </button>
          </div>
          <div class="space-y-2">
              <a href="https://banjjakpet.com/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                  class="flex items-center justify-between p-3 rounded-xl bg-emerald-50 hover:bg-emerald-100 transition-colors">
                  <div>
                      <span class="block text-xs font-black text-emerald-800">반짝 펫 예약하기</span>
                      <span class="text-[10px] text-gray-400">지역별 검증된 그루머 스파 예약 플랫폼</span>
                  </div>
                  <i class="fa-solid fa-arrow-up-right-from-square text-emerald-400 text-xs"></i>
              </a>
          </div>
      </div>

      <!-- 반려동물 동반 캠핑 -->
      <div id="section-healing-camping" class="hidden card-modern p-5 space-y-3">
          <div class="flex items-center justify-between border-b pb-3">
              <div class="flex items-center gap-2">
                  <span class="text-3xl">🏕️</span>
                  <h3 class="font-black text-gray-800 text-base">도그빌 오션 캠핑장</h3>
              </div>
              <button onclick="toggleShopSection('healing-camping'); closeQuestPanel();" class="text-gray-400 hover:text-gray-600">
                  <i class="fa-solid fa-xmark text-xl"></i>
              </button>
          </div>
          <div class="space-y-2">
              <a href="https://www.camfit.co.kr/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                  class="flex items-center justify-between p-3 rounded-xl bg-green-50 hover:bg-green-100 transition-colors">
                  <div>
                      <span class="block text-xs font-black text-green-800">캠핏 예약하기</span>
                      <span class="text-[10px] text-gray-400">반려동물과 동반 가능한 자연 속 힐링 캠핑장</span>
                  </div>
                  <i class="fa-solid fa-arrow-up-right-from-square text-green-400 text-xs"></i>
              </a>
          </div>
      </div>

      <!-- 펫 마사지 & 테라피 -->
      <div id="section-healing-therapy" class="hidden card-modern p-5 space-y-3">
          <div class="flex items-center justify-between border-b pb-3">
              <div class="flex items-center gap-2">
                  <span class="text-3xl">🌸</span>
                  <h3 class="font-black text-gray-800 text-base">아로마 펫 테라피 살롱</h3>
              </div>
              <button onclick="toggleShopSection('healing-therapy'); closeQuestPanel();" class="text-gray-400 hover:text-gray-600">
                  <i class="fa-solid fa-xmark text-xl"></i>
              </button>
          </div>
          <div class="space-y-2">
              <a href="https://healschool.co.kr/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                  class="flex items-center justify-between p-3 rounded-xl bg-pink-50 hover:bg-pink-100 transition-colors">
                  <div>
                      <span class="block text-xs font-black text-pink-800">더힐테라피센터 이동</span>
                      <span class="text-[10px] text-gray-400">사주맞춤형 아로마 오일 도포 및 관절 케어 프로그램</span>
                  </div>
                  <i class="fa-solid fa-arrow-up-right-from-square text-pink-400 text-xs"></i>
              </a>
          </div>
      </div>

      <!-- 펫호텔 (위탁) -->
      <div id="section-healing-hotel" class="hidden card-modern p-5 space-y-3">
          <div class="flex items-center justify-between border-b pb-3">
              <div class="flex items-center gap-2">
                  <span class="text-3xl">🏨</span>
                  <h3 class="font-black text-gray-800 text-base">가든 테라스 펫 리조트</h3>
              </div>
              <button onclick="toggleShopSection('healing-hotel'); closeQuestPanel();" class="text-gray-400 hover:text-gray-600">
                  <i class="fa-solid fa-xmark text-xl"></i>
              </button>
          </div>
          <div class="space-y-2">
              <a href="https://www.petliz.co.kr/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                  class="flex items-center justify-between p-3 rounded-xl bg-indigo-50 hover:bg-indigo-100 transition-colors">
                  <div>
                      <span class="block text-xs font-black text-indigo-800">펫리즈 호텔 예약</span>
                      <span class="text-[10px] text-gray-400">개별 테라스와 24시 안심 CCTV 케어 연계 서비스</span>
                  </div>
                  <i class="fa-solid fa-arrow-up-right-from-square text-indigo-400 text-xs"></i>
              </a>
          </div>
      </div>

      <!-- 동물병원 픽업·동행 -->
      <div id="section-healing-hospital" class="hidden card-modern p-5 space-y-3">
          <div class="flex items-center justify-between border-b pb-3">
              <div class="flex items-center gap-2">
                  <span class="text-3xl">🏥</span>
                  <h3 class="font-black text-gray-800 text-base">24시 센트럴 메디컬 센터</h3>
              </div>
              <button onclick="toggleShopSection('healing-hospital'); closeQuestPanel();" class="text-gray-400 hover:text-gray-600">
                  <i class="fa-solid fa-xmark text-xl"></i>
              </button>
          </div>
          <div class="space-y-2">
              <a href="https://www.petmeup.co.kr/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                  class="flex items-center justify-between p-3 rounded-xl bg-red-50 hover:bg-red-100 transition-colors">
                  <div>
                      <span class="block text-xs font-black text-red-800">펫미업 예약하기</span>
                      <span class="text-[10px] text-gray-400">24시 응급 진료 및 안심 픽업 동행 서비스</span>
                  </div>
                  <i class="fa-solid fa-arrow-up-right-from-square text-red-400 text-xs"></i>
              </a>
          </div>
      </div>

      <!-- 프리미엄 멀티샵 -->
      <div id="section-healing-shopping" class="hidden card-modern p-5 space-y-3">
          <div class="flex items-center justify-between border-b pb-3">
              <div class="flex items-center gap-2">
                  <span class="text-3xl">🛒</span>
                  <h3 class="font-black text-gray-800 text-base">펫라이프 프리미엄 멀티샵</h3>
              </div>
              <button onclick="toggleShopSection('healing-shopping'); closeQuestPanel();" class="text-gray-400 hover:text-gray-600">
                  <i class="fa-solid fa-xmark text-xl"></i>
              </button>
          </div>
          <div class="space-y-2">
              <a href="#" onclick="switchTab('shop'); showToast('스토어 탭으로 연결합니다.'); return false;"
                  class="flex items-center justify-between p-3 rounded-xl bg-amber-50 hover:bg-amber-100 transition-colors">
                  <div>
                      <span class="block text-xs font-black text-amber-800">프리미엄 멀티샵 즉시 구경하기</span>
                      <span class="text-[10px] text-gray-400">유기농 홀리스틱 사료 및 명품 프리미엄 의류 플래그십</span>
                  </div>
                  <i class="fa-solid fa-arrow-up-right-from-square text-amber-400 text-xs"></i>
              </a>
          </div>
      </div>
  </div>

</div>

<style>
/* ===== 전체 래퍼 ===== */
.island-world-wrap {
  font-family: -apple-system, 'Apple SD Gothic Neo', sans-serif;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

/* ===== 헤더 ===== */
.iw-header {
  display: flex;
  align-items: center;
  gap: 16px;
  background: linear-gradient(135deg, #f0fdf4, #dcfce7);
  border: 2px solid #86efac;
  border-radius: 24px;
  padding: 16px 24px;
  box-shadow: 0 4px 15px rgba(34,197,94,0.06);
}
.iw-header-icon {
  font-size: 2.5rem;
  animation: spin-slow 25s linear infinite;
}
@keyframes spin-slow { to { transform: rotate(360deg); } }
.iw-title {
  font-size: 1.3rem;
  font-weight: 950;
  color: #14532d;
  margin: 0;
  letter-spacing: -0.02em;
}
.iw-subtitle {
  font-size: 0.8rem;
  color: #166534;
  margin: 3px 0 0;
}
.iw-badge {
  margin-left: auto;
  background: #22c55e;
  color: white;
  font-size: 0.7rem;
  font-weight: 900;
  padding: 5px 14px;
  border-radius: 999px;
  white-space: nowrap;
  letter-spacing: 0.05em;
  box-shadow: 0 2px 8px rgba(34,197,94,0.3);
}

/* ===== 3-Col 레이아웃 ===== */
.iw-layout {
  display: grid;
  grid-template-columns: 210px 1fr 240px;
  gap: 16px;
  align-items: stretch;
  min-height: 520px;
}
@media (max-width: 960px) {
  .iw-layout { grid-template-columns: 1fr; }
}

/* ===== 공통 패널 ===== */
.iw-panel {
  background: white;
  border: 1.5px solid #e2e8f0;
  border-radius: 24px;
  padding: 18px;
  display: flex;
  flex-direction: column;
  gap: 14px;
  box-shadow: 0 4px 12px rgba(148,163,184,0.04);
}
.panel-section-label {
  font-size: 0.7rem;
  font-weight: 850;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: #64748b;
  margin: 0;
}
.panel-divider {
  border: none;
  border-top: 1.5px solid #f1f5f9;
  margin: 4px 0;
}

/* ===== 필터 리스트 ===== */
.filter-list { display: flex; flex-direction: column; gap: 6px; }
.filter-item {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 0.78rem;
  font-weight: 700;
  color: #334155;
  cursor: pointer;
  padding: 9px 12px;
  border-radius: 14px;
  transition: all 0.25s ease;
  border: 1.5px solid transparent;
}
.filter-item:hover { background: #f8fafc; border-color: #cbd5e1; }
.filter-item input { width: 16px; height: 16px; accent-color: #22c55e; cursor: pointer; }
.filter-dot { width: 10px; height: 10px; border-radius: 50%; flex-shrink: 0; }
.filter-count {
  margin-left: auto;
  background: #f1f5f9;
  color: #64748b;
  font-size: 0.65rem;
  font-weight: 800;
  padding: 2px 7px;
  border-radius: 99px;
}

/* 활성 통계 바 */
.active-stat { display: flex; flex-direction: column; gap: 8px; }
.stat-bar-bg { height: 8px; background: #f1f5f9; border-radius: 99px; overflow: hidden; }
.stat-bar-fill { height: 100%; background: linear-gradient(90deg, #4ade80, #22c55e); border-radius: 99px; transition: width 0.4s ease; width: 100%; }
.stat-text { font-size: 0.7rem; font-weight: 800; color: #166534; }

/* ===== 지도 컨테이너 ===== */
.iw-map-container {
  background: #bae6fd;
  border-radius: 28px;
  overflow: hidden;
  border: 3px solid #7dd3fc;
  box-shadow: inset 0 0 50px rgba(14,165,233,0.15), 0 10px 30px rgba(14,165,233,0.08);
  min-height: 450px;
  position: relative;
}

/* 지도 컨트롤 버튼 */
.map-controls {
  position: absolute;
  top: 16px;
  right: 16px;
  z-index: 1000;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.map-control-btn {
  width: 40px;
  height: 40px;
  background: white;
  border: 2px solid #e2e8f0;
  border-radius: 12px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 16px;
  color: #64748b;
  transition: all 0.2s ease;
  box-shadow: 0 2px 8px rgba(0,0,0,0.08);
}

.map-control-btn:hover {
  background: #f8fafc;
  border-color: #22c55e;
  color: #22c55e;
  transform: scale(1.05);
}

/* SVG 핀 인터랙션 */
.map-pin-group { transition: transform 0.28s cubic-bezier(0.175, 0.885, 0.32, 1.275); transform-origin: bottom center; }
.map-pin-group:hover { transform: scale(1.3) translateY(-6px); }

/* 미세 펄스 애니메이션 */
.pin-pulse-ring {
  animation: pin-pulse 2s infinite ease-out;
  transform-origin: center;
}
@keyframes pin-pulse {
  0% { r: 20; opacity: 0.9; stroke-width: 2.5; }
  100% { r: 35; opacity: 0; stroke-width: 0.5; }
}

/* 바다 배경 파도 애니메이션 */
.wave-ani-1 { animation: wave-drift 8s infinite ease-in-out; }
.wave-ani-2 { animation: wave-drift 10s infinite ease-in-out; animation-delay: 1.5s; }
.wave-ani-3 { animation: wave-drift 12s infinite ease-in-out; animation-delay: 3s; }
@keyframes wave-drift {
  0%, 100% { transform: translate(0, 0); }
  50% { transform: translate(15px, -5px); }
}

/* 돛단배 움직임 */
.boat-ani { animation: boat-sail 25s infinite linear; }
@keyframes boat-sail {
  0% { transform: translate(680px, 310px); }
  50% { transform: translate(500px, 340px) scaleX(-1); }
  100% { transform: translate(680px, 310px); }
}

/* 구름 움직임 */
.cloud-ani-1 { animation: cloud-float 90s infinite linear; }
.cloud-ani-2 { animation: cloud-float 120s infinite linear; animation-delay: -30s; }
@keyframes cloud-float {
  0% { transform: translate(-150px, 0); }
  100% { transform: translate(1100px, 0); }
}

/* ===== 우측 퀘스트 패널 ===== */
.quest-empty {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  gap: 12px;
  padding: 30px 10px;
  color: #94a3b8;
  font-size: 0.8rem;
  line-height: 1.7;
}
.quest-header {
  display: flex;
  align-items: center;
  gap: 14px;
  padding-bottom: 14px;
  border-bottom: 2px solid #f1f5f9;
}
.quest-badge-label {
  display: inline-block;
  font-size: 0.65rem;
  font-weight: 900;
  letter-spacing: 0.12em;
  color: #4f46e5;
  background: #eef2ff;
  border: 1.5px solid #c7d2fe;
  padding: 3px 9px;
  border-radius: 8px;
  text-transform: uppercase;
}
.quest-title {
  font-size: 1.05rem;
  font-weight: 950;
  color: #0f172a;
  margin: 4px 0 0;
}
.quest-desc-box {
  background: #f8fafc;
  border: 1.5px solid #f1f5f9;
  border-radius: 16px;
  padding: 14px;
  font-size: 0.8rem;
  color: #475569;
  line-height: 1.7;
}
.quest-reward-box {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  background: #fffbeb;
  border: 1.5px solid #fef08a;
  border-radius: 16px;
  padding: 14px;
}
.reward-icon { font-size: 1.5rem; flex-shrink: 0; }
.reward-label { font-size: 0.65rem; font-weight: 850; color: #b45309; text-transform: uppercase; letter-spacing: 0.05em; margin: 0; }
.reward-text { font-size: 0.8rem; font-weight: 800; color: #78350f; margin: 4px 0 0; line-height: 1.5; }
.quest-actions { display: flex; flex-direction: column; gap: 8px; margin-top: auto; }
.btn-primary {
  width: 100%;
  background: linear-gradient(135deg, #059669, #047857);
  color: white;
  font-size: 0.8rem;
  font-weight: 900;
  padding: 12px;
  border-radius: 14px;
  border: none;
  cursor: pointer;
  transition: all 0.2s ease;
  box-shadow: 0 4px 12px rgba(4,120,87,0.15);
}
.btn-primary:hover { opacity: 0.92; transform: translateY(-2px); box-shadow: 0 6px 16px rgba(4,120,87,0.22); }
.btn-secondary {
  width: 100%;
  background: #f8fafc;
  color: #64748b;
  font-size: 0.75rem;
  font-weight: 800;
  padding: 9px;
  border-radius: 12px;
  border: 1.5px solid #e2e8f0;
  cursor: pointer;
  transition: all 0.2s ease;
}
.btn-secondary:hover { background: #cbd5e1; color: #1e293b; }

/* 현대적인 상세 카드 디자인 */
.card-modern {
  background: white;
  border: 1.5px solid #e2e8f0;
  border-radius: 24px;
  box-shadow: 0 4px 12px rgba(148,163,184,0.05);
}
</style>
`;
