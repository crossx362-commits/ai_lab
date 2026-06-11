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
        color: '#dc2626',
        position: { left: '85%', top: '45%' } // 동쪽 라우 제도
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
        color: '#dc2626',
        position: { left: '78%', top: '52%' }
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
        color: '#e11d48',
        position: { left: '72%', top: '38%' }
    },

    // 🏨 펫호텔 & 위탁
    {
        id: 'hotel-dogs',
        name: '호텔 독스',
        category: 'hotel',
        emoji: '🏨',
        address: '서울 전역 (스마트 호텔)',
        phone: '1661-9974',
        hours: '24시간 입/퇴실 가능',
        description: '24시간 CCTV, 비밀번호 견사, 스마트 자동화 펫호텔',
        website: 'https://hoteldogs.co.kr/hoteling',
        services: ['24시간 CCTV', '비밀번호 견사', '자동 급식', '24시간 입퇴실'],
        color: '#4f46e5',
        position: { left: '18%', top: '28%' } // 서쪽 야사와 제도
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
        color: '#4f46e5',
        position: { left: '25%', top: '35%' }
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
        position: { left: '38%', top: '55%' } // 중앙 비티레부 서부
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
        position: { left: '32%', top: '48%' }
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
        position: { left: '42%', top: '62%' }
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
        position: { left: '55%', top: '25%' } // 북동 바누아레부
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
        position: { left: '48%', top: '18%' }
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
        position: { left: '28%', top: '42%' }
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
        color: '#8b5cf6',
        position: { left: '62%', top: '32%' }
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
        color: '#8b5cf6',
        position: { left: '68%', top: '28%' }
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
        position: { left: '52%', top: '58%' } // 중앙 비티레부 동부
    }
];

// 카테고리별 색상 맵
const CATEGORY_COLORS = {
    hospital: '#dc2626',
    hotel: '#4f46e5',
    grooming: '#0d9488',
    cafe: '#f59e0b',
    training: '#8b5cf6',
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
