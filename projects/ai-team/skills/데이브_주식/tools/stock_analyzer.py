"""
데이브 주식 분석 실행 스크립트
"""
import os
import sys
import datetime

_here = os.path.dirname(os.path.abspath(__file__))
# projects/ai-team/skills/데이브_주식/tools -> projects/ai-team/
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, "..", "..", ".."))
PROJECT_ROOT = os.path.abspath(os.path.join(AI_TEAM_ROOT, "..", ".."))
sys.path.insert(0, AI_TEAM_ROOT)

from _shared.env_loader import load_env
from google import genai
from google.genai import types
from analyze_stock_realtime import analyze_stock_realtime

# UTF-8 설정
if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

load_env()

def _calc_atr(records: list, period: int = 10) -> float:
    """최근 N일 ATR(평균실제범위) 계산."""
    import numpy as np
    if len(records) < period + 1:
        return 0.0
    trs = []
    for i in range(1, len(records)):
        high = records[i]["High"] if "High" in records[i] else records[i]["Close"]
        low  = records[i]["Low"]  if "Low"  in records[i] else records[i]["Close"]
        prev_close = records[i - 1]["Close"]
        tr = max(high - low, abs(high - prev_close), abs(low - prev_close))
        trs.append(tr)
    return round(float(np.mean(trs[-period:])), 0)


def _format_realtime_data(stock_code: str) -> str:
    """analyze_stock_realtime 결과를 프롬프트용 텍스트로 변환 (ATR 기반 손절 포함)."""
    try:
        print(f"[Dave] 네이버 금융 실시간 데이터 수집 중... ({stock_code})")
        data = analyze_stock_realtime(stock_code)
        if "error" in data:
            return f"[실시간 데이터 수집 실패: {data['error']}]"

        records = data.get("최근 30일 슈퍼트렌드 분석", [])
        recent = records[-5:] if len(records) >= 5 else records
        rows = "\n".join(
            f"  {r['Date']} | 종가 {r['Close']}원 | ST {r['Supertrend']}원 | {r['Trend']}"
            for r in recent
        )

        # ATR 계산 및 ATR 기반 손절 기준 도출
        atr = _calc_atr(records)
        current_price_num = int(str(data["현재 주가"]).replace("원", "").replace(",", ""))
        stop_normal = round(current_price_num - atr * 2.0, -1)

        # 매물대
        volume_profile = data.get("매물대 상위 3구간", [])
        vp_rows = "\n".join(
            f"  {v['가격대']} — 누적거래량 {v['누적거래량']:,}"
            for v in volume_profile
        ) if volume_profile else "  (데이터 부족)"

        # 보조지표
        ind = data.get("보조지표", {})
        ind_str = (
            f"  RSI(14): {ind.get('RSI14', 'N/A')} "
            f"{'과매도(반등 가능)' if ind.get('RSI14', 50) < 30 else '과매수 주의' if ind.get('RSI14', 50) > 70 else '중립'}\n"
            f"  MACD: {ind.get('MACD', 'N/A')} / Signal: {ind.get('MACD_Signal', 'N/A')} → {ind.get('MACD_상태', 'N/A')}\n"
            f"  볼린저밴드: 상단 {ind.get('BB_상단', 'N/A')}원 / 중단 {ind.get('BB_중단', 'N/A')}원 / 하단 {ind.get('BB_하단', 'N/A')}원 → {ind.get('BB_위치', 'N/A')}\n"
            f"  OBV 추세: {ind.get('OBV_추세', 'N/A')} (5일 변화: {ind.get('OBV_5일변화', 'N/A'):,})\n"
            f"  거래량 회전율: {ind.get('거래량회전율', 'N/A')} → {ind.get('거래량평균대비', 'N/A')}\n"
            f"  거래량 방향성: {ind.get('거래량방향성', 'N/A')}"
        )

        # 외국인/기관
        inv = data.get("외국인기관동향", {})
        inv_str = (
            f"  외국인 보유율: {inv.get('외국인보유율', 'N/A')} | 5일 순매매: {inv.get('외국인5일순매매', 'N/A'):,} → {inv.get('외국인추세', 'N/A')}\n"
            f"  기관 5일 순매매: {inv.get('기관5일순매매', 'N/A'):,} → {inv.get('기관추세', 'N/A')}"
        ) if inv else "  (데이터 수집 실패)"

        return (
            f"현재 주가: {data['현재 주가']}\n"
            f"현재 추세: {data['현재 추세']}\n"
            f"ATR(10일): {atr}원\n"
            f"ATR 기반 손절 기준:\n"
            f"  - 일반 (ATR×2.0): {stop_normal:,.0f}원\n"
            f"보조지표:\n{ind_str}\n"
            f"외국인/기관 동향:\n{inv_str}\n"
            f"매물대 상위 3구간:\n{vp_rows}\n"
            f"최근 5일 슈퍼트렌드:\n{rows}"
        )
    except Exception as e:
        return f"[실시간 데이터 수집 오류: {e}]"

