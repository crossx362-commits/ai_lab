#!/usr/bin/env python3
"""
경수(Kyungsu) 수사관 - 보안 스캐너 도구
ECC AgentShield를 사용한 프롬프트 인젝션 탐지 및 악성 코드 스캐닝
"""

import os
import sys
import json
import subprocess
from datetime import datetime
from pathlib import Path

# Root path discovery
_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, "projects")):
        break
    _root = os.path.dirname(_root)
sys.path.insert(0, _root)
sys.path.insert(0, os.path.join(_root, "projects", "ai-team"))

from _shared.env_loader import load_env
from _shared.telegram_notifier import send_telegram_message
from _shared.gemini_client import text

load_env()

AGENT_NAME = "경수_수사관"
ECC_ROOT = Path(_root) / "projects" / "ai-team" / "security" / "ecc"


class SecurityScanner:
    """ECC AgentShield 기반 보안 스캐너"""

    def __init__(self):
        self.scan_results = []
        self.threat_level = "SAFE"

    def scan_prompt_injection(self, content: str, source: str = "unknown") -> dict:
        """
        프롬프트 인젝션 패턴 탐지

        Args:
            content: 스캔할 텍스트
            source: 소스 식별자

        Returns:
            탐지 결과 dict
        """
        dangerous_patterns = [
            # 명령 주입 패턴
            r"ignore\s+(all\s+)?(previous|prior|above|earlier)\s+instructions?",
            r"disregard\s+.{0,20}instructions?",
            r"forget\s+.{0,20}(instructions?|rules?|guidelines?)",
            r"override\s+.{0,20}(protocol|rules?|settings?)",

            # 시스템 명령 패턴
            r"(sudo|rm\s+-rf|exec|eval|system|shell|cmd|powershell)\s*\(",
            r"os\.(system|popen|exec|spawn)",
            r"__import__\s*\(\s*['\"]os['\"]",

            # 시크릿 노출 유도
            r"(show|reveal|display|print)\s+.{0,20}(api.?key|token|password|secret|credential)",
            r"what\s+is\s+.{0,20}(api.?key|token|password)",

            # 역할 변경 시도
            r"(you\s+are\s+now|act\s+as|pretend\s+to\s+be|roleplay)",
            r"new\s+(instructions?|role|persona|character)",
        ]

        import re
        findings = []

        for pattern in dangerous_patterns:
            matches = re.finditer(pattern, content, re.IGNORECASE)
            for match in matches:
                findings.append({
                    "pattern": pattern,
                    "matched_text": match.group(0),
                    "position": match.span(),
                    "severity": "HIGH"
                })

        result = {
            "source": source,
            "timestamp": datetime.now().isoformat(),
            "findings_count": len(findings),
            "findings": findings,
            "threat_detected": len(findings) > 0,
            "content_preview": content[:200] + "..." if len(content) > 200 else content
        }

        self.scan_results.append(result)

        if len(findings) > 0:
            self.threat_level = "CRITICAL" if len(findings) >= 3 else "HIGH"

        return result

    def scan_file(self, file_path: str) -> dict:
        """파일 스캔 - 프롬프트 인젝션 + 악성 코드 패턴"""
        try:
            with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
                content = f.read()

            result = self.scan_prompt_injection(content, source=file_path)
            result["file_type"] = Path(file_path).suffix

            # 추가 파일 특화 검사
            if file_path.endswith('.py'):
                result["python_specific"] = self._scan_python_specific(content)
            elif file_path.endswith('.js') or file_path.endswith('.ts'):
                result["js_specific"] = self._scan_js_specific(content)

            return result

        except Exception as e:
            return {
                "source": file_path,
                "error": str(e),
                "threat_detected": False
            }

    def _scan_python_specific(self, content: str) -> dict:
        """Python 특화 보안 검사"""
        import re

        dangerous = []

        # eval/exec 사용
        if re.search(r"\b(eval|exec)\s*\(", content):
            dangerous.append("eval/exec detected")

        # pickle 사용 (역직렬화 취약점)
        if re.search(r"import\s+pickle|pickle\.(loads?|dumps?)", content):
            dangerous.append("pickle usage detected (deserialization risk)")

        # subprocess without shell=False
        if re.search(r"subprocess\.(call|run|Popen).*shell\s*=\s*True", content):
            dangerous.append("subprocess with shell=True (command injection risk)")

        return {
            "dangerous_patterns": dangerous,
            "risk_level": "HIGH" if dangerous else "LOW"
        }

    def _scan_js_specific(self, content: str) -> dict:
        """JavaScript/TypeScript 특화 보안 검사"""
        import re

        dangerous = []

        # eval 사용
        if re.search(r"\beval\s*\(", content):
            dangerous.append("eval() detected")

        # innerHTML (XSS 위험)
        if re.search(r"\.innerHTML\s*=", content):
            dangerous.append("innerHTML assignment (XSS risk)")

        # document.write
        if re.search(r"document\.write\s*\(", content):
            dangerous.append("document.write (XSS risk)")

        return {
            "dangerous_patterns": dangerous,
            "risk_level": "HIGH" if dangerous else "LOW"
        }

    def scan_directory(self, directory: str, extensions: list = None) -> dict:
        """디렉토리 재귀 스캔"""
        if extensions is None:
            extensions = ['.py', '.js', '.ts', '.md', '.txt', '.json']

        all_results = []
        threat_count = 0

        for root, dirs, files in os.walk(directory):
            # 무시할 디렉토리
            dirs[:] = [d for d in dirs if d not in ['.git', 'node_modules', '__pycache__', '.venv']]

            for file in files:
                if any(file.endswith(ext) for ext in extensions):
                    file_path = os.path.join(root, file)
                    result = self.scan_file(file_path)
                    all_results.append(result)

                    if result.get("threat_detected"):
                        threat_count += 1

        return {
            "directory": directory,
            "total_files_scanned": len(all_results),
            "threats_detected": threat_count,
            "results": all_results,
            "summary": self._generate_summary(all_results)
        }

    def _generate_summary(self, results: list) -> dict:
        """스캔 결과 요약 생성"""
        total_findings = sum(r.get("findings_count", 0) for r in results)
        critical_files = [r["source"] for r in results if r.get("threat_detected")]

        return {
            "total_findings": total_findings,
            "critical_files_count": len(critical_files),
            "critical_files": critical_files[:10],  # 상위 10개만
            "threat_level": self.threat_level
        }

    def generate_report(self, output_path: str = None) -> str:
        """보안 스캔 리포트 생성"""
        report = {
            "agent": AGENT_NAME,
            "scan_timestamp": datetime.now().isoformat(),
            "total_scans": len(self.scan_results),
            "threat_level": self.threat_level,
            "results": self.scan_results,
            "recommendations": self._generate_recommendations()
        }

        if output_path is None:
            output_path = os.path.join(_root, "output", "security_scans",
                                      f"scan_{datetime.now().strftime('%Y%m%d_%H%M%S')}.json")

        os.makedirs(os.path.dirname(output_path), exist_ok=True)

        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2, ensure_ascii=False)

        return output_path

    def _generate_recommendations(self) -> list:
        """보안 권장사항 생성"""
        recommendations = []

        if self.threat_level == "CRITICAL":
            recommendations.append("🚨 CRITICAL: 즉시 시스템 격리 및 전수조사 필요")
            recommendations.append("모든 시크릿 키 즉시 교체")
            recommendations.append("악성 코드 삽입 가능성 검토")
        elif self.threat_level == "HIGH":
            recommendations.append("⚠️ HIGH: 의심스러운 패턴 발견 - 수동 검토 필요")
            recommendations.append("관련 파일 격리 및 상세 분석")
        else:
            recommendations.append("✅ 현재까지 심각한 위협 미발견")
            recommendations.append("정기 스캔 유지 권장")

        return recommendations


