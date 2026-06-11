const SHOP_TEMPLATE = `
<div class="space-y-6 animate-fade-in">

    <!-- 🏝️ 펫 라이프 아일랜드 섬 맵 -->
    <div class="relative w-full rounded-3xl overflow-hidden shadow-2xl border-4 border-sky-300" style="height: 600px; background: linear-gradient(to bottom, #e0f2fe 0%, #bae6fd 50%, #0ea5e9 100%);">

        <!-- 파도 애니메이션 -->
        <div class="absolute inset-0 ocean-waves"></div>

        <!-- 하나의 큰 섬 (중앙) -->
        <div class="absolute island-main" style="top: 50%; left: 50%; transform: translate(-50%, -50%);">
            <!-- 섬 배경 -->
            <div class="island-bg">
                <div class="text-9xl">🏝️</div>
            </div>

            <!-- 섬 위의 가맹점 아이콘들 (원형 배치) -->
            <div class="absolute shop-icons">
                <!-- 스파 (12시) -->
                <button onclick="toggleShopSection('healing-spa')" class="shop-icon" style="top: -80px; left: 50%; transform: translateX(-50%);" title="펫 스파">
                    <div class="shop-icon-circle bg-gradient-to-br from-emerald-400 to-teal-500">
                        <span class="text-3xl">🛁</span>
                    </div>
                    <span class="shop-label">스파</span>
                </button>

                <!-- 캠핑 (2시) -->
                <button onclick="toggleShopSection('healing-camping')" class="shop-icon" style="top: -40px; right: -70px;" title="반려동물 캠핑">
                    <div class="shop-icon-circle bg-gradient-to-br from-green-400 to-emerald-500">
                        <span class="text-3xl">🏕️</span>
                    </div>
                    <span class="shop-label">캠핑</span>
                </button>

                <!-- 테라피 (4시) -->
                <button onclick="toggleShopSection('healing-therapy')" class="shop-icon" style="bottom: -40px; right: -70px;" title="펫 마사지">
                    <div class="shop-icon-circle bg-gradient-to-br from-pink-400 to-rose-500">
                        <span class="text-3xl">🌸</span>
                    </div>
                    <span class="shop-label">테라피</span>
                </button>

                <!-- 병원 (6시) -->
                <button onclick="toggleShopSection('care-hospital')" class="shop-icon" style="bottom: -80px; left: 50%; transform: translateX(-50%);" title="24시 동물병원">
                    <div class="shop-icon-circle bg-gradient-to-br from-red-400 to-rose-500">
                        <span class="text-3xl">🏥</span>
                    </div>
                    <span class="shop-label">병원</span>
                </button>

                <!-- 호텔 (8시) -->
                <button onclick="toggleShopSection('care-hotel')" class="shop-icon" style="bottom: -40px; left: -70px;" title="펫 호텔">
                    <div class="shop-icon-circle bg-gradient-to-br from-indigo-400 to-purple-500">
                        <span class="text-3xl">🏨</span>
                    </div>
                    <span class="shop-label">호텔</span>
                </button>

                <!-- 쇼핑 (10시) -->
                <button onclick="toggleShopSection('store')" class="shop-icon" style="top: -40px; left: -70px;" title="펫 용품">
                    <div class="shop-icon-circle bg-gradient-to-br from-amber-400 to-orange-500">
                        <span class="text-3xl">🛒</span>
                    </div>
                    <span class="shop-label">쇼핑</span>
                </button>

                <!-- 중앙 타이틀 -->
                <div class="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 text-center">
                    <div class="text-sm font-bold text-emerald-900 bg-white/90 px-4 py-2 rounded-full shadow-md">
                        펫 라이프<br/>아일랜드
                    </div>
                </div>
            </div>
        </div>

        <!-- 구름 장식 -->
        <div class="absolute top-10 left-20 text-4xl opacity-80 animate-float">☁️</div>
        <div class="absolute top-16 right-32 text-3xl opacity-70 animate-float" style="animation-delay: 1s;">☁️</div>
        <div class="absolute top-32 right-16 text-5xl opacity-60 animate-float" style="animation-delay: 2s;">☁️</div>

        <!-- 새 장식 -->
        <div class="absolute top-24 left-1/3 text-2xl animate-fly">🦜</div>
        <div class="absolute top-40 right-1/4 text-xl animate-fly" style="animation-delay: 1.5s;">🐦</div>

        <!-- 물고기 -->
        <div class="absolute bottom-20 left-1/4 text-2xl animate-swim">🐠</div>
        <div class="absolute bottom-32 right-1/3 text-xl animate-swim" style="animation-delay: 2s;">🐟</div>
    </div>

    <!-- 안내 메시지 -->
    <div class="card-modern p-5 text-center">
        <p class="text-sm text-gray-600">
            <i class="fa-solid fa-hand-pointer text-violet-500 mr-2"></i>
            섬 위의 아이콘을 클릭하여 가맹점을 탐험하세요
        </p>
    </div>

    <!-- 펫과나 스토어 준비 중 -->
    <div id="section-store" class="hidden bg-gradient-to-r from-amber-50 to-orange-50 rounded-3xl p-5 border border-amber-100 shadow-sm text-center space-y-3">
        <div class="text-4xl">🛒</div>
        <div>
            <p class="text-sm font-black text-gray-800">펫과나 스토어 오픈 준비 중</p>
            <p class="text-xs text-gray-400 mt-1">반려동물 맞춤 용품·간식·장난감을 곧 만나보세요!</p>
        </div>
        <div class="inline-flex items-center gap-1.5 bg-brand-500 text-white text-xs font-black px-4 py-2 rounded-xl">
            <i class="fa-solid fa-bell"></i> 오픈 알림 신청
        </div>
    </div>

    <!-- 펫 스파 & 미용 -->
    <div id="section-healing-spa" class="hidden card-modern p-5 space-y-3">
        <div class="flex items-center justify-between border-b pb-3">
            <div class="flex items-center gap-2">
                <span class="text-3xl">🛁</span>
                <h3 class="font-black text-gray-800 text-base">펫 스파 & 미용</h3>
            </div>
            <button onclick="toggleShopSection('healing-spa')" class="text-gray-400 hover:text-gray-600">
                <i class="fa-solid fa-xmark text-xl"></i>
            </button>
        </div>
        <div class="space-y-2">
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

    <!-- 반려동물 동반 캠핑 -->
    <div id="section-healing-camping" class="hidden card-modern p-5 space-y-3">
        <div class="flex items-center justify-between border-b pb-3">
            <div class="flex items-center gap-2">
                <span class="text-3xl">🏕️</span>
                <h3 class="font-black text-gray-800 text-base">반려동물 동반 캠핑</h3>
            </div>
            <button onclick="toggleShopSection('healing-camping')" class="text-gray-400 hover:text-gray-600">
                <i class="fa-solid fa-xmark text-xl"></i>
            </button>
        </div>
        <div class="space-y-2">
            <a href="https://www.camfit.co.kr/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                class="flex items-center justify-between p-3 rounded-xl bg-green-50 hover:bg-green-100 transition-colors">
                <div>
                    <span class="block text-xs font-black text-green-800">캠핏</span>
                    <span class="text-[10px] text-gray-400">반려동물 동반 필터 전국 캠핑장 검색</span>
                </div>
                <i class="fa-solid fa-arrow-up-right-from-square text-green-400 text-xs"></i>
            </a>
            <a href="https://reservation.knps.or.kr/contents/withPet.do" target="_blank" rel="noopener"
                class="flex items-center justify-between p-3 rounded-xl bg-green-50 hover:bg-green-100 transition-colors">
                <div>
                    <span class="block text-xs font-black text-green-800">국립공원 반려견 동반 캠핑</span>
                    <span class="text-[10px] text-gray-400">국립공원 공식 예약 시스템</span>
                </div>
                <i class="fa-solid fa-arrow-up-right-from-square text-green-400 text-xs"></i>
            </a>
        </div>
    </div>

    <!-- 펫 마사지 & 테라피 -->
    <div id="section-healing-therapy" class="hidden card-modern p-5 space-y-3">
        <div class="flex items-center justify-between border-b pb-3">
            <div class="flex items-center gap-2">
                <span class="text-3xl">🌸</span>
                <h3 class="font-black text-gray-800 text-base">펫 마사지 & 테라피</h3>
            </div>
            <button onclick="toggleShopSection('healing-therapy')" class="text-gray-400 hover:text-gray-600">
                <i class="fa-solid fa-xmark text-xl"></i>
            </button>
        </div>
        <div class="space-y-2">
            <a href="https://healschool.co.kr/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                class="flex items-center justify-between p-3 rounded-xl bg-pink-50 hover:bg-pink-100 transition-colors">
                <div>
                    <span class="block text-xs font-black text-pink-800">더힐테라피센터</span>
                    <span class="text-[10px] text-gray-400">반려동물 아로마·마사지 교육·상담</span>
                </div>
                <i class="fa-solid fa-arrow-up-right-from-square text-pink-400 text-xs"></i>
            </a>
        </div>
    </div>

    <!-- 펫호텔 (위탁) -->
    <div id="section-care-hotel" class="hidden card-modern p-5 space-y-3">
        <div class="flex items-center justify-between border-b pb-3">
            <div class="flex items-center gap-2">
                <span class="text-3xl">🏨</span>
                <h3 class="font-black text-gray-800 text-base">펫호텔 (위탁)</h3>
            </div>
            <button onclick="toggleShopSection('care-hotel')" class="text-gray-400 hover:text-gray-600">
                <i class="fa-solid fa-xmark text-xl"></i>
            </button>
        </div>
        <div class="space-y-2">
            <a href="https://www.petliz.co.kr/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                class="flex items-center justify-between p-3 rounded-xl bg-indigo-50 hover:bg-indigo-100 transition-colors">
                <div>
                    <span class="block text-xs font-black text-indigo-800">Petliz</span>
                    <span class="text-[10px] text-gray-400">청결도 4.8점 이상 펫호텔만 큐레이션</span>
                </div>
                <i class="fa-solid fa-arrow-up-right-from-square text-indigo-400 text-xs"></i>
            </a>
        </div>
    </div>

    <!-- 동물병원 픽업·동행 -->
    <div id="section-care-hospital" class="hidden card-modern p-5 space-y-3">
        <div class="flex items-center justify-between border-b pb-3">
            <div class="flex items-center gap-2">
                <span class="text-3xl">🏥</span>
                <h3 class="font-black text-gray-800 text-base">동물병원 픽업·동행</h3>
            </div>
            <button onclick="toggleShopSection('care-hospital')" class="text-gray-400 hover:text-gray-600">
                <i class="fa-solid fa-xmark text-xl"></i>
            </button>
        </div>
        <div class="space-y-2">
            <a href="https://www.petmeup.co.kr/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                class="flex items-center justify-between p-3 rounded-xl bg-red-50 hover:bg-red-100 transition-colors">
                <div>
                    <span class="block text-xs font-black text-red-800">펫미업</span>
                    <span class="text-[10px] text-gray-400">국내 1위 펫택시 · 병원 동행 케어</span>
                </div>
                <i class="fa-solid fa-arrow-up-right-from-square text-red-400 text-xs"></i>
            </a>
            <a href="https://hospital.fitpetmall.com/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                class="flex items-center justify-between p-3 rounded-xl bg-red-50 hover:bg-red-100 transition-colors">
                <div>
                    <span class="block text-xs font-black text-red-800">핏펫 병원 검색</span>
                    <span class="text-[10px] text-gray-400">진료과목별 근처 동물병원 검색·예약</span>
                </div>
                <i class="fa-solid fa-arrow-up-right-from-square text-red-400 text-xs"></i>
            </a>
        </div>
    </div>

</div>

<style>
/* 파도 애니메이션 */
.ocean-waves {
    background-image:
        radial-gradient(circle at 20% 50%, rgba(255,255,255,0.3) 0%, transparent 50%),
        radial-gradient(circle at 60% 70%, rgba(255,255,255,0.2) 0%, transparent 50%),
        radial-gradient(circle at 80% 30%, rgba(255,255,255,0.25) 0%, transparent 50%);
    animation: waves 10s ease-in-out infinite;
}

@keyframes waves {
    0%, 100% { transform: translateY(0); }
    50% { transform: translateY(-10px); }
}

/* 큰 섬 */
.island-main {
    position: relative;
    width: 400px;
    height: 400px;
    filter: drop-shadow(0 8px 24px rgba(0,0,0,0.2));
}

.island-bg {
    position: absolute;
    top: 50%;
    left: 50%;
    transform: translate(-50%, -50%);
    animation: float-island 6s ease-in-out infinite;
}

@keyframes float-island {
    0%, 100% { transform: translate(-50%, -50%) translateY(0); }
    50% { transform: translate(-50%, -50%) translateY(-15px); }
}

.shop-icons {
    position: absolute;
    top: 50%;
    left: 50%;
    width: 100%;
    height: 100%;
}

.shop-icon {
    position: absolute;
    background: none;
    border: none;
    cursor: pointer;
    transition: all 0.3s ease;
    outline: none;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.5rem;
}

.shop-icon:hover {
    transform: scale(1.15) translateY(-4px);
}

.shop-icon:active {
    transform: scale(1.05);
}

.shop-icon-circle {
    width: 80px;
    height: 80px;
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
    box-shadow: 0 4px 12px rgba(0,0,0,0.15);
    border: 4px solid white;
    animation: bounce 2s ease-in-out infinite;
}

.shop-icon:nth-child(1) .shop-icon-circle { animation-delay: 0s; }
.shop-icon:nth-child(2) .shop-icon-circle { animation-delay: 0.3s; }
.shop-icon:nth-child(3) .shop-icon-circle { animation-delay: 0.6s; }
.shop-icon:nth-child(4) .shop-icon-circle { animation-delay: 0.9s; }
.shop-icon:nth-child(5) .shop-icon-circle { animation-delay: 1.2s; }
.shop-icon:nth-child(6) .shop-icon-circle { animation-delay: 1.5s; }

.shop-label {
    font-size: 0.75rem;
    font-weight: 700;
    color: white;
    text-shadow: 0 2px 4px rgba(0,0,0,0.3);
    background: rgba(0,0,0,0.5);
    padding: 0.25rem 0.75rem;
    border-radius: 9999px;
}

/* 구름 떠다니기 */
@keyframes float {
    0%, 100% { transform: translateY(0) translateX(0); }
    50% { transform: translateY(-20px) translateX(10px); }
}

.animate-float {
    animation: float 6s ease-in-out infinite;
}

/* 새 날기 */
@keyframes fly {
    0% { transform: translateX(-100px); opacity: 0; }
    10% { opacity: 1; }
    90% { opacity: 1; }
    100% { transform: translateX(100vw); opacity: 0; }
}

.animate-fly {
    animation: fly 20s linear infinite;
}

/* 물고기 헤엄 */
@keyframes swim {
    0%, 100% { transform: translateX(0) scaleX(1); }
    50% { transform: translateX(30px) scaleX(-1); }
}

.animate-swim {
    animation: swim 4s ease-in-out infinite;
}
</style>
`;
