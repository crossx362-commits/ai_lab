const SETTINGS_TEMPLATE = `
<div class="grid grid-cols-1 lg:grid-cols-3 gap-6 items-start">

    <!-- 왼쪽 패널: 프로필 및 테마 커스터마이즈 -->
    <div class="lg:col-span-2 space-y-6">
        <!-- 운영 서비스 상태 -->
        <div class="bg-white rounded-3xl p-5 border border-amber-50 shadow-sm space-y-4">
            <div class="flex items-center justify-between border-b pb-2">
                <h3 class="font-black text-gray-800 text-sm flex items-center">
                    <i class="fa-solid fa-toggle-on text-brand-500 mr-2"></i>운영 서비스 상태
                </h3>
                <span class="text-[10px] font-black text-gray-400 bg-gray-50 px-2 py-1 rounded-full">env gate</span>
            </div>
            <div id="service-status-list" class="grid grid-cols-1 sm:grid-cols-2 gap-2"></div>
            <p class="text-[10px] text-gray-400 font-medium leading-relaxed px-1">
                AI와 결제는 현재 차단 상태로 운영됩니다. 배포 환경에서 플래그와 키를 켜면 같은 화면에서 즉시 준비 상태로 전환됩니다.
            </p>
        </div>

        <!-- 🔒 로그인 및 보안 계정 관리 (Login & Security options) -->
        <div class="bg-white rounded-3xl p-5 border border-amber-50 shadow-sm space-y-4">
            <h3 class="font-black text-gray-800 text-sm flex items-center border-b pb-2">
                <i class="fa-solid fa-shield-halved text-brand-500 mr-2"></i>로그인 및 보안 계정 관리 🔒
            </h3>
            <div class="space-y-3.5 text-xs">
                <div class="flex justify-between items-center bg-gray-50/50 p-3 rounded-xl border border-gray-100/50">
                    <div>
                        <span class="block text-[9px] text-gray-400 font-bold">현재 연결 계정</span>
                        <span id="settings-connected-email" class="font-bold text-gray-700">butler@petna.co.kr</span>
                    </div>
                    <span class="bg-brand-100 text-brand-700 font-black text-[9px] px-2.5 py-0.5 rounded-full">인증 완료</span>
                </div>
                <div class="space-y-1.5">
                    <span class="block font-bold text-gray-400 text-[10px] uppercase">간편 로그인 SNS 연동</span>
                    <div class="grid grid-cols-2 gap-2">
                        <button onclick="toggleSocialLoginLink('kakao')" id="link-btn-kakao" class="py-2 rounded-xl border border-gray-200 bg-white font-bold text-center flex items-center justify-center gap-1 hover:bg-yellow-50/20"><i class="fa-solid fa-comment text-yellow-600"></i>카카오 연동</button>
                        <button onclick="toggleSocialLoginLink('google')" id="link-btn-google" class="py-2 rounded-xl border border-gray-200 bg-white font-bold text-center flex items-center justify-center gap-1 hover:bg-rose-50/20"><i class="fa-brands fa-google text-rose-600"></i>구글 연동</button>
                    </div>
                </div>
                <div class="flex gap-2">
                    <button onclick="triggerLogout()" class="flex-1 bg-gray-100 hover:bg-gray-200 text-gray-700 font-bold py-2.5 rounded-xl transition-all text-center">
                        <i class="fa-solid fa-right-from-bracket mr-1"></i> 로그아웃 (로그인 화면 이동)
                    </button>
                    <button onclick="triggerWithdrawal()" class="flex-1 bg-rose-50 hover:bg-rose-100 text-rose-600 font-bold py-2.5 rounded-xl transition-all text-center">
                        <i class="fa-solid fa-user-xmark mr-1"></i> 회원 탈퇴
                    </button>
                </div>
            </div>
        </div>


        <!-- 📍 앱 권한 관리 -->
        <div class="bg-white rounded-3xl p-5 border border-amber-50 shadow-sm space-y-3">
            <h3 class="font-black text-gray-800 text-sm flex items-center border-b pb-2">
                <i class="fa-solid fa-bell text-brand-500 mr-2"></i>앱 권한 및 알림 설정 ⚙️
            </h3>
            <div class="space-y-4 text-xs">
                <!-- 위치 권한 토글 -->
                <div class="flex items-center justify-between bg-gray-50/60 p-3.5 rounded-xl border border-gray-100">
                    <div class="flex items-center gap-3">
                        <div id="location-perm-icon" class="w-9 h-9 rounded-xl bg-blue-100 flex items-center justify-center text-lg">📍</div>
                        <div>
                            <span class="block font-black text-gray-700">위치 권한 (GPS)</span>
                            <span id="location-perm-status-text" class="text-[10px] text-gray-400 font-bold">상태 확인 중...</span>
                        </div>
                    </div>
                    <button id="location-perm-btn" onclick="handleLocationPermission()"
                        class="px-4 py-2 rounded-xl text-[11px] font-black transition-all bg-blue-500 hover:bg-blue-600 text-white shadow-sm">
                        확인 중...
                    </button>
                </div>

                <!-- 알림 권한 및 토글 -->
                <div class="flex items-center justify-between bg-gray-50/60 p-3.5 rounded-xl border border-gray-100">
                    <div class="flex items-center gap-3">
                        <div id="notification-perm-icon" class="w-9 h-9 rounded-xl bg-amber-100 flex items-center justify-center text-lg">🔔</div>
                        <div>
                            <span class="block font-black text-gray-700">산책 완료 알림 수신</span>
                            <span id="notification-perm-status-text" class="text-[10px] text-gray-400 font-bold">상태 확인 중...</span>
                        </div>
                    </div>
                    <div class="flex items-center gap-3">
                        <button id="notification-perm-btn" onclick="requestNotificationPermission()"
                            class="px-3 py-1.5 rounded-xl text-[10px] font-black transition-all bg-blue-500 hover:bg-blue-600 text-white shadow-sm">
                            알림 허용하기
                        </button>
                        <label class="relative inline-flex items-center cursor-pointer">
                            <input type="checkbox" id="notification-toggle" onclick="toggleNotificationsEnabled()" class="sr-only peer">
                            <div class="w-9 h-5 bg-gray-200 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-4 after:w-4 after:transition-all peer-checked:bg-brand-500"></div>
                        </label>
                    </div>
                </div>

                <p class="text-[10px] text-gray-400 font-medium px-1 leading-relaxed">
                    📌 위치 권한을 허용하면 산책 탭에서 현재 위치 기반 지도가 자동으로 열립니다.<br>
                    🔔 알림 설정을 켜면 산책 완료(기록 정지) 시 귀여운 축하 알림 메시지를 받을 수 있습니다.
                </p>
            </div>
        </div>


        <!-- 화면 테마 설정 및 측정 표기법 단위 -->
        <div class="bg-white rounded-3xl p-5 border border-amber-50 shadow-sm space-y-4">
            <h3 class="font-black text-gray-800 text-sm flex items-center border-b pb-2">
                <i class="fa-solid fa-palette text-brand-500 mr-2"></i>테마 & 단위 가독성 설정 🎨
            </h3>
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-6 text-xs">
                <!-- 빛깔 테마 -->
                <div class="space-y-2">
                    <span class="block font-bold text-gray-400 text-[10px] uppercase">빛깔 테마 변경</span>
                    <div class="grid grid-cols-3 gap-2">
                        <button id="btn-theme-light" onclick="setAppTheme('light')"
                            class="py-2.5 rounded-xl border border-gray-200 hover:bg-gray-50 font-bold text-center">라이트</button>
                        <button id="btn-theme-sepia" onclick="setAppTheme('sepia')"
                            class="py-2.5 rounded-xl border border-gray-200 hover:bg-gray-50 font-bold text-center">세피아</button>
                        <button id="btn-theme-dark" onclick="setAppTheme('dark')"
                            class="py-2.5 rounded-xl border border-gray-200 hover:bg-gray-50 font-bold text-center">다크</button>
                    </div>
                </div>
                <!-- 측정 단위 -->
                <div class="space-y-2">
                    <span class="block font-bold text-gray-400 text-[10px] uppercase">측정 가독성 단위</span>
                    <div class="grid grid-cols-2 gap-2">
                        <button id="btn-unit-metric" onclick="setAppUnits('metric')"
                            class="py-2.5 rounded-xl border border-gray-200 hover:bg-gray-50 font-bold text-center">미터법
                            (km/kg)</button>
                        <button id="btn-unit-imperial" onclick="setAppUnits('imperial')"
                            class="py-2.5 rounded-xl border border-gray-200 hover:bg-gray-50 font-bold text-center">야드법
                            (mile/lbs)</button>
                    </div>
                </div>
            </div>
            <button onclick="saveAppSettings()"
                class="w-full bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-xs py-2.5 rounded-xl transition-all shadow-md">
                환경설정 적용 적용하기 ⚙️
            </button>
        </div>

        <!-- 💖 펫과나 동행 후원하기 -->
        <div class="bg-gradient-to-br from-amber-50/50 to-indigo-50/30 rounded-3xl p-5 border border-amber-100 shadow-sm space-y-4 relative overflow-hidden">
            <div class="absolute top-0 right-0 w-24 h-24 bg-brand-500/5 rounded-full -mr-8 -mt-8"></div>
            <h3 class="font-black text-gray-800 text-sm flex items-center border-b pb-2">
                <i class="fa-solid fa-heart text-rose-500 mr-2 animate-pulse"></i>따뜻한 동행 후원하기 💖
            </h3>
            <p class="text-xs text-gray-500 leading-relaxed">
                펫과나(Pet&Na)는 집사님들의 소중한 관심과 후원으로 무럭무럭 자라납니다. 후원금은 펫 사주 알고리즘 고도화와 안심 산책 데이터베이스 정밀 확장에 전액 사용됩니다.
            </p>
            
            <!-- 금액 등급 선택 -->
            <div class="grid grid-cols-3 gap-2">
                <button id="donation-preset-5000" onclick="selectDonationPreset(5000, this)" class="donation-preset-btn py-2.5 rounded-xl border border-amber-200/60 bg-white hover:bg-amber-50/60 text-gray-700 font-bold text-center text-xs transition-all">
                    <span class="block text-[10px] text-amber-500 font-bold">초보 집사</span>
                    5,000원
                </button>
                <button id="donation-preset-20000" onclick="selectDonationPreset(20000, this)" class="donation-preset-btn py-2.5 rounded-xl border border-amber-200/60 bg-white hover:bg-amber-50/60 text-gray-700 font-bold text-center text-xs transition-all">
                    <span class="block text-[10px] text-brand-600 font-bold">👑 우수 집사</span>
                    20,000원
                </button>
                <button id="donation-preset-50000" onclick="selectDonationPreset(50000, this)" class="donation-preset-btn py-2.5 rounded-xl border border-amber-200/60 bg-white hover:bg-amber-50/60 text-gray-700 font-bold text-center text-xs transition-all">
                    <span class="block text-[10px] text-indigo-500 font-bold">대감 집사</span>
                    50,000원
                </button>
            </div>

            <!-- 직접 입력 -->
            <div class="flex items-center gap-2">
                <div class="relative flex-grow">
                    <input type="number" id="donation-custom-amount" placeholder="직접 후원할 금액을 입력하세요" class="w-full text-xs border rounded-lg p-2.5 outline-none focus:border-brand-500 pr-8 bg-white">
                    <span class="absolute right-3 top-2.5 text-xs font-bold text-gray-400">원</span>
                </div>
                <button onclick="triggerDonation()" class="bg-brand-500 hover:bg-brand-600 text-white font-bold text-xs py-2.5 px-4 rounded-xl transition-all shadow-md flex-shrink-0 flex items-center gap-1.5">
                    <i class="fa-solid fa-coins"></i>후원하기
                </button>
            </div>
        </div>

        <!-- 📞 펫과나 헬프 데스크 (자주 묻는 질문) -->
        <div class="bg-white rounded-3xl p-5 border border-amber-50 shadow-sm space-y-4">
            <h3 class="font-black text-gray-800 text-sm flex items-center border-b pb-2">
                <i class="fa-solid fa-circle-question text-brand-500 mr-2"></i>펫과나 헬프 데스크 (FAQ) 📞
            </h3>
            <div class="space-y-2 text-xs">
                <!-- FAQ 1 -->
                <div class="border border-amber-100/60 rounded-xl overflow-hidden">
                    <button onclick="toggleFAQ(1)" class="w-full flex items-center justify-between p-3.5 bg-amber-50/30 font-bold text-gray-700 text-left hover:bg-amber-50/60 transition-all outline-none">
                        <span>🔮 Q. 펫 사주 성향 운세는 어떤 원리로 계산되나요?</span>
                        <i id="faq-icon-1" class="fa-solid fa-chevron-down text-[10px] text-brand-500 transition-transform"></i>
                    </button>
                    <div id="faq-content-1" class="hidden p-3.5 border-t border-amber-50/50 bg-white text-gray-500 leading-relaxed">
                        반려동물의 양력/음력 출생 정보(일시)와 동양 정통 사주명리학(오행, 십이운성, 신살) 알고리즘을 융합하여 분석합니다. 주인 집사님 사주의 삼합(三合), 육합(六合) 조화도와 반려동물의 타고난 기운의 상생상극(相生相剋) 상태를 정밀 연산하여 과학적이고 현명한 동행 솔루션을 제공합니다.
                    </div>
                </div>
                <!-- FAQ 2 -->
                <div class="border border-amber-100/60 rounded-xl overflow-hidden">
                    <button onclick="toggleFAQ(2)" class="w-full flex items-center justify-between p-3.5 bg-amber-50/30 font-bold text-gray-700 text-left hover:bg-amber-50/60 transition-all outline-none">
                        <span>🐕 Q. 펫 지능 테스트 결과는 신뢰할 수 있나요?</span>
                        <i id="faq-icon-2" class="fa-solid fa-chevron-down text-[10px] text-brand-500 transition-transform"></i>
                    </button>
                    <div id="faq-content-2" class="hidden p-3.5 border-t border-amber-50/50 bg-white text-gray-500 leading-relaxed">
                        글로벌 동물 행동 의학 연구소의 표준 인지 평가 척도(강아지/고양이 감각 인지 능력, 문제해결력, 단기 기억장치) 테스트를 간소화하여 주입했습니다. 집사님이 집에서 가볍게 진행해보는 홈 시뮬레이션 진단으로, 진단 완료 시 마이펫 프로필 룸의 지능 등급 배지로 영구 저장됩니다.
                    </div>
                </div>
                <!-- FAQ 3 -->
                <div class="border border-amber-100/60 rounded-xl overflow-hidden">
                    <button onclick="toggleFAQ(3)" class="w-full flex items-center justify-between p-3.5 bg-amber-50/30 font-bold text-gray-700 text-left hover:bg-amber-50/60 transition-all outline-none">
                        <span>🗺️ Q. 제작한 안심 산책 루트는 어떻게 활용하나요?</span>
                        <i id="faq-icon-3" class="fa-solid fa-chevron-down text-[10px] text-brand-500 transition-transform"></i>
                    </button>
                    <div id="faq-content-3" class="hidden p-3.5 border-t border-amber-50/50 bg-white text-gray-500 leading-relaxed">
                        산책하기 탭에서 GPS 주행 마킹 또는 모의 마킹을 통해 작성한 흔적 루트 지도는 '내 맵에 장착하기'를 눌러 언제든 다시 로드할 수 있으며, '루트 정보 공유' 기능을 통해 소셜 채팅방의 이웃 집사들에게 카드 한 장으로 직접 자랑 및 나눔 전송을 할 수 있습니다.
                    </div>
                </div>
                <!-- FAQ 4 -->
                <div class="border border-amber-100/60 rounded-xl overflow-hidden">
                    <button onclick="toggleFAQ(4)" class="w-full flex items-center justify-between p-3.5 bg-amber-50/30 font-bold text-gray-700 text-left hover:bg-amber-50/60 transition-all outline-none">
                        <span>📸 Q. 액티브 데코룸에 내 펫 미디어를 올리는 데 제한이 있나요?</span>
                        <i id="faq-icon-4" class="fa-solid fa-chevron-down text-[10px] text-brand-500 transition-transform"></i>
                    </button>
                    <div id="faq-content-4" class="hidden p-3.5 border-t border-amber-50/50 bg-white text-gray-500 leading-relaxed">
                        펫과나의 최신 프리미엄 그래픽 엔진은 직접 파일 올리기 기능(이미지 및 동영상 파일)을 완전 무상 지원합니다! 브라우저 기반 렌더링을 사용하므로 서버 용량 제한 없이 기기 스펙 내에서 100% 안전하게 무제한 스티커 꾸미기와 백업 저장이 지원됩니다.
                    </div>
                </div>
            </div>
        </div>

        <!-- ✍️ 1:1 안심 신문고 (문의 게시판) -->
        <div class="bg-white rounded-3xl p-5 border border-amber-50 shadow-sm space-y-4">
            <div class="flex items-center justify-between border-b pb-2">
                <h3 class="font-black text-gray-800 text-sm flex items-center">
                    <i class="fa-solid fa-pen-to-square text-brand-500 mr-2"></i>1:1 안심 신문고 (문의 게시판) ✍️
                </h3>
                <button onclick="openInquiryWriteModal()" class="bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-[10px] py-1.5 px-3 rounded-lg shadow-sm transition-all flex items-center gap-1">
                    <i class="fa-solid fa-feather-pointed"></i>문의 접수하기
                </button>
            </div>
            
            <!-- 문의 내역 목록 테이블 -->
            <div class="overflow-x-auto rounded-xl border border-gray-100 shadow-inner">
                <table class="w-full text-left text-xs border-collapse">
                    <thead>
                        <tr class="bg-amber-50/40 text-gray-500 font-bold border-b border-gray-100">
                            <th class="p-3 text-[10px]">접수일자</th>
                            <th class="p-3 text-[10px]">분류</th>
                            <th class="p-3 text-[10px]">문의제목</th>
                            <th class="p-3 text-[10px] text-center">처리상태</th>
                        </tr>
                    </thead>
                    <tbody id="inquiry-list-body">
                        <!-- Dynamic Rows -->
                    </tbody>
                </table>
            </div>
        </div>
    </div>

    <!-- 오른쪽 패널: 데이터 백업 및 복구 관리소 -->
    <div class="space-y-6">
        <div class="bg-white rounded-3xl p-5 border border-amber-50 shadow-sm space-y-4">
            <h3 class="font-black text-gray-800 text-sm flex items-center border-b pb-2">
                <i class="fa-solid fa-database text-brand-500 mr-2"></i>안심 데이터 보관소 🗄️
            </h3>
            <div class="space-y-2.5 text-xs">
                <!-- 풍요로운 데모 데이터 셋업 -->
                <button onclick="resetToRichDemoData()"
                    class="w-full bg-emerald-50 hover:bg-emerald-100 text-emerald-800 border border-emerald-200 font-bold py-3 px-4 rounded-xl transition-all flex items-center justify-between">
                    <span>🪄 일주일 데모 데이터 주입</span>
                    <i class="fa-solid fa-chevron-right"></i>
                </button>

                <!-- JSON 내보내기 -->
                <button onclick="exportAllDataAsJSON()"
                    class="w-full bg-amber-50 hover:bg-amber-100 text-brand-800 border border-amber-200 font-bold py-3 px-4 rounded-xl transition-all flex items-center justify-between">
                    <span>💾 데이터 전체 내보내기 (JSON)</span>
                    <i class="fa-solid fa-file-export"></i>
                </button>

                <!-- JSON 불러오기 -->
                <div class="relative w-full">
                    <input type="file" id="import-json-file" onchange="importDataFromJSON(event)"
                        accept=".json" class="absolute inset-0 opacity-0 cursor-pointer">
                    <div
                        class="w-full bg-blue-50 hover:bg-blue-100 text-blue-800 border border-blue-200 font-bold py-3 px-4 rounded-xl transition-all flex items-center justify-between">
                        <span>📂 백업 파일 불러오기 (JSON)</span>
                        <i class="fa-solid fa-file-import"></i>
                    </div>
                </div>

                <!-- 공장 초기화 -->
                <button onclick="wipeAllAppData()"
                    class="w-full bg-rose-50 hover:bg-rose-100 text-rose-800 border border-rose-200 font-bold py-3 px-4 rounded-xl transition-all flex items-center justify-between mb-2">
                    <span>🗑️ 보관소 완전 삭제 (초기화)</span>
                    <i class="fa-solid fa-trash-can"></i>
                </button>

                <!-- 보관소 완전 파괴 -->
                <button onclick="destroyAllLocalStorage()"
                    class="w-full bg-rose-600 hover:bg-rose-700 text-white font-extrabold py-3 px-4 rounded-xl transition-all flex items-center justify-between shadow-md">
                    <span>🚨 보관소 완전 파괴 (전체 초기화)</span>
                    <i class="fa-solid fa-bomb animate-pulse"></i>
                </button>
            </div>
        </div>

        <!-- 🚨 시스템 오류 로그 -->
        <div class="bg-white rounded-3xl p-5 border border-amber-50 shadow-sm space-y-4">
            <h3 class="font-black text-gray-800 text-sm flex items-center border-b pb-2">
                <i class="fa-solid fa-bug text-rose-500 mr-2 animate-pulse"></i>시스템 오류 로그 🚨
            </h3>
            <p class="text-xs text-gray-500 leading-relaxed">
                앱 실행 중 발생한 예외 상황 및 오류 경고 로그를 안전하게 보관 및 모니터링합니다.
            </p>
            <div class="flex gap-2">
                <button onclick="openErrorLogModal()" class="flex-1 bg-rose-50 hover:bg-rose-100 text-rose-600 font-bold py-2.5 rounded-xl transition-all text-center text-xs">
                    <i class="fa-solid fa-clipboard-list mr-1"></i>오류 로그 보기
                </button>
                <button onclick="clearSystemErrorLogs()" class="flex-1 bg-gray-100 hover:bg-gray-200 text-gray-700 font-bold py-2.5 rounded-xl transition-all text-center text-xs">
                    <i class="fa-solid fa-trash-can mr-1"></i>로그 비우기
                </button>
            </div>
        </div>

        <!-- ⚖️ 법적 고지 및 약관 -->
        <div class="bg-white rounded-3xl p-5 border border-amber-50 shadow-sm space-y-4">
            <h3 class="font-black text-gray-800 text-sm flex items-center border-b pb-2">
                <i class="fa-solid fa-scale-balanced text-brand-500 mr-2"></i>법적 고지 및 약관 ⚖️
            </h3>
            <div class="space-y-2.5 text-xs">
                <a href="/TERMS_OF_SERVICE.md" target="_blank" class="flex items-center justify-between bg-gray-50/60 p-3.5 rounded-xl border border-gray-100 hover:bg-brand-50 hover:border-brand-200 transition-all group">
                    <div class="flex items-center gap-3">
                        <div class="w-9 h-9 rounded-xl bg-blue-100 flex items-center justify-center text-lg">📋</div>
                        <div>
                            <span class="block font-black text-gray-700 group-hover:text-brand-600">서비스 이용약관</span>
                            <span class="text-[10px] text-gray-400 font-medium">펫과나 서비스 이용 규정</span>
                        </div>
                    </div>
                    <i class="fa-solid fa-external-link text-gray-300 group-hover:text-brand-500"></i>
                </a>

                <a href="/PRIVACY_POLICY.md" target="_blank" class="flex items-center justify-between bg-gray-50/60 p-3.5 rounded-xl border border-gray-100 hover:bg-brand-50 hover:border-brand-200 transition-all group">
                    <div class="flex items-center gap-3">
                        <div class="w-9 h-9 rounded-xl bg-emerald-100 flex items-center justify-center text-lg">🔒</div>
                        <div>
                            <span class="block font-black text-gray-700 group-hover:text-brand-600">개인정보처리방침</span>
                            <span class="text-[10px] text-gray-400 font-medium">개인정보 수집 및 이용 안내</span>
                        </div>
                    </div>
                    <i class="fa-solid fa-external-link text-gray-300 group-hover:text-brand-500"></i>
                </a>

                <button onclick="requestDataDeletion()" class="w-full flex items-center justify-between bg-rose-50/60 p-3.5 rounded-xl border border-rose-200 hover:bg-rose-100 hover:border-rose-300 transition-all group">
                    <div class="flex items-center gap-3">
                        <div class="w-9 h-9 rounded-xl bg-rose-100 flex items-center justify-center text-lg">🗑️</div>
                        <div class="text-left">
                            <span class="block font-black text-rose-600">개인정보 삭제 요청</span>
                            <span class="text-[10px] text-rose-400 font-medium">GDPR/PIPA 준수 - 즉시 처리</span>
                        </div>
                    </div>
                    <i class="fa-solid fa-chevron-right text-rose-300 group-hover:text-rose-500"></i>
                </button>
            </div>

            <p class="text-[9px] text-gray-400 font-medium leading-relaxed pt-2 border-t">
                💡 AI 건강 분석은 참고용 정보이며, 의학적 진단을 대체하지 않습니다. 반려동물에게 이상 증상이 있을 경우 반드시 전문 수의사와 상담하시기 바랍니다.
            </p>
        </div>

        <!-- ℹ️ 앱 버전 정보 -->
        <div class="bg-white rounded-3xl p-5 border border-amber-50 shadow-sm text-center space-y-2">
            <span class="block text-[10px] text-gray-400 font-bold uppercase tracking-wider">App Version Info</span>
            <div class="flex flex-col justify-center items-center">
                <span class="text-xl">🐾</span>
                <span class="text-xs font-black text-gray-700 mt-1">펫과나 (Pet&Na)</span>
                <span id="settings-app-version" class="text-[10px] text-brand-600 font-mono font-bold mt-1">v1.3.0</span>
            </div>
            <p class="text-[9px] text-gray-400 font-medium">© 2026 Pet&Na. All rights reserved.</p>
        </div>
    </div>

</div>
`;
