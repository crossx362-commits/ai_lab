let vetChatHistory = [];

async function sendVetChatMessage(userMessage) {
    if (!userMessage || !userMessage.trim()) return;

    const apiKey = window._env_?.GEMINI_API_KEY || "";
    if (!apiKey) {
        appendVetChatMessage('model', 'GEMINI_API_KEY가 설정되지 않았습니다.');
        return;
    }

    const input = document.getElementById('vet-chat-input');
    if (input) input.value = '';

    appendVetChatMessage('user', userMessage.trim());

    const pet = (typeof getActivePet === 'function') ? getActivePet() : null;
    const petName = pet?.name || '반려동물';
    const breed = pet?.breed || '품종 미상';
    const age = pet?.age ? `${pet.age}살` : '나이 미상';

    const systemPrompt = `당신은 10년 경력의 친절한 수의사 AI 어시스턴트입니다.
현재 상담 중인 반려동물: ${petName} (${breed}, ${age})
반려동물의 증상이나 건강 관련 질문에 답변해주세요.
의학적으로 긴급한 경우 즉시 동물병원 방문을 권고하세요.
답변은 한국어로, 친근하고 이해하기 쉽게 해주세요.`;

    vetChatHistory.push({ role: 'user', text: userMessage.trim() });

    const contents = [
        { role: 'user', parts: [{ text: systemPrompt }] },
        { role: 'model', parts: [{ text: `안녕하세요! ${petName}의 건강을 함께 챙겨드릴게요. 무엇이 걱정되시나요?` }] },
        ...vetChatHistory.map(m => ({
            role: m.role,
            parts: [{ text: m.text }]
        }))
    ];

    const loadingId = 'vet-loading-' + Date.now();
    appendVetChatLoading(loadingId);

    try {
        const res = await fetch(
            `https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=${apiKey}`,
            {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ contents })
            }
        );
        if (!res.ok) throw new Error(`API ${res.status}`);
        const data = await res.json();
        const text = data.candidates?.[0]?.content?.parts?.[0]?.text || '답변을 가져올 수 없습니다.';

        removeVetChatLoading(loadingId);
        vetChatHistory.push({ role: 'model', text });
        appendVetChatMessage('model', text);
    } catch (e) {
        removeVetChatLoading(loadingId);
        appendVetChatMessage('model', `오류가 발생했습니다: ${e.message}`);
        vetChatHistory.pop();
    }
}

function appendVetChatMessage(role, text) {
    const container = document.getElementById('vet-chat-messages');
    if (!container) return;

    const el = document.createElement('div');
    el.className = role === 'user'
        ? 'flex justify-end'
        : 'flex justify-start';

    const bubble = document.createElement('div');
    bubble.className = role === 'user'
        ? 'bg-brand-500 text-white rounded-2xl rounded-tr-sm px-4 py-2 text-sm max-w-[80%] leading-relaxed'
        : 'bg-gray-100 text-gray-800 rounded-2xl rounded-tl-sm px-4 py-2 text-sm max-w-[80%] leading-relaxed';
    bubble.textContent = text;

    el.appendChild(bubble);
    container.appendChild(el);
    container.scrollTop = container.scrollHeight;
}

function appendVetChatLoading(id) {
    const container = document.getElementById('vet-chat-messages');
    if (!container) return;

    const el = document.createElement('div');
    el.id = id;
    el.className = 'flex justify-start';
    el.innerHTML = '<div class="bg-gray-100 text-gray-500 rounded-2xl rounded-tl-sm px-4 py-2 text-sm">입력 중...</div>';
    container.appendChild(el);
    container.scrollTop = container.scrollHeight;
}

function removeVetChatLoading(id) {
    const el = document.getElementById(id);
    if (el) el.remove();
}

function renderVetChat() {
    const container = document.getElementById('vet-chat-messages');
    if (!container) return;
    container.innerHTML = '';

    const pet = (typeof getActivePet === 'function') ? getActivePet() : null;
    const petName = pet?.name || '반려동물';

    const welcomeEl = document.createElement('div');
    welcomeEl.className = 'flex justify-start';
    welcomeEl.innerHTML = `<div class="bg-gray-100 text-gray-800 rounded-2xl rounded-tl-sm px-4 py-2 text-sm max-w-[80%] leading-relaxed">안녕하세요! 저는 AI 수의사입니다. <strong>${petName}</strong>에 대해 궁금한 점이나 걱정되는 증상이 있으면 편하게 질문해 주세요.</div>`;
    container.appendChild(welcomeEl);

    vetChatHistory.forEach(m => appendVetChatMessage(m.role, m.text));
}

function openVetChatModal() {
    const modal = document.getElementById('vet-chat-modal');
    if (!modal) return;
    modal.classList.remove('hidden');
    modal.classList.add('flex');
    renderVetChat();
    setTimeout(() => {
        const input = document.getElementById('vet-chat-input');
        if (input) input.focus();
    }, 100);
}

function closeVetChatModal() {
    const modal = document.getElementById('vet-chat-modal');
    if (!modal) return;
    modal.classList.add('hidden');
    modal.classList.remove('flex');
}

function handleVetChatKeydown(e) {
    if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        const input = document.getElementById('vet-chat-input');
        if (input) sendVetChatMessage(input.value);
    }
}
