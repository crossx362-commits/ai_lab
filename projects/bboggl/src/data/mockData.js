// 상품/가격 데이터는 하드코딩하지 않는다. 검색은 각 쇼핑몰의 실제 결과로 연결한다(App.jsx MALLS).

export const posts = [
  { id: 101, title: '태블릿 30만원 이하 모델 비교 후기', author: 'minji', comments: 12, likes: 42, status: '공개' },
  { id: 102, title: '에어프라이어 스팀 기능 실제로 필요한가요?', author: 'cooknori', comments: 8, likes: 31, status: '공개' },
  { id: 103, title: '로봇청소기 흡입력보다 중요한 기준', author: 'reviewer', comments: 18, likes: 63, status: '검토' },
];

export const activity = [
  'FocusPad Mini Tablet 가격 알림 추가',
  'SteamNest Air Fryer 비교표 저장',
  '게시글에 좋아요를 눌렀습니다',
];

export const chartData = [
  { name: '월', searches: 1200, posts: 24 },
  { name: '화', searches: 1850, posts: 31 },
  { name: '수', searches: 1600, posts: 28 },
  { name: '목', searches: 2100, posts: 42 },
  { name: '금', searches: 2380, posts: 47 },
  { name: '토', searches: 2700, posts: 36 },
];
