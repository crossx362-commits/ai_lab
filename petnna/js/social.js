let activeSocialSubTab = 'feed'; // 자랑 피드가 기본 활성 서브탭

// 피드 작성 관련 전역 상태 병합
let attachedPhotoUrl = "";
let attachedVideoUrl = "";
let attachedWalkData = null;
let attachedHealthData = null;
let trimStart = 0;
let trimEnd = 5;

// 이웃 답장 대기 전역 상태
let activeReplyNotificationId = null;

// 1. 소셜 & 피드 메인 렌더링 진입점
function renderSocialRoom() {
    const reqList = document.getElementById('friend-requests-list');
    const reqCount = document.getElementById('req-count');
    const friendsList = document.getElementById('friends-list');

    // 친구 신청 목록 렌더링
    if (reqList && reqCount) {
        reqList.innerHTML = '';
        reqCount.innerText = friendRequests.length;
        if (friendRequests.length === 0) {
            reqList.innerHTML = `<div class="text-[10px] text-gray-400 text-center py-2">대기중인 신청건이 없습니다.</div>`;
        } else {
            friendRequests.forEach(r => {
                const el = document.createElement('div');
                el.className = "p-2.5 bg-brand-50/40 border border-brand-100/50 rounded-2xl flex items-center justify-between gap-2";
                el.innerHTML = `
                    <div class="flex items-center space-x-2 min-w-0">
                        <img loading="lazy" src="${r.avatar}" class="w-7 h-7 object-cover rounded-full border border-amber-100" onerror="this.src='https://placehold.co/100/fbeee0/732f18?text=${escapeHtml(r.nickname)}'">
                        <div class="min-w-0 text-[10px]">
                            <span class="font-bold text-gray-700 block truncate">${escapeHtml(r.nickname)}</span>
                            <span class="text-gray-400 block truncate">${escapeHtml(r.petName)} (${escapeHtml(r.petBreed)})</span>
                        </div>
                    </div>
                    <div class="flex space-x-1 shrink-0">
                        <button onclick="acceptFriend(${r.id})" class="bg-brand-500 hover:bg-brand-600 text-white font-bold text-[9px] px-2 py-1 rounded-lg transition-colors">수락</button>
                        <button onclick="rejectFriend(${r.id})" class="bg-gray-100 hover:bg-gray-200 text-gray-600 font-bold text-[9px] px-2 py-1 rounded-lg transition-colors">거절</button>
                    </div>
                `;
                reqList.appendChild(el);
            });
        }
    }

    // 대화 집사 목록 렌더링
    if (friendsList) {
        friendsList.innerHTML = '';
        if (friends.length === 0) {
            friendsList.innerHTML = `<div class="text-xs text-gray-400 text-center py-6">친구가 없습니다. 이웃을 탐색해보세요!</div>`;
        } else {
            friends.forEach(f => {
                const isActive = (f.id === activeChatFriendId);
                const isBlocked = typeof isBlockedByIdOrNickname === 'function' ? isBlockedByIdOrNickname(f) : false;
                const stateColor = f.status === 'online' ? 'bg-green-500' : 'bg-gray-400';
                
                let activeStyle;
                if (isBlocked) {
                    activeStyle = isActive ? 'bg-gray-100 border-gray-300 opacity-60' : 'hover:bg-gray-50 border-transparent opacity-50';
                } else {
                    activeStyle = isActive ? 'bg-brand-50 border-brand-200 shadow-sm' : 'hover:bg-gray-50 border-transparent';
                }

                const el = document.createElement('div');
                el.className = `p-3 rounded-2xl border-2 cursor-pointer flex items-center justify-between transition-all ${activeStyle}`;
                el.onclick = () => selectActiveChatFriend(f.id);

                el.innerHTML = `
                    <div class="flex items-center space-x-3 min-w-0">
                        <div class="relative shrink-0">
                            <img loading="lazy" src="${f.avatar}" class="w-9 h-9 object-cover rounded-full border border-amber-100 shadow-sm" onerror="this.src='https://placehold.co/100/fbeee0/732f18?text=${f.nickname}'">
                            <span class="absolute bottom-0 right-0 w-2.5 h-2.5 rounded-full border-2 border-white ${stateColor}"></span>
                        </div>
                        <div class="min-w-0 text-xs">
                            <span class="font-black text-gray-800 block truncate flex items-center gap-1">
                                ${f.nickname} <span class="font-medium text-gray-400 text-[10px]">(${f.petName})</span>
                                ${isBlocked ? `<span class="text-[8px] text-rose-500 font-extrabold bg-rose-50 border border-rose-100 rounded px-1 shrink-0">🚫 차단됨</span>` : ''}
                            </span>
                            <span class="text-gray-400 block truncate text-[10px] mt-0.5">${f.personality}</span>
                        </div>
                    </div>
                    <div class="flex flex-col items-end shrink-0 gap-1 text-[9px]">
                        <span class="bg-indigo-50 text-indigo-700 font-black px-1.5 py-0.5 rounded-full">성향 조화 ${f.chemistry}%</span>
                        ${f.unread > 0 && !isBlocked ? `<span class="bg-red-500 text-white font-bold w-4 h-4 rounded-full flex items-center justify-center text-[8px] animate-bounce">${f.unread}</span>` : ''}
                    </div>
                `;
                friendsList.appendChild(el);
            });
        }
    }

    // 중앙 서브탭 동기화 및 렌더링
    switchSocialSubTab(activeSocialSubTab);

    // 우측 나를 좋아하는 이웃 알림 렌더링
    renderLikeNotifications();
}

// 2. 중앙 서브탭 스위칭 매니저
function switchSocialSubTab(subTab) {
    activeSocialSubTab = subTab;

    const feedBtn = document.getElementById('social-subtab-feed-btn');
    const chatBtn = document.getElementById('social-subtab-chat-btn');
    const mailboxBtn = document.getElementById('social-subtab-mailbox-btn');
    const feedContent = document.getElementById('social-subtab-feed-content');
    const chatContent = document.getElementById('social-subtab-chat-content');
    const mailboxContent = document.getElementById('social-subtab-mailbox-content');

    if (feedBtn && chatBtn && feedContent && chatContent) {
        // 모든 컨텐츠 영역 숨김
        feedContent.classList.add('hidden');
        chatContent.classList.add('hidden');
        if (mailboxContent) mailboxContent.classList.add('hidden');

        // 비활성 상태 버튼 스타일 일괄 적용
        const inactiveStyle = "flex-grow flex items-center justify-center gap-1.5 py-2 px-4 rounded-xl text-xs font-bold transition-all text-gray-500 hover:text-brand-500 hover:bg-brand-50/20";
        const activeStyle = "flex-grow flex items-center justify-center gap-1.5 py-2 px-4 rounded-xl text-xs font-black transition-all text-brand-600 bg-brand-50 border border-brand-200/50 shadow-sm";

        feedBtn.className = inactiveStyle;
        chatBtn.className = inactiveStyle;
        if (mailboxBtn) mailboxBtn.className = inactiveStyle;

        if (subTab === 'feed') {
            feedContent.classList.remove('hidden');
            feedBtn.className = activeStyle;
            renderFeed();
        } else if (subTab === 'chat') {
            chatContent.classList.remove('hidden');
            chatBtn.className = activeStyle;
            renderChatHistoryWindow();
        } else if (subTab === 'mailbox') {
            if (mailboxContent) mailboxContent.classList.remove('hidden');
            if (mailboxBtn) mailboxBtn.className = activeStyle;
            if (typeof renderMailbox === 'function') renderMailbox();
        }
    }
}

