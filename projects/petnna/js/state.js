const INITIAL_PETS = [
    { id: 101, name: "댕이",  breed: "골든 리트리버", type: "dog",     imageUrl: "https://images.unsplash.com/photo-1552053831-71594a27632d?auto=format&fit=crop&q=80&w=300", age: "2살 (청소년기)", weight: "24.5", gender: "남아 (중성화 완료)", personality: "산책과 간식을 지향함", hunger: 70, happy: 80 },
    { id: 102, name: "나비",  breed: "샴 고양이",     type: "cat",     imageUrl: "https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba?auto=format&fit=crop&q=80&w=300", age: "3살 (성묘)", weight: "4.2", gender: "여아 (중성화 완료)", personality: "도도하고 조용함", hunger: 60, happy: 75 },
    { id: 103, name: "솜이",  breed: "드워프 토끼",   type: "rabbit",  imageUrl: "https://images.unsplash.com/photo-1585110396000-c9ffd4e4b308?auto=format&fit=crop&q=80&w=300", age: "1살 (청소년기)", weight: "1.8", gender: "여아", personality: "겁이 많고 당근 러버", hunger: 80, happy: 85 },
    { id: 104, name: "햄토리", breed: "골든 햄스터",   type: "hamster", imageUrl: "https://images.unsplash.com/photo-1425082661705-1834bfd09dca?auto=format&fit=crop&q=80&w=300", age: "1살", weight: "0.12", gender: "남아", personality: "활발하고 쳇바퀴 마니아", hunger: 65, happy: 90 }
];

const INITIAL_POSTS = [
    { id: 201, petName: "초코", petAvatar: "https://images.unsplash.com/photo-1537151625747-768eb6cf92b2?auto=format&fit=crop&q=80&w=150", content: "송도 센트럴파크 놀이터 다녀왔어요! 🌳 날씨 너무 좋고 댕댕이 친구들도 많아서 초코가 완전히 신났었네요 🐶 꼬리가 프로펠러처럼 돌아갔답니다!", image: "https://images.unsplash.com/photo-1548199973-03cce0bbc87b?auto=format&fit=crop&q=80&w=400", isVideo: false, likes: 5, liked: false, comments: [{ author: "체리맘", text: "초코 너무 활기차고 이쁘네요! 부러워요 ㅎㅎ" }] }
];

const INITIAL_FRIENDS = [
    { id: 501, nickname: "초코언니", petName: "초코", petBreed: "말티즈", petType: "dog", personality: "얌전하고 애교가 많음", avatar: "https://images.unsplash.com/photo-1587300003388-59208cc962cb?auto=format&fit=crop&q=80&w=150", status: "online", chemistry: 95, unread: 0, petBirthday: "2026-06-03" },
    { id: 502, nickname: "샤미마미", petName: "나비", petBreed: "샴 고양이", petType: "cat", personality: "도도하고 도망치기 명수", avatar: "https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba?auto=format&fit=crop&q=80&w=150", status: "online", chemistry: 84, unread: 0, petBirthday: "2025-11-20" },
    { id: 503, nickname: "귀쫑긋집사", petName: "솜이", petBreed: "드워프 토끼", petType: "rabbit", personality: "겁이 많고 당근 러버", avatar: "https://images.unsplash.com/photo-1585110396000-c9ffd4e4b308?auto=format&fit=crop&q=80&w=150", status: "offline", chemistry: 72, unread: 0, petBirthday: "2025-03-15" }
];

const INITIAL_CHATS = {
    501: [
        { sender: "friend", time: "어제", text: "안녕하세요! 혹시 송도 센트럴파크 잔디 구역 가보셨나요? 댕이가 거기서 친구들 엄청 좋아해요 🐶" },
        { sender: "me", time: "어제", text: "아 네! 센트럴파크 놀이터 자주 갑니다 ㅎㅎ 담에 마주치면 비스킷 선물할게요!" }
    ],
    502: [
        { sender: "friend", time: "오후 2:15", text: "집사님네 고양이도 상자 조그마한 거에 억지로 들어가려 하나요? 나비가 맨날 택배박스 전세 냈어요 📦" }
    ],
    503: [
        { sender: "friend", time: "3일 전", text: "토끼 발톱 정리하기 꿀팁 있을까요? 너무 버둥거려서 스파하듯이 감싸서 하고 있는데 쉽지 않네요 🐰" }
    ]
};

