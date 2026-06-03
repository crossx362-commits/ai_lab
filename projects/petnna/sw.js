const CACHE = 'petna-v4';
const STATIC = [
    './',
    './index.html',
    './css/style.css',
    './css/leaflet.css',
    './js/tailwind.js',
    './js/leaflet.js',
    './js/chart.umd.min.js',
    './js/state.js',
    './js/app.js',
    './js/mypet.js',
    './js/walk.js',
    './js/social.js',
    './js/album.js',
    './js/saju.js',
    './js/shop.js',
    './js/settings.js',
    './js/mailbox.js',
    './js/ai-health.js',
    './js/freemium.js',
    './js/share-card.js',
    './js/health-dashboard.js',
    './js/achievements.js',
    './manifest.json',
];

self.addEventListener('install', e => {
    e.waitUntil(
        caches.open(CACHE).then(c => c.addAll(STATIC)).then(() => self.skipWaiting())
    );
});

self.addEventListener('activate', e => {
    e.waitUntil(
        caches.keys().then(keys =>
            Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k)))
        ).then(() => self.clients.claim())
    );
});

// Cache-first for static, network-first for API
self.addEventListener('fetch', e => {
    if (e.request.url.includes('generativelanguage.googleapis.com') ||
        e.request.url.includes('supabase.co') ||
        e.request.url.includes('fonts.googleapis.com') ||
        e.request.url.includes('cdn.jsdelivr.net')) {
        return;
    }
    e.respondWith(
        caches.match(e.request).then(cached => cached || fetch(e.request).catch(() => cached))
    );
});

// 앱에서 보낸 알림 스케줄 메시지 처리
self.addEventListener('message', e => {
    if (e.data?.type === 'SCHEDULE_STREAK_REMINDER') {
        const { petName, streak, delayMs } = e.data;
        setTimeout(() => {
            self.registration.showNotification('🔥 스트릭을 지켜요!', {
                body: `${petName}의 ${streak}일 연속 건강 기록이 오늘 끊길 수 있어요. 지금 기록해주세요 🐾`,
                icon: '/manifest.json',
                badge: '/manifest.json',
                tag: 'streak-reminder',
                renotify: false,
                data: { url: '/' }
            });
        }, delayMs);
    }

    if (e.data?.type === 'SCHEDULE_AI_REMINDER') {
        const { petName, delayMs } = e.data;
        setTimeout(() => {
            self.registration.showNotification('🏥 AI 건강 분석', {
                body: `${petName}의 이번 달 AI 건강 분석이 아직 남아있어요. 사진 한 장으로 확인해보세요!`,
                icon: '/manifest.json',
                tag: 'ai-reminder',
                renotify: false,
                data: { url: '/' }
            });
        }, delayMs);
    }
});

// 알림 클릭 시 앱 열기
self.addEventListener('notificationclick', e => {
    e.notification.close();
    e.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(clientList => {
            if (clientList.length > 0) {
                return clientList[0].focus();
            }
            return clients.openWindow(e.notification.data?.url || '/');
        })
    );
});
