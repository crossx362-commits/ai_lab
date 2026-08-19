/* ═══════════════════════════════════════════════════════════
   작업물 데이터 — 여기에 객체 하나 추가하면 works.html에 카드가 생긴다.

   필드 설명:
   group : 'company'(회사 프로젝트 — 재직하며 만든 것) | 'personal'(개인 프로젝트 — 개인 작업물)
   cat   : 'reel'(애니메이션 릴) | 'ai'(AI 활용) | 'plan'(기획·컨셉)
   yt    : 유튜브 영상 ID (예: https://youtu.be/Zffl7MgPnJU → 'Zffl7MgPnJU')
   link  : 외부 링크 (yt 없이 링크만 있는 카드)
   icon  : yt/link 둘 다 없을 때 카드 상단에 표시할 이모지
   title : 카드 제목
   desc  : 한 줄 설명
   year  : 연도 표기 (선택)
   featured : true면 맨 앞에 정렬 (선택)

   ※ 최신 작업을 위에 추가하면 그 순서대로 표시된다.
   ═══════════════════════════════════════════════════════════ */
const WORKS = [
  {
    group: 'company', cat: 'reel', yt: 'Zffl7MgPnJU', featured: true,
    title: '2024~2026 Animation Reel',
    desc: '최근 작업 모음 — 인게임 애니메이션·시네마틱 컷씬',
    year: '2026',
  },
  {
    group: 'company', cat: 'ai', yt: 'XjDLAVSJy94', featured: true,
    title: 'AI 활용한 여러 가지 구현',
    desc: '환경 구현(쉐이더·라이트 세팅)부터 총격 광원·충격파 이펙트까지, 엔진에서 씬을 완성하는 과정',
    year: '2026',
  },
  {
    group: 'personal', cat: 'plan', icon: '🎯',
    title: '「아시라 ASHIRA」 TPS 캐릭터 모션 시트 & 제작 기획서',
    desc: '생성형 AI로 캐릭터 디자인·턴어라운드·모션 시트(로코모션/컴뱃/무기 핸들링) 제작 · A-Pose 리깅 기준·본 체인·IK 가이드까지 단독 기획',
    year: '2026',
  },
  {
    group: 'personal', cat: 'plan', icon: '🏙️',
    title: '환경 컨셉 — Factory District_07 & Sunbloom Village',
    desc: '인더스트리얼·판타지 두 톤의 레벨 디자인·머티리얼 시트를 생성형 AI로 제작',
    year: '2026',
  },
  { group: 'company', cat: 'reel', yt: 'cPBzkudoHLg', title: 'Run / Walk', desc: '로코모션 기본기 — 런·워크 사이클' },
  { group: 'company', cat: 'reel', yt: 'l8WhGaNYddc', title: 'Animation Reel', desc: '캐릭터 애니메이션 릴' },
  { group: 'company', cat: 'reel', yt: '6SmAAcfe_40', title: 'SD Character Animation 2021~2022', desc: '캐주얼 게임 SD 캐릭터 모션 모음', year: '2022' },
  { group: 'company', cat: 'reel', yt: 'ayzgLWvkG4A', title: 'Animation Reel', desc: '캐릭터 애니메이션 릴' },
  { group: 'company', cat: 'reel', yt: 'UG5UcBLrskU', title: 'Ani Reel 2020 vol.1', desc: '2020 애니메이션 릴 1부', year: '2020' },
  { group: 'company', cat: 'reel', yt: 'SHp0RMZ8qnc', title: 'Ani Reel 2020 vol.2', desc: '2020 애니메이션 릴 2부', year: '2020' },
  { group: 'company', cat: 'reel', yt: 'rHC5Gtyd4YU', title: 'Character Attack Animation', desc: '타격감을 살린 공격 모션' },
  { group: 'company', cat: 'reel', yt: 'I4-AMz5tZTM', title: 'Attack Clip', desc: '액션 공격 클립' },
  { group: 'company', cat: 'reel', yt: 'F2DDYvW9MoY', title: 'Animation Clip', desc: '애니메이션 클립' },
  { group: 'company', cat: 'reel', yt: '8O10hnlPp6A', title: 'Game Animation Reel 2017', desc: '2017 게임 애니메이션 릴', year: '2017' },
];
