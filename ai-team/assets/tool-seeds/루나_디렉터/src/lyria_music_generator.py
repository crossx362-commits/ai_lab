"""
루나 Lyria 래퍼 — lyria_music_gen.py 가 단일 출처.
"""
import os
import datetime
import importlib.util

_luna_path = os.path.abspath(
    os.path.join(os.path.dirname(__file__), "..", "lyria_music_gen.py")
)
_spec = importlib.util.spec_from_file_location("lyria_music_gen", _luna_path)
_mod  = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_mod)
generate_music_with_lyria = _mod.generate_music_with_lyria


class LyriaMusicGenerator:
    def __init__(self, api_key: str = None):
        if api_key:
            os.environ["GEMINI_API_KEY"] = api_key

    def generate_music(self, prompt: str, output_path: str = None, is_pro: bool = True) -> str:
        if output_path:
            out_dir  = os.path.dirname(os.path.abspath(output_path)) or "output"
            filename = os.path.basename(output_path)
        else:
            out_dir  = "output"
            ts       = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
            filename = f"bgm_{ts}.mp3"

        result = generate_music_with_lyria(prompt, output_filename=filename, is_pro=is_pro, output_dir=out_dir)
        return result or ""

