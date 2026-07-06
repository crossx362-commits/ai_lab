import React, { useEffect, useState } from 'react';
import {
  BarChart3,
  Bell,
  CircleUserRound,
  ExternalLink,
  Heart,
  LayoutDashboard,
  LockKeyhole,
  LogOut,
  MessageSquare,
  Search,
  Settings,
  ShoppingBag,
  UserPlus,
  X,
} from 'lucide-react';
import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { activity, chartData, posts } from './data/mockData.js';
import { checkSupabaseHealth, isSupabaseConfigured } from './lib/supabaseClient.js';
import {
  getSession,
  onAuthStateChange,
  signInWithEmail,
  signInWithProvider,
  signOut,
  signUpWithEmail,
} from './services/authService.js';
import { getMyProfile, updateMyProfile } from './services/profileService.js';
import { createPost, listPosts } from './services/postService.js';
import { createComment, listComments, toggleLike } from './services/engagementService.js';
import { getAdminStats, updatePostStatus } from './services/adminService.js';
import { fetchSpecs, searchProducts } from './services/productSearchService.js';

const tabs = [
  { id: 'compare', label: '비교', icon: Search },
  { id: 'board', label: '게시판', icon: MessageSquare },
  { id: 'profile', label: '마이', icon: CircleUserRound },
  { id: 'admin', label: '관리자', icon: LayoutDashboard },
];

// 실제 쇼핑몰 검색 엔드포인트. 앱은 가격/상품을 지어내지 않고,
// 검색어를 각 쇼핑몰의 실제 검색 결과 페이지로 그대로 전달한다.
const MALLS = [
  { name: '네이버쇼핑', note: '가격비교', build: (q) => `https://search.shopping.naver.com/search/all?query=${q}` },
  { name: '쿠팡', note: '로켓/일반배송', build: (q) => `https://www.coupang.com/np/search?q=${q}` },
  { name: '11번가', note: '쿠폰가', build: (q) => `https://search.11st.co.kr/Search.tmall?kwd=${q}` },
  { name: 'G마켓', note: '카드할인', build: (q) => `https://browse.gmarket.co.kr/search?keyword=${q}` },
  { name: '다나와', note: '가격비교', build: (q) => `https://search.danawa.com/dsearch.php?k1=${q}` },
];

function App() {
  const [activeTab, setActiveTab] = useState('compare');
  const [query, setQuery] = useState('');
  const [authMode, setAuthMode] = useState(null);
  const [session, setSession] = useState(null);
  const [hasSearched, setHasSearched] = useState(false);

  useEffect(() => {
    if (!isSupabaseConfigured) return undefined;
    getSession().then(setSession).catch(() => setSession(null));
    return onAuthStateChange(setSession);
  }, []);

  return (
    <div className="app-shell">
      <Header
        activeTab={activeTab}
        session={session}
        setActiveTab={setActiveTab}
        setAuthMode={setAuthMode}
        setSession={setSession}
      />
      <main className="page">
        {activeTab === 'compare' && (
          <CompareView
            hasSearched={hasSearched}
            query={query}
            setHasSearched={setHasSearched}
            setQuery={setQuery}
          />
        )}
        {activeTab === 'board' && <BoardView session={session} setAuthMode={setAuthMode} />}
        {activeTab === 'profile' && <ProfileView session={session} setAuthMode={setAuthMode} />}
        {activeTab === 'admin' && <AdminView session={session} setAuthMode={setAuthMode} />}
        {activeTab !== 'compare' && <BackendStatus />}
      </main>
      {authMode && <AuthModal mode={authMode} setMode={setAuthMode} setSession={setSession} />}
    </div>
  );
}