const INITIAL_PRODUCTS = [
    { id: 1, name: "유기농 락토프리 산양유 6개입", price: 12500, category: "food", desc: "영양 만점, 소화가 잘 되는 신선한 펫 전용 밀크입니다.", image: "https://images.unsplash.com/photo-1527156278757-0cebb01a3570?auto=format&fit=crop&q=80&w=200" },
    { id: 2, name: "바스락 황태 노즈워크 장난감", price: 8900, category: "toy", desc: "스트레스 완화와 지능 발달에 탁월한 황태 촉감 완구입니다.", image: "https://images.unsplash.com/photo-1576201836106-db1758fd1c97?auto=format&fit=crop&q=80&w=200" },
    { id: 3, name: "프리미엄 펫 케어 무자극 샴푸", price: 18000, category: "care", desc: "민감성 피부를 진정시키는 고순도 허브 안심 추출 샴푸.", image: "https://images.unsplash.com/photo-1608248597279-f99d160bfcbc?auto=format&fit=crop&q=80&w=200" },
    { id: 4, name: "부들부들 천연 마 가시 빗", price: 11000, category: "care", desc: "부드럽게 엉킨 털을 완벽 정리하고 관절 마사지 효과까지 부여.", image: "https://images.unsplash.com/photo-1615087240969-eeff2fa558f2?auto=format&fit=crop&q=80&w=200" }
];

const INITIAL_PLACES = [
    // 서울
    { id: 1, name: "서울숲 반려견 놀이터", lat: 37.5445, lng: 127.0374, category: "park", desc: "성수동 서울숲 내 대형 오프리쉬 운동장. 한강 뷰와 함께 신나게 뛰어놀 수 있습니다.", rating: 4.8, reviews: [{ author: "초코맘", rating: 5, comment: "드넓어서 강아지가 정말 좋아해요!" }] },
    { id: 2, name: "망원 한강공원 반려동물 구역", lat: 37.5551, lng: 126.9008, category: "park", desc: "한강변 드넓은 잔디밭에서 반려동물과 피크닉을 즐길 수 있습니다.", rating: 4.7, reviews: [{ author: "뽀삐아빠", rating: 5, comment: "한강 뷰 최고! 주차도 편해요." }] },
    { id: 3, name: "댕댕이카페 홍대점", lat: 37.5565, lng: 126.9236, category: "cafe", desc: "홍대 인근 반려동물 동반 가능 브런치 카페. 펫 전용 메뉴 다양.", rating: 4.6, reviews: [{ author: "체리누나", rating: 5, comment: "강아지 케이크가 너무 귀엽고 맛있어요!" }] },
    { id: 4, name: "24시 서울 동물병원 강남", lat: 37.4979, lng: 127.0276, category: "hospital", desc: "강남구 야간 응급 진료 가능 동물병원. MRI·CT 보유 종합 의료시설.", rating: 4.8, reviews: [{ author: "푸들맘", rating: 5, comment: "야간에도 빠르게 처치해 주셨어요." }] },
    // 인천·송도
    { id: 5, name: "송도 센트럴파크 도그파크", lat: 37.3945, lng: 126.6380, category: "park", desc: "대형견/중소형견 전용 구역 분리된 오프리쉬 운동장. 인조잔디 완비.", rating: 4.9, reviews: [{ author: "코코누나", rating: 5, comment: "안전 펜스도 튼튼하고 에티켓도 좋아요!" }] },
    { id: 6, name: "카페 멍멍랜드 송도", lat: 37.3915, lng: 126.6432, category: "cafe", desc: "무염 댕스무디, 황태 컵케이크 등 펫 전용 메뉴가 풍부한 브런치 카페.", rating: 4.7, reviews: [{ author: "해피맘", rating: 5, comment: "댕푸치노 최고!" }] },
    { id: 7, name: "스마트 동물의료센터 송도", lat: 37.3882, lng: 126.6348, category: "hospital", desc: "연중무휴 야간 응급 진료 종합 동물의료시설.", rating: 4.5, reviews: [{ author: "비숑맘", rating: 4, comment: "야간에도 꼼꼼히 치료해 주셨어요." }] },
    // 경기
    { id: 8, name: "판교 반려견 공원", lat: 37.3943, lng: 127.1097, category: "park", desc: "판교테크노밸리 인근 쾌적한 반려동물 테마파크. 수영장 시설 보유.", rating: 4.7, reviews: [{ author: "말티즈맘", rating: 5, comment: "수영장이 있어서 여름에 최고예요!" }] },
    { id: 9, name: "일산 호수공원 펫존", lat: 37.6599, lng: 126.7700, category: "park", desc: "호수를 끼고 산책 가능한 대규모 반려동물 허용 공원.", rating: 4.6, reviews: [{ author: "토리아빠", rating: 5, comment: "산책 코스가 아름다워요." }] },
    // 부산
    { id: 10, name: "해운대 반려견 해수욕장 구역", lat: 35.1588, lng: 129.1603, category: "park", desc: "해운대 해수욕장 내 반려동물 허용 구역. 바다 수영 가능!", rating: 4.9, reviews: [{ author: "제주강아지", rating: 5, comment: "바다에서 수영하는 강아지 모습이 너무 귀여워요!" }] },
    { id: 11, name: "광안리 펫 프렌들리 카페", lat: 35.1532, lng: 129.1182, category: "cafe", desc: "광안대교 뷰를 즐기며 반려동물과 함께하는 오션뷰 카페.", rating: 4.8, reviews: [{ author: "코기맘", rating: 5, comment: "뷰가 진짜 예술이에요!" }] }
];

