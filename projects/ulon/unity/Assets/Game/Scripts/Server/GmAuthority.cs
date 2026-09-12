using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    /// <summary>
    /// GM 명령을 누가 실행해도 되는가. 문은 여기 하나 — HUD 숨김은 권한이 아니다.
    /// 원격 클라(<see cref="EconomyAuthority.ClientOnly"/>)는 원장·CLI 계정만 통과한다.
    /// 에디터 오프라인·호스트는 개발 슬라이스라 기본 허용(네거티브 컨트롤은 <see cref="EditorBypass"/>를 끈다).
    /// </summary>
    public static class GmAuthority
    {
        /// <summary>네거티브 컨트롤 — 켜면 옛 구멍(아무나 지급)을 되살린다.</summary>
        public static bool NcOpen;

        /// <summary>에디터 슬라이스 기본 허용. 게이트가 무인증 거절을 잴 때 끈다.</summary>
        public static bool EditorBypass = true;

        public const string Denied = "unauthorized";

        public static bool Allowed(string account)
        {
            if (NcOpen)
                return true;
            if (GmAccounts.Listed(account))
                return true;
            if (EconomyAuthority.ClientOnly)
                return false;
            if (EditorBypass && Application.isEditor)
                return true;
            return false;
        }
    }
}
