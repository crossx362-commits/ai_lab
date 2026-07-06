/**
 * 📮 우체통(Mailbox) 기능 고도화 모듈
 */

// active folder state
if (typeof window.currentMailboxFolder === 'undefined') {
    window.currentMailboxFolder = 'inbox';
}
if (typeof window.activeLetterStamp === 'undefined') {
    window.activeLetterStamp = '🐾';
}

function getLetterFolder(letter) {
    if (letter.folder) return letter.folder;
    // 하위 호환성 처리: 보낸 사람 이름이 "나"이거나 내용이 To.로 시작하면 보낸 편지로 분류
    if (letter.sender && (letter.sender.includes("나 (") || letter.sender === "나")) {
        return 'sent';
    }
    return 'inbox';
}

function getReceiverFromContent(content) {
    if (!content) return null;
    const match = content.match(/^\[To\.\s*([^\]]+)\]/);
    return match ? match[1] : null;
}

function switchMailboxFolder(folder) {
    window.currentMailboxFolder = folder;
    
    // 탭 버튼 스타일 업데이트
    const folders = ['inbox', 'sent', 'trash'];
    folders.forEach(f => {
        const btn = document.getElementById(`mailbox-folder-${f}`);
        if (btn) {
            if (f === folder) {
                btn.classList.remove('text-gray-400', 'hover:text-gray-600');
                btn.classList.add('text-brand-600', 'bg-white', 'shadow-sm');
            } else {
                btn.classList.add('text-gray-400', 'hover:text-gray-600');
                btn.classList.remove('text-brand-600', 'bg-white', 'shadow-sm');
            }
        }
    });

    // 휴지통 비우기 버튼 활성화 분기
    const emptyTrashBtn = document.getElementById('empty-trash-btn');
    if (emptyTrashBtn) {
        const trashLetters = letters.filter(l => getLetterFolder(l) === 'trash');
        if (folder === 'trash' && trashLetters.length > 0) {
            emptyTrashBtn.classList.remove('hidden');
            emptyTrashBtn.classList.add('flex');
        } else {
            emptyTrashBtn.classList.remove('flex');
            emptyTrashBtn.classList.add('hidden');
        }
    }

    renderMailbox();
}

