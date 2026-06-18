#!/usr/bin/env python3
"""
ECC 통합 보안 감사 스크립트
경수(보안 스캐너) + 로율(컴플라이언스 감사) 통합 실행
"""

import os
import sys
import json
from datetime import datetime
from pathlib import Path

# Root path discovery
_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(4):
    if os.path.isdir(os.path.join(_root, "projects")):
        break
    _root = os.path.dirname(_root)
sys.path.insert(0, _root)
sys.path.insert(0, os.path.join(_root, "projects", "ai-team"))

# 에이전트 도구 임포트
sys.path.insert(0, os.path.join(_root, "projects", "ai-team", "skills", "경수_수사관", "tools"))
sys.path.insert(0, os.path.join(_root, "projects", "ai-team", "skills", "로율_변호사", "tools"))

from security_scanner import SecurityScanner
from compliance_auditor import ComplianceAuditor
from _shared.notify import send
from _shared.env import load_env

load_env()


class IntegratedSecurityAudit:
    """통합 보안 감사 시스템"""

    def __init__(self, project_root: str):
        self.project_root = project_root
        self.kyungsu_scanner = SecurityScanner()
        self.royul_auditor = ComplianceAuditor()
        self.overall_risk = "LOW"

    def run_full_audit(self, notify: bool = True) -> dict:
        """전체 보안 감사 실행"""
        print("=" * 60)
        print("ECC 통합 보안 감사 시작")
        print("=" * 60)
        print()

        results = {
            "timestamp": datetime.now().isoformat(),
            "project_root": self.project_root,
            "security_scan": None,
            "compliance_audit": None,
            "overall_risk": "LOW",
            "critical_findings": []
        }

        # 1. 경수: 보안 스캔 (프롬프트 인젝션, 악성 코드)
        print("[1/2] 🚨 경수 수사관 - 보안 스캔 실행 중...")
        security_result = self._run_security_scan()
        results["security_scan"] = security_result
        print(f"   ✓ 보안 스캔 완료: {security_result['threat_level']}")
        print()

        # 2. 로율: 컴플라이언스 감사 (개인정보, 라이선스)
        print("[2/2] ⚖️ 로율 변호사 - 컴플라이언스 감사 실행 중...")
        compliance_result = self._run_compliance_audit()
        results["compliance_audit"] = compliance_result
        print(f"   ✓ 컴플라이언스 감사 완료: {compliance_result['risk_level']}")
        print()

        # 종합 위험도 평가
        self.overall_risk = self._calculate_overall_risk(
            security_result.get("threat_level", "LOW"),
            compliance_result.get("risk_level", "LOW")
        )
        results["overall_risk"] = self.overall_risk

        # 크리티컬 발견사항 수집
        results["critical_findings"] = self._collect_critical_findings(
            security_result, compliance_result
        )

        # 리포트 저장
        report_path = self._save_integrated_report(results)
        results["report_path"] = report_path

        print("=" * 60)
        print(f"감사 완료: 종합 위험도 = {self.overall_risk}")
        print(f"리포트: {report_path}")
        print("=" * 60)
        print()

        # 텔레그램 알림
        if notify and self.overall_risk in ["HIGH", "CRITICAL"]:
            self._send_notification(results)

        return results

    def _run_security_scan(self) -> dict:
        """보안 스캔 실행"""
        # 주요 디렉토리 스캔
        scan_dirs = [
            os.path.join(self.project_root, "projects", "ai-team", "skills"),
            os.path.join(self.project_root, "projects", "ai-team", "scripts"),
            os.path.join(self.project_root, "projects", "petnna"),
        ]

        all_findings = []
        for scan_dir in scan_dirs:
            if os.path.exists(scan_dir):
                result = self.kyungsu_scanner.scan_directory(scan_dir)
                all_findings.extend(result.get("results", []))

        # 리포트 생성
        report_path = self.kyungsu_scanner.generate_report()

        return {
            "threat_level": self.kyungsu_scanner.threat_level,
            "total_files_scanned": len(all_findings),
            "threats_detected": sum(1 for r in all_findings if r.get("threat_detected")),
            "report_path": report_path,
            "findings": all_findings[:10]  # 상위 10개만
        }

    def _run_compliance_audit(self) -> dict:
        """컴플라이언스 감사 실행"""
        # 라이선스 스캔
        license_result = self.royul_auditor.scan_license_compliance(self.project_root)

        # 민감정보 스캔 (주요 파일들)
        sensitive_files = [
            os.path.join(self.project_root, ".env"),
            os.path.join(self.project_root, ".env.example"),
            os.path.join(self.project_root, "README.md"),
        ]

        for file_path in sensitive_files:
            if os.path.exists(file_path):
                try:
                    with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
                        content = f.read()
                    self.royul_auditor.scan_sensitive_data(content, file_path)
                except Exception:
                    pass

        # 리포트 생성
        report_path = self.royul_auditor.generate_compliance_report()

        return {
            "risk_level": self.royul_auditor.risk_level,
            "total_issues": len(self.royul_auditor.compliance_issues),
            "license_conflicts": len(license_result.get("license_conflicts", [])),
            "report_path": report_path,
            "issues": self.royul_auditor.compliance_issues[:10]  # 상위 10개만
        }

    def _calculate_overall_risk(self, security_level: str, compliance_level: str) -> str:
        """종합 위험도 계산"""
        risk_values = {"LOW": 1, "MEDIUM": 2, "HIGH": 3, "CRITICAL": 4, "SAFE": 0, "UNKNOWN": 1}

        sec_val = risk_values.get(security_level.upper(), 1)
        comp_val = risk_values.get(compliance_level.upper(), 1)

        max_val = max(sec_val, comp_val)

        if max_val >= 4:
            return "CRITICAL"
        elif max_val >= 3:
            return "HIGH"
        elif max_val >= 2:
            return "MEDIUM"
        else:
            return "LOW"

    def _collect_critical_findings(self, security_result: dict, compliance_result: dict) -> list:
        """크리티컬 발견사항 수집"""
        findings = []

        # 보안 위협
        if security_result.get("threats_detected", 0) > 0:
            for finding in security_result.get("findings", []):
                if finding.get("threat_detected"):
                    findings.append({
                        "type": "SECURITY",
                        "source": finding.get("source", "unknown"),
                        "description": f"프롬프트 인젝션 패턴 {finding.get('findings_count', 0)}개 발견",
                        "severity": "HIGH"
                    })

        # 컴플라이언스 이슈
        if compliance_result.get("total_issues", 0) > 0:
            for issue in compliance_result.get("issues", []):
                findings.append({
                    "type": "COMPLIANCE",
                    "source": issue.get("source", "unknown"),
                    "description": f"민감정보 {issue.get('sensitive_data_found', 0)}개 발견",
                    "severity": issue.get("compliance_risk", "MEDIUM")
                })

        return findings

    def _save_integrated_report(self, results: dict) -> str:
        """통합 리포트 저장"""
        output_dir = os.path.join(self.project_root, "output", "integrated_audits")
        os.makedirs(output_dir, exist_ok=True)

        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        report_path = os.path.join(output_dir, f"audit_{timestamp}.json")

        with open(report_path, 'w', encoding='utf-8') as f:
            json.dump(results, f, indent=2, ensure_ascii=False)

        return report_path

    def _send_notification(self, results: dict):
        """텔레그램 알림 전송"""
        message = f"""🔒 ECC 통합 보안 감사 결과

📊 종합 위험도: {results['overall_risk']}

🚨 경수 (보안 스캔):
   - 위협 레벨: {results['security_scan']['threat_level']}
   - 스캔 파일: {results['security_scan']['total_files_scanned']}개
   - 위협 발견: {results['security_scan']['threats_detected']}개

⚖️ 로율 (컴플라이언스):
   - 위험 레벨: {results['compliance_audit']['risk_level']}
   - 이슈: {results['compliance_audit']['total_issues']}개
   - 라이선스 충돌: {results['compliance_audit']['license_conflicts']}개

📋 크리티컬 발견사항: {len(results['critical_findings'])}개

리포트: {results['report_path']}
"""
        send(message)


def main():
    """CLI 진입점"""
    import argparse

    parser = argparse.ArgumentParser(description="ECC 통합 보안 감사")
    parser.add_argument("--no-notify", action="store_true", help="텔레그램 알림 비활성화")
    parser.add_argument("--project-root", default=_root, help="프로젝트 루트 경로")

    args = parser.parse_args()

    # UTF-8 설정
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")

    # 감사 실행
    audit = IntegratedSecurityAudit(args.project_root)
    results = audit.run_full_audit(notify=not args.no_notify)

    # 결과 요약 출력
    print("\n📊 감사 결과 요약:")
    print(f"   종합 위험도: {results['overall_risk']}")
    print(f"   크리티컬 발견사항: {len(results['critical_findings'])}개")
    print(f"   리포트 경로: {results['report_path']}")

    # 종료 코드 (CI/CD 통합용)
    if results['overall_risk'] in ["HIGH", "CRITICAL"]:
        sys.exit(1)
    else:
        sys.exit(0)


if __name__ == "__main__":
    main()
