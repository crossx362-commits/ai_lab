"""
prompt_crafter.py — 인스타그램용 고퀄리티 이미지 프롬프트 제작 모듈
테마: 자연(산/바다/사계절)/동물/인물/음식/여행 — tech/미래 테마 제외
지식 파일: knowledge/insta_prompt_craft_knowledge.md
"""
import random
import datetime

# ─── 지식 파일 기반 품질 공통 수식어 (knowledge §3 원칙 3) ──────────────────────
_KNOWLEDGE_QUALITY_SUFFIX = (
    "masterpiece, exquisite and intricate details, vibrant and rich tones, "
    "sharp focus, perfect artistic composition, cinematic ambience"
)

# ─── 카테고리 판별 키워드 맵 ──────────────────────────────────────────────────
CATEGORY_KEYWORDS = {
    "season_spring": [
        "봄", "벚꽃", "유채꽃", "개나리", "진달래", "봄꽃", "봄날", "cherry blossom",
        "spring", "꽃구경", "봄나들이", "봄여행", "tulip", "튤립",
    ],
    "season_summer": [
        "여름", "해수욕장", "바다", "해변", "수영", "휴가", "여름휴가", "summer",
        "ocean", "beach", "파도", "서핑", "수상스포츠", "바베큐", "장마", "열대",
    ],
    "season_autumn": [
        "가을", "단풍", "낙엽", "억새", "코스모스", "autumn", "fall", "단풍여행",
        "가을단풍", "가을하늘", "harvest", "추수", "가을나들이",
    ],
    "season_winter": [
        "겨울", "눈", "설경", "스키", "스노보드", "크리스마스", "winter", "snow",
        "ice", "눈썰매", "한파", "겨울여행", "snowfall", "서리", "빙하",
    ],
    "mountain": [
        "산", "등산", "트레킹", "산봉우리", "산정상", "백두산", "한라산", "설악산",
        "지리산", "mountain", "hiking", "peak", "ridge", "계곡", "폭포", "암벽",
    ],
    "landscape": [
        "자연", "우주", "풍경", "오로라", "하늘", "구름", "숲", "강", "호수",
        "일몰", "일출", "사막", "행성", "은하", "landscape", "scenery", "야경",
        "섬", "들판", "초원",
    ],
    "animal": [
        "고양이", "강아지", "동물", "새", "반려", "펫", "cat", "dog",
        "토끼", "곰", "여우", "늑대", "사자", "호랑이", "판다", "코알라",
        "앵무새", "물고기", "고래", "돌고래", "말", "사슴", "다람쥐",
    ],
    "person": [
        "패션", "스타일", "뷰티", "셀카", "인물", "오오티디", "룩", "ootd",
        "모델", "셀피", "메이크업", "코디", "헤어", "스킨케어", "피트니스",
        "라이프스타일", "포즈", "포트레이트", "인플루언서",
    ],
    "food": [
        "음식", "카페", "디저트", "케이크", "커피", "라떼", "맛집", "브런치",
        "파스타", "스시", "샐러드", "쿠키", "초콜릿", "아이스크림", "빵", "베이커리",
        "food", "cafe", "dessert", "recipe", "요리", "레스토랑",
    ],
    "travel": [
        "여행", "제주", "바르셀로나", "파리", "도쿄", "뉴욕", "방콕", "발리",
        "해외여행", "국내여행", "캠핑", "관광", "travel", "trip", "vacation",
        "adventure", "배낭", "관광지",
    ],
}