const INITIAL_SCHEDULES = [
    { id: 301, date: "2026-05-18", title: "초코 종합백신 3차 예방접종", type: "vet" },
    { id: 302, date: "2026-05-22", title: "체리 미용실 스파 예약", type: "groom" },
    { id: 303, date: "2026-05-25", title: "산책로 정기 정모 모임", type: "walk" }
];

const INITIAL_WALKS = [
    { id: 401, date: "2일 전", duration: "18:42", distance: "1.42", calories: "124", poop: 1, pee: 2, sniff: 4, coords: [[37.3912, 126.6392], [37.3920, 126.6370], [37.3932, 126.6360], [37.3948, 126.6368]], marks: [{ lat: 37.3920, lng: 126.6370, type: "poop" }, { lat: 37.3932, lng: 126.6360, type: "pee" }] },
    { id: 402, date: "5일 전", duration: "24:10", distance: "2.10", calories: "185", poop: 0, pee: 1, sniff: 7, coords: [[37.3912, 126.6392], [37.3918, 126.6412], [37.3932, 126.6428]], marks: [{ lat: 37.3918, lng: 126.6412, type: "sniff" }] }
];

const INITIAL_ALBUM = [
    { url: "https://images.unsplash.com/photo-1534361960057-19889db9621e?auto=format&fit=crop&q=80&w=300", isVideo: false },
    { url: "https://assets.mixkit.co/videos/preview/mixkit-dog-running-on-the-beach-41712-large.mp4", isVideo: true, start: 1.0, end: 5.5 },
    { url: "https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba?auto=format&fit=crop&q=80&w=300", isVideo: false }
];

const INITIAL_LETTERS = [
    { id: 1, sender: "초코언니", petName: "초코", content: "오늘 센트럴파크에서 본 댕이가 너무 귀여웠어요! 담에 보면 간식 나눠먹어요 ㅎㅎ", date: "2026-05-18", isRead: false },
    { id: 2, sender: "샤미마미", petName: "나비", content: "안녕하세요! 고양이 모래 정보 공유해주셔서 감사해요. 덕분에 나비가 아주 좋아하네요.", date: "2026-05-17", isRead: true }
];

