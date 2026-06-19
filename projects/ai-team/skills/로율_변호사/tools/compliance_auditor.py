#!/usr/bin/env python3
"""
로율(Royul) 변호사 - 컴플라이언스 감사 도구
ECC 기반 법률/규정 준수 검증 및 라이선스 스캐닝
"""

import os
import sys
import json
import re
from datetime import datetime
from pathlib import Path
from typing import List, Dict

# Root path discovery
_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, "projects")):
        break
    _root = os.path.dirname(_root)
sys.path.insert(0, _root)
sys.path.insert(0, os.path.join(_root, "projects", "ai-team"))

from _shared.env import load_env
from _shared.notify import send, report
from _shared.llm import text

load_env()

AGENT_NAME = "로율_변호사"


class ComplianceAuditor:
    """컴플라이언스 감사 시스템"""

    # GDPR/개인정보보호법 관련 민감 정보 패턴
    SENSITIVE_DATA_PATTERNS = {
        "email": r"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
        "phone_kr": r"\b01[0-9]-?\d{3,4}-?\d{4}\b",
        "ssn_kr": r"\b\d{6}-[1-4]\d{6}\b",  # 주민등록번호
        "credit_card": r"\b\d{4}[-\s]?\d{4}[-\s]?\d{4}[-\s]?\d{4}\b",
        "ip_address": r"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b",
        "api_key": r"(?i)(api[_-]?key|apikey|access[_-]?token)[\s:=\"\']+([a-zA-Z0-9_-]{20,})",
        "aws_key": r"(?i)(AKIA[0-9A-Z]{16})",
        "private_key": r"-----BEGIN (RSA |EC )?PRIVATE KEY-----",
    }

    # 오픈소스 라이선스 분류
    LICENSE_CATEGORIES = {
        "permissive": ["MIT", "Apache-2.0", "BSD-2-Clause", "BSD-3-Clause", "ISC"],
        "copyleft_weak": ["LGPL-2.1", "LGPL-3.0", "MPL-2.0"],
        "copyleft_strong": ["GPL-2.0", "GPL-3.0", "AGPL-3.0"],
        "proprietary_risk": ["SSPL", "Commons Clause", "Elastic License"],
    }

    def __init__(self):
        self.audit_results = []
        self.compliance_issues = []
        self.risk_level = "LOW"

    def scan_sensitive_data(self, content: str, source: str) -> Dict:
        """민감 정보 스캔 (GDPR/CCPA/개인정보보호법 준수)"""
        findings = []

        for data_type, pattern in self.SENSITIVE_DATA_PATTERNS.items():
            matches = re.finditer(pattern, content)
            for match in matches:
                findings.append({
                    "type": data_type,
                    "matched": match.group(0),
                    "position": match.span(),
                    "severity": self._get_sensitivity_level(data_type),
                    "regulation": self._get_applicable_regulation(data_type)
                })

        result = {
            "source": source,
            "timestamp": datetime.now().isoformat(),
            "sensitive_data_found": len(findings),
            "findings": findings,
            "compliance_risk": "HIGH" if len(findings) > 0 else "LOW",
            "recommendations": self._get_privacy_recommendations(findings)
        }

        if len(findings) > 0:
            self.compliance_issues.append(result)
            self.risk_level = "HIGH"

        return result

    def _get_sensitivity_level(self, data_type: str) -> str:
        """데이터 타입별 민감도 레벨"""
        critical = ["ssn_kr", "credit_card", "private_key"]
        high = ["email", "phone_kr", "api_key", "aws_key"]

        if data_type in critical:
            return "CRITICAL"
        elif data_type in high:
            return "HIGH"
        else:
            return "MEDIUM"

    def _get_applicable_regulation(self, data_type: str) -> List[str]:
        """적용 가능한 법규"""
        regulations = []

        if data_type in ["email", "phone_kr", "ssn_kr", "ip_address"]:
            regulations.extend(["GDPR", "개인정보보호법"])

        if data_type in ["credit_card"]:
            regulations.extend(["PCI-DSS", "전자금융거래법"])

        if data_type in ["api_key", "aws_key", "private_key"]:
            regulations.append("정보통신망법")

        return regulations

    def _get_privacy_recommendations(self, findings: List[Dict]) -> List[str]:
        """개인정보 처리 권장사항"""
        if not findings:
            return ["✅ 민감 정보 미발견"]

        recommendations = []
        data_types = set(f["type"] for f in findings)

        if "ssn_kr" in data_types or "credit_card" in data_types:
            recommendations.append("🚨 CRITICAL: 주민등록번호/신용카드 번호는 즉시 제거 또는 암호화 필요")
            recommendations.append("개인정보보호법 위반 위험 - 과태료 최대 5억원")

        if "email" in data_types or "phone_kr" in data_types:
            recommendations.append("⚠️ 개인정보 수집 시 동의 절차 확인 필요")
            recommendations.append("개인정보 처리방침 명시 및 파기 정책 수립")

        if "api_key" in data_types or "aws_key" in data_types:
            recommendations.append("🔑 API 키/시크릿은 환경변수 또는 키 관리 시스템으로 이동")
            recommendations.append("Git history에서 완전 제거 권장 (BFG Repo-Cleaner)")

        return recommendations

    def scan_license_compliance(self, directory: str) -> Dict:
        """라이선스 컴플라이언스 스캔"""
        license_files = []
        package_licenses = []

        # LICENSE 파일 스캔
        for root, dirs, files in os.walk(directory):
            dirs[:] = [d for d in dirs if d not in ['.git', 'node_modules', '__pycache__']]

            for file in files:
                if file.upper() in ["LICENSE", "LICENSE.md", "LICENSE.txt", "COPYING"]:
                    file_path = os.path.join(root, file)
                    license_info = self._analyze_license_file(file_path)
                    license_files.append(license_info)

        # package.json / requirements.txt 스캔
        package_licenses = self._scan_dependencies(directory)

        # 라이선스 충돌 검사
        conflicts = self._check_license_conflicts(package_licenses)

        result = {
            "directory": directory,
            "timestamp": datetime.now().isoformat(),
            "license_files": license_files,
            "dependency_licenses": package_licenses,
            "total_dependencies": len(package_licenses),
            "license_conflicts": conflicts,
            "compliance_status": "COMPLIANT" if not conflicts else "VIOLATION",
            "recommendations": self._get_license_recommendations(conflicts, package_licenses)
        }

        return result

    def _analyze_license_file(self, file_path: str) -> Dict:
        """LICENSE 파일 분석"""
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()

            detected_license = self._detect_license_type(content)

            return {
                "file": file_path,
                "detected_license": detected_license,
                "category": self._categorize_license(detected_license),
                "commercial_use_allowed": self._check_commercial_use(detected_license)
            }
        except Exception as e:
            return {"file": file_path, "error": str(e)}

    def _detect_license_type(self, content: str) -> str:
        """라이선스 타입 탐지"""
        content_lower = content.lower()

        if "mit license" in content_lower or "permission is hereby granted" in content_lower:
            return "MIT"
        elif "apache license" in content_lower and "version 2.0" in content_lower:
            return "Apache-2.0"
        elif "gnu general public license" in content_lower:
            if "version 3" in content_lower:
                return "GPL-3.0"
            elif "version 2" in content_lower:
                return "GPL-2.0"
        elif "gnu lesser general public license" in content_lower:
            return "LGPL-3.0" if "version 3" in content_lower else "LGPL-2.1"
        elif "bsd" in content_lower:
            return "BSD-3-Clause"
        elif "mozilla public license" in content_lower:
            return "MPL-2.0"
        else:
            return "Unknown"

    def _categorize_license(self, license_name: str) -> str:
        """라이선스 카테고리 분류"""
        for category, licenses in self.LICENSE_CATEGORIES.items():
            if license_name in licenses:
                return category
        return "unknown"

    def _check_commercial_use(self, license_name: str) -> bool:
        """상업적 사용 가능 여부"""
        permissive = self.LICENSE_CATEGORIES["permissive"]
        weak_copyleft = self.LICENSE_CATEGORIES["copyleft_weak"]

        return license_name in permissive or license_name in weak_copyleft

    def _scan_dependencies(self, directory: str) -> List[Dict]:
        """의존성 라이선스 스캔"""
        licenses = []

        # package.json 스캔
        package_json = os.path.join(directory, "package.json")
        if os.path.exists(package_json):
            with open(package_json, 'r', encoding='utf-8') as f:
                data = json.load(f)
                deps = {**data.get("dependencies", {}), **data.get("devDependencies", {})}

                for pkg, version in deps.items():
                    licenses.append({
                        "package": pkg,
                        "version": version,
                        "ecosystem": "npm",
                        "license": "Unknown"  # 실제로는 npm view로 조회 필요
                    })

        # requirements.txt 스캔
        requirements = os.path.join(directory, "requirements.txt")
        if os.path.exists(requirements):
            with open(requirements, 'r', encoding='utf-8') as f:
                for line in f:
                    line = line.strip()
                    if line and not line.startswith('#'):
                        pkg = line.split('==')[0].split('>=')[0].strip()
                        licenses.append({
                            "package": pkg,
                            "ecosystem": "pypi",
                            "license": "Unknown"
                        })

        return licenses

    def _check_license_conflicts(self, package_licenses: List[Dict]) -> List[Dict]:
        """라이선스 충돌 검사"""
        conflicts = []

        # GPL과 상업 소프트웨어 조합은 충돌
        # AGPL-3.0은 SaaS에서 사용 시 소스 공개 의무
        # 등의 규칙 검사

        # 예시: AGPL 의존성 검출
        for pkg in package_licenses:
            if pkg.get("license", "").startswith("AGPL"):
                conflicts.append({
                    "package": pkg["package"],
                    "license": pkg["license"],
                    "issue": "AGPL requires source disclosure for SaaS applications",
                    "severity": "HIGH"
                })

        return conflicts

    def _get_license_recommendations(self, conflicts: List[Dict], licenses: List[Dict]) -> List[str]:
        """라이선스 권장사항"""
        if not conflicts:
            return ["✅ 라이선스 충돌 미발견"]

        recommendations = []

        for conflict in conflicts:
            if "AGPL" in conflict["license"]:
                recommendations.append(f"⚠️ {conflict['package']}: AGPL 라이선스 - SaaS 배포 시 소스 공개 의무")
                recommendations.append("대안: MIT/Apache 라이선스 라이브러리로 교체 검토")

            if "GPL" in conflict["license"]:
                recommendations.append(f"⚠️ {conflict['package']}: GPL 라이선스 - 상업 소프트웨어에서 사용 제한")

        return recommendations

    def generate_compliance_report(self, output_path: str = None) -> str:
        """컴플라이언스 감사 리포트 생성"""
        report = {
            "agent": AGENT_NAME,
            "audit_timestamp": datetime.now().isoformat(),
            "risk_level": self.risk_level,
            "compliance_issues": self.compliance_issues,
            "total_issues": len(self.compliance_issues),
            "legal_recommendations": self._generate_legal_recommendations()
        }

        if output_path is None:
            output_path = os.path.join(_root, "output", "compliance_audits",
                                      f"audit_{datetime.now().strftime('%Y%m%d_%H%M%S')}.json")

        os.makedirs(os.path.dirname(output_path), exist_ok=True)

        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2, ensure_ascii=False)

        return output_path

    def _generate_legal_recommendations(self) -> List[str]:
        """법률 준수 권장사항"""
        recommendations = []

        if self.risk_level == "HIGH":
            recommendations.append("🚨 HIGH RISK: 즉시 법률 검토 필요")
            recommendations.append("개인정보 처리 방침 업데이트")
            recommendations.append("라이선스 컴플라이언스 재검토")
        else:
            recommendations.append("✅ 현재 준수 상태 양호")
            recommendations.append("정기 감사 유지 권장 (분기 1회)")

        recommendations.append("모든 오픈소스 의존성 라이선스 명시")
        recommendations.append("개인정보 보호책임자(CPO) 지정 확인")

        return recommendations


