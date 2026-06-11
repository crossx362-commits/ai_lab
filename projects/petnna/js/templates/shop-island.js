// 펫 라이프 — 캐주얼 다도해 SVG 월드맵 템플릿

const SHOP_ISLAND_TEMPLATE = `
<div class="island-world-wrap">

  <!-- 헤더 -->
  <div class="iw-header">
    <div class="iw-header-icon">🌏</div>
    <div>
      <h1 class="iw-title">펫 라이프 아일랜드</h1>
      <p class="iw-subtitle">섬을 눌러 가맹점을 탐험하세요</p>
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
          <span class="filter-dot" style="background:#5eead4"></span>
          <span>힐링 & 스파</span>
          <span class="filter-count">2</span>
        </label>
        <label class="filter-item">
          <input type="checkbox" checked id="f-medical" onchange="applyMapFilters()">
          <span class="filter-dot" style="background:#fb7185"></span>
          <span>메디컬 케어</span>
          <span class="filter-count">2</span>
        </label>
        <label class="filter-item">
          <input type="checkbox" checked id="f-hotel" onchange="applyMapFilters()">
          <span class="filter-dot" style="background:#818cf8"></span>
          <span>스테이 & 돌봄</span>
          <span class="filter-count">1</span>
        </label>
        <label class="filter-item">
          <input type="checkbox" checked id="f-shop" onchange="applyMapFilters()">
          <span class="filter-dot" style="background:#fbbf24"></span>
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

    <!-- 중앙: 캐주얼 SVG 다도해 지도 -->
    <div class="iw-map-container">
      <svg id="petlife-map" viewBox="0 0 800 550" xmlns="http://www.w3.org/2000/svg" style="width:100%;height:100%;">
        <defs>
          <!-- 바다 그라디언트 -->
          <linearGradient id="sea-grad" x1="0%" y1="0%" x2="0%" y2="100%">
            <stop offset="0%" stop-color="#bae6fd"/>
            <stop offset="100%" stop-color="#7dd3fc"/>
          </linearGradient>
          <!-- 섬 그라디언트들 -->
          <radialGradient id="island-main" cx="50%" cy="40%" r="60%">
            <stop offset="0%" stop-color="#d1fae5"/>
            <stop offset="60%" stop-color="#a7f3d0"/>
            <stop offset="100%" stop-color="#86efac"/>
          </radialGradient>
          <radialGradient id="island-sm1" cx="50%" cy="40%" r="60%">
            <stop offset="0%" stop-color="#d1fae5"/>
            <stop offset="100%" stop-color="#6ee7b7"/>
          </radialGradient>
          <filter id="island-shadow" x="-10%" y="-10%" width="120%" height="130%">
            <feDropShadow dx="0" dy="4" stdDeviation="6" flood-color="#0369a1" flood-opacity="0.18"/>
          </filter>
          <filter id="pin-shadow" x="-30%" y="-30%" width="160%" height="200%">
            <feDropShadow dx="0" dy="3" stdDeviation="3" flood-color="#000" flood-opacity="0.25"/>
          </filter>
          <!-- 파도 패턴 -->
          <pattern id="wave-pat" x="0" y="0" width="60" height="30" patternUnits="userSpaceOnUse">
            <path d="M0 15 Q15 5 30 15 Q45 25 60 15" fill="none" stroke="white" stroke-width="1" opacity="0.35"/>
          </pattern>
        </defs>

        <!-- 바다 배경 -->
        <rect width="800" height="550" fill="url(#sea-grad)"/>
        <!-- 파도 패턴 -->
        <rect width="800" height="550" fill="url(#wave-pat)" opacity="0.6"/>

        <!-- === 섬들 (피지 스타일 다도해, 텍스트 없음) === -->

        <!-- 🏝 메인 큰 섬 (중앙 좌측 / 피지 비티레부 느낌) -->
        <g filter="url(#island-shadow)">
          <ellipse cx="240" cy="290" rx="160" ry="105" fill="url(#island-main)" stroke="#6ee7b7" stroke-width="2.5"/>
          <!-- 내부 언덕/질감 -->
          <ellipse cx="220" cy="275" rx="80" ry="55" fill="#a7f3d0" opacity="0.5"/>
          <ellipse cx="265" cy="305" rx="55" ry="35" fill="#a7f3d0" opacity="0.3"/>
          <!-- 해변 라인 -->
          <ellipse cx="240" cy="290" rx="162" ry="107" fill="none" stroke="#fef08a" stroke-width="5" opacity="0.45"/>
        </g>

        <!-- 🏝 북동쪽 큰 섬 (바누아레부 느낌) -->
        <g filter="url(#island-shadow)">
          <path d="M430 90 Q480 70 540 85 Q590 95 610 120 Q625 145 600 165 Q560 180 510 175 Q460 170 435 150 Q415 130 430 90Z" fill="url(#island-sm1)" stroke="#6ee7b7" stroke-width="2"/>
          <path d="M430 90 Q480 70 540 85 Q590 95 610 120 Q625 145 600 165 Q560 180 510 175 Q460 170 435 150 Q415 130 430 90Z" fill="none" stroke="#fef08a" stroke-width="4" opacity="0.4"/>
        </g>

        <!-- 🏝 소규모 섬들 (서쪽) -->
        <g filter="url(#island-shadow)">
          <ellipse cx="90" cy="200" rx="55" ry="35" fill="#a7f3d0" stroke="#6ee7b7" stroke-width="1.5"/>
          <ellipse cx="90" cy="200" rx="56" ry="36" fill="none" stroke="#fef08a" stroke-width="3.5" opacity="0.4"/>
        </g>

        <!-- 🏝 동남 섬 (모알라 느낌) -->
        <g filter="url(#island-shadow)">
          <ellipse cx="560" cy="390" rx="75" ry="50" fill="#bbf7d0" stroke="#6ee7b7" stroke-width="2"/>
          <ellipse cx="560" cy="390" rx="76" ry="51" fill="none" stroke="#fef08a" stroke-width="4" opacity="0.4"/>
        </g>

        <!-- 🏝 남서 소섬 (카다부 느낌) -->
        <g filter="url(#island-shadow)">
          <ellipse cx="310" cy="455" rx="50" ry="32" fill="#a7f3d0" stroke="#6ee7b7" stroke-width="1.5"/>
          <ellipse cx="310" cy="455" rx="51" ry="33" fill="none" stroke="#fef08a" stroke-width="3.5" opacity="0.4"/>
        </g>

        <!-- 🏝 동쪽 소섬군 (라우 군도 느낌) -->
        <g filter="url(#island-shadow)">
          <ellipse cx="690" cy="250" rx="38" ry="25" fill="#d1fae5" stroke="#6ee7b7" stroke-width="1.5"/>
        </g>
        <g filter="url(#island-shadow)">
          <ellipse cx="710" cy="330" rx="28" ry="18" fill="#d1fae5" stroke="#6ee7b7" stroke-width="1.5"/>
        </g>
        <g filter="url(#island-shadow)">
          <ellipse cx="645" cy="170" rx="22" ry="14" fill="#d1fae5" stroke="#6ee7b7" stroke-width="1.5"/>
        </g>

        <!-- 미니 점섬들 -->
        <circle cx="175" cy="145" r="10" fill="#bbf7d0" stroke="#6ee7b7" stroke-width="1.5"/>
        <circle cx="395" cy="380" r="8" fill="#bbf7d0" stroke="#6ee7b7" stroke-width="1.5"/>
        <circle cx="490" cy="460" r="12" fill="#bbf7d0" stroke="#6ee7b7" stroke-width="1.5"/>
        <circle cx="740" cy="410" r="9" fill="#d1fae5" stroke="#6ee7b7" stroke-width="1.5"/>

        <!-- 파도 장식선 (배경에 생기 부여) -->
        <text x="60" y="400" font-size="20" opacity="0.25" fill="#0369a1">〰</text>
        <text x="680" y="480" font-size="20" opacity="0.2" fill="#0369a1">〰</text>
        <text x="370" y="250" font-size="16" opacity="0.2" fill="#0369a1">〰</text>

        <!-- ============================================================ -->
        <!-- 📍 펫샵 핀 노드들 (각 섬 위에 배치) -->
        <!-- filter-node: data-cat 속성으로 필터링 -->
        <!-- ============================================================ -->

        <!-- 🛁 스파 — 메인 섬 좌상 -->
        <g class="map-pin-group filter-node" data-cat="spa" onclick="selectIslandShop('healing-spa')" style="cursor:pointer;">
          <circle cx="190" cy="240" r="22" fill="#0d9488" filter="url(#pin-shadow)" class="pin-circle"/>
          <text x="190" y="248" text-anchor="middle" font-size="18">🛁</text>
          <!-- 핀 포인터 삼각형 -->
          <polygon points="190,266 184,256 196,256" fill="#0d9488"/>
          <!-- 반짝임 링 (hover 효과) -->
          <circle cx="190" cy="240" r="22" fill="none" stroke="white" stroke-width="2.5" opacity="0.7" class="pin-ring"/>
        </g>

        <!-- 🏕️ 캠핑 — 메인 섬 우측 -->
        <g class="map-pin-group filter-node" data-cat="spa" onclick="selectIslandShop('healing-camping')" style="cursor:pointer;">
          <circle cx="310" cy="270" r="22" fill="#16a34a" filter="url(#pin-shadow)" class="pin-circle"/>
          <text x="310" y="278" text-anchor="middle" font-size="18">🏕️</text>
          <polygon points="310,292 304,282 316,282" fill="#16a34a"/>
          <circle cx="310" cy="270" r="22" fill="none" stroke="white" stroke-width="2.5" opacity="0.7" class="pin-ring"/>
        </g>

        <!-- 🌸 테라피 — 북동 섬 -->
        <g class="map-pin-group filter-node" data-cat="medical" onclick="selectIslandShop('healing-therapy')" style="cursor:pointer;">
          <circle cx="520" cy="125" r="22" fill="#e11d48" filter="url(#pin-shadow)" class="pin-circle"/>
          <text x="520" y="133" text-anchor="middle" font-size="18">🌸</text>
          <polygon points="520,147 514,137 526,137" fill="#e11d48"/>
          <circle cx="520" cy="125" r="22" fill="none" stroke="white" stroke-width="2.5" opacity="0.7" class="pin-ring"/>
        </g>

        <!-- 🏥 병원 — 동남 섬 -->
        <g class="map-pin-group filter-node" data-cat="medical" onclick="selectIslandShop('healing-hospital')" style="cursor:pointer;">
          <circle cx="560" cy="370" r="22" fill="#dc2626" filter="url(#pin-shadow)" class="pin-circle"/>
          <text x="560" y="378" text-anchor="middle" font-size="18">🏥</text>
          <polygon points="560,392 554,382 566,382" fill="#dc2626"/>
          <circle cx="560" cy="370" r="22" fill="none" stroke="white" stroke-width="2.5" opacity="0.7" class="pin-ring"/>
        </g>

        <!-- 🏨 호텔 — 서쪽 소섬 -->
        <g class="map-pin-group filter-node" data-cat="hotel" onclick="selectIslandShop('healing-hotel')" style="cursor:pointer;">
          <circle cx="90" cy="188" r="22" fill="#4f46e5" filter="url(#pin-shadow)" class="pin-circle"/>
          <text x="90" y="196" text-anchor="middle" font-size="18">🏨</text>
          <polygon points="90,210 84,200 96,200" fill="#4f46e5"/>
          <circle cx="90" cy="188" r="22" fill="none" stroke="white" stroke-width="2.5" opacity="0.7" class="pin-ring"/>
        </g>

        <!-- 🛒 쇼핑 — 남서 소섬 -->
        <g class="map-pin-group filter-node" data-cat="shop" onclick="selectIslandShop('healing-shopping')" style="cursor:pointer;">
          <circle cx="310" cy="443" r="22" fill="#d97706" filter="url(#pin-shadow)" class="pin-circle"/>
          <text x="310" y="451" text-anchor="middle" font-size="18">🛒</text>
          <polygon points="310,465 304,455 316,455" fill="#d97706"/>
          <circle cx="310" cy="443" r="22" fill="none" stroke="white" stroke-width="2.5" opacity="0.7" class="pin-ring"/>
        </g>

        <!-- 나침반 (우하단) -->
        <g transform="translate(750,500)">
          <circle r="22" fill="white" opacity="0.85"/>
          <text y="5" text-anchor="middle" font-size="22">🧭</text>
        </g>
      </svg>
    </div>

    <!-- 우측: 가맹점 상세 퀘스트 패널 -->
    <div class="iw-panel iw-right-panel" id="shop-quest-panel">
      <!-- 기본 상태 -->
      <div id="quest-default-state">
        <p class="panel-section-label">가맹점 상세정보</p>
        <div class="quest-empty">
          <span style="font-size:2.5rem">🗺️</span>
          <p>지도의 <strong>핀</strong>을 눌러<br>가맹점을 탐험하세요!</p>
        </div>
      </div>

      <!-- 선택된 가맹점 상태 -->
      <div id="quest-active-state" class="hidden">
        <div class="quest-header">
          <span id="quest-emoji" style="font-size:2rem">🛁</span>
          <div>
            <span class="quest-badge-label" id="quest-badge">SPA</span>
            <h3 class="quest-title" id="quest-title">힐링 스파</h3>
          </div>
        </div>
        <div class="quest-desc-box">
          <p id="quest-desc">가맹점 설명이 여기에 표시됩니다.</p>
        </div>
        <div class="quest-reward-box">
          <span class="reward-icon">🎁</span>
          <div>
            <p class="reward-label">상생 혜택</p>
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
</div>

<style>
/* ===== 전체 래퍼 ===== */
.island-world-wrap {
  font-family: -apple-system, 'Apple SD Gothic Neo', sans-serif;
  display: flex;
  flex-direction: column;
  gap: 14px;
}

/* ===== 헤더 ===== */
.iw-header {
  display: flex;
  align-items: center;
  gap: 14px;
  background: linear-gradient(135deg, #ecfdf5, #f0fdf4);
  border: 1.5px solid #bbf7d0;
  border-radius: 20px;
  padding: 14px 20px;
}
.iw-header-icon {
  font-size: 2rem;
  animation: spin-slow 20s linear infinite;
}
@keyframes spin-slow { to { transform: rotate(360deg); } }
.iw-title {
  font-size: 1.15rem;
  font-weight: 900;
  color: #065f46;
  margin: 0;
}
.iw-subtitle {
  font-size: 0.7rem;
  color: #6b7280;
  margin: 0;
}
.iw-badge {
  margin-left: auto;
  background: #d1fae5;
  border: 1px solid #6ee7b7;
  color: #065f46;
  font-size: 0.65rem;
  font-weight: 800;
  padding: 4px 10px;
  border-radius: 999px;
  white-space: nowrap;
}

/* ===== 3-Col 레이아웃 ===== */
.iw-layout {
  display: grid;
  grid-template-columns: 200px 1fr 220px;
  gap: 14px;
  align-items: stretch;
  min-height: 560px;
}
@media (max-width: 900px) {
  .iw-layout { grid-template-columns: 1fr; }
}

/* ===== 공통 패널 ===== */
.iw-panel {
  background: #f8fafc;
  border: 1.5px solid #e2e8f0;
  border-radius: 20px;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.panel-section-label {
  font-size: 0.65rem;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: #94a3b8;
  margin: 0;
}
.panel-divider {
  border: none;
  border-top: 1px solid #e2e8f0;
  margin: 2px 0;
}

/* ===== 필터 리스트 ===== */
.filter-list { display: flex; flex-direction: column; gap: 8px; }
.filter-item {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 0.72rem;
  font-weight: 600;
  color: #374151;
  cursor: pointer;
  padding: 7px 8px;
  border-radius: 10px;
  transition: background 0.2s;
}
.filter-item:hover { background: #f1f5f9; }
.filter-item input { width: 14px; height: 14px; accent-color: #059669; cursor: pointer; }
.filter-dot { width: 9px; height: 9px; border-radius: 50%; flex-shrink: 0; }
.filter-count {
  margin-left: auto;
  background: #e2e8f0;
  color: #64748b;
  font-size: 0.6rem;
  font-weight: 700;
  padding: 1px 6px;
  border-radius: 99px;
}

/* 활성 통계 바 */
.active-stat { display: flex; flex-direction: column; gap: 6px; }
.stat-bar-bg { height: 6px; background: #e2e8f0; border-radius: 99px; overflow: hidden; }
.stat-bar-fill { height: 100%; background: linear-gradient(90deg, #34d399, #059669); border-radius: 99px; transition: width 0.4s ease; width: 100%; }
.stat-text { font-size: 0.65rem; font-weight: 700; color: #059669; }

/* ===== 지도 컨테이너 ===== */
.iw-map-container {
  background: #bae6fd;
  border-radius: 24px;
  overflow: hidden;
  border: 2.5px solid #7dd3fc;
  box-shadow: inset 0 0 40px rgba(14,165,233,0.12);
  min-height: 400px;
  position: relative;
}

/* SVG 핀 인터랙션 */
.map-pin-group { transition: transform 0.25s cubic-bezier(0.34,1.56,0.64,1); transform-origin: bottom center; }
.map-pin-group:hover { transform: scale(1.25) translateY(-4px); }
.map-pin-group:hover .pin-ring { opacity: 1 !important; animation: ping-ring 0.8s ease-out forwards; }
@keyframes ping-ring {
  0%   { r: 22; opacity: 0.9; }
  100% { r: 32; opacity: 0; }
}

/* ===== 우측 퀘스트 패널 ===== */
.quest-empty {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  gap: 10px;
  padding: 30px 10px;
  color: #94a3b8;
  font-size: 0.78rem;
  line-height: 1.6;
}
.quest-header {
  display: flex;
  align-items: center;
  gap: 12px;
  padding-bottom: 12px;
  border-bottom: 1px solid #e2e8f0;
}
.quest-badge-label {
  display: inline-block;
  font-size: 0.6rem;
  font-weight: 900;
  letter-spacing: 0.1em;
  color: #6366f1;
  background: #eef2ff;
  border: 1px solid #c7d2fe;
  padding: 2px 7px;
  border-radius: 6px;
}
.quest-title {
  font-size: 1rem;
  font-weight: 900;
  color: #1e293b;
  margin: 3px 0 0;
}
.quest-desc-box {
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 12px;
  font-size: 0.74rem;
  color: #475569;
  line-height: 1.65;
}
.quest-reward-box {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  background: #fffbeb;
  border: 1px solid #fde68a;
  border-radius: 12px;
  padding: 12px;
}
.reward-icon { font-size: 1.3rem; flex-shrink: 0; }
.reward-label { font-size: 0.6rem; font-weight: 800; color: #d97706; text-transform: uppercase; letter-spacing: 0.05em; margin: 0; }
.reward-text { font-size: 0.74rem; font-weight: 700; color: #92400e; margin: 3px 0 0; }
.quest-actions { display: flex; flex-direction: column; gap: 8px; margin-top: auto; }
.btn-primary {
  width: 100%;
  background: linear-gradient(135deg, #059669, #047857);
  color: white;
  font-size: 0.75rem;
  font-weight: 800;
  padding: 11px;
  border-radius: 12px;
  border: none;
  cursor: pointer;
  transition: opacity 0.2s, transform 0.15s;
}
.btn-primary:hover { opacity: 0.9; transform: translateY(-1px); }
.btn-secondary {
  width: 100%;
  background: #f1f5f9;
  color: #64748b;
  font-size: 0.7rem;
  font-weight: 700;
  padding: 8px;
  border-radius: 10px;
  border: 1px solid #e2e8f0;
  cursor: pointer;
  transition: background 0.2s;
}
.btn-secondary:hover { background: #e2e8f0; }
</style>
`;
