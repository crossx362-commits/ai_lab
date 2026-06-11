"""
데이브 주식 분석 실행 스크립트
"""
import os
import sys
import json
import datetime
import urllib.request

_here = os.path.dirname(os.path.abspath(__file__))
# projects/ai-team/skills/데이브_주식/tools -> projects/ai-team/
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, "..", "..", ".."))
PROJECT_ROOT = os.path.abspath(os.path.join(AI_TEAM_ROOT, "..", ".."))
sys.path.insert(0, AI_TEAM_ROOT)

from _shared.env_loader import load_env
from _shared.ollama_client import chat as lm_chat, is_available as lm_available

# UTF-8 설정
if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

load_env()

def run_analysis(query: str = "") -> str:
    print(f"[Dave] Starting stock analysis: {query}")
    
    # 1. 2026년 6월 11일 기준 실시간 데이터 / 매크로 뉴스 자율 탐색 (데이브의 자가 자료 탐색 가상 시뮬레이션)
    # 실제로는 실시간 웹 서치 및 API 조회가 들어가나, 여기서는 규칙에 맞춰 프롬프트를 통해 분석을 수행합니다.
    
    prompt = f"""당신은 거래량 입체분석·자가 자료 탐색·타점 통제 주식 전문 에이전트 '데이브(Dave)'입니다.
너는 시스템의 현재 시점인 **[2026년 6월 11일 목요일]**을 실시간 기준시로 고정 인식하며, 모든 글로벌 속보와 시황을 이 날짜 기준으로 동기화하여 분석합니다.

[사용자 요청/질문]: {query}

[분석 타깃 종목 정보]: 우리기술 (032820)
[실시간 계좌 고정값]: 
- 보유 예수금: 2,214,137원
- 투자자 평단가: 25,264원 고정
- 보유 수량: 640주

[데이브 절대 분석 강령 및 조건]:
1. 실시간 날짜 및 시간 동기화 (Temporal Context Rule): 보고서 타이틀 및 브리핑 상단에 현재 기준 날짜(2026년 6월 11일)를 명시하고 당일 발생 뉴스에 가중치 부여.
2. OBV 지표 분석: 주가 변동 대비 OBV 지표의 우상향/우하향 흐름을 추적하여 세력의 잔존 물량 상태를 추론.
3. SuperTrend(슈퍼트렌드) 신호 및 추세 동기화: ATR(평균실제범위) 지표 기반의 슈퍼트렌드 변곡점 돌파 신호(그린/바이 모드 vs 레드/셀 모드)를 감시하여, 현재 주가가 단기 상승 추세선의 지지를 받고 있는지 혹은 이탈하여 하락 구간에 진입했는지 기술적으로 판정.
4. 바닥권 회전율 분석: 평소 일일 거래량의 500%~1000% 상회 여부 확인 (매집 시작 시그널)
5. 고가권 거래량 없는 하락: 계단식 설거지 기만행위 판정
6. 상승 시 거래량 증가/하락 시 감소 법칙 검증
7. 물타기(추가 매수) 타점 강령: 현금 투입은 오직 [슈퍼밴드 청색 전환] + [SuperTrend 그린/바이 모드 전환 및 지지 확인] + [외인 지분율 최소 7.8%~8% 대 유지] + [상승 전환 시 거래량 증가 확인]이 동시에 만족될 때만 승인. 최종 진입 타점은 13,500원 ~ 14,000원 선 돌파 시점.
8. 탈출 매도 타점 강령: 16,500원 ~ 17,000원 구간 고정. 도달 시 물타기 성공 여부에 따라 손실률 압축(-19% 내외) 탈출 시나리오 제안 또는 미투입 시 보유 수량 640주의 최소 절반(320주) 기계적 예약 손절 지시.

[실행 가능한 대응 행동 플랜 (Action Plan) 요구 사항 - ★필수]:
- 분석 결과에 그치지 말고, 사용자가 **오늘 당장 취해야 할 명확한 포지션 행동 플랜(Action Plan)**을 도출하십시오.
  1. **예수금 보유 현황에 따른 오늘 자 즉각 행동:** 오늘 예수금 2,214,137원을 계좌에 어떻게 묶어둘지(Lock-up), 아니면 추가 진입을 할지 명확한 흑백 논리 판정.
  2. **물타기 성공 시 vs 실패 시 시나리오별 실질적 대응 수치:** 물타기가 성공해 평단가가 내려갈 경우 탈출 평단과 손실률 압축률 계산, 물타기 실패 시 320주 기계적 예약 손절 타점을 어떻게 설정할지 구체적 가격과 수량을 명시.
  3. **오늘 장 시작/장 마감 시점의 HTS/MTS 예약 주문 설정 방법:** 사용자가 기계적으로 설정해 둘 수 있는 실제 예약 주문 액션 가이드를 제공하십시오.

[초반 요약 배치 요구 사항 - ★필수]:
- 보고서 최상단에 **"💡 영숙이의 핵심 요약 브리핑"** 섹션을 만들고, 사장님이 한눈에 가장 이해하기 쉽도록 딱 3개의 명확하고 간단한 불릿 포인트로 오늘 무엇을 해야 하는지(예수금 묶어두기, 예약 매도 가격 등)를 정리하여 설명하십시오.

[시각화 & 디자인 요구 사항]:
- 보고서의 시각적 완성도와 가독성을 획득하기 위해 반드시 다음 요소를 포함하십시오:
  1. **구조적인 마크다운 도표(Table):** 계좌 진단 결과, 거래량 추이 비교, 매수/매도 시나리오별 타점 분석표(SuperTrend 및 슈퍼밴드 신호 상태 포함)를 표 포맷으로 정밀하게 시각화하십시오.
  2. **Mermaid Flowchart 또는 Sequence Diagram 차트:** 주가 분석 프로세스 또는 물타기/탈출 판단 흐름(Decision Tree, SuperTrend 분기 조건 포함)을 Mermaid 코드로 다이어그램화하여 본문에 삽입하십시오.
  3. **가독성이 좋은 인용구(>) 및 이모지 다채로운 활용:** 섹션별 가시성을 극대화하십시오.

위 규칙과 페르소나를 100% 반영하여 노션에 바로 붙여넣을 수 있는 Hierarchy 마크다운 포맷(##, ###) 및 영숙 보고용 요약본을 정확히 생성하십시오.
"""

    if not lm_available():
        return "❌ [데이브] Ollama 로컬 연결 실패로 분석을 수행할 수 없습니다."
        
    try:
        report = lm_chat(
            prompt,
            system="너는 주식 전문 에이전트 데이브(Dave)이다. 냉철하고 전문적인 투자 리스크 관리자 톤앤매너를 유지하라. 모든 문장은 반드시 사장님께 올리는 정중하고 예의 바른 극존칭의 존댓말(하십시오체, 해요체 등)을 사용하고 반말은 절대 사용하지 마라.",
            json_mode=False,
            max_tokens=2000,
            temperature=0.7
        )
        
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
    query_text = " ".join(sys.argv[1:]) if len(sys.argv) > 1 else "우리기술 금일 장 마감 분석 및 리스크 진단"
    result = run_analysis(query_text)
    print(result)