// 3. 자랑 피드 타임라인 렌더러
function renderFeed() {
    const feedContainer = document.getElementById('feed-list');
    if (!feedContainer) return;

    feedContainer.innerHTML = '';

    const blockedList = typeof getBlockedNeighbors === 'function' ? getBlockedNeighbors() : [];
    const visiblePosts = posts.filter(post => !blockedList.some(b => b.petName === post.petName));

    if (visiblePosts.length === 0) {
        feedContainer.innerHTML = `<div class="bg-white rounded-3xl p-8 border border-amber-50 text-center text-gray-400 text-xs">피드가 비어있습니다. 첫 소식을 업로드해 보세요! 📢</div>`;
        return;
    }

    visiblePosts.forEach(post => {
        const card = document.createElement('div');
        card.className = "bg-white rounded-3xl p-5 border border-amber-50 shadow-sm space-y-4";

        let header = `
            <div class="flex justify-between items-center">
                <div class="flex items-center space-x-2.5 cursor-pointer hover:opacity-85 transition-opacity" onclick="showOwnerProfile('${post.petName}', '${post.petAvatar}')" title="이웃집사 프로필 보기">
                    <img loading="lazy" src="${post.petAvatar}" class="w-9 h-9 object-cover rounded-full border border-amber-100 shadow-sm" onerror="this.src='https://placehold.co/100/fbeee0/732f18?text=${post.petName}'">
                    <div>
                        <span class="font-black text-xs text-gray-800 block flex items-center gap-1">${post.petName} <span class="bg-brand-550/10 text-[8px] text-brand-600 px-1 py-0.2 rounded font-extrabold border border-brand-200/50">프로필</span></span>
                        <span class="text-[9px] text-gray-400 block">이웃 반려 생활가</span>
                    </div>
                </div>
                <div class="relative">
                    <button onclick="togglePostMenu(event, ${post.id})" class="text-gray-300 hover:text-brand-500 transition-all p-1 text-xs"><i class="fa-solid fa-ellipsis-v"></i></button>
                    <div id="post-menu-${post.id}" class="hidden absolute right-0 top-6 bg-white border border-gray-200 rounded-xl shadow-lg py-1.5 w-20 z-30 text-[9px] font-bold text-center">
                        <button onclick="editFeedPost(${post.id})" class="w-full text-left px-2.5 py-1 hover:bg-gray-50 text-gray-700 flex items-center gap-1"><i class="fa-solid fa-pen-to-square"></i> 수정</button>
                        <button onclick="deleteFeedPost(${post.id})" class="w-full text-left px-2.5 py-1 hover:bg-gray-50 text-rose-600 flex items-center gap-1"><i class="fa-solid fa-trash-can"></i> 삭제</button>
                    </div>
                </div>
            </div>
            <p class="text-xs text-gray-600 leading-relaxed whitespace-pre-wrap">${escapeHtml(post.content)}</p>
        `;

        const filterVal = typeof getFilterCSSValue === 'function' ? getFilterCSSValue(post.filter || 'natural') : 'none';
        let stickersHtml = '';
        if (post.stickers && post.stickers.length > 0) {
            post.stickers.forEach(st => {
                let itemStyle = `position: absolute; left: ${st.left}%; top: ${st.top}%; z-index: ${st.zIndex || 20}; transform: translate(-50%, -50%) scale(${st.scale * 0.45}) rotate(${st.rotate}deg); pointer-events: none; transform-origin: center center;`;
                if (st.type === "emoji") {
                    stickersHtml += `<div style="${itemStyle} font-size: 14px;">${st.content}</div>`;
                } else {
                    stickersHtml += `<div class="p-0.5 rounded px-1 text-[5px] font-black leading-tight border ${st.bubbleTheme || 'bg-white/95 text-brand-700 border-amber-200/60'} shadow-sm" style="${itemStyle}">💬 ${st.content}</div>`;
                }
            });
        }

        let mediaView = '';
        if (post.items && post.items.length > 1) {
            let carouselSlides = '';
            post.items.forEach((item, itemIdx) => {
                const itemFilterVal = typeof getFilterCSSValue === 'function' ? getFilterCSSValue(item.filter || 'natural') : 'none';
                let itemStickersHtml = '';
                if (item.stickers && item.stickers.length > 0) {
                    item.stickers.forEach(st => {
                        let itemStyle = `position: absolute; left: ${st.left}%; top: ${st.top}%; z-index: ${st.zIndex || 20}; transform: translate(-50%, -50%) scale(${st.scale * 0.45}) rotate(${st.rotate}deg); pointer-events: none; transform-origin: center center;`;
                        if (st.type === "emoji") {
                            itemStickersHtml += `<div style="${itemStyle} font-size: 14px;">${st.content}</div>`;
                        } else {
                            itemStickersHtml += `<div class="p-0.5 rounded px-1 text-[5px] font-black leading-tight border ${st.bubbleTheme || 'bg-white/95 text-brand-700 border-amber-200/60'} shadow-sm" style="${itemStyle}">💬 ${st.content}</div>`;
                        }
                    });
                }

                let slideMedia = '';
                if (item.isVideo && item.videoUrl) {
                    slideMedia = `
                        <div class="relative w-full aspect-video bg-gray-900 overflow-hidden flex-shrink-0 snap-center">
                            <video id="post-video-${post.id}-${itemIdx}" src="${item.videoUrl}" style="filter: ${itemFilterVal};" class="w-full h-full object-cover" loop muted playsinline></video>
                            <div class="absolute inset-0 bg-black/20 flex items-center justify-center pointer-events-none" id="play-btn-overlay-${post.id}-${itemIdx}">
                                <button onclick="toggleCarouselVideoPlay('${post.id}-${itemIdx}')" class="pointer-events-auto bg-white/95 text-brand-600 w-10 h-10 rounded-full flex items-center justify-center shadow-lg hover:scale-110 transition-transform"><i id="play-icon-${post.id}-${itemIdx}" class="fa-solid fa-play text-xs pl-0.5"></i></button>
                            </div>
                            <span class="absolute bottom-2 left-2 bg-black/60 text-white font-mono text-[8px] px-1.5 py-0.5 rounded font-bold z-20">Trim: ${item.videoStart}s - ${item.videoEnd}s</span>
                            <div class="absolute inset-0 pointer-events-none z-10 overflow-hidden">
                                ${itemStickersHtml}
                            </div>
                        </div>
                    `;
                } else {
                    slideMedia = `
                        <div class="relative w-full aspect-video bg-gray-100 overflow-hidden flex-shrink-0 snap-center">
                            <img loading="lazy" src="${item.url}" style="filter: ${itemFilterVal};" class="w-full h-full object-cover">
                            <div class="absolute inset-0 pointer-events-none z-10 overflow-hidden">
                                ${itemStickersHtml}
                            </div>
                        </div>
                    `;
                }
                carouselSlides += slideMedia;
            });

            mediaView = `
                <div class="relative w-full aspect-video bg-gray-900 rounded-2xl overflow-hidden shadow-sm group">
                    <div id="carousel-${post.id}" class="w-full h-full flex overflow-x-auto snap-x snap-mandatory scroll-smooth no-scrollbar" onscroll="updateCarouselIndicators(${post.id})">
                        ${carouselSlides}
                    </div>
                    <button onclick="scrollCarousel(${post.id}, -1)" class="absolute left-2.5 top-1/2 -translate-y-1/2 bg-black/45 hover:bg-black/60 text-white w-7 h-7 rounded-full flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity z-20 text-xs shadow-md"><i class="fa-solid fa-chevron-left"></i></button>
                    <button onclick="scrollCarousel(${post.id}, 1)" class="absolute right-2.5 top-1/2 -translate-y-1/2 bg-black/45 hover:bg-black/60 text-white w-7 h-7 rounded-full flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity z-20 text-xs shadow-md"><i class="fa-solid fa-chevron-right"></i></button>
                    <div class="absolute top-3 right-3 bg-black/65 text-white text-[9px] font-black py-0.5 px-2 rounded-full z-20 shadow-sm tracking-wider">
                        <span id="indicator-cur-${post.id}">1</span> / ${post.items.length} 장 📸
                    </div>
                </div>
            `;
        } else if (post.isVideo && post.videoUrl) {
            mediaView = `
                <div class="relative w-full aspect-video bg-gray-900 rounded-2xl overflow-hidden shadow-inner border border-gray-800">
                    <video id="post-video-${post.id}" src="${post.videoUrl}" style="filter: ${filterVal};" class="w-full h-full object-cover" loop muted playsinline></video>
                    <div class="absolute inset-0 bg-black/20 flex items-center justify-center pointer-events-none" id="play-btn-overlay-${post.id}">
                        <button onclick="togglePostVideoPlay(${post.id})" class="pointer-events-auto bg-white/95 text-brand-600 w-12 h-12 rounded-full flex items-center justify-center shadow-lg hover:scale-110 transition-transform"><i id="play-icon-${post.id}" class="fa-solid fa-play text-lg pl-0.5"></i></button>
                    </div>
                    <span class="absolute bottom-2 left-2 bg-black/60 text-white font-mono text-[9px] px-2 py-0.5 rounded font-bold z-20">Trim: ${post.videoStart}s - ${post.videoEnd}s</span>
                    <div class="absolute inset-0 pointer-events-none z-10 overflow-hidden">
                        ${stickersHtml}
                    </div>
                </div>
            `;
        } else if (post.image) {
            mediaView = `
                <div class="relative w-full aspect-video bg-gray-100 rounded-2xl overflow-hidden shadow-sm">
                    <img loading="lazy" src="${post.image}" style="filter: ${filterVal};" class="w-full h-full object-cover">
                    <div class="absolute inset-0 pointer-events-none z-10 overflow-hidden">
                        ${stickersHtml}
                    </div>
                </div>
            `;
        }

        let walkAttachedCard = '';
        if (post.attachedWalk) {
            walkAttachedCard = `
                <div onclick="openWalkTrailModal(${post.id})" class="cursor-pointer bg-indigo-50/50 hover:bg-indigo-100/50 p-3.5 border border-indigo-100 rounded-2xl flex justify-between items-center transition-all">
                    <div class="space-y-0.5 flex-grow">
                        <span class="block text-[8px] text-indigo-500 font-bold uppercase tracking-wider flex items-center gap-1">
                            <span>이웃이 추천한 실제 산책로</span>
                            <span class="bg-indigo-200/50 text-indigo-800 text-[7px] font-black px-1.5 py-0.2 rounded-full">클릭 시 미리보기 🔍</span>
                        </span>
                        <h5 class="font-black text-gray-800 text-xs">🐾 안심 트랙 코스 (${post.attachedWalk.distance} km)</h5>
                        <span class="block text-[9px] text-gray-400">⏱️ 주행 완료 타임: ${post.attachedWalk.duration} / 마킹: ${post.attachedWalk.poop}💩 ${post.attachedWalk.pee}💦</span>
                    </div>
                    <button onclick="event.stopPropagation(); loadFeedWalkOnMap(${post.id})" class="bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-[10px] py-1.5 px-3.5 rounded-xl shadow transition-all shrink-0">
                        <i class="fa-solid fa-map-location-dot mr-1"></i> 전체지도 이동
                    </button>
                </div>
            `;
        }

        let healthAttachedCard = '';
        if (post.attachedHealth) {
            const poopIcons = { 'null': '-', 'normal': '💩 (쾌변)', 'hard': '🪨 (단단)', 'liquid': '💦 (무름)' };
            const poopDisplay = poopIcons[post.attachedHealth.poop] || '-';
            healthAttachedCard = `
                <div class="bg-teal-50/40 p-3 border border-teal-100 rounded-2xl transition-all shadow-sm">
                    <div class="cursor-pointer" onclick="showOwnerProfile('${post.petName}', '${post.petAvatar}')">
                        <span class="block text-[8px] text-teal-600 font-bold uppercase tracking-wider mb-2 flex items-center gap-1">
                            <i class="fa-solid fa-notes-medical"></i> <span>오늘의 건강 리포트 공유됨</span>
                            <span class="bg-teal-100/60 text-teal-800 text-[7px] font-black px-1.5 py-0.2 rounded-full">프로필 보기</span>
                        </span>
                        <div class="flex gap-2">
                            <div class="text-center bg-white p-2 rounded-xl border border-teal-50 shadow-sm flex-1">
                                <span class="block text-[9px] text-gray-400 font-bold mb-0.5">배변</span>
                                <span class="text-xs font-black text-gray-700">${poopDisplay}</span>
                            </div>
                            <div class="text-center bg-white p-2 rounded-xl border border-teal-50 shadow-sm flex-1">
                                <span class="block text-[9px] text-gray-400 font-bold mb-0.5">식사량</span>
                                <span class="text-xs font-black text-amber-500">${post.attachedHealth.food || 0}g</span>
                            </div>
                            <div class="text-center bg-white p-2 rounded-xl border border-teal-50 shadow-sm flex-1">
                                <span class="block text-[9px] text-gray-400 font-bold mb-0.5">음수량</span>
                                <span class="text-xs font-black text-blue-500">${post.attachedHealth.water || 0}ml</span>
                            </div>
                        </div>
                    </div>
                </div>
            `;
        }

        let aiHealthAttachedCard = '';
        if (post.attachedAiHealth) {
            const h = post.attachedAiHealth;
            const scoreColor = h.score >= 80 ? '#10b981' : h.score >= 60 ? '#f59e0b' : '#ef4444';
            const badgeClass = (val) => val === '정상' ? 'bg-emerald-100 text-emerald-700' : val === '주의' ? 'bg-amber-100 text-amber-700' : 'bg-red-100 text-red-700';
            aiHealthAttachedCard = `
                <div class="bg-violet-50/40 p-3 border border-violet-100 rounded-2xl shadow-sm space-y-2">
                    <span class="block text-[8px] text-violet-600 font-bold uppercase tracking-wider flex items-center gap-1">
                        <i class="fa-solid fa-microscope"></i> AI 건강 분석 결과 공유됨
                    </span>
                    <div class="flex items-center gap-3">
                        <div class="text-center min-w-[44px]">
                            <span class="block text-xl font-black" style="color:${scoreColor}">${h.score}</span>
                            <span class="text-[8px] text-gray-400 font-bold">건강점수</span>
                        </div>
                        <div class="flex-1 space-y-1">
                            <div class="flex gap-1 flex-wrap">
                                <span class="text-[9px] px-1.5 py-0.5 rounded-full font-black ${badgeClass(h.eyes)}">눈 ${escapeHtml(h.eyes)}</span>
                                <span class="text-[9px] px-1.5 py-0.5 rounded-full font-black ${badgeClass(h.skin)}">피부 ${escapeHtml(h.skin)}</span>
                                <span class="text-[9px] px-1.5 py-0.5 rounded-full font-black ${badgeClass(h.body)}">체형 ${escapeHtml(h.body)}</span>
                            </div>
                            <p class="text-[10px] text-gray-600 leading-snug">${escapeHtml(h.summary || '')}</p>
                        </div>
                    </div>
                </div>
            `;
        }

        let actions = `
            <div class="flex items-center space-x-4 pt-3 border-t text-xs font-bold text-gray-500">
                <button onclick="togglePostLike(${post.id})" class="flex items-center space-x-1 hover:text-rose-500 transition-colors ${post.liked ? 'text-rose-500' : ''}">
                    <i class="${post.liked ? 'fa-solid' : 'fa-regular'} fa-heart"></i>
                    <span class="font-mono text-[10px]">${post.likes}</span>
                </button>
                <button onclick="focusCommentInput(${post.id})" class="flex items-center space-x-1 hover:text-brand-500 transition-colors">
                    <i class="fa-regular fa-comment-dots"></i>
                    <span class="font-mono text-[10px]">${post.comments ? post.comments.length : 0}</span>
                </button>
            </div>
        `;

        let commentsListHtml = '';
        if (post.comments && post.comments.length > 0) {
            commentsListHtml = `
                <div class="space-y-2 bg-gray-50/50 p-3 rounded-2xl text-[11px] border border-gray-100/30">
                    ${post.comments.map(c => `
                        <div class="flex justify-between">
                            <span class="font-bold text-gray-700 shrink-0 mr-2">👤 ${c.author}</span>
                            <span class="text-gray-500 leading-normal flex-grow">${escapeHtml(c.text)}</span>
                        </div>
                    `).join('')}
                </div>
            `;
        }

        let commentForm = `
            <div class="flex items-center space-x-2">
                <input type="text" id="comment-input-${post.id}" placeholder="사랑스런 댓글을 달아 소통하세요..." class="flex-grow border border-gray-200 rounded-xl px-3 py-2 text-[11px] outline-none focus:border-brand-500 bg-gray-50/20" onkeydown="if(event.key === 'Enter') submitFeedComment(${post.id})">
                <button onclick="submitFeedComment(${post.id})" class="bg-brand-500 hover:bg-brand-600 text-white font-bold text-[10px] py-2 px-3.5 rounded-xl transition-colors shrink-0">
                    등록
                </button>
            </div>
        `;

        card.innerHTML = header + mediaView + walkAttachedCard + healthAttachedCard + aiHealthAttachedCard + actions + commentsListHtml + commentForm;
        feedContainer.appendChild(card);
    });
}

