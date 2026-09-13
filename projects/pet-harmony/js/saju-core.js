// 펫과나(saju.js startSajuAnalysis)에서 추출한 사주 해석 코어 — DOM 없음.
(function (root) {
  function hashString(str) {
    let hash = 0;
    for (let i = 0; i < str.length; i++) {
      hash = str.charCodeAt(i) + ((hash << 5) - hash);
    }
    return Math.abs(hash);
  }

  const ELEMENTS = [
    { el: '木 (나무)', key: '목', desc: '성장과 활력이 넘치는 따뜻한 봄의 기운' },
    { el: '火 (불)', key: '화', desc: '열정과 밝은 에너지가 솟구치는 여름의 기운' },
    { el: '土 (흙)', key: '토', desc: '모든 것을 포용하고 안정적인 중재자의 기운' },
    { el: '金 (쇠)', key: '금', desc: '단단하고 결단력 있는 가을의 기운' },
    { el: '水 (물)', key: '수', desc: '지혜롭고 유연하게 흘러가는 겨울의 기운' },
  ];

  const PET_READINGS = [
    '🐶 [식신생재격] 타고난 식복과 애교로 어디서든 굶지 않고 사랑받을 팔자입니다. 보호자의 마음을 녹이는 치명적인 매력을 가졌으며, 먹을 것을 보면 고도의 집중력을 발휘합니다. 가끔 고집을 부릴 땐 백 마디 말보다 간식 하나로 회유하는 것이 직빵입니다. 건강하고 무탈하게 장수할 좋은 기운을 가졌네요.',
    '🐱 [역마살/호기심] 두뇌 회전이 매우 빠르고 호기심이 왕성합니다. 가만히 있는 것보다 집안 구석구석 새로운 냄새를 맡고 돌아다녀야 직성이 풀리는 약간의 역마살이 있습니다. 낯선 물건이 오면 반드시 먼저 검사를 거쳐야 하며, 똑똑한 만큼 보호자의 약점을 잘 알고 교묘하게 이용할 줄 아는 영특한 아이입니다.',
    '🐾 [관인상생격] 보호자에 대한 충성심과 애착이 남다릅니다. 주인이 세상의 전부인 것처럼 행동하며, 주위 사람들에게도 다정다감하고 젠틀한 성격을 가졌습니다. 가족이 우울해하면 가장 먼저 다가와 위로해주는 속 깊은 천사입니다. 칭찬을 받을수록 능력이 배가 되니 무한한 칭찬이 필요합니다.',
    '🐰 [예민/섬세] 매우 예민하고 섬세한 영혼의 소유자입니다. 큰 소리나 급격한 환경 변화를 싫어하며, 안정적이고 포근한 자기만의 보금자리를 가장 좋아합니다. 낯선 사람에게는 곁을 잘 내어주지 않지만, 한 번 마음을 연 가족에게는 무한한 애정을 쏟습니다. 조용하고 평화로운 환경에서 가장 행복해합니다.',
    '🦁 [비견겁재/독립] 독립심이 강하고 자기주장이 아주 확실한 장군감입니다. 자기가 원할 때만 애교를 부리며, 귀찮게 구는 것을 딱 질색하는 밀당의 고수라 할 수 있습니다. 억지로 시키는 것을 싫어하지만 납득하면 누구보다 잘 따릅니다. 프라이드를 존중해주고 대등한 파트너로서 대해주면 최고의 관계가 됩니다.',
  ];

  const OWNER_READINGS = [
    '🧔 [자애/희생] 동물을 사랑하는 마음이 태평양처럼 넓고 깊어 펫에게 조건 없이 헌신하는 따뜻한 부모 사주입니다. 내 입에 들어가는 것보다 펫 입에 들어가는 간식이 더 기쁘며, 펫의 행복이 곧 나의 행복입니다. 펫의 럭셔리한 삶을 위해 열심히 돈을 벌게 될 팔자이니 체력 관리에 유의하세요.',
    '🏃 [활동/친구] 펫을 일방적으로 돌보기보다는 친구처럼 티격태격하며 지내는 평등하고 수평적인 관계를 추구합니다. 활동적인 에너지가 넘쳐서 펫과 함께 산책, 캠핑, 나들이를 자주 나가면 본인의 운까지 덩달아 크게 트이는 일석이조의 사주입니다. 펫과 찰떡 호흡을 자랑합니다.',
    '🧐 [세심/관리] 꼼꼼하고 세심한 성격으로 펫의 아주 작은 변화나 컨디션 저하도 금방 알아채는 훌륭한 관찰력을 가졌습니다. 식단, 배변, 영양제 등을 철저하게 기록하고 관리하는 데 탁월한 재능이 있습니다. 펫에게는 그야말로 완벽에 가까운 주치의이자 매니저 같은 든든한 존재입니다.',
    '✨ [교감/감수성] 감수성이 매우 풍부하여 펫과 깊은 영혼의 교감을 나눌 수 있는 사주입니다. 굳이 소리 내어 말하지 않아도 펫의 눈빛과 작은 몸짓만으로 무엇을 원하는지 직감적으로 알아챕니다. 펫 역시 당신의 기분을 귀신같이 알아채어 서로 뗄 수 없는 끈끈한 유대감을 형성하게 됩니다.',
    '🎉 [낙천/긍정] 다소 덤벙거리고 계획성이 부족할 순 있지만, 특유의 밝고 초긍정적인 에너지로 펫에게 끊임없는 즐거움을 선사합니다. 가끔 밥 시간이나 산책 시간을 깜빡 잊어도, 당신의 해맑은 미소와 진심 어린 사과(?)를 보며 펫은 모든 것을 용서할 것입니다. 집안에 웃음이 끊이지 않겠네요.',
  ];

  function compatTitle(score) {
    if (score >= 90) return '수어지교(水魚之交)';
    if (score >= 80) return '금상첨화(錦上添花)';
    if (score >= 70) return '유유상종(類類相從)';
    return '동상이몽(同床異夢)';
  }

  function pastDesc(score) {
    return score >= 90
      ? '전생에 함께 강을 건넜던 깊은 동반자적 인연입니다.'
      : '전생에 서로 소중히 여겼던 이웃이었습니다.';
  }

  function synergyDesc(score) {
    return score >= 90
      ? '서로의 기운을 보완하며 함께 있을 때 큰 행운이 따릅니다.'
      : '서로를 이해하고 천천히 맞춰가면 아주 좋은 흐름이 생깁니다.';
  }

  /** petBirth/ownerBirth ISO date; optional time HH:MM */
  function analyzePair(petBirth, ownerBirth, petTime, ownerTime) {
    const petHash = hashString(String(petBirth) + String(petTime || ''));
    const ownerHash = hashString(String(ownerBirth) + String(ownerTime || ''));
    const petElement = ELEMENTS[petHash % 5];
    const ownerElement = ELEMENTS[ownerHash % 5];
    const petReading = PET_READINGS[petHash % 5];
    const ownerReading = OWNER_READINGS[ownerHash % 5];
    const compatScore = 50 + ((petHash + ownerHash) % 51);

    return {
      petBirth,
      ownerBirth,
      petSummary: `${petElement.el}의 기운을 가진 펫`,
      petDesc: `[${petElement.desc}]\n\n${petReading}`,
      ownerSummary: `${ownerElement.el}의 기운을 가진 집사`,
      ownerDesc: `[${ownerElement.desc}]\n\n${ownerReading}`,
      petElement,
      ownerElement,
      compatScore,
      compatTitle: compatTitle(compatScore),
      pastDesc: pastDesc(compatScore),
      synergyDesc: synergyDesc(compatScore),
    };
  }

  const api = { analyzePair, ELEMENTS, compatTitle };
  root.PetSaju = api;
  if (typeof module !== 'undefined') module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
