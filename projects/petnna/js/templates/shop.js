const SHOP_TEMPLATE = `
<div class="space-y-5 animate-fade-in">

<!-- ===== 헤더 ===== -->
<div style="display:flex;align-items:center;gap:14px;background:linear-gradient(135deg,#ecfdf5,#f0fdf4);border:1.5px solid #bbf7d0;border-radius:20px;padding:14px 20px;">
  <span style="font-size:2rem;">🌏</span>
  <div>
    <h2 style="font-size:1.1rem;font-weight:900;color:#065f46;margin:0;">펫 라이프 아일랜드</h2>
    <p style="font-size:0.7rem;color:#6b7280;margin:0;">섬의 핀을 눌러 가맹점을 탐험하세요</p>
  </div>
  <span style="margin-left:auto;background:#d1fae5;border:1px solid #6ee7b7;color:#065f46;font-size:0.6rem;font-weight:800;padding:4px 10px;border-radius:999px;white-space:nowrap;">PET WORLD MAP</span>
</div>

<!-- ===== 3-Col 레이아웃 ===== -->
<div style="display:grid;grid-template-columns:190px 1fr 210px;gap:14px;align-items:start;">

  <!-- 좌측 필터 패널 -->
  <div style="background:#f8fafc;border:1.5px solid #e2e8f0;border-radius:20px;padding:16px;display:flex;flex-direction:column;gap:10px;">
    <p style="font-size:0.62rem;font-weight:800;text-transform:uppercase;letter-spacing:0.08em;color:#94a3b8;margin:0;">카테고리 필터</p>

    <label style="display:flex;align-items:center;gap:8px;font-size:0.72rem;font-weight:600;color:#374151;cursor:pointer;padding:7px 8px;border-radius:10px;background:#fff;border:1px solid #e2e8f0;">
      <input type="checkbox" checked id="f-spa" onchange="applyMapFilters()" style="accent-color:#059669;width:14px;height:14px;">
      <span style="width:9px;height:9px;border-radius:50%;background:#5eead4;flex-shrink:0;display:inline-block;"></span>
      <span>힐링 &amp; 스파</span>
      <span style="margin-left:auto;background:#e2e8f0;color:#64748b;font-size:0.6rem;font-weight:700;padding:1px 6px;border-radius:99px;">2</span>
    </label>

    <label style="display:flex;align-items:center;gap:8px;font-size:0.72rem;font-weight:600;color:#374151;cursor:pointer;padding:7px 8px;border-radius:10px;background:#fff;border:1px solid #e2e8f0;">
      <input type="checkbox" checked id="f-medical" onchange="applyMapFilters()" style="accent-color:#059669;width:14px;height:14px;">
      <span style="width:9px;height:9px;border-radius:50%;background:#fb7185;flex-shrink:0;display:inline-block;"></span>
      <span>메디컬 케어</span>
      <span style="margin-left:auto;background:#e2e8f0;color:#64748b;font-size:0.6rem;font-weight:700;padding:1px 6px;border-radius:99px;">2</span>
    </label>

    <label style="display:flex;align-items:center;gap:8px;font-size:0.72rem;font-weight:600;color:#374151;cursor:pointer;padding:7px 8px;border-radius:10px;background:#fff;border:1px solid #e2e8f0;">
      <input type="checkbox" checked id="f-hotel" onchange="applyMapFilters()" style="accent-color:#059669;width:14px;height:14px;">
      <span style="width:9px;height:9px;border-radius:50%;background:#818cf8;flex-shrink:0;display:inline-block;"></span>
      <span>스테이 &amp; 돌봄</span>
      <span style="margin-left:auto;background:#e2e8f0;color:#64748b;font-size:0.6rem;font-weight:700;padding:1px 6px;border-radius:99px;">1</span>
    </label>

    <label style="display:flex;align-items:center;gap:8px;font-size:0.72rem;font-weight:600;color:#374151;cursor:pointer;padding:7px 8px;border-radius:10px;background:#fff;border:1px solid #e2e8f0;">
      <input type="checkbox" checked id="f-shop" onchange="applyMapFilters()" style="accent-color:#059669;width:14px;height:14px;">
      <span style="width:9px;height:9px;border-radius:50%;background:#fbbf24;flex-shrink:0;display:inline-block;"></span>
      <span>쇼핑 광장</span>
      <span style="margin-left:auto;background:#e2e8f0;color:#64748b;font-size:0.6rem;font-weight:700;padding:1px 6px;border-radius:99px;">1</span>
    </label>

    <hr style="border:none;border-top:1px solid #e2e8f0;margin:4px 0;">
    <p style="font-size:0.62rem;font-weight:800;text-transform:uppercase;letter-spacing:0.08em;color:#94a3b8;margin:0;">활성 영토</p>
    <div>
      <div style="height:6px;background:#e2e8f0;border-radius:99px;overflow:hidden;">
        <div id="stat-bar" style="height:100%;background:linear-gradient(90deg,#34d399,#059669);border-radius:99px;width:100%;transition:width 0.4s;"></div>
      </div>
      <span id="stat-label" style="font-size:0.65rem;font-weight:700;color:#059669;margin-top:4px;display:block;">6 / 6 활성</span>
    </div>
  </div>

  <!-- 중앙 SVG 지도 — 귀여운 캐주얼 피지 맵 -->
  <div style="background:#7ecde0;border-radius:28px;overflow:hidden;border:3px solid #5ab8d0;box-shadow:0 4px 24px rgba(90,184,208,0.25);">
    <svg id="petlife-map" viewBox="0 0 1030 700" xmlns="http://www.w3.org/2000/svg" style="width:100%;display:block;">
      <defs>
        <!-- 밝은 민트 바다 -->
        <linearGradient id="sea-bg" x1="0%" y1="0%" x2="20%" y2="100%">
          <stop offset="0%" stop-color="#7ecde0"/>
          <stop offset="60%" stop-color="#6ac2d8"/>
          <stop offset="100%" stop-color="#55b4cc"/>
        </linearGradient>
        <!-- 섬 풀밭 (따뜻한 연녹색) -->
        <radialGradient id="isl-g" cx="40%" cy="35%" r="65%">
          <stop offset="0%" stop-color="#c8f0b0"/>
          <stop offset="55%" stop-color="#a8dea0"/>
          <stop offset="100%" stop-color="#8ecf8a"/>
        </radialGradient>
        <!-- 작은 섬 그라디언트 -->
        <radialGradient id="isl-s" cx="40%" cy="35%" r="65%">
          <stop offset="0%" stop-color="#c8f0b0"/>
          <stop offset="100%" stop-color="#96d890"/>
        </radialGradient>
        <!-- 섬 부드러운 그림자 -->
        <filter id="sf" x="-8%" y="-8%" width="120%" height="130%">
          <feDropShadow dx="0" dy="4" stdDeviation="5" flood-color="#3a9ab0" flood-opacity="0.22"/>
        </filter>
        <!-- 핀 글로우 그림자 -->
        <filter id="pf" x="-40%" y="-40%" width="180%" height="220%">
          <feDropShadow dx="0" dy="3" stdDeviation="4" flood-color="#000" flood-opacity="0.2"/>
        </filter>
      </defs>

      <!-- 바다 배경 -->
      <rect width="1030" height="700" fill="url(#sea-bg)" rx="4"/>

      <!-- 귀여운 물결선 (반복 패턴) -->
      <g opacity="0.22" stroke="white" stroke-width="1.5" fill="none" stroke-linecap="round">
        <path d="M30 80 Q55 70 80 80 Q105 90 130 80"/>
        <path d="M200 40 Q225 30 250 40 Q275 50 300 40"/>
        <path d="M700 60 Q725 50 750 60 Q775 70 800 60"/>
        <path d="M880 120 Q905 110 930 120 Q955 130 980 120"/>
        <path d="M50 550 Q75 540 100 550 Q125 560 150 550"/>
        <path d="M400 620 Q425 610 450 620 Q475 630 500 620"/>
        <path d="M750 580 Q775 570 800 580 Q825 590 850 580"/>
        <path d="M900 480 Q925 470 950 480 Q975 490 1000 480"/>
        <path d="M500 400 Q525 390 550 400 Q575 410 600 400"/>
      </g>

      <!-- 귀여운 별 반짝임 (바다 위) -->
      <g fill="white" opacity="0.35">
        <circle cx="60"  cy="160" r="2.5"/>
        <circle cx="360" cy="460" r="2"/>
        <circle cx="480" cy="280" r="2"/>
        <circle cx="700" cy="450" r="2.5"/>
        <circle cx="920" cy="180" r="2"/>
        <circle cx="980" cy="350" r="2.5"/>
        <circle cx="55"  cy="490" r="2"/>
        <circle cx="860" cy="560" r="2"/>
        <circle cx="600" cy="620" r="2.5"/>
        <!-- 작은 십자 별 -->
        <path d="M450 500 l0-5 l0 10 M445 505 l10 0" stroke="white" stroke-width="1.2" fill="none" opacity="0.6"/>
        <path d="M900 300 l0-4 l0 8 M896 304 l8 0"   stroke="white" stroke-width="1"   fill="none" opacity="0.5"/>
        <path d="M150 60  l0-4 l0 8 M146 64  l8 0"   stroke="white" stroke-width="1"   fill="none" opacity="0.5"/>
      </g>

      <!-- 귀여운 작은 물고기들 -->
      <g fill="white" opacity="0.28">
        <ellipse cx="470" cy="470" rx="7" ry="4"/>
        <polygon points="477,470 483,466 483,474"/>
        <ellipse cx="920" cy="420" rx="6" ry="3.5"/>
        <polygon points="926,420 931,417 931,423"/>
        <ellipse cx="60" cy="320" rx="5" ry="3"/>
        <polygon points="65,320 70,318 70,322"/>
      </g>

      <!-- ================================================================
           귀여운 파스텔 피지 섬들 — 모래 해변 이중 테두리
           ================================================================ -->

      <!-- ① Viti Levu (비티레부) — 메인 큰 섬 -->
      <!-- 모래 해변 테두리 (바깥 레이어) -->
      <path fill="#f5d98a" opacity="0.9"
        d="M 132,310
           C 135,294 144,282 160,274
           C 172,267 183,260 198,257
           C 213,253 224,247 238,244
           C 253,240 263,238 274,241
           C 288,244 298,252 307,260
           C 319,271 326,282 334,295
           C 344,308 352,318 358,332
           C 366,347 370,360 367,374
           C 364,387 357,397 347,405
           C 337,413 323,418 309,421
           C 294,424 278,424 265,421
           C 251,418 237,410 227,402
           C 216,394 208,383 201,371
           C 193,358 187,344 181,332
           C 174,319 165,312 150,310
           C 143,309 137,310 132,310 Z"/>
      <!-- 섬 본체 (초록 풀밭) -->
      <path filter="url(#sf)" fill="url(#isl-g)"
        d="M 142,312
           C 146,297 155,287 170,280
           C 181,274 191,267 205,264
           C 219,260 229,255 242,252
           C 256,248 265,246 275,249
           C 288,252 298,259 307,267
           C 318,277 325,287 332,299
           C 341,312 349,322 355,335
           C 362,348 366,361 363,373
           C 360,385 353,395 343,402
           C 333,410 320,414 306,417
           C 292,420 277,420 264,417
           C 251,414 238,407 228,399
           C 218,391 211,380 204,369
           C 197,356 191,342 185,331
           C 178,318 169,311 155,311
           C 149,311 144,312 142,312 Z"/>
      <!-- 내부 밝은 언덕 -->
      <path fill="#d4f4b8" opacity="0.55"
        d="M 205,282 C 230,272 256,270 275,275 C 292,280 304,289 313,302 C 322,314 325,330 320,343 C 315,355 305,364 292,370 C 278,376 262,377 248,373 C 233,369 220,361 210,350 C 200,339 196,324 198,310 C 199,300 201,290 205,282 Z"/>

      <!-- ② Vanua Levu (바누아레부) — 북동 가로 긴 섬 -->
      <path fill="#f5d98a" opacity="0.85"
        d="M 380,108 C 390,97 407,91 426,89 C 447,87 468,91 490,95 C 513,99 535,104 558,108 C 582,113 606,117 627,124 C 648,131 666,140 677,152 C 685,162 685,174 677,183 C 669,192 655,199 637,203 C 618,207 597,207 576,204 C 553,201 529,197 506,192 C 482,188 459,183 439,177 C 418,170 400,162 389,151 C 378,140 375,124 380,108 Z"/>
      <path filter="url(#sf)" fill="url(#isl-g)"
        d="M 390,112 C 400,102 416,97 434,95 C 454,93 474,97 496,102 C 518,106 540,111 562,116 C 584,121 606,126 625,133 C 644,140 659,149 668,160 C 675,169 674,180 666,189 C 657,197 643,203 625,206 C 606,209 585,208 563,205 C 540,202 516,198 493,193 C 470,188 448,184 428,178 C 408,172 392,164 383,153 C 374,142 372,126 380,112 Z"/>
      <path fill="#d4f4b8" opacity="0.5"
        d="M 435,110 C 466,106 502,108 532,115 C 562,122 584,133 594,145 C 602,155 598,167 582,175 C 563,184 535,187 507,185 C 478,183 450,177 430,167 C 409,157 399,143 407,129 C 412,120 420,113 435,110 Z"/>

      <!-- ③ Taveuni (타베우니) — 작은 세로 섬 -->
      <path fill="#f5d98a" opacity="0.8"
        d="M 675,107 C 683,100 694,98 702,102 C 712,107 717,118 715,131 C 712,145 706,157 697,165 C 688,172 679,172 673,165 C 667,158 667,146 670,135 C 673,122 672,114 675,107 Z"/>
      <path filter="url(#sf)" fill="url(#isl-s)"
        d="M 680,111 C 688,104 698,102 705,107 C 713,112 717,123 714,136 C 711,149 705,160 697,167 C 688,173 680,172 675,165 C 670,158 670,147 673,136 C 676,123 677,117 680,111 Z"/>

      <!-- ④ Kadavu (카다부) — 남쪽 섬 -->
      <path fill="#f5d98a" opacity="0.85"
        d="M 224,526 C 233,517 247,513 262,513 C 278,513 294,517 305,526 C 316,535 320,548 316,560 C 312,572 301,581 286,585 C 269,589 250,587 237,580 C 224,573 216,562 218,549 C 220,540 222,534 224,526 Z"/>
      <path filter="url(#sf)" fill="url(#isl-s)"
        d="M 232,530 C 241,522 254,518 268,518 C 282,518 296,522 306,531 C 315,540 318,552 314,563 C 310,574 299,582 284,585 C 268,589 251,587 239,580 C 226,573 219,562 221,550 C 223,541 225,535 232,530 Z"/>

      <!-- ⑤ 야사와 군도 — NW 소섬 체인 (귀여운 타원형) -->
      <ellipse fill="#f5d98a" opacity="0.8"  cx="96"  cy="230" rx="18" ry="30" transform="rotate(-15,96,230)"/>
      <ellipse filter="url(#sf)" fill="url(#isl-s)" cx="96"  cy="228" rx="16" ry="27" transform="rotate(-15,96,228)"/>
      <ellipse fill="#f5d98a" opacity="0.8"  cx="106" cy="186" rx="14" ry="22" transform="rotate(-15,106,186)"/>
      <ellipse filter="url(#sf)" fill="url(#isl-s)" cx="106" cy="185" rx="12" ry="20" transform="rotate(-15,106,185)"/>
      <ellipse fill="#f5d98a" opacity="0.8"  cx="119" cy="150" rx="12" ry="18" transform="rotate(-15,119,150)"/>
      <ellipse filter="url(#sf)" fill="url(#isl-s)" cx="119" cy="149" rx="10" ry="16" transform="rotate(-15,119,149)"/>
      <ellipse fill="#f5d98a" opacity="0.8"  cx="133" cy="122" rx="10" ry="15" transform="rotate(-15,133,122)"/>
      <ellipse filter="url(#sf)" fill="url(#isl-s)" cx="133" cy="121" rx="8"  ry="13" transform="rotate(-15,133,121)"/>

      <!-- ⑥ 라우 군도 — 동쪽 소섬들 -->
      <ellipse fill="#f5d98a" opacity="0.75" cx="730" cy="385" rx="20" ry="14"/>
      <ellipse filter="url(#sf)" fill="url(#isl-s)" cx="730" cy="384" rx="18" ry="12"/>
      <ellipse fill="#f5d98a" opacity="0.75" cx="618" cy="488" rx="16" ry="12"/>
      <ellipse filter="url(#sf)" fill="url(#isl-s)" cx="618" cy="487" rx="14" ry="10"/>
      <ellipse fill="#f5d98a" opacity="0.7"  cx="652" cy="510" rx="12" ry="9"/>
      <ellipse filter="url(#sf)" fill="url(#isl-s)" cx="652" cy="509" rx="10" ry="7"/>
      <ellipse fill="#f5d98a" opacity="0.75" cx="820" cy="220" rx="14" ry="10"/>
      <ellipse filter="url(#sf)" fill="url(#isl-s)" cx="820" cy="219" rx="12" ry="8"/>
      <ellipse fill="#f5d98a" opacity="0.75" cx="755" cy="255" rx="24" ry="16"/>
      <ellipse filter="url(#sf)" fill="url(#isl-s)" cx="755" cy="254" rx="22" ry="14"/>
      <ellipse fill="#f5d98a" opacity="0.75" cx="780" cy="330" rx="18" ry="13"/>
      <ellipse filter="url(#sf)" fill="url(#isl-s)" cx="780" cy="329" rx="16" ry="11"/>
      <ellipse filter="url(#sf)" fill="url(#isl-s)" cx="850" cy="290" rx="9"  ry="6"/>
      <ellipse filter="url(#sf)" fill="url(#isl-s)" cx="875" cy="355" rx="7"  ry="5"/>
      <ellipse filter="url(#sf)" fill="url(#isl-s)" cx="840" cy="430" rx="11" ry="7"/>
      <ellipse filter="url(#sf)" fill="url(#isl-s)" cx="890" cy="460" rx="8"  ry="5"/>
      <ellipse filter="url(#sf)" fill="url(#isl-s)" cx="610" cy="560" rx="18" ry="10"/>
      <ellipse filter="url(#sf)" fill="url(#isl-s)" cx="560" cy="570" rx="12" ry="8"/>
      <ellipse filter="url(#sf)" fill="url(#isl-s)" cx="680" cy="548" rx="9"  ry="6"/>
      <ellipse filter="url(#sf)" fill="url(#isl-s)" cx="810" cy="520" rx="7"  ry="5"/>
      <ellipse filter="url(#sf)" fill="url(#isl-s)" cx="865" cy="530" rx="6"  ry="4"/>

      <!-- 비티레부 서쪽 소군도 -->
      <ellipse fill="#f5d98a" opacity="0.75" cx="152" cy="390" rx="20" ry="12"/>
      <ellipse filter="url(#sf)" fill="url(#isl-s)" cx="152" cy="389" rx="18" ry="10"/>
      <ellipse fill="#f5d98a" opacity="0.75" cx="125" cy="412" rx="14" ry="10"/>
      <ellipse filter="url(#sf)" fill="url(#isl-s)" cx="125" cy="411" rx="12" ry="8"/>
      <!-- Levuka / Vandravandra 소섬 -->
      <ellipse filter="url(#sf)" fill="url(#isl-s)" cx="425" cy="315" rx="10" ry="7"/>
      <ellipse filter="url(#sf)" fill="url(#isl-s)" cx="535" cy="365" rx="8"  ry="5"/>


      <!-- ==================================================================
           펫샵 핀 균형 배치
           ① 호텔  → 야사와 군도        (NW)  cx=105 cy=175
           ② 스파  → 비티레부 서부 Nadi (W)   cx=185 cy=305
           ③ 캠핑  → 비티레부 동부 Suva (SE)  cx=328 cy=368
           ④ 테라피→ 바누아레부 서쪽    (N-C) cx=448 cy=143
           ⑤ 쇼핑  → 바누아레부 동쪽   (N-E) cx=615 cy=143
           ⑥ 병원  → 라우 군도 중앙    (E)   cx=780 cy=330
           ================================================================== -->

      <!-- ① 🏨 호텔 — 야사와 군도 (NW) -->
      <g class="filter-node" data-cat="hotel" onclick="selectIslandShop('healing-hotel')" style="cursor:pointer;" filter="url(#pf)">
        <circle cx="105" cy="175" r="22" fill="#a9583e"/>
        <circle cx="105" cy="175" r="22" fill="none" stroke="white" stroke-width="2.5"/>
        <rect x="97" y="167" width="16" height="13" rx="1" fill="none" stroke="white" stroke-width="1.8"/>
        <line x1="101" y1="167" x2="101" y2="180" stroke="white" stroke-width="1.1"/>
        <line x1="105" y1="167" x2="105" y2="180" stroke="white" stroke-width="1.1"/>
        <line x1="109" y1="167" x2="109" y2="180" stroke="white" stroke-width="1.1"/>
        <line x1="97"  y1="173" x2="113" y2="173" stroke="white" stroke-width="1.1"/>
        <polygon points="105,193 99,182 111,182" fill="#a9583e"/>
      </g>

      <!-- ② 🛁 스파 — 비티레부 서부 Nadi (W) -->
      <g class="filter-node" data-cat="spa" onclick="selectIslandShop('healing-spa')" style="cursor:pointer;" filter="url(#pf)">
        <circle cx="185" cy="305" r="22" fill="#0d9488"/>
        <circle cx="185" cy="305" r="22" fill="none" stroke="white" stroke-width="2.5"/>
        <path d="M176 301 Q181 297 185 301 Q189 305 194 301" fill="none" stroke="white" stroke-width="2" stroke-linecap="round"/>
        <path d="M176 307 Q181 303 185 307 Q189 311 194 307" fill="none" stroke="white" stroke-width="2" stroke-linecap="round"/>
        <path d="M176 313 Q181 309 185 313 Q189 317 194 313" fill="none" stroke="white" stroke-width="2" stroke-linecap="round"/>
        <polygon points="185,326 179,315 191,315" fill="#0d9488"/>
      </g>

      <!-- ③ 🏕️ 캠핑 — 비티레부 동부 Suva (SE) -->
      <g class="filter-node" data-cat="spa" onclick="selectIslandShop('healing-camping')" style="cursor:pointer;" filter="url(#pf)">
        <circle cx="328" cy="368" r="22" fill="#15803d"/>
        <circle cx="328" cy="368" r="22" fill="none" stroke="white" stroke-width="2.5"/>
        <polygon points="328,356 316,376 340,376" fill="none" stroke="white" stroke-width="2" stroke-linejoin="round"/>
        <line x1="328" y1="356" x2="328" y2="376" stroke="white" stroke-width="1.5"/>
        <polygon points="328,368 334,376 340,376" fill="white" opacity="0.35"/>
        <polygon points="328,385 322,374 334,374" fill="#15803d"/>
      </g>

      <!-- ④ 🌸 테라피 — 바누아레부 서쪽 (N-C) -->
      <g class="filter-node" data-cat="medical" onclick="selectIslandShop('healing-therapy')" style="cursor:pointer;" filter="url(#pf)">
        <circle cx="448" cy="143" r="22" fill="#e11d48"/>
        <circle cx="448" cy="143" r="22" fill="none" stroke="white" stroke-width="2.5"/>
        <line x1="448" y1="134" x2="448" y2="152" stroke="white" stroke-width="2.5" stroke-linecap="round"/>
        <line x1="439" y1="143" x2="457" y2="143" stroke="white" stroke-width="2.5" stroke-linecap="round"/>
        <polygon points="448,160 442,149 454,149" fill="#e11d48"/>
      </g>

      <!-- ⑤ 🛒 쇼핑 — 바누아레부 동쪽 (N-E) -->
      <g class="filter-node" data-cat="shop" onclick="selectIslandShop('healing-shopping')" style="cursor:pointer;" filter="url(#pf)">
        <circle cx="615" cy="143" r="22" fill="#d97706"/>
        <circle cx="615" cy="143" r="22" fill="none" stroke="white" stroke-width="2.5"/>
        <path d="M605 136 L608 148 L622 148 L624 136" fill="none" stroke="white" stroke-width="2" stroke-linejoin="round"/>
        <circle cx="610" cy="152" r="2" fill="white"/>
        <circle cx="620" cy="152" r="2" fill="white"/>
        <line x1="602" y1="136" x2="605" y2="136" stroke="white" stroke-width="2" stroke-linecap="round"/>
        <polygon points="615,160 609,149 621,149" fill="#d97706"/>
      </g>

      <!-- ⑥ 🏥 병원 — 라우 군도 중앙 Lakeba (E) -->
      <g class="filter-node" data-cat="medical" onclick="selectIslandShop('healing-hospital')" style="cursor:pointer;" filter="url(#pf)">
        <circle cx="780" cy="330" r="22" fill="#dc2626"/>
        <circle cx="780" cy="330" r="22" fill="none" stroke="white" stroke-width="2.5"/>
        <line x1="772" y1="322" x2="772" y2="338" stroke="white" stroke-width="3" stroke-linecap="round"/>
        <line x1="788" y1="322" x2="788" y2="338" stroke="white" stroke-width="3" stroke-linecap="round"/>
        <line x1="772" y1="330" x2="788" y2="330" stroke="white" stroke-width="3" stroke-linecap="round"/>
        <polygon points="780,347 774,336 786,336" fill="#dc2626"/>
      </g>

    </svg>
  </div>

  <!-- 우측 퀘스트 상세 패널 -->
  <div id="shop-quest-panel" style="background:#f8fafc;border:1.5px solid #e2e8f0;border-radius:20px;padding:16px;display:flex;flex-direction:column;gap:12px;min-height:440px;">
    <p style="font-size:0.62rem;font-weight:800;text-transform:uppercase;letter-spacing:0.08em;color:#94a3b8;margin:0;">가맹점 상세 정보</p>

    <div id="quest-default-state" style="flex:1;display:flex;flex-direction:column;align-items:center;justify-content:center;text-align:center;gap:10px;padding:20px 10px;color:#94a3b8;">
      <span style="font-size:2.5rem;">🗺️</span>
      <p style="font-size:0.75rem;line-height:1.6;margin:0;">지도의 <strong>핀</strong>을 눌러<br>가맹점을 탐험하세요!</p>
    </div>

    <div id="quest-active-state" class="hidden" style="display:flex;flex-direction:column;gap:10px;flex:1;">
      <div style="display:flex;align-items:center;gap:10px;padding-bottom:10px;border-bottom:1px solid #e2e8f0;">
        <span id="quest-emoji" style="font-size:2rem;">🛁</span>
        <div>
          <span id="quest-badge" style="font-size:0.58rem;font-weight:900;letter-spacing:0.1em;color:#cc785c;background:#faf3ef;border:1px solid #c7d2fe;padding:2px 6px;border-radius:5px;display:inline-block;">SPA</span>
          <h3 id="quest-title" style="font-size:0.95rem;font-weight:900;color:#1e293b;margin:3px 0 0;">힐링 스파</h3>
        </div>
      </div>
      <div style="background:#f1f5f9;border:1px solid #e2e8f0;border-radius:10px;padding:10px;font-size:0.72rem;color:#475569;line-height:1.6;">
        <p id="quest-desc" style="margin:0;">설명</p>
      </div>
      <div style="display:flex;align-items:flex-start;gap:8px;background:#fffbeb;border:1px solid #fde68a;border-radius:10px;padding:10px;">
        <span style="font-size:1.2rem;">🎁</span>
        <div>
          <p style="font-size:0.58rem;font-weight:800;color:#d97706;text-transform:uppercase;margin:0;">상생 혜택</p>
          <p id="quest-reward-name" style="font-size:0.72rem;font-weight:700;color:#92400e;margin:3px 0 0;">-</p>
        </div>
      </div>
      <div style="margin-top:auto;display:flex;flex-direction:column;gap:7px;">
        <button onclick="openActiveQuestLink()" style="width:100%;background:linear-gradient(135deg,#059669,#047857);color:white;font-size:0.73rem;font-weight:800;padding:11px;border-radius:11px;border:none;cursor:pointer;">예약하러 가기 →</button>
        <button onclick="closeQuestPanel()" style="width:100%;background:#f1f5f9;color:#64748b;font-size:0.68rem;font-weight:700;padding:8px;border-radius:9px;border:1px solid #e2e8f0;cursor:pointer;">← 전체 지도</button>
      </div>
    </div>
  </div>

</div>

<style>
.filter-node { transition: opacity 0.3s, transform 0.25s; transform-origin: center bottom; }
.filter-node:hover { transform: scale(1.2) translateY(-4px); }
</style>

<!-- ===== 가맹점 상세 섹션 (클릭 시 펼침) ===== -->

    <!-- 펫과나 스토어 준비 중 -->
    <div id="section-store" class="hidden bg-gradient-to-r from-amber-50 to-orange-50 rounded-3xl p-5 border border-amber-100 shadow-sm text-center space-y-3">
        <div class="text-4xl">🛒</div>

        <div>
            <p class="text-sm font-black text-gray-800">펫과나 스토어 오픈 준비 중</p>
            <p class="text-xs text-gray-400 mt-1">반려동물 맞춤 용품·간식·장난감을 곧 만나보세요!</p>
        </div>
        <div class="inline-flex items-center gap-1.5 bg-brand-500 text-white text-xs font-black px-4 py-2 rounded-xl">
            <i class="fa-solid fa-bell"></i> 오픈 알림 신청
        </div>
    </div>

    <!-- 펫 스파 & 미용 -->
    <div id="section-healing-spa" class="hidden card-modern p-5 space-y-3">
        <div class="flex items-center justify-between border-b pb-3">
            <div class="flex items-center gap-2">
                <span class="text-3xl">🛁</span>
                <h3 class="font-black text-gray-800 text-base">펫 스파 & 미용</h3>
            </div>
            <button onclick="toggleShopSection('healing-spa')" class="text-gray-400 hover:text-gray-600" aria-label="닫기">
                <i class="fa-solid fa-xmark text-xl"></i>
            </button>
        </div>
        <div class="space-y-2">
            <a href="https://banjjakpet.com/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                class="flex items-center justify-between p-3 rounded-xl bg-emerald-50 hover:bg-emerald-100 transition-colors">
                <div>
                    <span class="block text-xs font-black text-emerald-800">반짝</span>
                    <span class="text-[10px] text-gray-400">지역별 그루머 검색·예약 플랫폼</span>
                </div>
                <i class="fa-solid fa-arrow-up-right-from-square text-emerald-400 text-xs"></i>
            </a>
            <a href="https://www.petvip.co.kr/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                class="flex items-center justify-between p-3 rounded-xl bg-emerald-50 hover:bg-emerald-100 transition-colors">
                <div>
                    <span class="block text-xs font-black text-emerald-800">펫VIP</span>
                    <span class="text-[10px] text-gray-400">출장 미용·목욕·스파 통합 서비스</span>
                </div>
                <i class="fa-solid fa-arrow-up-right-from-square text-emerald-400 text-xs"></i>
            </a>
        </div>
    </div>

    <!-- 반려동물 동반 캠핑 -->
    <div id="section-healing-camping" class="hidden card-modern p-5 space-y-3">
        <div class="flex items-center justify-between border-b pb-3">
            <div class="flex items-center gap-2">
                <span class="text-3xl">🏕️</span>
                <h3 class="font-black text-gray-800 text-base">반려동물 동반 캠핑</h3>
            </div>
            <button onclick="toggleShopSection('healing-camping')" class="text-gray-400 hover:text-gray-600" aria-label="닫기">
                <i class="fa-solid fa-xmark text-xl"></i>
            </button>
        </div>
        <div class="space-y-2">
            <a href="https://www.camfit.co.kr/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                class="flex items-center justify-between p-3 rounded-xl bg-green-50 hover:bg-green-100 transition-colors">
                <div>
                    <span class="block text-xs font-black text-green-800">캠핏</span>
                    <span class="text-[10px] text-gray-400">반려동물 동반 필터 전국 캠핑장 검색</span>
                </div>
                <i class="fa-solid fa-arrow-up-right-from-square text-green-400 text-xs"></i>
            </a>
            <a href="https://reservation.knps.or.kr/contents/withPet.do" target="_blank" rel="noopener"
                class="flex items-center justify-between p-3 rounded-xl bg-green-50 hover:bg-green-100 transition-colors">
                <div>
                    <span class="block text-xs font-black text-green-800">국립공원 반려견 동반 캠핑</span>
                    <span class="text-[10px] text-gray-400">국립공원 공식 예약 시스템</span>
                </div>
                <i class="fa-solid fa-arrow-up-right-from-square text-green-400 text-xs"></i>
            </a>
        </div>
    </div>

    <!-- 펫 마사지 & 테라피 -->
    <div id="section-healing-therapy" class="hidden card-modern p-5 space-y-3">
        <div class="flex items-center justify-between border-b pb-3">
            <div class="flex items-center gap-2">
                <span class="text-3xl">🌸</span>
                <h3 class="font-black text-gray-800 text-base">펫 마사지 & 테라피</h3>
            </div>
            <button onclick="toggleShopSection('healing-therapy')" class="text-gray-400 hover:text-gray-600" aria-label="닫기">
                <i class="fa-solid fa-xmark text-xl"></i>
            </button>
        </div>
        <div class="space-y-2">
            <a href="https://healschool.co.kr/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                class="flex items-center justify-between p-3 rounded-xl bg-pink-50 hover:bg-pink-100 transition-colors">
                <div>
                    <span class="block text-xs font-black text-pink-800">더힐테라피센터</span>
                    <span class="text-[10px] text-gray-400">반려동물 아로마·마사지 교육·상담</span>
                </div>
                <i class="fa-solid fa-arrow-up-right-from-square text-pink-400 text-xs"></i>
            </a>
        </div>
    </div>

    <!-- 펫호텔 (위탁) -->
    <div id="section-care-hotel" class="hidden card-modern p-5 space-y-3">
        <div class="flex items-center justify-between border-b pb-3">
            <div class="flex items-center gap-2">
                <span class="text-3xl">🏨</span>
                <h3 class="font-black text-gray-800 text-base">펫호텔 (위탁)</h3>
            </div>
            <button onclick="toggleShopSection('care-hotel')" class="text-gray-400 hover:text-gray-600" aria-label="닫기">
                <i class="fa-solid fa-xmark text-xl"></i>
            </button>
        </div>
        <div class="space-y-2">
            <a href="https://www.petliz.co.kr/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                class="flex items-center justify-between p-3 rounded-xl bg-brand-50 hover:bg-brand-100 transition-colors">
                <div>
                    <span class="block text-xs font-black text-brand-800">Petliz</span>
                    <span class="text-[10px] text-gray-400">청결도 4.8점 이상 펫호텔만 큐레이션</span>
                </div>
                <i class="fa-solid fa-arrow-up-right-from-square text-brand-400 text-xs"></i>
            </a>
        </div>
    </div>

    <!-- 동물병원 픽업·동행 -->
    <div id="section-care-hospital" class="hidden card-modern p-5 space-y-3">
        <div class="flex items-center justify-between border-b pb-3">
            <div class="flex items-center gap-2">
                <span class="text-3xl">🏥</span>
                <h3 class="font-black text-gray-800 text-base">동물병원 픽업·동행</h3>
            </div>
            <button onclick="toggleShopSection('care-hospital')" class="text-gray-400 hover:text-gray-600" aria-label="닫기">
                <i class="fa-solid fa-xmark text-xl"></i>
            </button>
        </div>
        <div class="space-y-2">
            <a href="https://www.petmeup.co.kr/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                class="flex items-center justify-between p-3 rounded-xl bg-red-50 hover:bg-red-100 transition-colors">
                <div>
                    <span class="block text-xs font-black text-red-800">펫미업</span>
                    <span class="text-[10px] text-gray-400">국내 1위 펫택시 · 병원 동행 케어</span>
                </div>
                <i class="fa-solid fa-arrow-up-right-from-square text-red-400 text-xs"></i>
            </a>
            <a href="https://hospital.fitpetmall.com/?utm_source=petnna&utm_medium=app&utm_campaign=petlife" target="_blank" rel="noopener"
                class="flex items-center justify-between p-3 rounded-xl bg-red-50 hover:bg-red-100 transition-colors">
                <div>
                    <span class="block text-xs font-black text-red-800">핏펫 병원 검색</span>
                    <span class="text-[10px] text-gray-400">진료과목별 근처 동물병원 검색·예약</span>
                </div>
                <i class="fa-solid fa-arrow-up-right-from-square text-red-400 text-xs"></i>
            </a>
        </div>
    </div>

</div>

<style>
/* 파도 애니메이션 */
.ocean-waves {
    background-image:
        radial-gradient(circle at 20% 50%, rgba(255,255,255,0.3) 0%, transparent 50%),
        radial-gradient(circle at 60% 70%, rgba(255,255,255,0.2) 0%, transparent 50%),
        radial-gradient(circle at 80% 30%, rgba(255,255,255,0.25) 0%, transparent 50%);
    animation: waves 10s ease-in-out infinite;
}

@keyframes waves {
    0%, 100% { transform: translateY(0); }
    50% { transform: translateY(-10px); }
}

/* 큰 섬 */
.island-main {
    position: relative;
    width: 400px;
    height: 400px;
    filter: drop-shadow(0 8px 24px rgba(0,0,0,0.2));
}

.island-bg {
    position: absolute;
    top: 50%;
    left: 50%;
    transform: translate(-50%, -50%);
    animation: float-island 6s ease-in-out infinite;
}

@keyframes float-island {
    0%, 100% { transform: translate(-50%, -50%) translateY(0); }
    50% { transform: translate(-50%, -50%) translateY(-15px); }
}

.shop-icons {
    position: absolute;
    top: 50%;
    left: 50%;
    width: 100%;
    height: 100%;
}

.shop-icon {
    position: absolute;
    background: none;
    border: none;
    cursor: pointer;
    transition: all 0.3s ease;
    outline: none;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.5rem;
}

.shop-icon:hover {
    transform: scale(1.15) translateY(-4px);
}

.shop-icon:active {
    transform: scale(1.05);
}

.shop-icon-circle {
    width: 80px;
    height: 80px;
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
    box-shadow: 0 4px 12px rgba(0,0,0,0.15);
    border: 4px solid white;
    animation: bounce 2s ease-in-out infinite;
}

.shop-icon:nth-child(1) .shop-icon-circle { animation-delay: 0s; }
.shop-icon:nth-child(2) .shop-icon-circle { animation-delay: 0.3s; }
.shop-icon:nth-child(3) .shop-icon-circle { animation-delay: 0.6s; }
.shop-icon:nth-child(4) .shop-icon-circle { animation-delay: 0.9s; }
.shop-icon:nth-child(5) .shop-icon-circle { animation-delay: 1.2s; }
.shop-icon:nth-child(6) .shop-icon-circle { animation-delay: 1.5s; }

.shop-label {
    font-size: 0.75rem;
    font-weight: 700;
    color: white;
    text-shadow: 0 2px 4px rgba(0,0,0,0.3);
    background: rgba(0,0,0,0.5);
    padding: 0.25rem 0.75rem;
    border-radius: 9999px;
}

/* 구름 떠다니기 */
@keyframes float {
    0%, 100% { transform: translateY(0) translateX(0); }
    50% { transform: translateY(-20px) translateX(10px); }
}

.animate-float {
    animation: float 6s ease-in-out infinite;
}

/* 새 날기 */
@keyframes fly {
    0% { transform: translateX(-100px); opacity: 0; }
    10% { opacity: 1; }
    90% { opacity: 1; }
    100% { transform: translateX(100vw); opacity: 0; }
}

.animate-fly {
    animation: fly 20s linear infinite;
}

/* 물고기 헤엄 */
@keyframes swim {
    0%, 100% { transform: translateX(0) scaleX(1); }
    50% { transform: translateX(30px) scaleX(-1); }
}

.animate-swim {
    animation: swim 4s ease-in-out infinite;
}
</style>
`;
