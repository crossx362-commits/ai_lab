"""AniMax 도크 UI (PySide6, 3ds Max 2025).

    import animax.ui; animax.ui.show()
"""
import os
import traceback

from PySide6 import QtWidgets, QtCore, QtGui
from pymxs import runtime as rt

from . import core, sliders, keys, pose, spacify, arc, review
from .core import Track

_DOCK = [None]


def _main_window():
    try:
        import qtmax
        return qtmax.GetQMaxMainWindow()
    except Exception:
        return None


# ----------------------------------------------------------------- 위젯 유틸
class Section(QtWidgets.QWidget):
    """접을 수 있는 그룹."""

    def __init__(self, title, parent=None, open_=True):
        super().__init__(parent)
        lay = QtWidgets.QVBoxLayout(self)
        lay.setContentsMargins(0, 0, 0, 0)
        lay.setSpacing(2)
        self.btn = QtWidgets.QToolButton(text=title, checkable=True, checked=open_)
        self.btn.setToolButtonStyle(QtCore.Qt.ToolButtonTextBesideIcon)
        self.btn.setArrowType(QtCore.Qt.DownArrow if open_ else QtCore.Qt.RightArrow)
        self.btn.setStyleSheet("QToolButton{font-weight:bold;border:none;text-align:left;padding:3px;background:#3a3a3a;}")
        self.btn.setSizePolicy(QtWidgets.QSizePolicy.Expanding, QtWidgets.QSizePolicy.Fixed)
        self.body = QtWidgets.QWidget()
        self.body.setVisible(open_)
        self.grid = QtWidgets.QGridLayout(self.body)
        self.grid.setContentsMargins(6, 2, 6, 6)
        self.grid.setHorizontalSpacing(4)
        self.grid.setVerticalSpacing(3)
        lay.addWidget(self.btn)
        lay.addWidget(self.body)
        self.btn.toggled.connect(self._toggle)
        self._row = 0

    def _toggle(self, on):
        self.body.setVisible(on)
        self.btn.setArrowType(QtCore.Qt.DownArrow if on else QtCore.Qt.RightArrow)

    def add_row(self, *widgets):
        n = max(1, len(widgets))
        for i, w in enumerate(widgets):
            if isinstance(w, str):
                w = QtWidgets.QLabel(w)
            self.grid.addWidget(w, self._row, i, 1, 1)
        for i in range(n):
            self.grid.setColumnStretch(i, 1)
        self._row += 1

    def add_wide(self, w):
        self.grid.addWidget(w, self._row, 0, 1, 8)
        self._row += 1


def button(text, fn, tip=None):
    b = QtWidgets.QPushButton(text)
    b.clicked.connect(fn)
    if tip:
        b.setToolTip(tip)
    return b


class AxSlider(QtWidgets.QWidget):
    """드래그 중 begin/update/end 콜백을 부르는 -100..100 슬라이더. 놓으면 0으로 복귀."""

    def __init__(self, label, on_begin, on_update, on_end, on_cancel, menu=None, parent=None):
        super().__init__(parent)
        lay = QtWidgets.QHBoxLayout(self)
        lay.setContentsMargins(0, 0, 0, 0)
        self.lbl = QtWidgets.QLabel(label)
        self.lbl.setMinimumWidth(118)
        self.sl = QtWidgets.QSlider(QtCore.Qt.Horizontal)
        self.sl.setRange(-100, 100)
        self.sl.setValue(0)
        self.sl.setTickPosition(QtWidgets.QSlider.TicksBelow)
        self.sl.setTickInterval(50)
        self.val = QtWidgets.QLabel("0")
        self.val.setMinimumWidth(30)
        self.val.setAlignment(QtCore.Qt.AlignRight)
        lay.addWidget(self.lbl)
        lay.addWidget(self.sl, 1)
        lay.addWidget(self.val)
        self._cb = (on_begin, on_update, on_end, on_cancel)
        self._active = False
        self.sl.sliderPressed.connect(self._pressed)
        self.sl.valueChanged.connect(self._moved)
        self.sl.sliderReleased.connect(self._released)
        self.menu = menu
        if menu:
            self.setContextMenuPolicy(QtCore.Qt.CustomContextMenu)
            self.customContextMenuRequested.connect(lambda p: menu.exec(self.mapToGlobal(p)))

    def _pressed(self):
        try:
            self._active = bool(self._cb[0]())
        except Exception:
            self._active = False
            _report_exc()

    def _moved(self, v):
        self.val.setText(str(v))
        if not self._active:
            return
        try:
            self._cb[1](v / 100.0)
        except Exception:
            self._active = False
            _report_exc()
            self._cb[3]()

    def _released(self):
        if self._active:
            try:
                self._cb[2]()
            except Exception:
                _report_exc()
        self._active = False
        self.sl.blockSignals(True)
        self.sl.setValue(0)
        self.sl.blockSignals(False)
        self.val.setText("0")


