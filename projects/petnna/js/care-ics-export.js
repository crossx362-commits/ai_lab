// care-ics-export.js — 예방 케어 일정 → 캘린더(.ics) 내보내기 (백로그 나무 제안, P3)
// 돌봄 스케줄러(careSchedules)의 예정 항목을 클라이언트에서 RFC 5545 iCalendar(.ics)로 생성해
// 아이폰/구글 등 외부 캘린더 앱에 등록할 수 있게 파일로 내려받는다. 신규 라이브러리 없이 순수 JS.
(function () {
    'use strict';

    const DOW = ['SU', 'MO', 'TU', 'WE', 'TH', 'FR', 'SA'];

    // iCalendar 텍스트 이스케이프 (RFC 5545 §3.3.11): \ ; , 개행
    function _icsText(s) {
        return String(s == null ? '' : s)
            .replace(/\\/g, '\\\\')
            .replace(/;/g, '\\;')
            .replace(/,/g, '\\,')
            .replace(/\r?\n/g, '\\n');
    }

    function _pad(n) { return String(n).padStart(2, '0'); }

    // 로컬 플로팅 시각 YYYYMMDDTHHMMSS (TZID 없이 — 대부분의 캘린더가 기기 로컬로 해석)
    function _fmtLocal(d) {
        return d.getFullYear() + _pad(d.getMonth() + 1) + _pad(d.getDate()) +
            'T' + _pad(d.getHours()) + _pad(d.getMinutes()) + '00';
    }

    // UTC 타임스탬프 YYYYMMDDTHHMMSSZ (DTSTAMP용)
    function _fmtUtc(d) {
        return d.getUTCFullYear() + _pad(d.getUTCMonth() + 1) + _pad(d.getUTCDate()) +
            'T' + _pad(d.getUTCHours()) + _pad(d.getUTCMinutes()) + _pad(d.getUTCSeconds()) + 'Z';
    }

    // "HH:MM" → {h, m}
    function _parseTime(t) {
        const m = /^(\d{1,2}):(\d{2})/.exec(String(t || '09:00'));
        return { h: m ? +m[1] : 9, m: m ? +m[2] : 0 };
    }

    // 반복 일정의 첫 시작일(오늘 이후 가장 가까운 유효일)을 계산
    function _firstStart(schedule) {
        const { h, m } = _parseTime(schedule.time);
        const now = new Date();
        const base = new Date(now.getFullYear(), now.getMonth(), now.getDate(), h, m, 0);
        if (schedule.repeat === 'weekly') {
            const days = (schedule.repeatDays && schedule.repeatDays.length) ? schedule.repeatDays : [0, 1, 2, 3, 4, 5, 6];
            for (let i = 0; i < 7; i++) {
                const cand = new Date(base); cand.setDate(base.getDate() + i);
                if (days.includes(cand.getDay())) return cand;
            }
            return base;
        }
        if (schedule.repeat === 'monthly') {
            const ref = new Date(schedule.date || schedule.createdAt || now);
            const day = ref.getDate();
            const cand = new Date(now.getFullYear(), now.getMonth(), day, h, m, 0);
            if (cand < base) cand.setMonth(cand.getMonth() + 1);
            return cand;
        }
        return base; // daily
    }

    // 스케줄 하나 → VEVENT 문자열(배열 라인). 완료된 일회성은 제외.
    function _toEvent(schedule, dtstamp) {
        const { h, m } = _parseTime(schedule.time);
        let dtstart, rrule = null;

        if (schedule.repeat === 'once') {
            if (schedule.completed) return null;
            const p = String(schedule.date || '').split('-');
            if (p.length !== 3) return null;
            dtstart = new Date(+p[0], +p[1] - 1, +p[2], h, m, 0);
        } else if (schedule.repeat === 'weekly') {
            dtstart = _firstStart(schedule);
            const days = (schedule.repeatDays && schedule.repeatDays.length) ? schedule.repeatDays : [0, 1, 2, 3, 4, 5, 6];
            rrule = 'FREQ=WEEKLY;BYDAY=' + days.map(d => DOW[d]).join(',');
        } else if (schedule.repeat === 'monthly') {
            dtstart = _firstStart(schedule);
            rrule = 'FREQ=MONTHLY;BYMONTHDAY=' + dtstart.getDate();
        } else { // daily
            dtstart = _firstStart(schedule);
            rrule = 'FREQ=DAILY';
        }

        const typeName = (typeof getCareTypeName === 'function') ? getCareTypeName(schedule.type) : '케어';
        const summary = _icsText(`🐾 ${schedule.title}`);
        const lines = [
            'BEGIN:VEVENT',
            'UID:petnna-care-' + schedule.id + '@petnna',
            'DTSTAMP:' + dtstamp,
            'DTSTART:' + _fmtLocal(dtstart),
            'DURATION:PT30M',
            'SUMMARY:' + summary,
        ];
        if (rrule) lines.push('RRULE:' + rrule);
        const desc = _icsText([typeName, schedule.notes].filter(Boolean).join(' · ') + ' (펫과나)');
        lines.push('DESCRIPTION:' + desc);
        lines.push('BEGIN:VALARM', 'TRIGGER:-PT30M', 'ACTION:DISPLAY', 'DESCRIPTION:' + summary, 'END:VALARM');
        lines.push('END:VEVENT');
        return lines.join('\r\n');
    }

    // 활성 펫의 돌봄 일정을 .ics로 내려받기
    function exportCareScheduleIcs() {
        const careSchedules = (typeof AppStore !== 'undefined' && AppStore.getState('careSchedules')) ||
            { schedules: [], completionHistory: [] };
        const pet = (typeof getActivePet === 'function') ? getActivePet() : null;
        const petId = pet ? pet.id : null;
        const mine = careSchedules.schedules.filter(s => petId == null || s.petId == null || s.petId === petId);

        const dtstamp = _fmtUtc(new Date());
        const events = mine.map(s => _toEvent(s, dtstamp)).filter(Boolean);

        if (!events.length) {
            if (typeof showToast === 'function') showToast('내보낼 예정 일정이 없어요 📅');
            return;
        }

        const cal = [
            'BEGIN:VCALENDAR',
            'VERSION:2.0',
            'PRODID:-//petnna//care-schedule//KO',
            'CALSCALE:GREGORIAN',
            'METHOD:PUBLISH',
            'X-WR-CALNAME:' + _icsText((pet && pet.name ? pet.name + ' ' : '') + '예방 케어 일정'),
            ...events,
            'END:VCALENDAR',
        ].join('\r\n');

        try {
            const blob = new Blob([cal], { type: 'text/calendar;charset=utf-8' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = 'petnna-care' + (pet && pet.name ? '-' + String(pet.name).replace(/\s+/g, '') : '') + '.ics';
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            setTimeout(() => URL.revokeObjectURL(url), 1000);
            if (typeof showToast === 'function') showToast(`${events.length}개 일정을 캘린더 파일로 내보냈어요 📅`);
        } catch (e) {
            if (typeof showToast === 'function') showToast('캘린더 내보내기에 실패했어요 😢');
        }
    }

    window.exportCareScheduleIcs = exportCareScheduleIcs;
})();