// 4. 자랑 피드 액션 핸들러들
function togglePostLike(postId) {
    const p = posts.find(item => item.id === postId);
    if (!p) return;

    if (p.liked) {
        p.likes = Math.max(0, p.likes - 1);
        p.liked = false;
    } else {
        p.likes++;
        p.liked = true;
    }

    saveState();
    renderFeed();

    if (typeof updatePostLikesInSupabase === 'function') {
        updatePostLikesInSupabase(postId, p.likes);
    }
}

function focusCommentInput(postId) {
    const input = document.getElementById(`comment-input-${postId}`);
    if (input) input.focus();
}

function submitFeedComment(postId) {
    const input = document.getElementById(`comment-input-${postId}`);
    if (!input || !input.value.trim()) return;

    const text = input.value.trim();
    input.value = '';

    const p = posts.find(item => item.id === postId);
    if (!p) return;

    if (!p.comments) p.comments = [];
    p.comments.push({
        author: settings_nickname,
        text: text
    });

    saveState();
    renderFeed();
    showToast("따뜻한 이웃 덧글이 등록되었습니다!");

    if (typeof updatePostCommentsInSupabase === 'function') {
        updatePostCommentsInSupabase(postId, p.comments);
    }
}

function togglePostMenu(event, postId) {
    event.stopPropagation();
    
    // Close other post menus
    const allMenus = document.querySelectorAll('[id^="post-menu-"]');
    allMenus.forEach(m => {
        if (m.id !== `post-menu-${postId}`) {
            m.classList.add('hidden');
        }
    });

    const menu = document.getElementById(`post-menu-${postId}`);
    if (menu) {
        menu.classList.toggle('hidden');
    }
}

// Close menus when clicking outside
document.addEventListener('click', () => {
    const allMenus = document.querySelectorAll('[id^="post-menu-"]');
    allMenus.forEach(m => m.classList.add('hidden'));
});

let editingPostId = null;
let editingAttachedPhotoUrl = "";
let editingAttachedVideoUrl = "";
let editingIsVideo = false;

function editFeedPost(postId) {
    const post = posts.find(p => p.id === postId);
    if (!post) return;
    
    editingPostId = postId;
    editingAttachedPhotoUrl = post.image || "";
    editingAttachedVideoUrl = post.videoUrl || "";
    editingIsVideo = post.isVideo || false;

    // 모달 필드 채우기
    const contentTextarea = document.getElementById('feed-edit-content');
    if (contentTextarea) {
        contentTextarea.value = post.content || "";
    }

    updateFeedEditMediaPreview();

    // 모달 보이기
    const modal = document.getElementById('feed-edit-modal');
    if (modal) {
        modal.classList.remove('hidden');
        modal.classList.add('flex');
    }
}

function closeFeedEditModal() {
    const modal = document.getElementById('feed-edit-modal');
    if (modal) {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }
    editingPostId = null;
    editingAttachedPhotoUrl = "";
    editingAttachedVideoUrl = "";
    editingIsVideo = false;
}

function updateFeedEditMediaPreview() {
    const photoPreview = document.getElementById('feed-edit-photo-preview');
    const videoPreview = document.getElementById('feed-edit-video-preview');
    const mediaPlaceholder = document.getElementById('feed-edit-media-placeholder');

    if (!photoPreview || !videoPreview || !mediaPlaceholder) return;

    photoPreview.classList.add('hidden');
    videoPreview.classList.add('hidden');
    mediaPlaceholder.classList.add('hidden');

    if (editingIsVideo && editingAttachedVideoUrl) {
        videoPreview.src = editingAttachedVideoUrl;
        videoPreview.classList.remove('hidden');
    } else if (!editingIsVideo && editingAttachedPhotoUrl) {
        photoPreview.src = editingAttachedPhotoUrl;
        photoPreview.classList.remove('hidden');
    } else {
        mediaPlaceholder.classList.remove('hidden');
    }
}

function triggerFeedEditPhotoUpload() {
    const fileInput = document.getElementById('feed-edit-photo-upload');
    if (fileInput) {
        fileInput.click();
    }
}

function handleFeedEditPhotoUpload(e) {
    const file = e.target.files[0];
    if (!file) return;

    showToast("사진을 준비하는 중... 📸");

    const reader = new FileReader();
    reader.onload = function (event) {
        setTimeout(() => {
            editingAttachedPhotoUrl = event.target.result;
            editingAttachedVideoUrl = "";
            editingIsVideo = false;
            updateFeedEditMediaPreview();
            showToast("수정할 자랑 사진이 업로드되었습니다! 📸");
        }, 300);
    };
    reader.readAsDataURL(file);
}

function selectEditPresetPhoto() {
    selectPostPresetPhoto(); // 기존 프리셋 선택 모달 열기
}

function clearFeedEditMedia() {
    editingAttachedPhotoUrl = "";
    editingAttachedVideoUrl = "";
    editingIsVideo = false;
    updateFeedEditMediaPreview();
    showToast("첨부된 사진/영상이 삭제 상태로 지정되었습니다. 🗑️");
}

function submitFeedEditPost() {
    if (editingPostId === null) return;
    
    const post = posts.find(p => p.id === editingPostId);
    if (!post) {
        closeFeedEditModal();
        return;
    }

    const contentTextarea = document.getElementById('feed-edit-content');
    const trimmed = contentTextarea ? contentTextarea.value.trim() : "";
    if (!trimmed) {
        showToast("본문 이야기를 작성해 주세요!");
        return;
    }

    post.content = trimmed;
    post.image = editingAttachedPhotoUrl || null;
    post.videoUrl = editingAttachedVideoUrl || null;
    post.isVideo = editingIsVideo;

    // 만약 사진도 비디오도 없으면
    if (!post.image && !post.videoUrl) {
        post.image = null;
        post.videoUrl = null;
        post.isVideo = false;
    }

    saveState();
    renderFeed();
    closeFeedEditModal();
    showToast("자랑글이 수정되었습니다. 🎉");

    // Supabase에 데이터 업데이트 전파
    if (typeof updatePostContentInSupabase === 'function') {
        try {
            updatePostContentInSupabase(post.id, trimmed, post.image, post.videoUrl, post.isVideo);
        } catch(e) {
            console.error("Supabase post content and media update failed:", e);
        }
    }
}

function deleteFeedPost(postId) {
    showCustomDialog({
        title: "자랑글 삭제 ⚠️",
        message: "정말 해당 이웃 공유 자랑글을 삭제하시겠습니까?",
        icon: "🗑️",
        type: "confirm",
        onConfirm: () => {
            posts = posts.filter(p => p.id !== postId);
            saveState();
            renderFeed();
            showToast("글이 삭제되었습니다.");

            if (typeof deletePostFromSupabase === 'function') {
                deletePostFromSupabase(postId);
            }
        }
    });
}