const INITIAL_HEALTH_LOGS = {
    today: {
        date: new Date().toISOString().split('T')[0],
        poop: null, // null, 'normal', 'hard', 'liquid'
        water: 0, // ml
        food: 0, // g
        condition: "happy" // 'happy', 'tired', 'sick'
    },
    history: []
};

// ==========================================
// 🏗️ 아키텍처 고도화: 상태 관리 및 로깅 시스템 정의
// ==========================================

const AppConstants = {
    StorageKeys: {
        USER_EMAIL: 'petna_user_email',
        USER_NICKNAME: 'petna_user_nickname',
        APP_THEME: 'petna_app_theme',
        NOTIFICATIONS_ENABLED: 'petna_notifications_enabled',
        NOTIFICATION_PERMISSION: 'petna_notification_permission_granted',
        APP_UNIT: 'petna_app_unit',
        PETS: 'petna_pets',
        POSTS: 'petna_posts',
        SCHEDULES: 'petna_schedules',
        WALKS: 'petna_walks',
        ALBUMS: 'petna_albums',
        CART: 'petna_cart',
        PLACES: 'petna_places',
        MEALS: 'petna_meals',
        FRIENDS: 'petna_friends',
        CHATS: 'petna_chats',
        LETTERS: 'petna_letters',
        HEALTH_LOGS: 'petna_health_logs',
        CUSTOM_ROUTES: 'petna_custom_routes',
    }
};

const AppLogger = {
    getErrorLogs() {
        try {
            return JSON.parse(localStorage.getItem('petna_error_logs')) || [];
        } catch (e) {
            return [];
        }
    },
    addErrorLog(type, message, errorObj) {
        try {
            const logs = this.getErrorLogs();
            let stack = '';
            if (errorObj) {
                if (errorObj.stack) {
                    stack = errorObj.stack;
                } else if (typeof errorObj === 'object') {
                    try {
                        stack = JSON.stringify(errorObj, Object.getOwnPropertyNames(errorObj));
                    } catch (jsonErr) {
                        stack = String(errorObj);
                    }
                } else {
                    stack = String(errorObj);
                }
            }
            const newLog = {
                id: Date.now() + Math.random(),
                timestamp: new Date().toISOString(),
                type: type,
                message: message,
                stack: stack
            };
            logs.unshift(newLog);
            if (logs.length > 50) {
                logs.pop();
            }
            localStorage.setItem('petna_error_logs', JSON.stringify(logs));
        } catch (e) {
            console.error('Failed to write error log to storage', e);
        }
    },
    clearErrorLogs() {
        try {
            localStorage.removeItem('petna_error_logs');
        } catch (e) {
            console.error('Failed to clear error logs', e);
        }
    },
    info(message, ...args) {
        console.log(`%c[INFO] ${message}`, 'color: #0ea5e9; font-weight: bold;', ...args);
    },
    warn(message, ...args) {
        console.warn(`%c[WARN] ${message}`, 'color: #f59e0b; font-weight: bold;', ...args);
        this.addErrorLog('warn', message, args[0]);
        // warn은 토스트 미표시 — 사용자 노출이 필요한 경우 error() 사용
    },
    error(message, errorObj, ...args) {
        console.error(`%c[ERROR] ${message}`, 'color: #ef4444; font-weight: bold;', errorObj, ...args);
        this.addErrorLog('error', message, errorObj);
        if (typeof showToast === 'function') {
            showToast(`❌ ${message}: ${errorObj ? errorObj.message || errorObj : ''}`);
        }
    }
};

