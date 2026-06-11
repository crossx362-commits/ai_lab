// health.js — 건강 탭 템플릿 (티모 디자인 반영)

const HEALTH_TEMPLATE = `
<div class="space-y-5 animate-fade-in">

    <!-- 헤더 - 현대적 미니멀 -->
    <div class="glass rounded-2xl px-6 py-6 shadow-soft-lg border border-violet-100/50">
        <div class="flex items-center justify-between">
            <div class="flex items-center gap-4">
                <div class="w-14 h-14 bg-gradient-to-br from-violet-500 to-purple-600 rounded-2xl flex items-center justify-center shadow-soft">
                    <i class="fa-solid fa-heart-pulse text-white text-2xl"></i>
                </div>
                <div>
                    <h1 class="text-2xl font-bold text-gray-900 tracking-tight">건강 대시보드</h1>
                    <p class="text-sm text-gray-500 mt-1"><span id="health-pet-name" class="font-semibold text-violet-600">댕이</span>의 건강 관리</p>
                </div>
            </div>
            <button onclick="generateWeeklyHealthData()"
                class="btn-modern px-5 py-2.5 bg-violet-50 hover:bg-violet-100 text-violet-700 text-sm border border-violet-200/50">
                <i class="fa-solid fa-database text-xs mr-2"></i>데모 데이터
            </button>
        </div>
    </div>

    <!-- 월간 종합 케어 리포트 (맨 위로 이동) -->
    <div class="bg-white rounded-2xl p-5 border shadow-lg space-y-3">
        <div class="flex justify-between items-center">
            <div>
                <h3 class="font-bold text-gray-800 text-base flex items-center">
                    <i class="fa-solid fa-chart-line text-violet-500 mr-2"></i>월간 종합 케어 리포트 📊
                </h3>
                <p class="text-[11px] text-gray-400 mt-0.5">건강 트렌드 + 돌봄 일정 준수율 + AI 분석을 통합한 종합 리포트
                </p>
            </div>
            <button onclick="generateHealthReportPDF()"
                class="flex items-center gap-1.5 px-3 py-2 bg-violet-500 hover:bg-violet-600 text-white font-bold text-[11px] rounded-xl transition-all shadow-sm">
                <i class="fa-solid fa-file-pdf text-sm"></i> PDF 리포트
                <span class="text-[8px] bg-amber-100 text-amber-700 px-1.5 py-0.5 rounded font-bold">PRO</span>
            </button>
        </div>
        <div class="grid grid-cols-2 sm:grid-cols-4 gap-3">
            <div class="bg-violet-50 p-3 rounded-xl border border-violet-100 text-center">
                <div id="report-health-score" class="text-2xl font-bold text-violet-600">--</div>
                <div class="text-[10px] text-gray-500 font-bold mt-1">건강 점수</div>
            </div>
            <div class="bg-emerald-50 p-3 rounded-xl border border-emerald-100 text-center">
                <div id="report-care-rate" class="text-2xl font-bold text-emerald-600">--%</div>
                <div class="text-[10px] text-gray-500 font-bold mt-1">일정 준수율</div>
            </div>
            <div class="bg-amber-50 p-3 rounded-xl border border-amber-100 text-center">
                <div id="report-streak" class="text-2xl font-bold text-amber-600">--일</div>
                <div class="text-[10px] text-gray-500 font-bold mt-1">연속 기록</div>
            </div>
            <div class="bg-sky-50 p-3 rounded-xl border border-sky-100 text-center">
                <div id="report-ai-count" class="text-2xl font-bold text-sky-600">--회</div>
                <div class="text-[10px] text-gray-500 font-bold mt-1">AI 분석</div>
            </div>
        </div>
    </div>


    <!-- 오늘의 건강 기록 - 상단 배치 + 강조 색상 -->
    <div class="bg-gradient-to-br from-orange-50 to-amber-50 border-2 border-orange-200 rounded-3xl p-6 shadow-lg">
        <div class="flex items-center justify-between mb-5">
            <div class="flex items-center gap-2.5">
                <div class="w-10 h-10 bg-gradient-to-br from-orange-400 to-amber-500 rounded-xl flex items-center justify-center shadow-md">
                    <i class="fa-solid fa-notes-medical text-white text-lg"></i>
                </div>
                <div>
                    <h2 class="text-base font-black text-gray-800">오늘의 건강 기록</h2>
                    <p class="text-[10px] text-orange-600 font-bold">매일 기록으로 건강 관리 시작</p>
                </div>
            </div>
            <button onclick="openHealthLogModal()"
                class="px-4 py-2.5 bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 text-white font-black text-xs rounded-xl transition-all shadow-md hover:shadow-lg">
                <i class="fa-solid fa-plus mr-1"></i>기록하기
            </button>
        </div>

        <div class="grid grid-cols-3 gap-4">
            <div class="bg-white border-2 border-amber-200 rounded-2xl p-4 text-center hover:shadow-md transition-shadow">
                <div class="text-3xl mb-2">🍖</div>
                <div id="health-today-food" class="text-2xl font-black text-amber-600">--g</div>
                <div class="text-[11px] text-gray-600 mt-1 font-bold">식사량</div>
            </div>
            <div class="bg-white border-2 border-sky-200 rounded-2xl p-4 text-center hover:shadow-md transition-shadow">
                <div class="text-3xl mb-2">💧</div>
                <div id="health-today-water" class="text-2xl font-black text-sky-600">--ml</div>
                <div class="text-[11px] text-gray-600 mt-1 font-bold">음수량</div>
            </div>
            <div class="bg-white border-2 border-rose-200 rounded-2xl p-4 text-center hover:shadow-md transition-shadow">
                <div class="text-3xl mb-2">💩</div>
                <div id="health-today-poop" class="text-2xl font-black text-rose-600">--</div>
                <div class="text-[11px] text-gray-600 mt-1 font-bold">배변 상태</div>
            </div>
        </div>
    </div>

    <!-- 건강 트렌드 차트 - 파랑 배경으로 시인성 향상 -->
    <div class="bg-gradient-to-br from-sky-50 to-blue-50 rounded-3xl p-6 shadow-lg border border-sky-200">
        <div class="flex items-center justify-between mb-5">
            <div class="flex items-center gap-2.5">
                <div class="w-10 h-10 bg-gradient-to-br from-sky-400 to-blue-500 rounded-xl flex items-center justify-center shadow-md">
                    <i class="fa-solid fa-chart-line text-white text-lg"></i>
                </div>
                <div>
                    <h2 class="text-base font-black text-gray-800">7일 건강 트렌드</h2>
                    <p class="text-[10px] text-sky-600 font-bold">데이터로 보는 건강 변화</p>
                </div>
            </div>
            <button onclick="generateHealthReportPDF()"
                class="px-4 py-2 bg-gradient-to-r from-violet-500 to-purple-500 hover:from-violet-600 hover:to-purple-600 text-white font-black text-xs rounded-xl transition-all shadow-md hover:shadow-lg">
                <i class="fa-solid fa-file-pdf mr-1"></i>PDF 리포트
                <span class="text-[8px] bg-white/20 px-2 py-0.5 rounded-full ml-1">PRO</span>
            </button>
        </div>

        <!-- 사용법 안내 (데이터 없을 때만 표시) -->
        <div id="health-tutorial-main" class="hidden bg-white/60 backdrop-blur-sm p-4 rounded-xl border border-sky-300/50 mb-4">
            <div class="flex items-start gap-2">
                <span class="text-2xl">💡</span>
                <div class="flex-1 space-y-1">
                    <p class="text-xs font-black text-sky-700">건강 트렌드 사용법</p>
                    <ul class="text-xs text-gray-600 space-y-1 leading-tight">
                        <li class="flex items-start gap-1.5">
                            <span class="text-sky-500 mt-0.5">•</span>
                            <span>매일 <strong class="text-sky-600">건강 기록</strong> 버튼으로 식사·음수·배변을 기록하세요</span>
                        </li>
                        <li class="flex items-start gap-1.5">
                            <span class="text-sky-500 mt-0.5">•</span>
                            <span>7일간 기록이 쌓이면 자동으로 <strong class="text-sky-600">건강점수</strong>와 <strong class="text-sky-600">차트</strong>가 생성됩니다</span>
                        </li>
                        <li class="flex items-start gap-1.5">
                            <span class="text-sky-500 mt-0.5">•</span>
                            <span>테스트하려면 위의 <strong class="text-sky-600">데모 데이터</strong> 버튼을 눌러보세요</span>
                        </li>
                    </ul>
                </div>
            </div>
        </div>

        <div class="bg-white rounded-2xl p-4" style="min-height:120px; max-height:180px;">
            <canvas id="health-trend-chart-main"></canvas>
        </div>

        <!-- 90일 캘린더 히트맵 -->
        <div id="health-calendar-main" class="mt-4 pt-4 border-t border-sky-200"></div>
    </div>

    <!-- 🔬 심화 분석 섹션 - AI 기능 그룹화 -->
    <div class="space-y-4">
        <div class="flex items-center gap-2 px-2">
            <div class="flex-1 h-px bg-gradient-to-r from-transparent via-violet-300 to-transparent"></div>
            <h3 class="text-sm font-black text-violet-600">🔬 AI 심화 분석</h3>
            <div class="flex-1 h-px bg-gradient-to-r from-transparent via-violet-300 to-transparent"></div>
        </div>

        <!-- AI 건강 분석 - 명도 대비 강화 -->
        <div class="bg-gradient-to-br from-violet-100 to-purple-100 border-2 border-violet-300 rounded-3xl p-6 shadow-lg">
            <div class="flex items-center justify-between mb-4">
                <div class="flex items-center gap-2.5">
                    <div class="w-10 h-10 bg-gradient-to-br from-violet-500 to-purple-600 rounded-xl flex items-center justify-center shadow-md">
                        <i class="fa-solid fa-microscope text-white text-lg"></i>
                    </div>
                    <div>
                        <h2 class="text-base font-black text-gray-800">AI 건강 분석</h2>
                        <p class="text-[10px] text-violet-700 font-bold">
                            사진으로 10가지 항목 자동 분석 · 이번 달 <span id="ai-usage-count-health">0</span>/5회 사용
                        </p>
                    </div>
                </div>
                <div class="flex items-center gap-2">
                    <button id="ai-voice-btn-health" onclick="startVoiceConsultation()"
                        class="px-3 py-2 bg-violet-600 hover:bg-violet-700 text-white font-black text-xs rounded-xl transition-all shadow-md">
                        <i class="fa-solid fa-microphone mr-1"></i>증상 말하기
                    </button>
                    <button id="ai-health-analyze-btn-health" onclick="triggerAiHealthAnalysis()"
                        class="px-4 py-2 bg-gradient-to-r from-violet-600 to-purple-600 hover:from-violet-700 hover:to-purple-700 text-white font-black text-xs rounded-xl transition-all shadow-md">
                        <i class="fa-solid fa-camera mr-1"></i>사진 분석
                    </button>
                </div>
            </div>

            <input type="file" id="ai-health-photo-input-health" accept="image/*" class="hidden" onchange="runAiHealthAnalysis(event)">

            <div id="ai-voice-result-health" class="hidden bg-white/90 border-2 border-violet-300 rounded-xl p-3 mb-3"></div>
            <div id="ai-health-result-main" class="hidden space-y-2 mt-3"></div>
            <div id="ai-health-share-btn-wrap-health" class="hidden flex justify-end mt-3">
                <button onclick="shareHealthCard()"
                    class="px-3 py-1.5 bg-white hover:bg-violet-50 text-violet-700 font-black text-xs rounded-lg transition-all border-2 border-violet-300">
                    <i class="fa-solid fa-share-nodes mr-1"></i>공유 카드 저장
                </button>
            </div>

            <p class="text-[9px] text-violet-600 font-bold mt-3 bg-white/60 rounded-lg px-2 py-1">
                ※ 참고용 AI 분석 · 의학적 진단 아님 · 이상 시 수의사 상담
            </p>
        </div>

        <!-- AI 수의사 채팅 진입 버튼 -->
        <div class="bg-gradient-to-r from-emerald-500 to-teal-500 rounded-3xl p-5 shadow-lg flex items-center justify-between border-2 border-emerald-400">
            <div class="flex items-center gap-3">
                <div class="w-12 h-12 bg-white/30 backdrop-blur-sm rounded-xl flex items-center justify-center text-2xl shadow-inner">🏥</div>
                <div>
                    <p class="font-black text-white text-base">AI 수의사 상담</p>
                    <p class="text-[11px] text-emerald-100 font-medium">증상을 말하면 AI가 즉시 답변해요</p>
                </div>
            </div>
            <button onclick="openVetChatModal()" class="px-5 py-2.5 bg-white text-emerald-600 font-black text-sm rounded-xl shadow-md hover:bg-emerald-50 hover:shadow-lg transition-all outline-none">
                상담 시작
            </button>
        </div>
    </div>

    <!-- 식사 일지 & 밥먹는 시간 통합 (탭 형태) -->
    <div class="bg-white rounded-3xl p-6 shadow-lg border border-gray-100">
        <div class="flex items-center justify-between mb-5">
            <div class="flex items-center gap-2.5">
                <div class="w-10 h-10 bg-gradient-to-br from-amber-400 to-orange-500 rounded-xl flex items-center justify-center shadow-md">
                    <i class="fa-solid fa-bowl-food text-white text-lg"></i>
                </div>
                <div>
                    <h2 class="text-base font-black text-gray-800">식사 · 시간 · 음수 기록</h2>
                    <p class="text-[10px] text-gray-500 font-medium">건강한 식습관 관리</p>
                </div>
            </div>
            <button onclick="toggleMealForm(true)"
                class="px-4 py-2 bg-gradient-to-r from-amber-500 to-orange-500 hover:from-amber-600 hover:to-orange-600 text-white font-black text-xs rounded-xl transition-all shadow-md hover:shadow-lg">
                <i class="fa-solid fa-plus mr-1"></i>기록 추가
            </button>
        </div>

        <!-- 탭 버튼 (밥먹/시간/음수) -->
        <div class="flex gap-2 mb-4 p-1 bg-gray-100 rounded-xl">
            <button id="meal-tab-food" onclick="switchMealTab('food')" class="flex-1 py-2.5 rounded-lg font-bold text-xs transition-all bg-white text-amber-600 shadow-sm">
                <i class="fa-solid fa-utensils mr-1"></i>밥먹
            </button>
            <button id="meal-tab-time" onclick="switchMealTab('time')" class="flex-1 py-2.5 rounded-lg font-bold text-xs transition-all text-gray-500 hover:text-gray-700">
                <i class="fa-solid fa-clock mr-1"></i>시간
            </button>
            <button id="meal-tab-water" onclick="switchMealTab('water')" class="flex-1 py-2.5 rounded-lg font-bold text-xs transition-all text-gray-500 hover:text-gray-700">
                <i class="fa-solid fa-droplet mr-1"></i>음수
            </button>
        </div>

        <!-- 기록 추가 폼 -->
        <div id="meal-form" class="hidden bg-gradient-to-br from-amber-50 to-orange-50 border border-amber-200 p-4 rounded-2xl space-y-3 mb-4">
            <div class="flex items-center gap-2 text-amber-800 font-bold text-xs">
                <i class="fa-solid fa-pen-to-square"></i>
                <span>새로운 기록 추가</span>
            </div>
            <div class="grid grid-cols-2 gap-3 text-xs">
                <select id="meal-type" class="border-2 border-amber-200 rounded-xl p-2.5 outline-none bg-white font-medium focus:border-amber-400 transition-all">
                    <option value="아침">🌅 아침 밥</option>
                    <option value="점심">☀️ 점심 밥</option>
                    <option value="저녁">🌙 저녁 밥</option>
                    <option value="간식">🍖 간식 공급</option>
                </select>
                <input type="time" id="meal-time" class="border-2 border-amber-200 rounded-xl p-2.5 outline-none bg-white font-medium focus:border-amber-400 transition-all">
            </div>
            <input type="text" id="meal-notes" placeholder="사료명, 양, 칼로리 (예: 연어 습식 80g, 120kcal)"
                class="w-full text-xs border-2 border-amber-200 rounded-xl p-2.5 outline-none bg-white font-medium focus:border-amber-400 transition-all">
            <div class="flex gap-2 text-xs">
                <button onclick="toggleMealForm(false)" class="flex-1 bg-white border-2 border-gray-200 font-bold py-2.5 rounded-xl hover:bg-gray-50 transition-all">취소</button>
                <button onclick="saveMealRecord()" class="flex-1 bg-gradient-to-r from-amber-500 to-orange-500 hover:from-amber-600 hover:to-orange-600 text-white font-bold py-2.5 rounded-xl shadow-md transition-all">저장하기</button>
            </div>
        </div>

        <!-- 탭 컨텐츠 -->
        <div id="meal-content-food" class="meal-tab-content">
            <div id="meal-list" class="space-y-2 max-h-80 overflow-y-auto"></div>
        </div>
        <div id="meal-content-time" class="meal-tab-content hidden">
            <div class="text-center py-8 text-gray-400">
                <i class="fa-solid fa-clock text-4xl mb-3"></i>
                <p class="text-sm font-medium">밥 먹는 시간 분석</p>
                <p class="text-xs mt-1">곧 업데이트 예정입니다</p>
            </div>
        </div>
        <div id="meal-content-water" class="meal-tab-content hidden">
            <div class="bg-gradient-to-br from-sky-50 to-blue-50 border-2 border-sky-200 rounded-2xl p-4 text-center">
                <div class="flex items-center justify-center gap-3 mb-3">
                    <div class="text-4xl font-black text-sky-600" id="health-today-water-tab">-- ml</div>
                </div>
                <p class="text-xs text-gray-600 font-medium">오늘 음수량</p>
                <p class="text-[10px] text-gray-400 mt-1">💧 토토한 리터링(ENTH) 타입입니다! 체력 소모가 큰 타입이므로 수분 섭취 습관이 중요합니다.</p>
            </div>
        </div>
    </div>


</div>
`;
