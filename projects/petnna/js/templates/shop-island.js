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

    <!-- 중앙: 피지 섬 펫라이프 지도 -->
    <div class="iw-map-container">
      <!-- 피지 섬 배경 지도 -->
      <div id="petlife-map" class="fiji-map-wrapper">
        <!-- 피지 섬 구글맵 배경 이미지 -->
        <div class="fiji-map-bg" style="
          background-image: url('map.png');
          background-size: cover;
          background-position: center;
          background-color: #bae6fd;
          width: 100%;
          height: 100%;
          min-height: 600px;
          border-radius: 24px;
          position: relative;
        ">
          <!-- 가맹점 핀 오버레이 -->
          <div id="petlife-pins-container" class="pins-overlay"></div>
        </div>
      </div>

      <!-- 가맹점 상세 팝업 모달 -->
      <div id="location-popup" class="location-popup hidden">
        <div class="popup-content">
          <button onclick="closePetlifePopup()" class="popup-close">
            <i class="fa-solid fa-xmark"></i>
          </button>
          <div class="popup-header">
            <span id="popup-emoji" class="popup-emoji">🏥</span>
            <div>
              <h3 id="popup-name" class="popup-name">가맹점명</h3>
              <span id="popup-category" class="popup-category-badge">카테고리</span>
            </div>
          </div>
          <div class="popup-body">
            <p id="popup-description" class="popup-desc"></p>

            <div class="popup-info-grid">
              <div class="popup-info-item">
                <i class="fa-solid fa-location-dot"></i>
                <span id="popup-address"></span>
              </div>
              <div class="popup-info-item">
                <i class="fa-solid fa-phone"></i>
                <a id="popup-phone" href="tel:"></a>
              </div>
              <div class="popup-info-item">
                <i class="fa-solid fa-clock"></i>
                <span id="popup-hours"></span>
              </div>
            </div>

            <div id="popup-services" class="popup-services"></div>
          </div>
          <div class="popup-footer">
            <a id="popup-website-btn" href="#" target="_blank" rel="noopener" class="popup-btn-primary">
              <i class="fa-solid fa-globe"></i> 웹사이트 방문
            </a>
            <a id="popup-phone-btn" href="tel:" class="popup-btn-secondary">
              <i class="fa-solid fa-phone"></i> 전화 예약
            </a>
          </div>
        </div>
      </div>
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

/* ===== 피지 지도 스타일 ===== */
.fiji-map-wrapper {
  position: relative;
  width: 100%;
  height: 100%;
  min-height: 600px;
}

.pins-overlay {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  pointer-events: none;
}

/* 가맹점 핀 스타일 */
.petlife-pin {
  position: absolute;
  width: 50px;
  height: 50px;
  border-radius: 50% 50% 50% 0;
  transform: rotate(-45deg);
  border: 4px solid white;
  box-shadow: 0 6px 20px rgba(0,0,0,0.3), 0 0 0 0 rgba(255,255,255,0.6);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.4s cubic-bezier(0.175, 0.885, 0.32, 1.275);
  pointer-events: auto;
  animation: pin-drop 0.8s cubic-bezier(0.175, 0.885, 0.32, 1.275);
}

.petlife-pin span {
  font-size: 24px;
  transform: rotate(45deg);
  line-height: 1;
  filter: drop-shadow(0 2px 4px rgba(0,0,0,0.3));
}

.petlife-pin:hover {
  transform: rotate(-45deg) scale(1.3) translateY(-8px);
  box-shadow: 0 12px 30px rgba(0,0,0,0.4), 0 0 0 12px rgba(255,255,255,0.3);
  z-index: 100;
}

.petlife-pin::before {
  content: '';
  position: absolute;
  top: 50%;
  left: 50%;
  width: 100%;
  height: 100%;
  border-radius: 50% 50% 50% 0;
  background: inherit;
  transform: translate(-50%, -50%);
  animation: pin-pulse 2.5s infinite;
  opacity: 0;
}

@keyframes pin-pulse {
  0% {
    transform: translate(-50%, -50%) scale(1);
    opacity: 0.7;
  }
  100% {
    transform: translate(-50%, -50%) scale(2.2);
    opacity: 0;
  }
}

@keyframes pin-drop {
  0% {
    transform: rotate(-45deg) translateY(-150px) scale(0);
    opacity: 0;
  }
  60% {
    transform: rotate(-45deg) translateY(15px) scale(1.15);
  }
  100% {
    transform: rotate(-45deg) translateY(0) scale(1);
    opacity: 1;
  }
}