# ─── 카테고리별 스타일 풀 (매 호출마다 랜덤 선택) ──────────────────────────────
TYPE_PROMPT_POOLS = {
    "season_spring": [
        "Hyper-realistic spring photography, cherry blossoms in full bloom, soft pink petals, warm gentle sunlight,",
        "Dreamy spring landscape, flower meadow stretching to the horizon, pastel hues, fresh and alive,",
        "Macro photography, delicate spring flower petals covered in morning dew, ultra-detailed, luminous,",
        "Golden hour spring forest path, dappled sunlight through new leaves, warm greens and yellows,",
        "Aerial view of blooming flower fields, patchwork of vivid spring colors, breathtaking scale,",
        "Misty spring morning, cherry blossom trees along a quiet river, soft reflections, peaceful,",
        "DSLR spring portrait style, bokeh cherry blossoms background, warm diffused natural light,",
    ],
    "season_summer": [
        "Vibrant summer beach photography, crystal turquoise water, white sand, tropical warmth, 8K,",
        "Long exposure ocean waves, dramatic coastal cliffs, golden summer sunset, powerful and serene,",
        "Underwater ocean photography, colorful coral reef, tropical fish, crystal clear blue water,",
        "Sunflower field photography, endless golden blooms, bright blue summer sky, joyful and radiant,",
        "Summer forest waterfall, lush green canopy, cool mist, shafts of sunlight through trees,",
        "Aerial drone summer coastline, emerald ocean gradient, sandy shores, birds eye paradise,",
        "Dramatic summer storm photography, lightning over the ocean, powerful cloud formations,",
    ],
    "season_autumn": [
        "Hyper-realistic autumn foliage, fiery red and orange maple canopy, forest path, magical,",
        "Golden autumn hillside, warm amber and crimson leaves, soft afternoon glow, National Geographic,",
        "Misty autumn morning, fog in the valley, colorful treetops emerging, ethereal and serene,",
        "Macro autumn leaf photography, vivid red veins, raindrops on surface, incredible micro detail,",
        "Aerial autumn forest, patchwork of red gold green, sweeping panorama, breathtaking scale,",
        "Autumn riverside reflection, colorful trees mirrored in still water, symmetric perfection,",
        "Country road autumn, fallen leaves blanketing the path, warm glowing light, peaceful solitude,",
    ],
    "season_winter": [
        "Hyper-realistic winter landscape, pristine white snow, frost-covered trees, silent and serene,",
        "Blue hour winter photography, snow-covered village, warm glowing windows, magical atmosphere,",
        "Frozen lake at sunrise, ice crystals sparkling, dramatic pink and orange sky, otherworldly,",
        "Snowfall photography, large soft snowflakes, peaceful forest silence, ultra-detailed texture,",
        "Winter mountain summit, above the clouds, pure white expanse, epic and majestic scale,",
        "Ice cave photography, stunning blue ice formations, ethereal glow, spectacular geology,",
        "Starry winter night, Milky Way over snow-covered landscape, breathtaking celestial beauty,",
    ],
    "mountain": [
        "Epic mountain peak photography, dramatic rocky summit, sweeping panoramic vista, clouds below,",
        "Golden hour mountain landscape, warm light on ridgelines, deep valley shadows, majestic,",
        "Long exposure mountain waterfall, silky smooth water, mossy rocks, lush green surroundings,",
        "Misty mountain morning, layers of fog-filled valleys, serene and ethereal atmosphere,",
        "Aerial drone mountain photography, jagged peaks, deep gorges, sense of immense scale,",
        "Winter mountain at sunrise, snow-capped peak glowing pink, alpenglow, awe-inspiring beauty,",
        "Mountain trail photography, wildflowers lining the path, distant summits, sense of adventure,",
        "Dramatic mountain storm, dark clouds breaking, shafts of light piercing through, powerful,",
    ],
    "landscape": [
        "Hyper-realistic DSLR landscape, golden hour, Sony A7R V, 8K, National Geographic quality,",
        "Long-exposure night sky, Milky Way reflected in still lake, magical blue hour stars,",
        "Aerial drone photography, bird's-eye panorama, lush vibrant colors, sweeping wide angle,",
        "Misty morning landscape, soft diffused light, ethereal fog rolling through valleys,",
        "Aurora borealis photography, vivid green and purple lights, starry sky, frozen tundra,",
        "Tilt-shift photography, miniature world effect, colorful and whimsical creative perspective,",
        "Tropical paradise, lush green jungle, hidden waterfall, vivid emerald and turquoise tones,",
        "Desert landscape photography, sand dunes at golden hour, sweeping curves, warm rich tones,",
        "Coastal cliff photography, dramatic sea stacks, crashing waves, powerful ocean energy,",
    ],
    "animal": [
        "Ultra-photorealistic wildlife portrait, shallow depth of field, golden backlight, National Geographic,",
        "Adorable close-up animal portrait, soft studio lighting, fluffy detailed fur, heartwarming,",
        "Action wildlife photography, animal in motion, fast shutter, dynamic powerful energy,",
        "Underwater photography, colorful marine life, crystal turquoise water, magical ocean world,",
        "Macro photography, extreme close-up animal eye, incredible micro detail, mesmerizing patterns,",
        "Winter wildlife, animal in fresh snow, soft blue-white tones, serene peaceful scene,",
        "Playful animal candid, caught mid-action, joyful moment, warm sunlight, irresistibly cute,",
        "Bird photography, vivid plumage, perched on branch, bokeh forest background, stunning colors,",
        "Baby animal photography, tiny and adorable, soft natural light, utterly heartwarming,",
    ],
    "person": [
        "Candid lifestyle portrait, warm golden afternoon sunlight, genuine unposed laughter,",
        "Fashion editorial photograph, bold vivid colors, high contrast, Vogue magazine quality,",
        "Moody cinematic portrait, vintage film grain, muted earthy tones, artistic depth,",
        "Bright airy lifestyle photo, soft window light, minimal clean white aesthetic, fresh look,",
        "Urban street style photography, gritty city backdrop, dynamic confident composition,",
        "Film photography aesthetic, warm Kodak grain, nostalgic 90s mood, authentic vibe,",
        "Dramatic studio portrait, single Rembrandt light, powerful deep shadows, high fashion,",
        "Soft dreamy portrait, pastel tones, flower field background, romantic and serene,",
        "Active outdoor lifestyle photo, adventure energy, bright vivid natural colors,",
    ],
    "food": [
        "Professional food photography, flat lay overhead shot, soft natural window light, artful garnish,",
        "Moody dark food photography, dramatic side lighting, rich jewel tones, fine dining quality,",
        "Overhead food styling, vibrant fresh ingredients, rustic wooden table, appetizing colors,",
        "Extreme close-up macro food, incredible texture detail, steam wisping, mouth-watering,",
        "Bright minimal food photography, pure white background, pastel accents, clean modern aesthetic,",
        "Cafe latte art photography, warm cozy tones, steam rising, blurred bokeh background,",
        "Street food photography, authentic local scene, vibrant market colors, documentary style,",
        "Dessert glamour shot, perfect plating, pastel palette, sparkling sugar details,",
    ],
    "travel": [
        "Cinematic travel photography, vast landscape, golden sunset light, wanderlust atmosphere,",
        "Authentic street photography, local culture and people, vivid colors, documentary realism,",
        "Architecture photography, bold geometric lines, dramatic perspective, symmetry and scale,",
        "Beach travel photography, turquoise clear water, white sand, tropical paradise warmth,",
        "Night city travel, long exposure light trails, neon reflections on wet streets, electric energy,",
        "Backpacker adventure travel, rugged mountain trail, authentic journey, sweeping vista,",
        "Cultural heritage travel, ancient architecture, warm golden light, rich history and texture,",
        "Hidden gem travel spot, off the beaten path, lush untouched scenery, sense of discovery,",
    ],
}