function renderMailbox() {
    const listBody = document.getElementById('letter-list-body');
    if (!listBody) return;

    listBody.innerHTML = '';

    const folder = window.currentMailboxFolder || 'inbox';
    const filteredLetters = letters.filter(l => getLetterFolder(l) === folder);

    if (filteredLetters.length === 0) {
        let emptyMsg = "아직 받은 편지가 없습니다. 📮";
        if (folder === 'sent') {
            emptyMsg = "보낸 편지가 없습니다. 따뜻한 마음을 먼저 보내보세요! 📤";
        } else if (folder === 'trash') {
            emptyMsg = "휴지통이 비어 있습니다. 🗑️";
        }
        listBody.innerHTML = `
            <div class="flex flex-col items-center justify-center py-20 opacity-40">
                <div class="text-6xl mb-4">📮</div>
                <p class="text-xs font-bold text-gray-500">${emptyMsg}</p>
            </div>
        `;
        updateMailboxBadge();
        return;
    }

    filteredLetters.forEach(letter => {
        const item = document.createElement('div');
        const isUnread = (folder === 'inbox' && !letter.isRead);
        const unreadClass = isUnread ? 'bg-brand-50/20' : 'bg-white';
        
        item.className = `p-5 flex items-start gap-4 cursor-pointer hover:bg-gray-50/70 transition-all border-b border-gray-50/70 ${unreadClass}`;
        item.onclick = () => openLetterDetail(letter.id);
        
        // 아이콘 우표 표시
        let iconEmoji = letter.stamp || '🐾';
        if (folder === 'inbox') {
            iconEmoji = letter.isRead ? '📩' : '✉️';
        } else if (folder === 'sent') {
            iconEmoji = '📤';
        } else if (folder === 'trash') {
            iconEmoji = '🗑️';
        }

        let headingText = '';
        let buttonGroup = '';

        if (folder === 'inbox') {
            headingText = `<span class="font-black text-gray-800 text-xs break-words">${letter.sender} <span class="font-medium text-gray-400 text-[10px]">(${letter.petName || '우리 아이'})</span></span>`;
            buttonGroup = `
                <button onclick="event.stopPropagation(); replyToLetter('${letter.sender}', '${letter.content.replace(/'/g, "\\'")}')" class="bg-brand-50 hover:bg-brand-100 text-brand-600 font-bold text-[9px] px-2 py-0.5 rounded-lg transition-colors">답장</button>
                <button onclick="event.stopPropagation(); deleteLetter(${letter.id})" class="bg-rose-50 hover:bg-rose-100 text-rose-600 font-bold text-[9px] px-2 py-0.5 rounded-lg transition-colors">삭제</button>
            `;
        } else if (folder === 'sent') {
            const receiverName = letter.receiver || getReceiverFromContent(letter.content) || "이웃 집사";
            headingText = `<span class="font-black text-gray-800 text-xs break-words">To. ${receiverName} <span class="font-medium text-gray-400 text-[10px]">(${letter.petName || '우리 아이'})</span></span>`;
            buttonGroup = `
                <button onclick="event.stopPropagation(); deleteLetter(${letter.id})" class="bg-rose-50 hover:bg-rose-100 text-rose-600 font-bold text-[9px] px-2 py-0.5 rounded-lg transition-colors">삭제</button>
            `;
        } else if (folder === 'trash') {
            const receiverName = letter.receiver || getReceiverFromContent(letter.content) || "이웃";
            const originalType = letter.originalFolder === 'sent' ? '보낸 편지' : '받은 편지';
            headingText = `<span class="font-black text-gray-800 text-xs break-words">From. ${letter.sender} ➔ To. ${receiverName} <span class="font-medium text-gray-400 text-[9px] ml-1">(${originalType})</span></span>`;
            buttonGroup = `
                <button onclick="event.stopPropagation(); restoreLetter(${letter.id})" class="bg-brand-50 hover:bg-brand-100 text-brand-600 font-bold text-[9px] px-2 py-0.5 rounded-lg transition-colors">복원</button>
                <button onclick="event.stopPropagation(); deleteLetterPermanently(${letter.id})" class="bg-rose-50 hover:bg-rose-100 text-rose-600 font-bold text-[9px] px-2 py-0.5 rounded-lg transition-colors">영구삭제</button>
            `;
        }

        item.innerHTML = `
            <div class="relative shrink-0 select-none">
                <div class="w-12 h-12 rounded-2xl bg-white border border-amber-100 flex items-center justify-center text-2xl shadow-sm">
                    ${iconEmoji}
                </div>
                ${isUnread ? '<span class="absolute -top-1 -right-1 w-3 h-3 bg-rose-500 rounded-full border-2 border-white animate-pulse"></span>' : ''}
            </div>
            <div class="min-w-0 flex-grow">
                <div class="flex justify-between items-start mb-1 gap-2">
                    ${headingText}
                    <div class="flex items-center space-x-1.5 shrink-0">
                        <span class="text-[9px] text-gray-400 font-mono font-bold mt-0.5 mr-1">${letter.date}</span>
                        ${buttonGroup}
                    </div>
                </div>
                <p class="text-[11px] text-gray-500 leading-relaxed break-words whitespace-pre-wrap mt-1">${letter.content}</p>
            </div>
        `;
        listBody.appendChild(item);
    });

    updateMailboxBadge();
}

