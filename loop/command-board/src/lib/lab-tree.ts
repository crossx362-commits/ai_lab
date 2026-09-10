export type LabNode = {
  id: string;
  name: string;
  git: string;
  win: string;
  kind: "dir" | "file";
  note?: string;
  children?: LabNode[];
};

const W = "D:\\ai_lab";

function n(
  id: string,
  name: string,
  git: string,
  note?: string,
  children?: LabNode[],
): LabNode {
  const win = git ? W + "\\" + git.replaceAll("/", "\\") : W;
  return { id, name, git, win, kind: "dir", note, children };
}

export const LAB_TREE: LabNode = n("lab", "ai_lab", "", "모노레포 · master", [
  n("rules", "규칙", "", "에이전트 헌장", [
    n("agents-md", "AGENTS.md", "AGENTS.md"),
    n("claude-md", "CLAUDE.md", "CLAUDE.md"),
    n("directives", "DIRECTIVES.md", "DIRECTIVES.md"),
    n("handbook", "HANDBOOK.md", "HANDBOOK.md"),
  ]),
  n("projects", "projects", "projects", "제품", [
    n("petnna", "petnna", "projects/petnna", "PWA · Vercel", [
      n("petnna-js", "js", "projects/petnna/js"),
      n("petnna-api", "api", "projects/petnna/api"),
      n("petnna-sql", "migrations", "projects/petnna/migrations"),
    ]),
    n("ai-team", "ai-team", "projects/ai-team", "에이전트 프레임워크", [
      n("shared", "_shared", "projects/ai-team/_shared", "공용 모듈"),
      n("harness", "harness", "projects/ai-team/harness", "읽기전용 검증"),
      n("skills", "skills", "projects/ai-team/skills", "봇 로스터", [
        n("yewon", "예원_CEO", "projects/ai-team/skills/예원_CEO"),
        n("youngsuk", "영숙_비서", "projects/ai-team/skills/영숙_비서"),
        n("bomi", "봄이_QA", "projects/ai-team/skills/봄이_QA"),
        n("suri", "수리_개발자", "projects/ai-team/skills/수리_개발자"),
        n("teo", "테오_테스트", "projects/ai-team/skills/테오_테스트"),
        n("baekho", "백호_백엔드", "projects/ai-team/skills/백호_백엔드"),
        n("mio", "미오_디자인", "projects/ai-team/skills/미오_디자인"),
        n("namu", "나무_기획", "projects/ai-team/skills/나무_기획"),
        n("maru", "마루_게임개발", "projects/ai-team/skills/마루_게임개발"),
        n("byul", "별이_이미지품질", "projects/ai-team/skills/별이_이미지품질"),
      ]),
      n("scripts", "scripts", "projects/ai-team/scripts"),
      n("tests", "tests", "projects/ai-team/tests"),
    ]),
    n("ashes", "ashes-to-stars", "projects/ashes-to-stars", "Unity 게임", [
      n("unity", "unity", "projects/ashes-to-stars/unity"),
      n("art", "art", "projects/ashes-to-stars/art"),
    ]),
    n("homepage", "homepage", "projects/homepage", "랩 사이트"),
    n("ulon", "ulon", "projects/ulon"),
    n("bboggl", "bboggl", "projects/bboggl"),
    n("picker", "ai-model-picker", "projects/ai-model-picker"),
  ]),
  n("chinaguard", "ChinaGuard", "ChinaGuard", "C# 방화벽"),
  n("geoguard", "GeoGuard", "GeoGuard", "C# Geo IP"),
  n("docs", "docs", "docs", "아키텍처·게임스펙"),
  n("loop", "loop", "loop", "보드/루프 스크립트"),
  n("api", "api", "api", "서버리스"),
  n("tools", "tools", "tools"),
  n("reports", "reports", "reports"),
  n("output", "output", "output", "런타임 산출 · git 제외 다수"),
]);

export function flattenLab(node: LabNode = LAB_TREE): LabNode[] {
  const out: LabNode[] = [node];
  for (const child of node.children ?? []) out.push(...flattenLab(child));
  return out;
}

export function findLab(id: string): LabNode | undefined {
  return flattenLab().find((x) => x.id === id);
}