function BackendStatus() {
  const [health, setHealth] = useState({
    ok: isSupabaseConfigured,
    message: isSupabaseConfigured ? 'Supabase 확인 중' : 'Supabase 연결 대기',
  });

  useEffect(() => {
    checkSupabaseHealth().then(setHealth);
  }, []);

  return (
    <section className="backend-status">
      <div>
        <span className={`status-dot ${health.ok ? 'is-on' : ''}`} />
        <strong>{health.message}</strong>
      </div>
      <p>
        인증, 프로필, 게시판, 댓글, 좋아요, 관리자 API 레이어가 준비되었습니다.
        실제 DB 저장은 Supabase 프로젝트와 migration 적용 후 동작합니다.
      </p>
    </section>
  );
}

function Header({ activeTab, session, setActiveTab, setAuthMode, setSession }) {
  const handleSignOut = async () => {
    if (!isSupabaseConfigured) return;
    await signOut();
    setSession(null);
  };

  return (
    <header className="topbar">
      <div className="brand">
        <span className="brand-mark"><ShoppingBag className="icon" /></span>
        <div>
          <strong>모두비교</strong>
          <span>여러 쇼핑몰 한번에 검색</span>
        </div>
      </div>
      <nav className="tabs" aria-label="주요 메뉴">
        {tabs.map(({ id, label, icon: Icon }) => (
          <button
            className={`tab ${activeTab === id ? 'is-active' : ''}`}
            key={id}
            onClick={() => setActiveTab(id)}
            type="button"
          >
            <Icon className="icon" />
            <span>{label}</span>
          </button>
        ))}
      </nav>
      <div className="auth-actions">
        {session ? (
          <>
            <span className="session-pill">{session.user.email}</span>
            <button className="ghost-btn" onClick={handleSignOut} type="button"><LogOut className="icon" />로그아웃</button>
          </>
        ) : (
          <>
            <button className="ghost-btn" onClick={() => setAuthMode('login')} type="button"><LockKeyhole className="icon" />로그인</button>
            <button className="btn" onClick={() => setAuthMode('signup')} type="button"><UserPlus className="icon" />회원가입</button>
          </>
        )}
      </div>
    </header>
  );
}

function AuthModal({ mode, setMode, setSession }) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [message, setMessage] = useState(isSupabaseConfigured ? '' : 'Supabase 환경변수를 먼저 설정해야 실제 인증이 동작합니다.');
  const [loading, setLoading] = useState(false);
  const isLogin = mode === 'login';

  const submit = async (event) => {
    event.preventDefault();
    if (!isSupabaseConfigured) {
      setMessage('.env에 VITE_SUPABASE_URL과 VITE_SUPABASE_ANON_KEY를 넣어주세요.');
      return;
    }

    setLoading(true);
    setMessage('');
    try {
      const data = isLogin
        ? await signInWithEmail(email, password)
        : await signUpWithEmail(email, password);
      setSession(data.session ?? null);
      setMessage(isLogin ? '로그인되었습니다.' : '회원가입 확인 메일을 확인해주세요.');
      if (data.session) setMode(null);
    } catch (error) {
      setMessage(error.message);
    } finally {
      setLoading(false);
    }
  };

  const oauth = async (provider) => {
    if (!isSupabaseConfigured) {
      setMessage('.env 설정 후 소셜 로그인을 사용할 수 있습니다.');
      return;
    }
    await signInWithProvider(provider);
  };

  return (
    <div className="modal-backdrop" role="presentation">
      <section className="auth-modal" role="dialog" aria-modal="true" aria-label="인증">
        <button className="modal-close" onClick={() => setMode(null)} type="button"><X className="icon" /></button>
        <h2>{isLogin ? '로그인' : '회원가입'}</h2>
        <p>{isLogin ? '저장한 비교와 활동 내역을 불러옵니다.' : '이메일 계정으로 가격 비교를 시작합니다.'}</p>
        <form className="auth-form" onSubmit={submit}>
          <label>
            이메일
            <input onChange={(event) => setEmail(event.target.value)} placeholder="you@example.com" type="email" value={email} />
          </label>
          <label>
            비밀번호
            <input onChange={(event) => setPassword(event.target.value)} placeholder="8자 이상" type="password" value={password} />
          </label>
          <button className="btn" disabled={loading} type="submit">{loading ? '처리 중' : isLogin ? '로그인' : '회원가입'}</button>
        </form>
        <div className="oauth-row">
          <button className="ghost-btn" onClick={() => oauth('google')} type="button">Google</button>
          <button className="ghost-btn" onClick={() => oauth('kakao')} type="button">Kakao</button>
        </div>
        {message && <span className="auth-message">{message}</span>}
        <button className="link-btn" onClick={() => setMode(isLogin ? 'signup' : 'login')} type="button">
          {isLogin ? '계정이 없나요? 회원가입' : '이미 계정이 있나요? 로그인'}
        </button>
      </section>
    </div>
  );
}

