const MYPET_TEMPLATE = `
<div class="space-y-4 animate-fade-in">
    
    <!-- 날짜 & 날씨 -->
    <div class="glass rounded-xl px-4 py-3 shadow-soft">
        <div class="flex items-center justify-between gap-4">
            <!-- 날짜/시간 -->
            <div class="flex items-center gap-4">
                <div>
                    <span id="mypet-date-display" class="block text-xs font-medium text-gray-500">2026. 05. 23 (토)</span>
                    <span id="mypet-time-display" class="text-xl font-bold font-mono text-gray-900">14:30:00</span>
                </div>
                <!-- 날씨 -->
                <div class="flex items-center gap-2.5 border-l border-gray-200 pl-4">
                    <i class="fa-solid fa-sun text-2xl text-amber-400" id="mypet-weather-icon"></i>
                    <div>
                        <span id="mypet-weather-temp" class="block text-base font-bold text-gray-900">24°C</span>
                        <span id="mypet-weather-desc" class="block text-xs font-medium text-gray-500">맑음 (서울)</span>
                    </div>
                </div>
            </div>
            <!-- 미세먼지/습도 -->
            <div class="flex items-center gap-3 text-sm font-medium text-gray-600">
                <span class="flex items-center gap-1">😷 <span id="mypet-weather-dust">--</span></span>
                <span class="flex items-center gap-1">💧 <span id="mypet-weather-humidity">--%</span></span>
            </div>
        </div>
        <div id="mypet-weekly-weather-container" class="hidden"></div>
    </div>

    <!-- 오늘의 운세 (집사 + 펫) -->
    <div class="grid grid-cols-2 gap-3">
        <div class="card-modern bg-violet-50/50 p-3.5 space-y-1.5">
            <span class="block text-xs font-semibold text-violet-600">🧔 집사 오늘의 운세</span>
            <p id="mypet-butler-fortune-text" class="text-xs font-medium text-gray-700 leading-relaxed keep-all">로딩 중...</p>
        </div>
        <div class="card-modern bg-amber-50/50 p-3.5 space-y-1.5">
            <span class="block text-xs font-semibold text-amber-600">🐾 펫 오늘의 운세</span>
            <p id="mypet-fortune-text" class="text-xs font-medium text-gray-700 leading-relaxed keep-all">로딩 중...</p>
        </div>
    </div>

    <!-- ===== 방 + 사이드바를 나란히 배치 ===== -->
    <div class="grid grid-cols-1 lg:grid-cols-12 gap-4 items-start">

    <!-- 왼쪽: 댕이의 하루 방 -->
    <div class="lg:col-span-9 space-y-4">

        <!-- 댕이의 하루 방 -->
        <div id="pet-room-card" class="card-modern overflow-hidden">

            <!-- 헤더 -->
            <div class="px-6 pt-5 pb-4 border-b border-gray-100">
                <div class="flex items-start justify-between gap-4">
                    <div class="flex-1 min-w-0">
                        <h2 class="text-xl font-bold text-gray-900 keep-all mb-1.5" id="pet-room-name-wrapper">
                            <span id="pet-room-name">댕이의 하루 방 🏠</span>
                        </h2>
                        <div class="flex flex-wrap items-center gap-1.5">
                            <p id="pet-room-visit-badge" class="text-[11px] text-amber-500 font-bold">
                                🐾 집사의 <span id="pet-room-visit-count">1</span>번째 방문
                            </p>
                            <span id="room-layout-badge" class="room-layout-badge">
                                <i class="fa-solid fa-couch text-[9px]"></i>
                                <span id="room-layout-badge-text">거실형</span>
                            </span>
                        </div>
                    </div>
                    <div class="flex items-start gap-2 shrink-0">
                        <!-- 사주 정보 카드 -->
                        <div id="room-saju-card" class="bg-gradient-to-br from-violet-50 to-purple-50 border border-violet-200 rounded-xl px-3.5 py-3 max-w-sm shadow-soft">
                            <div class="text-[10px] leading-relaxed space-y-1.5">
                                <div class="flex items-center justify-between mb-1">
                                    <span class="font-black text-violet-700">🔮 사주 분석</span>
                                    <span id="room-saju-score" class="text-[9px] font-bold text-rose-600 bg-rose-50 px-2 py-0.5 rounded-full">미측정</span>
                                </div>
                                <div id="room-saju-result" class="text-gray-700 font-medium space-y-1.5">
                                    <div class="text-[9px] text-gray-500 border-b border-violet-100/50 pb-1">
                                        <span class="font-bold text-violet-600">👤 집사</span>: <span id="room-saju-butler">--년생</span>
                                        <div id="room-saju-owner-summary" class="mt-0.5 text-[9px] text-gray-600 font-normal"></div>
                                    </div>
                                    <div class="text-[9px] text-gray-500 border-b border-violet-100/50 pb-1">
                                        <span class="font-bold text-amber-600">🐾 펫</span>: <span id="room-saju-pet">--년생</span>
                                        <div id="room-saju-pet-summary" class="mt-0.5 text-[9px] text-gray-600 font-normal"></div>
                                    </div>
                                    <div id="room-saju-message" class="text-[9px] text-gray-600 leading-snug pt-0.5">
                                        조화도 탭에서 사주 궁합을 분석해보세요
                                    </div>
                                </div>
                                <button onclick="switchTab('saju'); setTimeout(() => switchSajuSubTab('harmony'), 200)" class="text-[9px] font-bold text-violet-500 hover:text-violet-600 mt-1">
                                    조화도 분석하기 →
                                </button>
                            </div>
                        </div>
                        <!-- 설정 버튼 -->
                        <button onclick="toggleRoomSettings()" id="room-settings-btn"
                            class="w-9 h-9 rounded-xl bg-gray-50 hover:bg-amber-50 border border-gray-200 hover:border-amber-200 flex items-center justify-center transition-all shrink-0">
                            <i class="fa-solid fa-gear text-gray-400 hover:text-amber-500 text-sm" id="room-settings-icon"></i>
                        </button>
                    </div>
                </div>
            </div>

            <!-- 설정 메뉴 (접기/펼치기) -->
            <div id="room-settings-menu" class="hidden border-t border-amber-50 bg-amber-50/40 px-5 py-3">
                <div class="grid grid-cols-5 gap-2">
                    <button onclick="openNotebookModal()"
                        class="room-settings-action flex flex-col items-center gap-1.5 p-2.5 bg-white rounded-xl border border-amber-100 hover:border-brand-300 hover:bg-brand-50 transition-all">
                        <i class="fa-solid fa-address-book text-brand-500 text-lg"></i>
                        <span class="text-[10px] font-black text-gray-600">생활수첩</span>
                    </button>
                    <button onclick="toggleButlerProfileEdit()"
                        class="room-settings-action flex flex-col items-center gap-1.5 p-2.5 bg-white rounded-xl border border-amber-100 hover:border-indigo-300 hover:bg-indigo-50 transition-all">
                        <i class="fa-solid fa-user-pen text-indigo-500 text-lg"></i>
                        <span class="text-[10px] font-black text-gray-600">집사 설정</span>
                    </button>
                    <button onclick="togglePetProfileEdit()"
                        class="room-settings-action flex flex-col items-center gap-1.5 p-2.5 bg-white rounded-xl border border-amber-100 hover:border-amber-300 hover:bg-amber-50 transition-all">
                        <i class="fa-solid fa-paw text-amber-500 text-lg"></i>
                        <span class="text-[10px] font-black text-gray-600">방 설정</span>
                    </button>
                    <button onclick="toggleStickerPicker()"
                        class="room-settings-action flex flex-col items-center gap-1.5 p-2.5 bg-white rounded-xl border border-amber-100 hover:border-rose-300 hover:bg-rose-50 transition-all">
                        <i class="fa-solid fa-wand-magic-sparkles text-rose-400 text-lg"></i>
                        <span class="text-[10px] font-black text-gray-600">꾸미기</span>
                    </button>
                    <button onclick="openPetRegistrationModal()"
                        class="room-settings-action flex flex-col items-center gap-1.5 p-2.5 bg-white rounded-xl border border-amber-100 hover:border-brand-300 hover:bg-brand-50 transition-all">
                        <i class="fa-solid fa-plus text-brand-500 text-lg"></i>
                        <span class="text-[10px] font-black text-gray-600">펫 추가</span>
                    </button>
                </div>

                <!-- 꾸미기 패널 -->
                <div id="room-sticker-picker-panel" class="hidden mt-3 bg-white border border-rose-100 p-3 rounded-2xl space-y-3 text-xs">
                    <div class="flex items-center justify-between pb-1.5 border-b border-gray-100">
                        <span class="font-black text-gray-700"><i class="fa-solid fa-wand-magic-sparkles mr-1 text-rose-400"></i>방 꾸미기</span>
                        <div class="flex items-center gap-2">
                            <button onclick="switchDecorTab('decor')" id="decor-tab-btn" class="decor-tab-btn is-active text-[9px] font-black px-2 py-0.5 rounded-lg transition-all">꾸미기</button>
                            <button onclick="switchDecorTab('shop')" id="shop-tab-btn" class="decor-tab-btn text-[9px] font-black px-2 py-0.5 rounded-lg transition-all">🐾 상점</button>
                        </div>
                    </div>
                    <!-- 방 테마 -->
                    <div>
                        <p class="text-[9px] font-black text-gray-500 mb-1.5">🎨 방 테마</p>
                        <div class="flex gap-2">
                            <button class="room-theme-btn flex flex-col items-center gap-0.5" data-theme="cozy" onclick="applyRoomTheme('cozy')" title="아늑한 방">
                                <span class="room-theme-swatch" style="background:linear-gradient(135deg,#fef7ee 50%,#e8cfa0 50%)"></span>
                                <span class="text-[8px] text-gray-500 font-bold">아늑</span>
                            </button>
                            <button class="room-theme-btn flex flex-col items-center gap-0.5" data-theme="mint" onclick="applyRoomTheme('mint')" title="민트 방">
                                <span class="room-theme-swatch" style="background:linear-gradient(135deg,#edfaf4 50%,#b8e0c8 50%)"></span>
                                <span class="text-[8px] text-gray-500 font-bold">민트</span>
                            </button>
                            <button class="room-theme-btn flex flex-col items-center gap-0.5" data-theme="purple" onclick="applyRoomTheme('purple')" title="보라 방">
                                <span class="room-theme-swatch" style="background:linear-gradient(135deg,#f5f0fe 50%,#d4c0f0 50%)"></span>
                                <span class="text-[8px] text-gray-500 font-bold">보라</span>
                            </button>
                            <button class="room-theme-btn flex flex-col items-center gap-0.5" data-theme="sky" onclick="applyRoomTheme('sky')" title="하늘 방">
                                <span class="room-theme-swatch" style="background:linear-gradient(135deg,#eaf4fe 50%,#b8d8f0 50%)"></span>
                                <span class="text-[8px] text-gray-500 font-bold">하늘</span>
                            </button>
                            <button class="room-theme-btn flex flex-col items-center gap-0.5" data-theme="pink" onclick="applyRoomTheme('pink')" title="핑크 방">
                                <span class="room-theme-swatch" style="background:linear-gradient(135deg,#fef0f4 50%,#f0c0cc 50%)"></span>
                                <span class="text-[8px] text-gray-500 font-bold">핑크</span>
                            </button>
                            <button class="room-theme-btn flex flex-col items-center gap-0.5" data-theme="dark" onclick="applyRoomTheme('dark')" title="밤 방">
                                <span class="room-theme-swatch" style="background:linear-gradient(135deg,#2a2a3e 50%,#1e1e2e 50%)"></span>
                                <span class="text-[8px] text-gray-500 font-bold">밤</span>
                            </button>
                        </div>
                    </div>
                    <!-- 아이템 -->
                    <div>
                        <p class="text-[9px] font-black text-gray-500 mb-1.5">🛋️ 아이템</p>
                        <div class="flex gap-1.5 flex-wrap mb-2">
                            <button class="sticker-cat-btn is-active px-2.5 py-1 rounded-lg border border-gray-200 text-[10px] font-black text-gray-600 transition-all" data-cat="가구" onclick="renderStickerPickerCategory('가구')">가구</button>
                            <button class="sticker-cat-btn px-2.5 py-1 rounded-lg border border-gray-200 text-[10px] font-black text-gray-600 transition-all" data-cat="식물" onclick="renderStickerPickerCategory('식물')">식물</button>
                            <button class="sticker-cat-btn px-2.5 py-1 rounded-lg border border-gray-200 text-[10px] font-black text-gray-600 transition-all" data-cat="장난감" onclick="renderStickerPickerCategory('장난감')">장난감</button>
                            <button class="sticker-cat-btn px-2.5 py-1 rounded-lg border border-gray-200 text-[10px] font-black text-gray-600 transition-all" data-cat="음식" onclick="renderStickerPickerCategory('음식')">음식</button>
                            <button class="sticker-cat-btn px-2.5 py-1 rounded-lg border border-gray-200 text-[10px] font-black text-gray-600 transition-all" data-cat="기타" onclick="renderStickerPickerCategory('기타')">기타</button>
                        </div>
                        <div id="sticker-emoji-grid" class="grid grid-cols-8 gap-1"></div>
                    </div>
                    <!-- 상점 패널 (기본 숨김) -->
                    <div id="room-shop-panel" class="hidden space-y-2">
                        <div class="flex items-center justify-between">
                            <p class="text-[9px] font-black text-gray-500">코인으로 프리미엄 아이템 구매</p>
                            <span id="room-shop-coin-display" class="text-[10px] font-black text-amber-600">🐾 0</span>
                        </div>
                        <div id="room-shop-grid" class="space-y-1.5"></div>
                    </div>
                </div>

                <!-- 집사 설정 패널 -->
                <div id="butler-profile-editor-panel" class="hidden mt-3 bg-white border border-indigo-100 p-4 rounded-2xl space-y-3 text-xs">
                    <div class="flex items-center justify-between pb-1 border-b border-gray-100">
                        <span class="font-black text-gray-700"><i class="fa-solid fa-user-gear mr-1 text-indigo-500"></i>집사 프로필</span>
                        <button onclick="toggleButlerProfileEdit()" class="text-gray-300 hover:text-gray-500"><i class="fa-solid fa-xmark"></i></button>
                    </div>
                    <div class="flex items-center gap-3">
                        <div class="shrink-0">
                            <div class="w-14 h-14 rounded-full bg-indigo-50 flex items-center justify-center text-2xl border-2 border-indigo-100 overflow-hidden relative cursor-pointer group" onclick="document.getElementById('butler-photo-upload').click()">
                                <span id="settings-avatar-disp">🧔</span>
                                <img loading="lazy" id="settings-avatar-image" class="hidden w-full h-full object-cover">
                                <div class="absolute inset-0 bg-black/40 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity rounded-full">
                                    <i class="fa-solid fa-camera text-white text-xs"></i>
                                </div>
                            </div>
                            <input type="file" id="butler-photo-upload" accept="image/*" class="hidden" onchange="uploadButlerProfilePhoto(event)">
                        </div>
                        <div class="flex-1 space-y-2">
                            <input type="text" id="settings-user-nickname" placeholder="닉네임" class="w-full border rounded-lg px-2.5 py-1.5 outline-none focus:border-indigo-400 text-xs">
                            <input type="email" id="settings-user-email" placeholder="이메일" class="w-full border rounded-lg px-2.5 py-1.5 outline-none focus:border-indigo-400 text-xs">
                            <div class="flex gap-1.5 text-base">
                                <button onclick="changeUserAvatar('🧔')" class="hover:scale-125 transition-transform">🧔</button>
                                <button onclick="changeUserAvatar('👩')" class="hover:scale-125 transition-transform">👩</button>
                                <button onclick="changeUserAvatar('🧑')" class="hover:scale-125 transition-transform">🧑</button>
                                <button onclick="changeUserAvatar('👵')" class="hover:scale-125 transition-transform">👵</button>
                                <button onclick="changeUserAvatar('👨‍🌾')" class="hover:scale-125 transition-transform">👨‍🌾</button>
                            </div>
                        </div>
                    </div>
                    <button onclick="saveUserProfile()" class="w-full bg-indigo-500 hover:bg-indigo-600 text-white font-bold text-xs py-2 rounded-xl transition-all">저장</button>
                </div>

                <!-- 방 설정 패널 -->
                <div id="pet-profile-editor-panel" class="hidden mt-3 bg-white border border-amber-100 p-4 rounded-2xl space-y-3 text-xs">
                    <div class="flex items-center justify-between pb-1 border-b border-gray-100">
                        <span class="font-black text-gray-700"><i class="fa-solid fa-paw mr-1 text-amber-500"></i>방 & 펫 설정</span>
                        <button onclick="togglePetProfileEdit()" class="text-gray-300 hover:text-gray-500"><i class="fa-solid fa-xmark"></i></button>
                    </div>
                    <div class="flex items-center gap-3">
                        <div class="shrink-0">
                            <div class="w-14 h-14 rounded-full bg-amber-50 flex items-center justify-center border-2 border-amber-100 overflow-hidden relative cursor-pointer group" onclick="document.getElementById('pet-room-photo-upload').click()">
                                <img loading="lazy" id="settings-pet-image" class="w-full h-full object-cover rounded-full" src="https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=300">
                                <div class="absolute inset-0 bg-black/40 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity rounded-full">
                                    <i class="fa-solid fa-camera text-white text-xs"></i>
                                </div>
                            </div>
                            <input type="file" id="pet-room-photo-upload" accept="image/*" class="hidden" onchange="uploadPetRoomPhoto(event)">
                        </div>
                        <div class="flex-1 space-y-2">
                            <input type="text" id="settings-pet-name" placeholder="반려동물 이름" class="w-full border rounded-lg px-2.5 py-1.5 outline-none focus:border-amber-400 text-xs">
                            <input type="text" id="settings-room-name-input" placeholder="방 이름 🏠" class="w-full border rounded-lg px-2.5 py-1.5 outline-none focus:border-amber-400 text-xs">
                            <div class="room-layout-picker grid grid-cols-2 gap-2">
                                <button type="button" id="room-layout-living" onclick="setRoomLayoutForActivePet('living')"
                                    class="room-layout-option room-layout-preview-card is-active" aria-pressed="true">
                                    <span class="room-layout-preview living">
                                        <span class="preview-sofa"></span>
                                        <span class="preview-rug"></span>
                                        <span class="preview-pet left"></span>
                                        <span class="preview-pet right"></span>
                                    </span>
                                    <span class="room-layout-copy">
                                        <strong><i class="fa-solid fa-house text-[11px]"></i> 거실형</strong>
                                        <small>차분한 여백 중심</small>
                                    </span>
                                </button>
                                <button type="button" id="room-layout-circle" onclick="setRoomLayoutForActivePet('circle')"
                                    class="room-layout-option room-layout-preview-card" aria-pressed="false">
                                    <span class="room-layout-preview circle">
                                        <span class="preview-line"></span>
                                        <span class="preview-pet top"></span>
                                        <span class="preview-pet left"></span>
                                        <span class="preview-pet right"></span>
                                    </span>
                                    <span class="room-layout-copy">
                                        <strong><i class="fa-solid fa-circle-nodes text-[11px]"></i> 교감형</strong>
                                        <small>마음이 이어지는 배치</small>
                                    </span>
                                </button>
                            </div>
                            <div class="flex gap-1.5">
                                <button onclick="changePetPresetPhoto('https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=300')" class="w-7 h-7 rounded-full overflow-hidden border-2 border-amber-100 hover:scale-110 transition-transform"><img loading="lazy" src="https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=100" class="w-full h-full object-cover"></button>
                                <button onclick="changePetPresetPhoto('https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba?auto=format&fit=crop&q=80&w=300')" class="w-7 h-7 rounded-full overflow-hidden border-2 border-amber-100 hover:scale-110 transition-transform"><img loading="lazy" src="https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba?auto=format&fit=crop&q=80&w=100" class="w-full h-full object-cover"></button>
                                <button onclick="changePetPresetPhoto('https://images.unsplash.com/photo-1583511655857-d19b40a7a54e?auto=format&fit=crop&q=80&w=300')" class="w-7 h-7 rounded-full overflow-hidden border-2 border-amber-100 hover:scale-110 transition-transform"><img loading="lazy" src="https://images.unsplash.com/photo-1583511655857-d19b40a7a54e?auto=format&fit=crop&q=80&w=100" class="w-full h-full object-cover"></button>
                                <button onclick="changePetPresetPhoto('https://images.unsplash.com/photo-1535268647977-a403b69fc756?auto=format&fit=crop&q=80&w=300')" class="w-7 h-7 rounded-full overflow-hidden border-2 border-amber-100 hover:scale-110 transition-transform"><img loading="lazy" src="https://images.unsplash.com/photo-1535268647977-a403b69fc756?auto=format&fit=crop&q=80&w=100" class="w-full h-full object-cover"></button>
                            </div>
                        </div>
                    </div>
                    <button onclick="savePetProfileAndRoom()" class="w-full bg-amber-500 hover:bg-amber-600 text-white font-bold text-xs py-2 rounded-xl transition-all">저장</button>
                </div>
            </div>

            <!-- 메인 콘텐츠 -->
            <div class="px-5 pb-5 space-y-4">

                <!-- 스테이지: 불규칙 배치 (집사 중앙, 펫들 주변) -->
                <div class="room-stage relative w-full h-[360px] md:h-[420px] flex items-center justify-center pt-5 pb-3">
                    <!-- 싸이월드 쿼터뷰 방 (SVG) -->
                    <svg aria-hidden="true" class="room-decor-layer"
                         viewBox="0 0 100 100" preserveAspectRatio="none"
                         xmlns="http://www.w3.org/2000/svg"
                         style="position:absolute;inset:0;width:100%;height:100%;z-index:0;pointer-events:none;border-radius:inherit;">
                      <defs>
                        <!-- 좌측 벽 그라데이션 (왼쪽 어둡 → 오른쪽) -->
                        <linearGradient id="cwWallL" x1="0" x2="1" y1="0" y2="0">
                          <stop offset="0%"   stop-color="var(--cw-wall-dark,#9a6c38)"/>
                          <stop offset="100%" stop-color="var(--cw-wall-side,#c89058)"/>
                        </linearGradient>
                        <!-- 바닥 그라데이션 (뒤 밝 → 앞 어둡) -->
                        <linearGradient id="cwFloorG" x1="0" x2="0" y1="0" y2="1">
                          <stop offset="0%"   stop-color="var(--cw-floor,#dfc070)"/>
                          <stop offset="100%" stop-color="var(--cw-floor-front,#b89040)"/>
                        </linearGradient>
                        <!-- 바닥 대각선 마루 패턴 -->
                        <pattern id="cwFloorP" x="0" y="0" width="9" height="9" patternUnits="userSpaceOnUse">
                          <line x1="0" y1="9" x2="9" y2="0" stroke="rgba(0,0,0,0.08)" stroke-width="0.4"/>
                        </pattern>
                        <!-- 창문 햇빛 glow -->
                        <radialGradient id="cwSun" cx="82" cy="8" r="38" gradientUnits="userSpaceOnUse">
                          <stop offset="0%"   stop-color="rgba(255,235,140,0.38)"/>
                          <stop offset="100%" stop-color="rgba(255,235,140,0)"/>
                        </radialGradient>
                        <!-- 창문 유리 -->
                        <linearGradient id="cwGlass" x1="0" x2="1" y1="0" y2="1">
                          <stop offset="0%"   stop-color="rgba(255,255,255,0.55)"/>
                          <stop offset="100%" stop-color="rgba(120,190,240,0.75)"/>
                        </linearGradient>
                        <!-- 뒷벽 위→아래 미세 그라데이션 -->
                        <linearGradient id="cwWallBG" x1="0" x2="0" y1="0" y2="1">
                          <stop offset="0%"   stop-color="var(--cw-wall-back,#fef0d8)"/>
                          <stop offset="100%" stop-color="var(--cw-wall-back,#fef0d8)" stop-opacity="0.88"/>
                        </linearGradient>
                      </defs>

                      <!-- ① 뒷벽 (우측 상단 사다리꼴) -->
                      <polygon points="27,0 100,4 100,63 27,63" fill="url(#cwWallBG)"/>

                      <!-- ② 좌측 벽 (평행사변형, 확연히 어두움) -->
                      <polygon points="0,11 27,0 27,63 0,72" fill="url(#cwWallL)"/>

                      <!-- ③ 걸레받이 (경계선) -->
                      <polygon points="0,72 27,63 100,63 100,67 27,67 0,76" fill="var(--cw-baseboard,#c07030)"/>

                      <!-- ④ 바닥 (사다리꼴 + 마루 패턴) -->
                      <polygon points="0,76 27,67 100,67 100,100 0,100" fill="url(#cwFloorG)"/>
                      <polygon points="0,76 27,67 100,67 100,100 0,100" fill="url(#cwFloorP)" opacity="0.9"/>

                      <!-- ⑤ 코너 라인 (좌측벽/뒷벽 경계) -->
                      <line x1="27" y1="0" x2="27" y2="63" stroke="var(--cw-baseboard,#c07030)" stroke-width="1.8"/>

                      <!-- ⑥ 창문 (뒷벽 오른쪽) -->
                      <rect x="69" y="5" width="24" height="33" rx="1.5" fill="var(--cw-baseboard,#c07030)"/>
                      <rect x="71" y="7" width="20" height="29" fill="url(#cwGlass)"/>
                      <!-- 창틀 십자 -->
                      <line x1="81" y1="7"  x2="81" y2="36" stroke="var(--cw-baseboard,#c07030)" stroke-width="1.5"/>
                      <line x1="71" y1="21" x2="91" y2="21" stroke="var(--cw-baseboard,#c07030)" stroke-width="1.5"/>
                      <!-- 창문 glare -->
                      <rect x="72" y="8"  width="7" height="11" rx="2" fill="rgba(255,255,255,0.52)"/>
                      <!-- 커튼 왼쪽 -->
                      <rect x="69" y="5" width="5" height="33" fill="var(--cw-baseboard,#c07030)" opacity="0.45"/>
                      <!-- 커튼 오른쪽 -->
                      <rect x="88" y="5" width="5" height="33" fill="var(--cw-baseboard,#c07030)" opacity="0.45"/>

                      <!-- ⑦ 창문 햇빛 (뒷벽 오버레이) -->
                      <polygon points="27,0 100,4 100,63 27,63" fill="url(#cwSun)"/>
                    </svg>
                    <!-- SVG 목줄 연결선 -->
                    <svg id="leash-svg" viewBox="0 0 100 100" preserveAspectRatio="none" class="absolute inset-0 w-full h-full pointer-events-none" style="z-index: 1;">
                        <!-- JS로 동적 생성 -->
                    </svg>

                    <!-- 집사 (중앙) -->
                    <div class="absolute" style="left: 50%; top: 42%; transform: translate(-50%, -50%); z-index: 10;">
                        <div class="flex flex-col items-center gap-1.5">
                            <span class="text-[10px] font-black text-brand-600 bg-brand-50 border border-brand-200 px-2 py-0.5 rounded-full flex items-center gap-1">
                                👑 집사
                            </span>
                            <div id="butler-graphic-container" onclick="triggerButlerPhotoUploadDirect()"
                                class="butler-stage-core w-28 h-28 flex items-center justify-center cursor-pointer hover:scale-105 transition-transform relative"
                                title="집사 사진 변경">
                                <!-- 싸이월드 미니미 스타일 캐릭터 -->
                                <svg id="butler-stage-avatar" viewBox="0 0 60 80" width="112" height="112" xmlns="http://www.w3.org/2000/svg" class="drop-shadow-lg">
                                  <!-- 그림자 -->
                                  <ellipse cx="30" cy="78" rx="14" ry="4" fill="rgba(0,0,0,0.15)"/>
                                  <!-- 다리 -->
                                  <rect x="21" y="57" width="7" height="14" rx="3" fill="#5b6af0"/>
                                  <rect x="32" y="57" width="7" height="14" rx="3" fill="#5b6af0"/>
                                  <!-- 신발 -->
                                  <ellipse cx="24.5" cy="71" rx="5" ry="3" fill="#2d2d2d"/>
                                  <ellipse cx="35.5" cy="71" rx="5" ry="3" fill="#2d2d2d"/>
                                  <!-- 몸 (셔츠) -->
                                  <rect x="16" y="34" width="28" height="26" rx="6" fill="#7c8ef8"/>
                                  <!-- 넥타이 -->
                                  <polygon points="30,36 27,44 30,47 33,44" fill="#e05050"/>
                                  <!-- 팔 왼쪽 -->
                                  <rect x="8" y="35" width="9" height="18" rx="4" fill="#7c8ef8"/>
                                  <!-- 팔 오른쪽 -->
                                  <rect x="43" y="35" width="9" height="18" rx="4" fill="#7c8ef8"/>
                                  <!-- 손 -->
                                  <circle cx="12" cy="54" r="4" fill="#f5c5a0"/>
                                  <circle cx="48" cy="54" r="4" fill="#f5c5a0"/>
                                  <!-- 목 -->
                                  <rect x="25" y="27" width="10" height="9" rx="2" fill="#f5c5a0"/>
                                  <!-- 얼굴 -->
                                  <circle cx="30" cy="20" r="16" fill="#f5c5a0"/>
                                  <!-- 눈 -->
                                  <circle cx="24" cy="19" r="2.5" fill="#333"/>
                                  <circle cx="36" cy="19" r="2.5" fill="#333"/>
                                  <circle cx="24.8" cy="18.2" r="1" fill="white"/>
                                  <circle cx="36.8" cy="18.2" r="1" fill="white"/>
                                  <!-- 볼터치 -->
                                  <ellipse cx="20" cy="23" rx="4" ry="2.5" fill="rgba(255,150,150,0.4)"/>
                                  <ellipse cx="40" cy="23" rx="4" ry="2.5" fill="rgba(255,150,150,0.4)"/>
                                  <!-- 입 -->
                                  <path d="M 26 25 Q 30 28 34 25" stroke="#c07060" stroke-width="1.5" fill="none" stroke-linecap="round"/>
                                  <!-- 머리카락 -->
                                  <path d="M 14 16 Q 14 2 30 2 Q 46 2 46 16 Q 44 8 30 8 Q 16 8 14 16 Z" fill="#3d2a1a"/>
                                  <!-- 왕관 장식 -->
                                  <rect x="22" y="3" width="16" height="6" rx="1" fill="#f0b030"/>
                                  <polygon points="22,3 22,9 25,6" fill="#e8a020"/>
                                  <polygon points="30,3 30,9 33,6" fill="#e8a020"/>
                                  <polygon points="38,3 38,9 35,6" fill="#e8a020"/>
                                  <circle cx="22" cy="3" r="1.5" fill="#e05050"/>
                                  <circle cx="30" cy="2" r="1.5" fill="#50c050"/>
                                  <circle cx="38" cy="3" r="1.5" fill="#5050e0"/>
                                </svg>
                                <img loading="lazy" id="butler-stage-image" class="hidden w-28 h-28 object-cover rounded-full border-4 border-brand-400 shadow-xl">
                            </div>
                            <input type="file" id="butler-direct-upload" accept="image/*" class="hidden" onchange="uploadButlerPhotoDirect(event)">
                            <span id="butler-stage-name" class="text-xs text-gray-600 font-black">집사</span>
                        </div>
                    </div>

                    <!-- 말풍선 (집사 위, 폭 제한) -->
                    <div id="pet-speech-bubble" class="room-speech-bubble absolute bg-amber-50 border border-amber-200 text-amber-800 text-[10px] font-bold py-1.5 px-2.5 rounded-xl keep-all text-center shadow-sm" style="left: 50%; top: 4%; transform: translateX(-50%); z-index: 11; max-width: 178px; white-space: normal; line-height: 1.4;">
                        <span id="pet-bubble-text">산책 가요! 🐕</span>
                        <div class="absolute -bottom-1.5 left-1/2 -translate-x-1/2 w-2.5 h-2.5 bg-amber-50 border-r border-b border-amber-200 rotate-45"></div>
                    </div>

                    <!-- 반려동물들 (겹침 방지 배치) -->
                    <div id="pet-stage-list" class="absolute inset-0" style="z-index: 5;">
                        <!-- renderPetStageList() - 각 펫을 원형으로 배치 -->
                    </div>
                </div>

                <!-- 펫 스탯바 + 코인 + 데일리 미션 -->
                <div class="bg-white border border-gray-100 rounded-2xl px-3 py-2.5 space-y-1.5">
                    <div id="pet-game-stat-bars"></div>
                    <div id="pet-daily-missions"></div>
                </div>

                <!-- 활성 펫 배지 -->
                <div class="flex gap-1 flex-wrap justify-center -mt-1">
                    <span id="pet-stage-mbti-badge" class="hidden bg-pink-100 text-pink-700 text-[10px] font-extrabold px-2 py-0.5 rounded-lg cursor-pointer" onclick="goToMbtiTest()">ENFP</span>
                    <span id="pet-stage-iq-badge" class="hidden bg-sky-100 text-sky-700 text-[10px] font-extrabold px-2 py-0.5 rounded-lg cursor-pointer" onclick="goToIqTest()">IQ 130</span>
                    <span id="pet-stage-saju-badge" class="hidden bg-amber-100 text-amber-600 text-[10px] font-black px-2 py-0.5 rounded-lg cursor-pointer" onclick="switchTab('saju')">☯️ 사주합</span>
                </div>
                <div id="pet-dday-bubble" class="hidden absolute z-30 bg-rose-500 text-white text-[10px] font-black py-1 px-2.5 rounded-xl shadow border-2 border-white animate-bounce">
                    <span id="pet-dday-text">D-3</span>
                </div>
                <input type="file" id="pet-direct-upload" accept="image/*" class="hidden" onchange="uploadPetPhotoDirect(event)">

                <!-- 컨디션 2칸 -->
                <div class="grid grid-cols-2 gap-2">
                    <div class="bg-gray-50 p-2.5 rounded-2xl border border-gray-100">
                        <div class="flex justify-between items-center mb-1.5">
                            <div class="flex items-center gap-1">
                                <span id="butler-condition-emoji" class="text-sm">🧔</span>
                                <span class="text-[11px] font-black text-gray-600">집사</span>
                            </div>
                            <span class="font-mono text-sm font-extrabold text-gray-700" id="butler-condition-pct">85%</span>
                        </div>
                        <div class="w-full bg-gray-200 h-1.5 rounded-full overflow-hidden mb-1">
                            <div id="butler-condition-bar" class="bg-brand-400 h-full transition-all duration-700" style="width:85%"></div>
                        </div>
                        <p id="butler-condition-desc" class="text-[10px] text-gray-500 font-medium leading-snug keep-all">로딩 중...</p>
                    </div>
                    <div class="bg-amber-50/60 p-2.5 rounded-2xl border border-amber-100">
                        <div class="flex justify-between items-center mb-1.5">
                            <div class="flex items-center gap-1">
                                <span id="pet-condition-emoji" class="text-sm">🐕</span>
                                <span class="text-[11px] font-black text-amber-700">펫</span>
                            </div>
                            <span class="font-mono text-sm font-extrabold text-amber-600" id="pet-condition-pct">90%</span>
                        </div>
                        <div class="w-full bg-amber-100 h-1.5 rounded-full overflow-hidden mb-1">
                            <div id="pet-condition-bar" class="bg-amber-400 h-full transition-all duration-700" style="width:90%"></div>
                        </div>
                        <p id="pet-condition-desc" class="text-[10px] text-amber-800/70 font-medium leading-snug keep-all">로딩 중...</p>
                    </div>
                </div>

                <!-- 건강 + 바로가기 아이콘 바 -->
                <div class="bg-gray-50 rounded-2xl border border-gray-100 px-3 py-2.5">
                    <div class="flex items-center justify-between">
                        <!-- 건강 3칸 -->
                        <div class="flex gap-4">
                            <div class="text-center">
                                <span id="health-log-poop" class="text-lg leading-none block">-</span>
                                <span class="text-[9px] text-gray-400 font-bold mt-0.5 block">배변</span>
                            </div>
                            <div class="text-center">
                                <span id="health-log-food" class="text-sm font-black text-gray-700 block">-g</span>
                                <span class="text-[9px] text-gray-400 font-bold mt-0.5 block">식사</span>
                            </div>
                            <div class="text-center">
                                <span id="health-log-water" class="text-sm font-black text-gray-700 block">-ml</span>
                                <span class="text-[9px] text-gray-400 font-bold mt-0.5 block">음수</span>
                            </div>
                        </div>
                        <!-- 아이콘 버튼 3개 -->
                        <div class="flex items-center gap-1.5">
                            <button onclick="openHealthLogModal()" title="건강 기록"
                                class="w-9 h-9 rounded-xl bg-white border border-gray-200 hover:border-brand-300 hover:bg-brand-50 flex items-center justify-center transition-all">
                                <i class="fa-solid fa-notes-medical text-brand-400 text-sm"></i>
                            </button>
                            <button onclick="openHealthReportModal()" title="맞춤 건강 조언"
                                class="w-9 h-9 rounded-xl bg-white border border-gray-200 hover:border-amber-300 hover:bg-amber-50 flex items-center justify-center transition-all">
                                <i class="fa-solid fa-wand-magic-sparkles text-amber-400 text-sm"></i>
                            </button>
                            <button onclick="switchTab('mailbox')" title="우체통" class="relative w-9 h-9 rounded-xl bg-white border border-gray-200 hover:border-amber-300 hover:bg-amber-50 flex items-center justify-center transition-all">
                                <i class="fa-solid fa-envelope text-amber-400 text-sm"></i>
                                <span id="mailbox-unread-count-badge" class="hidden absolute -top-1 -right-1 bg-rose-500 text-white text-[8px] font-black w-4 h-4 rounded-full flex items-center justify-center">0</span>
                            </button>
                        </div>
                    </div>
                    <!-- 사주 조언 한 줄 -->
                    <p id="personalized-health-tip" class="text-[10px] text-gray-400 font-medium mt-2 pt-2 border-t border-gray-100 keep-all leading-relaxed">✨ 분석 중...</p>
                </div>

            </div>
        </div>

        <!-- 건강 요약 카드 (이전 위치에서 제거됨) -->

        <!-- 사주/조화도 결과 카드 (숨김 — 배지에 통합) -->
        <div id="mypet-saju-card" style="display:none;">
            <!-- 결과가 없을 때의 UI -->
            <div id="mypet-saju-no-result" class="space-y-3"></div>

            <!-- 결과가 있을 때의 UI -->
            <div id="mypet-saju-has-result" class="hidden space-y-4 text-xs">
                <div id="mypet-harmony-display-box" class="hidden"></div>
                <div id="mypet-saju-grid-section" class="hidden"></div>
                <div id="mypet-saju-compat-section" class="hidden"></div>
                <div id="mypet-saju-buttons-section" class="hidden"></div>
            </div>
        </div>
    </div>

    <!-- 오른쪽: 챌린지 + 업적 -->
    <div class="lg:col-span-3 space-y-2.5">

        <!-- 산책 streak 배너 -->
        <div class="bg-gradient-to-br from-orange-50 to-amber-50/60 border border-amber-200/60 rounded-xl p-2.5 shadow-sm">
            <div id="walk-streak-banner">
                <div class="flex items-center gap-2 text-[10px] text-gray-400 font-bold">
                    <i class="fa-solid fa-fire text-gray-300"></i>
                    <span>산책을 시작하면 streak이 쌓여요!</span>
                </div>
            </div>
        </div>

        <!-- 일일 챌린지 -->
        <div class="bg-gradient-to-br from-orange-50 to-amber-50/60 border border-amber-200/60 rounded-xl p-3 shadow-sm">
            <div id="daily-challenges"></div>
        </div>

        <!-- 업적 배지 -->
        <div class="bg-gradient-to-br from-amber-50 to-yellow-50/60 border border-amber-200/60 rounded-xl p-2.5 shadow-sm">
            <div id="achievement-badges"></div>
        </div>

        <!-- 조화도 카드 -->
        <div id="harmony-widget-card" class="bg-gradient-to-br from-rose-50 to-pink-50 border border-rose-200 rounded-xl p-3 shadow-sm">
            <div class="flex items-center justify-between mb-2">
                <h3 class="text-[10px] font-black text-gray-700">
                    <span id="harmony-widget-icon">💖</span> 영혼의 조화도
                </h3>
                <button onclick="switchTab('saju'); setTimeout(() => switchSajuSubTab('harmony'), 200)" class="text-[8px] font-bold text-rose-500 hover:text-rose-600">측정하기</button>
            </div>
            <div class="text-center mb-2">
                <div id="harmony-widget-score" class="text-2xl font-black text-rose-600 mb-0.5">--점</div>
                <div id="harmony-widget-title" class="text-[9px] font-bold text-gray-700">조화도를 측정해보세요</div>
            </div>
            <div class="space-y-1 pt-2 border-t border-rose-100">
                <div class="flex items-center justify-between text-[9px]">
                    <span class="text-gray-500">👤 집사</span>
                    <span id="harmony-widget-butler" class="font-bold text-gray-700">--년생</span>
                </div>
                <div class="flex items-center justify-between text-[9px]">
                    <span class="text-gray-500">🐾 펫</span>
                    <span id="harmony-widget-pet" class="font-bold text-gray-700">--년생</span>
                </div>
            </div>
        </div>

    </div>

    </div><!-- /grid -->

    <!-- 프리미엄 업그레이드 모달 -->
    <div id="premium-modal" class="hidden fixed inset-0 bg-black/60 z-50 flex items-end justify-center">
        <div class="bg-white rounded-t-3xl px-6 pt-6 pb-10 w-full max-w-sm space-y-4 shadow-2xl animate-fade-in">
            <!-- 핸들 -->
            <div class="flex justify-center -mt-2 mb-2">
                <div class="w-10 h-1 bg-gray-200 rounded-full"></div>
            </div>
            <div class="text-center space-y-1.5">
                <span class="text-5xl">👑</span>
                <h3 class="text-lg font-black text-gray-900">펫과나 프리미엄</h3>
                <p class="text-xs text-gray-400 font-medium">이번 달 무료 AI 분석 3회를 모두 사용했습니다</p>
            </div>
            <!-- 구독 플랜 선택 탭 -->
            <div class="flex gap-2 bg-gray-100 p-1 rounded-2xl">
                <button id="premium-plan-monthly" onclick="selectPremiumPlan('monthly')"
                    class="flex-1 py-2 text-xs font-black rounded-xl bg-white text-violet-700 shadow-sm transition-all">
                    월간 구독
                </button>
                <button id="premium-plan-yearly" onclick="selectPremiumPlan('yearly')"
                    class="flex-1 py-2 text-xs font-bold rounded-xl text-gray-400 transition-all relative">
                    연간 구독
                    <span class="absolute -top-2 -right-1 bg-rose-500 text-white text-[9px] font-black px-1.5 py-0.5 rounded-full">-30%</span>
                </button>
            </div>
            <!-- 가격 배지 -->
            <div class="flex justify-center">
                <div id="premium-price-badge" class="bg-gradient-to-r from-violet-500 to-purple-600 text-white px-6 py-3 rounded-2xl text-center shadow-lg w-full">
                    <span id="premium-price-main" class="block text-2xl font-black">월 5,900원</span>
                    <span id="premium-price-sub" class="text-xs font-bold opacity-80">TTcare 대비 올인원 · 해지 언제든 가능</span>
                </div>
            </div>
            <!-- 혜택 -->
            <div class="grid grid-cols-2 gap-2">
                <div class="flex items-center gap-2 bg-violet-50 rounded-xl p-2.5">
                    <span class="text-lg">🏥</span>
                    <span class="text-[11px] font-black text-violet-700">AI 건강분석 무제한</span>
                </div>
                <div class="flex items-center gap-2 bg-violet-50 rounded-xl p-2.5">
                    <span class="text-lg">📖</span>
                    <span class="text-[11px] font-black text-violet-700">일기장 무제한 저장</span>
                </div>
                <div class="flex items-center gap-2 bg-violet-50 rounded-xl p-2.5">
                    <span class="text-lg">📊</span>
                    <span class="text-[11px] font-black text-violet-700">건강·일기 PDF 내보내기</span>
                </div>
                <div class="flex items-center gap-2 bg-violet-50 rounded-xl p-2.5">
                    <span class="text-lg">🎙️</span>
                    <span class="text-[11px] font-black text-violet-700">음성 문진 무제한</span>
                </div>
                <div id="premium-yearly-bonus" class="hidden flex items-center gap-2 bg-rose-50 rounded-xl p-2.5">
                    <span class="text-lg">📋</span>
                    <span class="text-[11px] font-black text-rose-700">건강 리포트 PDF 매월 자동 발송</span>
                </div>
                <div class="flex items-center gap-2 bg-violet-50 rounded-xl p-2.5">
                    <span class="text-lg">📅</span>
                    <span class="text-[11px] font-black text-violet-700">"1년 전 오늘" 추억 알림</span>
                </div>
                <div class="flex items-center gap-2 bg-violet-50 rounded-xl p-2.5">
                    <span class="text-lg">⚡</span>
                    <span class="text-[11px] font-black text-violet-700">우선 고객 지원</span>
                </div>
            </div>
            <div class="space-y-2">
                <button onclick="startStripeCheckout()"
                    class="w-full py-3.5 bg-gradient-to-r from-violet-500 to-purple-600 hover:from-violet-600 hover:to-purple-700 text-white font-black text-sm rounded-2xl transition-all shadow-lg active:scale-95">
                    💳 카드로 구독 시작
                </button>
                <button onclick="closePremiumModal()"
                    class="w-full py-2.5 text-gray-400 hover:text-gray-600 font-bold text-sm transition-colors">
                    나중에
                </button>
            </div>
        </div>
    </div>

</div>
`;