/* ===== 가맹점 상세 팝업 ===== */
.location-popup {
  position: fixed;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background: rgba(0,0,0,0.6);
  backdrop-filter: blur(8px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 9999;
  padding: 20px;
  animation: fade-in 0.3s ease;
}

.location-popup.hidden {
  display: none;
}

.popup-content {
  background: white;
  border-radius: 28px;
  max-width: 500px;
  width: 100%;
  max-height: 90vh;
  overflow-y: auto;
  box-shadow: 0 20px 60px rgba(0,0,0,0.3);
  position: relative;
  animation: slide-up 0.4s cubic-bezier(0.175, 0.885, 0.32, 1.275);
}

@keyframes fade-in {
  from { opacity: 0; }
  to { opacity: 1; }
}

@keyframes slide-up {
  from {
    transform: translateY(50px);
    opacity: 0;
  }
  to {
    transform: translateY(0);
    opacity: 1;
  }
}

.popup-close {
  position: absolute;
  top: 16px;
  right: 16px;
  width: 36px;
  height: 36px;
  border-radius: 50%;
  background: rgba(0,0,0,0.05);
  border: none;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 18px;
  color: #64748b;
  transition: all 0.2s;
  z-index: 10;
}

.popup-close:hover {
  background: rgba(0,0,0,0.1);
  color: #0f172a;
  transform: rotate(90deg);
}

.popup-header {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 32px 32px 20px;
  border-bottom: 2px solid #f1f5f9;
}

.popup-emoji {
  font-size: 48px;
  line-height: 1;
}

.popup-name {
  font-size: 20px;
  font-weight: 950;
  color: #0f172a;
  margin: 0 0 6px;
  letter-spacing: -0.02em;
}

.popup-category-badge {
  display: inline-block;
  background: linear-gradient(135deg, #f0fdf4, #dcfce7);
  color: #166534;
  font-size: 11px;
  font-weight: 900;
  padding: 4px 12px;
  border-radius: 12px;
  letter-spacing: 0.05em;
  text-transform: uppercase;
}

.popup-body {
  padding: 24px 32px;
}

.popup-desc {
  font-size: 15px;
  line-height: 1.7;
  color: #475569;
  margin: 0 0 20px;
}

.popup-info-grid {
  display: flex;
  flex-direction: column;
  gap: 12px;
  margin-bottom: 20px;
}

.popup-info-item {
  display: flex;
  align-items: center;
  gap: 12px;
  font-size: 14px;
  color: #64748b;
  padding: 10px 14px;
  background: #f8fafc;
  border-radius: 12px;
}

.popup-info-item i {
  color: #22c55e;
  font-size: 16px;
  width: 20px;
  text-align: center;
}

.popup-info-item a {
  color: #2563eb;
  text-decoration: none;
  font-weight: 700;
}

.popup-info-item a:hover {
  text-decoration: underline;
}

.popup-services {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 16px;
}

.service-tag {
  background: linear-gradient(135deg, #22c55e, #16a34a);
  color: white;
  font-size: 12px;
  font-weight: 800;
  padding: 6px 14px;
  border-radius: 999px;
  letter-spacing: 0.02em;
}

.popup-footer {
  padding: 20px 32px 32px;
  display: flex;
  gap: 12px;
}

.popup-btn-primary,
.popup-btn-secondary {
  flex: 1;
  padding: 14px;
  border-radius: 14px;
  font-size: 14px;
  font-weight: 900;
  text-align: center;
  text-decoration: none;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
}

.popup-btn-primary {
  background: linear-gradient(135deg, #22c55e, #16a34a);
  color: white;
  box-shadow: 0 4px 14px rgba(34,197,94,0.25);
}

.popup-btn-primary:hover {
  transform: translateY(-2px);
  box-shadow: 0 6px 20px rgba(34,197,94,0.35);
}

.popup-btn-secondary {
  background: white;
  color: #64748b;
  border: 2px solid #e2e8f0;
}

.popup-btn-secondary:hover {
  background: #f8fafc;
  border-color: #cbd5e1;
  color: #0f172a;
}

/* 반응형 */
@media (max-width: 640px) {
  .popup-content {
    max-width: 100%;
    margin: 0;
    border-radius: 28px 28px 0 0;
  }

  .popup-footer {
    flex-direction: column;
  }
}
</style>
`;
