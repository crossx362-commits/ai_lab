const MYPET_TEMPLATE = `
<div class="space-y-4 animate-fade-in">

    <!-- 오늘 카드: 알림(건강 다이제스트·추억)·챙길 것·케어 요약·운세를 한 카드로 통합
         (2026-07-25 오너 지시 — 알림 카드와 요약 카드가 같은 '오늘' 정보라 두 장으로
         나뉠 이유가 없었다). divide-y는 hidden이 아닌 형제 사이에만 선을 그으므로
         비어서 hidden 처리된 배너는 구분선을 만들지 않는다 — 각 배너 렌더러가
         내용 없을 때 host.hidden = true로 두는 규약에 의존한다. -->
    <div id="home-today-card" class="card-modern divide-y divide-gray-100 overflow-hidden">
        <!-- 🩺 홈 건강 조기감지 다이제스트 (health-digest.js가 채움, 위험 시에만 노출) -->
        <div id="health-digest-banner"></div>

        <!-- 📅 추억 다시보기 자동 회고 (memory-flashback.js가 채움, 데이터 있을 때만 노출) -->
        <div id="memory-flashback-banner"></div>

        <!-- 🌈 무지개다리 추모 공간 (memorial.js가 채움, 펫이 추모 모드일 때만 노출) -->
        <div id="memorial-banner"></div>

        <!-- 💊 오늘의 투약·케어 체크는 건강 탭 '투약·복약' 그룹으로 이동(2026-07-26 오너 지시).
             마이펫에는 아래 '오늘의 케어 요약'의 💊 칸이 남은 건수만 보여주고, 실제 목록은
             건강 탭 한 곳에서만 관리한다. -->

        <!-- 📌 선제 케어 넛지 '오늘 챙길 것' (care-nudge.js가 채움, 챙길 게 있을 때만 노출) -->
        <div id="care-nudge-banner"></div>

        <!-- 📊 오늘의 케어 통합 요약: 산책·급여·케어·기분·이상신호 한 줄 (today-care-card.js) -->
        <div id="today-care-strip"></div>

        <!-- 날짜/날씨는 2026-07-24부터 전 탭 공통 상단 헤더에 표시(index.html) -->

        <!-- 오늘의 운세 (집사 + 펫) -->
        <div class="grid grid-cols-1 sm:grid-cols-2 divide-y sm:divide-y-0 sm:divide-x divide-gray-100">
            <div class="bg-brand-50/40 px-4 py-3 flex items-start gap-2.5">
                <span class="text-base leading-none mt-0.5 shrink-0">🧔</span>
                <div class="min-w-0">
                    <span class="block text-[11px] font-bold text-brand-600 mb-0.5">집사 오늘의 운세</span>
                    <p id="mypet-butler-fortune-text" class="text-xs font-medium text-gray-700 leading-relaxed keep-all"><span class="skeleton" style="display:inline-block;width:6rem;height:0.7rem;vertical-align:middle" aria-label="로딩 중"></span></p>
                </div>
            </div>
            <div id="mypet-fortune-cell" class="bg-amber-50/40 px-4 py-3 flex items-start gap-2.5">
                <span class="text-base leading-none mt-0.5 shrink-0">🐾</span>
                <div class="min-w-0">
                    <span class="block text-[11px] font-bold text-amber-600 mb-0.5">펫 오늘의 운세</span>
                    <p id="mypet-fortune-text" class="text-xs font-medium text-gray-700 leading-relaxed keep-all"><span class="skeleton" style="display:inline-block;width:6rem;height:0.7rem;vertical-align:middle" aria-label="로딩 중"></span></p>
                </div>
            </div>
        </div>

        <!-- 맞춤 추천 TOP3는 펫라이프 탭(#reco-card-shop)으로 일원화 — 마이펫은 오늘의
             케어·상태에 집중(2026-07-25 오너 지시, 두 탭 중복 노출 해소) -->
    </div>

    <!-- ===== 방 + 사이드바를 나란히 배치 ===== -->
    <div class="grid grid-cols-1 lg:grid-cols-12 gap-4 items-stretch">

    <!-- 왼쪽: 댕이의 하루 방 -->
    <div class="lg:col-span-9 space-y-4">

        <!-- 댕이의 하루 방 -->
        <div id="pet-room-card" class="card-modern overflow-hidden">

            <!-- 헤더 -->
            <div class="px-6 pt-5 pb-4 border-b border-gray-100">
                <div class="flex flex-col lg:flex-row lg:items-center gap-4">
                    <div class="min-w-0 lg:shrink-0">
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
                    <div class="flex items-center gap-2 w-full lg:flex-1 lg:min-w-0">
                        <!-- 사주 + 영혼 조화도 통합 카드(2026-07-25 오너 지시) — 원래 사주 분석은
                             여기, 조화도 점수는 우측 사이드바에 따로 있었는데 둘 다 같은 진단에서
                             나온 결과인데다 집사·펫 년생과 진입 버튼까지 중복이었다. 한 카드로 합치고
                             room-harmony.js가 값을 쓰는 ID는 전부 그대로 보존한다. -->
                        <!-- 표면은 점수 요약만, 상세는 클릭 시 모달(오너 지시 2026-07-25 "단순하게
                             보이게 하고 클릭하면 작은창 떠서 자세히") -->
                        <button type="button" id="room-saju-card" onclick="openSajuHarmonyDetail()"
                            class="flex-1 min-w-0 text-left bg-gradient-to-br from-brand-50 to-rose-50 border border-brand-200 rounded-xl px-3.5 py-3 shadow-soft hover:border-brand-300 transition-colors">
                            <div class="flex items-center justify-between gap-2">
                                <span class="text-[10px] font-black text-brand-700 shrink-0">🔮 사주 · <span id="harmony-widget-icon">💖</span> 조화도</span>
                                <div class="flex items-center gap-1.5 shrink-0">
                                    <span id="harmony-widget-score" class="text-sm font-black text-rose-600">--점</span>
                                    <i class="fa-solid fa-chevron-right text-brand-300 text-[9px]"></i>
                                </div>
                            </div>
                            <div id="harmony-widget-title" class="text-[9px] font-bold text-rose-700 mt-0.5">조화도를 측정해보세요</div>
                        </button>
                        <!-- 펫 추가 (주요 기능 상시 노출 — 설정 메뉴 안에만 있던 것을 헤더로 승격) -->
                        <button onclick="openPetRegistrationModal()" id="room-add-pet-btn" title="펫 추가"
                            class="h-9 px-3 rounded-xl bg-brand-500 hover:bg-brand-600 text-white flex items-center gap-1.5 transition-all shrink-0 shadow-soft">
                            <i class="fa-solid fa-plus text-sm"></i>
                            <span class="text-xs font-black">펫 추가</span>
                        </button>
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
                <div class="grid grid-cols-4 gap-2">
                    <button onclick="openNotebookModal()"
                        class="room-settings-action flex flex-col items-center gap-1.5 p-2.5 bg-white rounded-xl border border-amber-100 hover:border-brand-300 hover:bg-brand-50 transition-all">
                        <i class="fa-solid fa-address-book text-brand-500 text-lg"></i>
                        <span class="text-[10px] font-black text-gray-600">생활수첩</span>
                    </button>
                    <button onclick="toggleButlerProfileEdit()"
                        class="room-settings-action flex flex-col items-center gap-1.5 p-2.5 bg-white rounded-xl border border-amber-100 hover:border-brand-300 hover:bg-brand-50 transition-all">
                        <i class="fa-solid fa-user-pen text-brand-500 text-lg"></i>
                        <span class="text-[10px] font-black text-gray-600">집사 설정</span>
                    </button>
                    <button onclick="togglePetProfileEdit()"
                        class="room-settings-action flex flex-col items-center gap-1.5 p-2.5 bg-white rounded-xl border border-amber-100 hover:border-amber-300 hover:bg-amber-50 transition-all">
                        <i class="fa-solid fa-paw text-amber-500 text-lg"></i>
                        <span class="text-[10px] font-black text-gray-600">방 설정</span>
                    </button>
                    <button onclick="openPetRegistrationModal()"
                        class="room-settings-action flex flex-col items-center gap-1.5 p-2.5 bg-white rounded-xl border border-amber-100 hover:border-brand-300 hover:bg-brand-50 transition-all">
                        <i class="fa-solid fa-plus text-brand-500 text-lg"></i>
                        <span class="text-[10px] font-black text-gray-600">펫 추가</span>
                    </button>
                </div>

                <!-- 집사 설정 패널 -->
                <div id="butler-profile-editor-panel" class="hidden mt-3 bg-white border border-brand-100 p-4 rounded-2xl space-y-3 text-xs">
                    <div class="flex items-center justify-between pb-1 border-b border-gray-100">
                        <span class="font-black text-gray-700"><i class="fa-solid fa-user-gear mr-1 text-brand-500"></i>집사 프로필</span>
                        <button onclick="toggleButlerProfileEdit()" class="text-gray-300 hover:text-gray-500" aria-label="닫기"><i class="fa-solid fa-xmark"></i></button>
                    </div>
                    <div class="flex items-center gap-3">
                        <div class="shrink-0">
                            <div class="w-14 h-14 rounded-full bg-brand-50 flex items-center justify-center text-2xl border-2 border-brand-100 overflow-hidden relative cursor-pointer group" onclick="document.getElementById('butler-photo-upload').click()">
                                <span id="settings-avatar-disp">🧔</span>
                                <img loading="lazy" id="settings-avatar-image" class="hidden w-full h-full object-cover">
                                <div class="absolute inset-0 bg-black/40 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity rounded-full">
                                    <i class="fa-solid fa-camera text-white text-xs"></i>
                                </div>
                            </div>
                            <input type="file" id="butler-photo-upload" accept="image/*" class="hidden" onchange="uploadButlerProfilePhoto(event)">
                        </div>
                        <div class="flex-1 space-y-2">
                            <input type="text" id="settings-user-nickname" placeholder="닉네임" class="w-full border rounded-lg px-2.5 py-1.5 outline-none focus:border-brand-400 text-xs">
                            <input type="email" id="settings-user-email" placeholder="이메일" class="w-full border rounded-lg px-2.5 py-1.5 outline-none focus:border-brand-400 text-xs">
                            <div class="flex gap-1.5 text-base">
                                <button onclick="changeUserAvatar('🧔')" class="hover:scale-125 transition-transform">🧔</button>
                                <button onclick="changeUserAvatar('👩')" class="hover:scale-125 transition-transform">👩</button>
                                <button onclick="changeUserAvatar('🧑')" class="hover:scale-125 transition-transform">🧑</button>
                                <button onclick="changeUserAvatar('👵')" class="hover:scale-125 transition-transform">👵</button>
                                <button onclick="changeUserAvatar('👨‍🌾')" class="hover:scale-125 transition-transform">👨‍🌾</button>
                            </div>
                        </div>
                    </div>
                    <button onclick="saveUserProfile()" class="w-full bg-brand-500 hover:bg-brand-600 text-white font-bold text-xs py-2 rounded-xl transition-all">저장</button>
                </div>

                <!-- 방 설정 패널 -->
                <div id="pet-profile-editor-panel" class="hidden mt-3 bg-white border border-amber-100 p-4 rounded-2xl space-y-3 text-xs">
                    <div class="flex items-center justify-between pb-1 border-b border-gray-100">
                        <span class="font-black text-gray-700"><i class="fa-solid fa-paw mr-1 text-amber-500"></i>방 & 펫 설정</span>
                        <button onclick="togglePetProfileEdit()" class="text-gray-300 hover:text-gray-500" aria-label="닫기"><i class="fa-solid fa-xmark"></i></button>
                    </div>
                    <div class="room-layout-picker">
                        <button id="room-layout-living" onclick="setRoomLayoutForActivePet('living')" class="room-layout-option is-active">
                            <span class="room-layout-preview-card">
                                <span class="room-layout-preview living">
                                    <span class="preview-sofa"></span>
                                    <span class="preview-rug"></span>
                                    <span class="preview-line"></span>
                                    <span class="preview-pet left"></span>
                                    <span class="preview-pet right"></span>
                                </span>
                            </span>
                            <span class="room-layout-copy">
                                <strong>거실형</strong>
                                <small>차분한 여백</small>
                            </span>
                        </button>
                        <button id="room-layout-circle" onclick="setRoomLayoutForActivePet('circle')" class="room-layout-option">
                            <span class="room-layout-preview-card">
                                <span class="room-layout-preview circle">
                                    <span class="preview-line"></span>
                                    <span class="preview-pet left"></span>
                                    <span class="preview-pet right"></span>
                                    <span class="preview-pet top"></span>
                                </span>
                            </span>
                            <span class="room-layout-copy">
                                <strong>원형</strong>
                                <small>활동적인 배치</small>
                            </span>
                        </button>
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
                            <div class="flex gap-1.5">
                                <button onclick="changePetPresetPhoto('https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=300')" class="w-7 h-7 rounded-full overflow-hidden border-2 border-amber-100 hover:scale-110 transition-transform"><img loading="lazy" src="https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=100" class="w-full h-full object-cover"></button>
                                <button onclick="changePetPresetPhoto('https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba?auto=format&fit=crop&q=80&w=300')" class="w-7 h-7 rounded-full overflow-hidden border-2 border-amber-100 hover:scale-110 transition-transform"><img loading="lazy" src="https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba?auto=format&fit=crop&q=80&w=100" class="w-full h-full object-cover"></button>
                                <button onclick="changePetPresetPhoto('https://images.unsplash.com/photo-1583511655857-d19b40a7a54e?auto=format&fit=crop&q=80&w=300')" class="w-7 h-7 rounded-full overflow-hidden border-2 border-amber-100 hover:scale-110 transition-transform"><img loading="lazy" src="https://images.unsplash.com/photo-1583511655857-d19b40a7a54e?auto=format&fit=crop&q=80&w=100" class="w-full h-full object-cover"></button>
                                <button onclick="changePetPresetPhoto('https://images.unsplash.com/photo-1535268647977-a403b69fc756?auto=format&fit=crop&q=80&w=300')" class="w-7 h-7 rounded-full overflow-hidden border-2 border-amber-100 hover:scale-110 transition-transform"><img loading="lazy" src="https://images.unsplash.com/photo-1535268647977-a403b69fc756?auto=format&fit=crop&q=80&w=100" class="w-full h-full object-cover"></button>
                            </div>
                        </div>
                    </div>
                    <!-- 🌈 무지개다리 추모 모드 (P2, 나무 제안) — 함께한 날·기일/생일 안내 -->
                    <div class="pt-2 border-t border-gray-100 space-y-2">
                        <label class="flex items-center gap-2 cursor-pointer">
                            <input type="checkbox" id="settings-pet-memorial" onchange="toggleMemorialFields()" class="accent-indigo-500">
                            <span class="font-black text-gray-600">🌈 무지개다리 추모 모드</span>
                        </label>
                        <div id="settings-memorial-fields" class="hidden grid grid-cols-2 gap-2">
                            <label class="block">
                                <span class="block text-[10px] font-bold text-gray-400 mb-0.5">생일</span>
                                <input type="date" id="settings-pet-birthdate" class="w-full border rounded-lg px-2 py-1.5 outline-none focus:border-indigo-400 text-xs">
                            </label>
                            <label class="block">
                                <span class="block text-[10px] font-bold text-gray-400 mb-0.5">기일</span>
                                <input type="date" id="settings-pet-memorialdate" class="w-full border rounded-lg px-2 py-1.5 outline-none focus:border-indigo-400 text-xs">
                            </label>
                        </div>
                    </div>
                    <button onclick="savePetProfileAndRoom()" class="w-full bg-amber-500 hover:bg-amber-600 text-white font-bold text-xs py-2 rounded-xl transition-all">저장</button>
                </div>
            </div>

            <!-- 메인 콘텐츠 -->
            <div class="px-5 pb-5 space-y-4">

                <div id="petgame-root" class="w-full"></div>

                <!-- 펫 스탯바 + 코인 + 데일리 미션 (추모 모드에선 숨김 — 허기 게이지·먹이주기 미션은
                     무지개다리를 건넌 아이에게 부적절하다) -->
                <div id="pet-game-panel" class="bg-white border border-gray-100 rounded-2xl px-3 py-2.5 space-y-1.5">
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
                        <p id="butler-condition-desc" class="text-[10px] text-gray-500 font-medium leading-snug keep-all"><span class="skeleton" style="display:inline-block;width:6rem;height:0.7rem;vertical-align:middle" aria-label="로딩 중"></span></p>
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
                        <p id="pet-condition-desc" class="text-[10px] text-amber-800/70 font-medium leading-snug keep-all"><span class="skeleton" style="display:inline-block;width:6rem;height:0.7rem;vertical-align:middle" aria-label="로딩 중"></span></p>
                    </div>
                </div>

                <!-- 바로가기 아이콘 바 (일일 건강 로그는 건강 탭으로 이동) -->
                <div class="bg-gray-50 rounded-2xl border border-gray-100 px-3 py-2.5">
                    <div class="flex items-center justify-end">
                        <!-- 아이콘 버튼 -->
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
                            <button onclick="PublicProfile.open()" title="미아방지 QR"
                                class="w-9 h-9 rounded-xl bg-white border border-gray-200 hover:border-rose-300 hover:bg-rose-50 flex items-center justify-center transition-all">
                                <i class="fa-solid fa-qrcode text-rose-400 text-sm"></i>
                            </button>
                        </div>
                    </div>
                    <!-- 사주 조언 한 줄 -->
                    <p id="personalized-health-tip" class="text-[10px] text-gray-400 font-medium mt-2 pt-2 border-t border-gray-100 keep-all leading-relaxed">✨ 분석 중...</p>
                </div>

            </div>
        </div>

        <!-- 투약·정기예방 대시보드 + 건강수첩은 건강 탭으로 이동됨 -->

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

    <!-- 오른쪽: 조화도 + 챌린지/업적 통합 카드 (2026-07-16 정리 전엔 개별 박스 9개가 세로로 나열돼 있었다) -->
    <!-- 왼쪽 하루 방 카드보다 콘텐츠가 훨씬 길어 하단이 안 맞던 문제 → sticky+내부 스크롤로 정렬(2026-07-22) -->
    <div class="lg:col-span-3 space-y-2.5 lg:sticky lg:top-20 lg:self-start lg:max-h-[calc(100vh-6rem)] lg:overflow-y-auto">

        <!-- 조화도 위젯은 좌측 '사주 · 조화도' 통합 카드로 흡수(2026-07-25 오너 지시).
             집사·펫 년생과 진입 버튼이 사주 카드와 중복이라 한 카드로 합쳤다. -->

        <!-- 챌린지 & 업적 통합 카드 (각 항목은 achievements.js가 그대로 채움 — 겉박스만 하나로 합침) -->
        <div id="home-challenge-card" class="card-modern grid grid-cols-2 gap-2 p-2 lg:block lg:gap-0 lg:p-0 lg:divide-y lg:divide-gray-100 overflow-hidden">
            <div class="p-2.5"><div id="weekly-care-challenge">
                <div class="flex items-center gap-2 text-[10px] text-gray-400 font-bold">
                    <i class="fa-solid fa-trophy text-gray-300"></i>
                    <span>이번 주 케어 챌린지를 확인해보세요!</span>
                </div>
            </div></div>
            <div class="p-2.5"><div id="weekly-walk-challenge">
                <div class="flex items-center gap-2 text-[10px] text-gray-400 font-bold">
                    <i class="fa-solid fa-bullseye text-gray-300"></i>
                    <span>이번 주 산책 목표를 세워보세요!</span>
                </div>
            </div></div>
            <div class="p-2.5"><div id="walk-streak-banner">
                <div class="flex items-center gap-2 text-[10px] text-gray-400 font-bold">
                    <i class="fa-solid fa-fire text-gray-300"></i>
                    <span>산책을 시작하면 streak이 쌓여요!</span>
                </div>
            </div></div>
            <div class="p-2.5"><div id="buddy-streak-card">
                <div class="flex items-center gap-2 text-[10px] text-gray-400 font-bold">
                    <i class="fa-solid fa-people-arrows text-gray-300"></i>
                    <span>이웃과 함께 산책 스트릭을 쌓아보세요!</span>
                </div>
            </div></div>
            <div class="p-2.5"><div id="hood-challenge">
                <div class="flex items-center gap-2 text-[10px] text-gray-400 font-bold">
                    <i class="fa-solid fa-house-chimney text-gray-300"></i>
                    <span>동네 이웃들과 공동 목표에 도전해보세요!</span>
                </div>
            </div></div>
            <div class="p-2.5"><div id="training-mission-card">
                <div class="flex items-center gap-2 text-[10px] text-gray-400 font-bold">
                    <i class="fa-solid fa-graduation-cap text-gray-300"></i>
                    <span>맞춤 훈련 미션을 확인해보세요!</span>
                </div>
            </div></div>
            <div class="p-2.5"><div id="training-passport-card">
                <div class="flex items-center gap-2 text-[10px] text-gray-400 font-bold">
                    <i class="fa-solid fa-passport text-gray-300"></i>
                    <span>익힌 훈련을 패스포트에 기록해보세요!</span>
                </div>
            </div></div>
            <div class="p-2.5"><div id="daily-care-streak-banner">
                <div class="flex items-center gap-2 text-[10px] text-gray-400 font-bold">
                    <i class="fa-solid fa-calendar-check text-gray-300"></i>
                    <span>산책·급여·투약 중 하나만 완료해도 케어 스트릭이 시작돼요!</span>
                </div>
            </div></div>
            <div class="p-3 col-span-2 lg:col-span-1"><div id="daily-challenges"></div></div>
            <div class="p-2.5 col-span-2 lg:col-span-1"><div id="achievement-badges"></div></div>
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
                    class="flex-1 py-2 text-xs font-black rounded-xl bg-white text-brand-700 shadow-sm transition-all">
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
                <div id="premium-price-badge" class="bg-gradient-to-r from-brand-500 to-brand-600 text-white px-6 py-3 rounded-2xl text-center shadow-lg w-full">
                    <span id="premium-price-main" class="block text-2xl font-black">월 5,900원</span>
                    <span id="premium-price-sub" class="text-xs font-bold opacity-80">TTcare 대비 올인원 · 해지 언제든 가능</span>
                </div>
            </div>
            <!-- 혜택 -->
            <div class="grid grid-cols-2 gap-2">
                <div class="flex items-center gap-2 bg-brand-50 rounded-xl p-2.5">
                    <span class="text-lg">🏥</span>
                    <span class="text-[11px] font-black text-brand-700">AI 건강분석 무제한</span>
                </div>
                <div class="flex items-center gap-2 bg-brand-50 rounded-xl p-2.5">
                    <span class="text-lg">📖</span>
                    <span class="text-[11px] font-black text-brand-700">일기장 무제한 저장</span>
                </div>
                <div class="flex items-center gap-2 bg-brand-50 rounded-xl p-2.5">
                    <span class="text-lg">📊</span>
                    <span class="text-[11px] font-black text-brand-700">건강·일기 PDF 내보내기</span>
                </div>
                <div class="flex items-center gap-2 bg-brand-50 rounded-xl p-2.5">
                    <span class="text-lg">🎙️</span>
                    <span class="text-[11px] font-black text-brand-700">음성 문진 무제한</span>
                </div>
                <div id="premium-yearly-bonus" class="hidden flex items-center gap-2 bg-rose-50 rounded-xl p-2.5">
                    <span class="text-lg">📋</span>
                    <span class="text-[11px] font-black text-rose-700">건강 리포트 PDF 매월 자동 발송</span>
                </div>
                <div class="flex items-center gap-2 bg-brand-50 rounded-xl p-2.5">
                    <span class="text-lg">📅</span>
                    <span class="text-[11px] font-black text-brand-700">"1년 전 오늘" 추억 알림</span>
                </div>
                <div class="flex items-center gap-2 bg-brand-50 rounded-xl p-2.5">
                    <span class="text-lg">⚡</span>
                    <span class="text-[11px] font-black text-brand-700">우선 고객 지원</span>
                </div>
            </div>
            <div class="space-y-2">
                <button onclick="startStripeCheckout()"
                    class="w-full py-3.5 bg-gradient-to-r from-brand-500 to-brand-600 hover:from-brand-600 hover:to-brand-700 text-white font-black text-sm rounded-2xl transition-all shadow-lg active:scale-95">
                    💳 카드로 구독 시작
                </button>
                <button onclick="closePremiumModal()"
                    class="w-full py-2.5 text-gray-400 hover:text-gray-600 font-bold text-sm transition-colors">
                    나중에
                </button>
            </div>
        </div>
    </div>

    <!-- 🔮 사주 · 조화도 상세 모달 — 카드 표면은 점수 요약만 보이고 상세는 여기서 본다.
         room-harmony.js가 값을 쓰는 ID들이 여기 살아 있어야 하므로(숨겨져 있어도 textContent
         쓰기는 정상 동작) 이 블록을 제거하거나 ID를 바꾸지 말 것. -->
    <div id="saju-harmony-modal" class="fixed inset-0 bg-black/60 items-center justify-center z-[110] p-4 hidden">
        <div class="bg-white rounded-3xl p-5 max-w-sm w-full shadow-2xl relative border border-brand-100 max-h-[90vh] overflow-y-auto no-scrollbar">
            <button onclick="closeSajuHarmonyDetail()" class="absolute top-4 right-4 text-gray-400 hover:text-gray-600 outline-none" aria-label="닫기">
                <i class="fa-solid fa-xmark text-lg"></i>
            </button>
            <div class="text-center space-y-1 mb-4">
                <div class="w-12 h-12 bg-rose-50 rounded-full flex items-center justify-center text-2xl mx-auto shadow-inner">🔮</div>
                <h4 class="font-black text-gray-800 text-sm">사주 · 영혼 조화도</h4>
                <div class="flex items-center justify-center gap-2 pt-1">
                    <span id="room-saju-score" class="text-[10px] font-bold text-brand-700 bg-brand-50 px-2.5 py-1 rounded-full">미측정</span>
                </div>
            </div>
            <div class="space-y-3 text-xs">
                <div class="bg-brand-50/40 rounded-2xl p-3 border border-brand-100/50">
                    <span class="font-bold text-brand-600 text-[11px]">👤 집사</span>
                    <span class="text-gray-600 text-[11px]">· <span id="room-saju-butler">--년생</span></span>
                    <div id="room-saju-owner-summary" class="mt-1 text-[11px] text-gray-700"></div>
                </div>
                <div class="bg-amber-50/40 rounded-2xl p-3 border border-amber-100/50">
                    <span class="font-bold text-amber-600 text-[11px]">🐾 펫</span>
                    <span class="text-gray-600 text-[11px]">· <span id="room-saju-pet">--년생</span></span>
                    <div id="room-saju-pet-summary" class="mt-1 text-[11px] text-gray-700"></div>
                </div>
                <div id="room-saju-message" class="text-[11px] text-gray-600 leading-relaxed text-center px-1">
                    조화도 탭에서 사주 조화도를 분석해보세요
                </div>
                <!-- 조화도 전체 상세(세부점수·오행분포·영역별·종합처방) — 모달 열 때 동적 렌더.
                     areas·elements는 배열/객체라 정적 ID로는 표현 불가하므로 렌더 함수가 채운다. -->
                <div id="saju-harmony-detail" class="space-y-3"></div>
            </div>
            <button onclick="closeSajuHarmonyDetail(); switchTab('saju'); setTimeout(() => switchSajuSubTab('harmony'), 200)"
                class="w-full mt-4 btn-modern bg-brand-500 hover:bg-brand-600 text-white py-2.5 text-xs font-bold rounded-xl">
                사주·조화도 분석하기 →
            </button>
            <!-- 구 조화도 위젯이 쓰던 년생 ID — 위 집사/펫 줄이 같은 값을 보여주므로 숨기되,
                 room-harmony.js의 기존 쓰기는 그대로 살린다 -->
            <span id="harmony-widget-butler" class="hidden"></span>
            <span id="harmony-widget-pet" class="hidden"></span>
        </div>
    </div>

</div>
`;
