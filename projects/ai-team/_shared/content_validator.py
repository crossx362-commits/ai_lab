"""
content_validator.py - 콘텐츠 검증 로직 통합

가희 검수관과 경수 수사관에서 중복되는 콘텐츠 검증 로직을 통합합니다.
"""
from typing import Dict, List, Optional


class ContentValidator:
    """콘텐츠 검증 통합 클래스"""

    # 금지 키워드 (YouTube 및 Instagram 공통)
    BANNED_PHRASES = [
        "lofi", "lo-fi", "lo fi", "study beats", "chill beats", "sleep music",
        "white noise", "ambient study", "focus music", "chillhop",
        "인공지능", "ai 생성", "ai-generated", "미래", "로봇", "테크", "tech",
        "네온", "neon", "neon-lit", "cyberpunk"
    ]

    # 경고 키워드 (사용 주의)
    WARNING_PHRASES = [
        "무료", "free", "다운로드", "download",
        "구독", "subscribe", "좋아요", "like"
    ]

    # YouTube 메타데이터 제한
    YOUTUBE_LIMITS = {
        "title_min": 10,
        "title_max": 100,
        "description_min": 20,
        "description_max": 5000,
        "tags_min": 5,
        "tags_max": 30,
        "tag_length_max": 30
    }

    # Instagram 메타데이터 제한
    INSTAGRAM_LIMITS = {
        "caption_max": 2200,
        "hashtags_max": 30,
        "hashtag_length_max": 30
    }

    def __init__(self):
        self.violations = []
        self.warnings = []

    def validate_youtube(
        self,
        title: str,
        description: str,
        tags: List[str]
    ) -> Dict[str, any]:
        """
        YouTube 메타데이터 검증

        Args:
            title: 동영상 제목
            description: 동영상 설명
            tags: 태그 리스트

        Returns:
            Dict: {"status": "PASS"|"FAIL", "violations": [...], "warnings": [...]}
        """
        self.violations = []
        self.warnings = []

        # 제목 검증
        self._check_title(title, platform="youtube")

        # 설명 검증
        self._check_description(description, platform="youtube")

        # 태그 검증
        self._check_tags(tags, platform="youtube")

        # 금지 키워드 검사
        self._check_banned_phrases(title, "제목")
        self._check_banned_phrases(description, "설명")
        for tag in tags:
            self._check_banned_phrases(tag, "태그")

        return {
            "status": "PASS" if not self.violations else "FAIL",
            "violations": self.violations,
            "warnings": self.warnings
        }

    def validate_instagram(
        self,
        caption: str,
        hashtags: List[str]
    ) -> Dict[str, any]:
        """
        Instagram 캡션 및 해시태그 검증

        Args:
            caption: 게시물 캡션
            hashtags: 해시태그 리스트

        Returns:
            Dict: {"status": "PASS"|"FAIL", "violations": [...], "warnings": [...]}
        """
        self.violations = []
        self.warnings = []

        # 캡션 길이 검증
        if len(caption) > self.INSTAGRAM_LIMITS["caption_max"]:
            self.violations.append(
                f"캡션 길이 초과: {len(caption)}자 (최대 {self.INSTAGRAM_LIMITS['caption_max']}자)"
            )

        # 해시태그 검증
        if len(hashtags) > self.INSTAGRAM_LIMITS["hashtags_max"]:
            self.violations.append(
                f"해시태그 개수 초과: {len(hashtags)}개 (최대 {self.INSTAGRAM_LIMITS['hashtags_max']}개)"
            )

        for tag in hashtags:
            if len(tag) > self.INSTAGRAM_LIMITS["hashtag_length_max"]:
                self.violations.append(
                    f"해시태그 길이 초과: '{tag}' ({len(tag)}자, 최대 {self.INSTAGRAM_LIMITS['hashtag_length_max']}자)"
                )

        # 금지 키워드 검사
        self._check_banned_phrases(caption, "캡션")
        for tag in hashtags:
            self._check_banned_phrases(tag, "해시태그")

        return {
            "status": "PASS" if not self.violations else "FAIL",
            "violations": self.violations,
            "warnings": self.warnings
        }

    def _check_title(self, title: str, platform: str = "youtube"):
        """제목 길이 검증"""
        limits = self.YOUTUBE_LIMITS if platform == "youtube" else {}
        min_len = limits.get("title_min", 10)
        max_len = limits.get("title_max", 100)

        if len(title) < min_len:
            self.violations.append(f"제목이 너무 짧음: {len(title)}자 (최소 {min_len}자)")
        if len(title) > max_len:
            self.violations.append(f"제목이 너무 김: {len(title)}자 (최대 {max_len}자)")

    def _check_description(self, description: str, platform: str = "youtube"):
        """설명 길이 검증"""
        limits = self.YOUTUBE_LIMITS if platform == "youtube" else {}
        min_len = limits.get("description_min", 20)
        max_len = limits.get("description_max", 5000)

        if len(description) < min_len:
            self.violations.append(f"설명이 너무 짧음: {len(description)}자 (최소 {min_len}자)")
        if len(description) > max_len:
            self.violations.append(f"설명이 너무 김: {len(description)}자 (최대 {max_len}자)")

    def _check_tags(self, tags: List[str], platform: str = "youtube"):
        """태그 개수 및 길이 검증"""
        limits = self.YOUTUBE_LIMITS if platform == "youtube" else {}
        min_count = limits.get("tags_min", 5)
        max_count = limits.get("tags_max", 30)
        max_tag_len = limits.get("tag_length_max", 30)

        if len(tags) < min_count:
            self.violations.append(f"태그 개수 부족: {len(tags)}개 (최소 {min_count}개)")
        if len(tags) > max_count:
            self.violations.append(f"태그 개수 초과: {len(tags)}개 (최대 {max_count}개)")

        for tag in tags:
            if len(tag) > max_tag_len:
                self.violations.append(f"태그 길이 초과: '{tag}' ({len(tag)}자)")

    def _check_banned_phrases(self, text: str, context: str):
        """금지 키워드 검사"""
        text_lower = text.lower()

        # 금지 키워드
        for phrase in self.BANNED_PHRASES:
            if phrase in text_lower:
                self.violations.append(f"{context}에 금지 키워드 포함: '{phrase}'")

        # 경고 키워드
        for phrase in self.WARNING_PHRASES:
            if phrase in text_lower:
                self.warnings.append(f"{context}에 주의 키워드 포함: '{phrase}'")

    def check_duplicate_words(self, text: str, stop_words: Optional[List[str]] = None) -> List[str]:
        """
        본문 내 중복 단어 검사 (2글자 이상)

        Args:
            text: 검사할 텍스트
            stop_words: 제외할 불용어 리스트

        Returns:
            List[str]: 중복된 단어 목록
        """
        if stop_words is None:
            stop_words = {
                "있는", "합니다", "한다", "그리고", "에서", "으로",
                "이다", "하고", "했다", "하는", "추천", "the", "and", "is"
            }

        import re
        words = re.findall(r'[a-zA-Z가-힣]{2,}', text.lower())

        counts = {}
        repeated = []

        for w in words:
            if w in stop_words:
                continue
            counts[w] = counts.get(w, 0) + 1
            if counts[w] > 1 and w not in repeated:
                repeated.append(w)

        return repeated


# 간편 사용을 위한 헬퍼 함수
def validate_youtube_content(title: str, description: str, tags: List[str]) -> Dict[str, any]:
    """YouTube 콘텐츠 검증 (간편 함수)"""
    validator = ContentValidator()
    return validator.validate_youtube(title, description, tags)


def validate_instagram_content(caption: str, hashtags: List[str]) -> Dict[str, any]:
    """Instagram 콘텐츠 검증 (간편 함수)"""
    validator = ContentValidator()
    return validator.validate_instagram(caption, hashtags)