def _report_exc():
    msg = traceback.format_exc()
    print("[AniMax] " + msg)
    if _DOCK[0] is not None:
        _DOCK[0].status(msg.strip().splitlines()[-1], error=True)


# ----------------------------------------------------------------- 메인 패널
class AniMaxPanel(QtWidgets.QWidget):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.session = None
        self.global_offset = None
        self.relationship = None
        outer = QtWidgets.QVBoxLayout(self)
        outer.setContentsMargins(4, 4, 4, 4)
        scroll = QtWidgets.QScrollArea()
        scroll.setWidgetResizable(True)
        scroll.setFrameShape(QtWidgets.QFrame.NoFrame)
        inner = QtWidgets.QWidget()
        self.vbox = QtWidgets.QVBoxLayout(inner)
        self.vbox.setContentsMargins(0, 0, 0, 0)
        self.vbox.setSpacing(4)
        scroll.setWidget(inner)
        outer.addWidget(scroll, 1)
        self.status_lbl = QtWidgets.QLabel("AniMax 준비됨")
        self.status_lbl.setWordWrap(True)
        self.status_lbl.setStyleSheet("color:#9c9;")
        outer.addWidget(self.status_lbl)

        self._build_scope()
        self._build_selection()
        self._build_sliders()
        self._build_keys()
        self._build_pose()
        self._build_spacify()
        self._build_arc()
        self._build_review()
        self.vbox.addStretch(1)

    # ---- 공통
    def status(self, text, error=False):
        self.status_lbl.setStyleSheet("color:%s;" % ("#f77" if error else "#9c9"))
        self.status_lbl.setText(text)

    def run(self, label, fn):
        try:
            r = fn()
            self.status("%s 완료%s" % (label, "" if r is None else " (%s)" % r))
            rt.redrawViews()
        except Exception:
            _report_exc()

    def tracks(self, expand_biped=False):
        nodes = core.selected_nodes()
        if expand_biped:
            nodes = core.expand_biped_selection(nodes)
        trs = core.tracks_for(nodes)
        ik = self.chk_ik.isChecked()
        for tr in trs:
            tr.pos_ik = ik
        if not trs:
            raise ValueError("선택된 오브젝트가 없다")
        return trs

    def range_(self):
        if self.rb_current.isChecked():
            return None
        return (self.sp_start.value(), self.sp_end.value())

    def range_or_anim(self):
        r = self.range_()
        return r if r else core.anim_range()

    def mode(self):
        return "current" if self.rb_current.isChecked() else "range"

    # ---- 섹션들
    def _build_scope(self):
        s = Section("대상 범위")
        self.rb_current = QtWidgets.QRadioButton("현재 프레임")
        self.rb_range = QtWidgets.QRadioButton("범위")
        self.rb_current.setChecked(True)
        self.sp_start = QtWidgets.QSpinBox(); self.sp_start.setRange(-100000, 100000)
        self.sp_end = QtWidgets.QSpinBox(); self.sp_end.setRange(-100000, 100000)
        a, b = core.anim_range()
        self.sp_start.setValue(a); self.sp_end.setValue(b)
        self.chk_ik = QtWidgets.QCheckBox("Biped 손·발 IK 위치도 쓰기")
        self.chk_ik.setToolTip("켜면 손·발의 월드 위치도 기록한다(IK 로 팔·다리가 풀림). 기본은 회전만.")
        s.add_row(self.rb_current, self.rb_range, self.sp_start, self.sp_end,
                  button("타임라인", self._range_from_timeline, "애니메이션 범위로 채움"))
        s.add_wide(self.chk_ik)
        self.vbox.addWidget(s)

    def _range_from_timeline(self):
        a, b = core.anim_range()
        self.sp_start.setValue(a); self.sp_end.setValue(b)
        self.rb_range.setChecked(True)

    def _build_selection(self):
        s = Section("선택 세트 · 미러")
        self.sets_box = QtWidgets.QWidget()
        self.sets_lay = QtWidgets.QGridLayout(self.sets_box)
        self.sets_lay.setContentsMargins(0, 0, 0, 0)
        s.add_wide(self.sets_box)
        self.ed_set = QtWidgets.QLineEdit(); self.ed_set.setPlaceholderText("세트 이름")
        self.btn_color = QtWidgets.QPushButton("색")
        self._set_color = "#4aa3ff"
        self.btn_color.clicked.connect(self._pick_color)
        s.add_row(self.ed_set, self.btn_color, button("+ 추가", self._add_set), button("- 삭제", self._remove_set))
        s.add_row(button("반대편 선택", lambda: self.run("반대편 선택", lambda: len(pose.select_opposite()))),
                  button("반대편 추가 선택", lambda: self.run("반대편 추가", lambda: len(pose.select_opposite(add=True)))),
                  button("미러 (포즈/범위)", self._mirror, "현재 프레임 또는 범위 내 키를 좌우 미러"))
        self.vbox.addWidget(s)
        self._refresh_sets()

    def _pick_color(self):
        c = QtWidgets.QColorDialog.getColor(QtGui.QColor(self._set_color), self, "세트 색상")
        if c.isValid():
            self._set_color = c.name()
            self.btn_color.setStyleSheet("background:%s;" % self._set_color)

    def _add_set(self):
        name = self.ed_set.text().strip()
        if not name:
            self.status("세트 이름을 입력하라", True); return
        self.run("세트 추가", lambda: review.add_set(name, color=self._set_color) and name)
        self._refresh_sets()

    def _remove_set(self):
        name = self.ed_set.text().strip()
        self.run("세트 삭제", lambda: review.remove_set(name) and name)
        self._refresh_sets()

    def _refresh_sets(self):
        while self.sets_lay.count():
            w = self.sets_lay.takeAt(0).widget()
            if w:
                w.deleteLater()
        sets = review.load_sets()
        for i, (name, entry) in enumerate(sorted(sets.items())):
            b = QtWidgets.QPushButton(name)
            b.setStyleSheet("background:%s;color:#111;font-weight:bold;" % entry.get("color", "#4aa3ff"))
            b.setToolTip(", ".join(entry.get("nodes", [])) + "\nCtrl+클릭: 추가 선택")
            b.clicked.connect(lambda _=False, n=name: self._select_set(n))
            self.sets_lay.addWidget(b, i // 3, i % 3)

    def _select_set(self, name):
        add = bool(QtWidgets.QApplication.keyboardModifiers() & QtCore.Qt.ControlModifier)
        self.run("세트 선택 " + name, lambda: len(review.select_set(name, add=add)))
        self.ed_set.setText(name)

    def _mirror(self):
        self.run("미러", lambda: pose.mirror(core.selected_nodes(), range_=self.range_()))

    def _build_sliders(self):
        s = Section("애니메이션 슬라이더")
        self.pivot_mode = "left"
        menu = QtWidgets.QMenu()
        grp = QtGui.QActionGroup(menu)
        for key, label in (("left", "피벗: 왼쪽 키"), ("right", "피벗: 오른쪽 키"), ("avg", "피벗: 평균")):
            act = menu.addAction(label); act.setCheckable(True); act.setChecked(key == "left")
            grp.addAction(act)
            act.triggered.connect(lambda _=False, k=key: setattr(self, "pivot_mode", k))
        self.sp_shift = QtWidgets.QSpinBox(); self.sp_shift.setRange(1, 200); self.sp_shift.setValue(10)
        self.chk_stagger = QtWidgets.QCheckBox("Stagger(선택 순서로 감쇠)")
        self.sp_amp_rot = QtWidgets.QDoubleSpinBox(); self.sp_amp_rot.setRange(0, 180); self.sp_amp_rot.setValue(10)
        self.sp_amp_pos = QtWidgets.QDoubleSpinBox(); self.sp_amp_pos.setRange(0, 1000); self.sp_amp_pos.setValue(5)
        for op in ("tween", "blend_neighbor", "blend_default", "ease", "scale_keys", "push_pull",
                   "connect_neighbor", "time_offset", "noise_wave"):
            s.add_wide(AxSlider(sliders.LABELS[op],
                                lambda o=op: self._slider_begin(o),
                                self._slider_update, self._slider_end, self._slider_cancel,
                                menu=menu if op == "scale_keys" else None))
        s.add_row("Time Offset 최대(f)", self.sp_shift, self.chk_stagger)
        s.add_row("Noise 회전(°)", self.sp_amp_rot, "Noise 위치", self.sp_amp_pos)
        s.add_wide(QtWidgets.QLabel("<small>Scale Keys 는 우클릭으로 피벗 전환. 슬라이더 놓으면 Ctrl+Z 한 번으로 되돌아간다.</small>"))
        self.vbox.addWidget(s)

    def _slider_begin(self, op):
        trs = self.tracks()
        ctx = {"pivot": self.pivot_mode, "max_shift": self.sp_shift.value(),
               "stagger_on": self.chk_stagger.isChecked(),
               "amp_rot": self.sp_amp_rot.value(), "amp_pos": self.sp_amp_pos.value()}
        mode = self.mode()
        if op in ("scale_keys", "push_pull", "time_offset", "noise_wave", "connect_neighbor") and mode == "current":
            # 이 슬라이더들은 키 여러 개가 있어야 의미가 있다 -> 범위 모드가 아니면 현재 키 하나만 대상
            pass
        self.session = sliders.SliderSession(op, trs, mode, self.range_(), ctx)
        self.session.begin()
        self.status("%s: %d개 노드" % (sliders.LABELS[op], len(trs)))
        return True

    def _slider_update(self, w):
        if self.session:
            self.session.update(w)

    def _slider_end(self):
        if self.session:
            self.session.end()
            self.status("%s 적용" % sliders.LABELS[self.session.op_name])
            self.session = None

    def _slider_cancel(self):
        if self.session:
            self.session.cancel()
            self.session = None

    def _build_keys(self):
        s = Section("키 유틸")
        self.sp_nudge = QtWidgets.QSpinBox(); self.sp_nudge.setRange(1, 100); self.sp_nudge.setValue(1)
        s.add_row(button("◀ Nudge", lambda: self._nudge(-1)), self.sp_nudge, button("Nudge ▶", lambda: self._nudge(1)),
                  button("Share Keys", lambda: self.run("Share Keys", lambda: len(keys.share_keys(self.tracks(), self.range_or_anim())))))
        s.add_row(button("Crop", lambda: self.run("Crop", lambda: keys.crop(self.tracks(), self.range_or_anim()))),
                  button("Delete Redundant", lambda: self.run("Delete Redundant", lambda: sum(len(v) for v in keys.delete_redundant(self.tracks()).values()))),
                  button("Smart Snap", lambda: self.run("Smart Snap", lambda: keys.smart_snap(self.tracks()))),
                  button("Smooth", lambda: self.run("Smooth", lambda: keys.smooth(self.tracks(), 1, 0.5, self.range_or_anim()))))
        self.cb_step = QtWidgets.QComboBox(); self.cb_step.addItems([str(i) for i in range(1, 8)])
        s.add_row(button("Keys Time 저장", lambda: self.run("Keys Time 저장", lambda: keys.save_keys_time(self.tracks()))),
                  button("Keys Time 복원", lambda: self.run("Keys Time 복원", lambda: keys.restore_keys_time(self.tracks()))),
                  "Bake 간격", self.cb_step,
                  button("Fast Bake", lambda: self.run("Fast Bake", lambda: keys.bake(self.tracks(), int(self.cb_step.currentText()), self.range_or_anim()))))
        s.add_row("탄젠트", button("Auto", lambda: self._tangent("auto")), button("Linear", lambda: self._tangent("linear")),
                  button("Step", lambda: self._tangent("step")))
        s.add_wide(QtWidgets.QLabel("<small>Biped 키는 TCB 라 Step 불가(Linear=continuity 0). 탄젠트는 범위 모드면 범위 내 키만.</small>"))
        self.vbox.addWidget(s)

    def _nudge(self, sign):
        d = sign * self.sp_nudge.value()
        r = self.range_()
        times = None if r is None else set(range(r[0], r[1] + 1))
        self.run("Nudge %+d" % d, lambda: keys.nudge(self.tracks(), d, times))

    def _tangent(self, kind):
        self.run("탄젠트 " + kind, lambda: keys.set_tangents(self.tracks(), kind, self.range_()))

    def _build_pose(self):
        s = Section("포즈 · Xform · 오프셋")
        s.add_row(button("Reset All", lambda: self._reset(("pos", "rot", "scale"))), button("Reset T", lambda: self._reset(("pos",))),
                  button("Reset R", lambda: self._reset(("rot",))), button("Reset S", lambda: self._reset(("scale",))),
                  button("기본 포즈 저장", lambda: self.run("기본 포즈 저장", lambda: pose.store_default(self.tracks(True))),
                         "Biped 는 이 저장 포즈로 Reset/Blend to Default 한다(전신 확장)"))
        s.add_row(button("Align → 마지막 선택", lambda: self.run("Align", lambda: pose.align(range_=self.range_()))),
                  button("Xform 복사", lambda: self.run("Xform 복사", lambda: pose.xform_copy(self.tracks(), self.range_()))),
                  button("Xform 붙여넣기", lambda: self.run("Xform 붙여넣기", lambda: pose.xform_paste(self.tracks()))),
                  button("붙여넣기 @현재", lambda: self.run("Xform 붙여넣기", lambda: pose.xform_paste(self.tracks(), at=core.current_frame()))))
        s.add_row(button("Global Offset 시작", self._go_begin, "현재 포즈 기억 → 뷰포트에서 파트를 옮긴 뒤 적용"),
                  button("Global Offset 적용", self._go_apply),
                  button("Relationship 설정", self._rel_set, "마지막 선택 = 드라이버, 나머지 = 팔로워"),
                  button("Relationship Bake", self._rel_bake))
        self.vbox.addWidget(s)

    def _reset(self, ch):
        def fn():
            done, skipped = pose.reset_pose(self.tracks(), ch)
            if skipped:
                self.status("기본 포즈 미저장 Biped 파트 건너뜀: %d개 (먼저 '기본 포즈 저장')" % len(skipped), True)
            return len(done)
        self.run("Reset", fn)

    def _go_begin(self):
        def fn():
            self.global_offset = pose.GlobalOffset(self.tracks())
            return len(self.global_offset.tracks)
        self.run("Global Offset 시작", fn)

    def _go_apply(self):
        if not self.global_offset:
            self.status("먼저 'Global Offset 시작'", True); return
        self.run("Global Offset 적용", lambda: self.global_offset.apply(self.range_or_anim()))
        self.global_offset = None

    def _rel_set(self):
        def fn():
            nodes = core.selected_nodes()
            if len(nodes) < 2:
                raise ValueError("드라이버(마지막 선택) + 팔로워가 필요하다")
            self.relationship = pose.Relationship(nodes[-1], nodes[:-1])
            return "%s ← %d" % (nodes[-1].name, len(nodes) - 1)
        self.run("Relationship 설정", fn)

    def _rel_bake(self):
        if not self.relationship:
            self.status("먼저 'Relationship 설정'", True); return
        self.run("Relationship Bake", lambda: self.relationship.bake(self.range_or_anim()))

    def _build_spacify(self):
        s = Section("Spacify (임시 컨트롤)")
        modes = [("World", "world"), ("New Pivot", "pivot"), ("Relative", "relative"), ("World Orient", "world_orient"),
                 ("Camera", "camera"), ("Group", "group"), ("Aim", "aim")]
        row = []
        for label, mode in modes:
            row.append(button(label, lambda _=False, m=mode: self._spacify(m)))
        s.add_row(*row[:4])
        s.add_row(*row[4:])
        self.chk_restore_keys = QtWidgets.QCheckBox("적용 후 원래 키 시간만 남기기")
        s.add_row(button("적용 (최근 세션)", self._spacify_apply), button("폐기 (최근 세션)", self._spacify_discard), self.chk_restore_keys)
        s.add_wide(QtWidgets.QLabel("<small>Relative/World Orient/Aim: 마지막 선택이 드라이버·타깃. Camera: 활성 카메라 뷰 필요. "
                                    "헬퍼(AX_*)를 애니메이션한 뒤 '적용'하면 매 프레임 원본 파트에 굽는다.</small>"))
        self.vbox.addWidget(s)

    def _spacify(self, mode):
        def fn():
            sess = spacify.create(mode, range_=self.range_or_anim())
            return "세션 #%d, 헬퍼 %d개" % (sess.id, len(sess.helpers))
        self.run("Spacify " + mode, fn)

    def _spacify_apply(self):
        sess = spacify.latest() or (spacify.recover_from_scene() and spacify.latest())
        if not sess:
            self.status("활성 Spacify 세션이 없다", True); return
        self.run("Spacify 적용", lambda: sess.apply(restore_keys=self.chk_restore_keys.isChecked()))

    def _spacify_discard(self):
        sess = spacify.latest() or (spacify.recover_from_scene() and spacify.latest())
        if not sess:
            self.status("활성 Spacify 세션이 없다", True); return
        self.run("Spacify 폐기", lambda: sess.discard())

    def _build_arc(self):
        s = Section("Arc Tracker")
        self.sp_before = QtWidgets.QSpinBox(); self.sp_before.setRange(0, 500); self.sp_before.setValue(12)
        self.sp_after = QtWidgets.QSpinBox(); self.sp_after.setRange(0, 500); self.sp_after.setValue(12)
        self.chk_cam = QtWidgets.QCheckBox("카메라 스페이스")
        s.add_row("이전", self.sp_before, "이후", self.sp_after, self.chk_cam)
        s.add_row(button("아크 켜기", self._arc_on), button("아크 끄기", lambda: self.run("아크 끄기", arc.stop)),
                  button("갱신", lambda: self.run("아크 갱신", arc.refresh)))
        self.vbox.addWidget(s)

    def _arc_on(self):
        def fn():
            cam = rt.getActiveCamera() if self.chk_cam.isChecked() else None
            if self.chk_cam.isChecked() and cam is None:
                raise ValueError("카메라 스페이스는 활성 카메라 뷰가 필요하다")
            return len(arc.start(before=self.sp_before.value(), after=self.sp_after.value(), camera=cam))
        self.run("아크 켜기", fn)

    def _build_review(self):
        s = Section("리뷰 · 전송", open_=False)
        self.cam_list = QtWidgets.QListWidget()
        self.cam_list.setMaximumHeight(80)
        self._refresh_cams()
        s.add_wide(self.cam_list)
        self.sp_pct = QtWidgets.QSpinBox(); self.sp_pct.setRange(10, 100); self.sp_pct.setValue(50)
        s.add_row(button("카메라 새로고침", self._refresh_cams), "크기%", self.sp_pct,
                  button("멀티캠 플레이블라스트", self._playblast), button("폴더 열기", self._open_pb_dir))
        s.add_row(button("Quick Export", lambda: self.run("Quick Export", lambda: review.quick_export())),
                  button("Quick Import", lambda: self.run("Quick Import", lambda: len(review.quick_import()))),
                  button("애니 JSON 내보내기", self._anim_export), button("애니 JSON 가져오기", self._anim_import))
        s.add_row(button("포즈 JSON 내보내기", self._pose_export), button("포즈 JSON 가져오기 @현재", self._pose_import),
                  button("Biped .bip 저장", self._bip_save), button("Biped .bip 불러오기", self._bip_load))
        self.vbox.addWidget(s)

    def _refresh_cams(self):
        self.cam_list.clear()
        for c in rt.cameras:
            if rt.superClassOf(c) != rt.camera:
                continue
            it = QtWidgets.QListWidgetItem(c.name)
            it.setFlags(it.flags() | QtCore.Qt.ItemIsUserCheckable)
            it.setCheckState(QtCore.Qt.Checked)
            self.cam_list.addItem(it)

    def _checked_cams(self):
        names = [self.cam_list.item(i).text() for i in range(self.cam_list.count())
                 if self.cam_list.item(i).checkState() == QtCore.Qt.Checked]
        return [rt.getNodeByName(n) for n in names]

    def _playblast(self):
        def fn():
            res = review.playblast(self._checked_cams(), range_=self.range_or_anim(), percent=self.sp_pct.value())
            return "; ".join(os.path.basename(p) for _, p in res)
        self.run("플레이블라스트", fn)

    def _open_pb_dir(self):
        d = os.path.join(str(rt.getDir(rt.Name("preview"))), "animax")
        os.makedirs(d, exist_ok=True)
        os.startfile(d)

    def _file(self, save, filt, default):
        d = str(rt.getDir(rt.Name("export")))
        if save:
            p, _ = QtWidgets.QFileDialog.getSaveFileName(self, "저장", os.path.join(d, default), filt)
        else:
            p, _ = QtWidgets.QFileDialog.getOpenFileName(self, "열기", d, filt)
        return p

    def _anim_export(self):
        p = self._file(True, "JSON (*.json)", "animax_anim.json")
        if p:
            self.run("애니 내보내기", lambda: pose.export_animation(self.tracks(True), p, self.range_or_anim()))

    def _anim_import(self):
        p = self._file(False, "JSON (*.json)", "")
        if p:
            self.run("애니 가져오기", lambda: pose.import_animation(p))

    def _pose_export(self):
        p = self._file(True, "JSON (*.json)", "animax_pose.json")
        if p:
            self.run("포즈 내보내기", lambda: pose.export_pose(self.tracks(True), p))

    def _pose_import(self):
        p = self._file(False, "JSON (*.json)", "")
        if p:
            self.run("포즈 가져오기", lambda: pose.import_pose(p))

    def _biped_root_sel(self):
        for n in core.selected_nodes():
            r = core.biped_root(n)
            if r is not None:
                return r
        raise ValueError("Biped 파트를 선택하라")

    def _bip_save(self):
        p = self._file(True, "Biped (*.bip)", "animax.bip")
        if p:
            self.run(".bip 저장", lambda: pose.biped_save(self._biped_root_sel(), p, self.range_()))

    def _bip_load(self):
        p = self._file(False, "Biped (*.bip)", "")
        if p:
            self.run(".bip 불러오기", lambda: pose.biped_load(self._biped_root_sel(), p))


# ----------------------------------------------------------------- 도크
class AniMaxDock(QtWidgets.QDockWidget):
    def __init__(self, parent=None):
        super().__init__("AniMax", parent)
        self.setObjectName("AniMaxDock")
        self.panel = AniMaxPanel(self)
        self.setWidget(self.panel)
        self.setAllowedAreas(QtCore.Qt.LeftDockWidgetArea | QtCore.Qt.RightDockWidgetArea)
        self.setMinimumWidth(360)

    def status(self, text, error=False):
        self.panel.status(text, error)


def show():
    main = _main_window()
    if _DOCK[0] is not None:
        try:
            _DOCK[0].close()
            _DOCK[0].deleteLater()
        except Exception:
            pass
    dock = AniMaxDock(main)
    if main is not None:
        main.addDockWidget(QtCore.Qt.RightDockWidgetArea, dock)
        dock.setFloating(False)
    dock.show()
    _DOCK[0] = dock
    return dock


def toggle():
    if _DOCK[0] is not None and _DOCK[0].isVisible():
        _DOCK[0].hide()
        return None
    if _DOCK[0] is not None:
        _DOCK[0].show()
        return _DOCK[0]
    return show()
