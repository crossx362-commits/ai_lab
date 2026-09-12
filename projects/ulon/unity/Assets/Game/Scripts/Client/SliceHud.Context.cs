using UnityEngine;
using Ulon.Shared;
using Ulon.Server;

namespace Ulon.Client
{
    public sealed partial class SliceHud : MonoBehaviour
    {
        ContextKind ctxKind;
        string ctxId = "";
        WorldBody ctxPet;
        Vector2 ctxScreen;
        Rect ctxRect;

        /// <summary>게이트·우클릭이 같이 읽는다. 열린 메뉴의 라벨.</summary>
        public static string[] ContextLabels()
        {
            var hud = FindAnyObjectByType<SliceHud>();
            if (hud == null)
                return ContextKinds.LabelsOf(ContextKind.None);
            return ContextKinds.LabelsOf(hud.ctxKind);
        }

        public static bool ContextConsumesClick()
        {
            var hud = FindAnyObjectByType<SliceHud>();
            if (hud == null || hud.ctxKind == ContextKind.None)
                return false;
            Vector2 gui = GuiMouse();
            return hud.ctxRect.width > 1f && hud.ctxRect.Contains(gui);
        }

        public static void CloseContext()
        {
            var hud = FindAnyObjectByType<SliceHud>();
            if (hud != null)
                hud.ClearContext();
        }

        public static void OpenWorldContext(ContextKind kind, UnityEngine.Object target, Vector2 screenMouse)
        {
            var hud = FindAnyObjectByType<SliceHud>();
            if (hud == null)
                return;
            hud.OpenContext(kind, target, screenMouse);
        }

        void ClearContext()
        {
            ctxKind = ContextKind.None;
            ctxId = "";
            ctxPet = null;
            ctxRect = default;
        }

        void OpenContext(ContextKind kind, UnityEngine.Object target, Vector2 screenMouse)
        {
            if (ContextKinds.NcHide || kind == ContextKind.None || target == null)
            {
                ClearContext();
                return;
            }
            ctxKind = kind;
            ctxScreen = screenMouse;
            ctxPet = target as WorldBody;
            var go = target as GameObject;
            if (go == null)
            {
                var mb = target as Component;
                go = mb != null ? mb.gameObject : null;
            }
            ctxId = go != null ? go.name : "";
        }

        void OpenWorldContextFromInput()
        {
            if (PersistDriver.Creating || PersistDriver.Frozen)
                return;
            Vector2 gui = GuiMouse();
            if (ctxKind != ContextKind.None && ctxRect.width > 1f && ctxRect.Contains(gui))
                return;
            if (OverHud(gui))
            {
                ClearContext();
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                ClearContext();
                return;
            }
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                ClearContext();
                return;
            }

            var trainer = hit.collider.GetComponentInParent<TrainerStation>();
            if (trainer != null)
            {
                OpenContext(ContextKind.Trainer, trainer, Input.mousePosition);
                return;
            }
            var plot = hit.collider.GetComponentInParent<HousePlotStation>();
            if (plot != null)
            {
                OpenContext(ContextKind.HousePlot, plot, Input.mousePosition);
                return;
            }
            var chest = hit.collider.GetComponentInParent<HouseChest>();
            if (chest != null)
            {
                OpenContext(ContextKind.HouseChest, chest, Input.mousePosition);
                return;
            }
            var vendor = hit.collider.GetComponentInParent<HouseVendor>();
            if (vendor != null)
            {
                OpenContext(ContextKind.HouseVendor, vendor, Input.mousePosition);
                return;
            }

            var world = OfflineWorld.Instance;
            var me = world != null ? world.Player : null;
            var body = hit.collider.GetComponentInParent<WorldBody>();
            if (me != null && body != null && !body.PetStabled
                && !string.IsNullOrEmpty(body.OwnerCharacterId)
                && body.OwnerCharacterId == me.CharacterId)
            {
                OpenContext(ContextKind.Pet, body, Input.mousePosition);
                return;
            }

            ClearContext();
        }

