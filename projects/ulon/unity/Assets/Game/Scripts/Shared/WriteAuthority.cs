namespace Ulon.Shared
{
    /// <summary>
    /// **"이 값을 내가 정해도 되는가"를 묻는 자리 하나**(축 ③·④).
    ///
    /// 판정 자체는 서버 계층(`Ulon.Server.EconomyAuthority`)이 안다 — 네트워크 상태를 봐야 하니까.
    /// 그런데 `Ulon.Shared`는 **참조가 없는 바닥 어셈블리**라 서버를 부를 수 없다(그렇게 두는 것이 맞다).
    /// 그래서 서버가 켜질 때 훅을 꽂는다. 문은 여전히 하나다 — 여기는 문고리일 뿐 판정은 저쪽이다.
    /// </summary>
    public static class WriteAuthority
    {
        /// <summary>기본값은 "내가 정해도 된다" — 오프라인 싱글·에디터 검사는 그대로 돈다.</summary>
        public static System.Func<string, bool> Refuse = _ => false;
    }
}
