using UnityEngine;

namespace AshesToStars
{
    /// <summary>모든 생성 전투 이펙트를 한 화면에서 확인하는 수동·자동 테스트 씬.</summary>
    public class VfxTestScreen : GameScreen
    {
        protected override string Title => "전투 이펙트 테스트";
        protected override string Subtitle => "개별 재생 또는 전체 자동 재생 · 스크린샷: GAME_START=go:VfxTest";
        protected override bool ShowBottomBar => false;
        string[] _keys = CombatVfxAtlas.RequiredKeys;
        int _page;
        // 테스트 씬을 열자마자 무엇을 검수하는지 보여야 한다. 수동 시작이면
        // 검은 배경만 남아 "이펙트가 안 보인다"로 읽히므로 자동 재생이 기본이다.
        bool _auto = true;
        float _next;
        int _cursor;

        public bool IsAutoPlaying => _auto;

        protected override void Update()
        {
            base.Update();
            if (!_auto || Time.time < _next) return;
            Play(_keys[_cursor++ % _keys.Length]);
            _next = Time.time + .65f;
        }

        protected override void Body(Rect r)
        {
            Info(r, 0, _auto ? "전체 자동 재생 중" : "버튼을 누르면 화면 중앙에서 이펙트를 재생한다");
            const int perPage = 6;
            int row = 1;
            int start = _page * perPage;
            for (int i = start; i < Mathf.Min(start + perPage, _keys.Length); i++)
                if (Row(r, row++, _keys[i])) Play(_keys[i]);

            float x = r.x + 350f;
            if (GUI.Button(new Rect(x, r.y + 88, 220, 42), _auto ? "자동 재생 중지" : "전체 자동 재생")) _auto = !_auto;
            if (GUI.Button(new Rect(x, r.y + 140, 220, 42), "다음 페이지")) _page = (_page + 1) % 3;
            if (GUI.Button(new Rect(x, r.y + 192, 220, 42), "이전 페이지")) _page = (_page + 2) % 3;
            if (GUI.Button(new Rect(x, r.y + 244, 220, 42), "직업 이펙트 6종"))
                for (int i = 0; i < 6; i++) FxPool.PlayJob(i, new Vector2((i - 2.5f) * 2f, 0f), 1.1f);
            if (GUI.Button(new Rect(x, r.y + 296, 220, 42), "상태·보스 이펙트 7종"))
                for (int i = 0; i < 7; i++) FxPool.PlayStatus(i, new Vector2((i - 3) * 2f, 0f), 1.1f);
            if (GUI.Button(new Rect(x, r.y + 348, 220, 42), "영지로 돌아가기")) GameFlow.Go(GameFlow.Estate);
        }

        static void Play(string key) => FxPool.PlayAtlas(key, Vector2.zero, 2f);
    }
}
