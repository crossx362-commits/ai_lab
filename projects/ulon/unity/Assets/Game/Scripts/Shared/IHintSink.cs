namespace Ulon.Shared
{
    /// <summary>
    /// 안내는 상태가 아니라 사건이다(검수 2026-09-08 B).
    /// 전역 Last*Message에 적어 두면 온라인 클라는 제 OfflineWorld만 봐서 화면이 영원히 빈다.
    /// 행동을 처리한 그 사람에게만 보낸다 — 구현은 NetAvatar의 TargetRpc.
    /// </summary>
    public interface IHintSink
    {
        void SendHint(string text);
        string ClientHint { get; }
    }
}
