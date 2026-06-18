# 에이전트 교차검증 시스템 구현 플랜

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** CEO(예원)가 고위험 태스크를 감지하면 관련 전문 에이전트 의견을 병렬 수집해 최선의 종합 결론을 도출하는 교차검증 시스템을 구현한다.

**Architecture:** `_detectHighRisk()` 순수 함수가 프롬프트에서 도메인을 감지 → `_runCouncil()` 메서드가 관련 에이전트를 `Promise.all()` 병렬 호출 (30s 타임아웃) → CEO 종합 보고서 입력에 전문가 의견 블록 추가. `[COUNCIL_NEEDED]` 태그는 specialist 출력 후 파싱해 같은 흐름을 트리거.

**Tech Stack:** TypeScript, VS Code Extension API, 기존 `_callAgentLLM()` / `buildSpecialistPrompt()` 재사용

---

## 파일 구조

| 파일 | 변경 유형 | 내용 |
|------|----------|------|
| `projects/ai-team/src/agents.ts` | Modify | `AgentDef`에 `councilDomains?: string[]` 필드 추가 |
| `projects/ai-team/src/extension.ts` | Modify | `COUNCIL_MAP` 상수, `_detectHighRisk()`, `_runCouncil()` 추가; CEO 종합 섹션·specialist 루프·`buildSpecialistPrompt()` 수정 |

---

## Task 1: `AgentDef`에 `councilDomains` 필드 추가

**Files:**
- Modify: `projects/ai-team/src/agents.ts:12-29`

- [ ] **Step 1: `AgentDef` 인터페이스에 필드 추가**

