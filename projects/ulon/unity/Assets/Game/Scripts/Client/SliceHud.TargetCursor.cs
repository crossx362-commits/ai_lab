using UnityEngine;
using Ulon.Shared;
using Ulon.Server;

namespace Ulon.Client
{
    public sealed partial class SliceHud : MonoBehaviour
    {
        TargetKind tgtKind;
        int tgtExtra;
        bool tgtArmed;
        ResourceNode tgtNode;

        static Rect TargetPromptRect =>
            new Rect((Screen.width - 420f) * 0.5f, Screen.height - 54f - 28f, 420f, 24f);

        /// <summary>게이트·MCP가 같이 읽는다. 지정 모드가 켜져 있는가.</summary>
        public static bool IsTargeting
        {
            get
            {
                var hud = FindAnyObjectByType<SliceHud>();
                return hud != null && hud.tgtKind != TargetKind.None && !TargetKinds.NcHide;
            }
        }

        public static string TargetPrompt()
        {
            var hud = FindAnyObjectByType<SliceHud>();
            if (hud == null)
                return TargetKinds.PromptOf(TargetKind.None);
            return TargetKinds.PromptOf(hud.tgtKind);
        }

        public static bool TargetConsumesClick()
        {
            var hud = FindAnyObjectByType<SliceHud>();
            return hud != null && hud.tgtKind != TargetKind.None && !TargetKinds.NcHide;
        }

        public static void BeginTarget(TargetKind kind, int extra = 0)
        {
            var hud = FindAnyObjectByType<SliceHud>();
            if (hud == null)
                return;
            hud.StartTarget(kind, extra);
        }

        public static void CancelTarget()
        {
            var hud = FindAnyObjectByType<SliceHud>();
            if (hud != null)
                hud.ClearTarget();
        }

        public static void ConfirmWorldTarget(UnityEngine.Object target)
        {
            var hud = FindAnyObjectByType<SliceHud>();
            if (hud == null || hud.tgtKind == TargetKind.None || TargetKinds.NcHide || target == null)
                return;
            var world = OfflineWorld.Instance;
            var me = world != null ? world.Player : null;
            var net = me != null ? me.GetComponent<NetAvatar>() : null;

            if (hud.tgtKind == TargetKind.Gather)
            {
                var node = AsNode(target);
                if (node == null)
                    return;
                hud.tgtNode = node;
                hud.tgtArmed = true;
                Gather(net);
                hud.ClearTarget();
                return;
            }

            var body = AsBody(target);
            if (body == null)
                return;
            hud.BindSelected(net, me, body);
            hud.tgtArmed = true;
            TargetKind kind = hud.tgtKind;
            int extra = hud.tgtExtra;
            if (kind == TargetKind.Heal)
                Bandage(net);
            else if (kind == TargetKind.Spell)
                Cast(net, (SpellId)extra);
            else if (kind == TargetKind.Interact)
                RunInteract(net, (TargetInteract)extra);
            hud.ClearTarget();
        }

        public static void TryConfirmPointer()
        {
            var hud = FindAnyObjectByType<SliceHud>();
            if (hud == null || hud.tgtKind == TargetKind.None || TargetKinds.NcHide)
                return;
            Vector2 gui = GuiMouse();
            if (hud.OverHud(gui) && !TargetPromptRect.Contains(gui))
                return;
            Camera cam = Camera.main;
            if (cam == null)
                return;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f))
                return;
            if (hud.tgtKind == TargetKind.Gather)
            {
                var node = hit.collider.GetComponentInParent<ResourceNode>();
                if (node != null)
                    ConfirmWorldTarget(node);
                return;
            }
            var body = hit.collider.GetComponentInParent<WorldBody>();
            if (body != null)
                ConfirmWorldTarget(body);
        }

        void OnDisable()
        {
            ClearTarget();
        }

        void StartTarget(TargetKind kind, int extra)
        {
            if (TargetKinds.NcHide || kind == TargetKind.None)
            {
                ClearTarget();
                return;
            }
            CloseContext();
            tgtKind = kind;
            tgtExtra = extra;
            tgtArmed = false;
            tgtNode = null;
            Cursor.visible = false;
        }

        void ClearTarget()
        {
            tgtKind = TargetKind.None;
            tgtExtra = 0;
            tgtArmed = false;
            tgtNode = null;
            Cursor.visible = true;
        }

        static bool EnterTarget(TargetKind kind, int extra = 0)
        {
            var hud = FindAnyObjectByType<SliceHud>();
            if (hud == null || TargetKinds.NcHide)
                return true;
            if (hud.tgtArmed && hud.tgtKind == kind && hud.tgtExtra == extra)
            {
                hud.tgtArmed = false;
                return false;
            }
            hud.StartTarget(kind, extra);
            return true;
        }

        void BindSelected(NetAvatar net, WorldBody me, WorldBody target)
        {
            if (me == null || target == null)
                return;
            OfflineWorld.Instance?.Select(me, target);
            var nob = target.GetComponent<FishNet.Object.NetworkObject>();
            if (net != null && net.IsClientInitialized && nob != null)
                net.RpcSelect(nob);
        }

        static WorldBody AsBody(UnityEngine.Object target)
        {
            if (target is WorldBody body)
                return body;
            var c = target as Component;
            if (c != null)
                return c.GetComponentInParent<WorldBody>();
            var go = target as GameObject;
            return go != null ? go.GetComponentInParent<WorldBody>() : null;
        }

        static ResourceNode AsNode(UnityEngine.Object target)
        {
            if (target is ResourceNode node)
                return node;
            var c = target as Component;
            if (c != null)
                return c.GetComponentInParent<ResourceNode>();
            var go = target as GameObject;
            return go != null ? go.GetComponentInParent<ResourceNode>() : null;
        }

        static void RunInteract(NetAvatar net, TargetInteract act)
        {
            if (act == TargetInteract.Evaluate) Evaluate(net);
            else if (act == TargetInteract.Lore) Lore(net);
            else if (act == TargetInteract.Vet) Vet(net);
            else if (act == TargetInteract.Scroll) UseScroll(net);
            else if (act == TargetInteract.Peace) Peace(net);
        }

        void DrawTargetCursor()
        {
            if (tgtKind == TargetKind.None || TargetKinds.NcHide)
                return;
            string prompt = TargetKinds.PromptOf(tgtKind);
            if (string.IsNullOrEmpty(prompt))
                return;

            Area(TargetPromptRect, () =>
            {
                GUILayout.BeginHorizontal();
                Color wasLabel = GUI.color;
                GUI.color = new Color(1f, 0.82f, 0.18f, 1f);
                GUILayout.Label("+");
                GUI.color = wasLabel;
                GUILayout.Label(prompt + "  (" + TargetKinds.CancelHint + ")");
                GUILayout.EndHorizontal();
            });

            Vector2 m = Event.current != null ? Event.current.mousePosition
                : new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            Color was = GUI.color;
            GUI.color = new Color(1f, 0.82f, 0.18f, 0.95f);
            const float arm = 11f;
            const float thick = 2f;
            GUI.DrawTexture(new Rect(m.x - arm, m.y - thick * 0.5f, arm * 2f, thick), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(m.x - thick * 0.5f, m.y - arm, thick, arm * 2f), Texture2D.whiteTexture);
            GUI.color = was;
        }
    }
}
