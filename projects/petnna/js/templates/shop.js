const SHOP_TEMPLATE = `
<div class="space-y-6 animate-fade-in">

    <!-- 펫과나 스토어 준비 중 배너 -->
    <div class="bg-gradient-to-r from-amber-50 to-orange-50 rounded-3xl p-5 border border-amber-100 shadow-sm text-center space-y-3">
        <div class="text-4xl">🛒</div>
        <div>
            <p class="text-sm font-black text-gray-800">펫과나 스토어 오픈 준비 중</p>
            <p class="text-xs text-gray-400 mt-1">반려동물 맞춤 용품·간식·장난감을 곧 만나보세요!</p>
        </div>
        <div class="inline-flex items-center gap-1.5 bg-brand-500 text-white text-xs font-black px-4 py-2 rounded-xl">
            <i class="fa-solid fa-bell"></i> 오픈 알림 신청
        </div>
    </div>

    <!-- 힐링스페이스 연결 -->
    <div class="bg-white rounded-3xl p-5 border-2 border-emerald-300 shadow-md">
        <h3 class="font-black text-gray-800 text-sm flex items-center gap-2 mb-3">
            <i class="fa-solid fa-spa text-emerald-500"></i> 힐링스페이스 연결 🌿
            <span class="ml-auto inline-flex items-center gap-1 bg-emerald-500 text-white text-[10px] font-black px-2.5 py-1 rounded-full shadow-sm">🌟 지금 바로 이용 가능</span>
        </h3>
        <div class="space-y-2">

            <!-- 펫 스파 & 미용 -->
            <div class="rounded-xl border border-gray-100 overflow-hidden">
                <button onclick="toggleShopSection('healing-spa')"
                    class="w-full flex items-center justify-between px-4 py-3 bg-gray-50 hover:bg-emerald-50 transition-colors text-left">
                    <div class="flex items-center gap-3">
                        <span class="text-lg">🛁</span>
                        <div>
                            <span class="block text-xs font-black text-gray-800">펫 스파 & 미용</span>
                            <span class="text-[10px] text-gray-400">전문 그루머 방문 / 센터 예약</span>
                        </div>
                    </div>
                    <i id="icon-healing-spa" class="fa-solid fa-chevron-down text-gray-300 text-xs transition-transform"></i>
                </button>
                <div id="section-healing-spa" class="hidden px-4 py-3 space-y-2 bg-white border-t border-gray-100">
                    <a href="https://banjjakpet.com/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                        class="flex items-center justify-between p-3 rounded-xl bg-emerald-50 hover:bg-emerald-100 transition-colors">
                        <div>
                            <span class="block text-xs font-black text-emerald-800">반짝</span>
                            <span class="text-[10px] text-gray-400">지역별 그루머 검색·예약 플랫폼</span>
                        </div>
                        <i class="fa-solid fa-arrow-up-right-from-square text-emerald-400 text-xs"></i>
                    </a>
                    <a href="https://www.petvip.co.kr/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                        class="flex items-center justify-between p-3 rounded-xl bg-emerald-50 hover:bg-emerald-100 transition-colors">
                        <div>
                            <span class="block text-xs font-black text-emerald-800">펫VIP</span>
                            <span class="text-[10px] text-gray-400">출장 미용·목욕·스파 통합 서비스</span>
                        </div>
                        <i class="fa-solid fa-arrow-up-right-from-square text-emerald-400 text-xs"></i>
                    </a>
                </div>
            </div>

            <!-- 펫 힐링 캠핑 -->
            <div class="rounded-xl border border-gray-100 overflow-hidden">
                <button onclick="toggleShopSection('healing-camping')"
                    class="w-full flex items-center justify-between px-4 py-3 bg-gray-50 hover:bg-emerald-50 transition-colors text-left">
                    <div class="flex items-center gap-3">
                        <span class="text-lg">🏕️</span>
                        <div>
                            <span class="block text-xs font-black text-gray-800">반려동물 동반 캠핑</span>
                            <span class="text-[10px] text-gray-400">전국 반려동물 동반 캠핑장 연결</span>
                        </div>
                    </div>
                    <i id="icon-healing-camping" class="fa-solid fa-chevron-down text-gray-300 text-xs transition-transform"></i>
                </button>
                <div id="section-healing-camping" class="hidden px-4 py-3 space-y-2 bg-white border-t border-gray-100">
                    <a href="https://www.camfit.co.kr/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                        class="flex items-center justify-between p-3 rounded-xl bg-emerald-50 hover:bg-emerald-100 transition-colors">
                        <div>
                            <span class="block text-xs font-black text-emerald-800">캠핏</span>
                            <span class="text-[10px] text-gray-400">반려동물 동반 필터 전국 캠핑장 검색</span>
                        </div>
                        <i class="fa-solid fa-arrow-up-right-from-square text-emerald-400 text-xs"></i>
                    </a>
                    <a href="https://reservation.knps.or.kr/contents/withPet.do" target="_blank" rel="noopener"
                        class="flex items-center justify-between p-3 rounded-xl bg-emerald-50 hover:bg-emerald-100 transition-colors">
                        <div>
                            <span class="block text-xs font-black text-emerald-800">국립공원 반려견 동반 캠핑</span>
                            <span class="text-[10px] text-gray-400">국립공원 공식 예약 시스템</span>
                        </div>
                        <i class="fa-solid fa-arrow-up-right-from-square text-emerald-400 text-xs"></i>
                    </a>
                </div>
            </div>

            <!-- 펫 마사지 테라피 -->
            <div class="rounded-xl border border-gray-100 overflow-hidden">
                <button onclick="toggleShopSection('healing-therapy')"
                    class="w-full flex items-center justify-between px-4 py-3 bg-gray-50 hover:bg-emerald-50 transition-colors text-left">
                    <div class="flex items-center gap-3">
                        <span class="text-lg">🌸</span>
                        <div>
                            <span class="block text-xs font-black text-gray-800">펫 마사지 & 테라피</span>
                            <span class="text-[10px] text-gray-400">아로마·마사지 전문 케어</span>
                        </div>
                    </div>
                    <i id="icon-healing-therapy" class="fa-solid fa-chevron-down text-gray-300 text-xs transition-transform"></i>
                </button>
                <div id="section-healing-therapy" class="hidden px-4 py-3 space-y-2 bg-white border-t border-gray-100">
                    <a href="https://healschool.co.kr/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                        class="flex items-center justify-between p-3 rounded-xl bg-emerald-50 hover:bg-emerald-100 transition-colors">
                        <div>
                            <span class="block text-xs font-black text-emerald-800">더힐테라피센터</span>
                            <span class="text-[10px] text-gray-400">반려동물 아로마·마사지 교육·상담</span>
                        </div>
                        <i class="fa-solid fa-arrow-up-right-from-square text-emerald-400 text-xs"></i>
                    </a>
                </div>
            </div>

        </div>
    </div>

    <!-- 돌보미 연결 -->
    <div class="bg-white rounded-3xl p-5 border border-sky-100 shadow-sm">
        <h3 class="font-black text-gray-800 text-sm flex items-center gap-2 mb-3">
            <i class="fa-solid fa-hand-holding-heart text-sky-500"></i> 돌보미 연결 🐾
        </h3>
        <div class="space-y-2">

            <!-- 펫시터 방문 돌봄 -->
            <div class="rounded-xl border border-gray-100 overflow-hidden">
                <button onclick="toggleShopSection('care-sitter')"
                    class="w-full flex items-center justify-between px-4 py-3 bg-gray-50 hover:bg-sky-50 transition-colors text-left">
                    <div class="flex items-center gap-3">
                        <span class="text-lg">🏠</span>
                        <div>
                            <span class="block text-xs font-black text-gray-800">펫시터 (방문 돌봄)</span>
                            <span class="text-[10px] text-gray-400">집으로 방문해 돌봐주는 전문 펫시터</span>
                        </div>
                    </div>
                    <i id="icon-care-sitter" class="fa-solid fa-chevron-down text-gray-300 text-xs transition-transform"></i>
                </button>
                <div id="section-care-sitter" class="hidden px-4 py-3 space-y-2 bg-white border-t border-gray-100">
                    <a href="https://wayopet.com/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                        class="flex items-center justify-between p-3 rounded-xl bg-sky-50 hover:bg-sky-100 transition-colors">
                        <div>
                            <span class="block text-xs font-black text-sky-800">와요 (WAYO)</span>
                            <span class="text-[10px] text-gray-400">자격증 펫시터 방문 돌봄·산책 앱 예약</span>
                        </div>
                        <i class="fa-solid fa-arrow-up-right-from-square text-sky-400 text-xs"></i>
                    </a>
                    <a href="https://petplanet.co/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                        class="flex items-center justify-between p-3 rounded-xl bg-sky-50 hover:bg-sky-100 transition-colors">
                        <div>
                            <span class="block text-xs font-black text-sky-800">펫플래닛</span>
                            <span class="text-[10px] text-gray-400">안전교육 이수 펫시터 돌봄·산책</span>
                        </div>
                        <i class="fa-solid fa-arrow-up-right-from-square text-sky-400 text-xs"></i>
                    </a>
                </div>
            </div>

            <!-- 펫호텔 위탁 -->
            <div class="rounded-xl border border-gray-100 overflow-hidden">
                <button onclick="toggleShopSection('care-hotel')"
                    class="w-full flex items-center justify-between px-4 py-3 bg-gray-50 hover:bg-sky-50 transition-colors text-left">
                    <div class="flex items-center gap-3">
                        <span class="text-lg">🌙</span>
                        <div>
                            <span class="block text-xs font-black text-gray-800">펫호텔 (위탁)</span>
                            <span class="text-[10px] text-gray-400">1박~장기 위탁 · 실시간 사진 보고</span>
                        </div>
                    </div>
                    <i id="icon-care-hotel" class="fa-solid fa-chevron-down text-gray-300 text-xs transition-transform"></i>
                </button>
                <div id="section-care-hotel" class="hidden px-4 py-3 space-y-2 bg-white border-t border-gray-100">
                    <a href="https://www.petliz.co.kr/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                        class="flex items-center justify-between p-3 rounded-xl bg-sky-50 hover:bg-sky-100 transition-colors">
                        <div>
                            <span class="block text-xs font-black text-sky-800">Petliz</span>
                            <span class="text-[10px] text-gray-400">청결도 4.8점 이상 펫호텔만 큐레이션</span>
                        </div>
                        <i class="fa-solid fa-arrow-up-right-from-square text-sky-400 text-xs"></i>
                    </a>
                </div>
            </div>

            <!-- 산책 대행 -->
            <div class="rounded-xl border border-gray-100 overflow-hidden">
                <button onclick="toggleShopSection('care-walk')"
                    class="w-full flex items-center justify-between px-4 py-3 bg-gray-50 hover:bg-sky-50 transition-colors text-left">
                    <div class="flex items-center gap-3">
                        <span class="text-lg">🚶</span>
                        <div>
                            <span class="block text-xs font-black text-gray-800">펫 산책 대행</span>
                            <span class="text-[10px] text-gray-400">GPS 실시간 추적 · 30분/1시간 단위</span>
                        </div>
                    </div>
                    <i id="icon-care-walk" class="fa-solid fa-chevron-down text-gray-300 text-xs transition-transform"></i>
                </button>
                <div id="section-care-walk" class="hidden px-4 py-3 space-y-2 bg-white border-t border-gray-100">
                    <a href="https://wayopet.com/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                        class="flex items-center justify-between p-3 rounded-xl bg-sky-50 hover:bg-sky-100 transition-colors">
                        <div>
                            <span class="block text-xs font-black text-sky-800">와요 — 산책 대행</span>
                            <span class="text-[10px] text-gray-400">자격증 펫시터 산책 예약</span>
                        </div>
                        <i class="fa-solid fa-arrow-up-right-from-square text-sky-400 text-xs"></i>
                    </a>
                </div>
            </div>

            <!-- 병원 픽업·동행 -->
            <div class="rounded-xl border border-gray-100 overflow-hidden">
                <button onclick="toggleShopSection('care-hospital')"
                    class="w-full flex items-center justify-between px-4 py-3 bg-gray-50 hover:bg-sky-50 transition-colors text-left">
                    <div class="flex items-center gap-3">
                        <span class="text-lg">🏥</span>
                        <div>
                            <span class="block text-xs font-black text-gray-800">동물병원 픽업·동행</span>
                            <span class="text-[10px] text-gray-400">병원 검색·예약 및 픽업 서비스</span>
                        </div>
                    </div>
                    <i id="icon-care-hospital" class="fa-solid fa-chevron-down text-gray-300 text-xs transition-transform"></i>
                </button>
                <div id="section-care-hospital" class="hidden px-4 py-3 space-y-2 bg-white border-t border-gray-100">
                    <a href="https://www.petmeup.co.kr/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                        class="flex items-center justify-between p-3 rounded-xl bg-sky-50 hover:bg-sky-100 transition-colors">
                        <div>
                            <span class="block text-xs font-black text-sky-800">펫미업</span>
                            <span class="text-[10px] text-gray-400">국내 1위 펫택시 · 병원 동행 케어</span>
                        </div>
                        <i class="fa-solid fa-arrow-up-right-from-square text-sky-400 text-xs"></i>
                    </a>
                    <a href="https://hospital.fitpetmall.com/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                        class="flex items-center justify-between p-3 rounded-xl bg-sky-50 hover:bg-sky-100 transition-colors">
                        <div>
                            <span class="block text-xs font-black text-sky-800">핏펫 병원 검색</span>
                            <span class="text-[10px] text-gray-400">진료과목별 근처 동물병원 검색·예약</span>
                        </div>
                        <i class="fa-solid fa-arrow-up-right-from-square text-sky-400 text-xs"></i>
                    </a>
                </div>
            </div>

        </div>
    </div>

    <!-- 펫 훈련소 -->
    <div class="bg-white rounded-3xl p-5 border border-orange-100 shadow-sm">
        <h3 class="font-black text-gray-800 text-sm flex items-center gap-2 mb-3">
            <i class="fa-solid fa-dog text-orange-500"></i> 펫 훈련소 🎓
        </h3>
        <div class="space-y-2">

            <!-- 방문 훈련사 -->
            <div class="rounded-xl border border-gray-100 overflow-hidden">
                <button onclick="toggleShopSection('train-visit')"
                    class="w-full flex items-center justify-between px-4 py-3 bg-gray-50 hover:bg-orange-50 transition-colors text-left">
                    <div class="flex items-center gap-3">
                        <span class="text-lg">🐕‍🦺</span>
                        <div>
                            <span class="block text-xs font-black text-gray-800">방문 훈련사 매칭</span>
                            <span class="text-[10px] text-gray-400">퍼피 트레이닝 · 문제 행동 교정</span>
                        </div>
                    </div>
                    <i id="icon-train-visit" class="fa-solid fa-chevron-down text-gray-300 text-xs transition-transform"></i>
                </button>
                <div id="section-train-visit" class="hidden px-4 py-3 space-y-2 bg-white border-t border-gray-100">
                    <a href="https://wayopet.com/trainer?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                        class="flex items-center justify-between p-3 rounded-xl bg-orange-50 hover:bg-orange-100 transition-colors">
                        <div>
                            <span class="block text-xs font-black text-orange-800">와요 — 훈련사</span>
                            <span class="text-[10px] text-gray-400">앱으로 전문 훈련사 예약</span>
                        </div>
                        <i class="fa-solid fa-arrow-up-right-from-square text-orange-400 text-xs"></i>
                    </a>
                    <a href="https://soomgo.com/hire/%ED%8E%AB-%ED%9B%88%EB%A0%A8" target="_blank" rel="noopener"
                        class="flex items-center justify-between p-3 rounded-xl bg-orange-50 hover:bg-orange-100 transition-colors">
                        <div>
                            <span class="block text-xs font-black text-orange-800">숨고 — 펫 훈련</span>
                            <span class="text-[10px] text-gray-400">훈련사 비교 선택·견적 요청</span>
                        </div>
                        <i class="fa-solid fa-arrow-up-right-from-square text-orange-400 text-xs"></i>
                    </a>
                    <a href="https://www.petvip.co.kr/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                        class="flex items-center justify-between p-3 rounded-xl bg-orange-50 hover:bg-orange-100 transition-colors">
                        <div>
                            <span class="block text-xs font-black text-orange-800">펫VIP</span>
                            <span class="text-[10px] text-gray-400">방문 훈련·미용 통합 서비스</span>
                        </div>
                        <i class="fa-solid fa-arrow-up-right-from-square text-orange-400 text-xs"></i>
                    </a>
                </div>
            </div>

            <!-- 온라인 교육 -->
            <div class="rounded-xl border border-gray-100 overflow-hidden">
                <button onclick="toggleShopSection('train-online')"
                    class="w-full flex items-center justify-between px-4 py-3 bg-gray-50 hover:bg-orange-50 transition-colors text-left">
                    <div class="flex items-center gap-3">
                        <span class="text-lg">📚</span>
                        <div>
                            <span class="block text-xs font-black text-gray-800">온라인 훈련 교육</span>
                            <span class="text-[10px] text-gray-400">강의·실습 기반 반려동물 교육</span>
                        </div>
                    </div>
                    <i id="icon-train-online" class="fa-solid fa-chevron-down text-gray-300 text-xs transition-transform"></i>
                </button>
                <div id="section-train-online" class="hidden px-4 py-3 space-y-2 bg-white border-t border-gray-100">
                    <a href="https://ebspetedu.co.kr/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                        class="flex items-center justify-between p-3 rounded-xl bg-orange-50 hover:bg-orange-100 transition-colors">
                        <div>
                            <span class="block text-xs font-black text-orange-800">EBS 펫에듀</span>
                            <span class="text-[10px] text-gray-400">온라인 강의+실습 반려동물 교육 플랫폼</span>
                        </div>
                        <i class="fa-solid fa-arrow-up-right-from-square text-orange-400 text-xs"></i>
                    </a>
                </div>
            </div>

        </div>
    </div>

</div>
`;
