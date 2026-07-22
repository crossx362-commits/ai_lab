# Petnna

Petnna is the pet-care web/PWA project in this monorepo. It is deployed on
Vercel and uses plain HTML, JavaScript, Tailwind CSS, Leaflet, Chart.js,
Supabase, and Gemini-backed health analysis.

## Project Layout

```text
projects/petnna/
├── index.html          # Main app entry
├── sw.js               # Service worker
├── manifest.json       # PWA manifest
├── api/                # Vercel/serverless API helpers
├── css/                # Stylesheets and Tailwind output
├── docs/               # Product and setup documentation
├── images/             # App image assets
├── js/                 # App controllers, state, views, and feature modules
├── js/templates/       # HTML template fragments
├── migrations/         # Supabase/database migrations
└── tests/              # App tests and checks
```

## Main Features

- AI pet health analysis
- GPS walk tracking
- Pet personality analysis
- Digital diary and PDF export
- Social features backed by Supabase
- Pet profile and care records

## Local Preview

Serve the app directory with a static server:

```bash
python3 -m http.server 8901 --directory projects/petnna
```

or:

```bash
cd projects/petnna
npx serve .
```

## Deployment

The app is configured for Vercel. Project-specific plaintext `.env` files should
not be committed. Use the root `ai_lab/.env` for local secrets and configure
deployment secrets in Vercel.

Useful files:

- `vercel.json`
- `inject-env.js`
- `SETUP_SUPABASE.md`
- `supabase_schema.sql`
- `PRIVACY_POLICY.md`
- `TERMS_OF_SERVICE.md`

## Operating Notes

- Do not merge frontend modules without browser testing.
- 미오 (Design) runs a weekly screenshot-based UX review
  (`projects/ai-team/skills/미오_디자인/tools/petnna_design_review.py`) that feeds the
  shared backlog — don't rely on it as a per-change check.
- Generated output belongs under root `output/` or `reports/`, not inside the
  app source tree unless it is an intentional checked-in asset.
