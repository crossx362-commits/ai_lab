# 파일/폴더 정리 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 루트에 산재한 파일들을 기능별 폴더로 이동하고, 중복 파일과 임시 폴더를 제거하여 프로젝트 구조를 정리한다.

**Architecture:** 루트 레벨은 README.md, SKILL.md, .env* 등 최소한의 파일만 유지. Python 스크립트는 `projects/ai-team/scripts/` 하위에 기능별 서브폴더로 분류. 세팅 문서는 `docs/setup/`으로 통합.

**Tech Stack:** bash, git mv

---

### Task 1: 임시 작업 폴더 및 중복 파일 삭제

**Files:**
- Delete: `오늘업무_2026-06-11/` (전체 폴더)
- Delete: `naver_finance_scraper.py` (루트, `connect-ai-packs/_shared/`에 동일 파일 존재)

- [ ] **Step 1: 삭제할 파일 확인**

```bash
ls /Users/junholee/ai_lab/오늘업무_2026-06-11/
diff /Users/junholee/ai_lab/naver_finance_scraper.py /Users/junholee/ai_lab/connect-ai-packs/_shared/naver_finance_scraper.py
```

Expected: 두 파일이 동일하거나 `_shared/` 버전이 최신

- [ ] **Step 2: git rm으로 제거**

```bash
cd /Users/junholee/ai_lab
git rm -r "오늘업무_2026-06-11/"
git rm naver_finance_scraper.py
```

- [ ] **Step 3: 커밋**

```bash
git commit -m "chore: 임시 작업 폴더 및 중복 파일 제거"
```

---

### Task 2: docs/setup/ 생성 및 세팅 문서 이동

**Files:**
- Create dir: `docs/setup/`
- Move from root: `ENCRYPTED_SECRETS_README.md`, `ENCRYPTION_SETUP_COMPLETE.md`, `ENV_README.md`, `ENV_SECURITY_README.md`, `NOTION_SETUP.md`, `QUICK_START_NOTION.md`, `DAILY_AUTOMATION_SETUP.md`, `AI_TEAM_AUTOMATION_README.md`

- [ ] **Step 1: docs/setup 폴더 생성 및 파일 이동**

```bash
mkdir -p /Users/junholee/ai_lab/docs/setup

git mv ENCRYPTED_SECRETS_README.md docs/setup/
git mv ENCRYPTION_SETUP_COMPLETE.md docs/setup/
git mv ENV_README.md docs/setup/
git mv ENV_SECURITY_README.md docs/setup/
git mv NOTION_SETUP.md docs/setup/
git mv QUICK_START_NOTION.md docs/setup/
git mv DAILY_AUTOMATION_SETUP.md docs/setup/
git mv AI_TEAM_AUTOMATION_README.md docs/setup/
```

- [ ] **Step 2: 이동 확인**

```bash
ls /Users/junholee/ai_lab/docs/setup/
```

Expected: 8개 .md 파일 목록 출력

- [ ] **Step 3: 커밋**

```bash
git commit -m "chore: 세팅 문서 docs/setup/으로 이동"
```

---

### Task 3: 기타 루트 마크다운 파일 이동

**Files:**
- Move: `channel_registration_status.md` → `reports/`
- Move: `marketing_strategy_youtube_shorts.md` → `reports/research/`
- Move: `AGENT_PIPELINE_REVIEW.md` → `docs/`

- [ ] **Step 1: 파일 이동**

```bash
cd /Users/junholee/ai_lab
git mv channel_registration_status.md reports/
git mv marketing_strategy_youtube_shorts.md reports/research/
git mv AGENT_PIPELINE_REVIEW.md docs/
```

- [ ] **Step 2: 커밋**

```bash
git commit -m "chore: 루트 마크다운 파일 각 폴더로 이동"
```

---

### Task 4: YouTube 관련 스크립트 이동

**Files:**
- Create dir: `projects/ai-team/scripts/youtube/`
- Move: `yt_auth.py`, `reauth_youtube.py`, `update_all_youtube_videos.py`, `make_videos_public.py`

- [ ] **Step 1: youtube 서브폴더 생성 및 파일 이동**

```bash
cd /Users/junholee/ai_lab
mkdir -p projects/ai-team/scripts/youtube

git mv yt_auth.py projects/ai-team/scripts/youtube/
git mv reauth_youtube.py projects/ai-team/scripts/youtube/
git mv update_all_youtube_videos.py projects/ai-team/scripts/youtube/
git mv make_videos_public.py projects/ai-team/scripts/youtube/
```

- [ ] **Step 2: 이동 확인**

```bash
ls projects/ai-team/scripts/youtube/
```

Expected: 4개 파일 목록

- [ ] **Step 3: 커밋**

```bash
git commit -m "chore: YouTube 스크립트 scripts/youtube/로 이동"
```

---

### Task 5: 보안 관련 스크립트 이동

**Files:**
- Create dir: `projects/ai-team/scripts/security/`
- Move: `encrypt_all_secrets.py`, `decrypt_all_secrets.py`

- [ ] **Step 1: security 서브폴더 생성 및 파일 이동**

```bash
cd /Users/junholee/ai_lab
mkdir -p projects/ai-team/scripts/security

git mv encrypt_all_secrets.py projects/ai-team/scripts/security/
git mv decrypt_all_secrets.py projects/ai-team/scripts/security/
```

- [ ] **Step 2: 커밋**

```bash
git commit -m "chore: 보안 스크립트 scripts/security/로 이동"
```

---

### Task 6: 에이전트 관련 스크립트 이동

**Files:**
- Create dir: `projects/ai-team/scripts/agents/`
- Move: `check_agent_env_connections.py`, `test_agent_api_connections.py`, `fix_agent_paths.py`

- [ ] **Step 1: agents 서브폴더 생성 및 파일 이동**

```bash
cd /Users/junholee/ai_lab
mkdir -p projects/ai-team/scripts/agents

git mv check_agent_env_connections.py projects/ai-team/scripts/agents/
git mv test_agent_api_connections.py projects/ai-team/scripts/agents/
git mv fix_agent_paths.py projects/ai-team/scripts/agents/
```

- [ ] **Step 2: 커밋**

```bash
git commit -m "chore: 에이전트 스크립트 scripts/agents/로 이동"
```

---

### Task 7: 기타 스크립트 이동

**Files:**
- Move to `projects/ai-team/scripts/`: `scan_env_usage.py`, `start_daily_automation.py`, `check_meta_video_api.py`

- [ ] **Step 1: 파일 이동**

```bash
cd /Users/junholee/ai_lab
git mv scan_env_usage.py projects/ai-team/scripts/
git mv start_daily_automation.py projects/ai-team/scripts/
git mv check_meta_video_api.py projects/ai-team/scripts/
```

- [ ] **Step 2: 커밋**

```bash
git commit -m "chore: 기타 스크립트 projects/ai-team/scripts/로 이동"
```

---

### Task 8: 최종 확인

- [ ] **Step 1: 루트 파일 목록 확인 — 잔여 파일이 없는지 체크**

```bash
ls /Users/junholee/ai_lab/*.md /Users/junholee/ai_lab/*.py 2>/dev/null
```

Expected: `README.md`, `SKILL.md` 만 `.md`로 남아있어야 함. `.py`는 없어야 함.

- [ ] **Step 2: git status로 누락된 변경 없는지 확인**

```bash
git status
```

Expected: `nothing to commit, working tree clean`
