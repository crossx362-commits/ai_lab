const SOCIAL_TEMPLATE = `
<div class="grid grid-cols-1 lg:grid-cols-4 gap-6 items-stretch">

    <!-- 1열: 왼쪽 패널 - 친구 검색, 대기 신청, 친구 리스트 (lg:col-span-1) -->
    <div class="lg:col-span-1 order-last lg:order-first bg-white rounded-3xl p-5 border border-amber-50 shadow-sm space-y-6">
        <!-- 친구 검색 & 신청 기능 -->
        <div>
            <h3 class="font-black text-gray-800 text-sm mb-3 flex items-center">
                <i class="fa-solid fa-user-plus text-brand-500 mr-2"></i>집사 검색
            </h3>
            <div class="flex space-x-2">
                <input type="text" id="friend-search-input" placeholder="닉네임 또는 펫 이름..."
                    class="flex-grow min-w-0 text-xs border border-gray-200 rounded-xl px-3 py-3 outline-none focus:border-brand-500 bg-gray-50/20">
                <button onclick="searchAndRequestFriend()"
                    class="bg-brand-500 hover:bg-brand-600 text-white font-black text-xs px-4 py-3 rounded-xl transition-colors shrink-0">찾기</button>
            </div>
        </div>

        <!-- 대기중인 친구 신청 정보 리스트 -->
        <div id="friend-requests-container" class="space-y-2">
            <h4 class="font-black text-gray-600 text-xs uppercase tracking-wider">받은 친구 신청 (<span id="req-count">1</span>)</h4>
            <div id="friend-requests-list" class="space-y-2">
                <!-- 동적 대기 신청인 로드 -->
            </div>
        </div>

        <!-- 집사 및 펫 리스트 (조화 지수 및 실시간 상태 마크 포함) -->
        <div class="space-y-3">
            <h4 class="font-black text-gray-600 text-xs uppercase tracking-wider">나의 이웃 집사들</h4>
            <div id="friends-list" class="space-y-2 max-h-[350px] overflow-y-auto no-scrollbar">
                <!-- 동적 친구들 주소록 로딩 -->
            </div>
        </div>
    </div>

    <!-- 2&3열: 중앙 패널 - 서브탭 조작계 + (피드 타임라인 OR DM 대화창) (lg:col-span-2) -->
    <div class="lg:col-span-2 space-y-4">
        <!-- 중앙 서브 네비게이션 탭 -->
        <div class="flex border border-amber-50 mb-2 bg-white p-1.5 rounded-2xl shadow-sm space-x-1.5 overflow-x-auto no-scrollbar whitespace-nowrap min-w-0 justify-start scroll-mask">
            <button onclick="switchSocialSubTab('feed')" id="social-subtab-feed-btn" 
                class="flex-grow flex-shrink-0 whitespace-nowrap flex items-center justify-center gap-1.5 py-3 px-4 rounded-xl text-xs font-black transition-all">
                <i class="fa-solid fa-signs-post text-sm"></i> 이웃 자랑 피드
            </button>
            <button onclick="switchSocialSubTab('chat')" id="social-subtab-chat-btn" 
                class="flex-grow flex-shrink-0 whitespace-nowrap flex items-center justify-center gap-1.5 py-3 px-4 rounded-xl text-xs font-black transition-all">
                <i class="fa-solid fa-comments text-sm"></i> 1:1 따뜻한 DM
            </button>
            <button onclick="switchSocialSubTab('mailbox')" id="social-subtab-mailbox-btn" 
                class="flex-grow flex-shrink-0 whitespace-nowrap flex items-center justify-center gap-1.5 py-3 px-4 rounded-xl text-xs font-black transition-all relative">
                <i class="fa-solid fa-envelope text-sm"></i> 우리 아이 우체통
                <span id="social-mailbox-unread-badge" class="absolute -top-1 -right-1 bg-red-500 text-white text-[8px] font-black w-4 h-4 rounded-full flex items-center justify-center hidden">0</span>
            </button>
        </div>

        <!-- 서브탭 1: 이웃 자랑 피드 컨텐츠 -->
        <div id="social-subtab-feed-content" class="space-y-6">
            <!-- 신규 피드 작성 박스 -->
            <div class="bg-white rounded-3xl p-5 border border-amber-50 shadow-sm space-y-4">
                <div class="flex items-center space-x-3">
                    <span class="text-xl">📢</span>
                    <h3 class="font-black text-gray-800 text-sm">오늘 있었던 특별한 펫 일상 자랑하기</h3>
                </div>
                <div class="flex justify-end mb-0.5">
                    <button type="button" onclick="generateSocialCaption(null)"
                        class="flex items-center gap-1 text-[11px] font-black text-violet-500 hover:text-violet-700 transition-colors outline-none">
                        <i class="fa-solid fa-wand-magic-sparkles text-[10px]"></i> AI 캡션 작성 ✍️
                    </button>
                </div>
                <textarea id="feed-input-content" rows="3"
                    placeholder="우리 아이의 이쁘고 귀여운 행동, 혹은 자랑하고 싶은 소식을 이웃들과 나눠보세요...!"
                    class="w-full text-xs border border-gray-200 rounded-2xl p-4 outline-none focus:border-brand-500 resize-none bg-gray-50/20"></textarea>

                <!-- 직접 사진 첨부 미리보기 영역 -->
                <div id="feed-photo-preview-container" class="hidden relative w-32 aspect-video bg-gray-100 rounded-xl overflow-hidden shadow-inner border border-gray-200 group">
                    <img loading="lazy" id="feed-photo-preview" src="" class="w-full h-full object-cover">
                    <div id="feed-photo-uploading-overlay" class="absolute inset-0 bg-black/50 flex flex-col items-center justify-center text-white text-[9px] font-bold space-y-1 hidden">
                        <i class="fa-solid fa-circle-notch animate-spin text-xs"></i>
                        <span>사진 업로드 중...</span>
                    </div>
                    <button onclick="clearAttachedPhoto()" class="absolute top-1 right-1 bg-black/60 hover:bg-black/80 text-white w-5 h-5 rounded-full flex items-center justify-center text-[10px] transition-colors"><i class="fa-solid fa-xmark"></i></button>
                </div>

                <!-- 동영상 구간 슬라이더 (트리머) -->
                <div id="feed-video-trimmer"
                    class="hidden bg-amber-50/50 p-4 rounded-2xl border border-amber-200/50 space-y-3">
                    <div class="flex justify-between items-center">
                        <span class="text-xs font-bold text-amber-800"><i class="fa-solid fa-scissors mr-1.5"></i> 비디오 트림 조절바</span>
                        <button onclick="clearAttachedVideo()" class="text-gray-400 hover:text-red-500 text-xs">
                            <i class="fa-solid fa-trash-can"></i> 삭제
                        </button>
                    </div>
                    <div class="grid grid-cols-2 gap-4 text-xs">
                        <div>
                            <label class="block text-[10px] text-gray-500 font-bold mb-1">시작 지점: <span id="feed-trim-start-label" class="text-brand-600 font-mono">0.0s</span></label>
                            <input type="range" id="feed-trim-start" min="0" max="10" step="0.5" value="0" oninput="updateFeedTrim()" class="w-full accent-brand-500">
                        </div>
                        <div>
                            <label class="block text-[10px] text-gray-500 font-bold mb-1">종료 지점: <span id="feed-trim-end-label" class="text-brand-600 font-mono">5.0s</span></label>
                            <input type="range" id="feed-trim-end" min="1" max="15" step="0.5" value="5" oninput="updateFeedTrim()" class="w-full accent-brand-500">
                        </div>
                    </div>
                    <!-- 구간 확인용 간이 프리뷰 -->
                    <div class="aspect-video bg-black rounded-xl overflow-hidden max-w-xs mx-auto">
                        <video id="feed-trim-preview" class="w-full h-full object-cover" muted playsinline></video>
                    </div>
                </div>

                <!-- 첨부 파일 인디케이터 배지들 -->
                <div class="flex flex-wrap gap-2 text-[10px] font-bold">
                    <span id="post-photo-indicator" class="hidden bg-emerald-50 text-emerald-700 px-3 py-1 rounded-full border border-emerald-200"><i class="fa-solid fa-image mr-1"></i> 이쁜 사진 장착됨</span>
                    <span id="post-video-indicator" class="hidden bg-amber-50 text-amber-700 px-3 py-1 rounded-full border border-amber-200"><i class="fa-solid fa-video mr-1"></i> 자랑 영상 연동됨</span>
                    <span id="post-walk-indicator" class="hidden bg-indigo-50 text-indigo-700 px-3 py-1 rounded-full border border-indigo-200"><i class="fa-solid fa-map-marked-alt mr-1"></i> 오늘 산책로 데이터 동봉됨</span>
                    <span id="post-health-indicator" class="hidden bg-teal-50 text-teal-700 px-3 py-1 rounded-full border border-teal-200"><i class="fa-solid fa-notes-medical mr-1"></i> 오늘의 건강 기록 연동됨</span>
                </div>

                <!-- 글쓰기 기능 버튼 모음 -->
                <div class="flex justify-between items-center pt-2 border-t border-gray-100">
                    <div class="flex flex-wrap gap-1.5">
                        <label for="feed-photo-upload" class="cursor-pointer bg-gray-50 hover:bg-brand-50 text-gray-600 hover:text-brand-600 font-bold text-xs py-3 px-3 rounded-xl transition-all flex items-center shadow-sm">
                            <i class="fa-solid fa-cloud-arrow-up mr-1 text-emerald-600"></i> 내 사진 올리기
                        </label>
                        <input type="file" id="feed-photo-upload" onchange="handleFeedPhotoUpload(event)" accept="image/*" class="hidden">

                        <button onclick="selectPostPresetPhoto()" class="bg-gray-50 hover:bg-brand-50 text-gray-600 hover:text-brand-600 font-bold text-xs py-3 px-3 rounded-xl transition-all flex items-center">
                            <i class="fa-solid fa-image mr-1 text-emerald-500"></i> 사진 프리셋
                        </button>
                        <button onclick="selectPostPresetVideo()" class="bg-gray-50 hover:bg-brand-50 text-gray-600 hover:text-brand-600 font-bold text-xs py-3 px-3 rounded-xl transition-all flex items-center">
                            <i class="fa-solid fa-video mr-1 text-amber-500"></i> 영상 프리셋
                        </button>
                        <button onclick="openAttachWalkModal()" class="bg-gray-50 hover:bg-brand-50 text-gray-600 hover:text-brand-600 font-bold text-xs py-3 px-3 rounded-xl transition-all flex items-center">
                            <i class="fa-solid fa-route mr-1 text-indigo-500"></i> 이동 첨부
                        </button>
                        <button onclick="toggleAttachHealthLog()" class="bg-gray-50 hover:bg-brand-50 text-gray-600 hover:text-brand-600 font-bold text-xs py-3 px-3 rounded-xl transition-all flex items-center">
                            <i class="fa-solid fa-notes-medical mr-1 text-teal-500"></i> 건강 기록 첨부
                        </button>
                        <button onclick="insertHashtags()" class="bg-gray-50 hover:bg-violet-50 text-gray-600 hover:text-violet-600 font-bold text-xs py-3 px-3 rounded-xl transition-all flex items-center">
                            <i class="fa-solid fa-hashtag mr-1 text-violet-400"></i> 해시태그
                        </button>
                    </div>
                    <button onclick="submitFeedPost()" class="bg-brand-500 hover:bg-brand-600 text-white font-black text-xs py-3 px-6 rounded-xl shadow-md transition-colors">
                        자랑 발행 🚀
                    </button>
                </div>
            </div>

            <!-- 타임라인 피드 리스트 -->
            <div id="feed-list" class="space-y-6">
                <!-- 동적 피드 카드가 JS를 통해 빌드됨 -->
            </div>
        </div>

        <!-- 서브탭 2: 1:1 따뜻한 DM 대화창 컨텐츠 -->
        <div id="social-subtab-chat-content" class="hidden bg-white rounded-3xl border border-amber-50 shadow-sm flex flex-col h-[600px] overflow-hidden">
            <!-- 대화창 파트너 프로필 헤더 -->
            <div id="chat-header" class="bg-brand-50/30 p-4 border-b border-gray-100 flex items-center justify-between shrink-0">
                <!-- 동적 상대방 상세 정보 로드 -->
            </div>

            <!-- 대화 메시지 목록 (스크롤) -->
            <div id="chat-messages" class="flex-grow p-4 overflow-y-auto space-y-4 bg-gray-50/40 no-scrollbar">
                <!-- 동적 챗 버블 렌더링 -->
            </div>

            <!-- 지능형 대화 작성 표시기 (Typing Indicator) -->
            <div id="chat-typing-indicator" class="px-4 py-2 text-[10px] text-gray-400 font-bold italic hidden bg-gray-50/40 shrink-0">
                <span id="typing-friend-name">상대방</span> 집사가 대답을 입력하는 중... 🐾
            </div>

            <!-- 메시지 전송 및 안심 데이터 첨부 조작계 -->
            <div class="p-4 border-t border-gray-100 space-y-3 bg-white shrink-0">
                <!-- 빠른 첨부 옵션 트레이 -->
                <div class="flex space-x-2">
                    <button onclick="shareWalkToActiveChat()" class="text-[10px] bg-indigo-50 hover:bg-indigo-100 text-indigo-700 font-bold py-1.5 px-3 rounded-xl transition-colors">
                        <i class="fa-solid fa-map-location-dot mr-1"></i> 산책로 지도 첨부
                    </button>
                    <button onclick="shareAlbumToActiveChat()" class="text-[10px] bg-pink-50 hover:bg-pink-100 text-pink-700 font-bold py-1.5 px-3 rounded-xl transition-colors">
                        <i class="fa-solid fa-wand-magic-sparkles mr-1"></i> 데코 앨범 첨부
                    </button>
                </div>

                <!-- 텍스트 입력창 -->
                <div class="flex items-center space-x-2">
                    <input type="text" id="chat-input-message" onkeypress="handleChatEnter(event)" placeholder="따뜻한 다이렉트 메시지를 남겨보세요..."
                        class="flex-grow border border-gray-200 rounded-xl px-4 py-2.5 text-xs outline-none focus:border-brand-500 bg-gray-50/20">
                    <button onclick="sendChatMessage()" class="bg-brand-500 hover:bg-brand-600 text-white font-black text-xs py-2.5 px-5 rounded-xl shadow transition-colors shrink-0">
                        전송 🚀
                    </button>
                </div>
            </div>
        </div>

        <!-- 서브탭 3: 우리 아이 우체통 컨텐츠 -->
        <div id="social-subtab-mailbox-content" class="hidden space-y-4">
            <div class="flex justify-between items-center bg-white rounded-3xl p-5 border border-amber-50 shadow-sm">
                <div>
                    <h3 class="text-sm font-black text-gray-800 flex items-center gap-1.5">
                        <i class="fa-solid fa-envelope text-brand-500"></i> 우리 아이 우체통 📮
                    </h3>
                    <p class="text-xs text-gray-500 font-bold mt-0.5">집사들이 보낸 따뜻한 마음이 도착했어요.</p>
                </div>
                <div class="flex items-center space-x-2">
                    <!-- 휴지통 비우기 버튼 -->
                    <button id="empty-trash-btn" onclick="emptyTrash()" class="hidden px-3.5 py-2 bg-rose-50 hover:bg-rose-100 text-rose-600 rounded-xl text-xs font-bold transition-all flex items-center gap-1">
                        <i class="fa-solid fa-trash-can"></i> 비우기
                    </button>
                    <button onclick="openWriteLetterModal()" class="w-10 h-10 bg-brand-500 text-white rounded-full shadow-lg flex items-center justify-center hover:scale-110 transition-transform">
                        <i class="fa-solid fa-pen-nib"></i>
                    </button>
                </div>
            </div>
            
            <!-- 편지함 폴더 탭 영역 -->
            <div class="flex border-b border-gray-100 bg-gray-50/50 p-1.5 rounded-2xl gap-1">
                <button onclick="switchMailboxFolder('inbox')" id="mailbox-folder-inbox" class="flex-1 py-2.5 text-center text-[11px] font-black rounded-xl transition-all text-brand-600 bg-white shadow-sm flex items-center justify-center gap-1 outline-none">
                    <span>📥</span> 받은 편지함
                    <span id="mailbox-inbox-badge" class="bg-brand-500 text-white text-[9px] px-1.5 py-0.5 rounded-full font-mono font-black min-w-[18px] text-center hidden">0</span>
                </button>
                <button onclick="switchMailboxFolder('sent')" id="mailbox-folder-sent" class="flex-1 py-2.5 text-center text-[11px] font-black rounded-xl transition-all text-gray-400 hover:text-gray-600 flex items-center justify-center gap-1 outline-none">
                    <span>📤</span> 보낸 편지함
                    <span id="mailbox-sent-badge" class="bg-gray-200 text-gray-600 text-[9px] px-1.5 py-0.5 rounded-full font-mono font-black min-w-[18px] text-center hidden">0</span>
                </button>
                <button onclick="switchMailboxFolder('trash')" id="mailbox-folder-trash" class="flex-1 py-2.5 text-center text-[11px] font-black rounded-xl transition-all text-gray-400 hover:text-gray-600 flex items-center justify-center gap-1 outline-none">
                    <span>🗑️</span> 휴지통
                    <span id="mailbox-trash-badge" class="bg-gray-200 text-gray-600 text-[9px] px-1.5 py-0.5 rounded-full font-mono font-black min-w-[18px] text-center hidden">0</span>
                </button>
            </div>

            <div id="letter-list-container" class="bg-white rounded-3xl border border-amber-50 shadow-sm overflow-hidden min-h-[300px]">
                <div id="letter-list-body" class="divide-y divide-gray-50"></div>
            </div>
        </div>
    </div>

    <!-- 4열: 오른쪽 패널 - 동네 핫플레이스 + 나를 좋아하는 이웃 집사 알림 (lg:col-span-1) -->
    <div class="lg:col-span-1 space-y-6">
        <!-- 동네 인기 급상승 플레이스 -->
        <div class="bg-white rounded-3xl p-5 border border-amber-50 shadow-sm space-y-4">
            <h4 class="font-black text-gray-800 text-sm flex items-center">
                <i class="fa-solid fa-fire text-amber-500 mr-2"></i> 동네 인기 급상승 플레이스 🔥
            </h4>
            <div class="space-y-3 text-xs">
                <div class="flex justify-between items-center p-2 hover:bg-brand-50 rounded-xl transition-colors cursor-pointer" onclick="flyToPlace(37.3945, 126.6380, '송도 센트럴파크 반려견 운동장')">
                    <span class="text-xl shrink-0">🌳</span>
                    <div class="flex-grow min-w-0 pl-3">
                        <span class="block font-bold text-gray-700">송도 센트럴파크 반려견 운동장</span>
                        <span class="text-xs text-gray-500">대형견 분리형 야외 천연 잔디밭</span>
                    </div>
                    <span class="text-amber-500 font-bold">⭐ 4.9</span>
                </div>
                <div class="flex justify-between items-center p-2 hover:bg-brand-50 rounded-xl transition-colors cursor-pointer" onclick="flyToPlace(37.3915, 126.6432, '카페 멍멍랜드')">
                    <span class="text-xl shrink-0">☕</span>
                    <div class="flex-grow min-w-0 pl-3">
                        <span class="block font-bold text-gray-700">카페 멍멍랜드 (애견 브런치)</span>
                        <span class="text-xs text-gray-500">무염 락토프리 댕푸치노 명소</span>
                    </div>
                    <span class="text-amber-500 font-bold">⭐ 4.7</span>
                </div>
            </div>
        </div>

        <!-- 나를 좋아하는 이웃 집사 (하트/댓글 실시간 로그) -->
        <div class="bg-white rounded-3xl p-5 border border-amber-50 shadow-sm space-y-4">
            <h4 class="font-black text-gray-800 text-sm flex items-center justify-between border-b pb-2">
                <span class="flex items-center">
                    <i class="fa-solid fa-heart-circle-check text-rose-500 mr-2"></i>나를 좋아하는 이웃 집사 💌
                </span>
                <span class="bg-rose-100 text-rose-600 font-bold text-[9px] px-2 py-0.5 rounded-full" id="like-notify-count">4</span>
            </h4>
            <div id="like-notification-list" class="space-y-3.5">
                <!-- 동적으로 좋아요 알림이 박진감 넘치게 주입됨 -->
            </div>
        </div>
    </div>
</div>
`;