function togglePostVideoPlay(postId) {
    const video = document.getElementById(`post-video-${postId}`);
    const icon = document.getElementById(`play-icon-${postId}`);
    if (!video || !icon) return;

    if (video.paused) {
        video.play();
        icon.className = "fa-solid fa-pause text-lg";
    } else {
        video.pause();
        icon.className = "fa-solid fa-play text-lg pl-0.5";
    }
}

// 5. 자랑 발행 글쓰기 폼 핸들러
function selectPostPresetPhoto() {
    const modal = document.getElementById('feed-photo-modal');
    if (modal) {
        modal.classList.remove('hidden');
        modal.classList.add('flex');
    }
}

function closePostPresetPhotoModal() {
    const modal = document.getElementById('feed-photo-modal');
    if (modal) {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }
}

function confirmPostPhoto(url) {
    if (editingPostId !== null) {
        editingAttachedPhotoUrl = url;
        editingAttachedVideoUrl = "";
        editingIsVideo = false;
        updateFeedEditMediaPreview();
        closePostPresetPhotoModal();
        showToast("수정할 사진이 첨부되었습니다! 📸");
        return;
    }

    attachedPhotoUrl = url;
    attachedVideoUrl = "";
    document.getElementById('post-photo-indicator').classList.remove('hidden');
    document.getElementById('post-video-indicator').classList.add('hidden');
    document.getElementById('feed-video-trimmer').classList.add('hidden');

    const albumBg = document.getElementById('decorator-bg');
    const albumVideo = document.getElementById('decorator-bg-video');
    const albumPlaceholder = document.getElementById('decorator-placeholder');

    if (albumBg && albumVideo && albumPlaceholder) {
        albumBg.src = url;
        albumBg.classList.remove('hidden');
        albumVideo.classList.add('hidden');
        albumPlaceholder.classList.add('hidden');
    }

    closePostPresetPhotoModal();
    showToast("자랑하고 싶은 최애 사진이 첨부되었습니다!");
}

/**
 * 📸 소셜 자랑 피드 글쓰기 창에서 사용자가 본인의 기기 사진을 직접 업로드할 때의 이벤트 핸들러
 */
function handleFeedPhotoUpload(e) {
    const file = e.target.files[0];
    if (!file) return;

    // 업로드 중 레이어 활성화 및 미리보기 그릇 표시
    const container = document.getElementById('feed-photo-preview-container');
    const overlay = document.getElementById('feed-photo-uploading-overlay');
    if (container) {
        container.classList.remove('hidden');
        container.classList.add('block');
    }
    if (overlay) {
        overlay.classList.remove('hidden');
    }

    const reader = new FileReader();
    reader.onload = function (event) {
        // 실제 완료 처리에 750ms 시뮬레이션 지연 제공
        setTimeout(() => {
            attachedPhotoUrl = event.target.result;
            attachedVideoUrl = ""; // 비디오 첨부 해제
            
            const img = document.getElementById('feed-photo-preview');
            if (img) {
                img.src = attachedPhotoUrl;
            }

            // 업로드 중 오버레이 숨기기
            if (overlay) {
                overlay.classList.add('hidden');
            }

            // 인디케이터 배지 조절
            document.getElementById('post-photo-indicator').classList.remove('hidden');
            document.getElementById('post-video-indicator').classList.add('hidden');
            document.getElementById('feed-video-trimmer').classList.add('hidden');
            
            showToast("내 자랑 사진이 성공적으로 장착되었습니다! 📸");
        }, 750);
    };
    reader.readAsDataURL(file);
}

/**
 * 📸 직접 첨부한 사진 취소 및 상태 클리어
 */
function clearAttachedPhoto() {
    attachedPhotoUrl = "";
    
    const container = document.getElementById('feed-photo-preview-container');
    const img = document.getElementById('feed-photo-preview');
    if (container && img) {
        img.src = "";
        container.classList.remove('block');
        container.classList.add('hidden');
    }

    document.getElementById('post-photo-indicator').classList.add('hidden');
    
    const input = document.getElementById('feed-photo-upload');
    if (input) input.value = "";
    
    showToast("첨부된 사진이 해제되었습니다.");
}

function selectPostPresetVideo() {
    const modal = document.getElementById('feed-video-modal');
    if (modal) {
        modal.classList.remove('hidden');
        modal.classList.add('flex');
    }
}

function closePostPresetVideoModal() {
    const modal = document.getElementById('feed-video-modal');
    if (modal) {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }
}

function confirmPostVideo(url) {
    attachedVideoUrl = url;
    attachedPhotoUrl = "";
    document.getElementById('post-photo-indicator').classList.add('hidden');
    document.getElementById('post-video-indicator').classList.remove('hidden');

    const trimmer = document.getElementById('feed-video-trimmer');
    const preview = document.getElementById('feed-trim-preview');
    if (trimmer && preview) {
        trimmer.classList.remove('hidden');
        preview.src = url;
        preview.load();
        preview.play();

        preview.ontimeupdate = function () {
            if (this.currentTime < trimStart) {
                this.currentTime = trimStart;
            }
            if (this.currentTime > trimEnd) {
                this.currentTime = trimStart;
            }
        };
    }

    const albumBg = document.getElementById('decorator-bg');
    const albumVideo = document.getElementById('decorator-bg-video');
    const albumPlaceholder = document.getElementById('decorator-placeholder');
    const albumTrimmerBox = document.getElementById('video-trimmer-box');

    if (albumBg && albumVideo && albumPlaceholder) {
        albumBg.classList.add('hidden');
        albumVideo.src = url;
        albumVideo.classList.remove('hidden');
        albumPlaceholder.classList.add('hidden');

        if (albumTrimmerBox) albumTrimmerBox.classList.remove('hidden');

        albumVideo.load();

        albumVideo.onloadedmetadata = function() {
            const duration = albumVideo.duration || 10;
            if (typeof albumTrimStart !== 'undefined') {
                albumTrimStart = 0;
                albumTrimEnd = Math.min(10, duration);
            }

            const startSlider = document.getElementById('trim-start-slider');
            const endSlider = document.getElementById('trim-end-slider');

            if (startSlider) {
                startSlider.max = duration;
                startSlider.value = 0;
            }
            if (endSlider) {
                endSlider.max = duration;
                endSlider.value = Math.min(10, duration);
            }

            const startValLabel = document.getElementById('trim-start-val');
            const endValLabel = document.getElementById('trim-end-val');
            const durationBadge = document.getElementById('trim-duration-badge');

            if (startValLabel) startValLabel.innerText = "0.0s";
            if (endValLabel) endValLabel.innerText = Math.min(10, duration).toFixed(1) + "s";
            if (durationBadge) durationBadge.innerText = "구간 길이: " + Math.min(10, duration).toFixed(1) + "초";
        };

        albumVideo.ontimeupdate = function() {
            if (typeof albumTrimStart !== 'undefined') {
                if (this.currentTime < albumTrimStart) {
                    this.currentTime = albumTrimStart;
                }
                if (this.currentTime > albumTrimEnd) {
                    this.currentTime = albumTrimStart;
                }
            }
        };
    }

    closePostPresetVideoModal();
    showToast("자랑 비디오가 첨부되었습니다! 아래 트림바로 구간을 조절하세요.");
}

function uploadCustomFeedVideo(event) {
    const file = event.target.files[0];
    if (!file) return;

    const url = URL.createObjectURL(file);
    confirmPostVideo(url);
}

function updateFeedTrim() {
    const startInput = document.getElementById('feed-trim-start');
    const endInput = document.getElementById('feed-trim-end');
    const startLabel = document.getElementById('feed-trim-start-label');
    const endLabel = document.getElementById('feed-trim-end-label');

    trimStart = parseFloat(startInput.value);
    trimEnd = parseFloat(endInput.value);

    if (trimStart >= trimEnd) {
        trimEnd = trimStart + 1.5;
        endInput.value = trimEnd;
    }

    startLabel.innerText = trimStart.toFixed(1) + "s";
    endLabel.innerText = trimEnd.toFixed(1) + "s";
}

function clearAttachedVideo() {
    attachedVideoUrl = "";
    document.getElementById('post-video-indicator').classList.add('hidden');
    document.getElementById('feed-video-trimmer').classList.add('hidden');
    showToast("동영상 첨부가 취소되었습니다.");
}

function openAttachWalkModal() {
    const modal = document.getElementById('feed-walk-attach-modal');
    const container = document.getElementById('attach-walk-list');
    if (!modal || !container) return;

    container.innerHTML = '';
    if (walks.length === 0) {
        container.innerHTML = `<div class="text-center py-4 text-xs text-gray-400">최근 완료된 산책 기록이 없습니다. 먼저 산책을 다녀오세요!</div>`;
    } else {
        walks.forEach(w => {
            const row = document.createElement('button');
            row.onclick = () => confirmWalkAttachment(w.id);
            row.className = "w-full text-left p-3 hover:bg-indigo-50 border border-gray-100 rounded-xl flex justify-between items-center transition-colors";
            row.innerHTML = `
                <div>
                    <span class="block text-[10px] text-gray-400 font-bold">${w.date}</span>
                    <span class="font-bold text-gray-700 text-xs mt-0.5">🐾 ${w.distance} km / ${w.duration}</span>
                </div>
                <span class="text-[9px] bg-indigo-100 text-indigo-700 px-2 py-0.5 rounded-full font-bold">첨부하기</span>
            `;
            container.appendChild(row);
        });
    }

    modal.classList.remove('hidden');
    modal.classList.add('flex');
}

function closeAttachWalkModal() {
    const modal = document.getElementById('feed-walk-attach-modal');
    if (modal) {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }
}

function insertHashtags() {
    const textarea = document.getElementById('feed-input-content');
    if (!textarea) return;

    const pet = typeof getActivePet === 'function' ? getActivePet() : null;
    const petType = pet?.type || 'dog';
    const petBreed = pet?.breed || '';

    const base = ['#펫과나', '#펫스타그램', '#반려동물'];
    const typeMap = {
        dog:    ['#강아지', '#댕댕이', '#강아지스타그램', '#멍스타그램'],
        cat:    ['#고양이', '#냥이', '#고양이스타그램', '#냥스타그램'],
        rabbit: ['#토끼', '#토끼스타그램', '#토끼집사'],
        hamster:['#햄스터', '#햄스터스타그램'],
    };
    const breedTag = petBreed ? [`#${petBreed.replace(/\s+/g,'').replace(/[()]/g,'')}`] : [];
    const tags = [...base, ...(typeMap[petType] || typeMap.dog), ...breedTag].join(' ');

    const cur = textarea.value;
    textarea.value = cur ? `${cur}\n\n${tags}` : tags;
    textarea.focus();
    showToast('해시태그가 추가되었습니다 #️⃣');
}