function openLetterDetail(id) {
    const letter = letters.find(l => l.id === id);
    if (!letter) return;

    const folder = getLetterFolder(letter);

    // 읽음 처리
    if (folder === 'inbox' && !letter.isRead) {
        letter.isRead = true;
        saveState();
        renderMailbox();
        updateMailboxBadge();
    }

    const receiverName = letter.receiver || getReceiverFromContent(letter.content) || "나";
    const senderName = letter.sender || "이웃 집사";
    const petName = letter.petName || "우리 아이";

    // 엽서 상세 모달 정보 바인딩
    document.getElementById('letter-detail-date').innerText = letter.date;
    document.getElementById('letter-detail-receiver-name').innerText = receiverName;
    document.getElementById('letter-detail-sender-name').innerText = senderName;
    document.getElementById('letter-detail-pet-name').innerText = `(${petName})`;
    
    // To. receiver prefix 제거해서 본문 깔끔하게 출력
    let cleanContent = letter.content;
    const prefix = `[To. ${receiverName}]`;
    if (cleanContent.startsWith(prefix)) {
        cleanContent = cleanContent.substring(prefix.length).trim();
    }
    document.getElementById('letter-detail-content').innerText = cleanContent;
    
    // 우표 표시
    document.getElementById('letter-detail-stamp-display').innerText = letter.stamp || '🐾';

    // 위치 뱃지
    let folderBadgeText = "📥 받은 편지함";
    let badgeClass = "bg-brand-50 text-brand-700";
    if (folder === 'sent') {
        folderBadgeText = "📤 보낸 편지함";
        badgeClass = "bg-brand-50 text-brand-700";
    } else if (folder === 'trash') {
        folderBadgeText = "🗑️ 휴지통";
        badgeClass = "bg-rose-50 text-rose-700";
    }
    const badgeEl = document.getElementById('letter-detail-folder-badge');
    badgeEl.innerText = folderBadgeText;
    badgeEl.className = `${badgeClass} font-extrabold text-[9px] px-2 py-0.5 rounded-full inline-block mt-0.5`;

    // 하단 버튼 구성
    const actionContainer = document.getElementById('letter-detail-actions');
    actionContainer.innerHTML = '';

    if (folder === 'inbox') {
        actionContainer.innerHTML = `
            <button onclick="closeLetterDetailModal(); replyToLetter('${senderName}', '${letter.content.replace(/'/g, "\\'")}')" class="w-1/2 bg-brand-500 hover:bg-brand-600 text-white font-bold py-3 rounded-2xl transition-colors shadow-md flex items-center justify-center gap-1.5 outline-none">
                <i class="fa-solid fa-reply"></i>답장 쓰기
            </button>
            <button onclick="closeLetterDetailModal(); deleteLetter(${letter.id})" class="w-1/4 bg-rose-50 hover:bg-rose-100 text-rose-600 font-bold py-3 rounded-2xl transition-colors flex items-center justify-center gap-1 outline-none">
                <i class="fa-solid fa-trash"></i>삭제
            </button>
            <button onclick="closeLetterDetailModal()" class="w-1/4 bg-gray-100 hover:bg-gray-200 text-gray-700 font-bold py-3 rounded-2xl transition-colors outline-none">
                닫기
            </button>
        `;
    } else if (folder === 'sent') {
        actionContainer.innerHTML = `
            <button onclick="closeLetterDetailModal(); deleteLetter(${letter.id})" class="w-1/2 bg-rose-50 hover:bg-rose-100 text-rose-600 font-bold py-3 rounded-2xl transition-colors flex items-center justify-center gap-1 outline-none">
                <i class="fa-solid fa-trash"></i>삭제
            </button>
            <button onclick="closeLetterDetailModal()" class="w-1/2 bg-gray-100 hover:bg-gray-200 text-gray-700 font-bold py-3 rounded-2xl transition-colors outline-none">
                닫기
            </button>
        `;
    } else if (folder === 'trash') {
        actionContainer.innerHTML = `
            <button onclick="closeLetterDetailModal(); restoreLetter(${letter.id})" class="w-1/2 bg-brand-50 hover:bg-brand-100 text-brand-700 font-bold py-3 rounded-2xl transition-colors flex items-center justify-center gap-1.5 outline-none">
                <i class="fa-solid fa-trash-arrow-up"></i>편지 복원
            </button>
            <button onclick="closeLetterDetailModal(); deleteLetterPermanently(${letter.id})" class="w-1/4 bg-rose-50 hover:bg-rose-100 text-rose-600 font-bold py-3 rounded-2xl transition-colors flex items-center justify-center gap-1 outline-none">
                <i class="fa-solid fa-circle-xmark"></i>영구 삭제
            </button>
            <button onclick="closeLetterDetailModal()" class="w-1/4 bg-gray-100 hover:bg-gray-200 text-gray-700 font-bold py-3 rounded-2xl transition-colors outline-none">
                닫기
            </button>
        `;
    }

    const modal = document.getElementById('letter-detail-modal');
    if (modal) {
        modal.classList.remove('hidden');
        modal.classList.add('flex');
    }
}

