import sys
# Add the project path that contains the _shared package
sys.path.insert(0, r'd:/ai-lab/projects/ai-team')

try:
    from _shared.ollama_client import is_available, chat
    print('Ollama available:', is_available())
    # Simple chat test
    response = chat('Say OK', task='test', max_tokens=5)
    print('Chat response:', response)
except Exception as e:
    print('Error during Ollama check:', e)
