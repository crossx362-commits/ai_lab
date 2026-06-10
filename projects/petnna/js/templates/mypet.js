const MYPET_TEMPLATE = `
<div class="space-y-4 animate-fade-in">
    
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">

    <!-- 왼쪽 주 컬럼 -->
    <div class="lg:col-span-8 space-y-4">

        <!-- 날짜 & 날씨 -->
        <div class="bg-gradient-to-r from-sky-50 to-teal-50/60 border border-sky-200/60 rounded-2xl px-4 py-3 shadow-sm animate-fade-in">
            <div class="flex items-center gap-4 flex-wrap">
                <div>
                    <span id="mypet-date-display" class="block text-xs font-bold text-gray-400">2026. 05. 23 (토)</span>
                    <span id="mypet-time-display" class="text-xl font-black font-mono text-gray-700">14:30:00</span>
                </div>
                <div class="flex items-center gap-2 border-l border-sky-200/60 pl-4">
                    <i class="fa-solid fa-sun text-2xl text-amber-400" id="mypet-weather-icon"></i>
                    <div>
                        <span id="mypet-weather-temp" class="block text-base font-black text-gray-800">24°C</span>
                        <span id="mypet-weather-desc" class="block text-xs font-bold text-gray-400">맑음 (서울)</span>
                    </div>
                </div>
                <div class="flex items-center gap-3 border-l border-sky-200/60 pl-4 text-xs font-bold text-gray-500">
                    <span>😷 <span id="mypet-weather-dust">--</span></span>
                    <span>💧 <span id="mypet-weather-humidity">--%</span></span>
                </div>
            </div>
            <div id="mypet-weekly-weather-container" class="hidden"></div>
        </div>

        <!-- 오늘의 운세 (집사 + 펫) -->
        <div class="grid grid-cols-2 gap-3">
            <div class="bg-gradient-to-br from-indigo-50 to-purple-50/60 border border-indigo-100 rounded-2xl p-3.5 space-y-1.5">
                <span class="block text-xs font-black text-indigo-600">🧔 집사 오늘의 운세</span>
                <p id="mypet-butler-fortune-text" class="text-sm font-bold text-gray-700 leading-snug keep-all">로딩 중...</p>
            </div>
            <div class="bg-gradient-to-br from-amber-50 to-orange-50/60 border border-amber-100 rounded-2xl p-3.5 space-y-1.5">
                <span class="block text-xs font-black text-amber-600">🐾 펫 오늘의 운세</span>
                <p id="mypet-fortune-text" class="text-sm font-bold text-gray-700 leading-snug keep-all">로딩 중...</p>
            </div>
        </div>

        <!-- 댕이의 하루 방 -->
        <div id="pet-room-card" class="bg-white rounded-3xl border border-amber-100 shadow-sm overflow-hidden">

            <!-- 헤더 -->
            <div class="flex items-center justify-between px-5 pt-4 pb-3">
                <div>
                    <h2 class="text-lg font-black text-gray-800 keep-all" id="pet-room-name-wrapper">
                        <span id="pet-room-name">댕이의 하루 방 🏠</span>
                    </h2>
                    <p id="pet-room-visit-badge" class="text-[11px] text-amber-500 font-bold mt-0.5">
                        🐾 집사의 <span id="pet-room-visit-count">1</span>번째 방문
                    </p>
                </div>
                <div class="flex items-center gap-2">
                    <!-- 설정 버튼 -->
                    <button onclick="toggleRoomSettings()" id="room-settings-btn"
                        class="w-9 h-9 rounded-xl bg-gray-50 hover:bg-amber-50 border border-gray-200 hover:border-amber-200 flex items-center justify-center transition-all">
                        <i class="fa-solid fa-gear text-gray-400 hover:text-amber-500 text-sm" id="room-settings-icon"></i>
                    </button>
                </div>
            </div>

            <!-- 설정 메뉴 (접기/펼치기) -->
            <div id="room-settings-menu" class="hidden border-t border-amber-50 bg-amber-50/40 px-5 py-3">
                <div class="grid grid-cols-4 gap-2">
                    <button onclick="openNotebookModal()"
                        class="flex flex-col items-center gap-1.5 p-2.5 bg-white rounded-xl border border-amber-100 hover:border-brand-300 hover:bg-brand-50 transition-all">
                        <i class="fa-solid fa-address-book text-brand-500 text-lg"></i>
                        <span class="text-[10px] font-black text-gray-600">생활수첩</span>
                    </button>
                    <button onclick="toggleButlerProfileEdit()"
                        class="flex flex-col items-center gap-1.5 p-2.5 bg-white rounded-xl border border-amber-100 hover:border-indigo-300 hover:bg-indigo-50 transition-all">
                        <i class="fa-solid fa-user-pen text-indigo-500 text-lg"></i>
                        <span class="text-[10px] font-black text-gray-600">집사 설정</span>
                    </button>
                    <button onclick="togglePetProfileEdit()"
                        class="flex flex-col items-center gap-1.5 p-2.5 bg-white rounded-xl border border-amber-100 hover:border-amber-300 hover:bg-amber-50 transition-all">
                        <i class="fa-solid fa-paw text-amber-500 text-lg"></i>
                        <span class="text-[10px] font-black text-gray-600">방 설정</span>
                    </button>
                    <button onclick="openPetRegistrationModal()"
                        class="flex flex-col items-center gap-1.5 p-2.5 bg-white rounded-xl border border-amber-100 hover:border-brand-300 hover:bg-brand-50 transition-all">
                        <i class="fa-solid fa-plus text-brand-500 text-lg"></i>
                        <span class="text-[10px] font-black text-gray-600">펫 추가</span>
                    </button>
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
                <div class="relative w-full h-[340px] md:h-[400px] flex items-center justify-center pt-3 pb-2">
                    <!-- SVG 목줄 연결선 -->
                    <svg id="leash-svg" viewBox="0 0 100 100" preserveAspectRatio="none" class="absolute inset-0 w-full h-full pointer-events-none" style="z-index: 1;">
                        <!-- JS로 동적 생성 -->
                    </svg>

                    <!-- 집사 (중앙) -->
                    <div class="absolute" style="left: 50%; top: 50%; transform: translate(-50%, -50%); z-index: 10;">
                        <div class="flex flex-col items-center gap-1.5">
                            <span class="text-[10px] font-black text-brand-600 bg-brand-50 border border-brand-200 px-2 py-0.5 rounded-full flex items-center gap-1">
                                👑 집사
                            </span>
                            <div id="butler-graphic-container" onclick="triggerButlerPhotoUploadDirect()"
                                class="w-32 h-32 flex items-center justify-center rounded-full bg-white border-4 border-brand-400 shadow-xl overflow-hidden cursor-pointer hover:border-brand-500 transition-all"
                                title="집사 사진 변경">
                                <span id="butler-stage-avatar" class="text-6xl">🧔</span>
                                <img loading="lazy" id="butler-stage-image" class="hidden w-full h-full object-cover rounded-full">
                            </div>
                            <input type="file" id="butler-direct-upload" accept="image/*" class="hidden" onchange="uploadButlerPhotoDirect(event)">
                            <span id="butler-stage-name" class="text-xs text-gray-600 font-black">집사</span>
                        </div>
                    </div>

                    <!-- 말풍선 (집사 머리 위) -->
                    <div id="pet-speech-bubble" class="absolute bg-amber-50 border border-amber-200 text-amber-800 text-[10px] font-bold py-1.5 px-2.5 rounded-xl keep-all text-center shadow-sm" style="left: 50%; top: 5%; transform: translateX(-50%); z-index: 11;">
                        <span id="pet-bubble-text">산책 가요! 🐕</span>
                        <div class="absolute -bottom-1.5 left-1/2 -translate-x-1/2 w-2.5 h-2.5 bg-amber-50 border-r border-b border-amber-200 rotate-45"></div>
                    </div>

                    <!-- 반려동물들 (불규칙 배치) -->
                    <div id="pet-stage-list" class="absolute inset-0" style="z-index: 5;">
                        <!-- renderPetStageList() - 각 펫을 원형으로 배치 -->
                    </div>
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

                <!-- AI 건강 분석 (10항목 + 음성문진) -->
                <div class="bg-gradient-to-br from-violet-50 to-purple-50/60 border border-violet-100 rounded-2xl p-3 space-y-2 transition-all duration-300">
                    <div class="flex items-center justify-between">
                        <div class="flex items-center gap-2">
                            <span class="text-sm">🏥</span>
                            <span class="text-[11px] font-black text-violet-700">AI 건강 분석</span>
                            <span id="ai-health-usage-badge" class="text-[9px] font-black text-violet-500 bg-violet-50 px-1.5 py-0.5 rounded-full"></span>
                        </div>
                        <div class="flex items-center gap-1">
                            <button id="ai-voice-btn"
                                onclick="startVoiceConsultation()"
                                class="flex items-center gap-1 px-2 py-1 bg-violet-500 hover:bg-violet-600 text-white font-black text-[9px] rounded-xl transition-all shadow-sm">
                                <i class="fa-solid fa-microphone text-[10px]"></i> 증상 말하기
                            </button>
                            <button id="ai-health-analyze-btn"
                                onclick="triggerAiHealthAnalysis()"
                                class="flex items-center gap-1 px-2 py-1 bg-violet-600 hover:bg-violet-700 text-white font-black text-[9px] rounded-xl transition-all shadow-sm">
                                <i class="fa-solid fa-camera text-[10px]"></i> 사진 분석
                            </button>
                        </div>
                    </div>
                    <input type="file" id="ai-health-photo-input" accept="image/*" class="hidden"
                        onchange="runAiHealthAnalysis(event)">
                    <div id="ai-voice-result" class="hidden bg-white/80 border border-violet-100 rounded-xl"></div>
                    <div id="ai-health-result" class="hidden space-y-1.5"></div>
                    <div id="ai-health-share-btn-wrap" class="hidden flex justify-end">
                        <button onclick="shareHealthCard()"
                            class="flex items-center gap-1 px-2.5 py-1 bg-violet-100 hover:bg-violet-200 text-violet-700 font-black text-[10px] rounded-xl transition-all">
                            <i class="fa-solid fa-share-nodes text-[10px]"></i> 공유 카드 저장
                        </button>
                    </div>
                    <p class="text-[9px] text-violet-400 font-medium">
                        ※ 참고용 AI 분석 · 의학적 진단 아님 · 이상 시 수의사 상담
                    </p>
                </div>

                <!-- 건강 트렌드 대시보드 + 스트릭 + 캘린더 -->
                <div class="bg-gradient-to-br from-emerald-50 to-teal-50/60 border border-emerald-100 rounded-2xl p-2.5 space-y-1.5 transition-all duration-300">
                    <div class="flex items-center justify-between">
                        <div class="flex items-center gap-1.5">
                            <span class="text-sm">📊</span>
                            <span class="text-[11px] font-black text-gray-700">7일 건강 트렌드</span>
                        </div>
                        <div class="flex items-center gap-1.5">
                            <div class="flex items-center gap-1">
                                <span class="text-[9px] text-gray-400 font-bold">건강점수</span>
                                <span id="health-score-value" class="text-lg font-black text-emerald-500">--</span>
                            </div>
                            <button onclick="generateWeeklyHealthData()"
                                class="flex items-center gap-1 px-1.5 py-0.5 bg-emerald-50 hover:bg-emerald-100 text-emerald-600 font-black text-[8px] rounded-lg transition-all border border-emerald-200"
                                title="일주일치 데모 데이터 생성 (누를 때마다 추가)">
                                <i class="fa-solid fa-database text-[8px]"></i> 데모
                            </button>
                            <button onclick="generateHealthReportPDF()"
                                class="flex items-center gap-1 px-1.5 py-0.5 bg-violet-50 hover:bg-violet-100 text-violet-600 font-black text-[8px] rounded-lg transition-all border border-violet-100">
                                <i class="fa-solid fa-file-pdf text-[8px]"></i> 리포트
                                <span class="text-[7px] bg-amber-100 text-amber-700 px-1 rounded font-black">PRO</span>
                            </button>
                        </div>
                    </div>
                    <div id="health-streak-badge" class="flex items-center gap-1 min-h-[12px]"></div>

                    <!-- 사용법 안내 (데이터 없을 때만 표시) -->
                    <div id="health-tutorial" class="hidden bg-white/60 backdrop-blur-sm p-2 rounded-xl border border-emerald-200/50">
                        <div class="flex items-start gap-1.5">
                            <span class="text-sm">💡</span>
                            <div class="flex-1 space-y-0.5">
                                <p class="text-[10px] font-bold text-emerald-700">건강 트렌드 사용법</p>
                                <ul class="text-[9px] text-gray-600 space-y-0.5 leading-tight">
                                    <li class="flex items-start gap-1">
                                        <span class="text-emerald-500 mt-0.5">•</span>
                                        <span><strong class="text-emerald-600">식사/물</strong> 탭에서 매일 기록하면 자동으로 차트가 생성됩니다</span>
                                    </li>
                                    <li class="flex items-start gap-1">
                                        <span class="text-emerald-500 mt-0.5">•</span>
                                        <span><strong class="text-emerald-600">데모 버튼</strong>을 누르면 일주일치 샘플 데이터를 추가할 수 있습니다</span>
                                    </li>
                                    <li class="flex items-start gap-1">
                                        <span class="text-emerald-500 mt-0.5">•</span>
                                        <span>7일간 기록이 쌓이면 <strong class="text-emerald-600">건강점수</strong>와 <strong class="text-emerald-600">연속 기록</strong> 배지가 표시됩니다</span>
                                    </li>
                                </ul>
                            </div>
                        </div>
                    </div>

                    <div style="min-height:70px; max-height:120px;">
                        <canvas id="health-trend-chart"></canvas>
                    </div>
                    <!-- 90일 캘린더 히트맵 -->
                    <div id="health-calendar" class="pt-0"></div>
                </div>

                <!-- 컨디션 2칸 (앰버 톤 통일) -->
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

                <!-- 오늘의 돌봄 스케줄 -->
                <div class="bg-gradient-to-br from-sky-50 to-blue-50/60 border border-sky-100 rounded-2xl p-3 space-y-2 transition-all duration-300">
                    <div class="flex items-center justify-between">
                        <div class="flex items-center gap-2">
                            <span class="text-sm">📅</span>
                            <span class="text-[11px] font-black text-gray-700">오늘의 돌봄 일정</span>
                            <span id="care-completion-badge" class="text-[9px] font-black bg-emerald-100 text-emerald-700 px-2 py-0.5 rounded-full"></span>
                        </div>
                        <button onclick="openCareScheduleModal()"
                            class="flex items-center gap-1 px-1.5 py-0.5 bg-sky-50 hover:bg-sky-100 text-sky-600 font-black text-[8px] rounded-lg transition-all border border-sky-200">
                            <i class="fa-solid fa-plus text-[8px]"></i> 추가
                        </button>
                    </div>
                    <div id="care-scheduler-container" class="space-y-1.5"></div>
                </div>

            </div>
        </div>

        <!-- 🔮 펫 & 집사 평생 사주 결과 카드 -->
        <div id="mypet-saju-card"
            class="bg-white rounded-3xl p-5 border border-amber-100 shadow-sm space-y-4">
            <div class="flex justify-between items-center pb-2 border-b border-gray-100">
                <h3 class="font-black text-gray-800 text-sm flex items-center">
                    <i class="fa-solid fa-yin-yang text-amber-500 mr-2"></i>펫 & 집사 평생 사주 & 영혼 조화도 🔮✨
                </h3>
            </div>

            <!-- 결과가 없을 때의 UI -->
            <div id="mypet-saju-no-result" class="space-y-3">
                <!-- 오늘의 운세 미리보기 -->
                <div class="bg-gradient-to-r from-amber-50 to-orange-50 border border-amber-100 rounded-2xl p-4 space-y-2">
                    <p class="text-[10px] font-black text-amber-700 flex items-center gap-1.5">🍀 오늘의 운세</p>
                    <p id="mypet-fortune-preview" class="text-xs text-gray-600 leading-relaxed font-medium"></p>
                    <button onclick="switchTab('saju'); setTimeout(() => switchSajuSubTab('fortune'), 200)"
                        class="text-[10px] font-black text-amber-600 hover:text-amber-700">자세히 보기 →</button>
                </div>
                <!-- 사주 분석 유도 -->
                <div class="bg-amber-50/40 border border-amber-100/50 rounded-2xl p-4 text-center space-y-2.5">
                    <p class="text-[11px] text-gray-500 leading-relaxed">
                        사주·영혼 조화도를 분석하면<br>여기에 결과가 표시됩니다
                    </p>
                    <button onclick="switchTab('saju')"
                        class="mx-auto bg-brand-500 hover:bg-brand-600 text-white font-black py-2 px-5 rounded-xl text-xs transition-colors shadow-sm">
                        사주 & 조화도 분석하기 🔮
                    </button>
                </div>
            </div>

            <!-- 결과가 있을 때의 UI (한 화면에 모두 노출) -->
            <div id="mypet-saju-has-result" class="hidden space-y-4 text-xs">
                <!-- 영혼 조화도 (종합 분석 결과) -->
                <div id="mypet-harmony-display-box" class="hidden bg-gradient-to-r from-rose-50 to-pink-50 border border-rose-100 rounded-2xl p-4 space-y-2.5">
                    <div class="flex justify-between items-center pb-1.5 border-b border-rose-200/30">
                        <span class="font-black text-rose-800 text-[11px] flex items-center gap-1.5">
                            <span>💖</span> 펫 & 집사 영혼 조화도
                        </span>
                        <span id="mypet-harmony-avg-score" class="bg-rose-500 text-white font-extrabold font-mono text-[10px] px-2.5 py-0.5 rounded-full shadow-sm">92점 (5단계)</span>
                    </div>
                    <div class="flex items-center gap-3">
                        <div class="w-10 h-10 rounded-full bg-white flex items-center justify-center text-xl shadow-sm shrink-0">
                            💞
                        </div>
                        <div>
                            <h4 id="mypet-harmony-title" class="font-black text-rose-700 text-xs">"영혼의 단짝, 완벽한 듀오!"</h4>
                            <p id="mypet-harmony-solution" class="text-[10px] text-gray-500 leading-relaxed line-clamp-2 mt-0.5">솔루션 내용...</p>
                        </div>
                    </div>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-3.5" id="mypet-saju-grid-section">
                    <!-- 펫 사주 -->
                    <div class="bg-brand-50/30 border border-brand-100/50 rounded-2xl p-4 space-y-2">
                        <span class="font-black text-brand-800 text-[11px] flex items-center gap-1">
                            <span>🐾</span> <span id="mypet-saju-pet-name-label">댕이</span>의 사주
                        </span>
                        <h4 id="mypet-saju-pet-summary" class="font-black text-brand-700 text-xs">목(木)의 지혜로운 수호견</h4>
                        <p id="mypet-saju-pet-desc" class="text-[10px] text-gray-500 leading-relaxed line-clamp-3">설명...</p>
                    </div>
                    <!-- 집사 사주 -->
                    <div class="bg-indigo-50/30 border border-indigo-100/50 rounded-2xl p-4 space-y-2">
                        <span class="font-black text-indigo-800 text-[11px] flex items-center gap-1">
                            <span>🧔</span> 집사 사주
                        </span>
                        <h4 id="mypet-saju-owner-summary" class="font-black text-indigo-700 text-xs">화(火)의 따뜻한 영혼</h4>
                        <p id="mypet-saju-owner-desc" class="text-[10px] text-gray-500 leading-relaxed line-clamp-3">설명...</p>
                    </div>
                </div>

                <!-- 조화 및 전생 인연 (관계) -->
                <div class="bg-rose-50/20 border border-rose-100/50 rounded-2xl p-4 space-y-3" id="mypet-saju-compat-section">
                    <div class="flex justify-between items-center border-b border-rose-100/30 pb-2">
                        <span class="font-black text-rose-800 text-[11px] flex items-center gap-1">
                            <span>💖</span> 펫과 집사의 전생 인연 & 궁합
                        </span>
                        <span id="mypet-saju-compat-score" class="bg-rose-100 text-rose-600 font-bold font-mono text-[10px] px-2 py-0.5 rounded-full">96점</span>
                    </div>
                    <h4 id="mypet-saju-compat-title" class="font-black text-rose-600 text-xs">"수어지교(水魚之交)"</h4>
                    <p id="mypet-saju-past-desc" class="text-[10px] text-gray-500 leading-relaxed line-clamp-3">전생 인연설 스토리...</p>

                    <div class="bg-emerald-50/30 p-2.5 rounded-xl border border-emerald-100/40 text-[10px] text-gray-600 space-y-1">
                        <strong class="text-emerald-800 block">🌿 현생 시너지 & 케어 조언</strong>
                        <p id="mypet-saju-synergy-desc" class="leading-relaxed line-clamp-2">조언...</p>
                    </div>
                </div>

                <div class="flex gap-2" id="mypet-saju-buttons-section">
                    <button onclick="switchTab('saju')"
                        class="flex-1 bg-brand-50 hover:bg-brand-100 text-brand-700 font-bold py-2 rounded-xl text-[10px] transition-colors text-center shadow-sm">
                        상세 사주서록(만세력) 보기 🔍
                    </button>
                    <button onclick="shareSajuToFeed()"
                        class="flex-grow bg-brand-500 hover:bg-brand-600 text-white font-black py-2 rounded-xl text-[10px] transition-colors shadow-sm">
                        내 피드에 자랑하기 📢
                    </button>
                </div>
            </div>
        </div>

        <!-- 월간 종합 케어 리포트 (건강 + 돌봄 통합) -->
        <div class="bg-white rounded-3xl p-5 border border-amber-100 shadow-sm space-y-3">
            <div class="flex justify-between items-center">
                <div>
                    <h3 class="font-black text-gray-800 text-base flex items-center">
                        <i class="fa-solid fa-chart-line text-brand-500 mr-2"></i>월간 종합 케어 리포트 📊
                    </h3>
                    <p class="text-[11px] text-gray-400 mt-0.5">건강 트렌드 + 돌봄 일정 준수율 + AI 분석을 통합한 종합 리포트
                    </p>
                </div>
                <button onclick="generateHealthReportPDF()"
                    class="flex items-center gap-1.5 px-3 py-2 bg-violet-500 hover:bg-violet-600 text-white font-black text-[11px] rounded-xl transition-all shadow-sm">
                    <i class="fa-solid fa-file-pdf text-sm"></i> PDF 리포트
                    <span class="text-[8px] bg-amber-100 text-amber-700 px-1.5 py-0.5 rounded font-black">PRO</span>
                </button>
            </div>
            <div class="grid grid-cols-2 sm:grid-cols-4 gap-3">
                <div class="bg-violet-50 p-3 rounded-xl border border-violet-100 text-center">
                    <div id="report-health-score" class="text-2xl font-black text-violet-600">--</div>
                    <div class="text-[10px] text-gray-500 font-bold mt-1">건강 점수</div>
                </div>
                <div class="bg-emerald-50 p-3 rounded-xl border border-emerald-100 text-center">
                    <div id="report-care-rate" class="text-2xl font-black text-emerald-600">--%</div>
                    <div class="text-[10px] text-gray-500 font-bold mt-1">일정 준수율</div>
                </div>
                <div class="bg-amber-50 p-3 rounded-xl border border-amber-100 text-center">
                    <div id="report-streak" class="text-2xl font-black text-amber-600">--일</div>
                    <div class="text-[10px] text-gray-500 font-bold mt-1">연속 기록</div>
                </div>
                <div class="bg-sky-50 p-3 rounded-xl border border-sky-100 text-center">
                    <div id="report-ai-count" class="text-2xl font-black text-sky-600">--회</div>
                    <div class="text-[10px] text-gray-500 font-bold mt-1">AI 분석</div>
                </div>
            </div>
        </div>
    </div>

    <!-- 우측 사이드바 컬럼 -->
    <div class="lg:col-span-4 space-y-4">

        <!-- 일일 챌린지 -->
        <div class="bg-gradient-to-br from-orange-50 to-amber-50/60 border border-amber-200/60 rounded-2xl p-4 shadow-sm">
            <div id="daily-challenges"></div>
        </div>

        <!-- 업적 배지 -->
        <div class="bg-gradient-to-br from-amber-50 to-yellow-50/60 border border-amber-200/60 rounded-2xl p-4 shadow-sm">
            <div id="achievement-badges"></div>
        </div>

        <!-- 돌봄 스케줄러 📅 -->
        <div class="bg-white rounded-3xl p-5 border border-amber-100 shadow-sm space-y-4">
            <div class="flex justify-between items-center pb-2 border-b">
                <h3 class="font-black text-gray-800 text-sm flex items-center">
                    <i class="fa-solid fa-calendar-days text-brand-500 mr-2"></i>돌봄 스케줄러 📅
                </h3>
                <button onclick="openScheduleModal()"
                    class="text-brand-600 hover:text-brand-700 font-black text-xs">
                    <i class="fa-solid fa-plus mr-1"></i>일정 등록
                </button>
            </div>

            <!-- 달력 헤더 -->
            <div class="flex justify-between items-center">
                <button onclick="changeMonth(-1)" class="text-gray-400 hover:text-gray-600"><i
                        class="fa-solid fa-chevron-left"></i></button>
                <span id="calendar-month-year" class="font-black text-xs text-gray-700">2026년 5월</span>
                <button onclick="changeMonth(1)" class="text-gray-400 hover:text-gray-600"><i
                        class="fa-solid fa-chevron-right"></i></button>
            </div>

            <!-- 달력 그리드 -->
            <div
                class="grid grid-cols-7 gap-1 text-center text-[10px] text-gray-400 font-bold uppercase tracking-wider border-b pb-2">
                <span>일</span><span>월</span><span>화</span><span>수</span><span>목</span><span>금</span><span>토</span>
            </div>
            <div id="calendar-days" class="grid grid-cols-7 gap-1 text-center text-xs">
                <!-- 날짜들 동적 수립 -->
            </div>

            <!-- 다가오는 주요 돌봄 checklist -->
            <div class="space-y-2.5 pt-4 border-t border-gray-100">
                <span class="block text-[10px] text-gray-400 font-bold uppercase tracking-wider">다가오는 핵심 돌봄 3</span>
                <div id="upcoming-schedules" class="space-y-2">
                    <!-- JS 동적 수립 -->
                </div>
            </div>
        </div>

        <!-- 식사 일지 & 밥 먹는 시간 관리 -->
        <div class="bg-white rounded-3xl p-4 sm:p-5 border border-amber-100 shadow-sm space-y-4 animate-fade-in">
            <div class="flex justify-between items-center pb-2 border-b border-gray-100">
                <h3 class="font-black text-gray-800 text-sm flex items-center">
                    <i class="fa-solid fa-bowl-food text-brand-500 mr-2"></i>식사 일지 & 밥 먹는 시간 🍖
                </h3>
                <button onclick="toggleMealForm(true)" class="text-brand-600 hover:text-brand-700 font-black text-xs">
                    <i class="fa-solid fa-plus mr-1"></i>기록 추가
                </button>
            </div>
            <div id="meal-form" class="hidden bg-brand-50/50 border border-brand-100 p-3 rounded-2xl space-y-2.5">
                <span class="block text-[10px] text-brand-800 font-bold"><i class="fa-solid fa-clock mr-1"></i> 새로운 배식 활동 기록</span>
                <div class="grid grid-cols-2 gap-2 text-xs">
                    <select id="meal-type" class="border rounded-lg p-1.5 outline-none bg-white">
                        <option value="아침">🌅 아침 밥</option>
                        <option value="점심">☀️ 점심 밥</option>
                        <option value="저녁">🌙 저녁 밥</option>
                        <option value="간식">🍖 간식 공급</option>
                    </select>
                    <input type="time" id="meal-time" class="border rounded-lg p-1.5 outline-none bg-white">
                </div>
                <input type="text" id="meal-notes" placeholder="사료명, 칼로리 혹은 반응 기재 (예: 연어 습식 80g)"
                    class="w-full text-xs border rounded-lg p-2 outline-none bg-white">
                <div class="flex space-x-2 text-[10px]">
                    <button onclick="toggleMealForm(false)" class="w-1/2 bg-white border font-bold py-1.5 rounded-lg">취소</button>
                    <button onclick="saveMealRecord()" class="w-1/2 bg-brand-500 text-white font-bold py-1.5 rounded-lg">저장하기</button>
                </div>
            </div>
            <div id="meal-list" class="space-y-2 max-h-40 overflow-y-auto no-scrollbar"></div>
        </div>
    </div>

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
