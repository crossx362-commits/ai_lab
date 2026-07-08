# ai_lab Skill Overview

Root-level pointer to the skill system. Each agent's actual skills live in
`projects/ai-team/skills/<agent>/SKILL.md`; cross-agent guidance lives in
`projects/ai-team/skills/공용스킬/`.

## Current Agents

See [AGENTS.md](AGENTS.md) for the up-to-date roster and tool paths.

## Petnna (product)

Petnna-facing agents (봄이 QA, 수리 dev, 미오 design, 나무 planning, 백호 backend,
테오 test) each keep their own `SKILL.md` under `projects/ai-team/skills/<agent>/`.

## Conventions

- Skill docs describe behavior and learned knowledge for one agent — do not
  merge across agents unless the roster changes.
- Run `python projects/ai-team/harness/check_all.py` after adding or removing
  an agent so the skill-registry and classification checks stay accurate.