function closeLetterDetailModal() {
    const modal = document.getElementById('letter-detail-modal');
    if (modal) {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }
}

function openWriteLetterModal(replyToSender = null, originalContent = null) {
    // 폼 초기화
    document.getElementById('letter-write-content').value = '';
    const customReceiver = document.getElementById('letter-write-receiver-custom');
    customReceiver.value = '';
    customReceiver.readOnly = false;
    customReceiver.classList.remove('bg-gray-100');
    
    document.getElementById('letter-write-char-count').innerText = "0 / 300자";

    // 답장 인용 영역 초기화
    const quoteContainer = document.getElementById('letter-reply-quote-container');
    const quoteText = document.getElementById('letter-reply-quote-text');
    if (originalContent) {
        quoteText.innerText = originalContent;
        quoteContainer.classList.remove('hidden');
        quoteContainer.classList.add('block');
    } else {
        quoteContainer.classList.add('hidden');
        quoteContainer.classList.remove('block');
    }

    // 받는 사람 리스트 바인딩
    const receiverSelect = document.getElementById('letter-write-receiver-select');
    receiverSelect.innerHTML = '<option value="">-- 이웃 집사 선택 --</option>';
    receiverSelect.disabled = false;
    
    if (typeof friends !== 'undefined' && friends.length > 0) {
        friends.forEach(f => {
            const opt = document.createElement('option');
            opt.value = f.nickname;
            opt.innerText = `${f.nickname} (${f.petName || '우리 아이'})`;
            receiverSelect.appendChild(opt);
        });
    }

    // 답장 수신인 pre-fill 설정
    if (replyToSender) {
        // 친구 목록에 있는지 검색
        let optionExists = false;
        for (let i = 0; i < receiverSelect.options.length; i++) {
            if (receiverSelect.options[i].value === replyToSender) {
                receiverSelect.selectedIndex = i;
                optionExists = true;
                break;
            }
        }
        
        receiverSelect.disabled = true;
        customReceiver.value = replyToSender;
        customReceiver.readOnly = true;
        customReceiver.classList.add('bg-gray-100');
        
        document.getElementById('letter-write-title').innerHTML = `<i class="fa-solid fa-reply text-brand-500 mr-2 text-base"></i>'${replyToSender}' 집사에게 답장 보내기`;
    } else {
        document.getElementById('letter-write-title').innerHTML = `<i class="fa-solid fa-envelope-open-text text-brand-500 mr-2 text-base"></i>마음을 담은 편지 쓰기`;
    }

    // 보낸 아이 리스트 바인딩
    const petSelect = document.getElementById('letter-write-sender-pet');
    petSelect.innerHTML = '';
    if (typeof pets !== 'undefined' && pets.length > 0) {
        pets.forEach(p => {
            const opt = document.createElement('option');
            opt.value = p.name;
            opt.innerText = `${p.name} (${p.breed || '반려동물'})`;
            petSelect.appendChild(opt);
        });
    } else {
        const opt = document.createElement('option');
        opt.value = "우리 아이";
        opt.innerText = "우리 아이";
        petSelect.appendChild(opt);
    }

    // 우표 기본값 리셋
    window.activeLetterStamp = '🐾';
    document.getElementById('postcard-stamp-slot').innerText = '🐾';
    const stampButtons = document.querySelectorAll('#stamp-selector-grid .stamp-btn');
    stampButtons.forEach((btn, index) => {
        btn.classList.remove('border-brand-500', 'bg-brand-50');
        btn.classList.add('border-transparent', 'bg-gray-50');
        if (index === 0) {
            btn.classList.remove('border-transparent', 'bg-gray-50');
            btn.classList.add('border-brand-500', 'bg-brand-50');
        }
    });

    const modal = document.getElementById('letter-write-modal');
    if (modal) {
        modal.classList.remove('hidden');
        modal.classList.add('flex');
    }
}