function confirmWalkAttachment(walkId) {
    const target = walks.find(w => w.id === walkId);
    if (!target) return;

    attachedWalkData = target;
    document.getElementById('post-walk-indicator').classList.remove('hidden');
    closeAttachWalkModal();
    showToast("오늘의 산책 동선 기록이 연동 첨부되었습니다! ✔");
}

function toggleAttachHealthLog() {
    if (attachedHealthData) {
        attachedHealthData = null;
        document.getElementById('post-health-indicator').classList.add('hidden');
        showToast("건강 기록 연동이 취소되었습니다.");
    } else {
        if (typeof healthLogs === 'undefined' || !healthLogs?.today) {
            showToast("오늘 기록된 헬스케어 데이터가 없습니다. 마이펫 룸에서 먼저 기록해주세요!");
            return;
        }
        attachedHealthData = { ...healthLogs.today };
        document.getElementById('post-health-indicator').classList.remove('hidden');
        showToast("오늘의 건강 기록이 소셜 게시물에 동봉되었습니다! 💊");
    }
}

function submitFeedPost() {
    const textInput = document.getElementById('feed-input-content');
    if (!textInput || !textInput.value.trim()) {
        showToast("자랑하고 싶은 본문 이야기를 꼭 채워주세요!");
        return;
    }

    const currentPet = getActivePet();
    const newPost = {
        id: Date.now(),
        petName: currentPet ? currentPet.name : "댕이",
        petAvatar: currentPet ? (currentPet.type === 'custom' ? currentPet.imageUrl : "https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=150") : "https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=150",
        content: textInput.value.trim(),
        image: attachedPhotoUrl,
        isVideo: !!attachedVideoUrl,
        videoUrl: attachedVideoUrl,
        videoStart: trimStart,
        videoEnd: trimEnd,
        attachedWalk: attachedWalkData,
        attachedHealth: attachedHealthData,
        likes: 0,
        liked: false,
        comments: []
    };

    posts.unshift(newPost);
    saveState();

    if (typeof uploadPostToSupabase === 'function') {
        try {
            uploadPostToSupabase(newPost);
        } catch (e) {
            console.error("Supabase post upload failed:", e);
        }
    }

    textInput.value = '';
    clearAttachedPhoto();
    attachedVideoUrl = '';
    attachedWalkData = null;
    attachedHealthData = null;
    
    // 안전한 DOM 갱신 리셋
    const videoInd = document.getElementById('post-video-indicator');
    const walkInd = document.getElementById('post-walk-indicator');
    const healthInd = document.getElementById('post-health-indicator');
    const trimmer = document.getElementById('feed-video-trimmer');
    if (videoInd) videoInd.classList.add('hidden');
    if (walkInd) walkInd.classList.add('hidden');
    if (healthInd) healthInd.classList.add('hidden');
    if (trimmer) trimmer.classList.add('hidden');

    renderFeed();
    if (typeof showCustomDialog === 'function') {
        showCustomDialog({
            title: "발행 완료! 🐾🎉",
            message: "집사님과 펫의 사랑스러운 자랑 글이 소셜 피드 타임라인에 성공적으로 등록되었습니다!",
            icon: "✨",
            type: "alert"
        });
    } else {
        showToast("자랑 피드가 활기차게 발행되었습니다! 🎉");
    }
}

// 6. 1:1 대화창(DM) 관련 로직
function selectActiveChatFriend(id) {
    activeChatFriendId = id;
    const targetFriend = friends.find(f => f.id === id);
    if (targetFriend) {
        targetFriend.unread = 0;
    }
    saveState();
    switchSocialSubTab('chat');
}

function acceptFriend(id) {
    const req = friendRequests.find(r => r.id === id);
    if (!req) return;

    friendRequests = friendRequests.filter(r => r.id !== id);

    const newFriend = {
        id: req.id,
        nickname: req.nickname,
        petName: req.petName,
        petBreed: req.petBreed,
        petType: "dog",
        personality: req.personality,
        avatar: req.avatar,
        status: "online",
        chemistry: Math.floor(Math.random() * 20) + 80,
        unread: 0
    };

    friends.push(newFriend);
    chatHistories[req.id] = [
        { sender: "friend", time: "방금 전", text: "친구 수락해주셔서 감사해요! 잘 부탁드립니다 🐕🐾" }
    ];

    saveState();
    renderSocialRoom();
    showToast(`'${req.nickname}' 집사님과 이웃 친구를 맺었습니다! 조화지수를 체크해 보세요!`);
}

function rejectFriend(id) {
    friendRequests = friendRequests.filter(r => r.id !== id);
    renderSocialRoom();
    showToast("친구 요청을 사양 처리했습니다.");
}

function searchAndRequestFriend() {
    const input = document.getElementById('friend-search-input');
    if (!input || !input.value.trim()) return;

    const val = input.value.trim();
    input.value = '';

    showCustomDialog({
        title: "친구 추가 성공 🤝",
        message: `'${val}' 집사에게 다정하게 친구 요청 엽서를 전달했습니다. 수락 시 소셜함에 나타납니다!`,
        icon: "💌"
    });
}

function renderChatHistoryWindow() {
    const partner = friends.find(f => f.id === activeChatFriendId);
    const headerContainer = document.getElementById('chat-header');
    const messagesContainer = document.getElementById('chat-messages');

    if (!partner || !headerContainer || !messagesContainer) {
        if (headerContainer) headerContainer.innerHTML = `<div class="text-xs text-gray-400">대화 상대를 목록에서 지정하세요.</div>`;
        if (messagesContainer) messagesContainer.innerHTML = '';
        return;
    }

    // 헤더 정보 로드
    headerContainer.innerHTML = `
        <div class="flex items-center space-x-3">
            <img loading="lazy" src="${partner.avatar}" class="w-10 h-10 object-cover rounded-full border border-amber-100 shadow-sm" onerror="this.src='https://placehold.co/100/fbeee0/732f18?text=${partner.nickname}'">
            <div>
                <span class="font-black text-xs text-gray-800 flex items-center">
                    ${partner.nickname} <span class="bg-amber-100 text-brand-700 text-[8px] px-1.5 py-0.5 rounded ml-1.5">${partner.petBreed} (${partner.petName})</span>
                </span>
                <span class="text-[9px] text-gray-400 block mt-0.5">${partner.personality}</span>
            </div>
        </div>
        <span class="text-xs font-black text-brand-600 font-mono"><i class="fa-solid fa-heart-pulse animate-pulse text-rose-500 mr-1"></i> 성향 조화 ${partner.chemistry}%</span>
    `;

    // 메시지 내역 렌더링
    messagesContainer.innerHTML = '';
    const history = chatHistories[activeChatFriendId] || [];

    history.forEach((chat, idx) => {
        const isMe = (chat.sender === "me");
        const bubbleAlign = isMe ? "justify-end" : "justify-start";
        const bgStyle = isMe ? "bg-brand-500 text-white rounded-br-none" : "bg-white text-gray-800 border border-amber-50 rounded-bl-none";

        let inlineAttachment = '';
        if (chat.walkData) {
            inlineAttachment = `
                <div class="mt-2 p-3 bg-indigo-50 border border-indigo-100 rounded-xl space-y-1 text-left text-gray-800 shadow-inner">
                    <span class="block text-[8px] text-indigo-500 font-bold uppercase tracking-wider">임베디드 산책 루트 지도 카드</span>
                    <span class="font-bold text-xs block">🐾 안심 경로 (${chat.walkData.distance} km)</span>
                    <span class="block text-[9px] text-gray-400">⏱️ 주행 완료: ${chat.walkData.duration} / 흔적 마킹: ${chat.walkData.poop}💩 ${chat.walkData.pee}💦</span>
                    <button onclick="loadSharedTrailFromChatIndex(${activeChatFriendId}, ${idx})" class="w-full mt-2 bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-[9px] py-1 rounded-lg shadow-sm transition-all">내 맵에 장착하기</button>
                </div>
            `;
        }

        if (chat.albumData) {
            let stickersHtml = '';
            if (chat.albumData.stickers && chat.albumData.stickers.length > 0) {
                chat.albumData.stickers.forEach(st => {
                    let itemStyle = `position: absolute; left: ${st.left}%; top: ${st.top}%; z-index: ${st.zIndex}; transform: translate(-50%, -50%) scale(${st.scale * 0.45}) rotate(${st.rotate}deg); pointer-events: none; transform-origin: center center;`;
                    if (st.type === "emoji") {
                        stickersHtml += `<div style="${itemStyle} font-size: 14px;">${st.content}</div>`;
                    } else {
                        stickersHtml += `<div class="p-0.5 rounded px-1 text-[5px] font-black leading-tight border ${st.bubbleTheme} shadow-sm" style="${itemStyle}">💬 ${st.content}</div>`;
                    }
                });
            }
            
            const filterVal = getFilterCSSValue(chat.albumData.filter || 'natural');

            inlineAttachment = `
                <div class="mt-2 p-2 bg-pink-50 border border-pink-100 rounded-xl space-y-1 text-left text-gray-800 shadow-inner">
                    <span class="block text-[8px] text-pink-500 font-bold uppercase tracking-wider">공유 데코 앨범 파일</span>
                    <div class="w-full aspect-video rounded-lg overflow-hidden border relative">
                        ${chat.albumData.isVideo ? `<video src="${chat.albumData.url}" style="filter: ${filterVal};" class="w-full h-full object-cover" muted loop autoplay playsinline></video>` : `<img loading="lazy" src="${chat.albumData.url}" style="filter: ${filterVal};" class="w-full h-full object-cover">`}
                        <div class="absolute inset-0 pointer-events-none z-10 overflow-hidden">
                            ${stickersHtml}
                        </div>
                    </div>
                </div>
            `;
        }

        const msgRow = document.createElement('div');
        msgRow.className = `flex items-start gap-2.5 ${bubbleAlign}`;

        let avatarMarkup = '';
        if (!isMe) {
            avatarMarkup = `<img loading="lazy" src="${partner.avatar}" class="w-7 h-7 object-cover rounded-full border shadow-sm" onerror="this.src='https://placehold.co/100/fbeee0/732f18?text=${partner.nickname}'">`;
        }

        msgRow.innerHTML = `
            ${avatarMarkup}
            <div class="max-w-[70%] space-y-0.5">
                <div class="p-3.5 rounded-2xl shadow-sm text-xs ${bgStyle} leading-relaxed">
                    <span>${escapeHtml(chat.text)}</span>
                    ${inlineAttachment}
                </div>
                <span class="text-[9px] text-gray-400 block font-mono text-right">${chat.time}</span>
            </div>
        `;
        messagesContainer.appendChild(msgRow);
    });

    // Handle Blocked State for Chat Inputs and Send buttons (Phase 5)
    const isBlocked = typeof isBlockedByIdOrNickname === 'function' ? isBlockedByIdOrNickname(partner) : false;
    const chatInput = document.getElementById('chat-input-message');
    const sendBtn = chatInput ? chatInput.nextElementSibling : null;
    
    if (chatInput) {
        if (isBlocked) {
            chatInput.disabled = true;
            chatInput.value = '';
            chatInput.placeholder = "차단한 이웃입니다. 대화를 나눌 수 없습니다.";
            chatInput.classList.add('bg-gray-100', 'cursor-not-allowed');
            if (sendBtn) {
                sendBtn.disabled = true;
                sendBtn.classList.remove('bg-brand-500', 'hover:bg-brand-600');
                sendBtn.classList.add('bg-gray-300', 'text-gray-500', 'cursor-not-allowed');
            }
        } else {
            chatInput.disabled = false;
            chatInput.placeholder = "따뜻한 다이렉트 메시지를 남겨보세요...";
            chatInput.classList.remove('bg-gray-100', 'cursor-not-allowed');
            if (sendBtn) {
                sendBtn.disabled = false;
                sendBtn.classList.add('bg-brand-500', 'hover:bg-brand-600');
                sendBtn.classList.remove('bg-gray-300', 'text-gray-500', 'cursor-not-allowed');
            }
        }
    }

    const attachButtons = document.querySelectorAll('[onclick="shareWalkToActiveChat()"], [onclick="shareAlbumToActiveChat()"]');
    attachButtons.forEach(btn => {
        if (isBlocked) {
            btn.disabled = true;
            btn.classList.add('opacity-50', 'cursor-not-allowed', 'pointer-events-none');
        } else {
            btn.disabled = false;
            btn.classList.remove('opacity-50', 'pointer-events-none', 'cursor-not-allowed');
        }
    });

    setTimeout(() => {
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }, 50);
}

