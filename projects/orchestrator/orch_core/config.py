"""설정 로드 + 경로 해석. 설정값이 실제 디스크와 맞는지 여기서 먼저 실패시킨다."""

from __future__ import annotations

import json
import os
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent  # projects/orchestrator
CONFIG_PATH = ROOT / "config.json"
STATE_DIR = ROOT / "state"
LOG_DIR = ROOT / "logs"


class ConfigError(RuntimeError):
    pass


@dataclass
class Target:
    name: str
    repo: Path
    unity_project: Path
    unity_version: str
    unity_editor: Path
    unity_timeout_sec: int
    unity_stall_sec: int
    allowed_write_globs: list[str]
    protected_globs: list[str]
    test_platforms: list[str]
    test_guard_globs: list[str]
    kind: str = "unity"                  # unity | blender — 판정 축이 갈린다
    blender_bin: Path | None = None
    blender_check: str = "orch_check.py"  # 저장소 안의 검증 장치(protected_globs로 보호할 것)
    blender_timeout_sec: int = 300

    @property
    def capability(self) -> str:
        """이 target의 구현자에게 필요한 능력. 이름이 아니라 능력으로 배정한다(providers.py)."""
        return "BLENDER" if self.kind == "blender" else "CODING"

    @property
    def engine_label(self) -> str:
        return f"Blender({self.blender_bin})" if self.kind == "blender" else f"Unity {self.unity_version}"

    @property
    def prompt_rules(self) -> str:
        """프롬프트의 [반드시 지킬 것] 중 엔진에 따라 갈리는 줄."""
        if self.kind == "blender":
            return (f"- Blender 배치(bpy) 프로젝트다. build.py 의 build() 가 **빈 씬에서** 실제 메시를 만들어야 하고,\n"
                    f"  tests/test_*.py 의 test_* 함수가 전부 통과해야 한다(검증 장치가 factory 씬에서 돌린다).\n"
                    f"- {self.blender_check} (검증 장치)는 수정하지 마라.")
        return (f"- Unity {self.unity_version} 프로젝트다. C# 코드가 컴파일되어야 하고 테스트가 통과해야 한다.\n"
                f"- Assets/Orch/ 아래(검증 장치)는 수정하지 마라.")


@dataclass
class AgentConfig:
    name: str
    type: str
    bin: str
    model: str | None
    sandbox_mode: str
    timeout_sec: int
    args: list[str]
    extra_config: list[str]
    permission_mode: str
    capabilities: list[str]      # 이름이 아니라 능력으로 배정한다(providers.py)
    enabled: bool


@dataclass
class Config:
    raw: dict
    default_target: str
    max_attempts: int
    unity_slots: int
    ladder: list[str]
    research_agent: str | None
    planner: str
    reviewer: str | None
    log_summarizer: dict
    prompt_max_chars: int

    def target(self, name: str | None = None) -> Target:
        name = name or self.default_target
        t = self.raw.get("targets", {}).get(name)
        if t is None:
            raise ConfigError(f"알 수 없는 target: {name}")
        repo = (ROOT / t["repo"]).resolve()
        if not repo.is_dir():
            raise ConfigError(f"target repo 없음: {repo}")
        kind = t.get("kind", "unity")
        if kind == "blender":
            # Blender 축: 유니티 필드는 비워 두고 검증 장치·실행 파일이 실제로 있는지만 본다.
            bbin = Path(t.get("blender_bin") or self.raw.get("blender_bin", ""))
            check = t.get("blender_check", "orch_check.py")
            if not bbin.is_file():
                raise ConfigError(f"Blender 실행 파일 없음: {bbin}")
            if not (repo / check).is_file():
                raise ConfigError(f"Blender 검증 장치 없음: {repo / check}")
            return Target(
                name=name, repo=repo, unity_project=repo, unity_version="", unity_editor=bbin,
                unity_timeout_sec=int(t.get("blender_timeout_sec", 300)), unity_stall_sec=0,
                allowed_write_globs=list(t.get("allowed_write_globs", ["**"])),
                protected_globs=list(t.get("protected_globs", [check])),
                test_platforms=[], test_guard_globs=list(t.get("test_guard_globs", ["tests/**"])),
                kind="blender", blender_bin=bbin, blender_check=check,
                blender_timeout_sec=int(t.get("blender_timeout_sec", 300)),
            )
        unity_project = (ROOT / t["unity_project"]).resolve()
        editor = Path(
            self.raw["unity_editor_template"].format(version=t["unity_version"])
        )
        if not (unity_project / "ProjectSettings" / "ProjectVersion.txt").is_file():
            raise ConfigError(f"Unity 프로젝트가 아님: {unity_project}")
        if not editor.is_file():
            raise ConfigError(f"Unity 에디터 없음: {editor}")
        return Target(
            name=name,
            repo=repo,
            unity_project=unity_project,
            unity_version=t["unity_version"],
            unity_editor=editor,
            unity_timeout_sec=int(t.get("unity_timeout_sec", 1800)),
            # 로그가 이만큼 안 자라면 멎은 것으로 본다(타임아웃을 다 태우지 않는다).
            unity_stall_sec=int(t.get("unity_stall_sec", 180)),
            allowed_write_globs=list(t.get("allowed_write_globs", ["**"])),
            protected_globs=list(t.get("protected_globs", [])),
            test_platforms=list(t.get("test_platforms", [])),
            test_guard_globs=list(t.get("test_guard_globs", [])),
        )

    def agent(self, name: str) -> AgentConfig:
        a = self.raw.get("agents", {}).get(name)
        if a is None:
            raise ConfigError(f"알 수 없는 agent: {name}")
        return AgentConfig(
            name=name,
            type=a.get("type", name),
            bin=a.get("bin", name),
            model=a.get("model"),
            sandbox_mode=a.get("sandbox_mode", "workspace-write"),
            timeout_sec=int(a.get("timeout_sec", 900)),
            args=list(a.get("args", [])),
            extra_config=list(a.get("extra_config", [])),
            permission_mode=a.get("permission_mode", "acceptEdits"),
            capabilities=list(a.get("capabilities", [])),
            enabled=bool(a.get("enabled", True)),
        )


def load(path: Path | None = None) -> Config:
    # ORCH_CONFIG로 설정을 갈아끼울 수 있게 둔다 — 게이트 자체를 시험하는 판(네거티브 컨트롤)에 쓴다.
    if path is not None:
        p = path
    elif os.getenv("ORCH_CONFIG"):
        p = Path(os.environ["ORCH_CONFIG"]).expanduser().resolve()
    else:
        p = CONFIG_PATH
    if not p.is_file():
        raise ConfigError(f"설정 파일 없음: {p}")
    raw = json.loads(p.read_text(encoding="utf-8"))
    STATE_DIR.mkdir(exist_ok=True)
    LOG_DIR.mkdir(exist_ok=True)
    return Config(
        raw=raw,
        default_target=raw.get("default_target", "sandbox"),
        max_attempts=int(raw.get("max_attempts", 3)),
        unity_slots=int(raw.get("unity_slots", 2)),
        ladder=list(raw.get("ladder", ["codex"])),
        research_agent=raw.get("research_agent"),
        planner=raw.get("planner", "astra"),
        reviewer=raw.get("reviewer"),
        log_summarizer=dict(raw.get("log_summarizer", {})),
        prompt_max_chars=int(raw.get("prompt_max_chars", 48000)),
    )


def env_flag(name: str, default: bool = False) -> bool:
    v = os.getenv(name)
    if v is None:
        return default
    return v.strip().lower() in ("1", "true", "yes", "on")
