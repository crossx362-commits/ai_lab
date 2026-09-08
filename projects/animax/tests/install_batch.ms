-- 설치 스크립트를 배치로 실행해 startup 파일과 매크로가 만들어지는지 확인
(
fileIn (pathConfig.appendPath (pathConfig.removePathLeaf (getFilenamePath (getSourceFileName()))) "install\AniMax_install.ms")
local f = (getDir #userStartupScripts) + "\AniMax_startup.ms"
local ok = doesFileExist f
local py = python.Execute "import sys; 'animax.ui' in sys.modules"
format "STARTUP_EXISTS=% UI_LOADED=%\n" ok py
local o = createFile ((getFilenamePath (getSourceFileName())) + "install_result.txt")
format "STARTUP_EXISTS=% UI_LOADED=% FILE=%\n" ok py f to:o
close o
)