function loadSharedTrailFromChatIndex(friendId, chatIndex) {
    const chatArray = chatHistories[friendId];
    if (!chatArray || !chatArray[chatIndex] || !chatArray[chatIndex].walkData) {
        showToast("첨부된 지도가 유실되었습니다.");
        return;
    }
    switchTab('walk');
    setTimeout(() => {
        loadSharedTrailOnMyMap(chatArray[chatIndex].walkData);
    }, 300);
}

function handleChatEnter(event) {
    if (event.key === 'Enter') {
        sendChatMessage();
    }
}

function sendChatMessage() {
    const input = document.getElementById('chat-input-message');
    if (!input || !input.value.trim() || !activeChatFriendId) return;

    const partner = friends.find(f => f.id === activeChatFriendId);
    const isBlocked = typeof isBlockedByIdOrNickname === 'function' ? isBlockedByIdOrNickname(partner) : false;
    if (isBlocked) {
        showToast("차단된 이웃에게는 메시지를 보낼 수 없습니다.");
        return;
    }

    const text = input.value.trim();
    input.value = '';

    const now = new Date();
    const timeStr = `${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`;

    if (!chatHistories[activeChatFriendId]) chatHistories[activeChatFriendId] = [];
    chatHistories[activeChatFriendId].push({
        sender: "me",
        time: timeStr,
        text: text
    });

    saveState();
    renderChatHistoryWindow();

    const indicator = document.getElementById('chat-typing-indicator');
    const typingName = document.getElementById('typing-friend-name');

    if (indicator && typingName && partner) {
        typingName.innerText = partner.nickname;
        indicator.classList.remove('hidden');

        const msgBox = document.getElementById('chat-messages');
        if (msgBox) msgBox.scrollTop = msgBox.scrollHeight;

        setTimeout(() => {
            indicator.classList.add('hidden');

            let autoReplyText = "와 정말이네요! 우리 다음에 한 번 날씨 좋을 때 같이 공원 트랙 산책해요 🐶🐾";
            if (partner.petType === 'cat' || partner.id === 502) {
                autoReplyText = "냐옹... 저희 고양이는 외출하면 얼어붙지만 집에서 상자 놀이하며 지켜볼게요! 항상 꿀팁 감사해요 📦😻";
            } else if (partner.petType === 'rabbit' || partner.id === 503) {
                autoReplyText = "오 정말 유용한 정보네요! 저희 솜이 당근 간식 타임 끝나고 코스 복사해서 지도 유심히 구경해볼게요 🐰🎈";
            }

            if (text.includes("산책") || text.includes("코스") || text.includes("지도")) {
                autoReplyText = "우와! 안심 지도에 있는 산책로를 보내주셨네요! 내일 펫 안심동선 켜고 그대로 한번 따라 걸어볼게요! 🗺️✨";
            } else if (text.includes("간식") || text.includes("사료") || text.includes("밥")) {
                autoReplyText = "맛있는 먹거리 사료 추천 감사해요! 저희 아이가 은근 까다로운데 황태 가루랑 연어 사서 시도해볼게요! 🍖";
            }

            chatHistories[activeChatFriendId].push({
                sender: "friend",
                time: timeStr,
                text: autoReplyText
            });

            saveState();
            renderChatHistoryWindow();

            showToast(`'${partner.nickname}' 이웃으로부터 따뜻한 DM 답변이 도착했습니다.`);
        }, 1800);
    }
}

function shareWalkToActiveChat() {
    if (walks.length === 0) {
        showToast("첨부할 수 있는 최근 완료된 산책로가 없습니다.");
        return;
    }

    const latestWalk = walks[0];
    const partner = friends.find(f => f.id === activeChatFriendId);
    if (!partner) return;

    const now = new Date();
    const timeStr = `${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`;

    chatHistories[activeChatFriendId].push({
        sender: "me",
        time: timeStr,
        text: `집사님! 오늘 우리 펫이랑 같이 걸었던 이쁜 산책 이동 코스 정보 보낼게요. 지도 수입해서 같이 달려보세요! 🗺️🏃`,
        walkData: latestWalk
    });

    saveState();
    renderChatHistoryWindow();
    showToast("완료한 산책 경로 핀 지도가 대화창 내에 고유 임베디드되어 전송되었습니다!");
}

function shareAlbumToActiveChat() {
    if (albums.length === 0) {
        showToast("첨부할 소장 데코 앨범 작품이 없습니다. 먼저 꾸미기를 완료해주세요!");
        return;
    }

    const latestAlbum = albums[0];
    const partner = friends.find(f => f.id === activeChatFriendId);
    if (!partner) return;

    const now = new Date();
    const timeStr = `${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`;

    chatHistories[activeChatFriendId].push({
        sender: "me",
        time: timeStr,
        text: "이웃 집사님! 오늘 제가 스티커 꾸미기방에서 정성껏 직접 완성한 우리 아이 앨범인데 자랑합니다 이쁘게 봐주세요! 🎨💖",
        albumData: latestAlbum
    });

    saveState();
    renderChatHistoryWindow();
    showToast("완성한 액티브 스티커 앨범 카드가 대화방에 쏙 공유되었습니다!");
}

// 7. 실시간 좋아요/댓글 알림 로그 매니저
let likeNotifications = JSON.parse(localStorage.getItem('petna_like_notifications')) || [
    { id: 1, type: "like", name: "송이 집사", pet: "🐶 송이", action: "님이 내 '산책 코스 자랑' 글에 하트를 보냈습니다. ❤️", time: "2분 전", isRead: false },
    { id: 2, type: "comment", name: "바람 집사", pet: "🐰 코코", action: "님이 내 '귀여운 토끼 세수' 영상에 댓글을 남겼습니다.", comment: "영상이 끊김 없이 너무 부드럽고 귀여워요! 구간 편집 대박 ㅠㅠ 🌸", time: "15분 전", isRead: true },
    { id: 3, type: "like", name: "치즈 집사", pet: "🐱 나비", action: "님이 집사 프로필에 따뜻한 응원의 좋아요를 남겼습니다.", time: "1시간 전", isRead: true },
    { id: 4, type: "comment", name: "토리 집사", pet: "🐹 토리", action: "님이 내 신규 자랑 발행글에 댓글을 작성했습니다.", comment: "댕이 눈이 정말 보석 같네요! 간식 백만 번 주고 싶다... 🍖", time: "3시간 전", isRead: true }
];

function saveLikeNotifications() {
    localStorage.setItem('petna_like_notifications', JSON.stringify(likeNotifications));
}

