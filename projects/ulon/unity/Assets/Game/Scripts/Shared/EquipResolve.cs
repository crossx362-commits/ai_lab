namespace Ulon.Shared
{
    public sealed class EquipRequest
    {
        public bool Ghost;
        public bool HasItem = true;
        public int Str = StatSet.DefaultStr;
        public int StrReq;
        public string TemplateId = "";
    }

    public static class EquipResolve
    {
        public static AttackResult Equip(EquipRequest req)
        {
            if (req == null || string.IsNullOrEmpty(req.TemplateId))
                return Fail("bad_request");
            if (req.Ghost)
                return Fail("ghost");
            if (!req.HasItem)
                return Fail("no_item");
            if (req.Str < req.StrReq)
                return Fail("str_req");
            return new AttackResult { Applied = true, Hit = true };
        }

        public static AttackResult Unequip(EquipRequest req)
        {
            if (req == null)
                return Fail("bad_request");
            if (req.Ghost)
                return Fail("ghost");
            if (string.IsNullOrEmpty(req.TemplateId))
                return Fail("not_equipped");
            return new AttackResult { Applied = true, Hit = true };
        }

        public static string MessageFor(string failReason, string templateId, int strReq)
        {
            if (failReason == "str_req")
                return "근력 " + strReq + " 필요 — " + templateId + " 장착 실패";
            if (failReason == "no_item")
                return "가방에 없음 — " + templateId;
            if (failReason == "ghost")
                return "유령은 장착할 수 없습니다";
            if (failReason == "not_equipped")
                return "장착 중이 아님";
            if (string.IsNullOrEmpty(failReason))
                return "장착: " + templateId;
            return failReason;
        }

        static AttackResult Fail(string reason) => new AttackResult { FailReason = reason };
    }
}
