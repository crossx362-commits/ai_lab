import sys
import os

sys.stdout.reconfigure(encoding='utf-8')

# Set path
_here = r"d:\ai_lab\projects\ai-team\skills\아린_관리자\tools"
os.chdir(_here)
sys.path.insert(0, _here)

_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, "reports")):
        break
    _root = os.path.dirname(_root)
sys.path.insert(0, _root)

print(f"Working dir: {os.getcwd()}")
print(f"Root: {_root}\n")

errors = []

# Test 1: uploader
try:
    from uploader import InstaUploader, load_env, ensure_token_fresh
    print('✅ uploader')
except Exception as e:
    errors.append(f'❌ uploader: {e}')
    print(f'❌ uploader: {e}')

# Test 2: telegram_notifier
try:
    from _shared.telegram_notifier import send_telegram_message
    print('✅ _shared.telegram_notifier')
except Exception as e:
    errors.append(f'❌ _shared.telegram_notifier: {e}')
    print(f'❌ _shared.telegram_notifier: {e}')

# Test 3: prompt_crafter
try:
    from prompt_crafter import craft_insta_prompt
    print('✅ prompt_crafter')
except Exception as e:
    errors.append(f'❌ prompt_crafter: {e}')
    print(f'❌ prompt_crafter: {e}')

# Test 4: 가희_검수관
try:
    import importlib.util as _ilu
    _gahee_path = os.path.join(_here, "..", "..", "가희_검수관", "tools", "content_inspector.py")
    _gahee_path = os.path.abspath(_gahee_path)
    print(f"\n가희 경로: {_gahee_path}")

    if os.path.exists(_gahee_path):
        _gahee_spec = _ilu.spec_from_file_location("content_inspector", _gahee_path)
        _gahee = _ilu.module_from_spec(_gahee_spec)
        _gahee_spec.loader.exec_module(_gahee)
        print('✅ 가희_검수관 content_inspector')
    else:
        errors.append(f'❌ 가희 경로 없음: {_gahee_path}')
        print(f'❌ 가희 경로 없음: {_gahee_path}')
except Exception as e:
    errors.append(f'❌ 가희_검수관: {e}')
    print(f'❌ 가희_검수관: {e}')

# Test 5: shorts_pipeline (if exists)
try:
    shorts_path = os.path.join(_here, "shorts_pipeline.py")
    if os.path.exists(shorts_path):
        print(f'\n✅ shorts_pipeline.py 존재')
    else:
        print(f'\n⚠️  shorts_pipeline.py 없음')
except Exception as e:
    print(f'⚠️  shorts_pipeline 확인 실패: {e}')

print(f'\n{"="*50}')
if errors:
    print(f'❌ 총 {len(errors)}개 에러:')
    for err in errors:
        print(f'  {err}')
else:
    print('✅ 모든 모듈 정상 연결')