# ─── 품질 수식어 풀 (랜덤 선택) ────────────────────────────────────────────────
QUALITY_SUFFIX_POOL = [
    "masterpiece, highly detailed, tack sharp focus, professional composition, award-winning",
    "stunning realism, exquisite detail, ultra-sharp, beautiful natural colors, perfect lighting, 8K",
    "cinematic quality, rich depth of field, fine art photography, vibrant tones, exceptional clarity",
    "breathtaking, photojournalistic quality, vivid colors, textural richness, superb composition",
    "high resolution, gallery-worthy, technically perfect, beautiful aesthetic, magazine quality",
    "visually striking, dynamic range, lush colors, precise detail, deeply immersive scene",
    "ultra-realistic, professional grade, rich atmosphere, bold composition, emotionally resonant",
]


def detect_category(topic: str) -> str:
    """
    트렌드 키워드에서 카테고리 자동 판별.
    우선순위: 사계절 > 산 > 음식 > 여행 > 동물 > 인물 > 자연풍경
    tech/미래 테마는 landscape로 처리.
    """
    topic_lower = topic.lower()
    for category in [
        "season_spring", "season_summer", "season_autumn", "season_winter",
        "mountain", "food", "travel", "animal", "landscape", "person",
    ]:
        for kw in CATEGORY_KEYWORDS[category]:
            if kw in topic_lower:
                return category
    # 매핑 안 되는 트렌드 → 현재 계절 가중치 적용 랜덤 선택
    month = datetime.date.today().month
    if month in (3, 4, 5):
        seasonal = ["season_spring"] * 4
    elif month in (6, 7, 8):
        seasonal = ["season_summer"] * 4
    elif month in (9, 10, 11):
        seasonal = ["season_autumn"] * 4
    else:
        seasonal = ["season_winter"] * 4
    pool = seasonal + ["mountain", "landscape", "landscape", "food", "travel", "animal", "animal", "person"]
    return random.choice(pool)


