// health.js — 건강 탭 템플릿 (티모 디자인 반영 + 아이콘 중심 UI)

const HEALTH_TEMPLATE = `
<div class="space-y-4 animate-fade-in">

    <!-- 헤더 + 조기감지 묶음 — 헤더 카드와 예측웰니스/주간리포트 카드를 한 장으로 병합
         (2026-07-26 오너 지시). 헤더는 제목·컨트롤뿐이라 아래가 비었고, 상태 두 줄도
         짧아 카드 두 장으로 나뉠 이유가 없었다. card-merge가 내부 테두리를 지우고
         행 사이에 구분선을 넣는다. -->
    <div class="glass rounded-2xl shadow-soft-lg border border-brand-100/50 overflow-hidden">
        <div class="p-5">
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
                    class="h-9 px-4 py-0 bg-white border border-brand-200 rounded-xl text-sm font-bold text-brand-700 hover:border-brand-300 transition-colors cursor-pointer">
                    <option value="">펫 선택</option>
                </select>
                <button onclick="generateWeeklyHealthData()"
                    class="btn-modern inline-flex items-center h-9 px-4 py-0 bg-brand-50 hover:bg-brand-100 text-brand-700 text-sm border border-brand-200/50">
                    <i class="fa-solid fa-database text-xs mr-2"></i>데모 데이터
                </button>
            </div>
        </div>
        </div>

        <!-- 예측 웰니스·주간 리포트: 데이터가 적을 땐 큰 빈 카드 2장이 모바일에서 핵심 '오늘의
             기록'을 아래로 밀어냈다. <details>로 접힌 1줄 요약으로 축소해 오늘의 기록을 먼저
             노출한다(미오 P2, 2026-08-05). 데스크톱(lg)에서는 요약을 숨기고 본문을 항상 펼쳐
             기존 2컬럼 레이아웃을 유지한다(css/style.css .health-top-analysis). 실제 이상
             소견(amber 카드)이 있으면 renderHealthTab이 open 속성을 켜 모바일에서도 자동 노출. -->
        <details id="health-top-analysis" class="health-top-analysis group/topfold border-t border-gray-100">
            <summary class="lg:hidden flex items-center gap-2 px-5 py-3 cursor-pointer list-none select-none text-sm font-semibold text-gray-600">
                <span class="text-lg">🔮</span>
                <span>예측 웰니스 · 주간 리포트</span>
                <span class="text-xs font-normal text-gray-400">건강 신호 요약</span>
                <i class="fa-solid fa-chevron-down text-[10px] text-gray-400 ml-auto transition-transform group-open/topfold:rotate-180"></i>
            </summary>
            <div class="health-top-analysis-body lg:grid lg:grid-cols-2 lg:gap-x-4 lg:items-start">
                <!-- 🔮 예측 웰니스 이상감지 (wellness-anomaly.js가 채움) -->
                <div id="wellness-anomaly-card"></div>

                <!-- 📈 주간 건강 변화 조기경보 (weekly-report.js가 채움) -->
                <div id="weekly-report-card"></div>

                <!-- 🏆 케어 스코어 & Proof-of-Care 공유 (care-score.js가 채움) -->
                <div id="care-score-card"></div>
            </div>
        </details>
    </div>

    <!-- 펫이 없을 때 — 위젯들이 '--g / 0회'만 늘어놓는 대신 등록을 유도한다
         (2026-07-26 2차 회의: 신규 사용자가 건강 탭에서 아무 안내 없이 빈 숫자만 봄).
         renderHealthTab()이 펫 0마리일 때 이걸 켜고 아래 2컬럼을 숨긴다. -->
    <div id="health-empty-state" class="hidden card-modern p-8 text-center space-y-4">
        <div class="text-6xl">🩺</div>
        <div class="space-y-1.5">
            <p class="text-lg font-black text-gray-800 keep-all">반려동물을 먼저 등록해주세요</p>
            <p class="text-xs text-gray-400 font-medium leading-relaxed keep-all">
                등록하면 식사·음수 기록, 건강 트렌드, AI 건강분석을<br>바로 쓸 수 있어요
            </p>
        </div>
        <button onclick="switchTab('mypet')"
            class="btn-modern bg-brand-500 hover:bg-brand-600 text-white px-5 py-2.5 text-sm font-bold">
            <i class="fa-solid fa-plus mr-1.5"></i>마이펫에서 등록하기
        </button>
    </div>

    <!-- 2컬럼 레이아웃 -->
    <div id="health-main-grid" class="grid grid-cols-1 lg:grid-cols-12 gap-4 items-start">

    <!-- 왼쪽 컬럼 (메인 콘텐츠) -->
    <div class="lg:col-span-8 space-y-4">

    <!-- 섹션 헤더로 좌측 카드들을 3개 논리 그룹(오늘 기록/건강 관리/AI 상담·도구)으로
         묶어 시각 위계 부여(미오 P1, 2026-07-29). 케어 위젯 소제목과 동일 스타일. -->
    <div class="flex items-center gap-2 px-1">
        <span class="text-base">📋</span>
        <span class="text-[11px] font-black text-gray-400 tracking-wide">오늘 기록</span>
    </div>

    <!-- 📋 오늘의 기록 & 트렌드 — 같은 식사·음수·배변 데이터의 입력(오늘)과 조회(7일·90일)라
         별도 카드 2장이던 것을 한 카드로 병합(2026-07-19). 이 섹션의 핵심 카드라
         그림자를 강조(shadow-soft-lg)해 아래 보조 카드와 위계를 준다.
         id는 접기 상태 키를 안정화하고, 모바일 초기 진입 시 하위 카드를 접을 때
         이 핵심 요약 카드만 펼친 채로 두기 위한 화이트리스트 앵커다(미오 P2, 2026-08-04). -->
    <div id="health-today-record-card" class="card-modern shadow-soft-lg p-5">
        <div class="flex items-center justify-between mb-4">
            <div class="flex items-center gap-3">
                <div class="text-3xl">📋</div>
                <div>
                    <h2 class="text-base font-semibold text-gray-900">오늘의 기록</h2>
                    <p class="text-xs text-gray-500">빠른 건강 체크</p>
                </div>
            </div>
            <!-- 헤더 [기록] 버튼 제거(2026-07-28 회의) — 아래 타일의 −/+ 로 바로 기록하고,
                 정확한 수치는 값 자체를 눌러 모달로 넣는다. 이 버튼까지 있으면 같은 모달을
                 여는 진입점이 한 카드 안에 셋이 된다. -->
        </div>

        <!-- 배변 타일은 제거(2026-07-26) — 아래 '원탭 컨디션'의 배변 칩과 같은 필드
             (healthLogs.today.poop)를 읽어 같은 값을 두 번 보여주고 있었다.
             수치 입력(섭취)은 여기, 상태 입력은 원탭으로 역할을 나눈다. -->
        <!-- 식사·음수 인라인 스테퍼(2026-07-28 회의) — 값 표시만 하던 타일이 입력을 겸한다.
             −/+ 는 값 양옆에 두어 카드 높이는 그대로다. 정확한 수치는 값을 눌러 모달로.
             (모달은 마이펫 아이콘 바·오늘의 퀘스트·주간 챌린지도 부르므로 존치한다 —
             회의는 건강 탭만 보고 '모달 폐기'를 논했으나 실제 호출부가 3곳 더 있었다.)
             375px에서는 슬라이더보다 −/+ 가 오조작이 적다는 게 회의 다수 의견.
             넓은 데스크톱에서 두 타일이 과도하게 늘어나 스테퍼 주변 여백만 커지던 것을
             max-w-md mx-auto로 묶어 중앙 밀도를 확보(미오 P2, 2026-08-04). 모바일은
             폭이 max-w-md 미만이라 기존과 동일하게 꽉 찬다. -->
        <div class="grid grid-cols-2 gap-3 max-w-md mx-auto">
            <!-- 식사량 -->
            <div class="card-modern bg-gradient-to-br from-amber-50 to-orange-50 p-4 text-center">
                <div class="text-3xl mb-1">🍖</div>
                <div class="flex items-center justify-center gap-1.5 mb-1">
                    <button type="button" id="food-step-minus" onclick="adjustTodayIntake('food', -10)"
                        aria-label="식사량 10g 줄이기"
                        class="w-8 h-8 shrink-0 rounded-full bg-white/80 border border-amber-200 text-amber-600 font-black
                               hover:bg-white active:scale-95 transition-all outline-none">−</button>
                    <button type="button" id="health-today-food" onclick="openHealthLogModal()"
                        aria-label="식사량 정확히 입력"
                        class="text-2xl font-bold text-amber-600 px-1 rounded hover:bg-white/70 transition-colors outline-none">--g</button>
                    <button type="button" id="food-step-plus" onclick="adjustTodayIntake('food', 10)"
                        aria-label="식사량 10g 늘리기"
                        class="w-8 h-8 shrink-0 rounded-full bg-white/80 border border-amber-200 text-amber-600 font-black
                               hover:bg-white active:scale-95 transition-all outline-none">+</button>
                </div>
                <div class="text-xs text-gray-600 font-semibold">식사량</div>
            </div>

            <!-- 음수량 -->
            <div class="card-modern bg-gradient-to-br from-sky-50 to-blue-50 p-4 text-center">
                <div class="text-3xl mb-1">💧</div>
                <div class="flex items-center justify-center gap-1.5 mb-1">
                    <button type="button" id="water-step-minus" onclick="adjustTodayIntake('water', -50)"
                        aria-label="음수량 50ml 줄이기"
                        class="w-8 h-8 shrink-0 rounded-full bg-white/80 border border-sky-200 text-sky-600 font-black
                               hover:bg-white active:scale-95 transition-all outline-none">−</button>
                    <button type="button" id="health-today-water" onclick="openHealthLogModal()"
                        aria-label="음수량 정확히 입력"
                        class="text-2xl font-bold text-sky-600 px-1 rounded hover:bg-white/70 transition-colors outline-none">--ml</button>
                    <button type="button" id="water-step-plus" onclick="adjustTodayIntake('water', 50)"
                        aria-label="음수량 50ml 늘리기"
                        class="w-8 h-8 shrink-0 rounded-full bg-white/80 border border-sky-200 text-sky-600 font-black
                               hover:bg-white active:scale-95 transition-all outline-none">+</button>
                </div>
                <div class="text-xs text-gray-600 font-semibold">음수량</div>
            </div>

        </div>

        <!-- 📝 원탭 컨디션 로그 (daily-condition.js가 채움) — 2026-07-26 오너 지시로
             '오늘의 컨디션 원탭 기록' 별도 카드를 이 카드에 흡수. 같은 날의 같은 기록을
             카드 두 장이 따로 묻던 구조를 없앤다(배변이 양쪽에 중복 노출됐다). -->
        <div id="daily-condition-widget" class="mt-4"></div>

        <!-- 📈 7일 건강 트렌드 — 2026-07-26 회의 다수안(입력↔조회 인접) + 오너 승인으로
             건강수첩 묶음에서 이 카드로 되돌림. 같은 식사·음수 데이터의 입력(위)과
             조회(아래)라 한 카드에 붙어 있어야 동선이 짧다. -->
        <div class="mt-5 pt-4 border-t border-gray-100">
        <!-- 📈 7일 건강 트렌드 — 2026-07-26 이 카드로 옮기며 상단 구분선(mt-5 pt-4 border-t)
             제거. 원래 '오늘의 기록' 안에서 위 내용과 나누는 선이었는데, 카드 최상단이 되면서
             위에 아무것도 없는 채 선만 떠 있었다(회의에서 테오가 지목). -->
        <div class="flex items-center gap-2 mb-3">
            <span class="text-xl">📈</span>
            <span class="text-sm font-bold text-gray-900">7일 건강 트렌드</span>
            <span class="text-xs text-gray-400">데이터로 보는 변화</span>
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

        <!-- 배변 건강 월간 캘린더 (상태별 색상 코딩) -->
        <div id="poop-calendar-main" class="mt-4 pt-4 border-t border-gray-200"></div>
        </div>

    </div>


    <!-- 섹션: 건강 관리 (팁·건강수첩·비용) -->
    <div class="flex items-center gap-2 px-1 pt-2">
        <span class="text-base">🗂️</span>
        <span class="text-[11px] font-black text-gray-400 tracking-wide">건강 관리 · 목표</span>
    </div>

    <div id="daily-care-tip-widget"></div>

    <!-- 📔 건강수첩 묶음 — 건강수첩(의료 이력) 아래로 영양 관리·일일 급식/칼로리·
         맞춤 식단·7일 트렌드를 흡수해 한 카드로 병합(2026-07-26 오너 지시).
         왼쪽 컬럼에 흩어져 있던 카드 5장을 한 장으로 줄인다.
         card-merge가 각 위젯이 그리는 내부 card-modern 테두리를 지우고 사이에 구분선을 넣는다. -->
    <div id="health-passport-group" class="card-modern card-merge overflow-hidden">
    <div class="overflow-hidden">
        <div class="px-5 pt-4 pb-3 border-b border-gray-100 flex items-center justify-between">
            <h2 class="text-base font-semibold text-gray-900 flex items-center gap-2">
                <i class="fa-solid fa-notes-medical text-brand-500"></i>건강수첩
            </h2>
            <div class="flex items-center gap-1.5">
                <button onclick="PetPassport.open()" class="text-xs font-bold text-brand-600 bg-brand-50 hover:bg-brand-100 border border-brand-100 px-3 py-1.5 rounded-full transition-all">
                    <i class="fa-solid fa-id-card mr-1"></i>응급·여행 프로필
                </button>
                <button onclick="shareVetVisitCard()" class="text-xs font-bold text-sky-600 bg-sky-50 hover:bg-sky-100 border border-sky-100 px-3 py-1.5 rounded-full transition-all">
                    <i class="fa-solid fa-hospital mr-1"></i>병원 준비 카드
                </button>
                <button onclick="exportMedicalRecordsPDF()" class="text-xs font-bold text-brand-600 bg-brand-50 hover:bg-brand-100 border border-brand-100 px-3 py-1.5 rounded-full transition-all">
                    <i class="fa-solid fa-file-pdf mr-1"></i>PDF 내보내기
                </button>
                <button onclick="InsuranceClaim.open()" class="text-xs font-bold text-emerald-600 bg-emerald-50 hover:bg-emerald-100 border border-emerald-100 px-3 py-1.5 rounded-full transition-all">
                    <i class="fa-solid fa-file-invoice-dollar mr-1"></i>보험 청구 패키지
                </button>
                <button onclick="openMedicalRecordModal()" class="text-xs font-bold text-white bg-brand-500 hover:bg-brand-600 px-3 py-1.5 rounded-full transition-all shadow-soft">
                    <i class="fa-solid fa-plus mr-1"></i>기록 추가
                </button>
            </div>
        </div>
        <div class="px-5 py-4">
            <div id="medical-records-timeline"></div>
        </div>
    </div>

    <!-- 🍖 영양 관리 (별도 카드 → 이 묶음으로 흡수) -->
    <!-- 🍖 영양 관리 섹션 -->
    <div class="card-modern p-5">
        <div class="flex items-center justify-between mb-4">
            <div class="flex items-center gap-2">
                <span class="text-3xl">🍖</span>
                <div>
                    <h3 class="text-base font-semibold text-gray-900">영양 관리</h3>
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
            <div id="meal-list" class="grid grid-cols-1 lg:grid-cols-2 gap-1.5 content-start max-h-52 overflow-y-auto pr-0.5"></div>
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

    <!-- 🍽️ 일일 급식·칼로리 + 🍚 맞춤 식단 추천 -->
    <div id="calorie-tracker-widget"></div>
    <div id="diet-recommend-widget"></div>

    </div>

    <!-- 💰 비용 묶음 — 가계부(내 지출)와 병원비 보드(동네 평균가)를 한 카드로 병합
         (2026-07-26 오너 지시). 둘 다 '돈' 주제인데 카드 두 장으로 나뉘어 있었고,
         가계부는 기록이 없으면 179px짜리 빈 카드만 덩그러니 남았다.
         card-merge가 내부 card-modern 테두리를 지우고 사이에 구분선을 넣는다. -->
    <div id="health-cost-group" class="card-modern card-merge overflow-hidden">
        <!-- 💰 반려동물 가계부 (백로그 나무, P3 — 카테고리별 지출·월별 집계) -->
        <div id="expense-tracker-widget"></div>

        <!-- 🏥 병원비 제보·비교 보드 (케어위젯 비용 그룹에서 이동 — 건강수첩과 짝, 2026-07-21) -->
        <div id="vet-cost-board-widget"></div>

        <!-- 📦 재고 소진 D-day 추적기 (백로그 나무, P2 — 사료·용품 잔량·재구매 넛지) -->
        <div id="inventory-tracker-widget"></div>
    </div>

    <!-- 섹션: AI 상담·도구 -->
    <div class="flex items-center gap-2 px-1 pt-2">
        <span class="text-base">🤖</span>
        <span class="text-[11px] font-black text-gray-400 tracking-wide">AI 상담 · 도구</span>
    </div>

    <!-- 🤖 AI 기능 — 흩어진 카드 4장 + 안내 카드를 한 카드 안 타일로 병합(2026-07-19) -->
    <div class="card-modern p-5 space-y-4">
        <div class="flex items-center gap-3">
            <div class="text-3xl">🤖</div>
            <div>
                <h3 class="text-base font-semibold text-gray-900">AI 기능</h3>
                <p class="text-xs text-gray-500">사진·채팅·음성으로 빠른 건강 체크</p>
            </div>
        </div>

        <!-- AI 기능 타일 그리드 -->
        <div class="grid grid-cols-1 md:grid-cols-2 gap-3">

            <!-- AI 건강 분석 -->
            <button onclick="triggerAiHealthAnalysis()" class="rounded-xl border border-gray-100 bg-gradient-to-br from-brand-50 to-brand-50 p-4 text-left group hover:scale-[1.02] transition-all">
                <div class="flex items-start justify-between mb-3">
                    <div class="flex items-center gap-3">
                        <div class="text-4xl">🔬</div>
                        <div>
                            <h3 class="text-base font-bold text-gray-900">건강 분석</h3>
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
            <button onclick="openVetChatModal()" class="rounded-xl border border-gray-100 bg-gradient-to-br from-emerald-50 to-teal-50 p-4 text-left group hover:scale-[1.02] transition-all">
                <div class="flex items-start justify-between mb-3">
                    <div class="flex items-center gap-3">
                        <div class="text-4xl">🏥</div>
                        <div>
                            <h3 class="text-base font-bold text-gray-900">AI 수의사</h3>
                            <p class="text-xs text-emerald-600 mt-1">증상 상담 즉시 답변</p>
                        </div>
                    </div>
                </div>
                <div class="flex items-center justify-between">
                    <p class="text-xs text-gray-500">24시간 언제든지</p>
                    <i class="fa-solid fa-comment-medical text-emerald-500 text-xl group-hover:scale-110 transition-transform"></i>
                </div>
            </button>

            <!-- 증상 빠른 진단 (규칙 기반 트리아지 — AI 백엔드 없이도 작동) -->
            <button onclick="SymptomTriage.open()" class="rounded-xl border border-gray-100 bg-gradient-to-br from-rose-50 to-pink-50 p-4 text-left group hover:scale-[1.02] transition-all">
                <div class="flex items-start justify-between mb-3">
                    <div class="flex items-center gap-3">
                        <div class="text-4xl">🩺</div>
                        <div>
                            <h3 class="text-base font-bold text-gray-900">증상 빠른 진단</h3>
                            <p class="text-xs text-rose-600 mt-1">이 아이 기준 긴급도 안내</p>
                        </div>
                    </div>
                </div>
                <div class="flex items-center justify-between">
                    <p class="text-xs text-gray-500">견종·나이·체중 반영</p>
                    <i class="fa-solid fa-stethoscope text-rose-500 text-xl group-hover:scale-110 transition-transform"></i>
                </div>
            </button>

            <!-- 응급 카드 (상황별 1차 처치 + 24h 병원 연결 — 정적, 백엔드 없이 작동) -->
            <button onclick="EmergencyCard.open()" class="rounded-xl border border-gray-100 bg-gradient-to-br from-red-50 to-rose-50 p-4 text-left group hover:scale-[1.02] transition-all">
                <div class="flex items-start justify-between mb-3">
                    <div class="flex items-center gap-3">
                        <div class="text-4xl">🚨</div>
                        <div>
                            <h3 class="text-base font-bold text-gray-900">응급 처치 가이드</h3>
                            <p class="text-xs text-red-600 mt-1">1차 처치 + 24h 병원</p>
                        </div>
                    </div>
                </div>
                <div class="flex items-center justify-between">
                    <p class="text-xs text-gray-500">중독·이물질·경련 등</p>
                    <i class="fa-solid fa-kit-medical text-red-500 text-xl group-hover:scale-110 transition-transform"></i>
                </div>
            </button>

            <!-- 급여 가능 음식 검색기 "이거 먹어도 돼?" (정적 안전/주의/금지 표 — 백엔드 없이 작동) -->
            <button onclick="FoodSafety.open()" class="rounded-xl border border-gray-100 bg-gradient-to-br from-lime-50 to-green-50 p-4 text-left group hover:scale-[1.02] transition-all">
                <div class="flex items-start justify-between mb-3">
                    <div class="flex items-center gap-3">
                        <div class="text-4xl">🍽️</div>
                        <div>
                            <h3 class="text-base font-bold text-gray-900">이거 먹어도 돼?</h3>
                            <p class="text-xs text-green-600 mt-1">음식 안전 검색</p>
                        </div>
                    </div>
                </div>
                <div class="flex items-center justify-between">
                    <p class="text-xs text-gray-500">개·고양이 안전/주의/금지</p>
                    <i class="fa-solid fa-bowl-food text-green-500 text-xl group-hover:scale-110 transition-transform"></i>
                </div>
            </button>

            <!-- 사료/간식 바코드 스캔 — Open Pet Food Facts 성분 조회 + 알러지 대조 (나무 P3) -->
            <button onclick="FoodScanner.open()" class="rounded-xl border border-gray-100 bg-gradient-to-br from-indigo-50 to-violet-50 p-4 text-left group hover:scale-[1.02] transition-all">
                <div class="flex items-start justify-between mb-3">
                    <div class="flex items-center gap-3">
                        <div class="text-4xl">📷</div>
                        <div>
                            <h3 class="text-base font-bold text-gray-900">바코드 스캔</h3>
                            <p class="text-xs text-indigo-600 mt-1">사료·간식 성분 조회</p>
                        </div>
                    </div>
                </div>
                <div class="flex items-center justify-between">
                    <p class="text-xs text-gray-500">알러지 성분 자동 대조</p>
                    <i class="fa-solid fa-barcode text-indigo-500 text-xl group-hover:scale-110 transition-transform"></i>
                </div>
            </button>

            <!-- 성분표 텍스트 붙여넣기 → 독성·알러지·기저질환 교차검증 + 안전 등급 (나무 P3) -->
            <button onclick="IngredientScanner.open()" class="rounded-xl border border-gray-100 bg-gradient-to-br from-amber-50 to-orange-50 p-4 text-left group hover:scale-[1.02] transition-all">
                <div class="flex items-start justify-between mb-3">
                    <div class="flex items-center gap-3">
                        <div class="text-4xl">🧪</div>
                        <div>
                            <h3 class="text-base font-bold text-gray-900">성분 안전 스캐너</h3>
                            <p class="text-xs text-amber-600 mt-1">성분표 붙여넣기 검사</p>
                        </div>
                    </div>
                </div>
                <div class="flex items-center justify-between">
                    <p class="text-xs text-gray-500">알러지·기저질환 교차검증</p>
                    <i class="fa-solid fa-flask text-amber-500 text-xl group-hover:scale-110 transition-transform"></i>
                </div>
            </button>

        </div>

        <!-- 저빈도 항목은 접어서 모바일 스크롤 단축 (미오 P3, 2026-07-28) -->
        <details class="group/more">
            <summary class="flex items-center justify-center gap-1.5 cursor-pointer list-none text-xs font-semibold text-gray-500 hover:text-gray-700 py-1.5 select-none">
                <span>다른 방법 더보기</span>
                <i class="fa-solid fa-chevron-down text-[10px] transition-transform group-open/more:rotate-180"></i>
            </summary>
            <div class="grid grid-cols-1 md:grid-cols-2 gap-3 mt-3">
                <!-- 음성 상담 -->
                <button onclick="startVoiceConsultation()" class="rounded-xl border border-gray-100 bg-gradient-to-br from-sky-50 to-blue-50 p-4 text-left group hover:scale-[1.02] transition-all">
                    <div class="flex items-start justify-between mb-3">
                        <div class="flex items-center gap-3">
                            <div class="text-4xl">🎤</div>
                            <div>
                                <h3 class="text-base font-bold text-gray-900">음성 상담</h3>
                                <p class="text-xs text-sky-600 mt-1">증상 말로 설명하기</p>
                            </div>
                        </div>
                    </div>
                    <div class="flex items-center justify-end">
                        <i class="fa-solid fa-microphone text-sky-500 text-xl group-hover:scale-110 transition-transform"></i>
                    </div>
                </button>
            </div>
        </details>

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
        <div class="rounded-xl bg-brand-50/50 p-3">
            <p class="text-xs text-center text-brand-700">
                <i class="fa-solid fa-info-circle mr-1"></i>
                AI 분석은 참고용이며 의학적 진단이 아닙니다. 이상 시 수의사와 상담하세요.
            </p>
        </div>

    </div>
    <!-- /AI 기능 섹션 끝 -->

    </div>
    <!-- /왼쪽 컬럼 끝 -->

    <!-- 오른쪽 컬럼 (투약·정기예방 + 월간 리포트 + 건강수첩 + 영양관리 + 돌봄 스케줄러) -->
    <div class="lg:col-span-4 space-y-3 lg:sticky lg:top-20 lg:self-start">

        <!-- 케어 위젯 묶음 (각자 다른 모듈이 자체 카드로 채움 — 4개 기능군 소제목으로 재그룹화,
             2026-07-16 개선회의 결정 회의_202607162027_1 + 미오 스타일·간격 제안 반영)
             id는 care-widget-instrumentation.js의 노출/클릭 최소 계측(회의_202607162027_3)이 사용 -->
        <div id="care-widgets-group" class="space-y-2">
            <div class="flex items-center gap-2 px-1">
                <i class="fa-solid fa-kit-medical text-brand-400 text-xs"></i>
                <span class="text-[11px] font-black text-gray-400 tracking-wide">케어 위젯</span>
            </div>

            <!-- 각 그룹: 위젯이 전부 빈 상태면 updateCareWidgetGroupVisibility()가 소제목째 숨긴다
                 (투약 일정 없는 사용자에게 '투약·복약' 고아 소제목이 뜨던 버그, 2026-07-19) -->
            <!-- 투약·복약 -->
            <div data-care-group class="space-y-2">
                <span class="block text-xs font-semibold text-gray-400 uppercase tracking-wide px-1">투약·복약</span>
                <!-- 오늘 체크 + 정기예방 대시보드를 한 카드로 병합(2026-07-26 오너 지시).
                     둘 다 '무슨 약/케어가 언제'라 따로 둘 이유가 없고, 대시보드는 항목이
                     한둘일 때 아래가 크게 비어 보였다. card-merge가 내부 카드 테두리를 지운다. -->
                <div id="health-medication-group" class="card-modern card-merge overflow-hidden">
                    <!-- 💊 오늘 투약·케어 체크 (care-check.js가 채움) — 2026-07-26 마이펫에서 이동 -->
                    <div id="care-check-banner"></div>
                    <!-- 💉 투약·정기예방 대시보드 (심장사상충/구충/백신 카운트다운) -->
                    <div id="preventive-care-dashboard"></div>
                </div>
                <!-- 🗒️ 생애주기 예방케어 체크리스트 (종·나이·중성화 기반 권장 항목, 나무 P2) -->
                <div id="preventive-checklist"></div>
                <!-- 📅 생애주기 예방 타임라인 (종·나이 기반 시기별 케어 흐름 + 리마인더, 나무 P3) -->
                <div id="preventive-timeline"></div>
                <!-- 💊 복약 순응도 30일 트래커 (이번 주 놓친 약 요약 포함) -->
                <div id="med-adherence-tracker"></div>
                <!-- 💊 투약 이행 로그(연속일·이번 달 이행률) — medication-log.js가 채움 -->
                <div id="medication-log-tracker"></div>
            </div>

            <!-- 체형·체중 -->
            <div data-care-group class="space-y-2 pt-4 border-t border-gray-100">
                <span class="block text-xs font-semibold text-gray-400 uppercase tracking-wide px-1">체형·체중</span>
                <!-- 💛 몸무게/QOL 주간 체크인 -->
                <div id="qol-checkin-widget"></div>
                <!-- 🐾 BCS 체형 셀프체크 위저드 -->
                <div id="bcs-wizard-widget"></div>
            </div>


            <!-- 영양(칼로리·식단)·비용(병원비) 그룹은 우측 레일 과밀 해소를 위해
                 왼쪽 컬럼(영양 관리·건강수첩 옆)으로 이동 — 2026-07-21 -->
        </div>

        <!-- 📊 월간 종합 리포트 -->
        <div class="card-modern p-4">
            <div class="flex justify-between items-center mb-4">
                <div class="flex items-center gap-2">
                    <span class="text-3xl">📊</span>
                    <div>
                        <h3 class="text-base font-semibold text-gray-900">월간 리포트</h3>
                        <p class="text-[10px] text-gray-500">이번 달 요약</p>
                    </div>
                </div>
                <button onclick="generateHealthReportPDF()"
                    class="btn-modern bg-brand-50 hover:bg-brand-100 text-brand-700 border border-brand-100 px-3 py-2 text-xs">
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
                    <i class="fa-solid fa-calendar-days text-brand-500 mr-2"></i>돌봄 스케줄러
                </h3>
                <button onclick="openCareScheduleModal()"
                    class="text-brand-600 hover:text-brand-700 font-black text-sm">
                    <i class="fa-solid fa-plus mr-1.5"></i>일정 추가
                </button>
            </div>

            <!-- 오늘의 일정 — id는 접미사 없이: care-scheduler.js·walk.js 렌더러가 이 이름을 찾는다.
                 (마이펫→건강 탭 이전(99757d36) 때 -health 접미사가 붙으며 배선이 끊겨
                  달력·일정이 영구 빈 채로 방치됐던 버그, 2026-07-19 수리) -->
            <div class="bg-gradient-to-br from-sky-50 to-blue-50/60 border border-sky-100 rounded-xl p-4 space-y-2">
                <div class="flex items-center justify-between">
                    <span class="text-sm font-black text-gray-700">📅 오늘의 일정</span>
                    <span id="care-completion-badge" class="text-xs font-black bg-emerald-100 text-emerald-700 px-2.5 py-1 rounded-full"></span>
                </div>
                <div id="care-scheduler-container" class="space-y-2"></div>
            </div>

            <!-- 이달의 투약 순응도 카드 (care-scheduler.js renderMedicationAdherenceCard) -->
            <div id="medication-adherence-card"></div>

            <!-- 주간 투약 순응도 리포트 카드 (care-scheduler.js renderWeeklyCareReport) -->
            <div id="weekly-care-report-card"></div>

            <!-- 달력 헤더 -->
            <div class="flex justify-between items-center">
                <button onclick="changeMonth(-1)" class="text-gray-400 hover:text-gray-600 transition-colors">
                    <i class="fa-solid fa-chevron-left"></i>
                </button>
                <span id="calendar-month-year" class="font-black text-sm text-gray-700"></span>
                <button onclick="changeMonth(1)" class="text-gray-400 hover:text-gray-600 transition-colors">
                    <i class="fa-solid fa-chevron-right"></i>
                </button>
            </div>

            <!-- 달력 그리드 -->
            <div class="grid grid-cols-7 gap-1.5 text-center text-xs text-gray-400 font-bold uppercase tracking-wider border-b pb-2">
                <span>일</span><span>월</span><span>화</span><span>수</span><span>목</span><span>금</span><span>토</span>
            </div>
            <div id="calendar-days" class="grid grid-cols-7 gap-1.5 text-center text-sm">
                <!-- 날짜들 동적 생성 -->
            </div>

            <!-- 다가오는 주요 돌봄 -->
            <div class="space-y-3 pt-4 border-t border-gray-100">
                <span class="block text-xs text-gray-400 font-bold uppercase tracking-wider">다가오는 핵심 돌봄 3</span>
                <div id="upcoming-schedules" class="space-y-2.5">
                    <!-- JS 동적 생성 -->
                </div>
            </div>
        </div>


    </div>
    <!-- /오른쪽 컬럼 끝 -->

    </div>
    <!-- /2컬럼 레이아웃 끝 -->

</div>
`;