function closeLetterWriteModal() {
    const modal = document.getElementById('letter-write-modal');
    if (modal) {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }
}

function selectPostcardStamp(stamp, element) {
    window.activeLetterStamp = stamp;
    const slot = document.getElementById('postcard-stamp-slot');
    if (slot) {
        slot.innerText = stamp;
    }
    
    // 버튼 테두리 강조 리셋
    const buttons = document.querySelectorAll('#stamp-selector-grid .stamp-btn');
    buttons.forEach(btn => {
        btn.classList.remove('border-brand-500', 'bg-brand-50');
        btn.classList.add('border-transparent', 'bg-gray-50');
    });
    
    if (element) {
        element.classList.remove('border-transparent', 'bg-gray-50');
        element.classList.add('border-brand-500', 'bg-brand-50');
    }
}

function handleReceiverSelectChange() {
    const select = document.getElementById('letter-write-receiver-select');
    const custom = document.getElementById('letter-write-receiver-custom');
    if (select.value) {
        custom.value = select.value;
        custom.readOnly = true;
        custom.classList.add('bg-gray-100');
    } else {
        custom.value = '';
        custom.readOnly = false;
        custom.classList.remove('bg-gray-100');
    }
}

function updateLetterCharCount() {
    const content = document.getElementById('letter-write-content').value;
    document.getElementById('letter-write-char-count').innerText = `${content.length} / 300자`;
}

function submitLetter() {
    const select = document.getElementById('letter-write-receiver-select');
    const custom = document.getElementById('letter-write-receiver-custom');
    const receiver = select.value || custom.value.trim();

    if (!receiver) {
        showToast("받는 집사의 이름을 선택하거나 입력해주세요.");
        return;
    }

    const content = document.getElementById('letter-write-content').value.trim();
    if (!content) {
        showToast("전하고 싶은 마음이 담긴 내용을 적어주세요.");
        return;
    }

    const petName = document.getElementById('letter-write-sender-pet').value;
    const stamp = window.activeLetterStamp || '🐾';

    const newLetter = {
        id: Date.now(),
        sender: settings_nickname || "나",
        receiver: receiver,
        petName: petName,
        content: `[To. ${receiver}] ${content}`,
        date: "방금 전",
        isRead: true,
        folder: 'sent',
        stamp: stamp
    };

    letters.unshift(newLetter);
    saveState();
    closeLetterWriteModal();
    
    // 만약 현재 보낸편지함이 아닐 시 보낸편지함으로 강제 전환하여 직관적 발송 확인 지원
    switchMailboxFolder('sent');
    showToast(`'${receiver}' 집사님에게 따뜻한 편지가 전송되었습니다! 🕊️`);
}

function replyToLetter(sender, originalContent = null) {
    openWriteLetterModal(sender, originalContent);
}

function deleteLetter(id) {
    showCustomDialog({
        title: "편지 삭제 🗑️",
        message: "정말 이 편지를 휴지통으로 이동하시겠습니까?",
        type: "confirm",
        onConfirm: () => {
            const letter = letters.find(l => l.id === id);
            if (letter) {
                letter.originalFolder = getLetterFolder(letter);
                letter.folder = 'trash';
                saveState();
                renderMailbox();
                updateMailboxBadge();
                showToast("편지가 휴지통으로 이동되었습니다.");
            }
        }
    });
}

function restoreLetter(id) {
    const letter = letters.find(l => l.id === id);
    if (letter) {
        letter.folder = letter.originalFolder || 'inbox';
        letter.originalFolder = null;
        saveState();
        renderMailbox();
        updateMailboxBadge();
        showToast("편지가 원래 편지함으로 복원되었습니다.");
    }
}