_AVG_PRICE = 25264   # 평단가
_HOLDINGS  = 640     # 보유 수량
_CASH      = 2214137 # 예수금
_EXIT_LO   = 16500   # 탈출 목표가 하단
_EXIT_HI   = 17000   # 탈출 목표가 상단

def _calc_scenarios(current_price: int, atr: float) -> str:
    """Python에서 미리 계산한 확정 수치를 문자열로 반환."""
    loss_now   = round((_AVG_PRICE - current_price) / _AVG_PRICE * 100, 1)
    stop_atr2  = round(current_price - atr * 2, -1)

    # 탈출 시나리오 — 현 보유 그대로
    loss_exit_lo = round((_AVG_PRICE - _EXIT_LO) / _AVG_PRICE * 100, 1)
    loss_exit_hi = round((_AVG_PRICE - _EXIT_HI) / _AVG_PRICE * 100, 1)

    # 물타기 후 탈출 시나리오 (13,750원 진입 가정, 예수금 절반 투입)
    add_funds   = _CASH // 2
    add_shares  = add_funds // 13750
    new_avg     = round((_AVG_PRICE * _HOLDINGS + 13750 * add_shares) / (_HOLDINGS + add_shares))
    loss_after_avg_lo = round((new_avg - _EXIT_LO) / new_avg * 100, 1)
    loss_after_avg_hi = round((new_avg - _EXIT_HI) / new_avg * 100, 1)

    return (
        f"[Python 사전 계산값 — 이 수치를 그대로 사용할 것]\n"
        f"현재 손실률: -{loss_now}% (평단 {_AVG_PRICE:,}원 → 현재가 {current_price:,}원)\n"
        f"ATR×2 손절선: {stop_atr2:,.0f}원\n"
        f"탈출 목표가: {_EXIT_LO:,}~{_EXIT_HI:,}원\n"
        f"  ├ 현 보유(640주) 그대로 16,500원 탈출: -{loss_exit_lo}% 손실\n"
        f"  ├ 현 보유(640주) 그대로 17,000원 탈출: -{loss_exit_hi}% 손실\n"
        f"  ├ 물타기({add_shares}주 추가, 새 평단 {new_avg:,}원) 후 16,500원 탈출: -{loss_after_avg_lo}% 손실\n"
        f"  └ 물타기({add_shares}주 추가, 새 평단 {new_avg:,}원) 후 17,000원 탈출: -{loss_after_avg_hi}% 손실"
    )

