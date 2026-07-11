// health.js — 건강 탭 템플릿 (티모 디자인 반영 + 아이콘 중심 UI)

const HEALTH_TEMPLATE = `
<div class="space-y-4 animate-fade-in">

    <!-- 헤더 -->
    <div class="glass rounded-2xl px-6 py-5 shadow-soft-lg border border-brand-100/50">
        <div class="flex items-center justify-between">
            <div class="flex items-center gap-4">
                <div class="w-12 h-12 bg-gradient-to-br from-brand-500 to-brand-600 rounded-2xl flex items-center justify-center shadow-soft">
                    <span class="text-3xl">❤️</span>
                </div>
                <div>
                    <h2 class="text-xl font-bold text-gray-900 tracking-tight">건강 대시보드</h2>
                    <p class="text-xs text-gray-500 mt-1">펫별 건강 관리 및 기록</p>
                </div>
            </div>
            <div class="flex items-center gap-3">
                <!-- 펫 선택 드롭다운 -->
                <select id="health-pet-selector" onchange="onHealthPetChange()"
                    class="px-4 py-2 bg-white border border-brand-200 rounded-xl text-sm font-bold text-brand-700 hover:border-brand-300 transition-colors cursor-pointer">
                    <option value="">펫 선택</option>
                </select>
                <button onclick="generateWeeklyHealthData()"
                    class="btn-modern px-4 py-2 bg-brand-50 hover:bg-brand-100 text-brand-700 text-sm border border-brand-200/50">
                    <i class="fa-solid fa-database text-xs mr-2"></i>데모 데이터
                </button>
            </div>
        </div>
    </div>

    <!-- 2컬럼 레이아웃 -->
    <div class="grid grid-cols-1 lg:grid-cols-12 gap-4 items-start">

    <!-- 왼쪽 컬럼 (메인 콘텐츠) -->
    <div class="lg:col-span-8 space-y-4">

    <!-- 🔮 예측 웰니스 이상감지 (wellness-anomaly.js가 채움) -->
    <div id="wellness-anomaly-card"></div>

    <!-- 📋 오늘의 건강 기록 -->
    <div class="card-modern p-6">
        <div class="flex items-center justify-between mb-6">
            <div class="flex items-center gap-3">
                <div class="text-4xl">📋</div>
                <div>
                    <h2 class="text-lg font-bold text-gray-900">오늘의 기록</h2>
                    <p class="text-xs text-gray-500">빠른 건강 체크</p>
                </div>
            </div>
            <button onclick="openHealthLogModal()" class="btn-modern bg-brand-500 hover:bg-brand-600 text-white px-4 py-2.5 text-sm">
                <i class="fa-solid fa-plus mr-1.5"></i>기록
            </button>
        </div>

        <div class="grid grid-cols-3 gap-4">
            <!-- 식사량 -->
            <button onclick="openHealthLogModal()" class="card-modern bg-gradient-to-br from-amber-50 to-orange-50 p-6 text-center group hover:scale-105 transition-transform">
                <div class="text-5xl mb-3">🍖</div>
                <div id="health-today-food" class="text-3xl font-bold text-amber-600 mb-2">--g</div>
                <div class="text-xs text-gray-600 font-semibold">식사량</div>
            </button>

            <!-- 음수량 -->
            <button onclick="openHealthLogModal()" class="card-modern bg-gradient-to-br from-sky-50 to-blue-50 p-6 text-center group hover:scale-105 transition-transform">
                <div class="text-5xl mb-3">💧</div>
                <div id="health-today-water" class="text-3xl font-bold text-sky-600 mb-2">--ml</div>
                <div class="text-xs text-gray-600 font-semibold">음수량</div>
            </button>

            <!-- 배변 -->
            <button onclick="openHealthLogModal()" class="card-modern bg-gradient-to-br from-rose-50 to-pink-50 p-6 text-center group hover:scale-105 transition-transform">
                <div class="text-5xl mb-3">💩</div>
                <div id="health-today-poop" class="text-3xl font-bold text-rose-600 mb-2">--회</div>
                <div class="text-xs text-gray-600 font-semibold">배변</div>
            </button>
        </div>
    </div>

    <!-- 💉 투약·정기예방 대시보드 (심장사상충/구충/백신 카운트다운) -->
    <div id="preventive-care-dashboard"></div>

    <!-- 📔 반려 건강수첩 (병원 방문·진료비·서류 아카이브) — localStorage MVP -->
    <div class="card-modern overflow-hidden">
        <div class="px-5 pt-4 pb-3 border-b border-gray-100 flex items-center justify-between">
            <h2 class="text-base font-bold text-gray-900 flex items-center gap-2">
                <i class="fa-solid fa-notes-medical text-brand-500"></i>건강수첩
            </h2>
            <button onclick="openMedicalRecordModal()" class="text-xs font-bold text-white bg-brand-500 hover:bg-brand-600 px-3 py-1.5 rounded-full transition-all shadow-soft">
                <i class="fa-solid fa-plus mr-1"></i>기록 추가
            </button>
        </div>
        <div class="px-5 py-4">
            <div id="medical-records-timeline"></div>
        </div>
    </div>

    <!-- 📈 건강 트렌드 차트 -->
    <div class="card-modern p-6">
        <div class="flex items-center justify-between mb-6">
            <div class="flex items-center gap-3">
                <div class="text-4xl">📈</div>
                <div>
                    <h2 class="text-lg font-bold text-gray-900">7일 건강 트렌드</h2>
                    <p class="text-xs text-gray-500">데이터로 보는 변화</p>
                </div>
            </div>
        </div>

        <!-- 사용법 안내 (데이터 없을 때만 표시) -->
        <div id="health-tutorial-main" class="hidden card-modern bg-brand-50/50 p-4 mb-4">
            <div class="flex items-start gap-3">
                <span class="text-3xl">💡</span>
                <div class="flex-1 space-y-2">
                    <p class="text-sm font-bold text-brand-700">건강 트렌드 사용법</p>
                    <ul class="text-xs text-gray-600 space-y-1.5 leading-relaxed">
                        <li class="flex items-start gap-2">
                            <span class="text-brand-500 mt-0.5">•</span>
                            <span>매일 <strong class="text-brand-600">건강 기록</strong> 버튼으로 식사·음수·배변을 기록하세요</span>
                        </li>
                        <li class="flex items-start gap-2">
                            <span class="text-brand-500 mt-0.5">•</span>
                            <span>7일간 기록이 쌓이면 자동으로 <strong class="text-brand-600">건강점수</strong>와 <strong class="text-brand-600">차트</strong>가 생성됩니다</span>
                        </li>
                        <li class="flex items-start gap-2">
                            <span class="text-brand-500 mt-0.5">•</span>
                            <span>테스트하려면 위의 <strong class="text-brand-600">데모 데이터</strong> 버튼을 눌러보세요</span>
                        </li>
                    </ul>
                </div>
            </div>
        </div>

        <div class="bg-gradient-to-br from-sky-50 to-blue-50 rounded-2xl p-4 border border-sky-100" style="min-height:120px; max-height:180px;">
            <canvas id="health-trend-chart-main"></canvas>
        </div>

        <!-- 90일 캘린더 히트맵 -->
        <div id="health-calendar-main" class="mt-4 pt-4 border-t border-gray-200"></div>
    </div>

    <!-- 🤖 AI 기능 섹션 -->
    <div class="space-y-4">
        <div class="flex items-center gap-2 px-2">
            <div class="flex-1 h-px bg-gradient-to-r from-transparent via-brand-300 to-transparent"></div>
            <div class="flex items-center gap-2">
                <span class="text-3xl">🤖</span>
                <h3 class="text-base font-bold text-brand-600">AI 기능</h3>
            </div>
            <div class="flex-1 h-px bg-gradient-to-r from-transparent via-brand-300 to-transparent"></div>
        </div>

        <!-- AI 기능 카드 그리드 -->
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">

            <!-- AI 건강 분석 -->
            <button onclick="triggerAiHealthAnalysis()" class="card-modern bg-gradient-to-br from-brand-50 to-brand-50 p-6 text-left group hover:scale-[1.02] transition-all">
                <div class="flex items-start justify-between mb-4">
                    <div class="flex items-center gap-3">
                        <div class="text-5xl">🔬</div>
                        <div>
                            <h3 class="text-lg font-bold text-gray-900">건강 분석</h3>
                            <p class="text-xs text-brand-600 mt-1">사진으로 10가지 항목 체크</p>
                        </div>
                    </div>
                </div>
                <div class="flex items-center justify-between">
                    <p class="text-xs text-gray-500">이번 달 <span id="ai-usage-count-health" class="font-bold text-brand-600">0</span>/5회</p>
                    <i class="fa-solid fa-camera text-brand-500 text-xl group-hover:scale-110 transition-transform"></i>
                </div>
            </button>

            <!-- AI 수의사 -->
            <button onclick="openVetChatModal()" class="card-modern bg-gradient-to-br from-emerald-50 to-teal-50 p-6 text-left group hover:scale-[1.02] transition-all">
                <div class="flex items-start justify-between mb-4">
                    <div class="flex items-center gap-3">
                        <div class="text-5xl">🏥</div>
                        <div>
                            <h3 class="text-lg font-bold text-gray-900">AI 수의사</h3>
                            <p class="text-xs text-emerald-600 mt-1">증상 상담 즉시 답변</p>
                        </div>
                    </div>
                </div>
                <div class="flex items-center justify-between">
                    <p class="text-xs text-gray-500">24시간 언제든지</p>
                    <i class="fa-solid fa-comment-medical text-emerald-500 text-xl group-hover:scale-110 transition-transform"></i>
                </div>
            </button>

            <!-- 음성 상담 -->
            <button onclick="startVoiceConsultation()" class="card-modern bg-gradient-to-br from-sky-50 to-blue-50 p-6 text-left group hover:scale-[1.02] transition-all">
                <div class="flex items-start justify-between mb-4">
                    <div class="flex items-center gap-3">
                        <div class="text-5xl">🎤</div>
                        <div>
                            <h3 class="text-lg font-bold text-gray-900">음성 상담</h3>
                            <p class="text-xs text-sky-600 mt-1">증상 말로 설명하기</p>
                        </div>
                    </div>
                </div>
                <div class="flex items-center justify-end">
                    <i class="fa-solid fa-microphone text-sky-500 text-xl group-hover:scale-110 transition-transform"></i>
                </div>
            </button>

            <!-- PDF 리포트 -->
            <button onclick="generateHealthReportPDF()" class="card-modern bg-gradient-to-br from-orange-50 to-amber-50 p-6 text-left group hover:scale-[1.02] transition-all">
                <div class="flex items-start justify-between mb-4">
                    <div class="flex items-center gap-3">
                        <div class="text-5xl">📄</div>
                        <div>
                            <h3 class="text-lg font-bold text-gray-900">PDF 리포트</h3>
                            <p class="text-xs text-orange-600 mt-1">건강 분석 문서화</p>
                        </div>
                    </div>
                </div>
                <div class="flex items-center justify-between">
                    <span class="text-xs bg-gradient-to-r from-orange-500 to-amber-500 text-white px-3 py-1 rounded-full font-bold">PRO</span>
                    <i class="fa-solid fa-file-pdf text-orange-500 text-xl group-hover:scale-110 transition-transform"></i>
                </div>
            </button>

        </div>

        <!-- AI 건강 분석 숨겨진 입력 & 결과 -->
        <input type="file" id="ai-health-photo-input-health" accept="image/*" class="hidden" onchange="runAiHealthAnalysis(event)">
        <div id="ai-voice-result-health" class="hidden card-modern p-4"></div>
        <div id="ai-health-result-main" class="hidden space-y-2"></div>
        <div id="ai-health-share-btn-wrap-health" class="hidden flex justify-end">
            <button onclick="shareHealthCard()" class="btn-modern bg-brand-500 hover:bg-brand-600 text-white px-4 py-2 text-sm">
                <i class="fa-solid fa-share-nodes mr-1.5"></i>공유 카드 저장
            </button>
        </div>

        <!-- AI 안내 -->
        <div class="card-modern bg-brand-50/50 p-4">
            <p class="text-xs text-center text-brand-700">
                <i class="fa-solid fa-info-circle mr-1"></i>
                AI 분석은 참고용이며 의학적 진단이 아닙니다. 이상 시 수의사와 상담하세요.
            </p>
        </div>

    </div>
    <!-- /AI 기능 섹션 끝 -->

    </div>
    <!-- /왼쪽 컬럼 끝 -->

    <!-- 오른쪽 컬럼 (월간 리포트 + 영양관리 + 돌봄 스케줄러) -->
    <div class="lg:col-span-4 space-y-4">

        <!-- 📊 월간 종합 리포트 -->
        <div class="card-modern p-4">
            <div class="flex justify-between items-center mb-4">
                <div class="flex items-center gap-2">
                    <span class="text-3xl">📊</span>
                    <div>
                        <h3 class="text-base font-bold text-gray-900">월간 리포트</h3>
                        <p class="text-[10px] text-gray-500">이번 달 요약</p>
                    </div>
                </div>
                <button onclick="generateHealthReportPDF()"
                    class="btn-modern bg-brand-500 hover:bg-brand-600 text-white px-3 py-2 text-xs">
                    <i class="fa-solid fa-file-pdf mr-1"></i>PDF
                </button>
            </div>
            <div class="grid grid-cols-2 gap-2">
                <div class="card-modern bg-brand-50/50 p-3 text-center">
                    <div class="text-2xl mb-1">💯</div>
                    <div id="report-health-score" class="text-xl font-bold text-brand-600">--</div>
                    <div class="text-[10px] text-gray-600 font-semibold">건강점수</div>
                </div>
                <div class="card-modern bg-emerald-50/50 p-3 text-center">
                    <div class="text-2xl mb-1">📅</div>
                    <div id="report-care-rate" class="text-xl font-bold text-emerald-600">--%</div>
                    <div class="text-[10px] text-gray-600 font-semibold">준수율</div>
                </div>
                <div class="card-modern bg-amber-50/50 p-3 text-center">
                    <div class="text-2xl mb-1">🔥</div>
                    <div id="report-streak" class="text-xl font-bold text-amber-600">--일</div>
                    <div class="text-[10px] text-gray-600 font-semibold">연속기록</div>
                </div>
                <div class="card-modern bg-sky-50/50 p-3 text-center">
                    <div class="text-2xl mb-1">🤖</div>
                    <div id="report-ai-count" class="text-xl font-bold text-sky-600">--회</div>
                    <div class="text-[10px] text-gray-600 font-semibold">AI분석</div>
                </div>
            </div>
        </div>

        <!-- 돌봄 스케줄러 📅 -->
        <div class="card-modern p-5 space-y-4">
            <div class="flex justify-between items-center pb-2 border-b">
                <h3 class="font-black text-gray-800 text-base flex items-center">
                    <i class="fa-solid fa-calendar-days text-brand-500 mr-2"></i>돌봄 스케줄러 📅
                </h3>
                <button onclick="openCareScheduleModal()"
                    class="text-brand-600 hover:text-brand-700 font-black text-sm">
                    <i class="fa-solid fa-plus mr-1.5"></i>일정 추가
                </button>
            </div>

            <!-- 오늘의 일정 -->
            <div class="bg-gradient-to-br from-sky-50 to-blue-50/60 border border-sky-100 rounded-xl p-4 space-y-2">
                <div class="flex items-center justify-between">
                    <span class="text-sm font-black text-gray-700">📅 오늘의 일정</span>
                    <span id="care-completion-badge-health" class="text-xs font-black bg-emerald-100 text-emerald-700 px-2.5 py-1 rounded-full"></span>
                </div>
                <div id="care-scheduler-container-health" class="space-y-2"></div>
            </div>

            <!-- 달력 헤더 -->
            <div class="flex justify-between items-center">
                <button onclick="changeMonth(-1)" class="text-gray-400 hover:text-gray-600 transition-colors">
                    <i class="fa-solid fa-chevron-left"></i>
                </button>
                <span id="calendar-month-year-health" class="font-black text-sm text-gray-700">2026년 6월</span>
                <button onclick="changeMonth(1)" class="text-gray-400 hover:text-gray-600 transition-colors">
                    <i class="fa-solid fa-chevron-right"></i>
                </button>
            </div>

            <!-- 달력 그리드 -->
            <div class="grid grid-cols-7 gap-1.5 text-center text-xs text-gray-400 font-bold uppercase tracking-wider border-b pb-2">
                <span>일</span><span>월</span><span>화</span><span>수</span><span>목</span><span>금</span><span>토</span>
            </div>
            <div id="calendar-days-health" class="grid grid-cols-7 gap-1.5 text-center text-sm">
                <!-- 날짜들 동적 생성 -->
            </div>

            <!-- 다가오는 주요 돌봄 -->
            <div class="space-y-3 pt-4 border-t border-gray-100">
                <span class="block text-xs text-gray-400 font-bold uppercase tracking-wider">다가오는 핵심 돌봄 3</span>
                <div id="upcoming-schedules-health" class="space-y-2.5">
                    <!-- JS 동적 생성 -->
                </div>
            </div>
        </div>

        <!-- 🍖 영양 관리 섹션 -->
        <div class="card-modern p-5">
            <div class="flex items-center justify-between mb-4">
                <div class="flex items-center gap-2">
                    <span class="text-3xl">🍖</span>
                    <div>
                        <h3 class="text-base font-bold text-gray-900">영양 관리</h3>
                        <p class="text-[10px] text-gray-500">식사 · 시간 · 음수</p>
                    </div>
                </div>
                <button onclick="toggleMealForm(true)" class="btn-modern bg-amber-500 hover:bg-amber-600 text-white px-3 py-2 text-xs">
                    <i class="fa-solid fa-plus mr-1"></i>기록
                </button>
            </div>

            <!-- 탭 버튼 (밥먹/시간/음수) -->
            <div class="flex gap-2 mb-3 p-1 bg-gray-100 rounded-xl">
                <button id="meal-tab-food" onclick="switchMealTab('food')" class="flex-1 py-2 rounded-lg font-bold text-xs transition-all bg-white text-amber-600 shadow-sm">
                    🍽️ 밥먹
                </button>
                <button id="meal-tab-time" onclick="switchMealTab('time')" class="flex-1 py-2 rounded-lg font-bold text-xs transition-all text-gray-500 hover:text-gray-700">
                    ⏰ 시간
                </button>
                <button id="meal-tab-water" onclick="switchMealTab('water')" class="flex-1 py-2 rounded-lg font-bold text-xs transition-all text-gray-500 hover:text-gray-700">
                    💧 음수
                </button>
            </div>

            <!-- 기록 추가 폼 -->
            <div id="meal-form" class="hidden card-modern bg-amber-50/50 p-3 space-y-2 mb-3">
                <div class="flex items-center gap-2 text-amber-800 font-bold text-[10px]">
                    <i class="fa-solid fa-pen-to-square"></i>
                    <span>새로운 기록 추가</span>
                </div>
                <div class="grid grid-cols-2 gap-2 text-xs">
                    <select id="meal-type" class="border-2 border-amber-200 rounded-xl p-2 outline-none bg-white font-medium focus:border-amber-400 transition-all text-[11px]">
                        <option value="아침">🌅 아침 밥</option>
                        <option value="점심">☀️ 점심 밥</option>
                        <option value="저녁">🌙 저녁 밥</option>
                        <option value="간식">🍖 간식 공급</option>
                    </select>
                    <input type="time" id="meal-time" class="border-2 border-amber-200 rounded-xl p-2 outline-none bg-white font-medium focus:border-amber-400 transition-all text-[11px]">
                </div>
                <input type="text" id="meal-notes" placeholder="사료명, 양 (예: 연어 습식 80g)"
                    class="w-full text-[11px] border-2 border-amber-200 rounded-xl p-2 outline-none bg-white font-medium focus:border-amber-400 transition-all">
                <div class="flex gap-2 text-xs">
                    <button onclick="toggleMealForm(false)" class="flex-1 btn-modern bg-gray-100 hover:bg-gray-200 text-gray-700 py-2">취소</button>
                    <button onclick="saveMealRecord()" class="flex-1 btn-modern bg-amber-500 hover:bg-amber-600 text-white py-2">저장하기</button>
                </div>
            </div>

            <!-- 탭 컨텐츠 -->
            <div id="meal-content-food" class="meal-tab-content">
                <div id="meal-list" class="space-y-1.5 max-h-52 overflow-y-auto pr-0.5"></div>
            </div>
            <div id="meal-content-time" class="meal-tab-content hidden">
                <div id="meal-timeline" class="space-y-1.5 max-h-52 overflow-y-auto pr-0.5"></div>
            </div>
            <div id="meal-content-water" class="meal-tab-content hidden">
                <div class="p-3 space-y-3">
                    <div class="flex items-center justify-between">
                        <span class="text-xs font-bold text-gray-600">오늘 음수량</span>
                        <span class="text-lg font-black text-sky-600" id="health-today-water-tab">-- ml</span>
                    </div>
                    <div class="w-full bg-gray-100 rounded-full h-2.5">
                        <div id="water-progress-bar" class="bg-gradient-to-r from-sky-400 to-blue-500 h-2.5 rounded-full transition-all duration-500" style="width: 0%"></div>
                    </div>
                    <div class="flex justify-between text-[10px] text-gray-400">
                        <span>0ml</span>
                        <span id="water-goal-label" class="font-semibold text-sky-500">목표: -- ml</span>
                        <span id="water-goal-max">--ml</span>
                    </div>
                    <p class="text-[10px] text-gray-400 text-center">체중 기반 권장 음수량 (50ml/kg)</p>
                </div>
            </div>
        </div>

    </div>
    <!-- /오른쪽 컬럼 끝 -->

    </div>
    <!-- /2컬럼 레이아웃 끝 -->

</div>
`;
