// health.js — 건강 탭 템플릿 (티모 디자인 반영)

const HEALTH_TEMPLATE = `
<div class="space-y-4 animate-fade-in">

    <!-- 헤더 -->
    <div class="bg-gradient-to-r from-emerald-50 to-teal-50 border border-emerald-200 rounded-2xl px-5 py-4 shadow-sm">
        <div class="flex items-center justify-between">
            <div>
                <h1 class="text-xl font-black text-gray-800 flex items-center gap-2">
                    <i class="fa-solid fa-heart-pulse text-emerald-500"></i>
                    건강 종합 대시보드
                </h1>
                <p class="text-xs text-gray-500 mt-1">🐾 <span id="health-pet-name">댕이</span>의 건강을 한눈에 확인하세요</p>
            </div>
            <button onclick="generateWeeklyHealthData()"
                class="px-3 py-2 bg-emerald-500 hover:bg-emerald-600 text-white font-black text-xs rounded-xl transition-all shadow-sm">
                <i class="fa-solid fa-database text-xs mr-1"></i>데모 데이터
            </button>
        </div>
    </div>

    <!-- 건강 요약 카드 4개 -->
    <div class="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <div class="bg-gradient-to-br from-violet-50 to-purple-50 border border-violet-200 rounded-2xl p-4 text-center">
            <div class="text-3xl font-black text-violet-600" id="health-summary-score">--</div>
            <div class="text-xs text-gray-600 font-bold mt-1">건강 점수</div>
            <div class="text-[9px] text-gray-400 mt-0.5">최근 7일 평균</div>
        </div>
        <div class="bg-gradient-to-br from-emerald-50 to-teal-50 border border-emerald-200 rounded-2xl p-4 text-center">
            <div class="text-3xl font-black text-emerald-600" id="health-summary-streak">--일</div>
            <div class="text-xs text-gray-600 font-bold mt-1">연속 기록</div>
            <div class="text-[9px] text-gray-400 mt-0.5">꾸준히 기록중!</div>
        </div>
        <div class="bg-gradient-to-br from-amber-50 to-orange-50 border border-amber-200 rounded-2xl p-4 text-center">
            <div class="text-3xl font-black text-amber-600" id="health-summary-food">--g</div>
            <div class="text-xs text-gray-600 font-bold mt-1">평균 식사량</div>
            <div class="text-[9px] text-gray-400 mt-0.5">7일 평균</div>
        </div>
        <div class="bg-gradient-to-br from-sky-50 to-blue-50 border border-sky-200 rounded-2xl p-4 text-center">
            <div class="text-3xl font-black text-sky-600" id="health-summary-water">--ml</div>
            <div class="text-xs text-gray-600 font-bold mt-1">평균 음수량</div>
            <div class="text-[9px] text-gray-400 mt-0.5">7일 평균</div>
        </div>
    </div>

    <!-- 건강 트렌드 차트 -->
    <div class="bg-white border border-emerald-100 rounded-2xl p-5 shadow-sm">
        <div class="flex items-center justify-between mb-4">
            <h2 class="text-base font-black text-gray-800 flex items-center gap-2">
                <i class="fa-solid fa-chart-line text-emerald-500"></i>
                7일 건강 트렌드
            </h2>
            <button onclick="generateHealthReportPDF()"
                class="px-3 py-1.5 bg-violet-500 hover:bg-violet-600 text-white font-black text-xs rounded-lg transition-all">
                <i class="fa-solid fa-file-pdf mr-1"></i>PDF 리포트
                <span class="text-[8px] bg-amber-100 text-amber-700 px-1.5 py-0.5 rounded ml-1">PRO</span>
            </button>
        </div>

        <!-- 사용법 안내 (데이터 없을 때만 표시) -->
        <div id="health-tutorial-main" class="hidden bg-emerald-50/60 backdrop-blur-sm p-4 rounded-xl border border-emerald-200/50 mb-4">
            <div class="flex items-start gap-2">
                <span class="text-2xl">💡</span>
                <div class="flex-1 space-y-1">
                    <p class="text-xs font-black text-emerald-700">건강 트렌드 사용법</p>
                    <ul class="text-xs text-gray-600 space-y-1 leading-tight">
                        <li class="flex items-start gap-1.5">
                            <span class="text-emerald-500 mt-0.5">•</span>
                            <span>매일 <strong class="text-emerald-600">건강 기록</strong> 버튼으로 식사·음수·배변을 기록하세요</span>
                        </li>
                        <li class="flex items-start gap-1.5">
                            <span class="text-emerald-500 mt-0.5">•</span>
                            <span>7일간 기록이 쌓이면 자동으로 <strong class="text-emerald-600">건강점수</strong>와 <strong class="text-emerald-600">차트</strong>가 생성됩니다</span>
                        </li>
                        <li class="flex items-start gap-1.5">
                            <span class="text-emerald-500 mt-0.5">•</span>
                            <span>테스트하려면 위의 <strong class="text-emerald-600">데모 데이터</strong> 버튼을 눌러보세요</span>
                        </li>
                    </ul>
                </div>
            </div>
        </div>

        <div style="min-height:120px; max-height:180px;">
            <canvas id="health-trend-chart-main"></canvas>
        </div>

        <!-- 90일 캘린더 히트맵 -->
        <div id="health-calendar-main" class="mt-4 pt-4 border-t border-gray-100"></div>
    </div>

    <!-- 오늘의 건강 기록 -->
    <div class="bg-white border border-teal-100 rounded-2xl p-5 shadow-sm">
        <div class="flex items-center justify-between mb-4">
            <h2 class="text-base font-black text-gray-800 flex items-center gap-2">
                <i class="fa-solid fa-notes-medical text-teal-500"></i>
                오늘의 건강 기록
            </h2>
            <button onclick="openHealthLogModal()"
                class="px-3 py-1.5 bg-teal-500 hover:bg-teal-600 text-white font-black text-xs rounded-lg transition-all">
                <i class="fa-solid fa-plus mr-1"></i>기록하기
            </button>
        </div>

        <div class="grid grid-cols-3 gap-3">
            <div class="bg-amber-50 border border-amber-200 rounded-xl p-3 text-center">
                <div class="text-2xl mb-1">🍖</div>
                <div id="health-today-food" class="text-xl font-black text-amber-600">--g</div>
                <div class="text-[10px] text-gray-500 mt-0.5">식사량</div>
            </div>
            <div class="bg-sky-50 border border-sky-200 rounded-xl p-3 text-center">
                <div class="text-2xl mb-1">💧</div>
                <div id="health-today-water" class="text-xl font-black text-sky-600">--ml</div>
                <div class="text-[10px] text-gray-500 mt-0.5">음수량</div>
            </div>
            <div class="bg-rose-50 border border-rose-200 rounded-xl p-3 text-center">
                <div class="text-2xl mb-1">💩</div>
                <div id="health-today-poop" class="text-xl font-black text-rose-600">--</div>
                <div class="text-[10px] text-gray-500 mt-0.5">배변 상태</div>
            </div>
        </div>
    </div>

    <!-- AI 건강 분석 -->
    <div class="bg-gradient-to-br from-violet-50 to-purple-50 border border-violet-200 rounded-2xl p-5 shadow-sm">
        <div class="flex items-center justify-between mb-3">
            <div>
                <h2 class="text-base font-black text-gray-800 flex items-center gap-2">
                    <i class="fa-solid fa-microscope text-violet-500"></i>
                    AI 건강 분석
                </h2>
                <p class="text-[10px] text-violet-600 font-medium mt-0.5">
                    사진으로 10가지 항목 자동 분석 · 이번 달 <span id="ai-usage-count-health">0</span>/3회 사용
                </p>
            </div>
            <div class="flex items-center gap-2">
                <button id="ai-voice-btn-health" onclick="startVoiceConsultation()"
                    class="px-3 py-1.5 bg-violet-500 hover:bg-violet-600 text-white font-black text-xs rounded-lg transition-all">
                    <i class="fa-solid fa-microphone mr-1"></i>증상 말하기
                </button>
                <button id="ai-health-analyze-btn-health" onclick="triggerAiHealthAnalysis()"
                    class="px-3 py-1.5 bg-violet-600 hover:bg-violet-700 text-white font-black text-xs rounded-lg transition-all">
                    <i class="fa-solid fa-camera mr-1"></i>사진 분석
                </button>
            </div>
        </div>

        <input type="file" id="ai-health-photo-input-health" accept="image/*" class="hidden" onchange="runAiHealthAnalysis(event)">

        <div id="ai-voice-result-health" class="hidden bg-white/80 border border-violet-200 rounded-xl p-3 mb-3"></div>
        <div id="ai-health-result-main" class="hidden space-y-2 mt-3"></div>
        <div id="ai-health-share-btn-wrap-health" class="hidden flex justify-end mt-3">
            <button onclick="shareHealthCard()"
                class="px-3 py-1.5 bg-violet-100 hover:bg-violet-200 text-violet-700 font-black text-xs rounded-lg transition-all">
                <i class="fa-solid fa-share-nodes mr-1"></i>공유 카드 저장
            </button>
        </div>

        <p class="text-[9px] text-violet-400 font-medium mt-3">
            ※ 참고용 AI 분석 · 의학적 진단 아님 · 이상 시 수의사 상담
        </p>
    </div>

    <!-- 식사 일지 -->
    <div class="bg-white border border-amber-100 rounded-2xl p-5 shadow-sm">
        <div class="flex items-center justify-between mb-4">
            <h2 class="text-base font-black text-gray-800 flex items-center gap-2">
                <i class="fa-solid fa-bowl-food text-amber-500"></i>
                식사 일지 & 밥 먹는 시간
            </h2>
            <button onclick="toggleMealForm(true)"
                class="px-3 py-1.5 bg-amber-500 hover:bg-amber-600 text-white font-black text-xs rounded-lg transition-all">
                <i class="fa-solid fa-plus mr-1"></i>기록 추가
            </button>
        </div>

        <div id="meal-form" class="hidden bg-amber-50/50 border border-amber-200 p-3 rounded-xl space-y-2.5 mb-3">
            <span class="block text-xs text-amber-800 font-bold">
                <i class="fa-solid fa-clock mr-1"></i>새로운 배식 활동 기록
            </span>
            <div class="grid grid-cols-2 gap-2 text-xs">
                <select id="meal-type" class="border rounded-lg p-2 outline-none bg-white">
                    <option value="아침">🌅 아침 밥</option>
                    <option value="점심">☀️ 점심 밥</option>
                    <option value="저녁">🌙 저녁 밥</option>
                    <option value="간식">🍖 간식 공급</option>
                </select>
                <input type="time" id="meal-time" class="border rounded-lg p-2 outline-none bg-white">
            </div>
            <input type="text" id="meal-notes" placeholder="사료명, 칼로리 혹은 반응 기재 (예: 연어 습식 80g)"
                class="w-full text-xs border rounded-lg p-2 outline-none bg-white">
            <div class="flex gap-2 text-xs">
                <button onclick="toggleMealForm(false)" class="flex-1 bg-white border font-bold py-2 rounded-lg">취소</button>
                <button onclick="saveMealRecord()" class="flex-1 bg-amber-500 hover:bg-amber-600 text-white font-bold py-2 rounded-lg">저장하기</button>
            </div>
        </div>

        <div id="meal-list" class="space-y-2 max-h-60 overflow-y-auto"></div>
    </div>

</div>
`;
