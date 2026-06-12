import sys
sys.stdout.reconfigure(encoding='utf-8')

with open('js/templates/mypet.js', 'r', encoding='utf-8') as f:
    content = f.read()

# Find the saju card section by its unique id
marker1 = 'mypet-saju-card" style="display:none;"'
idx1 = content.find(marker1)

if idx1 == -1:
    print("Not found by style, looking for id...")
    marker1 = 'id="mypet-saju-card"'
    idx1 = content.find(marker1)
    # Go back to find the <!-- comment start
    comment_start = content.rfind('<!--', 0, idx1)
    idx1 = comment_start
    print(f"Found at comment: {idx1}")
else:
    # Go back to find the <!-- comment start
    comment_start = content.rfind('<!--', 0, idx1)
    idx1 = comment_start

# Find closing of the header div (after </h3> and </div>)
h3_end = content.find('</div>', idx1)
h3_end = content.find('</div>', h3_end + 6)  # skip inner h3 closing
idx2_end = h3_end + len('</div>')

new_header = (
    '<!-- \U0001f496 \ud3ab & \uc9d1\uc0ac \uc601\ud63c \uc870\ud654\ub3c4 \uce74\ub4dc -->\n'
    '        <div id="mypet-saju-card"\n'
    '            class="bg-white rounded-3xl p-5 border border-rose-100 shadow-sm space-y-4">\n'
    '            <div class="flex justify-between items-center pb-2 border-b border-gray-100">\n'
    '                <h3 class="font-black text-gray-800 text-sm flex items-center">\n'
    '                    <i class="fa-solid fa-heart-pulse text-rose-500 mr-2"></i>\ud3ab &amp; \uc9d1\uc0ac \uc601\ud63c \uc870\ud654\ub3c4 \U0001f496\n'
    '                </h3>\n'
    '                <button onclick="switchTab(\'saju\'); setTimeout(() => switchSajuSubTab(\'harmony\'), 200)" class="text-[10px] font-black text-rose-500 hover:text-rose-700 border border-rose-200 px-2 py-0.5 rounded-lg hover:bg-rose-50 transition-all">\uc870\ud654\ub3c4 \ubd84\uc11d \u2192</button>\n'
    '            </div>'
)

content = content[:idx1] + new_header + content[idx2_end:]
with open('js/templates/mypet.js', 'w', encoding='utf-8') as f:
    f.write(content)
print("Done!")
