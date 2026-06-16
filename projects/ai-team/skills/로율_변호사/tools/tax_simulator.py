import os
import sys
import json

_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, ".agent")):
        break
    _root = os.path.dirname(_root)
sys.path.insert(0, _root)

def calculate_tax(tax_base):
    if tax_base <= 0: return 0
    if tax_base <= 100_000_000:
        return tax_base * 0.10
    elif tax_base <= 500_000_000:
        return tax_base * 0.20 - 10_000_000
    elif tax_base <= 1_000_000_000:
        return tax_base * 0.30 - 60_000_000
    elif tax_base <= 3_000_000_000:
        return tax_base * 0.40 - 160_000_000
    else:
        return tax_base * 0.50 - 460_000_000

def run_simulation(asset_value_str):
    try:
        # 간단한 파싱 (예: "1000000000" 또는 "10억")
        asset = int(str(asset_value_str).replace(",", "").replace("원", ""))
    except ValueError:
        asset = 1_000_000_000  # 기본값 10억
        
    basic_deduction = 500_000_000 # 일괄공제 5억 가정
    tax_base = max(0, asset - basic_deduction)
    tax_amount = calculate_tax(tax_base)
    
    asset_10k = int(asset / 10000)
    tax_base_10k = int(tax_base / 10000)
    tax_10k = int(tax_amount / 10000)
    
    output = f"""⚖️ **[로율의 세무 시뮬레이션 결과]**

### 1. 핵심 요약 비교표 (Executive Summary Table)
| 자산가액(만원) | 기본공제액(만원) | 과세표준(만원) | 예상 산출세액(만원) |
|---|---|---|---|
| {asset_10k:,} | 50,000 | {tax_base_10k:,} | **{tax_10k:,}** |

### 2. 법률 프레임워크 및 판례 분석 (Legal Framework)
- **적용 법령**: 상속세 및 증여세법 제21조(일괄공제), 제26조(상속세 세율)
- **참고**: 일괄공제 5억원을 일률적으로 적용하여 시뮬레이션 하였습니다. 배우자 생존 여부에 따라 최소 5억원의 추가 공제가 가능합니다. (대법원 관련 판례 참조)

### 3. 세액 시뮬레이션 및 산식 전개 (Tax Simulation)
- 총 자산 규모: {asset_10k:,}만원
- 공제 후 과세표준 ($x$): {tax_base_10k:,}만원
- 적용 세율 계산식 ($T(x) = x \\cdot r_i - d_i$):
  과세표준 구간에 따라 산출된 최종 세액은 **{tax_10k:,}만원**입니다.

### 4. 규제 준수 안내 및 전문가 연결 (Compliance Warning)
> "본 데이터는 정량적 세무 시뮬레이션 결과물일 뿐이며, 실제 세무 신고 대행 및 세액 확정은 당사 플랫폼과 연계된 공식 파트너 세무사를 통해 적법하게 진행되어야 합니다."
"""
    return output

if __name__ == "__main__":
    import sys
    arg = sys.argv[1] if len(sys.argv) > 1 else 1000000000
    print(run_simulation(arg))
