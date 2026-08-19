/* 공통 스크립트 — 등장 애니메이션 + lite 유튜브 플레이어 + Supabase(방명록·방문자수) */

// ─── Supabase (anon key는 공개용 키 — RLS로 보호됨) ───
const SB_URL = 'https://nlgjsdffgkygaylbjooc.supabase.co';
const SB_KEY = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Im5sZ2pzZGZmZ2t5Z2F5bGJqb29jIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzkxMzI0NTUsImV4cCI6MjA5NDcwODQ1NX0.hcx23C6GcjLn3HgI8IWBECd8luWTPkvYwVmi42XFt-s';

async function sbFetch(path, opts) {
  const o = opts || {};
  o.headers = Object.assign({ 'apikey': SB_KEY, 'Authorization': 'Bearer ' + SB_KEY }, o.headers || {});
  const res = await fetch(SB_URL + path, o);
  if (!res.ok) throw new Error('supabase ' + res.status);
  const text = await res.text();
  return text ? JSON.parse(text) : null;
}

// ─── 방문자 수: 최상단 내비에 배지로 표시. 세션당 1회만 증가, 실패하면 조용히 숨김 ───
(async function visitCounter() {
  let el = document.getElementById('visits');
  if (!el) {
    const nav = document.querySelector('nav');
    if (!nav) return;
    el = document.createElement('span');
    el.id = 'visits'; el.className = 'visits'; el.hidden = true;
    el.innerHTML = '👀 <b></b>';
    nav.appendChild(el);
  }
  try {
    let views;
    if (sessionStorage.getItem('bsj_counted')) {
      const rows = await sbFetch('/rest/v1/homepage_page_views?select=views&id=eq.1');
      views = rows && rows[0] && rows[0].views;
    } else {
      views = await sbFetch('/rest/v1/rpc/homepage_hit', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' });
      sessionStorage.setItem('bsj_counted', '1');
    }
    if (typeof views === 'number' || typeof views === 'string') {
      el.querySelector('b').textContent = Number(views).toLocaleString();
      el.hidden = false;
    }
  } catch (e) { /* 테이블 미생성/네트워크 실패 — 표시 생략 */ }
})();

// 스크롤 등장 애니메이션
const io = new IntersectionObserver((es) => {
  es.forEach(e => { if (e.isIntersecting) { e.target.classList.add('on'); io.unobserve(e.target); } });
}, { threshold: .12 });
document.querySelectorAll('.rv').forEach(el => io.observe(el));
// 폴백: 어떤 이유로든 관찰이 안 돌면 2.5초 뒤 전부 표시 (콘텐츠가 숨겨진 채 남지 않게)
setTimeout(() => document.querySelectorAll('.rv:not(.on)').forEach(el => el.classList.add('on')), 2500);

// 숫자 카운트업 ([data-count])
const cio = new IntersectionObserver((es) => {
  es.forEach(e => {
    if (!e.isIntersecting) return;
    const el = e.target, target = +el.dataset.count, t0 = performance.now();
    const tick = (t) => {
      const p = Math.min((t - t0) / 900, 1);
      el.textContent = Math.round(target * (1 - Math.pow(1 - p, 3))) + (el.dataset.suffix || '');
      if (p < 1) requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
    cio.unobserve(el);
  });
}, { threshold: .6 });
document.querySelectorAll('[data-count]').forEach(el => cio.observe(el));

// lite 유튜브: 썸네일 클릭 시에만 iframe 로드 (성능)
function mountLitePlayers(root) {
  (root || document).querySelectorAll('.media[data-yt]').forEach(m => {
    if (m.dataset.mounted) return;
    m.dataset.mounted = '1';
    const id = m.dataset.yt;
    m.innerHTML = '<img loading="lazy" src="https://i.ytimg.com/vi/' + id + '/hqdefault.jpg" alt="">' +
      '<div class="play" role="button" aria-label="영상 재생"><span>▶</span></div>';
    m.querySelector('.play').addEventListener('click', () => {
      m.innerHTML = '<iframe src="https://www.youtube-nocookie.com/embed/' + id +
        '?autoplay=1" title="YouTube" referrerpolicy="origin" ' +
        'allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture" allowfullscreen></iframe>';
    });
  });
}
mountLitePlayers();

/* ─── 스크린샷 갤러리 (개발일기·프로젝트 상세 공용) ───────────────
   쓰는 법 ①마크업: <div class="shots"><figure>…</figure></div>
           ②데이터: renderShots(el, [{src, cap, pixel}])
   클릭하면 원본 크기로 확대해서 보여준다. */
function renderShots(el, list){
  if (!el || !list || !list.length) return;
  const esc = s => { const d = document.createElement('div'); d.textContent = s || ''; return d.innerHTML; };
  el.className = 'shots';
  el.innerHTML = list.map(s =>
    '<figure' + (s.pixel ? ' class="pixel"' : '') + '>' +
      '<button class="sfig" type="button" data-src="' + esc(s.src) + '" data-cap="' + esc(s.cap) + '">' +
        '<img loading="lazy" src="' + esc(s.src) + '" alt="' + esc(s.cap) + '">' +
      '</button>' +
      (s.cap ? '<figcaption>' + esc(s.cap) + '</figcaption>' : '') +
    '</figure>').join('');
}

// 확대 보기 — 페이지마다 따로 만들지 않고 문서에 하나만 둔다
(function shotViewer(){
  let ov = null, lastFocus = null;
  function ensure(){
    if (ov) return ov;
    ov = document.createElement('div');
    ov.className = 'lb-overlay';
    ov.innerHTML = '<div class="lb" role="dialog" aria-modal="true" aria-label="스크린샷 확대">' +
      '<button class="lb-close" type="button" aria-label="닫기">✕</button>' +
      '<div class="lb-media shots-view"></div>' +
      '<div class="lb-body"><p class="scap"></p></div></div>';
    document.body.appendChild(ov);
    ov.querySelector('.lb-close').addEventListener('click', close);
    ov.addEventListener('click', e => { if (e.target === ov) close(); });
    return ov;
  }
  function close(){
    if (!ov) return;
    ov.classList.remove('open');
    ov.querySelector('.shots-view').innerHTML = '';
    document.body.style.overflow = '';
    if (lastFocus && lastFocus.focus) lastFocus.focus();
    lastFocus = null;
  }
  document.addEventListener('click', e => {
    const b = e.target.closest && e.target.closest('.sfig'); if (!b) return;
    const o = ensure();
    o.querySelector('.shots-view').innerHTML =
      '<img src="' + b.dataset.src + '" alt="' + (b.dataset.cap || '') + '">';
    o.querySelector('.scap').textContent = b.dataset.cap || '';
    o.classList.add('open');
    document.body.style.overflow = 'hidden';
    lastFocus = b;
    o.querySelector('.lb-close').focus();
  });
  document.addEventListener('keydown', e => {
    if (e.key === 'Escape' && ov && ov.classList.contains('open')) close();
  });
})();
