import { flattenLab, type LabNode } from "./lab-tree";

export type LabProject = {
  id: string;
  name: string;
  path: string;
  git: string;
  kind: string;
  note: string;
};

export const LAB_ROOT = "D:\\ai_lab";
export const LAB_GITHUB = "crossx362-commits/ai_lab";
export const LAB_BRANCH = "master";

const TOP_IDS = [
  "petnna",
  "ai-team",
  "ashes",
  "homepage",
  "ulon",
  "bboggl",
  "picker",
  "chinaguard",
  "geoguard",
  "docs",
  "loop",
];

function toProject(node: LabNode): LabProject {
  return {
    id: node.id,
    name: node.name,
    path: node.win,
    git: node.git,
    kind: node.note || "dir",
    note: node.note || "",
  };
}

export const LAB_PROJECTS: LabProject[] = [
  toProject(flattenLab().find((n) => n.id === "lab")!),
  ...TOP_IDS.map((id) => flattenLab().find((n) => n.id === id)!).filter(Boolean).map(toProject),
];