const AppStore = {
    _state: {
        pets: INITIAL_PETS,
        posts: INITIAL_POSTS,
        schedules: INITIAL_SCHEDULES,
        walks: INITIAL_WALKS,
        albums: INITIAL_ALBUM,
        cart: [],
        places: INITIAL_PLACES,
        meals: [
            { id: 1, type: "아침", time: "08:30", notes: "유기농 연어 사료 70g" },
            { id: 2, type: "간식", time: "14:15", notes: "황태 슬라이스 상호작용" }
        ],
        friends: INITIAL_FRIENDS,
        chatHistories: INITIAL_CHATS,
        activeChatFriendId: 502,
        friendRequests: [
            { id: 601, nickname: "웰시코기_쿵", petName: "쿵이", petBreed: "웰시코기", personality: "엉덩이가 매력적이고 활발함", avatar: "https://images.unsplash.com/photo-1583511655857-d19b40a7a54e?auto=format&fit=crop&q=80&w=150" }
        ],
        letters: INITIAL_LETTERS,
        healthLogs: INITIAL_HEALTH_LOGS,
        settings_email: localStorage.getItem('petna_user_email') || "butler@petna.co.kr",
        settings_nickname: "",
        settings_avatar: "",
        settings_photo_url: "",
        settings_theme: localStorage.getItem('petna_app_theme') || "light",
        settings_notifications_enabled: localStorage.getItem('petna_notifications_enabled') === 'true',
        settings_notification_permission_granted: localStorage.getItem('petna_notification_permission_granted') === 'true',
        settings_unit: localStorage.getItem('petna_app_unit') || "metric",
        statsChart: null,
        customRoutes: []
    },

    getState(key) {
        return this._state[key];
    },

    setState(key, value) {
        this._state[key] = value;
        // 데이터 상태 변경 시에만 저장 (settings_* 단순 변경은 호출자가 직접 save() 호출)
        const autoSaveKeys = new Set([
            'pets', 'posts', 'schedules', 'walks', 'albums',
            'cart', 'places', 'meals', 'friends', 'chatHistories', 'letters', 'healthLogs', 'customRoutes'
        ]);
        if (autoSaveKeys.has(key)) {
            this.save();
        }
    },

    load(email = "") {
        const targetEmail = email || this.getState('settings_email') || "butler@petna.co.kr";
        AppLogger.info(`Loading local storage state for ${targetEmail}`);

        try {
            const schema = {
                pets: { key: AppConstants.StorageKeys.PETS, fallback: [] },
                posts: { key: AppConstants.StorageKeys.POSTS, fallback: INITIAL_POSTS },
                schedules: { key: AppConstants.StorageKeys.SCHEDULES, fallback: INITIAL_SCHEDULES },
                walks: { key: AppConstants.StorageKeys.WALKS, fallback: INITIAL_WALKS },
                albums: { key: AppConstants.StorageKeys.ALBUMS, fallback: INITIAL_ALBUM },
                cart: { key: AppConstants.StorageKeys.CART, fallback: [] },
                places: { key: AppConstants.StorageKeys.PLACES, fallback: INITIAL_PLACES },
                meals: { 
                    key: AppConstants.StorageKeys.MEALS, 
                    fallback: [
                        { id: 1, type: "아침", time: "08:30", notes: "유기농 연어 사료 70g" },
                        { id: 2, type: "간식", time: "14:15", notes: "황태 슬라이스 상호작용" }
                    ] 
                },
                friends: { key: AppConstants.StorageKeys.FRIENDS, fallback: INITIAL_FRIENDS },
                chatHistories: { key: AppConstants.StorageKeys.CHATS, fallback: INITIAL_CHATS },
                letters: { key: AppConstants.StorageKeys.LETTERS, fallback: INITIAL_LETTERS },
                healthLogs: { key: AppConstants.StorageKeys.HEALTH_LOGS, fallback: INITIAL_HEALTH_LOGS },
                customRoutes: { key: AppConstants.StorageKeys.CUSTOM_ROUTES, fallback: [] }
            };

            Object.keys(schema).forEach(stateKey => {
                const config = schema[stateKey];
                const raw = localStorage.getItem(`${config.key}_${targetEmail}`) || localStorage.getItem(config.key);
                if (raw) {
                    try {
                        this._state[stateKey] = JSON.parse(raw);
                    } catch {
                        this._state[stateKey] = config.fallback;
                        AppLogger.warn(`Corrupted state reset to fallback: ${stateKey}`);
                    }
                } else {
                    this._state[stateKey] = config.fallback;
                }
            });

            this._state.settings_nickname = localStorage.getItem(`${AppConstants.StorageKeys.USER_NICKNAME}_${targetEmail}`) || localStorage.getItem(AppConstants.StorageKeys.USER_NICKNAME) || "초코 집사";
            this._state.settings_avatar = localStorage.getItem(`petna_user_avatar_${targetEmail}`) || localStorage.getItem(`petna_user_avatar`) || "🧔";
            this._state.settings_photo_url = localStorage.getItem(`petna_user_photo_url_${targetEmail}`) || localStorage.getItem(`petna_user_photo_url`) || "";
        } catch (error) {
            AppLogger.error("Failed to load user state", error);
        }
    },

    save() {
        const targetEmail = this.getState('settings_email') || "butler@petna.co.kr";
        AppLogger.info(`Saving current state for ${targetEmail}`);

        try {
            const schema = {
                pets: AppConstants.StorageKeys.PETS,
                posts: AppConstants.StorageKeys.POSTS,
                schedules: AppConstants.StorageKeys.SCHEDULES,
                walks: AppConstants.StorageKeys.WALKS,
                albums: AppConstants.StorageKeys.ALBUMS,
                cart: AppConstants.StorageKeys.CART,
                places: AppConstants.StorageKeys.PLACES,
                meals: AppConstants.StorageKeys.MEALS,
                friends: AppConstants.StorageKeys.FRIENDS,
                chatHistories: AppConstants.StorageKeys.CHATS,
                letters: AppConstants.StorageKeys.LETTERS,
                healthLogs: AppConstants.StorageKeys.HEALTH_LOGS,
                customRoutes: AppConstants.StorageKeys.CUSTOM_ROUTES
            };

            Object.keys(schema).forEach(stateKey => {
                const storageKey = schema[stateKey];
                const raw = JSON.stringify(this._state[stateKey]);
                try {
                    localStorage.setItem(`${storageKey}_${targetEmail}`, raw);
                    localStorage.setItem(storageKey, raw); // Fallback 백업
                } catch (setItemError) {
                    console.warn(`Local storage quota warning for key ${storageKey}:`, setItemError);
                }
            });

            localStorage.setItem('petna_notifications_enabled', this._state.settings_notifications_enabled);
            localStorage.setItem('petna_notification_permission_granted', this._state.settings_notification_permission_granted);
        } catch (error) {
            AppLogger.error("Failed to persist user state", error);
        }
    }
};

