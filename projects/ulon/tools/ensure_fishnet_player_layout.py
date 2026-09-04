#!/usr/bin/env python3
"""Keep FishNet serialized fields identical in editor and player.

PackageCache FishNet hides two SerializeFields behind #if UNITY_EDITOR.
Player builds then write/read a different MonoBehaviour layout and die with
Data/level0 is corrupted / Transfer_String. Re-apply after package restore.
"""
from __future__ import annotations
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
CACHE = ROOT / "unity" / "Library" / "PackageCache"
REPLACEMENTS = [
    (
        "Runtime/Managing/NetworkManager.cs",
        """        #if UNITY_EDITOR
        /// <summary>
        /// True to refresh the DefaultPrefabObjects collection whenever the editor enters play mode. This is an attempt to alleviate the DefaultPrefabObjects scriptable object not refreshing when using multiple editor applications such as ParrelSync.
        /// </summary>
        [Tooltip("True to refresh the DefaultPrefabObjects collection whenever the editor enters play mode. This is an attempt to alleviate the DefaultPrefabObjects scriptable object not refreshing when using multiple editor applications such as ParrelSync.")]
        [SerializeField]
        private bool _refreshDefaultPrefabs = false;
        #endif
""",
        """        /// <summary>
        /// True to refresh the DefaultPrefabObjects collection whenever the editor enters play mode. This is an attempt to alleviate the DefaultPrefabObjects scriptable object not refreshing when using multiple editor applications such as ParrelSync.
        /// Kept outside UNITY_EDITOR so player/editor serialization layouts match (level0 string read).
        /// </summary>
        [Tooltip("True to refresh the DefaultPrefabObjects collection whenever the editor enters play mode. This is an attempt to alleviate the DefaultPrefabObjects scriptable object not refreshing when using multiple editor applications such as ParrelSync.")]
        [SerializeField]
        private bool _refreshDefaultPrefabs = false;
""",
    ),
    (
        "Runtime/Object/NetworkBehaviour/NetworkBehaviour.cs",
        """        #if UNITY_EDITOR
        /// <summary>
        /// NetworkObject automatically added or discovered during edit time.
        /// </summary>
        [SerializeField]
        [HideInInspector]
        private NetworkObject _addedNetworkObject;
        #endif
""",
        """        /// <summary>
        /// NetworkObject automatically added or discovered during edit time.
        /// Kept outside UNITY_EDITOR so player/editor serialization layouts match (level0 string read).
        /// </summary>
        [SerializeField]
        [HideInInspector]
        private NetworkObject _addedNetworkObject;
""",
    ),
]


def main() -> int:
    roots = list(CACHE.glob("com.firstgeargames.fishnet@*"))
    if not roots:
        print("no FishNet package cache", file=sys.stderr)
        return 2
    changed = 0
    for root in roots:
        for rel, old, new in REPLACEMENTS:
            path = root / rel
            if not path.exists():
                continue
            text = path.read_text(encoding="utf-8")
            if new.strip() in text and old not in text:
                print("already patched", path)
                continue
            if old not in text:
                print("pattern missing", path, file=sys.stderr)
                return 3
            path.write_text(text.replace(old, new, 1), encoding="utf-8")
            print("patched", path)
            changed += 1
    print("ok changed", changed)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
