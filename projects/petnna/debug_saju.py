with open('js/templates/mypet.js', 'r', encoding='utf-8') as f:
    content = f.read()

idx = content.find('mypet-saju-card')
chunk = content[max(0,idx-100):idx+500]
with open('saju_card_debug.txt', 'w', encoding='utf-8') as f:
    f.write(chunk)
print("Written to saju_card_debug.txt")