function renderLikeNotifications() {
    const listEl = document.getElementById('like-notification-list');
    const countEl = document.getElementById('like-notify-count');
    if (!listEl) return;
    
    const unreadCount = likeNotifications.filter(n => !n.isRead).length;
    if (countEl) {
        countEl.innerText = unreadCount;
        if (unreadCount > 0) {
            countEl.className = "bg-rose-100 text-rose-600 font-black text-[9px] px-2 py-0.5 rounded-full animate-bounce";
        } else {
            countEl.className = "bg-gray-100 text-gray-400 font-bold text-[9px] px-2 py-0.5 rounded-full";
        }
    }

    listEl.innerHTML = likeNotifications.map(n => {
        const isReplied = !!n.replyText;
        const isReplying = (n.id === activeReplyNotificationId);

        let actionButtonHtml = "";
        if (!isReplied) {
            actionButtonHtml = `
                <button onclick="toggleNeighborReplyForm(${n.id})" class="text-[9px] bg-brand-50 hover:bg-brand-100 text-brand-600 font-bold px-2 py-1 rounded-lg transition-colors flex items-center gap-1 shadow-sm">
                    ${n.type === 'comment' ? '<i class="fa-solid fa-reply text-[8px] text-indigo-500"></i> 답장' : '<i class="fa-solid fa-heart text-[8px] text-rose-500 animate-pulse"></i> 반사'}
                </button>
            `;
        }

        let replyFormHtml = "";
        if (isReplying) {
            replyFormHtml = `
                <div class="mt-2.5 ml-10 p-2 bg-gray-50 border border-gray-100 rounded-xl flex gap-1.5 items-center shadow-inner">
                    <input type="text" id="neighbor-reply-input-${n.id}" placeholder="${n.type === 'comment' ? '감사의 답장 메시지를 입력하세요...' : '감사의 반사 메시지를 입력하세요...'}" class="flex-grow border border-gray-200 rounded-lg px-2 py-1 text-[10px] outline-none focus:border-brand-500 bg-white" onkeydown="if(event.key==='Enter') submitNeighborReply(${n.id})">
                    <button onclick="submitNeighborReply(${n.id})" class="bg-brand-500 hover:bg-brand-600 text-white font-bold text-[9px] py-1.5 px-2.5 rounded-lg transition-colors shrink-0">
                        전송 💬
                    </button>
                </div>
            `;
        }

        let replyContentHtml = "";
        if (isReplied) {
            replyContentHtml = `
                <div class="mt-2 ml-10 p-2 bg-brand-50/50 border border-brand-100/30 rounded-xl text-gray-700 font-medium shadow-sm relative">
                    <span class="block text-[9.5px] font-black text-brand-600 mb-0.5"><i class="fa-solid fa-reply-all mr-1 text-[9px]"></i>내 답장</span>
                    <span class="block text-[10px] leading-relaxed break-all">${escapeHtml(n.replyText)}</span>
                </div>
            `;
        }

        return `
            <div class="flex flex-col p-2.5 rounded-2xl transition-all duration-300 ${n.isRead ? 'bg-gray-50/50 hover:bg-gray-50' : 'bg-rose-50/40 hover:bg-rose-50 border border-rose-100/30'}">
                <div class="flex items-start justify-between w-full">
                    <div class="flex gap-2.5">
                        <div class="w-8 h-8 rounded-full bg-white flex items-center justify-center text-lg border border-gray-100 shadow-sm flex-shrink-0">
                            ${n.pet.split(' ')[0]}
                        </div>
                        <div class="text-[11px] text-gray-700 leading-relaxed">
                            <span class="font-black text-gray-800 block">${n.name} <span class="font-medium text-gray-400">(${n.pet})</span></span>
                            <p class="font-medium text-gray-600 mt-0.5">${n.action}</p>
                        </div>
                    </div>
                    <div class="flex flex-col gap-1 items-end flex-shrink-0">
                        ${!n.isRead ? '<span class="w-1.5 h-1.5 rounded-full bg-rose-500 mb-2"></span>' : ''}
                        ${actionButtonHtml}
                    </div>
                </div>
                
                ${n.type === 'comment' && n.comment ? `
                <div class="mt-2 ml-10 p-2 bg-amber-50/60 border border-amber-100/40 rounded-xl text-gray-700 font-medium relative shadow-sm">
                    <span class="block text-[10.5px] leading-relaxed break-all">"${n.comment}"</span>
                </div>
                ` : ''}

                ${replyContentHtml}
                ${replyFormHtml}
                
                <span class="text-[9px] text-gray-400 font-bold block mt-1 ml-10">${n.time}</span>
            </div>
        `;
    }).join('');
}

function toggleNeighborReplyForm(id) {
    if (activeReplyNotificationId === id) {
        activeReplyNotificationId = null;
    } else {
        activeReplyNotificationId = id;
    }
    renderLikeNotifications();
    // 답장 폼이 열리면 스크롤해서 입력창 보이게
    if (activeReplyNotificationId !== null) {
        setTimeout(() => {
            const input = document.getElementById(`neighbor-reply-input-${id}`);
            if (input) input.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        }, 50);
    }
}

function submitNeighborReply(id) {
    const input = document.getElementById(`neighbor-reply-input-${id}`);
    if (!input || !input.value.trim()) return;

    const text = input.value.trim();
    const found = likeNotifications.find(n => n.id === id);
    if (!found) return;

    found.replyText = text;
    found.isRead = true;
    activeReplyNotificationId = null;

    saveLikeNotifications();
    renderLikeNotifications();
    showToast(`💌 ${found.name} (${found.pet}) 님에게 감사의 마음이 배달되었습니다!`);
}

// 8. 실시간 알림 시뮬레이션 엔진 병합
let likeSimulationInterval = null;

function initLikeNotificationSimulation() {
    if (likeSimulationInterval) clearInterval(likeSimulationInterval);
    
    likeSimulationInterval = setInterval(() => {
        const petPool = [
            { name: "초코", type: "🐶" },
            { name: "해피", type: "🐶" },
            { name: "나비", type: "🐱" },
            { name: "보리", type: "🐶" },
            { name: "하늘", type: "🐰" },
            { name: "치즈", type: "🐱" },
            { name: "구름", type: "🐹" }
        ];
        
        const pet = petPool[Math.floor(Math.random() * petPool.length)];
        const isComment = Math.random() > 0.5;
        
        let newNotify = {};
        
        if (isComment) {
            const comments = [
                "어머나! 이 아가 어느 별에서 왔나요? 진짜 너무 예뻐서 심멎 😭💖",
                "산책 경로가 완전 꿀팁이네요! 이번 주말에 저희 댕댕이랑 꼭 가봐야겠어요 🏃‍♂️💨",
                "귀여움 한도 초과입니다... 집사님 일상 공유 매일매일 자주 부탁해요 ㅠㅠ",
                "배식 정보 피드가 진짜 알차네요! 스크랩 각입니다! 👍",
                "자랑 구간 반복 루프 영상 보는데 하루 피로가 다 날아가요 ㅋㅋㅋ 완전 힐링! 🐾",
                "이 펫룸 너무 코지하네요! 집사님도 댕이도 화이팅입니다!"
            ];
            
            const randomComment = comments[Math.floor(Math.random() * comments.length)];
            
            newNotify = {
                id: Date.now(),
                type: "comment",
                name: `${pet.name} 집사`,
                pet: `${pet.type} ${pet.name}`,
                action: "님이 내 최신 자랑 피드에 소중한 댓글을 남겼습니다.",
                comment: randomComment,
                time: "방금 전",
                isRead: false
            };
        } else {
            const actions = [
                "님이 내 자랑 피드에 깜짝 하트를 남겼습니다. ❤️",
                "님이 내 자랑 영상 구간 편집본이 최고라고 하트를 날렸습니다! 😍",
                "님이 내 산책 경로에 관심의 좋아요를 보냈습니다.",
                "님이 내 마이펫 일상 글에 따뜻한 하트를 더했습니다."
            ];
            
            const randomAction = actions[Math.floor(Math.random() * actions.length)];
            
            newNotify = {
                id: Date.now(),
                type: "like",
                name: `${pet.name} 집사`,
                pet: `${pet.type} ${pet.name}`,
                action: randomAction,
                time: "방금 전",
                isRead: false
            };
        }
        
        likeNotifications.unshift(newNotify);
        if (likeNotifications.length > 8) likeNotifications.pop(); 
        
        saveLikeNotifications();
        
        // 현재 소셜 탭의 자랑 피드 서브탭이 켜져있을 때만 토스트와 실시간 렌더링
        const socialTab = document.getElementById('tab-social');
        if (socialTab && socialTab.classList.contains('block')) {
            renderLikeNotifications();
            if (activeSocialSubTab === 'feed') {
                if (newNotify.type === 'comment') {
                    showToast(`💬 ${newNotify.name}님이 댓글을 남겼습니다: "${newNotify.comment.substring(0, 15)}..."`);
                } else {
                    showToast(`❤️ ${newNotify.name}님이 집사님에게 따뜻한 공감을 보냈습니다!`);
                }
            }
        }
    }, 20000); 
}

// 시뮬레이션 자동 기동
initLikeNotificationSimulation();

// 호환성용 산책 데이터 로더 (기존 feed.js의 지도 로드 브릿지)
function loadFeedWalkOnMap(postId) {
    const post = posts.find(p => p.id === postId);
    if (post && post.attachedWalk) {
        switchTab('walk');
        setTimeout(() => {
            loadSharedTrailOnMyMap(post.attachedWalk);
        }, 300);
    }
}

// 5. 이웃집사 프로필 카드 상세 보기 & 상호작용 핸들러
let currentNeighborFriendId = null;

function showOwnerProfile(petName, petAvatar) {
    const modal = document.getElementById('neighbor-profile-modal');
    if (!modal) return;

    // 1. 친구 데이터베이스에서 검색
    let friend = friends.find(f => f.petName === petName || f.nickname === petName);
    
    // 2. 만약 내 펫이라면?
    const myPetName = pets[0] ? pets[0].name : "댕이";
    
    let profileData = {};
    if (petName === myPetName) {
        const myPet = pets[0] || {};
        profileData = {
            nickname: "나 (보호자)",
            petName: myPetName,
            avatar: myPet.avatar || "https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=150",
            breed: myPet.breed || "시바견",
            type: myPet.type === "dog" ? "강아지 🐕" : myPet.type === "cat" ? "고양이 🐈" : "소중한 반려동물 🐾",
            personality: myPet.personality || "가족바라기, 산책 애호가",
            chemistry: 100,
            status: "online",
            isMe: true
        };
    } else if (friend) {
        profileData = {
            id: friend.id,
            nickname: friend.nickname,
            petName: friend.petName,
            avatar: friend.avatar,
            breed: friend.petBreed,
            type: friend.petType === "dog" ? "강아지 🐕" : friend.petType === "cat" ? "고양이 🐈" : "소중한 반려동물 🐾",
            personality: friend.personality,
            chemistry: friend.chemistry,
            status: friend.status,
            isMe: false
        };
    } else {
        // 비회원 혹은 새로 작성된 이웃의 경우 동적 프로필 자동생성
        profileData = {
            id: 501, // 기본 초코언니 대화방 연결
            nickname: petName + " 집사",
            petName: petName,
            avatar: petAvatar || "https://images.unsplash.com/photo-1543466835-00a7907e9de1?auto=format&fit=crop&q=80&w=150",
            breed: "하이브리드 펫",
            type: "강아지 🐕",
            personality: "활발하고 정이 아주 많음 🐾",
            chemistry: 88,
            status: "online",
            isMe: false
        };
    }

    // 3. 모달 DOM에 바인딩
    document.getElementById('neighbor-avatar').src = profileData.avatar;
    document.getElementById('neighbor-nickname').innerText = profileData.nickname;
    document.getElementById('neighbor-pet-name').innerText = profileData.petName;
    document.getElementById('neighbor-pet-breed').innerText = profileData.breed;
    document.getElementById('neighbor-pet-type').innerText = profileData.type;
    document.getElementById('neighbor-pet-personality').innerText = profileData.personality;
    document.getElementById('neighbor-chemistry').innerText = profileData.chemistry + "%";
    
    const conditions = ["활력 충만 ✨", "조금 피곤함 💤", "소화 불량 🥲", "기분 최고 😆"];
    const randomCondition = conditions[Math.floor(Math.random() * conditions.length)];
    const conditionEl = document.getElementById('neighbor-health-condition');
    if (conditionEl) {
        conditionEl.innerText = randomCondition;
        if (randomCondition.includes("피곤") || randomCondition.includes("불량")) {
            conditionEl.className = "text-xs font-black text-rose-500";
        } else {
            conditionEl.className = "text-xs font-black text-amber-600";
        }
    }
    
    const statusText = document.getElementById('neighbor-status-text');
    const statusBadge = document.getElementById('neighbor-status-badge');
    const chatBtn = document.getElementById('neighbor-chat-btn');
    
    if (profileData.status === "online") {
        statusText.innerText = "실시간 온라인 🟢";
        statusText.className = "font-bold text-green-600";
        statusBadge.className = "absolute bottom-1 right-2 w-4 h-4 rounded-full border-2 border-white bg-green-500";
    } else {
        statusText.innerText = "오프라인 💤";
        statusText.className = "font-bold text-gray-400";
        statusBadge.className = "absolute bottom-1 right-2 w-4 h-4 rounded-full border-2 border-white bg-gray-300";
    }

    // 4. 내 프로필인 경우 1:1 대화 불가 처리
    currentNeighborProfile = profileData;
    if (profileData.isMe) {
        chatBtn.classList.add('hidden');
    } else {
        chatBtn.classList.remove('hidden');
        currentNeighborFriendId = profileData.id;

        // If blocked, disable chatBtn and style it appropriately
        const isBlocked = typeof isNeighborBlocked === 'function' ? isNeighborBlocked(profileData) : false;
        if (isBlocked) {
            chatBtn.disabled = true;
            chatBtn.className = "w-full bg-gray-300 text-gray-500 font-extrabold py-3.5 rounded-2xl text-xs flex items-center justify-center gap-2 cursor-not-allowed outline-none";
            chatBtn.innerHTML = `<i class="fa-solid fa-comments"></i> 차단된 이웃 (대화 불가)`;
        } else {
            chatBtn.disabled = false;
            chatBtn.className = "w-full bg-brand-500 hover:bg-brand-600 text-white font-extrabold py-3.5 rounded-2xl text-xs shadow-md hover:shadow-lg transition-all flex items-center justify-center gap-2 outline-none";
            chatBtn.innerHTML = `<i class="fa-solid fa-comments"></i> 1:1 대화방 입장하기`;
        }
    }

    // Configure the management panel (Block/Delete) (Phase 5)
    const managePanel = document.getElementById('neighbor-manage-panel');
    if (managePanel) {
        if (profileData.isMe) {
            managePanel.classList.add('hidden');
        } else {
            managePanel.classList.remove('hidden');
            const isBlocked = typeof isNeighborBlocked === 'function' ? isNeighborBlocked(profileData) : false;
            const blockBtn = document.getElementById('neighbor-block-btn');
            if (blockBtn) {
                blockBtn.innerHTML = isBlocked 
                    ? `<i class="fa-solid fa-circle-check text-emerald-600 mr-1"></i> 차단 해제` 
                    : `<i class="fa-solid fa-ban text-rose-500 mr-1"></i> 이웃 차단`;
            }
        }
    }

    modal.classList.remove('hidden');
    modal.classList.add('flex');
}