function CompareView({ hasSearched, query, setHasSearched, setQuery }) {
  const trimmed = query.trim();
  const encoded = encodeURIComponent(trimmed);
  const [state, setState] = useState({ loading: false, configured: true, items: [], error: '' });
  // 비교 대상은 검색을 넘나들며 유지된다(각 항목에 담긴 시점의 검색어 _query 부착).
  const [selected, setSelected] = useState([]);

  useEffect(() => {
    if (!hasSearched || !trimmed) return undefined;
    let active = true;
    setState((current) => ({ ...current, loading: true, error: '' }));
    searchProducts(trimmed).then((data) => {
      if (!active) return;
      setState({ loading: false, configured: data.configured, items: data.items, error: data.error });
    });
    return () => { active = false; };
  }, [hasSearched, trimmed]);

  const toggleSelect = (item) => {
    const key = itemKey(item);
    setSelected((current) => (
      current.some((entry) => itemKey(entry) === key)
        ? current.filter((entry) => itemKey(entry) !== key)
        : [...current, { ...item, _query: trimmed }].slice(-3)
    ));
  };
  const removeSelected = (key) => setSelected((current) => current.filter((entry) => itemKey(entry) !== key));
  const selectedKeys = selected.map(itemKey);

  const hasProducts = state.configured && state.items.length > 0;
  const showLauncher = !state.loading && !hasProducts;

  return (
    <>
      <section className={`search-hero ${hasSearched ? 'has-results' : ''}`}>
        <SearchPanel query={query} setHasSearched={setHasSearched} setQuery={setQuery} />
      </section>
      {hasSearched && selected.length > 0 && (
        <ComparisonPanel items={selected} onClear={() => setSelected([])} onRemove={removeSelected} />
      )}
      {hasSearched && !trimmed && (
        <div className="empty-result">
          <Search className="icon" />
          <strong>검색어를 입력하세요</strong>
          <span>상품명 또는 모델명으로 실제 상품을 검색합니다.</span>
        </div>
      )}
      {hasSearched && trimmed && (
        <>
          {state.loading && (
            <div className="empty-result">
              <Search className="icon" />
              <strong>실제 상품을 불러오는 중…</strong>
              <span>‘{trimmed}’ 네이버 쇼핑 검색 중입니다.</span>
            </div>
          )}
          {!state.loading && hasProducts && (
            <ProductResults
              items={state.items}
              onToggle={toggleSelect}
              query={trimmed}
              selectedKeys={selectedKeys}
            />
          )}
          {showLauncher && (
            <MallLauncher encoded={encoded} reason={launcherReason(state)} trimmed={trimmed} />
          )}
          <BackendStatus />
        </>
      )}
    </>
  );
}

function launcherReason(state) {
  if (state.configured === false) {
    return '실시간 상품 조회는 네이버 API 키 설정 후 표시됩니다. 지금은 실제 쇼핑몰 검색으로 연결합니다.';
  }
  if (state.error) {
    return '실시간 조회에 실패했습니다. 실제 쇼핑몰 검색으로 연결합니다.';
  }
  return '해당 검색어의 상품을 각 쇼핑몰에서 직접 확인하세요.';
}

function itemKey(item) {
  return `${item.id}-${item.link}`;
}

const SPEC_COLORS = ['화이트', '블랙', '블루', '핑크', '그린', '옐로우', '퍼플', '내추럴', '티타늄', '실버', '골드', '그레이', '스페이스', '레드', '민트', '베이지', '그래파이트'];

