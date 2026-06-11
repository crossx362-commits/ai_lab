"""
notion_report_manager.py — AI 팀 통합 리서치 리포트 관리

Notion 데이터베이스에서 에이전트 작업을 읽고, 결과를 기록합니다.
"""
import os
import json
import datetime
from typing import Dict, List, Any


def _get_notion_token() -> str:
    """Notion API 토큰 가져오기."""
    return os.getenv("NOTION_API_KEY", "") or os.getenv("NOTION_TOKEN", "")


def _get_report_database_id() -> str:
    """AI 팀 통합 리포트 데이터베이스 ID."""
    return os.getenv("NOTION_DATABASE_ID", "") or os.getenv("NOTION_REPORT_DB_ID", "")


class NotionReportManager:
    """AI 팀 통합 리서치 리포트 관리자."""

    def __init__(self):
        self.token = _get_notion_token()
        self.database_id = _get_report_database_id()
        self.headers = {
            "Authorization": f"Bearer {self.token}",
            "Content-Type": "application/json",
            "Notion-Version": "2022-06-28"
        }

    def get_pending_tasks(self, agent_name: str = None) -> List[Dict]:
        """대기 중인 작업 목록 가져오기.

        Args:
            agent_name: 특정 에이전트의 작업만 필터링 (None이면 전체)

        Returns:
            대기 중인 작업 목록
        """
        if not self.token or not self.database_id:
            print("  [Notion] API 토큰 또는 데이터베이스 ID 없음")
            return []

        try:
            import urllib.request

            # Notion 데이터베이스 쿼리
            filter_query = {
                "filter": {
                    "property": "Status",
                    "status": {
                        "equals": "Not started"
                    }
                }
            }

            # 특정 에이전트 필터링
            if agent_name:
                filter_query["filter"] = {
                    "and": [
                        filter_query["filter"],
                        {
                            "property": "Agent",
                            "select": {
                                "equals": agent_name
                            }
                        }
                    ]
                }

            url = f"https://api.notion.com/v1/databases/{self.database_id}/query"
            payload = json.dumps(filter_query).encode("utf-8")

            req = urllib.request.Request(url, data=payload, headers=self.headers, method="POST")
            with urllib.request.urlopen(req, timeout=30) as response:
                data = json.loads(response.read())

            tasks = []
            for page in data.get("results", []):
                task = self._parse_task_page(page)
                if task:
                    tasks.append(task)

            return tasks

        except Exception as e:
            print(f"  [Notion] 작업 목록 조회 실패: {e}")
            return []

    def _parse_task_page(self, page: Dict) -> Dict:
        """Notion 페이지에서 작업 정보 파싱."""
        try:
            props = page.get("properties", {})

            task = {
                "id": page.get("id", ""),
                "title": self._get_title(props.get("Name", {})),
                "agent": self._get_select(props.get("Agent", {})),
                "status": self._get_status(props.get("Status", {})),
                "priority": self._get_select(props.get("Priority", {})),
                "description": self._get_rich_text(props.get("Description", {})),
                "deadline": self._get_date(props.get("Deadline", {})),
                "url": page.get("url", "")
            }

            return task

        except Exception as e:
            print(f"  [Notion] 페이지 파싱 실패: {e}")
            return {}

    def update_task_status(self, task_id: str, status: str, result: str = None) -> bool:
        """작업 상태 업데이트.

        Args:
            task_id: Notion 페이지 ID
            status: 상태 ("In progress", "Done", "Failed" 등)
            result: 작업 결과 (선택)

        Returns:
            성공 여부
        """
        if not self.token:
            return False

        try:
            import urllib.request

            properties = {
                "Status": {
                    "status": {
                        "name": status
                    }
                }
            }

            # 결과 추가
            if result:
                properties["Result"] = {
                    "rich_text": [
                        {
                            "text": {
                                "content": result[:2000]  # Notion 제한
                            }
                        }
                    ]
                }

            # 완료 시각 기록
            if status == "Done":
                properties["Completed"] = {
                    "date": {
                        "start": datetime.datetime.now().isoformat()
                    }
                }

            url = f"https://api.notion.com/v1/pages/{task_id}"
            payload = json.dumps({"properties": properties}).encode("utf-8")

            req = urllib.request.Request(url, data=payload, headers=self.headers, method="PATCH")
            with urllib.request.urlopen(req, timeout=30) as response:
                response.read()

            print(f"  [Notion] 작업 상태 업데이트: {status}")
            return True

        except Exception as e:
            print(f"  [Notion] 상태 업데이트 실패: {e}")
            return False

    def create_report_entry(self, agent_name: str, task_title: str, result: str,
                           metadata: Dict = None) -> bool:
        """새 리포트 항목 생성.

        Args:
            agent_name: 에이전트 이름
            task_title: 작업 제목
            result: 작업 결과
            metadata: 추가 메타데이터

        Returns:
            성공 여부
        """
        if not self.token or not self.database_id:
            return False

        try:
            import urllib.request

            properties = {
                "제목": {
                    "title": [
                        {
                            "text": {
                                "content": task_title[:100]
                            }
                        }
                    ]
                },
                "에이전트": {
                    "select": {
                        "name": agent_name
                    }
                },
                "상태": {
                    "select": {
                        "name": "완료"
                    }
                },
                "내용": {
                    "rich_text": [
                        {
                            "text": {
                                "content": "본문 리포트 참조"
                            }
                        }
                    ]
                },
                "날짜": {
                    "date": {
                        "start": datetime.datetime.now().isoformat()
                    }
                }
            }

            # 메타데이터 추가
            if metadata:
                for key, value in metadata.items():
                    if key == "url" and value:
                        properties["URL"] = {
                            "url": value
                        }
                    elif key == "priority":
                        properties["우선순위"] = {
                            "select": {
                                "name": value
                            }
                        }

            # Parse markdown and build blocks for children
            children_blocks = []
            lines = result.split("\n")
            in_code_block = False
            code_lines = []

            for line in lines:
                stripped = line.strip()
                
                # Code blocks (like Mermaid)
                if stripped.startswith("```"):
                    if in_code_block:
                        in_code_block = False
                        lang = code_lines[0] if code_lines else "plain text"
                        if lang.startswith("mermaid"):
                            lang = "mermaid"
                        elif lang.startswith("python"):
                            lang = "python"
                        else:
                            lang = "plain text"
                        
                        code_content = "\n".join(code_lines[1:])
                        children_blocks.append({
                            "object": "block",
                            "type": "code",
                            "code": {
                                "language": lang,
                                "rich_text": [{"type": "text", "text": {"content": code_content[:2000]}}]
                            }
                        })
                        code_lines = []
                    else:
                        in_code_block = True
                        code_lines.append(stripped.replace("```", ""))
                    continue

                if in_code_block:
                    code_lines.append(line)
                    continue

                # Empty lines
                if not stripped:
                    continue

                # Headings
                if stripped.startswith("### "):
                    children_blocks.append({
                        "object": "block",
                        "type": "heading_3",
                        "heading_3": {
                            "rich_text": [{"type": "text", "text": {"content": stripped[4:]}}]
                        }
                    })
                elif stripped.startswith("## "):
                    children_blocks.append({
                        "object": "block",
                        "type": "heading_2",
                        "heading_2": {
                            "rich_text": [{"type": "text", "text": {"content": stripped[3:]}}]
                        }
                    })
                elif stripped.startswith("# "):
                    children_blocks.append({
                        "object": "block",
                        "type": "heading_1",
                        "heading_1": {
                            "rich_text": [{"type": "text", "text": {"content": stripped[2:]}}]
                        }
                    })
                # Tables
                elif stripped.startswith("|"):
                    # Split rows, ignoring boundary lines like |---|---|
                    if "---" in stripped:
                        continue
                    cells = [c.strip() for c in stripped.split("|")[1:-1]]
                    if not cells:
                        continue
                    
                    # If the last block is a table, append row to it
                    if children_blocks and children_blocks[-1]["type"] == "table":
                        children_blocks[-1]["table"]["rows"].append({
                            "type": "table_row",
                            "table_row": {
                                "cells": [[{"type": "text", "text": {"content": cell}}] for cell in cells]
                            }
                        })
                    else:
                        children_blocks.append({
                            "object": "block",
                            "type": "table",
                            "table": {
                                "table_width": len(cells),
                                "has_column_header": True,
                                "has_row_header": False,
                                "rows": [
                                    {
                                        "type": "table_row",
                                        "table_row": {
                                            "cells": [[{"type": "text", "text": {"content": cell}}] for cell in cells]
                                        }
                                    }
                                ]
                            }
                        })
                # Bullet list items
                elif stripped.startswith("* ") or stripped.startswith("- "):
                    children_blocks.append({
                        "object": "block",
                        "type": "bulleted_list_item",
                        "bulleted_list_item": {
                            "rich_text": [{"type": "text", "text": {"content": stripped[2:]}}]
                        }
                    })
                # Callouts/Quotes
                elif stripped.startswith("> "):
                    children_blocks.append({
                        "object": "block",
                        "type": "callout",
                        "callout": {
                            "icon": {"type": "emoji", "emoji": "💡"},
                            "rich_text": [{"type": "text", "text": {"content": stripped[2:]}}]
                        }
                    })
                # Paragraph
                else:
                    # Parse simple bold formatting **text**
                    content_parts = []
                    parts = stripped.split("**")
                    for idx, part in enumerate(parts):
                        if idx % 2 == 1:
                            content_parts.append({
                                "type": "text",
                                "text": {"content": part},
                                "annotations": {"bold": True}
                            })
                        else:
                            content_parts.append({
                                "type": "text",
                                "text": {"content": part}
                            })
                    
                    children_blocks.append({
                        "object": "block",
                        "type": "paragraph",
                        "paragraph": {
                            "rich_text": content_parts if content_parts else [{"type": "text", "text": {"content": stripped[:2000]}}]
                        }
                    })

            # For Notion API, we must separate table block rows creation. 
            # In Notion API v1, children of pages cannot define rows inside table directly, 
            # we need to create table block first, then append table_row blocks as children.
            # However, to keep it simple, we convert markdown tables into structured bulleted text lists or standard paragraphs to render beautifully.
            # Let's fallback tables to structured formatting or bulleted lists for maximum Notion compatibility.
            final_blocks = []
            for block in children_blocks:
                if block["type"] == "table":
                    # Convert table representation to callout table block style
                    rows = block["table"]["rows"]
                    table_text = ""
                    for r_idx, row in enumerate(rows):
                        cells = [cell[0]["text"]["content"] for cell in row["table_row"]["cells"]]
                        table_text += " | ".join(cells) + "\n"
                        if r_idx == 0:
                            table_text += "---" * len(cells) + "\n"
                    
                    final_blocks.append({
                        "object": "block",
                        "type": "code",
                        "code": {
                            "language": "plain text",
                            "rich_text": [{"type": "text", "text": {"content": table_text[:2000]}}]
                        }
                    })
                else:
                    final_blocks.append(block)

            # Limit blocks to 100 per request (Notion API limit)
            children_blocks = final_blocks[:95]

            url = f"https://api.notion.com/v1/pages"
            payload = json.dumps({
                "parent": {"database_id": self.database_id},
                "properties": properties,
                "children": children_blocks
            }).encode("utf-8")

            req = urllib.request.Request(url, data=payload, headers=self.headers, method="POST")
            with urllib.request.urlopen(req, timeout=30) as response:
                response.read()

            print(f"  [Notion] 리포트 항목 생성: {task_title}")
            return True

        except Exception as e:
            print(f"  [Notion] 리포트 생성 실패: {e}")
            return False

    # 헬퍼 메서드
    def _get_title(self, prop: Dict) -> str:
        try:
            return prop.get("title", [{}])[0].get("text", {}).get("content", "")
        except:
            return ""

    def _get_select(self, prop: Dict) -> str:
        try:
            return prop.get("select", {}).get("name", "")
        except:
            return ""

    def _get_status(self, prop: Dict) -> str:
        try:
            return prop.get("status", {}).get("name", "")
        except:
            return ""

    def _get_rich_text(self, prop: Dict) -> str:
        try:
            texts = prop.get("rich_text", [])
            return " ".join([t.get("text", {}).get("content", "") for t in texts])
        except:
            return ""

    def _get_date(self, prop: Dict) -> str:
        try:
            return prop.get("date", {}).get("start", "")
        except:
            return ""


