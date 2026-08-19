/* ═══════════════════════════════════════════════════════════
   작업물 데이터 — 여기에 객체 하나 추가하면 works.html에 카드가 생긴다.

   필드 설명:
   cat   : 'reel'(애니메이션 릴) | 'ai'(AI 활용) | 'music'(음악) | 'plan'(기획·컨셉)
   yt    : 유튜브 영상 ID (예: https://youtu.be/Zffl7MgPnJU → 'Zffl7MgPnJU')
   link  : 외부 링크 (yt 없이 링크만 있는 카드 — 채널·인스타 등)
   icon  : yt/link 둘 다 없을 때 카드 상단에 표시할 이모지
   title : 카드 제목
   desc  : 한 줄 설명
   year  : 연도 표기 (선택)
   featured : true면 맨 앞에 정렬 (선택)

   ※ 최신 작업을 위에 추가하면 그 순서대로 표시된다.
   ═══════════════════════════════════════════════════════════ */
const WORKS = [
  {
    cat: 'reel', yt: 'Zffl7MgPnJU', featured: true,
    title: '2024~2026 Animation Reel',
    desc: '최근 작업 모음 — 인게임 애니메이션·시네마틱 컷씬',
    year: '2026',
  },
  {
    cat: 'ai', yt: 'XjDLAVSJy94', featured: true,
    title: 'AI 활용한 여러 가지 구현',
    desc: '환경 구현(쉐이더·라이트 세팅)부터 총격 광원·충격파 이펙트까지, 엔진에서 씬을 완성하는 과정',
    year: '2026',
  },
  {
    cat: 'plan', icon: '🎯',
    title: '「아시라 ASHIRA」 TPS 캐릭터 모션 시트 & 제작 기획서',
    desc: '생성형 AI로 캐릭터 디자인·턴어라운드·모션 시트(로코모션/컴뱃/무기 핸들링) 제작 · A-Pose 리깅 기준·본 체인·IK 가이드까지 단독 기획',
    year: '2026',
  },
  {
    cat: 'plan', icon: '🏙️',
    title: '환경 컨셉 — Factory District_07 & Sunbloom Village',
    desc: '인더스트리얼·판타지 두 톤의 레벨 디자인·머티리얼 시트를 생성형 AI로 제작',
    year: '2026',
  },
  {
    cat: 'music', link: 'https://www.youtube.com/@%EB%A5%98%EB%82%98-l7h', icon: '🎵',
    title: 'AI 음악 자동 생성 실험 — 채널 「류나」',
    desc: 'AI로 시티팝 BGM 트랙을 자동 생성·발행해 본 실험 채널 (CPI, GOLDEN MEMORIES, NIGHT CITYSCAPE 등)',
    year: '2026',
  },
  {
    cat: 'ai', link: 'https://www.instagram.com/crossx36/', icon: '📸',
    title: 'AI 자동 포스팅 연구 — 인스타그램',
    desc: 'AI가 콘텐츠 생성부터 게시까지 자동으로 처리하는 포스팅 파이프라인 실험',
    year: '2026',
  },
  { cat: 'reel', yt: 'cPBzkudoHLg', title: 'Run / Walk', desc: '로코모션 기본기 — 런·워크 사이클' },
  { cat: 'reel', yt: 'l8WhGaNYddc', title: 'Animation Reel', desc: '캐릭터 애니메이션 릴' },
  { cat: 'reel', yt: '6SmAAcfe_40', title: 'SD Character Animation 2021~2022', desc: '캐주얼 게임 SD 캐릭터 모션 모음', year: '2022' },
  { cat: 'reel', yt: 'ayzgLWvkG4A', title: 'Animation Reel', desc: '캐릭터 애니메이션 릴' },
  { cat: 'reel', yt: 'UG5UcBLrskU', title: 'Ani Reel 2020 vol.1', desc: '2020 애니메이션 릴 1부', year: '2020' },
  { cat: 'reel', yt: 'SHp0RMZ8qnc', title: 'Ani Reel 2020 vol.2', desc: '2020 애니메이션 릴 2부', year: '2020' },
  { cat: 'reel', yt: 'rHC5Gtyd4YU', title: 'Character Attack Animation', desc: '타격감을 살린 공격 모션' },
  { cat: 'reel', yt: 'I4-AMz5tZTM', title: 'Attack Clip', desc: '액션 공격 클립' },
  { cat: 'reel', yt: 'F2DDYvW9MoY', title: 'Animation Clip', desc: '애니메이션 클립' },
  { cat: 'reel', yt: '8O10hnlPp6A', title: 'Game Animation Reel 2017', desc: '2017 게임 애니메이션 릴', year: '2017' },
];