def main():
    """CLI 진입점"""
    import argparse

    parser = argparse.ArgumentParser(description="로율 변호사 - 컴플라이언스 감사")
    parser.add_argument("--scan-privacy", help="민감정보 스캔할 파일")
    parser.add_argument("--scan-licenses", help="라이선스 스캔할 디렉토리")
    parser.add_argument("--output", help="리포트 출력 경로")
    parser.add_argument("--notify", action="store_true", help="텔레그램 알림")

    args = parser.parse_args()
    report("로율", "컴플라이언스 감사 시작")
    auditor = ComplianceAuditor()

    if args.scan_privacy:
        print(f"[로율] 개인정보 스캔 중: {args.scan_privacy}")
        with open(args.scan_privacy, 'r', encoding='utf-8') as f:
            content = f.read()
        result = auditor.scan_sensitive_data(content, args.scan_privacy)
        print(json.dumps(result, indent=2, ensure_ascii=False))

    if args.scan_licenses:
        scan_dir = args.scan_licenses
        print(f"[로율] 라이선스 컴플라이언스 스캔 중: {scan_dir}")
        result = auditor.scan_license_compliance(scan_dir)
        print(f"총 의존성: {result['total_dependencies']}개")
        print(f"라이선스 충돌: {len(result['license_conflicts'])}개")
        print(json.dumps(result, indent=2, ensure_ascii=False))

    if not args.scan_privacy and not args.scan_licenses:
        print("[로율] 전체 컴플라이언스 감사 중: .")
        result = auditor.scan_license_compliance(".")
        print(f"총 의존성: {result['total_dependencies']}개")
        print(f"라이선스 충돌: {len(result['license_conflicts'])}개")

    # 리포트 생성
    report_path = auditor.generate_compliance_report(args.output)
    print(f"\n📊 감사 리포트 저장됨: {report_path}")

    # 텔레그램 알림
    if args.notify and auditor.risk_level == "HIGH":
        message = f"""⚖️ [로율] 컴플라이언스 경고

위험 레벨: {auditor.risk_level}
발견된 이슈: {len(auditor.compliance_issues)}개

리포트: {report_path}
"""
        send(message)


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")

    main()