def build_narrative(topic: str, category: str) -> str:
    """카테고리와 실제 토픽을 활용해 다양한 서사 묘사를 동적 생성."""
    t = topic[:60]
    narratives = {
        "season_spring": [
            f"A breathtaking spring scene of {t}, cherry blossoms dancing in warm gentle breeze",
            f"The magical spring beauty of {t}, soft pink petals blanketing the landscape",
            f"A dreamy spring afternoon at {t}, golden sunlight filtering through blooming flowers",
            f"A serene spring morning at {t}, misty air, fresh greens, life awakening everywhere",
        ],
        "season_summer": [
            f"A vibrant summer day at {t}, sparkling ocean waves, warm sun on golden sand",
            f"The lively summer energy of {t}, vivid colors, carefree and joyful atmosphere",
            f"A dramatic summer sunset at {t}, fiery orange and purple sky over calm water",
            f"A refreshing summer scene of {t}, cool mountain streams, lush green canopy",
        ],
        "season_autumn": [
            f"A stunning autumn landscape of {t}, blazing red and gold foliage everywhere",
            f"The peaceful autumn beauty of {t}, crisp air, warm amber light through the trees",
            f"A magical autumn morning at {t}, soft mist, colorful leaves floating gently down",
            f"A grand autumn panorama of {t}, endless fiery colors across rolling hills",
        ],
        "season_winter": [
            f"A pristine winter wonderland of {t}, untouched white snow, absolute tranquil silence",
            f"The magical winter scene of {t}, frost-covered trees sparkling in morning light",
            f"A cozy winter evening at {t}, warm glowing lights against blue dusk and fresh snow",
            f"A dramatic winter landscape of {t}, vast white expanse, dramatic sky above",
        ],
        "mountain": [
            f"A majestic mountain view of {t}, rugged peaks piercing through the clouds, epic scale",
            f"The breathtaking summit of {t}, panoramic vista in every direction, overwhelming beauty",
            f"A dramatic mountain trail scene at {t}, wildflowers, rushing streams, sense of adventure",
            f"Golden hour on {t}, warm alpenglow on rocky ridgelines, deep violet valley shadows",
        ],
        "landscape": [
            f"A breathtaking scene of {t}, cinematic composition, colors vibrant and alive",
            f"The stunning natural beauty of {t}, golden light cascading over rich textures",
            f"A dramatic awe-inspiring view of {t}, sweeping perspective, rich deep atmosphere",
            f"A serene and magical {t}, soft ethereal light, peaceful and deeply immersive",
            f"An epic wide-angle capture of {t}, grand scale, overwhelmingly beautiful",
        ],
        "animal": [
            f"An adorable expressive portrait of a {t}, joyful energy, heartwarming and vivid",
            f"A majestic {t} in its natural habitat, raw beauty, powerful commanding presence",
            f"A cute playful {t} caught in a perfect candid moment, soft warm natural light",
            f"A striking close-up of {t}, intense gaze, incredible texture and fine detail",
        ],
        "person": [
            f"A joyful {t} lifestyle moment, genuine unposed smile, vibrant sun-drenched setting",
            f"A stylish {t} fashion scene, fresh color palette, confident charismatic energy",
            f"An authentic candid {t} portrait, emotional depth, beautiful natural light",
            f"A dynamic {t} outdoor portrait, bold composition, warm golden tones, full of life",
        ],
        "food": [
            f"A beautifully styled {t}, vibrant fresh ingredients, delicious and deeply appetizing",
            f"An artful close-up of {t}, rich textures, culinary craftsmanship on full display",
            f"A cozy inviting scene featuring {t}, warm atmosphere, irresistible mouth-watering detail",
            f"A dramatic {t} hero shot, gleaming surfaces, perfect plating, restaurant-worthy",
        ],
        "travel": [
            f"A stunning travel destination scene of {t}, wanderlust-inspiring, vibrant with character",
            f"A breathtaking view of {t}, rich culture and natural beauty perfectly combined",
            f"An adventurous {t} travel moment, authentic local atmosphere, unforgettable scenery",
            f"A cinematic {t} landscape, golden hour warmth, sense of freedom and discovery",
        ],
    }
    options = narratives.get(category, [f"A compelling visual of {t}, rich professional quality"])
    return random.choice(options)