function closeNeighborProfileModal() {
    const modal = document.getElementById('neighbor-profile-modal');
    if (modal) {
        modal.classList.remove('flex');
        modal.classList.add('hidden');
    }
}

function sendGetWellBone() {
    showToast("🦴 이웃 펫에게 응원과 쾌유 기원 뼈다귀를 보냈습니다!");
    closeNeighborProfileModal();
}

function startChatWithNeighbor() {
    closeNeighborProfileModal();
    if (!currentNeighborFriendId) return;

    // 1. 소셜 룸 서브탭(채팅)으로 전환
    switchSocialSubTab('chat');

    // 2. 해당 친구 선택
    selectActiveChatFriend(currentNeighborFriendId);
    
    // 3. 소셜 룸 전체 재렌더링
    renderSocialRoom();

    showToast("💬 이웃집사와의 1:1 안심 대화방이 활성화되었습니다!");
}

function requestWalkSchedule() {
    closeNeighborProfileModal();
    showToast("🐾 공동 산책 일정이 성공적으로 신청되었습니다! 수락 시 알림이 전송됩니다.");
}

/**
 * 📸 다중 피드 사진 캐러셀용 좌우 슬라이드 스크롤 유틸
 */
function scrollCarousel(postId, direction) {
    const el = document.getElementById(`carousel-${postId}`);
    if (!el) return;
    const width = el.clientWidth;
    el.scrollBy({ left: direction * width, behavior: 'smooth' });
}

/**
 * 📸 다중 피드 사진 캐러셀용 스크롤 기반 현재 칭수/장수 인디케이터 갱신
 */
function updateCarouselIndicators(postId) {
    const el = document.getElementById(`carousel-${postId}`);
    const indicator = document.getElementById(`indicator-cur-${postId}`);
    if (!el || !indicator) return;
    const scrollLeft = el.scrollLeft;
    const width = el.clientWidth;
    const curPage = Math.round(scrollLeft / width) + 1;
    indicator.innerText = curPage;
}

/**
 * 📸 다중 피드 캐러셀 내부 비디오 재생/일시정지 토글 제어
 */
function toggleCarouselVideoPlay(videoKey) {
    const video = document.getElementById(`post-video-${videoKey}`);
    const icon = document.getElementById(`play-icon-${videoKey}`);
    if (!video) return;

    if (video.paused) {
        video.play();
        if (icon) {
            icon.className = "fa-solid fa-pause text-xs";
        }
    } else {
        video.pause();
        if (icon) {
            icon.className = "fa-solid fa-play text-xs pl-0.5";
        }
    }
}

// ==========================================
// 👥 이웃 집사 관리 기능 (차단 및 삭제) (Phase 5)
// ==========================================
let currentNeighborProfile = null;

function getBlockedNeighbors() {
    return JSON.parse(localStorage.getItem('petna_blocked_neighbors')) || [];
}

function isNeighborBlocked(profile) {
    if (!profile) return false;
    const blocked = getBlockedNeighbors();
    return blocked.some(b => b.nickname === profile.nickname || b.petName === profile.petName);
}

function isBlockedByIdOrNickname(friend) {
    if (!friend) return false;
    const blocked = getBlockedNeighbors();
    return blocked.some(b => b.id === friend.id || b.nickname === friend.nickname || b.petName === friend.petName);
}

function toggleBlockNeighbor() {
    if (!currentNeighborProfile) return;
    
    const blocked = getBlockedNeighbors();
    const isBlocked = isNeighborBlocked(currentNeighborProfile);
    
    let newBlocked;
    if (isBlocked) {
        newBlocked = blocked.filter(b => b.nickname !== currentNeighborProfile.nickname && b.petName !== currentNeighborProfile.petName);
        showToast(`✅ ${currentNeighborProfile.nickname} 님의 차단이 해제되었습니다.`);
    } else {
        newBlocked = [...blocked, {
            id: currentNeighborProfile.id,
            nickname: currentNeighborProfile.nickname,
            petName: currentNeighborProfile.petName
        }];
        showToast(`🚫 ${currentNeighborProfile.nickname} 님이 차단되었습니다. 이제 이 이웃의 글과 메시지가 보이지 않습니다.`);
    }
    
    localStorage.setItem('petna_blocked_neighbors', JSON.stringify(newBlocked));
    closeNeighborProfileModal();
    
    // Re-render feed and social room (chats/friends list)
    if (typeof renderFeed === 'function') renderFeed();
    if (typeof renderSocialRoom === 'function') renderSocialRoom();
}

function deleteNeighbor() {
    if (!currentNeighborProfile) return;
    
    showCustomDialog({
        title: "이웃 삭제 ⚠️",
        message: `정말 ${currentNeighborProfile.nickname} 님을 이웃 목록에서 삭제하시겠습니까?`,
        icon: "👤",
        type: "confirm",
        onConfirm: () => {
            // Remove from friends list
            friends = friends.filter(f => f.nickname !== currentNeighborProfile.nickname && f.petName !== currentNeighborProfile.petName);
            saveState();
            
            showToast(`🗑️ ${currentNeighborProfile.nickname} 님이 이웃 목록에서 삭제되었습니다.`);
            closeNeighborProfileModal();
            
            // Re-render feed and social room
            if (typeof renderFeed === 'function') renderFeed();
            if (typeof renderSocialRoom === 'function') renderSocialRoom();
        }
    });
}

async function generateSocialCaption() {
    const apiKey = window._env_?.GEMINI_API_KEY || "";
    if (!apiKey) { showToast("AI 캡션 생성에 GEMINI_API_KEY가 필요합니다."); return; }

    const textArea = document.getElementById('feed-input-content');
    if (!textArea) return;
    const prevVal = textArea.value;
    textArea.value = "AI 캡션 생성 중... ✍️";
    textArea.disabled = true;

    const pet = getActivePet();
    
    // Check if there is an attached photo and if it's base64
    let base64Data = null;
    let mimeType = "image/jpeg";
    if (attachedPhotoUrl && attachedPhotoUrl.startsWith("data:")) {
        const parts = attachedPhotoUrl.split(",");
        if (parts.length > 1) {
            base64Data = parts[1];
            const match = attachedPhotoUrl.match(/data:([^;]+);/);
            if (match) mimeType = match[1];
        }
    }

    const prompt = base64Data
        ? `이 반려동물 사진을 보고 진짜 집사가 인스타에 올릴 것처럼 짧고 자연스러운 한국어 자랑글 캡션을 써줘. 평소 친구에게 톡하듯 문어체 탈피. 이모지 1개. 해시태그 5개 포함. 캡션만 출력.`
        : `${pet?.name || "우리 아이"}의 일상을 공유하는 진짜 집사 말투의 인스타 자랑글 캡션을 써줘. 친근하고 부드러운 구어체 사용. 이모지 1개. 해시태그 5개 포함. 캡션만.`;

    try {
        const parts = [{ text: prompt }];
        if (base64Data) {
            parts.push({ inline_data: { mime_type: mimeType, data: base64Data } });
        }
        const res = await fetch(
            `https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=${apiKey}`,
            { method: "POST", headers: { "Content-Type": "application/json" },
              body: JSON.stringify({ contents: [{ parts }] }) }
        );
        const data = await res.json();
        const caption = data?.candidates?.[0]?.content?.parts?.[0]?.text?.trim() || prevVal;
        
        // Remove markdown block backticks if any returned by Gemini
        let cleanedCaption = caption.replace(/^```[a-zA-Z]*\n/, "").replace(/\n```$/, "").trim();
        textArea.value = cleanedCaption;
        showToast("✨ AI가 센스있는 이웃 자랑 캡션을 작성했습니다!");
    } catch (e) {
        textArea.value = prevVal;
        showToast("AI 캡션 생성 실패: " + e.message);
    }
    textArea.disabled = false;
}


