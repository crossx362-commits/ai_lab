using System;
using System.IO;
using Ulon.Server;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        static void AssertContainerDnD()
        {
            ItemData.Reload();
            string hudPath = Path.Combine(Application.dataPath, "Game/Scripts/Client/SliceHud.Container.cs");
            string worldPath = Path.Combine(Application.dataPath, "Game/Scripts/Server/OfflineWorld.Craft.cs");
            if (!File.Exists(hudPath))
                throw new InvalidOperationException("컨테이너 HUD 파일이 없습니다: " + hudPath);
            string hud = File.ReadAllText(hudPath);
            string world = File.ReadAllText(worldPath);
            if (hud.IndexOf("DrawBagGrid", StringComparison.Ordinal) < 0 ||
                hud.IndexOf("DrawBankGrid", StringComparison.Ordinal) < 0 ||
                hud.IndexOf("DropDragged", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("가방·은행 칸과 드롭이 HUD에 없습니다.");
            if (world.IndexOf("TryDepositOne", StringComparison.Ordinal) < 0 ||
                world.IndexOf("TryWithdrawOne", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("한 칸 입출금이 서버에 없습니다.");

            OfflineWorld.Instance?.ResetHousePlot();
            var worldGo = new GameObject("selfcheck-dnd-world");
            GameObject bodyGo = null;
            GameObject bankGo = null;
            try
            {
                var ow = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                ow.ResetHousePlot();

                bodyGo = new GameObject("selfcheck-dnd-body");
                bodyGo.transform.position = new Vector3(2400f, 0f, 2400f);
                var body = bodyGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.CharacterId = "dnd-body";
                body.RecalcFromStr(40);
                body.ResetHp();
                var bag = bodyGo.AddComponent<InventoryBag>();
                bag.Add(ItemCatalog.Pouch, 1);
                bag.Add(ItemCatalog.Cloth, 1);
                ow.StatsOf(body).ForceSet(40, 25, 25);

                bankGo = new GameObject("selfcheck-dnd-bank");
                bankGo.transform.position = bodyGo.transform.position;
                bankGo.AddComponent<BankStation>().InteractRange = 4f;

                string clothId = "";
                string pouchId = "";
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        clothId = bag.Items[i].InstanceId;
                    if (bag.Items[i].TemplateId == ItemCatalog.Pouch)
                        pouchId = bag.Items[i].InstanceId;
                }
                if (string.IsNullOrEmpty(clothId) || string.IsNullOrEmpty(pouchId))
                    throw new InvalidOperationException("천·주머니 InstanceId가 있어야 합니다.");

                bankGo.SetActive(false);
                var ranged = ow.TryDepositOne(body, clothId);
                if (ranged.Applied || ranged.FailReason != "range")
                    throw new InvalidOperationException("은행 사거리 밖은 range여야 합니다: " + ranged.FailReason);
                bankGo.SetActive(true);

                body.Ghost = true;
                var ghosted = ow.TryDepositOne(body, clothId);
                if (ghosted.Applied || ghosted.FailReason != "ghost")
                    throw new InvalidOperationException("유령은 입금 불가야 합니다: " + ghosted.FailReason);
                body.Ghost = false;

                var dep = ow.TryDepositOne(body, clothId);
                if (!dep.Applied)
                    throw new InvalidOperationException("TryDepositOne 실패: " + dep.FailReason);
                var vault = body.GetComponent<BankVault>();
                if (vault == null || vault.Items.Count != 1 || vault.Items[0].TemplateId != ItemCatalog.Cloth)
                    throw new InvalidOperationException("입금 뒤 은행에 천이 있어야 합니다.");
                bool clothInBag = false;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        clothInBag = true;
                if (clothInBag)
                    throw new InvalidOperationException("입금 뒤 가방에 천이 없어야 합니다.");

                string bankCloth = vault.Items[0].InstanceId;
                var wd = ow.TryWithdrawOne(body, bankCloth);
                if (!wd.Applied)
                    throw new InvalidOperationException("TryWithdrawOne 실패: " + wd.FailReason);
                if (vault.Items.Count != 0)
                    throw new InvalidOperationException("찾은 뒤 은행이 비어야 합니다.");
                clothInBag = false;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        clothInBag = true;
                if (!clothInBag)
                    throw new InvalidOperationException("찾은 뒤 가방에 천이 있어야 합니다.");

                var pouchMove = ow.TryMoveToPouch(body, ItemCatalog.Cloth, pouchId);
                if (!pouchMove.Applied)
                    throw new InvalidOperationException("주머니 넣기 실패: " + pouchMove.FailReason);

                Debug.Log("[Ulon] 컨테이너 DnD — 한 칸 입금·출금·사거리·유령 거절");
            }
            finally
            {
                ContainerMove.NcDenyAll = false;
                OfflineWorld.Instance?.ResetHousePlot();
                if (bodyGo != null)
                    UnityEngine.Object.DestroyImmediate(bodyGo);
                if (bankGo != null)
                    UnityEngine.Object.DestroyImmediate(bankGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertContainerDnDNegativeControl()
        {
            bool was = ContainerMove.NcDenyAll;
            bool red = false;
            try
            {
                ContainerMove.NcDenyAll = true;
                try { AssertContainerDnD(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { ContainerMove.NcDenyAll = was; }
            if (!red)
                throw new InvalidOperationException("컨테이너 DnD 네거티브 컨트롤 실패 — NcDenyAll 인데 통과했습니다.");
            Debug.Log("[Ulon] 컨테이너 DnD 네거티브 컨트롤 통과 — NcDenyAll 이면 FAIL");
        }
    }
}