def craft_insta_prompt(topic: str, topic_type: str = "auto") -> str:
    """
    인스타그램용 이미지 생성 프롬프트 반환.
    매 호출마다 스타일 풀에서 랜덤 선택 → 다양한 이미지 생성.
    tech/미래 테마 제외 — 자연/사계절/산/바다/동물/인물/음식/여행만 사용.

    Args:
        topic: 트렌드 키워드 (한국어/영어)
        topic_type: 'auto' | 'landscape' | 'animal' | 'person' | 'food' | 'travel'
                    | 'season_spring' | 'season_summer' | 'season_autumn' | 'season_winter' | 'mountain'
    """
    category = topic_type if topic_type != "auto" else detect_category(topic)
    pool = TYPE_PROMPT_POOLS.get(category, TYPE_PROMPT_POOLS["landscape"])
    type_prompt = random.choice(pool)
    narrative = build_narrative(topic, category)
    # knowledge §3 원칙3: 품질 수식어 풀 + 지식 파일 공통 수식어 중 랜덤 선택
    quality = random.choice(QUALITY_SUFFIX_POOL + [_KNOWLEDGE_QUALITY_SUFFIX])
    # 실사화 수식어 — 모든 이미지에 전역 적용 (CEO 지시 2026-05-28)
    realism = "photorealistic, shot on Sony A7R V DSLR, 85mm f/1.4 lens, natural lighting, hyper-detailed skin/texture,"
    final_prompt = f"{realism} {type_prompt} {narrative}, {quality}"
    print(f"🎯 [Prompt Crafter] 카테고리: {category.upper()} | 프롬프트 길이: {len(final_prompt)}자")
    return final_prompt


if __name__ == "__main__":
    test_cases = [
        "제주도 한라봄 산책길과 유채 숲속풍경",
        "설악산 단풍과 가을 계곡",
        "겨울 눈꽃 설경과 온천",
        "여름 바다 해변 서핑",
        "강아지와 함께하는 카페 브런치",
        "도쿄 골목 여행 스냅",
        "오오티디 패션 봄 코디",
    ]
    for t in test_cases:
        print(f"\n📌 주제: {t}")
        print(craft_insta_prompt(t))
        print(craft_insta_prompt(t))
        print("-" * 80)
