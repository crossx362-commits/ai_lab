const MODALS_TEMPLATE = `
<!-- 다목적 모달 레이어들 -->

${typeof MONTHLY_REPORT_MODAL !== 'undefined' ? MONTHLY_REPORT_MODAL : ''}

<!-- 후원 결제 모달 -->
<div id="donation-payment-modal" class="fixed inset-0 bg-black/60 items-center justify-center z-[110] p-4 hidden">
    <div class="bg-white rounded-3xl p-5 sm:p-6 max-w-sm w-full shadow-2xl relative border border-amber-100 max-h-[90vh] overflow-y-auto no-scrollbar">
        <button onclick="closeDonationModal()" class="absolute top-4 right-4 text-gray-400 hover:text-gray-600 outline-none" aria-label="닫기"><i class="fa-solid fa-xmark text-lg"></i></button>
        <div class="text-center space-y-3 mb-6">
            <div class="w-14 h-14 bg-rose-50 rounded-full flex items-center justify-center text-3xl mx-auto shadow-inner">💝</div>
            <h4 class="font-black text-gray-800 text-base">펫과나 동행 후원하기</h4>
            <p class="text-[11px] text-gray-400">선택하신 후원금은 펫과나 고도화에 전액 사용됩니다.</p>
            <div class="bg-amber-50/50 rounded-2xl p-3 inline-block border border-amber-100/50">
                <span class="text-xs text-gray-500 font-bold block">최종 후원금액</span>
                <span id="donation-modal-amount" class="text-xl font-black text-rose-500 font-mono">0원</span>
            </div>
        </div>
        
        <!-- 결제 수단 선택 -->
        <div class="space-y-3 text-xs mb-6">
            <span class="block font-bold text-gray-400 text-[10px] uppercase">간편 결제수단 선택</span>
            <div class="grid grid-cols-2 gap-2">
                <button id="donation-method-kakao" onclick="selectDonationMethod('kakao', this)" class="donation-method-btn flex items-center justify-center gap-1.5 py-3 rounded-xl border border-amber-100 bg-white text-gray-700 font-bold hover:bg-amber-50/30 transition-all outline-none">
                    <span class="text-sm">💛</span> 카카오페이
                </button>
                <button id="donation-method-toss" onclick="selectDonationMethod('toss', this)" class="donation-method-btn flex items-center justify-center gap-1.5 py-3 rounded-xl border border-amber-100 bg-white text-gray-700 font-bold hover:bg-amber-50/30 transition-all outline-none">
                    <span class="text-sm">💙</span> 토스페이
                </button>
                <button id="donation-method-naver" onclick="selectDonationMethod('naver', this)" class="donation-method-btn flex items-center justify-center gap-1.5 py-3 rounded-xl border border-amber-100 bg-white text-gray-700 font-bold hover:bg-amber-50/30 transition-all outline-none">
                    <span class="text-sm">💚</span> 네이버페이
                </button>
                <button id="donation-method-paypal" onclick="selectDonationMethod('paypal', this)" class="donation-method-btn flex items-center justify-center gap-1.5 py-3 rounded-xl border border-amber-100 bg-white text-gray-700 font-bold hover:bg-amber-50/30 transition-all outline-none">
                    <span class="text-sm">💳</span> 신용카드
                </button>
            </div>
        </div>

        <button onclick="confirmDonation()" class="w-full bg-rose-500 hover:bg-rose-600 text-white font-bold py-3.5 rounded-2xl text-xs shadow-md transition-colors flex items-center justify-center gap-2 outline-none">
            <i class="fa-solid fa-gift"></i>따뜻한 후원 결제 완료하기
        </button>
    </div>
</div>

<!-- 1:1 문의 접수 모달 -->
<div id="inquiry-write-modal" class="fixed inset-0 bg-black/60 items-center justify-center z-[110] p-4 hidden">
    <div class="bg-white rounded-3xl p-5 max-w-md w-full shadow-2xl relative border border-amber-100 max-h-[90vh] overflow-y-auto no-scrollbar">
        <button onclick="closeInquiryWriteModal()" class="absolute top-4 right-4 text-gray-400 hover:text-gray-600 outline-none" aria-label="닫기"><i class="fa-solid fa-xmark text-lg"></i></button>
        <h4 class="font-black text-gray-800 text-sm flex items-center border-b pb-3 mb-4">
            <i class="fa-solid fa-pen-nib text-brand-500 mr-2"></i>1:1 안심 신문고 문의 접수
        </h4>
        
        <div class="space-y-4 text-xs">
            <div>
                <label class="block font-bold text-gray-600 mb-1">문의 분류</label>
                <select id="inquiry-write-category" class="w-full border rounded-lg p-2.5 outline-none focus:border-brand-500 bg-gray-50/20">
                    <option value="사주 성향">🔮 사주 성향 / 운세</option>
                    <option value="지능 테스트">🐕 지능 테스트</option>
                    <option value="산책 루트">🗺️ 산책 루트</option>
                    <option value="데코 앨범">📸 데코 앨범</option>
                    <option value="기타 문의">💬 기타 문의</option>
                </select>
            </div>
            <div>
                <label class="block font-bold text-gray-600 mb-1">문의 제목</label>
                <input type="text" id="inquiry-write-title" placeholder="제목을 입력하세요" class="w-full border rounded-lg p-2.5 outline-none focus:border-brand-500 bg-gray-50/20">
            </div>
            <div>
                <label class="block font-bold text-gray-600 mb-1">문의 내용</label>
                <textarea id="inquiry-write-content" rows="5" placeholder="궁금하신 사항을 상세히 남겨주시면 펫과나 동행단이 신속하게 답변해 드립니다." class="w-full border rounded-lg p-2.5 outline-none focus:border-brand-500 bg-gray-50/20 resize-none"></textarea>
            </div>
            
            <button onclick="submitInquiry()" class="w-full bg-brand-500 hover:bg-brand-600 text-white font-bold py-3 rounded-2xl text-xs shadow-md transition-colors flex items-center justify-center gap-1.5 outline-none">
                <i class="fa-solid fa-paper-plane"></i>문의 사항 정식 등록하기
            </button>
        </div>
    </div>
</div>

<!-- 1:1 문의 상세 보기 모달 -->
<div id="inquiry-detail-modal" class="fixed inset-0 bg-black/60 items-center justify-center z-[110] p-4 hidden">
    <div class="bg-white rounded-3xl p-5 max-w-md w-full shadow-2xl relative border border-amber-100 max-h-[90vh] overflow-y-auto no-scrollbar">
        <button onclick="closeInquiryDetailModal()" class="absolute top-4 right-4 text-gray-400 hover:text-gray-600 outline-none" aria-label="닫기"><i class="fa-solid fa-xmark text-lg"></i></button>
        <h4 class="font-black text-gray-800 text-sm flex items-center border-b pb-3 mb-4">
            <i class="fa-solid fa-clipboard-question text-brand-500 mr-2"></i>문의 상세 내역 및 답변
        </h4>
        
        <div class="space-y-4 text-xs">
            <!-- 질문 내용 -->
            <div class="bg-amber-50/30 border border-amber-100/50 rounded-2xl p-4 space-y-2">
                <div class="flex items-center justify-between">
                    <span id="inquiry-detail-category" class="bg-brand-500 text-white font-bold text-[8px] py-0.5 px-2 rounded-full">카테고리</span>
                    <span id="inquiry-detail-date" class="text-[9px] text-gray-400 font-mono">2026-05-18</span>
                </div>
                <h5 id="inquiry-detail-title" class="font-bold text-gray-800 text-sm">문의 제목 자리</h5>
                <p id="inquiry-detail-content" class="text-gray-600 leading-relaxed whitespace-pre-line bg-white/50 p-2.5 rounded-lg border border-gray-100">문의 내용 상세가 표시되는 공간입니다.</p>
            </div>
            
            <!-- 답변 내용 -->
            <div class="space-y-2">
                <span class="block font-bold text-gray-700 flex items-center gap-1">
                    <i class="fa-solid fa-reply-all text-brand-500"></i>펫과나 안심 케어팀 답변
                </span>
                <div id="inquiry-detail-reply-box" class="bg-brand-50/40 border border-brand-100/50 rounded-2xl p-4">
                    <!-- 답변 상태에 따라 로딩바 또는 답변 표시 -->
                </div>
            </div>
        </div>
    </div>
</div>

<!-- 공통 안심 알림 및 컨펌용 프리미엄 로컬 모달 -->
<div id="custom-dialog-modal" class="fixed inset-0 bg-black/60 items-center justify-center z-[110] p-4 hidden">
    <div class="bg-white rounded-2xl p-5 sm:p-6 max-w-sm w-full text-center shadow-2xl relative max-h-[90vh] overflow-y-auto no-scrollbar">
        <div id="dialog-icon-container"
            class="w-14 h-14 bg-amber-50 rounded-full flex items-center justify-center text-2xl mx-auto mb-3">
            🐾
        </div>
        <h4 id="dialog-title" class="font-bold text-base text-gray-800 mb-1">펫과나 알림</h4>
        <p id="dialog-message" class="text-xs text-gray-500 mb-5 leading-relaxed">내용 안내문입니다.</p>
        <div class="flex space-x-2" id="dialog-actions">
            <!-- 동적으로 버튼 구성 -->
        </div>
    </div>
</div>

<!-- 1. 글작성 모달 (사진 프리셋) -->
<div id="feed-photo-modal" class="fixed inset-0 bg-black/60 items-center justify-center z-50 p-4 hidden">
    <div class="bg-white rounded-2xl p-5 max-w-md w-full shadow-xl max-h-[90vh] overflow-y-auto no-scrollbar">
        <div class="flex justify-between items-center mb-4">
            <h4 class="font-bold text-sm text-gray-800">이쁜 자랑 배경 선택</h4>
            <button onclick="closePostPresetPhotoModal()" class="text-gray-400 hover:text-gray-600"><i
                    class="fa-solid fa-xmark"></i></button>
        </div>
        <div class="grid grid-cols-2 gap-3 mb-4">
            <div class="cursor-pointer border-2 border-transparent hover:border-brand-500 rounded-xl overflow-hidden"
                onclick="confirmPostPhoto('https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=400')">
                <img loading="lazy" src="https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=200"
                    class="w-full h-24 object-cover">
            </div>
            <div class="cursor-pointer border-2 border-transparent hover:border-brand-500 rounded-xl overflow-hidden"
                onclick="confirmPostPhoto('https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba?auto=format&fit=crop&q=80&w=400')">
                <img loading="lazy" src="https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba?auto=format&fit=crop&q=80&w=200"
                    class="w-full h-24 object-cover">
            </div>
            <div class="cursor-pointer border-2 border-transparent hover:border-brand-500 rounded-xl overflow-hidden"
                onclick="confirmPostPhoto('https://images.unsplash.com/photo-1535268647977-a403b69fc756?auto=format&fit=crop&q=80&w=400')">
                <img loading="lazy" src="https://images.unsplash.com/photo-1535268647977-a403b69fc756?auto=format&fit=crop&q=80&w=200"
                    class="w-full h-24 object-cover">
            </div>
            <div class="cursor-pointer border-2 border-transparent hover:border-brand-500 rounded-xl overflow-hidden"
                onclick="confirmPostPhoto('https://images.unsplash.com/photo-1583511655857-d19b40a7a54e?auto=format&fit=crop&q=80&w=400')">
                <img loading="lazy" src="https://images.unsplash.com/photo-1583511655857-d19b40a7a54e?auto=format&fit=crop&q=80&w=200"
                    class="w-full h-24 object-cover">
            </div>
        </div>
        <p class="text-[10px] text-gray-400 text-center">자랑하고 싶은 최애 댕댕이/냥이 프리셋 사진 중 하나를 골라 공유해 보세요!</p>
    </div>
</div>

<!-- 1-2. 자랑피드 비디오 프리셋 선택 모달 -->
<div id="feed-video-modal" class="fixed inset-0 bg-black/60 items-center justify-center z-50 p-4 hidden">
    <div class="bg-white rounded-2xl p-5 max-w-sm w-full shadow-xl max-h-[90vh] overflow-y-auto no-scrollbar">
        <div class="flex justify-between items-center mb-3">
            <h4 class="font-bold text-sm text-gray-800">이쁜 자랑 영상 선택</h4>
            <button onclick="closePostPresetVideoModal()" class="text-gray-400 hover:text-gray-600"><i
                    class="fa-solid fa-xmark"></i></button>
        </div>
        <div class="space-y-2.5 mb-3">
            <button
                onclick="confirmPostVideo('https://assets.mixkit.co/videos/preview/mixkit-dog-running-on-the-beach-41712-large.mp4')"
                class="w-full p-2 text-left hover:bg-amber-50 border rounded-xl flex items-center space-x-3 transition-colors">
                <div class="w-14 h-10 rounded bg-gray-900 overflow-hidden shrink-0"><video
                        src="https://assets.mixkit.co/videos/preview/mixkit-dog-running-on-the-beach-41712-large.mp4"
                        class="w-full h-full object-cover" muted></video></div>
                <span class="text-xs font-bold text-gray-700">🏃 해변을 달리는 강아지</span>
            </button>
            <button
                onclick="confirmPostVideo('https://assets.mixkit.co/videos/preview/mixkit-playful-cat-lying-on-a-yellow-sofa-42354-large.mp4')"
                class="w-full p-2 text-left hover:bg-amber-50 border rounded-xl flex items-center space-x-3 transition-colors">
                <div class="w-14 h-10 rounded bg-gray-900 overflow-hidden shrink-0"><video
                        src="https://assets.mixkit.co/videos/preview/mixkit-playful-cat-lying-on-a-yellow-sofa-42354-large.mp4"
                        class="w-full h-full object-cover" muted></video></div>
                <span class="text-xs font-bold text-gray-700">🐱 옐로 소파 장난끼 고양이</span>
            </button>
        </div>
        <div class="border-t pt-3">
            <label class="block text-[11px] font-bold text-gray-400 mb-1.5">내 기기에서 직접 동영상 업로드</label>
            <input type="file" id="feed-custom-video-input" onchange="uploadCustomFeedVideo(event)" accept="video/*"
                class="text-xs block w-full file:mr-2 file:py-1 file:px-3 file:rounded-md file:border-0 file:text-xs file:font-bold file:bg-brand-100 file:text-brand-700 hover:file:bg-brand-200">
        </div>
    </div>
</div>

<!-- 2. 반려동물 등록 및 수정 모달 -->
<div id="pet-reg-modal" class="fixed inset-0 bg-black/60 items-center justify-center z-50 p-4 hidden">
    <div class="bg-white rounded-2xl p-5 sm:p-6 max-w-sm w-full shadow-xl max-h-[90vh] overflow-y-auto no-scrollbar">
        <div class="flex justify-between items-center mb-4 pb-2 border-b border-gray-100">
            <h4 class="font-bold text-gray-800 text-sm">반려동물 입양 등록</h4>
            <button onclick="closePetRegistrationModal()" class="text-gray-400 hover:text-gray-600"><i
                    class="fa-solid fa-xmark"></i></button>
        </div>
        <div class="space-y-3 text-xs text-gray-700">
            <div>
                <label class="block font-bold mb-1">펫 이름</label>
                <input type="text" id="reg-pet-name" placeholder="예: 초코, 체리"
                    class="w-full border border-gray-200 rounded-lg p-2 outline-none focus:border-brand-500">
            </div>
            <div>
                <label class="block font-bold mb-1">펫 종류 / 이미지 방식 선택</label>
                <select id="reg-pet-type" onchange="adjustPetTypeSelection()"
                    class="w-full border border-gray-200 rounded-lg p-2 outline-none focus:border-brand-500">
                    <option value="dog">🐕 강아지 일러스트 아바타</option>
                    <option value="cat">🐈 고양이 일러스트 아바타</option>
                    <option value="rabbit">🐰 토끼 일러스트 아바타</option>
                    <option value="hamster">🐹 햄스터 일러스트 아바타</option>
                    <option value="custom">🖼️ 직접 사진 / 이미지 등록</option>
                </select>
            </div>

            <!-- 커스텀 이미지 등록 옵션 레이어 -->
            <div id="custom-image-upload-group"
                class="hidden p-3 bg-brand-50/50 rounded-xl border border-brand-100/50 space-y-2.5">
                <div>
                    <span class="block text-[10px] font-bold text-gray-500 mb-1">내 기기에서 사진 올리기</span>
                    <div
                        class="relative border border-dashed border-gray-200 hover:border-brand-300 rounded-lg p-2 text-center cursor-pointer bg-white transition-all">
                        <input type="file" id="reg-pet-photo-file" onchange="uploadPetProfileImage(event)"
                            accept="image/*" class="absolute inset-0 opacity-0 cursor-pointer">
                        <i class="fa-solid fa-camera text-brand-500 text-xs mb-0.5"></i>
                        <span class="block text-[9px] font-bold text-gray-500">사진 파일 찾기</span>
                    </div>
                </div>
                <div>
                    <span class="block text-[10px] font-bold text-gray-500 mb-1">이미지 웹 URL 직접 연결</span>
                    <input type="text" id="reg-pet-photo-url" placeholder="https://... 이미지 경로 입력"
                        class="w-full border border-gray-200 rounded-lg p-2 text-[10px] outline-none bg-white">
                </div>
                <div>
                    <span class="block text-[9px] font-bold text-gray-400 mb-1">인기 고해상도 펫 프리셋 사진 선택</span>
                    <div class="grid grid-cols-4 gap-1.5">
                        <img loading="lazy" onclick="selectRegPresetPhoto('https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=300')"
                            src="https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=100"
                            class="h-8 w-full object-cover rounded-md cursor-pointer border border-transparent hover:border-brand-500 shadow-sm"
                            title="골든 리트리버">
                        <img loading="lazy" onclick="selectRegPresetPhoto('https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba?auto=format&fit=crop&q=80&w=300')"
                            src="https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba?auto=format&fit=crop&q=80&w=100"
                            class="h-8 w-full object-cover rounded-md cursor-pointer border border-transparent hover:border-brand-500 shadow-sm"
                            title="샴 고양이">
                        <img loading="lazy" onclick="selectRegPresetPhoto('https://images.unsplash.com/photo-1583511655857-d19b40a7a54e?auto=format&fit=crop&q=80&w=300')"
                            src="https://images.unsplash.com/photo-1583511655857-d19b40a7a54e?auto=format&fit=crop&q=80&w=100"
                            class="h-8 w-full object-cover rounded-md cursor-pointer border border-transparent hover:border-brand-500 shadow-sm"
                            title="프렌치 불독">
                        <img loading="lazy" onclick="selectRegPresetPhoto('https://images.unsplash.com/photo-1535268647977-a403b69fc756?auto=format&fit=crop&q=80&w=300')"
                            src="https://images.unsplash.com/photo-1535268647977-a403b69fc756?auto=format&fit=crop&q=80&w=100"
                            class="h-8 w-full object-cover rounded-md cursor-pointer border border-transparent hover:border-brand-500 shadow-sm"
                            title="아기 토끼">
                    </div>
                </div>
            </div>

            <div>
                <label class="block font-bold mb-1">상세 품종</label>
                <input type="text" id="reg-pet-breed" placeholder="예: 시바견, 러시안블루"
                    class="w-full border border-gray-200 rounded-lg p-2 outline-none focus:border-brand-500">
            </div>
            <div class="grid grid-cols-2 gap-2">
                <div>
                    <label class="block font-bold mb-1">나이</label>
                    <input type="text" id="reg-pet-age" placeholder="예: 3살"
                        class="w-full border border-gray-200 rounded-lg p-2 outline-none focus:border-brand-500">
                </div>
                <div>
                    <label class="block font-bold mb-1">몸무게 (kg)</label>
                    <input type="text" id="reg-pet-weight" placeholder="예: 4.5"
                        class="w-full border border-gray-200 rounded-lg p-2 outline-none focus:border-brand-500">
                </div>
            </div>
            <div>
                <label class="block font-bold mb-1">성별</label>
                <select id="reg-pet-gender"
                    class="w-full border border-gray-200 rounded-lg p-2 outline-none focus:border-brand-500">
                    <option value="남아 (중성화 완료)">남아 (중성화 완료)</option>
                    <option value="여아 (중성화 완료)">여아 (중성화 완료)</option>
                    <option value="남아">남아</option>
                    <option value="여아">여아</option>
                </select>
            </div>
            <div>
                <label class="block font-bold mb-1">성격 / 가치관</label>
                <input type="text" id="reg-pet-personality" placeholder="예: 산책과 간식을 지향함"
                    class="w-full border border-gray-200 rounded-lg p-2 outline-none focus:border-brand-500">
            </div>
        </div>
        <div class="mt-5 flex space-x-2 text-xs">
            <button onclick="closePetRegistrationModal()"
                class="w-1/2 bg-gray-100 hover:bg-gray-200 text-gray-700 font-bold py-2.5 rounded-xl">취소</button>
            <button onclick="submitPetRegistration()"
                class="w-1/2 bg-brand-500 hover:bg-brand-600 text-white font-bold py-2.5 rounded-xl shadow-md">양육
                등록</button>
        </div>
    </div>
</div>

<!-- 3. 일정 등록 모달 -->
<div id="schedule-modal" class="fixed inset-0 bg-black/60 items-center justify-center z-50 p-4 hidden">
    <div class="bg-white rounded-2xl p-5 max-w-sm w-full shadow-xl max-h-[90vh] overflow-y-auto no-scrollbar">
        <div class="flex justify-between items-center mb-3.5 pb-2 border-b border-gray-100">
            <h4 class="font-bold text-gray-800 text-sm">새로운 돌봄 일정 설정</h4>
            <button onclick="closeScheduleModal()" class="text-gray-400 hover:text-gray-600"><i
                    class="fa-solid fa-xmark"></i></button>
        </div>
        <div class="space-y-3.5 text-xs text-gray-700">
            <div>
                <label class="block font-bold mb-1">목표 돌봄행위 (일정 이름)</label>
                <input type="text" id="schedule-title" placeholder="예: 광견병 예방접종, 미용실 방문"
                    class="w-full border border-gray-200 rounded-lg p-2 outline-none focus:border-brand-500">
            </div>
            <div class="grid grid-cols-2 gap-2">
                <div>
                    <label class="block font-bold mb-1">일정 날짜</label>
                    <input type="date" id="schedule-date"
                        class="w-full border border-gray-200 rounded-lg p-2 outline-none focus:border-brand-500">
                </div>
                <div>
                    <label class="block font-bold mb-1">구분 꼬리표</label>
                    <select id="schedule-type"
                        class="w-full border border-gray-200 rounded-lg p-2 outline-none focus:border-brand-500">
                        <option value="vet">🏥 예방/동물병원</option>
                        <option value="groom">✂️ 미용/스파</option>
                        <option value="walk">🦮 산책/미팅</option>
                        <option value="etc">🎁 기타/생일</option>
                    </select>
                </div>
            </div>
        </div>
        <div class="mt-5 flex space-x-2 text-xs">
            <button onclick="closeScheduleModal()"
                class="w-1/2 bg-gray-100 hover:bg-gray-200 text-gray-700 font-bold py-2.5 rounded-xl">취소</button>
            <button onclick="submitSchedule()"
                class="w-1/2 bg-brand-500 hover:bg-brand-600 text-white font-bold py-2.5 rounded-xl shadow-md">계획
                수립</button>
        </div>
    </div>
</div>

<!-- 피드 글쓰기용 산책 기록 선택 모달 -->
<div id="feed-walk-attach-modal" class="fixed inset-0 bg-black/60 items-center justify-center z-50 p-4 hidden">
    <div class="bg-white rounded-2xl p-5 max-w-sm w-full shadow-xl max-h-[90vh] overflow-y-auto no-scrollbar">
        <div class="flex justify-between items-center mb-4 pb-2 border-b border-gray-100">
            <h4 class="font-bold text-gray-800 text-sm flex items-center">
                <i class="fa-solid fa-route text-brand-500 mr-2"></i>기록된 산책로/이동 첨부
            </h4>
            <button onclick="closeAttachWalkModal()" class="text-gray-400 hover:text-gray-600"><i
                    class="fa-solid fa-xmark"></i></button>
        </div>
        <div id="attach-walk-list" class="space-y-2 max-h-60 overflow-y-auto no-scrollbar">
            <!-- 사용자의 실제 완료된 산책 리스트 동적 로드 -->
        </div>
        <p class="text-[10px] text-gray-400 text-center mt-3">기록을 연동하면 친구들이 내 동선을 직접 지도에서 확인할 수 있습니다.</p>
    </div>
</div>

<!-- 우하단 간편 미세 토스트 메시지 알림기 -->
<div id="toast-message"
    class="fixed bottom-6 right-6 bg-gray-900/90 text-white text-xs font-bold px-4 py-3 rounded-xl shadow-lg z-[200] opacity-0 transition-opacity duration-300 pointer-events-none flex items-center space-x-2">
    <span>🐾</span> <span id="toast-text">안내문구입니다.</span>
</div>

<!-- 이웃집사 상세 정보 모달 (글라스모피즘 스타일) -->
<div id="neighbor-profile-modal" class="fixed inset-0 bg-black/60 items-center justify-center z-[120] p-4 hidden">
    <div class="bg-white rounded-3xl p-5 sm:p-6 max-w-sm w-full shadow-2xl relative border border-amber-100/60 overflow-y-auto no-scrollbar max-h-[90vh]">
        <!-- 닫기 버튼 -->
        <button onclick="closeNeighborProfileModal()" class="absolute top-4 right-4 text-gray-400 hover:text-gray-600 outline-none transition-colors" aria-label="닫기"><i class="fa-solid fa-xmark text-lg"></i></button>
        
        <!-- 프로필 카드 헤더 / 아바타 및 이름 -->
        <div class="text-center space-y-4 mb-6">
            <div class="relative inline-block">
                <img loading="lazy" id="neighbor-avatar" class="w-24 h-24 object-cover rounded-full mx-auto border-4 border-brand-100 shadow-md">
                <span id="neighbor-status-badge" class="absolute bottom-1 right-2 w-4.5 h-4.5 rounded-full border-2 border-white bg-green-500"></span>
            </div>
            
            <div class="space-y-1">
                <span id="neighbor-title" class="bg-brand-100 text-brand-700 font-extrabold text-[9px] uppercase tracking-wider px-2.5 py-0.5 rounded-full">이웃 반려 생활가</span>
                <h4 id="neighbor-nickname" class="font-black text-gray-800 text-lg">닉네임</h4>
                <p class="text-[10px] text-gray-400 flex items-center justify-center gap-1">
                    <span id="neighbor-pet-name">반려동물</span>의 집사님 🏠
                </p>
            </div>
        </div>

        <!-- 펫 상세 명세 카드 -->
        <div class="bg-amber-50/40 border border-amber-100/40 rounded-2xl p-4 space-y-3.5 mb-6 text-xs text-gray-600">
            <div class="flex items-center justify-between border-b border-amber-100/30 pb-2">
                <span class="font-extrabold text-[10px] text-amber-600/80 uppercase">🐾 반려동물 프로필</span>
                <span id="neighbor-pet-breed" class="font-bold text-gray-500">포메라니안</span>
            </div>
            <div class="grid grid-cols-2 gap-y-2.5 gap-x-2">
                <div>
                    <span class="block text-[9px] text-gray-400">분류</span>
                    <span id="neighbor-pet-type" class="font-bold text-gray-700">강아지 🐕</span>
                </div>
                <div>
                    <span class="block text-[9px] text-gray-400">성격 / 특징</span>
                    <span id="neighbor-pet-personality" class="font-bold text-gray-700">산책을 너무 좋아함</span>
                </div>
                <div>
                    <span class="block text-[9px] text-gray-400">우리 댕이와의 조화도 💖</span>
                    <span id="neighbor-chemistry" class="font-bold text-rose-500 font-mono text-xs">95%</span>
                </div>
                <div>
                    <span class="block text-[9px] text-gray-400">소셜 상태</span>
                    <span id="neighbor-status-text" class="font-bold text-green-600">실시간 온라인</span>
                </div>
            </div>
            <!-- 추가된 오늘의 컨디션 요약 (Phase 3) -->
            <div class="mt-3 pt-3 border-t border-amber-100/30 flex justify-between items-center bg-white p-2.5 rounded-xl shadow-sm border border-amber-50">
                <span class="text-[9px] font-bold text-amber-800"><i class="fa-solid fa-stethoscope mr-1"></i>오늘의 컨디션</span>
                <span id="neighbor-health-condition" class="text-xs font-black text-amber-600">활력 충만 ✨</span>
            </div>
        </div>

        <!-- 액션 버튼 그룹 -->
        <div class="space-y-2.5">
            <button id="neighbor-chat-btn" onclick="startChatWithNeighbor()" class="w-full bg-brand-500 hover:bg-brand-600 text-white font-extrabold py-3.5 rounded-2xl text-xs shadow-md hover:shadow-lg transition-all flex items-center justify-center gap-2 outline-none">
                <i class="fa-solid fa-comments"></i> 1:1 대화방 입장하기
            </button>
            <div class="flex gap-2">
                <button id="neighbor-walk-btn" onclick="requestWalkSchedule()" class="flex-grow bg-brand-50 hover:bg-brand-100 text-brand-700 font-extrabold py-3.5 rounded-2xl text-xs border border-brand-100 transition-all flex items-center justify-center gap-2 outline-none">
                    <i class="fa-solid fa-calendar-plus"></i> 공동 산책 신청
                </button>
                <button id="neighbor-bone-btn" onclick="sendGetWellBone()" class="flex-grow bg-rose-50 hover:bg-rose-100 text-rose-600 font-extrabold py-3.5 rounded-2xl text-[11px] border border-rose-100 transition-all flex items-center justify-center gap-1 outline-none group">
                    <i class="fa-solid fa-bone group-hover:animate-bounce"></i> 응원 뼈다귀 보내기
                </button>
            </div>
            
            <!-- 이웃 관리 영역 (차단/삭제) (Phase 5) -->
            <div id="neighbor-manage-panel" class="pt-3 border-t border-gray-100 flex gap-2 justify-center items-center text-[10px] font-bold">
                <button id="neighbor-block-btn" onclick="toggleBlockNeighbor()" class="text-gray-400 hover:text-rose-600 transition-colors flex items-center gap-1 outline-none">
                    <i class="fa-solid fa-ban"></i> 이웃 차단
                </button>
                <span class="text-gray-300">|</span>
                <button id="neighbor-delete-btn" onclick="deleteNeighbor()" class="text-gray-400 hover:text-red-700 transition-colors flex items-center gap-1 outline-none">
                    <i class="fa-solid fa-user-minus"></i> 이웃 삭제
                </button>
            </div>
        </div>
    </div>
</div>

<!-- 산책 경로(안심 트랙) 미리보기 모달 -->
<div id="walk-trail-modal" class="fixed inset-0 bg-black/60 items-center justify-center z-[110] p-4 hidden">
    <div class="bg-white rounded-3xl p-5 max-w-lg w-full shadow-2xl relative border border-amber-100/60 overflow-y-auto no-scrollbar max-h-[90vh]">
        <!-- 닫기 버튼 -->
        <button onclick="closeWalkTrailModal()" class="absolute top-4 right-4 text-gray-400 hover:text-gray-600 outline-none transition-colors z-[120]" aria-label="닫기"><i class="fa-solid fa-xmark text-lg"></i></button>
        
        <h4 class="font-black text-gray-800 text-sm flex items-center border-b pb-3 mb-4">
            <i class="fa-solid fa-route text-brand-500 mr-2"></i> 안심 산책 정복 트랙 지도 🗺️
        </h4>
        
        <!-- 지도 컨테이너 -->
        <div class="relative w-full bg-gray-50 rounded-2xl overflow-hidden border border-gray-100 shadow-inner mb-4" style="height: 320px; z-index: 1;">
            <div id="modal-trail-map" class="w-full h-full"></div>
        </div>
        
        <!-- 하단 대시보드 수치 -->
        <div class="grid grid-cols-3 gap-2 text-center text-xs mb-4">
            <div class="bg-gray-50 p-2.5 rounded-xl border border-gray-100">
                <span class="block text-[9px] text-gray-400 font-bold mb-0.5">이동 거리</span>
                <span id="modal-trail-distance" class="text-sm font-black text-brand-600 font-mono">0.00 km</span>
            </div>
            <div class="bg-gray-50 p-2.5 rounded-xl border border-gray-100">
                <span class="block text-[9px] text-gray-400 font-bold mb-0.5">산책 시간</span>
                <span id="modal-trail-duration" class="text-sm font-black text-gray-800 font-mono">00:00</span>
            </div>
            <div class="bg-gray-50 p-2.5 rounded-xl border border-gray-100">
                <span class="block text-[9px] text-gray-400 font-bold mb-0.5">소모 열량</span>
                <span id="modal-trail-calories" class="text-sm font-black text-rose-500 font-mono">0 kcal</span>
            </div>
        </div>

        <div class="flex gap-2 text-center text-[10px] text-gray-500 font-bold bg-amber-50/40 p-2.5 rounded-xl border border-amber-100/30 justify-around mb-3.5">
            <span>💩 응가: <span id="modal-trail-poop" class="font-mono text-brand-600">0</span>회</span>
            <span>💦 쉬야: <span id="modal-trail-pee" class="font-mono text-brand-600">0</span>회</span>
            <span>👃 킁킁: <span id="modal-trail-sniff" class="font-mono text-brand-600">0</span>회</span>
        </div>

        <!-- 🐾 산책 중 남긴 흔적 타임라인 목록 -->
        <div class="border-t border-gray-100 pt-3">
            <span class="block text-[10px] text-gray-400 font-black uppercase tracking-wider mb-2 flex items-center gap-1">
                <i class="fa-solid fa-shoe-prints text-brand-500"></i> 안심 산책 동선 흔적 타임라인 🐾
            </span>
            <div id="modal-trail-events" class="space-y-1.5 max-h-[110px] overflow-y-auto no-scrollbar text-[11px] text-gray-600">
                <!-- 동적으로 채워짐 -->
            </div>
        </div>
    </div>
</div>

<!-- 건강 기록 일지 모달 -->
<div id="health-log-modal" class="fixed inset-0 bg-black/60 items-center justify-center z-[110] p-4 hidden">
    <div class="bg-white rounded-3xl p-5 max-w-md w-full shadow-2xl relative border border-teal-100 max-h-[90vh] overflow-y-auto no-scrollbar">
        <button onclick="closeHealthLogModal()" class="absolute top-4 right-4 text-gray-400 hover:text-gray-600 outline-none" aria-label="닫기"><i class="fa-solid fa-xmark text-lg"></i></button>
        <h4 class="font-black text-gray-800 text-sm flex items-center border-b pb-3 mb-4">
            <i class="fa-solid fa-notes-medical text-teal-500 mr-2"></i>오늘의 스마트 건강 기록 일지
        </h4>
        
        <div class="space-y-4 text-xs">
            <!-- 배변 상태 기록 -->
            <div class="bg-teal-50/30 p-3 rounded-2xl border border-teal-100/50">
                <label class="block font-bold text-gray-600 mb-2">💩 오늘 우리 아이의 배변 상태는?</label>
                <div class="grid grid-cols-4 gap-2">
                    <button type="button" id="poop-type-null" onclick="selectPoopType('null')" class="health-poop-btn py-2.5 rounded-xl border border-gray-200 bg-white text-gray-400 font-bold hover:bg-gray-50 flex flex-col items-center justify-center gap-1 transition-all">
                        <span class="text-xl">🤷</span>없음
                    </button>
                    <button type="button" id="poop-type-normal" onclick="selectPoopType('normal')" class="health-poop-btn py-2.5 rounded-xl border border-gray-200 bg-white text-gray-700 font-bold hover:bg-amber-50 flex flex-col items-center justify-center gap-1 transition-all">
                        <span class="text-xl">💩</span>건강한 변
                    </button>
                    <button type="button" id="poop-type-hard" onclick="selectPoopType('hard')" class="health-poop-btn py-2.5 rounded-xl border border-gray-200 bg-white text-gray-700 font-bold hover:bg-amber-50 flex flex-col items-center justify-center gap-1 transition-all">
                        <span class="text-xl">🪨</span>딱딱한 변
                    </button>
                    <button type="button" id="poop-type-liquid" onclick="selectPoopType('liquid')" class="health-poop-btn py-2.5 rounded-xl border border-gray-200 bg-white text-gray-700 font-bold hover:bg-amber-50 flex flex-col items-center justify-center gap-1 transition-all">
                        <span class="text-xl">💦</span>묽은 변
                    </button>
                </div>
            </div>

            <!-- 식사량 기록 -->
            <div class="bg-teal-50/30 p-3 rounded-2xl border border-teal-100/50">
                <div class="flex justify-between items-center mb-2">
                    <label class="font-bold text-gray-600">🍚 식사(사료/간식) 급여량</label>
                    <span id="food-amount-disp" class="font-black text-teal-600 font-mono text-sm">0g</span>
                </div>
                <input type="range" id="food-amount-slider" min="0" max="500" value="0" step="10" oninput="document.getElementById('food-amount-disp').innerText = this.value + 'g'" class="w-full h-2 bg-gray-200 rounded-lg appearance-none cursor-pointer accent-teal-500">
            </div>

            <!-- 음수량 기록 -->
            <div class="bg-teal-50/30 p-3 rounded-2xl border border-teal-100/50">
                <div class="flex justify-between items-center mb-2">
                    <label class="font-bold text-gray-600">💧 오늘 하루 총 음수량</label>
                    <span id="water-amount-disp" class="font-black text-blue-500 font-mono text-sm">0ml</span>
                </div>
                <input type="range" id="water-amount-slider" min="0" max="1000" value="0" step="50" oninput="document.getElementById('water-amount-disp').innerText = this.value + 'ml'" class="w-full h-2 bg-gray-200 rounded-lg appearance-none cursor-pointer accent-blue-500">
            </div>
            
            <div class="space-y-2">
                <button onclick="saveHealthLog()" class="w-full bg-teal-500 hover:bg-teal-600 text-white font-bold py-3.5 rounded-2xl text-xs shadow-md transition-colors flex items-center justify-center gap-2 outline-none">
                    <i class="fa-solid fa-save"></i>건강 일지 안전하게 보관하기
                </button>
                <button onclick="generateWeeklyHealthData()" class="w-full bg-brand-500 hover:bg-brand-600 text-white font-bold py-2.5 rounded-2xl text-xs shadow-md transition-colors flex items-center justify-center gap-2 outline-none">
                    <i class="fa-solid fa-dice"></i>일주일치 데이터 랜덤 생성
                </button>
            </div>
        </div>
    </div>
</div>

<!-- 💡 월간 맞춤 헬스 리포트 모달 (Phase 3 Click-flow 고도화) -->
<div id="health-report-modal" class="fixed inset-0 bg-black/60 z-[100] hidden items-center justify-center p-5 sm:p-6 backdrop-blur-sm transition-opacity duration-300">
    <div class="bg-white rounded-[2rem] w-full max-w-sm overflow-hidden shadow-2xl relative flex flex-col max-h-[90vh]">
        <!-- Header -->
        <div class="bg-gradient-to-r from-brand-500 to-brand-600 p-5 text-center relative shrink-0">
            <h3 class="text-lg font-black text-white tracking-tight drop-shadow-sm">이번 달 맞춤 헬스 리포트 🔮</h3>
            <p class="text-brand-100 text-[10px] mt-1 font-medium">우리 아이 기질 기반 심층 분석</p>
            <button onclick="closeHealthReportModal()" class="absolute right-4 top-4 text-white/80 hover:text-white w-8 h-8 rounded-full bg-white/10 flex items-center justify-center transition-colors" aria-label="닫기">
                <i class="fa-solid fa-xmark text-lg"></i>
            </button>
        </div>

        <!-- Scrollable Content -->
        <div class="p-5 overflow-y-auto no-scrollbar space-y-5 bg-slate-50/50">
            
            <!-- Summary Badge -->
            <div class="flex items-center justify-between bg-white p-4 rounded-2xl border border-brand-50 shadow-sm">
                <div class="flex items-center gap-3">
                    <div class="w-12 h-12 bg-gradient-to-br from-brand-100 to-brand-100 rounded-full flex items-center justify-center text-2xl border-2 border-white shadow-sm">
                        📊
                    </div>
                    <div>
                        <span class="block text-[10px] text-gray-400 font-bold mb-0.5">현재 건강 달성률</span>
                        <span class="font-black text-gray-800 text-lg">상위 15% 📈</span>
                    </div>
                </div>
                <div class="text-right">
                    <span class="bg-brand-100 text-brand-700 text-[9px] font-black px-2 py-0.5 rounded-full block mb-1">건강 점수</span>
                    <span class="font-black text-brand-600 text-xl">92<span class="text-[10px] text-brand-400 ml-0.5">점</span></span>
                </div>
            </div>

            <!-- Chart Simulation -->
            <div class="bg-white p-4 rounded-2xl border border-gray-100 shadow-sm">
                <h4 class="text-xs font-black text-gray-800 mb-3 flex items-center gap-1.5"><i class="fa-solid fa-chart-line text-brand-500"></i> 기질 맞춤 5대 건강 지표</h4>
                <div class="space-y-3">
                    <!-- Progress item -->
                    <div>
                        <div class="flex justify-between text-[10px] font-bold mb-1">
                            <span class="text-gray-600">수분 밸런스 (음수량)</span>
                            <span class="text-blue-500">95%</span>
                        </div>
                        <div class="w-full bg-gray-100 rounded-full h-1.5"><div class="bg-blue-400 h-1.5 rounded-full" style="width: 95%"></div></div>
                    </div>
                    <!-- Progress item -->
                    <div>
                        <div class="flex justify-between text-[10px] font-bold mb-1">
                            <span class="text-gray-600">소화 및 장 건강</span>
                            <span class="text-amber-500">88%</span>
                        </div>
                        <div class="w-full bg-gray-100 rounded-full h-1.5"><div class="bg-amber-400 h-1.5 rounded-full" style="width: 88%"></div></div>
                    </div>
                    <!-- Progress item -->
                    <div>
                        <div class="flex justify-between text-[10px] font-bold mb-1">
                            <span class="text-gray-600">관절 및 근력 보존</span>
                            <span class="text-rose-500">76%</span>
                        </div>
                        <div class="w-full bg-gray-100 rounded-full h-1.5"><div class="bg-rose-400 h-1.5 rounded-full" style="width: 76%"></div></div>
                    </div>
                </div>
                <p class="text-[10px] text-gray-400 mt-3 bg-gray-50 p-2 rounded-lg leading-relaxed">
                    💡 <strong class="text-gray-600">Tip:</strong> 관절 건강 수치가 약간 부족합니다. 사주상 뼈가 얇은 체질일 수 있으니 실내에 미끄럼 방지 매트를 꼭 깔아주세요.
                </p>
            </div>

            <!-- Social Call to action -->
            <div class="bg-brand-50 border border-brand-100 p-4 rounded-2xl flex items-center justify-between">
                <div>
                    <span class="block font-black text-brand-900 text-xs mb-0.5">이웃들과 조언 나누기</span>
                    <span class="text-[9px] text-brand-500 font-medium">우리 아이의 기특한 달성률을 자랑해 보세요.</span>
                </div>
                <button onclick="closeHealthReportModal(); switchTab('social'); switchSocialSubTab('feed');" class="bg-brand-600 hover:bg-brand-700 text-white font-bold px-3 py-2 rounded-xl text-[10px] shadow-sm transition-colors shrink-0">
                    피드로 가기 <i class="fa-solid fa-arrow-right ml-0.5"></i>
                </button>
            </div>
            
        </div>
    </div>
</div>

<!-- 📮 편지 쓰기 모달 -->
<div id="letter-write-modal" class="fixed inset-0 bg-black/60 items-center justify-center z-[110] p-4 hidden">
    <div class="bg-white rounded-3xl shadow-2xl relative border border-amber-100 max-w-2xl w-full max-h-[90vh] overflow-y-auto no-scrollbar flex flex-col">
        <!-- 닫기 버튼 -->
        <button onclick="closeLetterWriteModal()" class="absolute top-4 right-4 text-gray-400 hover:text-gray-600 outline-none z-10" aria-label="닫기">
            <i class="fa-solid fa-xmark text-lg"></i>
        </button>
        
        <!-- 헤더 -->
        <div class="p-5 border-b border-amber-50 bg-gradient-to-r from-amber-50 to-orange-50/50 rounded-t-3xl shrink-0">
            <h4 id="letter-write-title" class="font-black text-gray-800 text-sm flex items-center">
                <i class="fa-solid fa-envelope-open-text text-brand-500 mr-2 text-base"></i>마음을 담은 편지 쓰기
            </h4>
            <p class="text-[10px] text-gray-400 font-bold mt-0.5">이웃 집사에게 따뜻한 마음과 소식을 전해보세요.</p>
        </div>
        
        <!-- 엽서 본문 (2열 레이아웃) -->
        <div class="p-6 grid grid-cols-1 md:grid-cols-12 gap-6 bg-[#fdfbf7]">
            <!-- 왼쪽: 편지 작성란 (줄노트 느낌) -->
            <div class="md:col-span-7 flex flex-col bg-white rounded-2xl border border-amber-100/70 p-5 shadow-sm min-h-[250px] relative">
                <div class="absolute top-4 right-4 text-[9px] font-mono text-amber-600/60 bg-amber-50 px-2 py-0.5 rounded-full font-bold">POSTCARD</div>
                <div class="flex-grow pt-4">
                    <textarea id="letter-write-content" rows="6" maxlength="300" placeholder="여기에 따뜻한 메시지를 남겨주세요...&#10;이웃 집사님이 미소를 지을 수 있는 따뜻한 한마디면 충분합니다. 💌" 
                        class="w-full bg-transparent border-0 resize-none outline-none text-xs text-gray-700 leading-relaxed placeholder:text-gray-300 font-medium"
                        oninput="updateLetterCharCount()"></textarea>
                </div>
                <!-- 답장 전용 원문 인용 (답장할 때만 보임) -->
                <div id="letter-reply-quote-container" class="hidden mt-2 p-2 bg-amber-50/70 border border-amber-100 rounded-xl max-h-[80px] overflow-y-auto no-scrollbar text-[10px] text-gray-500 italic">
                    <span class="block font-bold text-amber-700 not-italic mb-0.5">답장 대상 편지:</span>
                    <span id="letter-reply-quote-text"></span>
                </div>
                <div class="flex justify-between items-center mt-3 pt-3 border-t border-dashed border-amber-100">
                    <span id="letter-write-char-count" class="text-[10px] font-bold text-amber-500 font-mono">0 / 300자</span>
                    <span class="text-[10px] text-gray-400 font-bold">정성가득 작성해주세요 ✍️</span>
                </div>
            </div>
            
            <!-- 오른쪽: 받는 사람, 보낸 펫, 우표 선택 -->
            <div class="md:col-span-5 flex flex-col justify-between space-y-4">
                <!-- 받는 사람 & 보낸 펫 -->
                <div class="space-y-3.5 text-xs">
                    <!-- 받는 사람 -->
                    <div>
                        <label class="block font-black text-gray-600 mb-1 flex items-center gap-1">
                            <span>To.</span> 받는 집사
                        </label>
                        <div class="relative" id="recipient-selection-container">
                            <select id="letter-write-receiver-select" onchange="handleReceiverSelectChange()"
                                class="w-full border border-amber-100 rounded-xl p-2.5 outline-none focus:border-brand-500 bg-white font-bold text-gray-700">
                                <option value="">-- 이웃 집사 선택 --</option>
                                <!-- 동적 로딩 -->
                            </select>
                            <input type="text" id="letter-write-receiver-custom" placeholder="직접 집사 닉네임 입력" 
                                class="w-full mt-1.5 border border-amber-100 rounded-xl p-2.5 outline-none focus:border-brand-500 bg-white/70 font-bold text-gray-700 placeholder:text-gray-300">
                        </div>
                    </div>
                    
                    <!-- 보낸 펫 -->
                    <div>
                        <label class="block font-black text-gray-600 mb-1 flex items-center gap-1">
                            <span>From.</span> 보낸 아이
                        </label>
                        <select id="letter-write-sender-pet" 
                            class="w-full border border-amber-100 rounded-xl p-2.5 outline-none focus:border-brand-500 bg-white font-bold text-gray-700">
                            <!-- 동적 로딩 -->
                        </select>
                    </div>
                </div>
                
                <!-- 우표 선택 & 우표 디스플레이 -->
                <div class="border border-amber-100 bg-white rounded-2xl p-4 shadow-sm relative">
                    <div class="flex justify-between items-start mb-3">
                        <span class="text-[10px] font-black text-gray-400 uppercase tracking-wider">우표 붙이기</span>
                        <!-- 우표 visual slot -->
                        <div id="postcard-stamp-slot" class="w-14 h-16 border-2 border-dashed border-amber-200 bg-amber-50/50 rounded flex items-center justify-center text-3xl shadow-inner relative transform rotate-3 select-none">
                            🐾
                        </div>
                    </div>
                    
                    <!-- 우표 리스트 -->
                    <div class="grid grid-cols-5 gap-1.5" id="stamp-selector-grid">
                        <button onclick="selectPostcardStamp('🐾', this)" class="stamp-btn border-2 border-brand-500 bg-brand-50 rounded-lg p-1 text-base flex items-center justify-center hover:scale-105 transition-all outline-none">🐾</button>
                        <button onclick="selectPostcardStamp('🐈', this)" class="stamp-btn border-2 border-transparent bg-gray-50 rounded-lg p-1 text-base flex items-center justify-center hover:scale-105 transition-all outline-none">🐈</button>
                        <button onclick="selectPostcardStamp('🐰', this)" class="stamp-btn border-2 border-transparent bg-gray-50 rounded-lg p-1 text-base flex items-center justify-center hover:scale-105 transition-all outline-none">🐰</button>
                        <button onclick="selectPostcardStamp('💖', this)" class="stamp-btn border-2 border-transparent bg-gray-50 rounded-lg p-1 text-base flex items-center justify-center hover:scale-105 transition-all outline-none">💖</button>
                        <button onclick="selectPostcardStamp('🍀', this)" class="stamp-btn border-2 border-transparent bg-gray-50 rounded-lg p-1 text-base flex items-center justify-center hover:scale-105 transition-all outline-none">🍀</button>
                    </div>
                </div>
            </div>
        </div>
        
        <!-- 푸터 전송 버튼 -->
        <div class="p-5 border-t border-amber-50 bg-gray-50/50 flex space-x-2 text-xs shrink-0 rounded-b-3xl">
            <button onclick="closeLetterWriteModal()" class="w-1/3 bg-gray-200 hover:bg-gray-300 text-gray-700 font-black py-3 rounded-2xl transition-colors outline-none">
                작성 취소
            </button>
            <button onclick="submitLetter()" class="w-2/3 bg-brand-500 hover:bg-brand-600 text-white font-black py-3 rounded-2xl shadow-md transition-colors flex items-center justify-center gap-1.5 outline-none">
                <i class="fa-solid fa-paper-plane"></i>편지 날려보내기 🕊️
            </button>
        </div>
    </div>
</div>

<!-- 💌 편지 상세보기 모달 -->
<div id="letter-detail-modal" class="fixed inset-0 bg-black/60 items-center justify-center z-[110] p-4 hidden">
    <div class="bg-white rounded-3xl shadow-2xl relative border border-amber-100 max-w-2xl w-full max-h-[90vh] overflow-y-auto no-scrollbar flex flex-col">
        <!-- 닫기 버튼 -->
        <button onclick="closeLetterDetailModal()" class="absolute top-4 right-4 text-gray-400 hover:text-gray-600 outline-none z-10" aria-label="닫기">
            <i class="fa-solid fa-xmark text-lg"></i>
        </button>
        
        <!-- 헤더 -->
        <div class="p-5 border-b border-amber-50 bg-gradient-to-r from-amber-50 to-orange-50/50 rounded-t-3xl shrink-0">
            <h4 class="font-black text-gray-800 text-sm flex items-center">
                <i class="fa-solid fa-envelope-open-text text-brand-500 mr-2 text-base"></i>마음을 전하는 엽서 도착 💌
            </h4>
        </div>
        
        <!-- 엽서 본문 (2열 레이아웃) -->
        <div class="p-6 grid grid-cols-1 md:grid-cols-12 gap-6 bg-[#fdfbf7]">
            <!-- 왼쪽: 편지 내용 (줄노트 느낌) -->
            <div class="md:col-span-7 flex flex-col bg-white rounded-2xl border border-amber-100/70 p-5 shadow-sm min-h-[250px] relative">
                <div class="absolute top-4 right-4 text-[9px] font-mono text-amber-600/60 bg-amber-50 px-2 py-0.5 rounded-full font-bold">POSTCARD</div>
                <div class="flex-grow pt-4">
                    <p id="letter-detail-content" class="text-xs text-gray-700 leading-relaxed font-medium whitespace-pre-wrap break-all"></p>
                </div>
                <div class="mt-3 pt-3 border-t border-dashed border-amber-100 text-right">
                    <span id="letter-detail-date" class="text-[9px] text-gray-400 font-mono font-bold">2026-05-23</span>
                </div>
            </div>
            
            <!-- 오른쪽: 받는 사람, 보낸 사람, 우표 -->
            <div class="md:col-span-5 flex flex-col justify-between space-y-4">
                <!-- 받는 사람 & 보낸 사람 -->
                <div class="space-y-4 text-xs bg-white rounded-2xl border border-amber-50 p-4 shadow-sm flex-grow">
                    <div class="border-b border-gray-50 pb-3">
                        <span class="block text-[9px] text-gray-400 font-bold mb-0.5">받는 집사</span>
                        <div class="flex items-center gap-1.5">
                            <span class="text-xs font-black text-gray-800" id="letter-detail-receiver-name">나</span>
                        </div>
                    </div>
                    <div class="border-b border-gray-50 pb-3">
                        <span class="block text-[9px] text-gray-400 font-bold mb-0.5">보낸 집사</span>
                        <div class="flex items-center gap-1.5">
                            <span class="text-xs font-black text-gray-800" id="letter-detail-sender-name">초코언니</span>
                            <span class="text-[10px] text-gray-400 font-medium" id="letter-detail-pet-name">(초코)</span>
                        </div>
                    </div>
                    <div>
                        <span class="block text-[9px] text-gray-400 font-bold mb-0.5">편지 위치</span>
                        <span id="letter-detail-folder-badge" class="bg-brand-50 text-brand-700 font-extrabold text-[9px] px-2 py-0.5 rounded-full inline-block mt-0.5">받은 편지함</span>
                    </div>
                </div>
                
                <!-- 우표 표시 -->
                <div class="border border-amber-100 bg-white rounded-2xl p-4 shadow-sm flex items-center justify-between">
                    <span class="text-xs font-bold text-gray-500 font-bold">우체국 인증 우표</span>
                    <div id="letter-detail-stamp-display" class="w-14 h-16 border-2 border-dashed border-amber-200 bg-amber-50/50 rounded flex items-center justify-center text-3xl shadow-inner transform rotate-3 select-none">
                        🐾
                    </div>
                </div>
            </div>
        </div>
        
        <!-- 푸터 액션 버튼 -->
        <div id="letter-detail-actions" class="p-5 border-t border-amber-50 bg-gray-50/50 flex space-x-2 text-xs shrink-0 rounded-b-3xl">
            <!-- 동적으로 버튼이 렌더링 됨: 답장, 삭제, 복원, 영구삭제 등 -->
        </div>
    </div>
</div>

<!-- 🚨 시스템 오류 로그 상세 보기 모달 -->
<div id="error-log-modal" class="fixed inset-0 bg-black/60 items-center justify-center z-[110] p-4 hidden">
    <div class="bg-white rounded-3xl p-5 max-w-lg w-full shadow-2xl relative border border-rose-100 max-h-[90vh] overflow-y-auto no-scrollbar flex flex-col">
        <button onclick="closeErrorLogModal()" class="absolute top-4 right-4 text-gray-400 hover:text-gray-600 outline-none" aria-label="닫기"><i class="fa-solid fa-xmark text-lg"></i></button>
        <h4 class="font-black text-gray-800 text-sm flex items-center border-b pb-3 mb-4">
            <i class="fa-solid fa-bug text-rose-500 mr-2"></i>시스템 오류 로그 목록 🚨
        </h4>
        
        <div class="flex justify-between items-center mb-3">
            <span class="text-[10px] text-gray-400 font-bold uppercase">최근 발생한 오류 내역 (최대 50개)</span>
            <button onclick="clearSystemErrorLogs()" class="bg-gray-100 hover:bg-gray-200 text-gray-700 font-bold text-[9px] py-1 px-2.5 rounded-lg shadow-sm transition-all flex items-center gap-1">
                <i class="fa-solid fa-trash-can"></i>로그 비우기
            </button>
        </div>

        <div id="error-log-list" class="space-y-2.5 overflow-y-auto max-h-[50vh] pr-1">
            <!-- Dynamic Error Logs -->
        </div>

        <div class="mt-5 pt-3 border-t border-gray-100 flex gap-2">
            <button onclick="copyErrorLogsToClipboard()" class="flex-grow bg-rose-50 hover:bg-rose-100 text-rose-600 font-bold py-2.5 rounded-xl transition-all text-center text-xs">
                <i class="fa-solid fa-copy mr-1"></i>전체 로그 복사하기
            </button>
            <button onclick="closeErrorLogModal()" class="w-24 bg-gray-100 hover:bg-gray-200 text-gray-700 font-bold py-2.5 rounded-xl transition-all text-center text-xs">
                닫기
            </button>
        </div>
    </div>
</div>

<!-- 자랑글 수정 모달 ✏️ -->
<div id="feed-edit-modal" class="fixed inset-0 bg-black/60 items-center justify-center z-50 p-4 hidden">
    <div class="bg-white rounded-3xl p-5 sm:p-6 max-w-sm w-full shadow-2xl relative border border-amber-100 max-h-[90vh] overflow-y-auto no-scrollbar flex flex-col">
        <button onclick="closeFeedEditModal()" class="absolute top-4 right-4 text-gray-400 hover:text-gray-600 outline-none" aria-label="닫기"><i class="fa-solid fa-xmark text-lg"></i></button>
        <h4 class="font-black text-gray-800 text-sm flex items-center border-b pb-3 mb-4">
            <i class="fa-solid fa-pen-to-square text-brand-500 mr-2"></i>자랑글 수정 ✏️
        </h4>
        
        <div class="space-y-4 text-xs">
            <div>
                <label class="block font-bold text-gray-600 mb-1">본문 이야기 수정</label>
                <textarea id="feed-edit-content" rows="4" placeholder="내용을 작성해 주세요..." class="w-full border rounded-xl p-3 outline-none focus:border-brand-500 bg-gray-50/20 resize-none"></textarea>
            </div>
            
            <!-- 첨부된 미디어 수정 구역 -->
            <div>
                <label class="block font-bold text-gray-600 mb-2">첨부된 사진/영상</label>
                
                <!-- 미디어 미리보기 영역 -->
                <div id="feed-edit-media-preview-container" class="relative w-full aspect-video bg-gray-100 rounded-xl overflow-hidden shadow-inner border border-gray-200 group flex items-center justify-center">
                    <img loading="lazy" id="feed-edit-photo-preview" class="w-full h-full object-cover hidden">
                    <video id="feed-edit-video-preview" src="" class="w-full h-full object-cover hidden" controls></video>
                    <div id="feed-edit-media-placeholder" class="text-gray-400 text-xs font-bold py-6">첨부된 미디어가 없습니다.</div>
                </div>
                
                <!-- 조작 버튼들 -->
                <div class="flex gap-2 mt-3">
                    <button onclick="triggerFeedEditPhotoUpload()" class="flex-1 bg-gray-50 hover:bg-brand-50 text-gray-600 hover:text-brand-600 font-bold text-[10px] py-2 px-2.5 rounded-xl border border-gray-200 transition-all flex items-center justify-center gap-1">
                        <i class="fa-solid fa-cloud-arrow-up text-emerald-600"></i> 기기 사진 올리기
                    </button>
                    <input type="file" id="feed-edit-photo-upload" onchange="handleFeedEditPhotoUpload(event)" accept="image/*" class="hidden">
                    
                    <button onclick="selectEditPresetPhoto()" class="flex-1 bg-gray-50 hover:bg-brand-50 text-gray-600 hover:text-brand-600 font-bold text-[10px] py-2 px-2.5 rounded-xl border border-gray-200 transition-all flex items-center justify-center gap-1">
                        <i class="fa-solid fa-image text-emerald-500"></i> 프리셋 사진
                    </button>
                </div>
                
                <div class="flex gap-2 mt-2">
                    <button id="feed-edit-delete-media-btn" onclick="clearFeedEditMedia()" class="w-full bg-rose-50 hover:bg-rose-100 text-rose-600 font-bold text-[10px] py-2 rounded-xl border border-rose-100 transition-all flex items-center justify-center gap-1">
                        <i class="fa-solid fa-trash-can"></i> 첨부된 사진/영상 지우기
                    </button>
                </div>
            </div>
            
            <div class="flex gap-2 pt-2 border-t border-gray-100">
                <button onclick="closeFeedEditModal()" class="w-1/3 bg-gray-100 hover:bg-gray-200 text-gray-700 font-bold py-3 rounded-xl transition-all text-center">
                    취소
                </button>
                <button onclick="submitFeedEditPost()" class="w-2/3 bg-brand-500 hover:bg-brand-600 text-white font-bold py-3 rounded-xl transition-all text-center shadow-md">
                    수정 완료
                </button>
            </div>
        </div>
</div>

<!-- 📒 반려 생활수첩 모달 -->
<div id="notebook-modal" class="fixed inset-0 bg-black/60 items-center justify-center z-[110] p-4 hidden">
    <div class="bg-white rounded-3xl p-5 sm:p-6 max-w-sm w-full shadow-2xl relative border border-amber-100 max-h-[90vh] overflow-y-auto no-scrollbar flex flex-col">
        <button onclick="closeNotebookModal()" class="absolute top-4 right-4 text-gray-400 hover:text-gray-600 outline-none" aria-label="닫기"><i class="fa-solid fa-xmark text-lg"></i></button>
        <h4 class="font-black text-gray-800 text-sm flex items-center border-b pb-3 mb-4">
            <i class="fa-solid fa-address-book text-brand-500 mr-2"></i>반려 생활수첩 📝
        </h4>
        
        <!-- 모달 내부 탭 조작계 -->
        <div class="flex border-b border-gray-100 mb-4 bg-gray-50/50 p-1 rounded-2xl gap-1">
            <button onclick="switchNotebookTab('info')" id="notebook-tab-info" class="flex-1 py-2 text-center text-[11px] font-black rounded-xl transition-all text-brand-600 bg-white shadow-xs outline-none">
                📝 반려 프로필
            </button>
            <button onclick="switchNotebookTab('routes')" id="notebook-tab-routes" class="flex-1 py-2 text-center text-[11px] font-black rounded-xl transition-all text-gray-400 hover:text-gray-600 outline-none">
                🗺️ 맞춤 산책 코스
            </button>
        </div>

        <!-- 탭 1: 반려 프로필 관리 -->
        <div id="notebook-tab-content-info" class="space-y-4">
            <div class="flex justify-between items-center">
                <span class="text-xs font-bold text-gray-400">품종 스펙 및 성격 기재</span>
                <button id="btn-notebook-toggle" onclick="toggleNotebookEdit(true)" class="text-brand-600 hover:text-brand-700 font-bold text-xs">
                    <i class="fa-solid fa-pen-to-square mr-1"></i>편집
                </button>
            </div>

            <!-- 뷰 모드 -->
            <div id="notebook-view-mode" class="space-y-2.5 text-xs text-gray-700 font-medium">
                <div class="flex justify-between py-1 border-b border-dashed border-gray-50">
                    <span class="text-gray-400">품종 스펙</span>
                    <span id="pet-info-breed" class="font-bold">골든 리트리버</span>
                </div>
                <div class="flex justify-between py-1 border-b border-dashed border-gray-50">
                    <span class="text-gray-400">나이 정보</span>
                    <span id="pet-info-age" class="font-bold">2살 (청소년기)</span>
                </div>
                <div class="flex justify-between py-1 border-b border-dashed border-gray-50">
                    <span class="text-gray-400">성별</span>
                    <span id="pet-info-gender" class="font-bold">남아 (중성화 완료)</span>
                </div>
                <div class="flex justify-between py-1 border-b border-dashed border-gray-50">
                    <span class="text-gray-400">현재 몸무게</span>
                    <span id="pet-info-weight" class="font-bold">24.5 kg</span>
                </div>
                <div>
                    <span class="text-gray-400 block mb-1">우리 펫 성격 / 가치관</span>
                    <p id="pet-info-personality" class="bg-brand-50 text-brand-800 p-2.5 rounded-xl font-bold leading-relaxed keep-all">
                        천사견으로 사람을 엄청 좋아하며 물을 발견하면 일단 뛰쳐드는 성향이 강함.
                    </p>
                </div>
            </div>

            <!-- 에디터 폼 모드 -->
            <div id="notebook-edit-mode" class="space-y-3 text-xs hidden">
                <div>
                    <label class="block font-bold text-gray-500 mb-1">품종 스펙</label>
                    <input type="text" id="edit-nb-breed" class="w-full border rounded-lg p-2 outline-none focus:border-brand-500 bg-white">
                </div>
                <div>
                    <label class="block font-bold text-gray-500 mb-1">나이 정보</label>
                    <input type="text" id="edit-nb-age" class="w-full border rounded-lg p-2 outline-none focus:border-brand-500 bg-white">
                </div>
                <div>
                    <label class="block font-bold text-gray-500 mb-1">성별</label>
                    <input type="text" id="edit-nb-gender" class="w-full border rounded-lg p-2 outline-none focus:border-brand-500 bg-white">
                </div>
                <div>
                    <label class="block font-bold text-gray-500 mb-1">몸무게 (kg)</label>
                    <input type="text" id="edit-nb-weight" class="w-full border rounded-lg p-2 outline-none focus:border-brand-500 bg-white">
                </div>
                <div>
                    <label class="block font-bold text-gray-500 mb-1">우리 펫 성격 / 가치관</label>
                    <textarea id="edit-nb-personality" rows="2" class="w-full border rounded-lg p-2 outline-none focus:border-brand-500 bg-white"></textarea>
                </div>
                <div class="flex space-x-2 pt-2">
                    <button onclick="toggleNotebookEdit(false)" class="w-1/2 bg-gray-100 hover:bg-gray-200 font-bold py-2.5 rounded-xl">취소</button>
                    <button onclick="saveNotebookEdit()" class="w-1/2 bg-brand-500 hover:bg-brand-600 text-white font-bold py-2.5 rounded-xl shadow-md">저장 💾</button>
                </div>
            </div>
        </div>

        <!-- 탭 2: 맞춤 산책 코스 목록 -->
        <div id="notebook-tab-content-routes" class="space-y-4 hidden">
            <span class="block text-xs font-bold text-gray-400"><i class="fa-solid fa-compass text-brand-500 mr-1"></i> 저장된 나만의 맞춤 산책 경로</span>
            <div id="notebook-custom-routes-list" class="space-y-2 max-h-[300px] overflow-y-auto no-scrollbar">
                <!-- 맞춤 코스 리스트 동적 렌더링 -->
            </div>
            <p class="text-[10px] text-gray-400 text-center leading-relaxed mt-2">
                * 지도 탭에서 '산책 경로 만들기'로 생성한 코스 목록이 표기됩니다.<br>
                불러오기 버튼 클릭 시 지도 상에 해당 산책 코스가 연동됩니다.
            </p>
        </div>
    </div>
</div>

<!-- 📕 실물 다이어리 포토북 출판하기 모달 -->
<div id="photobook-publish-modal" class="fixed inset-0 bg-black/60 items-center justify-center z-[120] p-4 hidden">
    <div class="bg-white rounded-3xl max-w-lg w-full shadow-2xl relative border border-amber-100 max-h-[92vh] overflow-y-auto no-scrollbar flex flex-col">
        <!-- 닫기 버튼 -->
        <button onclick="closePhotobookPublishModal()" class="absolute top-4 right-4 text-gray-400 hover:text-gray-600 outline-none z-10" aria-label="닫기">
            <i class="fa-solid fa-xmark text-lg"></i>
        </button>
        
        <!-- 헤더 -->
        <div class="p-5 border-b border-amber-100 bg-gradient-to-r from-amber-50 to-brand-50 rounded-t-3xl shrink-0">
            <h4 class="font-black text-gray-800 text-sm flex items-center">
                <i class="fa-solid fa-book-open-reader text-brand-500 mr-2"></i> 내 댕냥이 실물 포토북 출판하기 📕✨
            </h4>
            <p class="text-[10px] text-gray-400 mt-1">집사가 정성껏 꾸민 모바일 일기장을 고품질 인쇄 도서로 평생 소장하세요.</p>
        </div>

        <div class="p-5 space-y-5 text-xs text-gray-700">
            <!-- 도서 커버 미리보기 -->
            <div class="bg-gray-50/50 p-4 rounded-2xl border border-amber-100/50 flex flex-col sm:flex-row gap-4 items-center">
                <div class="w-32 aspect-[3/4] bg-white rounded-lg shadow-md border border-gray-200 overflow-hidden relative flex-shrink-0 flex items-center justify-center">
                    <img loading="lazy" id="photobook-preview-cover" src="https://images.unsplash.com/photo-1543466835-00a7907e9de1?w=300" class="w-full h-full object-cover">
                    <div class="absolute inset-0 bg-black/20 flex flex-col justify-end p-2 text-white">
                        <span class="block text-[8px] font-bold opacity-80" id="photobook-preview-author">초코네 일기</span>
                        <h5 class="font-black text-[10px] leading-tight" id="photobook-preview-title">우리의 봄날</h5>
                    </div>
                </div>
                <div class="space-y-2 flex-grow w-full">
                    <div>
                        <label class="block font-bold text-gray-600 mb-1">도서 제목 설정</label>
                        <input type="text" id="photobook-title-input" oninput="updatePhotobookPreview()" value="우리의 소중한 추억 일기장" class="w-full border border-gray-200 rounded-xl p-2.5 outline-none focus:border-brand-500 bg-white font-bold text-xs">
                    </div>
                    <div class="grid grid-cols-2 gap-2 text-[10px]">
                        <div class="bg-white p-2 rounded-xl border border-gray-100">
                            <span class="block text-gray-400">선택된 일기 수</span>
                            <span class="font-black text-brand-600" id="photobook-page-count">0장 (0페이지)</span>
                        </div>
                        <div class="bg-white p-2 rounded-xl border border-gray-100">
                            <span class="block text-gray-400">커버 종류</span>
                            <span class="font-black text-gray-800" id="photobook-selected-cover">하드커버</span>
                        </div>
                    </div>
                </div>
            </div>

            <!-- 커버 및 재질 선택 -->
            <div class="space-y-3">
                <span class="block font-bold text-gray-600"><i class="fa-solid fa-sliders text-brand-500 mr-1"></i> 도서 및 종이 옵션</span>
                <div class="grid grid-cols-3 gap-2">
                    <button onclick="selectPhotobookOption('cover', 'soft', 12000, this)" class="photobook-opt-cover py-3 rounded-2xl border-2 border-transparent bg-gray-50 font-bold hover:bg-amber-50/20 text-center flex flex-col items-center justify-center gap-1 transition-all outline-none">
                        <span class="text-sm">📖</span>
                        <span>소프트커버</span>
                        <span class="text-[9px] text-gray-400">기본 요금</span>
                    </button>
                    <button onclick="selectPhotobookOption('cover', 'hard', 18000, this)" class="photobook-opt-cover py-3 rounded-2xl border-2 border-brand-500 bg-brand-50/50 font-bold text-center flex flex-col items-center justify-center gap-1 transition-all outline-none">
                        <span class="text-sm">📕</span>
                        <span class="text-brand-700">하드커버</span>
                        <span class="text-[9px] text-brand-500 font-medium">+6,000원</span>
                    </button>
                    <button onclick="selectPhotobookOption('cover', 'leather', 28000, this)" class="photobook-opt-cover py-3 rounded-2xl border-2 border-transparent bg-gray-50 font-bold hover:bg-amber-50/20 text-center flex flex-col items-center justify-center gap-1 transition-all outline-none">
                        <span class="text-sm">📙</span>
                        <span>레더 명품커버</span>
                        <span class="text-[9px] text-gray-400">+16,000원</span>
                    </button>
                </div>

                <div class="grid grid-cols-2 gap-2 mt-2">
                    <div>
                        <label class="block font-bold text-gray-500 mb-1">내지 인쇄 타입</label>
                        <select id="photobook-paper-select" onchange="calculatePhotobookPrice()" class="w-full border rounded-xl p-2.5 outline-none focus:border-brand-500 bg-white">
                            <option value="matte" selected>친환경 고급 무광지 (기본)</option>
                            <option value="glossy">비비드 유광 코팅지 (+3,000원)</option>
                            <option value="eco">100% 사탕수수 친환경 종이 (+1,500원)</option>
                        </select>
                    </div>
                    <div>
                        <label class="block font-bold text-gray-500 mb-1">인쇄 사이즈</label>
                        <select id="photobook-size-select" onchange="calculatePhotobookPrice()" class="w-full border rounded-xl p-2.5 outline-none focus:border-brand-500 bg-white">
                            <option value="square" selected>스퀘어 에디션 (15x15cm)</option>
                            <option value="a5">A5 클래식 세로 (14x20cm) (+2,000원)</option>
                            <option value="a4">A4 와이드 화보형 (21x29cm) (+7,000원)</option>
                        </select>
                    </div>
                </div>
            </div>

            <!-- 수령지 정보 -->
            <div class="space-y-2 bg-gray-50/30 p-4 rounded-2xl border border-gray-100">
                <span class="block font-bold text-gray-600"><i class="fa-solid fa-truck text-brand-500 mr-1"></i> 도서 안심 배송지 입력</span>
                <div class="space-y-2">
                    <div class="grid grid-cols-2 gap-2">
                        <input type="text" id="photobook-recipient" placeholder="수령인 성함" class="border border-gray-200 rounded-xl p-2 outline-none focus:border-brand-500 bg-white">
                        <input type="text" id="photobook-phone" placeholder="연락처 (010-XXXX-XXXX)" class="border border-gray-200 rounded-xl p-2 outline-none focus:border-brand-500 bg-white">
                    </div>
                    <input type="text" id="photobook-address" placeholder="기본 주소 및 상세 주소를 입력하세요" class="w-full border border-gray-200 rounded-xl p-2.5 outline-none focus:border-brand-500 bg-white">
                </div>
            </div>

            <!-- 결제 금액 및 결제수단 -->
            <div class="flex justify-between items-center bg-brand-50/60 p-4 rounded-2xl border border-brand-100">
                <div>
                    <span class="block font-bold text-brand-800 text-xs">최종 제작 및 출판 견적</span>
                    <span class="text-[9px] text-gray-400">도서 인쇄 비용 + 기본 배송비 무료 혜택 적용</span>
                </div>
                <div class="text-right">
                    <span id="photobook-final-price" class="text-xl font-black text-brand-600 font-mono">18,000원</span>
                </div>
            </div>

            <!-- 결제 및 출판 시작 -->
            <button onclick="confirmPhotobookPublish()" class="w-full bg-brand-500 hover:bg-brand-600 text-white font-black py-4 rounded-2xl text-sm shadow-md transition-colors flex items-center justify-center gap-2 outline-none">
                <i class="fa-solid fa-print"></i> 세상에 단 하나뿐인 내 펫 포토북 인쇄 주문하기 🚚
            </button>
        </div>
    </div>
</div>

<!-- 돌봄 일정 추가 모달 -->
<div id="care-schedule-modal" class="fixed inset-0 bg-black/60 items-center justify-center z-[110] p-4 hidden">
    <div class="bg-white rounded-3xl p-5 max-w-md w-full shadow-2xl relative border border-sky-100 max-h-[90vh] overflow-y-auto no-scrollbar">
        <button onclick="closeCareScheduleModal()" class="absolute top-4 right-4 text-gray-400 hover:text-gray-600 outline-none" aria-label="닫기"><i class="fa-solid fa-xmark text-lg"></i></button>
        <h4 class="font-black text-gray-800 text-sm flex items-center border-b pb-3 mb-4">
            <i class="fa-solid fa-calendar-plus text-sky-500 mr-2"></i>돌봄 일정 추가
        </h4>

        <div class="space-y-3 text-xs">
            <!-- 일정 타입 -->
            <div>
                <label class="block text-[10px] text-gray-400 font-bold mb-1.5">일정 유형</label>
                <div class="grid grid-cols-4 gap-2">
                    <button onclick="selectCareType('feed', this)" data-type="feed" class="care-type-btn flex flex-col items-center gap-1 py-2 rounded-xl border border-gray-200 bg-white hover:bg-sky-50 transition-all">
                        <span class="text-lg">🍖</span>
                        <span class="text-[9px] font-bold">식사</span>
                    </button>
                    <button onclick="selectCareType('water', this)" data-type="water" class="care-type-btn flex flex-col items-center gap-1 py-2 rounded-xl border border-gray-200 bg-white hover:bg-sky-50 transition-all">
                        <span class="text-lg">💧</span>
                        <span class="text-[9px] font-bold">음수</span>
                    </button>
                    <button onclick="selectCareType('walk', this)" data-type="walk" class="care-type-btn flex flex-col items-center gap-1 py-2 rounded-xl border border-gray-200 bg-white hover:bg-sky-50 transition-all">
                        <span class="text-lg">🚶</span>
                        <span class="text-[9px] font-bold">산책</span>
                    </button>
                    <button onclick="selectCareType('medicine', this)" data-type="medicine" class="care-type-btn flex flex-col items-center gap-1 py-2 rounded-xl border border-gray-200 bg-white hover:bg-sky-50 transition-all">
                        <span class="text-lg">💊</span>
                        <span class="text-[9px] font-bold">투약</span>
                    </button>
                </div>
                <div class="grid grid-cols-3 gap-2 mt-2">
                    <button onclick="selectCareType('vet', this)" data-type="vet" class="care-type-btn flex flex-col items-center gap-1 py-2 rounded-xl border border-gray-200 bg-white hover:bg-sky-50 transition-all">
                        <span class="text-lg">🏥</span>
                        <span class="text-[9px] font-bold">병원</span>
                    </button>
                    <button onclick="selectCareType('groom', this)" data-type="groom" class="care-type-btn flex flex-col items-center gap-1 py-2 rounded-xl border border-gray-200 bg-white hover:bg-sky-50 transition-all">
                        <span class="text-lg">✂️</span>
                        <span class="text-[9px] font-bold">미용</span>
                    </button>
                    <button onclick="selectCareType('play', this)" data-type="play" class="care-type-btn flex flex-col items-center gap-1 py-2 rounded-xl border border-gray-200 bg-white hover:bg-sky-50 transition-all">
                        <span class="text-lg">🎾</span>
                        <span class="text-[9px] font-bold">놀이</span>
                    </button>
                </div>
            </div>

            <!-- 일정 제목 -->
            <div>
                <label class="block text-[10px] text-gray-400 font-bold mb-1.5">일정 제목</label>
                <input id="care-schedule-title" type="text" placeholder="예: 아침 식사, 저녁 산책" class="w-full px-3 py-2.5 border border-gray-200 rounded-xl text-xs outline-none focus:border-sky-400 transition-colors">
            </div>

            <!-- 시간 -->
            <div>
                <label class="block text-[10px] text-gray-400 font-bold mb-1.5">시간</label>
                <input id="care-schedule-time" type="time" class="w-full px-3 py-2.5 border border-gray-200 rounded-xl text-xs outline-none focus:border-sky-400 transition-colors">
            </div>

            <!-- 반복 설정 -->
            <div>
                <label class="block text-[10px] text-gray-400 font-bold mb-1.5">반복</label>
                <select id="care-schedule-repeat" onchange="toggleRepeatDays()" class="w-full px-3 py-2.5 border border-gray-200 rounded-xl text-xs outline-none focus:border-sky-400 transition-colors">
                    <option value="daily">매일</option>
                    <option value="weekly">매주 (요일 선택)</option>
                    <option value="once">한 번만</option>
                </select>
            </div>

            <!-- 요일 선택 (주간 반복 시) -->
            <div id="care-schedule-repeat-days" class="hidden">
                <label class="block text-[10px] text-gray-400 font-bold mb-1.5">반복 요일</label>
                <div class="flex gap-1.5">
                    <button onclick="toggleRepeatDay(0, this)" data-day="0" class="repeat-day-btn flex-1 py-2 rounded-lg border border-gray-200 bg-white text-[10px] font-bold hover:bg-sky-50 transition-all">일</button>
                    <button onclick="toggleRepeatDay(1, this)" data-day="1" class="repeat-day-btn flex-1 py-2 rounded-lg border border-gray-200 bg-white text-[10px] font-bold hover:bg-sky-50 transition-all">월</button>
                    <button onclick="toggleRepeatDay(2, this)" data-day="2" class="repeat-day-btn flex-1 py-2 rounded-lg border border-gray-200 bg-white text-[10px] font-bold hover:bg-sky-50 transition-all">화</button>
                    <button onclick="toggleRepeatDay(3, this)" data-day="3" class="repeat-day-btn flex-1 py-2 rounded-lg border border-gray-200 bg-white text-[10px] font-bold hover:bg-sky-50 transition-all">수</button>
                    <button onclick="toggleRepeatDay(4, this)" data-day="4" class="repeat-day-btn flex-1 py-2 rounded-lg border border-gray-200 bg-white text-[10px] font-bold hover:bg-sky-50 transition-all">목</button>
                    <button onclick="toggleRepeatDay(5, this)" data-day="5" class="repeat-day-btn flex-1 py-2 rounded-lg border border-gray-200 bg-white text-[10px] font-bold hover:bg-sky-50 transition-all">금</button>
                    <button onclick="toggleRepeatDay(6, this)" data-day="6" class="repeat-day-btn flex-1 py-2 rounded-lg border border-gray-200 bg-white text-[10px] font-bold hover:bg-sky-50 transition-all">토</button>
                </div>
            </div>

            <!-- 날짜 (한 번만 선택 시) -->
            <div id="care-schedule-date-field" class="hidden">
                <label class="block text-[10px] text-gray-400 font-bold mb-1.5">날짜</label>
                <input id="care-schedule-date" type="date" class="w-full px-3 py-2.5 border border-gray-200 rounded-xl text-xs outline-none focus:border-sky-400 transition-colors">
            </div>

            <!-- 처방약 리필 카운트다운 (투약 타입 선택 시) -->
            <div id="care-schedule-refill-field" class="hidden">
                <label class="block text-[10px] text-gray-400 font-bold mb-1.5">💊 처방약 리필 (선택)</label>
                <div class="flex gap-1.5">
                    <input id="care-schedule-pill-total" type="number" min="0" step="1" placeholder="총 수량(정)" class="flex-1 px-3 py-2.5 border border-gray-200 rounded-xl text-xs outline-none focus:border-sky-400 transition-colors">
                    <input id="care-schedule-dose-per-day" type="number" min="0" step="0.5" placeholder="1일 투여 횟수" class="flex-1 px-3 py-2.5 border border-gray-200 rounded-xl text-xs outline-none focus:border-sky-400 transition-colors">
                </div>
                <p class="text-[9px] text-gray-400 mt-1">남은 약이 소진될 시점을 자동 계산해 재처방 알림을 띄워요</p>
            </div>

            <!-- 메모 -->
            <div>
                <label class="block text-[10px] text-gray-400 font-bold mb-1.5">메모 (선택)</label>
                <textarea id="care-schedule-notes" rows="2" placeholder="세부 사항을 입력하세요" class="w-full px-3 py-2.5 border border-gray-200 rounded-xl text-xs outline-none focus:border-sky-400 transition-colors resize-none"></textarea>
            </div>
        </div>

        <button onclick="submitCareSchedule()" class="w-full bg-sky-500 hover:bg-sky-600 text-white font-bold py-3.5 rounded-2xl text-xs shadow-md transition-colors flex items-center justify-center gap-2 outline-none mt-4">
            <i class="fa-solid fa-check"></i> 일정 추가하기
        </button>
    </div>
</div>

<!-- AI 수의사 채팅 모달 -->
<div id="vet-chat-modal" class="fixed inset-0 z-[110] hidden items-end justify-center bg-black/50">
    <div class="bg-white rounded-t-3xl w-full max-w-lg p-4 h-[80vh] flex flex-col shadow-2xl">
        <div class="flex justify-between items-center mb-3 pb-3 border-b border-gray-100">
            <div class="flex items-center gap-2">
                <div class="w-8 h-8 bg-emerald-100 rounded-full flex items-center justify-center text-lg">🏥</div>
                <div>
                    <h3 class="font-black text-gray-800 text-sm">AI 수의사 상담</h3>
                    <p class="text-[10px] text-emerald-600 font-medium">Gemini 기반 · 참고용 상담</p>
                </div>
            </div>
            <button onclick="closeVetChatModal()" class="w-8 h-8 flex items-center justify-center rounded-full bg-gray-100 hover:bg-gray-200 text-gray-500 font-bold transition-colors outline-none">✕</button>
        </div>
        <div id="vet-chat-messages" class="flex-1 overflow-y-auto space-y-2 mb-3 no-scrollbar"></div>
        <div class="flex gap-2">
            <input id="vet-chat-input" type="text" placeholder="증상이나 질문을 입력하세요..." class="flex-1 border border-gray-200 rounded-full px-4 py-2 text-sm outline-none focus:border-emerald-400 transition-colors" onkeydown="handleVetChatKeydown(event)" />
            <button onclick="sendVetChatMessage(document.getElementById('vet-chat-input').value)" class="bg-brand-500 hover:bg-brand-600 text-white rounded-full px-4 py-2 text-sm font-bold transition-colors outline-none">전송</button>
        </div>
        <p class="text-[9px] text-gray-400 text-center mt-2">※ AI 상담은 참고용이며 의학적 진단이 아닙니다</p>
    </div>
</div>
`;