def run_analysis(query: str = "", stock_code: str = "032820") -> str:
    print(f"[Dave] Starting stock analysis: {query}")

    realtime_summary = _format_realtime_data(stock_code)
    print(f"[Dave] 실시간 데이터 수집 완료:\n{realtime_summary}\n")

    # ATR 추출 후 시나리오 사전 계산
    try:
        current_price_num = int("".join(filter(str.isdigit, realtime_summary.split("현재 주가:")[1].split("\n")[0])))
        atr_num = float(realtime_summary.split("ATR(10일):")[1].split("원")[0].strip())
    except Exception:
        current_price_num, atr_num = 0, 0
    scenarios = _calc_scenarios(current_price_num, atr_num)

    today_str = datetime.datetime.now().strftime("%Y년 %m월 %d일 (%a)")

    prompt = f"""당신은 거래량 입체분석·자가 자료 탐색·타점 통제 주식 전문 에이전트 '데이브(Dave)'입니다.
오늘 날짜는 **{today_str}**입니다. 모든 분석은 이 날짜를 기준으로 동기화합니다.

[사용자 요청/질문]: {query}

[네이버 금융 실시간 데이터 - 실제 수집값]:
{realtime_summary}

[사전 계산된 수치 — Ollama가 직접 계산하지 말고 이 값을 그대로 사용]:
{scenarios}

[분석 타깃 종목 정보]: 우리기술 (032820)
[실시간 계좌 고정값]:
- 보유 예수금: {_CASH:,}원
- 투자자 평단가: {_AVG_PRICE:,}원 고정
- 보유 수량: {_HOLDINGS}주

[데이브 절대 분석 강령 및 조건]:
1. 실시간 날짜 및 시간 동기화 (Temporal Context Rule): 보고서 타이틀 및 브리핑 상단에 현재 기준 날짜(2026년 6월 11일)를 명시하고 당일 발생 뉴스에 가중치 부여.
2. OBV + 거래량 방향성 분석: 실시간 데이터의 OBV 추세·5일변화, 거래량 방향성(상승일 vs 하락일)을 반영하여 세력 매집/이탈 방향을 판단한다.
2-1. 매물대(Volume Profile): 실시간 데이터의 매물대 상위 3구간으로 지지/저항 판단. 현재가 대비 위치를 명시할 것.
2-2. 보조지표 반영 강령:
   - RSI(14): 30 이하 과매도·70 이상 과매수 판정. 현재 값을 명시하고 해석.
   - MACD: 골든크로스/데드크로스 상태를 명시하고 추세 전환 신호 여부 판단.
   - 볼린저밴드: 현재 주가 위치(상단/중단/하단)를 명시. 하단 이탈 시 과매도 반등 가능성 언급.
   - 거래량 회전율: 평균 대비 오늘 거래량 배율로 매집 가능성 판단.
2-3. 외국인/기관 동향: 실시간 데이터의 외국인보유율·5일 순매매 방향을 반영하여 수급 주체 판단. 외국인 보유율 7.8% 이상 유지 여부 명시.
3. SuperTrend 신호: ATR 기반 변곡점 감시. 그린/레드 모드 판정.
4. 거래량 회전율 500%~1000% 상회 여부 확인 (매집 시작 시그널)
5. 고가권 거래량 없는 하락: 계단식 설거지 기만행위 판정
6. 상승 시 거래량 증가/하락 시 감소 법칙 검증
7. 물타기(추가 매수) 타점 강령: 현금 투입은 오직 [슈퍼밴드 청색 전환] + [SuperTrend 그린/바이 모드 전환 및 지지 확인] + [외인 지분율 최소 7.8%~8% 대 유지] + [상승 전환 시 거래량 증가 확인]이 동시에 만족될 때만 승인. 최종 진입 타점은 13,500원 ~ 14,000원 선 돌파 시점.
8. 손절 기준 강령 (ATR 기반 트레일링 스톱 — 고정 금액 손절 절대 금지):
   - 손절 기준은 반드시 실시간 데이터에서 계산된 ATR(10일 평균실제범위)을 기반으로 산출한다.
   - 신규 물타기 진입 시 손절: 진입가 - (ATR × 2.0) → 일반 스윙 기준. 변동성이 낮으면 ATR×1.5, 높으면 ATR×3.0 적용.
   - 기존 보유 640주 손절: 현재가 - (ATR × 2.0) 기준으로 제시하되, SuperTrend 선이 더 가까우면 SuperTrend 선을 트레일링 스톱으로 우선 사용.
   - 보고서에 ATR 값과 함께 계산식을 명시하여 사장님이 직접 검증할 수 있도록 할 것.
   - 절대 금지: 물타기 진입 목표가(13,500~14,000원)를 손절 기준으로 혼용하지 말 것.
9. 탈출 매도 타점 강령: 16,500원 ~ 17,000원 구간 고정. 도달 시 물타기 성공 여부에 따라 손실률 압축(-19% 내외) 탈출 시나리오 제안 또는 미투입 시 보유 수량 640주의 최소 절반(320주) 기계적 예약 손절 지시.

[응답 형식 — 무조건 아래 순서로만]:

## 결론: [지금 X 하세요 / 하지 마세요] — 한 줄로 시작

물타기 조건 체크:
| 조건 | 상태 |
|---|---|
| SuperTrend 그린 전환 | ✅/❌ |
| 슈퍼밴드 청색 전환 | ✅/❌ |
| 거래량 급증(500%+) | ✅/❌ |

핵심 수치 (반드시 아래 항목 모두 포함):
- 현재 손실률: 사전 계산값 사용
- ATR×2 손절선: 사전 계산값 사용
- 탈출 목표가: 16,500~17,000원 + 도달 시 예상 손실률 (사전 계산값 사용)

매물대 분석 (반드시 포함):
- 실시간 데이터의 매물대 상위 3구간을 모두 표로 표시 (가격대 | 누적거래량 | 현재가 대비 위치)
- 각 구간이 현재가 위인지(저항) 아래인지(지지) 명시
- 가장 가까운 저항선과 지지선을 특정하고 돌파 난이도 판단

보조지표 요약 (반드시 포함):
- RSI: 값 + 과매도/과매수/중립 판정
- MACD: 골든/데드크로스 상태
- OBV: 상승/하락 + 세력 해석
- 외국인 보유율 + 매수/매도 추세

탈출 시나리오 (항상 포함):
- 물타기 성공 후 16,500원 탈출 시: 새 평단 계산 및 손실률 압축률
- 물타기 없이 현 보유(640주)로 16,500원 도달 시: 손실률

선택지: 사장님이 오늘 당장 취할 수 있는 행동 2가지만 번호로 제시.

절대 금지:
- 결론 전 배경 설명 / "분석 결과에 의하면" 서론
- Mermaid 다이어그램
- 모든 지표 나열식 긴 보고서
- 영숙 보고 / 영숙이 요약 섹션 (어떤 형태로도 금지)

"""

    api_key = os.getenv("GEMINI_API_KEY", "").strip('"').strip("'")
    if not api_key:
        return "❌ [데이브] GEMINI_API_KEY 환경변수가 설정되지 않았습니다."

    try:
        client = genai.Client(api_key=api_key)
        response = client.models.generate_content(
            model="gemini-2.5-flash",
            contents=prompt,
            config=types.GenerateContentConfig(
                system_instruction="너는 주식 전문 에이전트 데이브(Dave)이다. 결론부터 말하고 간결하게 답한다. 영숙 보고 섹션은 절대 생성하지 않는다.",
                max_output_tokens=2000,
                temperature=0.7
            )
        )
        report = response.text

        # 영숙 보고 섹션 후처리 제거
        for marker in ["영숙 보고", "영숙이 보고", "영숙님", "📱"]:
            if marker in report:
                report = report[:report.index(marker)].rstrip("-— \n")
        
        # 분석 결과를 reports/research/dave_stock_analysis.md 에 기록
        report_dir = os.path.join(PROJECT_ROOT, "reports", "research")
        os.makedirs(report_dir, exist_ok=True)
        report_path = os.path.join(report_dir, "dave_stock_analysis.md")
        
        with open(report_path, "w", encoding="utf-8") as f:
            f.write(report)

        # Notion에 즉시 자동 동기화 업로드
        try:
            from _shared.notion_report_manager import NotionReportManager
            manager = NotionReportManager()
            today_str = datetime.datetime.now().strftime("%Y-%m-%d")
            manager.create_report_entry(
                agent_name="데이브",
                task_title=f"우리기술 {today_str} 장마감 브리핑",
                result=report
            )
            print("[Dave] Notion report sync completed successfully.")
        except Exception as notion_err:
            print(f"[Dave] Notion sync warning: {notion_err}")
            
        return report
    except Exception as e:
        return f"❌ [데이브] 분석 중 오류 발생: {e}"

if __name__ == "__main__":
    # 사용법: python stock_analyzer.py [종목코드] [질문]
    # 예: python stock_analyzer.py 032820 "오늘 물타기 해도 될까요?"
    args = sys.argv[1:]
    if args and args[0].isdigit():
        code = args[0]
        query_text = " ".join(args[1:]) or "금일 장 마감 분석 및 리스크 진단"
    else:
        code = "032820"
        query_text = " ".join(args) or "우리기술 금일 장 마감 분석 및 리스크 진단"
    result = run_analysis(query_text, stock_code=code)
    print(result)
