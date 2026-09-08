import os, sys, traceback
HERE=os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.dirname(HERE))
out=[]
try:
    from PySide6 import QtWidgets
    app = QtWidgets.QApplication.instance()
    out.append("qapp exists: %s" % (app is not None))
    if app is None:
        app = QtWidgets.QApplication([])
    from animax import ui
    out.append("ui import OK")
    panel = ui.AniMaxPanel()
    out.append("panel built: sections=%d" % panel.vbox.count())
    # 슬라이더 begin/update/end 를 위젯 경로로 한 번 돌려본다 (선택 없음 -> 오류 상태 표시가 정상 경로)
    from pymxs import runtime as rt
    rt.resetMaxFile(rt.Name("noPrompt"))
    bip = rt.biped.createNew(100,-90,rt.Point3(0,0,0)); lth = rt.getNodeByName("Bip001 L Thigh")
    from animax.core import Track, Pose
    tr=Track(lth); b=tr.sample(0)
    tr.write(10, Pose(b.pos, rt.quat(rt.eulerAngles(0,0,40))*b.rot), True); tr.write(30, Pose(b.pos, rt.quat(rt.eulerAngles(0,0,-40))*b.rot), True)
    rt.sliderTime = 20; rt.select([lth])
    ok = panel._slider_begin("tween"); panel._slider_update(-1.0); panel._slider_end()
    out.append("widget slider path: begin=%s key20=%s status=%s" % (ok, 20 in tr.key_times(), panel.status_lbl.text()))
    panel._mirror(); out.append("mirror via panel: %s" % panel.status_lbl.text())
    panel._spacify("world"); out.append("spacify via panel: %s" % panel.status_lbl.text())
    panel._spacify_discard(); out.append("discard via panel: %s" % panel.status_lbl.text())
except Exception:
    out.append("TB " + traceback.format_exc())
open(os.path.join(HERE,"import_ui_result.txt"),"w",encoding="utf-8").write("\n".join(out))