// 🔗 하위 호환 브릿지: 기존 전역 변수 호출을 AppStore와 바인딩
const _stateKeysToBind = [
    'pets', 'posts', 'schedules', 'walks', 'albums', 'cart', 
    'places', 'meals', 'friends', 'chatHistories', 'activeChatFriendId', 
    'friendRequests', 'letters', 'healthLogs', 'settings_email', 'settings_nickname', 
    'settings_avatar', 'settings_photo_url', 'settings_theme', 
    'settings_notifications_enabled', 'settings_notification_permission_granted', 
    'settings_unit', 'statsChart', 'customRoutes'
];

_stateKeysToBind.forEach(stateKey => {
    Object.defineProperty(window, stateKey, {
        get() {
            return AppStore.getState(stateKey);
        },
        set(value) {
            AppStore.setState(stateKey, value);
        },
        configurable: true
    });
});

// 초기 구동 로드
AppStore.load(window.settings_email);

function loadState(email = "") {
    AppStore.load(email);
}

function saveState() {
    AppStore.save();
}

function showCustomDialog({ title, message, icon = "🐾", type = "alert", placeholder = "", val = "", multiline = false, onConfirm = null, onCancel = null }) {
    const modal = document.getElementById('custom-dialog-modal');
    if (!modal) return;
    const titleEl = document.getElementById('dialog-title');
    const msgEl = document.getElementById('dialog-message');
    const iconContainer = document.getElementById('dialog-icon-container');
    const actionsContainer = document.getElementById('dialog-actions');

    // Remove any existing prompt inputs first
    const existingInput = modal.querySelector('input.dialog-prompt-input, textarea.dialog-prompt-input');
    if (existingInput) existingInput.remove();

    if (titleEl) titleEl.innerText = title;
    if (msgEl) msgEl.innerText = message;
    if (iconContainer) iconContainer.innerText = icon;

    let promptInput = null;
    if (type === "prompt") {
        if (multiline) {
            promptInput = document.createElement('textarea');
            promptInput.rows = 4;
        } else {
            promptInput = document.createElement('input');
            promptInput.type = "text";
        }
        promptInput.placeholder = placeholder || "";
        promptInput.value = val || "";
        promptInput.className = "dialog-prompt-input w-full border border-gray-200 rounded-xl p-2.5 mb-4 outline-none focus:border-brand-500 text-xs font-medium";
        if (!multiline) {
            promptInput.className += " text-center";
        }
        msgEl.parentNode.insertBefore(promptInput, actionsContainer);
        // focus the input
        setTimeout(() => promptInput.focus(), 100);
    }

    if (actionsContainer) {
        actionsContainer.innerHTML = '';

        if (type === "confirm" || type === "prompt") {
            const cancelBtn = document.createElement('button');
            cancelBtn.className = "w-1/2 bg-gray-100 hover:bg-gray-200 text-gray-700 font-bold py-2.5 rounded-xl text-xs transition-colors";
            cancelBtn.innerText = "취소";
            cancelBtn.onclick = () => {
                modal.classList.add('hidden');
                modal.classList.remove('flex');
                const existingInput = modal.querySelector('input.dialog-prompt-input, textarea.dialog-prompt-input');
                if (existingInput) existingInput.remove();
                if (onCancel) onCancel();
            };

            const okBtn = document.createElement('button');
            okBtn.className = "w-1/2 bg-brand-500 hover:bg-brand-600 text-white font-bold py-2.5 rounded-xl text-xs transition-colors shadow-md";
            okBtn.innerText = "확인";
            okBtn.onclick = () => {
                let inputVal = null;
                if (type === "prompt" && promptInput) {
                    inputVal = promptInput.value;
                }
                modal.classList.add('hidden');
                modal.classList.remove('flex');
                const existingInput = modal.querySelector('input.dialog-prompt-input, textarea.dialog-prompt-input');
                if (existingInput) existingInput.remove();
                if (onConfirm) onConfirm(inputVal);
            };

            actionsContainer.appendChild(cancelBtn);
            actionsContainer.appendChild(okBtn);
        } else {
            const okBtn = document.createElement('button');
            okBtn.className = "w-full bg-brand-500 hover:bg-brand-600 text-white font-bold py-2.5 rounded-xl text-xs transition-colors shadow-md";
            okBtn.innerText = "확인";
            okBtn.onclick = () => {
                modal.classList.add('hidden');
                modal.classList.remove('flex');
                if (onConfirm) onConfirm();
            };
            actionsContainer.appendChild(okBtn);
        }
    }

    modal.classList.remove('hidden');
    modal.classList.add('flex');
}

function showToast(message) {
    const toast = document.getElementById('toast-message');
    const toastText = document.getElementById('toast-text');
    if (!toast || !toastText) return;
    toastText.innerText = message;
    toast.style.opacity = '1';
    setTimeout(() => {
        toast.style.opacity = '0';
    }, 2500);
}

// 🚨 전역 자바스크립트 오류 및 unhandledrejection 예외 수집기
window.addEventListener('error', function (event) {
    if (typeof AppLogger !== 'undefined' && AppLogger.addErrorLog) {
        const errorObj = event.error;
        const stackStr = errorObj && errorObj.stack 
            ? errorObj.stack 
            : `${event.filename || 'unknown'}:${event.lineno || 0}:${event.colno || 0}`;
        AppLogger.addErrorLog('global_error', event.message || 'Unknown runtime error', {
            message: event.message,
            stack: stackStr
        });
    }
});

window.addEventListener('unhandledrejection', function (event) {
    if (typeof AppLogger !== 'undefined' && AppLogger.addErrorLog) {
        const reason = event.reason;
        const msg = reason ? (reason.message || String(reason)) : 'Unhandled promise rejection';
        AppLogger.addErrorLog('global_rejection', msg, reason);
    }
});