[agents.ts:26-28](projects/ai-team/src/agents.ts#L26-L28) 의 `profileImage?` 라인 바로 뒤에 삽입:

```typescript
  /** Optional council membership — domains this agent is called for in cross-validation.
   *  Values must match keys in COUNCIL_MAP (extension.ts). */
  councilDomains?: string[];
```

- [ ] **Step 2: 관련 에이전트에 `councilDomains` 값 채우기**

`arin` 항목 (line ~50)에 추가:
```typescript
    councilDomains: ['content_publish'],
```

`developer` 항목 (line ~65)에 추가:
```typescript
    councilDomains: ['code_deploy'],
```

`business` 항목 (line ~78)에 추가:
```typescript
    councilDomains: ['business'],
```

`inspector` 항목 (line ~127)에 추가:
```typescript
    councilDomains: ['content_publish', 'video_quality'],
```

`gyeongsu` 항목 (line ~138)에 추가:
```typescript
    councilDomains: ['code_deploy'],
```

`editor` 항목 (line ~97)에 추가:
```typescript
    councilDomains: ['video_quality'],
```

`kevin` 항목 (line ~159)에 추가:
```typescript
    councilDomains: ['code_deploy'],
```

`royul` 항목 (line ~171)에 추가:
```typescript
    councilDomains: ['business'],
```

`researcher` 항목 (line ~118)에 추가:
```typescript
    councilDomains: ['business'],
```

`writer` 항목 (line ~105)에 추가:
```typescript
    councilDomains: ['content_publish'],
```

- [ ] **Step 3: TypeScript 컴파일 확인**

```bash
cd projects/ai-team && npm run compile 2>&1 | tail -5
```
Expected: 오류 없이 종료 (exit 0)

- [ ] **Step 4: 커밋**

```bash
cd projects/ai-team
git add src/agents.ts
git commit -m "feat(council): AgentDef에 councilDomains 필드 추가"
```

---

## Task 2: `COUNCIL_MAP` 상수 및 `_detectHighRisk()` 함수 추가

**Files:**
- Modify: `projects/ai-team/src/extension.ts` — `AGENT_ORDER` / `SPECIALIST_IDS` 정의 직후 (~line 181)

- [ ] **Step 1: `COUNCIL_MAP` 상수를 `src/extension.ts` 내 `AGENT_ORDER` 라인 바로 뒤에 삽입**

[extension.ts:180](projects/ai-team/src/extension.ts#L180) 다음 줄(빈 줄 두 개 삽입 후):

```typescript
/* 교차검증 도메인 → 소집 에이전트 매핑. agents.ts의 councilDomains 값과 키가 일치해야 함. */
const COUNCIL_MAP: Record<string, string[]> = {
    content_publish: ['arin', 'inspector', 'writer'],
    code_deploy:     ['developer', 'gyeongsu', 'kevin'],
    business:        ['business', 'royul', 'researcher'],
    video_quality:   ['inspector', 'editor'],
};
```

- [ ] **Step 2: `_detectHighRisk()` 순수 함수를 `COUNCIL_MAP` 바로 뒤에 삽입**

```typescript
/**
 * 프롬프트에서 고위험 도메인을 감지해 소집할 에이전트 ID 목록을 반환.
 * 해당 없으면 null.
 */
function _detectHighRisk(prompt: string): { domain: string; agentIds: string[] } | null {
    const p = prompt;
    if (/발행|업로드|포스팅|스케줄|게시|publish|upload/i.test(p))
        return { domain: 'content_publish', agentIds: COUNCIL_MAP.content_publish };
    if (/배포|deploy|api\s*연동|커밋|push|release/i.test(p))
        return { domain: 'code_deploy', agentIds: COUNCIL_MAP.code_deploy };
    if (/계약|수익화|가격|요금|협찬|파트너십|비즈니스\s*결정|투자/i.test(p))
        return { domain: 'business', agentIds: COUNCIL_MAP.business };
    if (/영상\s*품질|품질\s*검수|bgm\s*검수|audio\s*검수/i.test(p))
        return { domain: 'video_quality', agentIds: COUNCIL_MAP.video_quality };
    return null;
}
```

- [ ] **Step 3: 컴파일 확인**

```bash
cd projects/ai-team && npm run compile 2>&1 | tail -5
```
Expected: 오류 없이 종료

- [ ] **Step 4: 커밋**

```bash
cd projects/ai-team
git add src/extension.ts
git commit -m "feat(council): COUNCIL_MAP 상수 및 _detectHighRisk() 추가"
```

---

## Task 3: `_runCouncil()` 메서드 추가

**Files:**
- Modify: `projects/ai-team/src/extension.ts` — `_callAgentLLM()` 정의 바로 앞(~line 20770)

- [ ] **Step 1: `_runCouncil()` 메서드를 `private async _callAgentLLM(` 정의 바로 앞에 삽입**

```typescript
    /**
     * 고위험 판단 시 관련 에이전트를 병렬 호출해 전문가 의견을 수집.
     * 타임아웃(30s) 또는 LLM 실패한 에이전트는 조용히 제외 — 메인 흐름 블록 없음.
     */
    private async _runCouncil(
        task: string,
        agentIds: string[],
        modelName: string
    ): Promise<Array<{ agentId: string; opinion: string }>> {
        const COUNCIL_TIMEOUT_MS = 30_000;
        const results = await Promise.all(
            agentIds.map(async (id): Promise<{ agentId: string; opinion: string }> => {
                if (!AGENTS[id] || !isAgentActive(id)) return { agentId: id, opinion: '' };
                const sys = buildSpecialistPrompt(id);
                const usr = `[교차검증 요청]\n${task}\n\n당신의 전문 영역에서 이 결정에 대한 핵심 의견을 2-3문장으로 간결하게 제시하라. 찬성/반대/보완 모두 가능. 불확실하면 명시할 것.`;
                try {
                    const opinion = await Promise.race<string>([
                        this._callAgentLLM(sys, usr, modelName, id, false),
                        new Promise<string>((_, reject) =>
                            setTimeout(() => reject(new Error('council-timeout')), COUNCIL_TIMEOUT_MS)
                        ),
                    ]);
                    return { agentId: id, opinion: String(opinion).trim().slice(0, 800) };
                } catch {
                    return { agentId: id, opinion: '' };
                }
            })
        );
        return results.filter(r => r.opinion.length > 20);
    }
```

- [ ] **Step 2: 컴파일 확인**

```bash
cd projects/ai-team && npm run compile 2>&1 | tail -5
```
Expected: 오류 없이 종료

- [ ] **Step 3: 커밋**

```bash
cd projects/ai-team
git add src/extension.ts
git commit -m "feat(council): _runCouncil() 병렬 호출 메서드 추가"
```

---

## Task 4: CEO 종합 보고서에 교차검증 의견 삽입

**Files:**
- Modify: `projects/ai-team/src/extension.ts:20578-20638`

이 Task는 `_handleCorporatePrompt` 내 "5) CEO 종합 보고서" 섹션(line ~20557)을 수정한다.

- [ ] **Step 1: `plan.tasks.length <= 1` 분기 바로 아래 `else {` 블록의 시작 부분 찾기**

대상 위치: `post({ type: 'agentStart', agent: 'ceo', task: '종합 보고서 작성' });` 바로 앞 (line ~20579).

다음 `old` → `new` 패치를 적용:

**변경 전:**
```typescript
            } else {
                post({ type: 'agentStart', agent: 'ceo', task: '종합 보고서 작성' });
                _updateActiveDispatchStep(prompt, 'CEO 종합 보고서 작성 중');
                /* v2.89.46 — 산출물 없는 에이전트는 reportInput에서 제외 (CEO가 placeholder
                   출력 위험 제거). 명시적으로 "X명 중 Y명만 답변 도착" 메타 정보 포함. */
                const validTasks = plan.tasks.filter(t => nonEmptyOutputs.some(o => o.agent === t.agent));
                const reportInput = `[원 명령]\n${prompt}\n\n[브리프]\n${plan.brief}\n\n` +
                    `[응답 도착: ${validTasks.length}/${plan.tasks.length}명]\n\n` +
                    `[유효한 에이전트 산출물]\n${validTasks.map(t => `\n## ${AGENTS[t.agent]?.emoji} ${AGENTS[t.agent]?.name}\n${(outputs[t.agent] || '').slice(0, 2000)}`).join('\n')}\n\n` +
                    `규칙: 위 산출물 안의 실제 내용·숫자만 인용해 보고서 작성. "산출물을 기다리고 있습니다", "데이터가 제공되면" 같은 placeholder 표현 절대 금지 — 산출물은 이미 위에 있음.`;
```

**변경 후:**
```typescript
            } else {
                /* 교차검증 — 고위험 태스크이면 메인 specialist 외 전문가 병렬 의견 수집 */
                let councilBlock = '';
                const _highRisk = _detectHighRisk(prompt);
                if (_highRisk) {
                    const councilIds = _highRisk.agentIds.filter(id => !plan.tasks.some(t => t.agent === id) && isAgentActive(id));
                    if (councilIds.length > 0) {
                        const names = councilIds.map(id => `${AGENTS[id]?.emoji || ''} ${AGENTS[id]?.name || id}`).join(', ');
                        post({ type: 'response', value: `🗣️ 교차검증 중 — ${names} 의견 수집` });
                        try {
                            const opinions = await this._runCouncil(prompt, councilIds, modelName);
                            if (opinions.length > 0) {
                                councilBlock = `\n\n[교차검증 전문가 의견 (${_highRisk.domain})]\n` +
                                    opinions.map(o => `${AGENTS[o.agentId]?.emoji || ''} **${AGENTS[o.agentId]?.name || o.agentId}**: ${o.opinion}`).join('\n\n');
                            }
                        } catch { /* 교차검증 실패해도 메인 보고서는 계속 */ }
                    }
                }

                post({ type: 'agentStart', agent: 'ceo', task: '종합 보고서 작성' });
                _updateActiveDispatchStep(prompt, 'CEO 종합 보고서 작성 중');
                /* v2.89.46 — 산출물 없는 에이전트는 reportInput에서 제외 (CEO가 placeholder
                   출력 위험 제거). 명시적으로 "X명 중 Y명만 답변 도착" 메타 정보 포함. */
                const validTasks = plan.tasks.filter(t => nonEmptyOutputs.some(o => o.agent === t.agent));
                const reportInput = `[원 명령]\n${prompt}\n\n[브리프]\n${plan.brief}\n\n` +
                    `[응답 도착: ${validTasks.length}/${plan.tasks.length}명]\n\n` +
                    `[유효한 에이전트 산출물]\n${validTasks.map(t => `\n## ${AGENTS[t.agent]?.emoji} ${AGENTS[t.agent]?.name}\n${(outputs[t.agent] || '').slice(0, 2000)}`).join('\n')}` +
                    `${councilBlock}\n\n` +
                    `규칙: 위 산출물 안의 실제 내용·숫자만 인용해 보고서 작성. "산출물을 기다리고 있습니다", "데이터가 제공되면" 같은 placeholder 표현 절대 금지 — 산출물은 이미 위에 있음.` +
                    (councilBlock ? '\n\n[교차검증 의견을 종합 결론에 반드시 반영할 것]' : '');
```

- [ ] **Step 2: CEO 최종 출력 앞에 `📋 종합 결론:` 접두사 추가**

`ceoNarrative` 가 있을 때 출력하는 line(~20634):
```typescript
                    finalReport = `${breakdownLines.join('\n')}\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n## 👔 CEO 종합\n\n${ceoNarrative.trim()}`;
```
→
```typescript
                    const councilPrefix = councilBlock ? '📋 종합 결론 (교차검증 반영):\n\n' : '';
                    finalReport = `${breakdownLines.join('\n')}\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n## 👔 CEO 종합\n\n${councilPrefix}${ceoNarrative.trim()}`;
```

- [ ] **Step 3: 컴파일 확인**

```bash
cd projects/ai-team && npm run compile 2>&1 | tail -5
```
Expected: 오류 없이 종료

- [ ] **Step 4: 커밋**

```bash
cd projects/ai-team
git add src/extension.ts
git commit -m "feat(council): CEO 종합 보고서에 교차검증 의견 삽입"
```

---

## Task 5: `[COUNCIL_NEEDED]` 태그 자기요청 감지

**Files:**
- Modify: `projects/ai-team/src/extension.ts` — specialist 루프의 `out` 저장 직후 (~line 20175)

- [ ] **Step 1: specialist 루프에서 `out`을 `outputs[t.agent]`에 저장하는 줄 찾기**

`outputs[t.agent] = out;` 라인을 찾아 바로 뒤에 삽입:

```typescript
                /* 교차검증 자기요청 — [COUNCIL_NEEDED: 이유] 태그 감지 */
                const councilTagMatch = out.match(/^\[COUNCIL_NEEDED:\s*([^\]]+)\]/);
                if (councilTagMatch) {
                    const reason = councilTagMatch[1].trim();
                    post({ type: 'response', value: `🗣️ ${a.emoji} ${a.name}이(가) 교차검증 요청: ${reason}` });
                    /* 현재 에이전트를 제외한 고위험 연관 에이전트 소집 (또는 전 도메인 fallback) */
                    const selfDomains = AGENTS[t.agent]?.councilDomains || [];
                    const fallbackIds = selfDomains.length > 0
                        ? selfDomains.flatMap(d => COUNCIL_MAP[d] || []).filter(id => id !== t.agent && isAgentActive(id))
                        : SPECIALIST_IDS.filter(id => id !== t.agent && id !== 'ceo' && isAgentActive(id)).slice(0, 3);
                    const uniqueFallbackIds = [...new Set(fallbackIds)];
                    if (uniqueFallbackIds.length > 0) {
                        try {
                            const selfOpinions = await this._runCouncil(
                                `[${a.name} 자기요청 이유: ${reason}]\n${prompt}`,
                                uniqueFallbackIds,
                                modelName
                            );
                            if (selfOpinions.length > 0) {
                                const extra = selfOpinions.map(o => `${AGENTS[o.agentId]?.name}: ${o.opinion}`).join('\n');
                                outputs[t.agent] = `${out}\n\n[교차검증 추가 의견]\n${extra}`;
                            }
                        } catch { /* 실패 무시 */ }
                    }
                }
```

- [ ] **Step 2: `outputs[t.agent] = out;` 라인 위치 확인**

```bash
grep -n "outputs\[t\.agent\] = out" projects/ai-team/src/extension.ts | head -5
```
Expected: 한 줄 출력 (정확한 라인 번호 확인)

- [ ] **Step 3: 컴파일 확인**

```bash
cd projects/ai-team && npm run compile 2>&1 | tail -5
```
Expected: 오류 없이 종료

- [ ] **Step 4: 커밋**

```bash
cd projects/ai-team
git add src/extension.ts
git commit -m "feat(council): [COUNCIL_NEEDED] 자기요청 태그 감지 및 처리"
```

---

## Task 6: `buildSpecialistPrompt()`에 교차검증 자기요청 규칙 추가

**Files:**
- Modify: `projects/ai-team/src/extension.ts:7640-7646`

- [ ] **Step 1: `buildSpecialistPrompt()` 내 `[필수 자가평가]` 블록 찾기**

[extension.ts:7640](projects/ai-team/src/extension.ts#L7640) 의 `[필수 자가평가]` 블록 바로 앞 줄(빈 줄 후)에 삽입:

**변경 전:**
```typescript
[필수 자가평가 — 마지막 두 줄 강제]
```

**변경 후:**
```typescript
[교차검증 자기요청]
판단이 불확실하거나 다른 전문 영역이 필요하면 응답 맨 앞 줄에 \`[COUNCIL_NEEDED: <이유 한 줄>]\` 태그를 붙여라. 이유는 간결하게, 왜 다른 전문가가 필요한지 한 문장.

[필수 자가평가 — 마지막 두 줄 강제]
```

- [ ] **Step 2: 컴파일 확인**

```bash
cd projects/ai-team && npm run compile 2>&1 | tail -5
```
Expected: 오류 없이 종료

- [ ] **Step 3: 커밋**

```bash
cd projects/ai-team
git add src/extension.ts
git commit -m "feat(council): buildSpecialistPrompt에 COUNCIL_NEEDED 자기요청 규칙 추가"
```

---

## Task 7: 수동 검증

VS Code 빌드된 extension은 별도 test framework이 없으므로 수동 시나리오 검증.

- [ ] **Step 1: VS Code에서 Extension 재로드**

```bash
cd projects/ai-team && npm run compile
```
그 다음 VS Code: `Cmd+Shift+P` → `Developer: Reload Window`

- [ ] **Step 2: 고위험 태스크 시나리오 — 콘텐츠 발행**

AI Team 사이드바 채팅창에 입력:
```
인스타그램 릴스 3개 발행 일정 잡아줘
```
Expected:
- `🗣️ 교차검증 중 — 🌸 아린, 🔎 가희, ✍️ Writer 의견 수집` 메시지 출력
- CEO 종합 결론에 `📋 종합 결론 (교차검증 반영):` 접두사 출력

- [ ] **Step 3: 저위험 태스크 시나리오 — 단순 질문**

```
오늘 날씨 어때?
```
Expected:
- 교차검증 메시지 없음
- 기존 단독 응답 흐름 유지

- [ ] **Step 4: 고위험 태스크 시나리오 — 코드 배포**

```
코다리야 Vercel 배포해줘
```
Expected:
- `🗣️ 교차검증 중 — 💻 코다리, 👮 경수, 🤖 케빈 의견 수집` (이미 plan에 코다리가 있으면 코다리 제외된 나머지)
- 또는 코다리가 plan tasks 안에 있으면 경수·케빈만 소집됨 (필터 로직 확인)

- [ ] **Step 5: `[COUNCIL_NEEDED]` 시나리오**

```
루나야 이 BGM이 저작권 위반인지 확인해줘
```
Expected:
- 루나 응답 앞에 `[COUNCIL_NEEDED: 저작권 판단은 법률 전문가 의견 필요]` 가 나오면
- `🗣️ 루나이(가) 교차검증 요청: ...` 메시지 + 가희·로율 의견 수집

- [ ] **Step 6: 최종 커밋 (버전 범프)**

[extension.ts의 `version` 주석](projects/ai-team/src/extension.ts#L1) 업데이트:

[package.json:8](projects/ai-team/package.json#L8) 버전을 `2.89.157` → `2.89.158` 로 변경 후:

```bash
cd projects/ai-team
git add src/extension.ts package.json
git commit -m "chore: v2.89.158 — 에이전트 교차검증 시스템"
```

---

## 성공 기준 체크리스트

- [ ] 고위험 키워드 프롬프트 → `🗣️ 교차검증 중` 메시지 출력
- [ ] CEO 종합 결론에 `📋 종합 결론 (교차검증 반영):` 접두사 포함
- [ ] 저위험 태스크 → 교차검증 없이 기존 흐름 유지
- [ ] `[COUNCIL_NEEDED]` 태그 → 자동 교차검증 소집
- [ ] 교차검증 타임아웃(30s) 또는 LLM 실패 시 메인 흐름 정상 계속
- [ ] `npm run compile` 에러 없음
