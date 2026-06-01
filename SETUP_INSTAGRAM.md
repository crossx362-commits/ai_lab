# Instagram Access Token 발급 가이드

## 🔐 필요한 항목
- ✅ Instagram Business 계정 또는 Creator 계정
- ✅ Facebook Page (Instagram과 연결됨)
- ✅ Meta Developer App (이미 생성됨)

## 📋 현재 설정 정보
```
App ID: 1219822826776845
App Secret: 2b4e0b63ca84558ee64da6e856251235
```

## 🚀 발급 방법 (2가지 옵션)

### 옵션 1: Meta Graph API Explorer (추천)

1. **Graph API Explorer 접속**
   ```
   https://developers.facebook.com/tools/explorer/1219822826776845/
   ```

2. **User Token 생성**
   - "Get User Access Token" 버튼 클릭
   - 필요한 권한 선택:
     - `instagram_basic`
     - `instagram_content_publish`
     - `instagram_manage_comments`
     - `instagram_manage_insights`
     - `pages_show_list`
     - `pages_read_engagement`

3. **Instagram 계정 연결**
   - Meta 계정으로 로그인
   - Instagram Business 계정 권한 승인

4. **Token 복사**
   - 생성된 단기 토큰(Short-lived Token) 복사

5. **장기 토큰으로 변환 및 저장**
   ```bash
   python ai-team/assets/tool-seeds/코다리_개발자/instagram_token_refresher.py <단기토큰> <계정ID>
   ```
   - 자동으로 60일 장기 토큰으로 변환되어 `.env`에 저장됩니다
   - 이후 자동 갱신됩니다

### 옵션 2: 수동 API 호출

1. **단기 토큰 → 장기 토큰 변환**
   ```bash
   curl "https://graph.instagram.com/access_token?grant_type=ig_exchange_token&client_id=1219822826776845&client_secret=2b4e0b63ca84558ee64da6e856251235&access_token=<단기토큰>"
   ```

2. **계정 ID 확인**
   ```bash
   curl "https://graph.instagram.com/v23.0/me?fields=id,username&access_token=<장기토큰>"
   ```

3. **.env 파일에 수동 입력**
   ```bash
   INSTAGRAM_ACCESS_TOKEN="<장기토큰>"
   INSTAGRAM_ACCOUNT_ID="<계정ID>"
   ```

## 🔄 자동 갱신 설정

장기 토큰은 60일간 유효하며, 만료 10일 전부터 자동 갱신됩니다.
- 자동 갱신은 `telegram_bot.py`의 `_kodari_health_loop`에서 매일 체크됩니다.

수동 갱신:
```bash
python ai-team/assets/tool-seeds/코다리_개발자/instagram_token_refresher.py
```

## 📝 .env 파일 최종 형태

```env
INSTAGRAM_APP_ID="1219822826776845"
INSTAGRAM_APP_SECRET="2b4e0b63ca84558ee64da6e856251235"
INSTAGRAM_ACCESS_TOKEN="IGQWRxxx...xxx"  # 발급받은 토큰
INSTAGRAM_ACCOUNT_ID="17841xxx...xxx"    # Instagram Business 계정 ID
```

## ⚠️ 주의사항

1. **단기 토큰은 1시간만 유효**합니다 - 바로 장기 토큰으로 변환하세요
2. **Service Role Key는 사용하지 마세요** - User Access Token만 사용
3. 토큰은 `.env` 파일에만 저장됩니다 (Git에 커밋되지 않음)
4. Instagram Business 계정이 필요합니다 (개인 계정은 불가)

## 🔗 유용한 링크

- [Meta Developer Console](https://developers.facebook.com/apps/1219822826776845/)
- [Graph API Explorer](https://developers.facebook.com/tools/explorer/)
- [Instagram Basic Display API Docs](https://developers.facebook.com/docs/instagram-basic-display-api)
- [Access Token 디버거](https://developers.facebook.com/tools/debug/accesstoken/)