def main():
    """CLI 진입점"""
    import argparse

    parser = argparse.ArgumentParser(description="경수 수사관 - 보안 스캐너")
    parser.add_argument("--file", help="단일 파일 스캔")
    parser.add_argument("--dir", help="디렉토리 재귀 스캔")
    parser.add_argument("--text", help="텍스트 직접 스캔")
    parser.add_argument("--output", help="리포트 출력 경로")
    parser.add_argument("--notify", action="store_true", help="텔레그램 알림")

    args = parser.parse_args()

    scanner = SecurityScanner()

    if args.file:
        print(f"[경수] 파일 스캔 중: {args.file}")
        result = scanner.scan_file(args.file)
        print(json.dumps(result, indent=2, ensure_ascii=False))

    elif args.dir:
        print(f"[경수] 디렉토리 스캔 중: {args.dir}")
        result = scanner.scan_directory(args.dir)
        print(f"총 {result['total_files_scanned']}개 파일 스캔 완료")
        print(f"위협 발견: {result['threats_detected']}개")

    elif args.text:
        print("[경수] 텍스트 스캔 중...")
        result = scanner.scan_prompt_injection(args.text, source="cli_input")
        print(json.dumps(result, indent=2, ensure_ascii=False))

    else:
        print("[경수] 전체 프로젝트 스캔 시작...")
        result = scanner.scan_directory(_root)
        print(f"✅ 스캔 완료: {result['total_files_scanned']}개 파일")

    # 리포트 생성
    report_path = scanner.generate_report(args.output)
    print(f"\n📊 리포트 저장됨: {report_path}")

    # 텔레그램 알림
    if args.notify and scanner.threat_level in ["HIGH", "CRITICAL"]:
        summary = scanner._generate_summary(scanner.scan_results)
        message = f"""🚨 [경수] 보안 스캔 경고

위협 레벨: {scanner.threat_level}
발견된 위협: {summary['total_findings']}개
위험 파일: {summary['critical_files_count']}개

리포트: {report_path}
"""
        send_telegram_message(message)


if __name__ == "__main__":
    # UTF-8 encoding for Windows
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")

    main()
