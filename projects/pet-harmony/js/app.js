(function () {
  const $ = (sel, el = document) => el.querySelector(sel);
  const views = {
    home: $('#view-home'),
    form: $('#view-form'),
    result: $('#view-result'),
  };

  const ELS = ['목', '화', '토', '금', '수'];
  const EL_META = {
    목: { emoji: '🌳', color: '#16a34a' },
    화: { emoji: '🔥', color: '#ea580c' },
    토: { emoji: '⛰️', color: '#a16207' },
    금: { emoji: '🪙', color: '#64748b' },
    수: { emoji: '💧', color: '#0284c7' },
  };
  const MBTI_QUESTIONS = [
    { key: 'mbtiEnergy', label: '펫과 쉬는 날에는?', options: ['E|같이 밖에 나가고 싶어요', 'I|집에서 조용히 쉬고 싶어요'] },
    { key: 'mbtiStyle', label: '펫의 문제가 생기면?', options: ['N|여러 방법을 떠올려 시도해요', 'S|확실한 원인부터 차근차근 봐요'] },
    { key: 'mbtiRoutine', label: '펫과 루틴을 정할 때는?', options: ['F|서로 기분과 마음을 먼저 봐요', 'T|기준을 세우고 효율적으로 정해요'] },
  ];
  const LIFESTYLE_QUESTIONS = [
    { key: 'lifeTime', label: '평일에 펫과 보낼 수 있는 시간은?', options: ['high|하루에 꽤 오래 함께할 수 있어요', 'mid|아침·저녁에 꾸준히 챙길 수 있어요', 'low|짧아도 매일 챙기려고 해요'] },
    { key: 'lifeActivity', label: '같이 하고 싶은 활동은?', options: ['out|산책·놀이처럼 몸을 움직이는 것', 'home|집에서 쉬고 교감하는 것', 'mix|그날그날 다르게 하고 싶어요'] },
    { key: 'lifeRoutine', label: '돌봄 루틴은 어떤 편인가요?', options: ['steady|정해둔 시간에 꾸준히 하는 편', 'flex|상황에 맞춰 유연하게 하는 편', 'learn|배우면서 하나씩 만들어갈래요'] },
  ];

  const MODE_META = {
    overall: {
      title: '우리의 새 가족 종합 결과',
      blurb: '생일·보호자 스타일·생활 습관을 모두 살펴보고 한 번에 정리해드려요.',
      fields: [
        { key: 'owner', label: '내 생일', type: 'date', required: true },
        { key: 'pet', label: '펫 생일 (몰라도 괜찮아요)', type: 'date' },
        { key: 'petName', label: '펫 이름 (선택)', type: 'text' },
      ],
    },
    mbti: {
      title: '나는 어떤 보호자일까?',
      blurb: '펫과 함께 살 때 드러나는 내 보호자 스타일을 3문항으로 가볍게 알아봐요.',
      fields: [],
    },
    adopt: {
      title: '우리 집에 와도 괜찮을까?',
      blurb: '데려오기 전, 나와 이 친구가 얼마나 잘 맞는지 가볍게 봐요.',
      fields: [
        { key: 'owner', label: '내 생일', type: 'date', required: true },
        { key: 'ownerTime', label: '내 태어난 시간 (선택)', type: 'time' },
        { key: 'species', label: '어떤 친구를 만나고 있나요?', type: 'select', options: ['모름', '강아지', '고양이'] },
        { key: 'pet', label: '펫 생일 (몰라도 괜찮아요)', type: 'date' },
        { key: 'petTime', label: '펫 태어난 시간 (선택)', type: 'time' },
        { key: 'petName', label: '펫 이름 (선택)', type: 'text' },
      ],
    },
    dogcat: {
      title: '강아지랑 고양이, 누가 더 찰떡?',
      blurb: '둘 중 나와 더 맞는 쪽을 점수로 비교.',
      fields: [
        { key: 'owner', label: '내 생일', type: 'date', required: true },
        { key: 'ownerTime', label: '내 태어난 시간 (선택)', type: 'time' },
        { key: 'dog', label: '강아지 생일 (없으면 그냥 패스)', type: 'date' },
        { key: 'cat', label: '고양이 생일 (없으면 그냥 패스)', type: 'date' },
      ],
    },
    triangle: {
      title: '둘 사이에 펫까지 오면?',
      blurb: 'A ↔ B ↔ 펫, 약한 변을 한눈에.',
      fields: [
        { key: 'a', label: '첫 번째 사람 생일', type: 'date', required: true },
        { key: 'aTime', label: 'A 태어난 시간 (선택)', type: 'time' },
        { key: 'b', label: '두 번째 사람 생일', type: 'date', required: true },
        { key: 'bTime', label: 'B 태어난 시간 (선택)', type: 'time' },
        { key: 'pet', label: '펫 생일 (모르면 비워도 괜찮아요)', type: 'date' },
        { key: 'petTime', label: '펫 태어난 시간 (선택)', type: 'time' },
        { key: 'petName', label: '펫 이름 (선택)', type: 'text' },
      ],
    },
  };

  let state = {
    mode: null,
    paid: false,
    payload: null,
    photoData: '',
  };

  function show(name) {
    Object.values(views).forEach((v) => v.classList.add('hidden'));
    views[name].classList.remove('hidden');
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  function estimateBirth(kind) {
    const map = { dog: '2020-05-01', cat: '2021-09-15' };
    return map[kind];
  }

  async function loadPaypalSdk() {
    if (window.paypal) return true;
    if (!/^https?:$/.test(window.location.protocol)) return false;
    try {
      const config = await fetch('/api/paypal-config').then((response) => response.json());
      if (!config.clientId) return false;
      await new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = `https://www.paypal.com/sdk/js?client-id=${encodeURIComponent(config.clientId)}&currency=USD&intent=capture`;
        script.onload = resolve;
        script.onerror = reject;
        document.head.appendChild(script);
      });
      return !!window.paypal;
    } catch {
      return false;
    }
  }

  function saveResult(id, data) {
    localStorage.setItem('petHarmony:' + id, JSON.stringify(data));
    if (window.location.protocol === 'http:' || window.location.protocol === 'https:') {
      fetch('/api/results', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ resultId: id, payload: data }) }).catch(() => {});
    }
  }

  function loadResult(id) {
    try { return JSON.parse(localStorage.getItem('petHarmony:' + id) || 'null'); } catch { return null; }
  }

  function uid() {
    return Math.random().toString(36).slice(2, 10);
  }

  function openForm(mode) {
    state.mode = mode;
    state.paid = false;
    state.photoData = '';
    const meta = MODE_META[mode];
    $('#form-title').textContent = meta.title;
    $('#form-blurb').textContent = meta.blurb;
    $('#form-submit').textContent = mode === 'mbti' ? '내 보호자 스타일 보기' : mode === 'overall' ? '모든 검사 끝내고 최종 오디션 점수 보기' : '오디션 검사 결과 보기';
    const fields = $('#form-fields');
    fields.innerHTML = '';
    if (mode === 'overall') {
      const intro = document.createElement('p');
      intro.className = 'form-tip';
      intro.textContent = '한 화면에서 천천히 답해 주세요. 모든 답변을 마치면 마지막에 어울림 점수를 보여드려요.';
      fields.appendChild(intro);
    }
    meta.fields.forEach((f) => {
      const wrap = document.createElement('div');
      wrap.className = `field-card field-${f.type}`;
      const icon = f.type === 'date' ? '🎂' : f.type === 'time' ? '🕒' : f.key.includes('Name') ? '🐾' : '✍️';
      const control = f.type === 'select'
        ? `<select id="${f.key}" name="${f.key}">${f.options.map((o) => `<option value="${o === '모름' ? '' : o}">${o}</option>`).join('')}</select>`
        : `<input id="${f.key}" name="${f.key}" type="${f.type}" autocomplete="${f.type === 'date' ? 'bday' : 'off'}" ${f.required ? 'required' : ''} />`;
      const skip = f.type === 'date' && !f.required ? `<button type="button" class="skip-field" data-target="${f.key}">모름</button>` : '';
      wrap.innerHTML = `<span class="field-icon" aria-hidden="true">${icon}</span><div class="field-body"><label for="${f.key}">${f.label}</label>${control}${skip}</div>`;
      fields.appendChild(wrap);
    });
    fields.querySelectorAll('.skip-field').forEach((button) => {
      button.addEventListener('click', () => {
        const input = document.getElementById(button.dataset.target);
        if (input) { input.value = ''; button.textContent = '건너뜀'; button.classList.add('selected'); }
      });
    });
    if (mode === 'dogcat') {
      const tip = document.createElement('p');
      tip.className = 'lead';
      tip.style.marginTop = '8px';
      tip.textContent = '생일을 모르면 비워두세요. 기본 성향으로 가볍게 비교해볼게요.';
      fields.appendChild(tip);
    }
    if (mode === 'adopt') {
      const tip = document.createElement('p');
      tip.className = 'form-tip';
      tip.textContent = '내 생일만 있으면 시작할 수 있어요. 펫 생일은 몰라도 괜찮아요.';
      fields.appendChild(tip);
    }
    if (mode === 'mbti') {
      const mbtiTitle = document.createElement('p');
      mbtiTitle.className = 'form-section-title';
      mbtiTitle.textContent = '세 질문의 목적은 하나예요: 펫과 함께 살 때의 내 스타일';
      fields.appendChild(mbtiTitle);
      MBTI_QUESTIONS.forEach((q) => {
        const wrap = document.createElement('div');
        wrap.className = 'field-card mbti-question';
        wrap.innerHTML = `<span class="field-icon" aria-hidden="true">💬</span><div class="field-body"><label for="${q.key}">${q.label}</label><select id="${q.key}" name="${q.key}" required><option value="">골라주세요</option>${q.options.map((o) => { const [value, text] = o.split('|'); return `<option value="${value}">${text}</option>`; }).join('')}</select></div>`;
        fields.appendChild(wrap);
      });
    }
    if (mode === 'overall') {
      const title = document.createElement('p');
      title.className = 'form-section-title';
      title.textContent = '생활 방식도 확인할게요';
      fields.appendChild(title);
      MBTI_QUESTIONS.concat(LIFESTYLE_QUESTIONS).forEach((q) => {
        const wrap = document.createElement('div');
        wrap.className = 'field-card mbti-question';
        wrap.innerHTML = `<span class="field-icon" aria-hidden="true">💡</span><div class="field-body"><label for="${q.key}">${q.label}</label><select id="${q.key}" name="${q.key}" required><option value="">골라주세요</option>${q.options.map((o) => { const [value, text] = o.split('|'); return `<option value="${value}">${text}</option>`; }).join('')}</select></div>`;
        fields.appendChild(wrap);
      });
    }
    show('form');
  }

  function pairLine(h) {
    return h.relation.text;
  }

  function deriveMbti(values) {
    const letters = ['mbtiEnergy', 'mbtiStyle', 'mbtiRoutine'].map((key) => values[key]).filter(Boolean);
    return letters.length ? letters.join(' · ') : '';
  }

  function scoreColor(n) {
    if (n >= 70) return '#059669';
    if (n >= 40) return '#ea580c';
    return '#dc2626';
  }

  function gaugeSVG(score, label, note) {
    const r = 42;
    const c = 2 * Math.PI * r;
    const pct = Math.max(0, Math.min(100, Number(score) || 0));
    const offset = c * (1 - pct / 100);
    const color = scoreColor(pct);
    return `<div class="gauge">
      <svg viewBox="0 0 100 100" aria-label="${label} ${pct}점">
        <circle cx="50" cy="50" r="${r}" fill="none" stroke="#fed7aa" stroke-width="10"/>
        <circle cx="50" cy="50" r="${r}" fill="none" stroke="${color}" stroke-width="10"
          stroke-linecap="round" transform="rotate(-90 50 50)"
          stroke-dasharray="${c.toFixed(2)}" stroke-dashoffset="${offset.toFixed(2)}"
          style="--circ:${c.toFixed(2)};transition:stroke-dashoffset .9s ease"/>
        <text x="50" y="54" text-anchor="middle" class="gauge-score-text">${pct}</text>
      </svg>
      <div class="g-label">${label}</div>
      ${note ? `<div class="g-note">${note}</div>` : ''}
    </div>`;
  }

  function ohaengBars(sideLabel, elObj) {
    if (!elObj || !elObj.vec) return '';
    const max = Math.max(1, ...ELS.map((e) => elObj.vec[e] || 0));
    const rows = ELS.map((e) => {
      const v = elObj.vec[e] || 0;
      const w = Math.round((v / max) * 100);
      const m = EL_META[e];
      const dom = e === elObj.dominant ? ' dom' : '';
      return `<div class="ohaeng-bar-row${dom}"><span>${m.emoji}${e}</span>
        <div class="ohaeng-bar-track"><div class="ohaeng-bar-fill" style="width:${w}%;background:${m.color}"></div></div>
        <span>${v}</span></div>`;
    }).join('');
    return `<div class="ohaeng-side"><h4>${sideLabel} · ${EL_META[elObj.dominant]?.emoji || ''} ${elObj.dominant}</h4>${rows}</div>`;
  }

  function ohaengPanel(elements, relationType) {
    if (!elements || !elements.pet || !elements.owner) return '';
    return `<div class="card ohaeng-card">
      <p class="ohaeng-title">오행 밸런스</p>
      <div class="ohaeng-grid">
        ${ohaengBars('🐾 펫', elements.pet)}
        ${ohaengBars('👤 집사', elements.owner)}
      </div>
      ${relationType ? `<span class="rel-badge">관계: ${relationType}</span>` : ''}
    </div>`;
  }

  function vsDuel(dogScore, catScore, recommend, dogNote, catNote) {
    const d = Math.max(0, Math.min(100, dogScore));
    const c = Math.max(0, Math.min(100, catScore));
    return `<div class="vs-duel">
      <div class="vs-side dog${recommend === '강아지' ? ' win' : ''}">
        <div class="crown">${recommend === '강아지' ? '👑 추천' : ''}</div>
        <div class="emoji">🐶</div>
        <div class="name">강아지${dogNote ? ' · ' + dogNote : ''}</div>
        <div class="vs-bar-wrap"><div class="vs-bar" style="height:${Math.max(12, d * 1.1)}px"></div></div>
        <strong>${d}점</strong>
      </div>
      <div class="vs-mid">VS</div>
      <div class="vs-side cat${recommend === '고양이' ? ' win' : ''}">
        <div class="crown">${recommend === '고양이' ? '👑 추천' : ''}</div>
        <div class="emoji">🐱</div>
        <div class="name">고양이${catNote ? ' · ' + catNote : ''}</div>
        <div class="vs-bar-wrap"><div class="vs-bar" style="height:${Math.max(12, c * 1.1)}px"></div></div>
        <strong>${c}점</strong>
      </div>
    </div>`;
  }

  function triangleSVG(edges, weakest) {
    // nodes: A top-left, B top-right, Pet bottom
    const pts = { A: [60, 40], B: [240, 40], P: [150, 200] };
    const map = {};
    (edges || []).forEach((e) => { map[e.label] = e.score; });
    const ab = map['A ↔ B'] ?? 50;
    const ap = map['A ↔ 펫'] ?? 50;
    const bp = map['B ↔ 펫'] ?? 50;
    const weak = (weakest && weakest.label) || '';
    function edge(x1, y1, x2, y2, score, label) {
      const isWeak = label === weak;
      const w = 2 + (score / 100) * 8;
      const col = isWeak ? '#dc2626' : scoreColor(score);
      const dash = isWeak ? '6 5' : '0';
      const mx = (x1 + x2) / 2;
      const my = (y1 + y2) / 2;
      return `<line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}" stroke="${col}" stroke-width="${w}" stroke-dasharray="${dash}" stroke-linecap="round"/>
        <rect x="${mx - 22}" y="${my - 11}" width="44" height="20" rx="8" fill="#fff" stroke="${col}"/>
        <text x="${mx}" y="${my + 4}" text-anchor="middle" font-size="11" font-weight="700" fill="${col}">${score}</text>`;
    }
    function node(x, y, label, emoji) {
      return `<circle cx="${x}" cy="${y}" r="26" fill="#fff7ed" stroke="#fdba74" stroke-width="2"/>
        <text x="${x}" y="${y - 2}" text-anchor="middle" font-size="16">${emoji}</text>
        <text x="${x}" y="${y + 16}" text-anchor="middle" font-size="11" font-weight="800" fill="#1f2937">${label}</text>`;
    }
    return `<div class="tri-wrap">
      <svg viewBox="0 0 300 240" role="img" aria-label="삼각 조화도">
        ${edge(pts.A[0], pts.A[1], pts.B[0], pts.B[1], ab, 'A ↔ B')}
        ${edge(pts.A[0], pts.A[1], pts.P[0], pts.P[1], ap, 'A ↔ 펫')}
        ${edge(pts.B[0], pts.B[1], pts.P[0], pts.P[1], bp, 'B ↔ 펫')}
        ${node(pts.A[0], pts.A[1], 'A', '🧑')}
        ${node(pts.B[0], pts.B[1], 'B', '🧑')}
        ${node(pts.P[0], pts.P[1], '펫', '🐾')}
      </svg>
      <p class="tri-caption">점선·빨간 변 = 가장 약한 관계 (${weak || '-'})</p>
    </div>`;
  }

  function renderViz(data) {
    const root = $('#result-viz');
    if (!root) return;
    const free = data.free || {};
    const scores = free.scores || [];
    let html = '';

    if (data.mode === 'adopt') {
      html += `<div class="pair-hero">
        <div class="avatar" title="집사">👤</div>
        <div class="link">💕</div>
        <div class="avatar" title="펫">🐾</div>
      </div>`;
      html += `<div class="gauge-row">${scores.map((s) => gaugeSVG(s.score, s.label, s.note)).join('')}</div>`;
      html += ohaengPanel(free.elements, free.relationType);
    } else if (data.mode === 'dogcat') {
      const dog = scores.find((s) => /강아지/.test(s.label)) || scores[0];
      const cat = scores.find((s) => /고양이/.test(s.label)) || scores[1];
      html += vsDuel(dog?.score || 0, cat?.score || 0, free.recommend || '', dog?.note, cat?.note);
      html += `<div class="gauge-row" style="margin-top:8px">${scores.map((s) => gaugeSVG(s.score, s.label, s.note)).join('')}</div>`;
    } else if (data.mode === 'triangle') {
      html += triangleSVG(free.edges || scores.filter((s) => s.label !== '총점'), free.weakest);
      const total = scores.find((s) => s.label === '총점');
      if (total) html += `<div class="gauge-row">${gaugeSVG(total.score, '삼각 총점')}</div>`;
    } else {
      html += `<div class="gauge-row">${scores.map((s) => gaugeSVG(s.score, s.label, s.note)).join('')}</div>`;
    }

    const mainScore = scores.find((s) => /총점|조화도|종합/.test(s.label)) || scores[0];
    if (mainScore) {
      const score = Math.max(0, Math.min(100, Number(mainScore.score) || 0));
      const easy = score >= 80 ? '우리 집과 아주 잘 맞을 가능성이 높아요!' : score >= 60 ? '조금만 맞춰가면 좋은 팀이 될 수 있어요.' : '생활 루틴을 먼저 천천히 맞춰보면 좋아요.';
      const face = score >= 80 ? '🌟' : score >= 60 ? '🙂' : '🌱';
      html = `<div class="score-story"><div class="score-story-top"><span>${face} 우리 집과의 어울림 점수</span><strong>${score}<em>점</em></strong></div><div class="big-score-track"><i style="width:${score}%"></i></div><b>${easy}</b><small>점수가 높을수록 함께 지내는 그림이 더 편안하게 그려져요.</small></div>` + html;
    }
    root.innerHTML = html;
  }

  function compute(mode, values) {
    const H = window.PetHarmony;
    if (!H) throw new Error('PetHarmony engine missing');

    if (mode === 'overall') {
      const petISO = values.pet || estimateBirth(values.species === '고양이' ? 'cat' : 'dog');
      const analysis = H.analyze(petISO, values.owner);
      const saju = window.PetSaju.analyzePair(petISO, values.owner, '', '');
      const hint = deriveMbti(values);
      const lifestyleWeights = { high: 88, mid: 68, low: 48, out: 76, home: 64, mix: 70, steady: 84, flex: 68, learn: 58 };
      const lifestyleScores = LIFESTYLE_QUESTIONS.map((q) => lifestyleWeights[values[q.key]] || 60);
      const lifeScore = Math.round(lifestyleScores.reduce((sum, score) => sum + score, 0) / lifestyleScores.length);
      const total = Math.round((analysis.score + saju.compatScore + lifeScore) / 3);
      return {
        mode,
        title: '우리의 새 가족 종합 결과',
        free: {
          headline: `${values.petName ? `${values.petName}과 나` : '우리 펫과 나'}, 어울림 ${total}점`,
          scores: [
            { label: '생일 기반 케미', score: analysis.score },
            { label: '사주 케미', score: saju.compatScore },
            { label: '생활 준비도', score: lifeScore },
          ],
          summary: `${values.pet ? '' : '펫 생일 없이 기본 성향으로 계산했어요 · '}세 가지 결과를 한 번에 모아봤어요.`,
          sajuTeaser: hint ? `보호자 스타일: ${hint}` : '보호자 스타일 질문은 건너뛰었어요.',
          ownerMbti: hint,
          elements: analysis.elements,
          relationType: analysis.relation.type,
          harmonyDetail: {
            total,
            lifeScore,
            relationText: analysis.relation.text,
            petElement: analysis.elements.pet.dominant,
            ownerElement: analysis.elements.owner.dominant,
            hasPetBirth: !!values.pet,
          },
        },
        paid: { relation: analysis.relation, areas: analysis.areas, elements: analysis.elements, saju },
      };
    }

    if (mode === 'mbti') {
      const hint = deriveMbti(values);
      const scores = [
        { label: '에너지', score: values.mbtiEnergy === 'E' ? 78 : 42 },
        { label: '문제 해결', score: values.mbtiStyle === 'N' ? 76 : 48 },
        { label: '루틴 방식', score: values.mbtiRoutine === 'F' ? 72 : 54 },
      ];
      return {
        mode,
        title: '내 보호자 스타일',
        free: {
          headline: `나는 ${hint} 성향의 보호자`,
          scores,
          summary: '펫과 함께 살 때 드러나는 모습을 가볍게 정리해봤어요.',
          sajuTeaser: '정답이라기보다, 나와 펫의 생활 방식을 돌아보는 작은 힌트예요.',
          ownerMbti: hint,
        },
        paid: { saju: null },
      };
    }

    if (mode === 'adopt') {
      const petISO = values.pet || estimateBirth('dog');
      const analysis = H.analyze(petISO, values.owner);
      const saju = window.PetSaju.analyzePair(petISO, values.owner, values.petTime, values.ownerTime);
      return {
        mode,
        title: '우리 집에 와도 괜찮을까?',
        free: {
          headline: `${values.petName || '우리의 작은 친구'}와 함께할 점수`,
          scores: [
            { label: '명리 조화도', score: analysis.score },
            { label: '사주 케미', score: saju.compatScore },
          ],
          summary: `${values.pet ? '' : `${values.species || '펫'} 생일 없이 기본 성향으로 계산했어요 · `}${saju.compatTitle} · ${pairLine(analysis)}`,
          sajuTeaser: `${saju.petSummary} / ${saju.ownerSummary}`,
          ownerMbti: deriveMbti(values),
          elements: analysis.elements,
          relationType: analysis.relation.type,
        },
        paid: {
          relation: analysis.relation,
          areas: analysis.areas,
          elements: analysis.elements,
          saju,
        },
      };
    }

    if (mode === 'dogcat') {
      const dogISO = values.dog || estimateBirth('dog');
      const catISO = values.cat || estimateBirth('cat');
      const dog = H.analyze(dogISO, values.owner);
      const cat = H.analyze(catISO, values.owner);
      const dogSaju = window.PetSaju.analyzePair(dogISO, values.owner, '', values.ownerTime);
      const catSaju = window.PetSaju.analyzePair(catISO, values.owner, '', values.ownerTime);
      const dogBlend = Math.round((dog.score + dogSaju.compatScore) / 2);
      const catBlend = Math.round((cat.score + catSaju.compatScore) / 2);
      const recommend = dogBlend >= catBlend ? '강아지' : '고양이';
      return {
        mode,
        title: '강아지랑 고양이, 누가 더 찰떡?',
        free: {
          headline: '나와 더 맞는 쪽',
          scores: [
            { label: '나 ↔ 강아지', score: dogBlend, note: values.dog ? '' : '추정' },
            { label: '나 ↔ 고양이', score: catBlend, note: values.cat ? '' : '추정' },
          ],
          summary: `추천: ${recommend} · ${(recommend === '강아지' ? dogSaju : catSaju).compatTitle}`,
          sajuTeaser: recommend === '강아지' ? dogSaju.petSummary + ' / ' + dogSaju.ownerSummary : catSaju.petSummary + ' / ' + catSaju.ownerSummary,
          recommend,
          ownerMbti: deriveMbti(values),
        },
        paid: {
          dog,
          cat,
          dogSaju,
          catSaju,
          recommend,
          areasWinner: recommend === '강아지' ? dog.areas : cat.areas,
          saju: recommend === '강아지' ? dogSaju : catSaju,
        },
      };
    }

    const ab = H.harmony(values.a, values.b);
    const petISO = values.pet || estimateBirth('dog');
    const aPet = H.analyze(petISO, values.a);
    const bPet = H.analyze(petISO, values.b);
    const aSaju = window.PetSaju.analyzePair(petISO, values.a, values.petTime, values.aTime);
    const bSaju = window.PetSaju.analyzePair(petISO, values.b, values.petTime, values.bTime);
    const abSaju = window.PetSaju.analyzePair(values.a, values.b, values.aTime, values.bTime);
    const edgeScores = [
      { label: 'A ↔ B', score: Math.round((ab.score + abSaju.compatScore) / 2) },
      { label: 'A ↔ 펫', score: Math.round((aPet.score + aSaju.compatScore) / 2) },
      { label: 'B ↔ 펫', score: Math.round((bPet.score + bSaju.compatScore) / 2) },
    ];
    const total = Math.round(edgeScores.reduce((s, e) => s + e.score, 0) / 3);
    const weakest = edgeScores.reduce((w, e) => (e.score < w.score ? e : w), edgeScores[0]);
    return {
      mode,
      title: '둘 사이에 펫까지 오면?',
      free: {
        headline: `${values.petName || '우리 펫'}와 함께하는 세 식구 점수`,
        scores: edgeScores.concat([{ label: '총점', score: total }]),
        summary: `가장 약한 변: ${weakest.label} (${weakest.score}점) · ${aSaju.compatTitle}`,
        sajuTeaser: `${aSaju.petSummary} · A: ${aSaju.ownerSummary.replace('집사', 'A')} · B: ${bSaju.ownerSummary.replace('집사', 'B')}`,
        edges: edgeScores,
        weakest,
        total,
        ownerMbti: deriveMbti(values),
      },
      paid: {
        edges: edgeScores,
        weakest,
        aPet,
        bPet,
        ab,
        aSaju,
        bSaju,
        abSaju,
        areas: aPet.areas,
        saju: aSaju,
      },
    };
  }

  function renderResult(data, id) {
    syncUnlockStatus(data, id);
    state.payload = data;
    state.paid = !!(data.unlocked);
    $('#result-title').textContent = data.title;
    $('#result-headline').textContent = data.free.headline;
    const resultPhoto = $('#result-photo');
    if (resultPhoto) {
      resultPhoto.classList.toggle('hidden', !data.photoData);
      if (data.photoData) resultPhoto.querySelector('img').src = data.photoData;
    }
    const tabs = $('#scope-tabs');
    if (tabs) tabs.classList.toggle('hidden', data.mode !== 'overall');
    if (data.mode === 'overall') renderScope(data, 'life');
    renderViz(data);
    const scores = $('#result-scores');
    if (scores) {
      scores.innerHTML = '';
      scores.classList.add('hidden');
    }
    $('#result-summary').textContent = data.free.summary;
    const teaser = $('#result-saju-teaser');
    if (teaser) teaser.textContent = data.free.sajuTeaser || '';
    const mbti = $('#result-mbti');
    if (mbti) {
      mbti.textContent = data.free.ownerMbti ? `🧠 보호자 스타일 힌트: ${data.free.ownerMbti} — 펫과 함께 살 때 이런 성향이 보여요.` : '';
      mbti.classList.toggle('hidden', !data.free.ownerMbti);
    }
    const explain = $('#harmony-explain');
    if (explain) {
      const d = data.free.harmonyDetail;
      explain.innerHTML = d ? `<div class="explain-heading"><span class="eyebrow">점수 해설</span><strong>이 조화도는 이렇게 만들어졌어요</strong></div>
        <div class="explain-grid">
          <div><b>생일 기반 케미</b><p>두 사람의 기본적인 리듬과 에너지가 얼마나 잘 맞는지 본 점수예요.</p></div>
          <div><b>사주 케미</b><p>${d.petElement} 기운의 펫과 ${d.ownerElement} 기운의 보호자 사이 흐름을 참고했어요.</p></div>
          <div><b>생활 준비도</b><p>펫과 보낼 시간, 같이 할 활동, 돌봄 루틴에 대한 답을 바탕으로 계산했어요.</p></div>
        </div>
        <p class="explain-summary"><strong>${d.total >= 75 ? '서로 잘 맞춰갈 가능성이 높은 편이에요.' : d.total >= 55 ? '조금씩 맞춰가면 좋은 흐름을 만들 수 있어요.' : '생활 루틴을 천천히 맞추는 게 먼저예요.'}</strong> ${d.relationText} ${d.hasPetBirth ? '' : '펫 생일을 몰라 기본 성향으로 계산한 점수예요.'}</p>` : '';
      explain.classList.toggle('hidden', !d);
    }
    $('#share-text').textContent = `${data.free.headline}: ${data.free.scores.map((s) => `${s.label} ${s.score}`).join(' / ')} — ${data.free.summary}`;

    const detail = $('#paid-detail');
    const isMbti = data.mode === 'mbti';
    if (isMbti) {
      detail.innerHTML = '';
      $('#paid-lock').classList.add('hidden');
      detail.classList.add('hidden');
      $('#pdf-offer').classList.add('hidden');
    } else if (state.paid) {
      detail.innerHTML = renderPaidHtml(data);
      $('#paid-lock').classList.add('hidden');
      detail.classList.remove('blur');
      detail.classList.remove('hidden');
    } else {
      detail.innerHTML = '<p class="score-sub">상세 케어팁은 결제 후 열려요.</p>';
      $('#paid-lock').classList.remove('hidden');
      detail.classList.remove('blur');
      detail.classList.remove('hidden');
    }
    $('#result-id').textContent = id;
    attachPhotoUploader(data, id);
    setupPaypalButtons(data, id);
    show('result');
  }

  async function syncUnlockStatus(data, id) {
    if (!id || !/^https?:$/.test(window.location.protocol)) return;
    try {
      const response = await fetch(`/api/unlock-status?resultId=${encodeURIComponent(id)}`);
      if (!response.ok) {
        if (window.location.hostname.endsWith('.vercel.app')) {
          const changed = data.unlocked || data.pdfPaid;
          data.unlocked = false;
          data.pdfPaid = false;
          if (changed) {
            saveResult(id, data);
            renderResult(data, id);
          }
        }
        return;
      }
      const status = await response.json();
      const changed = data.unlocked !== !!status.tips || data.pdfPaid !== !!status.pdf;
      data.unlocked = !!status.tips;
      data.pdfPaid = !!status.pdf;
      if (changed) {
        saveResult(id, data);
        renderResult(data, id);
      }
    } catch {
      if (window.location.hostname.endsWith('.vercel.app')) {
        const changed = data.unlocked || data.pdfPaid;
        data.unlocked = false;
        data.pdfPaid = false;
        if (changed) {
          saveResult(id, data);
          renderResult(data, id);
        }
      }
    }
  }

  function attachPhotoUploader(data, id) {
    const input = $('#petPhoto');
    const preview = $('#photo-preview');
    if (!input || input.dataset.bound) return;
    input.dataset.bound = 'true';
    if (data.photoData && preview) {
      preview.querySelector('img').src = data.photoData;
      preview.classList.remove('hidden');
    }
    input.addEventListener('change', (event) => {
      const file = event.target.files?.[0];
      if (!file) return;
      const reader = new FileReader();
      reader.onload = () => {
        data.photoData = reader.result;
        state.photoData = reader.result;
        saveResult(id, data);
        if (preview) {
          preview.querySelector('img').src = reader.result;
          preview.classList.remove('hidden');
        }
      };
      reader.readAsDataURL(file);
    });
  }

  async function setupPaypalButtons(data, id) {
    if (data.mode === 'mbti') {
      const locked = document.querySelector('.locked');
      if (locked) locked.classList.add('hidden');
      return;
    }
    if (!(await loadPaypalSdk())) return;
    try {
      const health = await fetch('/api/health').then((response) => response.json());
      if (!health.paymentConfigured) {
        ['paypal-tips-button', 'paypal-pdf-button'].forEach((key) => {
          const el = document.getElementById(key);
          if (el) el.innerHTML = '<span class="payment-status">결제 설정을 준비 중이에요.</span>';
        });
        return;
      }
    } catch {
      const el = document.getElementById('paypal-tips-button');
      if (el) el.innerHTML = '<span class="payment-status">결제 연결을 확인 중이에요.</span>';
      return;
    }
    const renderPayment = (containerId, product, description, paid) => {
      const container = document.getElementById(containerId);
      if (!container) return;
      container.innerHTML = '';
      window.paypal.Buttons({
        style: { shape: 'rect', color: 'gold', layout: 'vertical', label: 'paypal', height: 40 },
        createOrder: async () => {
          const response = await fetch('/api/orders', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ resultId: id, product }) });
          if (!response.ok) throw new Error('order_create_failed');
          return (await response.json()).orderId;
        },
        onApprove: async (details) => {
          const response = await fetch('/api/capture', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ resultId: id, product, orderId: details.orderID }) });
          if (!response.ok) throw new Error('capture_failed');
          paid();
        },
        onCancel: () => alert('결제가 취소됐어요. 원할 때 다시 시도할 수 있어요.'),
        onError: () => alert('PayPal 결제 중 문제가 생겼어요. 잠시 후 다시 시도해 주세요.')
      }).render(`#${containerId}`);
    };
    renderPayment('paypal-tips-button', 'tips', 'Pet audition detailed care tips', () => {
      data.unlocked = true;
      saveResult(id, data);
      renderResult(data, id);
      alert('결제 완료! 상세 케어팁을 열었어요.');
    });
    const pdfOffer = $('#pdf-offer');
    if (!data.unlocked) {
      if (pdfOffer) pdfOffer.classList.add('hidden');
      return;
    }
    if (pdfOffer) pdfOffer.classList.remove('hidden');
    if (data.pdfPaid) {
      $('#paypal-pdf-button').classList.add('hidden');
      $('#save-pdf').classList.remove('hidden');
      $('#email-delivery').classList.remove('hidden');
      return;
    }
    renderPayment('paypal-pdf-button', 'pdf', 'Pet audition photo report - print or save as PDF', () => {
      data.pdfPaid = true;
      saveResult(id, data);
      $('#paypal-pdf-button').classList.add('hidden');
      $('#save-pdf').classList.remove('hidden');
      const emailDelivery = $('#email-delivery');
      if (emailDelivery) emailDelivery.classList.remove('hidden');
      alert('결제 완료! Print / Save as PDF 버튼이 열렸어요.');
    });
  }

  function renderScope(data, scope) {
    const out = $('#scope-result');
    if (!out || !data.free.harmonyDetail) return;
    const d = data.free.harmonyDetail;
    const base = d.total;
    const petEl = d.petElement;
    const now = new Date();
    let score = base;
    let label = '평생 흐름';
    let tip = '생일과 생활 방식을 바탕으로 본 기본 조화도예요.';
    if (scope === 'today') {
      score = window.PetHarmony.todayIndex(base, petEl, now);
      label = '오늘의 흐름';
      tip = '오늘은 산책·놀이·휴식 중 어떤 활동이 더 잘 맞는지 가볍게 참고해보세요.';
    } else if (scope === 'month') {
      score = window.PetHarmony.todayIndex(base, petEl, new Date(now.getFullYear(), now.getMonth(), 1));
      label = '이번 달 흐름';
      tip = '이번 달은 새로운 루틴을 시작하거나 함께하는 시간을 조정할 때 참고할 수 있어요.';
    } else if (scope === 'year') {
      score = window.PetHarmony.todayIndex(base, petEl, new Date(now.getFullYear(), 0, 1));
      label = '올해 흐름';
      tip = '올해의 큰 방향을 보는 참고값이에요. 실제 생활에서는 펫의 상태와 환경을 우선해 주세요.';
    }
    out.innerHTML = `<span class="scope-label">${label}</span><strong>조화도 ${score}점</strong><p>${tip}</p>`;
    out.classList.remove('hidden');
    document.querySelectorAll('#scope-tabs button').forEach((button) => button.classList.toggle('active', button.dataset.scope === scope));
  }

  function renderPaidHtml(data) {
    if (data.mode === 'overall') {
      const s = data.paid.saju;
      const areas = (data.paid.areas || []).map((a) => `<div class="pill">${a.emoji} ${a.name}: ${a.grade} — ${a.tip}</div>`).join('');
      return sajuBlock(s) + `<p>종합 점수는 생일 기반 케미·사주 케미·생활 준비도를 함께 참고했어요.</p><div>${areas}</div>`;
    }
    if (data.mode === 'mbti') {
      return `<div class="card mbti-result-card"><div class="badge">보호자 스타일 힌트</div><p><strong>${data.free.ownerMbti}</strong> 성향이라면, 펫과 지낼 때 내 방식과 상대의 신호를 함께 살펴보는 게 좋아요.</p><p>이 결과는 성격을 단정하는 진단이 아니라, 함께 사는 방식을 생각해보는 가벼운 참고 자료예요.</p></div>`;
    }
    if (data.mode === 'adopt') {
      const s = data.paid.saju;
      const areas = (data.paid.areas || []).map((a) => `<div class="pill">${a.emoji} ${a.name}: ${a.grade} — ${a.tip}</div>`).join('');
      return sajuBlock(s) +
        `<p><strong>명리 ${data.paid.relation.type}</strong> — ${data.paid.relation.text}</p>
        <p>펫 우세 오행: ${data.paid.elements.pet.dominant} / 집사 우세 오행: ${data.paid.elements.owner.dominant}</p>
        <div>${areas}</div>
        <p style="margin-top:12px">첫 30일 팁: 상극·주의 영역부터 루틴을 짧게 쪼개 적응시키세요.</p>`;
    }
    if (data.mode === 'dogcat') {
      const w = data.paid.recommend === '강아지' ? data.paid.dog : data.paid.cat;
      const s = data.paid.saju;
      const areas = (data.paid.areasWinner || []).map((a) => `<div class="pill">${a.emoji} ${a.name}: ${a.tip}</div>`).join('');
      return sajuBlock(s) +
        `<p>추천 <strong>${data.paid.recommend}</strong> — ${w.relation.text}</p>
        <p>견 명리 ${data.paid.dog.score} / 사주 ${data.paid.dogSaju.compatScore} · 묘 명리 ${data.paid.cat.score} / 사주 ${data.paid.catSaju.compatScore}</p>
        <div>${areas}</div>
        <p style="margin-top:12px">둘 다 키울 계획이면 낮은 쪽 변부터 케어 루틴을 보완하세요.</p>`;
    }
    const areas = (data.paid.areas || []).slice(0, 4).map((a) => `<div class="pill">${a.emoji} ${a.name}: ${a.tip}</div>`).join('');
    return sajuBlock(data.paid.aSaju) +
      `<p>약한 변 <strong>${data.paid.weakest.label}</strong> (${data.paid.weakest.score}점)을 먼저 챙기세요.</p>
      <p>A↔펫: ${data.paid.aPet.relation.type} · B↔펫: ${data.paid.bPet.relation.type}</p>
      <p>B 사주 요약: ${data.paid.bSaju.ownerSummary} — ${data.paid.bSaju.compatTitle}</p>
      <div>${areas}</div>
      <p style="margin-top:12px">역할 분담: 약한 변에 해당하는 사람이 산책/놀이 중 하나를 전담하면 균형이 빨리 올라가요.</p>`;
  }

  function sajuBlock(s) {
    if (!s) return '';
    return `<div class="card" style="margin-bottom:12px;background:#fff7ed">
      <div class="badge">사주 풀이</div>
      <p><strong>${s.compatTitle}</strong> (${s.compatScore}점)</p>
      <p>${s.pastDesc}</p>
      <p>${s.synergyDesc}</p>
      <p style="margin-top:10px"><strong>${s.petSummary}</strong><br>${s.petDesc.replace(/\n/g, '<br>')}</p>
      <p style="margin-top:10px"><strong>${s.ownerSummary}</strong><br>${s.ownerDesc.replace(/\n/g, '<br>')}</p>
    </div>`;
  }

  function readForm() {
    const meta = MODE_META[state.mode];
    const values = {};
    for (const f of meta.fields) {
      const el = document.getElementById(f.key);
      values[f.key] = el ? el.value.trim() : '';
      if (f.required && !values[f.key]) {
        alert(f.label + '을(를) 입력해 주세요.');
        return null;
      }
    }
    MBTI_QUESTIONS.forEach((q) => {
      const el = document.getElementById(q.key);
      values[q.key] = el ? el.value.trim() : '';
    });
    LIFESTYLE_QUESTIONS.forEach((q) => {
      const el = document.getElementById(q.key);
      values[q.key] = el ? el.value.trim() : '';
    });
    if (state.mode === 'overall') {
      const allQuestions = MBTI_QUESTIONS.concat(LIFESTYLE_QUESTIONS);
      const missing = allQuestions.find((q) => !values[q.key]);
      if (missing) {
        alert(`${missing.label}에 답해 주세요. 모든 검사를 마치면 어울림 점수를 볼 수 있어요.`);
        document.getElementById(missing.key)?.focus();
        return null;
      }
    }
    return values;
  }

  $('#mode-adopt').addEventListener('click', () => openForm('adopt'));
  $('#mode-overall').addEventListener('click', () => openForm('overall'));
  $('#mode-mbti').addEventListener('click', () => openForm('mbti'));
  $('#hero-start').addEventListener('click', () => openForm('adopt'));
  $('#mode-dogcat').addEventListener('click', () => openForm('dogcat'));
  $('#mode-triangle').addEventListener('click', () => openForm('triangle'));
  $('#back-home').addEventListener('click', () => show('home'));
  $('#back-form').addEventListener('click', () => {
    const mode = state.mode || state.payload?.mode;
    if (mode && MODE_META[mode]) openForm(mode);
    else show('home');
  });

  $('#harmony-form').addEventListener('submit', (e) => {
    e.preventDefault();
    const values = readForm();
    if (!values) return;
    const data = compute(state.mode, values);
    const id = uid();
    data.id = id;
    data.photoData = state.photoData;
    data.unlocked = false;
    data.createdAt = new Date().toISOString();
    saveResult(id, data);
    location.hash = '#/result/' + id;
    renderResult(data, id);
  });

  $('#copy-share').addEventListener('click', async () => {
    const text = $('#share-text').textContent;
    const shareUrl = new URL('https://petnna-app.vercel.app/');
    shareUrl.searchParams.set('utm_source', 'share');
    shareUrl.searchParams.set('utm_medium', 'copy');
    shareUrl.searchParams.set('utm_campaign', state.mode || 'result');
    const payload = text + '\n' + shareUrl.toString();
    try {
      await navigator.clipboard.writeText(payload);
      alert('공유 문구를 복사했어요.');
    } catch {
      prompt('복사해서 공유하세요', payload);
    }
  });
  $('#save-pdf').addEventListener('click', () => {
    window.print();
  });
  $('#send-report-email').addEventListener('click', async () => {
    const email = $('#report-email').value.trim();
    const status = $('#email-delivery-status');
    if (!state.payload?.pdfPaid) return;
    if (!email || !/^\S+@\S+\.\S+$/.test(email)) {
      status.textContent = '이메일 주소를 확인해 주세요.';
      return;
    }
    status.textContent = '링크를 준비하고 있어요.';
    try {
      const response = await fetch('/api/report-link', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ resultId: state.payload.id, email }),
      });
      const body = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(body.error || 'email_failed');
      status.textContent = '리포트 링크를 이메일로 보냈어요.';
    } catch (error) {
      status.textContent = error.message === 'email_not_configured'
        ? '이메일 발송 설정 전이에요. 지금은 Print / Save as PDF를 이용해 주세요.'
        : '이메일을 보내지 못했어요. 주소를 확인하거나 Print / Save as PDF를 이용해 주세요.';
    }
  });

  document.querySelectorAll('#scope-tabs button').forEach((button) => {
    button.addEventListener('click', () => renderScope(state.payload, button.dataset.scope));
  });

  async function route() {
    const hash = location.hash || '#/';
    const m = hash.match(/^#\/result\/([\w-]+)/);
    if (m) {
      let data = loadResult(m[1]);
      if (!data && (window.location.protocol === 'http:' || window.location.protocol === 'https:')) {
        try {
          const response = await fetch(`/api/results?resultId=${encodeURIComponent(m[1])}`);
          if (response.ok) {
            const remote = await response.json();
            data = remote.payload;
            saveResult(m[1], data);
          }
        } catch {}
      }
      if (!data) { show('home'); return; }
      // paid unlock only via payment flow — no /full hash bypass
      renderResult(data, m[1]);
      return;
    }
    show('home');
  }

  window.addEventListener('hashchange', route);
  route();
})();
