using System;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// 던전3는 아직 leftover다. 어떤 슬라이스도 실수로 만들지 않았는지 확인한다.
        ///
        /// 같은 if/throw 두 줄이 9개 파일 51곳에 복사돼 있었다. 그대로 두면 던전3를
        /// 실제로 구현하는 날 51곳을 손으로 고쳐야 하고, 하나라도 빠뜨리면 셀프체크가
        /// 무더기로 터진다. 판정을 여기 하나로 모아 그날 고칠 곳이 이 함수가 되게 한다.
        ///
        /// 던전3가 들어오면 이 함수의 의미만 뒤집으면 된다 — "없어야 한다"에서
        /// "정확히 하나 있고 온전해야 한다"로. 호출부 51곳은 그대로 둔다.
        /// </summary>
        /// <param name="context">어느 슬라이스 뒤에 확인하는지. 실패 메시지에 붙는다.</param>
        static void AssertDungeon3Leftover(string context = null)
        {
            if (GameObject.Find("Dungeon3") == null && GameObject.Find("Dungeon3Entrance") == null)
                return;
            throw new InvalidOperationException(
                string.IsNullOrEmpty(context)
                    ? "던전 3은 아직 두지 않습니다."
                    : context + " 던전3가 생기면 안 됩니다.");
        }
    }
}
