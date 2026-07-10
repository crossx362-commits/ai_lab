// ====================================================
// 펫라이프 실제 가맹점 데이터베이스
// ====================================================

const PETLIFE_REAL_LOCATIONS = [
    // 🏥 동물병원
    {
        id: 'hospital-snc',
        name: 'SNC 동물메디컬센터',
        category: 'hospital',
        emoji: '🏥',
        address: '서울특별시 강남구 역삼동',
        phone: '02-6959-7979',
        hours: '24시간 운영',
        description: '강남 역삼동 24시 응급센터. 최신 의료 장비를 갖춘 종합 동물병원',
        website: 'https://www.sncamc.co.kr/',
        services: ['24시 응급진료', '외과수술', '내과진료', '건강검진', '입원케어'],
        tags: ['야간진료', '예약가능', '평균진료비 높음'],
        color: '#dc2626',
        lat: 37.4979, // 강남구 역삼동
        lng: 127.0376
    },
    {
        id: 'hospital-central',
        name: '24시 센트럴동물메디컬센터',
        category: 'hospital',
        emoji: '🏥',
        address: '서울시 성동구 고산자로 207',
        phone: '02-3395-7974',
        hours: '24시간 운영',
        description: '성동구 24시 응급 동물병원. 전문 수의사팀 상주',
        website: 'http://www.cenamc.kr/',
        services: ['24시 응급', 'CT/MRI', '심장검사', '안과진료'],
        tags: ['야간진료', '예약가능', '평균진료비 높음'],
        color: '#dc2626',
        lat: 37.5635, // 성동구
        lng: 127.0376
    },
    {
        id: 'hospital-24in',
        name: '동물의료센터 24인치',
        category: 'hospital',
        emoji: '⚕️',
        address: '서울시 영등포구 가마산로 496',
        phone: '02-6953-2427',
        hours: '24시간 운영',
        description: '영등포 24시 동물병원. 365일 언제나 진료 가능',
        website: 'https://amc24in.com/',
        services: ['응급진료', '외과', '내과', '피부과'],
        tags: ['야간진료', '예약가능', '평균진료비 보통'],
        color: '#e11d48',
        lat: 37.5326, // 영등포구
        lng: 126.9026
    },

    // 🏨 펫호텔 & 위탁
    {
        id: 'hotel-dogs',
        name: '호텔 독스',
        category: 'hotel',
        emoji: '🏨',
        address: '서울 강남구 (스마트 호텔)',
        phone: '1661-9974',
        hours: '24시간 입/퇴실 가능',
        description: '24시간 CCTV, 비밀번호 견사, 스마트 자동화 펫호텔',
        website: 'https://hoteldogs.co.kr/hoteling',
        services: ['24시간 CCTV', '비밀번호 견사', '자동 급식', '24시간 입퇴실'],
        color: '#a9583e',
        lat: 37.5172, // 강남구 중심
        lng: 127.0473
    },
    {
        id: 'hotel-ijoa',
        name: '아이조아펫파크',
        category: 'hotel',
        emoji: '🏨',
        address: '경기도 광주시',
        phone: '031-761-0579',
        hours: '09:00 - 18:00',
        description: '반려동물 테마공원. 펫호텔, 수영장, 놀이터 복합 시설',
        website: 'https://www.ijoapetpark.com/ijoa/hotel.php',
        services: ['펫호텔', '수영장', '놀이공원', '카페'],
        color: '#a9583e',
        lat: 37.4138, // 경기도 광주시
        lng: 127.2558
    },

    // 🛁 애견미용 & 그루밍
    {
        id: 'grooming-character',
        name: '캐릭터그루밍',
        category: 'grooming',
        emoji: '🛁',
        address: '서울 강남구 도곡동 467-24 타워팰리스3차 앞',
        phone: '02-3463-7975',
        hours: '10:00 - 19:00',
        description: '타워팰리스 앞 프리미엄 애견미용. 예약제 운영',
        website: 'https://charactergrooming.imweb.me/',
        services: ['전견종 미용', '목욕', '스파', '네일케어'],
        color: '#0d9488',
        lat: 37.4920, // 강남구 도곡동
        lng: 127.0541
    },
    {
        id: 'grooming-banjjak',
        name: '반짝 펫 미용샵',
        category: 'grooming',
        emoji: '✂️',
        address: '서울 전역 (플랫폼)',
        phone: '앱 예약',
        hours: '지역별 상이',
        description: '지역별 검증된 미용샵 플랫폼. 간편 앱 예약',
        website: 'https://banjjakpet.com/',
        services: ['미용샵 검색', '온라인 예약', '리뷰 확인'],
        color: '#0d9488',
        lat: 37.5665, // 서울 중심
        lng: 126.9780
    },
    {
        id: 'grooming-petvip',
        name: '펫VIP 출장미용',
        category: 'grooming',
        emoji: '🚐',
        address: '서울/경기 출장 서비스',
        phone: '1644-8091',
        hours: '예약제',
        description: '집으로 찾아가는 반려동물 출장미용 서비스',
        website: 'https://www.petvip.co.kr/',
        services: ['출장미용', '방문목욕', '부분미용', '방문훈련'],
        color: '#14b8a6',
        lat: 37.5400, // 서울 중남부
        lng: 127.0000
    },

    // 🍽️ 애견동반 카페 & 레스토랑
    {
        id: 'cafe-kongti',
        name: '꽁티 드 툴레아',
        category: 'cafe',
        emoji: '☕',
        address: '서울 강남구 도산공원 인근',
        phone: '예약 권장',
        hours: '10:00 - 22:00',
        description: '도산공원 인근 프리미엄 브런치 카페. 반려견 동반 가능',
        website: '#',
        services: ['브런치', '커피', '디저트', '반려견 동반'],
        color: '#f59e0b',
        lat: 37.5220, // 도산공원
        lng: 127.0409
    },
    {
        id: 'cafe-slowforest',
        name: '슬로우포레스트',
        category: 'cafe',
        emoji: '🌳',
        address: '서울 종로구 삼청동',
        phone: '02-722-7063',
        hours: '11:00 - 21:00',
        description: '삼청동 2층 카페. 루프탑에서 반려견과 함께',
        website: '#',
        services: ['루프탑', '반려견 동반', '커피', '디저트'],
        color: '#f59e0b',
        lat: 37.5858, // 종로구 삼청동
        lng: 126.9831
    },
    {
        id: 'cafe-bottlefactory',
        name: '보틀팩토리',
        category: 'cafe',
        emoji: '♻️',
        address: '서울 서대문구 연희동',
        phone: '02-6339-0803',
        hours: '11:00 - 22:00',
        description: '친환경 카페. 모든 견종 실내동반 가능 (펫티켓 필수)',
        website: '#',
        services: ['친환경', '실내동반', '커피', '브런치'],
        color: '#10b981',
        lat: 37.5683, // 서대문구 연희동
        lng: 126.9283
    },

    // 🎓 애견훈련소 & 교육
    {
        id: 'training-esac',
        name: '이삭애견훈련소',
        category: 'training',
        emoji: '🎓',
        address: '경기도 (30년 경력)',
        phone: '031-XXX-XXXX',
        hours: '예약제',
        description: '30년 경력의 전문 애견훈련소. 문제행동 교정 전문',
        website: 'https://esac2000.co.kr/',
        services: ['기본훈련', '문제행동교정', '전문가과정', '1:1 맞춤'],
        color: '#cc785c',
        lat: 37.4500, // 경기도 성남
        lng: 127.1500
    },
    {
        id: 'training-dogmaru',
        name: '도그마루 홈스쿨',
        category: 'training',
        emoji: '🏫',
        address: '서울 전역 방문 훈련',
        phone: '방문예약',
        hours: '예약제',
        description: '집으로 찾아가는 강아지 홈스쿨. 배변/입질/분리불안 전문',
        website: 'https://dmhomeschool.co.kr/',
        services: ['배변훈련', '입질교정', '분리불안', '짖음훈련'],
        color: '#cc785c',
        lat: 37.5500, // 서울 중부
        lng: 127.0500
    },

    // 🛒 펫샵 & 용품
    {
        id: 'shop-minipet',
        name: '미니펫 강남직영점',
        category: 'shop',
        emoji: '🛒',
        address: '서울 강남구',
        phone: '02-XXX-XXXX',
        hours: '365일 연중무휴',
        description: '강아지/고양이 분양 및 펫용품 전문점',
        website: 'https://minipetmall.co.kr/',
        services: ['분양', '펫용품', '사료', '간식'],
        color: '#d97706',
        lat: 37.5100,
        lng: 127.0600
    },
    {
        id: 'shop-thedogs',
        name: '더독스',
        category: 'shop',
        emoji: '🛍️',
        address: '서울 금천구 두산로 70',
        phone: '010-9221-9253',
        hours: '평일 10:00 - 19:00',
        description: '반려동물 용품 전문 쇼핑몰',
        website: 'http://www.thedogs.co.kr/',
        services: ['사료', '간식', '장난감', '용품'],
        color: '#d97706',
        lat: 37.4790,
        lng: 126.8959
    },
    {
        id: 'shop-seoulreptile',
        name: '서울렙타일',
        category: 'shop',
        emoji: '🦎',
        address: '서울 중구 충정로역 인근',
        phone: '문의필요',
        hours: '평일 11:00 - 20:00',
        description: '양서·파충류 분양 및 용품 전문점',
        website: 'https://www.seoulreptile.co.kr/',
        services: ['파충류분양', '용품', '사료', '도소매'],
        color: '#84cc16',
        lat: 37.5600,
        lng: 126.9634
    },

    // 🏥 추가 동물병원
    {
        id: 'hospital-kangnam',
        name: '강남동물병원',
        category: 'hospital',
        emoji: '🏥',
        address: '서울 강남구 봉은사로 205',
        phone: '02-514-7582',
        hours: '평일 09:00 - 21:00',
        description: '차병원사거리 근처, 언주역 4번 출구 인근 동물병원',
        website: '#',
        services: ['일반진료', '예방접종', '건강검진', '수술'],
        tags: ['예약가능', '평균진료비 보통'],
        color: '#dc2626',
        lat: 37.5145,
        lng: 127.0470
    },
    {
        id: 'hospital-namc',
        name: '우리곁N 동물의료센터',
        category: 'hospital',
        emoji: '🏥',
        address: '서울시 (위치 문의)',
        phone: '문의필요',
        hours: '24시간 운영',
        description: '24시간 응급 진료 동물병원',
        website: 'https://www.namc.co.kr/',
        services: ['24시 응급', '외과', '내과', '영상진단'],
        tags: ['야간진료', '예약가능', '평균진료비 높음'],
        color: '#dc2626',
        lat: 37.5500,
        lng: 126.9900
    },

    // 🎓 추가 훈련소
    {
        id: 'training-starmong',
        name: '스타몽 강아지유치원',
        category: 'training',
        emoji: '🎓',
        address: '서울 (지점별 상이)',
        phone: '문의필요',
        hours: '평일 08:00 - 19:00',
        description: '강아지유치원, 반려견훈련, 견주교육 전문',
        website: 'https://starmong.co.kr/',
        services: ['유치원', '훈련', '호텔', '미용', '견주교육'],
        color: '#cc785c',
        lat: 37.5300,
        lng: 127.0200
    },

    // ☕ 추가 애견카페
    {
        id: 'cafe-nolo',
        name: '놀로스퀘어',
        category: 'cafe',
        emoji: '☕',
        address: '서울 강남구 청담동 46',
        phone: '문의필요',
        hours: '11:00 - 22:00',
        description: '청담동 반려견 동반 카페',
        website: '#',
        services: ['커피', '브런치', '반려견 동반', '놀이공간'],
        color: '#f59e0b',
        lat: 37.5240,
        lng: 127.0470
    },
    {
        id: 'cafe-twojentle',
        name: '두젠틀 강남점',
        category: 'cafe',
        emoji: '🐕',
        address: '서울 강남구 역삼동',
        phone: '문의필요',
        hours: '10:00 - 22:00',
        description: '역삼역 근처 넓은 공간의 애견카페',
        website: '#',
        services: ['커피', '음료', '반려견 놀이공간'],
        color: '#f59e0b',
        lat: 37.5000,
        lng: 127.0360
    },
    {
        id: 'cafe-hwamokto',
        name: '카페 화목토',
        category: 'cafe',
        emoji: '🌼',
        address: '서울 양천구 신정동',
        phone: '문의필요',
        hours: '11:00 - 21:00',
        description: '플라워샵과 도예공방이 함께하는 테마카페',
        website: '#',
        services: ['커피', '플라워샵', '도예체험', '반려견 동반'],
        color: '#f59e0b',
        lat: 37.5175,
        lng: 126.8560
    },

    // 🛁 추가 미용샵
    {
        id: 'grooming-mimi',
        name: '미미살롱펫',
        category: 'grooming',
        emoji: '✨',
        address: '서울 전역 방문미용',
        phone: '문의필요',
        hours: '예약제',
        description: '프리미엄 방문 반려동물 미용 서비스',
        website: 'http://mimisalon.pet/',
        services: ['방문미용', '프리미엄케어', '목욕', '스파'],
        color: '#14b8a6',
        lat: 37.5600,
        lng: 127.0100
    }
];

// 카테고리별 색상 맵
const CATEGORY_COLORS = {
    hospital: '#dc2626',
    hotel: '#a9583e',
    grooming: '#0d9488',
    cafe: '#f59e0b',
    training: '#cc785c',
    shop: '#d97706'
};

// 카테고리별 한글명
const CATEGORY_NAMES = {
    hospital: '동물병원',
    hotel: '펫호텔',
    grooming: '미용&그루밍',
    cafe: '카페&레스토랑',
    training: '훈련&교육',
    shop: '펫샵&용품'
};