        static Vector2 GuiMouse()
            => new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);

        bool OverHud(Vector2 gui)
        {
            for (int i = 0; i < DrawnAreas.Count; i++)
            {
                if (DrawnAreas[i].Contains(gui))
                    return true;
            }
            return false;
        }

        void DrawContextMenu(WorldBody me, NetAvatar net)
        {
            if (ctxKind == ContextKind.None)
            {
                ctxRect = default;
                return;
            }
            string[] labels = ContextKinds.LabelsOf(ctxKind);
            if (labels.Length == 0)
            {
                ClearContext();
                return;
            }

            float h = 48f + labels.Length * 28f;
            Vector2 gui = new Vector2(ctxScreen.x, Screen.height - ctxScreen.y);
            Rect r = new Rect(gui.x, gui.y, 176f, h);
            r = ClampOutsideCombat(r);
            ctxRect = r;

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                && !r.Contains(Event.current.mousePosition))
            {
                ClearContext();
                return;
            }

            Area(r, () =>
            {
                GUILayout.Label(ContextKinds.TitleOf(ctxKind));
                if (ctxKind == ContextKind.Trainer && Btn("훈련 열기"))
                {
                    OpenTrainer(net, me);
                    ClearContext();
                }
                if (ctxKind == ContextKind.Pet)
                {
                    if (Btn("따라와")) { PetCommand(net, me, 0); ClearContext(); }
                    if (Btn("기다려")) { PetCommand(net, me, 1); ClearContext(); }
                    if (Btn("지켜라")) { PetCommand(net, me, 2); ClearContext(); }
                    if (Btn("펫공격")) { PetAttack(net); ClearContext(); }
                    if (Btn("이리와")) { PetCome(net); ClearContext(); }
                    if (Btn("놓아준다")) { PetRelease(net); ClearContext(); }
                }
                if (ctxKind == ContextKind.HousePlot && Btn("토지 청구"))
                {
                    ClaimHouse(net, me);
                    ClearContext();
                }
                if (ctxKind == ContextKind.HouseChest)
                {
                    if (Btn("잠금")) { HouseLockdown(net, me); ClearContext(); }
                    if (Btn("꺼내기")) { HouseTake(net, me); ClearContext(); }
                }
                if (ctxKind == ContextKind.HouseVendor)
                {
                    if (Btn("진열")) { HouseVendorList(net, me); ClearContext(); }
                    if (Btn("벤더 구매")) { HouseVendorBuy(net, me); ClearContext(); }
                }
            });
        }

        static Rect ClampOutsideCombat(Rect r)
        {
            // 커서가 중앙 전투 영역·우측 패널과 겹치면 상태 카드 아래로 고정한다(화면 규칙).
            r.x = 12f;
            r.y = 12f + 148f + 8f;
            if (r.Overlaps(CombatZone))
                r.x = Mathf.Max(8f, CombatZone.x - r.width - 8f);
            r.x = Mathf.Clamp(r.x, 8f, Mathf.Max(8f, Screen.width - r.width - 8f));
            r.y = Mathf.Clamp(r.y, 8f, Mathf.Max(8f, Screen.height - r.height - 8f));
            return r;
        }

        void OpenTrainer(NetAvatar net, WorldBody me)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcTrainer(ctxId);
            else
                OfflineWorld.Instance?.TryTrainer(me, OfflineWorld.FindTrainer(ctxId));
            panel = Panel.Nearby;
        }

        void PetCommand(NetAvatar net, WorldBody me, int mode)
        {
            WorldBody pet = ctxPet;
            if (pet == null)
                return;
            var pno = pet.GetComponent<FishNet.Object.NetworkObject>();
            if (net != null && net.IsClientInitialized && pno != null)
            {
                net.RpcPetCommand(pno, mode);
                return;
            }
            if (OfflineWorld.Instance == null)
                return;
            if (mode == 1)
                OfflineWorld.Instance.TryPetStay(me, pet);
            else if (mode == 2)
                OfflineWorld.Instance.TryPetGuard(me, pet);
            else
                OfflineWorld.Instance.TryPetFollow(me, pet);
        }

        void ClaimHouse(NetAvatar net, WorldBody me)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcClaimHouse(ctxId);
            else
                OfflineWorld.Instance?.TryClaimHouse(me, OfflineWorld.FindHouseStation(ctxId));
        }

        void HouseLockdown(NetAvatar net, WorldBody me)
        {
            var chest = OfflineWorld.FindHouseChest(ctxId);
            if (net != null && net.IsClientInitialized)
            {
                net.RpcHouseLockdown(ctxId);
                return;
            }
            OfflineWorld.Instance?.TryLockdown(me, chest, ItemCatalog.Cloth);
        }

        void HouseTake(NetAvatar net, WorldBody me)
        {
            var chest = OfflineWorld.FindHouseChest(ctxId);
            if (net != null && net.IsClientInitialized)
            {
                net.RpcHouseTake(ctxId);
                return;
            }
            OfflineWorld.Instance?.TrySecureTake(me, chest);
        }

        void HouseVendorList(NetAvatar net, WorldBody me)
        {
            var vendor = OfflineWorld.FindHouseVendor(ctxId);
            if (net != null && net.IsClientInitialized)
            {
                net.RpcHouseVendorList(ctxId);
                return;
            }
            OfflineWorld.Instance?.TryListVendor(me, vendor, ItemCatalog.Cloth);
        }

        void HouseVendorBuy(NetAvatar net, WorldBody me)
        {
            var vendor = OfflineWorld.FindHouseVendor(ctxId);
            if (net != null && net.IsClientInitialized)
            {
                net.RpcHouseVendorBuy(ctxId);
                return;
            }
            OfflineWorld.Instance?.TryBuyHouseVendor(me, vendor);
        }
    }
}
