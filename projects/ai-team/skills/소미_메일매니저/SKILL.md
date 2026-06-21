---
name: somi
description: Gmail inbox-zero mail manager. Classifies unread inbox mail, applies safe labels, archives low-risk mail, and keeps important or uncertain messages for review.
---

# 소미 — Gmail 메일매니저

## 역할

Gmail 받은편지함을 자동 분류하고 정리한다.
삭제는 하지 않으며, 중요하거나 애매한 메일은 받은편지함에 남겨 사람이 확인하게 한다.

## 실행 방식

영숙 텔레그램 봇에서 메일 관련 요청을 받으면 `gmail_manager.run()`이 직접 호출된다.

```bash
python projects/ai-team/skills/소미_메일매니저/tools/gmail_manager.py
```

## 주요 도구 파일

| 파일 | 역할 |
|------|------|
| `tools/gmail_manager.py` | Gmail OAuth, 메일 조회, 라벨 생성, LLM 분류, 보관 처리 |

## 분류 정책

| 라벨 | 처리 |
|------|------|
| `IMPORTANT` | 받은편지함 유지 |
| `REVIEW` | 받은편지함 유지 |
| `FINANCE` | 라벨 적용 후 보관 |
| `DEV` | 라벨 적용 후 보관 |
| `SHOPPING` | 라벨 적용 후 보관 |
| `PROMOTION` | 라벨 적용 후 보관 |

## 안전 규칙

- 메일 삭제 금지.
- 금융, 보안, 계정, 결제 실패, 서비스 장애 메일은 보수적으로 중요 처리.
- 확신이 없으면 `REVIEW`로 분류하고 받은편지함에 남긴다.
- LLM 응답이 없거나 JSON 파싱에 실패하면 `REVIEW`로 처리한다.
