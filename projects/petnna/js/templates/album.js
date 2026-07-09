const ALBUM_TEMPLATE = `
<!-- 메인 일기장 타임라인 뷰 -->
<div class="space-y-6">
    <!-- 헤더 컨트롤 패널 -->
    <div class="bg-white rounded-3xl p-5 border border-amber-50 shadow-sm space-y-3">
        <div class="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-3">
            <div>
                <h2 class="text-lg font-black text-gray-800 flex items-center">
                    <i class="fa-solid fa-book-open text-brand-500 mr-2"></i> 우리의 사진 일기장 📖
                </h2>
                <!-- 일기 스트릭 + 카운트 -->
                <div id="diary-stats-row" class="flex items-center gap-3 mt-1.5"></div>
            </div>
            <div class="flex space-x-2 w-full sm:w-auto">
                <button onclick="openFriendInviteModal()" class="flex-1 sm:flex-none bg-brand-50 hover:bg-brand-100 text-brand-700 font-bold text-xs py-3 px-4 rounded-xl transition-all flex items-center justify-center gap-1.5 shadow-sm border border-brand-100">
                    <i class="fa-solid fa-user-group"></i> 공유 친구
                </button>
                <button onclick="exportDiaryPDF()" class="flex-1 sm:flex-none bg-brand-50 hover:bg-brand-100 text-brand-700 font-bold text-xs py-3 px-4 rounded-xl transition-all flex items-center justify-center gap-1.5 border border-brand-100">
                    <i class="fa-solid fa-file-pdf"></i> PDF
                    <span class="text-[8px] bg-amber-100 text-amber-700 px-1 rounded font-black">PRO</span>
                </button>
                <button onclick="openDiaryComposerModal()" class="flex-1 sm:flex-none bg-brand-500 hover:bg-brand-600 text-white font-black text-xs py-3 px-4 rounded-xl transition-all shadow-md flex items-center justify-center gap-1.5">
                    <i class="fa-solid fa-pen"></i> 일기 쓰기 ✍️
                </button>
            </div>
        </div>
        <!-- "1년 전 오늘" 팝업 슬롯 -->
        <div id="diary-on-this-day" class="hidden"></div>
    </div>

    <!-- 메인 콘텐츠 영역 -->
    <div class="grid grid-cols-1 lg:grid-cols-4 gap-6">
        <!-- 타임라인 컨테이너 (좌측 3/4) -->
        <div class="lg:col-span-3 bg-white rounded-3xl p-5 border border-amber-50 shadow-sm min-h-[400px]">
            <!-- 빈 상태일 때 보여줄 기본 일기장 플레이스홀더 -->
            <div id="diary-empty-state" class="flex flex-col items-center justify-center h-full text-center py-10">
                <div class="w-20 h-20 bg-brand-50 rounded-full flex items-center justify-center mb-4 text-brand-500 text-3xl shadow-inner">
                    <i class="fa-solid fa-book-heart"></i>
                </div>
                <h3 class="text-gray-800 font-black text-sm mb-2">아직 작성된 일기가 없어요!</h3>
                <p class="text-gray-400 text-xs keep-all max-w-xs mb-5 leading-relaxed">우측 상단 '새로운 일기 쓰기' 버튼을 눌러 소중한 아이와의 첫 번째 추억을 남겨보세요. 일기를 꾸미고 친구들과 공유할 수 있어요.</p>
                <button onclick="openDiaryComposerModal()" class="bg-brand-500 text-white font-bold text-xs py-3 px-5 rounded-xl hover:bg-brand-600 transition-all shadow-md">
                    첫 일기 작성하기 ✍️
                </button>
            </div>

            <div id="diary-timeline-container" class="hidden space-y-6 pt-2 border-l-2 border-brand-100 ml-3 pl-4 relative before:absolute before:inset-0 before:bg-brand-50/50">
                <!-- 타임라인 일기 엔트리가 동적 배치됨 -->
            </div>
            <div id="album-publish-btn-container" class="mt-4 hidden"></div>
        </div>

        <!-- 우측 사이드 패널 (우측 1/4) -->
        <div class="space-y-4">
            <!-- 바로가기 배너 -->
            <div class="bg-gradient-to-br from-brand-500 to-brand-600 rounded-3xl p-5 shadow-sm text-white cursor-pointer hover:shadow-md transition-all transform hover:-translate-y-0.5" onclick="goToDayRoom()">
                <div class="flex justify-between items-start mb-2">
                    <div class="bg-white/20 p-2 rounded-xl backdrop-blur-sm">
                        <span class="text-xl">🐾</span>
                    </div>
                    <span class="bg-white/20 text-[9px] font-black px-2 py-0.5 rounded-full backdrop-blur-sm">돌봄 스케쥴러</span>
                </div>
                <h3 class="font-black text-sm mb-1">댕이의 하루방 이동</h3>
                <p class="text-white/80 text-[10px] leading-tight">오늘의 산책, 미용, 병원 일정을 꼼꼼하게 관리하세요!</p>
                <div class="mt-3 flex items-center text-[10px] font-bold text-white/90 group">
                    <span>이동하기</span> <i class="fa-solid fa-arrow-right ml-1 transform group-hover:translate-x-1 transition-transform"></i>
                </div>
            </div>

            <!-- 일기 공유 친구 목록 -->
            <div class="bg-white rounded-3xl p-5 border border-gray-100 shadow-sm">
                <div class="flex justify-between items-center mb-4">
                    <h3 class="font-black text-gray-800 text-xs flex items-center">
                        <i class="fa-solid fa-user-group text-brand-500 mr-1.5"></i> 일기 공유 친구
                    </h3>
                    <span class="bg-brand-50 text-brand-600 text-[9px] font-black px-1.5 py-0.5 rounded-md" id="diary-friend-count">2명</span>
                </div>
                <p class="text-[9px] text-gray-400 mb-3 leading-tight">서로 일기를 공유하며 아이들의 성장을 함께 지켜봐요.</p>
                
                <div class="space-y-3" id="diary-shared-friends-list">
                    <!-- JS로 렌더링 -->
                    <div class="flex items-center justify-between">
                        <div class="flex items-center gap-2">
                            <div class="w-8 h-8 rounded-full bg-gray-200 overflow-hidden"><img loading="lazy" src="https://images.unsplash.com/photo-1543466835-00a7907e9de1?w=100" class="w-full h-full object-cover"></div>
                            <div>
                                <span class="block text-[11px] font-bold text-gray-800">이웃집 집사</span>
                                <span class="block text-[9px] text-gray-400">초코네</span>
                            </div>
                        </div>
                        <span class="text-[9px] font-bold text-brand-500 bg-brand-50 px-1.5 py-0.5 rounded">구독중</span>
                    </div>
                    <div class="flex items-center justify-between">
                        <div class="flex items-center gap-2">
                            <div class="w-8 h-8 rounded-full bg-gray-200 overflow-hidden"><img loading="lazy" src="https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba?w=100" class="w-full h-full object-cover"></div>
                            <div>
                                <span class="block text-[11px] font-bold text-gray-800">동네 냥이맘</span>
                                <span class="block text-[9px] text-gray-400">나비네</span>
                            </div>
                        </div>
                        <span class="text-[9px] font-bold text-brand-500 bg-brand-50 px-1.5 py-0.5 rounded">구독중</span>
                    </div>
                </div>
                
                <button onclick="openFriendInviteModal()" class="w-full mt-4 bg-gray-50 hover:bg-brand-50 text-gray-600 hover:text-brand-600 font-bold text-[10px] py-3 rounded-xl transition-colors border border-gray-100 flex items-center justify-center gap-1.5">
                    <i class="fa-solid fa-plus"></i> 친구 초대하기
                </button>
            </div>

            <!-- 성장 기록 -->
            <div class="bg-white rounded-3xl p-5 border border-gray-100 shadow-sm">
                <div class="flex items-center justify-between mb-3">
                    <h3 class="font-black text-gray-800 text-xs flex items-center">
                        <i class="fa-solid fa-weight-scale text-emerald-500 mr-1.5"></i> 성장 기록
                    </h3>
                    <button onclick="openWeightLogModal()" class="text-[9px] font-black bg-emerald-50 text-emerald-600 border border-emerald-100 px-2 py-1 rounded-lg hover:bg-emerald-100 transition-all">
                        + 기록
                    </button>
                </div>
                <div id="growth-chart-container" style="height:80px">
                    <canvas id="growth-chart"></canvas>
                </div>
                <div id="growth-empty" class="text-center py-3 text-[10px] text-gray-400 hidden">
                    체중을 기록하면 성장 그래프가 나타납니다!
                </div>
                <div id="growth-latest" class="mt-2 text-[10px] text-gray-500 font-medium"></div>
            </div>
        </div>
    </div>
</div>

<!-- 체중 기록 모달 -->
<div id="weight-log-modal" class="hidden fixed inset-0 bg-black/50 z-50 flex items-center justify-center px-4">
    <div class="bg-white rounded-3xl p-6 w-full max-w-xs space-y-4 shadow-2xl animate-fade-in">
        <div class="flex items-center justify-between">
            <h3 class="font-black text-gray-800 text-sm">⚖️ 체중 기록</h3>
            <button onclick="closeWeightLogModal()" class="text-gray-400 hover:text-gray-600" aria-label="닫기"><i class="fa-solid fa-xmark"></i></button>
        </div>
        <div class="space-y-3">
            <div>
                <label class="block text-[10px] font-bold text-gray-500 mb-1">체중 (kg)</label>
                <input type="number" id="weight-log-input" step="0.1" min="0" max="100"
                    placeholder="예: 4.2"
                    class="w-full border border-gray-200 rounded-xl px-3 py-2.5 outline-none focus:border-emerald-400 text-sm font-bold text-center">
            </div>
            <div>
                <label class="block text-[10px] font-bold text-gray-500 mb-1">메모 (선택)</label>
                <input type="text" id="weight-log-note" placeholder="예: 정기 검진 후"
                    class="w-full border border-gray-200 rounded-xl px-3 py-2 outline-none focus:border-emerald-400 text-xs">
            </div>
        </div>
        <button onclick="submitWeightLog()" class="w-full py-3 bg-emerald-500 hover:bg-emerald-600 text-white font-black text-sm rounded-2xl transition-all shadow-sm">
            기록 저장
        </button>
    </div>
</div>

<!-- ============================================== -->
<!-- 모달 1: 통합 일기 작성 & 사진 꾸미기 에디터 (Diary Composer) -->
<!-- ============================================== -->
<div id="diary-composer-modal" class="hidden fixed inset-0 z-[100] flex items-center justify-center p-2 sm:p-4 backdrop-blur-sm bg-black/60 opacity-0 transition-opacity duration-300">
    <!-- 대형 모달 레이아웃 (모바일에서는 전체화면 가까이) -->
    <div class="bg-white w-full max-w-5xl max-h-[95vh] rounded-3xl overflow-hidden shadow-2xl transform scale-95 transition-transform duration-300 flex flex-col">
        <!-- 모달 헤더 -->
        <div class="bg-brand-50 p-4 flex justify-between items-center border-b border-brand-100 shrink-0">
            <h3 class="font-black text-brand-800 text-sm"><i class="fa-solid fa-wand-magic-sparkles mr-1 text-brand-500"></i> 일기장 꾸미기 & 쓰기</h3>
            <div class="flex items-center gap-3">
                <button onclick="resetStickerCanvas()" class="text-[10px] text-gray-500 hover:text-red-500 font-bold px-2 py-1 bg-white rounded-lg shadow-sm">초기화</button>
                <button onclick="closeDiaryComposerModal()" class="text-brand-400 hover:text-brand-600 text-lg" aria-label="닫기"><i class="fa-solid fa-xmark"></i></button>
            </div>
        </div>
        
        <!-- 모달 바디 (스크롤 가능) -->
        <div class="p-4 sm:p-6 overflow-y-auto flex-grow bg-gray-50/30">
            <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 items-start">
                
                <!-- [좌측] 도화지 및 사진 설정 -->
                <div class="space-y-4">
                    <!-- 도화지 영역 -->
                    <div id="decorator-canvas" class="relative w-full aspect-[4/3] bg-gray-100 rounded-2xl overflow-hidden shadow-inner border border-amber-100/40 flex items-center justify-center">
                        <div id="decorator-placeholder" class="text-center p-6 space-y-2 pointer-events-none">
                            <span class="text-4xl block">🖼️</span>
                            <span class="block text-xs font-bold text-gray-400">우측 툴에서 사진/영상을 올리면 여기에 나타납니다.</span>
                        </div>
                        
                        <!-- 업로드 뱃지 -->
                        <div id="deco-uploading-badge" class="hidden absolute top-3 left-3 bg-brand-500/90 text-white text-[9px] font-black px-2.5 py-1 rounded-full z-20 shadow flex items-center gap-1.5 backdrop-blur-sm">
                            <i class="fa-solid fa-circle-notch animate-spin"></i><span>업로드 중...</span>
                        </div>
                        <div id="deco-upload-badge" class="hidden absolute top-3 left-3 bg-emerald-500/90 text-white text-[9px] font-black px-2 py-0.5 rounded-full z-20 shadow flex items-center gap-1 backdrop-blur-sm">
                            <i class="fa-solid fa-circle-check"></i><span>업로드 완료</span>
                        </div>

                        <img loading="lazy" id="decorator-bg" class="hidden w-full h-full object-cover pointer-events-none">
                        <video id="decorator-bg-video" class="hidden w-full h-full object-cover pointer-events-none" muted loop autoplay playsinline></video>
                        <div id="stickers-container" class="absolute inset-0 pointer-events-none z-10"></div>
                    </div>

                    <!-- 스티커 HUD (숨겨져 있다가 스티커 선택 시 나타남) -->
                    <div id="sticker-control-hud" class="hidden bg-amber-50 p-4 rounded-2xl border border-amber-200/50 space-y-3">
                        <div class="flex justify-between items-center pb-2 border-b border-amber-200/30">
                            <span class="text-[10px] font-black text-amber-900"><i class="fa-solid fa-expand text-brand-500 mr-1"></i> 선택한 스티커 조작</span>
                            <button onclick="deleteActiveSticker()" class="text-[10px] bg-rose-500 hover:bg-rose-600 text-white font-bold py-1 px-2 rounded-lg"><i class="fa-solid fa-trash-can"></i> 삭제</button>
                        </div>
                        <div class="grid grid-cols-2 gap-4">
                            <div class="space-y-1">
                                <div class="flex justify-between text-[9px] text-amber-800 font-bold"><span>크기 조절</span><span id="hud-scale-val">1.0x</span></div>
                                <input type="range" id="hud-sticker-scale" min="0.5" max="3" step="0.1" value="1.0" oninput="updateActiveStickerScale(this.value)" class="w-full h-2 bg-amber-200 rounded-lg accent-brand-500">
                            </div>
                            <div class="space-y-1">
                                <div class="flex justify-between text-[9px] text-amber-800 font-bold"><span>회전 각도</span><span id="hud-rotate-val">0°</span></div>
                                <input type="range" id="hud-sticker-rotate" min="-180" max="180" step="5" value="0" oninput="updateActiveStickerRotate(this.value)" class="w-full h-2 bg-amber-200 rounded-lg accent-brand-500">
                            </div>
                        </div>
                    </div>
                </div>

                <!-- [우측] 도구함 및 텍스트 일기 작성 -->
                <div class="space-y-4">
                    
                    <!-- 1. 사진 업로드 및 필터 -->
                    <div class="bg-white p-4 rounded-2xl border border-gray-100 shadow-sm space-y-4">
                        <h4 class="font-black text-gray-800 text-[11px] pb-1 border-b">1️⃣ 사진/영상 & 필터</h4>
                        <div class="flex gap-2 items-center">
                            <label for="deco-file-upload" class="cursor-pointer bg-brand-50 text-brand-600 border border-brand-100 font-black text-[11px] py-2 px-3 rounded-xl flex items-center space-x-1.5 shrink-0 transition-colors hover:bg-brand-100">
                                <i class="fa-solid fa-image"></i><span>파일 올리기</span>
                            </label>
                            <input type="file" id="deco-file-upload" onchange="uploadDecoDeviceMedia(event)" accept="image/*, video/*" class="hidden">
                            
                            <div class="flex overflow-x-auto gap-1.5 pb-1 no-scrollbar" id="deco-filter-bar">
                                <button onclick="applyDecoFilter('natural', this)" class="filter-btn bg-brand-500 text-white font-bold text-[9px] py-1.5 px-2 rounded-lg border border-transparent whitespace-nowrap">Natural</button>
                                <button onclick="applyDecoFilter('sepia', this)" class="filter-btn bg-gray-50 text-gray-600 font-bold text-[9px] py-1.5 px-2 rounded-lg border border-gray-200 whitespace-nowrap">세피아 📜</button>
                                <button onclick="applyDecoFilter('vintage', this)" class="filter-btn bg-gray-50 text-gray-600 font-bold text-[9px] py-1.5 px-2 rounded-lg border border-gray-200 whitespace-nowrap">빈티지 🎞️</button>
                            </div>
                        </div>
                        
                        <!-- 동영상 트리머 (비디오일 때만 나타남) -->
                        <div id="video-trimmer-box" class="hidden bg-brand-50/50 p-3 rounded-xl border border-brand-100 space-y-2 mt-2">
                            <span class="block text-[9px] text-brand-900 font-black flex items-center"><i class="fa-solid fa-scissors mr-1.5"></i> 동영상 재생 구간 자르기</span>
                            <div class="grid grid-cols-2 gap-2">
                                <input type="range" id="trim-start-slider" min="0" max="30" step="0.5" value="0" oninput="updateVideoTrimStart(this.value)" class="w-full h-1.5 bg-brand-200 rounded-lg accent-brand-600">
                                <input type="range" id="trim-end-slider" min="1" max="30" step="0.5" value="10" oninput="updateVideoTrimEnd(this.value)" class="w-full h-1.5 bg-brand-200 rounded-lg accent-brand-600">
                            </div>
                            <div class="flex justify-between text-[8px] font-bold text-brand-700">
                                <span>Start: <span id="trim-start-val">0.0s</span></span>
                                <span>End: <span id="trim-end-val">10.0s</span></span>
                            </div>
                        </div>
                    </div>

                    <!-- 2. 스티커 장식 -->
                    <div class="bg-white p-4 rounded-2xl border border-gray-100 shadow-sm space-y-3">
                        <h4 class="font-black text-gray-800 text-[11px] pb-1 border-b">2️⃣ 스티커 장식</h4>
                        <div class="grid grid-cols-8 gap-1.5 text-lg text-center">
                            <button onclick="addEmojiSticker('👑')" class="hover:scale-125 transition-transform">👑</button>
                            <button onclick="addEmojiSticker('🕶️')" class="hover:scale-125 transition-transform">🕶️</button>
                            <button onclick="addEmojiSticker('🎀')" class="hover:scale-125 transition-transform">🎀</button>
                            <button onclick="addEmojiSticker('✨')" class="hover:scale-125 transition-transform">✨</button>
                            <button onclick="addEmojiSticker('💖')" class="hover:scale-125 transition-transform">💖</button>
                            <button onclick="addEmojiSticker('🥩')" class="hover:scale-125 transition-transform">🥩</button>
                            <button onclick="addEmojiSticker('🥎')" class="hover:scale-125 transition-transform">🥎</button>
                            <button onclick="addEmojiSticker('💩')" class="hover:scale-125 transition-transform">💩</button>
                        </div>
                        <div class="flex space-x-1.5 pt-2">
                            <input type="text" id="sticker-text-input" placeholder="말풍선 입력..." class="flex-grow text-[10px] border border-gray-200 rounded-lg px-2 py-1.5 outline-none focus:border-brand-500 bg-gray-50">
                            <select id="sticker-bubble-theme" class="text-[9px] border border-gray-200 rounded-lg bg-white outline-none w-20">
                                <option value="bg-white/95 text-brand-700" selected>기본 🤍</option>
                                <option value="bg-amber-100/95 text-amber-900">골드 👑</option>
                            </select>
                            <button onclick="addTextSticker()" class="bg-brand-500 hover:bg-brand-600 text-white font-bold text-[9px] px-2.5 rounded-lg">생성</button>
                        </div>
                    </div>

                    <!-- 3. 일기 쓰기 -->
                    <div class="bg-white p-4 rounded-2xl border border-gray-100 shadow-sm space-y-3">
                        <h4 class="font-black text-gray-800 text-[11px] pb-1 border-b">3️⃣ 일기 내용 및 감정</h4>
                        <select id="diary-auth-mood" class="w-full border border-gray-200 rounded-xl p-2 outline-none text-[11px] bg-white text-gray-700 font-bold">
                            <option value="☀️ 맑음">☀️ 기분 좋은 맑음</option>
                            <option value="☁️ 흐림">☁️ 차분한 흐림</option>
                            <option value="☔ 비">☔ 감성적인 비</option>
                            <option value="🥳 신남">🥳 에너지 넘치고 신남!</option>
                            <option value="😴 피곤">😴 조금 피곤하고 나른함</option>
                            <option value="💖 사랑해">💖 사랑이 가득함</option>
                        </select>
                        <textarea id="diary-auth-text" rows="3" class="w-full border border-gray-200 rounded-xl p-3 outline-none text-[11px] focus:border-brand-500 bg-gray-50/50" placeholder="오늘 어떤 추억이 있었나요? 글을 남겨주세요."></textarea>
                    </div>

                </div>
            </div>
        </div>
        
        <!-- 모달 푸터 (액션 버튼) -->
        <div class="bg-white p-4 border-t border-gray-100 shrink-0">
            <button onclick="submitDiaryAuth()" class="w-full bg-brand-500 hover:bg-brand-600 text-white font-black text-sm py-3.5 rounded-xl shadow-md transition-all flex items-center justify-center gap-2">
                <i class="fa-solid fa-check"></i> 일기장 완성하고 덮기 📕
            </button>
        </div>
    </div>
</div>

<!-- ============================================== -->
<!-- 모달 2: 일기장 공유 친구 관리 (Friend Invite) -->
<!-- ============================================== -->
<div id="friend-invite-modal" class="hidden fixed inset-0 z-[110] flex items-center justify-center p-4 backdrop-blur-sm bg-black/60 opacity-0 transition-opacity duration-300">
    <div class="bg-white w-full max-w-sm rounded-3xl overflow-hidden shadow-2xl transform scale-95 transition-transform duration-300">
        <div class="bg-brand-50 p-4 flex justify-between items-center border-b border-brand-100">
            <h3 class="font-black text-brand-800 text-sm"><i class="fa-solid fa-user-plus mr-1"></i> 교환 일기 친구 초대</h3>
            <button onclick="closeFriendInviteModal()" class="text-brand-400 hover:text-brand-600" aria-label="닫기"><i class="fa-solid fa-xmark"></i></button>
        </div>
        <div class="p-5 space-y-4">
            <p class="text-[11px] text-gray-500 font-medium leading-relaxed">
                친구에게 교환 일기 초대장을 보냅니다. 친구가 수락하면 일기장이 통합되어 서로의 추억을 한 타임라인에서 볼 수 있습니다! 🤝
            </p>
            
            <!-- 이메일 직접 초대 -->
            <div class="space-y-2 border-b border-gray-100 pb-4">
                <p class="text-[10px] font-bold text-gray-500">📧 이메일로 직접 초대</p>
                <div class="flex gap-2">
                    <input type="text" id="invite-friend-name" placeholder="닉네임" class="w-24 text-[11px] border border-gray-200 rounded-xl px-2.5 py-2 outline-none focus:border-brand-400">
                    <input type="email" id="invite-friend-email" placeholder="이메일 주소" class="flex-1 text-[11px] border border-gray-200 rounded-xl px-2.5 py-2 outline-none focus:border-brand-400">
                    <button onclick="sendFriendInviteByEmail(this)" class="flex-shrink-0 bg-brand-500 hover:bg-brand-600 text-white text-[10px] font-bold px-3 py-2 rounded-xl transition-colors shadow-sm">초대</button>
                </div>
            </div>

            <div class="space-y-2">
                <p class="text-[10px] font-bold text-gray-500">🤖 AI 에이전트 (예시)</p>
                <div class="flex justify-between items-center bg-gradient-to-r from-brand-50 to-brand-50 p-3 rounded-xl border border-brand-200">
                    <div class="flex items-center gap-2">
                        <div class="w-8 h-8 rounded-full bg-gradient-to-br from-brand-400 to-brand-500 flex items-center justify-center text-white text-xs font-black">🤖</div>
                        <div>
                            <span class="block text-xs font-black text-gray-800">펫케어 AI 도우미</span>
                            <span class="block text-[9px] text-gray-400">ai-helper@petna.co.kr</span>
                        </div>
                    </div>
                    <button onclick="sendFriendInvite('펫케어 AI 도우미', 'ai-helper@petna.co.kr', this)" class="bg-brand-500 text-white text-[10px] font-bold py-1.5 px-3 rounded-lg shadow-sm hover:bg-brand-600 transition-colors">초대하기</button>
                </div>

                <div class="flex justify-between items-center bg-gradient-to-r from-blue-50 to-cyan-50 p-3 rounded-xl border border-blue-200">
                    <div class="flex items-center gap-2">
                        <div class="w-8 h-8 rounded-full bg-gradient-to-br from-blue-400 to-cyan-500 flex items-center justify-center text-white text-xs font-black">🧠</div>
                        <div>
                            <span class="block text-xs font-black text-gray-800">건강 분석 전문가</span>
                            <span class="block text-[9px] text-gray-400">health-ai@petna.co.kr</span>
                        </div>
                    </div>
                    <button onclick="sendFriendInvite('건강 분석 전문가', 'health-ai@petna.co.kr', this)" class="bg-blue-500 text-white text-[10px] font-bold py-1.5 px-3 rounded-lg shadow-sm hover:bg-blue-600 transition-colors">초대하기</button>
                </div>
            </div>
            
        </div>
    </div>
</div>
`;