function deleteLetterPermanently(id) {
    showCustomDialog({
        title: "영구 삭제 확인 ⚠️",
        message: "정말 이 편지를 영구히 삭제하시겠습니까? 삭제 후에는 복구할 수 없습니다.",
        type: "confirm",
        onConfirm: () => {
            letters = letters.filter(l => l.id !== id);
            saveState();
            renderMailbox();
            updateMailboxBadge();
            showToast("편지가 영구 삭제되었습니다.");
        }
    });
}

function emptyTrash() {
    showCustomDialog({
        title: "휴지통 비우기 확인 🗑️",
        message: "휴지통에 보관된 모든 편지를 영구 삭제하시겠습니까? 이 작업은 되돌릴 수 없습니다.",
        type: "confirm",
        onConfirm: () => {
            letters = letters.filter(l => getLetterFolder(l) !== 'trash');
            saveState();
            renderMailbox();
            updateMailboxBadge();
            showToast("휴지통이 비워졌습니다.");
        }
    });
}

function updateMailboxBadge() {
    const inboxUnread = letters.filter(l => getLetterFolder(l) === 'inbox' && !l.isRead).length;
    const sentCount = letters.filter(l => getLetterFolder(l) === 'sent').length;
    const trashCount = letters.filter(l => getLetterFolder(l) === 'trash').length;

    // 헤더 네비게이션 뱃지 업데이트
    const badge = document.getElementById('mailbox-count-badge');
    if (badge) {
        badge.innerText = inboxUnread;
        badge.style.display = inboxUnread > 0 ? 'flex' : 'none';
    }
    // 모바일 바텀 네비 뱃지 업데이트
    const mobileBadge = document.getElementById('mailbox-mobile-unread');
    if (mobileBadge) {
        mobileBadge.innerText = inboxUnread;
        if (inboxUnread > 0) {
            mobileBadge.classList.remove('hidden');
        } else {
            mobileBadge.classList.add('hidden');
        }
    }
    // 마이펫 룸 우체통 바로가기 뱃지 업데이트
    const roomBadge = document.getElementById('mailbox-unread-count-badge');
    if (roomBadge) {
        roomBadge.innerText = inboxUnread;
        if (inboxUnread > 0) {
            roomBadge.classList.remove('hidden');
        } else {
            roomBadge.classList.add('hidden');
        }
    }

    // 소셜 탭 내의 우체통 서브탭 뱃지 업데이트
    const socialSubtabBadge = document.getElementById('social-mailbox-unread-badge');
    if (socialSubtabBadge) {
        socialSubtabBadge.innerText = inboxUnread;
        if (inboxUnread > 0) {
            socialSubtabBadge.classList.remove('hidden');
        } else {
            socialSubtabBadge.classList.add('hidden');
        }
    }

    // 사내 우체통 로컬 탭 뱃지 업데이트
    const inboxBadgeEl = document.getElementById('mailbox-inbox-badge');
    if (inboxBadgeEl) {
        inboxBadgeEl.innerText = inboxUnread;
        if (inboxUnread > 0) {
            inboxBadgeEl.classList.remove('hidden');
            inboxBadgeEl.classList.add('inline-block');
        } else {
            inboxBadgeEl.classList.add('hidden');
            inboxBadgeEl.classList.remove('inline-block');
        }
    }
    const sentBadgeEl = document.getElementById('mailbox-sent-badge');
    if (sentBadgeEl) {
        sentBadgeEl.innerText = sentCount;
        if (sentCount > 0) {
            sentBadgeEl.classList.remove('hidden');
            sentBadgeEl.classList.add('inline-block');
        } else {
            sentBadgeEl.classList.add('hidden');
            sentBadgeEl.classList.remove('inline-block');
        }
    }
    const trashBadgeEl = document.getElementById('mailbox-trash-badge');
    if (trashBadgeEl) {
        trashBadgeEl.innerText = trashCount;
        if (trashCount > 0) {
            trashBadgeEl.classList.remove('hidden');
            trashBadgeEl.classList.add('inline-block');
        } else {
            trashBadgeEl.classList.add('hidden');
            trashBadgeEl.classList.remove('inline-block');
        }
    }
}