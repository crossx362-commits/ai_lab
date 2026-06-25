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

From the repository root, use the ai-team web preview helper:

```bash
python projects/ai-team/skills/코다리_개발자/tools/web_preview.py
```

Or serve the app directory with a static server:

```bash
cd projects/petnna
npx serve .
```

## Deployment

The app is configured for Vercel. Project-specific plaintext `.env` files should
not be committed. Use the root `D:\ai_lab\.env` for local secrets and configure
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
- Run the UI review helper after visual changes:

```bash
python projects/ai-team/skills/티모_디자이너/tools/petnna_reviewer.py
```

- Generated output belongs under root `output/` or `reports/`, not inside the
  app source tree unless it is an intentional checked-in asset.
