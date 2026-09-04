namespace Ulon.Client
{
    public static class TradeView
    {
        public static bool Open;
        public static int IdA;
        public static int IdB;
        public static string OfferA = "";
        public static string OfferB = "";
        public static bool AcceptA;
        public static bool AcceptB;
        public static string NameA = "";
        public static string NameB = "";
    }

    public static class PartyView
    {
        public static bool Open;
        public static bool PendingMe;
        public static string Leader = "";
        public static string Roster = "";
        public static string Chat = "";
    }

    public static class GuildView
    {
        public static bool Open;
        public static bool PendingMe;
        public static string GuildId = "";
        public static string GuildName = "";
        public static string Leader = "";
        public static string Roster = "";
        public static string WarName = "";
    }
}