// 상품명(실제 등록 텍스트)에서만 속성을 추출한다. 없는 값은 지어내지 않는다.
function parseSpecs(title) {
  const text = String(title || '');
  const storage = (text.match(/(\d+\s?TB)/i) || text.match(/(\d+\s?GB)/i) || [])[1];
  const color = SPEC_COLORS.find((name) => text.includes(name));
  return { storage: storage ? storage.replace(/\s+/g, '') : '', color: color || '' };
}

function ProductResults({ items, onToggle, query, selectedKeys }) {
  const prices = items.map((item) => item.price).filter((price) => price > 0);
  const minPrice = prices.length ? Math.min(...prices) : 0;

  return (
    <section className="product-results">
      <div className="section-title">
        <h2>‘{query}’ 실시간 상품</h2>
        <span>{items.length}개 · 클릭해 비교(다른 검색어도 담김, 최대 3)</span>
      </div>
      <div className="product-grid">
        {items.map((item) => {
          const key = itemKey(item);
          const selected = selectedKeys.includes(key);
          return (
            <div
              className={`result-card ${item.price === minPrice ? 'is-best' : ''} ${selected ? 'is-selected' : ''}`}
              key={key}
              onClick={() => onToggle(item)}
              onKeyDown={(event) => {
                if (event.key === 'Enter' || event.key === ' ') {
                  event.preventDefault();
                  onToggle(item);
                }
              }}
              role="button"
              tabIndex={0}
            >
              <img alt="" loading="lazy" src={item.image} />
              <div className="result-info">
                <span className="muted">{item.mall}{item.price === minPrice ? ' · 최저가' : ''}</span>
                <h3>{item.title}</h3>
                <strong>{item.price.toLocaleString()}원</strong>
                <div className="result-actions">
                  <span className={`compare-flag ${selected ? 'is-on' : ''}`}>{selected ? '✓ 비교중' : '비교 담기'}</span>
                  <a className="buy-link" href={item.link} onClick={(event) => event.stopPropagation()} rel="noreferrer" target="_blank">
                    구매 <ExternalLink className="icon" />
                  </a>
                </div>
              </div>
            </div>
          );
        })}
      </div>
    </section>
  );
}

// 다나와 조회 결과를 세션 동안 재사용(같은 모델 재선택 시 재요청 방지).
const specCache = new Map();
const SPEC_ORDER = ['화면', '램', 'CPU', '카메라', '최대충전', '배터리', '방수', '무게', '두께', '출시가', '특징'];

// 검색 결과에 같은 라인의 변형(프로/맥스/울트라 등)이 섞일 수 있어, 제목에서 변형 키워드를
// 감지해 다나와 쿼리에 반영한다. (검색어=신뢰 가능한 기본 모델, 변형+용량으로 정확히 특정)
const MODEL_VARIANTS = ['프로맥스', '프로 맥스', 'promax', 'max', '맥스', '울트라', 'ultra', '플러스', 'plus', '미니', 'mini', '에어', 'air'];

// 항목에 담길 때 저장해 둔 검색어(item._query)를 기준으로 다나와 쿼리를 만든다.
// 그래야 다른 검색어로 담은 상품(아이폰/갤럭시)도 각자 올바른 스펙을 조회한다.
function danawaQuery(item) {
  const base = String(item._query || '').toLowerCase();
  const title = String(item.title || '').toLowerCase();
  const { storage } = parseSpecs(item.title);
  const variant = MODEL_VARIANTS.find((word) => title.includes(word.toLowerCase()) && !base.includes(word.toLowerCase()));
  return [item._query || '', variant, storage].filter(Boolean).join(' ').replace(/\s+/g, ' ').trim();
}

