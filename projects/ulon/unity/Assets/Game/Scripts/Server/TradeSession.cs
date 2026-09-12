using UnityEngine;

namespace Ulon.Server
{
    public sealed class TradeSession
    {
        public WorldBody A;
        public WorldBody B;
        public string OfferA = "";
        public string OfferB = "";
        public int GoldA;
        public int GoldB;
        public bool AcceptA;
        public bool AcceptB;
        public bool Settled;

        public WorldBody Other(WorldBody me) => me == A ? B : A;

        public void SetOffer(WorldBody me, string template)
        {
            if (me == A) OfferA = template ?? "";
            else OfferB = template ?? "";
            AcceptA = false;
            AcceptB = false;
        }

        public void SetGold(WorldBody me, int gold)
        {
            int n = gold < 0 ? 0 : gold;
            if (me == A) GoldA = n;
            else GoldB = n;
            AcceptA = false;
            AcceptB = false;
        }

        public bool SetAccept(WorldBody me, bool value)
        {
            if (me == A) AcceptA = value;
            else AcceptB = value;
            return AcceptA && AcceptB;
        }
    }

    public sealed class Party
    {
        public WorldBody Leader;
        public WorldBody Pending;
        public readonly System.Collections.Generic.List<WorldBody> Members = new System.Collections.Generic.List<WorldBody>();
        public readonly System.Collections.Generic.List<string> Chat = new System.Collections.Generic.List<string>();

        public bool Contains(WorldBody body)
        {
            if (body == null)
                return false;
            if (body == Leader)
                return true;
            for (int i = 0; i < Members.Count; i++)
                if (Members[i] == body)
                    return true;
            return false;
        }

        public void Add(WorldBody body)
        {
            if (body == null || Contains(body))
                return;
            Members.Add(body);
            Pending = null;
        }

        public void Say(string name, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            Chat.Add((name ?? "?") + ": " + text);
            while (Chat.Count > 8)
                Chat.RemoveAt(0);
        }
    }

    public sealed class Guild
    {
        public string Id = "";
        public string Name = "";
        public string WarWithId = "";
        public WorldBody Leader;
        public WorldBody Pending;
        public readonly System.Collections.Generic.List<WorldBody> Members = new System.Collections.Generic.List<WorldBody>();

        public bool Contains(WorldBody body)
        {
            if (body == null)
                return false;
            if (body == Leader)
                return true;
            for (int i = 0; i < Members.Count; i++)
                if (Members[i] == body)
                    return true;
            return false;
        }

        public void Add(WorldBody body)
        {
            if (body == null || Contains(body))
                return;
            Members.Add(body);
            Pending = null;
        }
    }
}
