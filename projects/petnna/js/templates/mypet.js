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
                    <!-- Dog Paradise Field (isometric) -->
                    <svg aria-hidden="true" class="room-decor-layer"
                         viewBox="0 0 100 100" preserveAspectRatio="none"
                         xmlns="http://www.w3.org/2000/svg"
                         style="position:absolute;inset:0;width:100%;height:100%;z-index:0;pointer-events:none;border-radius:inherit;">
                      <defs>
                        <linearGradient id="dpSky" x1="0" y1="0" x2="0" y2="1">
                          <stop offset="0%"   stop-color="#2878d8"/>
                          <stop offset="100%" stop-color="#80c8f8"/>
                        </linearGradient>
                        <linearGradient id="dpGrass" x1="0" y1="0" x2="0" y2="1">
                          <stop offset="0%"   stop-color="#98d840"/>
                          <stop offset="100%" stop-color="#4a9808"/>
                        </linearGradient>
                        <linearGradient id="dpPool" x1="0" y1="0" x2="1" y2="1">
                          <stop offset="0%"   stop-color="#50d0f8"/>
                          <stop offset="50%"  stop-color="#18a8e0"/>
                          <stop offset="100%" stop-color="#0878b0"/>
                        </linearGradient>
                        <linearGradient id="dpBldg" x1="0" y1="0" x2="1" y2="0">
                          <stop offset="0%"   stop-color="#e8a060"/>
                          <stop offset="100%" stop-color="#b87030"/>
                        </linearGradient>
                        <linearGradient id="dpPath" x1="0" y1="0" x2="0" y2="1">
                          <stop offset="0%"   stop-color="#dcc888"/>
                          <stop offset="100%" stop-color="#b89848"/>
                        </linearGradient>
                        <linearGradient id="dpDeck" x1="0" y1="0" x2="0" y2="1">
                          <stop offset="0%"   stop-color="#f0e0b0"/>
                          <stop offset="100%" stop-color="#d8c888"/>
                        </linearGradient>
                        <radialGradient id="dpSun" cx="80%" cy="0%" r="70%">
                          <stop offset="0%"   stop-color="rgba(255,240,160,0.22)"/>
                          <stop offset="100%" stop-color="rgba(255,240,160,0)"/>
                        </radialGradient>
                      </defs>

                      <!-- ══ 하늘 ══ -->
                      <rect x="0" y="0" width="100" height="27" fill="url(#dpSky)"/>
                      <!-- 태양 -->
                      <circle cx="84" cy="7" r="6" fill="#ffe840" opacity="0.75"/>
                      <circle cx="84" cy="7" r="4" fill="#fff8b0" opacity="0.95"/>
                      <!-- 구름 1 (왼쪽) -->
                      <ellipse cx="16" cy="8"  rx="11" ry="4"   fill="white" opacity="0.96"/>
                      <ellipse cx="11" cy="9"  rx="7"  ry="3.5" fill="white"/>
                      <ellipse cx="21" cy="9"  rx="7"  ry="3"   fill="white"/>
                      <ellipse cx="16" cy="6"  rx="6"  ry="3"   fill="white" opacity="0.9"/>
                      <!-- 구름 2 (오른쪽) -->
                      <ellipse cx="56" cy="6"  rx="10" ry="3.5" fill="white" opacity="0.88"/>
                      <ellipse cx="51" cy="7"  rx="7"  ry="3"   fill="white" opacity="0.9"/>
                      <ellipse cx="61" cy="7"  rx="6"  ry="2.5" fill="white" opacity="0.85"/>

                      <!-- ══ 배경 절벽/언덕 ══ -->
                      <ellipse cx="8"   cy="27" rx="20" ry="9"  fill="#70b828"/>
                      <ellipse cx="35"  cy="25" rx="28" ry="9"  fill="#88cc30"/>
                      <ellipse cx="68"  cy="26" rx="26" ry="8"  fill="#78c028"/>
                      <ellipse cx="96"  cy="28" rx="18" ry="7"  fill="#68b020"/>
                      <!-- 언덕 하이라이트 -->
                      <ellipse cx="35"  cy="22" rx="16" ry="4"  fill="rgba(160,240,80,0.35)"/>
                      <ellipse cx="68"  cy="23" rx="12" ry="3"  fill="rgba(160,240,80,0.28)"/>

                      <!-- ══ 배경 야자수 (원거리) ══ -->
                      <!-- 야자 L1 -->
                      <rect x="2"  y="14" width="2"  height="13" fill="#9b6828" rx="1"/>
                      <ellipse cx="3"  cy="14" rx="7" ry="2.8" fill="#308020" transform="rotate(-25,3,14)"/>
                      <ellipse cx="3"  cy="13" rx="8" ry="2.5" fill="#40a028" transform="rotate(12,3,13)"/>
                      <ellipse cx="3"  cy="15" rx="6" ry="2.3" fill="#287018" transform="rotate(-40,3,15)"/>
                      <!-- 야자 L2 -->
                      <rect x="15" y="16" width="2"  height="11" fill="#8b5820" rx="1"/>
                      <ellipse cx="16" cy="16" rx="6" ry="2.5" fill="#288018" transform="rotate(20,16,16)"/>
                      <ellipse cx="16" cy="15" rx="7" ry="2.2" fill="#38a022" transform="rotate(-10,16,15)"/>
                      <!-- 야자 R1 -->
                      <rect x="83" y="13" width="2"  height="14" fill="#9b6828" rx="1"/>
                      <ellipse cx="84" cy="13" rx="7" ry="2.8" fill="#1a7018" transform="rotate(15,84,13)"/>
                      <ellipse cx="84" cy="12" rx="8" ry="2.5" fill="#2a9022" transform="rotate(-20,84,12)"/>
                      <ellipse cx="84" cy="14" rx="6" ry="2.3" fill="#186015" transform="rotate(38,84,14)"/>
                      <!-- 야자 R2 -->
                      <rect x="95" y="15" width="2"  height="12" fill="#8b5820" rx="1"/>
                      <ellipse cx="96" cy="15" rx="6" ry="2.5" fill="#207018" transform="rotate(-15,96,15)"/>
                      <ellipse cx="96" cy="14" rx="7" ry="2.2" fill="#309020" transform="rotate(22,96,14)"/>

                      <!-- ══ 잔디 메인 필드 ══ -->
                      <rect x="0" y="25" width="100" height="75" fill="url(#dpGrass)"/>
                      <!-- 원근 오버레이 -->
                      <rect x="0" y="25" width="100" height="14" fill="rgba(180,240,100,0.3)"/>
                      <rect x="0" y="85" width="100" height="15" fill="rgba(0,0,0,0.10)"/>
                      <!-- 햇빛 오버레이 -->
                      <rect x="0" y="0" width="100" height="100" fill="url(#dpSun)"/>

                      <!-- ══ 산책로 ══ -->
                      <!-- 가로 메인 패스 -->
                      <rect x="0"  y="63" width="100" height="6" fill="url(#dpPath)" opacity="0.75"/>
                      <!-- 세로 패스 (중앙) -->
                      <polygon points="44,25 56,25 60,100 40,100" fill="url(#dpPath)" opacity="0.65"/>

                      <!-- ══ 수영장 (왼쪽) ══ -->
                      <!-- 수영장 데크 (타일) -->
                      <rect x="1"  y="37" width="37" height="26" rx="3" fill="url(#dpDeck)"/>
                      <!-- 수영장 물 -->
                      <rect x="4"  y="40" width="31" height="20" rx="2" fill="url(#dpPool)"/>
                      <!-- 물 반짝임 -->
                      <ellipse cx="11" cy="46" rx="5"  ry="1.5" fill="rgba(255,255,255,0.45)"/>
                      <ellipse cx="24" cy="44" rx="4"  ry="1.2" fill="rgba(255,255,255,0.35)"/>
                      <ellipse cx="19" cy="54" rx="6"  ry="1.2" fill="rgba(255,255,255,0.28)"/>
                      <ellipse cx="30" cy="52" rx="3"  ry="1"   fill="rgba(255,255,255,0.38)"/>
                      <ellipse cx="8"  cy="56" rx="2.5" ry="0.8" fill="rgba(255,255,255,0.3)"/>
                      <!-- 수영장 레인 구분선 -->
                      <line x1="14" y1="40" x2="14" y2="60" stroke="rgba(255,255,255,0.5)" stroke-width="0.5" stroke-dasharray="1.5,2"/>
                      <line x1="24" y1="40" x2="24" y2="60" stroke="rgba(255,255,255,0.5)" stroke-width="0.5" stroke-dasharray="1.5,2"/>
                      <!-- 수영장 사다리 -->
                      <rect x="4"  y="40" width="1"  height="6" fill="#b0b8c8"/>
                      <rect x="4"  y="42" width="3"  height="0.6" fill="#b0b8c8"/>
                      <rect x="4"  y="44" width="3"  height="0.6" fill="#b0b8c8"/>
                      <!-- 수영장 표지판 -->
                      <rect x="6"  y="36" width="12" height="3" rx="1"   fill="#1868b8"/>
                      <rect x="6.5" y="36.5" width="11" height="2" rx="0.5" fill="#2888e0"/>
                      <!-- 데크 타일 그리드 -->
                      <line x1="1"  y1="43" x2="38" y2="43" stroke="rgba(200,180,120,0.4)" stroke-width="0.4"/>
                      <line x1="1"  y1="50" x2="38" y2="50" stroke="rgba(200,180,120,0.4)" stroke-width="0.4"/>
                      <line x1="1"  y1="57" x2="38" y2="57" stroke="rgba(200,180,120,0.4)" stroke-width="0.4"/>

                      <!-- ══ 어질리티 코스 (중앙) ══ -->
                      <!-- 어질리티 구역 잔디 -->
                      <rect x="36" y="28" width="36" height="35" rx="2" fill="#80c030" opacity="0.55"/>

                      <!-- 허들 1 -->
                      <rect x="38" y="46" width="1"  height="8"  fill="#e83028"/>
                      <rect x="46" y="46" width="1"  height="8"  fill="#e83028"/>
                      <rect x="38" y="46" width="9"  height="1.8" fill="#e83028"/>
                      <rect x="38" y="48.5" width="9"  height="1.5" fill="#f8e020"/>
                      <rect x="38" y="51" width="9"  height="1.5" fill="#e83028"/>

                      <!-- 허들 2 (작음) -->
                      <rect x="50" y="36" width="1"  height="7"  fill="#2080e0"/>
                      <rect x="57" y="36" width="1"  height="7"  fill="#2080e0"/>
                      <rect x="50" y="36" width="8"  height="1.5" fill="#2080e0"/>
                      <rect x="50" y="38" width="8"  height="1.5" fill="#f0f8ff"/>

                      <!-- 터널 -->
                      <ellipse cx="43" cy="58" rx="6"  ry="4"   fill="#f07828"/>
                      <ellipse cx="53" cy="59" rx="6"  ry="4"   fill="#f07828"/>
                      <rect x="43" y="55" width="10" height="8"  fill="#f07828"/>
                      <rect x="43" y="56" width="10" height="6"  fill="#d05e18"/>
                      <ellipse cx="43" cy="58" rx="4"  ry="2.8"  fill="#1a0e04"/>
                      <ellipse cx="53" cy="59" rx="4"  ry="2.8"  fill="#1a0e04"/>

                      <!-- 링/후프 -->
                      <circle cx="67" cy="44" r="5"   fill="none" stroke="#8820e8" stroke-width="2"/>
                      <rect x="65.5" y="48" width="1.5" height="7"  fill="#8820e8"/>
                      <rect x="68.5" y="48" width="1.5" height="7"  fill="#8820e8"/>
                      <rect x="64" y="55" width="8"   height="1"   fill="#8820e8"/>

                      <!-- 균형 빔 -->
                      <rect x="37" y="33" width="15" height="2.5" rx="0.5" fill="#c07828" transform="rotate(-4,37,33)"/>
                      <rect x="37" y="33" width="15" height="1"   rx="0.5" fill="#e09840" transform="rotate(-4,37,33)"/>
                      <rect x="37" y="35" width="2"  height="4"   fill="#a06020"/>
                      <rect x="50" y="34" width="2"  height="5"   fill="#a06020"/>

                      <!-- 테니스볼 -->
                      <circle cx="40" cy="65" r="1.6" fill="#c8e018"/>
                      <path d="M39,64.5 Q40,63.5 41,64.5" stroke="#88a010" stroke-width="0.4" fill="none"/>
                      <circle cx="55" cy="38" r="1.6" fill="#c8e018"/>
                      <circle cx="70" cy="60" r="1.6" fill="#c8e018"/>
                      <circle cx="42" cy="31" r="1.4" fill="#c8e018"/>

                      <!-- 프리스비 -->
                      <ellipse cx="47" cy="53" rx="3.5" ry="1.2" fill="#3040f0"/>
                      <ellipse cx="47" cy="53" rx="2.5" ry="0.7" fill="#5060ff"/>

                      <!-- ══ 휴식 공간 ══ -->
                      <!-- 파라솔 1 (주황) -->
                      <line x1="71" y1="60" x2="71" y2="73" stroke="#9b6020" stroke-width="0.9"/>
                      <ellipse cx="71" cy="60" rx="7"  ry="2.8" fill="#f05830"/>
                      <ellipse cx="71" cy="60" rx="5.5" ry="2"   fill="#f87050"/>
                      <path d="M71,60 L64,62" stroke="#d03818" stroke-width="0.8"/>
                      <path d="M71,60 L78,62" stroke="#d03818" stroke-width="0.8"/>
                      <path d="M71,60 L68,58" stroke="#d03818" stroke-width="0.8"/>
                      <path d="M71,60 L74,58" stroke="#d03818" stroke-width="0.8"/>
                      <!-- 벤치 -->
                      <rect x="63" y="68" width="10" height="3" rx="1"   fill="#c07828"/>
                      <rect x="63" y="68" width="10" height="1" rx="0.5" fill="#e09840"/>
                      <rect x="64" y="71" width="1.5" height="3.5" fill="#8b5020"/>
                      <rect x="71" y="71" width="1.5" height="3.5" fill="#8b5020"/>

                      <!-- 파라솔 2 (파랑) -->
                      <line x1="77" y1="68" x2="77" y2="79" stroke="#204888" stroke-width="0.9"/>
                      <ellipse cx="77" cy="68" rx="6"  ry="2.4" fill="#2860c8"/>
                      <ellipse cx="77" cy="68" rx="4.5" ry="1.7" fill="#4080e0"/>
                      <!-- 개 침대 -->
                      <ellipse cx="74" cy="75" rx="5"  ry="3"   fill="#e88040" opacity="0.9"/>
                      <ellipse cx="74" cy="74" rx="4"  ry="2"   fill="#f0a060"/>

                      <!-- 음수대 -->
                      <rect x="80" y="62" width="3.5" height="7" rx="1"   fill="#9898b0"/>
                      <ellipse cx="81.75" cy="62" rx="3" ry="1" fill="#c0c0d0"/>
                      <ellipse cx="81.75" cy="62" rx="1.2" ry="0.5" fill="#70c8e8"/>

                      <!-- ══ 펫 데이케어 건물 ══ -->
                      <!-- 건물 그림자 -->
                      <rect x="72" y="30" width="27" height="28" rx="2" fill="rgba(0,0,0,0.15)"/>
                      <!-- 건물 본체 -->
                      <rect x="71" y="29" width="27" height="27" rx="2" fill="url(#dpBldg)"/>
                      <!-- 지붕 -->
                      <polygon points="71,29 98,29 98,25 84.5,20 71,25" fill="#e02828"/>
                      <polygon points="71,25 98,25 98,27 71,27" fill="#f03838"/>
                      <!-- 건물 오른쪽 측면 음영 -->
                      <rect x="94" y="29" width="4" height="27" fill="#905020"/>
                      <!-- 창문 1 -->
                      <rect x="74" y="32" width="7"  height="6"  rx="1" fill="#80c8f0"/>
                      <rect x="74.5" y="32.5" width="6"  height="5"  rx="0.5" fill="#b0e0ff"/>
                      <rect x="77.5" y="32.5" width="0.6" height="5"  fill="rgba(80,150,200,0.4)"/>
                      <rect x="74.5" y="35"   width="6"  height="0.6" fill="rgba(80,150,200,0.4)"/>
                      <!-- 창문 2 -->
                      <rect x="85" y="32" width="7"  height="6"  rx="1" fill="#80c8f0"/>
                      <rect x="85.5" y="32.5" width="6"  height="5"  rx="0.5" fill="#b0e0ff"/>
                      <rect x="88.5" y="32.5" width="0.6" height="5"  fill="rgba(80,150,200,0.4)"/>
                      <!-- 문 -->
                      <rect x="80" y="42" width="9"  height="13" rx="1" fill="#7b4218"/>
                      <rect x="80.5" y="42.5" width="8"  height="12" rx="0.5" fill="#9b5828"/>
                      <rect x="80.5" y="42.5" width="3.5" height="12" fill="#8b4e22"/>
                      <circle cx="87.5" cy="49" r="0.8" fill="#f0c020"/>
                      <!-- 문 계단 -->
                      <rect x="79" y="54" width="11" height="2"  rx="0.5" fill="#c8a860"/>
                      <rect x="78" y="56" width="13" height="1.5" rx="0.5" fill="#b89040"/>
                      <!-- 건물 간판 -->
                      <rect x="73" y="28" width="22" height="4" rx="1"   fill="#f8f0c0"/>
                      <rect x="73.5" y="28.5" width="21" height="3" rx="0.5" fill="#ffe880"/>
                      <circle cx="78" cy="30" r="1.2" fill="#e84040"/>
                      <circle cx="81" cy="30" r="1.2" fill="#40c840"/>

                      <!-- ══ 도그 케넬 ══ -->
                      <!-- 케넬 1 -->
                      <rect x="72" y="58" width="12" height="11" rx="1"  fill="#d09040"/>
                      <polygon points="72,58 84,58 78,53" fill="#e02020"/>
                      <rect x="72" y="58" width="12" height="1" fill="#f04040"/>
                      <rect x="75" y="62" width="5"  height="7"  rx="0.5" fill="#7b3e18"/>
                      <circle cx="77" cy="66" r="0.6" fill="#f0c020"/>
                      <rect x="72" y="68" width="12" height="1" fill="#b07030"/>
                      <!-- 케넬 2 -->
                      <rect x="87" y="60" width="11" height="10" rx="1"  fill="#c88838"/>
                      <polygon points="87,60 98,60 92.5,55.5" fill="#d01818"/>
                      <rect x="89.5" y="64" width="4"  height="6"  rx="0.5" fill="#6b3418"/>
                      <circle cx="91" cy="67" r="0.5" fill="#f0c020"/>

                      <!-- ══ 나무/관목 (전경) ══ -->
                      <!-- 관목 (수영장 옆) -->
                      <ellipse cx="2"  cy="37" rx="5"  ry="4"   fill="#dd88cc" opacity="0.92"/>
                      <ellipse cx="2"  cy="36" rx="4"  ry="3"   fill="#f0a0dd"/>
                      <ellipse cx="5"  cy="38" rx="4"  ry="3"   fill="#cc6699" opacity="0.85"/>
                      <!-- 관목 (건물 옆) -->
                      <ellipse cx="70" cy="40" rx="5"  ry="3.5" fill="#44cc44" opacity="0.9"/>
                      <ellipse cx="70" cy="39" rx="4"  ry="2.5" fill="#55dd55"/>
                      <ellipse cx="73" cy="41" rx="3.5" ry="3"   fill="#f0c820" opacity="0.75"/>
                      <!-- 꽃 관목 (오른쪽) -->
                      <ellipse cx="63" cy="67" rx="4.5" ry="3.5" fill="#ff5588" opacity="0.88"/>
                      <ellipse cx="63" cy="66" rx="3.5" ry="2.5" fill="#ff78aa"/>
                      <ellipse cx="60" cy="68" rx="3"  ry="2.5" fill="#ff4477" opacity="0.8"/>
                      <!-- 야자수 (중앙 큰 것) -->
                      <rect x="49" y="25" width="2.5" height="20" fill="#a06828" rx="1.2"/>
                      <ellipse cx="50" cy="25" rx="9"  ry="3.5" fill="#309020" transform="rotate(-22,50,25)"/>
                      <ellipse cx="50" cy="24" rx="10" ry="3"   fill="#3ab828" transform="rotate(14,50,24)"/>
                      <ellipse cx="50" cy="26" rx="8"  ry="3"   fill="#287818" transform="rotate(-38,50,26)"/>
                      <ellipse cx="50" cy="23" rx="7"  ry="2.5" fill="#40c830" transform="rotate(5,50,23)"/>
                      <!-- 코코넛 -->
                      <circle cx="48.5" cy="27" r="1.2" fill="#8b5e28"/>
                      <circle cx="51.5" cy="26.5" r="1.1" fill="#7a5020"/>

                      <!-- ══ 지면 디테일 ══ -->
                      <!-- 꽃 (산발) -->
                      <circle cx="31" cy="71" r="1.2" fill="#ffee20"/>
                      <circle cx="31" cy="71" r="0.5" fill="#ff8800"/>
                      <circle cx="46" cy="80" r="1.2" fill="#ff99cc"/>
                      <circle cx="46" cy="80" r="0.5" fill="#ff44aa"/>
                      <circle cx="60" cy="76" r="1.2" fill="#ffffff"/>
                      <circle cx="60" cy="76" r="0.5" fill="#ffe870"/>
                      <circle cx="38" cy="87" r="1"   fill="#ff6644"/>
                      <circle cx="25" cy="82" r="1"   fill="#88ff44"/>
                      <circle cx="55" cy="90" r="1.1" fill="#ffcc00"/>
                      <circle cx="68" cy="85" r="1"   fill="#dd88ff"/>

                      <!-- 풀잎 -->
                      <rect x="33" y="76" width="1" height="4" fill="#44cc22"/>
                      <rect x="34" y="74" width="1" height="5" fill="#55dd33"/>
                      <rect x="35" y="76" width="1" height="4" fill="#44cc22"/>
                      <rect x="50" y="70" width="1" height="4" fill="#44bb22"/>
                      <rect x="51" y="68" width="1" height="5" fill="#55cc33"/>
                      <rect x="57" y="83" width="1" height="4" fill="#44bb22"/>
                      <rect x="58" y="81" width="1" height="5" fill="#55cc33"/>

                      <!-- 돌 -->
                      <ellipse cx="40" cy="78" rx="2.5" ry="1.2" fill="#b0a880" opacity="0.9"/>
                      <ellipse cx="40" cy="77.5" rx="1.8" ry="0.7" fill="#d0c8a0"/>
                      <ellipse cx="58" cy="85" rx="2"   ry="1"   fill="#a09870" opacity="0.85"/>
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
                                <!-- 싸이월드 도트 미니미 -->
                                <svg id="butler-stage-avatar" viewBox="0 0 20 28" width="112" height="112" xmlns="http://www.w3.org/2000/svg" shape-rendering="crispEdges" style="image-rendering:pixelated;filter:drop-shadow(0 3px 6px rgba(0,0,0,0.3))">
                                  <!-- ░░ 싸이월드 도트 미니미 (20×28 픽셀그리드) ░░ -->

                                  <!-- 바닥 그림자 -->
                                  <ellipse cx="10" cy="27.5" rx="7" ry="1" fill="rgba(0,0,0,0.22)" shape-rendering="auto"/>

                                  <!-- ══ 왕관 ══ -->
                                  <!-- 왕관 밴드 -->
                                  <rect x="4" y="1" width="11" height="3" fill="#f0c030"/>
                                  <rect x="3" y="2" width="13" height="2" fill="#f0c030"/>
                                  <!-- 왕관 어두운 아랫줄 -->
                                  <rect x="3" y="3" width="13" height="1" fill="#c09010"/>
                                  <!-- 왕관 밴드 하이라이트 -->
                                  <rect x="4" y="1" width="11" height="1" fill="#ffe060"/>
                                  <!-- 보석 -->
                                  <rect x="4"  y="0" width="2" height="2" fill="#ff3333"/>
                                  <rect x="8"  y="0" width="3" height="2" fill="#33cc33"/>
                                  <rect x="13" y="0" width="2" height="2" fill="#4444ff"/>
                                  <!-- 보석 하이라이트 -->
                                  <rect x="4"  y="0" width="1" height="1" fill="#ff9999"/>
                                  <rect x="8"  y="0" width="1" height="1" fill="#99ff99"/>
                                  <rect x="13" y="0" width="1" height="1" fill="#9999ff"/>

                                  <!-- ══ 머리카락 ══ -->
                                  <!-- 윗머리 -->
                                  <rect x="3" y="3"  width="13" height="3" fill="#3d2a1a"/>
                                  <!-- 옆머리 왼쪽 (밝은 면) -->
                                  <rect x="2" y="5"  width="3"  height="7" fill="#4e3520"/>
                                  <!-- 옆머리 오른쪽 (어두운 면) -->
                                  <rect x="14" y="5" width="3"  height="7" fill="#2a1a0a"/>
                                  <!-- 머리 하이라이트 -->
                                  <rect x="6" y="3"  width="5"  height="1" fill="#6b4a28"/>

                                  <!-- ══ 얼굴 ══ -->
                                  <rect x="4" y="5"  width="11" height="8" fill="#ffcc99"/>
                                  <!-- 얼굴 오른쪽 측면 음영 -->
                                  <rect x="14" y="5" width="1"  height="8" fill="#e8aa77"/>

                                  <!-- 눈 (왼쪽) -->
                                  <rect x="5"  y="7" width="3" height="3" fill="#ffffff"/>
                                  <rect x="6"  y="8" width="2" height="2" fill="#1a1008"/>
                                  <rect x="6"  y="8" width="1" height="1" fill="#ffffff"/>
                                  <!-- 눈 (오른쪽) -->
                                  <rect x="11" y="7" width="3" height="3" fill="#ffffff"/>
                                  <rect x="12" y="8" width="2" height="2" fill="#1a1008"/>
                                  <rect x="12" y="8" width="1" height="1" fill="#ffffff"/>
                                  <!-- 눈썹 -->
                                  <rect x="5"  y="6" width="3" height="1" fill="#3d2a1a"/>
                                  <rect x="11" y="6" width="3" height="1" fill="#3d2a1a"/>

                                  <!-- 볼터치 -->
                                  <rect x="4"  y="10" width="2" height="1" fill="#ffaaaa"/>
                                  <rect x="13" y="10" width="2" height="1" fill="#ffaaaa"/>

                                  <!-- 입 -->
                                  <rect x="7"  y="11" width="1" height="1" fill="#cc6644"/>
                                  <rect x="8"  y="12" width="3" height="1" fill="#cc6644"/>
                                  <rect x="11" y="11" width="1" height="1" fill="#cc6644"/>

                                  <!-- ══ 목 ══ -->
                                  <rect x="8" y="13" width="3" height="2" fill="#ffcc99"/>

                                  <!-- ══ 몸통/셔츠 ══ -->
                                  <!-- 왼팔 (밝은 면) -->
                                  <rect x="2"  y="14" width="3" height="6" fill="#8898ff"/>
                                  <!-- 손 왼쪽 -->
                                  <rect x="2"  y="20" width="3" height="2" fill="#ffcc99"/>
                                  <!-- 몸통 -->
                                  <rect x="4"  y="14" width="11" height="7" fill="#7080f0"/>
                                  <!-- 몸통 왼쪽 하이라이트 -->
                                  <rect x="5"  y="15" width="2" height="4" fill="#9aaeff"/>
                                  <!-- 몸통 오른쪽 음영 -->
                                  <rect x="13" y="14" width="2" height="7" fill="#4a56c8"/>
                                  <!-- 오른팔 (어두운 면) -->
                                  <rect x="14" y="14" width="3" height="6" fill="#5060d0"/>
                                  <!-- 손 오른쪽 -->
                                  <rect x="14" y="20" width="3" height="2" fill="#e8aa77"/>

                                  <!-- 칼라 -->
                                  <rect x="7"  y="14" width="2" height="1" fill="#ffffff"/>
                                  <rect x="10" y="14" width="2" height="1" fill="#ffffff"/>
                                  <!-- 넥타이 -->
                                  <rect x="9"  y="14" width="1" height="1" fill="#ff4444"/>
                                  <rect x="8"  y="15" width="3" height="5" fill="#ff4444"/>
                                  <!-- 넥타이 음영 -->
                                  <rect x="10" y="16" width="1" height="3" fill="#cc2222"/>
                                  <!-- 넥타이 하이라이트 -->
                                  <rect x="8"  y="15" width="1" height="2" fill="#ff8888"/>

                                  <!-- ══ 바지 ══ -->
                                  <rect x="5"  y="21" width="4" height="4" fill="#3344aa"/>
                                  <rect x="10" y="21" width="4" height="4" fill="#3344aa"/>
                                  <!-- 바지 음영 (오른쪽 다리) -->
                                  <rect x="12" y="21" width="2" height="4" fill="#223388"/>
                                  <!-- 바지 하이라이트 (왼쪽 다리) -->
                                  <rect x="5"  y="21" width="1" height="4" fill="#5566cc"/>

                                  <!-- ══ 신발 ══ -->
                                  <rect x="4"  y="25" width="6" height="2" fill="#222222"/>
                                  <rect x="9"  y="25" width="6" height="2" fill="#222222"/>
                                  <!-- 신발 발끝 -->
                                  <rect x="3"  y="25" width="1" height="2" fill="#222222"/>
                                  <rect x="15" y="25" width="1" height="2" fill="#111111"/>
                                  <!-- 신발 광택 -->
                                  <rect x="4"  y="25" width="2" height="1" fill="#444444"/>
                                  <rect x="9"  y="25" width="2" height="1" fill="#333333"/>
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