function ComparisonPanel({ items, onClear, onRemove }) {
  const [specMap, setSpecMap] = useState({});
  const keysSignature = items.map(itemKey).join('|');

  useEffect(() => {
    let active = true;
    items.forEach((item) => {
      const key = itemKey(item);
      const dq = danawaQuery(item);
      if (specCache.has(dq)) {
        const cached = specCache.get(dq);
        setSpecMap((current) => (current[key] === cached ? current : { ...current, [key]: cached }));
        return;
      }
      setSpecMap((current) => (current[key] ? current : { ...current, [key]: { loading: true, specs: {} } }));
      fetchSpecs(dq).then((data) => {
        const entry = { loading: false, matched: data.matched, specs: data.specs || {}, url: data.url || '' };
        specCache.set(dq, entry);
        if (active) setSpecMap((current) => ({ ...current, [key]: entry }));
      });
    });
    return () => { active = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [keysSignature]);

  const entries = items.map((item) => specMap[itemKey(item)] || { loading: true, specs: {} });
  const anyLoading = entries.some((entry) => entry.loading);
  const specKeys = Array.from(new Set(entries.flatMap((entry) => Object.keys(entry.specs || {}))))
    .sort((a, b) => {
      const ia = SPEC_ORDER.indexOf(a);
      const ib = SPEC_ORDER.indexOf(b);
      return (ia === -1 ? 99 : ia) - (ib === -1 ? 99 : ib);
    });

  const minPrice = Math.min(...items.map((item) => item.price).filter((price) => price > 0));

  return (
    <div className="compare-panel">
      <div className="section-title">
        <h3>모델별 기능 비교 ({items.length})</h3>
        <button className="ghost-btn" onClick={onClear} type="button">비교 초기화</button>
      </div>
      <div className="compare-scroll">
        <table className="compare-table">
          <thead>
            <tr>
              <th aria-hidden="true" />
              {items.map((item) => (
                <th key={itemKey(item)}>
                  <button aria-label="비교에서 제거" className="compare-remove" onClick={() => onRemove(itemKey(item))} type="button">×</button>
                  <img alt="" src={item.image} />
                  <span className="compare-col-query">{item._query}</span>
                  <span>{item.title}</span>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            <tr>
              <th scope="row">가격</th>
              {items.map((item) => (
                <td className={item.price === minPrice ? 'is-best' : ''} key={itemKey(item)}>
                  {item.price.toLocaleString()}원
                </td>
              ))}
            </tr>
            <tr>
              <th scope="row">판매처</th>
              {items.map((item) => <td key={itemKey(item)}>{item.mall}</td>)}
            </tr>
            {specKeys.map((specKey) => (
              <tr key={specKey}>
                <th scope="row">{specKey}</th>
                {items.map((item) => {
                  const entry = specMap[itemKey(item)] || {};
                  return <td key={itemKey(item)}>{entry.loading ? '…' : (entry.specs && entry.specs[specKey]) || '-'}</td>;
                })}
              </tr>
            ))}
            {!anyLoading && specKeys.length === 0 && (
              <tr>
                <th scope="row">스펙</th>
                <td colSpan={items.length}>다나와에서 이 모델의 스펙을 찾지 못했습니다. 상품 페이지에서 확인하세요.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
      <p className="compare-note">
        {anyLoading ? '다나와에서 스펙을 불러오는 중…' : '스펙 출처: 다나와(실시간). 상세는 각 상품 페이지에서 확인하세요.'}
      </p>
    </div>
  );
}

function MallLauncher({ encoded, reason, trimmed }) {
  return (
    <section className="mall-launcher">
      <div className="section-title">
        <h2>‘{trimmed}’ 실제 쇼핑몰 검색</h2>
        <span>{MALLS.length}개 쇼핑몰</span>
      </div>
      <p className="launcher-note">{reason}</p>
      <div className="mall-grid">
        {MALLS.map((mall) => (
          <a
            className="mall-search-card"
            href={mall.build(encoded)}
            key={mall.name}
            rel="noreferrer"
            target="_blank"
          >
            <div>
              <strong>{mall.name}</strong>
              <span className="muted">{mall.note}</span>
            </div>
            <span className="mall-search-cta">실제 검색 결과 보기 <ExternalLink className="icon" /></span>
          </a>
        ))}
      </div>
    </section>
  );
}

function SearchPanel({ query, setHasSearched, setQuery }) {
  const [draftQuery, setDraftQuery] = useState(query);

  const submitSearch = (event) => {
    event.preventDefault();
    setQuery(draftQuery.trim());
    setHasSearched(true);
  };

  return (
    <form className="search-panel" onSubmit={submitSearch}>
      <label className="search-box">
        <Search className="icon" />
        <input
          onChange={(event) => setDraftQuery(event.target.value)}
          placeholder="상품명, 모델명 검색"
          value={draftQuery}
        />
        <button className="btn search-submit" type="submit">검색</button>
      </label>
    </form>
  );
}

function BoardView({ session, setAuthMode }) {
  const [boardPosts, setBoardPosts] = useState(posts);
  const [postTitle, setPostTitle] = useState('');
  const [postContent, setPostContent] = useState('');
  const [boardMessage, setBoardMessage] = useState('');
  const [loadingPosts, setLoadingPosts] = useState(false);
  const [selectedPost, setSelectedPost] = useState(null);
  const [comments, setComments] = useState([]);
  const [commentText, setCommentText] = useState('');

  const loadPosts = async () => {
    if (!isSupabaseConfigured) return;
    setLoadingPosts(true);
    setBoardMessage('');
    try {
      const result = await listPosts({ page: 1, pageSize: 10 });
      if (result.posts?.length) {
        setBoardPosts(result.posts.map((post) => ({
          id: post.id,
          title: post.title,
          author: post.profiles?.display_name || 'user',
          comments: post.comments?.[0]?.count || 0,
          likes: post.likes?.[0]?.count || 0,
          status: post.status,
        })));
      }
    } catch (error) {
      setBoardMessage(`DB 스키마 적용 후 실제 게시글을 불러옵니다. (${error.message})`);
    } finally {
      setLoadingPosts(false);
    }
  };

  useEffect(() => {
    loadPosts();
  }, []);

  useEffect(() => {
    if (!selectedPost) {
      setSelectedPost(boardPosts[0] || null);
    }
  }, [boardPosts, selectedPost]);

  useEffect(() => {
    const loadComments = async () => {
      if (!selectedPost || !isUuid(selectedPost.id)) {
        setComments([
          { id: 'demo-1', content: '가격 변동 알림까지 붙으면 좋겠어요.', author: 'demo' },
          { id: 'demo-2', content: '실사용 후기도 같이 비교되면 편하겠네요.', author: 'reviewer' },
        ]);
        return;
      }
      try {
        const rows = await listComments(selectedPost.id);
        setComments(rows.map((row) => ({
          id: row.id,
          content: row.content,
          author: row.profiles?.display_name || 'user',
        })));
      } catch (error) {
        setBoardMessage(error.message);
      }
    };
    loadComments();
  }, [selectedPost]);

  const submitPost = async (event) => {
    event.preventDefault();
    if (!session?.user?.id) {
      setAuthMode('login');
      return;
    }
    setBoardMessage('');
    try {
      const created = await createPost({
        user_id: session.user.id,
        title: postTitle,
        content: postContent,
        status: 'published',
      });
      setBoardPosts((current) => [{
        id: created.id,
        title: created.title,
        author: session.user.email,
        comments: 0,
        likes: 0,
        status: created.status,
      }, ...current]);
      setPostTitle('');
      setPostContent('');
      setBoardMessage('게시글이 저장되었습니다.');
    } catch (error) {
      setBoardMessage(error.message);
    }
  };

  const submitComment = async (event) => {
    event.preventDefault();
    if (!selectedPost) return;
    if (!session?.user?.id) {
      setAuthMode('login');
      return;
    }
    if (!isUuid(selectedPost.id)) {
      setComments((current) => [{ id: `local-${Date.now()}`, content: commentText, author: session.user.email }, ...current]);
      setCommentText('');
      return;
    }
    try {
      const created = await createComment({
        post_id: selectedPost.id,
        user_id: session.user.id,
        content: commentText,
      });
      setComments((current) => [{ id: created.id, content: created.content, author: session.user.email }, ...current]);
      setCommentText('');
    } catch (error) {
      setBoardMessage(error.message);
    }
  };

  const likePost = async (post) => {
    if (!session?.user?.id) {
      setAuthMode('login');
      return;
    }
    if (isUuid(post.id)) {
      try {
        await toggleLike(post.id, session.user.id, false);
      } catch (error) {
        setBoardMessage(error.message);
        return;
      }
    }
    setBoardPosts((current) => current.map((item) => (
      item.id === post.id ? { ...item, likes: item.likes + 1 } : item
    )));
  };

  return (
    <section className="panel-page">
      <div className="section-title">
        <h1>커뮤니티 게시판</h1>
        <button className="btn" onClick={() => document.getElementById('post-title')?.focus()} type="button"><MessageSquare className="icon" />글쓰기</button>
      </div>
      <form className="post-form" onSubmit={submitPost}>
        <input
          id="post-title"
          onChange={(event) => setPostTitle(event.target.value)}
          placeholder="비교 후기나 질문 제목"
          required
          value={postTitle}
        />
        <textarea
          onChange={(event) => setPostContent(event.target.value)}
          placeholder="내용을 입력하세요. 로그인 후 작성자 본인만 수정/삭제할 수 있습니다."
          required
          value={postContent}
        />
        <button className="ghost-btn" type="submit">게시글 저장</button>
      </form>
      {boardMessage && <span className="auth-message">{boardMessage}</span>}
      <div className="board-list">
        {loadingPosts && <div className="activity-item">게시글을 불러오는 중입니다.</div>}
        {boardPosts.map((post) => (
          <article className={`board-item ${selectedPost?.id === post.id ? 'is-selected' : ''}`} key={post.id}>
            <div>
              <span className="muted">{post.author} · {post.status}</span>
              <h3>{post.title}</h3>
            </div>
            <div className="board-stats">
              <span><MessageSquare className="icon" />{post.comments}</span>
              <button onClick={() => setSelectedPost(post)} type="button">상세</button>
              <button onClick={() => likePost(post)} type="button"><Heart className="icon" />{post.likes}</button>
            </div>
          </article>
        ))}
      </div>
      <div className="comment-box">
        <h2>{selectedPost?.title || '상세/댓글 미리보기'}</h2>
        <form className="post-form" onSubmit={submitComment}>
          <textarea onChange={(event) => setCommentText(event.target.value)} placeholder="댓글을 입력하세요." required value={commentText} />
          <button className="ghost-btn" type="submit">댓글 저장</button>
        </form>
        <div className="comment-list">
          {comments.map((comment) => (
            <div className="comment-item" key={comment.id}>
              <strong>{comment.author}</strong>
              <span>{comment.content}</span>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}

function isUuid(value) {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(String(value));
}

function ProfileView({ session, setAuthMode }) {
  const [profile, setProfile] = useState(null);
  const [displayName, setDisplayName] = useState('');
  const [profileMessage, setProfileMessage] = useState('');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!isSupabaseConfigured || !session?.user?.id) return;
    getMyProfile(session.user.id)
      .then((data) => {
        setProfile(data);
        setDisplayName(data.display_name || '');
      })
      .catch((error) => setProfileMessage(error.message));
  }, [session]);

  const saveProfile = async (event) => {
    event.preventDefault();
    if (!session?.user?.id) return;
    setSaving(true);
    setProfileMessage('');
    try {
      const updated = await updateMyProfile(session.user.id, { display_name: displayName });
      setProfile(updated);
      setProfileMessage('프로필이 저장되었습니다.');
    } catch (error) {
      setProfileMessage(error.message);
    } finally {
      setSaving(false);
    }
  };

  if (!session) {
    return (
      <section className="profile-grid">
        <div className="profile-card">
          <CircleUserRound className="profile-icon" />
          <h1>로그인이 필요합니다</h1>
          <p>마이페이지는 인증된 사용자 본인 데이터만 표시합니다.</p>
          <button className="btn" onClick={() => setAuthMode('login')} type="button"><LockKeyhole className="icon" />로그인</button>
        </div>
        <div className="panel-page">
          <div className="section-title">
            <h2>활동 내역</h2>
            <Bell className="icon" />
          </div>
          <div className="activity-item">로그인 후 실제 활동 내역을 불러옵니다.</div>
        </div>
      </section>
    );
  }

  return (
    <section className="profile-grid">
      <div className="profile-card">
        <CircleUserRound className="profile-icon" />
        <h1>{profile?.display_name || session.user.email}</h1>
        <p>{session.user.email}</p>
        <form className="profile-form" onSubmit={saveProfile}>
          <label>
            표시 이름
            <input onChange={(event) => setDisplayName(event.target.value)} value={displayName} />
          </label>
          <button className="btn" disabled={saving} type="submit"><Settings className="icon" />{saving ? '저장 중' : '프로필 저장'}</button>
        </form>
        {profileMessage && <span className="auth-message">{profileMessage}</span>}
      </div>
      <div className="panel-page">
        <div className="section-title">
          <h2>활동 내역</h2>
          <Bell className="icon" />
        </div>
        {activity.map((item) => <div className="activity-item" key={item}>{item}</div>)}
      </div>
    </section>
  );
}

function AdminView({ session, setAuthMode }) {
  const [stats, setStats] = useState({ searches: 2380, users: 128, pending: 7 });
  const [adminPosts, setAdminPosts] = useState(posts);
  const [adminMessage, setAdminMessage] = useState('');

  useEffect(() => {
    const loadAdmin = async () => {
      if (!isSupabaseConfigured || !session?.user?.id) return;
      try {
        const result = await getAdminStats();
        if (result) {
          setStats({
            searches: 2380,
            users: result.users || 0,
            pending: result.posts || 0,
          });
        } else {
          setAdminMessage('관리자 권한이 있는 계정만 실제 통계를 볼 수 있습니다.');
        }
      } catch (error) {
        setAdminMessage(error.message);
      }
    };
    loadAdmin();
  }, [session]);

  const changeStatus = async (post) => {
    if (!session?.user?.id) {
      setAuthMode('login');
      return;
    }
    const nextStatus = post.status === 'published' || post.status === '공개' ? 'hidden' : 'published';
    if (isUuid(post.id)) {
      try {
        await updatePostStatus(post.id, nextStatus);
      } catch (error) {
        setAdminMessage(error.message);
        return;
      }
    }
    setAdminPosts((current) => current.map((item) => (
      item.id === post.id ? { ...item, status: nextStatus } : item
    )));
  };

  return (
    <section className="admin-grid">
      <div className="stat-card"><span>오늘 검색</span><strong>{stats.searches.toLocaleString()}</strong></div>
      <div className="stat-card"><span>가입 사용자</span><strong>{stats.users.toLocaleString()}</strong></div>
      <div className="stat-card"><span>관리 게시글</span><strong>{stats.pending.toLocaleString()}</strong></div>
      <div className="chart-panel">
        <div className="section-title">
          <h2>운영 통계</h2>
          <BarChart3 className="icon" />
        </div>
        <ResponsiveContainer height={260} width="100%">
          <BarChart data={chartData}>
            <CartesianGrid stroke="#E9E9E7" vertical={false} />
            <XAxis dataKey="name" tickLine={false} axisLine={false} />
            <YAxis tickLine={false} axisLine={false} />
            <Tooltip />
            <Bar dataKey="searches" fill="#2383E2" radius={[8, 8, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </div>
      <div className="panel-page">
        <div className="section-title">
          <h2>목록 관리</h2>
          <button className="ghost-btn" type="button">상태 변경</button>
        </div>
        {adminMessage && <span className="auth-message">{adminMessage}</span>}
        {adminPosts.map((post) => (
          <div className="admin-row" key={post.id}>
            <span>{post.title}</span>
            <button className="ghost-btn" onClick={() => changeStatus(post)} type="button">{post.status}</button>
          </div>
        ))}
      </div>
    </section>
  );
}

export default App;
