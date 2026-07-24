const SAJU_TEMPLATE = `
<div class="max-w-4xl mx-auto space-y-4 animate-fade-in text-gray-800">

    <!-- 🐾 검사할 펫 선택기 -->
    <div id="saju-pet-picker" class="bg-white rounded-2xl px-4 py-3 border border-amber-100 shadow-sm">
        <p class="text-[10px] font-black text-gray-500 mb-2">검사할 반려동물 선택</p>
        <div id="saju-pet-list" class="flex gap-2 flex-wrap"></div>
    </div>

    <!-- 사주/테스트 서브 탭 내비게이션 — 모바일은 4열 그리드로 7개 전부 노출(스크롤에 가려 3개가
         숨던 문제 해결, 2026-07-24), 데스크톱(md+)은 기존처럼 한 줄 -->
    <div class="grid grid-cols-4 gap-1.5 md:flex md:space-x-1.5 md:gap-0 md:overflow-x-auto md:no-scrollbar pb-1 shrink-0">
        <button onclick="switchSajuSubTab('harmony')" id="saju-tab-harmony"
            class="saju-subtab-btn whitespace-nowrap bg-rose-500 text-white font-black text-xs py-2 px-3.5 rounded-xl shadow-sm transition-all flex items-center gap-1">
            <span>💞</span> 영혼 조화도
        </button>
        <button onclick="switchSajuSubTab('saju')" id="saju-tab-saju"
            class="saju-subtab-btn whitespace-nowrap bg-white text-gray-500 font-bold text-xs py-2 px-3.5 rounded-xl border border-gray-200 transition-all hover:bg-gray-50 flex items-center gap-1">
            <span>🔮</span> 평생 사주
        </button>
        <button onclick="switchSajuSubTab('fortune')" id="saju-tab-fortune" class="saju-subtab-btn whitespace-nowrap bg-white text-gray-500 font-bold text-xs py-2 px-3.5 rounded-xl border border-gray-200 transition-all hover:bg-gray-50 flex items-center gap-1"><span>🍀</span> 오늘의 운세</button>
        <button onclick="switchSajuSubTab('mbti')" id="saju-tab-mbti" class="saju-subtab-btn whitespace-nowrap bg-white text-gray-500 font-bold text-xs py-2 px-3.5 rounded-xl border border-gray-200 transition-all hover:bg-gray-50 flex items-center gap-1"><span>🐾</span> MBTI 검사</button>
        <button onclick="switchSajuSubTab('petIq')" id="saju-tab-petIq" class="saju-subtab-btn whitespace-nowrap bg-white text-gray-500 font-bold text-xs py-2 px-3.5 rounded-xl border border-gray-200 transition-all hover:bg-gray-50 flex items-center gap-1"><span>🧠</span> 펫 지능</button>
        <button onclick="switchSajuSubTab('ownerIq')" id="saju-tab-ownerIq" class="saju-subtab-btn whitespace-nowrap bg-white text-gray-500 font-bold text-xs py-2 px-3.5 rounded-xl border border-gray-200 transition-all hover:bg-gray-50 flex items-center gap-1"><span>👀</span> 집사 눈치</button>
        <button onclick="switchSajuSubTab('arcade')" id="saju-tab-arcade"
            class="saju-subtab-btn whitespace-nowrap bg-white hover:bg-brand-50 text-gray-500 font-bold text-xs py-2 px-3.5 rounded-xl border border-gray-200 transition-all flex items-center gap-1">
            <span>🎮</span> 아케이드
        </button>
    </div>

    <!-- 1. 평생 사주 섹션 -->
    <div id="saju-main-section" class="space-y-4 block">
        <!-- 헤더 -->
        <div class="bg-gradient-to-r from-brand-500 to-amber-500 rounded-3xl p-4 text-white flex justify-between items-center shadow-md">
            <div class="flex flex-col">
                <span class="text-[10px] font-black uppercase tracking-widest opacity-80">Saju Analysis</span>
                <span class="text-lg font-black mt-0.5">반려동물 & 집사 평생 사주 🔮</span>
            </div>
            <span class="text-2xl">☯️</span>
        </div>

        <!-- 입력 폼 및 명리 가이드 그리드 (비어 보이지 않게 조밀하게 결합) -->
        <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
            
            <!-- 왼쪽/중앙 컬럼: 입력 폼 -->
            <div class="md:col-span-2 bg-white rounded-3xl p-4 sm:p-5 border border-amber-100 shadow-sm space-y-4">
                <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    <!-- 반려동물 사주 폼 -->
                    <div class="bg-brand-50/20 p-4 rounded-2xl border border-brand-100/40 space-y-3">
                        <h3 class="font-black text-brand-700 text-xs flex items-center gap-1.5">
                            <span>🐾</span> 반려동물 사주 정보
                        </h3>
                        <div class="space-y-2.5 text-[11px]">
                            <div>
                                <label class="block font-bold text-gray-500 mb-0.5">펫 이름</label>
                                <input type="text" id="saju-pet-name" placeholder="대표 펫 이름" class="w-full border border-gray-200 rounded-xl p-2 outline-none focus:border-brand-500 bg-white font-bold text-xs">
                            </div>
                            <div class="grid grid-cols-2 gap-2">
                                <div>
                                    <label class="block font-bold text-gray-500 mb-0.5">생년월일</label>
                                    <input type="date" id="saju-pet-birth" class="w-full border border-gray-200 rounded-xl p-2 outline-none focus:border-brand-500 bg-white text-xs">
                                </div>
                                <div>
                                    <label class="block font-bold text-gray-500 mb-0.5">태어난 시간</label>
                                    <select id="saju-pet-time" class="w-full border border-gray-200 rounded-xl p-2 outline-none focus:border-brand-500 bg-white text-xs">
                                        <option value="모름">모름/미기재</option>
                                        <option value="자시">子 (자시: 23:30~01:29)</option>
                                        <option value="축시">丑 (축시: 01:30~03:29)</option>
                                        <option value="인시">寅 (인시: 03:30~05:29)</option>
                                        <option value="묘시">卯 (묘시: 05:30~07:29)</option>
                                        <option value="진시">辰 (진시: 07:30~09:29)</option>
                                        <option value="사시">巳 (사시: 09:30~11:29)</option>
                                        <option value="오시">午 (오시: 11:30~13:29)</option>
                                        <option value="미시">未 (미시: 13:30~15:29)</option>
                                        <option value="신시">申 (신시: 15:30~17:29)</option>
                                        <option value="유시">酉 (유시: 17:30~19:29)</option>
                                        <option value="술시">戌 (술시: 19:30~21:29)</option>
                                        <option value="해시">亥 (해시: 21:30~23:29)</option>
                                    </select>
                                </div>
                            </div>
                        </div>
                    </div>

                    <!-- 집사 사주 폼 -->
                    <div class="bg-brand-50/20 p-4 rounded-2xl border border-brand-100/40 space-y-3">
                        <h3 class="font-black text-brand-700 text-xs flex items-center gap-1.5">
                            <span>🧔</span> 반려인(집사) 사주 정보
                        </h3>
                        <div class="space-y-2.5 text-[11px]">
                            <div>
                                <label class="block font-bold text-gray-500 mb-0.5">집사 이름</label>
                                <input type="text" id="saju-owner-name" placeholder="집사 닉네임" class="w-full border border-gray-200 rounded-xl p-2 outline-none focus:border-brand-500 bg-white font-bold text-xs">
                            </div>
                            <div class="grid grid-cols-2 gap-2">
                                <div>
                                    <label class="block font-bold text-gray-500 mb-0.5">생년월일</label>
                                    <input type="date" id="saju-owner-birth" class="w-full border border-gray-200 rounded-xl p-2 outline-none focus:border-brand-500 bg-white text-xs">
                                </div>
                                <div>
                                    <label class="block font-bold text-gray-500 mb-0.5">태어난 시간</label>
                                    <select id="saju-owner-time" class="w-full border border-gray-200 rounded-xl p-2 outline-none focus:border-brand-500 bg-white text-xs">
                                        <option value="모름">모름/미기재</option>
                                        <option value="자시">子 (자시: 23:30~01:29)</option>
                                        <option value="축시">丑 (축시: 01:30~03:29)</option>
                                        <option value="인시">寅 (인시: 03:30~05:29)</option>
                                        <option value="묘시">卯 (묘시: 05:30~07:29)</option>
                                        <option value="진시">辰 (진시: 07:30~09:29)</option>
                                        <option value="사시">巳 (사시: 09:30~11:29)</option>
                                        <option value="오시">午 (오시: 11:30~13:29)</option>
                                        <option value="미시">未 (미시: 13:30~15:29)</option>
                                        <option value="신시">申 (신시: 15:30~17:29)</option>
                                        <option value="유시">酉 (유시: 17:30~19:29)</option>
                                        <option value="술시">戌 (술시: 19:30~21:29)</option>
                                        <option value="해시">亥 (해시: 21:30~23:29)</option>
                                    </select>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <button onclick="startSajuAnalysis()" class="w-full bg-gradient-to-r from-brand-500 to-amber-500 hover:from-brand-600 hover:to-amber-600 text-white font-black py-3 rounded-2xl shadow-md transition-all text-xs tracking-wide">
                    🔮 평생 사주 분석하기
                </button>
            </div>

            <!-- 우측 컬럼: 동양 오행 상생상극 미니 가이드보드 (여백을 알차게 메움) -->
            <div class="bg-gradient-to-br from-amber-50/60 to-orange-50/40 rounded-3xl p-4 border border-amber-100 shadow-sm flex flex-col justify-between space-y-3">
                <div>
                    <h4 class="text-xs font-black text-amber-900 flex items-center gap-1.5 pb-1.5 border-b border-amber-200/50">
                        <span>☯️</span> 오행 상생상극(五行) 요약
                    </h4>
                    <p class="text-[10px] text-amber-800/80 leading-relaxed mt-2">
                        동양 전통 역학에서는 만물이 5가지 기운으로 순환한다고 봅니다. 반려동물의 기질과 나의 기질이 어떤 조화를 이루는지 표를 통해 알아보세요!
                    </p>
                </div>
                <div class="grid grid-cols-5 gap-1 text-center text-[9px] font-bold text-gray-600">
                    <span class="bg-emerald-100 text-emerald-800 p-1.5 rounded-lg">木<br>(나무)</span>
                    <span class="bg-rose-100 text-rose-800 p-1.5 rounded-lg">火<br>(불)</span>
                    <span class="bg-amber-100 text-amber-800 p-1.5 rounded-lg">土<br>(흙)</span>
                    <span class="bg-gray-100 text-gray-800 p-1.5 rounded-lg">金<br>(쇠)</span>
                    <span class="bg-sky-100 text-sky-800 p-1.5 rounded-lg">水<br>(물)</span>
                </div>
                <div class="text-[9px] text-amber-900 bg-white/70 p-2 rounded-xl border border-amber-200/30">
                    <span class="block font-black">🌿 기운의 찰떡 조화 예시</span>
                    <span class="block mt-0.5 leading-snug">수생목(水生木): 물이 나무를 기르듯 조화로운 상생 인연!</span>
                </div>
            </div>

        </div>

        <!-- 로딩 스크린 -->
        <div id="saju-loading-container" class="hidden bg-white rounded-3xl p-10 text-center space-y-3">
            <div class="animate-spin duration-3000 w-12 h-12 border-4 border-dashed border-brand-500 rounded-full mx-auto"></div>
            <h3 class="font-black text-gray-800 text-xs">명리 명운을 분석하는 중... 🔮</h3>
        </div>

        <!-- 결과 컨테이너 -->
        <div id="saju-result-container" class="hidden bg-[#fdfaf5] rounded-3xl p-4 sm:p-5 border border-amber-800/20 shadow-xl space-y-4">
            <h3 class="font-black text-amber-900 text-sm flex justify-between items-center pb-1 border-b border-amber-100">
                <span>📜 명리 성향 서록</span>
                <div class="flex items-center gap-1.5">
                    <button onclick="shareSajuCard()" class="text-[10px] text-amber-700 bg-amber-50 border border-amber-200 px-2.5 py-1 rounded-lg hover:bg-amber-100 transition-all active:scale-95 flex items-center gap-1 font-bold">
                        <i class="fa-solid fa-share-nodes"></i> 공유 카드
                    </button>
                    <button onclick="deleteSajuData()" class="text-[10px] text-rose-600 bg-white border border-rose-200 px-2 py-1 rounded-lg hover:bg-rose-50 transition-all active:scale-95 flex items-center gap-1 font-bold"><i class="fa-solid fa-trash-can"></i> 삭제</button>
                </div>
            </h3>
            
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                <!-- 펫 결과 -->
                <div class="bg-white p-4 rounded-2xl border border-amber-800/10 shadow-sm flex flex-col justify-between">
                    <div>
                        <span class="text-[9px] bg-amber-800/10 text-amber-900 font-black px-2 py-0.5 rounded" id="res-pet-badge">PET SAJU</span>
                        <p class="text-xs font-black text-gray-800 mt-2" id="res-pet-summary">목(木)의 기운을 가진 총명한 리더견</p>
                    </div>
                    <div class="mt-2.5 bg-gray-50/50 p-3 rounded-xl border border-gray-100 text-[11px] text-gray-600 text-left leading-relaxed" id="res-desc-pet">사주 풀이</div>
                </div>
                <!-- 집사 결과 -->
                <div class="bg-white p-4 rounded-2xl border border-amber-800/10 shadow-sm flex flex-col justify-between">
                    <div>
                        <span class="text-[9px] bg-brand-50 text-brand-800 font-black px-2 py-0.5 rounded" id="res-owner-badge">OWNER SAJU</span>
                        <p class="text-xs font-black text-gray-800 mt-2" id="res-owner-summary">화(Fire)의 열정을 가진 따뜻한 등대</p>
                    </div>
                    <div class="mt-2.5 bg-gray-50/50 p-3 rounded-xl border border-gray-100 text-[11px] text-gray-600 text-left leading-relaxed" id="res-desc-owner">사주 풀이</div>
                </div>
            </div>
        </div>
    </div>

    <!-- 2. 오늘의 운세 섹션 (Lucky Metas 추가하여 여백 차단) -->
    <div id="fortune-test-section" class="hidden space-y-4">
        <div id="fortune-calendar"></div>
        <div class="bg-gradient-to-r from-emerald-500 to-teal-500 rounded-3xl p-4 text-white flex justify-between items-center shadow-md">
            <div class="flex flex-col">
                <span class="text-[10px] font-black uppercase tracking-widest opacity-80">Today's Fortune</span>
                <span class="text-lg font-black mt-0.5">오늘의 운세 뽑기 🍀</span>
            </div>
            <span class="text-2xl">🥠</span>
        </div>

        <!-- 3열 대시보드 (좌측: 뽑기/결과, 우측: 행운의 팩터로 꽉 채움) -->
        <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
            <!-- 좌/중앙 컬럼: 포춘 쿠키 게임 & 결과 정보 -->
            <div class="md:col-span-2 space-y-4">
                <div id="fortune-draw-container" class="bg-white rounded-3xl p-8 border border-emerald-100 shadow-sm text-center space-y-4">
                    <i class="fa-solid fa-clover text-5xl text-emerald-400 animate-bounce"></i>
                    <h3 class="font-black text-gray-700 text-xs">포춘 쿠키를 열어 오늘의 운세를 확인하세요!</h3>
                    <button onclick="startFortuneDraw()" class="bg-emerald-500 hover:bg-emerald-600 text-white font-black text-xs py-2.5 px-6 rounded-xl shadow-md transition-colors">
                        포춘 쿠키 열기 🥠
                    </button>
                </div>

                <div id="fortune-result-container" class="hidden bg-gradient-to-br from-emerald-50/50 to-teal-50/40 rounded-3xl p-4 border border-emerald-200 shadow-lg space-y-3">
                    <div class="text-center border-b border-emerald-200/50 pb-2.5">
                        <h3 class="text-base font-black text-emerald-800" id="fortune-today-date">2026년 5월 23일</h3>
                        <p class="text-xs font-black text-emerald-600 mt-1" id="fortune-main-keyword">"뜻밖의 간식을 득템하는 날!"</p>
                        <span id="fortune-streak-badge" class="hidden inline-flex items-center gap-1 mt-2 bg-amber-100 text-amber-700 font-black text-[10px] py-1 px-2.5 rounded-full border border-amber-200">🔥 연속출석 <strong id="fortune-streak-count">1</strong>일</span>
                    </div>
                    
                    <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                        <div class="bg-white p-3.5 rounded-xl shadow-sm border border-emerald-100/50 space-y-1">
                            <span class="font-black text-emerald-800 text-[11px] flex items-center gap-1"><i class="fa-solid fa-paw text-[9px]"></i> 펫의 하루</span>
                            <p class="text-[10px] text-gray-600 leading-relaxed" id="fortune-pet-text">기분 좋은 에너지가 넘칩니다.</p>
                        </div>
                        <div class="bg-white p-3.5 rounded-xl shadow-sm border border-emerald-100/50 space-y-1">
                            <span class="font-black text-brand-700 text-[11px] flex items-center gap-1"><i class="fa-solid fa-user text-[9px]"></i> 집사의 하루</span>
                            <p class="text-[10px] text-gray-600 leading-relaxed" id="fortune-owner-text">예상치 못한 행운이 찾아옵니다.</p>
                        </div>
                    </div>
                </div>
            </div>

            <!-- 우측 컬럼: 행운의 아이템/숫자/색상 메타 카드 (비어 보이지 않게 조밀하게 렌더링) -->
            <div class="bg-white rounded-3xl p-4 border border-gray-100 shadow-sm space-y-3">
                <h4 class="text-xs font-black text-gray-800 pb-1.5 border-b flex items-center gap-1.5">
                    <span>✨</span> 오늘의 럭키 포인트
                </h4>
                <div class="grid grid-cols-2 gap-2 text-center text-[10px]">
                    <div class="bg-amber-50/50 p-2 rounded-xl border border-amber-100/40">
                        <span class="text-gray-400 block font-bold text-[9px]">행운의 숫자</span>
                        <strong class="text-amber-800 font-mono text-base block mt-0.5">7</strong>
                    </div>
                    <div class="bg-rose-50/50 p-2 rounded-xl border border-rose-100/40">
                        <span class="text-gray-400 block font-bold text-[9px]">행운의 컬러</span>
                        <strong class="text-rose-600 font-bold block mt-1">러블리 핑크 🩷</strong>
                    </div>
                    <div class="bg-emerald-50/50 p-2 rounded-xl border border-emerald-100/40">
                        <span class="text-gray-400 block font-bold text-[9px]">럭키 산책로</span>
                        <strong class="text-emerald-700 font-bold block mt-1">소나무 숲길 🌲</strong>
                    </div>
                    <div class="bg-brand-50/50 p-2 rounded-xl border border-brand-100/40">
                        <span class="text-gray-400 block font-bold text-[9px]">행운의 멘토</span>
                        <strong class="text-brand-700 font-bold block mt-1">동네 냥이맘 🐱</strong>
                    </div>
                </div>
                <div class="bg-teal-50 p-2.5 rounded-xl border border-teal-100/50 text-[10px] text-teal-800 leading-snug">
                    <span class="font-black block">💡 럭키 데일리 케어</span>
                    오늘 산책 후 따뜻한 타올로 발바닥을 5분 스파해주면 펫의 피로 해소에 매우 길합니다.
                </div>
            </div>
        </div>
    </div>

    <!-- 3. 지능 테스트 섹션 (테스트 안내 보강 및 레이아웃 컴팩트화) -->
    <div id="iq-test-section" class="hidden space-y-4">
        <div class="bg-gradient-to-r from-sky-500 to-blue-500 rounded-3xl p-4 text-white flex justify-between items-center shadow-md">
            <div class="flex flex-col">
                <span class="text-[10px] font-black uppercase tracking-widest opacity-80">IQ & Observation Test</span>
                <span class="text-lg font-black mt-0.5" id="iq-start-title">반려동물 지능(IQ) 테스트 🧠</span>
            </div>
            <span class="text-2xl">🧠</span>
        </div>

        <div id="iq-test-grid" class="grid grid-cols-1 md:grid-cols-3 gap-4 items-stretch">
            <!-- 메인 테스트 폼 영역 (2열 차지) -->
            <div class="md:col-span-2 bg-white rounded-3xl p-5 border border-sky-100 shadow-sm space-y-4">
                
                <div id="iq-start-screen" class="text-center py-6 space-y-3">
                    <i class="fa-solid fa-brain text-4xl text-sky-400"></i>
                    <p class="text-xs text-gray-500 leading-relaxed">
                        펫의 일상적인 리액션과 관찰 정보를 매칭하여 지능 백분율을 도출합니다.<br>진단 대상을 식별할 수 있도록 아래 이름을 입력해주세요.
                    </p>
                    <div class="flex justify-center gap-2 max-w-xs mx-auto pt-2">
                        <input type="text" id="iq-target-name" placeholder="이름 입력 (예: 초코)" class="border border-sky-200 rounded-xl p-2 text-center text-xs font-bold w-full outline-none focus:border-sky-500">
                        <button onclick="startIqTestStepper()" class="bg-sky-500 hover:bg-sky-600 text-white font-black text-xs py-2 px-5 rounded-xl shadow shrink-0 whitespace-nowrap">시작하기 🚀</button>
                    </div>
                </div>

                <div id="iq-stepper-screen" class="hidden max-w-xl mx-auto space-y-4">
                    <div class="flex justify-between items-center text-[10px] font-bold text-sky-600">
                        <span id="iq-progress-text">질문 1 / 10</span>
                        <div class="w-32 bg-gray-200 h-1.5 rounded-full overflow-hidden">
                            <div id="iq-progress-bar" class="bg-sky-500 h-full transition-all" style="width: 10%"></div>
                        </div>
                    </div>
                    <div class="bg-sky-50/50 border border-sky-100 p-4 rounded-xl min-h-[90px] flex items-center justify-center">
                        <h4 id="iq-question-title" class="font-black text-gray-800 text-xs text-center leading-relaxed">질문</h4>
                    </div>
                    <div id="iq-options-container" class="grid grid-cols-1 gap-2 pt-1"></div>
                </div>
            </div>

            <!-- 우측 가이드 팁 박스 (여백 제거용) -->
            <div class="bg-gradient-to-br from-sky-50/50 to-blue-50/30 rounded-3xl p-4 border border-sky-100 shadow-sm space-y-3">
                <h4 class="text-xs font-black text-sky-950 flex items-center gap-1.5 pb-1 border-b border-sky-200/50">
                    <span>💡</span> 테스트 진행 꿀팁
                </h4>
                <ul class="text-[10px] text-sky-900/80 space-y-2 list-disc pl-3.5 leading-relaxed">
                    <li>강아지/고양이의 평소 행동을 가만히 상상하며 가장 일치하는 답변을 솔직하게 클릭해 주세요.</li>
                    <li>진단은 약 2분 내외로 총 10문항으로 진행됩니다.</li>
                    <li>집사 눈치 테스트는 반려동물과의 평소 대화 및 제스처에 대한 보호자의 교감능력 지수를 테스트합니다.</li>
                </ul>
            </div>
        </div>

        <div id="iq-result-container" class="hidden bg-[#f0f9ff]/80 rounded-3xl p-5 border border-sky-200/50 shadow-inner space-y-3 text-center">
            <span class="text-[9px] font-extrabold text-sky-600 bg-sky-100 py-1 px-2.5 rounded-full" id="iq-res-badge">PET IQ</span>
            <h3 id="iq-res-score" class="text-3xl font-extrabold text-sky-600 font-mono mt-1">IQ 135</h3>
            <p id="iq-res-title" class="text-xs font-black text-gray-800">"우주 대천재급 지능"</p>
            <div class="bg-white p-3 rounded-xl text-left mt-2 text-[11px] text-gray-600 leading-relaxed shadow-sm border" id="iq-res-desc">설명</div>
            <button onclick="saveIqResult()" class="mt-2 bg-white border border-sky-200 text-sky-600 hover:bg-sky-50 font-bold py-2 px-4 rounded-xl text-[10px] transition-colors"><i class="fa-solid fa-check"></i> 확인 완료</button>
        </div>
    </div>

    <!-- 4. MBTI 섹션 (구조 보완) -->
    <div id="mbti-test-section" class="hidden space-y-4">
        <div class="bg-gradient-to-r from-pink-500 to-rose-500 rounded-3xl p-4 text-white flex justify-between items-center shadow-md">
            <div class="flex flex-col">
                <span class="text-[10px] font-black uppercase tracking-widest opacity-80">Personality MBTI Test</span>
                <span class="text-lg font-black mt-0.5" id="mbti-start-title">댕냥이 성향 MBTI 진단 🐾</span>
            </div>
            <span class="text-2xl">🐾</span>
        </div>

        <!-- 펫 & 집사 MBTI 전환 토글 버튼 (한 페이지에서 전환 가능하도록 추가) -->
        <div class="flex justify-center space-x-2 bg-gray-100 p-1 rounded-xl max-w-sm mx-auto">
            <button onclick="switchMbtiMode('pet')" id="mbti-mode-pet" class="flex-1 bg-white text-pink-600 font-bold text-xs py-2 rounded-lg shadow-sm transition-all">🐾 펫 MBTI</button>
            <button onclick="switchMbtiMode('owner')" id="mbti-mode-owner" class="flex-1 text-gray-500 font-bold text-xs py-2 rounded-lg transition-all">🧔 집사 MBTI</button>
        </div>

        <div id="mbti-test-grid" class="grid grid-cols-1 md:grid-cols-3 gap-4 items-stretch">
            <div class="md:col-span-2 bg-white rounded-3xl p-5 border border-pink-100 shadow-sm space-y-4">
                
                <div id="mbti-start-screen" class="text-center py-6 space-y-3">
                    <i class="fa-solid fa-paw text-4xl text-pink-400"></i>
                    <p class="text-xs text-gray-500 leading-relaxed">
                        펫의 일상 반응 습관을 분석하여 성향 유형 16가지를 정확하게 분류합니다.<br>진단 대상을 식별할 수 있도록 아래 이름을 입력해주세요.
                    </p>
                    <div class="flex justify-center gap-2 max-w-xs mx-auto pt-2">
                        <input type="text" id="mbti-target-name" placeholder="이름 입력 (예: 초코)" class="border border-pink-200 rounded-xl p-2 text-center text-xs font-bold w-full outline-none focus:border-pink-500">
                        <button onclick="startMbtiTestStepper()" class="bg-pink-500 hover:bg-pink-600 text-white font-black text-xs py-2 px-5 rounded-xl shadow shrink-0 whitespace-nowrap">시작하기 🚀</button>
                    </div>
                </div>

                <div id="mbti-stepper-screen" class="hidden max-w-xl mx-auto space-y-4">
                    <div class="flex justify-between items-center text-[10px] font-bold text-pink-600">
                        <span id="mbti-progress-text">질문 1 / 10</span>
                        <div class="w-32 bg-gray-200 h-1.5 rounded-full overflow-hidden">
                            <div id="mbti-progress-bar" class="bg-pink-500 h-full transition-all" style="width: 10%"></div>
                        </div>
                    </div>
                    <div class="bg-pink-50/50 border border-pink-100 p-4 rounded-xl min-h-[90px] flex items-center justify-center">
                        <h4 id="mbti-question-title" class="font-black text-gray-800 text-xs text-center leading-relaxed">질문</h4>
                    </div>
                    <div id="mbti-options-container" class="grid grid-cols-1 gap-2 pt-1"></div>
                </div>
            </div>

            <!-- 우측 가이드 팁 박스 (여백 제거용) -->
            <div class="bg-gradient-to-br from-pink-50/50 to-rose-50/30 rounded-3xl p-4 border border-pink-100 shadow-sm space-y-3">
                <h4 class="text-xs font-black text-pink-950 flex items-center gap-1.5 pb-1 border-b border-pink-200/50">
                    <span>💡</span> 성향 분석 안내
                </h4>
                <ul class="text-[10px] text-pink-900/80 space-y-2 list-disc pl-3.5 leading-relaxed">
                    <li>반려동물의 대인/대견 반응, 소리에 대한 예민도, 휴식 패턴 등을 복합 판단하는 최신 P-MBTI 방식입니다.</li>
                    <li>유형 진단을 모두 마치면 성향 매칭을 통해 반려 룸에 대표 뱃지(예: ENFP)가 이쁘게 장착됩니다.</li>
                </ul>
            </div>
        </div>

        <div id="mbti-result-container" class="hidden bg-[#fff1f2] rounded-3xl p-5 border border-pink-200/50 shadow-inner space-y-3 text-center">
            <span class="text-[9px] font-extrabold text-pink-600 bg-pink-100 py-1 px-2.5 rounded-full" id="mbti-res-badge">PET P-MBTI</span>
            <h3 id="mbti-res-score" class="text-3xl font-extrabold text-pink-600 font-mono mt-1">ENFP</h3>
            <p id="mbti-res-title" class="text-xs font-black text-gray-800">"천진난만 힐링 요정"</p>
            <div class="bg-white p-3 rounded-xl text-left mt-2 text-[11px] text-gray-600 leading-relaxed shadow-sm border" id="mbti-res-desc">설명</div>
            <div class="flex justify-center gap-2 mt-3">
                <button onclick="resetMbtiTest()" class="bg-white border border-pink-200 text-pink-600 hover:bg-pink-50 font-bold py-2 px-4 rounded-xl text-[10px] transition-all active:scale-95 flex items-center gap-1">
                    <i class="fa-solid fa-rotate-left"></i> 다시 검사하기
                </button>
                <button onclick="saveMbtiResult()" class="bg-pink-500 hover:bg-pink-600 text-white font-bold py-2 px-4 rounded-xl text-[10px] transition-all active:scale-95 flex items-center gap-1 shadow-sm">
                    <i class="fa-solid fa-check"></i> 확인 완료
                </button>
            </div>
        </div>
    </div>

    <!-- 5. 영혼 조화도 (Harmony) 섹션 (고밀도 리포트로 꽉 채움) -->
    <div id="harmony-test-section" class="hidden space-y-4">
        <div class="bg-gradient-to-r from-rose-500 to-pink-500 rounded-3xl p-4 text-white flex justify-between items-center shadow-md">
            <div class="flex flex-col">
                <span class="text-[10px] font-black uppercase tracking-widest opacity-80">Ultimate Harmony</span>
                <span class="text-lg font-black mt-0.5">궁극의 영혼 조화도 💞</span>
            </div>
            <span class="text-2xl">💖</span>
        </div>

        <div id="harmony-check-container" class="bg-white rounded-3xl p-6 border border-rose-100 shadow-sm text-center space-y-4">
            <p class="text-xs text-gray-500 max-w-md mx-auto leading-relaxed">
                사주, 지능 테스트, MBTI 성향 진단을 모두 연동하여 최종 영혼 조화 리포트를 완성합니다. 검사를 먼저 수행해 주시면 고도의 알고리즘 결과가 렌더링됩니다.
            </p>
            <button onclick="generateHarmonyReport()" class="bg-rose-500 hover:bg-rose-600 text-white font-black text-xs py-3 px-8 rounded-xl shadow-md transition-all">
                💞 나와 펫의 종합 조화도 분석하기
            </button>
            <p class="text-[9px] text-gray-400">지능 검사와 MBTI 검사를 먼저 완료하시면 더욱 정확한 종합 결과가 도출됩니다!</p>
        </div>

        <div id="harmony-result-container" class="hidden bg-gradient-to-br from-rose-50/50 to-pink-50/40 rounded-3xl p-4 sm:p-5 border border-rose-200 shadow-xl space-y-4">
            
            <!-- 2열 그리드로 낭비 없이 꽉 찬 리포트 화면 설계 -->
            <div class="grid grid-cols-1 md:grid-cols-5 gap-4">
                
                <!-- 왼쪽 2열: 종합 등급 원형 대시보드 및 지표 -->
                <div class="md:col-span-2 bg-white p-4 rounded-2xl border border-rose-800/10 flex flex-col items-center justify-center text-center shadow-sm">
                    <div class="relative w-24 h-24 flex items-center justify-center">
                        <div class="absolute inset-0 rounded-full border-4 border-rose-100"></div>
                        <div class="absolute inset-0 rounded-full border-4 border-rose-500 border-t-transparent animate-spin duration-3000 opacity-80"></div>
                        <div class="text-center">
                            <span class="text-[8px] font-black text-rose-500 block">종합 조화 등급</span>
                            <span class="block text-2xl font-black text-rose-600 font-mono mt-0.5" id="harmony-res-level">5단계</span>
                        </div>
                    </div>
                    <h4 class="font-black text-rose-900 text-xs mt-3" id="harmony-res-title">"영혼의 단짝, 완벽한 듀오!"</h4>
                    
                    <div class="w-full space-y-2 mt-4 text-[10px] text-left">
                        <div class="bg-gray-50 p-2 rounded-xl flex justify-between items-center border">
                            <span class="text-gray-500 font-bold">☯️ 명리 기운 조화 (사주)</span>
                            <span id="harmony-score-saju" class="font-black text-brand-600">90%</span>
                        </div>
                        <div class="bg-gray-50 p-2 rounded-xl flex justify-between items-center border">
                            <span class="text-gray-500 font-bold">🐾 라이프스타일 조화 (MBTI)</span>
                            <span id="harmony-score-mbti" class="font-black text-pink-600">85%</span>
                        </div>
                        <div class="bg-gray-50 p-2 rounded-xl flex justify-between items-center border">
                            <span class="text-gray-500 font-bold">🧠 인지 교감 조화 (지능/눈치)</span>
                            <span id="harmony-score-iq" class="font-black text-sky-600">95%</span>
                        </div>
                    </div>
                </div>

                <!-- 오른쪽 3열: 종합 솔루션 및 처방서 -->
                <div class="md:col-span-3 bg-white/80 p-4 rounded-2xl border border-rose-200/50 flex flex-col justify-between shadow-sm">
                    <div class="space-y-2 text-xs">
                        <span class="font-black text-rose-800 flex items-center gap-1.5 border-b pb-1.5"><i class="fa-solid fa-hand-holding-heart"></i> 종합 처방 및 가이드라인</span>
                        <p class="text-gray-600 text-[11px] leading-relaxed keep-all whitespace-pre-line" id="harmony-res-solution">솔루션 내용...</p>
                    </div>
                    <div class="mt-4 pt-3 border-t space-y-2">
                        <div class="flex gap-2">
                            <button onclick="resetHarmonyTest()" class="flex-1 bg-gray-50 hover:bg-gray-100 text-gray-600 font-bold py-2 rounded-xl text-[10px] border transition-colors text-center shadow-sm">다시 측정하기</button>
                            <button onclick="saveHarmonyToWidget()" class="flex-1 bg-rose-500 hover:bg-rose-600 text-white font-black py-2 rounded-xl text-[10px] transition-colors shadow-sm text-center">마이룸 등록</button>
                        </div>
                        <button onclick="shareHarmonyToSocial()" class="w-full bg-brand-500 hover:bg-brand-600 text-white font-black py-2 rounded-xl text-[10px] transition-colors shadow-sm text-center">
                            <i class="fa-solid fa-share-nodes mr-1"></i>소셜 피드에 자랑하기
                        </button>
                    </div>
                </div>
            </div>
        </div>
        <div id="harmony-plus"></div>
    </div>

    <!-- 6. 아케이드 섹션 -->
    <div id="arcade-test-section" class="hidden space-y-4">
        <div class="bg-gradient-to-r from-brand-500 to-brand-500 rounded-3xl p-4 text-white flex justify-between items-center shadow-md">
            <div class="flex flex-col">
                <span class="text-[10px] font-black uppercase tracking-widest opacity-80">Arcade Minigame</span>
                <span class="text-lg font-black mt-0.5">간식 터치 미니게임 🎮</span>
            </div>
            <span class="text-2xl">🎮</span>
        </div>

        <div class="bg-white rounded-3xl p-4 border border-gray-100 shadow-sm max-w-xl mx-auto space-y-4">
            <div class="flex justify-between items-center bg-brand-50 p-3 rounded-2xl border border-brand-100/50">
                <div class="text-[11px] text-brand-900 font-black">Score: <span id="arcade-score-display" class="font-mono text-base text-brand-600">0</span></div>
                <div class="text-[11px] text-brand-900 font-black">Lives: <span id="arcade-lives-display" class="text-sm tracking-widest text-rose-500 drop-shadow-sm">❤️❤️❤️</span></div>
            </div>

            <!-- 플레이 에어리어 -->
            <div id="arcade-play-area" class="relative w-full aspect-[4/3] bg-gradient-to-b from-brand-950 to-brand-900 rounded-2xl overflow-hidden border border-brand-950 shadow-inner">
                <!-- 게임 시작 오버레이 -->
                <div id="arcade-start-overlay" class="absolute inset-0 bg-black/75 z-20 flex flex-col items-center justify-center text-center p-6 space-y-3">
                    <span class="text-4xl animate-bounce">🍖</span>
                    <h3 class="text-white font-black text-sm">떨어지는 간식을 터치하세요!</h3>
                    <p class="text-[10px] text-gray-300">시간 제한이 없는 무한 모드! 점점 빨라집니다.<br>간식을 3번 놓치면 게임이 종료됩니다.</p>
                    <p class="text-amber-300 font-mono text-xs font-bold mt-1">최고 기록: <span id="arcade-start-best">0</span>점</p>
                    <button onclick="startArcadeGame()" class="bg-brand-500 hover:bg-brand-600 text-white font-black text-xs py-2.5 px-6 rounded-xl shadow-md transition-colors">게임 시작하기 🚀</button>
                </div>

                <!-- 게임오버 오버레이 -->
                <div id="arcade-gameover-overlay" class="hidden absolute inset-0 bg-black/85 z-20 flex flex-col items-center justify-center text-center p-6 space-y-3">
                    <span class="text-4xl">🏆</span>
                    <h3 class="text-white font-black text-sm">게임 종료!</h3>
                    <p class="text-brand-300 font-mono text-base font-extrabold">최종 점수: <span id="arcade-final-score">0</span>점</p>
                    <p class="text-amber-300 font-mono text-xs font-bold">최고 기록: <span id="arcade-best-score">0</span>점</p>
                    <button onclick="startArcadeGame()" class="bg-brand-500 hover:bg-brand-600 text-white font-black text-xs py-2 px-6 rounded-xl shadow-md transition-all">다시 도전하기 🔁</button>
                </div>
            </div>
        </div>
    </div>

</div>
`;
