import react from '@vitejs/plugin-react';
import { defineConfig, loadEnv } from 'vite';
import { searchNaverShopping } from './api/_naver.js';
import { bestSpecFor } from './api/_danawa.js';

// 개발 서버에서도 /api/* 가 동작하도록 미들웨어로 붙인다.
// (배포에서는 Vercel 서버리스 함수 api/*.js 가 같은 경로를 처리한다.)
function devApi(env) {
  const clientId = env.NAVER_CLIENT_ID;
  const clientSecret = env.NAVER_CLIENT_SECRET;

  const json = (res, body, status = 200) => {
    res.statusCode = status;
    res.setHeader('Content-Type', 'application/json');
    res.end(JSON.stringify(body));
  };

  return {
    name: 'dev-api',
    configureServer(server) {
      server.middlewares.use(async (req, res, next) => {
        if (!req.url) return next();
        const url = new URL(req.url, 'http://localhost');
        const query = (url.searchParams.get('q') || '').trim();

        if (url.pathname === '/api/search') {
          if (!clientId || !clientSecret) return json(res, { configured: false, items: [] });
          if (!query) return json(res, { configured: true, items: [] });
          try {
            const items = await searchNaverShopping(query, { clientId, clientSecret });
            return json(res, { configured: true, items });
          } catch (error) {
            return json(res, { configured: true, items: [], error: error.message }, 502);
          }
        }

        if (url.pathname === '/api/specs') {
          if (!query) return json(res, { matched: false, specs: {} });
          const ref = (url.searchParams.get('ref') || '').trim();
          try {
            return json(res, await bestSpecFor(query, ref));
          } catch (error) {
            return json(res, { matched: false, specs: {}, error: error.message });
          }
        }

        return next();
      });
    },
  };
}

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');

  return {
    plugins: [react(), devApi(env)],
    build: {
      rollupOptions: {
        output: {
          manualChunks: {
            supabase: ['@supabase/supabase-js'],
            charts: ['recharts'],
            icons: ['lucide-react'],
          },
        },
      },
    },
  };
});