# 간편 함수
def get_my_tasks(agent_name: str) -> List[Dict]:
    """내 대기 중인 작업 가져오기."""
    manager = NotionReportManager()
    return manager.get_pending_tasks(agent_name)


def report_task_done(task_id: str, result: str) -> bool:
    """작업 완료 보고."""
    manager = NotionReportManager()
    return manager.update_task_status(task_id, "Done", result)


def report_task_failed(task_id: str, error: str) -> bool:
    """작업 실패 보고."""
    manager = NotionReportManager()
    return manager.update_task_status(task_id, "Failed", error)


if __name__ == "__main__":
    # 테스트
    manager = NotionReportManager()

    if manager.token:
        print("=== Notion 연동 테스트 ===\n")

        # 루나의 대기 작업 확인
        tasks = manager.get_pending_tasks("루나")
        print(f"루나 대기 작업: {len(tasks)}개")

        for task in tasks[:3]:
            print(f"\n- {task.get('title', 'N/A')}")
            print(f"  상태: {task.get('status', 'N/A')}")
            print(f"  우선순위: {task.get('priority', 'N/A')}")
    else:
        print("[ERROR] NOTION_TOKEN 환경변수가 설정되지 않았습니다.")
        print("\n설정 방법:")
        print("1. Notion Integration 생성: https://www.notion.so/my-integrations")
        print("2. .env 파일에 추가:")
        print("   NOTION_TOKEN=secret_xxxxx")
        print("   NOTION_REPORT_DB_ID=xxxxx")
        print("3. 재암호화:")
        print("   python projects/ai-team/_shared/env_crypto.py encrypt .env .env.encrypted")
